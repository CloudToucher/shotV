class_name CombatHUD
extends Control

var _health_bar: ProgressBar
var _wave_label: Label
var _weapon_label: Label
var _kill_label: Label
var _hint_label: Label

var _quick_slots: Array[Label] = []

func _ready() -> void:
	mouse_filter = MOUSE_FILTER_IGNORE
	
	_health_bar = ProgressBar.new()
	_health_bar.position = Vector2(20, 20)
	_health_bar.size = Vector2(200, 16)
	_health_bar.max_value = 100
	_health_bar.value = 100
	_health_bar.show_percentage = false
	add_child(_health_bar)
	
	_wave_label = Label.new()
	_wave_label.position = Vector2(20, 42)
	_wave_label.add_theme_font_size_override("font_size", 16)
	_wave_label.add_theme_color_override("font_color", Palette.UI_TEXT)
	add_child(_wave_label)
	
	_weapon_label = Label.new()
	_weapon_label.position = Vector2(20, 66)
	_weapon_label.add_theme_font_size_override("font_size", 16)
	_weapon_label.add_theme_color_override("font_color", Palette.UI_TEXT)
	add_child(_weapon_label)
	
	_kill_label = Label.new()
	_kill_label.position = Vector2(20, 90)
	_kill_label.add_theme_font_size_override("font_size", 16)
	_kill_label.add_theme_color_override("font_color", Palette.UI_TEXT)
	add_child(_kill_label)
	
	_hint_label = Label.new()
	_hint_label.set_anchors_preset(PRESET_BOTTOM_WIDE)
	_hint_label.position.y = -60
	_hint_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_hint_label.add_theme_font_size_override("font_size", 18)
	_hint_label.add_theme_color_override("font_color", Palette.UI_TEXT)
	add_child(_hint_label)

	for i in 4:
		var lbl := Label.new()
		lbl.position = Vector2(20 + i * 50, 120)
		lbl.size = Vector2(40, 40)
		lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		lbl.add_theme_font_size_override("font_size", 14)
		
		var bg := ColorRect.new()
		bg.color = Color(Palette.FRAME, 0.2)
		bg.set_anchors_preset(PRESET_FULL_RECT)
		lbl.add_child(bg)
		
		add_child(lbl)
		_quick_slots.append(lbl)

func update_stats(health: int, max_health: int, wave: int, kills: int, 
		weapon_name: String, state: String, enemy_count: int) -> void:
	_health_bar.max_value = max_health
	_health_bar.value = health
	_wave_label.text = "波次: %d / 击杀: %d" % [wave, kills]
	_weapon_label.text = "武器: %s" % weapon_name
	_kill_label.text = "敌人: %d / 状态: %s" % [enemy_count, state]

func update_hint(text: String) -> void:
	_hint_label.text = text

func update_quick_slots(slots: Array) -> void:
	var keys := ["Z", "X", "C", "V"]
	for i in 4:
		var item_id: String = slots[i] if i < slots.size() else ""
		if item_id.is_empty():
			_quick_slots[i].text = keys[i]
			_quick_slots[i].get_child(0).color = Color(Palette.FRAME, 0.2)
		else:
			var def := GameData.get_item_def(item_id)
			_quick_slots[i].text = "%s\n%s" % [keys[i], def.short_label if def else ""]
			_quick_slots[i].get_child(0).color = Color(def.tint if def else Palette.FRAME, 0.4)
