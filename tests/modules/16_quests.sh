#!/bin/bash
# Модуль 16: Квесты и блокнот (TESTING_GUIDE секция 16)
# Проверяем: получение квестов через диалог, блокнот в радиальном меню

run_test_16_quests() {
    log_section "16 | Квесты и блокнот (секция 16)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 16.1 Поговорить со стражником (квест boar_hunt) ---
    log_section "16.1 Диалог со стражником (квест)"
    # Из TESTING_GUIDE: tap 600 800
    tap 600 800
    sleep "$WAIT_DIALOG"
    screenshot "16_quest_guard"

    # Тап по варианту ответа
    tap 540 1500
    sleep 2
    screenshot "16_quest_get"

    dump_logcat "quests"
    local quest_count
    quest_count=$(count_in_file "\[ZD:Interact\].*[Qq]uest\|[Tt]ask\|[Hh]unt" "$RESULTS_DIR/_logcat_quests.txt")
    if [ "$quest_count" -gt 0 ]; then
        log_pass "Квест получен ($quest_count записей [ZD:Interact])"
        get_zd_lines "Interact" 5 "$RESULTS_DIR/_logcat_quests.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет логов квеста — стражник не найден или квест не реализован"
    fi

    # Закрыть диалог
    tap 540 1800
    sleep 1

    # --- 16.2 Открыть блокнот через радиальное меню ---
    log_section "16.2 Открытие блокнота"
    # Из TESTING_GUIDE: лонг-пресс + свайп вверх к сектору блокнота
    longpress "$CENTER_X" "$CENTER_Y" 700
    sleep "$WAIT_ACTION"
    # Свайп вверх к сектору блокнота
    swipe "$CENTER_X" "$CENTER_Y" "$CENTER_X" 900 200
    sleep "$WAIT_ACTION"
    screenshot "16_quest_notebook"

    dump_logcat "quests_notebook"
    local notebook_count
    notebook_count=$(count_in_file "\[ZD:Inventory\].*[Nn]otebook\|[Bb]ook\|[Jj]ournal\|[Nn]ote" "$RESULTS_DIR/_logcat_quests_notebook.txt")
    if [ "$notebook_count" -gt 0 ]; then
        log_pass "Блокнот открыт ($notebook_count записей)"
    else
        log_warn "Нет логов блокнота — не реализован или свайп не попал в сектор"
    fi

    # --- 16.3 Закрыть блокнот ---
    tap 540 1800
    sleep 1

    [ "$_test_failed" -eq 0 ]
}
