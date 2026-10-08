#!/usr/bin/env python3
"""D-27: промпты частей вырезного рига героини (бок). Стиль — дословно из style-bible.md через style.py (ref2game art.md §2:
одна формулировка стиля на все вызовы). Части — по ref2game animation.md §1 (MIT, github.com/studioigor/ref2game):
тело без конечностей, ОДНА нога целиком, ОДНА рука целиком, каждая часть полная (с местами, которые обычно скрыты),
широкие зазоры между частями.

    python3 tools/art/d27_prompts.py parts  > p.txt      # лист частей (референс — кадр 0 бок)
    python3 tools/art/d27_prompts.py anchor > p.txt      # якорь в риг-позе (если лист частей не сойдётся с героиней)
"""
import pathlib, sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from style import STYLE, BG_GREY  # noqa: E402

# D-24b: героиня взрослая, вариант G (README «Героиня взрослая»), плюс три правки независимого критика к следующему набору (координатор,
# 2026-10-09): футболка заправлена только спереди, тёмные почти чёрно-каштановые волнистые волосы до плеч, голова прямо, плечи расслаблены.
WHO = ('the SAME young adult woman as in the reference image — about 20 years old, slender, adult proportions (head-to-height about 1:4.7), '
       'a narrow oval adult face with a defined jaw and cheekbones, small narrow eyes, not a child; '
       'dark brown, almost black-brown, loose wavy shoulder-length hair; '
       'a white t-shirt half-tucked — tucked in at the front only, loose at the sides and back, belt visible only at the buckle; '
       'a small olive-green canvas backpack worn on one shoulder; light-blue high-waisted jeans with rolled-up cuffs and a ripped knee; '
       'cream canvas sneakers of normal size')

PARTS = (
    'Character PART SHEET for a cut-out (paper-doll) skeletal animation rig of ' + WHO + '. '
    'Strict side view facing right, seen from a slightly high angle like an isometric game (Don\'t Starve). '
    'Exactly three separate pieces in one horizontal row with very wide empty gaps between them; no piece touches another: '
    '(1) LEFT — the BODY WITHOUT ARMS AND WITHOUT LEGS: head upright, chin level, relaxed shoulders; hair and face in profile, neck, the t-shirt torso with the backpack, '
    'and the top of the jeans around the hips and seat, ending just below the hips in a clean rounded edge; the shoulder is whole and smooth with NO sleeve on it '
    '(the sleeve belongs to the arm piece), no arm and no sleeve hole. '
    '(2) MIDDLE — ONE COMPLETE LEG standing perfectly straight and vertical: the jeans from the top of the thigh at the hip down to the rolled-up cuff, '
    'and the sneaker in side view with the toe pointing right, the sole flat and level. '
    '(3) RIGHT — ONE COMPLETE ARM hanging perfectly straight down: the short t-shirt sleeve at the top, the bare upper arm, the elbow, the forearm and a relaxed loose fist. '
    'Each piece is complete and whole, including the areas that are normally hidden by the other parts. '
    'Same proportions, same line, same colours and the same scale as the reference: the leg and the arm are exactly as long as on the reference character. '
    + STYLE + ' ' + BG_GREY
)

ANCHOR = (
    'Full-body character in a neutral RIG POSE: ' + WHO + '. Strict side view facing right, seen from a slightly high angle like an isometric game '
    '(Don\'t Starve). She stands upright, the feet under the hips a small step apart (the near foot a little forward, the far foot a little back), '
    'both legs straight and clearly separate with a gap between them; the near arm hangs straight down slightly away from the body with a clear gap '
    'between the arm and the torso, the hand a relaxed fist. Nothing overlaps the torso. Same proportions, line and colours as the reference. '
    + STYLE + ' ' + BG_GREY
)

if __name__ == '__main__':
    print({'parts': PARTS, 'anchor': ANCHOR}[sys.argv[1] if len(sys.argv) > 1 else 'parts'])
