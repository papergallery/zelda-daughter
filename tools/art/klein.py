#!/usr/bin/env python3
"""D-09: FLUX.2 Klein 4B в ComfyUI на ПК автора (туннель TensorLay, 127.0.0.1:8188) + маска фона BiRefNet в том же графе.

Граф — по comiclang `06-flux2-klein-edit.json` (Apache-2.0 модель, до 2 референсов через ReferenceLatent),
плюс узлы `ComfyUI_BiRefNet_ll` (MIT, модель BiRefNet General, MIT): к каждой картинке сразу пишется маска.

    klein.py --out DIR --name heroine-sheet --prompt-file p.txt --w 1536 --h 1024 --seeds 1,2,3
    klein.py --out DIR --name heroine-walk-front --ref DIR/heroine-front.png --prompt "..." --seeds 1,2
    klein.py --out DIR --name x --mask-only --ref img.png        # только маска для готовой картинки

Выход: DIR/<name>-s<seed>.png и DIR/<name>-s<seed>.mask.png (белое — персонаж), рядом <name>-s<seed>.json
(модель, seed, размер, шаги, промпт, референсы) — для повторения.

Перед запуском: правила видеокарты (docs/agent-handbook.md §2, docs/demo/decisions.md № 8) — не во время игры автора, busy-файл.
"""
import argparse, json, mimetypes, os, pathlib, sys, time, urllib.parse, urllib.request, uuid

COMFY = os.environ.get('COMFY_BASE', 'http://127.0.0.1:8188')
MODELS = {'distilled': 'flux-2-klein-4b-fp8.safetensors', 'base': 'flux-2-klein-base-4b-fp8.safetensors'}
BIREFNET = 'General.safetensors'


def http(method, path, data=None, headers=None, timeout=120):
    req = urllib.request.Request(COMFY + path, data=data, method=method, headers=headers or {})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read()


def upload(path: pathlib.Path) -> str:
    boundary = uuid.uuid4().hex
    name = f'zd-{path.stem}-{uuid.uuid4().hex[:6]}{path.suffix}'
    ctype = mimetypes.guess_type(path.name)[0] or 'application/octet-stream'
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="image"; filename="{name}"\r\n'
            f'Content-Type: {ctype}\r\n\r\n').encode() + path.read_bytes() + \
           f'\r\n--{boundary}\r\nContent-Disposition: form-data; name="overwrite"\r\n\r\ntrue\r\n--{boundary}--\r\n'.encode()
    return json.loads(http('POST', '/upload/image', body, {'Content-Type': f'multipart/form-data; boundary={boundary}'}))['name']


def mask_nodes(g, image_node, w, h, prefix):
    g['30'] = {'class_type': 'LoadRembgByBiRefNetModel', 'inputs': {'model': BIREFNET, 'device': 'AUTO', 'use_weight': False, 'dtype': 'float32'}}
    g['31'] = {'class_type': 'GetMaskByBiRefNet', 'inputs': {'model': ['30', 0], 'images': [image_node, 0], 'width': w, 'height': h,
                                                             'upscale_method': 'bilinear', 'mask_threshold': 0.0}}
    g['32'] = {'class_type': 'MaskToImage', 'inputs': {'mask': ['31', 0]}}
    g['33'] = {'class_type': 'SaveImage', 'inputs': {'images': ['32', 0], 'filename_prefix': prefix + '-mask'}}


def build(a, seed, refs):
    g = {
        '1': {'class_type': 'UNETLoader', 'inputs': {'unet_name': MODELS[a.model], 'weight_dtype': 'default'}},
        '2': {'class_type': 'CLIPLoader', 'inputs': {'clip_name': 'qwen_3_4b.safetensors', 'type': 'flux2', 'device': 'default'}},
        '3': {'class_type': 'VAELoader', 'inputs': {'vae_name': 'flux2-vae.safetensors'}},
        '10': {'class_type': 'CLIPTextEncode', 'inputs': {'text': a.prompt, 'clip': ['2', 0]}},
        '14': {'class_type': 'KSamplerSelect', 'inputs': {'sampler_name': 'euler'}},
        '15': {'class_type': 'Flux2Scheduler', 'inputs': {'steps': a.steps, 'width': a.w, 'height': a.h}},
        '16': {'class_type': 'RandomNoise', 'inputs': {'noise_seed': seed}},
        '17': {'class_type': 'EmptyFlux2LatentImage', 'inputs': {'width': a.w, 'height': a.h, 'batch_size': 1}},
        '18': {'class_type': 'SamplerCustomAdvanced', 'inputs': {'noise': ['16', 0], 'guider': ['13', 0], 'sampler': ['14', 0],
                                                                 'sigmas': ['15', 0], 'latent_image': ['17', 0]}},
        '19': {'class_type': 'VAEDecode', 'inputs': {'samples': ['18', 0], 'vae': ['3', 0]}},
        '20': {'class_type': 'SaveImage', 'inputs': {'images': ['19', 0], 'filename_prefix': f'zd/{a.name}'}},
    }
    cond = ['10', 0]
    for i, r in enumerate(refs):
        n = 40 + i * 4
        g[str(n)] = {'class_type': 'LoadImage', 'inputs': {'image': r}}
        g[str(n + 1)] = {'class_type': 'ImageScaleToTotalPixels', 'inputs': {'image': [str(n), 0], 'upscale_method': 'lanczos',
                                                                              'megapixels': a.ref_mp, 'resolution_steps': 1}}
        g[str(n + 2)] = {'class_type': 'VAEEncode', 'inputs': {'pixels': [str(n + 1), 0], 'vae': ['3', 0]}}
        g[str(n + 3)] = {'class_type': 'ReferenceLatent', 'inputs': {'conditioning': cond, 'latent': [str(n + 2), 0]}}
        cond = [str(n + 3), 0]
    if a.model == 'base':
        g['21'] = {'class_type': 'CLIPTextEncode', 'inputs': {'text': a.negative, 'clip': ['2', 0]}}
        g['13'] = {'class_type': 'CFGGuider', 'inputs': {'model': ['1', 0], 'positive': cond, 'negative': ['21', 0], 'cfg': a.cfg}}
    else:
        g['13'] = {'class_type': 'BasicGuider', 'inputs': {'model': ['1', 0], 'conditioning': cond}}
    mask_nodes(g, '19', a.w, a.h, f'zd/{a.name}')
    return g


