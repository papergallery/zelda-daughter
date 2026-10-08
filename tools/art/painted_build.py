#!/usr/bin/env python3
"""D-28: рисованные объекты мира (деревья, камни, пни, брёвна, кольцо костра) и заливки земли — пилот «уголок f1» (ADR-0010).

Листы — FLUX.2 Klein 4B на ПК автора (tools/art/klein.py, 0 ₽), промпты — tools/art/painted.json + блок стиля
docs/demo/sprites/style-bible.md дословно (tools/art/style.py). Сырые листы — в рабочей папке ($ZD_ART_WORK, вне git); seed,
промпт и время — рядом в <имя>-s<seed>.json и в docs/demo/sprites/painted-sources.json.

    painted_build.py gen trees [rocks …]       # листы → WORK/pt-<лист>-s<seed>.png (+ маска BiRefNet, + .json)
    painted_build.py split WORK/pt-trees-s11.png   # превью нарезки с номерами фигур (выбрать лучший seed и фигуры → painted.json figs)
    painted_build.py build                     # атлас Assets/Art/Painted/painted.png + painted.json, заливки земли, лист просмотра

Приёмы (ref2game, MIT, github.com/studioigor/ref2game — references/art.md §6–8, scripts/slice.py, prep.py; код свой):
- однотипные куски — одним листом с широкими зазорами (общий свет, палитра и линия), нарезка по связным областям альфы;
- точка опоры — середина нижней непрозрачной строки (основание); рост в метрах задаёт масштаб (ppm);
- цвет под прозрачными пикселями продолжен от края (без тёмной каймы при mip и фильтрации); обрезка по альфе;
- свет, тени, огонь, дым, туман в картинку не запекаются (стиль-библия): всё это делает движок;
- заливка земли — бесшовная: сдвиг на ½ и смешивание по кресту швов (references/art.md §6, «Texture for a shader»).
"""
import json, os, pathlib, subprocess, sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import style  # noqa: E402
import sprite_build as sb  # noqa: E402  (cutout, figures, only_figure, scaled, to_img)

CFG_PATH = HERE / 'painted.json'
CFG = json.loads(CFG_PATH.read_text(encoding='utf-8'))
OUT = ROOT / 'ZeldaDaughter/Assets/Art/Painted'
DOCS = ROOT / 'docs/demo/sprites'
SOURCES = DOCS / 'painted-sources.json'
PAD = 6


def work():
    w = pathlib.Path(os.environ.get('ZD_ART_WORK') or '.') / 'd28'
    w.mkdir(parents=True, exist_ok=True)
    return w


def prompt_of(sheet):
    s = CFG['sheets'][sheet]
    if s.get('texture'):
        return '\n\n'.join([s['prompt'], style.STYLE])
    parts = [s['prompt']]
    if sheet in ('trees', 'rocks', 'props'):
        parts.append(CFG['facets'])
    parts += [CFG['camera'], style.STYLE + ' ' + style.BG_GREY]
    return '\n\n'.join(parts)


def cmd_gen(*sheets):
    wd = work()
    for sheet in sheets:
        s = CFG['sheets'][sheet]
        pf = wd / f'pt-{sheet}.prompt.txt'
        pf.write_text(prompt_of(sheet), encoding='utf-8')
        subprocess.run([sys.executable, str(HERE / 'klein.py'), '--out', str(wd), '--name', f'pt-{sheet}', '--prompt-file', str(pf),
                        '--w', str(s['w']), '--h', str(s['h']), '--seeds', s.get('seeds', '1')], check=True)


# ---------- нарезка ----------

def sheet_figures(png):
    """Фигуры листа по порядку чтения: ряды сверху вниз (по перекрытию по высоте), в ряду — слева направо."""
    rgba = sb.cutout(png)
    figs = sb.figures(rgba)
    figs.sort(key=lambda f: f[1])
    rows = []
    for f in figs:
        cy = (f[1] + f[3]) / 2
        if rows and rows[-1][0][1] <= cy <= rows[-1][0][3]:
            rows[-1].append(f)
        else:
            rows.append([f])
    return rgba, [f for r in rows for f in sorted(r, key=lambda f: f[0])]


