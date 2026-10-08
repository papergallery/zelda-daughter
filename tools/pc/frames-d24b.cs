var s = "Assets/Scenes/region.unity";
var d = "C:/dev/zelda-builds/frames/";
// D-24b: the adult heroine (D-24b) next to villagers on the square, from the game camera. Billboard cards are built and
// turned by their own LateUpdate — called here in edit mode for every billboard (heroine and NPCs). Logs card heights.
string info = "";
System.Action<UnityEngine.Camera> prep = cam =>
{
    var m = typeof(ZeldaDaughter.Rendering.BillboardSprite).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var sb = new System.Text.StringBuilder();
    foreach (var bs in UnityEngine.Object.FindObjectsByType<ZeldaDaughter.Rendering.BillboardSprite>(UnityEngine.FindObjectsSortMode.None))
    {
        m.Invoke(bs, null);
        if (bs.Card == null) continue;
        var mf = bs.Card.GetComponent<UnityEngine.MeshFilter>();
        if (mf == null || mf.sharedMesh == null) continue;
        float lo = float.MaxValue, hi = float.MinValue; bool vis = false;
        foreach (var v in mf.sharedMesh.vertices)
        {
            var p = cam.WorldToScreenPoint(mf.transform.TransformPoint(v));
            if (p.x > 0 && p.x < cam.pixelWidth && p.y > 0 && p.y < cam.pixelHeight) vis = true;
            lo = System.Math.Min(lo, p.y); hi = System.Math.Max(hi, p.y);
        }
        if (vis) sb.Append($" {bs.transform.root.name}:{(hi - lo) * 2340f / cam.pixelHeight:0}px");
    }
    info = sb.ToString();
};
var l = new System.Collections.Generic.List<string>();
ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "warmup.png", 1f, new UnityEngine.Vector3(70, 1, -8.2f), 0f);
System.Action<string, float, float> F = (name, x, z) =>
    l.Add(ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "D-24b-" + name + ".png", 1f, new UnityEngine.Vector3(x, 1, z), 0f, 1080, 2340, prep) + info);
F("square-townswoman", 98.8f, 4.0f);
F("square-old-man", 70.4f, -7.6f);
F("square-herbalist", 74.8f, 13.6f);
return string.Join("\n", l);
