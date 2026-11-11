extends Node2D

# ============================================================================
# Турель (Модуль 2)
# Ищет ближайшего зомби в радиусе, поворачивается и стреляет патронами.
# ============================================================================

@export var range: float = 220.0
@export var fire_rate: float = 0.9
@export var damage: int = 20
@export var bullet_speed: float = 380.0
@export var ammo_cost: int = 1

var target: Node2D
var fire_timer: float = 0.0
var main: Node
var ammo: int = 0  # будет заправляться из экономики

func _ready() -> void:
	main = get_node_or_null("/root/Main") or get_tree().get_first_group("main")
	$DetectionArea/CollisionShape2D.shape.radius = range
	# Подключаем сигналы области обнаружения
	$DetectionArea.body_entered.connect(_on_body_entered)
	$DetectionArea.body_exited.connect(_on_body_exited)

func _process(delta: float) -> void:
	if fire_timer > 0:
		fire_timer -= delta
	# Поиск цели, если нет текущей или она мертва/ушла
	if not target or not is_instance_valid(target) or target.global_position.distance_to(global_position) > range:
		target = _find_target()
	if target:
		_look_at_target()
		if fire_timer <= 0:
			_fire()
			fire_timer = fire_rate

func _find_target() -> Node:
	var bodies: Array = $DetectionArea.get_overlapping_bodies()
	var nearest: Node = null
	var best_dist: float = range
	for b in bodies:
		if b.is_in_group("zombie") and b != self:
			var d = global_position.distance_to(b.global_position)
			if d < best_dist:
				best_dist = d
				nearest = b
	return nearest

func _look_at_target() -> void:
	if not target:
		return
	var dir = (target.global_position - global_position).normalized()
	var angle = dir.angle()
	var pivot: Node2D = $HeadPivot
	pivot.rotation = angle
	# Поворот спрейта головы
	if $HeadPivot/Head:
		$HeadPivot/Head.flip_h = dir.x < 0

func _fire() -> void:
	if not target:
		return
	# Тратим патроны из экономики
	if main.has_method("consume_ammo"):
		if not main.consume_ammo(ammo_cost):
			return  # нет патронов
	var dir = (target.global_position - global_position).normalized()
	var bullet_scene: PackedScene = load("res://scenes/bullet.tscn")
	var bullet: Area2D = bullet_scene.instantiate()
	bullet.global_position = global_position + dir * 24
	bullet.direction = dir
	bullet.damage = damage
	bullet.speed = bullet_speed
	bullet.owner = self
	# Добавляем в основную сцену
	var main_node: Node = get_node_or_null("/root/Main") or get_tree().get_first_group("main")
	if main_node.has_node("Bullets"):
		main_node.get_node("Bullets").add_child(bullet)
	# Вспышка
	var flash: ColorRect = $HeadPivot/Head/MuzzleFlash
	flash.modulate = Color(1, 1, 1, 0.9)
	flash.visible = true
	var t = get_tree().create_tween()
	t.tween_property(flash, "modulate:a", 0.0, 0.1)
	t.tween_callback(callable_partial(_hide_flash, flash))
	t.start()
	# Тряска камерыweak
	if main_node.has_method("shake_camera"):
		main_node.shake_camera(3.0, 0.08)

func _hide_flash(flash: ColorRect) -> void:
	flash.visible = false

func _on_body_entered(body: Node) -> void:
	if body.is_in_group("zombie"):
		target = body

func _on_body_exited(body: Node) -> void:
	if body == target:
		target = null
