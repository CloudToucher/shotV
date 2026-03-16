extends Node2D

const WORLD_CAMERA_ZOOM := 1.4
const GRID_SIZE := 56.0
const PLAYER_MAX_HEALTH := 100
const MACHINE_GUN_DAMAGE := 11
const GRENADE_DAMAGE := 34
const SNIPER_DAMAGE := 48
const MACHINE_GUN_SPEED := 960.0
const SNIPER_SPEED := 1560.0
const WAVE_START_DELAY := 0.75
const NEXT_WAVE_DELAY := 1.1
const RUN_SYNC_INTERVAL := 0.2
const INTERACTION_RADIUS := 120.0
const EXIT_ADVANCE_CHANNEL := 0.8
const EXIT_EXTRACT_CHANNEL := 1.1
const EXIT_GATE_OPEN := 0.42

var layout: WorldLayout = null
var player: PlayerAvatar
var camera: Camera2D
var terrain_node: Node2D
var obstacle_layer: Node2D
var marker_layer: Node2D
var enemy_layer: Node2D
var loot_layer: Node2D
var effects_layer: Node2D

var _hud: CombatHUD
var _minimap: Minimap
var _toast: ToastOverlay

var player_health: int = PLAYER_MAX_HEALTH
var current_weapon_slot: int = 1
var shot_cooldown: float = 0.0
var elapsed_seconds: float = 0.0
var run_elapsed: float = 0.0
var run_kills: int = 0
var run_highest_wave: int = 0
var zone_highest_wave: int = 0
var sync_timer: float = RUN_SYNC_INTERVAL
var shake_trauma: float = 0.0
var toast_timer: float = 0.0
var panel_open: bool = false
var map_open: bool = false

var enemies: Array = []
var ground_loot: Array = []
var enemy_projectiles: Array = []
var pending_spawns: Array = []
var burst_rings: Array = []
var needle_projectiles: Array = []
var grenade_projectiles: Array = []

var wave_index: int = 0
var next_wave_delay: float = WAVE_START_DELAY
var spawn_timer: float = 0.0
var enemy_id_counter: int = 0
var encounter_state: String = "active" # "active", "down", "clear"

var pending_exit_action: Dictionary = {}
var loadout_weapon_ids: Array = []

func _ready() -> void:
	camera = $Camera2D
	terrain_node = $Terrain
	obstacle_layer = $ObstacleLayer
	marker_layer = $MarkerLayer
	enemy_layer = $EnemyLayer
	
	loot_layer = Node2D.new()
	add_child(loot_layer)
	
	effects_layer = $EffectsLayer
	move_child(loot_layer, effects_layer.get_index())

	_hud = CombatHUD.new()
	_hud.set_anchors_preset(Control.PRESET_FULL_RECT)
	$UILayer.add_child(_hud)
	
	_minimap = Minimap.new()
	_minimap.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	_minimap.position = Vector2(get_viewport_rect().size.x - 220, 20)
	_minimap.size = Vector2(200, 200)
	$UILayer.add_child(_minimap)
	
	_toast = ToastOverlay.new()
	_toast.set_anchors_preset(Control.PRESET_TOP_WIDE)
	$UILayer.add_child(_toast)

	if $UILayer.has_node("HUD"):
		$UILayer/HUD.queue_free()

	player = PlayerAvatar.new()
	$Player.add_child(player)

	GameStore.run_structure_changed.connect(_on_run_structure_changed)
	_bootstrap_from_store()

func _on_run_structure_changed() -> void:
	_bootstrap_from_store()

func _bootstrap_from_store() -> void:
	var active_run = GameStore.save.get("session", {}).get("active_run")
	_clear_encounter_state()

	if active_run == null:
		layout = null
		player_health = PLAYER_MAX_HEALTH
		player.reset_state()
		player.set_life_ratio(1.0)
		return

	var map: Dictionary = active_run.get("map", {})
	var Routes := preload("res://world/routes.gd")
	var current_zone := Routes.get_current_run_zone(map)
	var zone_label: String = current_zone.get("label", "未知区域")
	var threat_level: int = current_zone.get("threat_level", 1)
	var allows_extraction: bool = current_zone.get("allows_extraction", false)
	var layout_seed: int = map.get("layout_seed", 1)

	layout = WorldLayout.create_combat_layout(
		map.get("route_id", ""), map.get("current_zone_id", ""),
		zone_label, threat_level, allows_extraction, layout_seed)

	_minimap.setup(layout.bounds, layout.obstacles, layout.markers)
	_rebuild_world_visuals()
	_hydrate_run(active_run)
	_rebuild_loot_visuals()

func _hydrate_run(run: Dictionary) -> void:
	var p: Dictionary = run.get("player", {})
	loadout_weapon_ids = p.get("loadout_weapon_ids", [GameData.WeaponType.MACHINE_GUN, GameData.WeaponType.GRENADE, GameData.WeaponType.SNIPER])
	player_health = p.get("health", PLAYER_MAX_HEALTH)
	run_elapsed = run.get("stats", {}).get("elapsed_seconds", 0.0)
	run_kills = run.get("stats", {}).get("kills", 0)
	run_highest_wave = run.get("stats", {}).get("highest_wave", 0)
	zone_highest_wave = run.get("map", {}).get("highest_wave", 0)
	ground_loot = run.get("ground_loot", [])

	player.reset_state()
	if layout:
		player.position = layout.player_spawn
	player.set_life_ratio(float(player_health) / PLAYER_MAX_HEALTH)

	var weapon_id: int = p.get("current_weapon_id", GameData.WeaponType.MACHINE_GUN)
	player.set_weapon_style(weapon_id)
	_set_weapon_slot_from_type(weapon_id)

	var status: String = run.get("status", "active")
	if status == "awaiting-settlement":
		encounter_state = "clear"
		_show_toast("等待结算", "请返回基地查看结果")
		return

	var Routes := preload("res://world/routes.gd")
	var map: Dictionary = run.get("map", {})
	var current_zone := Routes.get_current_run_zone(map)
	if current_zone.get("status") == "cleared":
		encounter_state = "clear"
		_show_toast("区域已压制", "前往出口后可继续推进")
		return

	if map.get("current_wave", 0) > 0 or map.get("hostiles_remaining", 0) > 0:
		wave_index = map.get("current_wave", 0)
		next_wave_delay = 0.35
		_show_toast(current_zone.get("label", "行动已恢复"), current_zone.get("description", ""))
		return

	_show_toast(current_zone.get("label", "战区接入完成"), current_zone.get("description", "探索当前区域"))

