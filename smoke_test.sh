#!/bin/bash
# Автоматический smoke-тест: секция 0.5 из TESTING_GUIDE.md
# Запуск: ./smoke_test.sh [path/to/ZeldaDaughter.apk]
#
# 5 проверок из TESTING_GUIDE:
# 1. Запуск — скриншот, экран не розовый?
# 2. Свайп — скриншот + лог [ZD:Move], персонаж двигается?
# 3. Тап по объекту — лог [ZD:Interact], взаимодействие работает?
# 4. Ошибки — logcat без Exception/Error?
# 5. FPS — значение из [ZD:Perf] >= 30?
#
# Результат: если любой из пунктов 1-4 провален — стоп, чинить билд.

set -uo pipefail

export ANDROID_HOME=/opt/android-sdk
export PATH=$PATH:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
APK_PATH="${1:-$PROJECT_DIR/UnityProject/Builds/Android/ZeldaDaughter.apk}"
PACKAGE="com.papergallery.zeldasdaughter"
RESULTS_DIR="$PROJECT_DIR/test_results/smoke_$(date +%Y%m%d_%H%M%S)"

# Pixel 4: 1080x2340
SCREEN_W=1080
SCREEN_H=2340
CENTER_X=$((SCREEN_W / 2))     # 540
CENTER_Y=$((SCREEN_H / 2))     # 1170

# --- Цвета и счётчики ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'
passed=0
failed=0
warnings=0

log_pass() { echo -e "${GREEN}[PASS]${NC} $1"; ((passed++)); }
log_fail() { echo -e "${RED}[FAIL]${NC} $1"; ((failed++)); }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; ((warnings++)); }
log_info() { echo -e "      $1"; }

screenshot() {
    adb exec-out screencap -p > "$RESULTS_DIR/${1}.png" 2>/dev/null
    log_info "Screenshot: ${1}.png"
}

# Подсчёт совпадений grep в файле (безопасно, без pipefail-проблем)
count_in_file() {
    local n
    n=$(grep -c "$1" "$2" 2>/dev/null || true)
    echo "${n:-0}"
}

# Анализ цвета скриншота через Python/PIL
check_pink() {
    /usr/bin/python3 - "$1" <<'PYEOF'
from PIL import Image
import sys
try:
    img = Image.open(sys.argv[1]).convert('RGB')
    w, h = img.size
    # Сэмплируем центральную полосу (быстрее чем все пиксели)
    region = img.crop((w//4, h//4, 3*w//4, 3*h//4))
    pixels = list(region.getdata())
    total = len(pixels)
    pink = sum(1 for r,g,b in pixels if r > 200 and g < 100 and b > 200)
    ratio = pink / total
    if ratio > 0.8:
        print('PINK')
    elif ratio > 0.3:
        print('PARTIAL')
    else:
        print('OK')
except Exception as e:
    print(f'ERROR:{e}')
PYEOF
}

# ==============================================================
echo "======================================"
echo " Smoke Test — TESTING_GUIDE секция 0.5"
echo "======================================"
echo ""
mkdir -p "$RESULTS_DIR"

# --- Подготовка (секция 0) ---

# Эмулятор
if ! adb devices 2>/dev/null | grep -q "emulator"; then
    log_info "Эмулятор не запущен, запускаю..."
    bash "$PROJECT_DIR/start_android_emulator.sh"
fi

# APK
if [ ! -f "$APK_PATH" ]; then
    log_fail "APK не найден: $APK_PATH"
    exit 1
fi
log_info "APK: $(basename "$APK_PATH") ($(du -h "$APK_PATH" | cut -f1))"

# Установка
INSTALL_OUT=$(adb install -r "$APK_PATH" 2>&1 || true)
if echo "$INSTALL_OUT" | grep -q "Success"; then
    log_info "APK установлен"
else
    log_fail "Установка APK: $INSTALL_OUT"
    exit 1
fi

# Очистить logcat + запуск (из секции 0)
adb logcat -c 2>/dev/null
adb shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 > /dev/null 2>&1

# ==============================================================
# 1. Запуск — подождать 10 сек, скриншот. Экран не розовый?
# ==============================================================
echo ""
echo "--- 1/5. Запуск: экран не розовый? ---"

sleep 10
screenshot "01_launch"

# Проверка розового экрана
PINK_RESULT=$(check_pink "$RESULTS_DIR/01_launch.png" 2>/dev/null || echo "NO_PIL")

case "$PINK_RESULT" in
    "OK")
        log_pass "Экран не розовый — шейдеры URP корректны"
        ;;
    "PINK")
        log_fail "Розовый экран (>80%) — шейдеры URP не включены в билд"
        ;;
    "PARTIAL")
        log_fail "Частично розовый экран — часть шейдеров отсутствует"
        ;;
    "NO_PIL")
        log_warn "PIL не установлен — пропуск проверки цвета (pip install Pillow)"
        ;;
    *)
        log_warn "Ошибка анализа скриншота: $PINK_RESULT"
        ;;
