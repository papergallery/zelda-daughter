#!/usr/bin/env python3
"""D-09: нормализация спрайтов персонажей на сервере (Pillow + numpy + scipy).

Вход — сырые генерации `tools/art/klein.py` в рабочей папке (по умолчанию $ZD_ART_WORK): <name>.png + <name>.mask.png
(маска BiRefNet). Отбор листов — `tools/art/selection.json`; паспорта (рост, вид) — `tools/art/sprites.json`.

    sprite_build.py split WORK/heroine-sheet-s3.png              # найденные фигуры, превью с рамками
    sprite_build.py crop  WORK/heroine-sheet-s3.png 0 OUT.png    # один вид с серым фоном — референс для кадров шага
    sprite_build.py build heroine [boar ...]                     # спрайты в Assets + лист docs/demo/sprites/<id>-sheet.png
    sprite_build.py all                                          # общий лист docs/demo/sprites/all.png
    sprite_build.py registry [id ...]                            # строки Assets/Art/Registries/characters.json

Что делает build:
1. Вырезка: альфа = маска BiRefNet; цвет края очищается от серого фона (C = a·F + (1−a)·B → F), мелкие пятна убираются.
2. Нарезка листа на фигуры — по пустым вертикальным полосам альфы (слева направо).
3. Масштаб — единые пиксели на метр (`ppm` в sprites.json) по «росту тела»: высота от верхней строки, где фигура шире
   0,22 от её типичной ширины (так тонкое древко копья и посох не считаются ростом), до нижней непрозрачной строки.
   Масштаб считается по опорному виду листа (люди — front, звери — side) и один на весь лист; кадры шага подгоняются
   под рост своего вида с листа.
4. Pivot — середина между крайними точками стоп в нижних 4 % фигуры; у всех кадров персонажа один холст и один pivot.
5. Мягкое приведение к палитре (`palette`, доля `palette_mix`), тёмные линии туши не трогаются.
"""
import json, os, pathlib, sys

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = pathlib.Path(__file__).resolve().parents[2]
CFG = json.loads((ROOT / 'tools/art/sprites.json').read_text(encoding='utf-8'))
SEL_PATH = pathlib.Path(os.environ.get('ZD_ART_SELECTION', ROOT / 'tools/art/selection.json'))
_OUT = os.environ.get('ZD_ART_OUT')  # для проверки скрипта: писать не в репозиторий
ASSETS = pathlib.Path(_OUT) / 'Sprites' if _OUT else ROOT / 'ZeldaDaughter/Assets/Art/Sprites'
DOCS = pathlib.Path(_OUT) / 'docs' if _OUT else ROOT / 'docs/demo/sprites'
VIEWS = ['front', 'side', 'back']
KEY_BAND, KEY_LO, KEY_HI, HALO_HI = 5, 0.03, 0.16, 0.16  # полоса кромки (px) и порог ключа по фону (доли 0..1 RGB)
LIGHT, DARK = (239, 231, 214), (42, 36, 32)
INK_RING = 0.35  # D-24: дыра в силуэте — окно фона, только если её кольцо ≥ 35 % туши


def work_dir():
    sel = json.loads(SEL_PATH.read_text(encoding='utf-8')) if SEL_PATH.exists() else {}
    return pathlib.Path(os.environ.get('ZD_ART_WORK') or sel.get('_work', '.'))


# ---------- вырезка ----------

