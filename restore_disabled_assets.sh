#!/bin/bash
# Возвращает DisabledAssets обратно в Assets/~Disabled
# (если понадобится использовать эти ассеты)

set -e

PROJECT="/var/www/html/Zelda's daughter/UnityProject"
SRC="$PROJECT/../DisabledAssets"
DST="$PROJECT/Assets/~Disabled"

if [ ! -d "$SRC" ]; then
    echo "ERROR: $SRC не найден. Нечего восстанавливать."
    exit 1
fi

if [ -d "$DST" ]; then
    echo "ERROR: $DST уже существует."
    exit 1
fi

echo "Возвращаю DisabledAssets в Assets/~Disabled..."
mv "$SRC" "$DST"

echo "Готово. Unity переимпортирует при следующем запуске (~60 мин)."
