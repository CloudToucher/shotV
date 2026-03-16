using System;
using System.Collections.Generic;
using System.Linq;
using ShotV.Data;
using ShotV.State;

namespace ShotV.Inventory;

public struct PlacementResult
{
    public bool Placed;
    public List<InventoryItemRecord> Items;
}

public struct BatchPlacementResult
{
    public List<InventoryItemRecord> Items;
    public List<string> PlacedIds;
    public List<InventoryItemRecord> Rejected;
}

public static class GridInventory
{
    public static InventoryItemRecord? CreateItemRecord(string itemId, int quantity, string? id = null)
    {
        if (!ItemData.ById.TryGetValue(itemId, out var def)) return null;
        id ??= BuildItemId(itemId);
        return new InventoryItemRecord
        {
            Id = id, ItemId = itemId,
            Quantity = Math.Max(1, Math.Min(def.MaxStack, quantity)),
            Width = def.Width, Height = def.Height,
        };
    }

    public static PlacementResult PlaceItemInGrid(int cols, int rows, List<InventoryItemRecord> items, InventoryItemRecord incoming)
    {
        var pos = FindFirstPlacement(cols, rows, items, incoming);
        if (pos == null)
            return new PlacementResult { Placed = false, Items = CloneItems(items) };

        var result = CloneItems(items);
        var placed = incoming.Clone();
        placed.X = pos.Value.x;
        placed.Y = pos.Value.y;
        result.Add(placed);
        return new PlacementResult { Placed = true, Items = result };
    }

    public static PlacementResult PlaceItemAtPosition(int cols, int rows, List<InventoryItemRecord> items, InventoryItemRecord incoming, int x, int y)
    {
        if (!CanPlaceItemAtPosition(cols, rows, items, incoming, x, y))
            return new PlacementResult { Placed = false, Items = CloneItems(items) };

        var result = CloneItems(items);
        var placed = incoming.Clone();
        placed.X = x;
        placed.Y = y;
        result.Add(placed);
        return new PlacementResult { Placed = true, Items = result };
    }

    public static BatchPlacementResult PlaceItemsInGrid(int cols, int rows, List<InventoryItemRecord> existing, List<InventoryItemRecord> incoming)
    {
        var items = CloneItems(existing);
        var placedIds = new List<string>();
        var rejected = new List<InventoryItemRecord>();

        var sorted = incoming.OrderByDescending(i => i.Width * i.Height).ThenByDescending(i => i.Quantity).ToList();
        foreach (var inc in sorted)
        {
            var result = PlaceItemInGrid(cols, rows, items, inc);
            if (!result.Placed)
            {
                rejected.Add(inc.Clone());
                continue;
            }
            items = result.Items;
            placedIds.Add(inc.Id);
        }

        return new BatchPlacementResult { Items = items, PlacedIds = placedIds, Rejected = rejected };
    }

    public static bool CanPlaceItemAtPosition(int cols, int rows, List<InventoryItemRecord> items, InventoryItemRecord incoming, int x, int y)
    {
        if (x < 0 || y < 0) return false;
        if (x + incoming.Width > cols || y + incoming.Height > rows) return false;
        return !IntersectsAny(items, x, y, incoming.Width, incoming.Height);
    }

    public static InventoryItemRecord? FindItemAtCell(List<InventoryItemRecord> items, int x, int y)
    {
        return items.FirstOrDefault(i => x >= i.X && x < i.X + i.Width && y >= i.Y && y < i.Y + i.Height);
    }

    public static (InventoryItemRecord? Item, List<InventoryItemRecord> Items) PickItemFromGridAtCell(List<InventoryItemRecord> items, int x, int y)
    {
        var target = FindItemAtCell(items, x, y);
        if (target == null)
            return (null, CloneItems(items));

        return (target.Clone(), items.Where(item => item.Id != target.Id).Select(item => item.Clone()).ToList());
    }

    public static InventoryItemRecord RotateItem(InventoryItemRecord item)
    {
        var rotated = item.Clone();
        rotated.Width = item.Height;
        rotated.Height = item.Width;
        rotated.Rotated = !item.Rotated;
        return rotated;
    }

    public static ResourceBundle BuildResourceLedgerFromItems(List<InventoryItemRecord> items)
    {
        var ledger = ResourceBundle.Zero();
        foreach (var item in items)
        {
            if (!ItemData.ById.TryGetValue(item.ItemId, out var def)) continue;
            ledger.Salvage += def.RecoveredResources.Salvage * item.Quantity;
            ledger.Alloy += def.RecoveredResources.Alloy * item.Quantity;
            ledger.Research += def.RecoveredResources.Research * item.Quantity;
        }
        return ledger;
    }

