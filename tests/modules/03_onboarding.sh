#!/bin/bash
# Модуль 03: Онбординг (TESTING_GUIDE секция 3)
# Проверяем: подсказки появляются, не блокируют управление, исчезают после действия

run_test_03_onboarding() {
    log_section "03 | Онбординг (секция 3)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    # Перезапуск чтобы поймать начало онбординга
    kill_app
    clear_logcat
    launch_app
    sleep "$WAIT_LAUNCH"

    # --- Скриншот до любых действий: должна быть подсказка свайпа ---
    screenshot "03_onboard_hint_before"
    log_info "Скриншот ДО действий — ожидаем иконку-подсказку свайпа"

    # Небольшая пауза, подсказка должна уже быть видна
    sleep 2

    # --- Свайп: подсказка должна исчезнуть ---
    log_section "3.1 Свайп — подсказка должна исчезнуть"
    swipe 540 1300 540 900 300
    sleep 2
    screenshot "03_onboard_hint_after_swipe"
    log_info "Скриншот ПОСЛЕ первого свайпа — подсказка должна исчезнуть"

    # --- Проверка логов онбординга ---
    dump_logcat "onboarding"
    local hint_count
    hint_count=$(count_in_file "[Hh]int\|[Tt]utorial\|[Oo]nboard" "$RESULTS_DIR/_logcat_onboarding.txt")

    if [ "$hint_count" -gt 0 ]; then
        log_pass "Онбординг-логи присутствуют ($hint_count записей)"
        grep -iE "hint|tutorial|onboard" "$RESULTS_DIR/_logcat_onboarding.txt" 2>/dev/null | tail -5 | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет логов hint/tutorial/onboard — система онбординга не логирует или не реализована"
    fi

    # --- Движение к интерактивному объекту — подсказка тапа ---
    log_section "3.2 Подход к объекту — подсказка тапа"
    swipe_direction up
    sleep 1
    swipe_direction up
    sleep 2
    screenshot "03_onboard_tap_hint"
    log_info "Скриншот у объекта — ожидаем подсказку тапа"

    # Тап по объекту — подсказка тапа должна исчезнуть
    tap "$TAP_OBJECT"
    sleep 2
    screenshot "03_onboard_tap_hint_gone"
    log_info "Скриншот после тапа — подсказка должна исчезнуть"

    # Визуальную проверку исчезновения подсказки делает человек по скриншотам
    log_warn "Визуальная проверка исчезновения подсказок требует ручного сравнения скриншотов"

    [ "$_test_failed" -eq 0 ]
}
