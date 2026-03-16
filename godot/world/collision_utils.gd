class_name CollisionUtils
extends RefCounted

static func resolve_circle_world_movement(
		pos: Vector2, next_x: float, next_y: float, radius: float,
		bounds: Rect2, obstacles: Array) -> Vector2:
	var start_x := clampf(pos.x, bounds.position.x + radius, bounds.end.x - radius)
	var start_y := clampf(pos.y, bounds.position.y + radius, bounds.end.y - radius)
	var x := clampf(next_x, bounds.position.x + radius, bounds.end.x - radius)
	if _collides_any_obstacle(x, start_y, radius, obstacles):
		x = start_x
	var y := clampf(next_y, bounds.position.y + radius, bounds.end.y - radius)
	if _collides_any_obstacle(x, y, radius, obstacles):
		y = start_y
	return Vector2(x, y)

static func clip_segment_to_world(
		origin: Vector2, target: Vector2, bounds: Rect2,
		obstacles: Array, padding: float = 0.0) -> Vector2:
	var best_t := _clip_segment_to_bounds_t(origin, target, bounds)
	for obs in obstacles:
		if not obs is Dictionary:
			continue
		var expanded := _expand_obstacle(obs, padding)
		var hit := _segment_rect_intersection(origin, target, expanded)
		if hit >= 0.0 and hit < best_t:
			best_t = hit
	return origin + (target - origin) * best_t

static func segment_circle_intersection(
		origin: Vector2, target: Vector2, circle_pos: Vector2,
		radius: float) -> float:
	var d := target - origin
	var f := origin - circle_pos
	var a := d.dot(d)
	var b := 2.0 * f.dot(d)
	var c := f.dot(f) - radius * radius
	var discriminant := b * b - 4.0 * a * c
	if a <= 0.0001 or discriminant < 0.0:
		return -1.0
	var root := sqrt(discriminant)
	var near := (-b - root) / (2.0 * a)
	var far := (-b + root) / (2.0 * a)
	if near >= 0.0 and near <= 1.0:
		return near
	if far >= 0.0 and far <= 1.0:
		return far
	return -1.0

static func point_inside_obstacle(x: float, y: float, obs: Dictionary) -> bool:
	var ox: float = obs.get("x", 0.0)
	var oy: float = obs.get("y", 0.0)
	var ow: float = obs.get("width", 0.0)
	var oh: float = obs.get("height", 0.0)
	return x >= ox and x <= ox + ow and y >= oy and y <= oy + oh

static func _collides_any_obstacle(x: float, y: float, padding: float, obstacles: Array) -> bool:
	for obs in obstacles:
		if not obs is Dictionary:
			continue
		var expanded := _expand_obstacle(obs, padding)
		if x >= expanded["left"] and x <= expanded["right"] and \
		   y >= expanded["top"] and y <= expanded["bottom"]:
			return true
	return false

static func _expand_obstacle(obs: Dictionary, padding: float) -> Dictionary:
	var ox: float = obs.get("x", 0.0)
	var oy: float = obs.get("y", 0.0)
	var ow: float = obs.get("width", 0.0)
	var oh: float = obs.get("height", 0.0)
	return {
		"left": ox - padding,
		"top": oy - padding,
		"right": ox + ow + padding,
		"bottom": oy + oh + padding,
	}

static func _clip_segment_to_bounds_t(origin: Vector2, target: Vector2, bounds: Rect2) -> float:
	var best_t := 1.0
	var dx := target.x - origin.x
	var dy := target.y - origin.y
	if abs(dx) > 0.0001:
		best_t = _resolve_intersection(best_t, (bounds.position.x - origin.x) / dx, origin, target, bounds, true)
		best_t = _resolve_intersection(best_t, (bounds.end.x - origin.x) / dx, origin, target, bounds, true)
	if abs(dy) > 0.0001:
		best_t = _resolve_intersection(best_t, (bounds.position.y - origin.y) / dy, origin, target, bounds, false)
		best_t = _resolve_intersection(best_t, (bounds.end.y - origin.y) / dy, origin, target, bounds, false)
	return best_t

static func _resolve_intersection(
		current_best: float, candidate_t: float,
		origin: Vector2, target: Vector2, bounds: Rect2,
		is_x_axis: bool) -> float:
	if candidate_t <= 0.0 or candidate_t > current_best:
		return current_best
	var px := origin.x + (target.x - origin.x) * candidate_t
	var py := origin.y + (target.y - origin.y) * candidate_t
	if is_x_axis and py >= bounds.position.y and py <= bounds.end.y:
		return candidate_t
	if not is_x_axis and px >= bounds.position.x and px <= bounds.end.x:
		return candidate_t
	return current_best

static func _segment_rect_intersection(
		origin: Vector2, target: Vector2, rect: Dictionary) -> float:
	var dx := target.x - origin.x
	var dy := target.y - origin.y
	var entry := 0.0
	var exit_val := 1.0
	var checks: Array = [
		[-dx, origin.x - rect.get("left", 0.0)],
		[dx, rect.get("right", 0.0) - origin.x],
		[-dy, origin.y - rect.get("top", 0.0)],
		[dy, rect.get("bottom", 0.0) - origin.y],
	]
	for check in checks:
		var p: float = check[0]
		var q: float = check[1]
		if abs(p) < 0.0001:
			if q < 0.0:
				return -1.0
			continue
		var ratio := q / p
		if p < 0.0:
			entry = maxf(entry, ratio)
		else:
			exit_val = minf(exit_val, ratio)
		if entry > exit_val:
			return -1.0
	if entry > 0.0:
		return entry
	if exit_val >= 0.0:
		return 0.0
	return -1.0