    public static List<InventoryItemRecord> AutoArrange(int cols, int rows, List<InventoryItemRecord> items)
    {
        var ordered = items.Select(i => { var c = i.Clone(); c.X = 0; c.Y = 0; return c; })
            .OrderByDescending(i => i.Width * i.Height).ThenByDescending(i => i.Quantity).ToList();
        var occupied = new bool[cols * rows];
        var result = SolveArrangement(cols, rows, occupied, ordered, new List<InventoryItemRecord>());
        return result ?? CloneItems(items);
    }

    public static string?[] SanitizeQuickSlots(string?[] slots, IEnumerable<string> validIds)
    {
        var valid = new HashSet<string>(validIds);
        var seen = new HashSet<string>();
        var result = new string?[GridInventoryState.RunQuickSlotCount];
        for (int i = 0; i < result.Length; i++)
        {
            var id = i < slots.Length ? slots[i] : null;
            if (id != null && valid.Contains(id) && !seen.Contains(id))
            {
                seen.Add(id);
                result[i] = id;
            }
        }
        return result;
    }

    public static string?[] AssignQuickSlotBinding(string?[] slots, int slotIndex, string? itemId)
    {
        var result = new string?[GridInventoryState.RunQuickSlotCount];
        for (int index = 0; index < result.Length; index++)
            result[index] = index < slots.Length ? slots[index] : null;

        if (slotIndex < 0 || slotIndex >= result.Length)
            return result;

        bool wasSameBinding = itemId != null && result[slotIndex] == itemId;
        for (int index = 0; index < result.Length; index++)
        {
            if (result[index] == itemId)
                result[index] = null;
        }

        result[slotIndex] = wasSameBinding ? null : itemId;
        return result;
    }

    public static List<InventoryItemRecord> CloneItems(List<InventoryItemRecord> items)
        => items.Select(i => i.Clone()).ToList();

    private static (int x, int y)? FindFirstPlacement(int cols, int rows, List<InventoryItemRecord> items, InventoryItemRecord incoming)
    {
        for (int y = 0; y <= rows - incoming.Height; y++)
            for (int x = 0; x <= cols - incoming.Width; x++)
                if (!IntersectsAny(items, x, y, incoming.Width, incoming.Height))
                    return (x, y);
        return null;
    }

    private static bool IntersectsAny(List<InventoryItemRecord> items, int x, int y, int w, int h)
    {
        foreach (var item in items)
        {
            if (item.X < x + w && item.X + item.Width > x && item.Y < y + h && item.Y + item.Height > y)
                return true;
        }
        return false;
    }

    private static List<InventoryItemRecord>? SolveArrangement(int cols, int rows, bool[] occupied, List<InventoryItemRecord> remaining, List<InventoryItemRecord> placed)
    {
        if (remaining.Count == 0) return placed.Select(i => i.Clone()).ToList();

        int anchorIdx = Array.IndexOf(occupied, false);
        if (anchorIdx == -1) return null;
        int ax = anchorIdx % cols;
        int ay = anchorIdx / cols;
        var attempted = new HashSet<string>();

        for (int i = 0; i < remaining.Count; i++)
        {
            var item = remaining[i];
            var variants = new List<InventoryItemRecord> { item };
            if (item.Width != item.Height) variants.Add(RotateItem(item));

            foreach (var variant in variants)
            {
                string sig = $"{variant.ItemId}:{variant.Quantity}:{variant.Width}x{variant.Height}:{variant.Rotated}";
                if (attempted.Contains(sig)) continue;
                attempted.Add(sig);

                if (!CanPlaceOnOccupied(cols, rows, occupied, variant, ax, ay)) continue;

                MarkCells(cols, occupied, variant, ax, ay, true);
                var nextRemaining = remaining.Take(i).Concat(remaining.Skip(i + 1)).ToList();
                var nextPlaced = new List<InventoryItemRecord>(placed) { new() { Id = variant.Id, ItemId = variant.ItemId, Quantity = variant.Quantity, X = ax, Y = ay, Width = variant.Width, Height = variant.Height, Rotated = variant.Rotated } };
                var result = SolveArrangement(cols, rows, occupied, nextRemaining, nextPlaced);
                MarkCells(cols, occupied, variant, ax, ay, false);
                if (result != null) return result;
            }
        }
        return null;
    }

    private static bool CanPlaceOnOccupied(int cols, int rows, bool[] occupied, InventoryItemRecord item, int x, int y)
    {
        if (x + item.Width > cols || y + item.Height > rows) return false;
        for (int row = y; row < y + item.Height; row++)
            for (int col = x; col < x + item.Width; col++)
                if (occupied[row * cols + col]) return false;
        return true;
    }

    private static void MarkCells(int cols, bool[] occupied, InventoryItemRecord item, int x, int y, bool value)
    {
        for (int row = y; row < y + item.Height; row++)
            for (int col = x; col < x + item.Width; col++)
                occupied[row * cols + col] = value;
    }

    private static string BuildItemId(string itemId)
        => $"item-{itemId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}-{Guid.NewGuid().ToString()[..8]}";
}
