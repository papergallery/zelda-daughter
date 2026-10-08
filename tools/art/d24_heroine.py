#!/usr/bin/env python3
"""D-24: героиня в манере жителей D-09 — промпты и запуск листов (GPT Image 2.5 flare через Polza, tools/art/polza.py).

Камера, стиль и состав фигур — те же, что у D-09 (`gen_sheets.py`: CAMERA, STYLE, HEROINE_BASE, HEROINE_POSES2).
Новое — «манера жителей»: референсы — сырые base-листы жителей (стиль, пропорции, лицо), старый лист героини — только
одежда (вариант C — без него, одежда только словами).

    d24_heroine.py prompt base A           # напечатать промпт варианта A
    d24_heroine.py run base A B C          # три варианта первого листа (по 7 ₽, 2K, 21:9)
    d24_heroine.py run poses2 A --base WORK/d24-base-A-0.png   # второй лист (шаг от камеры + позы), референс — выбранный base
    d24_heroine.py lineup OUT.png OLD_DIR WORK/d24-base-A-0.png …  # старая (готовые спрайты из OLD_DIR), варианты, 3 жителя:
                                                             # к камере и бок, 320 px/м, линии через 0,5 м

Выход — $ZD_ART_WORK (по умолчанию <_work selection.json>/../d24): d24-<what>-<вариант>-0.png/.json (+ .txt промпт).
"""
import argparse, json, os, pathlib, subprocess, sys

HERE = pathlib.Path(__file__).parent
sys.path.insert(0, str(HERE))
import gen_sheets as gs  # noqa: E402

SEL = gs.SEL
MODEL = gs.MODEL

WHO = ("an ADULT young woman of about twenty-two (a grown-up, not a child, not a teenager), an ordinary person from our modern "
       "world who got lost in a medieval fantasy land. Plain loose short-sleeved OFF-WHITE cotton t-shirt, blue denim jeans rolled "
       "up at the ankles, chunky worn off-white canvas sneakers with no logos, a small olive-green backpack on both shoulders, "
       "messy wavy shoulder-length dark brown hair, slightly tired curious face, empty hands, no weapons.")

MANNER = ("Draw her in EXACTLY the same manner as the villagers on the reference sheets, so that she clearly belongs to the same cast: "
          "the same simplified storybook-caricature face (soft rounded face, simple eyes drawn with a few ink strokes, small button "
          "nose, rosy cheeks, very little shading on the face — NOT a realistic or detailed face, NOT anime, NOT a fashion drawing), "
          "the same sturdy slightly stocky body (head about one fifth of her height, a bit wider waist and shoulders, slightly shorter "
          "legs, LARGE hands and LARGE chunky shoes), the same ink line weight, hatching and watercolor density.")
PUSH = ("Go a touch further into caricature than the villagers: a clearly big round head (about one fifth of her height or a bit "
        "more), short sturdy legs, oversized hands and very big chunky sneakers, a round simple face — but still an adult woman.")

STYLE_REFS = ("The first {n} reference images are character sheets of OTHER characters of this game (villagers). Use them for the "
              "drawing manner, body proportions, face style, line work, watercolor texture, palette, camera angle and scale (an adult "
              "of about 1.65 m is drawn as tall as the women there); do NOT copy their faces, hair, headwear or clothes.")
CLOTHES_REF = ("The LAST reference image is an earlier, too realistic drawing of this same woman: copy ONLY her clothes, colors, "
               "backpack and hairstyle from it — NOT its realistic face, slim proportions or drawing manner.")
SELF_REF = ("The first reference image is the character sheet of exactly this woman: keep the identical face, hair, clothes, colors, "
            "proportions, head size and art style. The other reference images show villagers of the game — use them only for the "
            "drawing manner and camera angle.")

# вариант → (жители-референсы, старый лист героини как референс одежды?, «нажим» карикатуры)
VARIANTS = {
    'A': (['townswoman', 'weaver'], True, False),
    'B': (['peasant', 'townswoman'], True, True),
    'C': (['townswoman', 'weaver', 'peasant'], False, False),
    'D': (['weaver', 'townswoman'], 'C', False),  # круг 2: C читалась подростком (критик) — взрослое лицо, тёмные волосы до плеч
}

