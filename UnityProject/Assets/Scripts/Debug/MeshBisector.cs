using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace ZeldaDaughter.Debugging
{
    /// <summary>
    /// Disables all MeshRenderers on scene load, then enables them one by one
    /// to identify which mesh causes SIGSEGV on SwiftShader.
    /// </summary>
    public class MeshBisector : MonoBehaviour
    {
        private static List<Renderer> _allRenderers = new();
        private int _enabledCount = 0;
        private float _timer = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoaded()
        {
            Debug.Log("[ZD:MeshBisect] AfterSceneLoad reached — no crash during init!");

            _allRenderers.Clear();
            var allRenderers = Object.FindObjectsOfType<Renderer>(true);

            foreach (var r in allRenderers)
            {
                if (r.enabled)
                    _allRenderers.Add(r);
            }

            Debug.Log($"[ZD:MeshBisect] Found {_allRenderers.Count} active renderers");

            var go = new GameObject("[MeshBisector]");
            DontDestroyOnLoad(go);
            go.AddComponent<MeshBisector>();
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;

            // Enable 5 renderers per second
            if (_timer >= 0.2f)
            {
                _timer = 0;

                if (_enabledCount < _allRenderers.Count)
                {
                    var r = _allRenderers[_enabledCount];
                    if (r != null)
                    {
                        r.enabled = true;
                        Debug.Log($"[ZD:MeshBisect] Enabled [{_enabledCount}/{_allRenderers.Count}] {r.gameObject.name} shader={r.sharedMaterial?.shader?.name ?? "null"}");
                    }
                    _enabledCount++;
                }
                else if (_enabledCount == _allRenderers.Count)
                {
                    Debug.Log($"[ZD:MeshBisect] ALL {_allRenderers.Count} renderers enabled. No crash!");
                    _enabledCount++; // Stop logging
                }
            }
        }
    }
}
