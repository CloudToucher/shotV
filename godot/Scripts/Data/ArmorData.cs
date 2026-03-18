using System.Collections.Generic;
using Godot;
using ShotV.Core;

namespace ShotV.Data;

public class ArmorDefinition
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Hint { get; init; } = "";
    public int ArmorLevel { get; init; }
    public float Mitigation { get; init; }
    public float MaxDurability { get; init; } = 100f;
    public float MaxHealthBonus { get; init; }
    public ResourceBundle RepairUnitCost { get; init; } = ResourceBundle.Zero();
    public ResourceBundle UpgradeBaseCost { get; init; } = ResourceBundle.Zero();
    public Color Tint { get; init; } = Palette.UiActive;
}

public static class ArmorData
{
    public const string DefaultArmorId = "scout-rig";

    public static readonly ArmorDefinition[] Catalog =
    {
        new()
        {
            Id = DefaultArmorId,
            Label = "侦察护甲",
            Hint = "轻型护甲，耐久恢复成本低，适合长线探索和快速撤离。",
            ArmorLevel = 1,
            Mitigation = 0.14f,
            MaxDurability = 92f,
            MaxHealthBonus = 0f,
            RepairUnitCost = new ResourceBundle { Salvage = 4 },
            UpgradeBaseCost = new ResourceBundle { Salvage = 18, Alloy = 2 },
            Tint = Palette.Dash,
        },
        new()
        {
            Id = "assault-rig",
            Label = "突击护甲",
            Hint = "中型护甲，兼顾耐久和减伤，适合作为默认战区装甲。",
            ArmorLevel = 2,
            Mitigation = 0.23f,
            MaxDurability = 112f,
            MaxHealthBonus = 12f,
            RepairUnitCost = new ResourceBundle { Salvage = 5, Alloy = 1 },
            UpgradeBaseCost = new ResourceBundle { Salvage = 24, Alloy = 4, Research = 1 },
            Tint = Palette.Frame,
        },
        new()
        {
            Id = "bulwark-rig",
            Label = "堡垒护甲",
            Hint = "重型护甲，容错最高，维修更贵，更适合清剿高危区域和 boss 战。",
            ArmorLevel = 3,
            Mitigation = 0.31f,
            MaxDurability = 136f,
            MaxHealthBonus = 24f,
            RepairUnitCost = new ResourceBundle { Salvage = 6, Alloy = 2 },
            UpgradeBaseCost = new ResourceBundle { Salvage = 30, Alloy = 6, Research = 2 },
            Tint = Palette.Warning,
        },
    };

    public static readonly Dictionary<string, ArmorDefinition> ById = BuildById();

    private static Dictionary<string, ArmorDefinition> BuildById()
    {
        var result = new Dictionary<string, ArmorDefinition>();
        foreach (var armor in Catalog)
            result[armor.Id] = armor;
        return result;
    }
}
