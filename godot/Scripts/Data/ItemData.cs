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
    public ResourceBundle? CraftCost { get; init; }
    public ItemUse? Use { get; init; }
}

public static class ItemData
{
    public static readonly ItemDefinition[] Catalog =
    {
        new()
        {
            Id = "salvage-scrap",
            Label = "废料块",
            ShortLabel = "废料",
            Description = "基础回收件，占位小但容易塞满背包。",
            Category = ItemCategory.Resource,
            Rarity = ItemRarity.Common,
            Width = 1,
            Height = 1,
            MaxStack = 8,
            Tint = Palette.PanelWarm,
            AccentColor = Palette.Accent,
            RecoveredResources = new ResourceBundle { Salvage = 1 },
        },
        new()
        {
            Id = "telemetry-cache",
            Label = "遥测数据",
            ShortLabel = "遥测",
            Description = "可转换为研究进度的数据缓存。",
            Category = ItemCategory.Intel,
            Rarity = ItemRarity.Rare,
            Width = 1,
            Height = 2,
            MaxStack = 4,
            Tint = Palette.Frame,
            AccentColor = Palette.Dash,
            RecoveredResources = new ResourceBundle { Research = 1 },
        },
        new()
        {
            Id = "alloy-plate",
            Label = "合金板",
            ShortLabel = "合金",
            Description = "中型结构材料，回收后补充合金库存。",
            Category = ItemCategory.Resource,
            Rarity = ItemRarity.Uncommon,
            Width = 2,
            Height = 1,
            MaxStack = 4,
            Tint = Palette.MinimapMarker,
            AccentColor = Palette.MinimapMarker,
            RecoveredResources = new ResourceBundle { Alloy = 1 },
        },
        new()
        {
            Id = "aegis-core",
            Label = "主核残片",
            ShortLabel = "主核",
            Description = "高价值主核残片，体积大但回收价值极高。",
            Category = ItemCategory.Boss,
            Rarity = ItemRarity.Legendary,
            Width = 2,
            Height = 2,
            MaxStack = 1,
            Tint = Palette.Danger,
            AccentColor = Palette.Warning,
            RecoveredResources = new ResourceBundle { Salvage = 24, Alloy = 8, Research = 6 },
        },
        new()
        {
            Id = "ammo-mg-ball",
            Label = "机枪标准弹",
            ShortLabel = "BALL",
            Description = "机枪 BALL 备用弹药。60 发封顶一格。",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Common,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.AccentSoft,
            AccentColor = Palette.Accent,
        },
        new()
        {
            Id = "ammo-mg-ap",
            Label = "机枪穿甲弹",
            ShortLabel = "AP",
            Description = "机枪 AP 备用弹药。60 发封顶一格。",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Uncommon,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.Frame,
            AccentColor = Palette.FrameSoft,
        },
        new()
        {
            Id = "ammo-mg-hp",
            Label = "机枪高损弹",
            ShortLabel = "HP",
            Description = "机枪 HP 备用弹药。60 发封顶一格。",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Uncommon,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.Danger,
            AccentColor = Palette.AccentSoft,
        },
        new()
        {
            Id = "ammo-mg-tracer",
            Label = "MG Tracer",
            ShortLabel = "TRACER",
            Description = "Machine gun TRACER reserve ammo. 60 rounds per stack.",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Common,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.Warning,
            AccentColor = Palette.Accent,
        },
        new()
        {
            Id = "ammo-mg-bonded",
            Label = "MG Bonded",
            ShortLabel = "BONDED",
            Description = "Machine gun BONDED reserve ammo. 60 rounds per stack.",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Uncommon,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.FrameSoft,
            AccentColor = Palette.Frame,
        },
        new()
        {
            Id = "ammo-gl-frag",
            Label = "破片榴弹",
            ShortLabel = "FRAG",
            Description = "榴弹 FRAG 备用弹药。60 发封顶一格。",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Common,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.Warning,
            AccentColor = Palette.Accent,
        },
        new()
        {
            Id = "ammo-gl-breach",
            Label = "破甲榴弹",
            ShortLabel = "BREACH",
            Description = "榴弹 BREACH 备用弹药。60 发封顶一格。",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Rare,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.Frame,
            AccentColor = Palette.Warning,
        },
        new()
        {
            Id = "ammo-gl-arc",
            Label = "电弧榴弹",
            ShortLabel = "ARC",
            Description = "榴弹 ARC 备用弹药。60 发封顶一格。",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Rare,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.Dash,
            AccentColor = Palette.Frame,
        },
        new()
        {
            Id = "ammo-gl-blast",
            Label = "GL Blast",
            ShortLabel = "BLAST",
            Description = "Grenade launcher BLAST reserve ammo. 60 rounds per stack.",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Uncommon,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.Warning,
            AccentColor = Palette.Danger,
        },
        new()
        {
            Id = "ammo-gl-flechette",
            Label = "GL Flechette",
            ShortLabel = "FLECH",
            Description = "Grenade launcher FLECH reserve ammo. 60 rounds per stack.",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Rare,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.UiActive,
            AccentColor = Palette.Warning,
        },
        new()
        {
            Id = "ammo-sn-match",
            Label = "狙击精确弹",
            ShortLabel = "MATCH",
            Description = "狙击 MATCH 备用弹药。60 发封顶一格。",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Common,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.UiActive,
            AccentColor = Palette.Frame,
        },
        new()
        {
            Id = "ammo-sn-sabot",
            Label = "狙击脱壳弹",
            ShortLabel = "SABOT",
            Description = "狙击 SABOT 备用弹药。60 发封顶一格。",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Rare,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.FrameSoft,
            AccentColor = Palette.Frame,
        },
        new()
        {
            Id = "ammo-sn-exp",
            Label = "狙击高爆弹",
            ShortLabel = "EXP",
            Description = "狙击 EXP 备用弹药。60 发封顶一格。",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Rare,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.Danger,
            AccentColor = Palette.Warning,
        },
        new()
        {
            Id = "ammo-sn-overmatch",
            Label = "SN Overmatch",
            ShortLabel = "OVRMCH",
            Description = "Sniper OVRMCH reserve ammo. 60 rounds per stack.",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Uncommon,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.Frame,
            AccentColor = Palette.UiActive,
        },
        new()
        {
            Id = "ammo-sn-rupture",
            Label = "SN Rupture",
            ShortLabel = "RUPTURE",
            Description = "Sniper RUPTURE reserve ammo. 60 rounds per stack.",
            Category = ItemCategory.Ammo,
            Rarity = ItemRarity.Rare,
            Width = 1,
            Height = 1,
            MaxStack = 60,
            Tint = Palette.Danger,
            AccentColor = Palette.Accent,
        },
        new()
        {
            Id = "med-injector",
            Label = "治疗针",
            ShortLabel = "治疗针",
            Description = "战区常见的单次应急治疗剂，可快速恢复生命。",
            Category = ItemCategory.Consumable,
            Rarity = ItemRarity.Common,
            Width = 1,
            Height = 2,
            MaxStack = 3,
            Tint = Palette.MinimapMarker,
            AccentColor = Palette.Frame,
            CraftCost = new ResourceBundle { Salvage = 8, Alloy = 1 },
            Use = new ItemUse { Heals = 28f },
        },
        new()
        {
            Id = "field-kit",
            Label = "战地急救包",
            ShortLabel = "急救包",
            Description = "占位更大，但能一次性恢复更多生命。",
            Category = ItemCategory.Consumable,
            Rarity = ItemRarity.Uncommon,
            Width = 2,
            Height = 2,
            MaxStack = 1,
            Tint = Palette.Frame,
            AccentColor = Palette.MinimapMarker,
            CraftCost = new ResourceBundle { Salvage = 14, Alloy = 2 },
            Use = new ItemUse { Heals = 56f },
        },
        new()
        {
            Id = "shock-charge",
            Label = "震爆罐",
            ShortLabel = "震爆",
            Description = "以自身为中心释放高压震爆，适合清开近身敌人。",
            Category = ItemCategory.Consumable,
            Rarity = ItemRarity.Rare,
            Width = 2,
            Height = 1,
            MaxStack = 2,
            Tint = Palette.Warning,
            AccentColor = Palette.Danger,
            CraftCost = new ResourceBundle { Salvage = 12, Alloy = 2, Research = 1 },
            Use = new ItemUse { ExplosionDamage = 46f, ExplosionRadius = 132f },
        },
        new()
        {
            Id = "dash-cell",
            Label = "机动电池",
            ShortLabel = "机动",
            Description = "重置冲刺冷却并立刻补上一段机动脉冲。",
            Category = ItemCategory.Consumable,
            Rarity = ItemRarity.Rare,
            Width = 1,
            Height = 1,
            MaxStack = 2,
            Tint = Palette.Dash,
            AccentColor = Palette.AccentSoft,
            CraftCost = new ResourceBundle { Salvage = 6, Alloy = 1, Research = 1 },
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
