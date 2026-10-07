using System;
using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.NPC;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    public class OnboardingChecklist : EditorWindow
    {
        private struct ChecklistItem
        {
            public string Name;
            public string SearchTag;
            public Type ComponentType;
            public float MaxDistanceFromSpawn;
        }

        private static readonly ChecklistItem[] Items =
        {
            new() { Name = "Spawn Point",    SearchTag = "Respawn",  ComponentType = typeof(SpawnZoneMarker),   MaxDistanceFromSpawn = 0f   },
            new() { Name = "Pickable Item",  SearchTag = null,       ComponentType = typeof(Pickupable),        MaxDistanceFromSpawn = 50f  },
            new() { Name = "Resource Node",  SearchTag = null,       ComponentType = typeof(ResourceNode),      MaxDistanceFromSpawn = 60f  },
            new() { Name = "Crafting Fire",  SearchTag = null,       ComponentType = typeof(StationInteractable), MaxDistanceFromSpawn = 80f },
            new() { Name = "First Enemy",    SearchTag = null,       ComponentType = typeof(EnemyFSM),          MaxDistanceFromSpawn = 100f },
            new() { Name = "NPC",            SearchTag = null,       ComponentType = typeof(NPCInteractable),   MaxDistanceFromSpawn = 120f },
            new() { Name = "Shop",           SearchTag = null,       ComponentType = typeof(MerchantInventory), MaxDistanceFromSpawn = 150f },
        };

        private Vector2 _scroll;
        private Vector3 _spawnPosition;
        private bool _spawnFound;

        [MenuItem("ZeldaDaughter/QA/Onboarding Checklist")]
        public static void Open()
        {
            GetWindow<OnboardingChecklist>("Onboarding Checklist").Show();
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
            if (!IsSceneAvailable())
            {
                EditorGUILayout.HelpBox(
                    "Загрузите сцену или войдите в Play Mode для проверки.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Обновить", EditorStyles.toolbarButton, GUILayout.Width(80)))
                Repaint();
            EditorGUILayout.EndHorizontal();

            FindSpawnPoint();
            DrawSpawnInfo();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Контрольные точки онбординга", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var item in Items)
                DrawChecklistRow(item);

            EditorGUILayout.EndScrollView();
        }

        private void DrawSpawnInfo()
        {
            if (_spawnFound)
            {
                EditorGUILayout.HelpBox(
                    $"Точка спавна: {_spawnPosition.x:F1}, {_spawnPosition.y:F1}, {_spawnPosition.z:F1}",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Точка спавна не найдена (тег 'Respawn' или компонент SpawnZoneMarker).",
                    MessageType.Warning);
            }
        }

        private void DrawChecklistRow(ChecklistItem item)
        {
            var (found, go) = FindObject(item);

            float distance = -1f;
            if (found && go != null && _spawnFound && item.MaxDistanceFromSpawn > 0f)
                distance = Vector3.Distance(_spawnPosition, go.transform.position);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            // Статус
            var statusColor = found ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.3f, 0.3f);
            var prevColor = GUI.color;
            GUI.color = statusColor;
            EditorGUILayout.LabelField(found ? "✓" : "✗", GUILayout.Width(20));
            GUI.color = prevColor;

            EditorGUILayout.LabelField(item.Name, EditorStyles.boldLabel, GUILayout.Width(140));

            if (found && go != null)
            {
                EditorGUILayout.LabelField(go.name, GUILayout.Width(160));

                if (distance >= 0f)
                {
                    var distColor = distance <= item.MaxDistanceFromSpawn ? Color.white : new Color(1f, 0.6f, 0.2f);
                    GUI.color = distColor;
                    EditorGUILayout.LabelField($"{distance:F1}m", GUILayout.Width(55));
                    GUI.color = prevColor;
                }
                else
                {
                    EditorGUILayout.LabelField("—", GUILayout.Width(55));
                }

                if (GUILayout.Button("Select", GUILayout.Width(55)))
                    Selection.activeGameObject = go;
            }
            else
            {
                EditorGUILayout.LabelField("Не найден", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();

            // Предупреждение о дистанции
            if (found && distance > 0f && distance > item.MaxDistanceFromSpawn)
            {
                EditorGUILayout.HelpBox(
                    $"Слишком далеко от спавна: {distance:F1}m > {item.MaxDistanceFromSpawn:F1}m",
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(1);
        }

        private void FindSpawnPoint()
        {
            _spawnFound = false;

            // Сначала ищем по тегу "Respawn"
            var spawnGo = GameObject.FindWithTag("Respawn");
            if (spawnGo != null)
            {
                _spawnPosition = spawnGo.transform.position;
                _spawnFound = true;
                return;
            }

            // Затем ищем компонент SpawnZoneMarker
            var marker = FindObjectOfType<SpawnZoneMarker>();
            if (marker != null)
            {
                _spawnPosition = marker.transform.position;
                _spawnFound = true;
            }
        }

        private static (bool found, GameObject go) FindObject(ChecklistItem item)
        {
            if (item.ComponentType != null)
            {
                var component = FindObjectOfType(item.ComponentType) as Component;
                if (component != null)
                    return (true, component.gameObject);
            }

            if (!string.IsNullOrEmpty(item.SearchTag))
            {
                try
                {
                    var go = GameObject.FindWithTag(item.SearchTag);
                    if (go != null) return (true, go);
                }
                catch (UnityException)
                {
                    // Тег не зарегистрирован в проекте
                }
            }

            return (false, null);
        }

        private static bool IsSceneAvailable()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().isLoaded;
        }
    }
}
