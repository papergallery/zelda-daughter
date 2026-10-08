#!/usr/bin/env python3
"""D-22b: растительность региона — акварельные billboard-спрайты (трава, цветы, кусты, тростник) в одном атласе.

Источник — листы GPT Image (Polza) по промптам из tools/art/vegetation.json (стиль — концепт F1). Сырые листы — в рабочей папке
($ZD_ART_WORK, вне git); их URL, цена и промпт — в docs/demo/sprites/vegetation-sources.json.

    veg_build.py gen grass [flowers water]      # листы → WORK/veg-<sheet>-0.png (+ .json), 7 ₽ за лист 2K
    veg_build.py split WORK/veg-grass-0.png      # нарезка (ключ по серому фону, как у персонажей): превью с номерами фигур
    veg_build.py build                           # атлас Assets/Art/Vegetation/vegetation.png + vegetation.json + лист просмотра

Что делает build:
1. Вырезка — sprite_build.cutout (маска keymask.py по ровному серому фону, цвет края очищен от фона).
2. Фигуры листа — sprite_build.figures (слева направо); отбор и рост в метрах — vegetation.json sheets[*].figs: [[id, рост_м] | null, …].
3. Масштаб — ppm пикселей на метр по росту фигуры; нижняя непрозрачная строка — земля (pivot снизу по центру основания).
4. Атлас — полки (shelf packing) с полем 6 px; цвет прозрачных пикселей продолжен от края (без тёмной каймы при фильтрации и mip).
5. Цвет — как нарисовано (палитра персонажей не применяется).
6. Силуэт — выпуклая оболочка непрозрачных пикселей, упрощённая до ≤ 8 вершин наружу (меш карточки по контуру, меньше площади alpha clip).
7. vegetation.json: id → rect (px, origin внизу слева, как UV Unity), size_m [ширина, высота], pivot_x (доля ширины), kind.
"""
import json, os, pathlib, subprocess, sys
sys.path.insert(0, str(pathlib.Path(__file__).parent))
import style  # noqa: E402  (docs/demo/sprites/style-bible.md)

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import sprite_build as sb  # noqa: E402  (cutout, figures, only_figure, scaled)

CFG_PATH = HERE / 'vegetation.json'
CFG = json.loads(CFG_PATH.read_text(encoding='utf-8'))
OUT = ROOT / 'ZeldaDaughter/Assets/Art/Vegetation'
DOCS = ROOT / 'docs/demo/sprites'
SOURCES = DOCS / 'vegetation-sources.json'
REF_CROP = (0, 1100, 827, 1792)  # F1: луг и цветы, без героини
PAD = 6


def work():
    w = pathlib.Path(os.environ.get('ZD_ART_WORK') or CFG.get('_work') or '.')
    w.mkdir(parents=True, exist_ok=True)
    return w


def ref_image():
    dst = work() / 'f1-meadow-ref.png'  # PNG: JPEG data-URL шлюз отвергает ("File type not supported")
    if not dst.exists():
        Image.open(ROOT / 'docs/concept/world-b/f1.jpg').crop(REF_CROP).convert('RGB').save(dst)
    return dst


def prompt_of(sheet):
    s = CFG['sheets'][sheet]
    return '\n\n'.join([s['prompt'], CFG['camera'], style.STYLE + ' ' + CFG['plants'] + ' ' + style.BG_GREY, CFG['refnote']])


