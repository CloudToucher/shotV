using System.Collections.Generic;
using Godot;
using ShotV.Core;

namespace ShotV.Data;

public class ResourceBundle
{
    public int Salvage { get; set; }
    public int Alloy { get; set; }
    public int Research { get; set; }

    public ResourceBundle Clone() => new() { Salvage = Salvage, Alloy = Alloy, Research = Research };

    public static ResourceBundle Zero() => new() { Salvage = 0, Alloy = 0, Research = 0 };

    public void Add(ResourceBundle other)
    {
        Salvage += other.Salvage;
        Alloy += other.Alloy;
        Research += other.Research;
    }
}

public class ItemUse
{
    public float Heals { get; init; }
    public float ExplosionDamage { get; init; }
    public float ExplosionRadius { get; init; }
    public bool RefreshDash { get; init; }
}

public class ItemDefinition
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string ShortLabel { get; init; } = "";
    public string Description { get; init; } = "";
    public ItemCategory Category { get; init; }
    public ItemRarity Rarity { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int MaxStack { get; init; }
    public Color Tint { get; init; }
    public Color AccentColor { get; init; }
    public ResourceBundle RecoveredResources { get; init; } = ResourceBundle.Zero();
    public ItemUse? Use { get; init; }
}

public static class ItemData
{
    public static readonly ItemDefinition[] Catalog =
    {
        new()
        {
            Id = "salvage-scrap", Label = "废料包", ShortLabel = "废料",
            Description = "基础回收物。占位小，但会快速堆满背包。",
            Category = ItemCategory.Resource, Rarity = ItemRarity.Common,
            Width = 1, Height = 1, MaxStack = 8,
            Tint = Palette.PanelWarm, AccentColor = Palette.Accent,
            RecoveredResources = new ResourceBundle { Salvage = 1 },
        },
        new()
        {
            Id = "telemetry-cache", Label = "遥测数据", ShortLabel = "遥测",
            Description = "可转化为研究进度的数据缓存。",
            Category = ItemCategory.Intel, Rarity = ItemRarity.Rare,
            Width = 1, Height = 2, MaxStack = 4,
            Tint = Palette.Frame, AccentColor = Palette.Dash,
            RecoveredResources = new ResourceBundle { Research = 1 },
        },
        new()
        {
            Id = "alloy-plate", Label = "合金板", ShortLabel = "合金",
            Description = "中型结构材料，占位更长，但能补充稀缺合金。",
            Category = ItemCategory.Resource, Rarity = ItemRarity.Uncommon,
            Width = 2, Height = 1, MaxStack = 4,
            Tint = Palette.MinimapMarker, AccentColor = Palette.MinimapMarker,
            RecoveredResources = new ResourceBundle { Alloy = 1 },
        },
        new()
        {
            Id = "aegis-core", Label = "主核残片", ShortLabel = "主核",
            Description = "高价值主核残片，体积大但回收价值极高。",
            Category = ItemCategory.Boss, Rarity = ItemRarity.Legendary,
            Width = 2, Height = 2, MaxStack = 1,
            Tint = Palette.Danger, AccentColor = Palette.Warning,
            RecoveredResources = new ResourceBundle { Salvage = 24, Alloy = 8, Research = 6 },
        },
        new()
        {
            Id = "med-injector", Label = "治疗针", ShortLabel = "治疗针",
            Description = "战区常见的单次应急治疗剂，可快速恢复生命。",
            Category = ItemCategory.Consumable, Rarity = ItemRarity.Common,
            Width = 1, Height = 2, MaxStack = 3,
            Tint = Palette.MinimapMarker, AccentColor = Palette.Frame,
            Use = new ItemUse { Heals = 28f },
        },
        new()
        {
            Id = "field-kit", Label = "战地急救包", ShortLabel = "急救包",
            Description = "占位更大，但能一次性恢复更多生命。",
            Category = ItemCategory.Consumable, Rarity = ItemRarity.Uncommon,
            Width = 2, Height = 2, MaxStack = 1,
            Tint = Palette.Frame, AccentColor = Palette.MinimapMarker,
            Use = new ItemUse { Heals = 56f },
        },
        new()
        {
            Id = "shock-charge", Label = "震爆罐", ShortLabel = "震爆",
            Description = "以自身为中心释放高压震爆，适合清开近身敌人。",
            Category = ItemCategory.Consumable, Rarity = ItemRarity.Rare,
            Width = 2, Height = 1, MaxStack = 2,
            Tint = Palette.Warning, AccentColor = Palette.Danger,
            Use = new ItemUse { ExplosionDamage = 46f, ExplosionRadius = 132f },
        },
        new()
        {
            Id = "dash-cell", Label = "机动电池", ShortLabel = "机动",
            Description = "重置冲刺冷却并立即补一段机动脉冲。",
            Category = ItemCategory.Consumable, Rarity = ItemRarity.Rare,
            Width = 1, Height = 1, MaxStack = 2,
            Tint = Palette.Dash, AccentColor = Palette.AccentSoft,
            Use = new ItemUse { RefreshDash = true },
        },
    };

    public static readonly Dictionary<string, ItemDefinition> ById;

    static ItemData()
    {
        ById = new Dictionary<string, ItemDefinition>();
        foreach (var item in Catalog)
            ById[item.Id] = item;
    }
}
