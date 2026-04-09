#!/bin/bash
# Test Runner — запуск модулей, сбор результатов, генерация отчёта
# Использование: source test_runner.sh && run_tests [--all|--from N|--only N,N|--critical]

TESTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$TESTS_DIR/lib/test_constants.sh"
source "$TESTS_DIR/lib/test_utils.sh"

export ANDROID_HOME=/opt/android-sdk
export PATH=$PATH:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator

# Оптимальный порядок прогона (из TESTING_GUIDE секция 22)
ALL_MODULES=(
    00_smoke
    01_launch
    02_movement
    03_onboarding
    04_interaction
    05_peasant
    06_inventory
    07_crafting
    12_water
    08_combat
    09_food
    10_progression
    11_road_to_town
    13_town_npc
    14_trading
    15_smithy
    16_quests
    17_map
    20_training
    22_sleep
    23_save
    24_respawn
    21_sound
)

# Критический чеклист (из конца TESTING_GUIDE)
CRITICAL_MODULES=(00_smoke 01_launch 02_movement 04_interaction 06_inventory 08_combat 23_save)

# Фоновые модули (собирают данные весь прогон)
BACKGROUND_MODULES=(18_daynight 19_weather 25_performance)

# --- Результаты по модулям ---
declare -A MODULE_RESULTS   # "pass fail warn skip"
declare -A MODULE_TIMES     # секунды
TOTAL_START=0

run_module() {
    local module_name="$1"
    local module_file="$TESTS_DIR/modules/${module_name}.sh"

    if [ ! -f "$module_file" ]; then
        log_warn "Модуль не найден: $module_file"
        MODULE_RESULTS["$module_name"]="0 0 0 1"
        MODULE_TIMES["$module_name"]="0"
        return 2
    fi

    reset_counters
    local start_time=$SECONDS

    echo ""
    echo "========================================"
    echo " Модуль: $module_name"
    echo "========================================"

    # Запускаем модуль в subshell-подобном режиме (source, но ловим ошибки)
    local exit_code=0
    (
        source "$module_file"
        "run_test_${module_name}" 2>&1
    ) || exit_code=$?

    # Для корректных счётчиков — source напрямую (subshell не передаёт переменные)
    reset_counters
    source "$module_file"
    "run_test_${module_name}" 2>&1 || exit_code=$?

    local elapsed=$(( SECONDS - start_time ))
    MODULE_RESULTS["$module_name"]="$_test_passed $_test_failed $_test_warnings $_test_skipped"
    MODULE_TIMES["$module_name"]="$elapsed"

    # Итог модуля
    if [ "$_test_failed" -gt 0 ]; then
        echo -e "\n${RED}=> $module_name: FAIL${NC} (pass=$_test_passed fail=$_test_failed warn=$_test_warnings, ${elapsed}s)"
    elif [ "$_test_skipped" -gt 0 ] && [ "$_test_passed" -eq 0 ]; then
        echo -e "\n${CYAN}=> $module_name: SKIP${NC} (${elapsed}s)"
    else
        echo -e "\n${GREEN}=> $module_name: PASS${NC} (pass=$_test_passed warn=$_test_warnings, ${elapsed}s)"
    fi

    return $exit_code
}

