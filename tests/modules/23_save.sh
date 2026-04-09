#!/bin/bash
# Модуль 23: Сохранение (TESTING_GUIDE секция 23)
# Проверяем: автосейв при сворачивании, полный перезапуск, сохранение прогресса

run_test_23_save() {
    log_section "23 | Сохранение (секция 23)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 23.1 Скриншот текущего состояния до сохранения ---
    screenshot "23_save_before"
    log_info "Скриншот ДО сохранения сохранён"

    # --- 23.2 Свернуть приложение (OnApplicationPause → автосейв) ---
    log_section "23.2 Свернуть приложение (автосейв OnApplicationPause)"
    adb shell input keyevent KEYCODE_HOME  # KEYCODE_HOME отсутствует в test_utils.sh, вызов напрямую из TESTING_GUIDE
    sleep 5

    # Вернуться в приложение
    launch_app
    sleep 3
    screenshot "23_save_after_pause"

    dump_logcat "save_pause"
    local save_pause_count
    save_pause_count=$(count_in_file "\[ZD:Save\]" "$RESULTS_DIR/_logcat_save_pause.txt")
    if [ "$save_pause_count" -gt 0 ]; then
        log_pass "Автосейв при сворачивании ($save_pause_count записей [ZD:Save])"
        get_zd_lines "Save" 5 "$RESULTS_DIR/_logcat_save_pause.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет [ZD:Save] при сворачивании — AutoSave не залогирован или не реализован"
    fi

    # Проверка что приложение живо после возврата
    if is_app_focused; then
        log_pass "Приложение восстановилось после сворачивания"
    else
        log_fail "Приложение не на переднем плане после возврата — возможно краш"
        return 1
    fi

    # --- 23.3 Полное убийство и перезапуск ---
    log_section "23.3 Убить приложение и перезапустить"
    clear_logcat
    kill_app

    log_info "Перезапуск приложения..."
    launch_app
    sleep "$WAIT_SCENE_LOAD"
    screenshot "23_save_after_kill"

    # --- 23.4 Проверка загрузки сейва ---
    dump_logcat "save_load"
    local load_count
    load_count=$(count_in_file "\[ZD:Save\].*[Ll]oad\|[Rr]ead\|[Rr]estor" "$RESULTS_DIR/_logcat_save_load.txt")
    if [ "$load_count" -gt 0 ]; then
        log_pass "Сейв загружен после перезапуска ($load_count записей [ZD:Save])"
        get_zd_lines "Save" 8 "$RESULTS_DIR/_logcat_save_load.txt" | while read -r line; do
            log_info "$line"
        done
    else
        local save_any
        save_any=$(count_in_file "\[ZD:Save\]" "$RESULTS_DIR/_logcat_save_load.txt")
        if [ "$save_any" -gt 0 ]; then
            log_warn "[ZD:Save] есть ($save_any), но Load не зафиксирован — другой формат лога"
        else
            log_warn "Нет [ZD:Save] после перезапуска — Save/Load не реализован или не залогирован"
        fi
    fi

    # --- 23.5 Проверка что приложение жив ---
    if is_app_focused; then
        log_pass "Приложение живо после перезапуска"
    else
        log_fail "Приложение не на переднем плане после перезапуска"
    fi

    # Проверка ошибок
    if check_no_errors "$RESULTS_DIR/_logcat_save_load.txt"; then
        log_pass "Нет ошибок после перезапуска"
    else
        local err_count
        err_count=$(count_in_file "Exception\|NullRef\|CRASH\|Fatal" "$RESULTS_DIR/_logcat_save_load.txt")
        log_fail "Найдено $err_count ошибок после перезапуска"
    fi

    [ "$_test_failed" -eq 0 ]
}
