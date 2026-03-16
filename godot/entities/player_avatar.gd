class_name PlayerAvatar
extends Node2D

const MAX_SPEED := 260.0
const ACCELERATION := 7.25
const BRAKE := 9.5
const DASH_SPEED := 760.0
const DASH_DURATION := 0.14
const DASH_COOLDOWN := 0.65
const PLAYER_RADIUS := 16.0

var velocity := Vector2.ZERO
var move_intent := Vector2.ZERO
var dash_direction := Vector2(0, -1)
var aim_angle := -PI / 2.0
var dash_time := 0.0
var dash_cooldown := 0.0
var shot_kick := 0.0
var dash_pulse := 0.0
var damage_pulse := 0.0
var life_ratio := 1.0
var arrow_distance_base := 38.0
var current_weapon_type: int = GameData.WeaponType.MACHINE_GUN

func get_collision_radius() -> float:
	return PLAYER_RADIUS

func is_dashing() -> bool:
	return dash_time > 0.0

func set_move_intent(x: float, y: float) -> void:
	var mag := sqrt(x * x + y * y)
	if mag <= 0.0001:
		move_intent = Vector2.ZERO
		return
	move_intent = Vector2(x / mag, y / mag)

func set_aim_angle(angle: float) -> void:
	aim_angle = angle

func set_life_ratio(ratio: float) -> void:
	life_ratio = clampf(ratio, 0.0, 1.0)

func flash_damage(intensity: float = 1.0) -> void:
	damage_pulse = maxf(damage_pulse, intensity)

func set_weapon_style(weapon_type: int) -> void:
	current_weapon_type = weapon_type
	match weapon_type:
		GameData.WeaponType.MACHINE_GUN: arrow_distance_base = 38.0
		GameData.WeaponType.GRENADE: arrow_distance_base = 42.0
		GameData.WeaponType.SNIPER: arrow_distance_base = 48.0
	queue_redraw()

func request_dash() -> bool:
	if dash_cooldown > 0.0 or dash_time > 0.0:
		return false
	var mag := move_intent.length()
	if mag > 0.0001:
		dash_direction = move_intent.normalized()
	else:
		dash_direction = Vector2(cos(aim_angle), sin(aim_angle))
	dash_time = DASH_DURATION
	dash_cooldown = DASH_COOLDOWN
	dash_pulse = 1.0
	return true

func refresh_dash_charge() -> void:
	dash_cooldown = 0.0
	dash_pulse = maxf(dash_pulse, 0.75)

func trigger_shot(intensity: float = 1.0) -> void:
	shot_kick = maxf(shot_kick, intensity)

func trigger_weapon_swap() -> void:
	dash_pulse = maxf(dash_pulse, 0.36)

func get_shot_origin() -> Vector2:
	var reach := arrow_distance_base + shot_kick * 6.0 + dash_pulse * 5.0
	return position + Vector2(cos(aim_angle), sin(aim_angle)) * reach

func reset_state() -> void:
	velocity = Vector2.ZERO
	move_intent = Vector2.ZERO
	dash_direction = Vector2(0, -1)
	aim_angle = -PI / 2.0
	dash_time = 0.0
	dash_cooldown = 0.0
	shot_kick = 0.0
	dash_pulse = 0.0
	damage_pulse = 0.0
	life_ratio = 1.0
	modulate.a = 1.0
	rotation = 0.0
	scale = Vector2.ONE

func update_avatar(delta: float, bounds: Rect2, elapsed: float, obstacles: Array = []) -> void:
	dash_cooldown = maxf(0.0, dash_cooldown - delta)
	shot_kick = maxf(0.0, shot_kick - delta * 9.0)
	dash_pulse = maxf(0.0, dash_pulse - delta * 4.5)
	damage_pulse = maxf(0.0, damage_pulse - delta * 5.0)

	if dash_time > 0.0:
		dash_time = maxf(0.0, dash_time - delta)
		velocity = dash_direction * DASH_SPEED
	else:
		var has_intent := move_intent.x != 0.0 or move_intent.y != 0.0
		var desired := move_intent * MAX_SPEED
		var response := ACCELERATION if has_intent else BRAKE
		var blend := minf(1.0, response * delta)
		velocity += (desired - velocity) * blend

	var next_pos := CollisionUtils.resolve_circle_world_movement(
		position,
		position.x + velocity.x * delta,
		position.y + velocity.y * delta,
		PLAYER_RADIUS, bounds, obstacles)

	var prev := position
	position = next_pos

	if absf(position.x - prev.x) < 0.001 and velocity.x != 0.0:
		velocity.x = 0.0
	if absf(position.y - prev.y) < 0.001 and velocity.y != 0.0:
		velocity.y = 0.0

	modulate.a = 0.72 + (0.7 + life_ratio * 0.3) * 0.28
	queue_redraw()

func _draw() -> void:
	var speed := velocity.length()
	var speed_factor := minf(1.0, speed / MAX_SPEED)
	var dash_factor := 1.0 if dash_time > 0.0 else dash_pulse
	var vitality := 0.7 + life_ratio * 0.3

	var glow_alpha := 0.12 + speed_factor * 0.1 + dash_factor * 0.18 + damage_pulse * 0.18
	var glow_scale := 1.0 + speed_factor * 0.2 + dash_factor * 0.16 + damage_pulse * 0.12
	var glow_color := Palette.PLAYER_EDGE
	glow_color.a = glow_alpha
	draw_rect(Rect2(Vector2(-22, -22) * glow_scale, Vector2(44, 44) * glow_scale), glow_color, true)

	var shell_color := Palette.PLAYER_BODY
	shell_color.a = 0.84 + life_ratio * 0.16
	var shell_scale := 1.0 + dash_factor * 0.05
	draw_rect(Rect2(Vector2(-14, -14) * shell_scale, Vector2(28, 28) * shell_scale), shell_color, true)

	var edge_color := Palette.PLAYER_EDGE
	edge_color.a = 0.66 + dash_factor * 0.26 + damage_pulse * 0.26
	draw_rect(Rect2(Vector2(-14, -14) * shell_scale, Vector2(28, 28) * shell_scale), edge_color, false, 2.0)

	var core_color := Palette.PLAYER_CORE
	core_color.a = 0.72 + vitality * 0.2 + damage_pulse * 0.22
	var core_scale := 1.0 + damage_pulse * 0.26
	draw_rect(Rect2(Vector2(-5, -5) * core_scale, Vector2(10, 10) * core_scale), core_color, true)

	var aim_x := cos(aim_angle)
	var aim_y := sin(aim_angle)
	var arrow_dist := arrow_distance_base + shot_kick * 8.0 + dash_factor * 10.0
	var arrow_pos := Vector2(aim_x * arrow_dist, aim_y * arrow_dist)
	var arrow_color := Palette.ACCENT
	arrow_color.a = 0.98
	var arrow_size := 12.0 + shot_kick * 2.0
	var arrow_points := PackedVector2Array([
		arrow_pos + Vector2(cos(aim_angle), sin(aim_angle)) * arrow_size,
		arrow_pos + Vector2(cos(aim_angle + 2.4), sin(aim_angle + 2.4)) * arrow_size * 0.8,
		arrow_pos + Vector2(cos(aim_angle - 2.4), sin(aim_angle - 2.4)) * arrow_size * 0.8,
	])
	draw_colored_polygon(arrow_points, arrow_color)
