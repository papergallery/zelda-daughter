using UnityEngine;

namespace ZeldaDaughter.UI
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Radial Menu Config", fileName = "RadialMenuConfig")]
    public class RadialMenuConfig : ScriptableObject
    {
        [Header("Long Press")]
        [SerializeField] private float _holdTime = 0.5f;
        [SerializeField] private float _maxDrift = 20f;

        [Header("Menu Layout")]
        [SerializeField] private float _menuRadius = 150f;
        [SerializeField] private string[] _sectorLabels = { "Инвентарь", "Карта", "Блокнот" };
        [SerializeField] private Sprite[] _sectorIcons;

        [Header("Animation")]
        [SerializeField] private float _openAnimDuration = 0.2f;
        [SerializeField] private float _closeAnimDuration = 0.15f;

        [Header("Gameplay")]
        [SerializeField] private bool _pauseOnOpen = true;

        public float HoldTime => _holdTime;
        public float MaxDrift => _maxDrift;
        public float MenuRadius => _menuRadius;
        public string[] SectorLabels => _sectorLabels;
        public Sprite[] SectorIcons => _sectorIcons;
        public float OpenAnimDuration => _openAnimDuration;
        public float CloseAnimDuration => _closeAnimDuration;
        public bool PauseOnOpen => _pauseOnOpen;
    }
}
