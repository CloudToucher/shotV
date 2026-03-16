class_name ToastOverlay
extends Control

var _toast_queue: Array[Dictionary] = []
var _current_toast: Dictionary = {}
var _toast_timer := 0.0
var _display_duration := 2.5
var _fade_duration := 0.3

var _label_title: Label
var _label_detail: Label
var _bg_rect: ColorRect

func _ready() -> void:
	_bg_rect = ColorRect.new()
	_bg_rect.color = Color(Palette.UI_TEXT, 0.8)
	_bg_rect.set_anchors_preset(PRESET_TOP_WIDE)
	_bg_rect.custom_minimum_size = Vector2(0, 70)
	add_child(_bg_rect)

	_label_title = Label.new()
	_label_title.add_theme_font_size_override("font_size", 18)
	_label_title.add_theme_color_override("font_color", Palette.UI_PANEL)
	_label_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_label_title.set_anchors_preset(PRESET_TOP_WIDE)
	_label_title.position.y = 12
	add_child(_label_title)

	_label_detail = Label.new()
	_label_detail.add_theme_font_size_override("font_size", 14)
	_label_detail.add_theme_color_override("font_color", Palette.UI_MUTED)
	_label_detail.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_label_detail.set_anchors_preset(PRESET_TOP_WIDE)
	_label_detail.position.y = 38
	add_child(_label_detail)

	modulate.a = 0.0
	mouse_filter = MOUSE_FILTER_IGNORE

func show_toast(title: String, detail: String, duration: float = 2.5) -> void:
	_toast_queue.append({
		"title": title,
		"detail": detail,
		"duration": duration
	})

func _process(delta: float) -> void:
	if _current_toast.is_empty():
		if _toast_queue.size() > 0:
			_current_toast = _toast_queue.pop_front()
			_label_title.text = _current_toast["title"]
			_label_detail.text = _current_toast["detail"]
			_toast_timer = _current_toast["duration"] + _fade_duration * 2
	else:
		_toast_timer -= delta
		if _toast_timer <= 0:
			_current_toast = {}
			modulate.a = 0.0
		else:
			var total_time: float = _current_toast["duration"] + _fade_duration * 2
			if _toast_timer > total_time - _fade_duration:
				modulate.a = (total_time - _toast_timer) / _fade_duration
			elif _toast_timer < _fade_duration:
				modulate.a = _toast_timer / _fade_duration
			else:
				modulate.a = 1.0
