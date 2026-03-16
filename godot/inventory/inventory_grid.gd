class_name InventoryGrid
extends RefCounted

const RUN_QUICK_SLOT_COUNT := 4

class ItemRecord:
	var id: String
	var item_id: String
	var quantity: int
	var x: int
	var y: int
	var width: int
	var height: int
	var rotated: bool

	func _init(p_id: String = "", p_item_id: String = "", p_qty: int = 1,
			p_x: int = 0, p_y: int = 0, p_w: int = 1, p_h: int = 1,
			p_rotated: bool = false) -> void:
		id = p_id
		item_id = p_item_id
		quantity = p_qty
		x = p_x
		y = p_y
		width = p_w
		height = p_h
		rotated = p_rotated

	func duplicate_record() -> ItemRecord:
		return ItemRecord.new(id, item_id, quantity, x, y, width, height, rotated)

	func to_dict() -> Dictionary:
		return {
			"id": id, "item_id": item_id, "quantity": quantity,
			"x": x, "y": y, "width": width, "height": height, "rotated": rotated,
		}

	static func from_dict(d: Dictionary) -> ItemRecord:
		return ItemRecord.new(
			d.get("id", ""), d.get("item_id", ""), d.get("quantity", 1),
			d.get("x", 0), d.get("y", 0), d.get("width", 1), d.get("height", 1),
			d.get("rotated", false))

static func create_item_record(item_id: String, quantity: int, record_id: String = "") -> ItemRecord:
	var def: GameData.ItemDef = GameData.get_item_def(item_id)
	if def == null:
		return null
	if record_id.is_empty():
		record_id = "item-%s-%s-%s" % [item_id, str(Time.get_ticks_msec()), str(randi() % 100000)]
	var item := ItemRecord.new(record_id, item_id,
		clampi(quantity, 1, def.max_stack), 0, 0, def.width, def.height, false)
	return item

static func place_item_in_grid(columns: int, rows: int, items: Array,
		incoming: ItemRecord) -> Dictionary:
	var placement := _find_first_placement(columns, rows, items, incoming)
	if placement.is_empty():
		return { "placed": false, "items": _clone_items(items) }
	var result := _clone_items(items)
	var placed := incoming.duplicate_record()
	placed.x = placement["x"]
	placed.y = placement["y"]
	result.append(placed)
	return { "placed": true, "items": result }

static func place_item_at_position(columns: int, rows: int, items: Array,
		incoming: ItemRecord, px: int, py: int) -> Dictionary:
	if not can_place_at(columns, rows, items, incoming, px, py):
		return { "placed": false, "items": _clone_items(items) }
	var result := _clone_items(items)
	var placed := incoming.duplicate_record()
	placed.x = px
	placed.y = py
	result.append(placed)
	return { "placed": true, "items": result }

static func place_items_in_grid(columns: int, rows: int, existing: Array,
		incoming: Array) -> Dictionary:
	var items := _clone_items(existing)
	var placed_ids: Array[String] = []
	var rejected: Array = []
	var sorted_incoming := incoming.duplicate()
	sorted_incoming.sort_custom(func(a, b): return a.width * a.height > b.width * b.height)
	for item in sorted_incoming:
		var result := place_item_in_grid(columns, rows, items, item)
		if result["placed"]:
			items = result["items"]
			placed_ids.append(item.id)
		else:
			rejected.append(item.duplicate_record())
	return { "items": items, "placed_ids": placed_ids, "rejected": rejected }

static func can_place_at(columns: int, rows: int, items: Array,
		incoming: ItemRecord, px: int, py: int) -> bool:
	if px < 0 or py < 0:
		return false
	if px + incoming.width > columns or py + incoming.height > rows:
		return false
	return not _intersects_any(items, px, py, incoming.width, incoming.height)

static func pick_item_at_cell(items: Array, cx: int, cy: int) -> Dictionary:
	var target := find_item_at_cell(items, cx, cy)
	if target == null:
		return { "item": null, "items": _clone_items(items) }
	var result: Array = []
	for item in items:
		if item.id != target.id:
			result.append(item.duplicate_record())
	return { "item": target.duplicate_record(), "items": result }

static func find_item_at_cell(items: Array, cx: int, cy: int) -> ItemRecord:
	for item in items:
		if cx >= item.x and cx < item.x + item.width and \
		   cy >= item.y and cy < item.y + item.height:
			return item
	return null

static func rotate_item(item: ItemRecord) -> ItemRecord:
	var rotated := item.duplicate_record()
	rotated.width = item.height
	rotated.height = item.width
	rotated.rotated = not item.rotated
	return rotated

static func get_used_cells(items: Array) -> int:
	var total := 0
	for item in items:
		total += item.width * item.height
	return total

static func get_capacity(columns: int, rows: int) -> int:
	return columns * rows

static func auto_arrange(columns: int, rows: int, items: Array) -> Dictionary:
	var ordered := _clone_items(items)
	for item in ordered:
		item.x = 0
		item.y = 0
	ordered.sort_custom(func(a, b): return a.width * a.height > b.width * b.height)
	var occupied := []
	occupied.resize(columns * rows)
	occupied.fill(false)
	var arranged := _solve_arrangement(columns, rows, occupied, ordered, [])
	if arranged == null:
		return { "arranged": false, "items": _clone_items(items) }
	return { "arranged": true, "items": arranged }