func _rebuild_world_visuals() -> void:
	if layout == null:
		return
	for child in terrain_node.get_children():
		child.queue_free()
	var terrain_draw := _TerrainDraw.new()
	terrain_draw.layout = layout
	terrain_draw.grid_size = GRID_SIZE
	terrain_node.add_child(terrain_draw)

	for child in obstacle_layer.get_children():
		child.queue_free()
	var obs_draw := _ObstacleDraw.new()
	obs_draw.obstacles = layout.obstacles
	obstacle_layer.add_child(obs_draw)

	for child in marker_layer.get_children():
		child.queue_free()
	var marker_draw := _MarkerDraw.new()
	marker_draw.markers = layout.markers
	marker_layer.add_child(marker_draw)

func _clear_encounter_state() -> void:
	encounter_state = "active"
	wave_index = 0
	run_kills = 0
	next_wave_delay = WAVE_START_DELAY
	spawn_timer = 0.0
	pending_spawns.clear()
	enemy_projectiles.clear()
	burst_rings.clear()
	needle_projectiles.clear()
	grenade_projectiles.clear()
	pending_exit_action = {}
	sync_timer = RUN_SYNC_INTERVAL
	run_elapsed = 0.0
	run_highest_wave = 0
	zone_highest_wave = 0
	ground_loot.clear()
	for enemy_data in enemies:
		if enemy_data.has("avatar") and enemy_data["avatar"] is Node:
			enemy_data["avatar"].queue_free()
	enemies.clear()
	shake_trauma = 0.0

func _process(delta: float) -> void:
	elapsed_seconds += delta
	shot_cooldown = maxf(0.0, shot_cooldown - delta)
	shake_trauma = maxf(0.0, shake_trauma - delta * 2.4)

	if layout == null:
		return

	if Input.is_action_just_pressed("panel_toggle"):
		panel_open = not panel_open
		if panel_open:
			map_open = false

	if Input.is_action_just_pressed("map_toggle"):
		map_open = not map_open
		if map_open:
			panel_open = false

	var active_run = GameStore.save.get("session", {}).get("active_run")
	var can_control := not panel_open and not map_open and not pending_exit_action.size() > 0 \
		and active_run != null and active_run.get("status") == "active" and encounter_state != "down"

	if can_control:
		run_elapsed += delta
		_handle_player_input(delta)
	else:
		player.set_move_intent(0.0, 0.0)

	player.update_avatar(delta, layout.bounds, elapsed_seconds, layout.obstacles)

	if active_run != null and active_run.get("status") == "active" and not panel_open and not map_open:
		if Input.is_action_just_pressed("interact"):
			_handle_world_interaction(active_run)
			_handle_loot_interaction(active_run)

	if encounter_state == "active" and not panel_open and not map_open:
		_advance_spawn_queue(delta)
		_update_enemies(delta)
		_update_enemy_projectiles(delta)

	_tick_exit_action(delta)
	_update_transient_effects(delta)
	_apply_camera()
	_draw_effects()
	_update_hud()
	
	_minimap.update_state(player.position, player.aim_angle, enemies, delta)
	_minimap.visible = map_open

	if not panel_open and not map_open and encounter_state == "active":
		sync_timer -= delta
		if sync_timer <= 0.0:
			_flush_run_snapshot()
			sync_timer = RUN_SYNC_INTERVAL

func _handle_player_input(delta: float) -> void:
	var move_x := Input.get_axis("move_left", "move_right")
	var move_y := Input.get_axis("move_up", "move_down")
	player.set_move_intent(move_x, move_y)

	var mouse_world := _screen_to_world(get_viewport().get_mouse_position())
	var aim_angle := (mouse_world - player.position).angle()
	player.set_aim_angle(aim_angle)

	if Input.is_action_just_pressed("weapon_1") and current_weapon_slot != 1:
		_switch_weapon(1)
	elif Input.is_action_just_pressed("weapon_2") and current_weapon_slot != 2:
		_switch_weapon(2)
	elif Input.is_action_just_pressed("weapon_3") and current_weapon_slot != 3:
		_switch_weapon(3)

	if Input.is_action_just_pressed("dash"):
		if player.request_dash():
			_add_shake(0.08)

	if Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT) and shot_cooldown <= 0.0:
		var weapon: GameData.WeaponDef = GameData.weapon_by_slot.get(current_weapon_slot, null)
		if weapon:
			_fire_weapon(weapon, mouse_world)
			shot_cooldown = weapon.cooldown

func _switch_weapon(slot: int) -> void:
	current_weapon_slot = slot
	var weapon: GameData.WeaponDef = GameData.weapon_by_slot.get(slot, null)
	if weapon:
		player.set_weapon_style(weapon.id)
		player.trigger_weapon_swap()
		_show_toast("武器槽 %d" % slot, "%s / %s" % [weapon.label, weapon.hint])

func _set_weapon_slot_from_type(weapon_type: int) -> void:
	match weapon_type:
		GameData.WeaponType.MACHINE_GUN: current_weapon_slot = 1
		GameData.WeaponType.GRENADE: current_weapon_slot = 2
		GameData.WeaponType.SNIPER: current_weapon_slot = 3

