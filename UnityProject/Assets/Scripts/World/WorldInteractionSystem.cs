using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.World
{
    public class WorldInteractionSystem : MonoBehaviour
    {
        [SerializeField] private List<WorldInteractionRule> _rules = new();
        [SerializeField] private float _hintDistance = 3f;
        [SerializeField] private float _hintCooldown = 60f;

        private PlayerInventory _inventory;
        private Transform _playerTransform;
        private readonly Dictionary<string, float> _lastHintTime = new();

        private void Awake()
        {
            _inventory = FindObjectOfType<PlayerInventory>();
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        /// <summary>
        /// Вызывается TapInteractionManager или другим скриптом при взаимодействии с объектом.
        /// Проверяет, есть ли у игрока предмет для применения на данный объект.
        /// </summary>
        public bool TryInteract(GameObject target)
        {
            if (_inventory == null || target == null)
                return false;

            foreach (var rule in _rules)
            {
                if (!target.CompareTag(rule.TargetTag))
                    continue;
                if (!_inventory.HasItem(rule.RequiredItem))
                    continue;

                ApplyRule(rule, target);
                return true;
            }

            return false;
        }

        private void ApplyRule(WorldInteractionRule rule, GameObject target)
        {
            switch (rule.Result)
            {
                case InteractionResult.LightFire:
                    if (target.TryGetComponent(out CampfireObject campfire))
                        campfire.Ignite();
                    break;

                case InteractionResult.TransformItem:
                    if (rule.ResultItem != null)
                    {
                        if (rule.ConsumeItem)
                            _inventory.RemoveItem(rule.RequiredItem, 1);
                        _inventory.AddItem(rule.ResultItem, 1);
                        SpeechBubbleManager.Say("Получилось!");
                    }
                    break;
            }
        }

        private void Update()
        {
            if (_playerTransform == null || _inventory == null)
                return;

            foreach (var rule in _rules)
            {
                if (!_inventory.HasItem(rule.RequiredItem))
                    continue;

                string key = rule.RequiredItem.Id + "_" + rule.TargetTag;
                if (_lastHintTime.TryGetValue(key, out float last) && Time.time - last < _hintCooldown)
                    continue;

                var objects = GameObject.FindGameObjectsWithTag(rule.TargetTag);
                foreach (var obj in objects)
                {
                    float dist = Vector3.Distance(_playerTransform.position, obj.transform.position);
                    if (dist <= _hintDistance)
                    {
                        _lastHintTime[key] = Time.time;
                        SpeechBubbleManager.Say("Хм, если поднести к этому...");
                        return;
                    }
                }
            }
        }
    }
}
