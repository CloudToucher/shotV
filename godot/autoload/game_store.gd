extends Node

signal mode_changed(new_mode: String, old_mode: String)
signal state_changed()
signal run_structure_changed()

var mode: String = "base" # "base" or "combat"
var save: Dictionary = {}
var hydrated: bool = false

var runtime_primary_action_ready: bool = false
var runtime_primary_action_hint: String = ""
var runtime_nearby_marker_id: String = ""
var runtime_nearby_marker_label: String = ""
var runtime_nearby_marker_kind: String = ""
var runtime_map_overlay_open: bool = false

func _ready() -> void:
	save = SaveManager.load_state()
	hydrated = true
	var active_run = save.get("session", {}).get("active_run")
	if active_run != null:
		mode = "combat"
	else:
		mode = "base"

func get_save() -> Dictionary:
	return save

func get_mode() -> String:
	return mode

func set_mode_to(new_mode: String) -> void:
	if new_mode == mode:
		return
	var old := mode
	mode = new_mode
	_clear_runtime()
	mode_changed.emit(new_mode, old)

func deploy_combat() -> void:
	if save.get("session", {}).get("active_run") != null:
		return
	if not runtime_primary_action_ready:
		return

	var timestamp := Time.get_datetime_string_from_system(true)
	var run_id := "run-%s-%s" % [str(Time.get_ticks_msec()), str(randi() % 100000)]
	var route_id: String = save.get("world", {}).get("selected_route_id", "combat-sandbox-route")
	var Routes := preload("res://world/routes.gd")
	var map_state := Routes.create_run_map_for_route(route_id)
	var loadout: Array = save.get("inventory", {}).get("equipped_weapon_ids",
		[GameData.WeaponType.MACHINE_GUN, GameData.WeaponType.GRENADE, GameData.WeaponType.SNIPER])

	var run := {
		"id": run_id,
		"scene_id": "combat-sandbox",
		"entered_at": timestamp,
		"status": "active",
		"pending_outcome": "",
		"player": {
			"health": 100,
			"max_health": 100,
			"current_weapon_id": loadout[0] if loadout.size() > 0 else GameData.WeaponType.MACHINE_GUN,
			"loadout_weapon_ids": loadout,
			"shots_fired": 0,
			"grenades_thrown": 0,
			"dashes_used": 0,
			"damage_taken": 0,
		},
		"map": map_state,
		"inventory": {
			"columns": 6,
			"rows": 4,
			"items": [],
			"quick_slots": ["", "", "", ""],
		},
		"ground_loot": [],
		"resources": { "salvage": 0, "alloy": 0, "research": 0 },
		"loot_entries": [],
		"stats": {
			"elapsed_seconds": 0.0,
			"kills": 0,
			"highest_wave": 0,
			"extracted": false,
			"boss_defeated": false,
		},
	}

	save["updated_at"] = timestamp
	if not save.has("base"):
		save["base"] = {}
	save["base"]["deployment_count"] = save["base"].get("deployment_count", 0) + 1
	if not save.has("session"):
		save["session"] = {}
	save["session"]["active_run"] = run
	_merge_world_with_map(map_state)

	_persist()
	set_mode_to("combat")
	run_structure_changed.emit()

func sync_active_run(snapshot: Dictionary) -> void:
	var active_run = save.get("session", {}).get("active_run")
	if active_run == null:
		return
	_merge_run_snapshot(active_run, snapshot)
	save["updated_at"] = Time.get_datetime_string_from_system(true)
	_merge_world_with_map(active_run.get("map", {}))
	_persist()
	state_changed.emit()

