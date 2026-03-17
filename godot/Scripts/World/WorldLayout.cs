using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ShotV.Core;
using ShotV.State;

namespace ShotV.World;

public class WorldObstacle
{
    public string Id { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public ObstacleKind Kind { get; set; } = ObstacleKind.Wall;
    public string? Label { get; set; }
}

public class WorldMarker
{
    public string Id { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public string Label { get; set; } = "";
    public MarkerKind Kind { get; set; }
}

public class WorldRegion
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public WorldZoneKind Kind { get; set; }
    public int ThreatLevel { get; set; }
    public float RewardMultiplier { get; set; }
    public string Description { get; set; } = "";
    public Rect2 Bounds { get; set; }
}

public class WorldSpawnAnchor
{
    public string Id { get; set; } = "";
    public string RegionId { get; set; } = "";
    public Vector2 Position { get; set; }
    public int ThreatLevel { get; set; }
}

public class WorldMapLayout
{
    public string Id { get; set; } = "";
    public uint Seed { get; set; }
    public Rect2 Bounds { get; set; }
    public Vector2 PlayerSpawn { get; set; }
    public Vector2 BossSpawn { get; set; }
    public Vector2 ExtractionPoint { get; set; }
    public List<Vector2> EnemySpawns { get; set; } = new();
    public List<WorldObstacle> Obstacles { get; set; } = new();
    public List<WorldMarker> Markers { get; set; } = new();
    public List<WorldRegion> Regions { get; set; } = new();
    public List<WorldSpawnAnchor> SpawnAnchors { get; set; } = new();

    public WorldRegion? GetRegionAtPosition(Vector2 point)
    {
        foreach (var region in Regions)
        {
            if (region.Bounds.HasPoint(point))
                return region;
        }

        WorldRegion? closest = null;
        float closestDistance = float.MaxValue;
        foreach (var region in Regions)
        {
            float distance = region.Bounds.GetCenter().DistanceSquaredTo(point);
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closest = region;
        }

        return closest;
    }
}

public struct CombatLayoutInput
{
    public string MapId;
    public string MapLabel;
    public List<RunZoneState> Regions;
    public uint Seed;
}

public static class WorldLayoutBuilder
{
    private const float WorldMargin = 140f;
    private static readonly Rect2[] RegionSlots =
    {
        new(0.08f, 0.60f, 0.25f, 0.24f),
        new(0.36f, 0.48f, 0.26f, 0.28f),
        new(0.67f, 0.18f, 0.24f, 0.30f),
        new(0.16f, 0.16f, 0.24f, 0.24f),
        new(0.66f, 0.62f, 0.24f, 0.20f),
    };

