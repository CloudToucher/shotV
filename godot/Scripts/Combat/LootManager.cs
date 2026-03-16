using System;
using System.Collections.Generic;
using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.Inventory;
using ShotV.State;

namespace ShotV.Combat;

public static class LootManager
{
    private static readonly Random _rng = new();

    private static readonly (string itemId, float weight)[] EnemyLootTable =
    {
        ("salvage-scrap", 0.55f),
        ("med-injector", 0.12f),
        ("shock-charge", 0.06f),
        ("dash-cell", 0.05f),
    };

    private static readonly (string itemId, float weight)[] BossLootTable =
    {
        ("aegis-core", 0.8f),
        ("alloy-plate", 0.5f),
        ("telemetry-cache", 0.4f),
        ("field-kit", 0.3f),
    };

    private static readonly Vector2[] DropOffsets =
    {
        new(-18, -8), new(16, -4), new(-6, 18), new(20, 14),
    };

    public static void ApplyKillRewards(
        EnemyActor enemy,
        RunState run,
        float rewardMultiplier,
        float elapsed)
    {
        var table = enemy.Type == HostileType.Boss ? BossLootTable : EnemyLootTable;
        int dropCount = enemy.Type == HostileType.Boss ? 3 : 1;

        for (int i = 0; i < dropCount; i++)
        {
            string? itemId = RollLootTable(table);
            if (itemId == null) continue;

            var record = GridInventory.CreateItemRecord(itemId, 1);
            if (record == null) continue;

            var offset = DropOffsets[i % DropOffsets.Length];
            var drop = new GroundLootDrop
            {
                Id = $"drop-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}-{_rng.Next(9999):D4}",
                Item = record,
                X = enemy.X + offset.X + (float)(_rng.NextDouble() * 12 - 6),
                Y = enemy.Y + offset.Y + (float)(_rng.NextDouble() * 12 - 6),
                Source = enemy.Type == HostileType.Boss ? LootSource.Boss : LootSource.Enemy,
            };

            run.GroundLoot.Add(drop);
            run.LootEntries.Add(new LootEntry
            {
                Id = record.Id,
                DefinitionId = record.ItemId,
                Label = ItemData.ById.TryGetValue(record.ItemId, out var def) ? def.Label : record.ItemId,
                Category = def?.Category ?? ItemCategory.Resource,
                Quantity = record.Quantity,
                Source = drop.Source,
                AcquiredAtWave = run.Map.CurrentWave,
                AcquiredAtSeconds = elapsed,
            });
        }
    }

    public static GroundLootDrop? FindNearbyGroundLoot(Vector2 playerPos, List<GroundLootDrop> groundLoot, float radius = 64f)
    {
        GroundLootDrop? nearest = null;
        float nearestDist = radius;

        foreach (var drop in groundLoot)
        {
            float dist = playerPos.DistanceTo(new Vector2(drop.X, drop.Y));
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = drop;
            }
        }
        return nearest;
    }

    public static bool TryPickupLoot(GroundLootDrop drop, GridInventoryState inventory, List<GroundLootDrop> groundLoot)
    {
        var result = GridInventory.PlaceItemInGrid(inventory.Columns, inventory.Rows, inventory.Items, drop.Item);
        if (!result.Placed) return false;

        inventory.Items = result.Items;
        groundLoot.Remove(drop);
        return true;
    }

    private static string? RollLootTable((string itemId, float weight)[] table)
    {
        foreach (var (itemId, weight) in table)
        {
            if (_rng.NextDouble() < weight)
                return itemId;
        }
        return null;
    }
}
