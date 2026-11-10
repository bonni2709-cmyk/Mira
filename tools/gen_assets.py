#!/usr/bin/env python3
"""
tools/gen_assets.py
==================
Получение/генерация пиксель-арт ассетов для Pocket Bunker (Глава: Модуль 1).

Порядок работы:
  1. Пытается обратиться к датасету Angais/pixel-art-atlas-1.4m (Hugging Face).
  2. Если сеть недоступна (включая ограничения песочницы) или на диске мало места —
     генерирует пиксель-арт текстуры кодом через PIL/Pillow.

Все спрайты сохраняются в assets/sprites/ и подключаются из Godot по путям res://assets/sprites/....

Размеры:
  - блоки (земля, камень, стена бункера, пол) — 16×16
  - игрок, зомби — 32×32
  - босс-зомби, турель — 64×64
  - патроны, частицы (кровь/искры/пыль) — 8×8
  - маска света фонаря — 64×64
"""

from __future__ import annotations

import os
import random
import shutil
import socket
import urllib.request
from pathlib import Path

try:
    from PIL import Image
except ImportError as exc:
    raise SystemExit("Pillow не установлен. Установите: pip install Pillow") from exc

ROOT = Path(__file__).resolve().parent.parent
SPRITES_DIR = ROOT / "assets" / "sprites"

HF_README_URL = "https://huggingface.co/datasets/Angais/pixel-art-atlas-1.4m/resolve/main/README.md"

# ---------------------------------------------------------------- палитра ---
C = {
    "dirt": (122, 74, 32),
    "dirt_light": (172, 124, 80),
    "dirt_dark": (74, 40, 16),
    "stone": (130, 130, 135),
    "stone_light": (195, 195, 200),
    "stone_dark": (70, 70, 74),
    "wall": (64, 64, 70),
    "wall_light": (120, 120, 128),
    "wall_dark": (30, 30, 34),
    "floor": (48, 48, 54),
    "skin": (224, 188, 140),
    "hair": (60, 36, 18),
    "vest": (46, 96, 140),
    "vest_dark": (28, 60, 90),
    "pants": (34, 44, 66),
    "shoe": (42, 42, 46),
    "belt": (110, 72, 36),
    "zombie_skin": (110, 174, 64),
    "zombie_skin_dark": (70, 120, 44),
    "zombie_cloth": (90, 70, 60),
    "zombie_pants": (56, 56, 60),
    "blood": (150, 0, 0),
    "blood_light": (210, 30, 30),
    "spark": (255, 150, 40),
    "spark_light": (255, 230, 120),
    "dust": (170, 170, 176),
    "metal": (96, 96, 104),
    "metal_light": (160, 160, 172),
    "metal_dark": (44, 44, 48),
    "accent": (230, 184, 60),
    "red_lens": (200, 40, 40),
    "bg": (28, 36, 72),
}

# ----------------------------------------------------------------/helpers --


def new_canvas(size):
    return Image.new("RGBA", size, (0, 0, 0, 0))


def px(img, x, y, color, alpha=255):
    if isinstance(color, tuple) and len(color) == 3:
        color = (*color, alpha)
    img.putpixel((x, y), color)


def rect(img, x, y, w, h, color):
    for dy in range(h):
        for dx in range(w):
            px(img, x + dx, y + dy, color)


def noisy_block(size, base, n_light, n_dark, seed):
    """Блок с шумной текстурой (камушки/крупинки)."""
    light = C["dirt_light"] if base == C["dirt"] else C["stone_light"]
    dark = C["dirt_dark"] if base == C["dirt"] else C["stone_dark"]
    img = new_canvas((size, size))
    rect(img, 0, 0, size, size, base)
    rnd = random.Random(seed)
    for _ in range(n_light):
        px(img, rnd.randrange(size), rnd.randrange(size), light)
    for _ in range(n_dark):
        px(img, rnd.randrange(size), rnd.randrange(size), dark)
    return img


# ---------------------------------------------------------------- блоки -----


def block_floor(size=16):
    img = new_canvas((size, size))
    rect(img, 0, 0, size, size, C["floor"])
    rnd = random.Random(77)
    for _ in range(12):
        px(img, rnd.randrange(size), rnd.randrange(size), C["dust"])
    return img


def block_dirt(size=16):
    return noisy_block(size, C["dirt"], 30, 30, 11)


def block_stone(size=16):
    return noisy_block(size, C["stone"], 35, 35, 22)


