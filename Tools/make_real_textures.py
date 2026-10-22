"""Generates the textures for the two levels after real games:

  Surf Mesa (Assets/Surf/Textures): after the CS:S map surf_mesa. Red-brown canyon rock shaded blue-teal, blue cave
    rock, the lava room's orange rock and the lava itself, smooth orange sand, the pale grey-blue metal of the ramps
    with their panel seams, the white tiles of the start zones, stalactites.
  Elden Ruins (Assets/Elden/Textures): after the Elden Ring gameplay preview. Meadow grass and dirt, cut-out grass
    blades and golden autumn leaves, bark, weathered ashlar walls and big ruin blocks, flagstones, cliff rock, old
    planks, slate, a red banner, mist, glows, the orange scrawl of a message left on the ground, the Erdtree's
    glowing canopy.

All of it is drawn here from noise and shapes, tileable where it repeats, so it can be tweaked and regenerated:

    python Tools/make_real_textures.py
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SURF = os.path.join(ROOT, "Assets", "Surf", "Textures")
ELDEN = os.path.join(ROOT, "Assets", "Elden", "Textures")


# ---- noise ---------------------------------------------------------------------------------------

def vnoise(size, cells, seed):
    """Value noise that tiles: `cells` random values across, smoothly interpolated, wrapping round."""
    rng = np.random.default_rng(seed)
    g = rng.random((cells, cells))
    x = np.arange(size) * cells / size
    i0 = np.floor(x).astype(int)
    f = x - i0
    i1 = (i0 + 1) % cells
    f = f * f * (3 - 2 * f)
    a = g[np.ix_(i0, i0)]
    b = g[np.ix_(i0, i1)]
    c = g[np.ix_(i1, i0)]
    d = g[np.ix_(i1, i1)]
    fx = f[None, :]
    fy = f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(size, base, octaves, seed, gain=0.5):
    s = np.zeros((size, size))
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        cells = base * 2 ** o
        if cells > size:
            break
        s += amp * vnoise(size, cells, seed + o * 101)
        norm += amp
        amp *= gain
    return s / norm


def ridged(size, base, octaves, seed):
    s = np.zeros((size, size))
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        cells = base * 2 ** o
        if cells > size:
            break
        n = 1 - np.abs(vnoise(size, cells, seed + o * 77) * 2 - 1)
        s += amp * n * n
        norm += amp
        amp *= 0.5
    return s / norm


def stretch(n, k):
    """Smears noise vertically (rock streaks), keeping it tileable."""
    out = n.copy()
    for i in range(1, k):
        out += np.roll(n, i, axis=0)
    return out / k


def norm01(a):
    return (a - a.min()) / max(1e-6, a.max() - a.min())


def ramp(t, stops):
    """Maps 0..1 through a list of (position, (r, g, b)) colour stops."""
    t = np.clip(t, 0, 1)
    out = np.zeros(t.shape + (3,))
    for (p0, c0), (p1, c1) in zip(stops, stops[1:]):
        m = (t >= p0) & (t <= p1)
        k = ((t - p0) / max(1e-6, p1 - p0))[m][:, None]
        out[m] = np.array(c0) * (1 - k) + np.array(c1) * k
    return out


def save(arr, folder, name):
    os.makedirs(folder, exist_ok=True)
    a = np.clip(arr, 0, 255).astype(np.uint8)
    mode = "RGBA" if a.shape[-1] == 4 else "RGB"
    Image.fromarray(a, mode).save(os.path.join(folder, name + ".png"))
    print("wrote", name)


def shade(rgb, k):
    return rgb * k[..., None]


# ---- Surf Mesa ---------------------------------------------------------------------------------------

def surf_rock(seed, stops, blue_mix, blue):
    S = 1024
    base = fbm(S, 4, 7, seed)
    streak = norm01(stretch(fbm(S, 32, 4, seed + 9), 48))
    cracks = ridged(S, 8, 6, seed + 3)
    t = norm01(base * 0.55 + streak * 0.45)
    col = ramp(t, stops)
    # blue-teal shading in patches, like the map's rock
    patch = np.clip((fbm(S, 3, 6, seed + 21) * 0.7 + streak * 0.3 - 0.4) * 2.6, 0, 1) * blue_mix
    col = col * (1 - patch[..., None]) + np.array(blue) * (0.6 + 0.6 * t[..., None]) * patch[..., None]
    col = shade(col, 0.72 + 0.4 * (1 - cracks ** 3) + 0.12 * (streak - 0.5))
    return col


def surf_textures():
    S = 512
    save(surf_rock(11, [(0, (60, 34, 24)), (0.45, (122, 70, 46)), (0.8, (160, 100, 66)), (1, (190, 132, 92))], 1.0, (64, 96, 108)), SURF, "rock_red")
    save(surf_rock(31, [(0, (16, 30, 40)), (0.5, (38, 70, 86)), (0.85, (70, 108, 122)), (1, (110, 140, 150))], 0.35, (96, 62, 44)), SURF, "rock_blue")
    lava_rock = surf_rock(51, [(0, (120, 30, 10)), (0.5, (210, 80, 24)), (0.85, (240, 130, 40)), (1, (255, 180, 80))], 0.0, (0, 0, 0))
    swirl = fbm(1024, 4, 3, 77)
    lava_rock[..., 1] *= 0.75 + 0.5 * np.abs(np.sin(swirl * 18))[...]
    save(lava_rock, SURF, "rock_lava")

    # sand: smooth orange-tan with soft ripples
    n = fbm(S, 4, 5, 5)
    y = np.arange(S)[:, None] / S
    rip = np.sin((y * 14 + fbm(S, 2, 3, 6) * 3) * 2 * math.pi) * 0.5 + 0.5
    t = norm01(n * 0.7 + rip * 0.3)
    save(ramp(t, [(0, (176, 122, 70)), (0.5, (208, 152, 94)), (1, (228, 178, 118))]), SURF, "sand")

    # ramp metal: pale grey-blue, faint brushing, a dark seam down the left edge of each panel
    n = fbm(S, 8, 4, 41) * 0.6 + stretch(fbm(S, 64, 2, 43), 40) * 0.4
    col = ramp(norm01(n), [(0, (150, 160, 176)), (1, (190, 198, 212))])
    x = np.arange(S)
    seam = np.exp(-((np.minimum(x, S - x)) / 2.5) ** 2)
    col = col * (1 - 0.55 * seam[None, :, None])
    save(col, SURF, "ramp")

    # start zone tiles: white with grey grout, 4 x 4
    col = np.full((S, S, 3), 236.0)
    col *= (0.96 + 0.06 * fbm(S, 8, 3, 61))[..., None]
    g = (np.arange(S) % (S // 4))
    line = (g < 4) | (g > S // 4 - 3)
    col[line, :, :] = 150
    col[:, line, :] = 150
    save(col, SURF, "tile")

    # lava: glowing cells
    a = fbm(S, 6, 5, 71)
    cells = ridged(S, 8, 3, 73)
    t = norm01(a * 0.5 + cells * 0.7)
    save(ramp(t, [(0, (150, 30, 0)), (0.45, (240, 110, 10)), (0.8, (255, 200, 40)), (1, (255, 250, 170))]), SURF, "lava")

    # stalactites: pale grey with drips
    n = norm01(stretch(fbm(S, 16, 4, 81), 60))
    save(ramp(n, [(0, (120, 124, 128)), (1, (225, 228, 230))]), SURF, "stalactite")

    save(np.full((8, 8, 3), 255.0), SURF, "white")
    save(radial(128, 2.2), SURF, "glow")


# ---- Elden Ruins -------------------------------------------------------------------------------------

def stone_blocks(S, rows, seed, stops, mortar, moss_amount, irregular=0.35):
    """Ashlar: rows of blocks of random widths, each its own tone, weathered, mortar between, moss creeping up."""
    rng = np.random.default_rng(seed)
    tone = np.zeros((S, S))
    edge = np.ones((S, S))
    rh = S // rows
    for r in range(rows):
        y0 = r * rh
        x = int(rng.integers(0, S))
        while True:
            w = int(S / rows * (1.2 + rng.random() * 1.6))
            v = rng.random()
            for i in range(w):
                xx = (x + i) % S
                tone[y0:y0 + rh, xx] = v
            # mortar at the block's left end
            for i in range(-2, 3):
                edge[y0:y0 + rh, (x + i) % S] = 0
            x += w
            if x >= S + int(rng.integers(0, S // 3)) or x - S > S:
                break
        edge[max(0, y0 - 2):y0 + 2, :] = 0
    soft = np.array(Image.fromarray((edge * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2.2))) / 255.0
    n = fbm(S, 8, 6, seed + 1)
    pits = ridged(S, 16, 3, seed + 2)
    t = np.clip(tone * irregular + n * 0.75 - (1 - soft) * 0.25 + 0.05, 0, 1)
    col = ramp(t, stops)
    col = shade(col, 0.82 + 0.25 * (1 - pits ** 4))
    col = col * soft[..., None] + np.array(mortar) * (1 - soft[..., None])
    if moss_amount > 0:
        m = np.clip((fbm(S, 4, 5, seed + 5) - (1 - moss_amount)) * 4, 0, 1) * np.clip(1 - soft + 0.3, 0, 1)
        col = col * (1 - m[..., None]) + np.array((78, 92, 46)) * (0.7 + 0.6 * n[..., None]) * m[..., None]
    return col


def grass_card(S, seed, tips, bases, count, golden=0.0):
    """A clump of grass blades on a transparent card: roots along the bottom, tips somewhere up the card."""
    rng = np.random.default_rng(seed)
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for i in range(count):
        x0 = S * (0.08 + 0.84 * rng.random())
        h = S * (0.35 + 0.62 * rng.random())
        lean = (rng.random() - 0.5) * S * 0.35
        w = S * (0.008 + 0.012 * rng.random())
        dry = rng.random() < golden
        tip = np.array(tips[1] if dry else tips[0], float) * (0.8 + 0.35 * rng.random())
        base = np.array(bases[1] if dry else bases[0], float)
        segs = 10
        for s in range(segs):
            a, b = s / segs, (s + 1) / segs
            xa = x0 + lean * a * a
            xb = x0 + lean * b * b
            ya, yb = S - a * h, S - b * h
            wa, wb = w * (1 - a), w * (1 - b)
            c = tuple(int(v) for v in (base * (1 - a) + tip * a)) + (255,)
            d.polygon([(xa - wa, ya), (xa + wa, ya), (xb + wb, yb), (xb - wb, yb)], fill=c)
    return np.array(img).astype(float)


def leaf_card(S, seed, colours, count, blob=0.42, size=(0.025, 0.05), glow=False):
    """A cluster of leaves on a transparent card: lots of small leaves in a rough ball, lit from the top."""
    rng = np.random.default_rng(seed)
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for i in range(count):
        r = blob * S * math.sqrt(rng.random())
        a = rng.random() * 2 * math.pi
        cx, cy = S / 2 + math.cos(a) * r, S / 2 + math.sin(a) * r * 0.85
        L = S * (size[0] + (size[1] - size[0]) * rng.random())
        ang = rng.random() * math.pi
        dx, dy = math.cos(ang) * L, math.sin(ang) * L
        nx, ny = -dy * 0.38, dx * 0.38
        col = np.array(colours[int(rng.integers(0, len(colours)))], float)
        light = 0.65 + 0.5 * (1 - (cy / S)) + 0.15 * (rng.random() - 0.5)
        if glow:
            light = 0.85 + 0.3 * rng.random()
        c = tuple(int(min(255, v * light)) for v in col) + (255,)
        d.polygon([(cx - dx, cy - dy), (cx + nx, cy + ny), (cx + dx, cy + dy), (cx - nx, cy - ny)], fill=c)
    return np.array(img).astype(float)


def radial(S, power, colour=(255, 255, 255)):
    y, x = np.mgrid[0:S, 0:S]
    r = np.sqrt((x - S / 2 + 0.5) ** 2 + (y - S / 2 + 0.5) ** 2) / (S / 2)
    a = np.clip(1 - r, 0, 1) ** power
    out = np.zeros((S, S, 4))
    out[..., :3] = colour
    out[..., 3] = a * 255
    return out


def elden_textures():
    S = 512
    # meadow ground: green with olive and straw patches
    n = fbm(S, 4, 6, 101)
    p = fbm(S, 3, 4, 102)
    fine = fbm(S, 64, 2, 103)
    t = norm01(n * 0.6 + fine * 0.4)
    col = ramp(t, [(0, (44, 56, 26)), (0.5, (78, 94, 40)), (1, (120, 128, 58))])
    straw = np.clip((p - 0.55) * 4, 0, 1)
    col = col * (1 - straw[..., None] * 0.6) + np.array((150, 132, 70)) * straw[..., None] * 0.6
    save(col, ELDEN, "grass_ground")

    # dirt path: grey-brown with pebbles
    n = fbm(S, 8, 6, 111)
    peb = ridged(S, 32, 2, 112)
    col = ramp(norm01(n), [(0, (70, 60, 48)), (0.5, (104, 92, 74)), (1, (138, 126, 104))])
    col = shade(col, 0.85 + 0.25 * peb ** 6)
    save(col, ELDEN, "dirt")

    # grass blades, green and gone golden
    save(grass_card(256, 121, [(168, 186, 84), (222, 188, 96)], [(70, 92, 38), (120, 102, 48)], 80, golden=0.2), ELDEN, "grass_blades")
    save(grass_card(256, 122, [(232, 196, 104), (240, 170, 70)], [(122, 104, 50), (128, 92, 42)], 70, golden=0.6), ELDEN, "grass_golden")

    # golden autumn leaves, and dark green ones
    save(leaf_card(256, 131, [(232, 178, 40), (214, 140, 26), (244, 206, 76), (196, 112, 22), (170, 150, 40)], 900), ELDEN, "leaves_gold")
    save(leaf_card(256, 132, [(52, 72, 34), (70, 92, 40), (40, 58, 30), (88, 104, 48)], 900), ELDEN, "leaves_green")

    # bark
    n = norm01(stretch(fbm(S, 32, 4, 141), 50) * 0.7 + fbm(S, 8, 4, 142) * 0.3)
    cr = ridged(S, 24, 2, 143)
    col = ramp(n, [(0, (40, 34, 28)), (0.6, (84, 74, 62)), (1, (120, 110, 96))])
    save(shade(col, 0.75 + 0.35 * (1 - stretch(cr, 30))), ELDEN, "bark")

    # walls: fine ashlar, warm grey; big ruin blocks, paler and mossier
    save(stone_blocks(S, 6, 151, [(0, (92, 88, 78)), (0.5, (140, 134, 118)), (1, (184, 176, 156))], (60, 56, 48), 0.25), ELDEN, "stone_wall")
    save(stone_blocks(S, 3, 152, [(0, (108, 104, 92)), (0.5, (156, 150, 132)), (1, (196, 190, 170))], (70, 66, 56), 0.45, 0.25), ELDEN, "stone_block")
    # castle stone: darker and colder
    save(stone_blocks(S, 8, 153, [(0, (66, 68, 66)), (0.5, (104, 106, 100)), (1, (146, 146, 136))], (40, 40, 38), 0.15), ELDEN, "stone_castle")

    # flagstones: big squares with grass in the joints
    col = stone_blocks(S, 4, 161, [(0, (96, 94, 86)), (0.5, (132, 128, 114)), (1, (166, 160, 142))], (62, 74, 36), 0.2, 0.5)
    save(col, ELDEN, "flagstone")

    # cliff rock
    n = fbm(S, 4, 7, 171)
    cr = ridged(S, 6, 5, 172)
    st = stretch(fbm(S, 32, 3, 173), 20)
    t = norm01(n * 0.6 + st * 0.4)
    col = ramp(t, [(0, (54, 54, 52)), (0.5, (98, 96, 90)), (1, (150, 146, 134))])
    save(shade(col, 0.7 + 0.4 * (1 - cr ** 3)), ELDEN, "rock")

    # weathered planks, running along u
    y = np.arange(S)
    board = (y // (S // 6))
    rng = np.random.default_rng(181)
    tone = rng.random(8)[board % 8]
    grain = stretch(fbm(S, 64, 3, 182).T, 80).T
    n = norm01(grain * 0.7 + fbm(S, 8, 3, 183) * 0.3)
    col = ramp(np.clip(n * 0.7 + tone[:, None] * 0.3, 0, 1), [(0, (54, 44, 34)), (0.5, (96, 80, 60)), (1, (130, 112, 88))])
    gap = (y % (S // 6)) < 3
    col[gap] *= 0.3
    save(col, ELDEN, "wood")

    # slate roof
    col = stone_blocks(S, 10, 191, [(0, (38, 42, 50)), (0.5, (58, 62, 72)), (1, (84, 88, 96))], (24, 26, 30), 0.1, 0.4)
    save(col, ELDEN, "slate")

    # a red banner with a gold border and a gold tree-ish sign
    W, H = 128, 256
    img = Image.new("RGB", (W, H), (110, 22, 20))
    d = ImageDraw.Draw(img)
    d.rectangle([6, 0, W - 7, H - 1], outline=(196, 156, 70), width=4)
    cx = W // 2
    d.line([(cx, 70), (cx, 190)], fill=(200, 160, 74), width=6)
    for k, (ax, ay) in enumerate([(-34, 60), (34, 60), (-24, 90), (24, 90), (-40, 120), (40, 120)]):
        d.line([(cx, 150 - k * 10), (cx + ax, ay)], fill=(200, 160, 74), width=4)
    d.ellipse([cx - 44, 46, cx + 44, 134], outline=(200, 160, 74), width=4)
    arr = np.array(img).astype(float)
    arr *= (0.8 + 0.3 * fbm(256, 8, 3, 195)[:H, :W])[..., None]
    save(arr, ELDEN, "banner")

    # mist: soft billows, see-through
    n = fbm(S, 3, 6, 201)
    m = np.clip((n - 0.3) * 1.8, 0, 1)
    out = np.zeros((S, S, 4))
    out[..., :3] = 255
    out[..., 3] = m * 255
    save(out, ELDEN, "mist")

    save(radial(128, 2.2), ELDEN, "glow")
    save(radial(128, 0.9), ELDEN, "glow_soft")

    # a beam of light: bright up the middle, soft to the sides, fading towards the top
    W, H = 64, 256
    x = np.abs(np.arange(W) - W / 2 + 0.5) / (W / 2)
    y = np.arange(H)[:, None] / H
    a = np.exp(-(x[None, :] / 0.35) ** 2) * (0.25 + 0.75 * y)
    out = np.zeros((H, W, 4))
    out[..., :3] = 255
    out[..., 3] = a * 255
    save(out, ELDEN, "beam")

    # a message left on the ground: an orange-red scrawl
    S2 = 256
    img = Image.new("RGBA", (S2, S2), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(211)
    for line in range(3):
        y0 = 80 + line * 40
        x = 40
        pts = []
        while x < 216:
            pts.append((x, y0 + (rng.random() - 0.5) * 26))
            x += 6 + rng.random() * 10
        d.line(pts, fill=(255, 140, 60, 255), width=5)
    img = img.filter(ImageFilter.GaussianBlur(2.5))
    arr = np.array(img).astype(float)
    arr[..., 3] = np.clip(arr[..., 3] * 1.8, 0, 255)
    save(arr, ELDEN, "message")

    # the Erdtree's canopy: glowing gold leaves
    save(leaf_card(256, 221, [(255, 214, 110), (255, 190, 70), (255, 230, 150), (240, 170, 60)], 1400, blob=0.46, size=(0.02, 0.045), glow=True), ELDEN, "erd_leaves")

    save(np.full((8, 8, 3), 255.0), ELDEN, "white")


def shared():
    # cloud noise for both skies
    n = fbm(512, 4, 7, 301)
    v = norm01(n) * 255
    save(np.stack([v, v, v], -1), SURF, "clouds")
    save(np.stack([v, v, v], -1), ELDEN, "clouds")


if __name__ == "__main__":
    surf_textures()
    elden_textures()
    shared()
