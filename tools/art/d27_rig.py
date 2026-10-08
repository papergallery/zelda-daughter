#!/usr/bin/env python3
"""D-27: лист частей героини (бок) → вырезной риг для Unity: атлас частей + <id>_<view>.rig.json (формат — core/ZeldaDaughter.Core/Cutout/CutoutRig.cs).

Приёмы — ref2game (MIT, github.com/studioigor/ref2game; scripts/partrig.py, rigcut.py, jointr.py; references/animation.md §1–3, §12, §15.5),
переписаны заново под наш формат:
- лист: тело без конечностей, ОДНА нога, ОДНА рука — три самых крупных пятна слева направо;
- нога режется на бедро / голень / стопу с нахлёстом (верхний кусок рисуется поверх нижнего), рука — на плечо / предплечье;
- круглая «шапка» сустава — диск текстуры верхнего сегмента радиусом в полуширину конечности у сустава (как jointr.py), поворачивается
  с верхним сегментом и прячет угол на сгибе;
- дальняя рука и нога — те же рисунки, темнее (рисуются за телом);
- суставы меряются на рисунке (доли высоты части задаются ключами и проверяются глазами по превью с метками).

    python3 tools/art/d27_rig.py cut --sheet parts.png --out ZeldaDaughter/Assets/Art/Sprites/heroine/rig --preview docs/demo/d27/rig-rest.png
    python3 tools/art/d27_rig.py render --rig .../heroine_side.rig.json --trace trace.json --out DIR [--every 2] [--scale 1]

Высота собранной фигуры — как у кадра 0 бок (`heroine_side_0.png`, 538 px при 320 px/м).
"""
import argparse, json, math, pathlib

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

ROOT = pathlib.Path(__file__).resolve().parents[2]
SIDE0 = ROOT / 'ZeldaDaughter/Assets/Art/Sprites/heroine/heroine_side_0.png'
LAYERS = ['forearmFar', 'upperArmFar', 'elbowCapFar', 'footFar', 'shinFar', 'thighFar', 'kneeCapFar',
          'footNear', 'shinNear', 'thighNear', 'kneeCapNear', 'body', 'forearmNear', 'upperArmNear', 'elbowCapNear']
FAR_DARK = 0.74          # дальняя конечность в тени тела (ref2game animation.md §3)


# ------------------------------------------------------------------ the sheet

