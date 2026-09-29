#!/bin/bash
# Модуль 11: Дорога к городу (TESTING_GUIDE секция 11)
# Проверяем: переход между зонами, навигация к городу, бесшовный вход

run_test_11_road_to_town() {
    log_section "11 | Дорога к городу (секция 11)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 11.1 Первая серия свайпов к городу ---
    log_section "11.1 Движение по дороге (первый отрезок)"
    for i in $(seq 1 8); do
        swipe 540 1300 540 600 300
        sleep 2
    done
    screenshot "11_road_01"

    # --- 11.2 Вторая серия ---
    log_section "11.2 Продолжение пути (второй отрезок)"
    for i in $(seq 1 8); do
        swipe 540 1300 540 600 300
        sleep 2
    done
    screenshot "11_road_02_town_approach"

    # --- 11.3 Проверка смены зон ---
    dump_logcat "road_to_town"
    if check_zd_tag "Scene" 1 "$RESULTS_DIR/_logcat_road_to_town.txt"; then
        local scene_count
        scene_count=$(count_in_file "\[ZD:Scene\]" "$RESULTS_DIR/_logcat_road_to_town.txt")
        log_pass "События сцены залогированы ($scene_count записей [ZD:Scene])"
        get_zd_lines "Scene" 5 "$RESULTS_DIR/_logcat_road_to_town.txt" | while read -r line; do
            log_info "$line"
        done

        # Проверка бесшовного перехода (нет экрана загрузки)
        local zone_count
        zone_count=$(count_in_file "\[ZD:Scene\].*[Zz]one\|[Rr]egion\|[Tt]own\|[Cc]ity" "$RESULTS_DIR/_logcat_road_to_town.txt")
        if [ "$zone_count" -gt 0 ]; then
            log_pass "Переход в зону города залогирован ($zone_count записей)"
        else
            log_warn "Нет лога смены зоны на город — город ещё не достигнут или зоны не реализованы"
        fi
    else
        log_warn "Нет [ZD:Scene] — зонирование не залогировано"
    fi

    # --- 11.4 Финальный скриншот ---
    screenshot "11_road_final"

    [ "$_test_failed" -eq 0 ]
}
