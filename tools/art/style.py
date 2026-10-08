#!/usr/bin/env python3
"""Блок стиля для всех промптов генерации — единственный источник: `docs/demo/sprites/style-bible.md`
(огороженные блоки ```style-prompt```, ```bg-grey```, ```bg-green```). Вставлять дословно.

    from style import STYLE, BG_GREY, BG_GREEN
    python3 tools/art/style.py            # напечатать блоки
"""
import pathlib, re

BIBLE = pathlib.Path(__file__).resolve().parents[2] / 'docs/demo/sprites/style-bible.md'


def block(name: str) -> str:
    m = re.search(r'```' + re.escape(name) + r'\n(.*?)\n```', BIBLE.read_text(encoding='utf-8'), re.S)
    if not m:
        raise KeyError(f'нет блока {name} в {BIBLE}')
    return ' '.join(m.group(1).split())


STYLE = block('style-prompt')
BG_GREY = block('bg-grey')
BG_GREEN = block('bg-green')

if __name__ == '__main__':
    print(STYLE, BG_GREY, BG_GREEN, sep='\n\n')