def cutout(png: pathlib.Path) -> np.ndarray:
    """RGBA float32 0..1 по картинке и её маске BiRefNet; цвет края очищен от фона."""
    img = np.asarray(Image.open(png).convert('RGB')).astype(np.float32) / 255
    mpath = png.with_name(png.stem + '.mask.png')
    m = Image.open(mpath).convert('L')
    if m.size != (img.shape[1], img.shape[0]):
        m = m.resize((img.shape[1], img.shape[0]), Image.Resampling.BILINEAR)
    a = np.asarray(m).astype(np.float32) / 255
    bg_px = img[a < 0.02]
    bg = np.median(bg_px, axis=0) if len(bg_px) else np.array([0.78, 0.78, 0.78], np.float32)
    a = np.where(a < 0.03, 0, a)
    a = np.clip((a - 0.03) / 0.97, 0, 1)
    # кромка: маска BiRefNet оставляет серый фон в прядях и по контуру — в полосе у края (не внутри фигуры,
    # чтобы не продырявить серо-голубые джинсы) альфа ещё и по удалённости цвета от фона
    core = ndimage.binary_erosion(a > 0.5, iterations=KEY_BAND)
    edge = (a > 0) & ~core
    dist = np.sqrt(((img - bg) ** 2).sum(-1))
    key = np.clip((dist - KEY_LO) / (KEY_HI - KEY_LO), 0, 1)
    # замкнутые «окна» фона внутри силуэта (между прядью и лицом, под лямкой): острова цвета фона от 40 px
    lab, n = ndimage.label(core & (dist < KEY_LO * 2))
    if n:
        sizes = ndimage.sum(np.ones_like(a), lab, index=np.arange(1, n + 1))
        big = np.zeros(n + 1, bool); big[1:] = sizes >= 40
        edge |= ndimage.binary_dilation(big[lab], iterations=2)
        core &= ~edge
    a = np.where(edge, a * key, a)
    # светлый ореол: GPT Image рисует вокруг фигур чуть более светлый серый (на 0,05–0,12 светлее фона); нейтральные
    # светлые пиксели, связанные с внешним фоном, — прочь (белая футболка и седые волосы внутри контура туши не задеты)
    rgbmax, rgbmin = img.max(-1), img.min(-1)
    lum, bl = img.mean(-1), float(bg.mean())
    halo = (a > 0) & (rgbmax - rgbmin < 0.07) & (lum > bl + 0.012) & (lum < bl + HALO_HI)
    hl, hn = ndimage.label(halo)
    if hn:
        touch = np.unique(hl[ndimage.binary_dilation(a <= 0.05, iterations=1) & halo])
        gone = np.isin(hl, touch[touch > 0])
        a = np.where(ndimage.binary_dilation(gone, iterations=1) & ~core, np.minimum(a, key * 0.5), a)
        a = np.where(gone, 0, a)
    # D-24: дыры внутри силуэта без туши по краю — не окна фона, а светлые пятна акварели на ткани (бледные джинсы,
    # кеды цвета фона): настоящее окно (между прядями, рукой и телом) обведено тушью (доля тёмного кольца 0,5–0,97),
    # пятно на ткани — нет (0–0,23). Пятну возвращается непрозрачность.
    holes = ndimage.binary_fill_holes(a > 0.5) & (a < 0.5)
    hl, hn = ndimage.label(holes)
    if hn:
        luma = img.mean(-1)
        for k, sl in enumerate(ndimage.find_objects(hl)):
            y0, y1 = max(sl[0].start - 6, 0), sl[0].stop + 6
            x0, x1 = max(sl[1].start - 6, 0), sl[1].stop + 6
            m = hl[y0:y1, x0:x1] == k + 1
            ring = ndimage.binary_dilation(m, iterations=4) & ~ndimage.binary_dilation(m, iterations=1)
            if ring.any() and (luma[y0:y1, x0:x1][ring] < 0.3).mean() < INK_RING:
                a[y0:y1, x0:x1] = np.where(ndimage.binary_dilation(m, iterations=1), 1.0, a[y0:y1, x0:x1])
                core[y0:y1, x0:x1] |= m
    safe = np.maximum(a, 0.08)[..., None]
    fg = np.clip((img - (1 - a[..., None]) * bg) / safe, 0, 1)
    fg = np.where(core[..., None], img, fg)  # в глубине фигуры — исходный цвет
    rgba = np.dstack([fg, a])
    # мелкие пятна (брызги акварели, обрывки фона) — прочь
    lab, n = ndimage.label(a > 0.1)
    if n > 1:
        sizes = ndimage.sum(np.ones_like(a), lab, index=np.arange(1, n + 1))
        keep = np.zeros(n + 1, bool); keep[1:] = sizes >= max(400, 0.002 * sizes.max())
        dil = ndimage.binary_dilation(keep[lab], iterations=3)
        rgba[..., 3] *= dil
    return rgba


def figures(rgba: np.ndarray, near=40):
    """Рамки фигур (x0, y0, x1, y1) слева направо: связные области альфы; крупные (≥ 8 % от самой большой) — фигуры,
    мелкие (пряди, кончик палки, брызги) присоединяются к ближайшей фигуре не дальше near px, иначе выбрасываются."""
    a = rgba[..., 3]
    lab, n = ndimage.label(a > 0.1, structure=np.ones((3, 3)))
    if not n:
        return []
    objs = ndimage.find_objects(lab)
    areas = ndimage.sum(np.ones_like(a), lab, index=np.arange(1, n + 1))
    big = [i for i in range(n) if areas[i] >= 0.08 * areas.max()]
    boxes = {i: [objs[i][1].start, objs[i][0].start, objs[i][1].stop, objs[i][0].stop] for i in big}
    owner = np.zeros(n + 1, int)
    for i in big:
        owner[i + 1] = i + 1
    for i in range(n):
        if i in boxes or areas[i] < 30:
            continue
        x0, y0, x1, y1 = objs[i][1].start, objs[i][0].start, objs[i][1].stop, objs[i][0].stop
        def gap(b):
            return max(b[0] - x1, x0 - b[2], 0) + max(b[1] - y1, y0 - b[3], 0)
        best = min(big, key=lambda k: gap(boxes[k]))
        if gap(boxes[best]) <= near:
            b = boxes[best]
            boxes[best] = [min(b[0], x0), min(b[1], y0), max(b[2], x1), max(b[3], y1)]
            owner[i + 1] = best + 1
    own = owner[lab]
    out = []
    for i in big:
        x0, y0, x1, y1 = boxes[i]
        x0, y0 = max(0, x0 - 3), max(0, y0 - 3)
        x1, y1 = min(a.shape[1], x1 + 3), min(a.shape[0], y1 + 3)
        m = ndimage.binary_dilation(own[y0:y1, x0:x1] == i + 1, iterations=3)  # + слабая кромка (альфа < 0,1)
        out.append((x0, y0, x1, y1, m))
    out.sort(key=lambda b: (b[0] + b[2]) / 2)
    return out


