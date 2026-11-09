#!/usr/bin/env python3
"""
Генератор иконки Trash-PC Tycoon 3D: ржавый системный блок в картонной коробке.

Рисуем в честном разрешении 64x64 (крупный пиксель, никакой антиалиасинга),
затем растягиваем NEAREST до нужных размеров:
  - icon.png                              512x512  (иконка проекта Godot)
  - android_icons/icon_192.png            192x192  (launcher icon)
  - android_icons/adaptive_fg_432.png     432x432  (adaptive foreground: блок в коробке)
  - android_icons/adaptive_bg_432.png     432x432  (adaptive background: грязная стена гаража)

Запуск:  python3 Tools/generate_icon.py
Зависимость: Pillow (pip install pillow). Скрипт детерминированный (seed фиксирован).
"""

import random
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
BASE = 64

# Грязная гаражная палитра. Неона нет.
WALL = (58, 48, 38)
WALL_DARK = (44, 36, 28)
CARDBOARD = (146, 104, 54)
CARDBOARD_LIGHT = (172, 128, 70)
CARDBOARD_DARK = (110, 76, 38)
TAPE = (196, 172, 96)
CASE_FRONT = (96, 98, 94)
CASE_SIDE = (72, 74, 70)
CASE_TOP = (116, 118, 110)
DARK = (30, 28, 26)
RUST = (128, 74, 36)
RUST_DARK = (94, 52, 26)
FAN = (40, 40, 38)
BUTTON = (150, 52, 34)
SLOT = (52, 52, 50)


def speckles(draw: ImageDraw.ImageDraw, box, colors, count, rng):
    x0, y0, x1, y1 = box
    for _ in range(count):
        x = rng.randint(x0, x1)
        y = rng.randint(y0, y1)
        draw.point((x, y), fill=rng.choice(colors))


def draw_scene(size_px: int, with_background: bool) -> Image.Image:
    rng = random.Random(2709)
    img = Image.new("RGBA", (BASE, BASE), (0, 0, 0, 0) if not with_background else WALL + (255,))
    d = ImageDraw.Draw(img)

    if with_background:
        # Стена гаража с потёками и пятнами.
        for y in range(0, BASE, 8):
            d.line([(0, y), (BASE, y)], fill=WALL_DARK)
        speckles(d, (0, 0, 63, 63), [WALL_DARK, (66, 55, 42), (50, 41, 31)], 260, rng)

    # --- Картонная коробка (изометрия: перед + правый бок + верх) ---
    # Передняя грань
    d.polygon([(8, 34), (38, 34), (38, 58), (8, 58)], fill=CARDBOARD)
    # Правая грань
    d.polygon([(38, 34), (52, 26), (52, 50), (38, 58)], fill=CARDBOARD_DARK)
    # Верх (открытые створки)
    d.polygon([(8, 34), (22, 26), (52, 26), (38, 34)], fill=CARDBOARD_LIGHT)
    d.polygon([(8, 34), (2, 24), (16, 18), (22, 26)], fill=CARDBOARD_LIGHT)   # левая створка
    d.polygon([(38, 34), (46, 20), (58, 24), (52, 26)], fill=CARDBOARD_DARK)  # правая створка
    # Скотч
    d.polygon([(21, 34), (25, 34), (25, 58), (21, 58)], fill=TAPE)
    d.polygon([(21, 34), (29, 29), (33, 29), (25, 34)], fill=TAPE)
    speckles(d, (8, 34, 52, 58), [CARDBOARD_DARK, RUST], 60, rng)
    d.line([(8, 58), (38, 58), (52, 50)], fill=(70, 48, 24), width=1)

    # --- Ржавый системный блок, торчит из коробки ---
    # Передняя грань корпуса
    d.polygon([(18, 8), (36, 8), (36, 34), (18, 34)], fill=CASE_FRONT)
    # Правая грань
    d.polygon([(36, 8), (44, 3), (44, 27), (36, 34)], fill=CASE_SIDE)
    # Верх
    d.polygon([(18, 8), (26, 3), (44, 3), (36, 8)], fill=CASE_TOP)
    # Заглушки отсеков и слоты
    d.rectangle([20, 11, 34, 14], fill=SLOT)
    d.rectangle([20, 17, 34, 19], fill=SLOT)
    d.rectangle([20, 22, 28, 24], fill=SLOT)
    # Вентилятор (круг + крест)
    d.ellipse([29, 21, 35, 27], outline=FAN, fill=(58, 58, 56))
    d.point((32, 24), fill=DARK)
    d.line([(32, 21), (32, 27)], fill=FAN)
    d.line([(29, 24), (35, 24)], fill=FAN)
    # Кнопка питания
    d.rectangle([20, 27, 23, 30], fill=BUTTON)
    d.point((21, 28), fill=(220, 120, 90))
    # Ржавчина и потёки по корпусу
    speckles(d, (18, 8, 44, 34), [RUST, RUST_DARK, (104, 62, 30)], 90, rng)
    d.line([(22, 14), (22, 20)], fill=RUST_DARK)
    d.line([(41, 8), (41, 16)], fill=RUST_DARK)
    # Изолента на углу корпуса
    d.polygon([(18, 8), (23, 8), (18, 13)], fill=TAPE)

    img = img.resize((size_px, size_px), Image.NEAREST)
    return img


def main() -> None:
    (ROOT / "android_icons").mkdir(exist_ok=True)

    icon = draw_scene(512, with_background=True)
    icon.save(ROOT / "icon.png")

    icon.convert("RGB").resize((192, 192), Image.NEAREST).save(ROOT / "android_icons" / "icon_192.png")

    foreground = draw_scene(432, with_background=False)
    foreground.save(ROOT / "android_icons" / "adaptive_fg_432.png")

    background = draw_scene(432, with_background=True)
    # Для фона берём только стену: перекрываем центр ровным грязным тоном с крапинками.
    bg = Image.new("RGB", (432, 432), WALL)
    rng = random.Random(41)
    bd = ImageDraw.Draw(bg)
    scale = 432 // BASE
    for _ in range(900):
        x = rng.randrange(BASE)
        y = rng.randrange(BASE)
        color = rng.choice([WALL_DARK, (66, 55, 42), (50, 41, 31)])
        bd.rectangle([x * scale, y * scale, x * scale + scale - 1, y * scale + scale - 1], fill=color)
    bg.save(ROOT / "android_icons" / "adaptive_bg_432.png")

    print("Icon generated: icon.png, android_icons/icon_192.png, adaptive_fg_432.png, adaptive_bg_432.png")


if __name__ == "__main__":
    main()
