using UnityEngine;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Костёр — размещаемый объект с двумя состояниями: Unlit (дрова) и Lit (горит).
    /// Поджигается кремнём из инвентаря игрока. После поджига создаёт trigger-зону RestZone
    /// для будущей системы отдыха (Этап 5+).
    /// Интегрирован с ElementState: дождь тушит костёр через RemoveElement(Fire),
    /// а Ignite применяет ApplyElement(Fire) для синхронизации с системой стихий.
    /// </summary>
    public class CampfireObject : MonoBehaviour, IInteractable
    {
        [Header("Visuals")]
        [SerializeField] private GameObject _unlitVisual;
        [SerializeField] private GameObject _litVisual;
        [SerializeField] private Light _fireLight;

        [Header("Settings")]
        [SerializeField] private CampfireConfig _config;

        [Header("Interaction")]
        [SerializeField] private Transform _interactionPoint;
        [SerializeField] private float _interactionRange = 2f;

        private bool _isLit;
        private SphereCollider _restZoneTrigger;
        private ElementState _elementState;

        public bool IsLit => _isLit;

        // IInteractable
        public InteractionType Type => InteractionType.Resource;
        public string InteractionPrompt => _isLit ? string.Empty : "Поджечь";
        public Transform InteractionPoint => _interactionPoint != null ? _interactionPoint : transform;
        public float InteractionRange => _interactionRange;

        private void Awake()
        {
            TryGetComponent(out _elementState);
            ApplyState(false);
        }

        private void OnEnable()
        {
            if (_elementState == null) return;
            _elementState.OnElementApplied += HandleElementApplied;
            _elementState.OnElementRemoved += HandleElementRemoved;
        }

        private void OnDisable()
        {
            if (_elementState == null) return;
            _elementState.OnElementApplied -= HandleElementApplied;
            _elementState.OnElementRemoved -= HandleElementRemoved;
        }

        private void HandleElementApplied(ElementTag tag)
        {
            if (tag != ElementTag.Fire) return;
            if (!_isLit)
                ApplyState(true);
        }

        private void HandleElementRemoved(ElementTag tag)
        {
            if (tag != ElementTag.Fire) return;
            if (_isLit)
                ApplyState(false);
        }

        public bool CanInteract()
        {
            return !_isLit;
        }

        public void Interact(GameObject actor)
        {
            if (_isLit) return;

            var inventory = actor.GetComponent<PlayerInventory>();
            if (inventory == null)
                inventory = actor.GetComponentInParent<PlayerInventory>();

            if (inventory == null) return;

            if (HasFlint(inventory))
            {
                // Кремень — многоразовый инструмент, не расходуем
                Ignite();
            }
            else
            {
                SpeechBubbleManager.Say("Нужно чем-то поджечь...");
            }
        }

        /// <summary>Зажигает костёр извне (например, от факела через WorldItemInteraction).</summary>
        public void Ignite()
        {
            if (_isLit) return;

            // Синхронизируем с системой стихий — HandleElementApplied вызовет ApplyState(true)
            if (_elementState != null)
                _elementState.ApplyElement(ElementTag.Fire);
            else
                ApplyState(true);
        }

        private static bool HasFlint(PlayerInventory inventory)
        {
            var items = inventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Item != null && items[i].Item.Id == "flint")
                    return true;
            }
            return false;
        }

        private void ApplyState(bool lit)
        {
            _isLit = lit;

            if (_unlitVisual != null) _unlitVisual.SetActive(!lit);
            if (_litVisual != null) _litVisual.SetActive(lit);

            ApplyLight(lit);

            if (lit && _restZoneTrigger == null)
                CreateRestZone();
        }

        private void ApplyLight(bool lit)
        {
            if (_fireLight == null) return;

            _fireLight.enabled = lit;

            if (!lit) return;

            if (_config != null)
            {
                _fireLight.color = _config.LightColor;
                _fireLight.intensity = _config.LightIntensity;
                _fireLight.range = _config.LightRange;
            }
            else
            {
                _fireLight.color = new Color(1f, 0.7f, 0.3f);
                _fireLight.intensity = 2f;
                _fireLight.range = 8f;
            }
        }

        private void CreateRestZone()
        {
            float radius = _config != null ? _config.RestZoneRadius : 3f;

            var triggerGo = new GameObject("RestZone");
            triggerGo.transform.SetParent(transform);
            triggerGo.transform.localPosition = Vector3.zero;
            triggerGo.tag = "RestZone";

            _restZoneTrigger = triggerGo.AddComponent<SphereCollider>();
            _restZoneTrigger.isTrigger = true;
            _restZoneTrigger.radius = radius;
        }
    }
}
