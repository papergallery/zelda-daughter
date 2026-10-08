var s = "Assets/Scenes/models-test.unity";
var d = "C:/dev/zelda-builds/frames/";
// The first render after opening a scene in a fresh state is empty: warm up once.
ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "warmup.png", 1f, new UnityEngine.Vector3(0, 0, -16), 10f);
var l = new System.Collections.Generic.List<string>();
l.Add(ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "models-test-spawn.png", 1f, new UnityEngine.Vector3(0, 0, -16), 10f));
l.Add(ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "models-test-house.png", 1f, new UnityEngine.Vector3(-12, 0, -2), 9f));
l.Add(ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "models-test-bridge.png", 1f, new UnityEngine.Vector3(6, 0, 8), 9f));
l.Add(ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "models-test-town.png", 1f, new UnityEngine.Vector3(-4, 0, 20), 12f));
l.Add(ZeldaDaughter.Editor.FrameCapture.Capture(s, d + "models-test-farm.png", 1f, new UnityEngine.Vector3(20, 0, 14), 12f));
return string.Join("\n", l);
