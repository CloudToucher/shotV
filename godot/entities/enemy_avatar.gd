class_name EnemyAvatar
extends Node2D

var definition: GameData.HostileDef
var enemy_type: int = GameData.HostileType.MELEE
var seed_val: float = 0.0
var aim_angle: float = 0.0
var life_ratio: float = 1.0
var motion: float = 0.0
var mode: int = GameData.HostileMode.ADVANCE
var damage_flash: float = 0.0
var attack_pulse: float = 0.0

func setup(type: int, p_seed: float) -> void:
	enemy_type = type
	seed_val = p_seed
	definition = GameData.hostile_by_type.get(type, null)

func set_aim_angle_val(angle: float) -> void:
	aim_angle = angle

func set_life_ratio_val(ratio: float) -> void:
	life_ratio = clampf(ratio, 0.0, 1.0)

func set_motion_val(amount: float) -> void:
	motion = clampf(amount, 0.0, 1.0)

func set_mode_val(m: int) -> void:
	mode = m

func trigger_damage_flash(intensity: float = 1.0) -> void:
	damage_flash = maxf(damage_flash, intensity)

func trigger_attack_pulse(intensity: float = 1.0) -> void:
	attack_pulse = maxf(attack_pulse, intensity)

func update_avatar(delta: float, elapsed: float) -> void:
	damage_flash = maxf(0.0, damage_flash - delta * 5.5)
	attack_pulse = maxf(0.0, attack_pulse - delta * 4.4)
	modulate.a = 0.7 + life_ratio * 0.3
	queue_redraw()

func _draw() -> void:
	if definition == null:
		return

	var mode_pulse := _get_mode_strength()
	var shell_scale := 1.0 + attack_pulse * 0.06 + mode_pulse * 0.04
	var glow_scale := 1.0 + motion * 0.08 + mode_pulse * 0.16 + damage_flash * 0.12

	var glow_color := definition.color_glow
	glow_color.a = 0.12 + mode_pulse * 0.16 + damage_flash * 0.18
	var edge_color := definition.color_edge
	edge_color.a = 0.72 + damage_flash * 0.18 + mode_pulse * 0.12
	var body_color := definition.color_body
	body_color.a = 0.9 + life_ratio * 0.1
	var core_color := Palette.ARENA_CORE
	core_color.a = 0.92

	match enemy_type:
		GameData.HostileType.MELEE:
			_draw_diamond(glow_color, 20.0 * glow_scale)
			_draw_diamond(body_color, 15.0 * shell_scale)
			_draw_diamond_outline(edge_color, 15.0 * shell_scale, 2.0)
			draw_rect(Rect2(-4, -4, 8, 8), core_color, true)
		GameData.HostileType.RANGED:
			var gs := 18.0 * glow_scale
			draw_rect(Rect2(-gs, -gs, gs * 2, gs * 2), glow_color, true)
			var ss := 12.0 * shell_scale
			draw_rect(Rect2(-ss, -ss, ss * 2, ss * 2), body_color, true)
			draw_rect(Rect2(-ss, -ss, ss * 2, ss * 2), edge_color, false, 2.0)
			draw_rect(Rect2(-5, -5, 10, 10), core_color, true)
		GameData.HostileType.CHARGER:
			var pts_glow := PackedVector2Array([
				Vector2(18, 0) * glow_scale,
				Vector2(-4, -19) * glow_scale,
				Vector2(-12, -8) * glow_scale,
				Vector2(-12, 8) * glow_scale,
				Vector2(-4, 19) * glow_scale,
			])
			draw_colored_polygon(pts_glow, glow_color)
			var pts_body := PackedVector2Array([
				Vector2(15, 0) * shell_scale,
				Vector2(-6, -14) * shell_scale,
				Vector2(-10, -6) * shell_scale,
				Vector2(-10, 6) * shell_scale,
				Vector2(-6, 14) * shell_scale,
			])
			draw_colored_polygon(pts_body, body_color)
			draw_polyline(pts_body, edge_color, 2.0)
			draw_rect(Rect2(-4, -4, 8, 8), core_color, true)
		GameData.HostileType.BOSS:
			var pts_glow := PackedVector2Array([
				Vector2(22, 0) * glow_scale, Vector2(10, -22) * glow_scale,
				Vector2(-12, -22) * glow_scale, Vector2(-24, 0) * glow_scale,
				Vector2(-12, 22) * glow_scale, Vector2(10, 22) * glow_scale,
			])
			draw_colored_polygon(pts_glow, glow_color)
			var pts_body := PackedVector2Array([
				Vector2(19, 0) * shell_scale, Vector2(8, -18) * shell_scale,
				Vector2(-10, -18) * shell_scale, Vector2(-20, 0) * shell_scale,
				Vector2(-10, 18) * shell_scale, Vector2(8, 18) * shell_scale,
			])
			draw_colored_polygon(pts_body, body_color)
			draw_polyline(pts_body, edge_color, 2.2)
			var core_pts := PackedVector2Array([
				Vector2(10, 0), Vector2(-6, -7), Vector2(-1, 0), Vector2(-6, 7),
			])
			draw_colored_polygon(core_pts, core_color)

	_draw_aim_indicator(mode_pulse)
	_draw_health_bar()

