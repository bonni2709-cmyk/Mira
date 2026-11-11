extends Node

# ============================================================================
# Менеджер сюжетных записок (Модуль 3)
# Хранит лор и показывает записки при событиях игры.
# ============================================================================

var notes_shown: Array = []

var lore_db: Dictionary = {
	"welcome": {
		"title": "Добро пожаловать в бункер",
		"text": "Ты заперт в подземном бункере 10×10. Зомби снаружи. Копай, строй, выживай.",
	},
	"first_night": {
		"title": "Первая ночь",
		"text": "Ночью зомби становятся активнее и пытаются пробиться к тебе. Укрепи стены!",
	},
	"turret_built": {
		"title": "Турель установлена",
		"text": "Автоматическая турель будет стрелять по ближайшим зомби. Не забудь о патронах.",
	},
	"low_hp": {
		"title": "Ранение",
		"text": "Твоё здоровье на исходе. Найди аптечку или вернись в безопасную зону.",
	},
	"generator": {
		"title": "Генератор энергии",
		"text": "Генератор вырабатывает энергию со временем. Она понадобится для mejora оборудования.",
	},
}

func show_note(key: String, auto_close: float = 6.0) -> void:
	if key in notes_shown:
		return
	notes_shown.append(key)
	var data: Dictionary = lore_db.get(key, {"title": "Записка", "text": "..."})
	var note_scene: PackedScene = load("res://scenes/ui/lore_note.tscn")
	var note: Control = note_scene.instantiate()
	note.show(data.title, data.text, auto_close)
	get_tree().get_first_group("main").get_node("HUD").add_child(note)
