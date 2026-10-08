#!/usr/bin/env python3
"""D-27 (критерий 3): метрики вырезной анимации по координатам суставов — проверки трассы суставов ref2game
(MIT, github.com/studioigor/ref2game, references/animation.md §14: foot slide, limp, stretch, arms, pop), переписаны под нашу трассу.

Трасса — JSON от ядра (`CutoutGaitTests.Writes_the_trace_when_asked`, ZD_TRACE_OUT) или из игры (`D27CutoutTests`,
docs/demo/d27/unity-trace.json): кадры {t, x (путь тела, м), phase, gait, planted [ближняя, дальняя], j [[x, y] по Joint, м]}.

    python3 tools/art/d27_metrics.py docs/demo/d27/unity-trace.json [ещё.json ...] [--md docs/demo/d27/metrics.md]

Пороги критерия: проскальзывание опорной стопы ≤ 2 px/кадр при 320 px/м; растяжение костей 0; ноги чередуются; шов цикла — без рывка
(вторая разность суставов на шве не больше, чем внутри цикла).
"""
import argparse, json, math, pathlib, statistics

JOINTS = ['Pelvis', 'Head', 'Top', 'HipNear', 'KneeNear', 'AnkleNear', 'ToeNear', 'HeelNear', 'HipFar', 'KneeFar', 'AnkleFar', 'ToeFar', 'HeelFar',
          'ShoulderNear', 'ElbowNear', 'HandNear', 'ShoulderFar', 'ElbowFar', 'HandFar']
J = {n: i for i, n in enumerate(JOINTS)}
BONES = [('HipNear', 'KneeNear'), ('KneeNear', 'AnkleNear'), ('HipFar', 'KneeFar'), ('KneeFar', 'AnkleFar'),
         ('ShoulderNear', 'ElbowNear'), ('ElbowNear', 'HandNear'), ('ShoulderFar', 'ElbowFar'), ('ElbowFar', 'HandFar')]


