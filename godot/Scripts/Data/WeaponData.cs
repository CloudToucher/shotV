using System.Collections.Generic;
using System.Linq;
using ShotV.Core;

namespace ShotV.Data;

public class WeaponAmmoDefinition
{
    public string Id { get; init; } = "";
    public string ReserveItemId { get; init; } = "";
    public string Label => string.IsNullOrWhiteSpace(ReserveItemId)
        ? Id
        : GameText.Text($"ammo.{ReserveItemId}.label");
    public string Hint => string.IsNullOrWhiteSpace(ReserveItemId)
        ? string.Empty
        : GameText.Text($"ammo.{ReserveItemId}.hint");
    public float Damage { get; init; }
    public int ArmorPenetration { get; init; }
    public int PierceCount { get; init; }
    public int StartingReserve { get; init; }
    public int PickupMin { get; init; }
    public int PickupMax { get; init; }
}

public class WeaponDefinition
{
    public WeaponType Id { get; init; }
    public WeaponFireMode FireMode { get; init; }
    public string Label => GameText.Text($"weapon.{Id}.label");
    public string Hint => GameText.Text($"weapon.{Id}.hint");
    public float Cooldown { get; init; }
    public float Range { get; init; }
    public float EffectWidth { get; init; }
    public float EffectDuration { get; init; }
    public float SplashRadius { get; init; }
    public float StimulusRadius { get; init; } = 360f;
    public int MagazineCapacity { get; init; }
    public float ReloadDuration { get; init; }
    public float BaseSpreadDegrees { get; init; }
    public float SpreadPerShotDegrees { get; init; }
    public float MaxSpreadDegrees { get; init; }
    public float SpreadRecoveryPerSecond { get; init; }
    public float MoveSpreadDegrees { get; init; }
    public float RecoilKick { get; init; }
    public float RecoilTwistDegrees { get; init; }
    public float MaxDurability { get; init; } = 100f;
    public float DurabilityLossPerShot { get; init; } = 0.2f;
    public ResourceBundle RepairUnitCost { get; init; } = ResourceBundle.Zero();
    public ResourceBundle UpgradeBaseCost { get; init; } = ResourceBundle.Zero();
    public WeaponAmmoDefinition[] AmmoTypes { get; init; } = System.Array.Empty<WeaponAmmoDefinition>();
    public string DefaultAmmoId { get; init; } = "";
}

public static class WeaponData
{
    public const int MaxLoadoutSize = 2;

    private static readonly WeaponAmmoDefinition[] AutomaticAmmoTypes =
    {
        new()
        {
            Id = "ball",
            ReserveItemId = "ammo-mg-ball",
            Damage = 11f,
            ArmorPenetration = 0,
            PierceCount = 0,
            StartingReserve = 150,
            PickupMin = 28,
            PickupMax = 40,
        },
        new()
        {
            Id = "ap",
            ReserveItemId = "ammo-mg-ap",
            Damage = 9f,
            ArmorPenetration = 2,
            PierceCount = 1,
            StartingReserve = 75,
            PickupMin = 16,
            PickupMax = 24,
        },
        new()
        {
            Id = "hp",
            ReserveItemId = "ammo-mg-hp",
            Damage = 15f,
            ArmorPenetration = 1,
            PierceCount = 0,
            StartingReserve = 75,
            PickupMin = 14,
            PickupMax = 20,
        },
        new()
        {
            Id = "tracer",
            ReserveItemId = "ammo-mg-tracer",
            Damage = 10f,
            ArmorPenetration = 0,
            PierceCount = 2,
            StartingReserve = 45,
            PickupMin = 12,
            PickupMax = 18,
        },
        new()
        {
            Id = "bonded",
            ReserveItemId = "ammo-mg-bonded",
            Damage = 12f,
            ArmorPenetration = 1,
            PierceCount = 1,
            StartingReserve = 45,
            PickupMin = 10,
            PickupMax = 16,
        },
    };

