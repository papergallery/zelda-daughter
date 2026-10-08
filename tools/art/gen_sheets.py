#!/usr/bin/env python3
"""D-09: промпты и запуск листов персонажей в облаке Polza (GPT Image 2.5, решение в docs/demo/decisions.md).

Два листа на персонажа, оба 16:9 2K, фигуры в ряд на ровном светло-сером фоне:
  base  — стоит: к камере, бок (вправо), от камеры; шаг боком: левая нога вперёд, правая вперёд.
          Референс — лист героини (только стиль, линия, палитра, ракурс камеры, масштаб).
  extra — шаг к камере ×2, шаг от камеры ×2 и позы из паспорта (sprites.json: poses).
          Референсы — свой base-лист (кто это) и лист героини (стиль).
Героиня — base без стилевого референса (она сама эталон), extra — свои позы (HEROINE_POSES).

    gen_sheets.py prompt boar base               # напечатать промпт
    gen_sheets.py run boar base [--variants 2] [--model openai/gpt-image-2.5-flare]
    gen_sheets.py run boar extra --base WORK/boar-base-a-0.png

Результаты — в $ZD_ART_WORK (selection.json → _work); рядом .json с промптом, моделью, URL и ценой.
"""
import argparse, json, os, pathlib, subprocess, sys

HERE = pathlib.Path(__file__).parent
CFG = json.loads((HERE / 'sprites.json').read_text(encoding='utf-8'))
SEL = json.loads((HERE / 'selection.json').read_text(encoding='utf-8'))
MODEL = 'openai/gpt-image-2.5-flare'

CAMERA = ("Camera: HIGH ANGLE, the same for every drawing. The viewer stands above and looks down at about 30 degrees, like the camera "
          "of an isometric top-down game (Don't Starve): the top of the head and the shoulders are seen from above, the body is slightly "
          "foreshortened toward the feet, and the feet are seen from above.")
CAMERA_ANIMAL = ("Camera: HIGH ANGLE, the same for every drawing. The viewer stands above and looks down at about 30 degrees, like the camera "
                 "of an isometric top-down game (Don't Starve): the back and the top of the head are seen from above.")
STYLE = ("Art style: hand-drawn storybook illustration made with a dip pen and watercolor — uneven brown-black ink outlines with a bit of "
         "scratchy hatching, loose transparent watercolor washes, muted warm earthy palette (ochre, olive, umber, rust, dusty red), slightly "
         "exaggerated whimsical proportions in the spirit of Don't Starve. Flat even light, no cast shadows, no ground, plain flat uniform "
         "light grey background. No text, no labels, no numbers, no logos, no watermark, no signature, no frame.")
STYLE_REF = ("The reference image shows a DIFFERENT character (the game's heroine). Use it ONLY for the art style, line work, watercolor "
             "texture, palette, camera angle and scale (an adult of about 1.65 m is drawn as tall as she is); do NOT copy her face, hair, "
             "clothes or backpack.")
SELF_REF = ("The first reference image is the character sheet of exactly this {noun}: keep the identical face, {fur}, colors, proportions "
            "and art style. The second reference image shows the game's heroine — use it only for style and camera angle.")

HUMAN_BASE = ["standing, front view facing the viewer, arms relaxed",
              "standing, side profile view facing right",
              "standing, back view seen from behind",
              "walking, side profile facing right, mid-stride with the left leg forward and the right arm swinging forward",
              "walking, side profile facing right, mid-stride with the right leg forward and the left arm swinging forward"]
ANIMAL_BASE = ["standing, three-quarter front view, coming toward the viewer",
               "standing, side profile view facing right",
               "standing, three-quarter back view, walking away from the viewer",
               "walking, side profile facing right, left front leg and right hind leg stepping forward",
               "walking, side profile facing right, right front leg and left hind leg stepping forward"]