def only_figure(rgba, fig):
    """Вырезать фигуру: только её пиксели (куски соседних фигур в той же рамке — прозрачны)."""
    x0, y0, x1, y1, m = fig
    p = rgba[y0:y1, x0:x1].copy()
    p[..., 3] *= m
    return p


def body_top_bottom(a: np.ndarray):
    widths = (a > 0.5).sum(axis=1)
    rows = np.where(widths > 0)[0]
    w80 = np.percentile(widths[rows], 80)
    top = rows[np.argmax(widths[rows] >= 0.22 * w80)]
    return int(top), int(rows[-1])


def erase(p, e):
    """[x0, y0, x1, y1] — стереть серые/светлые малонасыщенные пиксели, связанные с внешним фоном (тень, обрывок фона);
    {"rect": [...], "keep": "saturated"} — в прямоугольнике оставить только насыщенную ткань/кожу и 2 px контура вокруг.
    После — убрать крошки (связные куски < 150 px целиком внутри прямоугольника)."""
    rect, keep = (e['rect'], e.get('keep')) if isinstance(e, dict) else (e, None)
    h, w = p.shape[:2]
    y0, y1, x0, x1 = int(rect[1] * h), int(rect[3] * h), int(rect[0] * w), int(rect[2] * w)
    p = p.copy()
    inr = np.zeros((h, w), bool); inr[y0:y1, x0:x1] = True
    sat, lum, a = p[..., :3].max(-1) - p[..., :3].min(-1), p[..., :3].mean(-1), p[..., 3]
    if keep == 'saturated':
        cloth = ndimage.binary_fill_holes((sat >= 0.16) & (lum < 0.62) & (a > 0.5))  # шнурки и блики внутри — свои
        p[..., 3] = np.where(inr & ~ndimage.binary_dilation(cloth, iterations=2), 0, a)
    else:
        cand = inr & ((sat < 0.09) | ((lum > 0.58) & (sat < 0.16))) & (a > 0)
        lab, n = ndimage.label(cand)
        if n:
            touch = np.unique(lab[ndimage.binary_dilation(a <= 0.05, iterations=1) & cand])
            p[..., 3] = np.where(np.isin(lab, touch[touch > 0]), 0, a)
    lab, n = ndimage.label(p[..., 3] > 0.1)
    if n:
        sizes = ndimage.sum(np.ones((h, w)), lab, index=np.arange(1, n + 1))
        for k in np.where(sizes < 150)[0]:
            m = lab == k + 1
            if inr[m].all():
                p[..., 3][m] = 0
    return p


def grey_pockets(p, zone=0.3):
    """D-24: серые «карманы» фона между завитками волос (модель рисует их темнее фона — ключ их не снимает): в верхней
    zone фигуры нейтральные серые пиксели рядом с волосами (тёмно-рыжие) → прозрачные. Ткань ниже головы не трогается."""
    a = p[..., 3]
    rows = np.where((a > 0.5).any(axis=1))[0]
    y1 = rows[0] + int(zone * (rows[-1] - rows[0]))
    rgb = p[:y1, :, :3]
    sat, lum = rgb.max(-1) - rgb.min(-1), rgb.mean(-1)
    hair = ndimage.binary_dilation((rgb[..., 0] - rgb[..., 2] > 0.12) & (lum < 0.5) & (a[:y1] > 0.5), iterations=6)
    grey = (sat < 0.09) & (lum > 0.33) & (lum < 0.9) & (a[:y1] > 0.05) & hair
    p = p.copy()
    rim = ndimage.binary_dilation(grey, iterations=2) & ~grey & (sat < 0.15) & (lum > 0.3)  # светлая кайма вокруг кармана
    p[:y1, :, 3] = np.where(grey | rim, 0, a[:y1])
    # мелкие светлые блики внутри волос (модель рисует золотистые точки — на тёмном фоне мерцают): пятна до 40 px, заметно
    # светлее волос вокруг (локальная медиана 9×9) и окружённые волосами — цвет медианы
    hm = ((rgb[..., 0] - rgb[..., 2] > 0.08) & (lum < 0.5) & (a[:y1] > 0.5)).astype(np.float32)
    frac = ndimage.uniform_filter(hm, 11)
    med = np.dstack([ndimage.median_filter(rgb[..., c], size=9) for c in range(3)])
    sp = (lum > med.mean(-1) + 0.12) & (frac > 0.55) & (p[:y1, :, 3] > 0.5)
    lab, n = ndimage.label(sp)
    if n:
        sizes = ndimage.sum(sp, lab, index=np.arange(1, n + 1))
        small = np.zeros(n + 1, bool); small[1:] = sizes <= 40
        sp = ndimage.binary_dilation(small[lab], iterations=1) & (frac > 0.4)
        p[:y1, :, :3] = np.where(sp[..., None], med, p[:y1, :, :3])
    return p


