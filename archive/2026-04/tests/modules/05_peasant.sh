#!/bin/bash
# Модуль 05: Пролог — крестьянин (TESTING_GUIDE секция 5)
# Проверяем: NPC-взаимодействие через иконки, иконки направления к городу

run_test_05_peasant() {
    log_section "05 | Пролог: крестьянин (секция 5)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 5.1 Пройти по дороге к крестьянину (~15 сек ходьбы = 5 свайпов по 3 сек) ---
    log_section "5.1 Движение по дороге к крестьянину"
    for i in $(seq 1 5); do
        swipe 540 1400 540 700 300
        sleep 3
    done
    screenshot "05_peasant_road"

    # --- 5.2 Тап по NPC (крестьянин) ---
    log_section "5.2 Тап по NPC"
    tap "$TAP_NPC"
    sleep "$WAIT_DIALOG"
    screenshot "05_peasant_dialog"

    dump_logcat "peasant"

    # Проверка взаимодействия с NPC
    local interact_count
    interact_count=$(count_in_file "\[ZD:Interact\]" "$RESULTS_DIR/_logcat_peasant.txt")
    if [ "$interact_count" -gt 0 ]; then
        log_pass "NPC взаимодействие ($interact_count записей [ZD:Interact])"
        get_zd_lines "Interact" 5 "$RESULTS_DIR/_logcat_peasant.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет [ZD:Interact] при тапе по NPC — NPC вне зоны или не достигнут"
    fi

    # --- 5.3 Тап по иконке-ответу ---
    log_section "5.3 Тап по иконке ответа (нижняя область)"
    tap 540 1600
    sleep 1
    screenshot "05_peasant_response"

    local npc_interact_count
    npc_interact_count=$(count_in_file "\[ZD:Interact\].*npc\|NPC\|dialog\|Dialog" "$RESULTS_DIR/_logcat_peasant.txt")
    if [ "$npc_interact_count" -gt 0 ]; then
        log_pass "Диалог с NPC зафиксирован в логах"
    else
        log_warn "Диалог с NPC не залогирован — NPC может быть ещё не реализован"
    fi

    [ "$_test_failed" -eq 0 ]
}
