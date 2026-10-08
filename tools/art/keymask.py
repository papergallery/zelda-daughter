#!/usr/bin/env python3
"""D-09: маска фигур по ровному светло-серому фону — запасной путь к BiRefNet (когда ПК/туннель недоступны).

    keymask.py WORK/guard-base-a-0.png [...]     # пишет guard-base-a-0.mask.png рядом (если маски ещё нет; --force — перезаписать)

Фон — медиана рамки картинки; пиксель — фигура, если цвет дальше от фона, чем KEY (доли 0..1 RGB). Затем дыры внутри
силуэтов заливаются (окна фона между рукой и телом вычищает sprite_build.cutout по цвету), край смягчается на 1 px.
"""
import pathlib, sys

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

KEY_LO, KEY_HI = 0.045, 0.12


def keymask(png: pathlib.Path) -> Image.Image:
    img = np.asarray(Image.open(png).convert('RGB')).astype(np.float32) / 255
    border = np.concatenate([img[:12].reshape(-1, 3), img[-12:].reshape(-1, 3), img[:, :12].reshape(-1, 3), img[:, -12:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    # фон у генераций слегка неровный (виньетка): сравниваем с размытым фоном по месту
    dist = np.sqrt(((img - bg) ** 2).sum(-1))
    a = np.clip((dist - KEY_LO) / (KEY_HI - KEY_LO), 0, 1)
    solid = ndimage.binary_fill_holes(a > 0.5)
    solid = ndimage.binary_opening(solid, iterations=1)
    lab, n = ndimage.label(solid)
    if n:
        sizes = ndimage.sum(np.ones_like(a), lab, index=np.arange(1, n + 1))
        solid = np.isin(lab, np.where(sizes >= 200)[0] + 1)
    m = np.maximum(a * ndimage.binary_dilation(solid, iterations=2), solid.astype(np.float32))
    return Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6))


if __name__ == '__main__':
    force = '--force' in sys.argv
    for f in [pathlib.Path(x) for x in sys.argv[1:] if x != '--force']:
        out = f.with_name(f.stem + '.mask.png')
        if out.exists() and not force:
            continue
        keymask(f).save(out)
        print(out)
