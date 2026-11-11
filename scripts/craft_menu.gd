extends CanvasLayer

# ============================================================================
# UI крафта и строительства (Модуль 2)
# ============================================================================

@onready var main: Node = get_node("/root/Main") or get_tree().get_first_group("main")
@onready var lbl_resources: Label = $Panel/Resources
@onready var lbl_daynight: Label = $Panel/DayNight

func _ready() -> void:
	$Panel/BtnWall.pressed.connect(_on_wall)
	$Panel/BtnTurret.pressed.connect(_on_turret)
	$Panel/BtnGenerator.pressed.connect(_on_generator)
	$Panel/BtnCancel.pressed.connect(_on_cancel)
	$Panel/BtnAmmo.pressed.connect(_on_ammo)
	$Panel/BtnHeal.pressed.connect(_on_heal)
	$Panel/BtnSave.pressed.connect(_on_save)
	$Panel/BtnLoad.pressed.connect(_on_load)
	$Panel/BtnClose.pressed.connect(_on_close)
	visible = false

func _process(delta: float) -> void:
	if not visible:
		return
	var res: Dictionary = main.get_resources()
	lbl_resources.text = "Земля: %d  Камень: %d  Патроны: %d  Монеты: %d" % [
		res.get("dirt", 0), res.get("stone", 0), res.get("ammo", 0), res.get("coins", 0)
	]
	lbl_daynight.text = "НОЧЬ" if main.is_night_time() else "ДЕНЬ"
	lbl_daynight.custom_colors.font_color = Color(1, 0.4, 0.4) if main.is_night_time() else Color(0.6, 1, 0.6)

func _on_wall() -> void:
	main.set_build_mode("wall")

func _on_turret() -> void:
	main.set_build_mode("turret")

func _on_generator() -> void:
	main.set_build_mode("generator")

func _on_cancel() -> void:
	main.set_build_mode("")

func _on_ammo() -> void:
	if main.craft_item("ammo"):
		pass

func _on_heal() -> void:
	if main.craft_item("heal"):
		pass

func _on_save() -> void:
	main.save_game()

func _on_load() -> void:
	main.load_game()

func _on_close() -> void:
	visible = false

func show_menu() -> void:
	visible = true
