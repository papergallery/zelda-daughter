using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.NPC;
using ZeldaDaughter.Save;

namespace ZeldaDaughter.World
{
    public class MapManager : MonoBehaviour, ISaveable
    {
        [SerializeField] private MapRegionData[] _regions;
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private PlayerInventory _playerInventory;

        private readonly HashSet<string> _revealedMarkers = new();

        /// <summary>Вызывается при открытии нового маркера на карте.</summary>
        public static event Action<string> OnMarkerRevealed;

        /// <summary>Запрос на открытие маркера извне (например из DialogueEffectExecutor).</summary>
        public static event Action<string, string> OnMapMarkerRequested;

        public string SaveId => "map_manager";

        private void OnEnable()
        {
            SaveManager.Register(this);
            OnMapMarkerRequested += HandleMapMarkerRequested;
            DialogueEffectExecutor.OnMapMarkerRequested += RevealMarker;
        }

        private void OnDisable()
        {
            SaveManager.Unregister(this);
            OnMapMarkerRequested -= HandleMapMarkerRequested;
            DialogueEffectExecutor.OnMapMarkerRequested -= RevealMarker;
        }

        /// <summary>
        /// Возвращает регион, в котором находится игрок. Null если ни один не подходит.
        /// </summary>
        public MapRegionData GetCurrentRegion()
        {
            if (_playerTransform == null || _regions == null)
                return null;

            Vector3 pos = _playerTransform.position;
            for (int i = 0; i < _regions.Length; i++)
            {
                if (_regions[i] != null && _regions[i].ContainsPoint(pos))
                    return _regions[i];
            }
            return null;
        }

        /// <summary>
        /// Проверяет, есть ли у игрока карта для данного региона.
        /// </summary>
        public bool HasMapForRegion(string regionId)
        {
            MapRegionData region = FindRegion(regionId);
            if (region == null)
                return false;

            if (string.IsNullOrEmpty(region.RequiredMapItemId))
                return true;

            var inventory = GetInventory();
            if (inventory == null)
                return false;

            for (int i = 0; i < inventory.Items.Count; i++)
            {
                var stack = inventory.Items[i];
                if (stack.Item != null && stack.Item.Id == region.RequiredMapItemId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Открывает маркер. Если маркер новый — стреляет событием OnMarkerRevealed.
        /// </summary>
        public void RevealMarker(string markerId)
        {
            if (_revealedMarkers.Add(markerId))
                OnMarkerRevealed?.Invoke(markerId);
        }

        public bool IsMarkerRevealed(string markerId)
        {
            return _revealedMarkers.Contains(markerId);
        }

        /// <summary>
        /// Возвращает список открытых маркеров для региона.
        /// </summary>
        public List<MapMarkerData> GetRevealedMarkersForRegion(MapRegionData region)
        {
            var result = new List<MapMarkerData>();
            if (region?.Markers == null)
                return result;

            for (int i = 0; i < region.Markers.Length; i++)
            {
                var marker = region.Markers[i];
                if (_revealedMarkers.Contains(marker.markerId))
                    result.Add(marker);
            }
            return result;
        }

        private void HandleMapMarkerRequested(string regionId, string markerId)
        {
            RevealMarker(markerId);
        }

        private MapRegionData FindRegion(string regionId)
        {
            if (_regions == null) return null;
            for (int i = 0; i < _regions.Length; i++)
            {
                if (_regions[i] != null && _regions[i].RegionId == regionId)
                    return _regions[i];
            }
            return null;
        }

        private PlayerInventory GetInventory()
        {
            if (_playerInventory != null)
                return _playerInventory;

            // Fallback: поиск через тег Player
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null && playerObj.TryGetComponent(out PlayerInventory inv))
            {
                _playerInventory = inv;
                return inv;
            }
            return null;
        }

        // --- ISaveable ---

        [Serializable]
        private struct SaveData
        {
            public List<string> RevealedMarkers;
        }

        public object CaptureState()
        {
            return new SaveData
            {
                RevealedMarkers = new List<string>(_revealedMarkers)
            };
        }

        public void RestoreState(object state)
        {
            if (state is not SaveData data) return;

            _revealedMarkers.Clear();
            if (data.RevealedMarkers != null)
            {
                for (int i = 0; i < data.RevealedMarkers.Count; i++)
                    _revealedMarkers.Add(data.RevealedMarkers[i]);
            }
        }
    }
}
