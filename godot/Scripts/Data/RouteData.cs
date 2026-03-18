using System.Collections.Generic;
using System.Linq;
using ShotV.Core;

namespace ShotV.Data;

public class WorldRouteZoneDefinition
{
    public string Id { get; init; } = "";
    public string Label => GameText.Text($"zone.{Id}.label");
    public WorldZoneKind Kind { get; init; }
    public string Description => GameText.Text($"zone.{Id}.description");
    public int ThreatLevel { get; init; }
    public float RewardMultiplier { get; init; }
    public bool AllowsExtraction { get; init; }
}

public class WorldRouteDefinition
{
    public string Id { get; init; } = "";
    public string Label => GameText.Text($"route.{Id}.label");
    public string Summary => GameText.Text($"route.{Id}.summary");
    public int ExtractionPointCount { get; init; } = 3;
    public WorldRouteZoneDefinition[] Zones { get; init; } = System.Array.Empty<WorldRouteZoneDefinition>();
}

public static class RouteData
{
    public static readonly WorldRouteDefinition[] Routes =
    {
        new()
        {
            Id = "combat-sandbox-route",
            ExtractionPointCount = 3,
            Zones = new[]
            {
                new WorldRouteZoneDefinition { Id = "perimeter-dock", Kind = WorldZoneKind.Perimeter, ThreatLevel = 1, RewardMultiplier = 1f, AllowsExtraction = true },
                new WorldRouteZoneDefinition { Id = "relay-nest", Kind = WorldZoneKind.HighRisk, ThreatLevel = 2, RewardMultiplier = 1.2f, AllowsExtraction = false },
                new WorldRouteZoneDefinition { Id = "vault-approach", Kind = WorldZoneKind.HighValue, ThreatLevel = 3, RewardMultiplier = 1.45f, AllowsExtraction = false },
                new WorldRouteZoneDefinition { Id = "freight-lift", Kind = WorldZoneKind.Extraction, ThreatLevel = 2, RewardMultiplier = 1.15f, AllowsExtraction = true },
            }
        },
        new()
        {
            Id = "foundry-loop-route",
            ExtractionPointCount = 3,
            Zones = new[]
            {
                new WorldRouteZoneDefinition { Id = "slag-yard", Kind = WorldZoneKind.Perimeter, ThreatLevel = 1, RewardMultiplier = 1.05f, AllowsExtraction = true },
                new WorldRouteZoneDefinition { Id = "smelter-core", Kind = WorldZoneKind.HighRisk, ThreatLevel = 3, RewardMultiplier = 1.35f, AllowsExtraction = false },
                new WorldRouteZoneDefinition { Id = "rail-elevator", Kind = WorldZoneKind.Extraction, ThreatLevel = 2, RewardMultiplier = 1.1f, AllowsExtraction = true },
            }
        },
        new()
        {
            Id = "frost-wharf-route",
            ExtractionPointCount = 4,
            Zones = new[]
            {
                new WorldRouteZoneDefinition { Id = "ice-dock", Kind = WorldZoneKind.Perimeter, ThreatLevel = 1, RewardMultiplier = 1f, AllowsExtraction = true },
                new WorldRouteZoneDefinition { Id = "cold-storage", Kind = WorldZoneKind.HighRisk, ThreatLevel = 2, RewardMultiplier = 1.18f, AllowsExtraction = false },
                new WorldRouteZoneDefinition { Id = "breaker-gate", Kind = WorldZoneKind.Extraction, ThreatLevel = 2, RewardMultiplier = 1.08f, AllowsExtraction = true },
            }
        },
        new()
        {
            Id = "archive-drop-route",
            ExtractionPointCount = 3,
            Zones = new[]
            {
                new WorldRouteZoneDefinition { Id = "surface-stack", Kind = WorldZoneKind.Perimeter, ThreatLevel = 1, RewardMultiplier = 1.04f, AllowsExtraction = true },
                new WorldRouteZoneDefinition { Id = "index-shaft", Kind = WorldZoneKind.HighValue, ThreatLevel = 2, RewardMultiplier = 1.22f, AllowsExtraction = false },
                new WorldRouteZoneDefinition { Id = "sealed-vault", Kind = WorldZoneKind.Extraction, ThreatLevel = 3, RewardMultiplier = 1.18f, AllowsExtraction = true },
            }
        },
        new()
        {
            Id = "blackwell-route",
            ExtractionPointCount = 3,
            Zones = new[]
            {
                new WorldRouteZoneDefinition { Id = "shaft-mouth", Kind = WorldZoneKind.Perimeter, ThreatLevel = 1, RewardMultiplier = 1.02f, AllowsExtraction = true },
                new WorldRouteZoneDefinition { Id = "maintenance-ring", Kind = WorldZoneKind.HighRisk, ThreatLevel = 2, RewardMultiplier = 1.2f, AllowsExtraction = false },
                new WorldRouteZoneDefinition { Id = "deep-anchor", Kind = WorldZoneKind.Extraction, ThreatLevel = 3, RewardMultiplier = 1.16f, AllowsExtraction = true },
            }
        },
    };

    public static readonly Dictionary<string, WorldRouteDefinition> ById;
    public static IReadOnlyList<WorldRouteDefinition> Maps => Routes;

    static RouteData()
    {
        ById = Routes.ToDictionary(r => r.Id);
    }

    public static WorldRouteDefinition GetRoute(string routeId)
    {
        return ById.TryGetValue(routeId, out var route) ? route : Routes[0];
    }

    public static WorldRouteDefinition GetMap(string mapId)
    {
        return GetRoute(mapId);
    }

    public static string GetNextRouteId(string currentRouteId)
    {
        int idx = System.Array.FindIndex(Routes, r => r.Id == currentRouteId);
        if (idx == -1) return Routes[0].Id;
        return Routes[(idx + 1) % Routes.Length].Id;
    }

    public static string GetNextMapId(string currentMapId)
    {
        return GetNextRouteId(currentMapId);
    }
}