    private static readonly WeaponAmmoDefinition[] LauncherAmmoTypes =
    {
        new()
        {
            Id = "frag",
            ReserveItemId = "ammo-gl-frag",
            Damage = 34f,
            ArmorPenetration = 1,
            PierceCount = 5,
            StartingReserve = 24,
            PickupMin = 3,
            PickupMax = 5,
        },
        new()
        {
            Id = "breach",
            ReserveItemId = "ammo-gl-breach",
            Damage = 46f,
            ArmorPenetration = 3,
            PierceCount = 2,
            StartingReserve = 12,
            PickupMin = 2,
            PickupMax = 4,
        },
        new()
        {
            Id = "arc",
            ReserveItemId = "ammo-gl-arc",
            Damage = 28f,
            ArmorPenetration = 0,
            PierceCount = 7,
            StartingReserve = 12,
            PickupMin = 2,
            PickupMax = 4,
        },
        new()
        {
            Id = "blast",
            ReserveItemId = "ammo-gl-blast",
            Damage = 42f,
            ArmorPenetration = 0,
            PierceCount = 3,
            StartingReserve = 10,
            PickupMin = 2,
            PickupMax = 4,
        },
        new()
        {
            Id = "flechette",
            ReserveItemId = "ammo-gl-flechette",
            Damage = 26f,
            ArmorPenetration = 2,
            PierceCount = 9,
            StartingReserve = 10,
            PickupMin = 2,
            PickupMax = 4,
        },
    };

    private static readonly WeaponAmmoDefinition[] PrecisionAmmoTypes =
    {
        new()
        {
            Id = "match",
            ReserveItemId = "ammo-sn-match",
            Damage = 50f,
            ArmorPenetration = 1,
            PierceCount = 1,
            StartingReserve = 30,
            PickupMin = 5,
            PickupMax = 8,
        },
        new()
        {
            Id = "sabot",
            ReserveItemId = "ammo-sn-sabot",
            Damage = 42f,
            ArmorPenetration = 4,
            PierceCount = 3,
            StartingReserve = 18,
            PickupMin = 3,
            PickupMax = 6,
        },
        new()
        {
            Id = "exp",
            ReserveItemId = "ammo-sn-exp",
            Damage = 68f,
            ArmorPenetration = 0,
            PierceCount = 0,
            StartingReserve = 12,
            PickupMin = 2,
            PickupMax = 4,
        },
        new()
        {
            Id = "overmatch",
            ReserveItemId = "ammo-sn-overmatch",
            Damage = 48f,
            ArmorPenetration = 2,
            PierceCount = 4,
            StartingReserve = 18,
            PickupMin = 3,
            PickupMax = 5,
        },
        new()
        {
            Id = "rupture",
            ReserveItemId = "ammo-sn-rupture",
            Damage = 60f,
            ArmorPenetration = 1,
            PierceCount = 0,
            StartingReserve = 12,
            PickupMin = 2,
            PickupMax = 4,
        },
    };

    public static readonly WeaponDefinition MachineGun = new()
    {
        Id = WeaponType.MachineGun,
        FireMode = WeaponFireMode.Automatic,
        Cooldown = 0.095f,
        Range = 600f,
        EffectWidth = 4.8f,
        EffectDuration = 0.1f,
        StimulusRadius = 420f,
        MagazineCapacity = 40,
        ReloadDuration = 1.8f,
        BaseSpreadDegrees = 1.1f,
        SpreadPerShotDegrees = 0.72f,
        MaxSpreadDegrees = 8.4f,
        SpreadRecoveryPerSecond = 5.1f,
        MoveSpreadDegrees = 2.5f,
        RecoilKick = 1.05f,
        RecoilTwistDegrees = 2.8f,
        MaxDurability = 116f,
        DurabilityLossPerShot = 0.2f,
        RepairUnitCost = new ResourceBundle { Salvage = 4, Alloy = 1 },
        UpgradeBaseCost = new ResourceBundle { Salvage = 22, Alloy = 2 },
        DefaultAmmoId = "ball",
        AmmoTypes = AutomaticAmmoTypes,
    };