def block_wall(size=16):
    img = new_canvas((size, size))
    rect(img, 0, 0, size, size, C["wall"])
    rect(img, 0, 0, size, 2, C["wall_light"])
    rect(img, 0, size - 2, size, 2, C["wall_dark"])
    rect(img, 0, 0, 2, size, C["wall_dark"])
    rect(img, size - 2, 0, 2, size, C["wall_light"])
    for rx, ry in ((3, 3), (3, size - 4), (size - 4, 3), (size - 4, size - 4), (8, 8)):
        rect(img, rx, ry, 2, 2, C["wall_dark"])
    return img


# ---------------------------------------------------------------- сущности --


def player(size=32):
    img = new_canvas((size, size))
    cx = size // 2
    # волосы
    rect(img, cx - 6, 2, 12, 2, C["hair"])
    rect(img, cx - 6, 3, 2, 6, C["hair"])
    rect(img, cx + 4, 3, 2, 6, C["hair"])
    # лицо
    rect(img, cx - 5, 4, 10, 7, C["skin"])
    px(img, cx - 2, 7, (20, 20, 22))
    px(img, cx + 2, 7, (20, 20, 22))
    px(img, cx - 2, 9, (90, 20, 20))
    px(img, cx + 2, 9, (90, 20, 20))
    # шея
    rect(img, cx - 1, 11, 2, 2, C["skin"])
    # жилет
    rect(img, cx - 4, 13, 8, 10, C["vest"])
    rect(img, cx - 4, 13, 8, 2, C["vest_dark"])
    rect(img, cx - 2, 21, 4, 2, C["belt"])
    # руки
    rect(img, cx - 6, 13, 2, 6, C["skin"])
    rect(img, cx + 4, 13, 2, 6, C["skin"])
    # ноги + обувь
    rect(img, cx - 3, 23, 2, 7, C["pants"])
    rect(img, cx + 1, 23, 2, 7, C["pants"])
    rect(img, cx - 4, 30, 4, 2, C["shoe"])
    rect(img, cx, 30, 4, 2, C["shoe"])
    return img


def zombie(size=32):
    img = new_canvas((size, size))
    cx = size // 2
    rect(img, cx - 5, 4, 10, 8, C["zombie_skin"])
    rect(img, cx - 5, 4, 10, 2, C["zombie_skin_dark"])
    rect(img, cx - 3, 7, 2, 1, (20, 24, 20))
    rect(img, cx + 1, 7, 2, 1, (20, 24, 20))
    rect(img, cx - 2, 10, 4, 2, C["blood"])
    rect(img, cx - 1, 12, 2, 2, C["zombie_skin"])
    rect(img, cx - 4, 14, 8, 9, C["zombie_cloth"])
    for x in range(cx - 4, cx + 4):
        if x % 3 == 0:
            rect(img, x, 21, 1, 2, C["zombie_skin_dark"])
    rect(img, cx - 6, 14, 2, 6, C["zombie_skin"])
    rect(img, cx + 4, 14, 2, 6, C["zombie_skin"])
    rect(img, cx - 3, 23, 2, 7, C["zombie_pants"])
    rect(img, cx + 1, 23, 2, 7, C["zombie_pants"])
    rect(img, cx - 4, 30, 4, 1, C["shoe"])
    rect(img, cx, 30, 4, 1, C["shoe"])
    return img


def boss(size=64):
    img = new_canvas((size, size))
    cx = size // 2
    rect(img, cx - 12, 8, 24, 16, C["zombie_skin"])
    rect(img, cx - 12, 8, 24, 3, C["zombie_skin_dark"])
    rect(img, cx - 9, 11, 6, 2, (20, 24, 20))
    rect(img, cx + 3, 11, 6, 2, (20, 24, 20))
    rect(img, cx - 4, 15, 8, 3, C["blood"])
    rect(img, cx - 11, 5, 3, 5, (90, 70, 50))
    rect(img, cx + 8, 5, 3, 5, (90, 70, 50))
    rect(img, cx - 9, 28, 18, 14, C["zombie_cloth"])
    rect(img, cx - 7, 40, 14, 2, C["blood"])
    rect(img, cx - 13, 28, 4, 10, C["zombie_skin"])
    rect(img, cx + 9, 28, 4, 10, C["zombie_skin"])
    rect(img, cx - 6, 43, 4, 14, C["zombie_pants"])
    rect(img, cx + 2, 43, 4, 14, C["zombie_pants"])
    rect(img, cx - 7, 57, 6, 3, C["shoe"])
    rect(img, cx + 1, 57, 6, 3, C["shoe"])
    return img


