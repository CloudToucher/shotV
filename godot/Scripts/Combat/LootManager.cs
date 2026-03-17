using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.Inventory;
using ShotV.State;
using ShotV.World;

namespace ShotV.Combat;

public readonly struct LootPickupResult
{
    public LootPickupResult(bool pickedUp, int pickedQuantity, bool fullyCollected)
    {
        PickedUp = pickedUp;
        PickedQuantity = pickedQuantity;
        FullyCollected = fullyCollected;
    }

    public bool PickedUp { get; }
    public int PickedQuantity { get; }
    public bool FullyCollected { get; }
}

public static class LootManager
{
    private static readonly Random _rng = new();

    private static readonly (string itemId, float weight)[] EnemyLootTable =
    {
        ("salvage-scrap", 0.55f),
        ("alloy-plate", 0.08f),
        ("telemetry-cache", 0.05f),
        ("med-injector", 0.12f),
        ("shock-charge", 0.06f),
        ("dash-cell", 0.05f),
    };

    private static readonly (string itemId, float weight)[] PerimeterLootTable =
    {
        ("salvage-scrap", 0.56f),
        ("med-injector", 0.16f),
        ("dash-cell", 0.09f),
        ("alloy-plate", 0.08f),
        ("shock-charge", 0.06f),
        ("telemetry-cache", 0.05f),
    };

    private static readonly (string itemId, float weight)[] HighRiskLootTable =
    {
        ("salvage-scrap", 0.36f),
        ("alloy-plate", 0.22f),
        ("med-injector", 0.14f),
        ("shock-charge", 0.12f),
        ("dash-cell", 0.08f),
        ("telemetry-cache", 0.08f),
    };

    private static readonly (string itemId, float weight)[] HighValueLootTable =
    {
        ("salvage-scrap", 0.24f),
        ("alloy-plate", 0.24f),
        ("telemetry-cache", 0.24f),
        ("med-injector", 0.1f),
        ("shock-charge", 0.1f),
        ("dash-cell", 0.08f),
    };

    private static readonly (string itemId, float weight)[] ExtractionLootTable =
    {
        ("salvage-scrap", 0.34f),
        ("med-injector", 0.18f),
        ("dash-cell", 0.14f),
        ("alloy-plate", 0.15f),
        ("telemetry-cache", 0.08f),
        ("shock-charge", 0.06f),
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
        new(-18f, -8f),
        new(16f, -4f),
        new(-6f, 18f),
        new(20f, 14f),
    };