    public static readonly WeaponDefinition Grenade = new()
    {
        Id = WeaponType.Grenade,
        FireMode = WeaponFireMode.Launcher,
        Cooldown = 0.42f,
        Range = 360f,
        SplashRadius = 66f,
        StimulusRadius = 240f,
        MagazineCapacity = 6,
        ReloadDuration = 2.2f,
        BaseSpreadDegrees = 0.3f,
        SpreadPerShotDegrees = 0.42f,
        MaxSpreadDegrees = 2.1f,
        SpreadRecoveryPerSecond = 2.6f,
        MoveSpreadDegrees = 0.85f,
        RecoilKick = 0.82f,
        RecoilTwistDegrees = 1.6f,
        MaxDurability = 96f,
        DurabilityLossPerShot = 0.42f,
        RepairUnitCost = new ResourceBundle { Salvage = 5, Alloy = 1 },
        UpgradeBaseCost = new ResourceBundle { Salvage = 22, Alloy = 3, Research = 1 },
        DefaultAmmoId = "frag",
        AmmoTypes = LauncherAmmoTypes,
    };

    public static readonly WeaponDefinition Sniper = new()
    {
        Id = WeaponType.Sniper,
        FireMode = WeaponFireMode.Precision,
        Cooldown = 0.78f,
        Range = 100000f,
        EffectWidth = 10f,
        EffectDuration = 0.18f,
        StimulusRadius = 560f,
        MagazineCapacity = 5,
        ReloadDuration = 2.05f,
        BaseSpreadDegrees = 0.06f,
        SpreadPerShotDegrees = 0.18f,
        MaxSpreadDegrees = 1.35f,
        SpreadRecoveryPerSecond = 1.7f,
        MoveSpreadDegrees = 2.8f,
        RecoilKick = 1.34f,
        RecoilTwistDegrees = 4.6f,
        MaxDurability = 86f,
        DurabilityLossPerShot = 0.36f,
        RepairUnitCost = new ResourceBundle { Salvage = 4, Alloy = 1 },
        UpgradeBaseCost = new ResourceBundle { Salvage = 26, Alloy = 3, Research = 1 },
        DefaultAmmoId = "match",
        AmmoTypes = PrecisionAmmoTypes,
    };

    public static readonly WeaponDefinition Carbine = new()
    {
        Id = WeaponType.Carbine,
        FireMode = WeaponFireMode.Automatic,
        Cooldown = 0.075f,
        Range = 520f,
        EffectWidth = 4.3f,
        EffectDuration = 0.08f,
        StimulusRadius = 320f,
        MagazineCapacity = 28,
        ReloadDuration = 1.25f,
        BaseSpreadDegrees = 0.72f,
        SpreadPerShotDegrees = 0.52f,
        MaxSpreadDegrees = 5.8f,
        SpreadRecoveryPerSecond = 6.1f,
        MoveSpreadDegrees = 1.7f,
        RecoilKick = 0.78f,
        RecoilTwistDegrees = 1.8f,
        MaxDurability = 104f,
        DurabilityLossPerShot = 0.16f,
        RepairUnitCost = new ResourceBundle { Salvage = 3 },
        UpgradeBaseCost = new ResourceBundle { Salvage = 18, Alloy = 2 },
        DefaultAmmoId = "ball",
        AmmoTypes = AutomaticAmmoTypes,
    };

    public static readonly WeaponDefinition BattleRifle = new()
    {
        Id = WeaponType.BattleRifle,
        FireMode = WeaponFireMode.Automatic,
        Cooldown = 0.145f,
        Range = 760f,
        EffectWidth = 5.5f,
        EffectDuration = 0.12f,
        StimulusRadius = 410f,
        MagazineCapacity = 20,
        ReloadDuration = 1.55f,
        BaseSpreadDegrees = 0.34f,
        SpreadPerShotDegrees = 0.34f,
        MaxSpreadDegrees = 3.5f,
        SpreadRecoveryPerSecond = 4.8f,
        MoveSpreadDegrees = 1.4f,
        RecoilKick = 1.16f,
        RecoilTwistDegrees = 3.2f,
        MaxDurability = 100f,
        DurabilityLossPerShot = 0.24f,
        RepairUnitCost = new ResourceBundle { Salvage = 4, Alloy = 1 },
        UpgradeBaseCost = new ResourceBundle { Salvage = 22, Alloy = 3 },
        DefaultAmmoId = "bonded",
        AmmoTypes = AutomaticAmmoTypes,
    };

