#!/bin/bash
# Модуль 06: Инвентарь (TESTING_GUIDE секция 6)
# Проверяем: лонг-пресс, радиальное меню, открытие инвентаря, закрытие

run_test_06_inventory() {
    log_section "06 | Инвентарь (секция 6)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 6.1 Лонг-пресс на персонаже (центр экрана, ~600мс без движения) ---
    log_section "6.1 Лонг-пресс на персонаже"
    longpress "$CENTER_X" "$CENTER_Y" 600
    sleep "$WAIT_ACTION"
    screenshot "06_inventory_radial"

    dump_logcat "inventory_longpress"
    if check_zd_tag "Input" 1 "$RESULTS_DIR/_logcat_inventory_longpress.txt"; then
        local lp_count
        lp_count=$(count_in_file "\[ZD:Input\].*LongPress\|longpress\|long_press" "$RESULTS_DIR/_logcat_inventory_longpress.txt")
        if [ "$lp_count" -gt 0 ]; then
            log_pass "LongPress зафиксирован в [ZD:Input] ($lp_count записей)"
        else
            log_warn "[ZD:Input] есть, но LongPress не найден — возможно лог-формат отличается"
        fi
    else
        log_warn "Нет [ZD:Input] — лонг-пресс не залогирован"
    fi

    # --- 6.2 Открыть инвентарь (свайп к сектору инвентаря от центра) ---
    log_section "6.2 Открытие инвентаря через радиальное меню"
    # Свайп вправо к сектору "Инвентарь" (из TESTING_GUIDE)
    swipe "$CENTER_X" "$CENTER_Y" 700 "$CENTER_Y" 200
    sleep "$WAIT_ACTION"
    screenshot "06_inventory_open"

    dump_logcat "inventory_open"
    if check_zd_tag "Inventory" 1 "$RESULTS_DIR/_logcat_inventory_open.txt"; then
        local inv_count
        inv_count=$(count_in_file "\[ZD:Inventory\]" "$RESULTS_DIR/_logcat_inventory_open.txt")
        log_pass "Инвентарь открыт ($inv_count записей [ZD:Inventory])"
        get_zd_lines "Inventory" 5 "$RESULTS_DIR/_logcat_inventory_open.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет [ZD:Inventory] — инвентарь не открылся или не залогирован"
    fi

    # --- 6.3 Тап по первому предмету в инвентаре ---
    log_section "6.3 Тап по предмету в инвентаре"
    tap 300 600
    sleep 1
    screenshot "06_inventory_item_desc"

    # --- 6.4 Закрыть инвентарь (тап вне панели или Close) ---
    log_section "6.4 Закрытие инвентаря"
    close_ui
    screenshot "06_inventory_closed"

    # --- 6.5 Проверка лонг-пресса на ПУСТОМ месте (не должен открыть меню) ---
    log_section "6.5 Лонг-пресс на пустом месте — меню не должно открыться"
    clear_logcat
    longpress 100 400 600
    sleep "$WAIT_ACTION"
    screenshot "06_inventory_empty_longpress"

    dump_logcat "inventory_empty_lp"
    # На пустом месте радиальное меню не должно открываться
    local empty_lp_inv
    empty_lp_inv=$(count_in_file "\[ZD:Inventory\].*open\|Open" "$RESULTS_DIR/_logcat_inventory_empty_lp.txt")
    if [ "$empty_lp_inv" -eq 0 ]; then
        log_pass "Лонг-пресс на пустом месте — меню не открылось (корректно)"
    else
        log_fail "Лонг-пресс на пустом месте открыл меню — нарушение дизайна"
    fi

    [ "$_test_failed" -eq 0 ]
}
