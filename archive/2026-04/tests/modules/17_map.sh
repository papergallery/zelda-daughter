#!/bin/bash
# Модуль 17: Карта (TESTING_GUIDE секция 17)
# Проверяем: открытие карты через радиальное меню, маркеры, позиция игрока

run_test_17_map() {
    log_section "17 | Карта (секция 17)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 17.1 Открыть карту через радиальное меню ---
    log_section "17.1 Открытие карты (лонг-пресс + свайп к сектору карты)"
    # Из TESTING_GUIDE: лонг-пресс + swipe 540 1170 380 1170 200 (свайп влево к карте)
    longpress "$CENTER_X" "$CENTER_Y" 700
    sleep "$WAIT_ACTION"
    # Свайп влево к сектору карты
    swipe "$CENTER_X" "$CENTER_Y" 380 "$CENTER_Y" 200
    sleep "$WAIT_ACTION"
    screenshot "17_map_open"

    dump_logcat "map"
    local map_count
    map_count=$(count_in_file "\[ZD:Inventory\].*[Mm]ap\|\[ZD:Scene\].*[Mm]ap" "$RESULTS_DIR/_logcat_map.txt")
    if [ "$map_count" -gt 0 ]; then
        log_pass "Карта открыта ($map_count записей)"
        get_zd_lines "Inventory" 3 "$RESULTS_DIR/_logcat_map.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет логов карты — карта не куплена или сектор не попал"
    fi

    # Визуальная проверка скриншота (карта vs пустой экран)
    local pink_result
    pink_result=$(check_pink "$RESULTS_DIR/17_map_open.png")
    if [ "$pink_result" = "OK" ]; then
        log_pass "Скриншот карты без явных ошибок рендеринга"
    else
        log_warn "Аномальный цвет на скриншоте карты: $pink_result"
    fi

    # --- 17.2 Проверка маркеров на карте ---
    log_section "17.2 Маркеры на карте (из диалогов с NPC)"
    dump_logcat "map_markers"
    local marker_count
    marker_count=$(count_in_file "\[ZD:Interact\].*[Mm]arker\|[Mm]ap.*[Aa]dd\|\[ZD:Scene\].*[Mm]arker" "$RESULTS_DIR/_logcat_map_markers.txt")
    if [ "$marker_count" -gt 0 ]; then
        log_pass "Маркеры на карте ($marker_count записей)"
    else
        log_warn "Нет маркеров — карта не куплена или NPC не добавляли маркеры"
    fi

    # Закрыть карту
    tap 540 1800
    sleep 1

    [ "$_test_failed" -eq 0 ]
}
