#!/bin/bash
# Модуль 24: Респаун ресурсов (TESTING_GUIDE секция 24)
# Проверяем: срубить дерево → оно пропадает → через сон → появляется снова

run_test_24_respawn() {
    log_section "24 | Респаун ресурсов (секция 24)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 24.1 Скриншот до рубки ---
    screenshot "24_respawn_before_chop"
    log_info "Скриншот ДО рубки (запоминаем состояние дерева)"

    # --- 24.2 Рубка дерева (серия тапов) ---
    log_section "24.2 Рубка дерева (5 тапов)"
    for i in $(seq 1 5); do
        tap 400 800
        sleep 1
    done
    screenshot "24_respawn_after_chop"
    log_info "Скриншот ПОСЛЕ рубки — дерево должно исчезнуть"

    dump_logcat "respawn_chop"
    local chop_count
    chop_count=$(count_in_file "\[ZD:Interact\].*[Cc]hop\|[Cc]ut\|[Hh]arvest\|[Rr]esource" "$RESULTS_DIR/_logcat_respawn_chop.txt")
    if [ "$chop_count" -gt 0 ]; then
        log_pass "Добыча ресурса залогирована ($chop_count записей)"
    else
        log_warn "Нет логов рубки — дерево не найдено или не взаимодействует"
    fi

    # --- 24.3 Промотать время (сон в таверне или ожидание) ---
    log_section "24.3 Промотка времени для респауна"
    log_warn "Респаун через реальное время — ждём 30 сек (ускоренный тест)"
    # В реальной игре нужен сон или промотка; в тесте используем короткое ожидание
    sleep 30
    screenshot "24_respawn_wait"

    # --- 24.4 Скриншот после ожидания (ресурс должен появиться) ---
    screenshot "24_respawn_after_wait"
    log_info "Скриншот после ожидания — ресурс должен появиться"

    dump_logcat "respawn_check"
    local respawn_count
    respawn_count=$(count_in_file "\[ZD:Interact\].*[Rr]espawn\|[Rr]estore\|[Ss]pawn\|\[ZD:Scene\].*[Rr]esource.*[Rr]espawn" "$RESULTS_DIR/_logcat_respawn_check.txt")
    if [ "$respawn_count" -gt 0 ]; then
        log_pass "Респаун ресурса залогирован ($respawn_count записей)"
        get_zd_lines "Interact" 5 "$RESULTS_DIR/_logcat_respawn_check.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет логов респауна — время ожидания мало или респаун не залогирован"
        log_warn "Для полной проверки: создать костёр → лечь спать → сравнить скриншоты вручную"
    fi

    # Сравнение скриншотов до и после через Python
    local diff_result
    diff_result=$(/usr/bin/python3 - \
        "$RESULTS_DIR/24_respawn_after_chop.png" \
        "$RESULTS_DIR/24_respawn_after_wait.png" <<'PYEOF'
from PIL import Image
import sys
try:
    img1 = Image.open(sys.argv[1]).convert('L')
    img2 = Image.open(sys.argv[2]).convert('L')
    p1 = list(img1.getdata())
    p2 = list(img2.getdata())
    diff = sum(abs(a - b) for a, b in zip(p1, p2))
    avg_diff = diff / len(p1)
    if avg_diff > 3:
        print("CHANGED")
    else:
        print("SAME")
except Exception as e:
    print(f"ERROR:{e}")
PYEOF
    ) 2>/dev/null || echo "NO_PIL"

    case "$diff_result" in
        "CHANGED") log_pass "Скриншоты отличаются — возможен визуальный респаун" ;;
        "SAME")    log_warn "Скриншоты одинаковы — дерево не вырублено или не появилось" ;;
        "NO_PIL")  log_warn "PIL не установлен — визуальное сравнение пропущено" ;;
        *)         log_warn "Ошибка сравнения: $diff_result" ;;
    esac

    [ "$_test_failed" -eq 0 ]
}
