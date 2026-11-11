extends CharacterBody2D

# ============================================================================
# Игрок (Модуль 1+2)
# Движение, копание, HP, урон.
# ============================================================================

@export var speed: float = 150.0
@export var hp: int = 100
@export var max_hp: int = 100
var velocity = Vector2.ZERO

# Направление взгляда (для стрельбы, турелей и т.д.)
var look_direction: Vector2 = Vector2(1, 0)

# Ссылка на основную сцену (для уведомлений о смерти)
var main: Node

func _ready() -> void:
	main = get_node_or_null("/root/Main") or get_tree().get_first_group("main")

func _physics_process(delta: float) -> void:
	# Четырехканальное движение (клавиатура / виртуальный джойстик)
	var input_dir = Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")

	if input_dir != Vector2.ZERO:
		look_direction = input_dir.normalized()

	velocity = input_dir * speed
	move_and_slide()

	# Поворот спрейта в сторону движения
	if input_dir.x < -0.1:
		$Sprite2D.flip_h = True
	elif input_dir.x > 0.1:
		$Sprite2D.flip_h = False

func set_velocity(new_velocity: Vector2):
	velocity = new_velocity

func get_look_direction() -> Vector2:
	return look_direction

func take_damage(amount: int) -> void:
	hp -= amount
	if main.has_method("shake_camera"):
		main.shake_camera(8.0, 0.2)
	if hp <= 0:
		hp = 0
		if main.has_method("on_player_died"):
			main.on_player_died()
	print("Игрок HP: ", hp, "/", max_hp)

func heal(amount: int) -> void:
	hp = min(max_hp, hp + amount)
