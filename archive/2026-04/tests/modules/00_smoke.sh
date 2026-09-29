#!/bin/bash
# Модуль 00: Быстрый smoke-тест (TESTING_GUIDE секция 0.5)
# Портирован из smoke_test.sh с использованием утилит фреймворка

run_test_00_smoke() {
    log_section "00 | Smoke Test (секция 0.5)"

    # Предусловие
    if ! ensure_running; then
        log_fail "Приложение не запускается — smoke провален"
        return 1
    fi

    # --- 1/5. Запуск: экран не розовый? ---
    log_section "1/5 Запуск: экран не розовый?"
    sleep "$WAIT_LAUNCH"
    screenshot "00_smoke_01_launch"

    local pink_result
    pink_result=$(check_pink "$RESULTS_DIR/00_smoke_01_launch.png")
    case "$pink_result" in
        "OK")       log_pass "Экран не розовый — шейдеры URP корректны" ;;
        "PINK")     log_fail "Розовый экран (>80%) — шейдеры URP не включены в билд" ;;
        "PARTIAL")  log_fail "Частично розовый экран — часть шейдеров отсутствует" ;;
        "NO_PIL")   log_warn "PIL не установлен — пропуск проверки цвета" ;;
        *)          log_warn "Ошибка анализа скриншота: $pink_result" ;;
    esac

    # --- 2/5. Свайп: персонаж двигается? ---
    log_section "2/5 Свайп: персонаж двигается?"
    clear_logcat
    sleep 1
    swipe 540 1400 540 800 300
    sleep 1
    screenshot "00_smoke_02_after_swipe"

    dump_logcat "smoke_move"
    local move_count
    move_count=$(count_in_file "\[ZD:Move\]" "$RESULTS_DIR/_logcat_smoke_move.txt")
    local input_count
    input_count=$(count_in_file "\[ZD:Input\]" "$RESULTS_DIR/_logcat_smoke_move.txt")

    if [ "$move_count" -gt 0 ]; then
        log_pass "Персонаж двигается ($move_count записей [ZD:Move])"
    elif [ "$input_count" -gt 0 ]; then
        log_pass "Input обработан ($input_count [ZD:Input]), движение не залогировано"
    else
        log_warn "Нет [ZD:Move] / [ZD:Input] — Development Build отключён или логи не внедрены"
    fi

    # --- 3/5. Тап по объекту: взаимодействие работает? ---
    log_section "3/5 Тап: взаимодействие работает?"
    clear_logcat
    sleep 1
    tap 540 1000
    sleep 1
    screenshot "00_smoke_03_after_tap"

    dump_logcat "smoke_tap"
    local interact_count
    interact_count=$(count_in_file "\[ZD:Interact\]" "$RESULTS_DIR/_logcat_smoke_tap.txt")
    local tap_count
    tap_count=$(count_in_file "\[ZD:Input\].*Tap" "$RESULTS_DIR/_logcat_smoke_tap.txt")

    if [ "$interact_count" -gt 0 ]; then
        log_pass "Взаимодействие работает ($interact_count записей [ZD:Interact])"
    elif [ "$tap_count" -gt 0 ]; then
        log_pass "Тап обработан ($tap_count [ZD:Input] Tap), но объект не найден"
    else
        log_warn "Нет [ZD:Interact] / [ZD:Input] Tap — Development Build отключён"
    fi

    # --- 4/5. Ошибки: logcat чистый? ---
    log_section "4/5 Ошибки: logcat чистый?"
    dump_logcat "smoke_errors"
    if check_no_errors "$RESULTS_DIR/_logcat_smoke_errors.txt"; then
        log_pass "Нет Exception/NullRef/CRASH в Unity logcat"
    else
        local err_count
        err_count=$(count_in_file "Exception\|NullRef\|CRASH\|Fatal" "$RESULTS_DIR/_logcat_smoke_errors.txt")
        log_fail "Найдено $err_count ошибок в logcat"
    fi

    # --- 5/5. FPS >= 30? ---
    log_section "5/5 FPS >= 30?"
    dump_logcat "smoke_fps"
    local fps_val
    fps_val=$(get_fps_from_logcat "$RESULTS_DIR/_logcat_smoke_fps.txt")

    if [ "$fps_val" -gt 0 ] 2>/dev/null; then
        if [ "$fps_val" -ge 30 ]; then
            log_pass "FPS=$fps_val (>= 30)"
        elif [ "$fps_val" -ge 15 ]; then
            log_warn "FPS=$fps_val (< 30, приемлемо для эмулятора)"
        else
            log_fail "FPS=$fps_val (< 15, критично)"
        fi
    else
        local mem_mb
        mem_mb=$(get_memory_mb)
        if [ "$mem_mb" -gt 0 ]; then
            log_warn "Нет [ZD:Perf]. Память: ${mem_mb}MB (dumpsys). Development Build?"
        else
            log_warn "Нет данных FPS — Development Build отключён или логи не внедрены"
        fi
    fi

    [ "$_test_failed" -eq 0 ]
}