generate_report() {
    local report_file="$RESULTS_DIR/report.txt"
    local total_pass=0 total_fail=0 total_warn=0 total_skip=0
    local elapsed=$(( SECONDS - TOTAL_START ))

    {
        echo "=== ZELDA'S DAUGHTER TEST REPORT ==="
        echo "Date: $(date '+%Y-%m-%d %H:%M:%S')"
        echo "Build: $(basename "${APK_PATH:-unknown}")"
        echo "Emulator: zelda_test (Pixel 4, Android 11)"
        echo "Duration: $((elapsed / 60)) min $((elapsed % 60)) sec"
        echo ""
        echo "=== SECTIONS ==="

        for module in "${RUN_MODULES[@]}"; do
            local result="${MODULE_RESULTS[$module]:-0 0 0 0}"
            local time="${MODULE_TIMES[$module]:-0}"
            read -r p f w s <<< "$result"
            total_pass=$((total_pass + p))
            total_fail=$((total_fail + f))
            total_warn=$((total_warn + w))
            total_skip=$((total_skip + s))
            local total=$((p + f + w + s))

            local status="PASS"
            [ "$f" -gt 0 ] && status="FAIL"
            [ "$s" -gt 0 ] && [ "$p" -eq 0 ] && [ "$f" -eq 0 ] && status="SKIP"

            printf "[%-4s] %-25s %d/%d  (%ds)\n" "$status" "$module" "$p" "$total" "$time"
            [ "$f" -gt 0 ] && printf "       ^ %d failures\n" "$f"
        done

        local grand_total=$((total_pass + total_fail + total_warn + total_skip))
        echo ""
        echo "=== SUMMARY ==="
        echo "TOTAL:    $grand_total checks"
        echo "PASSED:   $total_pass"
        echo "FAILED:   $total_fail"
        echo "WARNINGS: $total_warn"
        echo "SKIPPED:  $total_skip"
        echo ""
        echo "=== PERFORMANCE ==="
        echo "Memory: $(get_memory_mb)MB"
        local fps
        fps=$(get_fps_from_logcat "$RESULTS_DIR/_logcat_tmp.txt" 2>/dev/null || echo "N/A")
        echo "FPS (last): $fps"
        echo ""
        echo "=== FILES ==="
        echo "Screenshots: $RESULTS_DIR/*.png"
        echo "Logcat:      $RESULTS_DIR/logcat_*.txt"
        echo "ZD logs:     $RESULTS_DIR/logcat_zd.txt"
    } > "$report_file"

    # Финальный logcat дамп
    adb logcat -d -s Unity > "$RESULTS_DIR/logcat_final.txt" 2>/dev/null || true
    grep "\[ZD:" "$RESULTS_DIR/logcat_final.txt" > "$RESULTS_DIR/logcat_zd.txt" 2>/dev/null || true

    # JSON-отчёт для qa-tester
    local json_file="$RESULTS_DIR/report.json"
    {
        echo "{"
        echo "  \"date\": \"$(date -Iseconds)\","
        echo "  \"build\": \"$(basename "${APK_PATH:-unknown}")\","
        echo "  \"duration_sec\": $elapsed,"
        echo "  \"total\": $grand_total, \"passed\": $total_pass, \"failed\": $total_fail,"
        echo "  \"warnings\": $total_warn, \"skipped\": $total_skip,"
        echo "  \"sections\": ["
        local first=true
        for module in "${RUN_MODULES[@]}"; do
            local result="${MODULE_RESULTS[$module]:-0 0 0 0}"
            local time="${MODULE_TIMES[$module]:-0}"
            read -r p f w s <<< "$result"
            local status="pass"
            [ "$f" -gt 0 ] && status="fail"
            [ "$s" -gt 0 ] && [ "$p" -eq 0 ] && [ "$f" -eq 0 ] && status="skip"
            $first || echo ","
            printf '    {"id": "%s", "status": "%s", "passed": %d, "failed": %d, "time_sec": %d}' \
                "$module" "$status" "$p" "$f" "$time"
            first=false
        done
        echo ""
        echo "  ]"
        echo "}"
    } > "$json_file"

    # Вывод в консоль
    echo ""
    cat "$report_file"
    echo ""
    echo "Отчёт сохранён: $report_file"
    echo "JSON:           $json_file"

    return $total_fail
}

run_tests() {
    local mode="$1"
    shift || true
    TOTAL_START=$SECONDS

    # Определить APK
    APK_PATH="${1:-$DEFAULT_APK}"
    [ ! -f "$APK_PATH" ] && APK_PATH="$FALLBACK_APK"

    # Определить RESULTS_DIR
    export RESULTS_DIR="$TESTS_DIR/reports/$(date +%Y%m%d_%H%M%S)"
    mkdir -p "$RESULTS_DIR"

    # Определить список модулей
    declare -a RUN_MODULES
    case "$mode" in
        --all)
            RUN_MODULES=("${ALL_MODULES[@]}")
            ;;
        --critical)
            RUN_MODULES=("${CRITICAL_MODULES[@]}")
            ;;
        --from)
            local from="$1"; shift || true
            local found=false
            for m in "${ALL_MODULES[@]}"; do
                if [[ "$m" == "$from"* ]] || $found; then
                    found=true
                    RUN_MODULES+=("$m")
                fi
            done
            if [ ${#RUN_MODULES[@]} -eq 0 ]; then
                echo "Модуль не найден: $from"
                return 1
            fi
            ;;
        --only)
            IFS=',' read -ra RUN_MODULES <<< "$1"; shift || true
            ;;
        *)
            # По умолчанию — все
            RUN_MODULES=("${ALL_MODULES[@]}")
            ;;
    esac

    echo "======================================"
    echo " Test Runner — Zelda's Daughter"
    echo " Модули: ${#RUN_MODULES[@]}"
    echo " Результаты: $RESULTS_DIR"
    echo "======================================"

    # Проверка эмулятора
    if ! adb devices 2>/dev/null | grep -q "emulator"; then
        log_info "Эмулятор не запущен, запускаю..."
        bash "$PROJECT_DIR/start_android_emulator.sh"
    fi

    # Запуск модулей
    for module in "${RUN_MODULES[@]}"; do
        run_module "$module" || true   # продолжаем даже при ошибке
    done

    # Отчёт
    generate_report
}
