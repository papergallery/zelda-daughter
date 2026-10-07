using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Audio;
using ZeldaDaughter.NPC;

namespace ZeldaDaughter.Editor
{
    public static class AudioZoneBuilder
    {
        private const float CityAmbiencePadding = 5f;

        // Building name → sound type label (used in log; actual AudioClip must be assigned manually)
        private static readonly (string[] Keywords, string SoundLabel, float StartHour, float EndHour)[] BuildingSoundRules =
        {
            (new[] { "blacksmith", "forge", "кузница", "smithy", "anvil" }, "HammerLoop",  6f, 20f),
            (new[] { "tavern", "inn", "таверна", "pub",  "bar"  },          "TavernNoise", 10f, 24f),
            (new[] { "market", "рынок", "stall",  "shop", "bazaar" },       "CrowdLoop",    8f, 18f),
        };

        [MenuItem("ZeldaDaughter/Setup/Create Audio Zones for Town")]
        public static void CreateAudioZonesForTown()
        {
            var stats = new Stats();

            // ── 1. CityAmbienceZone охватывает все здания ──────────────────────
            CreateCityAmbienceZone(ref stats);

            // ── 2. PointSoundEmitter на здания по именам ───────────────────────
            AddPointEmittersToBuildings(ref stats);

            // ── 3. BardPerformer на NPC с ролью Bard ──────────────────────────
            AddBardPerformers(ref stats);

            Debug.Log("[AudioZoneBuilder] Done. Results:");
            Debug.Log($"  CityAmbienceZone created: {stats.AmbienceZones}");
            Debug.Log($"  PointSoundEmitter added:  {stats.PointEmitters}");
            Debug.Log($"  BardPerformer added:      {stats.BardPerformers}");
            Debug.Log($"  Skipped (already set up): {stats.Skipped}");
        }

        // ── CityAmbienceZone ───────────────────────────────────────────────────

        private static void CreateCityAmbienceZone(ref Stats stats)
        {
            var bounds = CalculateBuildingBounds();
            if (!bounds.HasValue)
            {
                Debug.LogWarning("[AudioZoneBuilder] No buildings found — CityAmbienceZone not created.");
                return;
            }

            var zoneGO = new GameObject("CityAmbienceZone");
            Undo.RegisterCreatedObjectUndo(zoneGO, "Create CityAmbienceZone");

            var b = bounds.Value;
            zoneGO.transform.position = b.center;

            var col = Undo.AddComponent<BoxCollider>(zoneGO);
            col.isTrigger = true;
            col.size = b.size + Vector3.one * (CityAmbiencePadding * 2f);
            col.center = Vector3.zero;

            Undo.AddComponent<CityAmbienceZone>(zoneGO);

            // AudioSource нужен хотя бы один (CityAmbienceZone ожидает массив)
            Undo.AddComponent<AudioSource>(zoneGO);

            stats.AmbienceZones++;
            Debug.Log($"[AudioZoneBuilder] CityAmbienceZone created at {b.center}, size {col.size}.");
        }

        private static Bounds? CalculateBuildingBounds()
        {
            var allObjects = Object.FindObjectsOfType<GameObject>(includeInactive: false);
            Bounds? result = null;

            foreach (var go in allObjects)
            {
                if (!IsBuilding(go.name.ToLowerInvariant())) continue;

                var renderers = go.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    if (result == null)
                        result = r.bounds;
                    else
                    {
                        var merged = result.Value;
                        merged.Encapsulate(r.bounds);
                        result = merged;
                    }
                }
            }

            return result;
        }

        private static bool IsBuilding(string nameLower)
        {
            return nameLower.Contains("building") || nameLower.Contains("house") || nameLower.Contains("tavern")
                   || nameLower.Contains("inn") || nameLower.Contains("forge") || nameLower.Contains("blacksmith")
                   || nameLower.Contains("кузница") || nameLower.Contains("таверна")
                   || nameLower.Contains("market") || nameLower.Contains("рынок")
                   || nameLower.Contains("shop") || nameLower.Contains("stall")
                   || nameLower.Contains("tower") || nameLower.Contains("church")
                   || nameLower.Contains("mill") || nameLower.Contains("barn");
        }