def build_mask_only(name, img, w, h):
    g = {'1': {'class_type': 'LoadImage', 'inputs': {'image': img}},
         '20': {'class_type': 'SaveImage', 'inputs': {'images': ['1', 0], 'filename_prefix': f'zd/{name}'}}}
    mask_nodes(g, '1', w, h, f'zd/{name}')
    return g


def run(graph):
    res = json.loads(http('POST', '/prompt', json.dumps({'prompt': graph, 'client_id': uuid.uuid4().hex}).encode(),
                          {'Content-Type': 'application/json'}))
    if 'prompt_id' not in res:
        print('ERROR submitting:', json.dumps(res)[:2000]); sys.exit(2)
    pid, t0 = res['prompt_id'], time.time()
    while True:
        h = json.loads(http('GET', f'/history/{pid}'))
        if pid in h:
            st = h[pid].get('status', {})
            if st.get('status_str') == 'error':
                print('ERROR:', json.dumps(st)[:3000]); sys.exit(3)
            return h[pid]['outputs'], time.time() - t0
        if time.time() - t0 > 1800:
            print('TIMEOUT'); sys.exit(4)
        time.sleep(1.5)


def fetch(outputs, node):
    im = outputs[node]['images'][0]
    q = urllib.parse.urlencode({'filename': im['filename'], 'subfolder': im.get('subfolder', ''), 'type': im.get('type', 'output')})
    return http('GET', f'/view?{q}')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', required=True, type=pathlib.Path)
    ap.add_argument('--name', required=True)
    ap.add_argument('--prompt'); ap.add_argument('--prompt-file', type=pathlib.Path)
    ap.add_argument('--char', help='id из tools/art/sprites.json: промпт листа как в gen_sheets.py (base)')
    ap.add_argument('--negative', default='photo, 3d render, anime, text, watermark, signature, extra limbs, deformed hands')
    ap.add_argument('--ref', action='append', default=[], help='локальный png; до 2 (1-й — персонаж/вид, 2-й — стиль)')
    ap.add_argument('--ref-mp', type=float, default=1.0)
    ap.add_argument('--model', choices=list(MODELS), default='distilled')
    ap.add_argument('--steps', type=int); ap.add_argument('--cfg', type=float, default=4.0)
    ap.add_argument('--w', type=int, default=1024); ap.add_argument('--h', type=int, default=1024)
    ap.add_argument('--seeds', default='1')
    ap.add_argument('--mask-only', action='store_true')
    a = ap.parse_args()
    a.out.mkdir(parents=True, exist_ok=True)
    if a.mask_only:
        from PIL import Image
        src = pathlib.Path(a.ref[0]); w, h = Image.open(src).size
        outs, dt = run(build_mask_only(a.name, upload(src), w // 32 * 32, h // 32 * 32))
        (a.out / f'{a.name}.mask.png').write_bytes(fetch(outs, '33'))
        print(a.out / f'{a.name}.mask.png', f'({dt:.0f}s)'); return
    if a.prompt_file:
        a.prompt = a.prompt_file.read_text(encoding='utf-8').strip()
    if a.char:
        import gen_sheets
        a.prompt = gen_sheets.prompt(a.char, 'base', style_ref=False)
    if not a.prompt:
        ap.error('--prompt or --prompt-file')
    if a.steps is None:
        a.steps = 4 if a.model == 'distilled' else 20
    refs = [upload(pathlib.Path(r)) for r in a.ref]
    for seed in [int(s) for s in a.seeds.split(',')]:
        outs, dt = run(build(a, seed, refs))
        base = a.out / f'{a.name}-s{seed}'
        base.with_suffix('.png').write_bytes(fetch(outs, '20'))
        pathlib.Path(str(base) + '.mask.png').write_bytes(fetch(outs, '33'))
        meta = {'model': MODELS[a.model], 'seed': seed, 'w': a.w, 'h': a.h, 'steps': a.steps, 'prompt': a.prompt,
                'refs': a.ref, 'cfg': a.cfg if a.model == 'base' else None, 'negative': a.negative if a.model == 'base' else None,
                'seconds': round(dt, 1)}
        base.with_suffix('.json').write_text(json.dumps(meta, ensure_ascii=False, indent=1), encoding='utf-8')
        print(f'{base}.png  ({dt:.0f}s)', flush=True)


if __name__ == '__main__':
    main()
