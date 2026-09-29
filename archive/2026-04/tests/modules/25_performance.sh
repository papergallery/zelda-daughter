#!/bin/bash
# Модуль 25: Производительность (TESTING_GUIDE секция 25)
# Фоновый модуль: FPS, память, ANR, утечки, артефакты рендеринга

run_test_25_performance() {
    log_section "25 | Производительность (секция 25)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    # --- 25.1 FPS из [ZD:Perf] ---
    log_section "25.1 FPS из [ZD:Perf]"
    dump_logcat "perf"

    local fps_val
    fps_val=$(get_fps_from_logcat "$RESULTS_DIR/_logcat_perf.txt")
    if [ "$fps_val" -gt 0 ] 2>/dev/null; then
        if [ "$fps_val" -ge 30 ]; then
            log_pass "FPS=$fps_val (>= 30, норма)"
        elif [ "$fps_val" -ge 15 ]; then
            log_warn "FPS=$fps_val (15-30, приемлемо для эмулятора)"
        else
            log_fail "FPS=$fps_val (< 15, критично)"
        fi

        # Все строки [ZD:Perf] для полной картины
        get_zd_lines "Perf" 10 "$RESULTS_DIR/_logcat_perf.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет данных [ZD:Perf] FPS — Development Build или [ZD:Perf] не внедрён"
    fi

    # --- 25.2 Память через get_memory_mb ---
    log_section "25.2 Использование памяти"
    local mem_mb
    mem_mb=$(get_memory_mb)
    if [ "$mem_mb" -gt 0 ]; then
        if [ "$mem_mb" -le 512 ]; then
            log_pass "Память: ${mem_mb}MB (<= 512MB, норма)"
        elif [ "$mem_mb" -le 800 ]; then
            log_warn "Память: ${mem_mb}MB (512-800MB, повышенный расход)"
        else
            log_fail "Память: ${mem_mb}MB (> 800MB, критично для мобильного)"
        fi
    else
        log_warn "Нет данных памяти (dumpsys вернул 0) — приложение не запущено или ошибка"
    fi

    # --- 25.3 ANR (Application Not Responding) ---
    log_section "25.3 Проверка ANR"
    local anr_log
    anr_log=$(adb logcat -d 2>/dev/null | grep -i "ANR" | head -5 || true)
    if [ -z "$anr_log" ]; then
        log_pass "Нет ANR (Application Not Responding)"
    else
        log_fail "Обнаружены ANR события:"
        echo "$anr_log" | while read -r line; do
            log_info "$line"
        done
    fi

    # --- 25.4 Краши и ошибки за всю сессию ---
    log_section "25.4 Ошибки за всю сессию"
    if check_no_errors "$RESULTS_DIR/_logcat_perf.txt"; then
        log_pass "Нет Exception/NullRef/CRASH в текущем logcat"
    else
        local err_count
        err_count=$(count_in_file "Exception\|NullRef\|CRASH\|Fatal" "$RESULTS_DIR/_logcat_perf.txt")
        log_fail "Найдено $err_count ошибок в текущем logcat"
    fi

    # --- 25.5 Скриншот для проверки визуальных артефактов ---
    log_section "25.5 Визуальные артефакты"
    screenshot "25_perf_screenshot"
    local pink_result
    pink_result=$(check_pink "$RESULTS_DIR/25_perf_screenshot.png")
    case "$pink_result" in
        "OK")      log_pass "Нет розовых артефактов шейдеров" ;;
        "PINK")    log_fail "Розовый экран — шейдеры URP повредились в процессе" ;;
        "PARTIAL") log_fail "Частично розовый экран — часть шейдеров потерялась" ;;
        "NO_PIL")  log_warn "PIL не установлен — визуальная проверка пропущена" ;;
        *)         log_warn "Ошибка проверки: $pink_result" ;;
    esac

    # --- 25.6 Проверка утечек (повторный замер памяти после нагрузки) ---
    log_section "25.6 Проверка утечек памяти"
    # Серия действий для нагрузки
    for i in $(seq 1 5); do
        swipe_direction up
        sleep 0.3
    done
    open_inventory
    sleep 1
    close_ui
    sleep 1

    local mem_mb_after
    mem_mb_after=$(get_memory_mb)
    if [ "$mem_mb" -gt 0 ] && [ "$mem_mb_after" -gt 0 ]; then
        local mem_diff=$(( mem_mb_after - mem_mb ))
        if [ "$mem_diff" -le 50 ]; then
            log_pass "Утечек памяти нет (до=${mem_mb}MB, после=${mem_mb_after}MB, дельта=${mem_diff}MB)"
        else
            log_warn "Возможная утечка памяти: до=${mem_mb}MB, после=${mem_mb_after}MB, дельта=${mem_diff}MB"
        fi
    else
        log_warn "Нет данных для сравнения памяти"
    fi

    [ "$_test_failed" -eq 0 ]
}
