using System;
using System.Collections.Generic;
using System.Linq;
using ShotV.Core;
using ShotV.Data;
using ShotV.World;

namespace ShotV.State;

public enum ScenePanelMode
{
    None,
    Overview,
    Locker,
    Workshop,
    Command,
    Launch,
    CombatInventory,
}

public class SceneRuntimeState
{
    public bool PrimaryActionReady { get; set; }
    public string PrimaryActionHint { get; set; } = "";
    public string? NearbyMarkerId { get; set; }
    public string? NearbyMarkerLabel { get; set; }
    public MarkerKind? NearbyMarkerKind { get; set; }
    public bool MapOverlayOpen { get; set; }
    public bool PanelOpen { get; set; }
    public ScenePanelMode PanelMode { get; set; }
    public int NearbyLootCount { get; set; }
}

public class GameState
{
    public GameMode Mode { get; set; } = GameMode.Base;
    public SaveState Save { get; set; } = SaveState.CreateInitial();
    public bool Hydrated { get; set; } = true;
    public SceneRuntimeState Runtime { get; set; } = new();
}

public class DeploymentReadinessResult
{
    public bool CanDeploy { get; set; }
    public bool HasWarnings { get; set; }
    public string StatusLabel { get; set; } = "";
    public string Detail { get; set; } = "";
    public int HighestThreat { get; set; }
    public int StagedUnits { get; set; }
    public int HealingUnits { get; set; }
    public int MobilityUnits { get; set; }
    public int UtilityUnits { get; set; }
    public int OccupiedCells { get; set; }
    public int CapacityCells { get; set; }
}

public class GameStore
{
    public event Action<GameState, GameState>? StateChanged;

    private GameState _state;

    public GameStore()
    {
        _state = new GameState();
    }

    public GameState State => _state;

    public void Initialize(SaveState save)
    {
        save.Inventory ??= new InventoryState();
        save.Inventory.DeploymentPack ??= new GridInventoryState();
        save.Inventory.DeploymentPack.Items = save.Inventory.DeploymentPack.Items
            .Where(IsAllowedDeploymentPackItem)
            .Select(item => item.Clone())
            .ToList();
        save.Inventory.DeploymentPack.QuickSlots = new string?[GridInventoryState.RunQuickSlotCount];

        _state = new GameState
        {
            Mode = save.Session.ActiveRun != null ? GameMode.Combat : GameMode.Base,
            Save = save,
            Hydrated = true,
            Runtime = new SceneRuntimeState(),
        };
    }

    public void SelectNextWorldRoute()
    {
        SelectNextWorldMap();
    }

    public void SelectWorldRoute(string routeId)
    {
        SelectWorldMap(routeId);
    }

    public void SelectNextWorldMap()
    {
        SelectWorldMap(RouteData.GetNextMapId(_state.Save.World.SelectedRouteId));
    }

    public void SelectWorldMap(string mapId)
    {
        if (_state.Save.Session.ActiveRun != null) return;
        var map = RouteData.GetMap(mapId);
        var nextMap = RouteManager.CreateRunMapStateForRoute(map.Id);
        var save = _state.Save.Clone();
        save.UpdatedAt = Now();
        save.World.SelectedRouteId = nextMap.RouteId;
        save.World.SelectedZoneId = nextMap.CurrentZoneId;
        AddDiscoveredZone(save.World, nextMap.CurrentZoneId);
        Commit(_state.Mode, save);
    }

    public DeploymentReadinessResult EvaluateDeploymentReadiness()
    {
        return BuildDeploymentReadiness(_state.Save);
    }