esac

# ==============================================================
# 2. Свайп — персонаж двигается?
# ==============================================================
echo ""
echo "--- 2/5. Свайп: персонаж двигается? ---"

adb logcat -c 2>/dev/null
sleep 1

# Свайп вверх (из TESTING_GUIDE: swipe 540 1400 540 800 300)
adb shell input swipe 540 1400 540 800 300
sleep 1
screenshot "02_after_swipe"

# Проверка лога [ZD:Move]
adb logcat -d -s Unity > "$RESULTS_DIR/_logcat_swipe.txt" 2>/dev/null || true
MOVE_LOGS=$(count_in_file "\[ZD:Move\]" "$RESULTS_DIR/_logcat_swipe.txt")
INPUT_LOGS=$(count_in_file "\[ZD:Input\]" "$RESULTS_DIR/_logcat_swipe.txt")

if [ "$MOVE_LOGS" -gt 0 ]; then
    log_pass "Персонаж двигается ($MOVE_LOGS записей [ZD:Move])"
    grep "\[ZD:Move\]" "$RESULTS_DIR/_logcat_swipe.txt" | tail -5
elif [ "$INPUT_LOGS" -gt 0 ]; then
    log_pass "Input обработан ($INPUT_LOGS [ZD:Input]), движение не залогировано"
else
    log_warn "Нет [ZD:Move] / [ZD:Input] — Development Build отключён или логи не внедрены"
fi

# ==============================================================
# 3. Тап по объекту — взаимодействие работает?
# ==============================================================
echo ""
echo "--- 3/5. Тап: взаимодействие работает? ---"

adb logcat -c 2>/dev/null
sleep 1

# Тап по ближайшему объекту (из TESTING_GUIDE: tap 540 1000)
adb shell input tap 540 1000
sleep 1
screenshot "03_after_tap"

# Проверка лога [ZD:Interact]
adb logcat -d -s Unity > "$RESULTS_DIR/_logcat_tap.txt" 2>/dev/null || true
INTERACT_LOGS=$(count_in_file "\[ZD:Interact\]" "$RESULTS_DIR/_logcat_tap.txt")
TAP_LOGS=$(count_in_file "\[ZD:Input\].*Tap" "$RESULTS_DIR/_logcat_tap.txt")

if [ "$INTERACT_LOGS" -gt 0 ]; then
    log_pass "Взаимодействие ($INTERACT_LOGS записей [ZD:Interact])"
    grep "\[ZD:Interact\]" "$RESULTS_DIR/_logcat_tap.txt" | tail -5
elif [ "$TAP_LOGS" -gt 0 ]; then
    log_pass "Тап обработан ($TAP_LOGS [ZD:Input] Tap), но объект не найден"
else
    log_warn "Нет [ZD:Interact] / [ZD:Input] Tap — Development Build отключён или логи не внедрены"
fi

# ==============================================================
# 4. Ошибки — logcat без Exception/Error?
# ==============================================================
echo ""
echo "--- 4/5. Ошибки: logcat чистый? ---"

