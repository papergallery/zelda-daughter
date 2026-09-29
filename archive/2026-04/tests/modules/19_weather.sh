#!/bin/bash
# Модуль 19: Погода (TESTING_GUIDE секция 19)
# Фоновый модуль: проверяем логи погоды, скриншоты текущей погоды

run_test_19_weather() {
    log_section "19 | Погода (секция 19)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    # --- 19.1 Скриншот текущей погоды ---
    screenshot "19_weather_current"
    log_info "Скриншот текущей погоды сохранён"

    # --- 19.2 Проверка логов погоды ---
    dump_logcat "weather"

    local weather_count
    weather_count=$(count_in_file "\[ZD:Scene\].*[Ww]eather\|[Rr]ain\|[Ss]torm\|[Ss]unny\|[Cc]loud" "$RESULTS_DIR/_logcat_weather.txt")
    if [ "$weather_count" -gt 0 ]; then
        log_pass "Погода залогирована ($weather_count записей [ZD:Scene])"
        get_zd_lines "Scene" 5 "$RESULTS_DIR/_logcat_weather.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет [ZD:Scene] с погодными событиями — погода может не меняться или не залогирована"
    fi

    # --- 19.3 Проверка взаимодействий стихий ---
    log_section "19.3 Дождь тушит огонь, огонь в воде"
    local element_count
    element_count=$(count_in_file "\[ZD:Scene\].*[Ff]ire\|[Ee]xtinguish\|[Ww]et\|\[ZD:Inventory\].*[Ff]ire" "$RESULTS_DIR/_logcat_weather.txt")
    if [ "$element_count" -gt 0 ]; then
        log_pass "Взаимодействие стихий зафиксировано ($element_count записей)"
    else
        log_warn "Нет событий взаимодействия стихий — дождя нет или огонь не создавался"
    fi

    log_warn "Полная проверка погоды требует ожидания смены погодного цикла (случайный интервал)"

    [ "$_test_failed" -eq 0 ]
}