def cmd_split(*pngs):
    for src in pngs:
        src = pathlib.Path(src)
        rgba, figs = sheet_figures(src)
        im = Image.new('RGB', (rgba.shape[1], rgba.shape[0]), sb.LIGHT)
        im.paste(sb.to_img(rgba), (0, 0), sb.to_img(rgba))
        dr = ImageDraw.Draw(im)
        for k, (x0, y0, x1, y1, _) in enumerate(figs):
            dr.rectangle([x0, y0, x1, y1], outline=(200, 40, 40), width=3)
            dr.text((x0 + 6, y0 + 4), str(k), fill=(200, 40, 40))
        out = src.with_name(src.stem + '.split.jpg')
        im.save(out, quality=85)
        print(out, len(figs), 'figures')


def bleed(rgba):
    a = rgba[..., 3]
    solid = a > 0.05
    if not solid.any():
        return rgba
    idx = ndimage.distance_transform_edt(~solid, return_distances=False, return_indices=True)
    out = rgba.copy()
    out[..., :3] = rgba[idx[0], idx[1], :3]
    return out


def hull_of(alpha, max_vertices=10):
    """Выпуклая оболочка альфы > 0,1, упрощённая до ≤ max_vertices вершин наружу (меш карточки меньше квада — меньше пустого alpha clip).
    Координаты — доли ширины/высоты, v снизу вверх."""
    from scipy.spatial import ConvexHull
    h, w = alpha.shape
    ys, xs = np.nonzero(alpha > 0.1)
    pts = np.concatenate([np.stack([xs, ys], 1), np.stack([xs + 1, ys + 1], 1), np.stack([xs + 1, ys], 1), np.stack([xs, ys + 1], 1)]).astype(float)
    poly = [tuple(p) for p in pts[ConvexHull(pts).vertices]]

    def inter(p1, p2, p3, p4):
        d = (p1[0] - p2[0]) * (p3[1] - p4[1]) - (p1[1] - p2[1]) * (p3[0] - p4[0])
        if abs(d) < 1e-9:
            return None
        t = ((p1[0] - p3[0]) * (p3[1] - p4[1]) - (p1[1] - p3[1]) * (p3[0] - p4[0])) / d
        return (p1[0] + t * (p2[0] - p1[0]), p1[1] + t * (p2[1] - p1[1]))

    while len(poly) > max_vertices:
        n = len(poly)
        best = None
        for i in range(n):
            a, b, c, d = poly[i - 1], poly[i], poly[(i + 1) % n], poly[(i + 2) % n]
            q = inter(a, b, c, d)
            if q is None or not (-PAD < q[0] < w + PAD and -PAD < q[1] < h + PAD):
                continue
            # q снаружи ребра b–c (продолжения соседних рёбер сходятся наружу)
            cross = (c[0] - b[0]) * (q[1] - b[1]) - (c[1] - b[1]) * (q[0] - b[0])
            area = abs(cross) / 2
            if best is None or area < best[0]:
                best = (area, i, q)
        if best is None:
            break
        _, i, q = best
        nxt = (i + 1) % n
        poly[i] = q
        del poly[nxt]
    mx, my = (PAD - 1) / w, (PAD - 1) / h
    return [[round(min(max(x / w, -mx), 1 + mx), 4), round(min(max(1 - y / h, -my), 1 + my), 4)] for x, y in poly]


