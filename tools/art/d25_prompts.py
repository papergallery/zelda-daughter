#!/usr/bin/env python3
"""D-25: промпты роликов и кадров анимации — движение + дословный блок стиля из style-bible (`tools/art/style.py`).

    d25_prompts.py              # переписать tools/art/d25/*.txt (кроме листа GPT — его собирает d24_heroine/gen_sheets)
"""
import pathlib, sys

HERE = pathlib.Path(__file__).parent
sys.path.insert(0, str(HERE))
import style  # noqa: E402

KEEP = ('Keep exactly the same character, clothes, colors, proportions and size as in the first frame; the drawing must stay '
        'identical in manner to the first frame. Flat 2D hand-drawn animation, not 3D, no lighting changes. Static locked camera: '
        'no zoom, no pan, no camera shake, no camera motion. No sound.')

MOTIONS = {
    'run-side': ('The drawn young woman from the image does a light, easy jogging cycle IN PLACE, seen strictly from the side (profile '
                 'facing right): a relaxed run, legs and arms swinging in opposite phase, a small bounce, as if on a treadmill. She stays '
                 'on exactly the same spot in the middle of the frame, does not move forward, does not turn, does not leave the frame.'),
    'run-side-wan': ('2D hand-drawn animation. From the very first frame the young woman jogs in place with a steady, brisk running '
                     'cadence: a continuous light jog cycle seen strictly from the side (profile facing right), knees lifting, legs and '
                     'arms swinging in opposite phase, a small bounce at every step, about two steps per second. She stays on exactly '
                     'the same spot in the middle of the frame, never moves forward, never turns.'),
    'walk-side-wan': ('2D hand-drawn animation. From the very first frame the woman walks in place with a calm, steady walking cadence: '
                      'a continuous walk cycle seen strictly from the side (profile facing right), legs stepping and arms swinging gently '
                      'in opposite phase, about two steps per second. She stays on exactly the same spot in the middle of the frame, '
                      'never moves forward, never turns.'),
}
BETWEEN = ('The two reference images are two key frames of a hand-drawn 2D animation of the same young woman jogging in place, seen '
           'strictly from the side, facing right. Draw the ONE in-between frame exactly halfway between the first and the second key '
           'pose: legs and arms halfway between their positions in the two keys, the body at the same place and the same size, the feet '
           'on the same ground line. The same woman, clothes, backpack and colors.')


def video_prompt(motion):
    return ' '.join([motion, KEEP, style.STYLE, style.BG_GREEN])


def main():
    out = HERE / 'd25'
    out.mkdir(exist_ok=True)
    for name, m in MOTIONS.items():
        (out / f'{name}.txt').write_text(video_prompt(m) + '\n', encoding='utf-8')
    (out / 'klein-between.txt').write_text(' '.join([BETWEEN, style.STYLE, style.BG_GREY]) + '\n', encoding='utf-8')
    print('ok', len(MOTIONS) + 1)


if __name__ == '__main__':
    main()
