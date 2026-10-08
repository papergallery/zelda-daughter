var s = "Assets/Scenes/region.unity";
var d = "C:/dev/zelda-builds/frames/";
// The first render after opening a scene in a fresh state is empty: warm up once.
ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "warmup.png", 1f, new UnityEngine.Vector3(-168, 1, 0), 10f);
var l = new System.Collections.Generic.List<string>();
System.Action<string, float, float, float> F = (name, x, z, size) =>
    l.Add(ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "region-" + name + ".png", 1f, new UnityEngine.Vector3(x, 1, z), size));
F("spawn", -168, 0, 10f);
F("field", -131, 12, 11f);
F("road-forest", -88, -2, 11f);
F("forest", -85, -25, 11f);
F("glade", -72, -45, 11f);
F("lair", -50, -68, 11f);
F("bridge", 0, 0, 11f);
F("gate", 35, 0, 11f);
F("square", 72, 2, 12f);
F("forge", 94, 12, 10f);
F("tavern", 62, 16, 10f);
F("shop", 113, 8, 10f);
F("east", 112, 0, 11f);
F("overview-west", -120, 0, 28f);
F("overview-town", 90, 4, 28f);
return string.Join("\n", l);