    public static readonly WeaponDefinition Smg = new()
    {
        Id = WeaponType.Smg,
        FireMode = WeaponFireMode.Automatic,
        Cooldown = 0.055f,
        Range = 420f,
        EffectWidth = 4f,
        EffectDuration = 0.07f,
        StimulusRadius = 300f,
        MagazineCapacity = 36,
        ReloadDuration = 1.12f,
        BaseSpreadDegrees = 1.2f,
        SpreadPerShotDegrees = 0.78f,
        MaxSpreadDegrees = 8.8f,
        SpreadRecoveryPerSecond = 6.8f,
        MoveSpreadDegrees = 2.8f,
        RecoilKick = 0.74f,
        RecoilTwistDegrees = 1.9f,
        MaxDurability = 98f,
        DurabilityLossPerShot = 0.14f,
        RepairUnitCost = new ResourceBundle { Salvage = 3 },
        UpgradeBaseCost = new ResourceBundle { Salvage = 16, Alloy = 2 },
        DefaultAmmoId = "hp",
        AmmoTypes = AutomaticAmmoTypes,
    };

    public static readonly WeaponDefinition Marksman = new()
    {
        Id = WeaponType.Marksman,
        FireMode = WeaponFireMode.Precision,
        Cooldown = 0.31f,
        Range = 960f,
        EffectWidth = 7f,
        EffectDuration = 0.12f,
        StimulusRadius = 430f,
        MagazineCapacity = 12,
        ReloadDuration = 1.55f,
        BaseSpreadDegrees = 0.14f,
        SpreadPerShotDegrees = 0.22f,
        MaxSpreadDegrees = 1.8f,
        SpreadRecoveryPerSecond = 2.6f,
        MoveSpreadDegrees = 1.6f,
        RecoilKick = 1f,
        RecoilTwistDegrees = 2.6f,
        MaxDurability = 96f,
        DurabilityLossPerShot = 0.26f,
        RepairUnitCost = new ResourceBundle { Salvage = 4, Alloy = 1 },
        UpgradeBaseCost = new ResourceBundle { Salvage = 20, Alloy = 3 },
        DefaultAmmoId = "overmatch",
        AmmoTypes = PrecisionAmmoTypes,
    };

    public static readonly WeaponDefinition Scout = new()
    {
        Id = WeaponType.Scout,
        FireMode = WeaponFireMode.Precision,
        Cooldown = 0.46f,
        Range = 900f,
        EffectWidth = 8f,
        EffectDuration = 0.14f,
        StimulusRadius = 360f,
        MagazineCapacity = 8,
        ReloadDuration = 1.4f,
        BaseSpreadDegrees = 0.1f,
        SpreadPerShotDegrees = 0.16f,
        MaxSpreadDegrees = 1.2f,
        SpreadRecoveryPerSecond = 2.9f,
        MoveSpreadDegrees = 1.9f,
        RecoilKick = 0.92f,
        RecoilTwistDegrees = 2.2f,
        MaxDurability = 92f,
        DurabilityLossPerShot = 0.22f,
        RepairUnitCost = new ResourceBundle { Salvage = 3, Alloy = 1 },
        UpgradeBaseCost = new ResourceBundle { Salvage = 18, Alloy = 2, Research = 1 },
        DefaultAmmoId = "match",
        AmmoTypes = PrecisionAmmoTypes,
    };

