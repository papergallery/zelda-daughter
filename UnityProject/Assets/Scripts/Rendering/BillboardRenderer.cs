using UnityEngine;

namespace ZeldaDaughter.Rendering
{
    /// <summary>
    /// Rotates a sprite quad to always face the camera.
    /// Works with both perspective and orthographic isometric cameras.
    /// </summary>
    public class BillboardRenderer : MonoBehaviour
    {
        [SerializeField] private bool _lockYRotation = true;

        private Camera _cam;

        private void Awake()
        {
            _cam = Camera.main;
        }

        private void LateUpdate()
        {
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            if (_lockYRotation)
            {
                // For isometric view: only rotate around Y so sprite stays vertical
                // but faces the camera horizontally
                Vector3 camForward = _cam.transform.forward;
                camForward.y = 0f;

                if (camForward.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(-camForward, Vector3.up);
            }
            else
            {
                // Full billboard: always face camera including tilt
                transform.LookAt(
                    transform.position + _cam.transform.rotation * Vector3.forward,
                    _cam.transform.rotation * Vector3.up
                );
            }
        }

        public void SetLockYRotation(bool locked)
        {
            _lockYRotation = locked;
        }
    }
}
