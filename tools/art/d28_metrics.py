#!/usr/bin/env python3
"""D-28: цифры критерия п. 2 (docs/done-criteria/D-28.md) — кадр игры против концепта, и склейка «сборка | концепт».

    d28_metrics.py GAME.png CONCEPT.jpg [--splice OUT.jpg] [--label "сборка | f1"]

Порядок и пороги записаны до замера (docs/demo/ref2game-review.md §2):
- кадр игры (1080×2340) приводится к размеру концепта (827×1792, то же 9:19,5);
- средний цвет кадра → Lab; ΔE — CIE76 (основная, строже), рядом ΔE2000 для справки; порог ≤ 5;
- 8 кластеров палитры — median-cut PIL по кадру, уменьшенному до 200×430 (как README world-b); для каждого кластера игры —
  ΔE76 до ближайшего кластера концепта; порог — каждый ≤ 8;
- яркость L* по сетке 4 столбца × 8 строк (низкие частоты): корреляция Пирсона сеток и правила «того же рисунка» по одинаковым
  областям кадра (раскладка пилота повторяет f1 по экрану, tools/gen-pilot-f1.py): дорога светлее травы, тень под кроной
  большого дерева темнее открытой травы — в обоих кадрах.
"""
import argparse, json, math, sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

W, H = 827, 1792
# Области f1 в px (раскладка pilot-f1 повторяет их на экране): ось дороги — tools/gen-pilot-f1.py ROAD; трава — открытые участки луга;
# тень — под кроной большого дерева (tree_1, основание 150,385).
ROAD = [(120, 1450), (290, 1170), (385, 950), (425, 760), (480, 560), (610, 380)]
GRASS = [(470, 1180, 620, 1260), (200, 560, 330, 660), (520, 1380, 640, 1460), (20, 1380, 140, 1500)]
SHADE = [(95, 330, 215, 395)]


def lab(rgb):
    c = np.where(rgb > 0.04045, ((rgb + 0.055) / 1.055) ** 2.4, rgb / 12.92)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = c @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def de76(a, b):
    return float(np.sqrt(((np.asarray(a) - np.asarray(b)) ** 2).sum()))


def de2000(l1, l2):
    L1, a1, b1 = l1; L2, a2, b2 = l2
    C1, C2 = math.hypot(a1, b1), math.hypot(a2, b2)
    Cb = (C1 + C2) / 2
    G = 0.5 * (1 - math.sqrt(Cb ** 7 / (Cb ** 7 + 25 ** 7)))
    a1p, a2p = (1 + G) * a1, (1 + G) * a2
    C1p, C2p = math.hypot(a1p, b1), math.hypot(a2p, b2)
    h1p = math.degrees(math.atan2(b1, a1p)) % 360
    h2p = math.degrees(math.atan2(b2, a2p)) % 360
    dLp, dCp = L2 - L1, C2p - C1p
    dh = h2p - h1p
    if C1p * C2p == 0: dh = 0
    elif dh > 180: dh -= 360
    elif dh < -180: dh += 360
    dHp = 2 * math.sqrt(C1p * C2p) * math.sin(math.radians(dh / 2))
    Lbp, Cbp = (L1 + L2) / 2, (C1p + C2p) / 2
    hs = h1p + h2p
    if C1p * C2p == 0: hbp = hs
    elif abs(h1p - h2p) <= 180: hbp = hs / 2
    else: hbp = (hs + 360) / 2 if hs < 360 else (hs - 360) / 2
    T = 1 - 0.17 * math.cos(math.radians(hbp - 30)) + 0.24 * math.cos(math.radians(2 * hbp)) + 0.32 * math.cos(math.radians(3 * hbp + 6)) - 0.2 * math.cos(math.radians(4 * hbp - 63))
    dth = 30 * math.exp(-(((hbp - 275) / 25) ** 2))
    Rc = 2 * math.sqrt(Cbp ** 7 / (Cbp ** 7 + 25 ** 7))
    Sl = 1 + 0.015 * (Lbp - 50) ** 2 / math.sqrt(20 + (Lbp - 50) ** 2)
    Sc, Sh = 1 + 0.045 * Cbp, 1 + 0.015 * Cbp * T
    Rt = -math.sin(math.radians(2 * dth)) * Rc
    return math.sqrt((dLp / Sl) ** 2 + (dCp / Sc) ** 2 + (dHp / Sh) ** 2 + Rt * (dCp / Sc) * (dHp / Sh))


def palette(img):
    small = img.resize((200, 430), Image.Resampling.LANCZOS)
    q = small.quantize(8, method=Image.Quantize.MEDIANCUT)
    pal = np.array(q.getpalette()[:24], np.float32).reshape(8, 3) / 255
    counts = np.bincount(np.asarray(q).reshape(-1), minlength=8)
    return pal, counts


