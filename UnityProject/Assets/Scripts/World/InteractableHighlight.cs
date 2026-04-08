using UnityEngine;

namespace ZeldaDaughter.World
{
    public class InteractableHighlight : MonoBehaviour
    {
        [SerializeField] private float _highlightRange = 5f;
        [SerializeField] private Color _highlightColor = new Color(1f, 0.9f, 0.3f, 1f);
        [SerializeField] private float _emissionIntensity = 0.5f;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _propBlock;
        private Transform _player;
        private bool _isHighlighted;
        private int _frameOffset;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _propBlock = new MaterialPropertyBlock();
            // Разбить объекты по кадрам, чтобы не все обновлялись в один кадр
            _frameOffset = GetInstanceID() & 4;
        }

        private void Start()
        {
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
                _player = playerGO.transform;
        }

        private void Update()
        {
            if (_player == null)
                return;

            // Проверять каждые 5 кадров для экономии CPU
            if ((Time.frameCount + _frameOffset) % 5 != 0)
                return;

            float dist = Vector3.Distance(transform.position, _player.position);

            if (dist < _highlightRange && !_isHighlighted)
            {
                SetEmission(_highlightColor * _emissionIntensity);
                _isHighlighted = true;
            }
            else if (dist >= _highlightRange && _isHighlighted)
            {
                SetEmission(Color.black);
                _isHighlighted = false;
            }
        }

        private void OnDisable()
        {
            if (_isHighlighted)
            {
                SetEmission(Color.black);
                _isHighlighted = false;
            }
        }

        private void SetEmission(Color color)
        {
            foreach (var r in _renderers)
            {
                r.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(EmissionColor, color);
                r.SetPropertyBlock(_propBlock);
            }
        }
    }
}
