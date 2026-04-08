using System.Collections;
using UnityEngine;

namespace ZeldaDaughter.Combat
{
    public class EnemyAttackSignal : MonoBehaviour
    {
        [SerializeField] private Renderer _renderer;
        [SerializeField] private Color _warningColor = Color.red;
        [SerializeField] private int _flashCount = 3;

        private Color _originalColor;
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_mpb);
                _originalColor = _mpb.GetColor("_BaseColor");
                if (_originalColor == default) _originalColor = Color.white;
            }
        }

        public void ShowWindup(float duration)
        {
            StopAllCoroutines();
            StartCoroutine(WindupCoroutine(duration));
        }

        private IEnumerator WindupCoroutine(float duration)
        {
            if (_renderer == null) yield break;

            float interval = duration / (_flashCount * 2);

            for (int i = 0; i < _flashCount; i++)
            {
                SetColor(_warningColor);
                yield return new WaitForSeconds(interval);
                SetColor(_originalColor);
                yield return new WaitForSeconds(interval);
            }
        }

        private void SetColor(Color color)
        {
            if (_renderer == null) return;
            _mpb.SetColor("_BaseColor", color);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