func _draw_diamond(color: Color, size: float) -> void:
	var pts := PackedVector2Array([
		Vector2(0, -size), Vector2(size, 0), Vector2(0, size), Vector2(-size, 0),
	])
	draw_colored_polygon(pts, color)

func _draw_diamond_outline(color: Color, size: float, width: float) -> void:
	var pts := PackedVector2Array([
		Vector2(0, -size), Vector2(size, 0), Vector2(0, size), Vector2(-size, 0), Vector2(0, -size),
	])
	draw_polyline(pts, color, width)

func _draw_aim_indicator(mode_pulse: float) -> void:
	if definition == null:
		return
	var aim_dist := definition.radius + 10.0 + mode_pulse * 6.0
	var aim_pos := Vector2(cos(aim_angle), sin(aim_angle)) * aim_dist
	var blade_color := definition.color_edge
	blade_color.a = 0.28 + mode_pulse * 0.32 + attack_pulse * 0.18
	var blade_pts := PackedVector2Array([
		aim_pos + Vector2(cos(aim_angle), sin(aim_angle)) * 10.0,
		aim_pos + Vector2(cos(aim_angle + 2.6), sin(aim_angle + 2.6)) * 4.0,
		aim_pos + Vector2(cos(aim_angle - 2.6), sin(aim_angle - 2.6)) * 4.0,
	])
	draw_colored_polygon(blade_pts, blade_color)

func _draw_health_bar() -> void:
	if definition == null:
		return
	var visible_bar := enemy_type == GameData.HostileType.BOSS or life_ratio < 0.999 or damage_flash > 0.08
	if not visible_bar:
		return
	var bar_width := 62.0 if enemy_type == GameData.HostileType.BOSS else 30.0
	var bar_y := -definition.radius - 14.0
	var bg_color := Palette.UI_TEXT
	bg_color.a = 0.18
	draw_rect(Rect2(-bar_width / 2.0, bar_y, bar_width, 4.0), bg_color, true)
	var fill_color := Palette.DASH
	fill_color.a = 0.92
	draw_rect(Rect2(-bar_width / 2.0, bar_y, bar_width * life_ratio, 4.0), fill_color, true)

func _get_mode_strength() -> float:
	match mode:
		GameData.HostileMode.ADVANCE: return 0.0
		GameData.HostileMode.AIM: return 0.48
		GameData.HostileMode.WINDUP: return 0.74
		GameData.HostileMode.CHARGE: return 1.0
		GameData.HostileMode.RECOVER: return 0.24
	return 0.0
