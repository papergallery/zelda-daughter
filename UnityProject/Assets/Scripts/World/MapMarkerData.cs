using System;
using UnityEngine;

namespace ZeldaDaughter.World
{
    [Serializable]
    public struct MapMarkerData
    {
        public string markerId;
        public Vector2 normalizedPosition;
        public Sprite icon;
        public string label;
        public string description;
    }
}
