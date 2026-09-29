# Инфраструктура сервера

## Сервер
- **OS:** Ubuntu 24.04 LTS, x86_64
- **CPU:** 4x AMD EPYC 9354 32-Core (KVM)
- **RAM:** 7.8 GB (без swap)
- **Disk:** 58 GB total (LVM), ~18 GB free
- **GPU:** нет аппаратного ускорения
- **KVM:** доступен (/dev/kvm) — поддержка Android-эмулятора
- **Display:** нет физического дисплея, используется Xvfb

## Unity

### Установка
- **Версия:** Unity 2022.3.30f1
- **Путь:** `/opt/unity/Editor/Unity`
- **Установлен из:** https://download.unity3d.com/download_unity/70558241b701/LinuxEditorInstaller/Unity.tar.xz
- **Модули:** Linux Standalone + Android Build Support (из Mac .pkg, распакован через 7z+cpio)
- **Android SDK:** `/opt/android-sdk` (platform-tools, build-tools;33.0.1, platforms;android-33)
- **Android NDK:** `/opt/android-sdk/ndk/23.1.7779620` (r23b)
- **JDK:** OpenJDK 11 + 17 (cmdline-tools 8.0 используют JDK 11, sdkmanager новый — JDK 17)
- **Важно:** cmdline-tools/latest = версия 8.0 (совместима с JDK 11, которую использует Unity)

### Лицензия
- **Тип:** Personal (бесплатная)
- **Активация выполнена через Licensing Client:**
```bash
/opt/unity/Editor/Data/Resources/Licensing/Client/Unity.Licensing.Client \
  --activate-all \
  --include-personal \
  --username "EMAIL" \
  --password "PASSWORD"
```
- **Файл лицензии:** `~/.config/unity3d/Unity/licenses/` (UnityEntitlementLicense.xml)
- **Важно:** `-batchmode -nographics` требует Pro лицензию. Для Personal нужен Xvfb.

### Запуск Unity на этом сервере

Unity Personal НЕ поддерживает `-nographics`. Всегда запускать через Xvfb:

```bash
# Компиляция проекта
xvfb-run --auto-servernum --server-args="-screen 0 1024x768x24" \
  /opt/unity/Editor/Unity -batchmode \
  -projectPath "/var/www/html/Zelda's daughter/UnityProject" \
  -quit -logFile -

# Запуск тестов (EditMode)
xvfb-run --auto-servernum --server-args="-screen 0 1024x768x24" \
  /opt/unity/Editor/Unity -batchmode \
  -projectPath "/var/www/html/Zelda's daughter/UnityProject" \
  -runTests -testPlatform EditMode \
  -quit -logFile -

# Выполнение конкретного C# метода
xvfb-run --auto-servernum --server-args="-screen 0 1024x768x24" \
  /opt/unity/Editor/Unity -batchmode \
  -projectPath "/var/www/html/Zelda's daughter/UnityProject" \
  -executeMethod MyClass.MyMethod \
  -quit -logFile -

# Билд (когда будут модули)
xvfb-run --auto-servernum --server-args="-screen 0 1024x768x24" \
  /opt/unity/Editor/Unity -batchmode \
  -projectPath "/var/www/html/Zelda's daughter/UnityProject" \
  -buildTarget Android \
  -executeMethod BuildScript.Build \
  -quit -logFile -
```

### Swap
Swap отключён — 7.8 GB RAM достаточно для Unity и Android-эмулятора.
При необходимости можно создать:
```bash
sudo fallocate -l 4G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile
```

## Unity-проект

- **Путь:** `/var/www/html/Zelda's daughter/UnityProject/`
- **Рендер-пайплайн:** URP 14.0.11

### Установленные пакеты
| Пакет | Версия | Зачем |
|---|---|---|
| Universal RP | 14.0.11 | Рендер-пайплайн для мобильных |
| Input System | 1.7.0 | Новая система ввода (тач, свайп) |
| Test Framework | 1.3.9 | EditMode/PlayMode тесты |
| Cinemachine | 2.9.7 | Камера следования за персонажем |
| TextMeshPro | 3.0.6 | Текст (реплики, диалоги) |

### Unity MCP
- **НЕ установлен на сервере** (git URL не резолвится в batch mode)
- Устанавливать на машине с GUI: `Window > Package Manager > + > Add package from git URL`
- URL: `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`
- Репозиторий склонирован в `/tmp/unity-mcp/` для справки

