var s = "Assets/Scenes/pilot-f1.unity";
var d = "C:/dev/zelda-builds/frames/";
// D-28: the pilot «corner f1» (scenes/pilot-f1.json) from the game camera, framed like the concept: the heroine ≈ 1/9 of the frame height and
// 0.58 of it from the top (f1: feet at 1045 of 1792). Billboards are turned by their own LateUpdate (as frames-d24); the crown fade gets the
// heroine's feet; at night the campfire of the scene burns: the particle fire of the game (Fx registry, its own stone ring hidden — the ring is
// painted) and the point light of CampPresenter (colour, 3, range camp.json lightRadius 6). Logs the heroine's card height in px.
float size = 7.6f; // first frame at 7.3: the drawn heroine ≈ 1/8.7 of the frame → 7.6 for ≈ 1/9
string info = "";
System.Func<bool, UnityEngine.Vector3, System.Action<UnityEngine.Camera>> prep = (fire, heroAt) => cam =>
{
    var m = typeof(ZeldaDaughter.Rendering.BillboardSprite).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    foreach (var bs in UnityEngine.Object.FindObjectsByType<ZeldaDaughter.Rendering.BillboardSprite>(UnityEngine.FindObjectsSortMode.None)) m.Invoke(bs, null);
    var feet = heroAt - UnityEngine.Vector3.up;
    UnityEngine.Shader.SetGlobalVector("_ZD_HeroPos", new UnityEngine.Vector4(feet.x, feet.y, feet.z, 1f));
    cam.transform.position += cam.transform.up * (0.03f * 2f * cam.orthographicSize); // first frame: feet at 0.636 with 0.08 → 0.58 like f1
    info = "";
    foreach (var bs in UnityEngine.Object.FindObjectsByType<ZeldaDaughter.Rendering.BillboardSprite>(UnityEngine.FindObjectsSortMode.None))
    {
        if (bs.Card == null || bs.GetComponentInParent<ZeldaDaughter.Hero.HeroController>() == null) continue;
        var mf = bs.Card.GetComponent<UnityEngine.MeshFilter>();
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var v in mf.sharedMesh.vertices) { var p = cam.WorldToScreenPoint(mf.transform.TransformPoint(v)); lo = System.Math.Min(lo, p.y); hi = System.Math.Max(hi, p.y); }
        info = $" heroine={(hi - lo) * 2340f / cam.pixelHeight:0}px top={(1f - hi / cam.pixelHeight) * 2340f:0}px feet={(1f - lo / cam.pixelHeight) * 2340f:0}px";
    }
    if (!fire) return;
    var ring = UnityEngine.GameObject.Find("Objects/campfire_spawn").transform.position;
    var reg = UnityEditor.AssetDatabase.LoadAssetAtPath<ZeldaDaughter.World.FxRegistry>("Assets/Art/Registries/Fx.asset");
    var prefab = reg != null ? reg.Get("campfire") : null;
    info += prefab != null ? " fire=prefab" : " fire=none";
    if (prefab != null)
    {
        var fx = UnityEngine.Object.Instantiate(prefab, ring, UnityEngine.Quaternion.identity);
        foreach (var r in fx.GetComponentsInChildren<UnityEngine.MeshRenderer>(true)) r.enabled = false;
        foreach (var ps in fx.GetComponentsInChildren<UnityEngine.ParticleSystem>(true))
        {
            var main = ps.main;
            main.startSizeMultiplier *= ps.name == "Fire" ? 1.9f * 1.6f : 1.9f;
            ps.Simulate(1.6f, false, true);
        }
    }
    var l = new UnityEngine.GameObject("fire_light").AddComponent<UnityEngine.Light>();
    l.transform.position = ring + UnityEngine.Vector3.up * 0.8f;
    l.type = UnityEngine.LightType.Point;
    l.color = new UnityEngine.Color(1f, 0.62f, 0.3f);
    l.range = 6f;
    l.intensity = 3f;
    l.shadows = UnityEngine.LightShadows.None;
};
var log = new System.Collections.Generic.List<string>();
ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "warmup.png", 0.62f, new UnityEngine.Vector3(0, 1, 0), size);
var day = new UnityEngine.Vector3(0, 1, 0);
log.Add(ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "D-28-pilot-f1.png", 0.62f, day, size, 1080, 2340, prep(false, day)) + info);
// f1n: she is to the right of the fire (f1n px 465,1010 → 0.83, −0.06 m on the ground; tools/gen-pilot-f1.py at())
var night = new UnityEngine.Vector3(0.83f, 1, -0.06f);
log.Add(ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "D-28-pilot-f1n.png", 0f, night, size, 1080, 2340, prep(true, night)) + info);
return string.Join("\n", log);
