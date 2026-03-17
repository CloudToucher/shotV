using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ShotV.Core;
using ShotV.Data;

namespace ShotV.State;

public class PlayerRunState
{
    public class PlayerWeaponState
    {
        public WeaponType WeaponId { get; set; }
        public string AmmoTypeId { get; set; } = "";
        public int Magazine { get; set; }
        public int MagazineCapacity { get; set; }

        public PlayerWeaponState Clone() => new()
        {
            WeaponId = WeaponId,
            AmmoTypeId = AmmoTypeId,
            Magazine = Magazine,
            MagazineCapacity = MagazineCapacity,
        };
    }

    public float Health { get; set; } = 100f;
    public float MaxHealth { get; set; } = 100f;
    public WeaponType CurrentWeaponId { get; set; } = WeaponType.MachineGun;
    public List<WeaponType> LoadoutWeaponIds { get; set; } = new() { WeaponType.MachineGun, WeaponType.Grenade, WeaponType.Sniper };
    public List<PlayerWeaponState> WeaponStates { get; set; } = new();
    public int ShotsFired { get; set; }
    public int GrenadesThrown { get; set; }
    public int DashesUsed { get; set; }
    public float DamageTaken { get; set; }

    public PlayerRunState Clone() => new()
    {
        Health = Health, MaxHealth = MaxHealth, CurrentWeaponId = CurrentWeaponId,
        LoadoutWeaponIds = new List<WeaponType>(LoadoutWeaponIds),
        WeaponStates = WeaponStates.Select(state => state.Clone()).ToList(),
        ShotsFired = ShotsFired, GrenadesThrown = GrenadesThrown,
        DashesUsed = DashesUsed, DamageTaken = DamageTaken,
    };

    public void EnsureWeaponStates()
    {
        foreach (var weaponId in LoadoutWeaponIds)
            EnsureWeaponState(weaponId);
    }

    public PlayerWeaponState EnsureWeaponState(WeaponType weaponId)
    {
        var existing = WeaponStates.FirstOrDefault(state => state.WeaponId == weaponId);
        if (existing != null)
        {
            if (WeaponData.ById.TryGetValue(weaponId, out var definition))
            {
                existing.MagazineCapacity = definition.MagazineCapacity;
                if (string.IsNullOrWhiteSpace(existing.AmmoTypeId)
                    || !definition.AmmoTypes.Any(ammo => ammo.Id == existing.AmmoTypeId))
                    existing.AmmoTypeId = WeaponData.GetDefaultAmmo(definition).Id;
                existing.Magazine = Mathf.Clamp(existing.Magazine, 0, existing.MagazineCapacity);
            }

            return existing;
        }

        var fallback = WeaponData.ById.TryGetValue(weaponId, out var weapon)
            ? weapon
            : WeaponData.Loadout[0];
        var ammo = WeaponData.GetDefaultAmmo(fallback);
        var created = new PlayerWeaponState
        {
            WeaponId = weaponId,
            AmmoTypeId = ammo.Id,
            Magazine = fallback.MagazineCapacity,
            MagazineCapacity = fallback.MagazineCapacity,
        };
        WeaponStates.Add(created);
        return created;
    }
}

public class GroundLootDrop
{
    public string Id { get; set; } = "";
    public InventoryItemRecord Item { get; set; } = new();
    public float X { get; set; }
    public float Y { get; set; }
    public LootSource Source { get; set; }

    public GroundLootDrop Clone() => new()
    {
        Id = Id, Item = Item.Clone(), X = X, Y = Y, Source = Source,
    };
}

public class RunBossState
{
    public bool Spawned { get; set; }
    public bool Defeated { get; set; }
    public string? Label { get; set; }
    public int? Phase { get; set; }
    public float? Health { get; set; }
    public float? MaxHealth { get; set; }

    public RunBossState Clone() => new()
    {
        Spawned = Spawned, Defeated = Defeated, Label = Label,
        Phase = Phase, Health = Health, MaxHealth = MaxHealth,
    };
}

public class RunZoneState
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public WorldZoneKind Kind { get; set; }
    public RunZoneStatus Status { get; set; }
    public int ThreatLevel { get; set; }
    public float RewardMultiplier { get; set; }
    public bool AllowsExtraction { get; set; }
    public string Description { get; set; } = "";

    public RunZoneState Clone() => new()
    {
        Id = Id, Label = Label, Kind = Kind, Status = Status,
        ThreatLevel = ThreatLevel, RewardMultiplier = RewardMultiplier,
        AllowsExtraction = AllowsExtraction, Description = Description,
    };
}

public class RunMapState
{
    public string RouteId { get; set; } = "combat-sandbox-route";
    public string CurrentZoneId { get; set; } = "perimeter-dock";
    public uint LayoutSeed { get; set; } = 1;
    public List<RunZoneState> Zones { get; set; } = new();
    public int CurrentWave { get; set; }
    public int HighestWave { get; set; }
    public int HostilesRemaining { get; set; }
    public RunBossState Boss { get; set; } = new();

