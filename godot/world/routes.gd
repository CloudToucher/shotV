extends RefCounted

static var _world_routes: Array = []
static var _initialized := false

static func _ensure_init() -> void:
	if _initialized:
		return
	_initialized = true
	_world_routes = [
		{
			"id": "combat-sandbox-route",
			"label": "前线穿廊",
			"summary": "由外环码头切入，穿过中继巢区，最后从货运升降机撤离。",
			"zones": [
				{ "id": "perimeter-dock", "label": "外围码头", "kind": "perimeter",
				  "description": "入口压力较低，适合确认手感和路线。",
				  "threat_level": 1, "reward_multiplier": 1.0, "allows_extraction": true },
				{ "id": "relay-nest", "label": "中继巢区", "kind": "high-risk",
				  "description": "敌群密度明显抬升，但回收效率也更高。",
				  "threat_level": 2, "reward_multiplier": 1.2, "allows_extraction": false },
				{ "id": "vault-approach", "label": "金库前厅", "kind": "high-value",
				  "description": "高价值区入口，敌人更耐打，掉落更偏向合金与研究样本。",
				  "threat_level": 3, "reward_multiplier": 1.45, "allows_extraction": false },
				{ "id": "freight-lift", "label": "货运升降机", "kind": "extraction",
				  "description": "终端撤离出口，完成压制后即可带走整局战利品。",
				  "threat_level": 2, "reward_multiplier": 1.15, "allows_extraction": true },
			],
		},
		{
			"id": "foundry-loop-route",
			"label": "熔炉环线",
			"summary": "线路更短、节奏更快，适合高频刷取基础资源。",
			"zones": [
				{ "id": "slag-yard", "label": "废渣场", "kind": "perimeter",
				  "description": "短线入口区，适合试火后快速撤离。",
				  "threat_level": 1, "reward_multiplier": 1.05, "allows_extraction": true },
				{ "id": "smelter-core", "label": "熔炉核心", "kind": "high-risk",
				  "description": "高压中段区域，合金产出更稳定。",
				  "threat_level": 3, "reward_multiplier": 1.35, "allows_extraction": false },
				{ "id": "rail-elevator", "label": "轨道电梯", "kind": "extraction",
				  "description": "路线终点，清空后可直接结束本轮行动。",
				  "threat_level": 2, "reward_multiplier": 1.1, "allows_extraction": true },
			],
		},
		{
			"id": "frost-wharf-route",
			"label": "霜港折返",
			"summary": "寒区港口副本，占位内容为主，后续会接入低能见度和环境危害。",
			"zones": [
				{ "id": "ice-dock", "label": "冰封泊位", "kind": "perimeter",
				  "description": "风压低、敌情轻，适合作为副本壳子占位。",
				  "threat_level": 1, "reward_multiplier": 1.0, "allows_extraction": true },
				{ "id": "cold-storage", "label": "冷库连廊", "kind": "high-risk",
				  "description": "占位区域，预留给环境交互和冻结机制。",
				  "threat_level": 2, "reward_multiplier": 1.18, "allows_extraction": false },
				{ "id": "breaker-gate", "label": "破冰闸门", "kind": "extraction",
				  "description": "临时出口，后续会替换为完整副本终点事件。",
				  "threat_level": 2, "reward_multiplier": 1.08, "allows_extraction": true },
			],
		},
		{
			"id": "archive-drop-route",
			"label": "资料库坠层",
			"summary": "档案设施副本，目前是结构空壳，后续用于高价值情报线。",
			"zones": [
				{ "id": "surface-stack", "label": "表层书库", "kind": "perimeter",
				  "description": "安静但视野复杂，适合作为探索模板。",
				  "threat_level": 1, "reward_multiplier": 1.04, "allows_extraction": true },
				{ "id": "index-shaft", "label": "索引井道", "kind": "high-value",
				  "description": "占位区，后续会塞入密码门和资料采集交互。",
				  "threat_level": 2, "reward_multiplier": 1.22, "allows_extraction": false },
				{ "id": "sealed-vault", "label": "封存库厅", "kind": "extraction",
				  "description": "终点出口，占位版本仅保留基础推进流程。",
				  "threat_level": 3, "reward_multiplier": 1.18, "allows_extraction": true },
			],
		},
		{
			"id": "blackwell-route",
			"label": "黑井穿梭",
			"summary": "竖井运输副本，现阶段提供路线选择壳子，后续接入垂直区域和平台交互。",
			"zones": [
				{ "id": "shaft-mouth", "label": "井口平台", "kind": "perimeter",
				  "description": "进场平台，保留快速撤离口。",
				  "threat_level": 1, "reward_multiplier": 1.02, "allows_extraction": true },
				{ "id": "maintenance-ring", "label": "维护环廊", "kind": "high-risk",
				  "description": "占位中段，用于后续平台切换和环形战区。",
				  "threat_level": 2, "reward_multiplier": 1.2, "allows_extraction": false },
				{ "id": "deep-anchor", "label": "深层锚点", "kind": "extraction",
				  "description": "深层出口，当前只保留地图与结算骨架。",
				  "threat_level": 3, "reward_multiplier": 1.16, "allows_extraction": true },
			],
		},
	]

