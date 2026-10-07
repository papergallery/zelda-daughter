using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZeldaDaughter.Editor
{
    public static class EmptySceneBuilder
    {
        [MenuItem("ZeldaDaughter/Scenes/Build Empty Test Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Just a camera and light — nothing else
            var go = new GameObject("TestCube");
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            go.AddComponent<MeshRenderer>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/EmptyTestScene.unity");
            Debug.Log("[EmptySceneBuilder] Created Assets/Scenes/EmptyTestScene.unity");
        }
    }
}
