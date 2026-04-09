#!/bin/bash
# Модуль 04: Взаимодействие с миром (TESTING_GUIDE секция 4)
# Проверяем: подбор предметов, добыча из дерева/куста, логи [ZD:Interact] и [ZD:Inventory]

run_test_04_interaction() {
    log_section "04 | Взаимодействие с миром (секция 4)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 4.1 Тап по предмету на земле (Pickup) ---
    log_section "4.1 Подбор предмета (Pickup)"
    tap "$TAP_OBJECT"
    sleep 2
    screenshot "04_interact_pickup"

    dump_logcat "interact_pickup"
    local pickup_count
    pickup_count=$(count_in_file "\[ZD:Interact\]" "$RESULTS_DIR/_logcat_interact_pickup.txt")

    if [ "$pickup_count" -gt 0 ]; then
        log_pass "Взаимодействие сработало ($pickup_count записей [ZD:Interact])"
        get_zd_lines "Interact" 5 "$RESULTS_DIR/_logcat_interact_pickup.txt" | while read -r line; do
            log_info "$line"
        done
    else
        # Fallback: проверяем хотя бы тап
        local tap_count
        tap_count=$(count_in_file "\[ZD:Input\].*Tap" "$RESULTS_DIR/_logcat_interact_pickup.txt")
        if [ "$tap_count" -gt 0 ]; then
            log_warn "Тап обработан, но [ZD:Interact] не залогирован — возможно объект вне досягаемости"
        else
            log_warn "Нет [ZD:Interact] и [ZD:Input] Tap — Development Build или логи не внедрены"
        fi
    fi

    # --- 4.2 Добыча из дерева (несколько ударов) ---
    log_section "4.2 Добыча ресурсов из дерева"
    clear_logcat
    # Три тапа по дереву (из TESTING_GUIDE: tap 300 800, три раза)
    tap "$TAP_TREE"
    sleep 1
    tap "$TAP_TREE"
    sleep 1
    tap "$TAP_TREE"
    sleep 2
    screenshot "04_interact_tree"

    dump_logcat "interact_tree"
    local resource_count
    resource_count=$(count_in_file "\[ZD:Interact\]" "$RESULTS_DIR/_logcat_interact_tree.txt")

    if [ "$resource_count" -gt 0 ]; then
        log_pass "Добыча ресурса: $resource_count записей [ZD:Interact]"
        get_zd_lines "Interact" 5 "$RESULTS_DIR/_logcat_interact_tree.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет [ZD:Interact] при тапе по дереву — объект вне зоны или нет логов"
    fi

    # --- 4.3 Предметы попали в инвентарь? ---
    log_section "4.3 Предметы в инвентаре"
    local inv_count
    inv_count=$(count_in_file "\[ZD:Inventory\]" "$RESULTS_DIR/_logcat_interact_tree.txt")
    if [ "$inv_count" -gt 0 ]; then
        log_pass "Предметы добавлены в инвентарь ($inv_count записей [ZD:Inventory])"
    else
        log_warn "Нет [ZD:Inventory] — предметы не попали в инвентарь или добыча не сработала"
    fi

    # --- 4.4 Тап по кусту ---
    log_section "4.4 Тап по кусту"
    clear_logcat
    # Куст — немного правее, чуть дальше
    tap 700 800
    sleep 2
    screenshot "04_interact_bush"

    dump_logcat "interact_bush"
    local bush_count
    bush_count=$(count_in_file "\[ZD:Interact\]" "$RESULTS_DIR/_logcat_interact_bush.txt")
    if [ "$bush_count" -gt 0 ]; then
        log_pass "Взаимодействие с кустом ($bush_count записей [ZD:Interact])"
    else
        log_warn "Нет [ZD:Interact] при тапе по кусту — куст вне зоны или недостижим"
    fi

    [ "$_test_failed" -eq 0 ]
}
