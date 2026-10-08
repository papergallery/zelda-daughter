#!/usr/bin/env python3
"""D-09: клиент облачного генератора Polza (OpenAI-совместимый шлюз) для спрайтов и иконок.

Ключ и адрес — из ~/.config/thechest/polza.env (POLZA_API_KEY, POLZA_BASE_URL); ключ не печатается и не пишется на диск.

    polza.py balance
    polza.py models [подстрока]
    polza.py gen --model M --out DIR --name N --prompt-file P [--ref a.png ...] [--ar 3:2] [--res 2K] [--raw]

Выход: DIR/<name>-<k>.png и DIR/<name>-<k>.json (модель, промпт, референсы, размер, цена/время, если шлюз их вернул).
"""
import argparse, base64, json, mimetypes, pathlib, sys, time, urllib.error, urllib.request

ENV = pathlib.Path.home() / '.config/thechest/polza.env'


def env():
    d = {}
    for line in ENV.read_text(encoding='utf-8').splitlines():
        if '=' in line and not line.lstrip().startswith('#'):
            k, v = line.split('=', 1)
            d[k.strip()] = v.strip().strip('"').strip("'")
    return d['POLZA_BASE_URL'].rstrip('/'), d['POLZA_API_KEY']


def call(method, path, body=None, timeout=600):
    base, key = env()
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(base + path, data=data, method=method,
                                 headers={'Authorization': f'Bearer {key}', 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        txt = e.read().decode('utf-8', 'replace')
        return {'_http_error': e.code, '_body': txt[:3000]}


def data_url(p):
    p = pathlib.Path(p)
    return f'data:{mimetypes.guess_type(p.name)[0] or "image/png"};base64,' + base64.b64encode(p.read_bytes()).decode()


def fetch(url):
    if url.startswith('data:'):
        return base64.b64decode(url.split(',', 1)[1])
    with urllib.request.urlopen(url, timeout=300) as r:
        return r.read()


def images_of(res):
    out = []
    for it in res.get('data', []) or []:
        if it.get('b64_json'):
            out.append(base64.b64decode(it['b64_json']))
        elif it.get('url'):
            out.append(fetch(it['url']))
    return out


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest='cmd', required=True)
    sub.add_parser('balance')
    m = sub.add_parser('models'); m.add_argument('q', nargs='?', default='')
    g = sub.add_parser('gen')
    g.add_argument('--model', required=True); g.add_argument('--out', required=True, type=pathlib.Path)
    g.add_argument('--name', required=True); g.add_argument('--prompt'); g.add_argument('--prompt-file', type=pathlib.Path)
    g.add_argument('--ref', action='append', default=[], help='локальный png (уйдёт data-URL) или http(s)-URL')
    g.add_argument('--ar', default='3:2', help='aspect_ratio'); g.add_argument('--res', default='2K', help='image_resolution')
    g.add_argument('--n', type=int, default=1)
    g.add_argument('--extra', default='{}', help='доп. поля запроса JSON (quality, aspect_ratio, …)')
    g.add_argument('--raw', action='store_true', help='напечатать ответ без картинок (отладка формата)')
    f = sub.add_parser('fetch', help='забрать готовый запрос по requestId'); f.add_argument('rid'); f.add_argument('dst', type=pathlib.Path)
    a = ap.parse_args()
    if a.cmd == 'balance':
        print(json.dumps(call('GET', '/balance'), ensure_ascii=False)); return
    if a.cmd == 'models':
        res = call('GET', '/models')
        for x in res.get('data', res if isinstance(res, list) else []):
            s = json.dumps(x, ensure_ascii=False)
            if a.q.lower() in s.lower():
                print(s[:700])
        return
    if a.cmd == 'fetch':
        res = call('GET', f'/media/{a.rid}')
        from io import BytesIO
        from PIL import Image
        Image.open(BytesIO(images_of(res)[0])).convert('RGB').save(a.dst)
        print(a.dst, json.dumps(res.get('usage')), (res.get('data') or [{}])[0].get('url')); return
    prompt = a.prompt or a.prompt_file.read_text(encoding='utf-8').strip()
    inp = {'prompt': prompt, 'aspect_ratio': a.ar, 'image_resolution': a.res}
    if not a.model.startswith('openai/'):
        inp['output_format'] = 'png'
    if a.ref:
        inp['images'] = [r if r.startswith('http') else data_url(r) for r in a.ref]
    inp.update(json.loads(a.extra))
    body = {'model': a.model, 'input': inp}  # /media: параметры модели — в input (через /images/generations размер и разрешение терялись)
    t0 = time.time()
    for attempt in range(8):  # 429 — лимит шлюза (другие агенты тоже генерируют): ждать и повторить
        res = call('POST', '/media', body)
        if res.get('_http_error') != 429:
            break
        time.sleep(20 + 15 * attempt)
    rid = res.get('requestId') or res.get('id')
    while rid and not res.get('data') and res.get('status') not in ('failed', 'error', 'cancelled'):
        if time.time() - t0 > 900:
            print('TIMEOUT', rid); sys.exit(4)
        time.sleep(5)
        res = call('GET', f'/media/{rid}')
    dt = time.time() - t0
    if a.raw or not images_of(res):
        def strip(o):
            if isinstance(o, dict):
                return {k: (f'<{len(v)} chars>' if isinstance(v, str) and len(v) > 300 else strip(v)) for k, v in o.items()}
            if isinstance(o, list):
                return [strip(v) for v in o]
            return o
        print(json.dumps(strip(res), ensure_ascii=False)[:3000])
        if not images_of(res):
            sys.exit(2)
    a.out.mkdir(parents=True, exist_ok=True)
    for k, png in enumerate(images_of(res)):
        dst = a.out / f'{a.name}-{k}.png'
        from io import BytesIO
        from PIL import Image
        Image.open(BytesIO(png)).convert('RGB').save(dst)  # jpg/webp от шлюза → png
        meta = {'model': a.model, 'prompt': prompt, 'refs': a.ref, 'aspect_ratio': a.ar, 'image_resolution': a.res, 'extra': json.loads(a.extra),
                'seconds': round(dt, 1), 'usage': res.get('usage'), 'request': rid,
                'url': (res.get('data') or [{}])[k].get('url')}
        dst.with_suffix('.json').write_text(json.dumps(meta, ensure_ascii=False, indent=1), encoding='utf-8')
        print(dst, f'({dt:.0f}s)', 'usage', json.dumps(res.get('usage'))[:200])


if __name__ == '__main__':
    main()