static func get_all_routes() -> Array:
	_ensure_init()
	return _world_routes

static func get_world_route(route_id: String) -> Dictionary:
	_ensure_init()
	for route in _world_routes:
		if route.get("id") == route_id:
			return route
	if _world_routes.size() > 0:
		return _world_routes[0]
	return {}

static func get_next_world_route_id(current_route_id: String) -> String:
	_ensure_init()
	for i in _world_routes.size():
		if _world_routes[i].get("id") == current_route_id:
			return _world_routes[(i + 1) % _world_routes.size()].get("id", "")
	if _world_routes.size() > 0:
		return _world_routes[0].get("id", "")
	return ""

static func create_run_map_for_route(route_id: String) -> Dictionary:
	_ensure_init()
	var route := get_world_route(route_id)
	var zones_src: Array = route.get("zones", [])
	var zones: Array = []
	for i in zones_src.size():
		var z: Dictionary = zones_src[i]
		zones.append({
			"id": z.get("id", ""),
			"label": z.get("label", ""),
			"kind": z.get("kind", "perimeter"),
			"status": "active" if i == 0 else "locked",
			"threat_level": z.get("threat_level", 1),
			"reward_multiplier": z.get("reward_multiplier", 1.0),
			"allows_extraction": z.get("allows_extraction", false),
			"description": z.get("description", ""),
		})

	var first_zone_id: String = zones[0].get("id", "") if zones.size() > 0 else ""
	return {
		"scene_id": "combat-sandbox",
		"route_id": route.get("id", route_id),
		"current_zone_id": first_zone_id,
		"layout_seed": build_layout_seed("%s:%s" % [route_id, first_zone_id]),
		"zones": zones,
		"current_wave": 0,
		"highest_wave": 0,
		"hostiles_remaining": 0,
		"boss": {
			"spawned": false,
			"defeated": false,
			"label": "",
			"phase": 0,
			"health": 0,
			"max_health": 0,
		},
	}

static func get_current_run_zone(map: Dictionary) -> Dictionary:
	var current_id: String = map.get("current_zone_id", "")
	var zones: Array = map.get("zones", [])
	for zone in zones:
		if zone is Dictionary and zone.get("id") == current_id:
			return zone
	return {}

static func get_next_run_zone(map: Dictionary) -> Dictionary:
	var current_id: String = map.get("current_zone_id", "")
	var zones: Array = map.get("zones", [])
	for i in zones.size():
		if zones[i] is Dictionary and zones[i].get("id") == current_id:
			if i + 1 < zones.size():
				return zones[i + 1]
			return {}
	return {}

static func is_current_zone_cleared(map: Dictionary) -> bool:
	var zone := get_current_run_zone(map)
	return zone.get("status", "") == "cleared"

static func can_extract_from_map(map: Dictionary) -> bool:
	var zone := get_current_run_zone(map)
	return zone.get("allows_extraction", false)

static func is_route_complete(map: Dictionary) -> bool:
	var current := get_current_run_zone(map)
	if current.is_empty():
		return false
	return current.get("status") == "cleared" and get_next_run_zone(map).is_empty()

static func build_layout_seed(text: String) -> int:
	var hash_val: int = 2166136261
	for i in text.length():
		hash_val ^= text.unicode_at(i)
		hash_val = (hash_val * 16777619) & 0x7FFFFFFF
	return absi(hash_val)