static func build_resource_ledger(items: Array) -> Dictionary:
	var salvage := 0
	var alloy := 0
	var research := 0
	for item in items:
		var def: GameData.ItemDef = GameData.get_item_def(item.item_id)
		if def == null:
			continue
		salvage += def.recovered_salvage * item.quantity
		alloy += def.recovered_alloy * item.quantity
		research += def.recovered_research * item.quantity
	return { "salvage": salvage, "alloy": alloy, "research": research }

static func consume_item_by_id(items: Array, item_id: String, amount: int = 1) -> Dictionary:
	var next_items := _clone_items(items)
	var target: ItemRecord = null
	for item in next_items:
		if item.id == item_id:
			target = item
			break
	if target == null or amount <= 0:
		return { "consumed": false, "item": null, "items": next_items }
	target.quantity = maxi(0, target.quantity - amount)
	var consumed_item := target.duplicate_record()
	consumed_item.quantity = mini(amount, target.quantity + amount)
	var filtered: Array = []
	for item in next_items:
		if item.quantity > 0:
			filtered.append(item)
	return { "consumed": true, "item": consumed_item, "items": filtered }

static func sanitize_quick_slots(quick_slots: Array, valid_item_ids: Array) -> Array:
	var valid_set := {}
	for vid in valid_item_ids:
		valid_set[vid] = true
	var seen := {}
	var result: Array = []
	for i in RUN_QUICK_SLOT_COUNT:
		var item_id: String = quick_slots[i] if i < quick_slots.size() else ""
		if item_id.is_empty() or not valid_set.has(item_id) or seen.has(item_id):
			result.append("")
		else:
			seen[item_id] = true
			result.append(item_id)
	return result

static func assign_quick_slot(quick_slots: Array, slot_index: int,
		item_id: String) -> Array:
	var normalized: Array = []
	for i in RUN_QUICK_SLOT_COUNT:
		normalized.append(quick_slots[i] if i < quick_slots.size() else "")
	if slot_index < 0 or slot_index >= normalized.size():
		return normalized
	var was_same := not item_id.is_empty() and normalized[slot_index] == item_id
	for i in normalized.size():
		if normalized[i] == item_id:
			normalized[i] = ""
	normalized[slot_index] = "" if was_same else item_id
	return normalized

static func _find_first_placement(columns: int, rows: int, items: Array,
		incoming: ItemRecord) -> Dictionary:
	for py in range(0, rows - incoming.height + 1):
		for px in range(0, columns - incoming.width + 1):
			if not _intersects_any(items, px, py, incoming.width, incoming.height):
				return { "x": px, "y": py }
	return {}

static func _intersects_any(items: Array, px: int, py: int, w: int, h: int) -> bool:
	for item in items:
		if item.x < px + w and item.x + item.width > px and \
		   item.y < py + h and item.y + item.height > py:
			return true
	return false

static func _clone_items(items: Array) -> Array:
	var result: Array = []
	for item in items:
		result.append(item.duplicate_record())
	return result

static func _solve_arrangement(columns: int, rows: int, occupied: Array,
		remaining: Array, placed: Array) -> Array:
	if remaining.size() == 0:
		return _clone_items(placed)
	var anchor_idx := -1
	for i in occupied.size():
		if not occupied[i]:
			anchor_idx = i
			break
	if anchor_idx == -1:
		return null
	var ax := anchor_idx % columns
	var ay := anchor_idx / columns
	var attempted := {}

	for i in remaining.size():
		var item: ItemRecord = remaining[i]
		for variant in _get_variants(item):
			var sig := "%s:%d:%dx%d:%s" % [variant.item_id, variant.quantity,
				variant.width, variant.height, str(variant.rotated)]
			if attempted.has(sig):
				continue
			attempted[sig] = true
			if not _can_place_on_occupied(columns, rows, occupied, variant, ax, ay):
				continue
			_mark_cells(columns, occupied, variant, ax, ay, true)
			var next_remaining := remaining.duplicate()
			next_remaining.remove_at(i)
			var next_placed := placed.duplicate()
			var placed_item := variant.duplicate_record()
			placed_item.x = ax
			placed_item.y = ay
			next_placed.append(placed_item)
			var result := _solve_arrangement(columns, rows, occupied, next_remaining, next_placed)
			_mark_cells(columns, occupied, variant, ax, ay, false)
			if result != null:
				return result
	return null

static func _get_variants(item: ItemRecord) -> Array:
	var variants: Array = [item.duplicate_record()]
	if item.width != item.height:
		variants.append(rotate_item(item))
	return variants

static func _can_place_on_occupied(columns: int, rows: int, occupied: Array,
		item: ItemRecord, px: int, py: int) -> bool:
	if px + item.width > columns or py + item.height > rows:
		return false
	for row in range(py, py + item.height):
		for col in range(px, px + item.width):
			if occupied[row * columns + col]:
				return false
	return true

static func _mark_cells(columns: int, occupied: Array, item: ItemRecord,
		px: int, py: int, value: bool) -> void:
	for row in range(py, py + item.height):
		for col in range(px, px + item.width):
			occupied[row * columns + col] = value
