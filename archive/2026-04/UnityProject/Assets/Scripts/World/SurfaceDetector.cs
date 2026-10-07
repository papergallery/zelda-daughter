using UnityEngine;

namespace ZeldaDaughter.World
{
    public enum SurfaceType { Grass, Stone, Dirt, Wood, Water, Sand }

    /// <summary>
    /// Determines the surface type beneath the player via downward raycast.
    /// Fires OnSurfaceChanged when the surface type changes.
    /// </summary>
    public class SurfaceDetector : MonoBehaviour
    {
        public static event System.Action<SurfaceType> OnSurfaceChanged;

        public SurfaceType CurrentSurface { get; private set; }

        private SurfaceType _previousSurface;
        private int _frameCounter;
        private const int CheckInterval = 5;
        private const float RayOriginOffset = 0.1f;
        private const float RayLength = 2f;

        private void Update()
        {
            _frameCounter++;
            if (_frameCounter < CheckInterval)
                return;

            _frameCounter = 0;
            DetectSurface();
        }

        private void DetectSurface()
        {
            var origin = transform.position + Vector3.up * RayOriginOffset;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, RayLength))
                CurrentSurface = TagToSurfaceType(hit.collider.tag);
            else
                CurrentSurface = SurfaceType.Grass;

            if (CurrentSurface != _previousSurface)
            {
                _previousSurface = CurrentSurface;
                OnSurfaceChanged?.Invoke(CurrentSurface);
            }
        }

        private static SurfaceType TagToSurfaceType(string tag)
        {
            if (tag.Equals("Stone"))  return SurfaceType.Stone;
            if (tag.Equals("Wood"))   return SurfaceType.Wood;
            if (tag.Equals("Water"))  return SurfaceType.Water;
            if (tag.Equals("Dirt"))   return SurfaceType.Dirt;
            if (tag.Equals("Sand"))   return SurfaceType.Sand;
            return SurfaceType.Grass;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var origin = transform.position + Vector3.up * RayOriginOffset;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, origin + Vector3.down * RayLength);
        }
#endif
    }
}
