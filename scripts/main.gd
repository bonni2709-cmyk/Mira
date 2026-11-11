extends Node2D

# ============================================================================
# Pocket Bunker — Main scene script (Модуль 2: Зомби, Оборона, Экономика)
# ============================================================================

@onready var tilemap: TileMap = $World
@onready var player: CharacterBody2D = $Player
@onready var sun_light: DirectionalLight2D = $SunLight
@onready var canvas_modulate: CanvasModulate = $CanvasModulate
@onready var zombies_parent: Node2D = $Zombies
@onready var turrets_parent: Node2D = $TurretHolder
@onready var bullets_parent: Node2D = $Bullets
@onready var camera: Camera2D = $Player/Camera2D

# --- Константы сетки ---
const CELL_SIZE: int = 16
const GRID_SIZE: int = 10

const HARDNESS: Dictionary = {
	"floor": 0,
	"dirt": 3,
	"stone": 6,
	"wall": 9999,
}

# Стоимость постройки (в ресурсах)
const BUILD_COST: Dictionary = {
	"wall": {"stone": 5},
	"turret": {"stone": 10, "ammo": 5},
	"generator": {"stone": 15, "dirt": 10},
}

# --- Состояние блоков: "x,y" -> {type, hardness} ---
var block_states: Dictionary = {}

# --- Ресурсы и экономика ---
var resources: Dictionary = {
	"dirt": 20,
	"stone": 20,
	"energy": 0,
	"ammo": 10,
	"coins": 50,
}

# --- День / ночь ---
var is_night: bool = false
var cycle_timer: float = 0.0
var day_duration: float = 45.0
var night_duration: float = 25.0
var night_spawn_interval: float = 3.0
var night_spawn_timer: float = 0.0
var max_zombies: int = 8

# --- Hit-stop ---
var hit_stopped: bool = false
var hit_stop_timer: float = 0.0

# --- Сцены ---
var zombie_scene: PackedScene = load("res://scenes/zombie.tscn")
var turret_scene: PackedScene = load("res://scenes/turret.tscn")
var bullet_scene: PackedScene = load("res://scenes/bullet.tscn")

# --- Генераторы (энергия со временем) ---
var generators: Array = []

# --- Сохранения ---
var save_file: String = "user://savegame.json"

func _ready() -> void:
	_randomize_seed()
	_generate_bunker()
	player.global_position = Vector2(96, 96)
	# Применяем стартовое освещение (день)
	_apply_lighting(is_night)
	print("Pocket Bunker готов. Сетка ", GRID_SIZE, "x", GRID_SIZE)
	print("Ресурсы: ", resources)

# ============================================================================
# Генерация бункера
# ============================================================================
func _generate_bunker() -> void:
	for x in range(GRID_SIZE):
		for y in range(GRID_SIZE):
			var cell_pos: Vector2i = Vector2i(x, y)
			var type_name: String = _pick_biome(x, y)
			_set_cell(cell_pos, type_name)

func _pick_biome(x: int, y: int) -> String:
	if x == 0 or x == GRID_SIZE - 1 or y == 0 or y == GRID_SIZE - 1:
		return "wall"
	var r: float = randf()
	if r < 0.20:
		return "stone"
	if r < 0.60:
		return "dirt"
	return "floor"

func _source_id(type_name: String) -> int:
	match type_name:
		"floor": return 0
		"dirt": return 1
		"stone": return 2
		"wall": return 3
		_: return -1

func _set_cell(cell_pos: Vector2i, type_name: String) -> void:
	var key: String = _key(cell_pos)
	tilemap.set_cell(cell_pos, _source_id(type_name))
	block_states[key] = {"type": type_name, "hardness": HARDNESS[type_name]}

func _key(cell_pos: Vector2i) -> String:
	return str(cell_pos.x) + "," + str(cell_pos.y)

# ============================================================================
# Вспомогательные преобразования координат
# ============================================================================
func world_to_map(world_pos: Vector2) -> Vector2i:
	return tilemap.local_to_map(tilemap.to_local(world_pos))

func map_to_world(cell_pos: Vector2i) -> Vector2:
	return tilemap.to_global(tilemap.map_to_local(cell_pos)) + Vector2(CELL_SIZE / 2, CELL_SIZE / 2)