def skin_fix(p, rect):
    """D-24: «гипсовая» рука — модель оставила кисть/предплечье почти без цвета. В прямоугольнике (доли рамки фигуры)
    бледные тёплые/нейтральные пиксели (не туши, не джинсы) перекрашиваются в цвет кожи этого же кадра с их светлотой."""
    h, w = p.shape[:2]
    y0, y1, x0, x1 = int(rect[1] * h), int(rect[3] * h), int(rect[0] * w), int(rect[2] * w)
    rgb, a = p[..., :3], p[..., 3]
    sat, lum = rgb.max(-1) - rgb.min(-1), rgb.mean(-1)
    skin = (rgb[..., 0] > rgb[..., 1]) & (rgb[..., 1] > rgb[..., 2]) & (sat > 0.2) & (lum > 0.45) & (lum < 0.85) & (a > 0.9)
    ref = np.median(rgb[skin], axis=0)
    p = p.copy()
    r = rgb[y0:y1, x0:x1]
    s, l = sat[y0:y1, x0:x1], lum[y0:y1, x0:x1]
    pale = (s < 0.24) & (l > 0.4) & (r[..., 2] <= r[..., 0] + 0.01) & (a[y0:y1, x0:x1] > 0.3)
    wgt = (np.clip((0.24 - s) / 0.1, 0, 1) * np.clip((l - 0.4) / 0.1, 0, 1) * pale)[..., None]
    tgt = np.clip(ref[None, None, :] * (l / ref.mean())[..., None], 0, 1)
    p[y0:y1, x0:x1, :3] = r * (1 - wgt) + tgt * wgt
    return p


def deblush(p, k=0.55, zone=0.32):
    """D-24: румяные круги на щеках (критик: «анфас моложе профиля») — в верхней zone фигуры кожа, заметно краснее медианы
    кожи кадра, сводится к медиане на долю k (светлота сохраняется). Волосы (тёмные) и туши не задеваются."""
    a = p[..., 3]
    rows = np.where((a > 0.5).any(axis=1))[0]
    y1 = rows[0] + int(zone * (rows[-1] - rows[0]))
    rgb = p[..., :3]
    sat, lum = rgb.max(-1) - rgb.min(-1), rgb.mean(-1)
    skin = (rgb[..., 0] > rgb[..., 1]) & (rgb[..., 1] > rgb[..., 2]) & (sat > 0.2) & (lum > 0.45) & (lum < 0.85) & (a > 0.9)
    ref = np.median(rgb[skin], axis=0)
    red = (rgb[..., 0] - rgb[..., 1]) - (ref[0] - ref[1])
    m = (red > 0.04) & (lum > 0.4) & (a > 0.5) & (rgb[..., 0] > rgb[..., 2] + 0.1)
    m[y1:] = False
    w = (np.clip((red - 0.04) / 0.08, 0, 1) * k * m)[..., None]
    tgt = np.clip(ref[None, None, :] * (lum / ref.mean())[..., None], 0, 1)
    p = p.copy()
    p[..., :3] = rgb * (1 - w) + tgt * w
    return p


def despeck(p, rect, d=0.12, size=40):
    """Мелкие светлые пятна (≤ size px), светлее локальной медианы 9×9 на d, в прямоугольнике (доли рамки) — цвет медианы."""
    h, w = p.shape[:2]
    y0, y1, x0, x1 = int(rect[1] * h), int(rect[3] * h), int(rect[0] * w), int(rect[2] * w)
    p = p.copy()
    rgb = p[y0:y1, x0:x1, :3]
    med = np.dstack([ndimage.median_filter(rgb[..., c], size=9) for c in range(3)])
    sp = (rgb.mean(-1) > med.mean(-1) + d) & (p[y0:y1, x0:x1, 3] > 0.5)
    lab, n = ndimage.label(sp)
    if n:
        sizes = ndimage.sum(sp, lab, index=np.arange(1, n + 1))
        small = np.zeros(n + 1, bool); small[1:] = sizes <= size
        sp = ndimage.binary_dilation(small[lab], iterations=1)
        p[y0:y1, x0:x1, :3] = np.where(sp[..., None], med, rgb)
    return p


