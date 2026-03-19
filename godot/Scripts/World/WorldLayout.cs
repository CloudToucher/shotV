using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ShotV.Core;
using ShotV.Data;
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
    public List<Vector2> ExtractionPoints { get; set; } = new();
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
    public int ExtractionPointCount;
}

public static class WorldLayoutBuilder
{
    private const float WorldMargin = 140f;
    private const float BaseFurnitureUnit = CombatConstants.GridSize * 0.5f;
    private static readonly Rect2[] RegionSlots =
    {
        new(0.08f, 0.60f, 0.25f, 0.24f),
        new(0.36f, 0.48f, 0.26f, 0.28f),
        new(0.67f, 0.18f, 0.24f, 0.30f),
        new(0.16f, 0.16f, 0.24f, 0.24f),
        new(0.66f, 0.62f, 0.24f, 0.20f),
    };
    private static readonly Vector2[] ExtractionSlots =
    {
        new(0.86f, 0.76f),
        new(0.14f, 0.22f),
        new(0.82f, 0.18f),
        new(0.18f, 0.72f),
        new(0.5f, 0.12f),
    };

    public static WorldMapLayout CreateCombatLayout(CombatLayoutInput input)
    {
        int regionCount = Mathf.Max(1, input.Regions.Count);
        int maxThreat = input.Regions.Count > 0 ? input.Regions.Max(region => region.ThreatLevel) : 1;
        float width = 4200f + regionCount * 220f;
        float height = 3000f + maxThreat * 160f;
        var bounds = new Rect2(0, 0, width, height);
        var playerSpawn = new Vector2(width * 0.5f, height - 260f);
        var rng = new SeededRng(input.Seed);

        var regions = BuildCombatRegions(bounds, input.Regions);
        var extractionPoints = CreateExtractionPoints(bounds, regions, playerSpawn, input.ExtractionPointCount);
        var bossSpawn = regions.OrderByDescending(region => region.ThreatLevel).First().Bounds.GetCenter();

        var obstacles = new List<WorldObstacle>();
        var safetyRects = new List<Rect2>
        {
            new(playerSpawn.X - 210, playerSpawn.Y - 170, 420, 280),
        };
        safetyRects.AddRange(extractionPoints.Select(point => new Rect2(point.X - 180, point.Y - 180, 360, 360)));
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

        var spawnAnchors = CreateSpawnAnchors(regions, obstacles, playerSpawn, extractionPoints, input.Seed ^ 0x51eb851f);
        var markers = new List<WorldMarker>
        {
            new() { Id = "entry", X = playerSpawn.X, Y = playerSpawn.Y + 90f, Label = GameText.Text("marker.entry.label"), Kind = MarkerKind.Entry },
        };

        for (int extractionIndex = 0; extractionIndex < extractionPoints.Count; extractionIndex++)
        {
            var point = extractionPoints[extractionIndex];
            markers.Add(new WorldMarker
            {
                Id = $"extraction-{extractionIndex + 1}",
                X = point.X,
                Y = point.Y,
                Label = extractionPoints.Count == 1
                    ? GameText.Text("marker.extraction.label")
                    : $"{GameText.Text("marker.extraction.label")} {extractionIndex + 1}",
                Kind = MarkerKind.Extraction,
            });
        }

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
            ExtractionPoints = extractionPoints,
            EnemySpawns = spawnAnchors.Select(anchor => anchor.Position).ToList(),
            Obstacles = obstacles,
            Markers = markers,
            Regions = regions,
            SpawnAnchors = spawnAnchors,
        };
    }

    public static WorldMapLayout CreateBaseLayout(uint seed = 20260314)
    {
        var bounds = new Rect2(0, 0, BaseFurnitureUnit * 80f, BaseFurnitureUnit * 60f);
        var playerSpawn = BasePoint(40f, 47f);
        var markers = new List<WorldMarker>
        {
            new() { Id = "command", X = BasePoint(40f, 10.5f).X, Y = BasePoint(40f, 10.5f).Y, Label = GameText.Text("marker.command.label"), Kind = MarkerKind.Station },
            new() { Id = "locker", X = BasePoint(26.5f, 26.5f).X, Y = BasePoint(26.5f, 26.5f).Y, Label = GameText.Text("marker.locker.label"), Kind = MarkerKind.Locker },
            new() { Id = "workshop", X = BasePoint(54f, 27.5f).X, Y = BasePoint(54f, 27.5f).Y, Label = GameText.Text("marker.workshop.label"), Kind = MarkerKind.Station },
            new() { Id = "trader", X = BasePoint(63f, 49.5f).X, Y = BasePoint(63f, 49.5f).Y, Label = "军需商", Kind = MarkerKind.Station },
            new() { Id = "launch", X = BasePoint(40f, 52f).X, Y = BasePoint(40f, 52f).Y, Label = GameText.Text("marker.launch.label"), Kind = MarkerKind.Entry },
        };
        var obstacles = new List<WorldObstacle>
        {
            MakeBaseFurniture("north-wall-left", 14, 6, 8, 2, ObstacleKind.Wall),
            MakeBaseFurniture("north-wall-right", 58, 6, 8, 2, ObstacleKind.Wall),
            MakeBaseFurniture("archive-wall-left", 27, 11, 5, 2, ObstacleKind.Station, "档案墙"),
            MakeBaseFurniture("command-holo", 37, 12, 6, 3, ObstacleKind.Station, "战术台"),
            MakeBaseFurniture("archive-wall-right", 48, 11, 5, 2, ObstacleKind.Station),
            MakeBaseFurniture("command-rack-left", 33, 16, 3, 2, ObstacleKind.Station),
            MakeBaseFurniture("command-rack-right", 44, 16, 3, 2, ObstacleKind.Station),
            MakeBaseFurniture("map-table", 37, 18, 6, 2, ObstacleKind.Cover),
            MakeBaseFurniture("med-bay", 11, 14, 4, 2, ObstacleKind.Station, "医药柜"),
            MakeBaseFurniture("scout-console", 56, 13, 4, 2, ObstacleKind.Station, "侦测台"),
            MakeBaseFurniture("power-stack", 63, 11, 4, 3, ObstacleKind.Cover, "供电堆"),
            MakeBaseFurniture("coolant-bank", 68, 12, 3, 2, ObstacleKind.Cover),

            MakeBaseFurniture("bunk-a", 9, 20, 4, 2, ObstacleKind.Cover, "休整铺"),
            MakeBaseFurniture("bunk-b", 9, 24, 4, 2, ObstacleKind.Cover),
            MakeBaseFurniture("bunk-c", 9, 28, 4, 2, ObstacleKind.Cover),
            MakeBaseFurniture("armor-rack", 18, 20, 3, 5, ObstacleKind.Locker, "护甲架"),
            MakeBaseFurniture("locker-bank-left", 23, 23, 3, 3, ObstacleKind.Locker),
            MakeBaseFurniture("locker-bank-right", 27, 23, 3, 3, ObstacleKind.Locker),
            MakeBaseFurniture("supply-pallet", 18, 29, 4, 2, ObstacleKind.Cover),
            MakeBaseFurniture("loadout-bench", 24, 30, 4, 2, ObstacleKind.Station, "配装台"),
            MakeBaseFurniture("hydro-shelf", 13, 39, 4, 2, ObstacleKind.Station),
            MakeBaseFurniture("sorter-table", 18, 43, 4, 2, ObstacleKind.Cover, "回收箱"),

            MakeBaseFurniture("fabricator", 51, 23, 6, 3, ObstacleKind.Station, "加工台"),
            MakeBaseFurniture("tool-wall", 59, 21, 3, 5, ObstacleKind.Station, "工具墙"),
            MakeBaseFurniture("parts-rack", 64, 23, 3, 4, ObstacleKind.Locker),
            MakeBaseFurniture("repair-bench", 52, 29, 5, 2, ObstacleKind.Station),
            MakeBaseFurniture("drone-rack", 61, 29, 4, 2, ObstacleKind.Station, "无人机架"),
            MakeBaseFurniture("alloy-bins", 67, 30, 3, 2, ObstacleKind.Cover),
            MakeBaseFurniture("charge-rack", 60, 39, 4, 2, ObstacleKind.Station),
            MakeBaseFurniture("cable-spools", 66, 42, 3, 2, ObstacleKind.Cover),

            MakeBaseFurniture("prep-table", 33, 35, 14, 2, ObstacleKind.Station, "整备台"),
            MakeBaseFurniture("salvage-left", 27, 41, 4, 3, ObstacleKind.Cover),
            MakeBaseFurniture("prep-rack-left", 33, 40, 3, 2, ObstacleKind.Locker),
            MakeBaseFurniture("prep-rack-right", 44, 40, 3, 2, ObstacleKind.Locker),
            MakeBaseFurniture("sortie-pack", 49, 41, 4, 3, ObstacleKind.Cover),
            MakeBaseFurniture("support-crates", 56, 43, 4, 2, ObstacleKind.Cover),
            MakeBaseFurniture("trade-counter", 60, 46, 6, 2, ObstacleKind.Station, "军需商"),
            MakeBaseFurniture("trade-rack", 67, 46, 3, 3, ObstacleKind.Locker),
            MakeBaseFurniture("launch-cargo-left", 31, 50, 3, 2, ObstacleKind.Cover),
            MakeBaseFurniture("launch-console", 39, 49, 3, 2, ObstacleKind.Station, "闸门控制"),
            MakeBaseFurniture("launch-cargo-right", 46, 50, 3, 2, ObstacleKind.Cover),
            MakeBaseFurniture("launch-pillar-left", 36, 53, 2, 3, ObstacleKind.Wall),
            MakeBaseFurniture("launch-pillar-right", 42, 53, 2, 3, ObstacleKind.Wall),
        };

        return new WorldMapLayout
        {
            Id = "base-camp",
            Seed = seed,
            Bounds = bounds,
            PlayerSpawn = playerSpawn,
            BossSpawn = BasePoint(40f, 10f),
            ExtractionPoints = new List<Vector2> { BasePoint(40f, 52f) },
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

    private static List<Vector2> CreateExtractionPoints(Rect2 bounds, List<WorldRegion> regions, Vector2 playerSpawn, int requestedCount)
    {
        int targetCount = Mathf.Clamp(requestedCount <= 0 ? 3 : requestedCount, 1, ExtractionSlots.Length);
        var points = new List<Vector2>(targetCount);

        foreach (var region in regions.Where(region => region.Kind == WorldZoneKind.Extraction))
        {
            var candidate = ProjectPointToNearestEdge(bounds, region.Bounds.GetCenter(), 180f);
            if (TryRegisterExtractionPoint(points, candidate, playerSpawn) && points.Count >= targetCount)
                return points;
        }

        foreach (var slot in ExtractionSlots)
        {
            var candidate = new Vector2(
                bounds.Position.X + bounds.Size.X * slot.X,
                bounds.Position.Y + bounds.Size.Y * slot.Y);
            candidate = ClampToWorldMargin(bounds, candidate);
            if (TryRegisterExtractionPoint(points, candidate, playerSpawn) && points.Count >= targetCount)
                break;
        }

        if (points.Count == 0)
            points.Add(new Vector2(bounds.End.X - 180f, bounds.End.Y - 220f));

        return points;
    }

    private static bool TryRegisterExtractionPoint(List<Vector2> points, Vector2 candidate, Vector2 playerSpawn)
    {
        if (candidate.DistanceTo(playerSpawn) < 360f)
            return false;

        foreach (var existing in points)
        {
            if (existing.DistanceTo(candidate) < 320f)
                return false;
        }

        points.Add(candidate);
        return true;
    }

    private static Vector2 ProjectPointToNearestEdge(Rect2 bounds, Vector2 point, float margin)
    {
        float minX = bounds.Position.X + margin;
        float maxX = bounds.End.X - margin;
        float minY = bounds.Position.Y + margin;
        float maxY = bounds.End.Y - margin;

        float left = Mathf.Abs(point.X - bounds.Position.X);
        float right = Mathf.Abs(bounds.End.X - point.X);
        float top = Mathf.Abs(point.Y - bounds.Position.Y);
        float bottom = Mathf.Abs(bounds.End.Y - point.Y);

        if (left <= right && left <= top && left <= bottom)
            return new Vector2(minX, Mathf.Clamp(point.Y, minY, maxY));
        if (right <= top && right <= bottom)
            return new Vector2(maxX, Mathf.Clamp(point.Y, minY, maxY));
        if (top <= bottom)
            return new Vector2(Mathf.Clamp(point.X, minX, maxX), minY);
        return new Vector2(Mathf.Clamp(point.X, minX, maxX), maxY);
    }

    private static Vector2 ClampToWorldMargin(Rect2 bounds, Vector2 point)
    {
        return new Vector2(
            Mathf.Clamp(point.X, bounds.Position.X + WorldMargin, bounds.End.X - WorldMargin),
            Mathf.Clamp(point.Y, bounds.Position.Y + WorldMargin, bounds.End.Y - WorldMargin));
    }

    private static List<WorldSpawnAnchor> CreateSpawnAnchors(
        List<WorldRegion> regions,
        List<WorldObstacle> obstacles,
        Vector2 playerSpawn,
        List<Vector2> extractionPoints,
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

                bool nearExtraction = extractionPoints.Any(extractionPoint => point.DistanceTo(extractionPoint) < 260f);
                if (point.DistanceTo(playerSpawn) < 360f || nearExtraction)
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

    private static Vector2 BasePoint(float cellX, float cellY)
    {
        return new Vector2(cellX * BaseFurnitureUnit, cellY * BaseFurnitureUnit);
    }

    private static WorldObstacle MakeBaseFurniture(string id, int cellX, int cellY, int widthCells, int heightCells, ObstacleKind kind, string? label = null)
    {
        return new WorldObstacle
        {
            Id = id,
            X = cellX * BaseFurnitureUnit,
            Y = cellY * BaseFurnitureUnit,
            Width = widthCells * BaseFurnitureUnit,
            Height = heightCells * BaseFurnitureUnit,
            Kind = kind,
            Label = label,
        };
    }
}
