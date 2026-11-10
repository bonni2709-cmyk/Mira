extends Node2D

# ============================================================================
# Pocket Bunker — Main scene script (Модуль 1: Ядро, Ассеты, Игрок, Копание)
# ============================================================================

@onready var tilemap: TileMap = $World
@onready var player: CharacterBody2D = $Player

# Размер клетки и сетки
const CELL_SIZE: int = 16
const GRID_SIZE: int = 10

# Прочность блоков (сколько ударов киркой нужно)
const HARDNESS: Dictionary = {
	"floor": 0,
	"dirt": 3,
	"stone": 6,
	"wall": 9999,  # неразрушимая стена бункера
}

# Карта состояний блоков: ключ "x,y" -> {type, hardness}
var block_states: Dictionary = {}

# Ресурсы игрока
var resources: Dictionary = {
	"dirt": 0,
	"stone": 0,
}

# Флаг hit-stop (заморозка игры при убийстве / критическом ударе)
var hit_stopped: bool = false
var hit_stop_timer: float = 0.0

# Ссылка на камеру (находится внутри игрока)
@onready var camera: Camera2D = $Player/Camera2D

func _ready() -> void:
	_randomize_seed()
	_generate_bunker()
	# Спавн игрока на стартовой клетке (внутри бункера)
	player.global_position = Vector2(96, 96)
	print("Pocket Bunker готов. Сетка ", GRID_SIZE, "x", GRID_SIZE)

# --------------------------------------------------------------------------
# Генерация бункера
# --------------------------------------------------------------------------
func _generate_bunker() -> void:
	for x in range(GRID_SIZE):
		for y in range(GRID_SIZE):
			var cell_pos: Vector2i = Vector2i(x, y)
			var type_name: String = _pick_biome(x, y)
			_set_cell(cell_pos, type_name)

func _pick_biome(x: int, y: int) -> String:
	# Границы карты — стены бункера (неразрушимые)
	if x == 0 or x == GRID_SIZE - 1 or y == 0 or y == GRID_SIZE - 1:
		return "wall"
	# Внутри бункера смесь пола, земли и камня
	var r: float = randf()
	if r < 0.25:
		return "stone"
	if r < 0.65:
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
	var key: String = str(cell_pos.x) + "," + str(cell_pos.y)
	tilemap.set_cell(cell_pos, _source_id(type_name))
	block_states[key] = {"type": type_name, "hardness": HARDNESS[type_name]}

# --------------------------------------------------------------------------
# Ввод — копание по клику / касанию
# --------------------------------------------------------------------------
func _input(event: InputEvent) -> void:
	if hit_stopped:
		return
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		_mine_at(event.global_position)

func _mine_at(screen_pos: Vector2) -> void:
	var world_pos: Vector2 = screen_pos
	var cell_pos: Vector2i = tilemap.local_to_map(tilemap.to_local(world_pos))
	var key: String = str(cell_pos.x) + "," + str(cell_pos.y)

	if not block_states.has(key):
		return

	var state: Dictionary = block_states[key]
	var type_name: String = state.type

	# Стены бункера и пол не копаются
	if type_name == "wall" or type_name == "floor":
		return

	# Наносим урон блоку
	state.hardness -= 1

	# Частицы пыли при каждом ударе
	_spawn_dust(world_pos)

	if state.hardness <= 0:
		# БLOCK разрушен
		tilemap.erase_cell(cell_pos)
		block_states.erase(key)
		if resources.has(type_name):
			resources[type_name] += 1
		else:
			resources[type_name] = 1
		print("Разрушен ", type_name, ". Ресурсы: ", resources)

# --------------------------------------------------------------------------
# Частицы (пыль от стен / земли)
# --------------------------------------------------------------------------
func _spawn_dust(world_pos: Vector2) -> void:
	var p: Sprite2D = Sprite2D.new()
	var tex: Texture2D = load("res://assets/sprites/particles/dust.png")
	p.texture = tex
	p.position = tilemap.to_local(world_pos) + Vector2(8, 8) - Vector2(4, 4)
	p.modulate = Color(1, 1, 1, 0.8)
	add_child(p)

	var t: Tween = get_tree().create_tween()
	t.tween_property(p, "modulate:a", 0.0, 0.25)
	t.tween_property(p, "position", p.position + Vector2(randf_range(-8, 8), randf_range(-16, -4)), 0.25)
	t.tween_callback(callable_partial(_queue_free_node, p))
	t.start()

func _queue_free_node(node: Node) -> void:
	node.queue_free()

# --------------------------------------------------------------------------
# Эффекты/game juice
# --------------------------------------------------------------------------
# Тряска камеры (вызывается извне — при взрывах, ударах)
func shake_camera(strength: float = 10.0, duration: float = 0.2) -> void:
	var t: Tween = get_tree().create_tween()
	var offset: Vector2 = camera.position
	t.tween_property(camera, "position", offset + Vector2(randf_range(-strength, strength), randf_range(-strength, strength)), 0.05)
	t.tween_property(camera, "position", offset - Vector2(randf_range(-strength, strength), randf_range(-strength, strength)), 0.05)
	t.tween_property(camera, "position", offset, 0.1)
	t.start()

# Hit-stop: заморозка на несколько секунд (в секундах)
func trigger_hit_stop(duration: float = 0.05) -> void:
	hit_stopped = true
	hit_stop_timer = duration
	Engine.time_scale = 0.0
	print("HIT-STOP ", duration, " сек")

func _process_hit_stop(delta: float) -> void:
	if hit_stopped:
		hit_stop_timer -= delta
		if hit_stop_timer <= 0:
			hit_stopped = false
			Engine.time_scale = 1.0

# --------------------------------------------------------------------------
# Геттеры (для UI / других модулей)
# --------------------------------------------------------------------------
func get_resource(type_name: String) -> int:
	return resources.get(type_name, 0)

func get_resources() -> Dictionary:
	return resources.duplicate()

func get_block_type(cell_pos: Vector2i) -> String:
	var key: String = str(cell_pos.x) + "," + str(cell_pos.y)
	if block_states.has(key):
		return block_states[key].type
	return ""

func is_cell_diggable(cell_pos: Vector2i) -> bool:
	var key: String = str(cell_pos.x) + "," + str(cell_pos.y)
	if not block_states.has(key):
		return false
	var t: String = block_states[key].type
	return t == "dirt" or t == "stone"