func _fire_weapon(weapon: GameData.WeaponDef, aim_point: Vector2) -> void:
	var origin := player.get_shot_origin()
	var dir := (aim_point - player.position).normalized()

	if weapon.id == GameData.WeaponType.GRENADE:
		player.trigger_shot(0.78)
		grenade_projectiles.append({
			"start": origin, "end": aim_point, "age": 0.0, "duration": 0.34,
		})
		_spawn_ring(origin, 8.0, 26.0, 0.16, Palette.ACCENT_SOFT, 3.0)
		_add_shake(0.07)
		return

	var weapon_range := 100000.0 if weapon.id == GameData.WeaponType.SNIPER else weapon.weapon_range
	var target := origin + dir * weapon_range
	var clipped := CollisionUtils.clip_segment_to_world(origin, target, layout.bounds, layout.obstacles, 2.0)

	var best_hit: Dictionary = {}
	var best_t := 2.0
	for enemy_data in enemies:
		var e_pos := Vector2(enemy_data["x"], enemy_data["y"])
		var e_radius: float = enemy_data["definition"].radius + weapon.effect_width * 0.65
		var t := CollisionUtils.segment_circle_intersection(origin, clipped, e_pos, e_radius)
		if t >= 0.0 and t < best_t:
			best_t = t
			best_hit = enemy_data

	var trail_end := clipped
	if weapon.id == GameData.WeaponType.MACHINE_GUN and not best_hit.is_empty():
		trail_end = origin + (clipped - origin) * best_t
		_damage_enemy(best_hit, MACHINE_GUN_DAMAGE, trail_end)

	if weapon.id == GameData.WeaponType.SNIPER:
		for enemy_data in enemies.duplicate():
			var e_pos := Vector2(enemy_data["x"], enemy_data["y"])
			var e_radius: float = enemy_data["definition"].radius + weapon.effect_width * 0.65
			var t := CollisionUtils.segment_circle_intersection(origin, clipped, e_pos, e_radius)
			if t >= 0.0:
				var hit_point := origin + (clipped - origin) * t
				_damage_enemy(enemy_data, SNIPER_DAMAGE, hit_point)

	var dist := trail_end.distance_to(origin)
	var speed := SNIPER_SPEED if weapon.id == GameData.WeaponType.SNIPER else MACHINE_GUN_SPEED
	var is_sniper := weapon.id == GameData.WeaponType.SNIPER

	player.trigger_shot(1.45 if is_sniper else 0.9)
	needle_projectiles.append({
		"start": origin, "end": trail_end, "dir": dir, "age": 0.0,
		"duration": clampf(dist / speed, 0.08, 0.28 if is_sniper else 0.18),
		"length": 26.0 if is_sniper else 14.0,
		"width": 5.0 if is_sniper else 3.0,
		"color": Palette.PLAYER_EDGE if is_sniper else Palette.ACCENT,
	})
	_add_shake(0.14 if is_sniper else 0.04)

func _advance_spawn_queue(delta: float) -> void:
	if pending_spawns.size() > 0:
		spawn_timer -= delta
		while spawn_timer <= 0.0 and pending_spawns.size() > 0:
			var next: GameData.SpawnOrder = pending_spawns.pop_front()
			_spawn_enemy(next.type)
			spawn_timer += next.delay
		return

	if enemies.size() > 0:
		return

	next_wave_delay -= delta
	if next_wave_delay <= 0.0:
		wave_index += 1
		next_wave_delay = NEXT_WAVE_DELAY
		spawn_timer = 0.2
		pending_spawns = GameData.build_wave_orders(wave_index)
		zone_highest_wave = maxi(zone_highest_wave, wave_index)
		run_highest_wave = maxi(run_highest_wave, wave_index)
		_show_toast("波次 %02d" % wave_index, GameData.build_wave_hint(wave_index))
		_flush_run_snapshot()

func _spawn_enemy(type: int) -> void:
	var def: GameData.HostileDef = GameData.hostile_by_type.get(type, null)
	if def == null:
		return

	var spawn_pos := _pick_spawn_point(type)
	var avatar := EnemyAvatar.new()
	avatar.setup(type, enemy_id_counter * 0.37 + randf())
	avatar.position = spawn_pos
	enemy_layer.add_child(avatar)

	var enemy_data := {
		"id": enemy_id_counter,
		"type": type,
		"definition": def,
		"avatar": avatar,
		"x": spawn_pos.x,
		"y": spawn_pos.y,
		"health": def.max_health,
		"contact_cooldown": 0.1 + randf() * 0.2,
		"attack_cooldown": 0.35 + randf() * def.attack_cooldown,
		"mode": GameData.HostileMode.ADVANCE,
		"mode_timer": 0.0,
		"charge_dir": Vector2(0, 1),
		"facing_angle": -PI / 2.0,
		"phase": 1,
		"pattern": "nova",
		"phase_shifted": false,
	}
	enemy_id_counter += 1
	enemies.append(enemy_data)

	if type == GameData.HostileType.BOSS:
		_spawn_ring(spawn_pos, 18.0, 92.0, 0.28, Palette.WARNING, 4.0)
		_show_toast("区域主核出现", "%s正在接管战区" % def.label)
		_add_shake(0.18)

func _pick_spawn_point(type: int) -> Vector2:
	if type == GameData.HostileType.BOSS and layout:
		return layout.boss_spawn
	if layout and layout.enemy_spawns.size() > 0:
		return layout.enemy_spawns[enemy_id_counter % layout.enemy_spawns.size()]
	if layout:
		var side := enemy_id_counter % 4
		var b := layout.bounds
		match side:
			0: return Vector2(b.position.x + 24, lerpf(b.position.y, b.end.y, randf()))
			1: return Vector2(b.end.x - 24, lerpf(b.position.y, b.end.y, randf()))
			2: return Vector2(lerpf(b.position.x, b.end.x, randf()), b.position.y + 24)
			_: return Vector2(lerpf(b.position.x, b.end.x, randf()), b.end.y - 24)
	return Vector2.ZERO

