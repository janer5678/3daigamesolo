"""Generates the textures for the Stream level (Assets/Stream/Textures), after swagtwinn's stream: THUG-ish concrete,
chat messages (green check badge, name, message), the slot machine's reel symbols, blocky 16-pixel grass/dirt/log/
pink-leaf blocks, an FL Studio-style piano roll, Roblox studs and pink sakura stickers.

    python Tools/make_stream_textures.py
"""
import os
import random

from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Stream", "Textures")
FONTS = "C:/Windows/Fonts"
rng = random.Random(2026)


def font(names, size):
    for n in names:
        p = os.path.join(FONTS, n)
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def concrete():
    img = Image.new("RGB", (256, 256))
    px = img.load()
    for y in range(256):
        for x in range(256):
            v = 150 + rng.randint(-14, 14)
            px[x, y] = (v, v, v - 4)
    d = ImageDraw.Draw(img)
    for i in range(0, 256, 64):
        d.line([(i, 0), (i, 256)], fill=(110, 110, 106), width=2)
        d.line([(0, i), (256, i)], fill=(110, 110, 106), width=2)
    for _ in range(6):
        x, y = rng.randrange(256), rng.randrange(256)
        pts = [(x, y)]
        for _ in range(6):
            x += rng.randint(-14, 14); y += rng.randint(-14, 14); pts.append((x, y))
        d.line(pts, fill=(100, 100, 96), width=1)
    img.save(os.path.join(OUT, "concrete.png"))


CHAT = [("gaggingwithtape", "W"), ("sicktwinnn", "w"), ("gaggingwithtape", "wwwwwwwwww"), ("ironmmouse", "rakai funny"),
        ("yashen_drg", "yo its my bi"), ("resultingpsychoanalysts", "pls rtaid"), ("gaggingwithtape", "Swag"), ("sicktwinnn", "hi dada")]


def chat():
    fb = font(["arialbd.ttf", "arial.ttf"], 34)
    for i, (name, msg) in enumerate(CHAT):
        img = Image.new("RGB", (512, 64), (34, 18, 52))
        d = ImageDraw.Draw(img)
        d.rectangle([8, 14, 40, 46], fill=(60, 200, 80))
        d.line([(14, 30), (22, 40), (36, 18)], fill=(255, 255, 255), width=5)
        col = (170, 120, 255) if i % 2 == 0 else (90, 230, 120)
        d.text((50, 12), name + ":", font=fb, fill=col)
        w = d.textlength(name + ": ", font=fb)
        d.text((50 + w, 12), msg, font=fb, fill=(255, 255, 255))
        img.save(os.path.join(OUT, "chat%d.png" % i))


def reels():
    # five symbols down a strip: cherry, 7, heart, skull, W
    img = Image.new("RGB", (128, 640), (250, 246, 236))
    d = ImageDraw.Draw(img)
    f = font(["arialbd.ttf"], 96)
    for i in range(5):
        y = i * 128
        d.line([(0, y), (128, y)], fill=(180, 170, 150), width=3)
        if i == 0:
            d.ellipse([20, y + 60, 60, y + 100], fill=(220, 30, 40)); d.ellipse([62, y + 54, 102, y + 94], fill=(220, 30, 40))
            d.line([(40, y + 60), (70, y + 20), (82, y + 54)], fill=(40, 140, 40), width=6)
        elif i == 1:
            d.text((34, y + 10), "7", font=f, fill=(230, 40, 40))
        elif i == 2:
            d.ellipse([22, y + 30, 66, y + 74], fill=(240, 90, 160)); d.ellipse([62, y + 30, 106, y + 74], fill=(240, 90, 160))
            d.polygon([(24, y + 60), (104, y + 60), (64, y + 110)], fill=(240, 90, 160))
        elif i == 3:
            d.ellipse([28, y + 18, 100, y + 90], fill=(30, 30, 34)); d.rectangle([44, y + 80, 84, y + 110], fill=(30, 30, 34))
            d.ellipse([42, y + 42, 60, y + 60], fill=(250, 246, 236)); d.ellipse([68, y + 42, 86, y + 60], fill=(250, 246, 236))
        else:
            d.text((14, y + 10), "W", font=f, fill=(60, 180, 70))
    img.save(os.path.join(OUT, "reels.png"))


def block(name, base, spread, top=None, stripe=None):
    img = Image.new("RGB", (16, 16))
    px = img.load()
    for y in range(16):
        for x in range(16):
            c = tuple(max(0, min(255, base[k] + rng.randint(-spread, spread))) for k in range(3))
            if top and y < 4 + (rng.random() < 0.4):
                c = tuple(max(0, min(255, top[k] + rng.randint(-spread, spread))) for k in range(3))
            if stripe and x % 5 == 0:
                c = tuple(max(0, v - 30) for v in c)
            px[x, y] = c
    img.save(os.path.join(OUT, name + ".png"))


def piano():
    img = Image.new("RGB", (512, 512), (52, 58, 64))
    d = ImageDraw.Draw(img)
    for y in range(0, 512, 16):
        d.line([(0, y), (512, y)], fill=(44, 48, 54) if (y // 16) % 12 in (1, 3, 6, 8, 10) else (64, 70, 76), width=16 if False else 1)
    for x in range(0, 512, 32):
        d.line([(x, 0), (x, 512)], fill=(80, 86, 92) if x % 128 else (110, 116, 122), width=1)
    img.save(os.path.join(OUT, "pianoroll.png"))


def studs():
    img = Image.new("RGB", (64, 64), (255, 255, 255))
    d = ImageDraw.Draw(img)
    for y in range(0, 64, 32):
        for x in range(0, 64, 32):
            d.ellipse([x + 6, y + 6, x + 26, y + 26], fill=(225, 225, 225), outline=(200, 200, 200))
    img.save(os.path.join(OUT, "studs.png"))


def sakura():
    img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    import math
    for k in range(5):
        a = k * 2 * math.pi / 5 - math.pi / 2
        cx, cy = 128 + math.cos(a) * 62, 128 + math.sin(a) * 62
        d.ellipse([cx - 50, cy - 50, cx + 50, cy + 50], fill=(255, 182, 210, 255), outline=(230, 120, 160, 255), width=4)
    d.ellipse([100, 100, 156, 156], fill=(255, 230, 240, 255))
    img.save(os.path.join(OUT, "sakura.png"))


def white():
    Image.new("RGBA", (8, 8), (255, 255, 255, 255)).save(os.path.join(OUT, "white.png"))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    concrete(); chat(); reels(); piano(); studs(); sakura(); white()
    block("grasstop", (96, 160, 56), 18)
    block("grassside", (134, 96, 66), 14, top=(96, 160, 56))
    block("dirt", (134, 96, 66), 16)
    block("log", (110, 84, 52), 12, stripe=True)
    block("pinkleaves", (236, 150, 200), 30)
    print("stream textures ->", OUT)