def alpha_floor(p, rect, lo=0.6):
    """Полупрозрачный след (остаток тени/фона) в прямоугольнике: альфа < lo → 0."""
    h, w = p.shape[:2]
    y0, y1, x0, x1 = int(rect[1] * h), int(rect[3] * h), int(rect[0] * w), int(rect[2] * w)
    p = p.copy()
    a = p[y0:y1, x0:x1, 3]
    p[y0:y1, x0:x1, 3] = np.where(a < lo, 0, a)
    return p


def hair_match(p, ref):
    """Цвет волос кадра (тёмные тёплые пиксели) — к среднему и разбросу волос кадра ref."""
    def hm(q):
        rgb = q[..., :3]; lum = rgb.mean(-1)
        return (rgb[..., 0] - rgb[..., 2] > 0.06) & (lum < 0.45) & (lum > 0.08) & (q[..., 3] > 0.5)
    m, r = hm(p), hm(ref)
    out = p.copy()
    for c in range(3):
        a, b = p[..., c][m], ref[..., c][r]
        out[..., c] = np.where(m, np.clip((p[..., c] - a.mean()) / (a.std() + 1e-6) * b.std() + b.mean(), 0, 1), p[..., c])
    return out


def width80(a):
    w = (a > 0.5).sum(axis=1)
    return float(np.percentile(w[w > 0], 80))


def feet_x(a: np.ndarray, top: int, bottom: int) -> float:
    band = a[max(bottom - int(0.04 * (bottom - top)), 0):bottom + 1] > 0.5
    xs = np.where(band.any(axis=0))[0]
    return (xs[0] + xs[-1]) / 2


def to_img(rgba: np.ndarray) -> Image.Image:
    return Image.fromarray(np.clip(rgba * 255 + 0.5, 0, 255).astype(np.uint8), 'RGBA')


def scaled(rgba: np.ndarray, s: float) -> np.ndarray:
    """Масштаб с премультипликацией альфы (без тёмных/серых ореолов на краях)."""
    h, w = rgba.shape[:2]
    pm = rgba.copy(); pm[..., :3] *= pm[..., 3:4]
    size = (max(1, round(w * s)), max(1, round(h * s)))
    ch = [np.asarray(Image.fromarray(pm[..., i]).resize(size, Image.Resampling.LANCZOS)) for i in range(4)]
    out = np.clip(np.dstack(ch), 0, 1)
    al = out[..., 3:4]
    out[..., :3] = np.where(al > 1e-3, out[..., :3] / np.maximum(al, 1e-3), 0)
    return np.clip(out, 0, 1)


def color_match(p, ref):
    m, r = p[..., 3] > 0.5, ref[..., 3] > 0.5
    out = p.copy()
    for c in range(3):
        a, b = p[..., c][m], ref[..., c][r]
        out[..., c] = np.clip((p[..., c] - a.mean()) / (a.std() + 1e-6) * b.std() + b.mean(), 0, 1)
    return out


# ---------- палитра ----------

def palette_soft(rgba: np.ndarray, mix: float) -> np.ndarray:
    pal = np.array([[int(h[i:i + 2], 16) for i in (1, 3, 5)] for h in CFG['palette']], np.float32) / 255
    rgb = rgba[..., :3].reshape(-1, 3)
    d = ((rgb[:, None, :] - pal[None, :, :]) ** 2).sum(-1)
    q = pal[d.argmin(1)].reshape(rgba.shape[:2] + (3,))
    luma = (rgba[..., :3] @ np.array([0.299, 0.587, 0.114], np.float32))
    m = mix * np.clip((luma - 0.16) / 0.16, 0, 1)[..., None]  # туши (тёмное) не трогаем
    out = rgba.copy()
    out[..., :3] = rgba[..., :3] * (1 - m) + q * m
    return out


# ---------- команды ----------

def cmd_split(src):
    src = pathlib.Path(src)
    rgba = cutout(src)
    figs = figures(rgba)
    im = Image.open(src).convert('RGB'); dr = ImageDraw.Draw(im)
    for i, (x0, y0, x1, y1, _) in enumerate(figs):
        t, b = body_top_bottom(rgba[y0:y1, x0:x1, 3])
        dr.rectangle([x0, y0, x1, y1], outline=(255, 0, 0), width=3)
        dr.line([x0, y0 + t, x1, y0 + t], fill=(0, 0, 255), width=2)
        dr.text((x0 + 4, y0 + 4), str(i), fill=(255, 0, 0))
        print(i, (x0, y0, x1, y1), 'body', b - t + 1)
    out = src.with_name(src.stem + '.split.jpg')
    im.save(out, quality=85); print(out)


