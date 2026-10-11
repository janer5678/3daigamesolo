"""Generates the PSX-style textures for the Akihabara level (Assets/Akiba/Textures).

Small, low-colour images in the spirit of PlayStation-era textures: neon signs with real Japanese text
(rendered from the Windows Japanese fonts), shopfronts, building facades with lit windows, LED tickers,
billboards, graffiti, vending machines, the station sign, tarmac and pavement. Everything is drawn here
from shapes, noise and text, so it can be regenerated or tweaked any time:

    python Tools/make_akiba_textures.py
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Akiba", "Textures")
FONTS = "C:/Windows/Fonts"
rng = random.Random(1987)


def font(size, bold=True):
    for name in (["YuGothB.ttc", "msgothic.ttc"] if bold else ["YuGothM.ttc", "msgothic.ttc"]):
        p = os.path.join(FONTS, name)
        if os.path.exists(p):
            return ImageFont.truetype(p, size, index=0)
    return ImageFont.load_default()


def save(img, name):
    img = img.convert("RGBA") if img.mode == "RGBA" else img.convert("RGB")
    img.save(os.path.join(OUT, name + ".png"))


def crunch(img, colours=48):
    """Fewer colours, like old hardware."""
    if img.mode == "RGBA":
        a = img.getchannel("A")
        rgb = img.convert("RGB").quantize(colours, dither=Image.Dither.FLOYDSTEINBERG).convert("RGB")
        rgb.putalpha(a)
        return rgb
    return img.convert("RGB").quantize(colours, dither=Image.Dither.FLOYDSTEINBERG).convert("RGB")


def noise(img, amount=18, seed=None):
    r = random.Random(seed if seed is not None else rng.random())
    px = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            p = px[x, y]
            n = r.randint(-amount, amount)
            px[x, y] = tuple(max(0, min(255, c + n)) for c in p[:3]) + ((p[3],) if len(p) == 4 else ())
    return img


def text_center(d, box, s, f, fill, stroke=0, stroke_fill=(0, 0, 0)):
    x0, y0, x1, y1 = box
    bb = d.textbbox((0, 0), s, font=f, stroke_width=stroke)
    w, h = bb[2] - bb[0], bb[3] - bb[1]
    d.text((x0 + (x1 - x0 - w) / 2 - bb[0], y0 + (y1 - y0 - h) / 2 - bb[1]), s, font=f, fill=fill, stroke_width=stroke, stroke_fill=stroke_fill)


NEON = [(255, 60, 140), (60, 230, 255), (255, 230, 60), (255, 90, 40), (120, 255, 120), (190, 110, 255), (255, 255, 255), (255, 150, 30)]
DARK = [(30, 20, 60), (20, 30, 70), (60, 10, 40), (10, 40, 50), (40, 10, 70), (15, 15, 25)]

V_WORDS = ["秋葉原", "電気街", "アニメ", "ゲーム", "フィギュア", "カード", "中古", "パソコン", "メイド", "ラーメン", "カラオケ",
           "漫画", "同人誌", "ホビー", "家電", "免税", "激安", "ジャンク", "模型", "音楽", "電脳", "ドーナツ", "寿司", "喫茶"]
H_WORDS = ["PCパーツ", "中古ゲーム", "アニメショップ", "フィギュア館", "トレカ専門", "メイドカフェ", "ゲームセンター", "電子部品",
           "ラーメン屋", "カラオケ館", "ガチャガチャ", "レトロゲーム", "ホビー天国", "電気街口", "免税店", "家電量販", "漫画喫茶", "コスプレ"]
LED_LINES = ["★ 新作入荷 ★ 本日セール開催中 ★ AKIHABARA ★ ",
             "◆ 中古ゲーム 高価買取 ◆ PCパーツ 激安 ◆ ",
             "♪ メイドカフェ 営業中 ♪ いらっしゃいませ ♪ "]


def vertical_sign(i):
    w, h = 32, 128
    bg = rng.choice(DARK + NEON[:3])
    fg = rng.choice([c for c in NEON if c != bg])
    img = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, w - 1, h - 1), outline=fg, width=2)
    word = V_WORDS[i % len(V_WORDS)]
    n = len(word)
    f = font(min(24, int((h - 8) / max(n, 1)) - 2))
    step = (h - 8) / n
    for k, ch in enumerate(word):
        text_center(d, (2, 4 + k * step, w - 2, 4 + (k + 1) * step), ch, f, fg)
    save(crunch(img, 24), "sign_v_%d" % i)


def horizontal_sign(i):
    w, h = 128, 32
    bg = rng.choice(DARK + NEON)
    fg = rng.choice([c for c in NEON if c != bg])
    img = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, w - 1, h - 1), outline=fg, width=2)
    word = H_WORDS[i % len(H_WORDS)]
    f = font(min(22, int(118 / max(len(word), 1) * 1.05)))
    text_center(d, (3, 2, w - 3, h - 2), word, f, fg, stroke=1, stroke_fill=(0, 0, 0) if bg in NEON else bg)
    save(crunch(img, 24), "sign_h_%d" % i)


def led(i):
    w, h = 256, 16
    img = Image.new("RGB", (w * 2, h * 2), (10, 6, 8))
    d = ImageDraw.Draw(img)
    col = [(255, 70, 40), (255, 170, 30), (90, 255, 110)][i % 3]
    text_center(d, (0, 0, w * 2, h * 2), LED_LINES[i % len(LED_LINES)], font(26), col)
    img = img.resize((w, h), Image.NEAREST)
    # LED dots: dark every other pixel line
    px = img.load()
    for y in range(h):
        for x in range(w):
            if x % 2 == 1 or y % 2 == 1:
                p = px[x, y]
                px[x, y] = (p[0] // 3, p[1] // 3, p[2] // 3)
    save(img, "led_%d" % i)


def billboard(i):
    s = 128
    top, bot = rng.choice(NEON), rng.choice(DARK)
    img = Image.new("RGB", (s, s))
    d = ImageDraw.Draw(img)
    for y in range(s):
        t = y / s
        d.line((0, y, s, y), fill=tuple(int(top[k] * (1 - t) + bot[k] * t) for k in range(3)))
    # a big sun / halo, stars
    d.ellipse((s * 0.2, s * 0.08, s * 0.8, s * 0.68), fill=tuple(min(255, c + 60) for c in top))
    for _ in range(14):
        x, y = rng.randint(0, s), rng.randint(0, s)
        d.polygon([(x, y - 4), (x + 1, y - 1), (x + 4, y), (x + 1, y + 1), (x, y + 4), (x - 1, y + 1), (x - 4, y), (x - 1, y - 1)], fill=(255, 255, 255))
    # a character: hair, face, big eyes
    hair = rng.choice(NEON)
    cx, cy = s * 0.5, s * 0.46
    d.ellipse((cx - 30, cy - 34, cx + 30, cy + 26), fill=hair)
    d.polygon([(cx - 30, cy - 4), (cx - 40, cy + 40), (cx - 18, cy + 18)], fill=hair)
    d.polygon([(cx + 30, cy - 4), (cx + 40, cy + 40), (cx + 18, cy + 18)], fill=hair)
    d.ellipse((cx - 22, cy - 18, cx + 22, cy + 24), fill=(255, 226, 210))
    d.polygon([(cx - 24, cy - 20), (cx + 24, cy - 20), (cx + 10, cy - 4), (cx, cy - 12), (cx - 12, cy - 2)], fill=hair)
    eye = rng.choice([(60, 120, 255), (255, 60, 120), (120, 60, 220), (40, 200, 160)])
    for ex in (-10, 10):
        d.ellipse((cx + ex - 6, cy - 4, cx + ex + 6, cy + 10), fill=eye)
        d.ellipse((cx + ex - 3, cy - 2, cx + ex + 1, cy + 3), fill=(255, 255, 255))
    d.arc((cx - 5, cy + 10, cx + 5, cy + 17), 20, 160, fill=(200, 60, 90), width=2)
    # body, title
    d.polygon([(cx - 30, s), (cx - 22, cy + 26), (cx + 22, cy + 26), (cx + 30, s)], fill=rng.choice(NEON))
    title = rng.choice(["新作", "発売中", "限定", "大人気", "予約受付", "放送中", "最新刊", "新登場"])
    text_center(d, (0, s - 34, s, s - 4), title, font(26), (255, 255, 255), stroke=2, stroke_fill=(20, 10, 40))
    save(crunch(img, 40), "billboard_%d" % i)


def facade(i):
    """One bay of a building front: 4 m wide x 3.2 m high."""
    w, h = 64, 52
    wall = rng.choice([(150, 140, 150), (120, 130, 150), (170, 160, 140), (110, 100, 120), (140, 150, 160), (90, 90, 110)])
    img = Image.new("RGB", (w, h), wall)
    d = ImageDraw.Draw(img)
    kind = i % 5
    if kind == 0:   # tiled wall, one window
        for y in range(0, h, 4):
            d.line((0, y, w, y), fill=tuple(c - 18 for c in wall))
        for x in range(0, w, 4):
            d.line((x, 0, x, h), fill=tuple(c - 12 for c in wall))
        lit = rng.random() < 0.6
        d.rectangle((14, 12, 50, 40), fill=(255, 220, 140) if lit else (40, 50, 80), outline=(60, 60, 70))
        d.line((32, 12, 32, 40), fill=(60, 60, 70))
    elif kind == 1:  # office glass
        for x in range(0, w, 16):
            lit = rng.random() < 0.5
            d.rectangle((x + 1, 6, x + 15, 46), fill=(200, 230, 255) if lit else (30, 50, 90))
            if lit and rng.random() < 0.5:
                d.rectangle((x + 4, 30, x + 12, 46), fill=(120, 140, 170))
        d.rectangle((0, 46, w, h), fill=tuple(c - 25 for c in wall))
    elif kind == 2:  # apartment with a balcony rail
        d.rectangle((10, 6, 54, 36), fill=(255, 200, 120) if rng.random() < 0.5 else (35, 40, 70))
        d.rectangle((6, 34, 58, 48), fill=tuple(c - 30 for c in wall))
        for x in range(8, 58, 4):
            d.line((x, 36, x, 46), fill=(200, 200, 210))
        d.line((6, 36, 58, 36), fill=(220, 220, 230), width=2)
    elif kind == 3:  # posters in lit windows
        d.rectangle((4, 8, 60, 44), fill=(255, 240, 200))
        for x in range(6, 58, 13):
            d.rectangle((x, 12, x + 11, 40), fill=rng.choice(NEON))
            d.ellipse((x + 2, 16, x + 9, 24), fill=(255, 226, 210))
        d.rectangle((4, 8, 60, 44), outline=(40, 40, 50))
    else:            # dark wall, small windows, air-con
        for x in (8, 40):
            d.rectangle((x, 10, x + 16, 28), fill=(255, 210, 130) if rng.random() < 0.4 else (30, 36, 64), outline=(50, 50, 60))
        d.rectangle((22, 34, 40, 46), fill=(210, 210, 220))
        d.ellipse((26, 36, 36, 45), fill=(150, 150, 160))
    noise(img, 10)
    save(crunch(img, 32), "facade_%d" % i)


def shopfront(i):
    """A shop at street level: 4 m x 3.2 m, a lit interior full of stuff and a banner on top."""
    w, h = 64, 52
    img = Image.new("RGB", (w, h), (250, 240, 220))
    d = ImageDraw.Draw(img)
    banner = rng.choice(NEON)
    d.rectangle((0, 0, w, 12), fill=banner)
    word = H_WORDS[(i * 3) % len(H_WORDS)]
    text_center(d, (1, 0, w - 1, 12), word, font(max(8, min(11, int(62 / len(word) * 1.1)))), (20, 10, 30))
    # shelves of boxes
    for y in range(16, 46, 8):
        d.line((2, y + 7, w - 2, y + 7), fill=(150, 130, 120))
        x = 3
        while x < w - 4:
            bw = rng.randint(3, 7)
            d.rectangle((x, y + rng.randint(0, 3), x + bw, y + 7), fill=rng.choice(NEON + [(255, 255, 255), (80, 120, 255)]))
            x += bw + 1
    d.rectangle((0, 12, 3, h), fill=(80, 80, 90))
    d.rectangle((w - 4, 12, w, h), fill=(80, 80, 90))
    d.rectangle((0, 46, w, h), fill=(110, 100, 100))
    save(crunch(img, 40), "shop_%d" % i)


def graffiti(i):
    w, h = 128, 64
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    fill, line = rng.choice(NEON), rng.choice([(20, 10, 30), (255, 255, 255)])
    # blobby letters: a tag in fat shapes
    tag = rng.choice(["GG", "RUDIE", "TOKYO", "BEAT", "YOYO", "AKIBA", "RIDE", "JET"])
    f = font(46)
    for o in range(4, 0, -1):
        text_center(d, (0, 0, w, h), tag, f, line + (255,), stroke=o + 2, stroke_fill=line + (255,))
    text_center(d, (0, 0, w, h), tag, f, fill + (255,), stroke=2, stroke_fill=tuple(min(255, c + 80) for c in fill) + (255,))
    for _ in range(6):   # drips and stars
        x = rng.randint(10, w - 10)
        d.line((x, h // 2 + 10, x, h // 2 + rng.randint(14, 28)), fill=fill + (255,), width=2)
    save(img, "graffiti_%d" % i)


def simple():
    save(Image.new("RGB", (8, 8), (255, 255, 255)), "white")
    # tarmac
    img = Image.new("RGB", (128, 128), (64, 62, 72))
    noise(img, 14, 1)
    d = ImageDraw.Draw(img)
    for _ in range(8):
        x, y = rng.randint(0, 127), rng.randint(0, 127)
        pts = [(x, y)]
        for _ in range(6):
            x += rng.randint(-8, 8); y += rng.randint(-8, 8); pts.append((x, y))
        d.line(pts, fill=(44, 42, 50))
    save(crunch(img, 24), "asphalt")
    # pavement tiles (1 m tiles, the texture is 2 m)
    img = Image.new("RGB", (64, 64), (150, 144, 140))
    d = ImageDraw.Draw(img)
    for ty in range(4):
        for tx in range(4):
            c = rng.choice([(158, 150, 146), (142, 136, 134), (168, 160, 150), (136, 140, 146)])
            d.rectangle((tx * 16 + 1, ty * 16 + 1, tx * 16 + 15, ty * 16 + 15), fill=c)
    noise(img, 8, 2)
    save(crunch(img, 24), "pavement")
    # tactile paving: yellow with raised dots
    img = Image.new("RGB", (32, 32), (230, 190, 40))
    d = ImageDraw.Draw(img)
    for y in range(3, 32, 8):
        for x in range(3, 32, 8):
            d.ellipse((x - 2, y - 2, x + 2, y + 2), fill=(250, 220, 90), outline=(190, 150, 20))
    save(img, "tactile")
    # concrete
    img = Image.new("RGB", (128, 128), (130, 128, 136))
    noise(img, 16, 3)
    img = img.filter(ImageFilter.GaussianBlur(0.6))
    d = ImageDraw.Draw(img)
    d.line((0, 64, 128, 64), fill=(110, 108, 116)); d.line((64, 0, 64, 128), fill=(110, 108, 116))
    for _ in range(5):
        x = rng.randint(0, 127)
        d.line((x, 0, x + rng.randint(-6, 6), rng.randint(10, 60)), fill=(105, 100, 104))
    save(crunch(img, 24), "concrete")
    # rooftop
    img = Image.new("RGB", (64, 64), (118, 116, 124))
    noise(img, 12, 4)
    d = ImageDraw.Draw(img)
    for y in range(0, 64, 16): d.line((0, y, 64, y), fill=(96, 94, 104))
    save(crunch(img, 16), "roof")
    # metal shutter
    img = Image.new("RGB", (64, 64), (170, 172, 180))
    d = ImageDraw.Draw(img)
    for y in range(0, 64, 4):
        d.line((0, y, 64, y), fill=(120, 122, 130)); d.line((0, y + 1, 64, y + 1), fill=(200, 202, 210))
    save(img, "shutter")
    # hazard stripes
    img = Image.new("RGB", (32, 32), (250, 210, 30))
    d = ImageDraw.Draw(img)
    for k in range(-32, 64, 16):
        d.polygon([(k, 0), (k + 8, 0), (k + 40, 32), (k + 32, 32)], fill=(30, 30, 30))
    save(img, "hazard")
    # metal
    img = Image.new("RGB", (32, 32), (160, 162, 172))
    noise(img, 10, 5)
    save(img, "metal")
    # vending machine front
    img = Image.new("RGB", (32, 64), (230, 40, 50))
    d = ImageDraw.Draw(img)
    d.rectangle((3, 4, 28, 36), fill=(220, 236, 255))
    for row in range(3):
        for col in range(5):
            x, y = 5 + col * 5, 6 + row * 10
            d.rectangle((x, y, x + 3, y + 7), fill=rng.choice(NEON + [(255, 255, 255), (60, 90, 200)]))
            d.point((x + 1, y + 9), fill=(255, 60, 60))
    d.rectangle((6, 44, 26, 52), fill=(30, 30, 40))
    d.rectangle((20, 40, 26, 42), fill=(250, 220, 60))
    save(img, "vending")
    # capsule toy machines
    img = Image.new("RGB", (32, 32), (240, 240, 245))
    d = ImageDraw.Draw(img)
    d.ellipse((4, 2, 28, 20), fill=(200, 230, 255), outline=(150, 150, 170))
    for _ in range(9):
        x, y = rng.randint(7, 23), rng.randint(6, 16)
        d.ellipse((x - 2, y - 2, x + 2, y + 2), fill=rng.choice(NEON))
    d.rectangle((6, 22, 26, 31), fill=rng.choice(NEON))
    d.ellipse((13, 24, 19, 30), fill=(250, 250, 250))
    save(img, "gacha")
    # station sign
    img = Image.new("RGB", (256, 64), (250, 250, 250))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 40, 256, 52), fill=(60, 170, 70))
    text_center(d, (0, 2, 256, 34), "秋葉原", font(30), (20, 20, 30))
    text_center(d, (0, 30, 256, 42), "Akihabara", font(10, False), (40, 40, 50))
    text_center(d, (2, 40, 80, 52), "← 御徒町", font(9), (255, 255, 255))
    text_center(d, (176, 40, 254, 52), "神田 →", font(9), (255, 255, 255))
    save(img, "station_sign")
    # train side: stainless with a green stripe and windows
    img = Image.new("RGB", (256, 64), (196, 200, 208))
    d = ImageDraw.Draw(img)
    for x in range(8, 256, 32):
        d.rectangle((x, 10, x + 22, 30), fill=(40, 60, 90))
        d.rectangle((x + 2, 12, x + 20, 20), fill=(110, 150, 200))
    d.rectangle((0, 34, 256, 42), fill=(60, 180, 80))
    for x in (20, 140):
        d.rectangle((x, 8, x + 14, 56), fill=(170, 174, 184), outline=(120, 124, 132))
    noise(img, 6, 7)
    save(crunch(img, 32), "train_side")
    # wall of posters
    img = Image.new("RGB", (128, 64), (60, 50, 70))
    d = ImageDraw.Draw(img)
    for _ in range(26):
        x, y = rng.randint(-10, 120), rng.randint(-10, 56)
        c = rng.choice(NEON + [(255, 255, 255)])
        d.rectangle((x, y, x + rng.randint(14, 26), y + rng.randint(18, 30)), fill=c, outline=(20, 20, 30))
        d.ellipse((x + 4, y + 4, x + 12, y + 12), fill=(255, 226, 210))
    save(crunch(img, 40), "posters")
    # arcade and maid cafe signs
    for name, word, bg, fg in (("arcade", "ゲームセンター", (20, 10, 60), (60, 230, 255)), ("maid", "メイドカフェ", (255, 140, 190), (255, 255, 255)),
                               ("ramen", "ラーメン", (200, 30, 30), (255, 230, 120)), ("karaoke", "カラオケ", (40, 10, 80), (255, 230, 60))):
        img = Image.new("RGB", (128, 48), bg)
        d = ImageDraw.Draw(img)
        d.rectangle((0, 0, 127, 47), outline=fg, width=3)
        text_center(d, (4, 4, 124, 44), word, font(min(30, int(118 / len(word) * 1.1))), fg, stroke=1, stroke_fill=(0, 0, 0))
        save(crunch(img, 24), name)


def main():
    os.makedirs(OUT, exist_ok=True)
    simple()
    for i in range(16): vertical_sign(i)
    for i in range(16): horizontal_sign(i)
    for i in range(3): led(i)
    for i in range(10): billboard(i)
    for i in range(10): facade(i)
    for i in range(10): shopfront(i)
    for i in range(6): graffiti(i)
    print("textures written to", OUT, len(os.listdir(OUT)))


if __name__ == "__main__":
    main()
