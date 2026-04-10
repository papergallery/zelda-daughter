#!/bin/bash
# Полный цикл тестирования на Android-эмуляторе
set -e

export ANDROID_HOME=/opt/android-sdk
export PATH=$PATH:$ANDROID_HOME/cmdline-tools/latest/bin:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
APK_PATH="$PROJECT_DIR/ZeldaDaughter.apk"
PACKAGE="com.papergallery.zeldasdaughter"
SCREENSHOTS_DIR="$PROJECT_DIR/screenshots"

mkdir -p "$SCREENSHOTS_DIR"

# 1. Проверить эмулятор
if ! adb devices 2>/dev/null | grep -q "emulator"; then
    echo "[!] Эмулятор не запущен. Запускаю..."
    bash "$PROJECT_DIR/start_android_emulator.sh"
fi

# 2. Проверить APK
if [ ! -f "$APK_PATH" ]; then
    echo "[!] APK не найден: $APK_PATH"
    echo "    Запусти сборку сначала."
    exit 1
fi

# 3. Установить APK
echo "[*] Устанавливаю APK..."
adb install -r "$APK_PATH" 2>&1 | tail -1

# 4. Запустить игру
echo "[*] Запускаю игру..."
adb shell am start -n "$PACKAGE/com.unity3d.player.UnityPlayerActivity"
sleep 5

# 5. Скриншот
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
adb exec-out screencap -p > "$SCREENSHOTS_DIR/launch_$TIMESTAMP.png"
echo "[✓] Скриншот: $SCREENSHOTS_DIR/launch_$TIMESTAMP.png"

# 6. Логи Unity
echo "[*] Последние логи Unity:"
adb logcat -d -s Unity | tail -20

echo ""
echo "=== Полезные команды ==="
echo "Скриншот:     adb exec-out screencap -p > screenshot.png"
echo "Тап:          adb shell input tap X Y"
echo "Свайп:        adb shell input swipe X1 Y1 X2 Y2 duration_ms"
echo "Логи Unity:   adb logcat -s Unity"
echo "Стоп игры:    adb shell am force-stop $PACKAGE"
echo "Перезапуск:   adb shell am start -n $PACKAGE/com.unity3d.player.UnityPlayerActivity"