func mark_current_zone_cleared(snapshot: Dictionary = {}) -> void:
	var active_run = save.get("session", {}).get("active_run")
	if active_run == null or active_run.get("status") != "active":
		return
	if not snapshot.is_empty():
		_merge_run_snapshot(active_run, snapshot)

	var map: Dictionary = active_run.get("map", {})
	var zones: Array = map.get("zones", [])
	var current_zone_id: String = map.get("current_zone_id", "")
	for zone in zones:
		if zone is Dictionary and zone.get("id") == current_zone_id:
			zone["status"] = "cleared"
	map["hostiles_remaining"] = 0
	var boss: Dictionary = map.get("boss", {})
	boss["defeated"] = true
	boss["health"] = 0
	save["updated_at"] = Time.get_datetime_string_from_system(true)
	_merge_world_with_map(map)
	_persist()
	state_changed.emit()

func mark_run_outcome(outcome: String, snapshot: Dictionary = {}) -> void:
	var active_run = save.get("session", {}).get("active_run")
	if active_run == null:
		return
	if not snapshot.is_empty():
		_merge_run_snapshot(active_run, snapshot)
	active_run["status"] = "awaiting-settlement"
	active_run["pending_outcome"] = outcome
	if outcome == "down":
		active_run["player"]["health"] = 0
	save["updated_at"] = Time.get_datetime_string_from_system(true)
	_persist()
	state_changed.emit()

func resolve_active_run_to_base(outcome: String = "extracted") -> void:
	var active_run = save.get("session", {}).get("active_run")
	if active_run == null:
		return
	var resolved_outcome: String = active_run.get("pending_outcome", outcome)
	if resolved_outcome.is_empty():
		resolved_outcome = outcome

	var timestamp := Time.get_datetime_string_from_system(true)
	var extraction := {
		"run_id": active_run.get("id", ""),
		"outcome": resolved_outcome,
		"success": resolved_outcome != "down",
		"resolved_at": timestamp,
		"duration_seconds": active_run.get("stats", {}).get("elapsed_seconds", 0),
		"kills": active_run.get("stats", {}).get("kills", 0),
		"highest_wave": active_run.get("stats", {}).get("highest_wave", 0),
		"boss_defeated": active_run.get("stats", {}).get("boss_defeated", false),
		"resources_recovered": { "salvage": 0, "alloy": 0, "research": 0 },
		"resources_lost": { "salvage": 0, "alloy": 0, "research": 0 },
	}

	if resolved_outcome != "down":
		var run_resources: Dictionary = active_run.get("resources", {})
		extraction["resources_recovered"] = run_resources.duplicate()
		var base_res: Dictionary = save.get("base", {}).get("resources", {})
		base_res["salvage"] = base_res.get("salvage", 0) + run_resources.get("salvage", 0)
		base_res["alloy"] = base_res.get("alloy", 0) + run_resources.get("alloy", 0)
		base_res["research"] = base_res.get("research", 0) + run_resources.get("research", 0)

	save.get("session", {})["active_run"] = null
	save.get("session", {})["last_extraction"] = extraction
	var world: Dictionary = save.get("world", {})
	world["active_route_id"] = ""
	save["updated_at"] = timestamp
	_persist()
	set_mode_to("base")
	run_structure_changed.emit()

func advance_active_run_zone() -> void:
	var active_run = save.get("session", {}).get("active_run")
	if active_run == null or active_run.get("status") != "active":
		return
	var map: Dictionary = active_run.get("map", {})
	var zones: Array = map.get("zones", [])
	var current_id: String = map.get("current_zone_id", "")
	var current_idx := -1
	for i in zones.size():
		if zones[i] is Dictionary and zones[i].get("id") == current_id:
			current_idx = i
			break
	if current_idx == -1 or current_idx + 1 >= zones.size():
		return
	var current_zone: Dictionary = zones[current_idx]
	if current_zone.get("status") != "cleared":
		return
	var next_zone: Dictionary = zones[current_idx + 1]
	next_zone["status"] = "active"
	map["current_zone_id"] = next_zone["id"]
	map["current_wave"] = 0
	map["highest_wave"] = 0
	map["hostiles_remaining"] = 0
	map["boss"] = { "spawned": false, "defeated": false, "label": "", "phase": 0, "health": 0, "max_health": 0 }
	var Routes := preload("res://world/routes.gd")
	map["layout_seed"] = Routes.build_layout_seed("%s:%s:%s" % [map.get("route_id", ""), next_zone["id"], str(Time.get_ticks_msec())])
	save["updated_at"] = Time.get_datetime_string_from_system(true)
	_merge_world_with_map(map)
	_persist()
	run_structure_changed.emit()