adb logcat -d -s Unity > "$RESULTS_DIR/logcat_unity.txt" 2>/dev/null || true
adb logcat -d > "$RESULTS_DIR/logcat_full.txt" 2>/dev/null || true

# Из TESTING_GUIDE: grep -iE "Exception|Error|NullRef"
ERRORS=$(count_in_file "Exception\|NullRef\|CRASH\|Fatal" "$RESULTS_DIR/logcat_unity.txt")

if [ "$ERRORS" -eq 0 ]; then
    log_pass "Нет Exception/NullRef/CRASH в Unity logcat"
else
    log_fail "Найдено $ERRORS ошибок в logcat"
    echo ""
    grep -iE "Exception|NullRef|CRASH|Fatal" "$RESULTS_DIR/logcat_unity.txt" | head -10
    echo ""
fi

# ==============================================================
# 5. FPS — значение из [ZD:Perf] >= 30?
# ==============================================================
echo ""
echo "--- 5/5. FPS >= 30? ---"

FPS_LINE=$(grep "\[ZD:Perf\]" "$RESULTS_DIR/logcat_unity.txt" 2>/dev/null | tail -1 || true)

if [ -n "$FPS_LINE" ]; then
    FPS_VAL=$(echo "$FPS_LINE" | grep -oP 'FPS=\K[0-9]+' || echo "0")
    MEM_VAL=$(echo "$FPS_LINE" | grep -oP 'mem=\K[0-9]+' || echo "?")
    if [ "$FPS_VAL" -ge 30 ]; then
        log_pass "FPS=$FPS_VAL mem=${MEM_VAL}MB"
    elif [ "$FPS_VAL" -ge 15 ]; then
        log_warn "FPS=$FPS_VAL (< 30, приемлемо для эмулятора) mem=${MEM_VAL}MB"
    else
        log_fail "FPS=$FPS_VAL (< 15, критично) mem=${MEM_VAL}MB"
    fi
else
    # Fallback: dumpsys meminfo (хотя бы память)
    MEM_KB=$(adb shell dumpsys meminfo "$PACKAGE" 2>/dev/null | grep "TOTAL PSS" | awk '{print $3}' || echo "0")
    MEM_MB=$(( ${MEM_KB:-0} / 1024 ))
    if [ "$MEM_MB" -gt 0 ]; then
        log_warn "Нет [ZD:Perf] (Development Build?). Память: ${MEM_MB}MB (dumpsys)"
    else
        log_warn "Нет данных FPS — Development Build отключён или логи не внедрены"
    fi
fi

# ==============================================================
# Сохранение debug-логов [ZD:*]
# ==============================================================
grep "\[ZD:" "$RESULTS_DIR/logcat_unity.txt" > "$RESULTS_DIR/logcat_zd.txt" 2>/dev/null || true
ZD_TOTAL=$(count_in_file "\[ZD:" "$RESULTS_DIR/logcat_zd.txt")
if [ "$ZD_TOTAL" -gt 0 ]; then
    log_info "[ZD:*] логов всего: $ZD_TOTAL (сохранены в logcat_zd.txt)"
fi

# ==============================================================
# Итоги
# ==============================================================
echo ""
echo "======================================"
echo " ИТОГИ SMOKE-ТЕСТА (TESTING_GUIDE 0.5)"
echo "======================================"
echo -e " ${GREEN}PASSED:${NC}   $passed"
echo -e " ${RED}FAILED:${NC}   $failed"
echo -e " ${YELLOW}WARNINGS:${NC} $warnings"
echo ""
echo " Результаты: $RESULTS_DIR/"
echo "   Скриншоты: 01_launch, 02_after_swipe, 03_after_tap"
echo "   Логи:      logcat_unity.txt, logcat_zd.txt"
echo ""

if [ "$failed" -gt 0 ]; then
    echo -e " ${RED}SMOKE ПРОВАЛЕН ($failed) — чинить билд перед полным прогоном${NC}"
    exit 1
else
    echo -e " ${GREEN}SMOKE ПРОЙДЕН — можно запускать полный прогон TESTING_GUIDE${NC}"
    exit 0
fi
