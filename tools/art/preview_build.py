#!/usr/bin/env python3
"""D-25: страница превью анимаций персонажей — `docs/demo/sprites/preview/index.html` + кадры рядом (0,35 размера, PNG).

Источник — реестр `ZeldaDaughter/Assets/Art/Registries/characters.json`: front/side/back (кадр 0 — стоит, 1…N−1 — цикл шага)
и `poses` (позы и переходы любой длины). На странице: персонаж, анимация, фон (светлый / тёмный / шахматка), отражение по
горизонтали, скорость (кадров в секунду), покадрово. Страница без внешних библиотек — можно публиковать как есть.

    preview_build.py [id ...]          # по умолчанию все персонажи реестра
"""
import json, pathlib, shutil, sys

from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
REG = ROOT / 'ZeldaDaughter/Assets/Art/Registries/characters.json'
OUT = ROOT / 'docs/demo/sprites/preview'
SCALE = 0.35


def anims(rec):
    out = {}
    for v in ('side', 'front', 'back'):
        fr = rec.get(v) or []
        if len(fr) >= 4:
            out[f'шаг {v} (цикл {len(fr) - 1})'] = fr[1:]
        elif len(fr) > 1:
            out[f'шаг {v} (D-09: 1→0→2→0)'] = [fr[1], fr[0], fr[2], fr[0]] if len(fr) == 3 else fr
        if fr:
            out[f'стоит {v}'] = fr[:1]
    for k, fr in sorted((rec.get('poses') or {}).items()):
        out[k] = fr
    if rec.get('down'):
        out['down'] = [rec['down']]
    return out


def main(ids):
    reg = json.loads(REG.read_text(encoding='utf-8'))['characters']
    if OUT.exists():
        shutil.rmtree(OUT / 'frames', ignore_errors=True)
    (OUT / 'frames').mkdir(parents=True, exist_ok=True)
    data = {}
    for cid in ids or sorted(reg):
        rec = reg[cid]
        if rec.get('placeholder'):
            continue
        data[cid] = {}
        for name, frames in anims(rec).items():
            rel = []
            for f in frames:
                src = ROOT / 'ZeldaDaughter' / f
                dst = OUT / 'frames' / cid / src.name
                if not dst.exists():
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    im = Image.open(src)
                    im.resize((max(1, int(im.width * SCALE)), max(1, int(im.height * SCALE))), Image.Resampling.LANCZOS).save(dst, optimize=True)
                rel.append(f'frames/{cid}/{src.name}')
            data[cid][name] = rel
    html = PAGE.replace('__DATA__', json.dumps(data, ensure_ascii=False))
    (OUT / 'index.html').write_text(html, encoding='utf-8')
    n = sum(len(v) for v in data.values())
    print(OUT / 'index.html', len(data), 'персонажей,', n, 'анимаций')


PAGE = r'''<!doctype html>
<html lang="ru"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Анимации персонажей</title>
<style>
:root{--bg:#f4efe4;--fg:#2a2420;--muted:#7a6f62;--line:#d8cfbf;--accent:#8a5a2b;--panel:#fffaf1}
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){--bg:#1f1b18;--fg:#efe7da;--muted:#a89a88;--line:#3a332c;--accent:#d29a5c;--panel:#2a2420}}
:root[data-theme="dark"]{--bg:#1f1b18;--fg:#efe7da;--muted:#a89a88;--line:#3a332c;--accent:#d29a5c;--panel:#2a2420}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:15px/1.45 system-ui,-apple-system,"Segoe UI",sans-serif}
main{max-width:980px;margin:0 auto;padding:16px}h1{font-size:20px;margin:4px 0 12px}
.bar{display:flex;flex-wrap:wrap;gap:8px 14px;align-items:center;margin-bottom:12px}
select,button,input{font:inherit;color:var(--fg);background:var(--panel);border:1px solid var(--line);border-radius:6px;padding:5px 8px}
button.on{border-color:var(--accent);color:var(--accent)}label{color:var(--muted)}
.stage{display:flex;justify-content:center;align-items:flex-end;min-height:340px;border:1px solid var(--line);border-radius:8px;padding:16px;overflow:hidden}
.stage.light{background:#efe7d6}.stage.dark{background:#2a2420}
.stage.check{background:conic-gradient(#ccc 25%,#eee 0 50%,#ccc 0 75%,#eee 0) 0 0/24px 24px}
.stage img{image-rendering:auto;max-width:100%}.flip{transform:scaleX(-1)}
.strip{display:flex;gap:6px;overflow-x:auto;margin-top:12px;padding-bottom:6px}
.strip img{height:110px;border:1px solid var(--line);border-radius:4px;background:#efe7d6;cursor:pointer}
.strip img.cur{border-color:var(--accent)}.info{color:var(--muted);margin-top:8px}
</style></head><body><main>
<h1>Анимации персонажей</h1>
<div class="bar">
 <label>Персонаж <select id="cid"></select></label>
 <label>Анимация <select id="anim"></select></label>
</div>
<div class="bar">
 <label>Фон</label><button data-bg="light" class="on">светлый</button><button data-bg="dark">тёмный</button><button data-bg="check">прозрачный</button>
 <button id="flip">отразить</button>
 <label>Скорость <input id="fps" type="range" min="1" max="24" value="10"> <span id="fpsv">10</span> к/с</label>
 <button id="play">пауза</button><button id="prev">◀</button><button id="next">▶</button>
</div>
<div id="stage" class="stage light"><img id="img" alt=""></div>
<div id="strip" class="strip"></div>
<div id="info" class="info"></div>
</main><script>
const DATA=__DATA__;
const $=id=>document.getElementById(id);let frames=[],k=0,timer=null,playing=true;
function fill(sel,items){sel.innerHTML='';for(const t of items){const o=document.createElement('option');o.textContent=t;sel.appendChild(o)}}
function show(){if(!frames.length)return;k=(k+frames.length)%frames.length;$('img').src=frames[k];
 [...$('strip').children].forEach((im,i)=>im.classList.toggle('cur',i===k));$('info').textContent=`кадр ${k+1} из ${frames.length} · ${frames[k].split('/').pop()}`}
function restart(){clearInterval(timer);if(playing)timer=setInterval(()=>{k++;show()},1000/+$('fps').value)}
function pick(){frames=DATA[$('cid').value][$('anim').value]||[];k=0;$('strip').innerHTML='';
 frames.forEach((f,i)=>{const im=document.createElement('img');im.src=f;im.onclick=()=>{k=i;playing=false;$('play').textContent='играть';restart();show()};$('strip').appendChild(im)});show();restart()}
fill($('cid'),Object.keys(DATA));$('cid').onchange=()=>{fill($('anim'),Object.keys(DATA[$('cid').value]));pick()};$('anim').onchange=pick;
document.querySelectorAll('[data-bg]').forEach(b=>b.onclick=()=>{document.querySelectorAll('[data-bg]').forEach(x=>x.classList.remove('on'));b.classList.add('on');$('stage').className='stage '+b.dataset.bg});
$('flip').onclick=()=>{$('img').classList.toggle('flip');$('flip').classList.toggle('on')};
$('fps').oninput=()=>{$('fpsv').textContent=$('fps').value;restart()};
$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'пауза':'играть';restart()};
$('prev').onclick=()=>{k--;show()};$('next').onclick=()=>{k++;show()};
$('cid').onchange();
</script></body></html>
'''

if __name__ == '__main__':
    main(sys.argv[1:])
