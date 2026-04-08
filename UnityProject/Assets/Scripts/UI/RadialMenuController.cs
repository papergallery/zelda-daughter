using System.Collections;
using UnityEngine;
using ZeldaDaughter.Input;

namespace ZeldaDaughter.UI
{
    public class RadialMenuController : MonoBehaviour
    {
        [SerializeField] private RadialMenuConfig _config;
        [SerializeField] private RectTransform _menuRoot;
        [SerializeField] private RadialMenuSector[] _sectors;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Transform _playerTransform;

        public static event System.Action<int> OnSectorSelected;
        public static event System.Action OnMenuClosed;

        private Camera _mainCamera;
        private bool _isOpen;
        private int _hoveredSector = -1;
        private float _savedTimeScale;
        private Vector2 _menuScreenPos;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            _mainCamera = Camera.main;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
            if (_menuRoot != null)
                _menuRoot.gameObject.SetActive(false);
        }

        private void Start()
        {
            SetupSectors();
        }

        private void OnEnable()
        {
            GestureDispatcher.OnLongPressStart += Open;
            GestureDispatcher.OnLongPressEnd += Close;
        }

        private void OnDisable()
        {
            GestureDispatcher.OnLongPressStart -= Open;
            GestureDispatcher.OnLongPressEnd -= Close;
        }

        private void Update()
        {
            if (!_isOpen) return;

            Vector2 pointerPos;
#if UNITY_EDITOR
            pointerPos = UnityEngine.Input.mousePosition;
#else
            if (UnityEngine.Input.touchCount > 0)
                pointerPos = UnityEngine.Input.GetTouch(0).position;
            else
                return;
#endif

            UpdateHoveredSector(pointerPos);
        }

        private void SetupSectors()
        {
            if (_sectors == null || _config == null) return;

            for (int i = 0; i < _sectors.Length; i++)
            {
                string label = i < _config.SectorLabels.Length ? _config.SectorLabels[i] : "";
                Sprite icon = _config.SectorIcons != null && i < _config.SectorIcons.Length
                    ? _config.SectorIcons[i] : null;
                _sectors[i].Setup(i, label, icon);

                // Расположить сектор по кругу, начиная сверху
                float angle = (360f / _sectors.Length) * i - 90f;
                float rad = angle * Mathf.Deg2Rad;
                var pos = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * _config.MenuRadius;
                _sectors[i].GetComponent<RectTransform>().anchoredPosition = pos;
            }
        }

        private void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            if (_playerTransform != null && _mainCamera != null)
            {
                _menuScreenPos = _mainCamera.WorldToScreenPoint(_playerTransform.position);
                _menuRoot.position = _menuScreenPos;
            }

            _menuRoot.gameObject.SetActive(true);

            if (_config != null && _config.PauseOnOpen)
            {
                _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            _hoveredSector = -1;
            StopAllCoroutines();
            StartCoroutine(AnimateOpen());
        }

        private void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            if (_hoveredSector >= 0)
                OnSectorSelected?.Invoke(_hoveredSector);

            if (_config != null && _config.PauseOnOpen)
                Time.timeScale = _savedTimeScale;

            StopAllCoroutines();
            StartCoroutine(AnimateClose());
            OnMenuClosed?.Invoke();
        }

        private void UpdateHoveredSector(Vector2 pointerPos)
        {
            if (_sectors == null || _sectors.Length == 0) return;

            Vector2 delta = pointerPos - _menuScreenPos;

            // Если палец слишком близко к центру — ничего не выбрано
            if (delta.magnitude < 30f)
            {
                SetHovered(-1);
                return;
            }

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            // Сдвиг: 0-й сектор начинается сверху
            angle = (angle + 90f) % 360f;

            float sectorSize = 360f / _sectors.Length;
            int index = Mathf.Clamp(Mathf.FloorToInt(angle / sectorSize), 0, _sectors.Length - 1);

            SetHovered(index);
        }

        private void SetHovered(int index)
        {
            if (_hoveredSector == index) return;

            if (_hoveredSector >= 0 && _hoveredSector < _sectors.Length)
                _sectors[_hoveredSector].SetHighlight(false);

            _hoveredSector = index;

            if (_hoveredSector >= 0 && _hoveredSector < _sectors.Length)
                _sectors[_hoveredSector].SetHighlight(true);
        }

        private IEnumerator AnimateOpen()
        {
            float duration = _config != null ? _config.OpenAnimDuration : 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                if (_canvasGroup != null) _canvasGroup.alpha = t;
                _menuRoot.localScale = Vector3.one * t;

                yield return null;
            }

            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            _menuRoot.localScale = Vector3.one;
        }

        private IEnumerator AnimateClose()
        {
            float duration = _config != null ? _config.CloseAnimDuration : 0.15f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = 1f - Mathf.SmoothStep(0f, 1f, elapsed / duration);

                if (_canvasGroup != null) _canvasGroup.alpha = t;
                _menuRoot.localScale = Vector3.one * t;

                yield return null;
            }

            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            _menuRoot.gameObject.SetActive(false);
        }
    }
}