func _update_enemies(delta: float) -> void:
	var player_pos := player.position
	var player_radius := player.get_collision_radius()

	for enemy_data in enemies:
		enemy_data["contact_cooldown"] = maxf(0.0, enemy_data["contact_cooldown"] - delta)
		enemy_data["attack_cooldown"] = maxf(0.0, enemy_data["attack_cooldown"] - delta)

		var ex: float = enemy_data["x"]
		var ey: float = enemy_data["y"]
		var dx := player_pos.x - ex
		var dy := player_pos.y - ey
		var dist := sqrt(dx * dx + dy * dy)
		var dir_x := dx / dist if dist > 0.0001 else 0.0
		var dir_y := dy / dist if dist > 0.0001 else -1.0
		enemy_data["facing_angle"] = atan2(dy, dx)

		var def: GameData.HostileDef = enemy_data["definition"]
		var vel := Vector2.ZERO

		match enemy_data["type"]:
			GameData.HostileType.MELEE:
				vel = Vector2(dir_x, dir_y) * def.move_speed
			GameData.HostileType.RANGED:
				vel = _update_ranged_enemy(enemy_data, delta, dist, dir_x, dir_y)
			GameData.HostileType.CHARGER:
				vel = _update_charger_enemy(enemy_data, delta, dist, dir_x, dir_y)
			GameData.HostileType.BOSS:
				vel = _update_boss_enemy(enemy_data, delta, dist, dir_x, dir_y)

		var avatar: EnemyAvatar = enemy_data["avatar"]
		avatar.set_aim_angle_val(enemy_data["facing_angle"])

		var next_pos := CollisionUtils.resolve_circle_world_movement(
			Vector2(ex, ey), ex + vel.x * delta, ey + vel.y * delta,
			def.radius, layout.bounds, layout.obstacles)
		enemy_data["x"] = next_pos.x
		enemy_data["y"] = next_pos.y

		var cdx := player_pos.x - enemy_data["x"]
		var cdy := player_pos.y - enemy_data["y"]
		var cdist := sqrt(cdx * cdx + cdy * cdy)
		var min_dist := def.radius + player_radius + 2.0

		if cdist > 0.0001 and cdist < min_dist:
			var scale_val := min_dist / cdist
			enemy_data["x"] = player_pos.x - cdx * scale_val
			enemy_data["y"] = player_pos.y - cdy * scale_val

		if enemy_data["contact_cooldown"] <= 0.0 and cdist <= def.radius + player_radius + 6.0:
			_apply_player_damage(int(def.contact_damage), Vector2(enemy_data["x"], enemy_data["y"]))
			enemy_data["contact_cooldown"] = def.contact_interval
			avatar.trigger_attack_pulse(0.78)

		avatar.position = Vector2(enemy_data["x"], enemy_data["y"])
		avatar.set_motion_val(minf(1.0, vel.length() / maxf(1.0, def.move_speed)))
		avatar.set_mode_val(enemy_data["mode"])
		avatar.set_life_ratio_val(enemy_data["health"] / def.max_health)
		avatar.update_avatar(delta, elapsed_seconds)

func _update_ranged_enemy(ed: Dictionary, delta: float, dist: float, dir_x: float, dir_y: float) -> Vector2:
	var def: GameData.HostileDef = ed["definition"]
	if ed["mode"] == GameData.HostileMode.AIM:
		ed["mode_timer"] = maxf(0.0, ed["mode_timer"] - delta)
		if ed["mode_timer"] <= 0.0:
			_spawn_enemy_projectile(ed, dir_x, dir_y)
			ed["mode"] = GameData.HostileMode.ADVANCE
			ed["attack_cooldown"] = def.attack_cooldown
			ed["avatar"].trigger_attack_pulse(1.0)
		return Vector2.ZERO

	var pref_dist := def.preferred_distance if def.preferred_distance > 0 else 250.0
	var vel := Vector2.ZERO
	if dist > pref_dist + 48:
		vel = Vector2(dir_x, dir_y) * def.move_speed
	elif dist < pref_dist - 44:
		vel = Vector2(-dir_x, -dir_y) * def.move_speed * 0.85
	else:
		var sign_val := 1.0 if ed["id"] % 2 == 0 else -1.0
		vel = Vector2(-dir_y * sign_val, dir_x * sign_val) * def.move_speed * 0.58

	var atk_range := def.attack_range if def.attack_range > 0 else 420.0
	if ed["attack_cooldown"] <= 0.0 and dist <= atk_range:
		ed["mode"] = GameData.HostileMode.AIM
		ed["mode_timer"] = def.attack_windup if def.attack_windup > 0 else 0.42
		ed["avatar"].trigger_attack_pulse(0.55)
	return vel

func _update_charger_enemy(ed: Dictionary, delta: float, dist: float, dir_x: float, dir_y: float) -> Vector2:
	var def: GameData.HostileDef = ed["definition"]
	if ed["mode"] == GameData.HostileMode.WINDUP:
		ed["facing_angle"] = atan2(ed["charge_dir"].y, ed["charge_dir"].x)
		ed["mode_timer"] = maxf(0.0, ed["mode_timer"] - delta)
		if ed["mode_timer"] <= 0.0:
			ed["mode"] = GameData.HostileMode.CHARGE
			ed["mode_timer"] = def.charge_duration if def.charge_duration > 0 else 0.28
			ed["attack_cooldown"] = def.attack_cooldown
			ed["avatar"].trigger_attack_pulse(1.0)
		return Vector2.ZERO

	if ed["mode"] == GameData.HostileMode.CHARGE:
		ed["facing_angle"] = atan2(ed["charge_dir"].y, ed["charge_dir"].x)
		var spd := def.charge_speed if def.charge_speed > 0 else 560.0
		var vel := ed["charge_dir"] * spd
		ed["mode_timer"] = maxf(0.0, ed["mode_timer"] - delta)
		if ed["mode_timer"] <= 0.0:
			ed["mode"] = GameData.HostileMode.RECOVER
			ed["mode_timer"] = def.recover_duration if def.recover_duration > 0 else 0.5
		return vel

	if ed["mode"] == GameData.HostileMode.RECOVER:
		ed["mode_timer"] = maxf(0.0, ed["mode_timer"] - delta)
		if ed["mode_timer"] <= 0.0:
			ed["mode"] = GameData.HostileMode.ADVANCE
		return Vector2.ZERO

	var vel := Vector2(dir_x, dir_y) * def.move_speed
	var trigger_dist := def.charge_trigger_distance if def.charge_trigger_distance > 0 else 240.0
	if ed["attack_cooldown"] <= 0.0 and dist <= trigger_dist:
		ed["mode"] = GameData.HostileMode.WINDUP
		ed["mode_timer"] = def.attack_windup if def.attack_windup > 0 else 0.56
		ed["charge_dir"] = Vector2(dir_x, dir_y)
		ed["avatar"].trigger_attack_pulse(0.75)
	return vel

