using ShotV.Core;

namespace ShotV.State;

public class WeaponBenchState
{
    public WeaponType WeaponId { get; set; }
    public float Durability { get; set; } = 100f;
    public float MaxDurability { get; set; } = 100f;
    public int UpgradeLevel { get; set; }

    public WeaponBenchState Clone() => new()
    {
        WeaponId = WeaponId,
        Durability = Durability,
        MaxDurability = MaxDurability,
        UpgradeLevel = UpgradeLevel,
    };
}

public class ArmorBenchState
{
    public string ArmorId { get; set; } = "";
    public float Durability { get; set; } = 100f;
    public float MaxDurability { get; set; } = 100f;
    public int UpgradeLevel { get; set; }

    public ArmorBenchState Clone() => new()
    {
        ArmorId = ArmorId,
        Durability = Durability,
        MaxDurability = MaxDurability,
        UpgradeLevel = UpgradeLevel,
    };
}