def seamless(img, blend=0.18):
    """Бесшовная заливка: картинка, сдвинутая на ½, смешивается с исходной по кресту швов (шов исходника уходит в середину и
    закрывается сдвинутой копией, у которой там середина картинки)."""
    a = np.asarray(img.convert('RGB')).astype(np.float32)
    h, w = a.shape[:2]
    sh = np.roll(a, (h // 2, w // 2), axis=(0, 1))
    yy, xx = np.mgrid[0:h, 0:w]
    dx = np.abs(xx - w / 2) / (w * blend)
    dy = np.abs(yy - h / 2) / (h * blend)
    m = np.clip(1 - np.minimum(dx, dy), 0, 1)           # 1 на кресте швов сдвинутой картинки… нам нужна исходная там, где шов у сдвинутой
    # сдвинутая копия: её швы — на кресте через середину; исходная: её швы — по краям. На кресте берём исходную, по краям — сдвинутую.
    m = m ** 0.8
    out = sh * (1 - m[..., None]) + a * m[..., None]
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


_REF_LAB = {}


def ref_lab(name):
    """Среднее и разброс Lab кропа f1 (color_refs)."""
    if name not in _REF_LAB:
        box = CFG['color_refs'][name]
        im = np.asarray(Image.open(ROOT / 'docs/concept/world-b/f1.jpg').convert('RGB').crop(tuple(box))).astype(np.float32) / 255
        lab = sb._lab(im).reshape(-1, 3)
        _REF_LAB[name] = (lab.mean(0), lab.std(0))
    return _REF_LAB[name]


def lab_transfer(rgb, weight, ref):
    """Частичный перенос цвета к кропу f1 (ref2game art.md §4: mean/std в Lab, сила k < 1 — свои контрасты куска сохраняются).
    weight — вес пикселя (альфа); ref — [имя кропа, k]."""
    if not ref:
        return rgb
    name, k = ref
    lab = sb._lab(np.clip(rgb, 0, 1))
    w = weight.reshape(-1) > 0.5
    flat = lab.reshape(-1, 3)[w]
    m, s = flat.mean(0), flat.std(0) + 1e-6
    rm, rs = ref_lab(name)
    tm, ts = m + (rm - m) * k, s * (rs / s) ** k
    out = (lab - m) / s * ts + tm + np.array(CFG.get('grade_offset', {}).get('lab', [0, 0, 0]), np.float32)
    return sb._lab_inv(out).astype(np.float32)


def cmd_build():
    ppm = CFG['ppm']
    wd = work()
    items = []
    src_meta = {'_source': 'D-28: листы рисованных объектов (tools/art/painted_build.py gen) — FLUX.2 Klein 4B на ПК, 0 ₽; выбор seed — painted.json', 'sheets': {}}
    cache = {}
    for sheet, s in CFG['sheets'].items():
        if s.get('texture') or not s.get('figs'):
            continue
        for k, pick in enumerate(s['figs']):
            if not pick:
                continue
            pid, height_m, kind = pick[:3]
            seed = pick[3] if len(pick) > 3 else s['pick']
            png = wd / f'pt-{sheet}-s{seed}.png'
            if png not in cache:
                meta = json.loads(png.with_suffix('.json').read_text(encoding='utf-8'))
                src_meta['sheets'][png.name] = {q: meta[q] for q in ('model', 'seed', 'w', 'h', 'steps', 'seconds', 'prompt')}
                cache[png] = sheet_figures(png)
            rgba, figs = cache[png]
            if k >= len(figs):
                print(f'WARN {sheet}: нет фигуры {k} в {png.name}')
                continue
            p = sb.only_figure(rgba, figs[k])
            p[..., :3] = lab_transfer(p[..., :3], p[..., 3], s.get('ref'))
            rows = np.where((p[..., 3] > 0.5).any(axis=1))[0]
            cols = np.where((p[..., 3] > 0.05).any(axis=0))[0]
            p = p[:rows[-1] + 1, cols[0]:cols[-1] + 1]
            top = np.where((p[..., 3] > 0.05).any(axis=1))[0][0]
            p = p[top:]
            # основание: середина непрозрачного в нижних 6 % высоты (ствол дерева, низ камня)
            base_rows = p[int(p.shape[0] * 0.94):, :, 3] > 0.5
            xs = np.nonzero(base_rows.any(axis=0))[0]
            pivot = float((xs[0] + xs[-1] + 1) / 2 / p.shape[1]) if len(xs) else 0.5
            p = sb.scaled(p, height_m * ppm / p.shape[0])
            items.append((pid, kind, p, pivot))
    items.sort(key=lambda t: -t[2].shape[0])
    W = 2048
    x = y = PAD
    shelf = 0
    place = {}
    for pid, kind, p, pivot in items:
        h, w = p.shape[:2]
        if x + w + PAD > W:
            x, y, shelf = PAD, y + shelf + 2 * PAD, 0
        place[pid] = (x, y, w, h)
        x += w + 2 * PAD
        shelf = max(shelf, h)
    H = 1 << int(np.ceil(np.log2(y + shelf + PAD)))
    atlas = np.zeros((H, W, 4), np.float32)
    for pid, kind, p, pivot in items:
        x0, y0, w, h = place[pid]
        atlas[y0:y0 + h, x0:x0 + w] = p
    atlas = bleed(atlas)
    OUT.mkdir(parents=True, exist_ok=True)
    sb.to_img(atlas).save(OUT / 'painted.png', optimize=True)
    meta = {'_source': 'D-28: tools/art/painted_build.py build — атлас рисованных объектов; rect в px от нижнего левого угла (UV Unity), size_m — ширина и высота '
                       'в метрах, pivot_x — доля ширины у основания, hull — выпуклый контур (доли, v снизу).',
            'texture': 'Assets/Art/Painted/painted.png', 'size': [W, H], 'ppm': ppm, 'sprites': {}}
    for pid, kind, p, pivot in items:
        x0, y0, w, h = place[pid]
        meta['sprites'][pid] = {'rect': [x0, H - y0 - h, w, h], 'size_m': [round(w / ppm, 3), round(h / ppm, 3)], 'pivot_x': round(pivot, 4),
                                'kind': kind, 'hull': hull_of(p[..., 3])}
    meta['sprites'] = dict(sorted(meta['sprites'].items()))
    # заливки земли
    meta['ground'] = {}
    for sheet, s in CFG['sheets'].items():
        if not s.get('texture') or 'pick' not in s:
            continue
        png = wd / f'pt-{sheet}-s{s["pick"]}.png'
        m = json.loads(png.with_suffix('.json').read_text(encoding='utf-8'))
        src_meta['sheets'][png.name] = {k: m[k] for k in ('model', 'seed', 'w', 'h', 'steps', 'seconds', 'prompt')}
        rgb = np.asarray(Image.open(png).convert('RGB')).astype(np.float32) / 255
        rgb = lab_transfer(rgb, np.ones(rgb.shape[:2], np.float32), s.get('ref'))
        tex = seamless(Image.fromarray((np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8)))
        name = sheet.replace('ground_', 'ground-') + '.png'
        tex.save(OUT / name, optimize=True)
        meta['ground'][sheet.replace('ground_', '')] = {'texture': f'Assets/Art/Painted/{name}', 'tile_m': s.get('tile_m', 6)}
    (OUT / 'painted.json').write_text(json.dumps(meta, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    SOURCES.write_text(json.dumps(src_meta, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    sheet_png(items)
    print(OUT / 'painted.png', (W, H), 'sprites', len(items))


def sheet_png(items):
    """Лист просмотра: все куски на светлой и тёмной полосе, линии через 0,5 м, рядом героиня по росту 1,65 м (палка)."""
    k = 0.5
    ppm = CFG['ppm']
    hmax = max(p.shape[0] for _, _, p, _ in items)
    bands = []
    for bg in (sb.LIGHT, sb.DARK):
        Wd = int(sum(p.shape[1] * k + 16 for _, _, p, _ in items)) + 60
        Hd = int(hmax * k) + 40
        im = Image.new('RGB', (Wd, Hd), bg)
        dr = ImageDraw.Draw(im)
        gy = Hd - 16
        for m in np.arange(0, 8.0, 0.5):
            yy = gy - int(m * ppm * k)
            if yy >= 0:
                dr.line([0, yy, Wd, yy], fill=(120, 110, 95) if bg == sb.LIGHT else (95, 88, 78), width=2 if m % 1 == 0 else 1)
        dr.line([20, gy, 20, gy - int(1.65 * ppm * k)], fill=(40, 90, 160), width=6)
        xx = 44
        for pid, _, p, _ in sorted(items, key=lambda t: t[0]):
            img = sb.to_img(p)
            img = img.resize((max(1, int(img.width * k)), max(1, int(img.height * k))), Image.Resampling.LANCZOS)
            im.paste(img, (xx, gy - img.height), img)
            if bg == sb.LIGHT:
                dr.text((xx, 2), pid, fill=(60, 50, 40))
            xx += img.width + 16
        bands.append(im)
    out = Image.new('RGB', (bands[0].width, sum(b.height for b in bands)))
    out.paste(bands[0], (0, 0)); out.paste(bands[1], (0, bands[0].height))
    DOCS.mkdir(parents=True, exist_ok=True)
    out.save(DOCS / 'painted-sheet.jpg', quality=85)
    print(DOCS / 'painted-sheet.jpg', out.size)


if __name__ == '__main__':
    c, *args = sys.argv[1:] or ['help']
    {'gen': cmd_gen, 'split': cmd_split, 'build': lambda: cmd_build()}.get(c, lambda *a: print(__doc__))(*args)