    public void DeployCombat()
    {
        if (_state.Save.Session.ActiveRun != null || !_state.Runtime.PrimaryActionReady) return;
        var readiness = BuildDeploymentReadiness(_state.Save);
        if (!readiness.CanDeploy) return;

        var timestamp = Now();
        var runId = BuildRunId();
        var loadout = BuildRunLoadout(_state.Save);
        var mapState = RouteManager.CreateRunMapStateForRoute(_state.Save.World.SelectedRouteId);
        var nextRun = RunState.CreateInitial(runId, timestamp, loadout, mapState);
        var save = _state.Save.Clone();
        nextRun.Inventory = save.Inventory.DeploymentPack.Clone();
        nextRun.Inventory.Items = nextRun.Inventory.Items
            .Where(IsAllowedDeploymentPackItem)
            .Select(item => item.Clone())
            .ToList();
        nextRun.Inventory.Items = ShotV.Inventory.GridInventory.StoreItemsInGrid(
            nextRun.Inventory.Columns,
            nextRun.Inventory.Rows,
            nextRun.Inventory.Items,
            BuildInitialReserveAmmoItems(loadout)).Items;
        nextRun.Inventory.QuickSlots = new string?[GridInventoryState.RunQuickSlotCount];
        nextRun.Resources = ShotV.Inventory.GridInventory.BuildResourceLedgerFromItems(nextRun.Inventory.Items);
        save.UpdatedAt = timestamp;
        save.Base.DeploymentCount++;
        save.Inventory.DeploymentPack = new GridInventoryState
        {
            Columns = nextRun.Inventory.Columns,
            Rows = nextRun.Inventory.Rows,
        };
        save.Session.ActiveRun = nextRun;
        save.World.ActiveRouteId = mapState.RouteId;
        MergeWorldWithRunMap(save.World, mapState);
        Commit(GameMode.Combat, save);
    }

    public void SyncActiveRun(RunState snapshot)
    {
        var save = _state.Save.Clone();
        if (save.Session.ActiveRun == null) return;
        save.UpdatedAt = Now();
        save.Session.ActiveRun = snapshot.Clone();
        MergeWorldWithRunMap(save.World, snapshot.Map);
        Commit(GameMode.Combat, save);
    }

    public void MarkCurrentZoneCleared()
    {
        var save = _state.Save.Clone();
        var run = save.Session.ActiveRun;
        if (run == null || run.Status != RunStateStatus.Active) return;
        RouteManager.MarkCurrentZoneCleared(run.Map);
        bool routeComplete = RouteManager.IsRunRouteComplete(run.Map);
        run.Map.HostilesRemaining = 0;
        run.Map.Boss.Defeated = true;
        run.Map.Boss.Health = 0;
        run.Stats.HighestWave = Math.Max(run.Stats.HighestWave, Math.Max(run.Map.HighestWave, run.Map.CurrentWave));
        if (routeComplete) run.Stats.BossDefeated = true;
        save.UpdatedAt = Now();
        MergeWorldWithRunMap(save.World, run.Map);
        Commit(GameMode.Combat, save);
    }

    public void AdvanceActiveRunZone(bool force = false)
    {
        var save = _state.Save.Clone();
        var run = save.Session.ActiveRun;
        if (run == null || run.Status != RunStateStatus.Active) return;
        if (!force && !_state.Runtime.PrimaryActionReady) return;
        var nextMap = RouteManager.AdvanceRunMapZone(run.Map);
        if (nextMap == null) return;
        run.Map = nextMap;
        run.PendingOutcome = null;
        save.UpdatedAt = Now();
        MergeWorldWithRunMap(save.World, nextMap);
        Commit(GameMode.Combat, save);
    }

    public void MarkRunOutcome(RunResolutionOutcome outcome)
    {
        var save = _state.Save.Clone();
        var run = save.Session.ActiveRun;
        if (run == null) return;
        FinalizeRunForOutcome(run, outcome);
        save.UpdatedAt = Now();
        MergeWorldWithRunMap(save.World, run.Map);
        Commit(GameMode.Combat, save);
    }

    public void ResolveActiveRunToBase(RunResolutionOutcome outcome = RunResolutionOutcome.Extracted, bool force = false)
    {
        var save = _state.Save.Clone();
        var run = save.Session.ActiveRun;
        if (run == null) return;

        if (run.Status == RunStateStatus.Active)
        {
            if (!RouteManager.CanExtractFromRunMap(run.Map)) return;
            if (!force && !_state.Runtime.PrimaryActionReady) return;
        }

        var timestamp = Now();
        var resolvedOutcome = run.Status == RunStateStatus.AwaitingSettlement
            ? (run.PendingOutcome ?? outcome)
            : RouteManager.IsRunRouteComplete(run.Map) ? RunResolutionOutcome.BossClear : outcome;

        FinalizeRunForOutcome(run, resolvedOutcome);
        var settlement = SettleRunInventoryInBase(save.Inventory, run, resolvedOutcome);

        save.UpdatedAt = timestamp;
        save.Base.Resources.Salvage += settlement.ResourcesRecovered.Salvage;
        save.Base.Resources.Alloy += settlement.ResourcesRecovered.Alloy;
        save.Base.Resources.Research += settlement.ResourcesRecovered.Research;
        save.Inventory = settlement.Inventory;
        save.Session.ActiveRun = null;
        save.Session.LastExtraction = BuildExtractionResult(run, resolvedOutcome, timestamp, settlement);
        MergeWorldWithRunMap(save.World, run.Map);
        save.World.ActiveRouteId = null;
        Commit(GameMode.Base, save);
    }

