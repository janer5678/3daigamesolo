"""Generates the textures for the Circulation level (Assets/Circulation/Textures), after the Renai Circulation video:
a pink "LOVE?" card in thin white letters, and a sheet of soft pink hiragana to float about.

    python Tools/make_circulation_textures.py
"""
import os

from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Circulation", "Textures")
FONTS = "C:/Windows/Fonts"


def font(names, size):
    for n in names:
        p = os.path.join(FONTS, n)
        if os.path.exists(p):
            return ImageFont.truetype(p, size, index=0) if n.endswith(".ttc") else ImageFont.truetype(p, size)
    return ImageFont.load_default()


def love():
    img = Image.new("RGB", (512, 256), (244, 184, 196))
    d = ImageDraw.Draw(img)
    f = font(["segoeuil.ttf", "segoeui.ttf", "arial.ttf"], 130)
    d.text((70, 40), "LOVE?", font=f, fill=(255, 255, 255))
    img.save(os.path.join(OUT, "love.png"))


def kana():
    img = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    f = font(["YuGothR.ttc", "msgothic.ttc"], 96)
    chars = "こいらぶすきせのねゆめかわうへ♡"
    for i, ch in enumerate(chars):
        x, y = (i % 4) * 128, (i // 4) * 128
        col = (236, 120, 150, 255) if i % 3 else (150, 170, 236, 255)
        d.text((x + 16, y + 6), ch, font=f, fill=col)
    img.save(os.path.join(OUT, "kana.png"))


def white():
    Image.new("RGBA", (8, 8), (255, 255, 255, 255)).save(os.path.join(OUT, "white.png"))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    love(); kana(); white()
    print("circulation textures ->", OUT)
