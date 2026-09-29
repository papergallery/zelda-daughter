#!/bin/bash
# Модуль 13: Город и NPC (TESTING_GUIDE секция 13)
# Проверяем: диалоги, языковую механику (кракозябры), несколько NPC

run_test_13_town_npc() {
    log_section "13 | Город и NPC (секция 13)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 13.1 Первый NPC (торговец/стражник у входа) ---
    log_section "13.1 Тап по NPC в городе"
    tap 400 900
    sleep "$WAIT_DIALOG"
    screenshot "13_npc_01_dialog"

    dump_logcat "npc_dialog"

    local npc_count
    npc_count=$(count_in_file "\[ZD:Interact\].*[Nn][Pp][Cc]\|[Dd]ialog" "$RESULTS_DIR/_logcat_npc_dialog.txt")
    if [ "$npc_count" -gt 0 ]; then
        log_pass "Диалог с NPC ($npc_count записей [ZD:Interact])"
        get_zd_lines "Interact" 5 "$RESULTS_DIR/_logcat_npc_dialog.txt" | while read -r line; do
            log_info "$line"
        done
    else
        local tap_count
        tap_count=$(count_in_file "\[ZD:Input\].*Tap" "$RESULTS_DIR/_logcat_npc_dialog.txt")
        if [ "$tap_count" -gt 0 ]; then
            log_warn "Тап обработан, но NPC не найден — NPC не в зоне досягаемости"
        else
            log_warn "Нет [ZD:Interact] при тапе по NPC — Development Build или NPC не реализован"
        fi
    fi

    # --- 13.2 Тап по варианту ответа ---
    log_section "13.2 Тап по варианту ответа иконкой"
    tap 540 1600
    sleep 2
    screenshot "13_npc_01_response"

    # --- 13.3 Второй NPC ---
    log_section "13.3 Второй NPC (разнообразие диалогов)"
    # Закрыть диалог
    tap 540 1800
    sleep 1
    # Подойти к другому NPC
    swipe 540 1170 300 1170 300
    sleep 2
    tap 350 900
    sleep "$WAIT_DIALOG"
    screenshot "13_npc_02_dialog"

    dump_logcat "npc2"
    local npc2_count
    npc2_count=$(count_in_file "\[ZD:Interact\]" "$RESULTS_DIR/_logcat_npc2.txt")
    if [ "$npc2_count" -gt 0 ]; then
        log_pass "Второй NPC взаимодействие ($npc2_count записей)"
    else
        log_warn "Нет логов второго NPC — только один NPC в зоне или не достигнут"
    fi

    # --- 13.4 Разблокировка слов (языковая прогрессия) ---
    log_section "13.4 Проверка языковой механики"
    # После нескольких диалогов часть слов должна раскрываться
    local lang_count
    lang_count=$(count_in_file "\[ZD:Interact\].*[Ll]ang\|[Ww]ord\|[Ss]cramble\|[Ll]earn" "$RESULTS_DIR/_logcat_npc2.txt")
    if [ "$lang_count" -gt 0 ]; then
        log_pass "Языковая прогрессия ($lang_count записей)"
    else
        log_warn "Нет логов языковой механики — не реализована или недостаточно диалогов"
    fi

    # Закрыть диалог
    tap 540 1800
    sleep 1

    [ "$_test_failed" -eq 0 ]
}
