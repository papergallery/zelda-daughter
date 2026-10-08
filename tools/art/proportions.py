#!/usr/bin/env python3
"""D-24: замер пропорций «голова / рост» у спрайтов (RGBA, прозрачный фон) или фигур сырого листа.

    proportions.py sprite A.png [B.png ...] --grid G.png         # 1) верх фигур с делениями через 1 % роста
    proportions.py sprite A.png B.png --chin 21,20 --debug D.png   # 2) подбородок (в % роста от макушки) → доли
    proportions.py sheet RAW.png [--figs 0,1] ...                  # то же по фигурам сырого листа (маска рядом)

Голова = от верхней непрозрачной строки до подбородка; рост — от макушки до нижней непрозрачной строки (как «замер»
D-09). Подбородок отмечается глазом по сетке (--grid): автоматический поиск по цвету кожи не годится — лицо, шея и
вырез футболки одного цвета, а линия подбородка тушью прерывистая. Точность — ±1 % роста (≈ ±0,25 головы при 1/5).
Без --chin печатается грубая автооценка по коже (только для подсказки).
"""
import argparse, pathlib, sys

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

sys.path.insert(0, str(pathlib.Path(__file__).parent))


def skin_mask(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx, mn = rgb.max(-1), rgb.min(-1)
    return (r > 0.55) & (r > g) & (g > b) & (r - b > 0.08) & (r - b < 0.42) & (mx - mn < 0.45) & (g > 0.38)


def measure(rgba, chin_pct=None):
    a = rgba[..., 3]
    rows = np.where((a > 0.5).any(axis=1))[0]
    top, bot = int(rows[0]), int(rows[-1])
    h = bot - top + 1
    if chin_pct is not None:
        chin = top + int(round(chin_pct / 100 * h))
        return {'top': top, 'chin': chin, 'bottom': bot, 'head': chin - top + 1, 'height': h,
                'ratio': (chin - top + 1) / h, 'heads': h / (chin - top + 1), 'manual': True}
    upper = top + int(0.4 * h)
    sk = skin_mask(rgba[..., :3]) & (a > 0.5)
    sk[upper:] = False
    sk = ndimage.binary_opening(sk, iterations=1)
    lab, n = ndimage.label(sk)
    if not n:
        return None
    sizes = ndimage.sum(np.ones_like(a), lab, index=np.arange(1, n + 1))
    face = lab == (int(np.argmax(sizes)) + 1)
    w = face.sum(axis=1)
    fr = np.where(w > 0)[0]
    wmax = np.percentile(w[fr], 90)
    chin = int(fr[np.where(w[fr] >= 0.5 * wmax)[0][-1]])
    return {'top': top, 'chin': chin, 'bottom': bot, 'head': chin - top + 1, 'height': h,
            'ratio': (chin - top + 1) / h, 'heads': h / (chin - top + 1)}


def load_sprite(p):
    return np.asarray(Image.open(p).convert('RGBA')).astype(np.float32) / 255


def debug_strip(items, out):
    cells = []
    for name, rgba, m in items:
        im = Image.fromarray((rgba * 255).astype(np.uint8), 'RGBA')
        bg = Image.new('RGBA', im.size, (225, 220, 205, 255)); bg.alpha_composite(im)
        dr = ImageDraw.Draw(bg)
        if m:
            for y, c in ((m['top'], (0, 0, 255)), (m['chin'], (255, 0, 0)), (m['bottom'], (0, 140, 0))):
                dr.line([0, y, im.width, y], fill=c, width=2)
            dr.text((4, 4), f"{name}\n1/{m['heads']:.2f}", fill=(0, 0, 0))
        cells.append(bg.convert('RGB'))
    H = max(c.height for c in cells)
    W = sum(c.width for c in cells)
    s = Image.new('RGB', (W, H), (255, 255, 255))
    x = 0
    for c in cells:
        s.paste(c, (x, H - c.height)); x += c.width
    s.thumbnail((3000, 1200))
    s.save(out)


def grid_strip(items, out, frac=0.36, H=520):
    cells = []
    for name, rgba, _ in items:
        a = rgba[..., 3]
        rows = np.where((a > 0.5).any(axis=1))[0]
        top, bot = int(rows[0]), int(rows[-1]); h = bot - top + 1
        im = Image.fromarray((rgba * 255).astype(np.uint8), 'RGBA').crop((0, top, rgba.shape[1], top + int(frac * h)))
        cols = np.where(np.asarray(im)[..., 3].max(0) > 128)[0]
        im = im.crop((max(0, cols[0] - 10), 0, cols[-1] + 10, im.height))
        k = H / im.height
        im = im.resize((int(im.width * k), H))
        bg = Image.new('RGBA', (im.width + 40, H), (235, 230, 215, 255)); bg.alpha_composite(im, (40, 0))
        dr = ImageDraw.Draw(bg)
        for pct in range(0, int(frac * 100) + 1):
            y = int(pct / 100 * h * k)
            dr.line([0 if pct % 5 == 0 else 30, y, bg.width, y], fill=(255, 0, 0) if pct % 5 == 0 else (90, 90, 255), width=1)
            if pct % 2 == 0:
                dr.text((2, y + 1), str(pct), fill=(0, 0, 0))
        dr.text((44, H - 14), name, fill=(0, 0, 0))
        cells.append(bg.convert('RGB'))
    s = Image.new('RGB', (sum(c.width for c in cells), H), (255, 255, 255))
    x = 0
    for c in cells:
        s.paste(c, (x, 0)); x += c.width
    s.save(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('mode', choices=['sprite', 'sheet']); ap.add_argument('files', nargs='+')
    ap.add_argument('--figs', default=''); ap.add_argument('--debug'); ap.add_argument('--grid')
    ap.add_argument('--chin', default='', help='подбородок каждой фигуры, %% роста от макушки, через запятую')
    a = ap.parse_args()
    items = []
    chins = [float(x) for x in a.chin.split(',')] if a.chin else []
    for f in a.files:
        f = pathlib.Path(f)
        if a.mode == 'sprite':
            parts = [(f.stem, load_sprite(f))]
        else:
            import sprite_build as sb
            rgba = sb.cutout(f)
            figs = sb.figures(rgba)
            idx = [int(x) for x in a.figs.split(',')] if a.figs else range(len(figs))
            parts = [(f'{f.stem}#{i}', sb.only_figure(rgba, figs[i])) for i in idx]
        for name, rgba in parts:
            m = measure(rgba, chins[len(items)] if len(items) < len(chins) else None)
            items.append((name, rgba, m))
            print(f"{name}: " + (f"голова {m['head']} px / рост {m['height']} px = {m['ratio']:.3f} (1/{m['heads']:.2f})" if m else 'лицо не найдено'))
    if a.grid:
        grid_strip(items, a.grid); print(a.grid)
    if a.debug:
        debug_strip(items, a.debug); print(a.debug)


if __name__ == '__main__':
    main()
