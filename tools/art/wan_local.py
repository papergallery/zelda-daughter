#!/usr/bin/env python3
"""D-25: локальный ролик Wan 2.2 TI2V 5B (Apache-2.0) в ComfyUI на ПК автора — бесплатная замена облачного видео.

Модели (репак Comfy-Org, Apache-2.0) в C:\\Users\\paper\\ai-hub\\comfyui\\models: diffusion_models/wan2.2_ti2v_5B_fp16,
text_encoders/umt5_xxl_fp8_e4m3fn_scaled, vae/wan2.2_vae. Узлы — штатные ComfyUI 0.20 (Wan22ImageToVideoLatent).
Перед запуском — правила видеокарты (`docs/agent-handbook.md` §2): busy-файл, не во время игры автора.

    wan_local.py --image key.png --prompt-file p.txt --out DIR --name N [--w 544 --h 960 --length 49 --steps 20 --seed 1]

Выход: DIR/<name>.mp4 (24 к/с, ffmpeg из кадров) и DIR/<name>.json (параметры, время).
"""
import argparse, json, pathlib, subprocess, sys, tempfile, time, urllib.parse

HERE = pathlib.Path(__file__).parent
sys.path.insert(0, str(HERE))
import klein  # noqa: E402  (http, upload, run — тот же ComfyUI через туннель TensorLay)

NEG = ('camera motion, zoom, pan, shaking, moving forward, turning around, 3d render, photo, realistic, blurry, deformed, extra limbs, '
       'text, watermark, ground, shadow, scenery')


def graph(a, img):
    return {
        '1': {'class_type': 'UNETLoader', 'inputs': {'unet_name': 'wan2.2_ti2v_5B_fp16.safetensors', 'weight_dtype': 'default'}},
        '2': {'class_type': 'CLIPLoader', 'inputs': {'clip_name': 'umt5_xxl_fp8_e4m3fn_scaled.safetensors', 'type': 'wan'}},
        '3': {'class_type': 'VAELoader', 'inputs': {'vae_name': 'wan2.2_vae.safetensors'}},
        '4': {'class_type': 'ModelSamplingSD3', 'inputs': {'model': ['1', 0], 'shift': a.shift}},
        '5': {'class_type': 'CLIPTextEncode', 'inputs': {'clip': ['2', 0], 'text': a.prompt}},
        '6': {'class_type': 'CLIPTextEncode', 'inputs': {'clip': ['2', 0], 'text': NEG}},
        '7': {'class_type': 'LoadImage', 'inputs': {'image': img}},
        '8': {'class_type': 'Wan22ImageToVideoLatent', 'inputs': {'vae': ['3', 0], 'width': a.w, 'height': a.h, 'length': a.length,
                                                                  'batch_size': 1, 'start_image': ['7', 0]}},
        '9': {'class_type': 'KSampler', 'inputs': {'model': ['4', 0], 'seed': a.seed, 'steps': a.steps, 'cfg': a.cfg,
                                                   'sampler_name': 'uni_pc', 'scheduler': 'simple', 'positive': ['5', 0],
                                                   'negative': ['6', 0], 'latent_image': ['8', 0], 'denoise': 1.0}},
        '10': {'class_type': 'VAEDecode', 'inputs': {'samples': ['9', 0], 'vae': ['3', 0]}},
        '11': {'class_type': 'SaveImage', 'inputs': {'images': ['10', 0], 'filename_prefix': f'zd-wan/{a.name}'}},
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--image', required=True, type=pathlib.Path); ap.add_argument('--prompt'); ap.add_argument('--prompt-file', type=pathlib.Path)
    ap.add_argument('--out', required=True, type=pathlib.Path); ap.add_argument('--name', required=True)
    ap.add_argument('--w', type=int, default=544); ap.add_argument('--h', type=int, default=960); ap.add_argument('--length', type=int, default=49)
    ap.add_argument('--steps', type=int, default=20); ap.add_argument('--cfg', type=float, default=5.0); ap.add_argument('--shift', type=float, default=8.0)
    ap.add_argument('--seed', type=int, default=1)
    ap.add_argument('--fetch', help='забрать готовое задание ComfyUI по prompt_id (если клиент отвалился по тайм-ауту)')
    a = ap.parse_args()
    if a.prompt_file:
        a.prompt = a.prompt_file.read_text(encoding='utf-8').strip()
    a.out.mkdir(parents=True, exist_ok=True)
    if a.fetch:
        t0 = time.time()
        while True:
            try:
                h = json.loads(klein.http('GET', f'/history/{a.fetch}', timeout=60))
            except Exception:  # ComfyUI занят расчётом — HTTP отвечает медленно
                h = {}
            if a.fetch in h and h[a.fetch].get('outputs'):
                outs, dt = h[a.fetch]['outputs'], -1.0
                break
            if time.time() - t0 > 3600:
                print('TIMEOUT'); sys.exit(4)
            time.sleep(20)
    else:
        outs, dt = klein.run(graph(a, klein.upload(a.image)))
    ims = outs['11']['images']
    with tempfile.TemporaryDirectory() as tmp:
        for k, im in enumerate(ims):
            q = urllib.parse.urlencode({'filename': im['filename'], 'subfolder': im.get('subfolder', ''), 'type': im.get('type', 'output')})
            (pathlib.Path(tmp) / f'f{k:04d}.png').write_bytes(klein.http('GET', f'/view?{q}'))
        mp4 = a.out / f'{a.name}.mp4'
        subprocess.run(['ffmpeg', '-v', 'error', '-y', '-framerate', '24', '-i', str(pathlib.Path(tmp) / 'f%04d.png'),
                        '-c:v', 'libx264', '-crf', '12', '-pix_fmt', 'yuv420p', str(mp4)], check=True)
    meta = {'model': 'wan2.2_ti2v_5B_fp16 (ComfyUI, ПК автора)', 'image': str(a.image), 'prompt': a.prompt, 'negative': NEG,
            'w': a.w, 'h': a.h, 'length': a.length, 'steps': a.steps, 'cfg': a.cfg, 'shift': a.shift, 'seed': a.seed,
            'frames': len(ims), 'seconds': round(dt, 1)}
    (a.out / f'{a.name}.json').write_text(json.dumps(meta, ensure_ascii=False, indent=1), encoding='utf-8')
    print(mp4, f'{len(ims)} кадров, {dt:.0f} с')


if __name__ == '__main__':
    main()