    public ExtractionResult? PreviewActiveRunSettlement(RunResolutionOutcome fallbackOutcome = RunResolutionOutcome.Extracted)
    {
        var run = _state.Save.Session.ActiveRun;
        if (run == null) return null;

        var previewRun = run.Clone();
        var resolvedOutcome = previewRun.Status == RunStateStatus.AwaitingSettlement
            ? (previewRun.PendingOutcome ?? fallbackOutcome)
            : RouteManager.IsRunRouteComplete(previewRun.Map) ? RunResolutionOutcome.BossClear : fallbackOutcome;

        FinalizeRunForOutcome(previewRun, resolvedOutcome);
        var settlement = SettleRunInventoryInBase(_state.Save.Inventory, previewRun, resolvedOutcome);
        return BuildExtractionResult(previewRun, resolvedOutcome, Now(), settlement);
    }

    public void ResetSave()
    {
        var save = SaveState.CreateInitial();
        _state = new GameState
        {
            Mode = GameMode.Base,
            Save = save,
            Hydrated = true,
            Runtime = new SceneRuntimeState(),
        };
        StateChanged?.Invoke(_state, _state);
    }

    public void SetMapOverlayOpen(bool open)
    {
        UpdateSceneRuntime(rt =>
        {
            rt.MapOverlayOpen = open;
            if (open)
            {
                rt.PanelOpen = false;
                rt.PanelMode = ScenePanelMode.None;
            }
        });
    }

    public void ToggleMapOverlay()
    {
        bool nextOpen = !_state.Runtime.MapOverlayOpen;
        SetMapOverlayOpen(nextOpen);
    }

    public void OpenScenePanel(ScenePanelMode mode)
    {
        UpdateSceneRuntime(rt =>
        {
            rt.PanelOpen = true;
            rt.PanelMode = mode;
            rt.MapOverlayOpen = false;
        });
    }

    public void CloseScenePanel()
    {
        UpdateSceneRuntime(rt =>
        {
            rt.PanelOpen = false;
            rt.PanelMode = ScenePanelMode.None;
        });
    }

    public void ToggleScenePanel(ScenePanelMode defaultMode)
    {
        UpdateSceneRuntime(rt =>
        {
            if (!rt.PanelOpen)
            {
                rt.PanelOpen = true;
                rt.PanelMode = defaultMode;
                rt.MapOverlayOpen = false;
                return;
            }

            if (rt.PanelMode != defaultMode)
            {
                rt.PanelMode = defaultMode;
                rt.MapOverlayOpen = false;
                return;
            }

            rt.PanelOpen = false;
            rt.PanelMode = ScenePanelMode.None;
        });
    }

    public void AutoArrangeBaseStash()
    {
        if (_state.Save.Session.ActiveRun != null)
            return;

        var save = _state.Save.Clone();
        save.Inventory.StoredItems = ShotV.Inventory.GridInventory.AutoArrange(
            save.Inventory.StashColumns,
            save.Inventory.StashRows,
            save.Inventory.StoredItems);
        save.UpdatedAt = Now();
        Commit(_state.Mode, save);
    }

    public void AutoArrangeActiveRunInventory()
    {
        var save = _state.Save.Clone();
        var run = save.Session.ActiveRun;
        if (run == null)
            return;

        run.Inventory.Items = ShotV.Inventory.GridInventory.AutoArrange(
            run.Inventory.Columns,
            run.Inventory.Rows,
            run.Inventory.Items);
        save.UpdatedAt = Now();
        Commit(GameMode.Combat, save);
    }

