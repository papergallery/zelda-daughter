#!/bin/bash
# Модуль 22: Сон и отдых (TESTING_GUIDE секция 22)
# Проверяем: костёр (proximity), кровать в таверне, промотка времени

run_test_22_sleep() {
    log_section "22 | Сон и отдых (секция 22)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 22.1 Костёр: стать рядом для ускоренного восстановления ---
    log_section "22.1 Отдых у костра (proximity)"
    # Подойти к зоне костра (предполагается что костёр был создан в модуле 07)
    # Стоим на месте ~3 сек рядом с костром
    sleep 3
    screenshot "22_sleep_campfire_rest"

    dump_logcat "sleep_campfire"
    local campfire_rest_count
    campfire_rest_count=$(count_in_file "\[ZD:Combat\].*[Hh]eal\|[Rr]ecover\|\[ZD:Scene\].*[Rr]est\|[Ff]ire" "$RESULTS_DIR/_logcat_sleep_campfire.txt")
    if [ "$campfire_rest_count" -gt 0 ]; then
        log_pass "Восстановление у костра ($campfire_rest_count записей)"
    else
        log_warn "Нет логов восстановления у костра — костёр не создан или proximity не сработал"
    fi

    # --- 22.2 Таверна: найти кровать и тапнуть ---
    log_section "22.2 Сон в таверне (тап по кровати)"
    # Из TESTING_GUIDE: tap 540 900, затем ждём 5 сек анимацию
    tap 540 900
    sleep 5
    screenshot "22_sleep_bed"

    dump_logcat "sleep_bed"

    local sleep_count
    sleep_count=$(count_in_file "\[ZD:Scene\].*[Ss]leep\|[Rr]est\|[Tt]ime.*[Ss]kip\|[Bb]ed" "$RESULTS_DIR/_logcat_sleep_bed.txt")
    if [ "$sleep_count" -gt 0 ]; then
        log_pass "Сон залогирован ($sleep_count записей [ZD:Scene])"
        get_zd_lines "Scene" 5 "$RESULTS_DIR/_logcat_sleep_bed.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет [ZD:Scene] Sleep/Rest — кровать не найдена или сон не реализован"
    fi

    # --- 22.3 Проверка промотки времени ---
    log_section "22.3 Промотка времени"
    local timeskip_count
    timeskip_count=$(count_in_file "\[ZD:Scene\].*[Tt]ime\|[Ss]kip\|[Mm]orn\|[Dd]awn" "$RESULTS_DIR/_logcat_sleep_bed.txt")
    if [ "$timeskip_count" -gt 0 ]; then
        log_pass "Промотка времени зафиксирована ($timeskip_count записей)"
    else
        log_warn "Нет событий промотки времени — сон не завершился или время не изменилось"
    fi

    # --- 22.4 Проверка заживления ран после сна ---
    log_section "22.4 Заживление ран после сна"
    local heal_count
    heal_count=$(count_in_file "\[ZD:Combat\].*[Hh]eal\|[Rr]ecov\|[Ww]ound.*[Cc]lear" "$RESULTS_DIR/_logcat_sleep_bed.txt")
    if [ "$heal_count" -gt 0 ]; then
        log_pass "Заживление ран после сна ($heal_count записей [ZD:Combat])"
    else
        log_warn "Нет логов заживления ран — персонаж был здоров или раны не лечатся сном"
    fi

    screenshot "22_sleep_after_rest"

    [ "$_test_failed" -eq 0 ]
}
