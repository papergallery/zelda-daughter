// Before files under Assets/Scenes change on disk (zd-sync, archives): leave no scene file open, so Unity has
// nothing to ask "modified externally — Reload?" about. A dirty scene is never discarded.
if (UnityEditor.EditorApplication.isPlaying) return "[ZD:Sync] playing - scene left open";
var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (s.isDirty) return "[ZD:Sync] unsaved changes in " + s.path + " - scene left open";
if (string.IsNullOrEmpty(s.path)) return "[ZD:Sync] already on an untitled scene";
UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
return "[ZD:Sync] empty scene (was " + s.path + ")";
