"""Generates photographic textures for the Akihabara level with Stable Diffusion XL (runs locally on the GPU).

Everything in the level is meant to look like real photos stuck on low-poly shapes (the Mallbat look):
building fronts, shopfronts, neon signs, ads, tarmac, pavement, vehicles, vending machines, graffiti.
Ground textures are made to tile seamlessly; the rest are used whole on a face or a panel.

    Tools/imggen-venv/Scripts/python Tools/gen_akiba_photos.py            # make anything missing
    Tools/imggen-venv/Scripts/python Tools/gen_akiba_photos.py --redo ad_  # remake everything starting ad_

Output: Assets/Akiba/Photos/<name>.png
"""
import os
import sys

import numpy as np
import torch
from diffusers import StableDiffusionXLPipeline
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Akiba", "Photos")

PHOTO = "photograph, realistic, 35mm film photo, sharp focus"
NIGHT = "at night, neon lights, Akihabara Tokyo"
FRONT = "straight-on front view, flat, orthographic, centered, full frame"
NEG = "blurry, watermark, logo, signature, frame, border, cartoon, illustration, painting, 3d render, people, person, crowd"

# name: (prompt, size, tile, negative override)
JOBS = {}


def job(name, prompt, size=(768, 768), tile=False, neg=NEG):
    JOBS[name] = (prompt, size, tile, neg)


# building fronts
for i, kind in enumerate(["tall Akihabara electronics building covered in vertical neon signs and anime banners",
                          "Akihabara building facade with huge anime posters and glowing signboards",
                          "narrow Tokyo building facade with stacked colourful shop signs on every floor",
                          "Akihabara arcade building facade with bright lights and posters",
                          "Tokyo commercial building facade with LED screens and neon kanji signs",
                          "old Tokyo mixed-use building facade with air conditioners and small neon signs"]):
    job("fac_akiba_%d" % i, f"{PHOTO}, {kind}, {NIGHT}, {FRONT}, no sky, no street, building wall fills the frame", (768, 1024))
for i, kind in enumerate(["modern office tower facade, grid of lit windows", "glass curtain wall skyscraper facade with lit offices",
                          "dark skyscraper facade with rows of windows, some lit", "concrete office building facade, rows of windows"]):
    job("fac_tower_%d" % i, f"{PHOTO}, {kind}, Tokyo, at night, {FRONT}, the wall fills the whole frame, no sky", (768, 1024))
for i, kind in enumerate(["Japanese apartment building facade with balconies and laundry", "old Tokyo apartment block facade with small balconies, some windows lit"]):
    job("fac_apartment_%d" % i, f"{PHOTO}, {kind}, at night, {FRONT}, the wall fills the whole frame, no sky", (768, 1024))
job("fac_side", f"{PHOTO}, side wall of a Tokyo building, grey concrete with pipes, air conditioner units and a fire escape, at night, {FRONT}, wall fills the frame", (768, 1024))

# shops at street level
for i, kind in enumerate(["electronics shop entrance full of gadgets", "anime figure shop entrance with shelves of figures",
                          "game centre arcade entrance with UFO catcher machines", "Japanese convenience store front",
                          "ramen shop front with noren curtain and lanterns", "retro video game shop front with stacked games",
                          "PC parts shop front with boxes stacked high", "trading card shop front with display cases"]):
    job("shop_%d" % i, f"{PHOTO}, {kind}, Akihabara Tokyo at night, bright interior, {FRONT}, shopfront fills the frame", (1024, 768))

# neon signs and ads
for i in range(8):
    job("vsign_%d" % i, f"{PHOTO}, a single vertical neon sign with Japanese characters glowing, close-up, isolated on a dark night background, Akihabara, {FRONT}",
        (384, 1024))
for i in range(6):
    job("hsign_%d" % i, f"{PHOTO}, a single wide horizontal illuminated shop sign with Japanese characters, close-up, night, {FRONT}", (1024, 384))
for i, kind in enumerate(["anime girl advertisement billboard for a new game", "idol concert poster billboard with a Japanese pop idol",
                          "energy drink advertisement billboard, bright colours", "video game console advertisement billboard",
                          "anime movie poster billboard with dramatic characters", "smartphone advertisement billboard",
                          "maid cafe advertisement poster, cute", "mecha robot anime advertisement billboard",
                          "fashion advertisement billboard with a model", "sushi restaurant advertisement billboard"]):
    neg = NEG.replace("cartoon, illustration, painting, ", "")
    job("ad_%d" % i, f"{PHOTO} of an {kind}, the billboard fills the frame, glossy print, {FRONT}", (768, 768), False, neg)

