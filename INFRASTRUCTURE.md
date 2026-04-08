# Инфраструктура сервера

## Сервер
- **OS:** Ubuntu 24.04 LTS, x86_64
- **CPU:** 2x Intel Xeon Silver 4416+
- **RAM:** 3.9 GB + 4 GB swap
- **Disk:** ~39 GB total, ~8.7 GB free
- **GPU:** QXL виртуальный (нет аппаратного ускорения)
- **Display:** нет физического дисплея, используется Xvfb

## Unity

### Установка
- **Версия:** Unity 2022.3.30f1
- **Путь:** `/opt/unity/Editor/Unity`
- **Установлен из:** https://download.unity3d.com/download_unity/70558241b701/LinuxEditorInstaller/Unity.tar.xz
- **Модули:** только Editor (Android/iOS Build Support НЕ установлены — ставить на другой машине)

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
```bash
# Создан вручную (не переживёт перезагрузку, если не добавить в fstab)
sudo fallocate -l 4G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile

# Для постоянного swap — добавить в /etc/fstab:
# /swapfile none swap sw 0 0
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
│   └── Выполнение Editor-скриптов   Билды Android/iOS
├── Git push                         Git push
└── Конфиги, контент, документация
```

## Известные ограничения

1. **RAM:** 3.9 GB + 4 GB swap — Unity работает медленно, возможны OOM при больших операциях
2. **Диск:** ~8.7 GB свободно — не хватит для Android/iOS модулей
3. **GPU:** виртуальный QXL — нет рендеринга, только логика и компиляция
4. **Personal лицензия:** не поддерживает `-nographics`, обязательно использовать Xvfb
5. **Swap не постоянный:** после перезагрузки нужно пересоздать или добавить в fstab
