extends Node2D

var current_scene: Node = null
var current_mode: String = ""

func _ready() -> void:
	GameStore.mode_changed.connect(_on_mode_changed)
	GameStore.run_structure_changed.connect(_on_run_structure_changed)
	_swap_scene(GameStore.get_mode())

func _on_mode_changed(new_mode: String, _old_mode: String) -> void:
	_swap_scene(new_mode)

func _on_run_structure_changed() -> void:
	if GameStore.get_mode() != current_mode:
		_swap_scene(GameStore.get_mode())

func _swap_scene(mode: String) -> void:
	if current_scene != null:
		current_scene.queue_free()
		current_scene = null

	current_mode = mode

	if mode == "combat":
		var combat_scene := preload("res://scenes/combat_scene.tscn").instantiate()
		add_child(combat_scene)
		current_scene = combat_scene
	else:
		var base_scene := preload("res://scenes/base_camp.tscn").instantiate()
		add_child(base_scene)
		current_scene = base_scene
