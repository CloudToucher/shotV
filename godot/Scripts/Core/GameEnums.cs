namespace ShotV.Core;

public enum GameMode { Base, Combat }

public enum WeaponType
{
    MachineGun,
    Grenade,
    Sniper,
    Carbine,
    BattleRifle,
    Smg,
    Marksman,
    Scout,
    AntiMaterial,
}

public enum WeaponSlot { Slot1 = 1, Slot2 = 2, Slot3 = 3 }

public enum WeaponFireMode { Automatic, Launcher, Precision }

public enum HostileType { Melee, Ranged, Charger, Stalker, Suppressor, Boss }

public enum HostileMode { Advance, Aim, Windup, Charge, Recover }

public enum ItemCategory { Resource, Intel, Boss, Consumable, Ammo, Weapon }

public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

public enum WorldZoneKind { Perimeter, HighRisk, HighValue, Extraction }

public enum RunStateStatus { Active, AwaitingSettlement }

public enum RunResolutionOutcome { Extracted, BossClear, Down }

public enum RunZoneStatus { Locked, Active, Cleared }

public enum EncounterState { Active, Down, Clear }

public enum LootSource { Enemy, Boss, Wave, Manual }

public enum MarkerKind { Entry, Objective, Extraction, Locker, Station }

public enum ObstacleKind { Wall, Cover, Locker, Station }

public enum BossPattern { Fan, Nova, Lance, Spiral }
