using System.Collections;
using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Отслеживает истощение ResourceNode и восстанавливает его через RespawnTime.
    /// Размещать на том же GameObject, что и ResourceNode.
    /// </summary>
    public class ResourceRespawner : MonoBehaviour
    {
        private ResourceNode _node;

        private void Awake()
        {
            _node = GetComponent<ResourceNode>();
        }

        private void OnEnable()
        {
            if (_node != null)
                _node.OnDepleted += HandleDepleted;
        }

        private void OnDisable()
        {
            if (_node != null)
                _node.OnDepleted -= HandleDepleted;
        }

        private void HandleDepleted(ResourceNode node)
        {
            if (node.Data == null)
                return;

            StartCoroutine(RespawnAfterDelay(node.Data.RespawnTime));
        }

        private IEnumerator RespawnAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _node.Respawn();
        }
    }
}