def cmd_crop(src, idx, out, margin=40):
    src = pathlib.Path(src)
    rgba = cutout(src)
    x0, y0, x1, y1, _ = figures(rgba)[int(idx)]
    im = Image.open(src).convert('RGB')
    w, h = im.size
    box = (max(0, x0 - margin), max(0, y0 - margin), min(w, x1 + margin), min(h, y1 + margin))
    im.crop(box).save(out); print(out, box)


def piece(src: pathlib.Path, idx: int, flip: bool):
    rgba = cutout(src)
    figs = figures(rgba)
    p = only_figure(rgba, figs[idx])
    if flip:
        p = p[:, ::-1].copy()
    return p, len(figs)


def frame_order(name):
    view, rest = name.split('_', 1)
    pose = rest.rsplit('_', 1)[0] if '_' in rest else ''
    num = int(rest.rsplit('_', 1)[-1]) if rest.rsplit('_', 1)[-1].isdigit() else 0
    return (pose != '', pose, VIEWS.index(view) if view in VIEWS else 9, num, rest)


def cmd_build(cid):
    """selection.json[cid].sources: [{file, ref, match?, figs: [имя кадра | null, …]}].
    Имя кадра — <view>_<frame> (шаг; 0 — стоит) или <view>_<pose>_<frame>. Первый источник — base: его фигура ref
    имеет рост height_m. У остальных фигура ref приводится к росту кадра match базового листа (обычно front_0)."""
    ch = CFG['characters'][cid]
    sel = json.loads(SEL_PATH.read_text(encoding='utf-8'))[cid]
    wd = work_dir()
    ppm = CFG['ppm']
    frames, body = {}, {}
    groups = {}  # D-25: кадры цикла с общего холста ролика — имя кадра → группа (одно смещение на группу)
    for n, src in enumerate(sel['sources']):
        if 'dir' in src:  # готовые кадры (d25_frames.py: 320 px/м, общий холст группы, палитра) — без вырезки и масштаба
            d = pathlib.Path(src['dir']) if pathlib.Path(src['dir']).is_absolute() else wd / src['dir']
            files = sorted(d.glob('[0-9]*.png'))
            if len(files) != len(src['names']):
                print(f'WARN {cid} {d}: кадров {len(files)}, имён {len(src["names"])}')
            for f, name in zip(files, src['names']):
                frames[name] = np.asarray(Image.open(f).convert('RGBA')).astype(np.float32) / 255
                groups[name] = src.get('group', str(d))
            continue
        rgba = cutout(wd / src['file'])
        figs = figures(rgba)
        if len(figs) != len(src['figs']):
            print(f'WARN {cid} {src["file"]}: фигур {len(figs)}, в выборе {len(src["figs"])}')
        crops = [only_figure(rgba, f) for f in figs]
        t, b = body_top_bottom(crops[src['ref']][..., 3])
        if ch.get('measure') == 'bbox':  # широкий плащ «съедает» узкую голову в эвристике роста — меряем по рамке
            rows = np.where((crops[src['ref']][..., 3] > 0.5).any(axis=1))[0]
            t, b = rows[0], rows[-1]
        if n == 0:
            target = ch['height_m'] * ppm
        else:
            m = src.get('match', 'front_0')
            target = body.get(m, ch['height_m'] * ppm)  # кадра нет (взят с другого листа) — рост из паспорта
            if 'target_m' in src:  # рост опорной фигуры задан явно, м
                target = src['target_m'] * ppm
        s = target / (b - t + 1)
        if n and src.get('match_by') == 'width':  # другой наклон камеры: подгонять по ширине фигуры, а не по росту
            s = width80(frames[src['match']][..., 3]) / width80(crops[src['ref']][..., 3])
        for k, name in enumerate(src['figs']):
            if not name or k >= len(crops):
                continue
            p = crops[k][:, ::-1].copy() if name in src.get('flip', []) else crops[k]
            for rect in src.get('skin', {}).get(name, []):  # перекрасить бледную руку в цвет кожи кадра
                p = skin_fix(p, rect)
            for e in src.get('erase', {}).get(name, []):  # зачистка артефакта в прямоугольнике (доли рамки фигуры)
                p = erase(p, e)
            for rect in src.get('despeck', {}).get(name, []):
                p = despeck(p, rect)
            for rect in src.get('alpha_floor', {}).get(name, []):
                p = alpha_floor(p, rect)
            if name in src.get('deblush', []):
                p = deblush(p)
            if src.get('grey_pockets') and 'down' not in name:
                p = grey_pockets(p)
            if src.get('color_match'):  # выровнять оттенок под кадр базового листа (среднее и разброс по каналам)
                p = color_match(p, frames[src['color_match']])
            frames[name] = palette_soft(scaled(p, s), CFG['palette_mix'])
            tt, bb = body_top_bottom(frames[name][..., 3])
            body[name] = bb - tt + 1
        if n == 0:
            print(cid, 'scale', round(s, 3))
    for name, ref in sel.get('post_color_match', {}).items():  # выровнять оттенок кадра под кадр с другого листа
        frames[name] = color_match(frames[name], frames[ref])
    for name, ref in sel.get('post_hair_match', {}).items():  # цвет волос — под кадр ref
        frames[name] = hair_match(frames[name], frames[ref])
    # общий холст и pivot (лежачие позы — по центру рамки)
    pad = 8
    info = {}
    for k, p in frames.items():
        tt, bb = body_top_bottom(p[..., 3])
        rows = np.where((p[..., 3] > 0.02).any(axis=1))[0]
        if any(w in k for w in ('down', 'dead')):
            cols = np.where((p[..., 3] > 0.02).any(axis=0))[0]
            fx = (cols[0] + cols[-1]) / 2
        else:
            fx = feet_x(p[..., 3], tt, bb)
        info[k] = (fx, int(rows[0]), bb)
    for g in set(groups.values()):  # группа: одно смещение (медиана центра стоп, нижняя опора группы) — без центровки по кадру
        ks = [k for k in groups if groups[k] == g]
        gfx = float(np.median([info[k][0] for k in ks])); gbb = max(info[k][2] for k in ks)
        for k in ks:
            info[k] = (gfx, info[k][1], gbb)
    half = max(max(fx, frames[k].shape[1] - fx) for k, (fx, _, _) in info.items())
    up = max(bb - top for (_, top, bb) in info.values())
    down = max(frames[k].shape[0] - 1 - bb for k, (_, _, bb) in info.items())
    W = int(np.ceil(half)) * 2 + 2 * pad
    H = up + down + pad + 1  # снизу без поля: стопы на нижней строке — pivot BottomCenter (RegistryBuilder) с точностью ≤ 1 см
    px, py = W // 2, pad + up  # pivot в пикселях (сверху)
    outdir = ASSETS / cid
    outdir.mkdir(parents=True, exist_ok=True)
    for old in outdir.glob(f'{cid}_*.png'):
        old.unlink()
    placed = {}
    for name in sorted(frames, key=frame_order):
        fx, _, bb = info[name]
        canvas = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        canvas.alpha_composite(to_img(frames[name]), (int(round(px - fx)), py - bb))
        canvas.save(outdir / f'{cid}_{name}.png', optimize=True)
        placed[name] = canvas
    walk = {v: sorted(int(n.split('_')[1]) for n in frames if n.count('_') == 1 and n.startswith(v + '_')) for v in VIEWS}
    poses = {}
    for n in frames:
        if n.count('_') == 2:
            v, pose, f = n.split('_')
            poses.setdefault(pose, {}).setdefault(v, []).append(int(f))
    meta = {'id': cid, 'ppu': ppm, 'height_m': ch['height_m'], 'size_px': [W, H],
            'pivot': [round(px / W, 4), round((H - 1 - py) / H, 4)], 'side_faces': 'right',
            'walk': walk, 'poses': {k: {v: sorted(f) for v, f in d.items()} for k, d in sorted(poses.items())},
            'source': sel}
    (outdir / f'{cid}.sprite.json').write_text(json.dumps(meta, ensure_ascii=False, indent=1), encoding='utf-8')
    sheet_png(cid, placed, ppm, ch['height_m'], (px, py))
    print(cid, 'canvas', W, H, 'pivot', meta['pivot'], 'frames', len(frames))


