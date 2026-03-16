using System.Collections.Generic;

namespace ShotV.State;

public class WorldState
{
    public string SelectedRouteId { get; set; } = "combat-sandbox-route";
    public string SelectedZoneId { get; set; } = "perimeter-dock";
    public List<string> DiscoveredZones { get; set; } = new() { "perimeter-dock" };
    public string? ActiveRouteId { get; set; }

    public WorldState Clone() => new()
    {
        SelectedRouteId = SelectedRouteId,
        SelectedZoneId = SelectedZoneId,
        DiscoveredZones = new List<string>(DiscoveredZones),
        ActiveRouteId = ActiveRouteId,
    };
}