    public RunMapState Clone() => new()
    {
        RouteId = RouteId, CurrentZoneId = CurrentZoneId, LayoutSeed = LayoutSeed,
        Zones = Zones.Select(z => z.Clone()).ToList(),
        CurrentWave = CurrentWave, HighestWave = HighestWave, HostilesRemaining = HostilesRemaining,
        Boss = Boss.Clone(),
    };
}

public class LootEntry
{
    public string Id { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    public string Label { get; set; } = "";
    public ItemCategory Category { get; set; }
    public int Quantity { get; set; } = 1;
    public LootSource Source { get; set; }
    public int AcquiredAtWave { get; set; }
    public float AcquiredAtSeconds { get; set; }

    public LootEntry Clone() => new()
    {
        Id = Id, DefinitionId = DefinitionId, Label = Label,
        Category = Category, Quantity = Quantity, Source = Source,
        AcquiredAtWave = AcquiredAtWave, AcquiredAtSeconds = AcquiredAtSeconds,
    };
}

public class RunStats
{
    public float ElapsedSeconds { get; set; }
    public int Kills { get; set; }
    public int HighestWave { get; set; }
    public bool Extracted { get; set; }
    public bool BossDefeated { get; set; }

    public RunStats Clone() => new()
    {
        ElapsedSeconds = ElapsedSeconds, Kills = Kills, HighestWave = HighestWave,
        Extracted = Extracted, BossDefeated = BossDefeated,
    };
}

public class RunState
{
    public string Id { get; set; } = "";
    public string EnteredAt { get; set; } = "";
    public RunStateStatus Status { get; set; } = RunStateStatus.Active;
    public RunResolutionOutcome? PendingOutcome { get; set; }
    public PlayerRunState Player { get; set; } = new();
    public RunMapState Map { get; set; } = new();
    public GridInventoryState Inventory { get; set; } = new();
    public List<GroundLootDrop> GroundLoot { get; set; } = new();
    public ResourceBundle Resources { get; set; } = ResourceBundle.Zero();
    public List<LootEntry> LootEntries { get; set; } = new();
    public RunStats Stats { get; set; } = new();

    public RunState Clone() => new()
    {
        Id = Id, EnteredAt = EnteredAt, Status = Status, PendingOutcome = PendingOutcome,
        Player = Player.Clone(), Map = Map.Clone(), Inventory = Inventory.Clone(),
        GroundLoot = GroundLoot.Select(g => g.Clone()).ToList(),
        Resources = Resources.Clone(),
        LootEntries = LootEntries.Select(l => l.Clone()).ToList(),
        Stats = Stats.Clone(),
    };

    public static RunState CreateInitial(string runId, string enteredAt, List<WeaponType> loadoutWeaponIds, RunMapState mapState)
    {
        return new RunState
        {
            Id = runId,
            EnteredAt = enteredAt,
            Status = RunStateStatus.Active,
            Player = new PlayerRunState
            {
                Health = CombatConstants.PlayerMaxHealth,
                MaxHealth = CombatConstants.PlayerMaxHealth,
                CurrentWeaponId = loadoutWeaponIds.Count > 0 ? loadoutWeaponIds[0] : WeaponType.MachineGun,
                LoadoutWeaponIds = new List<WeaponType>(loadoutWeaponIds),
                WeaponStates = loadoutWeaponIds
                    .Where(WeaponData.ById.ContainsKey)
                    .Select(weaponId =>
                    {
                        var definition = WeaponData.ById[weaponId];
                        var ammo = WeaponData.GetDefaultAmmo(definition);
                        return new PlayerRunState.PlayerWeaponState
                        {
                            WeaponId = weaponId,
                            AmmoTypeId = ammo.Id,
                            Magazine = definition.MagazineCapacity,
                            MagazineCapacity = definition.MagazineCapacity,
                        };
                    })
                    .ToList(),
            },
            Map = mapState.Clone(),
            Inventory = new GridInventoryState(),
        };
    }
}

public class ExtractionResult
{
    public string RunId { get; set; } = "";
    public RunResolutionOutcome Outcome { get; set; }
    public bool Success { get; set; }
    public string ResolvedAt { get; set; } = "";
    public int DurationSeconds { get; set; }
    public int Kills { get; set; }
    public int HighestWave { get; set; }
    public bool BossDefeated { get; set; }
    public ResourceBundle ResourcesRecovered { get; set; } = ResourceBundle.Zero();
    public ResourceBundle ResourcesLost { get; set; } = ResourceBundle.Zero();
    public List<LootEntry> LootRecovered { get; set; } = new();
    public List<LootEntry> LootLost { get; set; } = new();
    public string SummaryLabel { get; set; } = "";
}

public class SessionState
{
    public RunState? ActiveRun { get; set; }
    public ExtractionResult? LastExtraction { get; set; }

    public SessionState Clone() => new()
    {
        ActiveRun = ActiveRun?.Clone(),
        LastExtraction = LastExtraction,
    };
}