# круг 2 (вариант D): замечания критика к C — лицо подростка, рука без цвета, волосы светлее и короче паспорта, слёзы в позах
WHO_D = ("a YOUNG ADULT woman of about 24 (clearly a grown-up woman, not a child, not a teenager), an ordinary person from our "
         "modern world who got lost in a medieval fantasy land: oval face with a defined jaw and chin, a visible neck, adult "
         "shoulders and a slight waist, small almond-shaped eyes drawn as simple ink strokes, calm slightly tired expression. Hair: "
         "dark brown, almost black, loose waves, shoulder length (the ends touch her shoulders). Plain loose short-sleeved OFF-WHITE "
         "cotton t-shirt, blue denim jeans rolled up at the ankles, chunky worn off-white canvas sneakers with no logos, a small "
         "olive-green backpack on both shoulders, empty hands, no weapons. Both arms and hands have the same warm skin tone as her face.")
ADULT_D = ("Proportions of an adult villager like the weaver woman in the first reference: head about one fifth of her height (NOT "
           "bigger), sturdy adult body, big hands and big chunky shoes. NOT a child, NOT a kid, NOT a teen, NOT chibi: no big round "
           "eyes, no rosy blush circles on the cheeks, no tears, no eye bags.")
CLOTHES_C = ("The LAST reference image is an earlier draft of this same woman: keep her clothes, backpack, sneakers, sturdy build and "
             "drawing manner, but there she looked too young — draw her face and neck clearly adult (as described) and her hair "
             "darker and longer, down to the shoulders.")
POSES2_D = list(gs.HEROINE_POSES2)
POSES2_D[2] = ("side profile facing right (the same strict side view as the next drawing), swinging a short wooden stick back over "
               "her shoulder (wind-up of a strike)")
POSES2_D[5] = "front view, standing hunched, clutching her side with one hand, grimacing in pain (dry eyes, no tears)"
POSES2_D[6] = "front view, standing and taking a bite of a piece of bread held with both hands (no crumbs)"


# круг 3 (критик, круг 2): attack_0 снова вышел в 3/4 — отдельная пара «стоит боком (как в референсе) + замах строго боком»;
# стоящая фигура — опора масштаба (match side_0)
ATTACK0 = ['standing, strict side profile view facing right, exactly as the standing side view in the reference sheet',
           'strict side profile facing right, torso turned fully sideways, only the near shoulder visible, chest not visible, the '
           'backpack seen from the side exactly as in the standing side view; she swings a short wooden stick back: the stick is '
           'raised high above and behind her head, clearly separated from the hair, its silhouette against the empty background '
           '(wind-up of a strike). NOT a three-quarter view, NOT a frontal torso.']


def attack0_prompt():
    lines = ['Game character sprite sheet for a 2D storybook game: 2 full-body drawings of ONE and the same person side by side, '
             'evenly spaced with a wide empty gap, nothing overlapping, the same height and scale, feet on one horizontal ground line:']
    lines += [f'{i + 1}) {f};' for i, f in enumerate(ATTACK0)]
    lines += [gs.CAMERA, 'The person: ' + WHO_D, gs.STYLE, MANNER, ADULT_D,
              'The reference image is the character sheet of exactly this woman: keep the identical face, hair, clothes, colors, '
              'proportions, head size and art style.']
    return '\n'.join(lines)


def work():
    return pathlib.Path(os.environ.get('ZD_ART_WORK', SEL['_work'] + '/../d24'))


def url_of(png):
    return json.loads(pathlib.Path(png).with_suffix('.json').read_text(encoding='utf-8'))['url']


def villager_url(cid):
    return url_of(pathlib.Path(SEL['_work']) / SEL[cid]['sources'][0]['file'])