    public void MoveEquippedWeapon(int sourceIndex, int targetIndex)
    {
        if (_state.Save.Session.ActiveRun != null)
            return;

        var save = _state.Save.Clone();
        var equipped = save.Inventory.EquippedWeaponIds;
        if (sourceIndex < 0 || sourceIndex >= equipped.Count || targetIndex < 0 || targetIndex >= equipped.Count)
            return;
        if (sourceIndex == targetIndex)
            return;

        var weapon = equipped[sourceIndex];
        equipped.RemoveAt(sourceIndex);
        equipped.Insert(targetIndex, weapon);
        save.UpdatedAt = Now();
        Commit(_state.Mode, save);
    }

    public void CraftWorkshopItem(string itemId)
    {
        if (_state.Save.Session.ActiveRun != null)
            return;
        if (!ItemData.ById.TryGetValue(itemId, out var definition) || definition.CraftCost == null)
            return;

        var save = _state.Save.Clone();
        if (!CanAfford(save.Base.Resources, definition.CraftCost))
            return;

        var craftedItem = ShotV.Inventory.GridInventory.CreateItemRecord(itemId, 1);
        if (craftedItem == null)
            return;

        var placement = ShotV.Inventory.GridInventory.PlaceItemInGrid(
            save.Inventory.StashColumns,
            save.Inventory.StashRows,
            save.Inventory.StoredItems,
            craftedItem);
        if (!placement.Placed)
            return;

        SpendResources(save.Base.Resources, definition.CraftCost);
        save.Inventory.StoredItems = placement.Items;
        save.UpdatedAt = Now();
        Commit(GameMode.Base, save);
    }

    public void UpdateBaseStashItems(List<InventoryItemRecord> items)
    {
        if (_state.Save.Session.ActiveRun != null)
            return;

        var save = _state.Save.Clone();
        save.Inventory.StoredItems = ShotV.Inventory.GridInventory.CloneItems(items);
        save.UpdatedAt = Now();
        Commit(GameMode.Base, save);
    }

    public void UpdateDeploymentInventoryState(List<InventoryItemRecord> stashItems, List<InventoryItemRecord> deploymentPackItems)
    {
        if (_state.Save.Session.ActiveRun != null)
            return;

        var save = _state.Save.Clone();
        save.Inventory.StoredItems = ShotV.Inventory.GridInventory.CloneItems(stashItems);
        save.Inventory.DeploymentPack.Items = deploymentPackItems
            .Where(IsAllowedDeploymentPackItem)
            .Select(item => item.Clone())
            .ToList();
        save.Inventory.DeploymentPack.QuickSlots = new string?[GridInventoryState.RunQuickSlotCount];
        save.UpdatedAt = Now();
        Commit(GameMode.Base, save);
    }

    public void UpdateActiveRunInventoryState(List<InventoryItemRecord> items, List<GroundLootDrop>? groundLoot = null, string?[]? quickSlots = null)
    {
        var save = _state.Save.Clone();
        var run = save.Session.ActiveRun;
        if (run == null)
            return;

        run.Inventory.Items = ShotV.Inventory.GridInventory.CloneItems(items);
        if (groundLoot != null)
            run.GroundLoot = groundLoot.Select(drop => drop.Clone()).ToList();

        run.Inventory.QuickSlots = ShotV.Inventory.GridInventory.SanitizeQuickSlots(
            quickSlots ?? run.Inventory.QuickSlots,
            run.Inventory.Items.Select(item => item.Id));
        run.Resources = ShotV.Inventory.GridInventory.BuildResourceLedgerFromItems(run.Inventory.Items);

        save.UpdatedAt = Now();
        Commit(GameMode.Combat, save);
    }

    public void BindActiveRunQuickSlot(int slotIndex, string? itemId)
    {
        var save = _state.Save.Clone();
        var run = save.Session.ActiveRun;
        if (run == null)
            return;

        var nextQuickSlots = ShotV.Inventory.GridInventory.AssignQuickSlotBinding(run.Inventory.QuickSlots, slotIndex, itemId);
        run.Inventory.QuickSlots = ShotV.Inventory.GridInventory.SanitizeQuickSlots(nextQuickSlots, run.Inventory.Items.Select(item => item.Id));
        save.UpdatedAt = Now();
        Commit(GameMode.Combat, save);
    }