### Структура папок
```
UnityProject/
├── Assets/
│   ├── Scripts/
│   │   ├── Input/          # ZeldaDaughter.Input
│   │   ├── Combat/         # ZeldaDaughter.Combat
│   │   ├── Inventory/      # ZeldaDaughter.Inventory
│   │   ├── Progression/    # ZeldaDaughter.Progression
│   │   ├── World/          # ZeldaDaughter.World
│   │   ├── NPC/            # ZeldaDaughter.NPC
│   │   ├── Save/           # ZeldaDaughter.Save
│   │   ├── UI/             # ZeldaDaughter.UI
│   │   └── Editor/         # ZeldaDaughter.Editor
│   ├── Models/
│   ├── Animations/
│   ├── Audio/ (SFX, Ambient, Music)
│   ├── UI/
│   ├── Prefabs/
│   ├── Scenes/
│   ├── Materials/
│   ├── ScriptableObjects/
│   └── Content/ (Replicas, Dialogues, Quests, Items, Configs)
├── Library/                 # автогенерация, в .gitignore
├── Logs/                    # в .gitignore
├── Packages/
├── ProjectSettings/
└── UserSettings/            # в .gitignore
```

## Инструменты

| Инструмент | Версия | Путь |
|---|---|---|
| Unity Editor | 2022.3.30f1 | /opt/unity/Editor/Unity |
| Git | 2.43.0 | /usr/bin/git |
| Python | 3.12.3 | /usr/bin/python3 |
| uv | 0.11.4 | ~/.local/bin/uv |
| Xvfb | 21.1.12 | /usr/bin/xvfb-run |
| Blender | 4.0.2 | /usr/bin/blender |
| Android SDK | cmdline-tools 8.0 | /opt/android-sdk |
| Android NDK | r23b | /opt/android-sdk/ndk/23.1.7779620 |
| OpenJDK 11 | 11.0.30 | /usr/lib/jvm/java-11-openjdk-amd64 |
| OpenJDK 17 | 17.x | /usr/lib/jvm/java-17-openjdk-amd64 |
| 7z | - | /usr/bin/7z |

## MCP-серверы

Определены в `.mcp.json`:
| Сервер | Назначение | Статус |
|---|---|---|
| unity-mcp | Управление Unity Editor (268 tools) | Только с GUI |
| android-mcp | Управление Android-эмулятором (25 tools) | Работает на VPS |

## Агенты Claude Code

Определены в `.claude/agents/`:
| Агент | Модель | Файл |
|---|---|---|
| architect | Opus | .claude/agents/architect.md |
| code-writer | Sonnet | .claude/agents/code-writer.md |
| level-designer | Sonnet | .claude/agents/level-designer.md |
| qa-tester | Haiku | .claude/agents/qa-tester.md |
| content-writer | Opus | .claude/agents/content-writer.md |

## Схема работы

```
Этот сервер (VPS)                    Другая машина (с GUI)
├── Агенты пишут C# код              Git pull
├── Unity batch mode (Xvfb)          Unity Editor с GUI
│   ├── Компиляция                   Визуальная проверка сцен
│   ├── EditMode тесты               PlayMode тесты
│   ├── Выполнение Editor-скриптов   Тестирование на устройстве
│   └── Android APK билд (headless)
├── Android-эмулятор (zelda_test)
│   ├── Установка APK (adb install)
│   ├── Smoke-тест (скриншоты + logcat)
│   ├── Полный прогон TESTING_GUIDE
│   └── Debug-логи [ZD:*] для верификации
├── Git push                         Git push
└── Конфиги, контент, документация
```

## Android-эмулятор

- **KVM:** доступен, hardware acceleration работает
- **SDK:** emulator + system-images;android-30;google_apis;x86_64
- **AVD:** zelda_test (Pixel 4)
- **RAM:** 7.8 GB — достаточно для Unity + Android-эмулятора одновременно
- **MCP:** `npx -y android-mcp-server` (martingeidobler) — 25 tools для Claude Code

### Команды
```bash
export PATH=$PATH:/opt/android-sdk/platform-tools:/opt/android-sdk/emulator
./start_android_emulator.sh  # запуск эмулятора
./test_on_emulator.sh        # установка APK + скриншот
./smoke_test.sh              # автоматический smoke-тест
adb emu kill                 # остановка
```

### Debug-логи [ZD:*]
Система структурированных логов для автоматической верификации (только в Development-билдах):
```bash
adb logcat -s Unity | grep "\[ZD:"           # все debug-логи
adb logcat -s Unity | grep "\[ZD:Combat\]"   # только бой
adb logcat -s Unity | grep "\[ZD:Perf\]"     # FPS и память
```
Категории: `Input`, `Move`, `Interact`, `Inventory`, `Combat`, `Progression`, `Save`, `Scene`, `Perf`.
Код: `Assets/Scripts/Debug/` (namespace `ZeldaDaughter.Debugging`).

## Известные ограничения

1. **Диск:** ~18 GB свободно — следить за местом при билдах
2. **GPU:** нет аппаратного ускорения — только логика и компиляция, рендеринг через software
3. **Personal лицензия:** не поддерживает `-nographics`, обязательно использовать Xvfb
