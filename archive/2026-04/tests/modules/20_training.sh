#!/bin/bash
# Модуль 20: Тренировка оружия (TESTING_GUIDE секция 20)
# Проверяем: манекен, мастерство оружия, [ZD:Progression], [ZD:Combat]

run_test_20_training() {
    log_section "20 | Тренировка оружия (секция 20)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 20.1 Серия тапов по тренировочному манекену ---
    log_section "20.1 Серия ударов по манекену (20 тапов)"
    # Из TESTING_GUIDE: tap 540 800, 20 раз с паузой 0.5 сек
    for i in $(seq 1 20); do
        tap 540 800
        sleep 0.5
    done
    screenshot "20_training_hits"

    # --- 20.2 Проверка прогрессии ---
    dump_logcat "training"

    if check_zd_tag "Progression" 1 "$RESULTS_DIR/_logcat_training.txt"; then
        local prog_count
        prog_count=$(count_in_file "\[ZD:Progression\]" "$RESULTS_DIR/_logcat_training.txt")
        log_pass "[ZD:Progression] залогирован ($prog_count записей)"

        local weapon_count
        weapon_count=$(count_in_file "\[ZD:Progression\].*[Ww]eapon\|[Pp]roficien\|[Ss]kill\|[Mm]aster" "$RESULTS_DIR/_logcat_training.txt")
        if [ "$weapon_count" -gt 0 ]; then
            log_pass "Мастерство оружия растёт ($weapon_count записей)"
            get_zd_lines "Progression" 8 "$RESULTS_DIR/_logcat_training.txt" | while read -r line; do
                log_info "$line"
            done
        else
            log_warn "[ZD:Progression] есть, но weapon/proficiency не зафиксированы"
        fi
    else
        log_warn "Нет [ZD:Progression] — манекен не найден или система прогрессии не залогирована"
    fi

    # --- 20.3 Проверка боевых логов (удары по манекену) ---
    if check_zd_tag "Combat" 1 "$RESULTS_DIR/_logcat_training.txt"; then
        local combat_count
        combat_count=$(count_in_file "\[ZD:Combat\]" "$RESULTS_DIR/_logcat_training.txt")
        log_pass "[ZD:Combat] при ударах по манекену ($combat_count записей)"
    else
        log_warn "Нет [ZD:Combat] — манекен не реагирует на удары или не реализован"
    fi

    screenshot "20_training_final"

    [ "$_test_failed" -eq 0 ]
}
