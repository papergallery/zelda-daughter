#!/bin/bash
# Модуль 07: Крафт и костёр (TESTING_GUIDE секция 7)
# Проверяем: drag&drop крафт, рецепты, размещение предмета в мире

run_test_07_crafting() {
    log_section "07 | Крафт (секция 7)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 7.1 Открыть инвентарь ---
    log_section "7.1 Открыть инвентарь"
    open_inventory
    screenshot "07_craft_inventory_open"

    # --- 7.2 Drag предмет из слота 1 на слот 2 (крафт) ---
    log_section "7.2 Drag для крафта (слот A → слот B)"
    # Лонг-пресс на предмете в слоте 1 (начало drag)
    # Затем свайп до слота 2 (крафтовая пара из TESTING_GUIDE)
    swipe 200 500 400 500 500
    sleep "$WAIT_CRAFT"
    screenshot "07_craft_result"

    dump_logcat "crafting"
    if check_zd_tag "Inventory" 1 "$RESULTS_DIR/_logcat_crafting.txt"; then
        local craft_count
        craft_count=$(count_in_file "\[ZD:Inventory\].*[Cc]raft" "$RESULTS_DIR/_logcat_crafting.txt")
        if [ "$craft_count" -gt 0 ]; then
            log_pass "Крафт выполнен ($craft_count записей [ZD:Inventory] Craft)"
            get_zd_lines "Inventory" 5 "$RESULTS_DIR/_logcat_crafting.txt" | while read -r line; do
                log_info "$line"
            done
        else
            log_warn "[ZD:Inventory] есть, но Craft не зафиксирован — слоты пустые или несовместимые"
        fi
    else
        log_warn "Нет [ZD:Inventory] — крафт не выполнен или не залогирован"
    fi

    # --- 7.3 Drag предмета за пределы инвентаря (размещение в мире) ---
    log_section "7.3 Размещение предмета в мире (drag за пределы инвентаря)"
    # Из TESTING_GUIDE: swipe 200 500 540 1400 800
    swipe 200 500 540 1400 800
    sleep "$WAIT_ACTION"
    screenshot "07_craft_place_world"

    dump_logcat "crafting_place"
    local place_count
    place_count=$(count_in_file "\[ZD:Inventory\].*[Pp]lace\|[Dd]rop\|[Ww]orld" "$RESULTS_DIR/_logcat_crafting_place.txt")
    if [ "$place_count" -gt 0 ]; then
        log_pass "Предмет размещён в мире ($place_count записей)"
    else
        log_warn "Нет логов размещения предмета — drag за пределы не сработал или не залогирован"
    fi

    # --- 7.4 Костёр: разместить дрова через drag ---
    log_section "7.4 Крафт костра (дрова → мир, кремень → дрова)"
    # Закрыть инвентарь если открыт
    close_ui
    sleep 1

    # Открыть инвентарь снова
    open_inventory
    sleep 1

    # Drag дрова в мир (нижняя часть экрана)
    swipe 200 500 540 1600 800
    sleep "$WAIT_ACTION"

    # Drag кремень на дрова в мире (примерная позиция)
    open_inventory
    sleep 1
    swipe 400 500 540 1600 800
    sleep "$WAIT_CRAFT"
    screenshot "07_craft_campfire"

    dump_logcat "crafting_fire"
    local fire_count
    fire_count=$(count_in_file "\[ZD:Inventory\].*[Ff]ire\|[Cc]ampfire\|[Bb]onfire" "$RESULTS_DIR/_logcat_crafting_fire.txt")
    if [ "$fire_count" -gt 0 ]; then
        log_pass "Костёр создан ($fire_count записей)"
    else
        log_warn "Нет логов костра — рецепт дрова+кремень не сработал или не залогирован"
    fi

    close_ui

    [ "$_test_failed" -eq 0 ]
}