def road_mask():
    m = Image.new('L', (W, H), 0)
    ImageDraw.Draw(m).line(ROAD, fill=255, width=110)
    return np.asarray(m) > 0


def region_L(L, boxes):
    return float(np.mean([L[y0:y1, x0:x1].mean() for x0, y0, x1, y1 in boxes]))


def measure(game_path, concept_path):
    g = Image.open(game_path).convert('RGB').resize((W, H), Image.Resampling.LANCZOS)
    c = Image.open(concept_path).convert('RGB')
    if c.size != (W, H):
        c = c.resize((W, H), Image.Resampling.LANCZOS)
    ga, ca = np.asarray(g, np.float32) / 255, np.asarray(c, np.float32) / 255
    gl, cl = lab(ga), lab(ca)
    out = {}
    gm, cm = ga.reshape(-1, 3).mean(0), ca.reshape(-1, 3).mean(0)
    out['mean_hex'] = {'game': '#%02x%02x%02x' % tuple(int(v * 255 + .5) for v in gm), 'concept': '#%02x%02x%02x' % tuple(int(v * 255 + .5) for v in cm)}
    glm, clm = lab(gm[None])[0], lab(cm[None])[0]
    out['mean_dE76'] = round(de76(glm, clm), 2)
    out['mean_dE2000'] = round(de2000(glm, clm), 2)
    gp, gc = palette(g)
    cp, cc = palette(c)
    gpl, cpl = lab(gp), lab(cp)
    rows = []
    for i in range(8):
        d = [de76(gpl[i], cpl[j]) for j in range(8)]
        j = int(np.argmin(d))
        rows.append({'game': '#%02x%02x%02x' % tuple(int(v * 255 + .5) for v in gp[i]), 'share': round(float(gc[i] / gc.sum()), 3),
                     'nearest': '#%02x%02x%02x' % tuple(int(v * 255 + .5) for v in cp[j]), 'dE76': round(d[j], 2)})
    out['palette'] = rows
    out['palette_max_dE76'] = max(r['dE76'] for r in rows)
    out['concept_palette'] = ['#%02x%02x%02x' % tuple(int(v * 255 + .5) for v in p) for p in cp]
    # сетка 4×8 по L*
    def grid(L):
        return np.array([[L[r * H // 8:(r + 1) * H // 8, k * W // 4:(k + 1) * W // 4].mean() for k in range(4)] for r in range(8)])
    gg, cg = grid(gl[..., 0]), grid(cl[..., 0])
    out['grid_L_game'] = np.round(gg, 1).tolist()
    out['grid_L_concept'] = np.round(cg, 1).tolist()
    out['grid_corr'] = round(float(np.corrcoef(gg.reshape(-1), cg.reshape(-1))[0, 1]), 3)
    rm = road_mask()
    for name, L in (('game', gl[..., 0]), ('concept', cl[..., 0])):
        road, grass, shade = float(L[rm].mean()), region_L(L, GRASS), region_L(L, SHADE)
        out[f'rules_{name}'] = {'road_L': round(road, 1), 'grass_L': round(grass, 1), 'shade_L': round(shade, 1),
                                'road_lighter_than_grass': road > grass, 'shade_darker_than_grass': shade < grass}
    out['pass'] = {'mean_dE76<=5': out['mean_dE76'] <= 5, 'palette_each_dE76<=8': out['palette_max_dE76'] <= 8,
                   'same_drawing': out['rules_game']['road_lighter_than_grass'] and out['rules_game']['shade_darker_than_grass']}
    return out, g, c


def splice(g, c, path, label):
    gap = 16
    im = Image.new('RGB', (W * 2 + gap, H + 60), (24, 22, 20))
    im.paste(g, (0, 60)); im.paste(c, (W + gap, 60))
    d = ImageDraw.Draw(im)
    try:
        f = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 30)
    except OSError:
        f = ImageFont.load_default()
    a, b = (label.split('|') + ['', ''])[:2]
    d.text((20, 14), a.strip(), fill=(235, 225, 205), font=f)
    d.text((W + gap + 20, 14), b.strip(), fill=(235, 225, 205), font=f)
    im.save(path, quality=88)


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('game'); ap.add_argument('concept')
    ap.add_argument('--splice'); ap.add_argument('--label', default='сборка | концепт')
    a = ap.parse_args()
    res, g, c = measure(a.game, a.concept)
    if a.splice:
        splice(g, c, a.splice, a.label)
        res['splice'] = a.splice
    print(json.dumps(res, ensure_ascii=False, indent=1))
