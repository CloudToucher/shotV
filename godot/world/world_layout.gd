class_name WorldLayout
extends RefCounted

var id: String = ""
var seed_val: int = 0
var bounds: Rect2 = Rect2()
var player_spawn: Vector2 = Vector2.ZERO
var boss_spawn: Vector2 = Vector2.ZERO
var enemy_spawns: Array[Vector2] = []
var obstacles: Array[Dictionary] = []
var markers: Array[Dictionary] = []

static func create_combat_layout(route_id: String, zone_id: String,
		zone_label: String, threat_level: int, allows_extraction: bool,
		layout_seed: int) -> WorldLayout:
	var layout := WorldLayout.new()
	layout.id = "%s:%s" % [route_id, zone_id]
	layout.seed_val = layout_seed

	var width := 2800.0 + threat_level * 260.0
	var height := 2200.0 + threat_level * 220.0
	layout.bounds = Rect2(0, 0, width, height)
	layout.player_spawn = Vector2(width * 0.5, height - 220.0)
	layout.boss_spawn = Vector2(width * 0.5, 220.0)

	var exit_point := Vector2(width - 240.0, height - 260.0)
	var rng := _create_rng(layout_seed)

	var safety_rects: Array[Rect2] = [
		Rect2(layout.player_spawn.x - 180, layout.player_spawn.y - 160, 360, 260),
		Rect2(layout.boss_spawn.x - 220, layout.boss_spawn.y - 160, 440, 260),
		Rect2(exit_point.x - 180, exit_point.y - 150, 360, 240),
		Rect2(width * 0.5 - 120, 0, 240, height),
		Rect2(layout.player_spawn.x - 120, layout.player_spawn.y - 110,
			exit_point.x - layout.player_spawn.x + 240, 220),
	]

	var obstacle_target := 24 + threat_level * 6
	var margin := 140.0
	var index := 0
	var attempts := 0

	while layout.obstacles.size() < obstacle_target and attempts < obstacle_target * 24:
		attempts += 1
		var obs := {
			"id": "combat-obstacle-%d" % index,
			"x": margin + rng.call() * (width - margin * 2 - 240),
			"y": margin + rng.call() * (height - margin * 2 - 220),
			"width": 90.0 + rng.call() * (210.0 if threat_level >= 3 else 170.0),
			"height": 72.0 + rng.call() * (180.0 if threat_level >= 2 else 140.0),
			"kind": "wall" if rng.call() > 0.55 else "cover",
		}
		var obs_rect := Rect2(obs["x"], obs["y"], obs["width"], obs["height"])

		var blocked := false
		for sr in safety_rects:
			if sr.intersects(obs_rect):
				blocked = true
				break
		if blocked:
			continue

		var overlap := false
		for existing in layout.obstacles:
			var er := Rect2(existing["x"] - 48, existing["y"] - 48,
				existing["width"] + 96, existing["height"] + 96)
			if er.intersects(obs_rect):
				overlap = true
				break
		if overlap:
			continue

		layout.obstacles.append(obs)
		index += 1

	layout.enemy_spawns = _create_spawn_points(layout.bounds, layout.obstacles, layout.player_spawn)

	layout.markers = [
		{ "id": "entry", "x": layout.player_spawn.x, "y": layout.player_spawn.y + 90,
		  "label": "投送点", "kind": "entry" },
		{ "id": "objective", "x": layout.boss_spawn.x, "y": layout.boss_spawn.y,
		  "label": "%s核心" % zone_label, "kind": "objective" },
		{ "id": "exit", "x": exit_point.x, "y": exit_point.y,
		  "label": "撤离出口" if allows_extraction else "区域出口", "kind": "extraction" },
	]

	return layout