def trim_x(c, margin=6):
    a = np.asarray(c)[..., 3]
    cols = np.where(a.max(axis=0) > 8)[0]
    if not len(cols):
        return c
    return c.crop((max(0, cols[0] - margin), 0, min(c.width, cols[-1] + 1 + margin), c.height))


def band(cells, bg, ppm, height_m, pivot_y, label_h=0, gap=12, k=0.75):
    cells = [trim_x(c) for c in cells]
    W = sum(int(c.width * k) for c in cells) + gap * (len(cells) + 1)
    H = int(cells[0].height * k) + 2 * gap
    im = Image.new('RGB', (W, H), bg)
    dr = ImageDraw.Draw(im)
    gy = gap + int(pivot_y * k)
    line = (120, 110, 95) if bg == LIGHT else (95, 88, 78)
    for m in np.arange(0, 3.01, 0.5):
        y = gy - int(m * ppm * k)
        if y >= 0:
            dr.line([0, y, W, y], fill=line, width=2 if m in (0, 1, 2) else 1)
    x = gap
    for c in cells:
        cw, chh = int(c.width * k), int(c.height * k)
        im.paste(c.resize((cw, chh), Image.Resampling.LANCZOS), (x, gap), c.resize((cw, chh), Image.Resampling.LANCZOS))
        x += cw + gap
    return im


