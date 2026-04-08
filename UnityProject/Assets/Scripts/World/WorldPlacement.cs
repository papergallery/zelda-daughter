using UnityEngine;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.World
{
    public class WorldPlacement : MonoBehaviour
    {
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private Color _validColor = new(0.3f, 0.9f, 0.3f, 0.5f);
        [SerializeField] private Color _invalidColor = new(0.9f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private float _maxPlaceDistance = 10f;

        public static event System.Action<ItemData, GameObject> OnItemPlaced;

        private PlayerInventory _inventory;
        private Camera _mainCamera;
        private GameObject _ghost;
        private ItemData _currentItem;
        private int _sourceSlot;
        private bool _isPlacing;
        private bool _isValidPosition;
        private Renderer[] _ghostRenderers;
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _inventory = FindObjectOfType<PlayerInventory>();
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            InventoryDragHandler.OnDropOutsidePanel += StartPlacing;
        }

        private void OnDisable()
        {
            InventoryDragHandler.OnDropOutsidePanel -= StartPlacing;
        }

        private void Update()
        {
            if (!_isPlacing || _ghost == null) return;

            Vector2 pointerPos;
            bool pointerUp;

#if UNITY_EDITOR
            pointerPos = UnityEngine.Input.mousePosition;
            pointerUp = UnityEngine.Input.GetMouseButtonDown(0);
#else
            if (UnityEngine.Input.touchCount > 0)
            {
                var touch = UnityEngine.Input.GetTouch(0);
                pointerPos = touch.position;
                pointerUp = touch.phase == TouchPhase.Began;
            }
            else
            {
                CancelPlacing();
                return;
            }
#endif

            // Raycast на ground
            var ray = _mainCamera.ScreenPointToRay(pointerPos);
            if (Physics.Raycast(ray, out var hit, 100f, _groundLayer))
            {
                _ghost.transform.position = hit.point;

                // Валидация: поверхность почти горизонтальна
                _isValidPosition = hit.normal.y > 0.7f;

                // Проверка дистанции от игрока
                if (_inventory != null)
                {
                    float dist = Vector3.Distance(hit.point, _inventory.transform.position);
                    if (dist > _maxPlaceDistance)
                        _isValidPosition = false;
                }

                // Overlap check (не в стене/здании)
                if (_isValidPosition)
                {
                    var colliders = Physics.OverlapSphere(hit.point, 0.3f);
                    for (int i = 0; i < colliders.Length; i++)
                    {
                        if (colliders[i].CompareTag("Building") || colliders[i].CompareTag("Water"))
                        {
                            _isValidPosition = false;
                            break;
                        }
                    }
                }

                UpdateGhostColor(_isValidPosition ? _validColor : _invalidColor);
            }

            // Тап для подтверждения
            if (pointerUp)
            {
                if (_isValidPosition)
                    ConfirmPlacement();
                else
                    SpeechBubbleManager.Say("Сюда нельзя поставить...");
            }
        }

        private void StartPlacing(ItemData item)
        {
            if (item == null || item.WorldPrefab == null)
            {
                SpeechBubbleManager.Say("Это нельзя поставить.");
                return;
            }

            _currentItem = item;
            _isPlacing = true;
            _isValidPosition = false;

            // Найти слот с этим предметом
            if (_inventory != null)
            {
                for (int i = 0; i < _inventory.SlotCount; i++)
                {
                    var slot = _inventory.GetSlot(i);
                    if (slot.Item == item)
                    {
                        _sourceSlot = i;
                        break;
                    }
                }
            }

            // Создать призрак
            _ghost = Instantiate(item.WorldPrefab);
            _ghost.name = "PlacementGhost";

            // Отключить коллайдеры и скрипты на призраке
            foreach (var col in _ghost.GetComponentsInChildren<Collider>())
                col.enabled = false;
            foreach (var mb in _ghost.GetComponentsInChildren<MonoBehaviour>())
                mb.enabled = false;

            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>();
            UpdateGhostColor(_invalidColor);

            // Закрыть инвентарь
            var panel = FindObjectOfType<InventoryPanel>();
            if (panel != null) panel.Close();
        }

        private void ConfirmPlacement()
        {
            if (_ghost == null || _currentItem == null) return;

            var position = _ghost.transform.position;
            var rotation = _ghost.transform.rotation;

            Destroy(_ghost);
            _ghost = null;

            // Создать реальный объект
            var placed = Instantiate(_currentItem.WorldPrefab, position, rotation);
            var placeable = placed.GetComponent<PlaceableObject>();
            if (placeable == null)
                placeable = placed.AddComponent<PlaceableObject>();
            placeable.Setup(_currentItem);

            // Убрать из инвентаря
            if (_inventory != null)
                _inventory.RemoveFromSlot(_sourceSlot, 1);

            OnItemPlaced?.Invoke(_currentItem, placed);
            _isPlacing = false;
            _currentItem = null;
        }

        private void CancelPlacing()
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }
            _isPlacing = false;
            _currentItem = null;
        }

        private void UpdateGhostColor(Color color)
        {
            if (_ghostRenderers == null) return;
            _mpb.SetColor("_BaseColor", color);
            for (int i = 0; i < _ghostRenderers.Length; i++)
                _ghostRenderers[i].SetPropertyBlock(_mpb);
        }
    }
}