    public static WorldMapLayout CreateCombatLayout(CombatLayoutInput input)
    {
        int regionCount = Mathf.Max(1, input.Regions.Count);
        int maxThreat = input.Regions.Count > 0 ? input.Regions.Max(region => region.ThreatLevel) : 1;
        float width = 3400f + regionCount * 160f;
        float height = 2500f + maxThreat * 120f;
        var bounds = new Rect2(0, 0, width, height);
        var playerSpawn = new Vector2(width * 0.5f, height - 220f);
        var rng = new SeededRng(input.Seed);

        var regions = BuildCombatRegions(bounds, input.Regions);
        var extractionRegion = regions.FirstOrDefault(region => region.Kind == WorldZoneKind.Extraction) ?? regions[^1];
        var extractionPoint = new Vector2(extractionRegion.Bounds.End.X - 140f, extractionRegion.Bounds.GetCenter().Y);
        var bossSpawn = regions.OrderByDescending(region => region.ThreatLevel).First().Bounds.GetCenter();

        var obstacles = new List<WorldObstacle>();
        var safetyRects = new List<Rect2>
        {
            new(playerSpawn.X - 210, playerSpawn.Y - 170, 420, 280),
            new(extractionPoint.X - 180, extractionPoint.Y - 180, 360, 360),
        };
        safetyRects.AddRange(regions.Select(region => new Rect2(region.Bounds.GetCenter() - new Vector2(110f, 110f), new Vector2(220f, 220f))));

        int obstacleTarget = 34 + maxThreat * 10;
        int index = 0;
        int attempts = 0;

        while (obstacles.Count < obstacleTarget && attempts < obstacleTarget * 28)
        {
            attempts++;
            float ox = WorldMargin + rng.Next() * (width - WorldMargin * 2 - 260);
            float oy = WorldMargin + rng.Next() * (height - WorldMargin * 2 - 240);
            float ow = 90f + rng.Next() * 190f;
            float oh = 72f + rng.Next() * 156f;
            var kind = rng.Next() > 0.58f ? ObstacleKind.Wall : ObstacleKind.Cover;
            var obstacleRect = new Rect2(ox, oy, ow, oh);

            if (safetyRects.Any(safe => safe.Intersects(obstacleRect)))
                continue;

            bool hitExisting = false;
            foreach (var existing in obstacles)
            {
                var expanded = new Rect2(existing.X - 52f, existing.Y - 52f, existing.Width + 104f, existing.Height + 104f);
                if (expanded.Intersects(obstacleRect))
                {
                    hitExisting = true;
                    break;
                }
            }
            if (hitExisting)
                continue;

            obstacles.Add(new WorldObstacle
            {
                Id = $"combat-obstacle-{index}",
                X = ox,
                Y = oy,
                Width = ow,
                Height = oh,
                Kind = kind,
            });
            index++;
        }

        var spawnAnchors = CreateSpawnAnchors(regions, obstacles, playerSpawn, extractionPoint, input.Seed ^ 0x51eb851f);
        var markers = new List<WorldMarker>
        {
            new() { Id = "entry", X = playerSpawn.X, Y = playerSpawn.Y + 90f, Label = "投送点", Kind = MarkerKind.Entry },
            new() { Id = "extraction", X = extractionPoint.X, Y = extractionPoint.Y, Label = "撤离出口", Kind = MarkerKind.Extraction },
        };

        foreach (var region in regions)
        {
            markers.Add(new WorldMarker
            {
                Id = $"region-{region.Id}",
                X = region.Bounds.GetCenter().X,
                Y = region.Bounds.GetCenter().Y,
                Label = region.Label,
                Kind = region.Kind is WorldZoneKind.HighRisk or WorldZoneKind.HighValue ? MarkerKind.Objective : MarkerKind.Station,
            });
        }

        return new WorldMapLayout
        {
            Id = input.MapId,
            Seed = input.Seed,
            Bounds = bounds,
            PlayerSpawn = playerSpawn,
            BossSpawn = bossSpawn,
            ExtractionPoint = extractionPoint,
            EnemySpawns = spawnAnchors.Select(anchor => anchor.Position).ToList(),
            Obstacles = obstacles,
            Markers = markers,
            Regions = regions,
            SpawnAnchors = spawnAnchors,
        };
    }

    public static WorldMapLayout CreateBaseLayout(uint seed = 20260314)
    {
        var bounds = new Rect2(0, 0, 2240, 1680);
        var playerSpawn = new Vector2(1120, 1320);
        var markers = new List<WorldMarker>
        {
            new() { Id = "command", X = 1120, Y = 320, Label = "指挥台", Kind = MarkerKind.Station },
            new() { Id = "locker", X = 740, Y = 720, Label = "储物柜", Kind = MarkerKind.Locker },
            new() { Id = "workshop", X = 1490, Y = 760, Label = "工坊台", Kind = MarkerKind.Station },
            new() { Id = "launch", X = 1120, Y = 1460, Label = "出击闸门", Kind = MarkerKind.Entry },
        };
        var obstacles = new List<WorldObstacle>
        {
            MakeObstacle("north-wall-left", 468, 176, 220, 42, ObstacleKind.Wall),
            MakeObstacle("north-wall-right", 1552, 176, 220, 42, ObstacleKind.Wall),
            MakeObstacle("command-console", 1038, 368, 164, 70, ObstacleKind.Station),
            MakeObstacle("command-rack-left", 952, 452, 82, 56, ObstacleKind.Station),
            MakeObstacle("command-rack-right", 1206, 452, 82, 56, ObstacleKind.Station),
            MakeObstacle("locker-bank-left", 654, 762, 84, 60, ObstacleKind.Locker),
            MakeObstacle("locker-bank-right", 748, 762, 84, 60, ObstacleKind.Locker),
            MakeObstacle("workbench-main", 1404, 800, 154, 62, ObstacleKind.Station),
            MakeObstacle("workbench-rack", 1580, 786, 78, 78, ObstacleKind.Station),
            MakeObstacle("cargo-crate-left", 886, 1106, 96, 58, ObstacleKind.Cover),
            MakeObstacle("cargo-crate-right", 1258, 1106, 96, 58, ObstacleKind.Cover),
            MakeObstacle("launch-pillar-left", 1030, 1492, 60, 74, ObstacleKind.Wall),
            MakeObstacle("launch-pillar-right", 1150, 1492, 60, 74, ObstacleKind.Wall),
            MakeObstacle("launch-console", 1088, 1386, 64, 42, ObstacleKind.Station),
        };

        return new WorldMapLayout
        {
            Id = "base-camp",
            Seed = seed,
            Bounds = bounds,
            PlayerSpawn = playerSpawn,
            BossSpawn = new Vector2(1120, 280),
            ExtractionPoint = new Vector2(1120, 1460),
            EnemySpawns = new List<Vector2>(),
            Obstacles = obstacles,
            Markers = markers,
        };
    }