static func create_base_layout() -> WorldLayout:
	var layout := WorldLayout.new()
	layout.id = "base-camp"
	layout.seed_val = 20260314
	layout.bounds = Rect2(0, 0, 2240, 1680)
	layout.player_spawn = Vector2(1120, 1320)
	layout.boss_spawn = Vector2(1120, 280)

	layout.markers = [
		{ "id": "command", "x": 1120, "y": 320, "label": "指挥台", "kind": "station" },
		{ "id": "locker", "x": 740, "y": 720, "label": "储物柜", "kind": "locker" },
		{ "id": "workshop", "x": 1490, "y": 760, "label": "工坊台", "kind": "station" },
		{ "id": "launch", "x": 1120, "y": 1460, "label": "出击闸门", "kind": "entry" },
	]

	layout.obstacles = [
		_obs("north-wall-left", 468, 176, 220, 42, "wall"),
		_obs("north-wall-right", 1552, 176, 220, 42, "wall"),
		_obs("command-console", 1038, 368, 164, 70, "station"),
		_obs("command-rack-left", 952, 452, 82, 56, "station"),
		_obs("command-rack-right", 1206, 452, 82, 56, "station"),
		_obs("locker-bank-left", 654, 762, 84, 60, "locker"),
		_obs("locker-bank-right", 748, 762, 84, 60, "locker"),
		_obs("workbench-main", 1404, 800, 154, 62, "station"),
		_obs("workbench-rack", 1580, 786, 78, 78, "station"),
		_obs("cargo-crate-left", 886, 1106, 96, 58, "cover"),
		_obs("cargo-crate-right", 1258, 1106, 96, 58, "cover"),
		_obs("launch-pillar-left", 1030, 1492, 60, 74, "wall"),
		_obs("launch-pillar-right", 1150, 1492, 60, 74, "wall"),
		_obs("launch-console", 1088, 1386, 64, 42, "station"),
	]

	return layout

static func _obs(id: String, x: float, y: float, w: float, h: float, kind: String) -> Dictionary:
	return { "id": id, "x": x, "y": y, "width": w, "height": h, "kind": kind }

static func _create_rng(seed_val: int) -> Callable:
	var state := [seed_val & 0x7FFFFFFF]
	return func() -> float:
		state[0] = (state[0] ^ (state[0] >> 15)) & 0x7FFFFFFF
		state[0] = (state[0] * 16777619) & 0x7FFFFFFF
		state[0] = (state[0] ^ (state[0] + (state[0] ^ (state[0] >> 7)) * 61)) & 0x7FFFFFFF
		return float(state[0]) / 2147483647.0

static func _create_spawn_points(bounds: Rect2, obstacles: Array, player_spawn: Vector2) -> Array[Vector2]:
	var candidates: Array[Vector2] = [
		Vector2(bounds.position.x + 160, bounds.position.y + 180),
		Vector2(bounds.end.x - 160, bounds.position.y + 180),
		Vector2(bounds.position.x + 160, bounds.end.y - 180),
		Vector2(bounds.end.x - 160, bounds.end.y - 180),
		Vector2(bounds.position.x + 120, bounds.end.y * 0.55),
		Vector2(bounds.end.x - 120, bounds.end.y * 0.55),
		Vector2(bounds.position.x + bounds.end.x * 0.28, bounds.position.y + 120),
		Vector2(bounds.position.x + bounds.end.x * 0.72, bounds.position.y + 120),
		Vector2(bounds.position.x + bounds.end.x * 0.22, bounds.end.y - 120),
		Vector2(bounds.position.x + bounds.end.x * 0.78, bounds.end.y - 120),
	]

	var result: Array[Vector2] = []
	for point in candidates:
		if point.distance_to(player_spawn) < 380:
			continue
		var in_obstacle := false
		for obs in obstacles:
			if not obs is Dictionary:
				continue
			var expanded := Rect2(
				obs.get("x", 0.0) - 36, obs.get("y", 0.0) - 36,
				obs.get("width", 0.0) + 72, obs.get("height", 0.0) + 72)
			if expanded.has_point(point):
				in_obstacle = true
				break
		if not in_obstacle:
			result.append(point)
	return result