func _update_boss_enemy(ed: Dictionary, delta: float, dist: float, dir_x: float, dir_y: float) -> Vector2:
	var def: GameData.HostileDef = ed["definition"]
	var pref_dist := (def.preferred_distance if def.preferred_distance > 0 else 250.0) - (22.0 if ed["phase"] == 2 else 0.0)
	var orbit_sign := 1.0 if ed["id"] % 2 == 0 else -1.0
	ed["facing_angle"] = atan2(dir_y, dir_x)

	if not ed["phase_shifted"] and ed["health"] <= def.max_health * 0.5:
		ed["phase_shifted"] = true
		ed["phase"] = 2
		ed["mode"] = GameData.HostileMode.RECOVER
		ed["mode_timer"] = 1.05
		ed["attack_cooldown"] = 0.45
		_spawn_ring(Vector2(ed["x"], ed["y"]), 24.0, 118.0, 0.34, Palette.DANGER, 4.0)
		_show_toast("第二阶段", "%s火力已升级" % def.label)
		_add_shake(0.32)
		return Vector2.ZERO

	if ed["mode"] == GameData.HostileMode.AIM:
		ed["mode_timer"] = maxf(0.0, ed["mode_timer"] - delta)
		if ed["mode_timer"] <= 0.0:
			_fire_boss_attack(ed, atan2(dir_y, dir_x))
			ed["mode"] = GameData.HostileMode.RECOVER
			ed["mode_timer"] = 0.3 if ed["phase"] == 1 else 0.2
		return Vector2(-dir_y * orbit_sign, dir_x * orbit_sign) * def.move_speed * 0.2

	if ed["mode"] == GameData.HostileMode.RECOVER:
		ed["mode_timer"] = maxf(0.0, ed["mode_timer"] - delta)
		if ed["mode_timer"] <= 0.0:
			ed["mode"] = GameData.HostileMode.ADVANCE
		return Vector2.ZERO

	var vel := Vector2.ZERO
	if dist > pref_dist + 42:
		vel = Vector2(dir_x, dir_y) * def.move_speed
	elif dist < pref_dist - 32:
		vel = Vector2(-dir_x, -dir_y) * def.move_speed * 0.85
	else:
		vel = Vector2(-dir_y * orbit_sign, dir_x * orbit_sign) * def.move_speed * 0.62

	if ed["attack_cooldown"] <= 0.0:
		ed["pattern"] = "fan" if ed["pattern"] == "nova" else "nova"
		ed["mode"] = GameData.HostileMode.AIM
		ed["mode_timer"] = 0.66 if ed["phase"] == 1 else 0.48
		ed["avatar"].trigger_attack_pulse(1.0)
	return vel

func _fire_boss_attack(ed: Dictionary, target_angle: float) -> void:
	var def: GameData.HostileDef = ed["definition"]
	var speed := def.projectile_speed if def.projectile_speed > 0 else 290.0
	var count := 18 if ed["phase"] == 1 else 28

	if ed["pattern"] == "fan":
		var spread := 0.95 if ed["phase"] == 1 else 1.28
		var fan_count := 7 if ed["phase"] == 1 else 11
		for i in fan_count:
			var t := float(i) / float(fan_count - 1) if fan_count > 1 else 0.5
			var angle := target_angle - spread * 0.5 + spread * t
			_spawn_hostile_projectile(ed, angle, speed, Palette.ENEMY_BOSS, Palette.ENEMY_BOSS_GLOW)
		ed["attack_cooldown"] = 1.35 if ed["phase"] == 1 else 0.92
	else:
		for i in count:
			var angle := float(i) * (TAU / float(count))
			_spawn_hostile_projectile(ed, angle, speed, Palette.DANGER, Palette.ENEMY_BOSS_GLOW)
		ed["attack_cooldown"] = 1.65 if ed["phase"] == 1 else 1.18

	_spawn_ring(Vector2(ed["x"], ed["y"]), 20.0, 112.0, 0.24, Palette.ACCENT_SOFT, 3.0)
	_add_shake(0.16)

func _spawn_enemy_projectile(ed: Dictionary, dir_x: float, dir_y: float) -> void:
	var def: GameData.HostileDef = ed["definition"]
	var angle := atan2(dir_y, dir_x)
	var speed := def.projectile_speed if def.projectile_speed > 0 else 320.0
	_spawn_hostile_projectile(ed, angle, speed, Palette.ENEMY_PROJECTILE, Palette.ACCENT_SOFT)

func _spawn_hostile_projectile(ed: Dictionary, angle: float, speed: float, color: Color, glow_color: Color) -> void:
	var def: GameData.HostileDef = ed["definition"]
	var radius := def.projectile_radius if def.projectile_radius > 0 else 7.0
	var dir_x := cos(angle)
	var dir_y := sin(angle)
	enemy_projectiles.append({
		"x": ed["x"] + dir_x * (def.radius + 10),
		"y": ed["y"] + dir_y * (def.radius + 10),
		"vx": dir_x * speed,
		"vy": dir_y * speed,
		"radius": radius,
		"damage": def.projectile_damage if def.projectile_damage > 0 else 12,
		"age": 0.0,
		"duration": 2.2,
		"color": color,
		"glow_color": glow_color,
	})

func _update_enemy_projectiles(delta: float) -> void:
	var player_pos := player.position
	var player_radius := player.get_collision_radius()
	var to_remove: Array[int] = []

	for i in range(enemy_projectiles.size() - 1, -1, -1):
		var proj: Dictionary = enemy_projectiles[i]
		proj["age"] += delta
		proj["x"] += proj["vx"] * delta
		proj["y"] += proj["vy"] * delta

		var p_pos := Vector2(proj["x"], proj["y"])
		var hit_player := not player.is_dashing() and p_pos.distance_to(player_pos) <= proj["radius"] + player_radius
		var outside := layout and (proj["x"] < layout.bounds.position.x - 20 or proj["x"] > layout.bounds.end.x + 20 \
			or proj["y"] < layout.bounds.position.y - 20 or proj["y"] > layout.bounds.end.y + 20)
		var expired := proj["age"] >= proj["duration"]

		if hit_player:
			_apply_player_damage(int(proj["damage"]), p_pos)
			enemy_projectiles.remove_at(i)
		elif outside or expired:
			enemy_projectiles.remove_at(i)