def cmd_gen(*sheets):
    wd = work()
    for sheet in sheets:
        s = CFG['sheets'][sheet]
        pf = wd / f'veg-{sheet}.prompt.txt'
        pf.write_text(prompt_of(sheet), encoding='utf-8')
        subprocess.run([sys.executable, str(HERE / 'polza.py'), 'gen', '--model', CFG['model'], '--out', str(wd), '--name', f'veg-{sheet}',
                        '--prompt-file', str(pf), '--ref', CFG['ref_url'], '--ar', s['ar'], '--res', s['res']], check=True)
        meta = json.loads((wd / f'veg-{sheet}-0.json').read_text(encoding='utf-8'))
        src = json.loads(SOURCES.read_text(encoding='utf-8')) if SOURCES.exists() else {'_source': 'D-22b: листы растительности (tools/art/veg_build.py gen)', 'sheets': {}}
        src['sheets'][f'veg-{sheet}-0.png'] = {'model': meta['model'], 'request': meta.get('request'), 'url': meta.get('url'), 'usage': meta.get('usage'),
                                               'aspect_ratio': s['ar'], 'image_resolution': s['res'], 'ref': CFG['ref'], 'prompt': meta['prompt']}
        SOURCES.write_text(json.dumps(src, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')


def ensure_mask(png):
    m = png.with_name(png.stem + '.mask.png')
    if not m.exists():
        subprocess.run([sys.executable, str(HERE / 'keymask.py'), str(png)], check=True)


def cmd_split(src):
    src = pathlib.Path(src)
    ensure_mask(src)
    sb.cmd_split(src)


def bleed(rgba):
    """Цвет прозрачных пикселей — от ближайшего непрозрачного (фильтрация и mip не тянут серый/чёрный край)."""
    a = rgba[..., 3]
    solid = a > 0.05
    if not solid.any():
        return rgba
    idx = ndimage.distance_transform_edt(~solid, return_distances=False, return_indices=True)
    out = rgba.copy()
    out[..., :3] = rgba[idx[0], idx[1], :3]
    return out


def hull_of(alpha, max_vertices=8, pad=2.0):
    """Выпуклый многоугольник ≤ max_vertices вокруг пикселей альфы > 0,1: оболочка, затем убираем ребро, которое добавляет меньше всего
    площади (продлеваем соседние рёбра до пересечения), пока вершин больше нужного. Координаты — доли ширины/высоты, v снизу вверх."""
    from scipy.spatial import ConvexHull
    h, w = alpha.shape
    ys, xs = np.nonzero(alpha > 0.1)
    pts = np.concatenate([np.stack([xs, ys], 1), np.stack([xs + 1, ys], 1), np.stack([xs, ys + 1], 1), np.stack([xs + 1, ys + 1], 1)]).astype(np.float64)
    hull = pts[ConvexHull(pts).vertices]                       # против часовой в координатах картинки (y вниз)
    c = hull.mean(0)
    hull = c + (hull - c) * (1 + pad / max(w, h))              # чуть наружу: край мазка не срезается
    poly = [tuple(p) for p in hull]

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
            a, b, c2, d = poly[i - 1], poly[i], poly[(i + 1) % n], poly[(i + 2) % n]   # убрать ребро b–c2
            q = inter(a, b, c2, d)
            if q is None or not (-(PAD - 1) <= q[0] <= w + PAD - 1 and -(PAD - 1) <= q[1] <= h + PAD - 1):
                continue
            # точка должна лежать снаружи ребра (иначе рёбра расходятся)
            area = abs((b[0] - q[0]) * (c2[1] - q[1]) - (c2[0] - q[0]) * (b[1] - q[1])) / 2
            if best is None or area < best[0]:
                best = (area, i, q)
        if best is None:
            break
        _, i, q = best
        poly = [p for k, p in enumerate(poly) if k not in (i, (i + 1) % n)]
        poly.insert(i if i < len(poly) + 1 else len(poly), q)
        # восстановить порядок: q встаёт на место убранных b, c2
        poly = order_ccw(poly)
    # за край рамки — не дальше поля атласа (PAD − 1 px прозрачного продолжения), иначе обрезали бы крайние мазки
    mx, my = (PAD - 1) / w, (PAD - 1) / h
    out = [[round(min(max(x / w, -mx), 1 + mx), 4), round(min(max(1 - y / h, -my), 1 + my), 4)] for x, y in poly]
    return out


def order_ccw(poly):
    c = np.mean(poly, 0)
    return sorted(poly, key=lambda p: np.arctan2(p[1] - c[1], p[0] - c[0]))


def polygon_area(poly):
    a = 0.0
    for i in range(len(poly)):
        x1, y1 = poly[i - 1]; x2, y2 = poly[i]
        a += x1 * y2 - x2 * y1
    return abs(a) / 2


def cmd_build():
    ppm = CFG['ppm']
    wd = work()
    items = []
    for sheet, s in CFG['sheets'].items():
        if not s.get('figs'):
            continue
        png = wd / f'veg-{sheet}-0.png'
        ensure_mask(png)
        rgba = sb.cutout(png)
        figs = sb.figures(rgba)
        if len(figs) != len(s['figs']):
            print(f'WARN {sheet}: фигур {len(figs)}, в выборе {len(s["figs"])}')
        for k, pick in enumerate(s['figs']):
            if not pick or k >= len(figs):
                continue
            vid, height_m, kind = pick[0], pick[1], pick[2] if len(pick) > 2 else sheet
            p = sb.only_figure(rgba, figs[k])
            rows = np.where((p[..., 3] > 0.5).any(axis=1))[0]
            cols = np.where((p[..., 3] > 0.05).any(axis=0))[0]
            p = p[:rows[-1] + 1, cols[0]:cols[-1] + 1]
            top = np.where((p[..., 3] > 0.05).any(axis=1))[0][0]
            p = p[top:]
            s_ = height_m * ppm / p.shape[0]
            p = sb.scaled(p, s_)  # палитру персонажей не применяем: лиловые цветы сереют
            items.append((vid, kind, p))
    # полки: по убыванию высоты
    items.sort(key=lambda t: -t[2].shape[0])
    W = 2048
    x = y = PAD
    shelf = 0
    place = {}
    for vid, kind, p in items:
        h, w = p.shape[:2]
        if x + w + PAD > W:
            x, y, shelf = PAD, y + shelf + 2 * PAD, 0
        place[vid] = (x, y, w, h, kind)
        x += w + 2 * PAD
        shelf = max(shelf, h)
    H = 1 << int(np.ceil(np.log2(y + shelf + PAD)))
    atlas = np.zeros((H, W, 4), np.float32)
    for vid, kind, p in items:
        x0, y0, w, h, _ = place[vid]
        atlas[y0:y0 + h, x0:x0 + w] = p
    atlas = bleed(atlas)
    OUT.mkdir(parents=True, exist_ok=True)
    sb.to_img(atlas).save(OUT / 'vegetation.png', optimize=True)
    meta = {'_source': 'D-22b: tools/art/veg_build.py build — атлас растительности; rect в px от нижнего левого угла (UV Unity), size_m — ширина и высота в метрах, '
                       'основание — нижний край по центру.', 'texture': 'Assets/Art/Vegetation/vegetation.png', 'size': [W, H], 'ppm': ppm, 'sprites': {}}
    for vid, (x0, y0, w, h, kind) in sorted(place.items()):
        p = next(q for v, _, q in items if v == vid)
        hull = hull_of(p[..., 3])
        meta['sprites'][vid] = {'rect': [x0, H - y0 - h, w, h], 'size_m': [round(w / ppm, 3), round(h / ppm, 3)], 'kind': kind, 'hull': hull,
                                'hull_area': round(polygon_area(hull), 3)}
    (OUT / 'vegetation.json').write_text(json.dumps(meta, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    sheet_png(items)
    print(OUT / 'vegetation.png', (W, H), 'sprites', len(items))


def sheet_png(items):
    """Лист просмотра: все спрайты на светлой и тёмной полосе, линии через 0,25 м."""
    k = 0.6
    ppm = CFG['ppm']
    hmax = max(p.shape[0] for _, _, p in items)
    bands = []
    for bg in (sb.LIGHT, sb.DARK):
        Wd = int(sum(p.shape[1] * k + 16 for _, _, p in items)) + 16
        Hd = int(hmax * k) + 40
        im = Image.new('RGB', (Wd, Hd), bg)
        dr = ImageDraw.Draw(im)
        gy = Hd - 16
        for m in np.arange(0, 3.0, 0.25):
            yy = gy - int(m * ppm * k)
            if yy >= 0:
                dr.line([0, yy, Wd, yy], fill=(120, 110, 95) if bg == sb.LIGHT else (95, 88, 78), width=2 if m % 1 == 0 else 1)
        xx = 16
        for vid, _, p in sorted(items, key=lambda t: t[0]):
            img = sb.to_img(p)
            img = img.resize((max(1, int(img.width * k)), max(1, int(img.height * k))), Image.Resampling.LANCZOS)
            im.paste(img, (xx, gy - img.height), img)
            if bg == sb.LIGHT:
                dr.text((xx, 2), vid, fill=(60, 50, 40))
            xx += img.width + 16
        bands.append(im)
    out = Image.new('RGB', (bands[0].width, sum(b.height for b in bands)))
    out.paste(bands[0], (0, 0)); out.paste(bands[1], (0, bands[0].height))
    DOCS.mkdir(parents=True, exist_ok=True)
    out.quantize(256, method=Image.Quantize.MEDIANCUT).save(DOCS / 'vegetation-sheet.png', optimize=True)
    print(DOCS / 'vegetation-sheet.png', out.size)


if __name__ == '__main__':
    c, *args = sys.argv[1:] or ['help']
    if c == 'gen':
        cmd_gen(*args)
    elif c == 'split':
        [cmd_split(a) for a in args]
    elif c == 'build':
        cmd_build()
    else:
        print(__doc__)
