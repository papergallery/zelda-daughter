#!/usr/bin/env python3
"""D-17b: generated audio → OGG Vorbis in the Unity project. Music -16 LUFS, effects peak -3 dBFS, silence trimmed."""
import json, os, subprocess, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
DL = os.path.join(HERE, 'dl')
OUT = sys.argv[1]  # ZeldaDaughter/Assets/Art/Audio/Generated; the mp3 from Polza in ./dl next to this script (not in git; requests — tools/audio/d17b-prompts.json)
SR = 44100


def load(name, mono=True):
    cmd = ['ffmpeg', '-v', 'error', '-i', os.path.join(DL, name + '.mp3'), '-f', 'f32le', '-ac', '1' if mono else '2', '-ar', str(SR), '-']
    a = np.frombuffer(subprocess.run(cmd, capture_output=True, check=True).stdout, dtype=np.float32).copy()
    return a if mono else a.reshape(-1, 2)


def rms_env(x, win=0.01):
    n = int(SR * win)
    m = x if x.ndim == 1 else x.mean(axis=1)
    k = len(m) // n
    return np.sqrt((m[:k * n].reshape(k, n) ** 2).mean(axis=1) + 1e-12), n


def trim(x, thr_db=-50.0, pad=0.02):
    env, n = rms_env(x)
    above = np.where(20 * np.log10(env) > thr_db)[0]
    if len(above) == 0: return x
    s = max(0, above[0] * n - int(pad * SR))
    e = min(len(x), (above[-1] + 1) * n + int(pad * SR))
    return x[s:e]


def fade(x, fin=0.005, fout=0.05):
    x = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    shape = (-1,) + (1,) * (x.ndim - 1)
    if a: x[:a] *= np.linspace(0, 1, a).reshape(shape)
    if b: x[-b:] *= np.linspace(1, 0, b).reshape(shape)
    return x


def peak(x, db=-3.0):
    return x * (10 ** (db / 20) / max(1e-9, np.abs(x).max()))


def write_ogg(x, path, q=4, loudnorm=None):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    ch = 1 if x.ndim == 1 else 2
    af = []
    if loudnorm is not None:
        # two-pass loudnorm: measure, then apply linearly
        m = subprocess.run(['ffmpeg', '-hide_banner', '-f', 'f32le', '-ar', str(SR), '-ac', str(ch), '-i', '-', '-af',
                            f'loudnorm=I={loudnorm}:TP=-1.5:LRA=11:print_format=json', '-f', 'null', '-'],
                           input=x.astype(np.float32).tobytes(), capture_output=True, check=True).stderr.decode()
        j = json.loads(m[m.rindex('{'):m.rindex('}') + 1])
        af = ['-af', f"loudnorm=I={loudnorm}:TP=-1.5:LRA=11:measured_I={j['input_i']}:measured_TP={j['input_tp']}:"
                     f"measured_LRA={j['input_lra']}:measured_thresh={j['input_thresh']}:offset={j['target_offset']}:linear=true,aresample={SR}"]
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', str(ch), '-i', '-'] + af +
                   ['-c:a', 'libvorbis', '-q:a', str(q), '-map_metadata', '-1', path],
                   input=x.astype(np.float32).tobytes(), check=True)
    print(f'{os.path.relpath(path, OUT):40s} {len(x) / SR:6.2f}s {os.path.getsize(path) // 1024:5d} KB')


def seg(x, a, b):
    return x[int(a * SR):int(b * SR)]


def onsets(x, min_gap=0.6, rel_db=-18.0):
    env, n = rms_env(x)
    db = 20 * np.log10(env)
    thr = db.max() + rel_db
    res, last = [], -1e9
    for i in range(1, len(db)):
        if db[i] > thr and db[i - 1] <= thr and (i - last) * n / SR > min_gap:
            res.append(i * n / SR); last = i
    return res


# ---------------------------------------------------------------- music (stereo, -16 LUFS)
music = {'bard/bard_merry_oakbeam_revel': 'suno1a', 'bard/bard_pensive_embers_a': 'suno2a', 'bard/bard_pensive_embers_b': 'suno2b'}
for out, src in music.items():
    x = trim(load(src, mono=False), -55.0, 0.05)
    write_ogg(fade(x, 0.01, 0.5), os.path.join(OUT, out + '.ogg'), q=3, loudnorm=-16)

