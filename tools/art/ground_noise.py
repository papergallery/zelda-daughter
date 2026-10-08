#!/usr/bin/env python3
"""D-22b: бесшовная текстура шума для акварельной земли (Zelda/Toon, _ZD_GROUND) — без генерации нейросетью, детерминированно.

    python3 tools/art/ground_noise.py      # → ZeldaDaughter/Assets/Art/Ground/ground_noise.png (512×512 RGBA, линейная)

Каналы (все бесшовные — шум периодический по построению):
  R — пятна: шум 1/f^2.2 (БПФ), растянут до 0..1 по перцентилям; шейдер берёт его на трёх масштабах (заливки, мазки, зерно);
  G — то же, другое зерно (развязка масштабов, рваная кромка дороги);
  B — номер ячейки Вороного (≈ 22×22 ячеек на плитку) как случайный тон 0..1: булыжник площади и «мозаика» песка/листвы;
  A — расстояние до границы ячейки (F2 − F1), 0 на шве: швы между камнями.
"""
import pathlib

import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / 'ZeldaDaughter/Assets/Art/Ground/ground_noise.png'
N = 512
CELLS = 22


def fbm(seed, beta=2.2):
    rng = np.random.default_rng(seed)
    white = rng.standard_normal((N, N))
    f = np.fft.fftfreq(N)
    fx, fy = np.meshgrid(f, f)
    r = np.sqrt(fx * fx + fy * fy)
    r[0, 0] = 1.0
    spec = np.fft.fft2(white) / r ** (beta / 2)
    spec[0, 0] = 0
    n = np.real(np.fft.ifft2(spec))
    lo, hi = np.percentile(n, [1, 99])
    return np.clip((n - lo) / (hi - lo), 0, 1)


def voronoi(seed):
    rng = np.random.default_rng(seed)
    # точки — по одной в ячейке сетки с дрожанием (ровнее, чем чистый Пуассон: камни одного размера)
    gy, gx = np.mgrid[0:CELLS, 0:CELLS]
    pts = (np.stack([gx, gy], -1).reshape(-1, 2) + 0.15 + 0.7 * rng.random((CELLS * CELLS, 2))) / CELLS
    tone = rng.random(len(pts))
    ys, xs = np.mgrid[0:N, 0:N]
    p = np.stack([(xs + 0.5) / N, (ys + 0.5) / N], -1)
    d1 = np.full((N, N), 9.0); d2 = np.full((N, N), 9.0); idx = np.zeros((N, N), int)
    for k, q in enumerate(pts):
        d = p - q
        d -= np.round(d)  # по тору — бесшовно
        dist = np.sqrt((d ** 2).sum(-1))
        closer = dist < d1
        d2 = np.where(closer, d1, np.minimum(d2, dist))
        idx = np.where(closer, k, idx)
        d1 = np.where(closer, dist, d1)
    edge = np.clip((d2 - d1) * CELLS / 0.6, 0, 1)
    return tone[idx], edge


def main():
    r, g = fbm(1), fbm(2)
    b, a = voronoi(3)
    img = np.dstack([r, g, b, a])
    OUT.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray((img * 255 + 0.5).astype(np.uint8), 'RGBA').save(OUT, optimize=True)
    print(OUT)


if __name__ == '__main__':
    main()
