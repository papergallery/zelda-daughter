var s = "Assets/Scenes/prologue-grey.unity";
var a = ZeldaDaughter.Editor.FrameCapture.Capture(s, "C:/dev/zelda-builds/frames/prologue-grey-day.png", 1f);
var b = ZeldaDaughter.Editor.FrameCapture.Capture(s, "C:/dev/zelda-builds/frames/prologue-grey-night.png", 0f);
return a + "\n" + b;
