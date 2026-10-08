// D-28: only the pilot scene (scenes/pilot-f1.json), twice — the hash must repeat; the editor gets an empty scene back.
var before = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
var a = ZeldaDaughter.Editor.SceneBuilder.Build("../scenes/pilot-f1.json");
var b = ZeldaDaughter.Editor.SceneBuilder.Build("../scenes/pilot-f1.json");
ZeldaDaughter.Editor.SceneBuilder.RestoreScene(before);
return $"pilot-f1 hash {a} / {b}";
