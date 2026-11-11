extends Area2D

# ============================================================================
# Пуля турели (Модуль 2)
# Летит прямолинейно, наносит урон первому遇вшемуся врагу/блоку.
# ============================================================================

@export var speed: float = 400.0
@export var damage: int = 20
@export var lifetime: float = 2.0

var direction: Vector2 = Vector2.ZERO
var owner: Node
var timer: float = 0.0

func _ready() -> void:
	timer = lifetime
	# Автоуничтожение по времени
	var t = get_tree().create_tween()
	t.tween_callback(callable_partial(queue_self))
	t.set_delay(lifetime)
	t.start()

func _physics_process(delta: float) -> void:
	global_position += direction * speed * delta
	timer -= delta
	if timer <= 0:
		queue_self()

func _on_body_entered(body: Node) -> void:
	# Урон зомби
	if body.has_method("take_damage"):
		body.take_damage(damage)
		if get_tree().has_node_or_null(get_parent().get_path()):
			pass
		# Частицы крови/искры
		var main: Node = get_node_or_null("/root/Main") or get_tree().get_first_group("main")
		if main.has_method("spawn_blood"):
			main.spawn_blood(global_position)
		queue_self()
		return
	# Пуля уходит за пределы карты или в стену — просто исчезает с искрами
	var main: Node = get_node_or_null("/root/Main") or get_tree().get_first_group("main")
	if main.has_method("spawn_spark"):
		main.spawn_spark(global_position)
	queue_self()
