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
    Shop,
    Maintenance,
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
        save.Base ??= new BaseState();
        save.Inventory ??= new InventoryState();
        save.Inventory.DeploymentPack ??= new GridInventoryState();
        if (save.Version < 10 && save.Base.Credits <= 0)
            save.Base.Credits = ShopData.StartingCredits;
        save.Version = Math.Max(save.Version, 10);
        EnsureEquipmentStates(save.Inventory);
        save.Inventory.DeploymentPack.Items = save.Inventory.DeploymentPack.Items
            .Where(IsAllowedDeploymentPackItem)
            .Select(item => item.Clone())
            .ToList();
        save.Inventory.DeploymentPack.QuickSlots = new string?[GridInventoryState.RunQuickSlotCount];
        if (save.Session.ActiveRun != null)
            HydrateActiveRunEquipment(save.Inventory, save.Session.ActiveRun);

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

    public void DeployCombat(bool force = false)
    {
        if (_state.Save.Session.ActiveRun != null || (!force && !_state.Runtime.PrimaryActionReady)) return;
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
        ApplyBaseEquipmentToRun(save.Inventory, nextRun);
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
        PersistRunEquipmentToInventory(save.Inventory, run);
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
        EnsureEquipmentStates(save.Inventory);
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

    public void CycleEquippedWeapon(int slotIndex, int direction)
    {
        if (_state.Save.Session.ActiveRun != null)
            return;

        var save = _state.Save.Clone();
        EnsureEquipmentStates(save.Inventory);

        var equipped = save.Inventory.EquippedWeaponIds;
        if (slotIndex < 0 || slotIndex >= equipped.Count || direction == 0)
            return;

        int currentCatalogIndex = System.Array.FindIndex(WeaponData.Catalog, weapon => weapon.Id == equipped[slotIndex]);
        if (currentCatalogIndex < 0)
            currentCatalogIndex = 0;

        int step = direction > 0 ? 1 : -1;
        int nextCatalogIndex = (currentCatalogIndex + step + WeaponData.Catalog.Length) % WeaponData.Catalog.Length;
        var nextWeaponId = WeaponData.Catalog[nextCatalogIndex].Id;
        int existingIndex = equipped.IndexOf(nextWeaponId);
        if (existingIndex >= 0 && existingIndex != slotIndex)
        {
            (equipped[slotIndex], equipped[existingIndex]) = (equipped[existingIndex], equipped[slotIndex]);
        }
        else
        {
            equipped[slotIndex] = nextWeaponId;
        }

        save.UpdatedAt = Now();
        Commit(_state.Mode, save);
    }

    public void SelectEquippedArmor(string armorId)
    {
        if (_state.Save.Session.ActiveRun != null)
            return;
        if (!ArmorData.ById.ContainsKey(armorId))
            return;

        var save = _state.Save.Clone();
        EnsureEquipmentStates(save.Inventory);
        if (!save.Inventory.OwnedArmorIds.Contains(armorId))
            save.Inventory.OwnedArmorIds.Add(armorId);
        save.Inventory.EquippedArmorId = armorId;
        save.UpdatedAt = Now();
        Commit(_state.Mode, save);
    }

    public void RepairWeapon(WeaponType weaponId)
    {
        if (_state.Save.Session.ActiveRun != null)
            return;

        var save = _state.Save.Clone();
        EnsureEquipmentStates(save.Inventory);
        var state = EnsureWeaponBenchState(save.Inventory, weaponId);
        var repairCost = EquipmentRules.GetWeaponRepairCost(state);
        if (!HasResourceCost(repairCost) || !CanAfford(save.Base.Resources, repairCost))
            return;

        SpendResources(save.Base.Resources, repairCost);
        state.Durability = state.MaxDurability;
        save.UpdatedAt = Now();
        Commit(_state.Mode, save);
    }

    public void UpgradeWeapon(WeaponType weaponId)
    {
        if (_state.Save.Session.ActiveRun != null)
            return;
        if (!WeaponData.ById.TryGetValue(weaponId, out var definition))
            return;

        var save = _state.Save.Clone();
        EnsureEquipmentStates(save.Inventory);
        var state = EnsureWeaponBenchState(save.Inventory, weaponId);
        if (state.UpgradeLevel >= EquipmentRules.MaxWeaponUpgradeLevel)
            return;

        int nextLevel = state.UpgradeLevel + 1;
        var upgradeCost = EquipmentRules.GetWeaponUpgradeCost(definition, nextLevel);
        if (!CanAfford(save.Base.Resources, upgradeCost))
            return;

        SpendResources(save.Base.Resources, upgradeCost);
        state.UpgradeLevel = nextLevel;
        state.Durability = state.MaxDurability;
        save.UpdatedAt = Now();
        Commit(_state.Mode, save);
    }

    public void RepairArmor(string armorId)
    {
        if (_state.Save.Session.ActiveRun != null)
            return;
        if (!ArmorData.ById.ContainsKey(armorId))
            return;

        var save = _state.Save.Clone();
        EnsureEquipmentStates(save.Inventory);
        var state = EnsureArmorBenchState(save.Inventory, armorId);
        var repairCost = EquipmentRules.GetArmorRepairCost(state);
        if (!HasResourceCost(repairCost) || !CanAfford(save.Base.Resources, repairCost))
            return;

        SpendResources(save.Base.Resources, repairCost);
        state.Durability = state.MaxDurability;
        save.UpdatedAt = Now();
        Commit(_state.Mode, save);
    }

    public void UpgradeArmor(string armorId)
    {
        if (_state.Save.Session.ActiveRun != null)
            return;
        if (!ArmorData.ById.TryGetValue(armorId, out var definition))
            return;

        var save = _state.Save.Clone();
        EnsureEquipmentStates(save.Inventory);
        var state = EnsureArmorBenchState(save.Inventory, armorId);
        if (state.UpgradeLevel >= EquipmentRules.MaxArmorUpgradeLevel)
            return;

        int nextLevel = state.UpgradeLevel + 1;
        var upgradeCost = EquipmentRules.GetArmorUpgradeCost(definition, nextLevel);
        if (!CanAfford(save.Base.Resources, upgradeCost))
            return;

        SpendResources(save.Base.Resources, upgradeCost);
        state.UpgradeLevel = nextLevel;
        state.Durability = state.MaxDurability;
        save.UpdatedAt = Now();
        Commit(_state.Mode, save);
    }

    public void RepairAllEquipment()
    {
        if (_state.Save.Session.ActiveRun != null)
            return;

        var save = _state.Save.Clone();
        EnsureEquipmentStates(save.Inventory);
        var totalCost = ResourceBundle.Zero();
        foreach (var weaponState in save.Inventory.WeaponStates)
            totalCost.Add(EquipmentRules.GetWeaponRepairCost(weaponState));
        foreach (var armorState in save.Inventory.ArmorStates)
            totalCost.Add(EquipmentRules.GetArmorRepairCost(armorState));

        if (!HasResourceCost(totalCost) || !CanAfford(save.Base.Resources, totalCost))
            return;

        SpendResources(save.Base.Resources, totalCost);
        foreach (var weaponState in save.Inventory.WeaponStates)
            weaponState.Durability = weaponState.MaxDurability;
        foreach (var armorState in save.Inventory.ArmorStates)
            armorState.Durability = armorState.MaxDurability;

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

    public void BuyShopAmmo(string itemId, int quantity)
    {
        if (_state.Save.Session.ActiveRun != null || quantity <= 0)
            return;
        if (!ShopData.TryGetAmmoOffer(itemId, out var offer))
            return;

        var save = _state.Save.Clone();
        int totalCost = offer.PricePerRound * quantity;
        if (save.Base.Credits < totalCost)
            return;

        var incoming = BuildShopPurchaseRecords(itemId, quantity);
        if (incoming.Count == 0)
            return;

        var placement = ShotV.Inventory.GridInventory.StoreItemsInGrid(
            save.Inventory.DeploymentPack.Columns,
            save.Inventory.DeploymentPack.Rows,
            save.Inventory.DeploymentPack.Items,
            incoming);
        if (placement.Rejected.Count > 0)
            return;

        save.Base.Credits -= totalCost;
        save.Inventory.DeploymentPack.Items = placement.Items;
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

    public void UpdateActiveRunInventoryState(
        List<InventoryItemRecord> items,
        List<GroundLootDrop>? groundLoot = null,
        string?[]? quickSlots = null,
        List<WeaponType>? loadoutWeaponIds = null,
        WeaponType? currentWeaponId = null)
    {
        var save = _state.Save.Clone();
        var run = save.Session.ActiveRun;
        if (run == null)
            return;

        run.Inventory.Items = ShotV.Inventory.GridInventory.CloneItems(items);
        if (groundLoot != null)
            run.GroundLoot = groundLoot.Select(drop => drop.Clone()).ToList();

        if (loadoutWeaponIds != null)
        {
            var nextLoadout = loadoutWeaponIds
                .Where(WeaponData.ById.ContainsKey)
                .Distinct()
                .Take(WeaponData.MaxLoadoutSize)
                .ToList();
            if (nextLoadout.Count > 0)
            {
                run.Player.LoadoutWeaponIds = nextLoadout;
                run.Player.CurrentWeaponId = currentWeaponId.HasValue && nextLoadout.Contains(currentWeaponId.Value)
                    ? currentWeaponId.Value
                    : nextLoadout[0];
            }
        }

        SanitizeRunLoadout(run.Player);

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
            StatusLabel = GameText.Text("readiness.ready"),
            Detail = GameText.Text("readiness.acceptable"),
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
            result.StatusLabel = GameText.Text("readiness.blocked");
            result.Detail = GameText.Text("readiness.no_weapon");
            return result;
        }

        var warnings = new List<string>();
        var armorState = GetEquippedArmorState(save.Inventory);
        if (armorState == null || EquipmentRules.GetDurabilityRatio(armorState.Durability, armorState.MaxDurability) < 0.2f)
            warnings.Add("护甲接近失效");
        if (save.Inventory.WeaponStates.Any(state => save.Inventory.EquippedWeaponIds.Contains(state.WeaponId)
            && EquipmentRules.GetDurabilityRatio(state.Durability, state.MaxDurability) < 0.2f))
            warnings.Add("武器耐久偏低");
        if (result.HighestThreat >= 2 && result.HealingUnits <= 0)
            warnings.Add(GameText.Text("readiness.warning.no_medical"));
        if (result.HighestThreat >= 2 && result.MobilityUnits <= 0)
            warnings.Add(GameText.Text("readiness.warning.no_mobility"));
        if (result.HighestThreat >= 2 && result.UtilityUnits <= 0)
            warnings.Add(GameText.Text("readiness.warning.no_utility"));
        if (result.HighestThreat >= 3 && result.StagedUnits < 2)
            warnings.Add(GameText.Text("readiness.warning.light_payload"));
        if (result.HighestThreat <= 1 && result.StagedUnits <= 0)
            warnings.Add(GameText.Text("readiness.warning.empty_pack"));

        if (warnings.Count > 0)
        {
            result.HasWarnings = true;
            result.StatusLabel = GameText.Text("readiness.caution");
            result.Detail = GameText.Format("readiness.warning.detail", string.Join("、", warnings));
        }

        return result;
    }

    private static void EnsureEquipmentStates(InventoryState inventory)
    {
        inventory.EquippedWeaponIds = inventory.EquippedWeaponIds
            .Where(WeaponData.ById.ContainsKey)
            .Distinct()
            .ToList();
        foreach (var defaultWeaponId in WeaponData.DefaultLoadoutIds)
        {
            if (inventory.EquippedWeaponIds.Count >= WeaponData.MaxLoadoutSize)
                break;
            if (!inventory.EquippedWeaponIds.Contains(defaultWeaponId))
                inventory.EquippedWeaponIds.Add(defaultWeaponId);
        }
        inventory.EquippedWeaponIds = inventory.EquippedWeaponIds
            .Take(WeaponData.MaxLoadoutSize)
            .ToList();
        if (inventory.EquippedWeaponIds.Count == 0)
            inventory.EquippedWeaponIds = new List<WeaponType>(WeaponData.DefaultLoadoutIds);

        if (inventory.OwnedArmorIds.Count == 0)
            inventory.OwnedArmorIds = ArmorData.Catalog.Select(armor => armor.Id).ToList();
        inventory.OwnedArmorIds = inventory.OwnedArmorIds
            .Where(ArmorData.ById.ContainsKey)
            .Distinct()
            .ToList();
        if (inventory.OwnedArmorIds.Count == 0)
            inventory.OwnedArmorIds.Add(ArmorData.DefaultArmorId);

        if (string.IsNullOrWhiteSpace(inventory.EquippedArmorId) || !ArmorData.ById.ContainsKey(inventory.EquippedArmorId))
            inventory.EquippedArmorId = inventory.OwnedArmorIds[0];
        else if (!inventory.OwnedArmorIds.Contains(inventory.EquippedArmorId))
            inventory.OwnedArmorIds.Insert(0, inventory.EquippedArmorId);

        inventory.WeaponStates = inventory.WeaponStates
            .Where(state => WeaponData.ById.ContainsKey(state.WeaponId))
            .GroupBy(state => state.WeaponId)
            .Select(group => group.First())
            .ToList();

        foreach (var weapon in WeaponData.Catalog)
        {
            var state = EnsureWeaponBenchState(inventory, weapon.Id);
            if (state.MaxDurability <= 0f)
            {
                state.MaxDurability = weapon.MaxDurability;
                if (state.Durability <= 0f)
                    state.Durability = state.MaxDurability;
            }
            else
            {
                state.MaxDurability = weapon.MaxDurability;
            }
            state.Durability = Math.Clamp(state.Durability, 0f, state.MaxDurability);
            state.UpgradeLevel = Math.Clamp(state.UpgradeLevel, 0, EquipmentRules.MaxWeaponUpgradeLevel);
        }

        foreach (var armorId in inventory.OwnedArmorIds)
        {
            var state = EnsureArmorBenchState(inventory, armorId);
            if (!ArmorData.ById.TryGetValue(armorId, out var definition))
                continue;

            if (state.MaxDurability <= 0f)
            {
                state.MaxDurability = definition.MaxDurability;
                if (state.Durability <= 0f)
                    state.Durability = state.MaxDurability;
            }
            else
            {
                state.MaxDurability = definition.MaxDurability;
            }
            state.Durability = Math.Clamp(state.Durability, 0f, state.MaxDurability);
            state.UpgradeLevel = Math.Clamp(state.UpgradeLevel, 0, EquipmentRules.MaxArmorUpgradeLevel);
        }
    }

    private static WeaponBenchState EnsureWeaponBenchState(InventoryState inventory, WeaponType weaponId)
    {
        var existing = inventory.WeaponStates.FirstOrDefault(state => state.WeaponId == weaponId);
        if (existing != null)
            return existing;

        float maxDurability = WeaponData.ById.TryGetValue(weaponId, out var definition)
            ? definition.MaxDurability
            : 100f;
        var created = new WeaponBenchState
        {
            WeaponId = weaponId,
            Durability = maxDurability,
            MaxDurability = maxDurability,
        };
        inventory.WeaponStates.Add(created);
        return created;
    }

    private static ArmorBenchState EnsureArmorBenchState(InventoryState inventory, string armorId)
    {
        var existing = inventory.ArmorStates.FirstOrDefault(state => state.ArmorId == armorId);
        if (existing != null)
            return existing;

        float maxDurability = ArmorData.ById.TryGetValue(armorId, out var definition)
            ? definition.MaxDurability
            : 100f;
        var created = new ArmorBenchState
        {
            ArmorId = armorId,
            Durability = maxDurability,
            MaxDurability = maxDurability,
        };
        inventory.ArmorStates.Add(created);
        return created;
    }

    private static ArmorBenchState? GetEquippedArmorState(InventoryState inventory)
    {
        if (string.IsNullOrWhiteSpace(inventory.EquippedArmorId))
            return null;

        return inventory.ArmorStates.FirstOrDefault(state => state.ArmorId == inventory.EquippedArmorId);
    }

    private static void SanitizeRunLoadout(PlayerRunState player)
    {
        var loadout = player.LoadoutWeaponIds
            .Where(WeaponData.ById.ContainsKey)
            .Distinct()
            .Take(WeaponData.MaxLoadoutSize)
            .ToList();

        foreach (var defaultWeaponId in WeaponData.DefaultLoadoutIds)
        {
            if (loadout.Count >= WeaponData.MaxLoadoutSize)
                break;
            if (!loadout.Contains(defaultWeaponId))
                loadout.Add(defaultWeaponId);
        }

        if (loadout.Count == 0)
            loadout = WeaponData.DefaultLoadoutIds.Take(WeaponData.MaxLoadoutSize).ToList();

        player.LoadoutWeaponIds = loadout;
        if (!player.LoadoutWeaponIds.Contains(player.CurrentWeaponId))
            player.CurrentWeaponId = player.LoadoutWeaponIds[0];

        player.WeaponStates = player.WeaponStates
            .Where(state => WeaponData.ById.ContainsKey(state.WeaponId) && player.LoadoutWeaponIds.Contains(state.WeaponId))
            .GroupBy(state => state.WeaponId)
            .Select(group => group.First())
            .ToList();
    }

    private static void ApplyBaseEquipmentToRun(InventoryState inventory, RunState run)
    {
        EnsureEquipmentStates(inventory);
        SanitizeRunLoadout(run.Player);
        run.Player.EnsureWeaponStates();
        foreach (var weaponId in run.Player.LoadoutWeaponIds)
        {
            var source = EnsureWeaponBenchState(inventory, weaponId);
            var target = run.Player.EnsureWeaponState(weaponId);
            target.MaxDurability = source.MaxDurability;
            target.Durability = Math.Clamp(source.Durability, 0f, source.MaxDurability);
            target.UpgradeLevel = source.UpgradeLevel;
        }

        string armorId = inventory.EquippedArmorId ?? ArmorData.DefaultArmorId;
        var armorBench = EnsureArmorBenchState(inventory, armorId);
        run.Player.Armor = new PlayerRunState.PlayerArmorState
        {
            ArmorId = armorBench.ArmorId,
            Durability = Math.Clamp(armorBench.Durability, 0f, armorBench.MaxDurability),
            MaxDurability = armorBench.MaxDurability,
            UpgradeLevel = armorBench.UpgradeLevel,
        };

        run.Player.MaxHealth = CombatConstants.PlayerMaxHealth + EquipmentRules.GetArmorMaxHealthBonus(run.Player.Armor);
        run.Player.Health = run.Player.MaxHealth;
    }

    private static void HydrateActiveRunEquipment(InventoryState inventory, RunState run)
    {
        EnsureEquipmentStates(inventory);
        SanitizeRunLoadout(run.Player);
        run.Player.EnsureWeaponStates();
        foreach (var weaponId in run.Player.LoadoutWeaponIds)
        {
            var bench = EnsureWeaponBenchState(inventory, weaponId);
            var runtime = run.Player.EnsureWeaponState(weaponId);
            if (runtime.MaxDurability <= 0f)
            {
                runtime.MaxDurability = bench.MaxDurability;
                if (runtime.Durability <= 0f)
                    runtime.Durability = bench.Durability;
            }
            else
            {
                runtime.MaxDurability = bench.MaxDurability;
            }
            runtime.Durability = Math.Clamp(runtime.Durability, 0f, runtime.MaxDurability);
            runtime.UpgradeLevel = runtime.UpgradeLevel > 0 ? runtime.UpgradeLevel : bench.UpgradeLevel;
        }

        if (string.IsNullOrWhiteSpace(run.Player.Armor.ArmorId))
        {
            string armorId = inventory.EquippedArmorId ?? ArmorData.DefaultArmorId;
            var bench = EnsureArmorBenchState(inventory, armorId);
            run.Player.Armor = new PlayerRunState.PlayerArmorState
            {
                ArmorId = bench.ArmorId,
                Durability = bench.Durability,
                MaxDurability = bench.MaxDurability,
                UpgradeLevel = bench.UpgradeLevel,
            };
        }
        else
        {
            var bench = EnsureArmorBenchState(inventory, run.Player.Armor.ArmorId);
            if (run.Player.Armor.MaxDurability <= 0f)
            {
                run.Player.Armor.MaxDurability = bench.MaxDurability;
                if (run.Player.Armor.Durability <= 0f)
                    run.Player.Armor.Durability = bench.Durability;
            }
            else
            {
                run.Player.Armor.MaxDurability = bench.MaxDurability;
            }
            run.Player.Armor.Durability = Math.Clamp(run.Player.Armor.Durability, 0f, run.Player.Armor.MaxDurability);
            run.Player.Armor.UpgradeLevel = run.Player.Armor.UpgradeLevel > 0 ? run.Player.Armor.UpgradeLevel : bench.UpgradeLevel;
        }

        float previousRatio = run.Player.MaxHealth > 0.001f ? run.Player.Health / run.Player.MaxHealth : 1f;
        run.Player.MaxHealth = CombatConstants.PlayerMaxHealth + EquipmentRules.GetArmorMaxHealthBonus(run.Player.Armor);
        run.Player.Health = Math.Clamp(run.Player.MaxHealth * previousRatio, 0f, run.Player.MaxHealth);
    }

    private static void PersistRunEquipmentToInventory(InventoryState inventory, RunState run)
    {
        EnsureEquipmentStates(inventory);
        foreach (var runtime in run.Player.WeaponStates)
        {
            var bench = EnsureWeaponBenchState(inventory, runtime.WeaponId);
            bench.MaxDurability = runtime.MaxDurability > 0f ? runtime.MaxDurability : bench.MaxDurability;
            bench.Durability = Math.Clamp(runtime.Durability, 0f, bench.MaxDurability);
            bench.UpgradeLevel = Math.Max(bench.UpgradeLevel, runtime.UpgradeLevel);
        }

        if (!string.IsNullOrWhiteSpace(run.Player.Armor.ArmorId))
        {
            if (!inventory.OwnedArmorIds.Contains(run.Player.Armor.ArmorId))
                inventory.OwnedArmorIds.Add(run.Player.Armor.ArmorId);

            inventory.EquippedArmorId = run.Player.Armor.ArmorId;
            var bench = EnsureArmorBenchState(inventory, run.Player.Armor.ArmorId);
            bench.MaxDurability = run.Player.Armor.MaxDurability > 0f ? run.Player.Armor.MaxDurability : bench.MaxDurability;
            bench.Durability = Math.Clamp(run.Player.Armor.Durability, 0f, bench.MaxDurability);
            bench.UpgradeLevel = Math.Max(bench.UpgradeLevel, run.Player.Armor.UpgradeLevel);
        }
    }

    private static bool HasResourceCost(ResourceBundle cost)
    {
        return cost.Salvage > 0 || cost.Alloy > 0 || cost.Research > 0;
    }

    private static List<InventoryItemRecord> BuildShopPurchaseRecords(string itemId, int quantity)
    {
        var records = new List<InventoryItemRecord>();
        if (!ItemData.ById.TryGetValue(itemId, out var definition))
            return records;

        int remaining = quantity;
        while (remaining > 0)
        {
            int chunk = Math.Min(definition.MaxStack, remaining);
            var record = ShotV.Inventory.GridInventory.CreateItemRecord(itemId, chunk);
            if (record == null)
                break;

            records.Add(record);
            remaining -= chunk;
        }

        return records;
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
            return zoneLabel != null
                ? GameText.Format("overlay.extraction.boss_clear_with_zone", zoneLabel)
                : GameText.Text("overlay.extraction.boss_clear");
        if (outcome == RunResolutionOutcome.Down)
            return GameText.Text("overlay.extraction.down");
        return zoneLabel != null
            ? GameText.Format("overlay.extraction.extracted_with_zone", zoneLabel)
            : GameText.Text("overlay.extraction.extracted");
    }

    private static List<WeaponType> BuildRunLoadout(SaveState save)
    {
        var loadout = save.Inventory.EquippedWeaponIds
            .Where(WeaponData.ById.ContainsKey)
            .Distinct()
            .Take(WeaponData.MaxLoadoutSize)
            .ToList();
        foreach (var defaultWeaponId in WeaponData.DefaultLoadoutIds)
        {
            if (loadout.Count >= WeaponData.MaxLoadoutSize)
                break;
            if (!loadout.Contains(defaultWeaponId))
                loadout.Add(defaultWeaponId);
        }

        return loadout.Count > 0
            ? loadout
            : new List<WeaponType>(WeaponData.DefaultLoadoutIds);
    }

    private static List<InventoryItemRecord> BuildInitialReserveAmmoItems(IEnumerable<WeaponType> loadout)
    {
        var records = new List<InventoryItemRecord>();
        var ammoReserves = new Dictionary<string, int>();

        foreach (var weaponId in loadout.Distinct())
        {
            if (!WeaponData.ById.TryGetValue(weaponId, out var weapon))
                continue;

            foreach (var ammo in weapon.AmmoTypes)
            {
                if (string.IsNullOrWhiteSpace(ammo.ReserveItemId) || ammo.StartingReserve <= 0)
                    continue;

                int currentReserve = ammoReserves.TryGetValue(ammo.ReserveItemId, out var existingReserve)
                    ? existingReserve
                    : 0;
                ammoReserves[ammo.ReserveItemId] = Math.Max(currentReserve, ammo.StartingReserve);
            }
        }

        foreach (var (reserveItemId, reserveQuantity) in ammoReserves)
        {
            int remaining = reserveQuantity;
            while (remaining > 0)
            {
                int chunk = Math.Min(60, remaining);
                var record = ShotV.Inventory.GridInventory.CreateItemRecord(reserveItemId, chunk);
                if (record != null)
                    records.Add(record);
                remaining -= chunk;
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
