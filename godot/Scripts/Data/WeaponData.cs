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
    public WeaponSlot Slot { get; init; }
    public WeaponType Id { get; init; }
    public string Label => GameText.Text($"weapon.{Id}.label");
    public string Hint => GameText.Text($"weapon.{Id}.hint");
    public float Cooldown { get; init; }
    public float Range { get; init; }
    public float EffectWidth { get; init; }
    public float EffectDuration { get; init; }
    public float SplashRadius { get; init; }
    public int MagazineCapacity { get; init; }
    public float ReloadDuration { get; init; }
    public float BaseSpreadDegrees { get; init; }
    public float SpreadPerShotDegrees { get; init; }
    public float MaxSpreadDegrees { get; init; }
    public float SpreadRecoveryPerSecond { get; init; }
    public float MoveSpreadDegrees { get; init; }
    public float RecoilKick { get; init; }
    public float RecoilTwistDegrees { get; init; }
    public WeaponAmmoDefinition[] AmmoTypes { get; init; } = System.Array.Empty<WeaponAmmoDefinition>();
    public string DefaultAmmoId { get; init; } = "";
}

public static class WeaponData
{
    public static readonly WeaponDefinition MachineGun = new()
    {
        Slot = WeaponSlot.Slot1,
        Id = WeaponType.MachineGun,
        Cooldown = 0.085f,
        Range = 560f,
        EffectWidth = 4.5f,
        EffectDuration = 0.09f,
        MagazineCapacity = 30,
        ReloadDuration = 1.45f,
        BaseSpreadDegrees = 0.9f,
        SpreadPerShotDegrees = 0.65f,
        MaxSpreadDegrees = 7.8f,
        SpreadRecoveryPerSecond = 5.4f,
        MoveSpreadDegrees = 2.2f,
        RecoilKick = 1f,
        RecoilTwistDegrees = 2.4f,
        DefaultAmmoId = "ball",
        AmmoTypes = new[]
        {
            new WeaponAmmoDefinition
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
            new WeaponAmmoDefinition
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
            new WeaponAmmoDefinition
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
            new WeaponAmmoDefinition
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
            new WeaponAmmoDefinition
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
        },
    };

    public static readonly WeaponDefinition Grenade = new()
    {
        Slot = WeaponSlot.Slot2,
        Id = WeaponType.Grenade,
        Cooldown = 0.42f,
        Range = 360f,
        SplashRadius = 66f,
        MagazineCapacity = 6,
        ReloadDuration = 2.2f,
        BaseSpreadDegrees = 0.3f,
        SpreadPerShotDegrees = 0.42f,
        MaxSpreadDegrees = 2.1f,
        SpreadRecoveryPerSecond = 2.6f,
        MoveSpreadDegrees = 0.85f,
        RecoilKick = 0.82f,
        RecoilTwistDegrees = 1.6f,
        DefaultAmmoId = "frag",
        AmmoTypes = new[]
        {
            new WeaponAmmoDefinition
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
            new WeaponAmmoDefinition
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
            new WeaponAmmoDefinition
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
            new WeaponAmmoDefinition
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
            new WeaponAmmoDefinition
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
        },
    };

    public static readonly WeaponDefinition Sniper = new()
    {
        Slot = WeaponSlot.Slot3,
        Id = WeaponType.Sniper,
        Cooldown = 0.72f,
        Range = 100000f,
        EffectWidth = 9f,
        EffectDuration = 0.16f,
        MagazineCapacity = 5,
        ReloadDuration = 1.9f,
        BaseSpreadDegrees = 0.06f,
        SpreadPerShotDegrees = 0.18f,
        MaxSpreadDegrees = 1.4f,
        SpreadRecoveryPerSecond = 1.8f,
        MoveSpreadDegrees = 2.6f,
        RecoilKick = 1.28f,
        RecoilTwistDegrees = 4.2f,
        DefaultAmmoId = "match",
        AmmoTypes = new[]
        {
            new WeaponAmmoDefinition
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
            new WeaponAmmoDefinition
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
            new WeaponAmmoDefinition
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
            new WeaponAmmoDefinition
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
            new WeaponAmmoDefinition
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
        },
    };

    public static readonly WeaponDefinition[] Loadout = { MachineGun, Grenade, Sniper };

    public static readonly Dictionary<WeaponSlot, WeaponDefinition> BySlot = new()
    {
        { WeaponSlot.Slot1, MachineGun },
        { WeaponSlot.Slot2, Grenade },
        { WeaponSlot.Slot3, Sniper },
    };

    public static readonly Dictionary<WeaponType, WeaponDefinition> ById = new()
    {
        { WeaponType.MachineGun, MachineGun },
        { WeaponType.Grenade, Grenade },
        { WeaponType.Sniper, Sniper },
    };

    public static readonly Dictionary<string, WeaponAmmoDefinition> ByReserveItemId = Loadout
        .SelectMany(weapon => weapon.AmmoTypes)
        .Where(ammo => !string.IsNullOrWhiteSpace(ammo.ReserveItemId))
        .ToDictionary(ammo => ammo.ReserveItemId, ammo => ammo);

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
