#!/bin/bash
# Полный прогон всех тестов из TESTING_GUIDE.md
# Использование: ./run_all.sh [--all|--critical|--from 08|--only 06,07] [path/to/apk]
cd "$(dirname "$0")"
source lib/test_runner.sh
run_tests "${1:---all}" "${@:2}"