# ============================================================================
# День / Ночь
# ============================================================================
func _process(delta: float) -> void:
	if hit_stopped:
		_process_hit_stop(delta)
		return

	_process_day_night(delta)
	_process_generators(delta)
	_process_zombie_spawn(delta)
	_process_input(delta)

func _process_generators(delta: float) -> void:
	if generators.is_empty():
		return
	var energy_gain: float = generators.size() * 0.5 * delta
	resources["energy"] += energy_gain

func _process_day_night(delta: float) -> void:
	cycle_timer += delta
	var phase: float
	if is_night:
		if cycle_timer >= night_duration:
			is_night = false
			cycle_timer = 0
			_apply_lighting(false)
			print("Наступил ДЕНЬ")
	else:
		if cycle_timer >= day_duration:
			is_night = true
			cycle_timer = 0
			_apply_lighting(true)
			print("Наступила НОЧЬ — зомби выходят!")

	# Плавное изменение освещения в зависимости от фазы
	_apply_lighting(is_night)

func _apply_lighting(night: bool) -> void:
	if night:
		sun_light.energy = 0.04
		sun_light.color = Color(0.25, 0.2, 0.35)
		canvas_modulate.color = Color(0.10, 0.10, 0.16)
	else:
		sun_light.energy = 0.14
		sun_light.color = Color(0.55, 0.58, 0.65)
		canvas_modulate.color = Color(0.22, 0.22, 0.28)

func _process_zombie_spawn(delta: float) -> void:
	if not is_night:
		return
	var zombie_count: int = zombies_parent.get_child_count()
	if zombie_count >= max_zombies:
		return
	night_spawn_timer += delta
	if night_spawn_timer >= night_spawn_interval:
		night_spawn_timer = 0
		_spawn_zombie()

func _spawn_zombie() -> void:
	var zombie: Node2D = zombie_scene.instantiate()
	# Спавн у случайного края карты
	var side: int = randi() % 4
	var pos: Vector2
	match side:
		0: pos = Vector2(0, randf_range(0, GRID_SIZE * CELL_SIZE))        # лево
		1: pos = Vector2(GRID_SIZE * CELL_SIZE, randf_range(0, GRID_SIZE * CELL_SIZE))  # право
		2: pos = Vector2(randf_range(0, GRID_SIZE * CELL_SIZE), 0)        # верх
		3: pos = Vector2(randf_range(0, GRID_SIZE * CELL_SIZE), GRID_SIZE * CELL_SIZE)  # низ
	_: pos = Vector2(0, 0)
	zombie.global_position = pos
	zombie.target = player
	zombie.main = self
	zombies_parent.add_child(zombie)

# ============================================================================
# Ввод (копание, строительство)
# ============================================================================
func _process_input(delta: float) -> void:
	if hit_stopped:
		return
	if Input.is_action_just_pressed("ui_select"):
		var mouse_pos: Vector2 = get_global_mouse_position()
		_mine_at(mouse_pos)

func _mine_at(screen_pos: Vector2) -> void:
	var cell_pos: Vector2i = world_to_map(screen_pos)
	var key: String = _key(cell_pos)
	if not block_states.has(key):
		return
	var state: Dictionary = block_states[key]
	var type_name: String = state.type
	if type_name == "wall" or type_name == "floor":
		return
	state.hardness -= 1
	spawn_dust(map_to_world(cell_pos))
	if state.hardness <= 0:
		tilemap.erase_cell(cell_pos)
		block_states.erase(key)
		if resources.has(type_name):
			resources[type_name] += 1
		print("Разрушен ", type_name, ". Ресурсы: ", resources)

# --- Постройка по правому клику (режим строительства выбирается из UI) ---
var build_mode: String = ""  # "wall", "turret", "generator" или ""

func set_build_mode(mode: String) -> void:
	build_mode = mode
	print("Режим строительства: ", mode if mode != "" else "отменён")

