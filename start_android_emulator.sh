#!/bin/bash
# Запуск Android-эмулятора в headless режиме для тестирования
set -e

export ANDROID_HOME=/opt/android-sdk
export PATH=$PATH:$ANDROID_HOME/cmdline-tools/latest/bin:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator

AVD_NAME="zelda_test"
SYSTEM_IMAGE="system-images;android-30;google_apis;x86_64"

# Создать AVD если не существует
if ! avdmanager list avd 2>/dev/null | grep -q "$AVD_NAME"; then
    echo "Создаю AVD: $AVD_NAME..."
    echo "no" | avdmanager create avd \
        -n "$AVD_NAME" \
        -k "$SYSTEM_IMAGE" \
        -d "pixel_4" \
        --force
    echo "AVD создан."
fi

# Проверить что эмулятор не запущен
if adb devices 2>/dev/null | grep -q "emulator"; then
    echo "Эмулятор уже запущен."
    adb devices
    exit 0
fi

echo "Запускаю эмулятор (headless, swiftshader)..."
nohup emulator -avd "$AVD_NAME" \
    -no-window \
    -no-audio \
    -no-boot-anim \
    -no-snapshot \
    -gpu swiftshader_indirect \
    -memory 1024 \
    -cores 2 \
    -partition-size 4096 \
    > /tmp/emulator.log 2>&1 &

echo "PID: $!"
echo "Ожидаю загрузку..."

# Ждать до 120 сек
for i in $(seq 1 60); do
    if [ "$(adb shell getprop sys.boot_completed 2>/dev/null)" = "1" ]; then
        echo "Эмулятор готов! (${i}x2 сек)"
        adb devices
        exit 0
    fi
    sleep 2
done

echo "Таймаут 120 сек. Проверь /tmp/emulator.log"
exit 1
