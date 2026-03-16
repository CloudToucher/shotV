extends Node2D

const WORLD_CAMERA_ZOOM := 1.4
const GRID_SIZE := 80.0
const INTERACTION_RADIUS := 120.0
const LAUNCH_GATE_CHANNEL_SECONDS := 0.72
const LAUNCH_GATE_OPEN_SECONDS := 0.34

var layout: WorldLayout
var player: PlayerAvatar
var camera: Camera2D
var terrain_node: Node2D
var obstacle_layer: Node2D
var marker_layer: Node2D
var hint_label: Label
var title_label: Label

var panel_open := false
var map_open := false
var pending_launch_gate: Dictionary = {}
var elapsed_seconds := 0.0

func _ready() -> void:
	layout = WorldLayout.create_base_layout()
	camera = $Camera2D
	terrain_node = $Terrain
	obstacle_layer = $ObstacleLayer
	marker_layer = $MarkerLayer
	hint_label = $UILayer/HintLabel
	title_label = $UILayer/TitleLabel

	player = PlayerAvatar.new()
	player.position = layout.player_spawn
	player.set_weapon_style(GameData.WeaponType.MACHINE_GUN)
	$Player.add_child(player)

	_rebuild_world()
	_update_title()

func _process(delta: float) -> void:
	elapsed_seconds += delta

	if Input.is_action_just_pressed("panel_toggle"):
		panel_open = not panel_open
		if panel_open:
			map_open = false

	if Input.is_action_just_pressed("map_toggle"):
		map_open = not map_open
		if map_open:
			panel_open = false

	if Input.is_action_just_pressed("interact"):
		_handle_nearby_interaction()

	if not panel_open and not map_open and pending_launch_gate.is_empty():
		var move_x := Input.get_axis("move_left", "move_right")
		var move_y := Input.get_axis("move_up", "move_down")
		player.set_move_intent(move_x, move_y)
	else:
		player.set_move_intent(0.0, 0.0)

	var mouse_world := _screen_to_world(get_viewport().get_mouse_position())
	var aim_angle := (mouse_world - player.position).angle()
	player.set_aim_angle(aim_angle)

	player.update_avatar(delta, layout.bounds, elapsed_seconds, layout.obstacles)
	_tick_launch_gate(delta)
	_apply_camera()
	_update_hint()
	_sync_scene_runtime()

func _rebuild_world() -> void:
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

	for marker in layout.markers:
		var lbl := Label.new()
		lbl.text = marker.get("label", "")
		lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		lbl.position = Vector2(marker["x"] - 40, marker["y"] + 22)
		lbl.size = Vector2(80, 20)
		lbl.add_theme_font_size_override("font_size", 12)
		marker_layer.add_child(lbl)

func _apply_camera() -> void:
	var zoom := WORLD_CAMERA_ZOOM
	camera.zoom = Vector2(zoom, zoom)
	camera.position = player.position

func _screen_to_world(screen_pos: Vector2) -> Vector2:
	return camera.get_global_transform().affine_inverse() * \
		get_viewport().get_canvas_transform().affine_inverse() * screen_pos

func _find_nearby_marker(radius: float = INTERACTION_RADIUS) -> Dictionary:
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

func _handle_nearby_interaction() -> void:
	var marker := _find_nearby_marker()
	if marker.is_empty():
		return

	var marker_id: String = marker.get("id", "")
	if marker_id == "launch":
		if pending_launch_gate.is_empty():
			pending_launch_gate = {
				"phase": "charging",
				"elapsed": 0.0,
				"duration": LAUNCH_GATE_CHANNEL_SECONDS,
				"marker_label": marker.get("label", "出击闸门"),
			}
			panel_open = false
			map_open = false
		return

	if marker_id == "command":
		panel_open = not panel_open
		map_open = false
	elif marker_id == "workshop":
		panel_open = not panel_open
		map_open = false
	elif marker_id == "locker":
		panel_open = not panel_open
		map_open = false

func _tick_launch_gate(delta: float) -> void:
	if pending_launch_gate.is_empty():
		return
	var marker := _find_nearby_marker()
	if marker.is_empty() or marker.get("id") != "launch" or map_open:
		pending_launch_gate = {}
		return

	pending_launch_gate["elapsed"] = pending_launch_gate.get("elapsed", 0.0) + delta
	if pending_launch_gate["elapsed"] < pending_launch_gate.get("duration", 1.0):
		return

	if pending_launch_gate.get("phase") == "charging":
		pending_launch_gate = {
			"phase": "opening",
			"elapsed": 0.0,
			"duration": LAUNCH_GATE_OPEN_SECONDS,
			"marker_label": pending_launch_gate.get("marker_label", ""),
		}
		return

	pending_launch_gate = {}
	GameStore.deploy_combat()

func _update_title() -> void:
	var Routes := preload("res://world/routes.gd")
	var route := Routes.get_world_route(
		GameStore.save.get("world", {}).get("selected_route_id", "combat-sandbox-route"))
	title_label.text = "基地待命 // 当前路线：%s" % route.get("label", "未知")

func _update_hint() -> void:
	var marker := _find_nearby_marker()
	if not pending_launch_gate.is_empty():
		var progress := pending_launch_gate.get("elapsed", 0.0) / maxf(0.01, pending_launch_gate.get("duration", 1.0))
		if pending_launch_gate.get("phase") == "charging":
			hint_label.text = "接近 %s：部署校验中 %d%%" % [pending_launch_gate.get("marker_label", ""), int(progress * 100)]
		else:
			hint_label.text = "接近 %s：闸门开启中 %d%%" % [pending_launch_gate.get("marker_label", ""), int(progress * 100)]
	elif marker.is_empty():
		hint_label.text = "靠近站点后按 E 交互，或前往出击闸门打开部署确认。"
	elif marker.get("id") == "launch":
		hint_label.text = "接近 %s：按 E 启动闸门校验" % marker.get("label", "")
	else:
		hint_label.text = "接近 %s：按 E 交互" % marker.get("label", "")

func _sync_scene_runtime() -> void:
	var marker := _find_nearby_marker()
	var at_launch := marker.get("id", "") == "launch"
	GameStore.update_scene_runtime({
		"primary_action_ready": at_launch and pending_launch_gate.is_empty(),
		"primary_action_hint": "已到达出击闸门，按 E 启动部署校验。" if at_launch else "前往出击闸门后才能出发。",
		"nearby_marker_id": marker.get("id", ""),
		"nearby_marker_label": marker.get("label", ""),
		"nearby_marker_kind": marker.get("kind", ""),
		"map_overlay_open": map_open,
	})

class _TerrainDraw extends Node2D:
	var layout: WorldLayout
	var grid_size: float = 80.0

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
			var rect := Rect2(obs.get("x", 0), obs.get("y", 0),
				obs.get("width", 0), obs.get("height", 0))
			var kind: String = obs.get("kind", "wall")
			var fill_color: Color
			match kind:
				"wall": fill_color = Palette.OBSTACLE_WALL
				"cover": fill_color = Palette.OBSTACLE_COVER
				"station": fill_color = Palette.OBSTACLE_STATION
				"locker": fill_color = Palette.OBSTACLE_LOCKER
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
				"station": color = Palette.FRAME
				"locker": color = Palette.WARNING
				_: color = Palette.FRAME
			draw_circle(pos, 8.0, color)
			var outer_color := color
			outer_color.a = 0.3
			draw_arc(pos, 16.0, 0, TAU, 32, outer_color, 1.5)
