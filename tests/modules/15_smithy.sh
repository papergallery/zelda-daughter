#!/bin/bash
# Модуль 15: Кузница (TESTING_GUIDE секция 15)
# Проверяем: плавильня (руда → металл), наковальня (металл+палка → меч)

run_test_15_smithy() {
    log_section "15 | Кузница (секция 15)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 15.1 Тап по плавильне ---
    log_section "15.1 Тап по плавильне"
    tap 400 900
    sleep "$WAIT_ACTION"
    screenshot "15_smithy_smelter"

    dump_logcat "smithy_smelter"
    local smelter_count
    smelter_count=$(count_in_file "\[ZD:Interact\].*[Ss]melt\|[Ff]urnace\|[Pp]lace" "$RESULTS_DIR/_logcat_smithy_smelter.txt")
    if [ "$smelter_count" -gt 0 ]; then
        log_pass "Плавильня открыта ($smelter_count записей [ZD:Interact])"
    else
        log_warn "Нет логов плавильни — плавильня не найдена или не в городе"
    fi

    # --- 15.2 Drag руды в плавильню ---
    log_section "15.2 Плавка руды"
    # Из TESTING_GUIDE: swipe 300 600 540 900 500
    swipe 300 600 540 900 500
    sleep 5  # ждём плавку (таймер)
    screenshot "15_smithy_smelting"

    dump_logcat "smithy_smelt"
    local smelt_count
    smelt_count=$(count_in_file "\[ZD:Inventory\].*[Ss]melt\|[Mm]etal\|[Oo]re" "$RESULTS_DIR/_logcat_smithy_smelt.txt")
    if [ "$smelt_count" -gt 0 ]; then
        log_pass "Плавка залогирована ($smelt_count записей [ZD:Inventory])"
    else
        log_warn "Нет логов плавки — руда в инвентаре отсутствует или плавильня не активировалась"
    fi

    # Забрать металл
    tap 540 900
    sleep 1

    # --- 15.3 Тап по наковальне ---
    log_section "15.3 Тап по наковальне"
    tap 600 900
    sleep "$WAIT_ACTION"
    screenshot "15_smithy_anvil"

    dump_logcat "smithy_anvil"
    local anvil_count
    anvil_count=$(count_in_file "\[ZD:Interact\].*[Aa]nvil\|[Ff]org" "$RESULTS_DIR/_logcat_smithy_anvil.txt")
    if [ "$anvil_count" -gt 0 ]; then
        log_pass "Наковальня открыта ($anvil_count записей [ZD:Interact])"
    else
        log_warn "Нет логов наковальни — наковальня не найдена или рядом нет"
    fi

    # --- 15.4 Крафт меча (металл + палка) ---
    log_section "15.4 Крафт меча (металл + палка)"
    # Из TESTING_GUIDE: drag металл и палку в UI наковальни
    swipe 300 600 540 800 500
    sleep 1
    swipe 400 600 540 800 500
    sleep "$WAIT_CRAFT"
    screenshot "15_smithy_sword"

    dump_logcat "smithy_craft"
    local sword_count
    sword_count=$(count_in_file "\[ZD:Inventory\].*[Ss]word\|[Kk]nife\|[Cc]raft.*[Mm]etal" "$RESULTS_DIR/_logcat_smithy_craft.txt")
    if [ "$sword_count" -gt 0 ]; then
        log_pass "Оружие скрафтено на наковальне ($sword_count записей)"
    else
        log_warn "Нет логов крафта оружия — нет материалов или наковальня не активирована"
    fi

    close_ui

    [ "$_test_failed" -eq 0 ]
}
