#!/bin/bash
# Модуль 21: Звук (TESTING_GUIDE секция 21)
# ОГРАНИЧЕНИЕ: звук нельзя услышать на headless-эмуляторе.
# Проверяем только через логи: AudioSource, ошибки загрузки звуков.

run_test_21_sound() {
    log_section "21 | Звук (секция 21)"

    log_warn "ОГРАНИЧЕНИЕ: полная проверка звука требует реального устройства"
    log_warn "На headless-эмуляторе проверяем только через логи Unity"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    # --- 21.1 Проверка AudioSource через логи ---
    log_section "21.1 Проверка загрузки AudioSource"
    dump_logcat "sound"

    local audio_count
    audio_count=$(count_in_file "[Aa]udio\|[Ss]ound\|[Mm]usic\|AudioSource\|AudioClip" "$RESULTS_DIR/_logcat_sound.txt")
    if [ "$audio_count" -gt 0 ]; then
        log_pass "Аудио-компоненты упоминаются в логах ($audio_count записей)"
        grep -iE "audio|sound|music" "$RESULTS_DIR/_logcat_sound.txt" 2>/dev/null | head -5 | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет упоминания аудио в логах — звуковая система не инициализирована или не залогирована"
    fi

    # --- 21.2 Проверка ошибок загрузки звуков ---
    log_section "21.2 Ошибки загрузки звуковых файлов"
    local audio_errors
    audio_errors=$(count_in_file "[Aa]udio.*[Ee]rror\|[Ss]ound.*[Ff]ail\|[Cc]lip.*null\|AudioClip.*[Mm]issing" "$RESULTS_DIR/_logcat_sound.txt")
    if [ "$audio_errors" -eq 0 ]; then
        log_pass "Нет ошибок загрузки звуковых файлов"
    else
        log_fail "Найдено $audio_errors ошибок загрузки аудио"
        grep -iE "audio.*error|sound.*fail|clip.*null" "$RESULTS_DIR/_logcat_sound.txt" 2>/dev/null | head -5 | while read -r line; do
            log_info "$line"
        done
    fi

    # --- 21.3 Проверка 3D-звука и шагов ---
    log_section "21.3 Шаги и ambient (через логи)"
    # Делаем пару движений для провокации звуков
    swipe_direction up
    sleep 1
    swipe_direction up
    sleep 1

    dump_logcat "sound_footstep"
    local footstep_count
    footstep_count=$(count_in_file "[Ff]ootstep\|[Ff]oot.*[Ss]tep\|[Ww]alk.*[Ss]ound\|[Aa]mbient" "$RESULTS_DIR/_logcat_sound_footstep.txt")
    if [ "$footstep_count" -gt 0 ]; then
        log_pass "Шаги/ambient упоминаются в логах ($footstep_count записей)"
    else
        log_warn "Нет логов шагов/ambient — звуки шагов не залогированы (обычное поведение)"
    fi

    log_warn "Для проверки громкости, 3D-затухания и микширования — нужно реальное устройство"

    [ "$_test_failed" -eq 0 ]
}
