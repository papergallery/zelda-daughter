#!/bin/bash
# Модуль 02: Движение (TESTING_GUIDE секция 2)
# Проверяем: свайп-управление во всех направлениях, ходьба/бег, логи [ZD:Move] и [ZD:Input]

run_test_02_movement() {
    log_section "02 | Движение (секция 2)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- Ходьба: короткий свайп вверх ---
    log_section "2.1 Ходьба (короткий свайп)"
    swipe 540 1300 540 1000 300
    sleep 1
    screenshot "02_move_walk"

    # --- Бег: длинный свайп вверх ---
    log_section "2.2 Бег (длинный свайп)"
    swipe 540 1500 540 500 300
    sleep 1
    screenshot "02_move_run"

    # --- Все 4 направления ---
    log_section "2.3 Свайп влево"
    swipe_direction left
    sleep 1

    log_section "2.4 Свайп вправо"
    swipe_direction right
    sleep 1

    log_section "2.5 Свайп вниз"
    swipe_direction down
    sleep 1
    screenshot "02_move_directions"

    # --- Снова вперёд и стоп ---
    swipe_direction up
    sleep 1

    # --- Проверка логов ---
    dump_logcat "movement"

    if check_zd_tag "Move" 1 "$RESULTS_DIR/_logcat_movement.txt"; then
        local move_count
        move_count=$(count_in_file "\[ZD:Move\]" "$RESULTS_DIR/_logcat_movement.txt")
        log_pass "[ZD:Move] присутствует ($move_count записей)"
        get_zd_lines "Move" 5 "$RESULTS_DIR/_logcat_movement.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет [ZD:Move] — Development Build или логи движения не внедрены"
    fi

    if check_zd_tag "Input" 1 "$RESULTS_DIR/_logcat_movement.txt"; then
        local input_count
        input_count=$(count_in_file "\[ZD:Input\]" "$RESULTS_DIR/_logcat_movement.txt")
        log_pass "[ZD:Input] присутствует ($input_count записей)"
    else
        log_warn "Нет [ZD:Input] — система ввода не пишет логи"
    fi

    # --- Финальный скриншот ---
    screenshot "02_move_final"

    [ "$_test_failed" -eq 0 ]
}