def prompt(what, var):
    vil, clothes, push = VARIANTS[var]
    figs = gs.HEROINE_BASE if what == 'base' else (POSES2_D if var == 'D' else gs.HEROINE_POSES2)
    lines = [f'Game character sprite sheet for a 2D storybook game: {len(figs)} full-body drawings of ONE and the same person in a single '
             'row, evenly spaced with wide empty gaps, nothing overlapping, all the same height and scale, all feet on one horizontal '
             'ground line:']
    lines += [f'{i + 1}) {f};' for i, f in enumerate(figs)]
    lines += [gs.CAMERA, 'The person: ' + (WHO_D if var == 'D' else WHO), gs.STYLE, MANNER]
    if var == 'D':
        lines.append(ADULT_D)
    if push:
        lines.append(PUSH)
    if what == 'base':
        lines.append(STYLE_REFS.format(n=len(vil)))
        if clothes == 'C':
            lines.append(CLOTHES_C)
        elif clothes:
            lines.append(CLOTHES_REF)
    else:
        lines.append(SELF_REF)
        lines.append('Exactly the same camera angle, head size and body scale as the first reference image.')
    lines.append('All drawings use exactly the same camera angle and the same head size.')
    return '\n'.join(lines)


def refs(what, var, base=None):
    vil, clothes, _ = VARIANTS[var]
    if what == 'base':
        r = [villager_url(c) for c in vil]
        if clothes == 'C':
            r.append(url_of(pathlib.Path(SEL['_work']) / '../d24/d24-base-C-0.png'))
        elif clothes:
            r.append(url_of(pathlib.Path(SEL['_work']) / SEL['heroine']['sources'][0]['file']))
        return r
    return [url_of(base)] + [villager_url(c) for c in vil[:2]]


# подбородок, % роста от макушки (к камере, бок) — замер глазом по сетке `proportions.py … --grid`, 2026-10-08
CHINS = {'старая (D-09)': (22.5, 21), 'вар. A': (25, 22.5), 'вар. B': (26, 23.5), 'вар. C': (25.5, 22.5),
         'итог D': (25, 22.5), 'townswoman': (22.5, 20.5), 'weaver': (26, 20.5), 'herbalist': (23.5, 21.5)}


