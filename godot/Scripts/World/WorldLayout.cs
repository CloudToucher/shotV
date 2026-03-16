using System;
using System.Collections.Generic;
using Godot;
using ShotV.Core;

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

public class WorldMapLayout
{
    public string Id { get; set; } = "";
    public uint Seed { get; set; }
    public Rect2 Bounds { get; set; }
    public Vector2 PlayerSpawn { get; set; }
    public Vector2 BossSpawn { get; set; }
    public List<Vector2> EnemySpawns { get; set; } = new();
    public List<WorldObstacle> Obstacles { get; set; } = new();
    public List<WorldMarker> Markers { get; set; } = new();
}

public struct CombatLayoutInput
{
    public string RouteId;
    public string ZoneId;
    public string ZoneLabel;
    public int ThreatLevel;
    public bool AllowsExtraction;
    public uint Seed;
}

public static class WorldLayoutBuilder
{
    private const float WorldMargin = 140f;

    public static WorldMapLayout CreateCombatLayout(CombatLayoutInput input)
    {
        float width = 2800f + input.ThreatLevel * 260f;
        float height = 2200f + input.ThreatLevel * 220f;
        var bounds = new Rect2(0, 0, width, height);
        var playerSpawn = new Vector2(width * 0.5f, height - 220f);
        var bossSpawn = new Vector2(width * 0.5f, 220f);
        var exitPoint = new Vector2(width - 240f, height - 260f);
        var rng = new SeededRng(input.Seed);

        var obstacles = new List<WorldObstacle>();
        var safetyRects = new List<Rect2>
        {
            new(playerSpawn.X - 180, playerSpawn.Y - 160, 360, 260),
            new(bossSpawn.X - 220, bossSpawn.Y - 160, 440, 260),
            new(exitPoint.X - 180, exitPoint.Y - 150, 360, 240),
            new(width * 0.5f - 120, 0, 240, height),
            new(playerSpawn.X - 120, playerSpawn.Y - 110, exitPoint.X - playerSpawn.X + 240, 220),
        };

        int obstacleTarget = 24 + input.ThreatLevel * 6;
        int index = 0;
        int attempts = 0;

        while (obstacles.Count < obstacleTarget && attempts < obstacleTarget * 24)
        {
            attempts++;
            float ox = WorldMargin + rng.Next() * (width - WorldMargin * 2 - 240);
            float oy = WorldMargin + rng.Next() * (height - WorldMargin * 2 - 220);
            float ow = 90f + rng.Next() * (input.ThreatLevel >= 3 ? 210f : 170f);
            float oh = 72f + rng.Next() * (input.ThreatLevel >= 2 ? 180f : 140f);
            var kind = rng.Next() > 0.55f ? ObstacleKind.Wall : ObstacleKind.Cover;
            var obstRect = new Rect2(ox, oy, ow, oh);

            bool hitSafety = false;
            foreach (var sr in safetyRects)
            {
                if (sr.Intersects(obstRect)) { hitSafety = true; break; }
            }
            if (hitSafety) continue;

            bool hitExisting = false;
            foreach (var existing in obstacles)
            {
                var expanded = new Rect2(existing.X - 48, existing.Y - 48, existing.Width + 96, existing.Height + 96);
                if (expanded.Intersects(obstRect)) { hitExisting = true; break; }
            }
            if (hitExisting) continue;

            obstacles.Add(new WorldObstacle { Id = $"combat-obstacle-{index}", X = ox, Y = oy, Width = ow, Height = oh, Kind = kind });
            index++;
        }

        var enemySpawns = CreateCombatSpawnPoints(bounds, obstacles, playerSpawn);
        var markers = new List<WorldMarker>
        {
            new() { Id = "entry", X = playerSpawn.X, Y = playerSpawn.Y + 90, Label = "投送点", Kind = MarkerKind.Entry },
            new() { Id = "objective", X = bossSpawn.X, Y = bossSpawn.Y, Label = $"{input.ZoneLabel}核心", Kind = MarkerKind.Objective },
            new() { Id = "exit", X = exitPoint.X, Y = exitPoint.Y, Label = input.AllowsExtraction ? "撤离出口" : "区域出口", Kind = MarkerKind.Extraction },
        };

        return new WorldMapLayout
        {
            Id = $"{input.RouteId}:{input.ZoneId}",
            Seed = input.Seed,
            Bounds = bounds,
            PlayerSpawn = playerSpawn,
            BossSpawn = bossSpawn,
            EnemySpawns = enemySpawns,
            Obstacles = obstacles,
            Markers = markers,
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
            EnemySpawns = new List<Vector2>(),
            Obstacles = obstacles,
            Markers = markers,
        };
    }

    private static List<Vector2> CreateCombatSpawnPoints(Rect2 bounds, List<WorldObstacle> obstacles, Vector2 playerSpawn)
    {
        var candidates = new List<Vector2>
        {
            new(bounds.Position.X + 160, bounds.Position.Y + 180),
            new(bounds.End.X - 160, bounds.Position.Y + 180),
            new(bounds.Position.X + 160, bounds.End.Y - 180),
            new(bounds.End.X - 160, bounds.End.Y - 180),
            new(bounds.Position.X + 120, bounds.End.Y * 0.55f),
            new(bounds.End.X - 120, bounds.End.Y * 0.55f),
            new(bounds.Position.X + bounds.Size.X * 0.28f, bounds.Position.Y + 120),
            new(bounds.Position.X + bounds.Size.X * 0.72f, bounds.Position.Y + 120),
            new(bounds.Position.X + bounds.Size.X * 0.22f, bounds.End.Y - 120),
            new(bounds.Position.X + bounds.Size.X * 0.78f, bounds.End.Y - 120),
        };

        var result = new List<Vector2>();
        foreach (var pt in candidates)
        {
            if (pt.DistanceTo(playerSpawn) < 380f) continue;
            bool insideObstacle = false;
            foreach (var obs in obstacles)
            {
                var expanded = new Rect2(obs.X - 36, obs.Y - 36, obs.Width + 72, obs.Height + 72);
                if (expanded.HasPoint(pt)) { insideObstacle = true; break; }
            }
            if (!insideObstacle) result.Add(pt);
        }
        return result;
    }

    private static WorldObstacle MakeObstacle(string id, float x, float y, float w, float h, ObstacleKind kind)
    {
        return new WorldObstacle { Id = id, X = x, Y = y, Width = w, Height = h, Kind = kind };
    }
}