    public void UpdateSceneRuntime(Action<SceneRuntimeState> patch)
    {
        var prev = _state;
        var rt = new SceneRuntimeState
        {
            PrimaryActionReady = prev.Runtime.PrimaryActionReady,
            PrimaryActionHint = prev.Runtime.PrimaryActionHint,
            NearbyMarkerId = prev.Runtime.NearbyMarkerId,
            NearbyMarkerLabel = prev.Runtime.NearbyMarkerLabel,
            NearbyMarkerKind = prev.Runtime.NearbyMarkerKind,
            MapOverlayOpen = prev.Runtime.MapOverlayOpen,
            PanelOpen = prev.Runtime.PanelOpen,
            PanelMode = prev.Runtime.PanelMode,
            NearbyLootCount = prev.Runtime.NearbyLootCount,
        };
        patch(rt);
        _state = new GameState
        {
            Mode = prev.Mode,
            Save = prev.Save,
            Hydrated = prev.Hydrated,
            Runtime = rt,
        };
        StateChanged?.Invoke(_state, prev);
    }

    public void ClearSceneRuntime()
    {
        var prev = _state;
        _state = new GameState
        {
            Mode = prev.Mode,
            Save = prev.Save,
            Hydrated = prev.Hydrated,
            Runtime = new SceneRuntimeState(),
        };
        StateChanged?.Invoke(_state, prev);
    }

    private void Commit(GameMode mode, SaveState save)
    {
        var prev = _state;
        _state = new GameState
        {
            Mode = mode,
            Save = save,
            Hydrated = true,
            Runtime = mode == prev.Mode ? prev.Runtime : new SceneRuntimeState(),
        };
        StateChanged?.Invoke(_state, prev);
    }

    private static void FinalizeRunForOutcome(RunState run, RunResolutionOutcome outcome)
    {
        int hw = Math.Max(run.Stats.HighestWave, Math.Max(run.Map.HighestWave, run.Map.CurrentWave));
        run.Status = RunStateStatus.AwaitingSettlement;
        run.PendingOutcome = outcome;
        run.Player.Health = outcome == RunResolutionOutcome.Down ? 0 : run.Player.Health;
        run.Map.HighestWave = hw;
        run.Map.HostilesRemaining = outcome == RunResolutionOutcome.Down ? run.Map.HostilesRemaining : 0;
        if (outcome == RunResolutionOutcome.BossClear)
        {
            run.Map.Boss.Defeated = true;
            run.Map.Boss.Health = 0;
        }
        run.Stats.HighestWave = hw;
        run.Stats.Extracted = outcome == RunResolutionOutcome.Extracted;
        run.Stats.BossDefeated = outcome == RunResolutionOutcome.BossClear || run.Stats.BossDefeated;
    }

    private static void MergeWorldWithRunMap(WorldState world, RunMapState map)
    {
        world.SelectedRouteId = map.RouteId;
        world.SelectedZoneId = map.CurrentZoneId;
        foreach (var zone in map.Zones.Where(z => z.Status != RunZoneStatus.Locked))
            AddDiscoveredZone(world, zone.Id);
        world.ActiveRouteId ??= map.RouteId;
    }

    private static void AddDiscoveredZone(WorldState world, string zoneId)
    {
        if (!world.DiscoveredZones.Contains(zoneId))
            world.DiscoveredZones.Add(zoneId);
    }

    private struct RunInventorySettlement
    {
        public InventoryState Inventory;
        public ResourceBundle ResourcesRecovered;
        public ResourceBundle ResourcesLost;
        public List<LootEntry> LootRecovered;
        public List<LootEntry> LootLost;
    }