# ---------------------------------------------------------------- wolf (mono, peak -3)
# chosen by ear (an audio model, docs/demo/decisions.md): the dog-like takes are dropped, the yelp is lowered to sound like a bigger animal
def lower(x, semitones):
    r = 2 ** (semitones / 12)
    return np.interp(np.arange(0, len(x), r), np.arange(len(x)), x).astype(np.float32)

g = load('fx_wolf_growl_1')
b = load('fx_wolf_bite2_2')
write_ogg(peak(fade(trim(seg(g, 0.0, 1.85)), 0.005, 0.15)), os.path.join(OUT, 'wolf/wolf_growl_01.ogg'))
write_ogg(peak(fade(trim(seg(b, 0.0, 1.0)), 0.005, 0.1)), os.path.join(OUT, 'wolf/wolf_growl_02.ogg'))
write_ogg(peak(fade(trim(seg(g, 1.75, 5.3)), 0.005, 0.3)), os.path.join(OUT, 'wolf/wolf_growl_03.ogg'))
bite = trim(seg(b, 0.0, 2.5))
write_ogg(peak(fade(bite, 0.005, 0.25)), os.path.join(OUT, 'wolf/wolf_bite_01.ogg'))
write_ogg(peak(fade(lower(bite, -2), 0.005, 0.25)), os.path.join(OUT, 'wolf/wolf_bite_02.ogg'))
# the pained yelp as generated: lowered it sounded comical (audio-model check), the dog-like takes are dropped
write_ogg(peak(fade(trim(load('fx_wolf_yelp2_1')), 0.003, 0.1)), os.path.join(OUT, 'wolf/wolf_yelp_01.ogg'))
for i in (1, 2):
    h = trim(load(f'fx_wolf_howl_{i}'), -50.0, 0.05)
    write_ogg(peak(fade(h, 0.02, 0.8)), os.path.join(OUT, f'wolf/wolf_howl_{i:02d}.ogg'))

# ---------------------------------------------------------------- wooden steps (mono, peak -3): 6 single steps
s = load('fx_step_wood_1')
on = onsets(s)
print('step onsets:', ['%.2f' % t for t in on])
steps = []
for k, t in enumerate(on):
    end = min(on[k + 1] - 0.05 if k + 1 < len(on) else t + 0.6, t + 0.6)
    steps.append(seg(s, max(0, t - 0.03), end))
# the loudest middle ones — skip the first (a run-in) and the quiet tail
order = sorted(range(1, len(steps)), key=lambda k: -np.abs(steps[k]).max())[:6]
for n, k in enumerate(sorted(order), 1):
    write_ogg(peak(fade(trim(steps[k], -48.0, 0.01), 0.003, 0.06)), os.path.join(OUT, f'steps/step_wood_{n:02d}.ogg'))

# ---------------------------------------------------------------- night: crickets loop (mono, -20 LUFS), owl (peak -3)
c1 = seg(load('fx_amb_night_crickets_2'), 0.4, 12.9)
c2 = seg(load('fx_amb_night_crickets_1'), 0.4, 12.8)
c1 = c1 / np.sqrt((c1 ** 2).mean()); c2 = c2 / np.sqrt((c2 ** 2).mean())  # equal loudness before the join
X = int(1.5 * SR)
ramp = np.linspace(0, 1, X, dtype=np.float32)
eq = lambda r: np.sqrt(r)  # equal-power cross-fade
body = np.concatenate([c1[:-X], c1[-X:] * eq(1 - ramp) + c2[:X] * eq(ramp), c2[X:]])
# seamless: the last X samples fade into the first X, then the head is dropped
loop = np.concatenate([body[-X:] * eq(1 - ramp) + body[:X] * eq(ramp), body[X:-X]])
loop = loop * (0.3 / np.abs(loop).max())
write_ogg(loop, os.path.join(OUT, 'night/amb_night_crickets_loop.ogg'), q=3, loudnorm=-20)
for i in (1, 2):
    o = trim(load(f'fx_amb_night_owl_{i}'), -50.0, 0.05)
    write_ogg(peak(fade(o, 0.02, 0.6)), os.path.join(OUT, f'night/owl_{i:02d}.ogg'))
