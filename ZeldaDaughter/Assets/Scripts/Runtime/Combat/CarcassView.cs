using UnityEngine;
using ZeldaDaughter.Core.Loot;
using ZeldaDaughter.Game;
using ZeldaDaughter.Input;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.Combat
{
    /// <summary>
    /// A dead enemy lying where it fell (D-13): the same figure turned on its side (the set's «down» frame once D-09 draws it), a little darker
    /// once the minimum was taken. Tappable until the knife takes the rest and the core says the carcass is gone.
    /// </summary>
    public sealed class CarcassView : MonoBehaviour
    {
        private static readonly Color Taken = new Color(0.82f, 0.78f, 0.74f);

        [SerializeField] private BillboardSprite _sprite;
        [SerializeField] private Tappable _tappable;
        private Carcass _carcass;
        private string _id;
        private CarcassState _shown = (CarcassState)(-1);

        public string CarcassId => _id;
        public BillboardSprite Sprite => _sprite;

        public void Configure(GameSession session, Carcass carcass, BillboardSprite sprite, Tappable tappable)
        {
            _carcass = carcass;
            _id = carcass.Id;
            _sprite = sprite;
            _tappable = tappable;
            var cam = sprite.Camera != null ? sprite.Camera.transform.right : Vector3.right;
            sprite.FaceDirection(StableHash(carcass.Id) % 2 == 0 ? cam : -cam); // lies one way or the other
            sprite.SetPose(new BillboardPose { Lying = true });
        }

        private void Update()
        {
            if (_carcass == null || _carcass.State == _shown) return;
            _shown = _carcass.State;
            _sprite.SetTint(_shown == CarcassState.Looted ? Taken : Color.white);
        }

        public void Dispose(WorldIndex index)
        {
            if (_tappable != null)
            {
                _tappable.Enabled = false;
                index.UnregisterDynamic(_tappable);
            }
            Destroy(gameObject);
        }

        private static int StableHash(string s)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
                return h & 0x7fffffff;
            }
        }
    }
}
