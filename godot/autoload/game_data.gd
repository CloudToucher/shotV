extends Node

enum WeaponType { MACHINE_GUN, GRENADE, SNIPER }
enum HostileType { MELEE, RANGED, CHARGER, BOSS }
enum HostileMode { ADVANCE, AIM, WINDUP, CHARGE, RECOVER }
enum ItemCategory { RESOURCE, INTEL, BOSS, CONSUMABLE }
enum ItemRarity { COMMON, UNCOMMON, RARE, EPIC, LEGENDARY }

class WeaponDef:
	var slot: int
	var id: int # WeaponType enum
	var label: String
	var hint: String
	var cooldown: float
	var weapon_range: float
	var effect_width: float
	var effect_duration: float
	var splash_radius: float

	func _init(p_slot: int, p_id: int, p_label: String, p_hint: String,
			p_cooldown: float, p_range: float, p_effect_w: float,
			p_effect_dur: float, p_splash: float = 0.0) -> void:
		slot = p_slot
		id = p_id
		label = p_label
		hint = p_hint
		cooldown = p_cooldown
		weapon_range = p_range
		effect_width = p_effect_w
		effect_duration = p_effect_dur
		splash_radius = p_splash

class HostileDef:
	var type: int # HostileType enum
	var label: String
	var radius: float
	var max_health: float
	var move_speed: float
	var contact_damage: float
	var contact_interval: float
	var attack_cooldown: float
	var attack_windup: float
	var attack_range: float
	var preferred_distance: float
	var projectile_speed: float
	var projectile_radius: float
	var projectile_damage: float
	var charge_trigger_distance: float
	var charge_speed: float
	var charge_duration: float
	var recover_duration: float
	var color_body: Color
	var color_edge: Color
	var color_glow: Color

class ItemDef:
	var id: String
	var label: String
	var short_label: String
	var description: String
	var category: int # ItemCategory enum
	var rarity: int # ItemRarity enum
	var width: int
	var height: int
	var max_stack: int
	var tint: Color
	var accent: Color
	var recovered_salvage: int
	var recovered_alloy: int
	var recovered_research: int
	var use_heals: int
	var use_explosion_damage: int
	var use_explosion_radius: int
	var use_refresh_dash: bool

class SpawnOrder:
	var type: int # HostileType enum
	var delay: float

	func _init(p_type: int, p_delay: float) -> void:
		type = p_type
		delay = p_delay

var weapon_loadout: Array[WeaponDef] = []
var weapon_by_slot: Dictionary = {}
var hostile_by_type: Dictionary = {}
var item_catalog: Array[ItemDef] = []
var item_by_id: Dictionary = {}

func _ready() -> void:
	_init_weapons()
	_init_hostiles()
	_init_items()

func _init_weapons() -> void:
	var mg := WeaponDef.new(1, WeaponType.MACHINE_GUN, "机枪",
		"中距离压制主武器，射速高，持续火力稳定。",
		0.085, 560.0, 4.5, 0.09)
	var gl := WeaponDef.new(2, WeaponType.GRENADE, "榴弹",
		"短抛物线投射，落点爆炸，适合清理密集敌群。",
		0.46, 360.0, 0.0, 0.0, 66.0)
	var sn := WeaponDef.new(3, WeaponType.SNIPER, "狙击",
		"高穿透精确射击，单线爆发高，适合点杀目标。",
		0.72, 100000.0, 9.0, 0.16)
	weapon_loadout = [mg, gl, sn]
	weapon_by_slot = { 1: mg, 2: gl, 3: sn }