# ground, walls, materials (tile seamlessly)
job("photo_asphalt", f"{PHOTO}, top-down view of dark asphalt road surface texture, seamless, uniform", (768, 768), True)
job("photo_pavement", f"{PHOTO}, top-down view of Japanese sidewalk paving tiles, grey square tiles, seamless texture, uniform", (768, 768), True)
job("photo_tactile", f"{PHOTO}, top-down view of yellow tactile paving blocks with raised dots, seamless texture", (512, 512), True)
job("photo_concrete", f"{PHOTO}, grey weathered concrete wall texture, seamless, uniform, flat", (768, 768), True)
job("photo_roof", f"{PHOTO}, top-down view of a flat concrete rooftop surface with stains, seamless texture", (768, 768), True)
job("photo_shutter", f"{PHOTO}, closed metal roller shutter of a Tokyo shop with a little graffiti, {FRONT}", (768, 768), True)
job("photo_metal", f"{PHOTO}, scratched painted steel plate texture, seamless, uniform", (512, 512), True)
job("photo_posters", f"{PHOTO}, wall completely covered in torn layered posters and flyers, Tokyo, {FRONT}", (1024, 512))
for i in range(4):
    job("photo_graffiti_%d" % i, f"{PHOTO}, colourful graffiti piece on a concrete wall, Jet Set Radio style tag, {FRONT}, wall fills the frame", (1024, 512))

# vehicles and machines
job("photo_bus", f"{PHOTO}, side view of a Tokyo city bus, white and green, full length, {FRONT}, plain background", (1024, 384))
job("photo_truck", f"{PHOTO}, side view of a Japanese box truck with advertising on the side, full length, {FRONT}", (1024, 384))
job("photo_taxi", f"{PHOTO}, side view of a black Tokyo taxi, full length, {FRONT}, plain background", (1024, 384))
job("photo_car", f"{PHOTO}, side view of a small white Japanese kei car, full length, {FRONT}, plain background", (1024, 384))
job("photo_train", f"{PHOTO}, side view of a silver JR Yamanote line train carriage with a green stripe and windows, full length, {FRONT}", (1024, 384))
job("photo_vending", f"{PHOTO}, front of a Japanese drinks vending machine glowing at night, rows of bottles, {FRONT}, machine fills the frame", (512, 1024))
job("photo_gacha", f"{PHOTO}, front of a row of capsule toy gashapon machines, colourful, {FRONT}, fills the frame", (768, 768))


def make_tileable(img):
    """Blend each half over the other so the edges wrap."""
    a = np.asarray(img).astype(np.float32)
    h, w = a.shape[:2]
    rolled = np.roll(np.roll(a, h // 2, axis=0), w // 2, axis=1)
    y = np.abs(np.linspace(-1, 1, h))[:, None]
    x = np.abs(np.linspace(-1, 1, w))[None, :]
    m = np.clip(1.6 - np.maximum(x, y) * 1.6, 0, 1)[..., None]   # 1 in the middle, 0 at the edges
    out = a * m + rolled * (1 - m)
    return Image.fromarray(out.clip(0, 255).astype(np.uint8))


def main():
    os.makedirs(OUT, exist_ok=True)
    redo = sys.argv[sys.argv.index("--redo") + 1] if "--redo" in sys.argv else None
    todo = [n for n in JOBS if redo and n.startswith(redo) or not os.path.exists(os.path.join(OUT, n + ".png"))]
    if not todo:
        print("nothing to do")
        return
    local = os.path.join(ROOT, "Tools", "models", "sdxl")   # fetched by Tools/fetch_sdxl.py
    src = local if os.path.exists(os.path.join(local, "unet", "diffusion_pytorch_model.fp16.safetensors")) else "stabilityai/stable-diffusion-xl-base-1.0"
    pipe = StableDiffusionXLPipeline.from_pretrained(src, torch_dtype=torch.float16, variant="fp16", use_safetensors=True).to("cuda")
    pipe.set_progress_bar_config(disable=True)
    for k, name in enumerate(todo):
        prompt, size, tile, neg = JOBS[name]
        g = torch.Generator("cuda").manual_seed(1000 + k * 7 + len(name))
        w, h = max(size[0], 512), max(size[1], 512)
        img = pipe(prompt=prompt, negative_prompt=neg, width=w // 8 * 8, height=h // 8 * 8, num_inference_steps=28, guidance_scale=6.5, generator=g).images[0]
        img = img.resize(size, Image.LANCZOS)
        if tile:
            img = make_tileable(img)
        # textures for an old-school look: keep them small
        small = (size[0] // 2, size[1] // 2)
        img.resize(small, Image.LANCZOS).save(os.path.join(OUT, name + ".png"))
        print("made", name, "(%d/%d)" % (k + 1, len(todo)), flush=True)


if __name__ == "__main__":
    main()
