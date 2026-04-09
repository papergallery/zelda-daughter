#!/bin/bash
# Модуль 10: Прогрессия навыков (TESTING_GUIDE секция 10)
# Проверяем: рост навыков через бой, логи [ZD:Progression]

run_test_10_progression() {
    log_section "10 | Прогрессия навыков (секция 10)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 10.1 Серия боёв для накопления прогрессии ---
    log_section "10.1 Серия ударов по врагу (10 тапов)"
    for i in $(seq 1 10); do
        tap 540 900
        sleep 0.5
    done
    sleep 2
    screenshot "10_progression_combat"

    # --- 10.2 Проверка [ZD:Progression] ---
    dump_logcat "progression"

    if check_zd_tag "Progression" 1 "$RESULTS_DIR/_logcat_progression.txt"; then
        local prog_count
        prog_count=$(count_in_file "\[ZD:Progression\]" "$RESULTS_DIR/_logcat_progression.txt")
        log_pass "[ZD:Progression] залогирован ($prog_count записей)"

        local stat_count
        stat_count=$(count_in_file "\[ZD:Progression\].*[Ss]tat\|[Cc]hang\|[Gg]row\|[Ll]evel" "$RESULTS_DIR/_logcat_progression.txt")
        if [ "$stat_count" -gt 0 ]; then
            log_pass "StatChanged зафиксирован ($stat_count записей)"
        else
            log_warn "[ZD:Progression] есть, но StatChanged не зафиксирован — мало ударов или порог высок"
        fi

        get_zd_lines "Progression" 8 "$RESULTS_DIR/_logcat_progression.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет [ZD:Progression] — система прогрессии не залогирована или не реализована"
    fi

    # --- 10.3 Много движения (тренировка выносливости) ---
    log_section "10.2 Серия свайпов (тренировка выносливости)"
    for i in $(seq 1 8); do
        swipe_direction up
        sleep 0.3
    done
    sleep 1

    dump_logcat "progression_move"
    local endurance_count
    endurance_count=$(count_in_file "\[ZD:Progression\].*[Ee]nduranc\|[Ss]tamin\|[Ss]trength" "$RESULTS_DIR/_logcat_progression_move.txt")
    if [ "$endurance_count" -gt 0 ]; then
        log_pass "Прогрессия выносливости/силы ($endurance_count записей)"
    else
        log_warn "Нет прогрессии выносливости — нужно больше движений или перегруз"
    fi

    screenshot "10_progression_final"

    [ "$_test_failed" -eq 0 ]
}
