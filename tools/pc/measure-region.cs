// D-10 criterion 7: objects, triangles and draw calls in the frame of a place (region.unity, the game camera, 1080x2340).
var s = "Assets/Scenes/region.unity";
string previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
var res = new System.Collections.Generic.List<string>();
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(s, UnityEditor.SceneManagement.OpenSceneMode.Single);
var cam = UnityEngine.Camera.main;
var hero = UnityEngine.Object.FindFirstObjectByType<ZeldaDaughter.Hero.HeroController>();
var iso = cam.GetComponent<ZeldaDaughter.World.IsoCamera>();
var all = UnityEngine.Object.FindObjectsByType<UnityEngine.MeshRenderer>(UnityEngine.FindObjectsSortMode.None);
res.Add("scene renderers=" + all.Length);
long sceneTris = 0;
foreach (var r in all) { var mf = r.GetComponent<UnityEngine.MeshFilter>(); if (mf != null && mf.sharedMesh != null) for (int i = 0; i < mf.sharedMesh.subMeshCount; i++) sceneTris += mf.sharedMesh.GetIndexCount(i) / 3; }
res.Add("scene triangles=" + sceneTris);
string[] names = { "spawn", "field", "forest", "glade", "bridge", "gate", "square", "forge", "tavern", "east" };
float[,] pts = { { -168, 0 }, { -131, 12 }, { -85, -25 }, { -72, -45 }, { 0, 0 }, { 35, 0 }, { 72, 2 }, { 94, 12 }, { 62, 16 }, { 112, 0 } };
for (int k = 0; k < names.Length; k++)
{
    hero.Teleport(new UnityEngine.Vector3(pts[k, 0], 1, pts[k, 1]), 0f);
    iso.SnapToTarget();
    cam.aspect = 1080f / 2340f;
    var planes = UnityEngine.GeometryUtility.CalculateFrustumPlanes(cam);
    int n = 0; long tris = 0; var mats = new System.Collections.Generic.HashSet<UnityEngine.Material>();
    foreach (var r in all)
    {
        if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
        if (!UnityEngine.GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
        var mf = r.GetComponent<UnityEngine.MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
        n++;
        for (int i = 0; i < mf.sharedMesh.subMeshCount; i++) tris += mf.sharedMesh.GetIndexCount(i) / 3;
        foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m);
    }
    var rt = new UnityEngine.RenderTexture(1080, 2340, 24);
    cam.targetTexture = rt;
    cam.Render();
    cam.Render();
    string stats = "UnityStats batches=" + UnityEditor.UnityStats.batches + " drawCalls=" + UnityEditor.UnityStats.drawCalls + " triangles=" + UnityEditor.UnityStats.triangles + " setPass=" + UnityEditor.UnityStats.setPassCalls;
    cam.targetTexture = null;
    rt.Release();
    res.Add(names[k] + ": visible renderers=" + n + " mesh triangles=" + tris + " materials=" + mats.Count + " | " + stats);
}
if (!string.IsNullOrEmpty(previous)) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(previous, UnityEditor.SceneManagement.OpenSceneMode.Single);
return string.Join("\n", res);
