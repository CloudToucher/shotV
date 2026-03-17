using System;
using System.Linq;
using Godot;
using ShotV.Core;
using ShotV.Data;
using ShotV.State;

namespace ShotV.World;

public static class RouteManager
{
    public static RunMapState CreateRunMapStateForRoute(string routeId)
    {
        var route = RouteData.GetRoute(routeId);
        return new RunMapState
        {
            RouteId = route.Id,
            CurrentZoneId = route.Zones[0].Id,
            LayoutSeed = MathUtil.BuildLayoutSeedFromText($"{route.Id}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}:{Guid.NewGuid()}"),
            Zones = route.Zones.Select(zone => new RunZoneState
            {
                Id = zone.Id,
                Label = zone.Label,
                Kind = zone.Kind,
                Status = RunZoneStatus.Active,
                ThreatLevel = zone.ThreatLevel,
                RewardMultiplier = zone.RewardMultiplier,
                AllowsExtraction = zone.AllowsExtraction,
                Description = zone.Description,
            }).ToList(),
            CurrentWave = 0,
            HighestWave = 0,
            HostilesRemaining = 0,
            Boss = new RunBossState(),
        };
    }

    public static RunZoneState? GetCurrentRunZone(RunMapState map)
    {
        return map.Zones.FirstOrDefault(zone => zone.Id == map.CurrentZoneId);
    }

    public static RunZoneState? GetNextRunZone(RunMapState map)
    {
        return null;
    }

    public static bool IsCurrentRunZoneCleared(RunMapState map)
    {
        return false;
    }

    public static bool CanExtractFromRunMap(RunMapState map)
    {
        return map.Zones.Count > 0;
    }

    public static bool IsRunRouteComplete(RunMapState map)
    {
        return false;
    }

    public static void MarkCurrentZoneCleared(RunMapState map)
    {
    }

    public static RunMapState? AdvanceRunMapZone(RunMapState map)
    {
        return null;
    }

    public static bool SetCurrentRunZone(RunMapState map, string zoneId)
    {
        if (map.Zones.All(zone => zone.Id != zoneId))
            return false;

        map.CurrentZoneId = zoneId;
        return true;
    }

    public static RunZoneState? ResolveZoneAtPosition(RunMapState map, WorldMapLayout layout, Vector2 position)
    {
        var region = layout.GetRegionAtPosition(position);
        if (region == null)
            return GetCurrentRunZone(map);

        return map.Zones.FirstOrDefault(zone => zone.Id == region.Id) ?? GetCurrentRunZone(map);
    }
}