func _damage_enemy(enemy_data: Dictionary, amount: int, impact_pos: Vector2) -> void:
	var idx := enemies.find(enemy_data)
	if idx == -1:
		return
	enemy_data["health"] = maxf(0.0, enemy_data["health"] - amount)
	enemy_data["avatar"].trigger_damage_flash(1.0 if amount >= SNIPER_DAMAGE else 0.74)
	_spawn_ring(impact_pos, 6.0, 24.0, 0.12, Palette.ACCENT_SOFT, 2.0)
	_add_shake(0.08 if enemy_data["type"] == GameData.HostileType.BOSS else 0.03)

	if enemy_data["health"] <= 0.0:
		run_kills += 1
		_spawn_ring(Vector2(enemy_data["x"], enemy_data["y"]), 10.0, 50.0, 0.22, enemy_data["definition"].color_glow, 3.0)
		_add_shake(0.27 if enemy_data["type"] == GameData.HostileType.BOSS else 0.1)
		enemy_data["avatar"].queue_free()
		enemies.erase(enemy_data)

		if enemy_data["type"] == GameData.HostileType.BOSS:
			encounter_state = "clear"
			pending_spawns.clear()
			enemy_projectiles.clear()
			
			_drop_loot("aegis-core", enemy_data["x"], enemy_data["y"], 1)
			
			var Routes := preload("res://world/routes.gd")
			var map: Dictionary = GameStore.save.get("session", {}).get("active_run", {}).get("map", {})
			var next_zone := Routes.get_next_run_zone(map)
			if next_zone.is_empty():
				_show_toast("路线已完成", "前往撤离出口完成本次副本")
			else:
				_show_toast("区域已压制", "前往出口后可推进下一区域")
			_add_shake(0.4)
			GameStore.mark_current_zone_cleared(_build_run_snapshot())
		else:
			if randf() < 0.35:
				var drop_id := "salvage-scrap"
				if enemy_data["type"] == GameData.HostileType.CHARGER and randf() < 0.5:
					drop_id = "alloy-plate"
				elif enemy_data["type"] == GameData.HostileType.RANGED and randf() < 0.2:
					drop_id = "telemetry-cache"
				_drop_loot(drop_id, enemy_data["x"], enemy_data["y"], 1)

		_flush_run_snapshot()

func _drop_loot(item_id: String, x: float, y: float, qty: int) -> void:
	ground_loot.append({
		"id": "loot-%s-%s-%s" % [item_id, str(Time.get_ticks_msec()), str(randi() % 1000)],
		"item_id": item_id,
		"x": x,
		"y": y,
		"quantity": qty
	})
	_rebuild_loot_visuals()

func _apply_player_damage(amount: int, source_pos: Vector2) -> void:
	if player.is_dashing() or encounter_state != "active":
		return
	player_health = maxi(0, player_health - amount)
	player.set_life_ratio(float(player_health) / PLAYER_MAX_HEALTH)
	player.flash_damage(1.0 if amount >= 16 else 0.72)
	_spawn_ring(source_pos, 10.0, 34.0, 0.16, Palette.DANGER, 3.0)
	_add_shake(0.2 if amount >= 16 else 0.12)
	_flush_run_snapshot()

	if player_health <= 0:
		encounter_state = "down"
		pending_spawns.clear()
		enemy_projectiles.clear()
		_show_toast("行动中止", "本局待结算，请返回基地")
		GameStore.mark_run_outcome("down", _build_run_snapshot())

func _handle_loot_interaction(active_run: Dictionary) -> void:
	var player_pos := player.position
	var pickup_radius := 60.0
	
	var to_pickup: Array = []
	for i in range(ground_loot.size() - 1, -1, -1):
		var loot := ground_loot[i]
		var l_pos := Vector2(loot["x"], loot["y"])
		if l_pos.distance_to(player_pos) <= pickup_radius:
			to_pickup.append(loot)
			ground_loot.remove_at(i)
			
	if to_pickup.size() == 0:
		return
		
	var inv_dict: Dictionary = active_run.get("inventory", {})
	var cols: int = inv_dict.get("columns", 6)
	var rows: int = inv_dict.get("rows", 4)
	var inv_items: Array = inv_dict.get("items", [])
	
	var inventory_records: Array = []
	for d in inv_items:
		inventory_records.append(InventoryGrid.ItemRecord.from_dict(d))
		
	var incoming_records: Array = []
	for loot in to_pickup:
		var rec := InventoryGrid.create_item_record(loot["item_id"], loot["quantity"])
		if rec:
			incoming_records.append(rec)
			
	var result := InventoryGrid.place_items_in_grid(cols, rows, inventory_records, incoming_records)
	
	var new_inv_items: Array = []
	for r in result["items"]:
		new_inv_items.append(r.to_dict())
		
	inv_dict["items"] = new_inv_items
	active_run["inventory"] = inv_dict
	
	# Any rejected items go back to ground
	for rec in result["rejected"]:
		ground_loot.append({
			"id": "loot-%s-%s" % [rec.item_id, str(Time.get_ticks_msec())],
			"item_id": rec.item_id,
			"x": player.position.x + (randf() - 0.5) * 40,
			"y": player.position.y + (randf() - 0.5) * 40,
			"quantity": rec.quantity
		})
		
	if result["placed_ids"].size() > 0:
		_show_toast("拾取成功", "获得了 %d 件物品" % result["placed_ids"].size())
		_add_shake(0.05)
	if result["rejected"].size() > 0:
		_show_toast("背包已满", "有 %d 件物品无法拾取" % result["rejected"].size())
		
	_rebuild_loot_visuals()
	_flush_run_snapshot()

func _handle_world_interaction(active_run: Dictionary) -> void:
	if pending_exit_action.size() > 0:
		return
	var marker := _find_nearby_marker()
	if marker.is_empty():
		return

	if marker.get("id") == "exit":
		var Routes := preload("res://world/routes.gd")
		var map: Dictionary = active_run.get("map", {})
		var next_zone := Routes.get_next_run_zone(map)
		var can_advance := Routes.is_current_zone_cleared(map) and not next_zone.is_empty()
		var can_extract := Routes.can_extract_from_map(map)

		if can_advance:
			pending_exit_action = {
				"kind": "advance", "phase": "charging", "elapsed": 0.0,
				"duration": EXIT_ADVANCE_CHANNEL, "marker_id": "exit",
				"next_zone_label": next_zone.get("label", "下一区域"),
			}
			_show_toast("区域通道接管中", "保持在出口范围内")
		elif can_extract:
			pending_exit_action = {
				"kind": "extract", "phase": "charging", "elapsed": 0.0,
				"duration": EXIT_EXTRACT_CHANNEL, "marker_id": "exit",
			}
			_show_toast("撤离通道校验中", "保持在出口范围内")
		else:
			_show_toast(marker.get("label", "出口"), "出口尚未联通，先完成当前区域压制")