func _init_hostiles() -> void:
	var melee := HostileDef.new()
	melee.type = HostileType.MELEE
	melee.label = "追猎体"
	melee.radius = 18.0
	melee.max_health = 34.0
	melee.move_speed = 142.0
	melee.contact_damage = 12.0
	melee.contact_interval = 0.58
	melee.attack_cooldown = 0.24
	melee.color_body = Palette.ENEMY_MELEE
	melee.color_edge = Palette.ENEMY_EDGE
	melee.color_glow = Palette.ENEMY_MELEE_GLOW

	var ranged := HostileDef.new()
	ranged.type = HostileType.RANGED
	ranged.label = "射击体"
	ranged.radius = 17.0
	ranged.max_health = 30.0
	ranged.move_speed = 96.0
	ranged.contact_damage = 10.0
	ranged.contact_interval = 0.8
	ranged.attack_cooldown = 1.45
	ranged.attack_windup = 0.42
	ranged.attack_range = 420.0
	ranged.preferred_distance = 250.0
	ranged.projectile_speed = 320.0
	ranged.projectile_radius = 7.0
	ranged.projectile_damage = 12.0
	ranged.color_body = Palette.ENEMY_RANGED
	ranged.color_edge = Palette.ENEMY_EDGE
	ranged.color_glow = Palette.ENEMY_RANGED_GLOW

	var charger := HostileDef.new()
	charger.type = HostileType.CHARGER
	charger.label = "冲锋体"
	charger.radius = 20.0
	charger.max_health = 48.0
	charger.move_speed = 94.0
	charger.contact_damage = 20.0
	charger.contact_interval = 0.72
	charger.attack_cooldown = 2.4
	charger.attack_windup = 0.56
	charger.charge_trigger_distance = 240.0
	charger.charge_speed = 560.0
	charger.charge_duration = 0.28
	charger.recover_duration = 0.5
	charger.color_body = Palette.ENEMY_CHARGER
	charger.color_edge = Palette.ENEMY_EDGE
	charger.color_glow = Palette.ENEMY_CHARGER_GLOW

	var boss := HostileDef.new()
	boss.type = HostileType.BOSS
	boss.label = "神盾主核"
	boss.radius = 34.0
	boss.max_health = 520.0
	boss.move_speed = 82.0
	boss.contact_damage = 26.0
	boss.contact_interval = 0.8
	boss.attack_cooldown = 1.5
	boss.attack_windup = 0.72
	boss.attack_range = 480.0
	boss.preferred_distance = 250.0
	boss.projectile_speed = 290.0
	boss.projectile_radius = 8.0
	boss.projectile_damage = 16.0
	boss.color_body = Palette.ENEMY_BOSS
	boss.color_edge = Palette.ENEMY_EDGE
	boss.color_glow = Palette.ENEMY_BOSS_GLOW

	hostile_by_type = {
		HostileType.MELEE: melee,
		HostileType.RANGED: ranged,
		HostileType.CHARGER: charger,
		HostileType.BOSS: boss,
	}

func _init_items() -> void:
	item_catalog = [
		_make_item("salvage-scrap", "废料包", "废料",
			"基础回收物。占位小，但会快速堆满背包。",
			ItemCategory.RESOURCE, ItemRarity.COMMON, 1, 1, 8,
			Palette.PANEL_WARM, Palette.ACCENT, 1, 0, 0),
		_make_item("telemetry-cache", "遥测数据", "遥测",
			"可转化为研究进度的数据缓存。",
			ItemCategory.INTEL, ItemRarity.RARE, 1, 2, 4,
			Palette.FRAME, Palette.DASH, 0, 0, 1),
		_make_item("alloy-plate", "合金板", "合金",
			"中型结构材料，占位更长，但能补充稀缺合金。",
			ItemCategory.RESOURCE, ItemRarity.UNCOMMON, 2, 1, 4,
			Palette.MINIMAP_MARKER, Palette.MINIMAP_MARKER, 0, 1, 0),
		_make_item("aegis-core", "主核残片", "主核",
			"高价值主核残片，体积大但回收价值极高。",
			ItemCategory.BOSS, ItemRarity.LEGENDARY, 2, 2, 1,
			Palette.DANGER, Palette.WARNING, 24, 8, 6),
		_make_item_consumable("med-injector", "治疗针", "治疗针",
			"战区常见的单次应急治疗剂，可快速恢复生命。",
			ItemRarity.COMMON, 1, 2, 3,
			Palette.MINIMAP_MARKER, Palette.FRAME, 28, 0, 0, false),
		_make_item_consumable("field-kit", "战地急救包", "急救包",
			"占位更大，但能一次性恢复更多生命。",
			ItemRarity.UNCOMMON, 2, 2, 1,
			Palette.FRAME, Palette.MINIMAP_MARKER, 56, 0, 0, false),
		_make_item_consumable("shock-charge", "震爆罐", "震爆",
			"以自身为中心释放高压震爆，适合清开近身敌人。",
			ItemRarity.RARE, 2, 1, 2,
			Palette.WARNING, Palette.DANGER, 0, 46, 132, false),
		_make_item_consumable("dash-cell", "机动电池", "机动",
			"重置冲刺冷却并立即补一段机动脉冲。",
			ItemRarity.RARE, 1, 1, 2,
			Palette.DASH, Palette.ACCENT_SOFT, 0, 0, 0, true),
	]
	for item in item_catalog:
		item_by_id[item.id] = item