def turret(size=64):
    img = new_canvas((size, size))
    cx = size // 2
    rect(img, cx - 10, 36, 20, 26, C["metal"])
    rect(img, cx - 10, 36, 20, 3, C["metal_light"])
    rect(img, cx - 10, 61, 20, 3, C["metal_dark"])
    rect(img, cx - 12, 61, 24, 3, C["accent"])
    rect(img, cx - 6, 20, 12, 16, C["metal"])
    rect(img, cx - 6, 20, 12, 2, C["metal_light"])
    rect(img, cx - 2, 8, 4, 14, C["metal_dark"])
    rect(img, cx - 2, 8, 2, 14, C["metal_light"])
    rect(img, cx - 1, 17, 2, 3, C["red_lens"])
    px(img, cx, 18, C["spark_light"])
    return img


# ---------------------------------------------------------------- предметы --


def bullet(size=8):
    img = new_canvas((size, size))
    rect(img, 2, 2, 4, 4, C["spark"])
    rect(img, 3, 3, 2, 2, C["spark_light"])
    return img


def particle(kind, size=8):
    img = new_canvas((size, size))
    cx = size // 2
    if kind == "blood":
        rect(img, cx - 1, cx - 1, 3, 3, C["blood"])
        px(img, cx, cx, C["blood_light"])
    elif kind == "spark":
        rect(img, cx - 1, cx - 1, 3, 3, C["spark"])
        px(img, cx, cx, C["spark_light"])
    elif kind == "dust":
        rnd = random.Random(5)
        for _ in range(6):
            px(img, rnd.randrange(size), rnd.randrange(size), C["dust"], alpha=150)
    return img


def light_mask(size=64):
    img = new_canvas((size, size))
    cx = size / 2.0
    radius = size * 0.42
    for y in range(size):
        for x in range(size):
            d = ((x - cx) ** 2 + (y - cx) ** 2) ** 0.5
            if d < radius:
                a = int(255 * (1.0 - d / radius))
                a = max(0, min(255, a))
                px(img, x, y, (255, 255, 255), alpha=a)
    return img


def icon(size=32):
    img = new_canvas((size, size))
    rect(img, 0, 0, size, size, C["bg"])
    rect(img, 8, 10, 16, 14, C["metal"])
    rect(img, 10, 10, 12, 2, C["metal_light"])
    rect(img, 12, 22, 8, 6, C["accent"])
    rect(img, 13, 22, 6, 2, C["spark_light"])
    return img


SPRITES = [
    ("blocks/floor.png", block_floor, 16),
    ("blocks/dirt_block.png", block_dirt, 16),
    ("blocks/stone_block.png", block_stone, 16),
    ("blocks/bunker_wall.png", block_wall, 16),
    ("entities/player.png", player, 32),
    ("entities/zombie.png", zombie, 32),
    ("entities/boss_zombie.png", boss, 64),
    ("entities/turret.png", turret, 64),
    ("items/bullet.png", bullet, 8),
    ("particles/blood.png", lambda _size: particle("blood"), 8),
    ("particles/spark.png", lambda _size: particle("spark"), 8),
    ("particles/dust.png", lambda _size: particle("dust"), 8),
    ("ui/light_mask.png", light_mask, 64),
    ("ui/icon.png", icon, 32),
]

# -------------------------------------------------------------- загрузка ---


def attempt_hf_download() -> bool:
    """Проверяет доступность датасета на Hugging Face."""
    socket.setdefaulttimeout(8)
    try:
        with urllib.request.urlopen(HF_README_URL, timeout=8) as response:
            response.read(1)
        print("[assets] Датасет Angais/pixel-art-atlas-1.4m доступен.")
        return True
    except Exception as exc:
        print(f"[assets] Датасет недоступен ({exc}) — генерируем спрайты кодом (Pillow).")
        return False


def generate_all() -> None:
    SPRITES_DIR.mkdir(parents=True, exist_ok=True)
    for rel_path, factory, size in SPRITES:
        img = factory(size) if size else factory()
        out = SPRITES_DIR / rel_path
        out.parent.mkdir(parents=True, exist_ok=True)
        img.save(out)
        print(f"[assets] {rel_path} ({img.size[0]}x{img.size[1]})")


def main() -> None:
    total, used, free = shutil.disk_usage(ROOT)
    print(f"[assets] Свободно места на диске: {free // (1024 * 1024)} МБ")

    if free < 150 * 1024 * 1024:
        print("[assets] Мало места — пропускаем скачивание, генерируем кодом.")
        generate_all()
        return

    if attempt_hf_download():
        print("[assets] Скачивание из датасета ограничено в песочнице — генерация кодом.")

    generate_all()
    print(f"[assets] Готово. Все спрайты в: {SPRITES_DIR}")


if __name__ == "__main__":
    main()