def sheet_png(cid, placed, ppm, height_m, pivot):
    names = sorted(placed, key=frame_order)
    walk = [placed[k] for k in names if k.count('_') == 1]
    pose = [placed[k] for k in names if k.count('_') == 2]
    parts = [band(walk, LIGHT, ppm, height_m, pivot[1]), band(walk, DARK, ppm, height_m, pivot[1])]
    if pose:
        parts += [band(pose, LIGHT, ppm, height_m, pivot[1]), band(pose, DARK, ppm, height_m, pivot[1])]
    out = Image.new('RGB', (max(p.width for p in parts), sum(p.height for p in parts)), LIGHT)
    y = 0
    for p in parts:
        out.paste(p, (0, y)); y += p.height
    DOCS.mkdir(parents=True, exist_ok=True)
    out.quantize(256, method=Image.Quantize.MEDIANCUT).save(DOCS / f'{cid}-sheet.png', optimize=True)  # лист для просмотра: 256 цветов


def cmd_all():
    order = list(CFG['characters'])
    cells_f, cells_s = [], []
    for cid in order:
        d = ASSETS / cid
        if not (d / f'{cid}_front_0.png').exists():
            continue
        meta = json.loads((d / f'{cid}.sprite.json').read_text(encoding='utf-8'))
        for v, lst in (('front', cells_f), ('side', cells_s)):
            im = Image.open(d / f'{cid}_{v}_0.png')
            py = int(round(im.height * (1 - meta['pivot'][1]))) - 1
            # выровнять по земле: все клетки одной высоты над pivot
            lst.append((im, py))
    rows = []
    for cells in (cells_f, cells_s):
        up = max(py for _, py in cells); down = max(im.height - py for im, py in cells)
        norm = []
        for im, py in cells:
            c = Image.new('RGBA', (im.width, up + down), (0, 0, 0, 0)); c.alpha_composite(im, (0, up - py)); norm.append(c)
        rows += [band(norm, LIGHT, CFG['ppm'], 0, up, k=0.5), band(norm, DARK, CFG['ppm'], 0, up, k=0.5)]
    W = max(r.width for r in rows)
    out = Image.new('RGB', (W, sum(r.height for r in rows)), LIGHT)
    y = 0
    for r in rows:
        out.paste(r, (0, y)); y += r.height
    out.quantize(256, method=Image.Quantize.MEDIANCUT).save(DOCS / 'all.png', optimize=True); print(DOCS / 'all.png', out.size)


def cmd_registry(*ids):
    """Строки реестра Assets/Art/Registries/characters.json для собранных персонажей (front/back/side — кадры шага, down — лежит)."""
    reg = ROOT / 'ZeldaDaughter/Assets/Art/Registries/characters.json'
    d = json.loads(reg.read_text(encoding='utf-8'))
    for cid in ids or CFG['characters']:
        mp = ASSETS / cid / f'{cid}.sprite.json'
        if not mp.exists():
            continue
        meta = json.loads(mp.read_text(encoding='utf-8'))
        rel = f'Assets/Art/Sprites/{cid}/{cid}_'
        rec = {v: [f'{rel}{v}_{f}.png' for f in meta['walk'][v]] for v in VIEWS}
        down = next((f'{rel}side_{p}_0.png' for p in ('down', 'dead') if p in meta['poses']), None)
        if down:
            rec['down'] = down
        old = d['characters'].get(cid, {})
        rec['pixelsPerMeter'] = meta['ppu']
        rec['strideMeters'] = old.get('strideMeters', 0.8)
        for k, v in old.items():  # прочие поля (poses — D-11/D-12, …) не терять
            rec.setdefault(k, v)
        d['characters'][cid] = rec
    lines = ',\n'.join(f'    {json.dumps(k)}: {json.dumps(v, ensure_ascii=False)}' for k, v in sorted(d['characters'].items()))
    head = ''.join(f'  {json.dumps(k)}: {json.dumps(v, ensure_ascii=False)},\n' for k, v in d.items() if k != 'characters')
    reg.write_text('{\n' + head + '  "characters": {\n' + lines + '\n  }\n}\n', encoding='utf-8')
    print(reg)


if __name__ == '__main__':
    c, *args = sys.argv[1:] or ['help']
    if c == 'split':
        [cmd_split(a) for a in args]
    elif c == 'crop':
        cmd_crop(*args)
    elif c == 'build':
        [cmd_build(a) for a in args]
    elif c == 'all':
        cmd_all()
    elif c == 'registry':
        cmd_registry(*args)
    else:
        print(__doc__)
