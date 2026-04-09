#!/bin/bash
# Модуль 18: День/ночь (TESTING_GUIDE секция 18)
# Фоновый модуль: серия скриншотов для анализа изменения освещения

run_test_18_daynight() {
    log_section "18 | День/ночь (секция 18) — фоновый мониторинг"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    log_info "Полный цикл день/ночь = 20-30 мин реального времени"
    log_info "Этот модуль делает 3 скриншота с интервалом 5 мин для сравнения"

    # --- 18.1 Скриншот 0 (сейчас) ---
    screenshot "18_daynight_t00"
    dump_logcat "daynight_t00"

    local time_log_count
    time_log_count=$(count_in_file "\[ZD:Scene\].*[Tt]ime\|[Dd]ay\|[Nn]ight\|[Ss]unset\|[Dd]awn" "$RESULTS_DIR/_logcat_daynight_t00.txt")
    if [ "$time_log_count" -gt 0 ]; then
        log_pass "Логи времени суток присутствуют ($time_log_count записей [ZD:Scene])"
        get_zd_lines "Scene" 5 "$RESULTS_DIR/_logcat_daynight_t00.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет [ZD:Scene] с тегами времени суток — система день/ночь не залогирована"
    fi

    # --- 18.2 Скриншот через 5 мин ---
    log_info "Ожидание 5 минут для изменения освещения..."
    sleep 300
    screenshot "18_daynight_t05"

    dump_logcat "daynight_t05"
    local night_count
    night_count=$(count_in_file "\[ZD:Scene\].*[Nn]ight\|[Ee]ven\|[Ss]unset" "$RESULTS_DIR/_logcat_daynight_t05.txt")
    if [ "$night_count" -gt 0 ]; then
        log_pass "Переход к вечеру/ночи зафиксирован ($night_count записей)"
    else
        log_warn "Нет событий ночи через 5 мин — цикл длиннее или не реализован"
    fi

    # --- 18.3 Скриншот через ещё 5 мин ---
    log_info "Ожидание ещё 5 минут..."
    sleep 300
    screenshot "18_daynight_t10"

    # Сравнение скриншотов через Python — проверяем что они визуально отличаются
    local differ_result
    differ_result=$(/usr/bin/python3 - \
        "$RESULTS_DIR/18_daynight_t00.png" \
        "$RESULTS_DIR/18_daynight_t10.png" <<'PYEOF'
from PIL import Image
import sys
try:
    img1 = Image.open(sys.argv[1]).convert('L')
    img2 = Image.open(sys.argv[2]).convert('L')
    p1 = list(img1.getdata())
    p2 = list(img2.getdata())
    total = len(p1)
    diff = sum(abs(a - b) for a, b in zip(p1, p2))
    avg_diff = diff / total
    # avg_diff > 5 означает заметное изменение освещения
    if avg_diff > 5:
        print("CHANGED")
    elif avg_diff > 1:
        print("SLIGHT")
    else:
        print("SAME")
except Exception as e:
    print(f"ERROR:{e}")
PYEOF
    ) 2>/dev/null || echo "NO_PIL"

    case "$differ_result" in
        "CHANGED") log_pass "Освещение изменилось между t=0 и t=10мин (avg_diff > 5)" ;;
        "SLIGHT")  log_warn "Незначительное изменение освещения — цикл медленный или разница мала" ;;
        "SAME")    log_warn "Освещение не изменилось — цикл день/ночь не виден на скриншотах" ;;
        "NO_PIL")  log_warn "PIL не установлен — визуальное сравнение пропущено (pip install Pillow)" ;;
        *)         log_warn "Ошибка сравнения скриншотов: $differ_result" ;;
    esac

    [ "$_test_failed" -eq 0 ]
}