HUMAN_WALK = ["walking toward the viewer (front view), mid-stride, left leg forward and right arm swinging forward",
              "walking toward the viewer (front view), mid-stride, right leg forward and left arm swinging forward",
              "walking away from the viewer (back view), mid-stride, left leg forward",
              "walking away from the viewer (back view), mid-stride, right leg forward"]
ANIMAL_WALK = ["walking toward the viewer, three-quarter front view, left front leg stepping forward",
               "walking toward the viewer, three-quarter front view, right front leg stepping forward",
               "walking away from the viewer, three-quarter back view, left hind leg stepping forward",
               "walking away from the viewer, three-quarter back view, right hind leg stepping forward"]
ANIMAL_POSES = ["crouched low and tense right before charging, side profile facing right, head down, about to leap (wind-up)",
                "lying dead on its side, legs limp, eyes closed, seen from above (a carcass), side view, head to the right"]
SIDE_WALK_PAIR = ["walking, side profile facing right, the leg NEAREST to the viewer is forward and the far arm swings forward",
                  "walking, side profile facing right, the OPPOSITE phase of the previous drawing: the leg nearest to the viewer is BEHIND, "
                  "the far leg is forward, and the near arm swings forward"]
HEROINE_BASE = (["standing, front view facing the viewer, arms relaxed", "standing, side profile view facing right",
                 "standing, back view seen from behind (backpack visible)"] + SIDE_WALK_PAIR +
                ["walking toward the viewer (front view), mid-stride, her left leg forward and right arm swinging forward",
                 "walking toward the viewer (front view), mid-stride, her right leg forward and left arm swinging forward"])
HEROINE_POSES2 = ["walking away from the viewer (back view, backpack visible), mid-stride, left leg forward",
                  "walking away from the viewer (back view, backpack visible), mid-stride, right leg forward",
                  "side profile facing right, swinging a short wooden stick back over her shoulder (wind-up of a strike)",
                  "side profile facing right, lunging forward and striking with the short wooden stick at arm's length",
                  "side profile facing right, squatting down and reaching to the ground with one hand to pick something up",
                  "front view, standing hunched, clutching her side with one hand in pain",
                  "front view, standing and taking a bite of a piece of bread held with both hands",
                  "lying unconscious flat on her back ON THE GROUND, arms spread, eyes closed, body horizontal with her head to the left, "
                  "the whole body inside the picture, nothing under her head"]
ANIMAL_FIX = ["walking, side profile facing right, the OPPOSITE phase of the reference walking drawings: the legs nearest to the viewer "
              "are BEHIND and the far legs are forward",
              "dead, lying FLAT on its side ON THE GROUND, legs limp and stretched sideways, eyes closed, seen from slightly above, "
              "the body low and touching the ground"]
HEROINE_POSES = {
    'attack': ["side profile facing right, swinging a short wooden stick back over her shoulder (wind-up of a strike)",
               "side profile facing right, lunging forward and striking with the short wooden stick at arm's length"],
    'pickup': ["front view, squatting down and reaching to the ground with one hand to pick something up",
               "side profile facing right, squatting down and reaching to the ground with one hand"],
    'hurt': ["front view, standing hunched, clutching her side with one hand in pain",
             "side profile facing right, standing hunched, clutching her side with one hand in pain"],
    'eat': ["front view, standing and taking a bite of a piece of bread held with both hands",
            "side profile facing right, standing and taking a bite of a piece of bread held with both hands"],
    'down': ["lying unconscious on her back on the ground, arms spread, eyes closed, seen from above, body horizontal with her head to the left"],
}


