extends Control

# ============================================================================
# Сюжетная записка с лором (Модуль 3)
# Всплывающее окно с текстом, закрывается по кнопке или по таймеру.
# ============================================================================

@onready var title_label: Label = $VBoxContainer/TitleLabel
@onready var text_label: Label = $VBoxContainer/TextLabel
@onready var close_btn: Button = $VBoxContainer/CloseBtn

var auto_close_time: float = 0.0

func _ready() -> void:
	close_btn.pressed.connect(_on_close)
	if auto_close_time > 0:
		var t = get_tree().create_tween()
		t.tween_callback(callable_partial(_on_close))
		t.set_delay(auto_close_time)
		t.start()

func show(title: String, text: String, auto_close: float = 0.0) -> void:
	title_label.text = title
	text_label.text = text
	auto_close_time = auto_close
	visible = true
	modulate = Color(1, 1, 1, 0)
	var t = get_tree().create_tween()
	t.tween_property(self, "modulate:a", 1.0, 0.4)
	t.start()

func _on_close() -> void:
	var t = get_tree().create_tween()
	t.tween_property(self, "modulate:a", 0.0, 0.3)
	t.tween_callback(callable_partial(queue_self))
	t.start()
