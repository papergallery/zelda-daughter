#!/usr/bin/env python3
"""D-25: промежуточные кадры — из ролика видео-модели или листа фаз GPT Image — в спрайты цикла, и замер плавности.

    d25_frames.py video CLIP.mp4 OUT [--n 8] [--skip 12] [--ref heroine_side_0.png] [--height-m 1.65]
        ролик (зелёный фон) → кадры → ключ зелёного + despill → один цикл (период — по похожести силуэтов) → n кадров
        поровну по времени цикла → общий масштаб (320 px/м по росту первого кадра ролика = стоящая фигура) → общий холст
        В КООРДИНАТАХ РОЛИКА (камера неподвижна: кадры по отдельности не центрируются).
    d25_frames.py sheet SHEET.png OUT [--n 8] [--stand 0]
        лист фаз (серый фон, маска keymask рядом): фигура stand — стоит (опора масштаба), остальные — фазы цикла;
        по горизонтали кадры выравниваются по корпусу (центр бёдер), по вертикали — по общей линии земли листа.
    d25_frames.py metrics DIR [--label L]          # замер готовых кадров DIR/*.png (общий холст)
    d25_frames.py strip DIR OUT.png [--gif OUT.gif] # склейка на светлом/тёмном + GIF (12 к/с)

Замер (на холсте 320 px/м): «опора» — (1) разброс по X центра корпуса (строки 30–55 % роста), (2) разброс по Y нижней
строки силуэта в кадрах с опорой (низ ниже медианы + 3 px не считается полётом); IoU силуэтов (альфа > 0,5) соседних
кадров, включая последний → первый (цикл). Критерий D-25: опора ≤ 2 px, IoU соседних ≥ 0,8.
"""
import argparse, glob, json, pathlib, subprocess, sys, tempfile

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

HERE = pathlib.Path(__file__).parent
sys.path.insert(0, str(HERE))
import sprite_build as sb  # noqa: E402

PPM = sb.CFG['ppm']
GREEN_LO_K, GREEN_HI_K = 0.25, 0.70  # доли «зелёности» фона: ниже — фигура, выше — фон


def green_key(img):
    """RGB float 0..1 → RGBA. Альфа — по «зелёности» gness = g − max(r, b): у фона ≈ 0,44 (#00B140), у фигуры ≤ 0,05
    (оливковый рюкзак ≈ 0,02); переход — между 25 % и 70 % зелёности фона (по рамке кадра). Мелкие острова — прочь.
    Despill: на кромке (не в глубине фигуры) g не выше max(r, b) — зелёный ореол на акварели снимается, цвет внутри
    фигуры не трогается."""
    r, g, b = img[..., 0], img[..., 1], img[..., 2]
    gness = g - np.maximum(r, b)
    border = np.concatenate([gness[:8].ravel(), gness[-8:].ravel(), gness[:, :8].ravel(), gness[:, -8:].ravel()])
    bg = float(np.median(border))
    lo, hi = GREEN_LO_K * bg, GREEN_HI_K * bg
    a = np.clip((hi - gness) / (hi - lo), 0, 1)
    solid = ndimage.binary_fill_holes(a > 0.5)
    lab, n = ndimage.label(solid)
    if n:
        sizes = ndimage.sum(solid, lab, index=np.arange(1, n + 1))
        keep = np.zeros(n + 1, bool); keep[1:] = sizes >= max(300, 0.02 * sizes.max())
        solid = keep[lab]
    a = np.where(ndimage.binary_dilation(solid, iterations=2), a, 0)
    core = ndimage.binary_erosion(a > 0.95, iterations=3)
    out = img.copy()
    spill = np.maximum(r, b)
    out[..., 1] = np.where(~core & (g > spill), spill, g)
    # цвет кромки: убрать примесь фона (C = a·F + (1 − a)·B → F)
    bgc = np.median(img[a < 0.02], axis=0) if (a < 0.02).any() else np.array([0, 0.69, 0.25], np.float32)
    edge = (a > 0.05) & (a < 0.95)
    f = (out - (1 - a[..., None]) * bgc) / np.maximum(a, 0.05)[..., None]
    out = np.where(edge[..., None], np.clip(f, 0, 1), out)
    out[..., 1] = np.where(edge, np.minimum(out[..., 1], np.maximum(out[..., 0], out[..., 2]) + 0.02), out[..., 1])
    return np.dstack([out, a]).astype(np.float32)


def silhouette(p):
    return p[..., 3] > 0.5


def iou(a, b):
    i = np.logical_and(a, b).sum(); u = np.logical_or(a, b).sum()
    return float(i / u) if u else 1.0