        // ── PointSoundEmitter ──────────────────────────────────────────────────

        private static void AddPointEmittersToBuildings(ref Stats stats)
        {
            var allObjects = Object.FindObjectsOfType<GameObject>(includeInactive: false);

            foreach (var go in allObjects)
            {
                string nameLower = go.name.ToLowerInvariant();

                foreach (var rule in BuildingSoundRules)
                {
                    if (!MatchesKeywords(nameLower, rule.Keywords)) continue;

                    if (go.TryGetComponent<PointSoundEmitter>(out _))
                    {
                        stats.Skipped++;
                        break;
                    }

                    var emitter = Undo.AddComponent<PointSoundEmitter>(go);

                    // Настраиваем рабочие часы через SerializedObject
                    var so = new SerializedObject(emitter);
                    so.FindProperty("_startHour").floatValue = rule.StartHour;
                    so.FindProperty("_endHour").floatValue   = rule.EndHour;
                    so.FindProperty("_looping").boolValue    = true;
                    so.ApplyModifiedProperties();

                    // AudioSource должен быть на объекте (PointSoundEmitter ищет его в Awake)
                    if (!go.TryGetComponent<AudioSource>(out _))
                        Undo.AddComponent<AudioSource>(go);

                    stats.PointEmitters++;
                    Debug.Log($"[AudioZoneBuilder] PointSoundEmitter ({rule.SoundLabel}) added to '{go.name}'. Assign AudioClip manually.");
                    break;
                }
            }
        }

        private static bool MatchesKeywords(string nameLower, string[] keywords)
        {
            foreach (var kw in keywords)
            {
                if (nameLower.Contains(kw)) return true;
            }
            return false;
        }

        // ── BardPerformer ──────────────────────────────────────────────────────

        private static void AddBardPerformers(ref Stats stats)
        {
            var schedulers = Object.FindObjectsOfType<NPCScheduler>(includeInactive: false);

            foreach (var scheduler in schedulers)
            {
                // Получаем профиль через SerializedObject чтобы не нарушать инкапсуляцию
                var so = new SerializedObject(scheduler);
                var profileProp = so.FindProperty("_profile");
                if (profileProp == null || profileProp.objectReferenceValue == null) continue;

                var profile = profileProp.objectReferenceValue as NPCProfile;
                if (profile == null || profile.Role != NPCRole.Bard) continue;

                var go = scheduler.gameObject;

                if (go.TryGetComponent<BardPerformer>(out _))
                {
                    stats.Skipped++;
                    continue;
                }

                Undo.AddComponent<BardPerformer>(go);

                if (!go.TryGetComponent<AudioSource>(out _))
                    Undo.AddComponent<AudioSource>(go);

                stats.BardPerformers++;
                Debug.Log($"[AudioZoneBuilder] BardPerformer added to '{go.name}'. Assign BardMusicData manually.");
            }

            // Запасной поиск — по имени, если нет NPCScheduler
            var allObjects = Object.FindObjectsOfType<GameObject>(includeInactive: false);
            foreach (var go in allObjects)
            {
                string nameLower = go.name.ToLowerInvariant();
                if (!nameLower.Contains("bard") && !nameLower.Contains("musician") && !nameLower.Contains("performer"))
                    continue;

                if (go.TryGetComponent<BardPerformer>(out _))
                {
                    stats.Skipped++;
                    continue;
                }

                Undo.AddComponent<BardPerformer>(go);

                if (!go.TryGetComponent<AudioSource>(out _))
                    Undo.AddComponent<AudioSource>(go);

                stats.BardPerformers++;
                Debug.Log($"[AudioZoneBuilder] BardPerformer added to '{go.name}' (by name). Assign BardMusicData manually.");
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private struct Stats
        {
            public int AmbienceZones;
            public int PointEmitters;
            public int BardPerformers;
            public int Skipped;
        }
    }
}
