#!/usr/bin/env python3
"""D-09: иконки предметов и разговора — листы 3×3 одной генерацией Klein, нарезка на сервере.

    icon_build.py prompts OUTDIR            # группы и промпты: OUTDIR/icons-<n>.txt (+ icons-<n>.ids: сетка и id)
    icon_build.py gen OUTDIR STYLE_URL [n…] # листы через Polza; маска — keymask.py
    icon_build.py cut WORK/icons-1-s3.png   # вырезка по маске (BiRefNet ∪ ключ по серому фону), 9 ячеек, превью
    icon_build.py place WORK/icons-1-s3.png 0=item:stick 1=item:short_stick 4=talk:town ...
                                            # ячейка → Assets/Art/Icons/<id>.png (talk: talk_<id>.png), 256×256
    icon_build.py auto WORK/icons-1-0.png   # то же, ячейки по порядку из icons-1.ids
    icon_build.py sheet                     # лист для просмотра docs/demo/sprites/icons-sheet.png + реестры

Сетка: предметы 4×3, разговор 5×2. Иконки разговора лежат как `talk_<id>.png`: id map/coin/hammer/letter/locket есть и у предметов, а картинки разные.
"""
import json, pathlib, sys
sys.path.insert(0, str(pathlib.Path(__file__).parent))
import style  # noqa: E402  (docs/demo/sprites/style-bible.md)

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import sprite_build as sb  # noqa: E402

ROOT = sb.ROOT
CFG = json.loads((ROOT / 'tools/art/icons.json').read_text(encoding='utf-8'))
ICONS = ROOT / 'ZeldaDaughter/Assets/Art/Icons'
REG = ROOT / 'ZeldaDaughter/Assets/Art/Registries'


def groups():
    """Листы: предметы по 12 (4×3), разговор + hint_hand по 10 (5×2)."""
    items = [('item', k) for k in CFG['items']]
    talk = [('talk', k) for k in CFG['talk']]
    out = [(g, 4, 3) for g in (items[i:i + 12] for i in range(0, len(items), 12))]
    out += [(g, 5, 2) for g in (talk[i:i + 10] for i in range(0, len(talk), 10))]
    return out


def prompt(g, cols, rows):
    lst = '; '.join(f'{i + 1}) {CFG["items" if k == "item" else "talk"][x]}' for i, (k, x) in enumerate(g))
    return CFG['sheet'].format(n=len(g), cols=cols, rows=rows, list=lst) + ' ' + style.STYLE + ' ' + style.BG_GREY + \
        ' The reference image shows a character from the same game: use it only for the art style, ink line and palette.'


def cmd_prompts(out):
    out = pathlib.Path(out); out.mkdir(parents=True, exist_ok=True)
    for n, (g, cols, rows) in enumerate(groups(), 1):
        (out / f'icons-{n}.txt').write_text(prompt(g, cols, rows), encoding='utf-8')
        (out / f'icons-{n}.ids').write_text(f'{cols}x{rows}\n' + '\n'.join(f'{k}:{x}' for k, x in g), encoding='utf-8')
        print(n, cols, rows, [x for _, x in g])


def cmd_gen(out, style_ref_url, *nums):
    """Листы через Polza (GPT Image 2.5 flare, 1K): icons-<n>-0.png + .json в out."""
    import subprocess
    cmd_prompts(out)
    procs = []
    for n, (g, cols, rows) in enumerate(groups(), 1):
        if nums and str(n) not in nums:
            continue
        procs.append(subprocess.Popen([sys.executable, str(pathlib.Path(__file__).with_name('polza.py')), 'gen', '--model',
                                       'openai/gpt-image-2.5-flare', '--out', out, '--name', f'icons-{n}', '--prompt-file',
                                       str(pathlib.Path(out) / f'icons-{n}.txt'), '--ref', style_ref_url,
                                       '--ar', '4:3' if cols == 4 else '21:9', '--res', '1K']))
    for p in procs:
        p.wait()


