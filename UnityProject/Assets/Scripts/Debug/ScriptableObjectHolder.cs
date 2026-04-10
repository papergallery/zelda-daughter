using UnityEngine;

namespace ZeldaDaughter.Debugging
{
    /// <summary>
    /// Holds ScriptableObject references so they're loaded in runtime.
    /// Used by RemoteInputReceiver to find CraftRecipeDatabase via Resources.FindObjectsOfTypeAll.
    /// </summary>
    public class ScriptableObjectHolder : MonoBehaviour
    {
        [SerializeField] private ScriptableObject[] _assets;
    }
}