def dist(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def metrics(tr):
    ppu = tr.get('ppu', 320)
    fr = tr['frames']
    run = [f for f in fr if f['gait'] >= 1.0]
    W = lambda f, n: (f['x'] + f['j'][J[n]][0], f['j'][J[n]][1])          # world along the run, metres
    m = {'source': tr.get('source', '?'), 'fps': tr.get('fps'), 'frames_run': len(run)}
    # foot slide: a planted foot (both frames planted, full gait) moving in the world
    slide = 0.0
    for a, b in zip(run, run[1:]):
        for k, side in ((0, 'Near'), (1, 'Far')):
            if a['planted'][k] and b['planted'][k]:
                for part in ('Ankle', 'Toe', 'Heel'):
                    slide = max(slide, dist(W(a, part + side), W(b, part + side)))
    m['slide_px_per_frame'] = slide * ppu
    # alternation: touchdowns in order, one per leg per cycle
    downs = []
    for a, b in zip(run, run[1:]):
        for k in (0, 1):
            if b['planted'][k] and not a['planted'][k]:
                downs.append((b['x'], k))
    alt = all(downs[i][1] != downs[i - 1][1] for i in range(1, len(downs)))
    steps = [downs[i][0] - downs[i - 1][0] for i in range(1, len(downs))]
    m['touchdowns'] = len(downs)
    m['alternate'] = alt
    m['step_m_mean'] = statistics.mean(steps) if steps else float('nan')
    m['step_m_spread'] = (max(steps) - min(steps)) if steps else float('nan')
    m['steps_per_min'] = tr['speed'] / m['step_m_mean'] * 60 if steps else float('nan')
    # bone stretch: against the median length (the drawing's)
    stretch = 0.0
    for a_, b_ in BONES:
        ls = [dist(f['j'][J[a_]], f['j'][J[b_]]) for f in fr]
        med = statistics.median(ls)
        stretch = max(stretch, max(abs(l - med) for l in ls))
    m['stretch_px'] = stretch * ppu
    # pops: second difference of every joint (rig space), at the seam of the cycle vs inside it
    seam, inside, where = 0.0, 0.0, ''
    for i in range(2, len(run)):
        a, b, c = run[i - 2], run[i - 1], run[i]
        at_seam = c['phase'] < b['phase'] or b['phase'] < a['phase']
        for n, k in J.items():
            acc = math.hypot(c['j'][k][0] - 2 * b['j'][k][0] + a['j'][k][0], c['j'][k][1] - 2 * b['j'][k][1] + a['j'][k][1])
            if at_seam:
                seam = max(seam, acc)
            elif acc > inside:
                inside, where = acc, n
    m['seam_jerk_px'] = seam * ppu
    m['max_jerk_px'] = inside * ppu
    m['max_jerk_joint'] = where
    # limp: the head's lowest point in each leg's stance
    dn = [f['j'][J['Head']][1] for f in run if f['planted'][0]]
    df = [f['j'][J['Head']][1] for f in run if f['planted'][1]]
    m['limp_px'] = abs(min(dn) - min(df)) * ppu if dn and df else float('nan')
    hy = [f['j'][J['Pelvis']][1] for f in run]
    m['pelvis_bob_px'] = (max(hy) - min(hy)) * ppu if hy else float('nan')
    # arms against legs: correlation of a hand's x with its own leg's ankle x
    def corr(xs, ys):
        mx, my = statistics.mean(xs), statistics.mean(ys)
        sxy = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
        return sxy / math.sqrt(sum((x - mx) ** 2 for x in xs) * sum((y - my) ** 2 for y in ys) + 1e-12)
    m['arm_leg_corr_near'] = corr([f['j'][J['HandNear']][0] for f in run], [f['j'][J['AnkleNear']][0] for f in run])
    m['arm_leg_corr_far'] = corr([f['j'][J['HandFar']][0] for f in run], [f['j'][J['AnkleFar']][0] for f in run])
    # standing: breath by the bones (pelvis moves, feet stay)
    stand = [f for f in fr if f['gait'] <= 0.0]
    if stand:
        py = [f['j'][J['Pelvis']][1] for f in stand]
        m['breath_pelvis_px'] = (max(py) - min(py)) * ppu
        feet = max(dist(stand[0]['j'][J['AnkleNear']], f['j'][J['AnkleNear']]) for f in stand[:int(len(stand) / 2) or 1])
        m['stand_foot_move_px'] = feet * ppu
    m['duty_measured'] = sum(1 for f in run if f['planted'][0]) / max(1, len(run))
    return m


ROWS = [
    ('slide_px_per_frame', 'Проскальзывание опорной стопы, px/кадр (320 px/м)', '≤ 2', lambda v: v <= 2.0),
    ('alternate', 'Ноги чередуются (касания: ближняя, дальняя, …)', 'да', lambda v: v is True),
    ('touchdowns', 'Касаний земли в отрезке бега', '—', None),
    ('step_m_mean', 'Длина шага, м (среднее)', 'stride/2', None),
    ('step_m_spread', 'Разброс длины шага, м (хромота по шагу)', '≈ 0', lambda v: v <= 0.06),
    ('steps_per_min', 'Темп, шагов/мин', '90–190', lambda v: 90 <= v <= 190),
    ('stretch_px', 'Растяжение костей, px (макс. от медианы)', '0', lambda v: v < 0.25),
    ('seam_jerk_px', 'Рывок на шве цикла, px/кадр² (2-я разность)', '≤ внутри цикла', None),
    ('max_jerk_px', 'Самый резкий сустав внутри цикла, px/кадр²', '—', None),
    ('limp_px', 'Хромота: разница двух провалов головы, px', '≤ 2', lambda v: v <= 2.0),
    ('pelvis_bob_px', 'Ход таза в беге, px', '—', None),
    ('arm_leg_corr_near', 'Рука против своей ноги, корреляция x (ближняя)', '< −0,6', lambda v: v < -0.6),
    ('arm_leg_corr_far', 'Рука против своей ноги, корреляция x (дальняя)', '< −0,6', lambda v: v < -0.6),
    ('duty_measured', 'Доля опоры ближней ноги', '0,3–0,4', None),
    ('breath_pelvis_px', 'Стойка: ход таза от дыхания, px', '> 0 (кости)', lambda v: v > 0.5),
    ('stand_foot_move_px', 'Стойка: стопа сдвинулась, px', '0', lambda v: v < 0.1),
]


def fmt(v):
    if isinstance(v, bool):
        return 'да' if v else 'НЕТ'
    if isinstance(v, float):
        return f'{v:.2f}'.replace('.', ',')
    return str(v)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('traces', nargs='+')
    ap.add_argument('--md', default='')
    a = ap.parse_args()
    ms = [metrics(json.loads(pathlib.Path(t).read_text(encoding='utf-8'))) for t in a.traces]
    head = '| Метрика | Порог | ' + ' | '.join(f"{m['source']} ({m['fps']:.0f} к/с)" for m in ms) + ' |'
    lines = [head, '|---|---|' + '---|' * len(ms)]
    for key, title, thr, ok in ROWS:
        cells = []
        for m in ms:
            v = m.get(key)
            if v is None:
                cells.append('—'); continue
            mark = '' if ok is None else (' ✓' if ok(v) else ' ✗')
            if key == 'seam_jerk_px':
                mark = ' ✓' if v <= m['max_jerk_px'] * 1.05 + 1e-6 else ' ✗'
            if key == 'max_jerk_px':
                cells.append(f"{fmt(v)} ({m['max_jerk_joint']})"); continue
            cells.append(fmt(v) + mark)
        lines.append(f'| {title} | {thr} | ' + ' | '.join(cells) + ' |')
    table = '\n'.join(lines)
    print(table)
    if a.md:
        pathlib.Path(a.md).write_text('# D-27 · метрики вырезного бега героини (бок)\n\n'
                                      'Скрипт `tools/art/d27_metrics.py` по трассам суставов: ' + ', '.join(f'`{t}`' for t in a.traces) +
                                      '. Бег 2,5 м/с, шаг цикла 1,7 м (`data/gait.json`), 320 px/м.\n\n' + table + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
