extends Node

const SAVE_PATH := "user://shotv_save.json"
const SAVE_VERSION := 6

func save_state(data: Dictionary) -> void:
	data["version"] = SAVE_VERSION
	var json_string := JSON.stringify(data, "\t")
	var file := FileAccess.open(SAVE_PATH, FileAccess.WRITE)
	if file:
		file.store_string(json_string)
		file.close()

func load_state() -> Dictionary:
	if not FileAccess.file_exists(SAVE_PATH):
		return _create_initial_save()
	var file := FileAccess.open(SAVE_PATH, FileAccess.READ)
	if not file:
		return _create_initial_save()
	var json_string := file.get_as_text()
	file.close()
	var json := JSON.new()
	var err := json.parse(json_string)
	if err != OK:
		return _create_initial_save()
	var data = json.data
	if not data is Dictionary:
		return _create_initial_save()
	return _hydrate_save(data)

func clear_save() -> void:
	if FileAccess.file_exists(SAVE_PATH):
		DirAccess.remove_absolute(SAVE_PATH)

func _create_initial_save() -> Dictionary:
	var now := Time.get_datetime_string_from_system(true)
	return {
		"version": SAVE_VERSION,
		"created_at": now,
		"updated_at": now,
		"base": {
			"facility_level": 1,
			"deployment_count": 0,
			"resources": { "salvage": 120, "alloy": 24, "research": 0 },
			"unlocked_stations": ["command", "workshop"],
		},
		"inventory": {
			"stash_columns": 8,
			"stash_rows": 6,
			"equipped_weapon_ids": [
				GameData.WeaponType.MACHINE_GUN,
				GameData.WeaponType.GRENADE,
				GameData.WeaponType.SNIPER,
			],
			"equipped_armor_id": "",
			"stored_items": [],
		},
		"world": {
			"selected_route_id": "combat-sandbox-route",
			"selected_zone_id": "perimeter-dock",
			"discovered_zones": ["perimeter-dock"],
			"active_route_id": "",
		},
		"session": {
			"active_run": null,
			"last_extraction": null,
		},
		"settings": {
			"developer_mode": true,
		},
	}

func _hydrate_save(data: Dictionary) -> Dictionary:
	var initial := _create_initial_save()
	var result := initial.duplicate(true)

	if data.has("version"):
		result["version"] = data["version"]
	if data.has("created_at"):
		result["created_at"] = str(data["created_at"])
	if data.has("updated_at"):
		result["updated_at"] = str(data["updated_at"])

	if data.has("base") and data["base"] is Dictionary:
		var base: Dictionary = data["base"]
		var rb: Dictionary = result["base"]
		rb["facility_level"] = _read_int(base, "facility_level", rb["facility_level"])
		rb["deployment_count"] = _read_int(base, "deployment_count", rb["deployment_count"])
		if base.has("resources") and base["resources"] is Dictionary:
			var res: Dictionary = base["resources"]
			var rr: Dictionary = rb["resources"]
			rr["salvage"] = _read_int(res, "salvage", rr["salvage"])
			rr["alloy"] = _read_int(res, "alloy", rr["alloy"])
			rr["research"] = _read_int(res, "research", rr["research"])

	if data.has("inventory") and data["inventory"] is Dictionary:
		var inv: Dictionary = data["inventory"]
		var ri: Dictionary = result["inventory"]
		ri["stash_columns"] = _read_int(inv, "stash_columns", ri["stash_columns"])
		ri["stash_rows"] = _read_int(inv, "stash_rows", ri["stash_rows"])
		if inv.has("stored_items") and inv["stored_items"] is Array:
			ri["stored_items"] = inv["stored_items"]
		if inv.has("equipped_weapon_ids") and inv["equipped_weapon_ids"] is Array:
			ri["equipped_weapon_ids"] = inv["equipped_weapon_ids"]

	if data.has("world") and data["world"] is Dictionary:
		var world: Dictionary = data["world"]
		var rw: Dictionary = result["world"]
		rw["selected_route_id"] = _read_str(world, "selected_route_id", rw["selected_route_id"])
		rw["selected_zone_id"] = _read_str(world, "selected_zone_id", rw["selected_zone_id"])
		if world.has("discovered_zones") and world["discovered_zones"] is Array:
			rw["discovered_zones"] = world["discovered_zones"]
		rw["active_route_id"] = _read_str(world, "active_route_id", rw["active_route_id"])

	if data.has("session") and data["session"] is Dictionary:
		var sess: Dictionary = data["session"]
		result["session"] = sess.duplicate(true)

	if data.has("settings") and data["settings"] is Dictionary:
		var sett: Dictionary = data["settings"]
		var rs: Dictionary = result["settings"]
		if sett.has("developer_mode"):
			rs["developer_mode"] = bool(sett["developer_mode"])

	return result

func _read_int(dict: Dictionary, key: String, fallback: int) -> int:
	if dict.has(key) and (dict[key] is int or dict[key] is float):
		return int(dict[key])
	return fallback

func _read_str(dict: Dictionary, key: String, fallback: String) -> String:
	if dict.has(key) and dict[key] is String:
		return dict[key]
	return fallback
