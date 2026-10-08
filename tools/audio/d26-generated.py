#!/usr/bin/env python3
"""D-26: the body's sounds and the killing blow → OGG Vorbis mono in the Unity project (Suno через Polza, модель suno/sounds).
Usage: d26-generated.py <dir with fx_*.mp3> <ZeldaDaughter/Assets/Art/Audio/Generated>
Запросы — tools/audio/d26-prompts.json; mp3 не в git. Нарезка по огибающей (окна 0,25 с / 0,1 с, см. docs/demo/decisions.md D-26)."""
import os, subprocess, sys
import numpy as np

SRC, OUT = sys.argv[1], sys.argv[2]
SR = 44100


def load(name):
    cmd = ['ffmpeg', '-v', 'error', '-i', os.path.join(SRC, name + '.mp3'), '-f', 'f32le', '-ac', '1', '-ar', str(SR), '-']
    return np.frombuffer(subprocess.run(cmd, capture_output=True, check=True).stdout, dtype=np.float32).copy()


def seg(x, a, b):
    return x[int(a * SR):int(b * SR)]


def fade(x, fin=0.01, fout=0.12):
    x = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a: x[:a] *= np.linspace(0, 1, a)
    if b: x[-b:] *= np.linspace(1, 0, b)
    return x


def peak(x, db=-4.0):
    return x * (10 ** (db / 20) / max(1e-9, np.abs(x).max()))


def write_ogg(x, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '1', '-i', '-', '-c:a', 'libvorbis', '-q:a', '4',
                    '-map_metadata', '-1', path], input=x.astype(np.float32).tobytes(), check=True)
    print(f'{os.path.relpath(path, OUT):36s} {len(x) / SR:5.2f}s {os.path.getsize(path) // 1024:4d} KB')


# breathing of a wounded woman: three breaths of the first take (bursts at 2.75-4.0, 5.0-6.5, 7.75-9.0 s)
b = load('fx_breath_hurt_0')
for n, (s, e) in enumerate([(2.70, 4.15), (4.95, 6.65), (7.70, 9.25)], 1):
    write_ogg(peak(fade(seg(b, s, e), 0.03, 0.2)), os.path.join(OUT, f'body/breath_hurt_{n:02d}.ogg'))
# the stomach: three growls of the first take (0.25-2.5, 5.0-8.0, 8.5-11.0 s)
g = load('fx_stomach_growl_0')
for n, (s, e) in enumerate([(0.20, 2.90), (4.90, 8.30), (8.40, 11.00)], 1):
    write_ogg(peak(fade(seg(g, s, e), 0.02, 0.25), -5.0), os.path.join(OUT, f'body/stomach_{n:02d}.ogg'))
# the killing blow: both takes, the first 0.8 s
for n in (0, 1):
    write_ogg(peak(fade(seg(load(f'fx_kill_blow_{n}'), 0.03, 0.85), 0.003, 0.2), -3.0), os.path.join(OUT, f'combat/kill_{n + 1:02d}.ogg'))
