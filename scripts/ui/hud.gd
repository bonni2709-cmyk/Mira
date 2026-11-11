extends CanvasLayer

# ============================================================================
# Мобильный HUD (Модуль 3)
# Полоски HP/ресурсов, кнопки действия, экран смерти, всплывающие цифры урона.
# ============================================================================

@onready var main: Node = get_node("/root/Main") or get_tree().get_first_group("main")
@onready var hp_label: Label = $TopPanel/VBoxContainer/HPLabel
@onready var res_label: Label = $TopPanel/VBoxContainer/ResLabel
@onready var cycle_label: Label = $TopPanel/VBoxContainer/CycleLabel
@onready var dmg_container: Node2D = $DamageNumberContainer
@onready var death_screen: Control = $DeathScreen
@onready var btn_dig: Button = $BottomPanel/HBoxContainer/BtnDig
@onready var btn_shoot: Button = $BottomPanel/HBoxContainer/BtnShoot
@onready var btn_build: Button = $BottomPanel/HBoxContainer/BtnBuild
@onready var btn_restart: Button = $DeathScreen/VBoxContainer/BtnRestart
@onready var btn_menu: Button = $DeathScreen/VBoxContainer/BtnMenu

# Режим стрельбы игрока (открывается при нажатии кнопки стрельбы)
var shoot_mode: bool = false

func _ready() -> void:
	btn_dig.pressed.connect(_on_dig)
	btn_shoot.pressed.connect(_on_shoot_toggle)
	btn_build.pressed.connect(_on_build)
	btn_restart.pressed.connect(_on_restart)
	btn_menu.pressed.connect(_on_menu)
	death_screen.visible = false

func _process(delta: float) -> void:
	if death_screen.visible:
		return
	# Обновляем данные из Main
	var res: Dictionary = main.get_resources()
	hp_label.text = "❤ HP: %d/%d" % [main.get_node("Player").hp, main.get_node("Player").max_hp]
	res_label.text = "🟤 З:%d ⚪ К:%d 💥 П:%d 🪙 М:%d ⚡ E:%d" % [
		res.get("dirt", 0), res.get("stone", 0), res.get("ammo", 0),
		res.get("coins", 0), int(res.get("energy", 0))
	]
	cycle_label.text = "☽ НОЧЬ" if main.is_night_time() else "☀ ДЕНЬ"
	cycle_label.custom_colors.font_color = Color(1, 0.4, 0.4) if main.is_night_time() else Color(0.8, 1, 0.8)

	# Кнопка копания — активна только если под курсором роемый блок
	var mouse_pos = get_global_mouse_position()
	var cell_pos = main.world_to_map(mouse_pos)
	btn_dig.disabled = not main.is_cell_diggable(cell_pos)

	# Кнопка строительства — активна в режиме постройки
	btn_build.disabled = false
	btn_build.text = "🧱 СТРОИТЬ (%s)" % main.get_build_mode() if main.get_build_mode() != "" else "🧱 СТРОИТЬ"

	# Режим стрельбы — подсветка
	btn_shoot.modulate = Color(1, 1, 1, 0.7) if not shoot_mode else Color(1, 0.8, 0.4, 1)

func _on_dig() -> void:
	# Копание по центру экрана / позиции игрока (для мобилки)
	var player: Node2D = main.get_node("Player")
	main._mine_at(player.global_position)

func _on_shoot_toggle() -> void:
	shoot_mode = not shoot_mode

func _on_build() -> void:
	var mode: String = main.get_build_mode()
	if mode == "":
		main.set_build_mode("wall")
	else:
		main.set_build_mode("")

func _on_restart() -> void:
	get_tree().reload_current_scene()

func _on_menu() -> void:
	death_screen.visible = false
	get_tree().change_scene_to_file("res://scenes/main.tscn")

# Показать экран смерти
func show_death() -> void:
	death_screen.visible = true
	get_tree().paused = false

# Всплывающая цифра урона
func show_damage_number(position: Vector2, amount: int, critical: bool = false) -> void:
	var lbl: Label = Label.new()
	lbl.text = str(amount)
	lbl.custom_colors.font_color = Color(1, 0.3, 0.3) if critical else Color(1, 1, 1)
	lbl.custom_colors.font_outline_color = Color(0, 0, 0, 1)
	lbl.custom_colors.font_outline_size = Vector2(2, 2)
	lbl.position = position
	lbl.modulate = Color(1, 1, 1, 1)
	dmg_container.add_child(lbl)

	var t: Tween = get_tree().create_tween()
	t.tween_property(lbl, "position", position + Vector2(0, -40), 0.8)
	t.tween_property(lbl, "modulate:a", 0.0, 0.8)
	t.set_delay(0.2)
	t.tween_callback(callable_partial(lbl.queue_free))
	t.start()
