#!/bin/bash
# Модуль 14: Торговля (TESTING_GUIDE секция 14)
# Проверяем: UI торговли, бартер, обмен предметами

run_test_14_trading() {
    log_section "14 | Торговля (секция 14)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 14.1 Тап по торговцу ---
    log_section "14.1 Тап по торговцу"
    tap 400 800
    sleep "$WAIT_DIALOG"
    screenshot "14_trade_npc"

    # --- 14.2 Тап по иконке "торговля" ---
    log_section "14.2 Открытие UI торговли"
    tap 540 1500
    sleep "$WAIT_ACTION"
    screenshot "14_trade_ui"

    dump_logcat "trading"
    local trade_ui_count
    trade_ui_count=$(count_in_file "\[ZD:Inventory\].*[Tt]rad\|[Bb]arter\|[Ss]hop" "$RESULTS_DIR/_logcat_trading.txt")
    if [ "$trade_ui_count" -gt 0 ]; then
        log_pass "UI торговли открыт ($trade_ui_count записей [ZD:Inventory])"
        get_zd_lines "Inventory" 5 "$RESULTS_DIR/_logcat_trading.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет логов торговли — торговец не найден или UI не открылся"
    fi

    # --- 14.3 Drag предмет игрока в зону обмена ---
    log_section "14.3 Drag предмета игрока в зону обмена"
    # Из TESTING_GUIDE: swipe 200 600 540 900 500
    swipe 200 600 540 900 500
    sleep "$WAIT_ACTION"

    # --- 14.4 Drag предмет торговца в зону обмена ---
    log_section "14.4 Drag предмета торговца в зону обмена"
    # Из TESTING_GUIDE: swipe 800 600 540 900 500
    swipe 800 600 540 900 500
    sleep "$WAIT_ACTION"
    screenshot "14_trade_deal"

    # --- 14.5 Подтвердить обмен ---
    log_section "14.5 Подтверждение сделки"
    tap 540 1100
    sleep "$WAIT_ACTION"
    screenshot "14_trade_done"

    dump_logcat "trading_confirm"
    local trade_confirm_count
    trade_confirm_count=$(count_in_file "\[ZD:Inventory\].*[Tt]rad\|[Ee]xchang\|[Cc]onfirm" "$RESULTS_DIR/_logcat_trading_confirm.txt")
    if [ "$trade_confirm_count" -gt 0 ]; then
        log_pass "Сделка подтверждена ($trade_confirm_count записей)"
    else
        log_warn "Нет логов подтверждения сделки — обмен не завершён или не залогирован"
    fi

    # Закрыть UI
    close_ui

    [ "$_test_failed" -eq 0 ]
}
