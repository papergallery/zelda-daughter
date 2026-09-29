#!/bin/bash
# Модуль 08: Боевая система (TESTING_GUIDE секция 8)
# Проверяем: агрессия врагов, удары, урон, раны, нокаут, лут

run_test_08_combat() {
    log_section "08 | Боевая система (секция 8)"

    if ! ensure_running; then
        log_fail "Приложение не запущено"
        return 1
    fi

    clear_logcat
    sleep 1

    # --- 8.1 Двигаться к зоне кабанов (серия свайпов вниз) ---
    log_section "8.1 Движение к зоне кабанов"
    for i in $(seq 1 5); do
        swipe 540 900 540 1500 300
        sleep 2
    done
    screenshot "08_combat_boar_area"

    # --- 8.2 Атака врага (3 тапа) ---
    log_section "8.2 Атака врага (тапы)"
    tap 540 900
    sleep 1
    tap 540 900
    sleep 1
    tap 540 900
    sleep "$WAIT_COMBAT"
    screenshot "08_combat_attack"

    # --- 8.3 Уклонение (свайп в сторону) ---
    log_section "8.3 Уклонение от атаки врага"
    swipe 540 1170 200 1170 200
    sleep 1
    screenshot "08_combat_dodge"

    # --- 8.4 Проверка боевых логов ---
    dump_logcat "combat"

    # EnemyAggro
    if check_zd_tag "Combat" 1 "$RESULTS_DIR/_logcat_combat.txt"; then
        local combat_count
        combat_count=$(count_in_file "\[ZD:Combat\]" "$RESULTS_DIR/_logcat_combat.txt")
        log_pass "[ZD:Combat] присутствует ($combat_count записей)"

        local aggro_count
        aggro_count=$(count_in_file "\[ZD:Combat\].*[Aa]ggro\|[Ee]nemy" "$RESULTS_DIR/_logcat_combat.txt")
        if [ "$aggro_count" -gt 0 ]; then
            log_pass "EnemyAggro зафиксирован ($aggro_count записей)"
        else
            log_warn "Нет EnemyAggro в [ZD:Combat] — враги не заагрились или вне зоны"
        fi

        local attack_count
        attack_count=$(count_in_file "\[ZD:Combat\].*[Aa]ttack\|[Hh]it" "$RESULTS_DIR/_logcat_combat.txt")
        if [ "$attack_count" -gt 0 ]; then
            log_pass "PlayerAttack зафиксирован ($attack_count записей)"
        else
            log_warn "Нет Attack в [ZD:Combat] — удары не зафиксированы или враг не достигнут"
        fi

        local damage_count
        damage_count=$(count_in_file "\[ZD:Combat\].*[Dd]amag\|[Hh]urt\|[Ww]ound" "$RESULTS_DIR/_logcat_combat.txt")
        if [ "$damage_count" -gt 0 ]; then
            log_pass "PlayerDamaged/WoundAdded зафиксирован ($damage_count записей)"
        else
            log_warn "Нет Damage/Wound в [ZD:Combat] — персонаж не получал урон или враги не достигли"
        fi

        get_zd_lines "Combat" 8 "$RESULTS_DIR/_logcat_combat.txt" | while read -r line; do
            log_info "$line"
        done
    else
        log_warn "Нет [ZD:Combat] — боевая система не залогирована или враги не найдены"
    fi

    # --- 8.5 Ещё атаки для добивания ---
    log_section "8.5 Серия атак (добить врага)"
    for i in $(seq 1 5); do
        tap 540 900
        sleep 0.5
    done
    sleep 2
    screenshot "08_combat_kill"

    dump_logcat "combat_kill"
    local death_count
    death_count=$(count_in_file "\[ZD:Combat\].*[Dd]ead\|[Dd]eath\|[Kk]ill" "$RESULTS_DIR/_logcat_combat_kill.txt")
    if [ "$death_count" -gt 0 ]; then
        log_pass "Враг убит ($death_count записей [ZD:Combat] Kill/Dead)"
    else
        log_warn "Нет логов смерти врага — враг ещё жив или логи смерти не внедрены"
    fi

    # --- 8.6 Тап по туше (лут) ---
    log_section "8.6 Тап по туше врага (лут)"
    tap 540 900
    sleep 2
    screenshot "08_combat_loot"

    dump_logcat "combat_loot"
    local loot_count
    loot_count=$(count_in_file "\[ZD:Interact\].*[Ll]oot\|[Cc]orpse\|[Bb]ody" "$RESULTS_DIR/_logcat_combat_loot.txt")
    if [ "$loot_count" -gt 0 ]; then
        log_pass "Лут с тушки ($loot_count записей [ZD:Interact])"
    else
        log_warn "Нет логов лута — туша не найдена или лут не реализован"
    fi

    [ "$_test_failed" -eq 0 ]
}