func _place_building(cell_pos: Vector2i, b_type: String) -> bool:
	var key: String = _key(cell_pos)
	if block_states.has(key):
		return false  # клетка занята
	var cost: Dictionary = BUILD_COST.get(b_type, {})
	for res in cost.keys():
		if resources.get(res, 0) < cost[res]:
			print("Недостаточно ", res, " для постройки ", b_type)
			return false
	# Списываем ресурсы
	for res in cost.keys():
		resources[res] -= cost[res]
	# Ставим блок
	if b_type == "wall":
		_set_cell(cell_pos, "wall")
	elif b_type == "turret":
		_set_cell(cell_pos, "floor")  # под подставкой — пол
		var turret: Node2D = turret_scene.instantiate()
		turret.global_position = map_to_world(cell_pos)
		turret.main = self
		turrets_parent.add_child(turret)
		spawn_dust(map_to_world(cell_pos))
	elif b_type == "generator":
		_set_cell(cell_pos, "floor")
		var gen: Dictionary = {"pos": cell_pos, "key": _key(cell_pos)}
		generators.append(gen)
		spawn_dust(map_to_world(cell_pos))
	print("Построено: ", b_type)
	return true

# ============================================================================
# Экономика / крафт
# ============================================================================
func get_resource(type_name: String) -> int:
	return resources.get(type_name, 0)

func add_resource(type_name: String, amount: int) -> void:
	if not resources.has(type_name):
		resources[type_name] = 0
	resources[type_name] += amount

func consume_ammo(amount: int) -> bool:
	if resources.get("ammo", 0) >= amount:
		resources["ammo"] -= amount
		return true
	return false

func craft_item(item_type: String) -> bool:
	# Рецепты крафта
	var recipes: Dictionary = {
		"ammo": {"stone": 5, "coins": 10, "give": {"ammo": 20}},
		"heal": {"stone": 3, "coins": 15, "give": {"hp": 30}},
		"pickaxe_upgrade": {"stone": 10, "coins": 30},  # апгрейд кирки (уменьшает прочность блоков)
	}
	var recipe: Dictionary = recipes.get(item_type, {})
	if recipe.empty():
		return false
	# Проверяем ресурсы
	for res in recipe.keys():
		if res == "give":
			continue
		if resources.get(res, 0) < recipe[res]:
			print("Недостаточно ресурсов для крафта ", item_type)
			return false
	# Списываем и выдаем
	for res in recipe.keys():
		if res == "give":
			continue
		resources[res] -= recipe[res]
	for res, amt in recipe.get("give", {}).items():
		if res == "hp":
			if player.has_method("heal"):
				player.heal(amt)
		else:
			add_resource(res, amt)
	print("Крафт: ", item_type, " выполнено. Ресурсы: ", resources)
	return true

# ============================================================================
# Методы для зомби / турелей / пуль
# ============================================================================
func is_cell_diggable(cell_pos: Vector2i) -> bool:
	var key: String = _key(cell_pos)
	if not block_states.has(key):
		return false
	var t: String = block_states[key].type
	return t == "dirt" or t == "stone"

func damage_block(cell_pos: Vector2i, amount: int) -> void:
	var key: String = _key(cell_pos)
	if not block_states.has(key):
		return
	block_states[key].hardness -= amount
	if block_states[key].hardness <= 0:
		var t: String = block_states[key].type
		tilemap.erase_cell(cell_pos)
		block_states.erase(key)
		if resources.has(t):
			resources[t] += 1

func spawn_blood(pos: Vector2) -> void:
	_spawn_particle("blood", pos, Color(1, 0.2, 0.2))

func spawn_dust(pos: Vector2) -> void:
	_spawn_particle("dust", pos, Color(0.8, 0.8, 0.8))

func spawn_spark(pos: Vector2) -> void:
	_spawn_particle("spark", pos, Color(1, 0.6, 0.2))

func _spawn_particle(kind: String, pos: Vector2, tint: Color) -> void:
	var tex_path: String = "res://assets/sprites/particles/" + kind + ".png"
	var tex: Texture2D = load(tex_path)
	var p: Sprite2D = Sprite2D.new()
	p.texture = tex
	p.position = tilemap.to_local(pos) + Vector2(4, 4)
	p.modulate = tint
	add_child(p)
	var t: Tween = get_tree().create_tween()
	t.tween_property(p, "modulate:a", 0.0, 0.3)
	t.tween_property(p, "position", p.position + Vector2(randf_range(-10, 10), randf_range(-16, -4)), 0.3)
	t.tween_callback(callable_partial(_queue_free_node, p))
	t.start()

