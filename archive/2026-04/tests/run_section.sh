#!/bin/bash
# Запуск одной секции: ./run_section.sh 08  или  ./run_section.sh 08_combat
cd "$(dirname "$0")"
source lib/test_runner.sh
run_tests --only "$1" "${@:2}"
