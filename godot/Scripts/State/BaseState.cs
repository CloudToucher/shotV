using System.Collections.Generic;
using ShotV.Data;

namespace ShotV.State;

public class BaseResources
{
    public int Salvage { get; set; }
    public int Alloy { get; set; }
    public int Research { get; set; }

    public BaseResources Clone() => new() { Salvage = Salvage, Alloy = Alloy, Research = Research };
}

public class BaseState
{
    public int FacilityLevel { get; set; } = 1;
    public int DeploymentCount { get; set; }
    public BaseResources Resources { get; set; } = new() { Salvage = 120, Alloy = 24, Research = 0 };
    public List<string> UnlockedStations { get; set; } = new() { "command", "workshop" };

    public BaseState Clone() => new()
    {
        FacilityLevel = FacilityLevel,
        DeploymentCount = DeploymentCount,
        Resources = Resources.Clone(),
        UnlockedStations = new List<string>(UnlockedStations),
    };
}
