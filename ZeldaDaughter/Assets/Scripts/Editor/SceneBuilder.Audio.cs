using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Audio;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-17: the Audio object on Game — <see cref="AudioDirector"/> with the places of the scene that sound (town, field, river by the config;
    /// the square's stone and the bridge's and tavern's wood for the footsteps) and the bard at the tavern's bar. The places come from the config
    /// (docs/demo/unity-architecture.md §5), no numbers here but the fades. Also sets the import of the purchased clips named by
    /// sounds.json (Vorbis; long loops streamed; spatial ones mono) — that changes only .meta files under Assets/ThirdParty, which is not in git.
    /// </summary>
    public static partial class SceneBuilder
    {
        const float TownFade = 25f, FieldFade = 20f, RiverFade = 18f;
        const float TavernRadius = 5.5f;
        const float BardFromHour = 17f, BardToHour = 23f;

        static partial void AddAudio(BuildContext ctx)
        {
            var sounds = ctx.Art.Sounds;
            AudioImport.Apply(sounds);

            var zones = new List<SoundZone>();
            foreach (var w in ctx.Config.Water) zones.Add(SoundZoneOf("river", w, RiverFade));
            foreach (var z in ctx.Config.Zones)
            {
                if (z.Tags.Contains("field")) zones.Add(SoundZoneOf("field", z, FieldFade));
                if (z.Tags.Contains("square")) zones.Add(SoundZoneOf("stone", z, 0f));
                if (z.Tags.Contains("bridge")) zones.Add(SoundZoneOf("wood", z, 0f));
            }

            // the town: a circle round the buildings near the square (the hut in the field is not the town)
            var square = ctx.Config.Zones.FirstOrDefault(z => z.Tags.Contains("square"));
            var houses = ctx.Config.Objects.Where(o => o.Tags.Contains("building")).ToList();
            if (square != null) houses = houses.Where(o => Dist(o.Position.X, o.Position.Z, square.Center.X, square.Center.Z) < 70f).ToList();
            if (houses.Count > 0)
            {
                float cx = houses.Average(o => o.Position.X), cz = houses.Average(o => o.Position.Z);
                float r = houses.Max(o => Dist(o.Position.X, o.Position.Z, cx, cz)) + 6f;
                zones.Add(CircleZone("town", cx, cz, r, TownFade));
            }

            var tavern = ctx.Config.Objects.FirstOrDefault(o => o.Id == "tavern");
            if (tavern != null) zones.Add(CircleZone("wood", tavern.Position.X, tavern.Position.Z, TavernRadius, 0f));

            var go = new GameObject("Audio");
            go.transform.SetParent(ctx.Game.transform, false);
            go.AddComponent<AudioDirector>().Configure(ctx.Session, ctx.HeroCtl, sounds, zones.ToArray());

            // the bard sits at the tavern's bar
            Vector3? bard = null;
            if (ctx.Index.TryGetAnchor("anchor_tavern_bar", out var bar)) bard = bar;
            else if (tavern != null) bard = new Vector3(tavern.Position.X, 0f, tavern.Position.Z);
            if (bard.HasValue)
            {
                var b = new GameObject("Bard");
                b.transform.SetParent(ctx.Game.transform, false);
                b.transform.position = bard.Value + Vector3.up * 1.2f;
                b.AddComponent<BardSource>().Configure(ctx.Session, sounds, BardFromHour, BardToHour);
            }
        }

        static float Dist(float ax, float az, float bx, float bz) => Mathf.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        static SoundZone SoundZoneOf(string kind, Area area, float fade) => new SoundZone { Kind = kind, AreaJson = area.ToJson(), Fade = fade };

        static SoundZone CircleZone(string kind, float x, float z, float radius, float fade) =>
            new SoundZone { Kind = kind, AreaJson = new Area { Shape = "circle", Center = new Pt(x, z), Radius = radius }.ToJson(), Fade = fade };
    }

    /// <summary>Import settings of the clips the registry uses: Vorbis; the long loops (ambience) streamed, the spatial ones in mono.</summary>
    static class AudioImport
    {
        public static void Apply(SoundRegistry registry)
        {
            if (registry == null) return;
            int changed = 0;
            foreach (var def in registry.Sounds)
            {
                bool loop = def.Id.StartsWith("amb_", System.StringComparison.Ordinal) && !def.Spatial || def.Id.StartsWith("fire_", System.StringComparison.Ordinal);
                foreach (var clip in def.Clips)
                {
                    if (clip == null) continue;
                    var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
                    if (imp == null) continue;
                    var s = imp.defaultSampleSettings;
                    var want = s;
                    want.loadType = loop && clip.length > 8f ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
                    want.compressionFormat = AudioCompressionFormat.Vorbis;
                    want.quality = 0.5f;
                    want.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
                    bool mono = def.Spatial;
                    if (s.loadType == want.loadType && s.compressionFormat == want.compressionFormat && Mathf.Approximately(s.quality, want.quality)
                        && s.sampleRateSetting == want.sampleRateSetting && imp.forceToMono == mono) continue;
                    imp.defaultSampleSettings = want;
                    imp.forceToMono = mono;
                    imp.SaveAndReimport();
                    changed++;
                }
            }
            if (changed > 0) Debug.Log($"[ZD:Audio] import settings set on {changed} clips");
        }
    }
}