    private static RunInventorySettlement SettleRunInventoryInBase(InventoryState inventory, RunState run, RunResolutionOutcome outcome)
    {
        var carriedIds = new HashSet<string>(run.Inventory.Items.Select(i => i.Id));

        if (outcome == RunResolutionOutcome.Down)
        {
            return new RunInventorySettlement
            {
                Inventory = inventory.Clone(),
                ResourcesRecovered = ResourceBundle.Zero(),
                ResourcesLost = ShotV.Inventory.GridInventory.BuildResourceLedgerFromItems(run.Inventory.Items),
                LootRecovered = new List<LootEntry>(),
                LootLost = run.LootEntries.Where(e => carriedIds.Contains(e.Id)).Select(e => e.Clone()).ToList(),
            };
        }

        var stashCandidates = run.Inventory.Items
            .Where(IsPersistedBaseInventoryItem)
            .ToList();
        var convertedItems = run.Inventory.Items
            .Where(item => !IsPersistedBaseInventoryItem(item))
            .ToList();

        var placement = ShotV.Inventory.GridInventory.PlaceItemsInGrid(
            inventory.StashColumns, inventory.StashRows,
            inventory.StoredItems, stashCandidates);

        var recoveredIds = new HashSet<string>(placement.PlacedIds);
        foreach (var item in convertedItems)
            recoveredIds.Add(item.Id);

        var recoveredItems = run.Inventory.Items.Where(i => recoveredIds.Contains(i.Id)).ToList();
        var lostItems = stashCandidates.Where(i => !recoveredIds.Contains(i.Id)).ToList();

        var newInventory = inventory.Clone();
        newInventory.StoredItems = placement.Items;

        return new RunInventorySettlement
        {
            Inventory = newInventory,
            ResourcesRecovered = ShotV.Inventory.GridInventory.BuildResourceLedgerFromItems(recoveredItems),
            ResourcesLost = ShotV.Inventory.GridInventory.BuildResourceLedgerFromItems(lostItems),
            LootRecovered = run.LootEntries.Where(e => recoveredIds.Contains(e.Id)).Select(e => e.Clone()).ToList(),
            LootLost = run.LootEntries.Where(e => carriedIds.Contains(e.Id) && !recoveredIds.Contains(e.Id)).Select(e => e.Clone()).ToList(),
        };
    }

    private static bool IsPersistedBaseInventoryItem(InventoryItemRecord item)
    {
        if (!ItemData.ById.TryGetValue(item.ItemId, out var definition))
            return false;

        return definition.Use != null || definition.Category == ItemCategory.Consumable;
    }

    private static bool IsAllowedDeploymentPackItem(InventoryItemRecord item)
    {
        return IsPersistedBaseInventoryItem(item);
    }

    private static DeploymentReadinessResult BuildDeploymentReadiness(SaveState save)
    {
        var map = RouteData.GetMap(save.World.SelectedRouteId);
        var pack = save.Inventory.DeploymentPack;
        var result = new DeploymentReadinessResult
        {
            HighestThreat = map.Zones.Length > 0 ? map.Zones.Max(zone => zone.ThreatLevel) : 0,
            CapacityCells = pack.Columns * pack.Rows,
            OccupiedCells = pack.Items.Sum(item => item.Width * item.Height),
            StagedUnits = pack.Items.Sum(item => item.Quantity),
            StatusLabel = "Ready",
            Detail = "Deployment pack is acceptable for the selected map.",
            CanDeploy = true,
        };

        foreach (var item in pack.Items)
        {
            if (!ItemData.ById.TryGetValue(item.ItemId, out var definition) || definition.Use == null)
                continue;

            if (definition.Use.Heals > 0)
                result.HealingUnits += item.Quantity;
            if (definition.Use.RefreshDash)
                result.MobilityUnits += item.Quantity;
            if (definition.Use.ExplosionDamage > 0)
                result.UtilityUnits += item.Quantity;
        }

        if (save.Inventory.EquippedWeaponIds.Count == 0)
        {
            result.CanDeploy = false;
            result.StatusLabel = "Blocked";
            result.Detail = "At least one weapon must remain equipped before deployment.";
            return result;
        }

        if (result.HighestThreat >= 3 && result.HealingUnits <= 0)
        {
            result.CanDeploy = false;
            result.StatusLabel = "Blocked";
            result.Detail = "High-risk maps require at least one healing consumable in the deployment pack.";
            return result;
        }

        if (result.HighestThreat >= 2 && result.StagedUnits <= 0)
        {
            result.CanDeploy = false;
            result.StatusLabel = "Blocked";
            result.Detail = "Stage at least one consumable before entering medium or high-risk regions.";
            return result;
        }

        var warnings = new List<string>();
        if (result.HighestThreat >= 2 && result.HealingUnits <= 0)
            warnings.Add("no medical support");
        if (result.HighestThreat >= 2 && result.MobilityUnits <= 0)
            warnings.Add("no mobility reserve");
        if (result.HighestThreat >= 2 && result.UtilityUnits <= 0)
            warnings.Add("no area-control item");
        if (result.HighestThreat >= 3 && result.StagedUnits < 2)
            warnings.Add("payload is very light");
        if (result.HighestThreat <= 1 && result.StagedUnits <= 0)
            warnings.Add("deploying with an empty pack");

        if (warnings.Count > 0)
        {
            result.HasWarnings = true;
            result.StatusLabel = "Caution";
            result.Detail = $"Map is deployable, but {string.Join(", ", warnings)}.";
        }

        return result;
    }

