using System.Collections.Generic;
using ShotV.Core;

namespace ShotV.Data;

public class WeaponDefinition
{
    public WeaponSlot Slot { get; init; }
    public WeaponType Id { get; init; }
    public string Label { get; init; } = "";
    public string Hint { get; init; } = "";
    public float Cooldown { get; init; }
    public float Range { get; init; }
    public float EffectWidth { get; init; }
    public float EffectDuration { get; init; }
    public float SplashRadius { get; init; }
}

public static class WeaponData
{
    public static readonly WeaponDefinition MachineGun = new()
    {
        Slot = WeaponSlot.Slot1,
        Id = WeaponType.MachineGun,
        Label = "机枪",
        Hint = "中距离压制主武器，射速高，持续火力稳定。",
        Cooldown = 0.085f,
        Range = 560f,
        EffectWidth = 4.5f,
        EffectDuration = 0.09f,
    };

    public static readonly WeaponDefinition Grenade = new()
    {
        Slot = WeaponSlot.Slot2,
        Id = WeaponType.Grenade,
        Label = "榴弹",
        Hint = "短抛物线投射，落点爆炸，适合清理密集敌群。",
        Cooldown = 0.46f,
        Range = 360f,
        EffectWidth = 0f,
        EffectDuration = 0f,
        SplashRadius = 66f,
    };

    public static readonly WeaponDefinition Sniper = new()
    {
        Slot = WeaponSlot.Slot3,
        Id = WeaponType.Sniper,
        Label = "狙击",
        Hint = "高穿透精确射击，单线爆发高，适合点杀目标。",
        Cooldown = 0.72f,
        Range = 100000f,
        EffectWidth = 9f,
        EffectDuration = 0.16f,
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
}
