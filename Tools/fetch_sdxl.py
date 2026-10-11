"""Downloads the SDXL base model files gen_akiba_photos.py needs into Tools/models/sdxl, many connections
at once (one connection to Hugging Face is slow here; lots of them in parallel are not)."""
import os
import sys
import threading
import urllib.request
from concurrent.futures import ThreadPoolExecutor

REPO = "https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/resolve/main/"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "models", "sdxl")
FILES = ["model_index.json", "scheduler/scheduler_config.json",
         "tokenizer/vocab.json", "tokenizer/merges.txt", "tokenizer/special_tokens_map.json", "tokenizer/tokenizer_config.json",
         "tokenizer_2/vocab.json", "tokenizer_2/merges.txt", "tokenizer_2/special_tokens_map.json", "tokenizer_2/tokenizer_config.json",
         "text_encoder/config.json", "text_encoder/model.fp16.safetensors",
         "text_encoder_2/config.json", "text_encoder_2/model.fp16.safetensors",
         "unet/config.json", "unet/diffusion_pytorch_model.fp16.safetensors",
         "vae/config.json", "vae/diffusion_pytorch_model.fp16.safetensors"]
CHUNK = 16 * 1024 * 1024
lock = threading.Lock()


def size_of(url):
    req = urllib.request.Request(url, method="HEAD")
    with urllib.request.urlopen(req, timeout=60) as r:
        return int(r.headers["Content-Length"])


def fetch_range(url, path, start, end):
    for attempt in range(8):
        try:
            req = urllib.request.Request(url, headers={"Range": "bytes=%d-%d" % (start, end)})
            with urllib.request.urlopen(req, timeout=120) as r:
                data = r.read()
            if len(data) != end - start + 1:
                raise IOError("short read")
            with lock:
                with open(path, "r+b") as f:
                    f.seek(start)
                    f.write(data)
            return
        except Exception as e:
            if attempt == 7:
                raise


def fetch(name, pool):
    url = REPO + name
    path = os.path.join(OUT, name.replace("/", os.sep))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    total = size_of(url)
    if os.path.exists(path) and os.path.getsize(path) == total and not os.path.exists(path + ".part"):
        return
    with open(path + ".part", "wb") as f:
        f.truncate(total)
    jobs = [pool.submit(fetch_range, url, path + ".part", s, min(s + CHUNK, total) - 1) for s in range(0, total, CHUNK)]
    for j in jobs:
        j.result()
    os.replace(path + ".part", path)
    print("got", name, "%.1f MB" % (total / 1e6), flush=True)


def main():
    with ThreadPoolExecutor(max_workers=24) as pool:
        for name in sorted(FILES, key=lambda n: n.endswith(".safetensors")):
            fetch(name, pool)
    print("done", OUT, flush=True)


if __name__ == "__main__":
    main()
