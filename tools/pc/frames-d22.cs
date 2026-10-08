var s = "Assets/Scenes/region.unity";
var d = "C:/dev/zelda-builds/frames/";
// D-22: the places of the region from the game camera (size from the scene config, 0 = its own), next to the concept frames.
ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "warmup.png", 0.8f, new UnityEngine.Vector3(-168, 1, 0), 10f);
var l = new System.Collections.Generic.List<string>();
System.Action<string, float, float, float> F = (name, x, z, day) =>
    l.Add(ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "d22-" + name + ".png", day, new UnityEngine.Vector3(x, 1, z), 0f));
F("spawn", -168, 0, 0.8f);
F("meadow", -140, 4, 0.8f);
F("trail", -88, -4, 1f);
F("forest", -80, -30, 1f);
F("bridge", 0, 0, 1f);
F("gate", 36, 0, 1f);
F("square", 72, -9, 1f);
F("square2", 70, 14, 1f);
F("east", 112, 0, 1f);
return string.Join("\n", l);