func _tick_exit_action(delta: float) -> void:
	if pending_exit_action.is_empty():
		return
	var marker := _find_nearby_marker()
	if marker.is_empty() or marker.get("id") != pending_exit_action.get("marker_id"):
		pending_exit_action = {}
		_show_toast("出口接管已取消", "离开出口范围会中断读条")
		return

	pending_exit_action["elapsed"] += delta
	if pending_exit_action["elapsed"] < pending_exit_action.get("duration", 1.0):
		return

	if pending_exit_action.get("phase") == "charging":
		if pending_exit_action.get("kind") == "extract":
			pending_exit_action = {}
			_show_toast("撤离完成", "请确认结算结果")
			GameStore.mark_run_outcome("extracted", _build_run_snapshot())
			return
		pending_exit_action["phase"] = "opening"
		pending_exit_action["elapsed"] = 0.0
		pending_exit_action["duration"] = EXIT_GATE_OPEN
		return

	var completed := pending_exit_action.duplicate()
	pending_exit_action = {}
	GameStore.sync_active_run(_build_run_snapshot())
	if completed.get("kind") == "advance":
		_show_toast("出口已接管", "正在推进到 %s" % completed.get("next_zone_label", "下一区域"))
		GameStore.advance_active_run_zone()

func _find_nearby_marker(radius: float = INTERACTION_RADIUS) -> Dictionary:
	if layout == null:
		return {}
	var player_pos := player.position
	var nearest: Dictionary = {}
	var nearest_dist := radius
	for marker in layout.markers:
		var m_pos := Vector2(marker["x"], marker["y"])
		var dist := m_pos.distance_to(player_pos)
		if dist < nearest_dist:
			nearest = marker
			nearest_dist = dist
	return nearest

func _flush_run_snapshot() -> void:
	GameStore.sync_active_run(_build_run_snapshot())

func _build_run_snapshot() -> Dictionary:
	var weapon_def: GameData.WeaponDef = GameData.weapon_by_slot.get(current_weapon_slot, null)
	var current_weapon_id: int = weapon_def.id if weapon_def else GameData.WeaponType.MACHINE_GUN
	return {
		"player": {
			"health": player_health,
			"max_health": PLAYER_MAX_HEALTH,
			"current_weapon_id": current_weapon_id,
			"loadout_weapon_ids": loadout_weapon_ids,
		},
		"stats": {
			"elapsed_seconds": run_elapsed,
			"kills": run_kills,
			"highest_wave": run_highest_wave,
			"extracted": false,
			"boss_defeated": encounter_state == "clear",
		},
		"map": {
			"current_wave": wave_index,
			"highest_wave": zone_highest_wave,
			"hostiles_remaining": enemies.size() + pending_spawns.size() if encounter_state != "clear" else 0,
		},
		"ground_loot": ground_loot,
	}

func _update_transient_effects(delta: float) -> void:
	for proj in needle_projectiles:
		proj["age"] += delta
	for proj in grenade_projectiles:
		proj["age"] += delta
	for ring in burst_rings:
		ring["age"] += delta

	for i in range(grenade_projectiles.size() - 1, -1, -1):
		var proj: Dictionary = grenade_projectiles[i]
		if proj["age"] >= proj["duration"]:
			var end_pos: Vector2 = proj["end"]
			grenade_projectiles.remove_at(i)
			var splash := GameData.weapon_by_slot.get(2, null)
			var radius := splash.splash_radius if splash else 66.0
			for enemy_data in enemies.duplicate():
				var e_pos := Vector2(enemy_data["x"], enemy_data["y"])
				var dist := e_pos.distance_to(end_pos)
				if dist <= radius + enemy_data["definition"].radius:
					var falloff := 1.0 - dist / (radius + enemy_data["definition"].radius)
					_damage_enemy(enemy_data, int(GRENADE_DAMAGE * (0.55 + falloff * 0.45)), e_pos)
			_spawn_ring(end_pos, 14.0, radius, 0.24, Palette.ACCENT, 4.0)
			_add_shake(0.18)

	needle_projectiles = needle_projectiles.filter(func(p): return p["age"] < p["duration"])
	burst_rings = burst_rings.filter(func(r): return r["age"] < r["duration"])

func _spawn_ring(pos: Vector2, start_r: float, end_r: float, dur: float, color: Color, width: float) -> void:
	burst_rings.append({ "pos": pos, "age": 0.0, "duration": dur,
		"start_radius": start_r, "end_radius": end_r, "color": color, "width": width })

func _add_shake(intensity: float) -> void:
	shake_trauma = minf(1.0, shake_trauma + intensity)

func _apply_camera() -> void:
	if layout == null:
		return
	camera.zoom = Vector2(WORLD_CAMERA_ZOOM, WORLD_CAMERA_ZOOM)
	var target_pos := player.position
	var shake_power := shake_trauma * shake_trauma * 14.0
	var sx := sin(elapsed_seconds * 48.0) * shake_power * 0.72 if shake_trauma > 0.0001 else 0.0
	var sy := cos(elapsed_seconds * 44.0) * shake_power if shake_trauma > 0.0001 else 0.0
	camera.position = target_pos + Vector2(sx, sy)

func _screen_to_world(screen_pos: Vector2) -> Vector2:
	return camera.get_global_transform().affine_inverse() * \
		get_viewport().get_canvas_transform().affine_inverse() * screen_pos

func _draw_effects() -> void:
	for child in effects_layer.get_children():
		child.queue_free()
	var fx := _EffectsDraw.new()
	fx.burst_rings = burst_rings
	fx.needle_projectiles = needle_projectiles
	fx.grenade_projectiles = grenade_projectiles
	fx.enemy_projectiles = enemy_projectiles
	effects_layer.add_child(fx)

