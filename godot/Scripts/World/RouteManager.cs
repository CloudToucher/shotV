using System;
using System.Linq;
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
            LayoutSeed = MathUtil.BuildLayoutSeedFromText($"{route.Id}:{route.Zones[0].Id}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}:{Guid.NewGuid()}"),
            Zones = route.Zones.Select((z, i) => new RunZoneState
            {
                Id = z.Id,
                Label = z.Label,
                Kind = z.Kind,
                Status = i == 0 ? RunZoneStatus.Active : RunZoneStatus.Locked,
                ThreatLevel = z.ThreatLevel,
                RewardMultiplier = z.RewardMultiplier,
                AllowsExtraction = z.AllowsExtraction,
                Description = z.Description,
            }).ToList(),
            CurrentWave = 0,
            HighestWave = 0,
            HostilesRemaining = 0,
            Boss = new RunBossState(),
        };
    }

    public static RunZoneState? GetCurrentRunZone(RunMapState map)
    {
        return map.Zones.FirstOrDefault(z => z.Id == map.CurrentZoneId);
    }

    public static RunZoneState? GetNextRunZone(RunMapState map)
    {
        int idx = map.Zones.FindIndex(z => z.Id == map.CurrentZoneId);
        if (idx == -1 || idx + 1 >= map.Zones.Count) return null;
        return map.Zones[idx + 1];
    }

    public static bool IsCurrentRunZoneCleared(RunMapState map)
    {
        return GetCurrentRunZone(map)?.Status == RunZoneStatus.Cleared;
    }

    public static bool CanExtractFromRunMap(RunMapState map)
    {
        var zone = GetCurrentRunZone(map);
        return zone?.AllowsExtraction ?? false;
    }

    public static bool IsRunRouteComplete(RunMapState map)
    {
        var current = GetCurrentRunZone(map);
        return current != null && current.Status == RunZoneStatus.Cleared && GetNextRunZone(map) == null;
    }

    public static void MarkCurrentZoneCleared(RunMapState map)
    {
        foreach (var zone in map.Zones)
        {
            if (zone.Id == map.CurrentZoneId)
                zone.Status = RunZoneStatus.Cleared;
        }
        map.HostilesRemaining = 0;
        map.Boss.Defeated = true;
        map.Boss.Health = 0;
    }

    public static RunMapState? AdvanceRunMapZone(RunMapState map)
    {
        var current = GetCurrentRunZone(map);
        var next = GetNextRunZone(map);
        if (current == null || current.Status != RunZoneStatus.Cleared || next == null) return null;

        var newMap = map.Clone();
        newMap.CurrentZoneId = next.Id;
        newMap.LayoutSeed = MathUtil.BuildLayoutSeedFromText($"{map.RouteId}:{next.Id}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}:{Guid.NewGuid()}");
        foreach (var zone in newMap.Zones)
        {
            if (zone.Id == current.Id) zone.Status = RunZoneStatus.Cleared;
            else if (zone.Id == next.Id) zone.Status = RunZoneStatus.Active;
        }
        newMap.CurrentWave = 0;
        newMap.HighestWave = 0;
        newMap.HostilesRemaining = 0;
        newMap.Boss = new RunBossState();
        return newMap;
    }
}
