using UnityEngine;
using ZeldaDaughter.Audio;
using ZeldaDaughter.Rendering;
using ZeldaDaughter.UI;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Game
{
    /// <summary>
    /// The registries of the art (docs/demo/unity-architecture.md §2.5) in one place on the Game object, set by SceneBuilder, so a presenter
    /// gets what it draws with from <c>art</c> in its <c>Configure</c> instead of searching for assets. Any of them may be null (placeholders then).
    /// </summary>
    public sealed class ArtAssets : MonoBehaviour
    {
        [SerializeField] private UiLook _ui;
        [SerializeField] private SpriteLook _sprites;
        [SerializeField] private CharacterRegistry _characters;
        [SerializeField] private IconRegistry _itemIcons;
        [SerializeField] private IconRegistry _talkIcons;
        [SerializeField] private SoundRegistry _sounds;
        [SerializeField] private FxRegistry _fx;

        public UiLook Ui => _ui;
        public SpriteLook Sprites => _sprites;
        public CharacterRegistry Characters => _characters;
        public IconRegistry ItemIcons => _itemIcons;
        public IconRegistry TalkIcons => _talkIcons;
        public SoundRegistry Sounds => _sounds;
        public FxRegistry Fx => _fx;

        public void Configure(UiLook ui, SpriteLook sprites, CharacterRegistry characters, IconRegistry itemIcons, IconRegistry talkIcons, SoundRegistry sounds, FxRegistry fx)
        {
            _ui = ui;
            _sprites = sprites;
            _characters = characters;
            _itemIcons = itemIcons;
            _talkIcons = talkIcons;
            _sounds = sounds;
            _fx = fx;
        }
    }
}
