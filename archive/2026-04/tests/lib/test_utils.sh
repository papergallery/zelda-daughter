#!/bin/bash
# Утилиты тестового фреймворка — вынесены из smoke_test.sh + расширены
# Источник: TESTING_GUIDE.md секции автоматизации

# --- Цвета ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

# --- Счётчики (глобальные) ---
_test_passed=0
_test_failed=0
_test_warnings=0
_test_skipped=0
_test_failures_detail=""

# --- Логирование ---
log_pass() {
    echo -e "${GREEN}[PASS]${NC} $1"
    ((_test_passed++))
}

log_fail() {
    echo -e "${RED}[FAIL]${NC} $1"
    ((_test_failed++))
    _test_failures_detail+="[FAIL] $1\n"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
    ((_test_warnings++))
}

log_skip() {
    echo -e "${CYAN}[SKIP]${NC} $1"
    ((_test_skipped++))
}

log_info() {
    echo -e "      $1"
}

log_section() {
    echo ""
    echo -e "${CYAN}--- $1 ---${NC}"
}

# --- Сбросить счётчики (для каждого модуля) ---
reset_counters() {
    _test_passed=0
    _test_failed=0
    _test_warnings=0
    _test_skipped=0
    _test_failures_detail=""
}

get_summary() {
    echo "$_test_passed $_test_failed $_test_warnings $_test_skipped"
}

# --- Скриншот ---
screenshot() {
    local name="$1"
    adb exec-out screencap -p > "$RESULTS_DIR/${name}.png" 2>/dev/null
    log_info "Screenshot: ${name}.png"
}

# --- Подсчёт совпадений в файле ---
count_in_file() {
    local n
    n=$(grep -c "$1" "$2" 2>/dev/null || true)
    echo "${n:-0}"
}

# --- Logcat ---
dump_logcat() {
    local suffix="${1:-tmp}"
    adb logcat -d -s Unity > "$RESULTS_DIR/_logcat_${suffix}.txt" 2>/dev/null || true
}

clear_logcat() {
    adb logcat -c 2>/dev/null || true
}

# Проверить наличие N+ записей [ZD:$tag]
check_zd_tag() {
    local tag="$1"
    local min_count="${2:-1}"
    local logfile="${3:-$RESULTS_DIR/_logcat_tmp.txt}"
    local count
    count=$(count_in_file "\[ZD:${tag}\]" "$logfile")
    if [ "$count" -ge "$min_count" ]; then
        return 0
    else
        return 1
    fi
}

# Получить последние строки с тегом
get_zd_lines() {
    local tag="$1"
    local n="${2:-5}"
    local logfile="${3:-$RESULTS_DIR/_logcat_tmp.txt}"
    grep "\[ZD:${tag}\]" "$logfile" 2>/dev/null | tail -"$n"
}

# Проверка на ошибки в logcat (из TESTING_GUIDE: grep -iE "Exception|Error|NullRef")
check_no_errors() {
    local logfile="${1:-$RESULTS_DIR/_logcat_tmp.txt}"
    local errors
    errors=$(count_in_file "Exception\|NullRef\|CRASH\|Fatal" "$logfile")
    if [ "$errors" -eq 0 ]; then
        return 0
    else
        return 1
    fi
}

# --- Ввод (обёртки над adb shell input) ---
tap() {
    local x="$1" y="$2"
    adb shell input tap "$x" "$y"
    log_info "Tap ($x, $y)"
}

swipe() {
    # Принимает: x1 y1 x2 y2 duration_ms
    adb shell input swipe "$@"
    log_info "Swipe ($1,$2) -> ($3,$4) ${5}ms"
}

swipe_direction() {
    local dir="$1"
    case "$dir" in
        up)         swipe $SWIPE_UP ;;
        down)       swipe $SWIPE_DOWN ;;
        left)       swipe $SWIPE_LEFT ;;
        right)      swipe $SWIPE_RIGHT ;;
        up_long)    swipe $SWIPE_UP_LONG ;;
        up_short)   swipe $SWIPE_UP_SHORT ;;
        *) log_warn "Unknown direction: $dir" ;;
    esac
}

longpress() {
    local x="$1" y="$2"
    local duration="${3:-700}"
    adb shell input swipe "$x" "$y" "$x" "$y" "$duration"
    log_info "LongPress ($x, $y) ${duration}ms"
}

# --- Анализ скриншота: розовый экран ---
check_pink() {
    local result
    result=$(/usr/bin/python3 - "$1" <<'PYEOF'
from PIL import Image
import sys
try:
    img = Image.open(sys.argv[1]).convert('RGB')
    w, h = img.size
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
    ) 2>/dev/null || echo "NO_PIL"
    echo "$result"
}

# --- Управление приложением ---
launch_app() {
    adb shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 > /dev/null 2>&1
    sleep "$WAIT_LAUNCH"
}

kill_app() {
    adb shell am force-stop "$PACKAGE" 2>/dev/null || true
    sleep 2
}

ensure_running() {
    local focus
    focus=$(adb shell dumpsys window 2>/dev/null | grep "mCurrentFocus" || true)
    if echo "$focus" | grep -q "$PACKAGE"; then
        return 0
    else
        log_info "Приложение не запущено, перезапускаю..."
        launch_app
        focus=$(adb shell dumpsys window 2>/dev/null | grep "mCurrentFocus" || true)
        if echo "$focus" | grep -q "$PACKAGE"; then
            return 0
        else
            return 1
        fi
    fi
}

# Проверка что приложение на переднем плане
is_app_focused() {
    adb shell dumpsys window 2>/dev/null | grep "mCurrentFocus" | grep -q "$PACKAGE"
}

# --- Открыть инвентарь (лонг-пресс центр → свайп к сектору) ---
open_inventory() {
    longpress "$CENTER_X" "$CENTER_Y" 700
    sleep "$WAIT_ACTION"
    # Свайп вверх к сектору инвентаря (примерные координаты)
    swipe "$CENTER_X" "$CENTER_Y" "$CENTER_X" "$((CENTER_Y - 200))" 200
    sleep "$WAIT_ACTION"
}

# --- Закрыть UI (тап в пустую область) ---
close_ui() {
    tap 50 50
    sleep "$WAIT_ACTION"
}

# --- Ожидание + скриншот ---
wait_and_screenshot() {
    local seconds="$1"
    local name="$2"
    sleep "$seconds"
    screenshot "$name"
}

# --- Производительность ---
get_memory_mb() {
    local mem_kb
    mem_kb=$(adb shell dumpsys meminfo "$PACKAGE" 2>/dev/null | grep "TOTAL PSS" | awk '{print $3}' || echo "0")
    echo $(( ${mem_kb:-0} / 1024 ))
}

get_fps_from_logcat() {
    local logfile="${1:-$RESULTS_DIR/_logcat_tmp.txt}"
    grep "\[ZD:Perf\]" "$logfile" 2>/dev/null | tail -1 | grep -oP 'FPS=\K[0-9]+' || echo "0"
}

# --- Ориентация экрана ---
check_portrait() {
    local rotation
    rotation=$(adb shell dumpsys window 2>/dev/null | grep "mCurrentRotation" | grep -oP 'mCurrentRotation=\K[0-9]' || echo "-1")
    # 0 = portrait, 1 = landscape, 2 = reverse portrait, 3 = reverse landscape
    [ "$rotation" = "0" ]
}
