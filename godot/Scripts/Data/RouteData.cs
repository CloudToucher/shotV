using System.Collections.Generic;
using System.Linq;
using ShotV.Core;

namespace ShotV.Data;

public class WorldRouteZoneDefinition
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public WorldZoneKind Kind { get; init; }
    public string Description { get; init; } = "";
    public int ThreatLevel { get; init; }
    public float RewardMultiplier { get; init; }
    public bool AllowsExtraction { get; init; }
}

public class WorldRouteDefinition
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Summary { get; init; } = "";
    public WorldRouteZoneDefinition[] Zones { get; init; } = System.Array.Empty<WorldRouteZoneDefinition>();
}

public static class RouteData
{
    public static readonly WorldRouteDefinition[] Routes =
    {
        new()
        {
            Id = "combat-sandbox-route", Label = "前线穿廊",
            Summary = "由外环码头切入，穿过中继巢区，最后从货运升降机撤离。",
            Zones = new[]
            {
                new WorldRouteZoneDefinition { Id = "perimeter-dock", Label = "外围码头", Kind = WorldZoneKind.Perimeter, Description = "入口压力较低，适合确认手感和路线。", ThreatLevel = 1, RewardMultiplier = 1f, AllowsExtraction = true },
                new WorldRouteZoneDefinition { Id = "relay-nest", Label = "中继巢区", Kind = WorldZoneKind.HighRisk, Description = "敌群密度明显抬升，但回收效率也更高。", ThreatLevel = 2, RewardMultiplier = 1.2f, AllowsExtraction = false },
                new WorldRouteZoneDefinition { Id = "vault-approach", Label = "金库前厅", Kind = WorldZoneKind.HighValue, Description = "高价值区入口，敌人更耐打，掉落更偏向合金与研究样本。", ThreatLevel = 3, RewardMultiplier = 1.45f, AllowsExtraction = false },
                new WorldRouteZoneDefinition { Id = "freight-lift", Label = "货运升降机", Kind = WorldZoneKind.Extraction, Description = "终端撤离出口，完成压制后即可带走整局战利品。", ThreatLevel = 2, RewardMultiplier = 1.15f, AllowsExtraction = true },
            }
        },
        new()
        {
            Id = "foundry-loop-route", Label = "熔炉环线",
            Summary = "线路更短、节奏更快，适合高频刷取基础资源。",
            Zones = new[]
            {
                new WorldRouteZoneDefinition { Id = "slag-yard", Label = "废渣场", Kind = WorldZoneKind.Perimeter, Description = "短线入口区，适合试火后快速撤离。", ThreatLevel = 1, RewardMultiplier = 1.05f, AllowsExtraction = true },
                new WorldRouteZoneDefinition { Id = "smelter-core", Label = "熔炉核心", Kind = WorldZoneKind.HighRisk, Description = "高压中段区域，合金产出更稳定。", ThreatLevel = 3, RewardMultiplier = 1.35f, AllowsExtraction = false },
                new WorldRouteZoneDefinition { Id = "rail-elevator", Label = "轨道电梯", Kind = WorldZoneKind.Extraction, Description = "路线终点，清空后可直接结束本轮行动。", ThreatLevel = 2, RewardMultiplier = 1.1f, AllowsExtraction = true },
            }
        },
        new()
        {
            Id = "frost-wharf-route", Label = "霜港折返",
            Summary = "寒区港口副本，占位内容为主，后续会接入低能见度和环境危害。",
            Zones = new[]
            {
                new WorldRouteZoneDefinition { Id = "ice-dock", Label = "冰封泊位", Kind = WorldZoneKind.Perimeter, Description = "风压低、敌情轻，适合作为副本壳子占位。", ThreatLevel = 1, RewardMultiplier = 1f, AllowsExtraction = true },
                new WorldRouteZoneDefinition { Id = "cold-storage", Label = "冷库连廊", Kind = WorldZoneKind.HighRisk, Description = "占位区域，预留给环境交互和冻结机制。", ThreatLevel = 2, RewardMultiplier = 1.18f, AllowsExtraction = false },
                new WorldRouteZoneDefinition { Id = "breaker-gate", Label = "破冰闸门", Kind = WorldZoneKind.Extraction, Description = "临时出口，后续会替换为完整副本终点事件。", ThreatLevel = 2, RewardMultiplier = 1.08f, AllowsExtraction = true },
            }
        },
        new()
        {
            Id = "archive-drop-route", Label = "资料库坠层",
            Summary = "档案设施副本，目前是结构空壳，后续用于高价值情报线。",
            Zones = new[]
            {
                new WorldRouteZoneDefinition { Id = "surface-stack", Label = "表层书库", Kind = WorldZoneKind.Perimeter, Description = "安静但视野复杂，适合作为探索模板。", ThreatLevel = 1, RewardMultiplier = 1.04f, AllowsExtraction = true },
                new WorldRouteZoneDefinition { Id = "index-shaft", Label = "索引井道", Kind = WorldZoneKind.HighValue, Description = "占位区，后续会塞入密码门和资料采集交互。", ThreatLevel = 2, RewardMultiplier = 1.22f, AllowsExtraction = false },
                new WorldRouteZoneDefinition { Id = "sealed-vault", Label = "封存库厅", Kind = WorldZoneKind.Extraction, Description = "终点出口，占位版本仅保留基础推进流程。", ThreatLevel = 3, RewardMultiplier = 1.18f, AllowsExtraction = true },
            }
        },
        new()
        {
            Id = "blackwell-route", Label = "黑井穿梭",
            Summary = "竖井运输副本，现阶段提供路线选择壳子，后续接入垂直区域和平台交互。",
            Zones = new[]
            {
                new WorldRouteZoneDefinition { Id = "shaft-mouth", Label = "井口平台", Kind = WorldZoneKind.Perimeter, Description = "进场平台，保留快速撤离口。", ThreatLevel = 1, RewardMultiplier = 1.02f, AllowsExtraction = true },
                new WorldRouteZoneDefinition { Id = "maintenance-ring", Label = "维护环廊", Kind = WorldZoneKind.HighRisk, Description = "占位中段，用于后续平台切换和环形战区。", ThreatLevel = 2, RewardMultiplier = 1.2f, AllowsExtraction = false },
                new WorldRouteZoneDefinition { Id = "deep-anchor", Label = "深层锚点", Kind = WorldZoneKind.Extraction, Description = "深层出口，当前只保留地图与结算骨架。", ThreatLevel = 3, RewardMultiplier = 1.16f, AllowsExtraction = true },
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
