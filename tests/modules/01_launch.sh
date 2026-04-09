#!/bin/bash
# Модуль 01: Запуск и первые секунды (TESTING_GUIDE секция 1)
# Проверяем: запуск без краша, ориентация, шейдеры, загрузка сцены

run_test_01_launch() {
    log_section "01 | Запуск и первые секунды (секция 1)"

    if ! ensure_running; then
        log_fail "Приложение не запускается"
        return 1
    fi

    # Дать время на полную загрузку
    sleep "$WAIT_LAUNCH"
    screenshot "01_launch_initial"

    # --- Проверка ориентации ---
    if check_portrait; then
        log_pass "Ориентация Portrait (rotation=0)"
    else
        log_fail "Неверная ориентация — не portrait"
    fi

    # --- Проверка шейдеров (розовый экран) ---
    local pink_result
    pink_result=$(check_pink "$RESULTS_DIR/01_launch_initial.png")
    case "$pink_result" in
        "OK")       log_pass "Шейдеры URP корректны — нет розового экрана" ;;
        "PINK")     log_fail "Розовый экран (>80%) — URP шейдеры не включены" ;;
        "PARTIAL")  log_fail "Частично розовый — часть URP шейдеров отсутствует" ;;
        "NO_PIL")   log_warn "PIL не установлен — визуальная проверка пропущена" ;;
        *)          log_warn "Ошибка анализа изображения: $pink_result" ;;
    esac

    # --- Проверка крашей ---
    dump_logcat "launch_check"
    if check_no_errors "$RESULTS_DIR/_logcat_launch_check.txt"; then
        log_pass "Нет Exception/NullRef/CRASH после запуска"
    else
        local err_count
        err_count=$(count_in_file "Exception\|NullRef\|CRASH\|Fatal" "$RESULTS_DIR/_logcat_launch_check.txt")
        log_fail "Найдено $err_count ошибок в logcat после запуска"
        get_zd_lines "Scene" 5 "$RESULTS_DIR/_logcat_launch_check.txt" | while read -r line; do
            log_info "$line"
        done
    fi

    # --- Проверка загрузки сцены [ZD:Scene] ---
    if check_zd_tag "Scene" 1 "$RESULTS_DIR/_logcat_launch_check.txt"; then
        log_pass "Сцена загружена ([ZD:Scene] присутствует в логах)"
        get_zd_lines "Scene" 3 "$RESULTS_DIR/_logcat_launch_check.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет [ZD:Scene] в logcat — возможно Development Build не включён"
    fi

    # --- Скриншот через 3 сек для проверки что всё отрисовалось ---
    sleep 3
    screenshot "01_launch_stable"

    [ "$_test_failed" -eq 0 ]
}