func select_world_route(route_id: String) -> void:
	if save.get("session", {}).get("active_run") != null:
		return
	var Routes := preload("res://world/routes.gd")
	var route := Routes.get_world_route(route_id)
	if route.is_empty():
		return
	var world: Dictionary = save.get("world", {})
	world["selected_route_id"] = route_id
	if route.has("zones") and route["zones"] is Array and route["zones"].size() > 0:
		world["selected_zone_id"] = route["zones"][0].get("id", "")
	save["updated_at"] = Time.get_datetime_string_from_system(true)
	_persist()
	state_changed.emit()

func reset_save() -> void:
	SaveManager.clear_save()
	save = SaveManager.load_state()
	mode = "base"
	_clear_runtime()
	_persist()
	state_changed.emit()
	run_structure_changed.emit()

func update_scene_runtime(patch: Dictionary) -> void:
	if patch.has("primary_action_ready"):
		runtime_primary_action_ready = patch["primary_action_ready"]
	if patch.has("primary_action_hint"):
		runtime_primary_action_hint = patch["primary_action_hint"]
	if patch.has("nearby_marker_id"):
		runtime_nearby_marker_id = patch["nearby_marker_id"]
	if patch.has("nearby_marker_label"):
		runtime_nearby_marker_label = patch["nearby_marker_label"]
	if patch.has("nearby_marker_kind"):
		runtime_nearby_marker_kind = patch["nearby_marker_kind"]
	if patch.has("map_overlay_open"):
		runtime_map_overlay_open = patch["map_overlay_open"]

func _clear_runtime() -> void:
	runtime_primary_action_ready = false
	runtime_primary_action_hint = ""
	runtime_nearby_marker_id = ""
	runtime_nearby_marker_label = ""
	runtime_nearby_marker_kind = ""
	runtime_map_overlay_open = false

func _persist() -> void:
	SaveManager.save_state(save)

func _merge_run_snapshot(run: Dictionary, snapshot: Dictionary) -> void:
	if snapshot.has("player"):
		var p: Dictionary = snapshot["player"]
		var rp: Dictionary = run.get("player", {})
		for key in p:
			rp[key] = p[key]
	if snapshot.has("map"):
		var m: Dictionary = snapshot["map"]
		var rm: Dictionary = run.get("map", {})
		for key in m:
			rm[key] = m[key]
	if snapshot.has("inventory"):
		run["inventory"] = snapshot["inventory"]
	if snapshot.has("ground_loot"):
		run["ground_loot"] = snapshot["ground_loot"]
	if snapshot.has("resources"):
		run["resources"] = snapshot["resources"]
	if snapshot.has("loot_entries"):
		run["loot_entries"] = snapshot["loot_entries"]
	if snapshot.has("stats"):
		var s: Dictionary = snapshot["stats"]
		var rs: Dictionary = run.get("stats", {})
		for key in s:
			rs[key] = s[key]

func _merge_world_with_map(map: Dictionary) -> void:
	var world: Dictionary = save.get("world", {})
	world["selected_route_id"] = map.get("route_id", world.get("selected_route_id", ""))
	world["selected_zone_id"] = map.get("current_zone_id", world.get("selected_zone_id", ""))
	var discovered: Array = world.get("discovered_zones", [])
	var zones: Array = map.get("zones", [])
	for zone in zones:
		if zone is Dictionary and zone.get("status", "locked") != "locked":
			var zid: String = zone.get("id", "")
			if zid != "" and not discovered.has(zid):
				discovered.append(zid)
	world["discovered_zones"] = discovered
	if world.get("active_route_id", "").is_empty():
		world["active_route_id"] = map.get("route_id", "")