    private static bool CanAfford(BaseResources stock, ResourceBundle cost)
    {
        return stock.Salvage >= cost.Salvage
            && stock.Alloy >= cost.Alloy
            && stock.Research >= cost.Research;
    }

    private static void SpendResources(BaseResources stock, ResourceBundle cost)
    {
        stock.Salvage -= cost.Salvage;
        stock.Alloy -= cost.Alloy;
        stock.Research -= cost.Research;
    }

    private static ExtractionResult BuildExtractionResult(RunState run, RunResolutionOutcome outcome, string resolvedAt, RunInventorySettlement settlement)
    {
        var currentZone = RouteManager.GetCurrentRunZone(run.Map);
        return new ExtractionResult
        {
            RunId = run.Id,
            Outcome = outcome,
            Success = outcome != RunResolutionOutcome.Down,
            ResolvedAt = resolvedAt,
            DurationSeconds = (int)Math.Round(run.Stats.ElapsedSeconds),
            Kills = run.Stats.Kills,
            HighestWave = Math.Max(run.Stats.HighestWave, Math.Max(run.Map.HighestWave, run.Map.CurrentWave)),
            BossDefeated = run.Stats.BossDefeated || outcome == RunResolutionOutcome.BossClear,
            ResourcesRecovered = settlement.ResourcesRecovered,
            ResourcesLost = settlement.ResourcesLost,
            LootRecovered = settlement.LootRecovered,
            LootLost = settlement.LootLost,
            SummaryLabel = BuildExtractionSummaryLabel(outcome, currentZone?.Label),
        };
    }

    private static string BuildExtractionSummaryLabel(RunResolutionOutcome outcome, string? zoneLabel)
    {
        if (outcome == RunResolutionOutcome.BossClear)
            return zoneLabel != null ? $"{zoneLabel}压制完成" : "地图压制完成";
        if (outcome == RunResolutionOutcome.Down)
            return "行动失败";
        return zoneLabel != null ? $"已从{zoneLabel}撤离" : "成功撤离";
    }

    private static List<WeaponType> BuildRunLoadout(SaveState save)
    {
        return save.Inventory.EquippedWeaponIds.Count > 0
            ? new List<WeaponType>(save.Inventory.EquippedWeaponIds)
            : new List<WeaponType> { WeaponType.MachineGun, WeaponType.Grenade, WeaponType.Sniper };
    }

    private static List<InventoryItemRecord> BuildInitialReserveAmmoItems(IEnumerable<WeaponType> loadout)
    {
        var records = new List<InventoryItemRecord>();
        foreach (var weaponId in loadout.Distinct())
        {
            if (!WeaponData.ById.TryGetValue(weaponId, out var weapon))
                continue;

            foreach (var ammo in weapon.AmmoTypes)
            {
                if (string.IsNullOrWhiteSpace(ammo.ReserveItemId) || ammo.StartingReserve <= 0)
                    continue;

                int remaining = ammo.StartingReserve;
                while (remaining > 0)
                {
                    int chunk = Math.Min(60, remaining);
                    var record = ShotV.Inventory.GridInventory.CreateItemRecord(ammo.ReserveItemId, chunk);
                    if (record != null)
                        records.Add(record);
                    remaining -= chunk;
                }
            }
        }

        return records;
    }

    private static string BuildRunId()
    {
        return $"run-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}-{Guid.NewGuid().ToString()[..8]}";
    }

    private static string Now() => DateTime.UtcNow.ToString("o");
}
