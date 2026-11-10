extends CharacterBody2D

@export var speed: float = 150.0
var velocity = Vector2.ZERO

# Направление взгляда (для стрельбы, турелей и т.д.)
var look_direction: Vector2 = Vector2(1, 0)

func _physics_process(delta: float) -> void:
	# Четырехканальное движение (клавиатура / виртуальный джойстик)
	var input_dir = Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
	
	if input_dir != Vector2.ZERO:
		look_direction = input_dir.normalized()
	
	velocity = input_dir * speed
	move_and_slide()
	
	# Поворот спрайта в сторону движения (простой хинт для пиксель-арта)
	if input_dir.x < -0.1:
		$Sprite2D.flip_h = true
	elif input_dir.x > 0.1:
		$Sprite2D.flip_h = false

func set_velocity(new_velocity: Vector2):
	velocity = new_velocity

func get_look_direction() -> Vector2:
	return look_direction
