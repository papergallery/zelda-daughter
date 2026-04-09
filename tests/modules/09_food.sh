#!/bin/bash
# Модуль 09: Еда и голод (TESTING_GUIDE секция 9)
# Проверяем: drag еды на персонажа, поедание, логи голода

run_test_09_food() {
    log_section "09 | Еда и голод (секция 9)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 9.1 Открыть инвентарь ---
    log_section "9.1 Открыть инвентарь для еды"
    open_inventory
    screenshot "09_food_inventory"

    # --- 9.2 Drag еды из слота на персонажа (центр экрана) ---
    log_section "9.2 Drag ягод/еды на персонажа"
    # Из TESTING_GUIDE: swipe 300 500 540 1170 500
    # Слот с едой (примерный слот 1: x=300, y=500) → персонаж (центр: 540, 1170)
    swipe 300 500 540 1170 500
    sleep 2
    screenshot "09_food_eat"

    # --- 9.3 Проверка логов ---
    dump_logcat "food"

    local eat_count
    eat_count=$(count_in_file "\[ZD:Inventory\].*[Ee]at\|[Ff]ood\|[Hh]unger\|[Cc]onsume" "$RESULTS_DIR/_logcat_food.txt")
    if [ "$eat_count" -gt 0 ]; then
        log_pass "Поедание еды зафиксировано ($eat_count записей [ZD:Inventory])"
        get_zd_lines "Inventory" 5 "$RESULTS_DIR/_logcat_food.txt" | while read -r line; do
            log_info "$line"
        done
    else
        # Пробуем искать через [ZD:Combat] — хил регистрируется там
        local heal_count
        heal_count=$(count_in_file "\[ZD:Combat\].*[Hh]eal\|[Rr]ecover" "$RESULTS_DIR/_logcat_food.txt")
        if [ "$heal_count" -gt 0 ]; then
            log_pass "Хил от еды зафиксирован в [ZD:Combat] ($heal_count записей)"
        else
            log_warn "Нет логов поедания еды — слот пустой или drag не попал на персонажа"
        fi
    fi

    # --- 9.4 Закрыть инвентарь ---
    close_ui
    sleep 1

    # --- 9.5 Проверка логов голода (если уже долго не ели) ---
    log_section "9.5 Проверка голода"
    dump_logcat "hunger"
    local hunger_count
    hunger_count=$(count_in_file "\[ZD:Inventory\].*[Hh]unger\|[Ss]tarv" "$RESULTS_DIR/_logcat_hunger.txt")
    if [ "$hunger_count" -gt 0 ]; then
        log_pass "Голод залогирован ($hunger_count записей)"
    else
        log_warn "Нет логов голода — скрытый голод ещё не накопился или не залогирован"
    fi

    [ "$_test_failed" -eq 0 ]
}