def key(img: np.ndarray, lo=0.045, hi=0.12) -> np.ndarray:
    """RGBA из листа на ровном сером: альфа по расстоянию до фона (медиана рамки), дыры силуэтов залиты."""
    f = img[:, :, :3].astype(np.float32) / 255
    border = np.concatenate([f[:12].reshape(-1, 3), f[-12:].reshape(-1, 3), f[:, :12].reshape(-1, 3), f[:, -12:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    a = np.clip((np.sqrt(((f - bg) ** 2).sum(-1)) - lo) / (hi - lo), 0, 1)
    solid = ndimage.binary_fill_holes(a > 0.5)
    a = np.maximum(a, solid.astype(np.float32) * (ndimage.binary_erosion(solid, iterations=2)))
    a[~ndimage.binary_dilation(solid, iterations=1)] = 0
    out = np.dstack([img[:, :, :3], (a * 255).astype(np.uint8)])
    return out


def blobs(rgba: np.ndarray, n=3):
    m = rgba[:, :, 3] > 100
    lab, k = ndimage.label(ndimage.binary_closing(m, iterations=3))
    sizes = ndimage.sum(np.ones_like(lab), lab, range(1, k + 1))
    keep = list(np.argsort(-sizes)[:n] + 1)
    out = []
    for l in keep:
        yy, xx = np.where(lab == l)
        y0, y1, x0, x1 = yy.min(), yy.max() + 1, xx.min(), xx.max() + 1
        sub = rgba[y0:y1, x0:x1].copy()
        own = ndimage.binary_dilation(lab[y0:y1, x0:x1] == l, iterations=2)
        sub[~own] = 0
        out.append((x0, sub))
    out.sort(key=lambda t: t[0])
    return [s for _, s in out]


def scaled(img: np.ndarray, s: float) -> np.ndarray:
    h, w = img.shape[:2]
    im = Image.fromarray(img).resize((max(1, round(w * s)), max(1, round(h * s))), Image.LANCZOS)
    return np.array(im)


def row_mid(img, y):
    xs = np.where(img[int(np.clip(y, 0, img.shape[0] - 1)), :, 3] > 100)[0]
    return float(xs.mean()) if len(xs) else img.shape[1] / 2


def rows_of(img):
    ys = np.where((img[:, :, 3] > 100).any(axis=1))[0]
    return int(ys.min()), int(ys.max())


def half_width(img, y):
    xs = np.where(img[int(np.clip(y, 0, img.shape[0] - 1)), :, 3] > 100)[0]
    return (xs.max() - xs.min() + 1) / 2 if len(xs) else 4.0


def band(img, y0, y1):
    """Только строки [y0, y1) части (остальное прозрачно)."""
    out = img.copy()
    y0, y1 = max(0, int(round(y0))), min(img.shape[0], int(round(y1)))
    out[:y0] = 0
    out[y1:] = 0
    return out


def top_round(img, y_cut, cx, r):
    """Срез сверху по дуге (полукруг радиусом r вокруг (cx, y_cut + r)): верх куска не торчит углом при повороте."""
    h, w = img.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    keep = (yy >= y_cut + r) | (((xx - cx) ** 2 + (yy - (y_cut + r)) ** 2) <= r * r)
    out = img.copy()
    out[~keep] = 0
    return out


def disc(img, cx, cy, r, feather=1.5):
    h, w = img.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    k = np.clip((r - d) / feather + 0.5, 0, 1)
    out = img.copy()
    out[:, :, 3] = (out[:, :, 3].astype(np.float32) * k).astype(np.uint8)
    return out


def darker(img, k=FAR_DARK):
    out = img.copy()
    out[:, :, :3] = (out[:, :, :3].astype(np.float32) * k).astype(np.uint8)
    return out


def trim(img, pad=2):
    ys, xs = np.where(img[:, :, 3] > 0)
    y0, y1, x0, x1 = max(0, ys.min() - pad), min(img.shape[0], ys.max() + 1 + pad), max(0, xs.min() - pad), min(img.shape[1], xs.max() + 1 + pad)
    return img[y0:y1, x0:x1], (x0, y0)


def bleed(img, it=4):
    """RGB краёв растекается под прозрачные пиксели (без тёмного ореола при фильтрации, ref2game art.md §8)."""
    out = img.copy()
    a = out[:, :, 3] > 0
    for _ in range(it):
        grown = ndimage.binary_dilation(a)
        ring = grown & ~a
        if not ring.any():
            break
        _, (iy, ix) = ndimage.distance_transform_edt(~a, return_indices=True)
        out[ring, :3] = out[iy[ring], ix[ring], :3]
        a = grown
    return out


# ------------------------------------------------------------------ cut

def cut(a):
    sheet = np.array(Image.open(a.sheet).convert('RGBA'))
    rgba = key(sheet)
    body, leg, arm = blobs(rgba)
    # --- measure on the sheet (each part in its own frame)
    by0, by1 = rows_of(body); bh = by1 - by0
    bxs = np.where((body[:, :, 3] > 100).any(axis=0))[0]; bx0, bw = bxs.min(), bxs.max() - bxs.min()
    ly0, ly1 = rows_of(leg); lh = ly1 - ly0
    ay0, ay1 = rows_of(arm); ah = ay1 - ay0
    f = lambda s: [float(v) for v in s.split(',')]
    hipB = (bx0 + f(a.hip)[0] * bw, by0 + f(a.hip)[1] * bh)                 # the hip joint on the body
    shB = (bx0 + f(a.shoulder)[0] * bw, by0 + f(a.shoulder)[1] * bh)
    hipL_y = ly0 + a.leg_hip * lh
    ankL_y = ly0 + a.leg_ankle * lh
    kneeL_y = hipL_y + a.leg_knee * (ankL_y - hipL_y)
    hipL = (row_mid(leg, hipL_y), hipL_y); kneeL = (row_mid(leg, kneeL_y), kneeL_y); ankL = (row_mid(leg, ankL_y), ankL_y)
    shA_y = ay0 + a.arm_shoulder * ah; elA_y = ay0 + a.arm_elbow * ah; hdA_y = ay0 + a.arm_hand * ah
    shA = (row_mid(arm, shA_y), shA_y); elA = (row_mid(arm, elA_y), elA_y); hdA = (row_mid(arm, hdA_y), hdA_y)
    # --- scale: hair top to sole as tall as the approved side frame
    s0 = np.array(Image.open(SIDE0))[:, :, 3] > 100
    ys0 = np.where(s0.any(axis=1))[0]
    target = a.height or float(ys0.max() - ys0.min() + 1)
    total = (hipB[1] - by0) + (ly1 - hipL_y)
    s = target / total
    print(f'sheet: body {bh}px leg {lh}px arm {ah}px; figure {total:.0f}px -> {target:.0f}px (x{s:.3f})')
    body, leg, arm = scaled(body, s), scaled(leg, s), scaled(arm, s)
    S = lambda p: (p[0] * s, p[1] * s)
    hipB, shB, hipL, kneeL, ankL, shA, elA, hdA = map(S, (hipB, shB, hipL, kneeL, ankL, shA, elA, hdA))
    ly0, ly1 = rows_of(leg); ay0, ay1 = rows_of(arm); by0, by1 = rows_of(body)
    ko = max(2, round(a.overlap * target))

    # --- rig space: px, origin = the ground under the pelvis, +x = facing (right), +y = up
    sole_rows = leg[ly1 - max(2, round(0.02 * target)):ly1 + 1, :, 3] > 100
    sx = np.where(sole_rows.any(axis=0))[0]
    toeL, heelL = (float(sx.max()), float(ly1)), (float(sx.min()), float(ly1))
    hip_sep = a.hip_sep * target / 2
    # the body is placed so that its hip joint is at rig (0, hipY); the leg hangs from the hip
    hipY = ly1 - hipL[1]                        # hip height above the sole
    def from_body(p):                           # body px → rig px
        return (p[0] - hipB[0], hipY + (hipB[1] - p[1]))
    def from_leg(p, dx=0.0):                    # leg px → rig px (hip joint of the leg on the rig hip)
        return (p[0] - hipL[0] + dx, hipY + (hipL[1] - p[1]))
    def from_arm(p, sh):                        # arm px → rig px (shoulder joint on the given rig shoulder)
        return (sh[0] + p[0] - shA[0], sh[1] + (shA[1] - p[1]))
    pelvis = (0.0, hipY)
    hipNear, hipFar = (-hip_sep, hipY), (hip_sep, hipY)
    shNear = from_body(shB)
    shFar = (shNear[0] + a.shoulder_far_dx * target, shNear[1] + a.shoulder_far_dy * target)
    joints = {
        'pelvis': pelvis, 'hipNear': hipNear, 'hipFar': hipFar,
        'knee': from_leg(kneeL, -hip_sep), 'ankle': from_leg(ankL, -hip_sep), 'toe': from_leg(toeL, -hip_sep), 'heel': from_leg(heelL, -hip_sep),
        'shoulderNear': shNear, 'shoulderFar': shFar, 'elbow': from_arm(elA, shNear), 'hand': from_arm(hdA, shNear),
    }
    top_y = by0
    tx = np.where(body[top_y, :, 3] > 100)[0]
    joints['top'] = from_body((float(tx.mean()), float(top_y)))
    bandm = body[:, :, 3] > 100
    bandm[:top_y] = False; bandm[int(top_y + 0.13 * target):] = False
    hy, hx = np.where(bandm)
    joints['head'] = from_body((float(hx.mean()), float(hy.mean())))

    # --- pieces (part px) and their pivots (part px)
    hipr = half_width(leg, hipL[1] + ko)
    thigh = top_round(band(leg, 0, kneeL[1] + ko), hipL[1] - hipr * 0.6, hipL[0], hipr * 1.15) if a.round_thigh else band(leg, 0, kneeL[1] + ko)
    shin = band(leg, kneeL[1] - ko, ankL[1] - a.foot_up * target + ko)
    foot = band(leg, ankL[1] - a.foot_up * target - ko, leg.shape[0])
    up = band(arm, 0, elA[1] + ko)
    fore = band(arm, elA[1] - ko, arm.shape[0])
    kr = half_width(leg, kneeL[1] - 2 * ko) * a.cap
    er = half_width(arm, elA[1] - 2 * ko) * a.cap
    kcap = disc(band(leg, 0, kneeL[1] + kr + 2), kneeL[0], kneeL[1], kr)
    ecap = disc(band(arm, 0, elA[1] + er + 2), elA[0], elA[1], er)
    pieces = {
        'body': (body, hipB, 0.0),
        'thigh': (thigh, hipL, angle(hipL, kneeL)), 'shin': (shin, kneeL, angle(kneeL, ankL)), 'foot': (foot, ankL, 0.0),
        'kneeCap': (kcap, kneeL, angle(hipL, kneeL)),
        'upperArm': (up, shA, angle(shA, elA)), 'forearm': (fore, elA, angle(elA, hdA)), 'elbowCap': (ecap, elA, angle(shA, elA)),
    }
    # --- atlas: near pieces, then far copies (darker); shelf packing
    items = []
    for name in LAYERS:
        base = name.replace('Near', '').replace('Far', '')
        img, piv, rest = pieces[base]
        if name.endswith('Far'):
            img = darker(img)
        t, (ox, oy) = trim(img)
        items.append((name, bleed(t), (piv[0] - ox, piv[1] - oy), rest))
    W = 1024
    x = y = rowh = 0
    place = {}
    for name, img, piv, rest in sorted(items, key=lambda it: -it[1].shape[0]):
        h, w = img.shape[:2]
        if x + w > W:
            x, y, rowh = 0, y + rowh + 2, 0
        place[name] = (x, y)
        x += w + 2
        rowh = max(rowh, h)
    H = 1 << int(math.ceil(math.log2(y + rowh)))
    atlas = np.zeros((H, W, 4), np.uint8)
    parts = {}
    for name, img, piv, rest in items:
        px, py = place[name]
        h, w = img.shape[:2]
        atlas[py:py + h, px:px + w] = img
        parts[name] = {'rect': [px, py, w, h], 'pivot': [round(px + piv[0], 2), round(py + piv[1], 2)], 'rest': round(rest, 5)}
    out = pathlib.Path(a.out); out.mkdir(parents=True, exist_ok=True)
    name = f'{a.id}_{a.view}'
    Image.fromarray(atlas).save(out / f'{name}_rig.png')
    rig = {
        '_source': f'D-27: tools/art/d27_rig.py cut из листа частей {pathlib.Path(a.sheet).name} ({a.sheet_source}); суставы — доли {vars_of(a)}; '
                   'приёмы ref2game (MIT, github.com/studioigor/ref2game).',
        'id': a.id, 'view': a.view, 'ppu': 320, 'faces': 'right',
        'atlas': {'file': f'{name}_rig.png', 'w': W, 'h': H},
        'joints': {k: [round(v[0], 2), round(v[1], 2)] for k, v in joints.items()},
        'parts': parts,
    }
    (out / f'{name}.rig.json').write_text(json.dumps(rig, ensure_ascii=False, indent=1), encoding='utf-8')
    print('atlas', W, 'x', H, '->', out / f'{name}_rig.png')
    print('joints', json.dumps(rig['joints']))
    if a.preview:
        rest = rest_placements(rig)
        fig = render_frame(rig, atlas, rest, scale=1.0, marks=True)
        side0 = Image.open(SIDE0).convert('RGBA')
        bb = side0.getbbox(); side0 = side0.crop((bb[0] - 20, bb[1] - 20, bb[2] + 20, bb[3] + 4))
        canvas = Image.new('RGBA', (fig.width + side0.width + 40, max(fig.height, side0.height) + 10), (200, 200, 200, 255))
        canvas.alpha_composite(fig, (0, canvas.height - fig.height))
        canvas.alpha_composite(side0, (fig.width + 40, canvas.height - side0.height))
        canvas.convert('RGB').save(a.preview)
        print('preview', a.preview)


def vars_of(a):
    return {k: getattr(a, k) for k in ('hip', 'shoulder', 'leg_hip', 'leg_knee', 'leg_ankle', 'arm_shoulder', 'arm_elbow', 'arm_hand', 'hip_sep', 'cap', 'foot_up')}


def angle(a, b):
    """Угол кости a→b от «прямо вниз», + — вперёд (вправо), в координатах картинки (y вниз)."""
    return math.atan2(b[0] - a[0], b[1] - a[1])


# ------------------------------------------------------------------ render (preview, frame sheets from a trace)

def rest_placements(rig):
    """Поза «как нарисовано»: каждая часть на своём суставе без поворота (проверка, что сборка = рисунок)."""
    j = {k: tuple(v) for k, v in rig['joints'].items()}
    off = (j['hipFar'][0] - j['hipNear'][0], 0)
    far = lambda p: (p[0] + off[0], p[1])
    shf = (j['shoulderFar'][0] - j['shoulderNear'][0], j['shoulderFar'][1] - j['shoulderNear'][1])
    farA = lambda p: (p[0] + shf[0], p[1] + shf[1])
    at = {'body': j['pelvis'], 'thighNear': j['hipNear'], 'shinNear': j['knee'], 'footNear': j['ankle'], 'kneeCapNear': j['knee'],
          'thighFar': far(j['hipNear']), 'shinFar': far(j['knee']), 'footFar': far(j['ankle']), 'kneeCapFar': far(j['knee']),
          'upperArmNear': j['shoulderNear'], 'forearmNear': j['elbow'], 'elbowCapNear': j['elbow'],
          'upperArmFar': j['shoulderFar'], 'forearmFar': farA(j['elbow']), 'elbowCapFar': farA(j['elbow'])}
    return [(at[n][0] / 320, at[n][1] / 320, 0.0) for n in LAYERS]


def render_frame(rig, atlas, placements, scale=1.0, marks=False, joints=None, pad=(250, 40, 230, 20)):
    """Части по местам (метры риг-пространства, поворот против часовой) на прозрачном холсте; низ — земля."""
    ppu = rig['ppu'] * scale
    L, T, R, B = pad
    top = max(v[1] for v in rig['joints'].values()) * scale
    Wc, Hc = int(L + R), int(top + T + B)
    canvas = Image.new('RGBA', (Wc, Hc), (0, 0, 0, 0))
    gx, gy = L, Hc - B                          # ground point on the canvas
    for name, (x, y, rot) in zip(LAYERS, placements):
        p = rig['parts'][name]
        rx, ry, w, h = p['rect']
        piece = Image.fromarray(atlas[ry:ry + h, rx:rx + w])
        if scale != 1.0:
            piece = piece.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)
        pvx, pvy = (p['pivot'][0] - rx) * scale, (p['pivot'][1] - ry) * scale
        # rotate about the pivot: PIL rotates counter-clockwise for positive angles (image y down = screen up flipped → same sense)
        deg = math.degrees(rot)
        big = Image.new('RGBA', (piece.width * 3 + 8, piece.height * 3 + 8), (0, 0, 0, 0))
        cx, cy = big.width // 2, big.height // 2
        big.alpha_composite(piece, (int(round(cx - pvx)), int(round(cy - pvy))))
        big = big.rotate(deg, resample=Image.BICUBIC, center=(cx, cy))
        dx, dy = gx + x * ppu - cx, gy - y * ppu - cy
        layer = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
        layer.paste(big, (int(round(dx)), int(round(dy))), big)
        canvas = Image.alpha_composite(canvas, layer)
    if marks:
        d = ImageDraw.Draw(canvas)
        pts = joints if joints is not None else [tuple(v) for v in rig['joints'].values()]
        for (x, y) in pts:
            if joints is None:
                X, Y = gx + x * scale, gy - y * scale
            else:
                X, Y = gx + x * ppu, gy - y * ppu
            d.ellipse([X - 3, Y - 3, X + 3, Y + 3], outline=(255, 0, 255, 255), width=1)
    return canvas


def render(a):
    rig = json.loads(pathlib.Path(a.rig).read_text(encoding='utf-8'))
    atlas = np.array(Image.open(pathlib.Path(a.rig).with_name(rig['atlas']['file'])).convert('RGBA'))
    tr = json.loads(pathlib.Path(a.trace).read_text(encoding='utf-8'))
    out = pathlib.Path(a.out); out.mkdir(parents=True, exist_ok=True)
    frames = tr['frames']
    sel = range(a.start, min(len(frames), a.stop if a.stop else len(frames)), a.every)
    for k, i in enumerate(sel):
        fr = frames[i]
        img = render_frame(rig, atlas, [tuple(p) for p in fr['p']], scale=a.scale, marks=a.marks,
                           joints=[tuple(v) for v in fr['j']] if a.marks else None)
        bg = Image.new('RGBA', img.size, (200, 200, 200, 255)); bg.alpha_composite(img)
        bg.convert('RGB').save(out / f'f{k:03d}.png')
    print(len(list(sel)), 'frames ->', out)


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest='cmd', required=True)
    c = sub.add_parser('cut')
    c.add_argument('--sheet', required=True); c.add_argument('--out', required=True); c.add_argument('--preview', default='')
    c.add_argument('--sheet-source', default='', help='модель/запрос/цена — в _source')
    c.add_argument('--id', default='heroine'); c.add_argument('--view', default='side')
    c.add_argument('--height', type=float, default=0, help='рост фигуры, px (0 — как heroine_side_0)')
    c.add_argument('--hip', default='0.61,0.89', help='сустав бедра на теле: доли рамки тела x,y')
    c.add_argument('--shoulder', default='0.53,0.38', help='плечевой сустав на теле: доли рамки тела x,y')
    c.add_argument('--leg-hip', type=float, default=0.17); c.add_argument('--leg-knee', type=float, default=0.5, help='колено: доля hip→ankle')
    c.add_argument('--leg-ankle', type=float, default=0.91); c.add_argument('--foot-up', type=float, default=0.03, help='срез стопы выше лодыжки, доля роста')
    c.add_argument('--arm-shoulder', type=float, default=0.1); c.add_argument('--arm-elbow', type=float, default=0.5); c.add_argument('--arm-hand', type=float, default=0.93)
    c.add_argument('--hip-sep', type=float, default=0.03, help='разнос ближнего и дальнего бедра, доля роста')
    c.add_argument('--shoulder-far-dx', type=float, default=0.025); c.add_argument('--shoulder-far-dy', type=float, default=0.004)
    c.add_argument('--overlap', type=float, default=0.012, help='нахлёст кусков на суставе, доля роста')
    c.add_argument('--cap', type=float, default=1.0, help='радиус шапки сустава / полуширина конечности')
    c.add_argument('--round-thigh', action='store_true', help='верх бедра срезать дугой')
    r = sub.add_parser('render')
    r.add_argument('--rig', required=True); r.add_argument('--trace', required=True); r.add_argument('--out', required=True)
    r.add_argument('--every', type=int, default=1); r.add_argument('--start', type=int, default=0); r.add_argument('--stop', type=int, default=0)
    r.add_argument('--scale', type=float, default=1.0); r.add_argument('--marks', action='store_true')
    a = ap.parse_args()
    {'cut': cut, 'render': render}[a.cmd](a)


if __name__ == '__main__':
    main()
