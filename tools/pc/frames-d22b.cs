var s = "Assets/Scenes/region.unity";
var d = "C:/dev/zelda-builds/frames/";
// D-22b: the places of the region from the game camera WITH the heroine in the frame (her card is built and turned by its own LateUpdate,
// called here in edit mode), next to the concept frames F1, F1n, F3, F4, env-bridge. Logs the heroine's share of the frame height (card mesh
// on screen). Morning frames set the mist (_ZD_Mist, MorningMist does it in play); the night frame adds the campfire's two lights the way
// CampPresenter makes them in play (far 1.6 / 9 m, core 3.2 / 2.4 m) — in edit mode no presenter runs.
System.Func<UnityEngine.Camera, string> heroine = cam =>
{
    var hero = UnityEngine.Object.FindFirstObjectByType<ZeldaDaughter.Hero.HeroController>();
    var p = hero.transform.position;
    UnityEngine.Shader.SetGlobalVector("_ZD_HeroPos", new UnityEngine.Vector4(p.x, p.y - 1f, p.z, 1f));
    var bs = hero.GetComponentInChildren<ZeldaDaughter.Rendering.BillboardSprite>();
    var m = typeof(ZeldaDaughter.Rendering.BillboardSprite).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    m.Invoke(bs, null);
    var mf = bs.Card.GetComponent<UnityEngine.MeshFilter>();
    float lo = float.MaxValue, hi = float.MinValue;
    foreach (var v in mf.sharedMesh.vertices)
    {
        float y = cam.WorldToScreenPoint(mf.transform.TransformPoint(v)).y;
        lo = System.Math.Min(lo, y); hi = System.Math.Max(hi, y);
    }
    float k = (hi - lo) / cam.pixelHeight;
    return "card " + (k * 2340f).ToString("0") + " px of 2340 = 1/" + (1f / k).ToString("0.0");
};
System.Action<UnityEngine.Vector3> fire = at =>
{
    var c = new UnityEngine.Color(1f, 0.55f, 0.22f);
    float[] ys = { 0.8f, 0.45f }, ranges = { 9f, 2.4f }, intens = { 1.6f, 3.2f };
    for (int i = 0; i < 2; i++)
    {
        var fl = new UnityEngine.GameObject("frame_fire").AddComponent<UnityEngine.Light>();
        fl.type = UnityEngine.LightType.Point; fl.color = c; fl.range = ranges[i]; fl.intensity = intens[i]; fl.shadows = UnityEngine.LightShadows.None;
        fl.transform.position = at + new UnityEngine.Vector3(0f, ys[i], 0f);
    }
};
var outLines = new System.Collections.Generic.List<string>();
string lastShare = "";
ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "warmup.png", 0.8f, new UnityEngine.Vector3(-168, 1, 0), 0f);
System.Action<string, float, float, float, float, bool> F = (name, x, z, day, mist, night) =>
    outLines.Add(ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "d22b-" + name + ".png", day, new UnityEngine.Vector3(x, 1, z), 0f, 1080, 2340,
        cam =>
        {
            UnityEngine.Shader.SetGlobalFloat("_ZD_Mist", mist);
            if (night) fire(new UnityEngine.Vector3(-166.5f, 0f, -4.6f));
            lastShare = heroine(cam);
        }) + " " + lastShare);
F("spawn", -167, 0.6f, 0.8f, 1f, false);        // F1: на дороге у бревна, утро, туман
F("road", -96, 0.2f, 0.9f, 0.6f, false);        // дорога у поворота к поляне
F("meadow", -132, 1.2f, 0.9f, 0.6f, false);     // луг у поля
F("night", -165.4f, -3.2f, 0f, 0f, true);       // F1n: ночь у костра спавна
F("square", 70, -7, 1f, 0f, false);             // F4: площадь, фонтан
F("gate", 37, -1.2f, 1f, 0f, false);            // F3: ворота города
F("bridge", -5.5f, 0.2f, 1f, 0f, false);        // env-bridge
F("forest", -80, -32, 1f, 0f, false);
UnityEngine.Shader.SetGlobalFloat("_ZD_Mist", 0f);
return string.Join("\n", outLines.ToArray());
