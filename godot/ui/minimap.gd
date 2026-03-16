class_name Minimap
extends Control

const MAP_BG := Color("ffffff")
const MAP_BORDER := Color("4db9e6")
const COLOR_OBSTACLE := Color("bacdd8")
const COLOR_PLAYER := Color("4db9e6")
const COLOR_ENEMY := Color("ff6b6b")
const COLOR_MARKER := Color("ffb033")

var _bounds: Rect2
var _obstacles: Array = []
var _markers: Array = []
var _enemies: Array = []
var _player_pos: Vector2
var _player_aim: float
var _update_timer := 0.0

func setup(bounds: Rect2, obstacles: Array, markers: Array) -> void:
	_bounds = bounds
	_obstacles = obstacles
	_markers = markers

func update_state(player_pos: Vector2, aim_angle: float, enemies: Array, delta: float) -> void:
	_player_pos = player_pos
	_player_aim = aim_angle
	_enemies = enemies

	_update_timer += delta
	if _update_timer >= 0.2:
		_update_timer = 0.0
		queue_redraw()

func _draw() -> void:
	if _bounds.size.x <= 0 or _bounds.size.y <= 0:
		return

	var w := size.x
	var h := size.y
	draw_rect(Rect2(0, 0, w, h), MAP_BG, true)
	draw_rect(Rect2(0, 0, w, h), MAP_BORDER, false, 2.0)

	var scale_x := w / _bounds.size.x
	var scale_y := h / _bounds.size.y
	var min_scale := minf(scale_x, scale_y)
	
	var offset_x := (w - _bounds.size.x * min_scale) * 0.5
	var offset_y := (h - _bounds.size.y * min_scale) * 0.5

	for obs in _obstacles:
		if not obs is Dictionary: continue
		var x := offset_x + (obs.get("x", 0.0) - _bounds.position.x) * min_scale
		var y := offset_y + (obs.get("y", 0.0) - _bounds.position.y) * min_scale
		var ow := obs.get("width", 0.0) * min_scale
		var oh := obs.get("height", 0.0) * min_scale
		draw_rect(Rect2(x, y, ow, oh), COLOR_OBSTACLE, true)

	for marker in _markers:
		if not marker is Dictionary: continue
		var mx := offset_x + (marker.get("x", 0.0) - _bounds.position.x) * min_scale
		var my := offset_y + (marker.get("y", 0.0) - _bounds.position.y) * min_scale
		draw_circle(Vector2(mx, my), 3.0, COLOR_MARKER)

	for ed in _enemies:
		var ex := offset_x + (ed["x"] - _bounds.position.x) * min_scale
		var ey := offset_y + (ed["y"] - _bounds.position.y) * min_scale
		draw_circle(Vector2(ex, ey), 2.5, COLOR_ENEMY)

	var px := offset_x + (_player_pos.x - _bounds.position.x) * min_scale
	var py := offset_y + (_player_pos.y - _bounds.position.y) * min_scale
	draw_circle(Vector2(px, py), 3.0, COLOR_PLAYER)
	
	var aim_end := Vector2(px + cos(_player_aim) * 8.0, py + sin(_player_aim) * 8.0)
	draw_line(Vector2(px, py), aim_end, COLOR_PLAYER, 1.5)
