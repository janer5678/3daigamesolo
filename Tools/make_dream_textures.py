"""Generates the textures for the Dream level (Assets/Dream/Textures), after LSD: Dream Emulator.

Small, noisy, low-colour images like the PlayStation game's: streaky wooden planks, yellow paper walls, red
marbled flesh, speckled granite, yellow and green grass, rocky walls, teal roof tiles, red lacquer, a loud
purple patterned floor, a green-and-yellow mosaic, glowing orange kanji. Drawn from noise and shapes, so it can
be regenerated any time:

    python Tools/make_dream_textures.py
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Dream", "Textures")
FONTS = "C:/Windows/Fonts"
rng = random.Random(1998)
S = 128


def noise_img(size, scale, octaves=3, seed=0):
    """Smooth value noise, 0..1, tiling."""
    r = random.Random(seed)
    out = [[0.0] * size for _ in range(size)]
    amp, total = 1.0, 0.0
    for o in range(octaves):
        n = max(2, int(size / scale * (2 ** o)))
        grid = [[r.random() for _ in range(n)] for _ in range(n)]
        for y in range(size):
            fy = y / size * n
            y0 = int(fy) % n; y1 = (y0 + 1) % n; ty = fy - int(fy); ty = ty * ty * (3 - 2 * ty)
            for x in range(size):
                fx = x / size * n
                x0 = int(fx) % n; x1 = (x0 + 1) % n; tx = fx - int(fx); tx = tx * tx * (3 - 2 * tx)
                a = grid[y0][x0] + (grid[y0][x1] - grid[y0][x0]) * tx
                b = grid[y1][x0] + (grid[y1][x1] - grid[y1][x0]) * tx
                out[y][x] += (a + (b - a) * ty) * amp
        total += amp
        amp *= 0.5
    return [[v / total for v in row] for row in out]


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def from_noise(n, c0, c1, grain=18, seed=0):
    r = random.Random(seed)
    img = Image.new("RGB", (len(n), len(n)))
    px = img.load()
    for y in range(len(n)):
        for x in range(len(n)):
            c = lerp(c0, c1, min(1, max(0, n[y][x])))
            g = r.randint(-grain, grain)
            px[x, y] = tuple(max(0, min(255, v + g)) for v in c)
    return img


def crunch(img, colours=32):
    return img.convert("RGB").quantize(colours, dither=Image.Dither.FLOYDSTEINBERG).convert("RGB")


def save(img, name):
    crunch(img).save(os.path.join(OUT, name + ".png"))


def wood():
    img = Image.new("RGB", (S, S))
    px = img.load()
    n = noise_img(S, 16, 3, 1)
    for y in range(S):
        plank = y // 16
        base = [(150, 120, 70), (170, 140, 82), (130, 104, 60), (160, 128, 76)][plank % 4]
        for x in range(S):
            streak = math.sin(x * 0.5 + n[y][x] * 9 + plank * 3) * 0.5 + 0.5
            c = lerp(base, (90, 70, 40), streak * 0.5)
            if y % 16 == 0 or (x + plank * 37) % 64 == 0:
                c = (60, 46, 28)
            px[x, y] = c
    save(img, "wood")


def yellow_wall():
    img = Image.new("RGB", (S, S))
    px = img.load()
    n = noise_img(S, 32, 2, 2)
    for y in range(S):
        for x in range(S):
            t = y / S   # darker towards the bottom, like the game's gradient walls
            c = lerp((236, 222, 120), (120, 110, 40), t * t)
            c = lerp(c, (200, 180, 90), n[y][x] * 0.3)
            if x < 4 or x > S - 5:
                c = (150, 74, 30)   # the posts between panels
            px[x, y] = c
    save(img, "yellowwall")


def ceiling():
    img = Image.new("RGB", (S, S), (40, 40, 36))
    d = ImageDraw.Draw(img)
    for i in range(0, S, 32):
        d.line([(i, 0), (i, S)], fill=(80, 70, 50), width=2)
        d.line([(0, i), (S, i)], fill=(80, 70, 50), width=2)
    save(img, "ceiling")


def flesh(name, bright):
    n = noise_img(S, 24, 4, 3 + bright)
    img = Image.new("RGB", (S, S))
    px = img.load()
    for y in range(S):
        for x in range(S):
            v = math.sin((n[y][x] * 14 + x * 0.05) * 1.3) * 0.5 + 0.5
            c = lerp((110, 30, 30), (220, 150, 140), v) if not bright else lerp((200, 30, 20), (255, 120, 80), v)
            px[x, y] = c
    save(img, name)


def granite():
    save(from_noise(noise_img(S, 6, 2, 4), (90, 90, 96), (190, 190, 196), 40, 4), "granite")


def grass(name, c0, c1, seed):
    save(from_noise(noise_img(S, 10, 3, seed), c0, c1, 26, seed), name)


def rock():
    n = noise_img(S, 12, 4, 7)
    img = from_noise(n, (70, 44, 30), (150, 100, 70), 30, 7)
    d = ImageDraw.Draw(img)
    for _ in range(40):
        x, y = rng.randrange(S), rng.randrange(S)
        d.ellipse([x, y, x + rng.randint(6, 16), y + rng.randint(4, 10)], outline=(40, 26, 18))
    save(img, "rock")


def roof():
    img = Image.new("RGB", (S, S), (40, 120, 110))
    d = ImageDraw.Draw(img)
    for y in range(0, S, 8):
        d.line([(0, y), (S, y)], fill=(20, 70, 64), width=2)
        for x in range(0 if (y // 8) % 2 else 8, S, 16):
            d.line([(x, y), (x, y + 8)], fill=(70, 160, 150))
    save(img, "roof")


def plaster():
    img = from_noise(noise_img(S, 32, 2, 8), (230, 228, 222), (250, 248, 244), 10, 8)
    d = ImageDraw.Draw(img)
    d.rectangle([0, S - 16, S, S], fill=(180, 60, 40))   # a red skirting along the bottom
    for x in range(0, S, 32):
        d.line([(x, 0), (x, S)], fill=(160, 70, 50), width=3)
    save(img, "plaster")


def lacquer():
    save(from_noise(noise_img(S, 20, 2, 9), (190, 30, 20), (240, 70, 40), 14, 9), "lacquer")


def purple_floor():
    img = Image.new("RGB", (S, S), (150, 60, 200))
    d = ImageDraw.Draw(img)
    for y in range(0, S, 32):
        d.rectangle([0, y, S, y + 4], fill=(250, 220, 40))
        d.rectangle([0, y + 6, S, y + 8], fill=(220, 40, 60))
        for x in range(0, S, 16):
            d.rectangle([x + 3, y + 14, x + 11, y + 22], fill=(240, 80, 120) if (x // 16) % 2 else (40, 180, 220))
    save(img, "purplefloor")


def mosaic():
    img = Image.new("RGB", (S, S))
    d = ImageDraw.Draw(img)
    cols = [(60, 220, 60), (250, 230, 40), (40, 120, 40), (250, 120, 30), (40, 200, 220)]
    for y in range(0, S, 8):
        for x in range(0, S, 8):
            d.rectangle([x, y, x + 7, y + 7], fill=cols[rng.randrange(len(cols))])
            d.rectangle([x + 2, y + 2, x + 5, y + 5], fill=(30, 60, 30))
    save(img, "mosaic")


def kanji():
    img = Image.new("RGB", (S, S), (34, 10, 46))
    d = ImageDraw.Draw(img)
    path = os.path.join(FONTS, "msgothic.ttc")
    f = ImageFont.truetype(path, 20, index=0) if os.path.exists(path) else ImageFont.load_default()
    chars = "夢光影心水火月風空森道橋眠鏡星"
    for y in range(0, S, 26):
        for x in range(0, S, 26):
            d.text((x + 2, y + 2), chars[rng.randrange(len(chars))], font=f, fill=(255, 140 + rng.randrange(80), 20))
    save(img, "kanji")


def dark_city():
    img = Image.new("RGB", (S, S), (10, 10, 18))
    d = ImageDraw.Draw(img)
    for x in range(0, S, 6):
        h = rng.randint(30, 110)
        d.rectangle([x, S - h, x + 5, S], fill=(20, 20, 30))
        for wy in range(S - h + 4, S, 6):
            if rng.random() < 0.3:
                d.point((x + 2, wy), fill=(250, 200, 90))
    save(img, "darkcity")


def stone_path():
    n = noise_img(S, 16, 3, 11)
    img = from_noise(n, (120, 116, 108), (170, 164, 150), 16, 11)
    d = ImageDraw.Draw(img)
    for y in range(0, S, 32):
        for x in range(0 if (y // 32) % 2 else 16, S, 32):
            d.rectangle([x, y, x + 31, y + 31], outline=(80, 76, 70))
    save(img, "stonepath")


def white():
    Image.new("RGB", (8, 8), (255, 255, 255)).save(os.path.join(OUT, "white.png"))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    wood(); yellow_wall(); ceiling(); flesh("flesh", 0); flesh("fleshglow", 1); granite()
    grass("grassyellow", (150, 150, 70), (200, 190, 110), 5); grass("grassgreen", (40, 140, 30), (90, 200, 50), 6)
    rock(); roof(); plaster(); lacquer(); purple_floor(); mosaic(); kanji(); dark_city(); stone_path(); white()
    print("dream textures ->", OUT)