    private static List<WorldRegion> BuildCombatRegions(Rect2 bounds, List<RunZoneState> inputRegions)
    {
        var regions = new List<WorldRegion>(inputRegions.Count);
        var takenSlots = new HashSet<int>();

        foreach (var region in inputRegions)
        {
            int slotIndex = PickRegionSlot(region.Kind, takenSlots);
            takenSlots.Add(slotIndex);
            var slot = RegionSlots[slotIndex];
            var regionRect = new Rect2(
                bounds.Position.X + bounds.Size.X * slot.Position.X,
                bounds.Position.Y + bounds.Size.Y * slot.Position.Y,
                bounds.Size.X * slot.Size.X,
                bounds.Size.Y * slot.Size.Y);

            regions.Add(new WorldRegion
            {
                Id = region.Id,
                Label = region.Label,
                Kind = region.Kind,
                ThreatLevel = region.ThreatLevel,
                RewardMultiplier = region.RewardMultiplier,
                Description = region.Description,
                Bounds = regionRect,
            });
        }

        return regions;
    }

    private static int PickRegionSlot(WorldZoneKind kind, HashSet<int> takenSlots)
    {
        int[] preference = kind switch
        {
            WorldZoneKind.Extraction => new[] { 4, 2, 1, 0, 3 },
            WorldZoneKind.HighValue => new[] { 2, 1, 3, 4, 0 },
            WorldZoneKind.HighRisk => new[] { 1, 3, 2, 0, 4 },
            _ => new[] { 0, 3, 1, 4, 2 },
        };

        foreach (int slot in preference)
        {
            if (!takenSlots.Contains(slot))
                return slot;
        }

        for (int slot = 0; slot < RegionSlots.Length; slot++)
        {
            if (!takenSlots.Contains(slot))
                return slot;
        }

        return 0;
    }

    private static List<WorldSpawnAnchor> CreateSpawnAnchors(
        List<WorldRegion> regions,
        List<WorldObstacle> obstacles,
        Vector2 playerSpawn,
        Vector2 extractionPoint,
        uint seed)
    {
        var rng = new SeededRng(seed);
        var anchors = new List<WorldSpawnAnchor>();
        int index = 0;

        foreach (var region in regions)
        {
            int targetCount = 3 + Mathf.Clamp(region.ThreatLevel, 1, 4);
            int attempts = 0;

            while (anchors.Count(anchor => anchor.RegionId == region.Id) < targetCount && attempts < targetCount * 16)
            {
                attempts++;
                float x = region.Bounds.Position.X + 96f + rng.Next() * Mathf.Max(120f, region.Bounds.Size.X - 192f);
                float y = region.Bounds.Position.Y + 96f + rng.Next() * Mathf.Max(120f, region.Bounds.Size.Y - 192f);
                var point = new Vector2(x, y);

                if (point.DistanceTo(playerSpawn) < 360f || point.DistanceTo(extractionPoint) < 260f)
                    continue;

                bool blocked = false;
                foreach (var obstacle in obstacles)
                {
                    var expanded = new Rect2(obstacle.X - 40f, obstacle.Y - 40f, obstacle.Width + 80f, obstacle.Height + 80f);
                    if (expanded.HasPoint(point))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked)
                    continue;

                anchors.Add(new WorldSpawnAnchor
                {
                    Id = $"spawn-anchor-{index}",
                    RegionId = region.Id,
                    Position = point,
                    ThreatLevel = region.ThreatLevel,
                });
                index++;
            }
        }

        return anchors;
    }

    private static WorldObstacle MakeObstacle(string id, float x, float y, float w, float h, ObstacleKind kind)
    {
        return new WorldObstacle { Id = id, X = x, Y = y, Width = w, Height = h, Kind = kind };
    }
}
