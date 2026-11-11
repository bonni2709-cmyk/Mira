extends CharacterBody2D

# ============================================================================
# Зомби (Модуль 2)
# Двигается к игроку, ломает стены на пути, наносит урон при касании.
# ============================================================================

@export var speed: float = 45.0
@export var hp: int = 30
@export var max_hp: int = 30
@export var damage: int = 15
@export var attack_range: float = 20.0
@export var attack_cooldown: float = 1.2
@export var drop_chance: float = 0.6

var target: Node2D
var attack_timer: float = 0.0
var knockback_dir: Vector2 = Vector2.ZERO
var knockback_force: float = 0.0
var is_dead: bool = false

# Ссылка на Main (для ломки блоков и уведомления о смерти)
var main: Node

func _ready() -> void:
	main = get_node_or_null("/root/Main") or get_tree().get_first_group("main")
	$HitFlash.modulate = Color(1, 1, 1, 0)
	max_hp = hp

func _physics_process(delta: float) -> void:
	if is_dead or not target:
		velocity = Vector2.ZERO
		move_and_slide()
		return

	# Нокаут после удара
	if knockback_force > 0:
		velocity = knockback_dir * knockback_force
		knockback_force *= 0.85
		if knockback_force < 5:
			knockback_force = 0
		move_and_slide()
		return

	var to_target = (target.global_position - global_position)
	var dist = to_target.length()

	if dist > attack_range:
		var dir = to_target.normalized()
		velocity = dir * speed
		# Поворот спрейта в сторону движения
		if $Sprite2D:
			$Sprite2D.flip_h = dir.x < 0
		move_and_slide()
	else:
		velocity = Vector2.ZERO
		if $Sprite2D:
			$Sprite2D.flip_h = to_target.x < 0
		# Атака
		attack_timer += delta
		if attack_timer >= attack_cooldown:
			attack_timer = 0
			_attack()

	# Попытка сломать стену на пути (если стоит перед зомби)
	_try_mine_ahead()

func _attack() -> void:
	# Урон игроку
	if target.has_method("take_damage"):
		target.take_damage(damage)
	# Всплывающая цифра урона
	if main.has_method("spawn_damage_number"):
		main.spawn_damage_number(target.global_position, damage, critical=False)
	# Частицы крови при ударе
	if main.has_method("spawn_blood"):
		main.spawn_blood(global_position)
	# Тряска камеры
	if main.has_method("shake_camera"):
		main.shake_camera(6.0, 0.15)

func _try_mine_ahead() -> void:
	# Если перед зомби блок — пробуем его сломать (грызет стену)
	if not main or not main.has_method("is_cell_diggable"):
		return
	var dir = $Sprite2D.flip_h and Vector2(-1, 0) or Vector2(1, 0)
	var ahead = global_position + dir * Vector2(18, 0)
	var cell_pos = main.world_to_map(ahead)
	if main.is_cell_diggable(cell_pos):
		main.damage_block(cell_pos, 1)
		if main.has_method("spawn_dust"):
			main.spawn_dust(main.map_to_world(cell_pos))

func take_damage(amount: int, dir: Vector2 = Vector2.ZERO) -> void:
	if is_dead:
		return
	hp -= amount
	if main.has_method("spawn_blood"):
		main.spawn_blood(global_position)
	if main.has_method("shake_camera"):
		main.shake_camera(4.0, 0.12)
	# Нокаут
	if dir.length() > 0:
		knockback_dir = dir.normalized()
		knockback_force = 120.0
	# Вспышка красного
	var t = get_tree().create_tween()
	t.tween_property($HitFlash, "modulate:a", 0.6, 0.05)
	t.tween_property($HitFlash, "modulate:a", 0.0, 0.15)
	t.start()
	if hp <= 0:
		die()

func die() -> void:
	is_dead = true
	$CollisionShape2D.disabled = true
	$Sprite2D.modulate = Color(0.4, 0.4, 0.4, 0.6)
	# Частиц крови
	if main.has_method("spawn_blood"):
		for i in range(6):
			main.spawn_blood(global_position + Vector2(randf_range(-12, 12), randf_range(-12, 12)))
	# Hit-stop
	if main.has_method("trigger_hit_stop"):
		main.trigger_hit_stop(0.05)
	# Дроп ресурсов
	if main.has_method("add_resource") and randf() < drop_chance:
		main.add_resource("stone", 1)
	if main.has_method("on_zombie_died"):
		main.on_zombie_died()
	# Удаляем через короткую задержку
	var t = get_tree().create_tween()
	t.tween_property($Sprite2D, "modulate:a", 0.0, 0.3)
	t.tween_callback(callable_partial(queue_self))
	t.start()

func queue_self() -> void:
	queue_free()
