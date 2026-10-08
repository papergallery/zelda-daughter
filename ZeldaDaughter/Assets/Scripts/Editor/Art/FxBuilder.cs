using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-16: Ian's Fire Pack (Assets/ThirdParty/IansFirePack, not in git; imported by tools/pc/zd-import-fire.ps1) → the three fire prefabs of fx.json
    /// (<c>campfire</c>, <c>torch_flame</c>, <c>grass_fire</c>) in Assets/ThirdParty/IansFirePack/Zelda/. Each is an unpacked copy of a pack prefab with
    /// the colours pulled to the warm muted palette (decision 5: less saturated, a little darker and warmer), without lights, flicker scripts and
    /// sounds — the game lights and sounds fire itself (CampPresenter, NatureFx, HeroTorchLight, Audio). Without the pack nothing is made and fx.json
    /// keeps the primitives. Run it before BuildAll; the prefab paths do not change, so the registry stays valid.
    /// </summary>
    public static class FxBuilder
    {
        const string Pack = "Assets/ThirdParty/IansFirePack";
        const string OutDir = Pack + "/Zelda";

        struct Source { public string Key, Path; public float Scale; }

        static readonly Source[] Sources =
        {
            new Source { Key = "campfire", Path = Pack + "/_URP Specific/Prefabs/CampFire/Camp Fire Medium URP.prefab", Scale = 1f },
            new Source { Key = "torch_flame", Path = Pack + "/_URP Specific/Prefabs/Torch URP.prefab", Scale = 1f },
            new Source { Key = "grass_fire", Path = Pack + "/_URP Specific/Prefabs/Fire Small URP.prefab", Scale = 0.7f },
        };

        [MenuItem("Zelda/Art/Build fire prefabs")]
        public static int Build()
        {
            int made = 0;
            foreach (var s in Sources)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(s.Path);
                if (src == null) { Debug.LogWarning($"[ZD:Art] fire pack prefab missing, {s.Key} stays a primitive: {s.Path}"); continue; }
                if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder(Pack, "Zelda");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = s.Key;
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * s.Scale;
                Strip(go);
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) Mute(ps);
                PrefabUtility.SaveAsPrefabAsset(go, $"{OutDir}/{s.Key}.prefab");
                Object.DestroyImmediate(go);
                made++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ZD:Art] fire prefabs made={made}");
            return made;
        }

        /// <summary>No lights, light flicker scripts or audio: the game does those.</summary>
        static void Strip(GameObject go)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Name == "LightFlicker") Object.DestroyImmediate(mb);
            foreach (var l in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
            foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
        }

        /// <summary>Pull a colour towards grey by a quarter, then warm it (less blue) and darken a little; alpha is kept.</summary>
        public static Color Mute(Color c)
        {
            float grey = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            var m = Color.Lerp(c, new Color(grey, grey, grey, c.a), 0.25f);
            return new Color(m.r * 0.95f, m.g * 0.84f, m.b * 0.66f, c.a);
        }

        static Gradient Mute(Gradient g)
        {
            var keys = g.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Mute(keys[i].color);
            var n = new Gradient();
            n.SetKeys(keys, g.alphaKeys);
            return n;
        }

        static ParticleSystem.MinMaxGradient Mute(ParticleSystem.MinMaxGradient m)
        {
            switch (m.mode)
            {
                case ParticleSystemGradientMode.Color: return new ParticleSystem.MinMaxGradient(Mute(m.color));
                case ParticleSystemGradientMode.TwoColors: return new ParticleSystem.MinMaxGradient(Mute(m.colorMin), Mute(m.colorMax));
                case ParticleSystemGradientMode.Gradient: return new ParticleSystem.MinMaxGradient(Mute(m.gradient));
                case ParticleSystemGradientMode.TwoGradients: return new ParticleSystem.MinMaxGradient(Mute(m.gradientMin), Mute(m.gradientMax));
                default: return m; // RandomColor: leave
            }
        }

        static void Mute(ParticleSystem ps)
        {
            var main = ps.main;
            main.startColor = Mute(main.startColor);
            var col = ps.colorOverLifetime;
            if (col.enabled) col.color = Mute(col.color);
        }
    }
}
