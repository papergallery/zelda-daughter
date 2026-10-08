using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Scenes;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;
using ZeldaDaughter.Input;
using ZeldaDaughter.NPC;
using ZeldaDaughter.Rendering;
using ZeldaDaughter.UI;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// W0: the session part of the one scene builder (docs/demo/unity-architecture.md §5). <see cref="BuildSession"/> makes the Game object — the
    /// session, its UI, the index of the scene, the picker and the input router, the window stack and the fader, the pick-up and talk presenters,
    /// the art registries — and then calls each package's partial method in order. A package adds its part by implementing its method in its own
    /// file (<c>static partial void AddNpcs(BuildContext ctx)</c>); <c>SceneBuilder.cs</c> is touched by W0 only.
    /// </summary>
    public static partial class SceneBuilder
    {
        /// <summary>Everything a package's part may need or add; objects are stored by id so a later part can find an earlier one's.</summary>
        sealed class BuildContext
        {
            public SceneConfig Config;
            public DataSet Data;
            public ModelCatalog Catalog;
            public GameObject Ground, Hero, Game;
            public HeroController HeroCtl;
            public Camera Cam;
            public IsoCamera Iso;
            public Light SunLight;
            public Transform ObjectsRoot;
            public List<SceneTags> Tagged;

            public GameSession Session;
            public SessionUI UI;
            public WorldIndex Index;
            public WorldPicker Picker;
            public InputRouter Router;
            public WindowStack Windows;
            public ScreenFader Fader;
            public ArtAssets Art;
            public SunController SunCtl;

            /// <summary>Anything a part made that others may need: key = a name the part documents (<c>npc_peasant</c>, <c>radial_menu</c>).</summary>
            public readonly Dictionary<string, GameObject> ById = new Dictionary<string, GameObject>();
        }

        // Each package implements its method in its own file; unimplemented ones are simply not called.
        static partial void AddHeroView(BuildContext ctx);  // D-11  SceneBuilder.Hero.cs
        static partial void AddNpcs(BuildContext ctx);      // D-12  SceneBuilder.Npcs.cs
        static partial void AddCombat(BuildContext ctx);    // D-13  SceneBuilder.Combat.cs
        static partial void AddItemsUi(BuildContext ctx);   // D-14  SceneBuilder.Items.cs
        static partial void AddScreens(BuildContext ctx);   // D-15  SceneBuilder.Screens.cs (+ MapBaker)
        static partial void AddNature(BuildContext ctx);    // D-16  SceneBuilder.Nature.cs
        static partial void AddAudio(BuildContext ctx);     // D-17  SceneBuilder.Audio.cs
        static partial void AddHints(BuildContext ctx);     // D-18  SceneBuilder.Hints.cs
        static partial void AddMist(BuildContext ctx);      // D-22  SceneBuilder.Mist.cs

        static void EnsureLayers() => ProjectSetup.EnsureLayers();

        static void BuildSession(BuildContext ctx)
        {
            var config = ctx.Config;
            AssignLayers(ctx);

            var game = new GameObject("Game");
            ctx.Game = game;
            var look = RegistryBuilder.Load<UiLook>(RegistryBuilder.UiLookPath);
            var sprites = RegistryBuilder.Load<SpriteLook>(RegistryBuilder.SpriteLookPath);
            var talkIcons = RegistryBuilder.Load<IconRegistry>(RegistryBuilder.TalkIconsPath);
            ctx.Art = game.AddComponent<ArtAssets>();
            ctx.Art.Configure(look, sprites,
                RegistryBuilder.Load<CharacterRegistry>(RegistryBuilder.CharactersPath),
                RegistryBuilder.Load<IconRegistry>(RegistryBuilder.ItemIconsPath), talkIcons,
                RegistryBuilder.Load<ZeldaDaughter.Audio.SoundRegistry>(RegistryBuilder.SoundsPath),
                RegistryBuilder.Load<FxRegistry>(RegistryBuilder.FxPath));

            ctx.SunCtl = game.AddComponent<SunController>();
            ctx.SunCtl.Configure(ctx.SunLight, V(config.Light.Rotation), config.Light.Intensity, ColorOf(config.Ambient.Color));
            ctx.SunCtl.SetCamera(ctx.Cam);

            var uiGo = new GameObject("UI");
            uiGo.transform.SetParent(game.transform, false);
            ctx.UI = uiGo.AddComponent<SessionUI>();
            ctx.UI.Configure(ctx.Cam, ctx.Hero.transform, look, talkIcons);

            var zones = Object.FindObjectsByType<ZoneArea>(FindObjectsSortMode.None).OrderBy(z => z.Id, System.StringComparer.Ordinal).ToArray();
            ctx.Index = game.AddComponent<WorldIndex>();
            ctx.Index.Configure(ctx.Tagged.ToArray(), zones);

            ctx.Picker = game.AddComponent<WorldPicker>();
            ctx.Picker.Configure(ctx.Cam, ctx.HeroCtl, ctx.Index);
            ctx.HeroCtl.SetPicker(ctx.Picker);

            ctx.Session = game.AddComponent<GameSession>();
            ctx.Session.Configure(ctx.HeroCtl, ctx.SunCtl, ctx.UI, ctx.Index, config.Name, config.Save.Slot);

            ctx.Router = game.AddComponent<InputRouter>();
            ctx.Router.Configure(ctx.HeroCtl, ctx.Session);
            ctx.Windows = game.AddComponent<WindowStack>();
            ctx.Windows.Configure(ctx.Session, ctx.UI, ctx.HeroCtl);
            ctx.Fader = game.AddComponent<ScreenFader>();
            ctx.Fader.Configure(ctx.UI);
            game.AddComponent<PickupPresenter>().Configure(ctx.Session);
            game.AddComponent<TalkPresenter>().Configure(ctx.Session, ctx.UI);

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(game.transform, false);

            AddHeroView(ctx);
            AddNpcs(ctx);
            AddCombat(ctx);
            AddItemsUi(ctx);
            AddScreens(ctx);
            AddNature(ctx);
            AddAudio(ctx);
            AddHints(ctx);
            AddMist(ctx);
        }

        /// <summary>A tappable on every object of the config except markers: pick-ups, stations, beds and NPCs by their data; the rest are scenery (touch names them, tap does nothing).</summary>
        static void AddTapTarget(GameObject go, ObjectConfig o)
        {
            if (o.Marker) return;
            var kind = TapKind.Scenery;
            if (o.Item != null) kind = TapKind.Pickup;
            else if (o.Station != null || o.Tags.Contains("station")) kind = TapKind.Station;
            else if (o.Tags.Contains("bed")) kind = TapKind.Bed;
            else if (o.Tags.Contains("npc")) kind = TapKind.Npc;
            go.AddComponent<Tappable>().Configure(o.Id, kind);
        }

        /// <summary>Ground → Ground, the hero and the NPCs → Actors, every other collider of the scene (objects and scatter) → Blocking.</summary>
        static void AssignLayers(BuildContext ctx)
        {
            int ground = Layer(ProjectSetup.Layers[0]), blocking = Layer(ProjectSetup.Layers[1]), actors = Layer(ProjectSetup.Layers[2]);
            ctx.Ground.layer = ground;
            ctx.Hero.layer = actors;
            foreach (var tags in ctx.Tagged)
            {
                int layer = tags.Has("npc") ? actors : blocking;
                if (tags.GetComponentInChildren<Collider>(true) != null) SetLayerRecursive(tags.gameObject, layer);
            }
            var scatter = GameObject.Find("Scatter");
            if (scatter != null)
                foreach (var c in scatter.GetComponentsInChildren<Collider>(true)) c.gameObject.layer = blocking;
        }

        static int Layer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer < 0) { Debug.LogError($"[ZD:Scene] layer '{name}' does not exist — run Zelda → Project → Apply settings"); return 0; }
            return layer;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
