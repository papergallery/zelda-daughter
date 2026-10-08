#!/usr/bin/env python3
"""D-27 (критерий 4): склейки и GIF «вырезной риг | видео-кадры D-25 (Seedance)» для критика.

    # без Unity: риг по трассе ядра против кадров heroine_side_1..12 по тому же пути (фаза = путь / 1,7 м)
    python3 tools/art/d27_sheets.py core --rig .../heroine_side.rig.json --trace trace.json --out docs/demo/frames/D-27-core
    # из игры: папки кадров D27Frames (rig/, video/) — кропы 360×420 вокруг героини с кадра 1080×2340
    python3 tools/art/d27_sheets.py game --dir zd-frames-d27 --out docs/demo/frames/D-27-game

Выход: <out>-strip.jpg (полцикла через кадр, риг сверху, видео снизу), <out>.gif (бок о бок, 30 к/с, ×2 для крупного плана при --zoom).
"""
import argparse, glob, json, math, pathlib, sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import d27_rig  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parents[2]
SPR = ROOT / 'ZeldaDaughter/Assets/Art/Sprites/heroine'
BG = (200, 200, 200, 255)


def label(img, text, size=16):
    from PIL import ImageFont
    try:
        font = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', size)
    except OSError:
        font = ImageFont.load_default()
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = d.textbbox((6, 4), text, font=font)
    d.rectangle([0, 0, x1 + 6, y1 + 4], fill=(255, 255, 255, 220))
    d.text((6, 4), text, fill=(0, 0, 0, 255), font=font)
    return img


VIDEO = SPR


def video_frame(phase, n=12):
    """Кадр видео-цикла D-25 по фазе пути (12 кадров side_1..12 героини D; после D-24b их нет в master — `--video-dir`,
    достать: `git archive 7df35a62 ZeldaDaughter/Assets/Art/Sprites/heroine`)."""
    k = 1 + int(phase * n) % n
    return Image.open(VIDEO / f'heroine_side_{k}.png').convert('RGBA')


def core(a):
    global VIDEO
    if a.video_dir:
        VIDEO = pathlib.Path(a.video_dir)
    rig = json.loads(pathlib.Path(a.rig).read_text(encoding='utf-8'))
    atlas = np.array(Image.open(pathlib.Path(a.rig).with_name(rig['atlas']['file'])).convert('RGBA'))
    tr = json.loads(pathlib.Path(a.trace).read_text(encoding='utf-8'))
    fr = [f for f in tr['frames'] if f['gait'] >= 1.0]
    step = max(1, round(tr['fps'] / a.fps))
    cyc = int(round(tr['stride'] / tr['speed'] * tr['fps']))      # frames per cycle
    sel = fr[:2 * cyc:step]
    pairs = []
    for f in sel:
        r = d27_rig.render_frame(rig, atlas, [tuple(p) for p in f['p']])
        v = video_frame(f['phase'])
        # the video frame on the same canvas: its pivot (feet) on the rig's ground point
        vc = Image.new('RGBA', r.size, (0, 0, 0, 0))
        vc.alpha_composite(v, (int(250 - v.width / 2), int(r.height - 20 - v.height + 3)))
        pairs.append((r, vc))
    out = pathlib.Path(a.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    write(pairs, out, ('риг (ядро), героиня G', 'видео-кадры D-25 Seedance, героиня D'), a)


def game(a):
    d = pathlib.Path(a.dir)
    rig = sorted(glob.glob(str(d / 'rig' / 'run-*.png')))
    vid = sorted(glob.glob(str(d / 'video' / 'run-*.png')))
    n = min(len(rig), len(vid))
    pairs = [(Image.open(rig[i]).convert('RGBA'), Image.open(vid[i]).convert('RGBA')) for i in range(n)]
    out = pathlib.Path(a.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    write(pairs, out, ('риг в игре', 'видео-кадры в игре'), a)
    for tag in ('rig', 'video'):
        st = sorted(glob.glob(str(d / tag / 'stand-*.png')))
        if st:
            fr = [Image.open(f).convert('RGB') for f in st]
            if a.zoom != 1:
                fr = [f.resize((f.width * a.zoom, f.height * a.zoom), Image.LANCZOS) for f in fr]
            fr[0].save(f'{out}-stand-{tag}.gif', save_all=True, append_images=fr[1:], duration=100, loop=0)
        full = sorted(glob.glob(str(d / tag / '*-full.png')))
        for f in full[:1]:
            Image.open(f).convert('RGB').save(f'{out}-full-{tag}.jpg', quality=88)


def write(pairs, out, names, a):
    w, h = pairs[0][0].size
    z = a.zoom
    frames = []
    for r, v in pairs:
        c = Image.new('RGBA', (2 * w + 10, h), BG)
        c.alpha_composite(r, (0, 0))
        c.alpha_composite(v, (w + 10, 0))
        if z != 1 or a.gscale != 1:
            c = c.resize((round(c.width * z * a.gscale), round(c.height * z * a.gscale)), Image.LANCZOS)
        label(c, f'{names[0]}  |  {names[1]}')
        frames.append(c.convert('RGB'))
    frames[0].save(f'{out}.gif', save_all=True, append_images=frames[1:], duration=int(1000 / a.fps), loop=0)
    # strip: half a cycle every other frame, rig over video
    k = max(1, len(pairs) // 2 // a.strip)
    pick = pairs[:len(pairs) // 2:k][:a.strip]
    s = Image.new('RGBA', (w * len(pick), 2 * h + 10), BG)
    for i, (r, v) in enumerate(pick):
        s.alpha_composite(r, (i * w, 0))
        s.alpha_composite(v, (i * w, h + 10))
    label(s, f'сверху {names[0]}, снизу {names[1]}; полцикла')
    s.convert('RGB').save(f'{out}-strip.jpg', quality=88)
    print(f'{out}.gif ({len(frames)} frames), {out}-strip.jpg')


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest='cmd', required=True)
    c = sub.add_parser('core'); c.add_argument('--rig', required=True); c.add_argument('--trace', required=True); c.add_argument('--out', required=True)
    c.add_argument('--video-dir', default='', help='кадры бега Seedance D-25 (heroine_side_1..12.png)')
    g = sub.add_parser('game'); g.add_argument('--dir', required=True); g.add_argument('--out', required=True)
    for p in (c, g):
        p.add_argument('--fps', type=float, default=30); p.add_argument('--zoom', type=int, default=1); p.add_argument('--strip', type=int, default=8)
        p.add_argument('--gscale', type=float, default=1.0, help='масштаб GIF (размер файла)')
    a = ap.parse_args()
    {'core': core, 'game': game}[a.cmd](a)


if __name__ == '__main__':
    main()