def lineup(out, old_dir, sheets, villagers=('townswoman', 'weaver', 'herbalist'), k=0.6):
    """Ряд «к камере» и ряд «бок» в одном масштабе (320 px/м). Варианты масштабируются по фигуре к камере листа
    (рост 1,65 м по body_top_bottom, как sprite_build build), бок — тем же множителем. Под фигурой: голова/рост (по CHINS,
    красная черта — подбородок) и ширина/рост (80-й перцентиль ширины строк)."""
    import numpy as np
    from PIL import Image, ImageDraw
    import sprite_build as sb
    ppm = sb.CFG['ppm']
    S = sb.ROOT / 'ZeldaDaughter/Assets/Art/Sprites'
    cols = []  # (подпись, front RGBA, side RGBA)

    def spr(path):
        im = np.asarray(Image.open(path).convert('RGBA')).astype(np.float32) / 255
        rows = np.where((im[..., 3] > 0.02).any(1))[0]
        return im[:rows[-1] + 1]
    od = pathlib.Path(old_dir)
    cols.append(('старая (D-09)', spr(od / 'heroine_front_0.png'), spr(od / 'heroine_side_0.png')))
    for sh in sheets:
        rgba = sb.cutout(pathlib.Path(sh))
        figs = sb.figures(rgba)
        f, sd = sb.only_figure(rgba, figs[0]), sb.only_figure(rgba, figs[1])
        t, b = sb.body_top_bottom(f[..., 3])
        s = 1.65 * ppm / (b - t + 1)
        cols.append((pathlib.Path(sh).stem.replace('d24-base-', 'вар. ').replace('-0', ''), sb.scaled(f, s), sb.scaled(sd, s)))
    cols.append(('итог D', spr(S / 'heroine/heroine_front_0.png'), spr(S / 'heroine/heroine_side_0.png')))
    for c in villagers:
        cols.append((c, spr(S / c / f'{c}_front_0.png'), spr(S / c / f'{c}_side_0.png')))
    try:
        from PIL import ImageFont
        font = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 16)
    except OSError:
        font = None
    rows_img = []
    for idx in (1, 2):
        cells = []
        for name, *fs in cols:
            p = fs[idx - 1]
            a = p[..., 3]
            rr = np.where((a > 0.02).any(1))[0]; cc = np.where((a > 0.02).any(0))[0]
            p = p[:rr[-1] + 1, cc[0]:cc[-1] + 1]
            hh = rr[-1] - rr[0] + 1
            wr = np.percentile((p[..., 3] > 0.5).sum(1)[rr[0]:], 80) / hh
            chin = CHINS.get(name, (None, None))[idx - 1]
            cells.append((name, sb.to_img(p), chin, hh, rr[0], wr))
        H = int(2.0 * ppm * k) + 76
        W = sum(max(int(c.width * k), 150) + 30 for _, c, *_ in cells) + 30
        im = Image.new('RGB', (W, H), sb.LIGHT); dr = ImageDraw.Draw(im)
        gy = H - 64
        for m in np.arange(0, 2.01, 0.5):
            dr.line([0, gy - int(m * ppm * k), W, gy - int(m * ppm * k)], fill=(120, 110, 95), width=2 if m % 1 == 0 else 1)
        x = 30
        for name, c, chin, hh, top, wr in cells:
            c = c.resize((int(c.width * k), int(c.height * k)), Image.Resampling.LANCZOS)
            y0 = gy - c.height
            im.paste(c, (x, y0), c)
            txt = name
            if chin:
                yc = y0 + int((top + chin / 100 * hh) * k)
                dr.line([x - 6, yc, x + c.width + 6, yc], fill=(200, 30, 30), width=1)
                txt += f'\nголова 1/{100 / chin:.1f}'
            txt += f'\nширина {wr:.2f}'
            dr.multiline_text((x, gy + 4), txt, fill=(40, 30, 20), font=font, spacing=1)
            x += max(c.width, 150) + 30
        rows_img.append(im)
    W = max(r.width for r in rows_img)
    o = Image.new('RGB', (W, sum(r.height for r in rows_img)), sb.LIGHT)
    y = 0
    for r in rows_img:
        o.paste(r, (0, y)); y += r.height
    o.save(out, optimize=True); print(out, o.size)


def main():
    ap = argparse.ArgumentParser()
    if sys.argv[1:2] == ['attack0']:  # d24_heroine.py attack0 [prompt]
        p = attack0_prompt()
        if sys.argv[2:3] == ['prompt']:
            print(p); return
        out = work(); pf = out / 'd24-attack0-D.txt'; pf.write_text(p, encoding='utf-8')
        subprocess.run([sys.executable, str(HERE / 'polza.py'), 'gen', '--model', MODEL, '--out', str(out), '--name', 'd24-attack0-D',
                        '--prompt-file', str(pf), '--ar', '3:2', '--res', '1K', '--ref', url_of(out / 'd24-base-D-0.png')])
        return
    if sys.argv[1:2] == ['lineup']:
        lineup(sys.argv[2], sys.argv[3], sys.argv[4:]); return
    ap.add_argument('cmd', choices=['prompt', 'run']); ap.add_argument('what', choices=['base', 'poses2'])
    ap.add_argument('variants', nargs='+'); ap.add_argument('--base'); ap.add_argument('--tag', default='')
    a = ap.parse_args()
    out = work(); out.mkdir(parents=True, exist_ok=True)
    procs = []
    for v in a.variants:
        p = prompt(a.what, v)
        if a.cmd == 'prompt':
            print(p, '\nREFS:', refs(a.what, v, a.base)); continue
        name = f'd24-{a.what}-{v}{a.tag}'
        pf = out / f'{name}.txt'; pf.write_text(p, encoding='utf-8')
        cmd = [sys.executable, str(HERE / 'polza.py'), 'gen', '--model', MODEL, '--out', str(out), '--name', name,
               '--prompt-file', str(pf), '--ar', '21:9', '--res', '2K']
        for r in refs(a.what, v, a.base):
            cmd += ['--ref', r]
        procs.append(subprocess.Popen(cmd))
    for pr in procs:
        pr.wait()


if __name__ == '__main__':
    main()