def cut_alpha(png):
    png = pathlib.Path(png)
    img = np.asarray(Image.open(png).convert('RGB')).astype(np.float32) / 255
    m = Image.open(png.with_name(png.stem + '.mask.png')).convert('L').resize((img.shape[1], img.shape[0]))
    mb = np.asarray(m).astype(np.float32) / 255
    border = np.concatenate([img[:8].reshape(-1, 3), img[-8:].reshape(-1, 3), img[:, :8].reshape(-1, 3), img[:, -8:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    dist = np.sqrt(((img - bg) ** 2).sum(-1))
    mk = np.clip((dist - 0.05) / 0.10, 0, 1)
    a = np.maximum(mb, mk)
    # замкнутые окна фона (кольцо цепочки, ручка кружки): острова цвета фона от 60 px — по ключу
    isl, n = ndimage.label((a > 0.5) & (dist < 0.06))
    if n:
        sz = ndimage.sum(np.ones_like(a), isl, index=np.arange(1, n + 1))
        big = np.zeros(n + 1, bool); big[1:] = sz >= 60
        a = np.where(ndimage.binary_dilation(big[isl], iterations=2), mk, a)
    a = np.where(a < 0.04, 0, a)
    lab, n = ndimage.label(a > 0.15)
    sizes = ndimage.sum(np.ones_like(a), lab, index=np.arange(1, n + 1)) if n else np.array([])
    keep = np.zeros(n + 1, bool)
    if n:
        keep[1:] = sizes >= 300
    a = a * ndimage.binary_dilation(keep[lab], iterations=2)
    safe = np.maximum(a, 0.08)[..., None]
    fg = np.clip((img - (1 - a[..., None]) * bg) / safe, 0, 1)
    fg = np.where(a[..., None] > 0.9, img, fg)
    return np.dstack([fg, a]), lab, keep


def cells(png, cols=3, rows=3):
    rgba, lab, keep = cut_alpha(png)
    H, W = rgba.shape[:2]
    out = [np.zeros_like(rgba) for _ in range(cols * rows)]
    for i in np.where(keep)[0]:
        ys, xs = np.where(lab == i)
        cy, cx = ys.mean(), xs.mean()
        c = min(int(cy / H * rows), rows - 1) * cols + min(int(cx / W * cols), cols - 1)
        reg = ndimage.binary_dilation(lab == i, iterations=3)
        out[c][reg] = rgba[reg]
    return out


def fit(rgba, size, pad=14):
    a = rgba[..., 3]
    if a.max() < 0.1:
        return None
    ys, xs = np.where(a > 0.02)
    crop = rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    s = (size - 2 * pad) / max(crop.shape[:2])
    p = sb.scaled(crop, s)
    p = sb.palette_soft(p, sb.CFG['palette_mix'])
    canvas = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    canvas.alpha_composite(sb.to_img(p), ((size - p.shape[1]) // 2, (size - p.shape[0]) // 2))
    return canvas


def grid_of(png):
    ids = pathlib.Path(png).with_name(pathlib.Path(png).stem.rsplit('-', 1)[0] + '.ids')
    cols, rows = ids.read_text(encoding='utf-8').splitlines()[0].split('x')
    return int(cols), int(rows)


def cmd_cut(png):
    cols, rows = grid_of(png)
    cs = cells(png, cols, rows)
    prev = Image.new('RGB', (cols * 260, rows * 260), sb.LIGHT)
    dr = ImageDraw.Draw(prev)
    for i, c in enumerate(cs):
        im = fit(c, 256)
        if im:
            prev.paste(im, ((i % cols) * 260, (i // cols) * 260), im)
        dr.text(((i % cols) * 260 + 4, (i // cols) * 260 + 4), str(i), fill=(200, 0, 0))
    out = pathlib.Path(png).with_suffix('.cells.png'); prev.save(out); print(out)


def cmd_place(png, *pairs):
    cs = cells(png, *grid_of(png))
    ICONS.mkdir(parents=True, exist_ok=True)
    for p in pairs:
        idx, kid = p.split('=')
        kind, iid = kid.split(':')
        im = fit(cs[int(idx)], CFG['size'])
        name = f'talk_{iid}.png' if kind == 'talk' else f'{iid}.png'
        im.save(ICONS / name, optimize=True)
        print(ICONS / name, '<-', png, idx)


def cmd_auto(png):
    """Ячейки по порядку из .ids (как в промпте: по строкам слева направо)."""
    ids = pathlib.Path(png).with_name(pathlib.Path(png).stem.rsplit('-', 1)[0] + '.ids').read_text(encoding='utf-8').splitlines()[1:]
    cmd_place(png, *[f'{i}={x}' for i, x in enumerate(ids)])


def cmd_sheet():
    files = [(k, i, ICONS / (f'talk_{i}.png' if k == 'talk' else f'{i}.png')) for k in ('item', 'talk')
             for i in CFG['items' if k == 'item' else 'talk']]
    have = [f for f in files if f[2].exists()]
    cols = 9; cell = 140
    rows = (len(have) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * cell * 2, rows * cell), sb.LIGHT)
    dr = ImageDraw.Draw(sheet)
    for n, (k, i, f) in enumerate(have):
        im = Image.open(f).resize((120, 120), Image.Resampling.LANCZOS)
        x, y = (n % cols) * cell, (n // cols) * cell
        sheet.paste(im, (x + 10, y + 4), im)
        dark = Image.new('RGB', (cell, cell), sb.DARK)
        dark.paste(im, (10, 4), im)
        sheet.paste(dark, (cols * cell + x, y))
        dr.text((x + 4, y + cell - 14), i if k == 'item' else 'talk:' + i, fill=(90, 70, 50))
    sheet.quantize(256, method=Image.Quantize.MEDIANCUT).save(sb.DOCS / 'icons-sheet.png', optimize=True); print(sb.DOCS / 'icons-sheet.png', len(have), '/', len(files))
    # реестры: строка — запись, по алфавиту
    for fn, kind in (('item-icons.json', 'item'), ('talk-icons.json', 'talk')):
        p = REG / fn
        txt = p.read_text(encoding='utf-8')
        d = json.loads(txt)
        for k, i, f in have:
            if k == kind:
                d['icons'][i] = {'path': 'Assets/Art/Icons/' + f.name}
        lines = ',\n'.join(f'    {json.dumps(k)}: {json.dumps(v, ensure_ascii=False)}' for k, v in sorted(d['icons'].items()))
        head = {k: v for k, v in d.items() if k != 'icons'}
        body = '{\n' + ''.join(f'  {json.dumps(k)}: {json.dumps(v, ensure_ascii=False)},\n' for k, v in head.items()) + \
               '  "icons": {\n' + lines + '\n  }\n}\n'
        p.write_text(body, encoding='utf-8'); print(p)


if __name__ == '__main__':
    c, *args = sys.argv[1:] or ['help']
    {'prompts': cmd_prompts, 'gen': cmd_gen, 'cut': cmd_cut, 'place': cmd_place, 'auto': cmd_auto, 'sheet': cmd_sheet}.get(c, lambda *a: print(__doc__))(*args)
