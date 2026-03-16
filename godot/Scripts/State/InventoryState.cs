using System.Collections.Generic;
using System.Linq;
using ShotV.Core;

namespace ShotV.State;

public class InventoryItemRecord
{
    public string Id { get; set; } = "";
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 1;
    public bool Rotated { get; set; }

    public InventoryItemRecord Clone() => new()
    {
        Id = Id, ItemId = ItemId, Quantity = Quantity,
        X = X, Y = Y, Width = Width, Height = Height, Rotated = Rotated,
    };
}

public class GridInventoryState
{
    public int Columns { get; set; } = 6;
    public int Rows { get; set; } = 4;
    public List<InventoryItemRecord> Items { get; set; } = new();
    public string?[] QuickSlots { get; set; } = new string?[RunQuickSlotCount];

    public const int RunQuickSlotCount = 4;

    public GridInventoryState Clone() => new()
    {
        Columns = Columns, Rows = Rows,
        Items = Items.Select(i => i.Clone()).ToList(),
        QuickSlots = (string?[])QuickSlots.Clone(),
    };
}

public class InventoryState
{
    public int StashColumns { get; set; } = 8;
    public int StashRows { get; set; } = 6;
    public List<WeaponType> EquippedWeaponIds { get; set; } = new() { WeaponType.MachineGun, WeaponType.Grenade, WeaponType.Sniper };
    public string? EquippedArmorId { get; set; }
    public List<InventoryItemRecord> StoredItems { get; set; } = new();

    public InventoryState Clone() => new()
    {
        StashColumns = StashColumns, StashRows = StashRows,
        EquippedWeaponIds = new List<WeaponType>(EquippedWeaponIds),
        EquippedArmorId = EquippedArmorId,
        StoredItems = StoredItems.Select(i => i.Clone()).ToList(),
    };
}
