# Scripts Architecture

## Namespaces
Каждая папка = namespace. Файлы внутри папки используют namespace папки.

| Папка | Namespace | Ответственность |
|-------|-----------|-----------------|
| Input/ | ZeldaDaughter.Input | Диспетчер жестов, свайп, тап, лонг-пресс |
| Combat/ | ZeldaDaughter.Combat | Урон, раны, оружие, AI врагов, нокаут |
| Inventory/ | ZeldaDaughter.Inventory | Инвентарь, предметы, вес, крафт, рецепты |
| Progression/ | ZeldaDaughter.Progression | Kenshi-прогрессия, характеристики, кривые роста |
| World/ | ZeldaDaughter.World | День/ночь, погода, вода, стихии, ресурсы, спавн |
| NPC/ | ZeldaDaughter.NPC | Диалоги, расписание, торговля, язык |
| Save/ | ZeldaDaughter.Save | Сериализация, автосейв, загрузка |
| UI/ | ZeldaDaughter.UI | Радиальное меню, инвентарь UI, карта, блокнот, реплики |
| Editor/ | ZeldaDaughter.Editor | Editor-скрипты: расстановка карты, генерация, инструменты |

## Правила

- Один класс = один файл (исключение: мелкие enum/struct рядом с основным классом)
- ScriptableObject для данных и конфигов (предметы, рецепты, NPC-расписания, кривые прогрессии)
- MonoBehaviour только для поведения на GameObject
- Системы общаются через C# events (`public static event Action<T>`), не прямые ссылки
- `[SerializeField] private` вместо `public` для Inspector
- Кэшировать GetComponent в Awake()
- Не использовать Find/FindObjectOfType в Update
- Все магические числа — в ScriptableObject или const