func _update_hud() -> void:
	var weapon: GameData.WeaponDef = GameData.weapon_by_slot.get(current_weapon_slot, null)
	_hud.update_stats(player_health, PLAYER_MAX_HEALTH, wave_index, run_kills, weapon.label if weapon else "无", encounter_state, enemies.size())
	_hud.update_hint(_build_hint_text())
	
	var active_run = GameStore.save.get("session", {}).get("active_run")
	if active_run:
		_hud.update_quick_slots(active_run.get("inventory", {}).get("quick_slots", ["", "", "", ""]))

func _build_hint_text() -> String:
	var marker := _find_nearby_marker()
	if not pending_exit_action.is_empty():
		var progress := pending_exit_action.get("elapsed", 0.0) / maxf(0.01, pending_exit_action.get("duration", 1.0))
		return "%s %d%%" % ["撤离中" if pending_exit_action.get("kind") == "extract" else "推进中", int(progress * 100)]
	
	var near_loot := false
	for loot in ground_loot:
		if Vector2(loot["x"], loot["y"]).distance_to(player.position) <= 60.0:
			near_loot = true
			break
			
	if near_loot:
		return "附近有物资：按 E 拾取"
		
	if marker.is_empty():
		return "WASD 移动 / 鼠标射击 / Space 冲刺 / E 交互"
	return "接近 %s：按 E 交互" % marker.get("label", "")

func _show_toast(title: String, detail: String, duration: float = 1.2) -> void:
	_toast.show_toast(title, detail, duration)

func _rebuild_loot_visuals() -> void:
	for child in loot_layer.get_children():
		child.queue_free()
	var loot_draw := _LootDraw.new()
	loot_draw.ground_loot = ground_loot
	loot_layer.add_child(loot_draw)

class _LootDraw extends Node2D:
	var ground_loot: Array = []
	func _draw() -> void:
		for loot in ground_loot:
			var pos := Vector2(loot["x"], loot["y"])
			var def := GameData.get_item_def(loot["item_id"])
			if def == null: continue
			var color := def.tint
			var accent := def.accent
			
			draw_circle(pos, 8.0, color)
			draw_circle(pos, 4.0, accent)
			var outer_color := color
			outer_color.a = 0.4
			draw_arc(pos, 12.0, 0, TAU, 16, outer_color, 1.5)

class _TerrainDraw extends Node2D:
	var layout: WorldLayout
	var grid_size: float = 56.0

	func _draw() -> void:
		if layout == null:
			return
		draw_rect(layout.bounds, Palette.WORLD_FLOOR, true)
		var grid_color := Palette.GRID
		grid_color.a = 0.5
		var x := layout.bounds.position.x
		while x <= layout.bounds.end.x:
			draw_line(Vector2(x, layout.bounds.position.y), Vector2(x, layout.bounds.end.y), grid_color, 1.0)
			x += grid_size
		var y := layout.bounds.position.y
		while y <= layout.bounds.end.y:
			draw_line(Vector2(layout.bounds.position.x, y), Vector2(layout.bounds.end.x, y), grid_color, 1.0)
			y += grid_size
		draw_rect(layout.bounds, Palette.FRAME, false, 2.0)

class _ObstacleDraw extends Node2D:
	var obstacles: Array = []
	func _draw() -> void:
		for obs in obstacles:
			if not obs is Dictionary:
				continue
			var rect := Rect2(obs.get("x", 0), obs.get("y", 0), obs.get("width", 0), obs.get("height", 0))
			var kind: String = obs.get("kind", "wall")
			var fill_color: Color
			match kind:
				"wall": fill_color = Palette.OBSTACLE_WALL
				"cover": fill_color = Palette.OBSTACLE_COVER
				_: fill_color = Palette.OBSTACLE_FILL
			draw_rect(rect, fill_color, true)
			var edge_color := Palette.OBSTACLE_EDGE
			edge_color.a = 0.6
			draw_rect(rect, edge_color, false, 1.5)

class _MarkerDraw extends Node2D:
	var markers: Array = []
	func _draw() -> void:
		for marker in markers:
			if not marker is Dictionary:
				continue
			var pos := Vector2(marker.get("x", 0), marker.get("y", 0))
			var kind: String = marker.get("kind", "entry")
			var color: Color
			match kind:
				"entry": color = Palette.ACCENT
				"extraction": color = Palette.MINIMAP_MARKER
				"objective": color = Palette.DANGER
				_: color = Palette.FRAME
			draw_circle(pos, 8.0, color)
			var outer_color := color
			outer_color.a = 0.3
			draw_arc(pos, 16.0, 0, TAU, 32, outer_color, 1.5)

class _EffectsDraw extends Node2D:
	var burst_rings: Array = []
	var needle_projectiles: Array = []
	var grenade_projectiles: Array = []
	var enemy_projectiles: Array = []

	func _draw() -> void:
		for ring in burst_rings:
			var progress: float = ring["age"] / ring["duration"]
			var radius: float = lerpf(ring["start_radius"], ring["end_radius"], progress)
			var color: Color = ring["color"]
			color.a = 1.0 - progress
			draw_arc(ring["pos"], radius, 0, TAU, 32, color, maxf(1.0, ring["width"] - progress * 2))

		for proj in needle_projectiles:
			var progress: float = proj["age"] / proj["duration"]
			var head: Vector2 = proj["start"].lerp(proj["end"], progress)
			var tail: Vector2 = head - proj["dir"] * proj["length"]
			var color: Color = proj["color"]
			color.a = 1.0 - progress
			draw_line(tail, head, color, proj["width"])

		for proj in grenade_projectiles:
			var progress: float = proj["age"] / proj["duration"]
			var travel: Vector2 = proj["start"].lerp(proj["end"], progress)
			travel.y -= sin(progress * PI) * 28.0
			draw_circle(travel, 7.0, Palette.ACCENT)
			draw_circle(travel, 3.0, Palette.ARENA_CORE)

		for proj in enemy_projectiles:
			var pos := Vector2(proj["x"], proj["y"])
			var glow: Color = proj["glow_color"]
			glow.a = 0.16
			draw_circle(pos, proj["radius"] * 1.5, glow)
			var color: Color = proj["color"]
			color.a = 0.94
			draw_circle(pos, proj["radius"], color)