def figures(cid, what):
    ch = CFG['characters'][cid]
    human = ch['kind'] == 'human'
    if what == 'base' and cid == 'heroine':
        return HEROINE_BASE
    if what == 'base':
        return HUMAN_BASE if human else ANIMAL_BASE
    if what == 'poses2':
        return HEROINE_POSES2
    if what == 'fix':
        return ANIMAL_FIX
    if what == 'poses' and cid == 'heroine':
        return [f for fs in HEROINE_POSES.values() for f in fs]
    figs = list(HUMAN_WALK if human else ANIMAL_WALK)
    if not human:
        figs += ANIMAL_POSES
    for name, desc in ch.get('poses', {}).items():
        if name == 'point':
            figs += [f'front view, {desc}', f'side profile facing right, {desc}']
        elif name == 'strike':
            figs += ['side profile facing right, hammer raised high above his head before a strike',
                     'side profile facing right, hammer brought down hard in front of him at waist height (the strike)']
    return figs


def prompt(cid, what, style_ref=True):
    ch = CFG['characters'][cid]
    human = ch['kind'] == 'human'
    noun = 'person' if human else 'animal'
    figs = figures(cid, what)
    lines = [f'Game character sprite sheet for a 2D storybook game: {len(figs)} full-body drawings of ONE and the same {noun} in a single row, '
             'evenly spaced with wide empty gaps, nothing overlapping, all the same height and scale, all feet on one horizontal ground line:']
    lines += [f'{i + 1}) {f};' for i, f in enumerate(figs)]
    lines.append(CAMERA if human else CAMERA_ANIMAL)
    lines.append(('The person: ' if human else 'The animal: ') + ch['who'])
    lines.append(STYLE)
    if what == 'base' and style_ref and cid != 'heroine':
        lines.append(STYLE_REF)
    if what == 'base' and cid == 'heroine' and style_ref:
        lines.append('The reference image is an earlier sheet of this same woman: keep her face, hair, jeans, sneakers, backpack, '
                     'proportions, camera angle and art style, but her t-shirt is now plain OFF-WHITE. All drawings use exactly the same '
                     'camera angle and the same head size.')
    if what != 'base':
        lines.append(SELF_REF.format(noun=noun, fur='hair, clothes' if human else 'fur'))
        lines.append('Exactly the same camera angle, head size and body scale as the first reference image.')
    return '\n'.join(lines)


def heroine_url():
    meta = pathlib.Path(SEL['_work']) / (SEL['heroine']['sources'][0]['file'].replace('.png', '.json'))
    return json.loads(meta.read_text(encoding='utf-8'))['url']


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('cmd', choices=['prompt', 'run']); ap.add_argument('cid'); ap.add_argument('what', choices=['base', 'extra', 'poses', 'poses2', 'fix'])
    ap.add_argument('--variants', type=int, default=1); ap.add_argument('--model', default=MODEL)
    ap.add_argument('--base', help='свой base-лист (png с .json рядом) — референс для extra/poses')
    ap.add_argument('--out', default=os.environ.get('ZD_ART_WORK', SEL['_work'] + '/../gen'))
    a = ap.parse_args()
    p = prompt(a.cid, a.what)
    if a.cmd == 'prompt':
        print(p); return
    out = pathlib.Path(a.out); out.mkdir(parents=True, exist_ok=True)
    pf = out / f'{a.cid}-{a.what}.txt'; pf.write_text(p, encoding='utf-8')
    refs = []
    if a.what != 'base' or a.base:
        refs.append(json.loads(pathlib.Path(a.base).with_suffix('.json').read_text(encoding='utf-8'))['url'])
    if a.cid != 'heroine' or a.what != 'base':
        refs.append(heroine_url())
    procs = []
    for v in range(a.variants):
        cmd = [sys.executable, str(HERE / 'polza.py'), 'gen', '--model', a.model, '--out', str(out), '--name', f'{a.cid}-{a.what}-{chr(97 + v)}',
               '--prompt-file', str(pf), '--ar', '16:9' if len(figures(a.cid, a.what)) <= 6 else '21:9', '--res', '2K']
        for r in refs:
            cmd += ['--ref', r]
        procs.append(subprocess.Popen(cmd))
    for pr in procs:
        pr.wait()


if __name__ == '__main__':
    main()
