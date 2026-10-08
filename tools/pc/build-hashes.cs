var logs = new System.Collections.Generic.List<string>();
UnityEngine.Application.LogCallback cb = (c, st, t) => { if (c.Contains("built region") || c.Contains("scatter region") || t != UnityEngine.LogType.Log) logs.Add(t + " " + c); };
UnityEngine.Application.logMessageReceived += cb;
ZeldaDaughter.Editor.SceneBuilder.BuildAll();
logs.Add("--- second run");
ZeldaDaughter.Editor.SceneBuilder.BuildAll();
UnityEngine.Application.logMessageReceived -= cb;
return string.Join("\n", logs);
