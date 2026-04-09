#!/bin/bash
# =============================================================
# Zelda's Daughter — iOS Build Script для Mac
# Запуск: chmod +x build_ios.sh && ./build_ios.sh
# =============================================================

set -e

# --- Настройки ---
UNITY_VERSION="2022.3.30f1"
PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
UNITY_PROJECT="$PROJECT_DIR/UnityProject"
IOS_BUILD_DIR="$PROJECT_DIR/ios-build"
XCODE_PROJECT="$IOS_BUILD_DIR/Unity-iPhone.xcodeproj"
BUNDLE_ID="com.papergallery.zeldasdaughter"
PRODUCT_NAME="Zelda's Daughter"
TEAM_ID=""  # Заполни свой Apple Team ID, или оставь пустым для автоподбора

# --- Цвета ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

info()  { echo -e "${GREEN}[✓]${NC} $1"; }
warn()  { echo -e "${YELLOW}[!]${NC} $1"; }
error() { echo -e "${RED}[✗]${NC} $1"; exit 1; }

# --- 1. Найти Unity ---
echo ""
echo "=== Zelda's Daughter — iOS Build ==="
echo ""

UNITY_APP="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app"
UNITY_BIN="$UNITY_APP/Contents/MacOS/Unity"

if [ ! -f "$UNITY_BIN" ]; then
    # Попробовать другой путь
    UNITY_BIN=$(find /Applications/Unity -name "Unity" -path "*$UNITY_VERSION*MacOS*" 2>/dev/null | head -1)
fi

if [ -z "$UNITY_BIN" ] || [ ! -f "$UNITY_BIN" ]; then
    error "Unity $UNITY_VERSION не найден. Установи через Unity Hub:
    1. Открой Unity Hub
    2. Installs → Install Editor → Archive → $UNITY_VERSION
    3. Отметь iOS Build Support
    4. Запусти скрипт заново"
fi
info "Unity: $UNITY_BIN"

# Проверить iOS Build Support
if [ ! -d "$UNITY_APP/Contents/PlaybackEngines/iOSSupport" ]; then
    error "iOS Build Support не установлен. В Unity Hub:
    Installs → $UNITY_VERSION → Add Modules → iOS Build Support"
fi
info "iOS Build Support: установлен"

# --- 2. Проверить Xcode ---
if ! xcode-select -p &>/dev/null; then
    error "Xcode не установлен. Установи из App Store и запусти:
    sudo xcode-select --switch /Applications/Xcode.app"
fi
info "Xcode: $(xcode-select -p)"

# --- 3. Git pull (если это git-репо) ---
if [ -d "$PROJECT_DIR/.git" ]; then
    warn "Обновляю из git..."
    cd "$PROJECT_DIR"
    git pull origin master 2>/dev/null && info "Git: обновлено" || warn "Git pull не удался (возможно offline)"
fi

# --- 4. Собрать Xcode-проект через Unity ---
info "Собираю iOS Xcode-проект (это займёт ~5-10 мин)..."
mkdir -p "$IOS_BUILD_DIR"

# Удалить lockfile если остался
rm -f "$UNITY_PROJECT/Temp/UnityLockfile" 2>/dev/null

"$UNITY_BIN" -batchmode -quit \
    -projectPath "$UNITY_PROJECT" \
    -buildTarget iOS \
    -executeMethod ZeldaDaughter.Editor.IOSBuilder.BuildXcodeProject \
    -logFile "$PROJECT_DIR/ios-build-log.txt" \
    2>&1 || true

# Проверить результат
if [ ! -d "$XCODE_PROJECT" ]; then
    # Fallback: собрать стандартным способом если IOSBuilder не существует
    warn "IOSBuilder не найден, использую стандартный метод..."
    "$UNITY_BIN" -batchmode -quit \
        -projectPath "$UNITY_PROJECT" \
        -buildTarget iOS \
        -executeMethod UnityEditor.BuildPipeline.BuildPlayer \
        -logFile "$PROJECT_DIR/ios-build-log.txt" \
        2>&1 || true
fi

if [ ! -d "$XCODE_PROJECT" ]; then
    error "Xcode-проект не создан. Проверь лог: $PROJECT_DIR/ios-build-log.txt"
fi

info "Xcode-проект создан: $IOS_BUILD_DIR"

# --- 5. Настроить signing в Xcode ---
# Автоматический signing через sed в pbxproj
PBXPROJ="$XCODE_PROJECT/project.pbxproj"

if [ -f "$PBXPROJ" ]; then
    # Включить automatic signing
    sed -i '' 's/CODE_SIGN_IDENTITY = ""/CODE_SIGN_IDENTITY = "Apple Development"/g' "$PBXPROJ" 2>/dev/null
    sed -i '' 's/ProvisioningStyle = Manual/ProvisioningStyle = Automatic/g' "$PBXPROJ" 2>/dev/null

    # Установить Team ID если задан
    if [ -n "$TEAM_ID" ]; then
        sed -i '' "s/DevelopmentTeam = \"\"/DevelopmentTeam = \"$TEAM_ID\"/g" "$PBXPROJ" 2>/dev/null
        sed -i '' "s/DEVELOPMENT_TEAM = \"\"/DEVELOPMENT_TEAM = \"$TEAM_ID\"/g" "$PBXPROJ" 2>/dev/null
        info "Team ID: $TEAM_ID"
    else
        warn "Team ID не задан — выбери команду вручную в Xcode (Signing & Capabilities → Team)"
    fi
    info "Signing настроен (Automatic)"
fi

# --- 6. Собрать через xcodebuild (опционально) ---
echo ""
echo "=== Что дальше? ==="
echo ""
echo "Вариант A — Через Xcode (рекомендуется для первого раза):"
echo "  open \"$XCODE_PROJECT\""
echo "  1. Выбери Team в Signing & Capabilities"
echo "  2. Подключи iPhone, выбери устройство"
echo "  3. Cmd+R (Run)"
echo ""
echo "Вариант B — Через командную строку (если signing уже настроен):"
echo "  cd \"$IOS_BUILD_DIR\""
echo "  xcodebuild -project Unity-iPhone.xcodeproj \\"
echo "    -scheme Unity-iPhone \\"
echo "    -destination 'generic/platform=iOS' \\"
echo "    -configuration Release \\"
echo "    build"
echo ""

# Спросить пользователя
read -p "Открыть Xcode-проект сейчас? (y/n) " -n 1 -r
echo ""
if [[ $REPLY =~ ^[Yy]$ ]]; then
    open "$XCODE_PROJECT"
    info "Xcode открыт. Выбери Team и нажми Cmd+R"
fi

echo ""
info "Готово! Лог: $PROJECT_DIR/ios-build-log.txt"
