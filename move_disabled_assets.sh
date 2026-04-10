#!/bin/bash
# Перемещает ~Disabled/ из Assets/ за пределы Unity-проекта
# чтобы Unity не импортировал ~19000 неиспользуемых файлов (292 MB)
# При необходимости можно вернуть обратно скриптом restore_disabled_assets.sh

set -e

PROJECT="/var/www/html/Zelda's daughter/UnityProject"
SRC="$PROJECT/Assets/~Disabled"
DST="$PROJECT/../DisabledAssets"

if [ ! -d "$SRC" ]; then
    echo "ERROR: $SRC не найден"
    exit 1
fi

# Проверить что Unity не запущен
if pgrep -f "Unity.*batchmode" > /dev/null 2>&1 || pgrep -f "Unity.*projectPath" > /dev/null 2>&1; then
    echo "ERROR: Unity запущен. Дождись завершения."
    exit 1
fi

echo "Перемещаю ~Disabled/ за пределы Assets..."
echo "  Из: $SRC"
echo "  В:  $DST"

# Переместить
mv "$SRC" "$DST"
rm -f "$SRC.meta" 2>/dev/null

# Добавить в .gitignore если ещё нет
GITIGNORE="$PROJECT/../.gitignore"
if ! grep -q "DisabledAssets" "$GITIGNORE" 2>/dev/null; then
    echo "" >> "$GITIGNORE"
    echo "# Неиспользуемые ассеты (перемещены из Assets/~Disabled)" >> "$GITIGNORE"
    echo "DisabledAssets/" >> "$GITIGNORE"
    echo "Добавлено в .gitignore"
fi

COUNT=$(find "$DST" -type f 2>/dev/null | wc -l)
SIZE=$(du -sh "$DST" 2>/dev/null | cut -f1)

echo ""
echo "Готово! Перемещено $COUNT файлов ($SIZE)"
echo "Unity больше не будет их импортировать."
echo ""
echo "Для возврата: ./restore_disabled_assets.sh"