    public static void ApplyKillRewards(
        EnemyActor enemy,
        RunState run,
        WorldRegion? region,
        float rewardMultiplier,
        float elapsed)
    {
        var table = enemy.Type == HostileType.Boss ? BossLootTable : ResolveEnemyLootTable(region);
        int dropCount = enemy.Type == HostileType.Boss ? 3 : 1;
        if (enemy.Type != HostileType.Boss && _rng.NextDouble() < ResolveExtraDropChance(region, rewardMultiplier))
            dropCount++;

        for (int index = 0; index < dropCount; index++)
            TrySpawnItemDrop(RollLootTable(table), enemy, run, elapsed, index);

        int ammoAttempts = enemy.Type == HostileType.Boss ? 2 : 1;
        for (int index = 0; index < ammoAttempts; index++)
        {
            if (enemy.Type != HostileType.Boss && _rng.NextDouble() > ResolveAmmoDropChance(region, rewardMultiplier))
                continue;

            TrySpawnAmmoDrop(enemy, run, region, elapsed, dropCount + index);
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

    public static LootPickupResult TryPickupLoot(GroundLootDrop drop, GridInventoryState inventory, List<GroundLootDrop> groundLoot)
    {
        int before = drop.Item.Quantity;
        var result = GridInventory.StoreItemInGrid(inventory.Columns, inventory.Rows, inventory.Items, drop.Item);
        if (result.AcceptedQuantity <= 0)
            return new LootPickupResult(false, 0, false);

        inventory.Items = result.Items;
        if (result.AcceptedQuantity >= before)
        {
            groundLoot.Remove(drop);
            return new LootPickupResult(true, result.AcceptedQuantity, true);
        }

        drop.Item.Quantity = before - result.AcceptedQuantity;
        return new LootPickupResult(true, result.AcceptedQuantity, false);
    }

    private static void TrySpawnItemDrop(string? itemId, EnemyActor enemy, RunState run, float elapsed, int offsetIndex)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        var record = GridInventory.CreateItemRecord(itemId, 1);
        if (record == null)
            return;

        var drop = CreateDrop(record, enemy, offsetIndex);
        run.GroundLoot.Add(drop);

        if (!ItemData.ById.TryGetValue(record.ItemId, out var definition) || definition.Category == ItemCategory.Ammo)
            return;

        run.LootEntries.Add(new LootEntry
        {
            Id = record.Id,
            DefinitionId = record.ItemId,
            Label = definition.Label,
            Category = definition.Category,
            Quantity = record.Quantity,
            Source = drop.Source,
            AcquiredAtWave = run.Map.CurrentWave,
            AcquiredAtSeconds = elapsed,
        });
    }

    private static void TrySpawnAmmoDrop(EnemyActor enemy, RunState run, WorldRegion? region, float elapsed, int offsetIndex)
    {
        var ammo = RollAmmoForRun(run, region);
        if (ammo == null || string.IsNullOrWhiteSpace(ammo.ReserveItemId))
            return;

        int quantity = ammo.PickupMin;
        if (ammo.PickupMax > ammo.PickupMin)
            quantity += _rng.Next(ammo.PickupMax - ammo.PickupMin + 1);
        if (enemy.Type == HostileType.Boss)
            quantity += Mathf.Max(2, ammo.PickupMin);

        var record = GridInventory.CreateItemRecord(ammo.ReserveItemId, quantity);
        if (record == null)
            return;

        run.GroundLoot.Add(CreateDrop(record, enemy, offsetIndex));
    }

    private static GroundLootDrop CreateDrop(InventoryItemRecord record, EnemyActor enemy, int offsetIndex)
    {
        var offset = DropOffsets[offsetIndex % DropOffsets.Length];
        return new GroundLootDrop
        {
            Id = $"drop-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}-{_rng.Next(9999):D4}",
            Item = record,
            X = enemy.X + offset.X + (float)(_rng.NextDouble() * 12 - 6),
            Y = enemy.Y + offset.Y + (float)(_rng.NextDouble() * 12 - 6),
            Source = enemy.Type == HostileType.Boss ? LootSource.Boss : LootSource.Enemy,
        };
    }

    private static (string itemId, float weight)[] ResolveEnemyLootTable(WorldRegion? region)
    {
        return region?.Kind switch
        {
            WorldZoneKind.HighRisk => HighRiskLootTable,
            WorldZoneKind.HighValue => HighValueLootTable,
            WorldZoneKind.Extraction => ExtractionLootTable,
            WorldZoneKind.Perimeter => PerimeterLootTable,
            _ => EnemyLootTable,
        };
    }

    private static float ResolveExtraDropChance(WorldRegion? region, float rewardMultiplier)
    {
        float chance = rewardMultiplier >= 1.2f ? rewardMultiplier - 0.95f : 0f;
        if (region == null)
            return Mathf.Clamp(chance, 0f, 0.65f);

        chance += region.Kind switch
        {
            WorldZoneKind.HighRisk => 0.08f,
            WorldZoneKind.HighValue => 0.12f,
            WorldZoneKind.Extraction => 0.05f,
            _ => 0.02f,
        };
        chance += Mathf.Max(0, region.ThreatLevel - 1) * 0.04f;
        return Mathf.Clamp(chance, 0f, 0.72f);
    }

    private static float ResolveAmmoDropChance(WorldRegion? region, float rewardMultiplier)
    {
        float chance = 0.22f + Mathf.Max(0f, rewardMultiplier - 1f) * 0.18f;
        if (region == null)
            return Mathf.Clamp(chance, 0.18f, 0.5f);

        chance += region.Kind switch
        {
            WorldZoneKind.HighRisk => 0.12f,
            WorldZoneKind.HighValue => 0.08f,
            WorldZoneKind.Extraction => 0.1f,
            _ => 0.04f,
        };
        chance += (region.ThreatLevel - 1) * 0.05f;
        return Mathf.Clamp(chance, 0.18f, 0.62f);
    }

    private static WeaponAmmoDefinition? RollAmmoForRun(RunState run, WorldRegion? region)
    {
        var weighted = new List<(WeaponAmmoDefinition ammo, float weight)>();
        foreach (var weaponId in run.Player.LoadoutWeaponIds.Distinct())
        {
            if (!WeaponData.ById.TryGetValue(weaponId, out var weapon))
                continue;

            foreach (var ammo in weapon.AmmoTypes)
            {
                int reserve = GridInventory.CountItemQuantity(run.Inventory.Items, ammo.ReserveItemId);
                float weight = 0.55f;
                if (weaponId == run.Player.CurrentWeaponId)
                    weight += 0.35f;
                if (ammo.Id == weapon.DefaultAmmoId)
                    weight += 0.28f;
                if (reserve < weapon.MagazineCapacity)
                    weight += 1.05f;
                else if (reserve < ammo.StartingReserve * 0.5f)
                    weight += 0.62f;
                else if (reserve > ammo.StartingReserve * 1.5f)
                    weight *= 0.45f;

                if (region != null)
                {
                    if (region.Kind == WorldZoneKind.HighRisk && (ammo.Id is "ap" or "bonded" or "sabot" or "overmatch" or "breach" or "flechette"))
                        weight += 0.22f;
                    if (region.Kind == WorldZoneKind.HighValue && ammo.Id is "exp" or "rupture" or "arc" or "blast")
                        weight += 0.16f;
                }

                weighted.Add((ammo, weight));
            }
        }

        if (weighted.Count == 0)
            return null;

        float totalWeight = weighted.Sum(entry => Mathf.Max(0f, entry.weight));
        if (totalWeight <= 0f)
            return weighted[0].ammo;

        double roll = _rng.NextDouble() * totalWeight;
        foreach (var entry in weighted)
        {
            roll -= Mathf.Max(0f, entry.weight);
            if (roll <= 0d)
                return entry.ammo;
        }

        return weighted[^1].ammo;
    }

    private static string? RollLootTable((string itemId, float weight)[] table)
    {
        float totalWeight = 0f;
        foreach (var (_, weight) in table)
            totalWeight += Mathf.Max(0f, weight);

        if (totalWeight <= 0f)
            return null;

        double roll = _rng.NextDouble() * totalWeight;
        foreach (var (itemId, weight) in table)
        {
            roll -= Mathf.Max(0f, weight);
            if (roll <= 0d)
                return itemId;
        }

        return table.Length > 0 ? table[^1].itemId : null;
    }
}
