#!/bin/bash
# Константы для тестового фреймворка — координаты, пакет, тайминги
# Источник: TESTING_GUIDE.md, эмулятор Pixel 4

# --- Android ---
PACKAGE="com.papergallery.zeldasdaughter"
ACTIVITY="com.unity3d.player.UnityPlayerActivity"

# --- Экран Pixel 4: 1080x2340 ---
SCREEN_W=1080
SCREEN_H=2340
CENTER_X=$((SCREEN_W / 2))     # 540
CENTER_Y=$((SCREEN_H / 2))     # 1170
TOP_Y=600
BOTTOM_Y=1800

# --- Тайминги (секунды) ---
WAIT_LAUNCH=10        # после запуска приложения
WAIT_ACTION=1         # после тапа/свайпа
WAIT_CRAFT=2          # после крафта
WAIT_COMBAT=3         # после начала боя
WAIT_DIALOG=2         # после начала диалога
WAIT_SCENE_LOAD=8     # после перезапуска приложения
WAIT_SAVE=3           # после сохранения

# --- Свайпы (из TESTING_GUIDE) ---
# Формат: X1 Y1 X2 Y2 DURATION_MS
SWIPE_UP="540 1400 540 800 300"
SWIPE_DOWN="540 800 540 1400 300"
SWIPE_LEFT="700 1170 300 1170 300"
SWIPE_RIGHT="300 1170 700 1170 300"
SWIPE_UP_LONG="540 1500 540 500 300"     # бег
SWIPE_UP_SHORT="540 1300 540 1000 300"   # ходьба

# --- Координаты зон (приблизительные) ---
# Эти координаты зависят от расположения объектов в сцене и могут требовать калибровки
TAP_OBJECT="540 1000"        # объект перед персонажем
TAP_TREE="300 800"           # дерево сбоку
TAP_NPC="540 900"            # NPC перед персонажем

# --- Пути ---
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DEFAULT_APK="$PROJECT_DIR/UnityProject/Builds/Android/ZeldaDaughter.apk"
FALLBACK_APK="$PROJECT_DIR/ZeldaDaughter.apk"