def read_frames(clip, tmp):
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(clip), str(pathlib.Path(tmp) / 'f%04d.png')], check=True)
    fs = sorted(glob.glob(str(pathlib.Path(tmp) / 'f*.png')))
    return [np.asarray(Image.open(f).convert('RGB')).astype(np.float32) / 255 for f in fs]


def sim(p, q):
    """Похожесть кадров: IoU силуэтов × (1 − средняя разница цвета в пересечении). Только по силуэту полцикла (смена
    ног) почти неотличим от полного — ближняя и дальняя нога различаются тоном, это и ловит цвет."""
    a, b = p[..., 3] > 0.5, q[..., 3] > 0.5
    inter = a & b
    if not inter.any():
        return 0.0
    d = float(np.abs(p[..., :3][inter] - q[..., :3][inter]).mean())
    return iou(a, b) * (1 - 2 * d)


def find_period(fr, skip, lo=6, hi=40):
    """Период цикла: сдвиг T с наибольшей средней похожестью кадров t и t+T (кратные периоды дают то же — берём короткий,
    если длинный не лучше заметно)."""
    best = None
    for T in range(lo, min(hi, (len(fr) - skip) // 2) + 1):
        m = float(np.mean([sim(fr[t], fr[t + T]) for t in range(skip, len(fr) - T, 2)]))
        if best is None or m > best[1] + 0.01:
            best = (T, m)
    return best


def best_start(fr, skip, T):
    """Начало цикла: t, где кадр t ближе всего к кадру t+T (стык цикла незаметен)."""
    return max((sim(fr[t], fr[t + T]), t) for t in range(skip, len(fr) - T))[1]


def place(frames, out, names=None):
    """Общий холст: объединённая рамка всех кадров (координаты сохраняются), pivot — середина по X на нижней строке."""
    al = np.stack([f[..., 3] for f in frames])
    rows = np.where(al.max(axis=(0, 2)) > 0.02)[0]; cols = np.where(al.max(axis=(0, 1)) > 0.02)[0]
    y0, y1, x0, x1 = rows[0], rows[-1] + 1, cols[0], cols[-1] + 1
    out = pathlib.Path(out); out.mkdir(parents=True, exist_ok=True)
    for old in out.glob('*.png'):
        old.unlink()
    for i, f in enumerate(frames):
        name = names[i] if names else f'{i + 1:02d}'
        sb.to_img(f[y0:y1, x0:x1]).save(out / f'{name}.png', optimize=True)
    return out


def hip_x(p):
    s_ = p[..., 3] > 0.5; rows = np.where(s_.any(1))[0]; h = rows[-1] - rows[0]
    return float(np.mean(np.where(s_[rows[0] + int(0.3 * h): rows[0] + int(0.55 * h)])[1]))


def align(frames):
    """Снять ведение камеры ролика: (1) по X — центр корпуса каждого кадра к общему среднему (у бега на месте корпус над
    точкой опоры почти неподвижен, а ролик «плывёт» на 8–10 px); (2) по Y — в кадрах с опорой нижняя точка силуэта к
    общей линии земли (медиана), в кадрах полёта — сдвиг как у соседних опорных (подскок сохраняется)."""
    hx = [hip_x(p) for p in frames]
    mx = float(np.mean(hx))
    low = [int(np.where((p[..., 3] > 0.5).any(1))[0][-1]) for p in frames]
    ground = int(np.median(low))
    stance = [l >= ground - 3 for l in low]
    dy = [ground - l if st else None for l, st in zip(low, stance)]
    n = len(frames)
    for i in range(n):  # полёт: сдвиг — среднее соседних опорных
        if dy[i] is None:
            nb = [dy[(i + d) % n] for d in (-1, 1, -2, 2) if dy[(i + d) % n] is not None]
            dy[i] = int(round(np.mean(nb[:2]))) if nb else 0
    return [ndimage.shift(p, (dy[i], mx - hx[i], 0), order=1, mode='constant') for i, p in enumerate(frames)]


def scale_all(frames, s):
    return [sb.scaled(f, s) for f in frames]


def cmd_video(a):
    with tempfile.TemporaryDirectory() as tmp:
        raw = read_frames(a.clip, tmp)
    rgba = [green_key(f) for f in raw]
    sils = [silhouette(p) for p in rgba]
    small = [sb.scaled(p, 0.35) for p in rgba]  # поиск периода — на уменьшенных кадрах (быстрее, шум кромки не мешает)
    T, m = (a.period, float(np.mean([sim(small[t], small[t + a.period]) for t in range(a.skip, len(small) - a.period)]))) \
        if a.period else find_period(small, a.skip)
    t0 = best_start(small, a.skip, T)
    print(f'кадров {len(rgba)}, период {T} кадров ({T / a.fps:.2f} с при {a.fps} к/с), похожесть через период {m:.3f}, начало {t0}')
    # масштаб: стоящая фигура первого кадра ролика = рост паспорта
    idx = [t0 + int(round(k * T / a.n)) for k in range(a.n)]
    if a.cycle_height_px:  # ролик не начинается со стойки — масштаб по медиане роста кадров цикла
        hs = [np.ptp(np.where(sils[i].any(1))[0]) + 1 for i in idx]
        s = a.cycle_height_px / float(np.median(hs))
    else:  # первый кадр ролика — стоящая фигура = рост паспорта
        t, b = sb.body_top_bottom(rgba[0][..., 3])
        s = a.height_m * PPM / (b - t + 1)
    pick = [rgba[i] for i in idx]
    if a.detrend:  # дрейф фигуры за цикл: кадры t0 и t0+T — одна фаза, их корпус должен совпасть; сдвиг кадра k — линейная доля
        def hipx(p):
            s_ = p[..., 3] > 0.5; rows = np.where(s_.any(1))[0]; h = rows[-1] - rows[0]
            return float(np.mean(np.where(s_[rows[0] + int(0.3 * h): rows[0] + int(0.55 * h)])[1]))
        dx, dy = hipx(rgba[t0 + T]) - hipx(rgba[t0]), 0
        pick = [ndimage.shift(p, (0, -dx * (i - t0) / T, 0), order=1, mode='constant') for p, i in zip(pick, idx)]
        print(f'дрейф за цикл {dx:.1f} px ролика — снят линейно')
    if a.color_ref:  # цвет — к кадру 0 (утверждённый спрайт): ОДНО линейное преобразование на весь цикл (по кадру — мерцание)
        ref = np.asarray(Image.open(a.color_ref).convert('RGBA')).astype(np.float32) / 255
        rp = ref[..., :3][ref[..., 3] > 0.5]
        allp = np.concatenate([p[..., :3][p[..., 3] > 0.5] for p in pick])
        mu, sd, rmu, rsd = allp.mean(0), allp.std(0) + 1e-6, rp.mean(0), rp.std(0)
        for p in pick:
            p[..., :3] = np.clip((p[..., :3] - mu) / sd * rsd + rmu, 0, 1)
    pick = [sb.palette_soft(p, sb.CFG['palette_mix']) for p in scale_all(pick, s)]
    if a.align:
        pick = align(pick)
    out = place(pick, a.out)
    meta = {'clip': str(a.clip), 'align': bool(a.align), 'frames': len(rgba), 'period': T, 'sim_period': round(m, 3), 'start': t0, 'picked': idx,
            'scale': round(s, 4), 'n': a.n}
    (out / 'source.json').write_text(json.dumps(meta, ensure_ascii=False, indent=1), encoding='utf-8')
    print('кадры', idx, '→', out)
    metrics(out, a.label or out.name)


def cmd_sheet(a):
    src = pathlib.Path(a.sheet)
    rgba = sb.cutout(src)
    figs = sb.figures(rgba)
    crops = [sb.only_figure(rgba, f) for f in figs]
    t, b = sb.body_top_bottom(crops[a.stand][..., 3])
    s = a.height_m * PPM / (b - t + 1)
    ground = max(f[3] for f in figs)  # общая линия земли листа (нижняя строка рамок)
    phases = [i for i in range(len(figs)) if i != a.stand][:a.n]
    frames = []
    for i in phases:
        x0, y0, x1, y1, _ = figs[i]
        p = sb.palette_soft(sb.scaled(crops[i], s), sb.CFG['palette_mix'])
        al = p[..., 3]
        h = al.shape[0]
        rows = np.where((al > 0.5).any(1))[0]
        band = al[rows[0] + int(0.30 * (rows[-1] - rows[0])): rows[0] + int(0.55 * (rows[-1] - rows[0]))] > 0.5
        cx = float(np.mean(np.where(band)[1]))
        frames.append((p, cx, int(round((ground - y1) * s))))  # (кадр, центр корпуса X, отступ низа рамки от земли)
    W = int(max(max(cx, p.shape[1] - cx) for p, cx, _ in frames) * 2) + 8
    H = max(p.shape[0] + off for p, _, off in frames) + 4
    canv = []
    for p, cx, off in frames:
        c = np.zeros((H, W, 4), np.float32)
        x = int(round(W / 2 - cx)); y = H - off - p.shape[0]
        c[y:y + p.shape[0], x:x + p.shape[1]] = p
        canv.append(c)
    out = place(canv, a.out)
    (out / 'source.json').write_text(json.dumps({'sheet': str(src), 'figs': len(figs), 'stand': a.stand, 'phases': phases,
                                                 'scale': round(s, 4)}, ensure_ascii=False, indent=1), encoding='utf-8')
    print('фигур', len(figs), 'фазы', phases, '→', out)
    metrics(out, a.label or out.name)


def load_dir(d):
    fs = sorted(pathlib.Path(d).glob('[0-9]*.png'))
    return fs, [np.asarray(Image.open(f).convert('RGBA')).astype(np.float32) / 255 for f in fs]


def metrics(d, label=''):
    fs, fr = load_dir(d)
    sils = [silhouette(p) for p in fr]
    hip, low, heights = [], [], []
    for s in sils:
        rows = np.where(s.any(1))[0]
        h = rows[-1] - rows[0]
        band = s[rows[0] + int(0.30 * h): rows[0] + int(0.55 * h)]
        hip.append(float(np.mean(np.where(band)[1])))
        low.append(int(rows[-1])); heights.append(int(h))
    med = np.median(low)
    stance = [l for l in low if l >= med - 3]
    ious = [iou(sils[i], sils[(i + 1) % len(sils)]) for i in range(len(sils))]
    res = {'label': label, 'frames': len(fr), 'hip_x_range_px': round(max(hip) - min(hip), 1),
           'ground_y_range_px': int(max(stance) - min(stance)), 'flight_frames': len(low) - len(stance),
           'iou_adjacent_mean': round(float(np.mean(ious)), 3), 'iou_adjacent_min': round(float(min(ious)), 3),
           'iou_adjacent': [round(x, 3) for x in ious], 'height_px_range': [min(heights), max(heights)]}
    (pathlib.Path(d) / 'metrics.json').write_text(json.dumps(res, ensure_ascii=False, indent=1), encoding='utf-8')
    print(json.dumps({k: v for k, v in res.items() if k != 'iou_adjacent'}, ensure_ascii=False))
    return res


def cmd_strip(a):
    fs, fr = load_dir(a.dir)
    ims = [sb.to_img(p) for p in fr]
    k = a.k
    w, h = int(ims[0].width * k), int(ims[0].height * k)
    o = Image.new('RGB', (w * len(ims), h * 2), sb.LIGHT)
    dr = ImageDraw.Draw(o)
    dr.rectangle([0, h, o.width, 2 * h], fill=sb.DARK)
    for i, im in enumerate(ims):
        im = im.resize((w, h), Image.Resampling.LANCZOS)
        o.paste(im, (i * w, 0), im); o.paste(im, (i * w, h), im)
        dr.line([i * w, 0, i * w, 2 * h], fill=(150, 140, 120))
    o.save(a.png, optimize=True); print(a.png, o.size)
    if a.gif:
        frames = []
        for im in ims:
            bg = Image.new('RGBA', im.size, sb.LIGHT + (255,)); bg.alpha_composite(im)
            frames.append(bg.convert('RGB').resize((w, h)).quantize(128))
        frames[0].save(a.gif, save_all=True, append_images=frames[1:], duration=int(1000 / a.fps), loop=0, optimize=True)
        print(a.gif)


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest='cmd', required=True)
    v = sub.add_parser('video'); v.add_argument('clip'); v.add_argument('out')
    v.add_argument('--n', type=int, default=8); v.add_argument('--period', type=int); v.add_argument('--detrend', action='store_true'); v.add_argument('--align', action='store_true'); v.add_argument('--skip', type=int, default=12); v.add_argument('--fps', type=float, default=24)
    v.add_argument('--height-m', type=float, default=1.65); v.add_argument('--color-ref'); v.add_argument('--label')
    v.add_argument('--cycle-height-px', type=float, help='медиана роста кадров цикла, px при 320 px/м (если ролик не со стойки)')
    s = sub.add_parser('sheet'); s.add_argument('sheet'); s.add_argument('out'); s.add_argument('--n', type=int, default=8)
    s.add_argument('--stand', type=int, default=0); s.add_argument('--height-m', type=float, default=1.65); s.add_argument('--label')
    m = sub.add_parser('metrics'); m.add_argument('dir'); m.add_argument('--label', default='')
    t = sub.add_parser('strip'); t.add_argument('dir'); t.add_argument('png'); t.add_argument('--gif')
    t.add_argument('--k', type=float, default=0.5); t.add_argument('--fps', type=float, default=12)
    a = ap.parse_args()
    {'video': cmd_video, 'sheet': cmd_sheet, 'metrics': lambda a: metrics(a.dir, a.label), 'strip': cmd_strip}[a.cmd](a)


if __name__ == '__main__':
    main()
