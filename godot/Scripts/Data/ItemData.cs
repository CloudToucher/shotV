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
    public string Label => GameText.Text($"item.{Id}.label");
    public string ShortLabel => GameText.Text($"item.{Id}.short");
    public string Description => GameText.Text($"item.{Id}.description");
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
