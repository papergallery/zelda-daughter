using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.Progression;

namespace ZeldaDaughter.Editor
{
    public class BalanceEditorWindow : EditorWindow
    {
        private static readonly string[] TabNames = { "Прогрессия", "Враги", "Экономика" };

        private int _selectedTab;
        private Vector2 _scroll;

        // Вкладка "Прогрессия"
        private List<StatGrowthCurve> _growthCurves = new();
        private WeaponProficiencyData _weaponProfData;
        private int _simulationActions = 50;

        // Вкладка "Враги"
        private List<EnemyData> _enemies = new();

        // Вкладка "Экономика"
        private List<ItemData> _items = new();

        [MenuItem("ZeldaDaughter/Balance/Overview")]
        public static void Open()
        {
            GetWindow<BalanceEditorWindow>("Balance Overview").Show();
        }

        private void OnEnable()
        {
            ReloadAll();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Обновить", EditorStyles.toolbarButton, GUILayout.Width(80)))
                ReloadAll();
            EditorGUILayout.EndHorizontal();

            _selectedTab = GUILayout.Toolbar(_selectedTab, TabNames);
            EditorGUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            switch (_selectedTab)
            {
                case 0: DrawProgressionTab(); break;
                case 1: DrawEnemiesTab(); break;
                case 2: DrawEconomyTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // -------------------------------------------------------------------------
        // Вкладка "Прогрессия"
        // -------------------------------------------------------------------------

        private void DrawProgressionTab()
        {
            EditorGUILayout.LabelField("Кривые роста характеристик", EditorStyles.boldLabel);

            if (_growthCurves.Count == 0)
            {
                EditorGUILayout.HelpBox("Нет StatGrowthCurve в Assets/Data/Progression/", MessageType.Info);
            }
            else
            {
                foreach (var curve in _growthCurves)
                {
                    if (curve == null) continue;
                    DrawGrowthCurveRow(curve);
                }
            }

            EditorGUILayout.Space(8);
            DrawSimulationBlock();

            EditorGUILayout.Space(8);
            DrawWeaponProfBlock();
        }

        private void DrawGrowthCurveRow(StatGrowthCurve curve)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(curve.StatType.ToString(), EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField($"baseRate: {curve.BaseGrowthRate:F2}", GUILayout.Width(110));
            EditorGUILayout.LabelField($"max: {curve.MaxValue:F0}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"decay: {curve.DecayExponent:F2}", GUILayout.Width(80));
            EditorGUILayout.LabelField($"failMul: {curve.FailureMultiplier:F2}", GUILayout.Width(90));
            EditorGUILayout.LabelField($"victBonus: {curve.VictoryBonus:F1}", GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSimulationBlock()
        {
            EditorGUILayout.LabelField("Симуляция роста", EditorStyles.boldLabel);

            _simulationActions = EditorGUILayout.IntField("Количество действий (N):", _simulationActions);
            _simulationActions = Mathf.Max(1, _simulationActions);

            EditorGUILayout.Space(4);

            foreach (var curve in _growthCurves)
            {
                if (curve == null) continue;

                float value = 0f;
                for (int i = 0; i < _simulationActions; i++)
                    value = Mathf.Min(value + curve.CalculateGrowth(value, 1f), curve.MaxValue);

                float normalized = curve.MaxValue > 0f ? value / curve.MaxValue : 0f;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(curve.StatType.ToString(), GUILayout.Width(120));
                EditorGUILayout.LabelField($"→ {value:F1} / {curve.MaxValue:F0}  ({normalized * 100f:F1}%)",
                    GUILayout.Width(200));
                var rect = GUILayoutUtility.GetRect(18, 10, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, Mathf.Clamp01(normalized), string.Empty);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawWeaponProfBlock()
        {
            if (_weaponProfData == null) return;

            EditorGUILayout.LabelField("Мастерство оружия (WeaponProficiencyData)", EditorStyles.boldLabel);

            var weaponTypes = (WeaponType[])System.Enum.GetValues(typeof(WeaponType));
            foreach (var type in weaponTypes)
            {
                var entry = _weaponProfData.GetEntry(type);
                if (entry.MaxValue <= 0f) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(type.ToString(), EditorStyles.boldLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField($"baseRate: {entry.BaseGrowthRate:F2}", GUILayout.Width(110));
                EditorGUILayout.LabelField($"max: {entry.MaxValue:F0}", GUILayout.Width(70));
                EditorGUILayout.LabelField($"decay: {entry.DecayExponent:F2}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"failMul: {entry.FailureMultiplier:F2}", GUILayout.Width(90));
                EditorGUILayout.EndHorizontal();

                // Симуляция за N действий
                float value = 0f;
                for (int i = 0; i < _simulationActions; i++)
                    value = Mathf.Min(value + _weaponProfData.CalculateGrowth(type, value, 1f), entry.MaxValue);

                float normalized = value / entry.MaxValue;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"За {_simulationActions} ударов:", GUILayout.Width(130));
                EditorGUILayout.LabelField($"{value:F1} ({normalized * 100f:F1}%)", GUILayout.Width(120));
                var rect = GUILayoutUtility.GetRect(18, 10, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, Mathf.Clamp01(normalized), string.Empty);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }
        }

        // -------------------------------------------------------------------------
        // Вкладка "Враги"
        // -------------------------------------------------------------------------

        private void DrawEnemiesTab()
        {
            EditorGUILayout.LabelField("Данные врагов", EditorStyles.boldLabel);

            if (_enemies.Count == 0)
            {
                EditorGUILayout.HelpBox("EnemyData не найдены.", MessageType.Info);
                return;
            }

            // Шапка таблицы
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Имя", EditorStyles.boldLabel, GUILayout.Width(130));
            EditorGUILayout.LabelField("HP", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Урон", EditorStyles.boldLabel, GUILayout.Width(55));
            EditorGUILayout.LabelField("Скор. атаки", EditorStyles.boldLabel, GUILayout.Width(90));
            EditorGUILayout.LabelField("Ударов(str0)", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Ударов(str50)", EditorStyles.boldLabel, GUILayout.Width(105));
            EditorGUILayout.EndHorizontal();

            foreach (var enemy in _enemies)
            {
                if (enemy == null) continue;
                DrawEnemyRow(enemy);
            }
        }

        private void DrawEnemyRow(EnemyData enemy)
        {
            // Расчёт ударов для убийства: HP / (damage * damageMultiplier)
            // strength=0 → normalized=0 → mul=1.0
            // strength=50 → normalized=0.5 → mul = 1 + 0.5 * maxDamageBonus
            // Используем приблизительные значения без ссылки на StatEffectConfig
            float hitsAtStr0 = enemy.Damage > 0f ? Mathf.Ceil(enemy.MaxHP / enemy.Damage) : float.PositiveInfinity;
            // При Strength=50/100=0.5, bonusMultiplier=1 + 0.5*1.5=1.75 (стандартный maxDamageBonus)
            const float str50DamageMul = 1.75f;
            float hitsAtStr50 = enemy.Damage > 0f ? Mathf.Ceil(enemy.MaxHP / (enemy.Damage * str50DamageMul)) : float.PositiveInfinity;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(enemy.name, GUILayout.Width(130));
            EditorGUILayout.LabelField(enemy.MaxHP.ToString("F0"), GUILayout.Width(50));
            EditorGUILayout.LabelField(enemy.Damage.ToString("F1"), GUILayout.Width(55));
            EditorGUILayout.LabelField(enemy.AttackCooldown > 0f ? $"1/{enemy.AttackCooldown:F1}s" : "—", GUILayout.Width(90));
            EditorGUILayout.LabelField(float.IsInfinity(hitsAtStr0) ? "∞" : hitsAtStr0.ToString("F0"), GUILayout.Width(100));
            EditorGUILayout.LabelField(float.IsInfinity(hitsAtStr50) ? "∞" : hitsAtStr50.ToString("F0"), GUILayout.Width(105));
            if (GUILayout.Button("Ping", GUILayout.Width(40)))
                EditorGUIUtility.PingObject(enemy);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // -------------------------------------------------------------------------
        // Вкладка "Экономика"
        // -------------------------------------------------------------------------

        private void DrawEconomyTab()
        {
            EditorGUILayout.LabelField("Предметы с ценой", EditorStyles.boldLabel);

            if (_items.Count == 0)
            {
                EditorGUILayout.HelpBox("ItemData с BaseValue > 0 не найдены.", MessageType.Info);
                return;
            }

            DrawEconomyStats();
            EditorGUILayout.Space(4);

            // Шапка таблицы
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Имя", EditorStyles.boldLabel, GUILayout.Width(160));
            EditorGUILayout.LabelField("Цена", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Тип", EditorStyles.boldLabel, GUILayout.Width(90));
            EditorGUILayout.LabelField("Вес", EditorStyles.boldLabel, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            foreach (var item in _items)
            {
                if (item == null) continue;
                DrawItemRow(item);
            }
        }

        private void DrawEconomyStats()
        {
            int min = int.MaxValue, max = int.MinValue;
            long sum = 0;
            foreach (var item in _items)
            {
                if (item == null) continue;
                if (item.BaseValue < min) min = item.BaseValue;
                if (item.BaseValue > max) max = item.BaseValue;
                sum += item.BaseValue;
            }

            float avg = _items.Count > 0 ? (float)sum / _items.Count : 0f;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Всего предметов: {_items.Count}  |  " +
                $"Мин: {min}  |  Макс: {max}  |  Среднее: {avg:F1}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawItemRow(ItemData item)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(item.DisplayName, GUILayout.Width(160));
            EditorGUILayout.LabelField(item.BaseValue.ToString(), GUILayout.Width(60));
            EditorGUILayout.LabelField(item.ItemType.ToString(), GUILayout.Width(90));
            EditorGUILayout.LabelField(item.Weight.ToString("F2"), GUILayout.Width(50));
            if (GUILayout.Button("Ping", GUILayout.Width(40)))
                EditorGUIUtility.PingObject(item);
            EditorGUILayout.EndHorizontal();
        }

        // -------------------------------------------------------------------------
        // Загрузка данных
        // -------------------------------------------------------------------------

        private void ReloadAll()
        {
            LoadGrowthCurves();
            LoadWeaponProfData();
            LoadEnemies();
            LoadItems();
        }

        private void LoadGrowthCurves()
        {
            _growthCurves.Clear();
            var guids = AssetDatabase.FindAssets("t:StatGrowthCurve", new[] { "Assets/Data/Progression" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var curve = AssetDatabase.LoadAssetAtPath<StatGrowthCurve>(path);
                if (curve != null) _growthCurves.Add(curve);
            }
        }

        private void LoadWeaponProfData()
        {
            _weaponProfData = null;
            var guids = AssetDatabase.FindAssets("t:WeaponProficiencyData", new[] { "Assets" });
            if (guids.Length > 0)
                _weaponProfData = AssetDatabase.LoadAssetAtPath<WeaponProficiencyData>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private void LoadEnemies()
        {
            _enemies.Clear();
            var guids = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                if (enemy != null) _enemies.Add(enemy);
            }
        }

        private void LoadItems()
        {
            _items.Clear();
            var guids = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null && item.BaseValue > 0) _items.Add(item);
            }
        }
    }
}