    public static readonly WeaponDefinition AntiMaterial = new()
    {
        Id = WeaponType.AntiMaterial,
        FireMode = WeaponFireMode.Precision,
        Cooldown = 1.08f,
        Range = 100000f,
        EffectWidth = 13f,
        EffectDuration = 0.2f,
        StimulusRadius = 640f,
        MagazineCapacity = 4,
        ReloadDuration = 2.75f,
        BaseSpreadDegrees = 0.02f,
        SpreadPerShotDegrees = 0.12f,
        MaxSpreadDegrees = 0.9f,
        SpreadRecoveryPerSecond = 1.45f,
        MoveSpreadDegrees = 3.2f,
        RecoilKick = 1.72f,
        RecoilTwistDegrees = 5.6f,
        MaxDurability = 80f,
        DurabilityLossPerShot = 0.48f,
        RepairUnitCost = new ResourceBundle { Salvage = 5, Alloy = 2 },
        UpgradeBaseCost = new ResourceBundle { Salvage = 28, Alloy = 4, Research = 2 },
        DefaultAmmoId = "sabot",
        AmmoTypes = PrecisionAmmoTypes,
    };

    public static readonly WeaponDefinition[] Catalog =
    {
        MachineGun,
        Carbine,
        Smg,
        BattleRifle,
        Grenade,
        Marksman,
        Scout,
        Sniper,
        AntiMaterial,
    };

    public static readonly WeaponDefinition[] DefaultLoadout = { MachineGun, Grenade };

    public static readonly WeaponType[] DefaultLoadoutIds = DefaultLoadout
        .Select(weapon => weapon.Id)
        .ToArray();

    public static readonly Dictionary<WeaponType, string> InventoryItemIdByWeaponId = Catalog
        .ToDictionary(weapon => weapon.Id, weapon => $"weapon-{weapon.Id.ToString().ToLowerInvariant()}");

    public static readonly Dictionary<WeaponType, WeaponDefinition> ById = Catalog
        .ToDictionary(weapon => weapon.Id, weapon => weapon);

    public static readonly Dictionary<string, WeaponType> WeaponIdByInventoryItemId = InventoryItemIdByWeaponId
        .ToDictionary(entry => entry.Value, entry => entry.Key);

    public static readonly Dictionary<string, WeaponAmmoDefinition> ByReserveItemId = Catalog
        .SelectMany(weapon => weapon.AmmoTypes)
        .Where(ammo => !string.IsNullOrWhiteSpace(ammo.ReserveItemId))
        .GroupBy(ammo => ammo.ReserveItemId)
        .ToDictionary(group => group.Key, group => group.First());

    public static WeaponDefinition GetDefaultWeapon()
    {
        return DefaultLoadout[0];
    }

    public static string GetInventoryItemId(WeaponType weaponId)
    {
        return InventoryItemIdByWeaponId.TryGetValue(weaponId, out var itemId)
            ? itemId
            : $"weapon-{weaponId.ToString().ToLowerInvariant()}";
    }

    public static bool TryGetWeaponIdFromInventoryItem(string itemId, out WeaponType weaponId)
    {
        return WeaponIdByInventoryItemId.TryGetValue(itemId, out weaponId);
    }

    public static WeaponAmmoDefinition GetDefaultAmmo(WeaponDefinition weapon)
    {
        if (weapon.AmmoTypes.Length == 0)
            return new WeaponAmmoDefinition();

        return weapon.AmmoTypes.FirstOrDefault(ammo => ammo.Id == weapon.DefaultAmmoId) ?? weapon.AmmoTypes[0];
    }

    public static WeaponAmmoDefinition GetAmmo(WeaponDefinition weapon, string ammoId)
    {
        return weapon.AmmoTypes.FirstOrDefault(ammo => ammo.Id == ammoId) ?? GetDefaultAmmo(weapon);
    }

    public static WeaponAmmoDefinition GetNextAmmo(WeaponDefinition weapon, string currentAmmoId)
    {
        if (weapon.AmmoTypes.Length == 0)
            return new WeaponAmmoDefinition();

        int currentIndex = System.Array.FindIndex(weapon.AmmoTypes, ammo => ammo.Id == currentAmmoId);
        if (currentIndex < 0)
            currentIndex = 0;

        return weapon.AmmoTypes[(currentIndex + 1) % weapon.AmmoTypes.Length];
    }

    public static WeaponAmmoDefinition? FindAmmoByReserveItem(string itemId)
    {
        return ByReserveItemId.TryGetValue(itemId, out var ammo)
            ? ammo
            : null;
    }
}
