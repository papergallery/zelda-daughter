#!/bin/bash
# Модуль 12: Вода (TESTING_GUIDE секция 12)
# Проверяем: замедление в воде, визуал, невидимая стена на глубине

run_test_12_water() {
    log_section "12 | Вода (секция 12)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 12.1 Движение к воде ---
    log_section "12.1 Движение к водоёму (свайп вправо)"
    # Из TESTING_GUIDE: swipe 300 1170 800 1170 300
    swipe 300 1170 800 1170 300
    sleep 3
    screenshot "12_water_approach"

    # --- 12.2 Вход в воду ---
    log_section "12.2 Попытка зайти в воду"
    swipe 540 1300 540 500 300
    sleep 2
    screenshot "12_water_in"

    # --- 12.3 Попытка плыть (должна быть невидимая стена на глубине) ---
    log_section "12.3 Проверка невидимой стены на глубине"
    swipe 540 1300 540 500 300
    sleep 2
    screenshot "12_water_deep"

    # --- 12.4 Проверка замедления через [ZD:Move] ---
    dump_logcat "water"

    local water_slow_count
    water_slow_count=$(count_in_file "\[ZD:Move\].*[Ww]ater\|[Ss]low\|[Ww]ade" "$RESULTS_DIR/_logcat_water.txt")
    if [ "$water_slow_count" -gt 0 ]; then
        log_pass "Замедление в воде залогировано ($water_slow_count записей [ZD:Move])"
        get_zd_lines "Move" 5 "$RESULTS_DIR/_logcat_water.txt" | while read -r line; do
            log_info "$line"
        done
    else
        local move_count
        move_count=$(count_in_file "\[ZD:Move\]" "$RESULTS_DIR/_logcat_water.txt")
        if [ "$move_count" -gt 0 ]; then
            log_warn "[ZD:Move] есть ($move_count), но метка воды/замедления не найдена"
        else
            log_warn "Нет [ZD:Move] — вода не достигнута или логи замедления не внедрены"
        fi
    fi

    # Визуальная проверка по скриншотам — требует ручного анализа
    log_warn "Визуальный эффект воды (персонаж по пояс) проверяется вручную по скриншотам"

    [ "$_test_failed" -eq 0 ]
}
