using System;
using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Progression;

namespace ZeldaDaughter.Editor
{
    public class ProgressionDebugWindow : EditorWindow
    {
        private static readonly string[] TierNames = { "Новичок", "Ученик", "Умелый", "Мастер" };

        private PlayerStats _playerStats;
        private Vector2 _scroll;

        [MenuItem("ZeldaDaughter/Debug/Progression Stats")]
        public static void Open()
        {
            GetWindow<ProgressionDebugWindow>("Progression Stats").Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to debug progression", MessageType.Info);
                return;
            }

            // Ищем PlayerStats каждый кадр — дёшево, т.к. только в Play Mode
            if (_playerStats == null)
                _playerStats = FindObjectOfType<PlayerStats>();

            if (_playerStats == null)
            {
                EditorGUILayout.HelpBox("PlayerStats не найден в сцене", MessageType.Warning);
                return;
            }

            DrawButtons();
            EditorGUILayout.Space(4);
            DrawStats();
        }

        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reset All"))
                _playerStats.DebugResetAll();

            if (GUILayout.Button("Max All"))
                _playerStats.DebugMaxAll();

            if (GUILayout.Button("+10 to All"))
                _playerStats.DebugAddToAll(10f);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStats()
        {
            var config = _playerStats.Config;
            var statTypes = (StatType[])Enum.GetValues(typeof(StatType));

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var type in statTypes)
            {
                float value = _playerStats.GetStat(type);
                float maxValue = 100f;

                if (config != null)
                {
                    var curve = config.GetCurve(type);
                    if (curve != null) maxValue = curve.MaxValue;
                }

                int tier = _playerStats.GetTier(type);
                string tierName = tier >= 0 && tier < TierNames.Length ? TierNames[tier] : tier.ToString();

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(type.ToString(), EditorStyles.boldLabel, GUILayout.Width(130));
                EditorGUILayout.LabelField(tierName, GUILayout.Width(70));
                EditorGUILayout.LabelField($"{value:F1} / {maxValue:F0}", GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();

                float normalized = maxValue > 0f ? Mathf.Clamp01(value / maxValue) : 0f;
                var rect = GUILayoutUtility.GetRect(18, 10, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, normalized, string.Empty);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