func _make_item(p_id: String, p_label: String, p_short: String,
		p_desc: String, p_cat: int, p_rarity: int, p_w: int, p_h: int,
		p_stack: int, p_tint: Color, p_accent: Color,
		p_salv: int, p_alloy: int, p_research: int) -> ItemDef:
	var item := ItemDef.new()
	item.id = p_id
	item.label = p_label
	item.short_label = p_short
	item.description = p_desc
	item.category = p_cat
	item.rarity = p_rarity
	item.width = p_w
	item.height = p_h
	item.max_stack = p_stack
	item.tint = p_tint
	item.accent = p_accent
	item.recovered_salvage = p_salv
	item.recovered_alloy = p_alloy
	item.recovered_research = p_research
	return item

func _make_item_consumable(p_id: String, p_label: String, p_short: String,
		p_desc: String, p_rarity: int, p_w: int, p_h: int,
		p_stack: int, p_tint: Color, p_accent: Color,
		p_heals: int, p_expl_dmg: int, p_expl_rad: int,
		p_refresh_dash: bool) -> ItemDef:
	var item := _make_item(p_id, p_label, p_short, p_desc,
		ItemCategory.CONSUMABLE, p_rarity, p_w, p_h, p_stack,
		p_tint, p_accent, 0, 0, 0)
	item.use_heals = p_heals
	item.use_explosion_damage = p_expl_dmg
	item.use_explosion_radius = p_expl_rad
	item.use_refresh_dash = p_refresh_dash
	return item

func build_wave_orders(wave: int) -> Array[SpawnOrder]:
	var orders: Array[SpawnOrder] = []
	if wave >= 5:
		orders.append(SpawnOrder.new(HostileType.BOSS, 0.35))
		return orders

	var melee_count := 2 + wave
	var ranged_count := (1 + (wave - 2) / 2) if wave >= 2 else 0
	var charger_count := (1 + (wave - 3) / 2) if wave >= 3 else 0

	for i in melee_count:
		orders.append(SpawnOrder.new(HostileType.MELEE, 0.28 + randf() * 0.18))
	for i in ranged_count:
		orders.append(SpawnOrder.new(HostileType.RANGED, 0.28 + randf() * 0.18))
	for i in charger_count:
		orders.append(SpawnOrder.new(HostileType.CHARGER, 0.28 + randf() * 0.18))
	if wave >= 4 and wave % 2 == 0:
		orders.append(SpawnOrder.new(HostileType.RANGED, 0.28 + randf() * 0.18))

	orders.shuffle()
	return orders

func build_wave_hint(wave: int) -> String:
	if wave >= 5:
		return "区域主核正在接管战场"
	if wave < 2:
		return "追猎体开始从外围压近"
	if wave < 3:
		return "射击体加入火力线"
	return "冲锋体开始压迫位移节奏"

func get_item_def(item_id: String) -> ItemDef:
	return item_by_id.get(item_id, null)

func get_weapon_def(weapon_type: int) -> WeaponDef:
	match weapon_type:
		WeaponType.MACHINE_GUN: return weapon_by_slot.get(1, null)
		WeaponType.GRENADE: return weapon_by_slot.get(2, null)
		WeaponType.SNIPER: return weapon_by_slot.get(3, null)
	return null