func _queue_free_node(node: Node) -> void:
	node.queue_free()

# ============================================================================
# Эффекты (game juice)
# ============================================================================
func shake_camera(strength: float = 10.0, duration: float = 0.2) -> void:
	var offset: Vector2 = camera.position
	var t: Tween = get_tree().create_tween()
	t.tween_property(camera, "position", offset + Vector2(randf_range(-strength, strength), randf_range(-strength, strength)), 0.05)
	t.tween_property(camera, "position", offset - Vector2(randf_range(-strength, strength), randf_range(-strength, strength)), 0.05)
	t.tween_property(camera, "position", offset, 0.1)
	t.start()

func trigger_hit_stop(duration: float = 0.05) -> void:
	hit_stopped = true
	hit_stop_timer = duration
	Engine.time_scale = 0.0

func _process_hit_stop(delta: float) -> void:
	if hit_stopped:
		hit_stop_timer -= delta
		if hit_stop_timer <= 0:
			hit_stopped = false
			Engine.time_scale = 1.0

# ============================================================================
# События смерти
# ============================================================================
func on_zombie_died() -> void:
	# Начисляем монеты за убитого зомби
	add_resource("coins", 5)

func on_player_died() -> void:
	print("ИГРОК ПОГИБ")
	# Экран смерти будет в Модуле 3 (UI)
	# Пока что просто пауза
	get_tree().paused = true

# ============================================================================
# Геттеры для UI
# ============================================================================
func get_resources() -> Dictionary:
	return resources.duplicate()

func is_night_time() -> bool:
	return is_night

func get_build_mode() -> String:
	return build_mode

# ============================================================================
# Ввод меню крафта
# ============================================================================
func _input(event: InputEvent) -> void:
	if hit_stopped:
		return
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		_mine_at(event.global_position)
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_RIGHT and event.pressed and build_mode != "":
		var cell_pos: Vector2i = world_to_map(event.global_position)
		if _place_building(cell_pos, build_mode):
			build_mode = ""
	if event is InputEventKey and event.pressed and event.keycode == KEY_C:
		var menu: CanvasLayer = $CraftMenu
		menu.show_menu()

# ============================================================================
# Сохранение / загрузка (JSON)
# ============================================================================
func save_game() -> void:
	var save_data: Dictionary = {
		"resources": resources,
		"player_pos": [player.global_position.x, player.global_position.y],
		"player_hp": player.hp,
		"is_night": is_night,
		"cycle_timer": cycle_timer,
		"blocks": block_states,
	}
	var file: FileAccess = FileAccess.open(save_file, FileAccess.WRITE)
	file.store_string(JSON.stringify(save_data, "\t"))
	file.close()
	print("Игра сохранена в ", save_file)

func load_game() -> void:
	if not FileAccess.file_exists(save_file):
		print("Сохранение не найдено")
		return
	var file: FileAccess = FileAccess.open(save_file, FileAccess.READ)
	var data: String = file.get_as_text()
	file.close()
	var save_data: Dictionary = JSON.parse(data).result
	if not save_data:
		return
	resources = save_data.get("resources", resources)
	if save_data.has("player_pos"):
		var pos: Array = save_data["player_pos"]
		player.global_position = Vector2(pos[0], pos[1])
	if save_data.has("player_hp") and player.has_method("hp"):
		player.hp = save_data["player_hp"]
	is_night = save_data.get("is_night", false)
	cycle_timer = save_data.get("cycle_timer", 0.0)
	_apply_lighting(is_night)
	# Восстановление блоков (упрощённо — только типы, без хитростей)
	block_states = save_data.get("blocks", {})
	tilemap.clear()
	for key in block_states.keys():
		var parts: Array = key.split(",")
		var cell_pos: Vector2i = Vector2i(int(parts[0]), int(parts[1]))
		var state: Dictionary = block_states[key]
		_set_cell(cell_pos, state.type)
	print("Игра загружена из ", save_file)
