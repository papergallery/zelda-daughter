using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Game;
using ZeldaDaughter.Hero;

namespace ZeldaDaughter.UI
{
    /// <summary>What <see cref="WindowStack"/> shows: an id, the root rectangle to put on the Windows layer, and notices of opening/closing.</summary>
    public interface IWindow
    {
        string Id { get; }
        RectTransform Root { get; }
        void OnOpened();
        void OnClosed();
    }

    /// <summary>
    /// One window at a time (docs/demo/unity-architecture.md §3): opening it dims the world, stops the hero (<c>Locked</c>, walk cancelled),
    /// and a tap beside the window closes it — the dim behind it takes the touch, so it never reaches the hero. The window is any
    /// <see cref="IWindow"/> (composition, no base class); the world keeps running under it.
    /// </summary>
    public sealed class WindowStack : MonoBehaviour
    {
        [SerializeField] private GameSession _session;
        [SerializeField] private SessionUI _ui;
        [SerializeField] private HeroController _hero;

        private Image _backdrop;
        private Button _backdropButton;
        private IWindow _current;
        private bool _suspended;

        public void Configure(GameSession session, SessionUI ui, HeroController hero)
        {
            _session = session;
            _ui = ui;
            _hero = hero;
        }

        public string CurrentId => _current?.Id;
        public bool IsOpen(string id) => _current != null && _current.Id == id;
        public bool AnyOpen => _current != null;

        /// <summary>Opens the window, closing the one that is open. The window's own anchors and size are kept; its parent becomes the Windows layer.</summary>
        public void Open(IWindow window)
        {
            if (_current != null) Close();
            EnsureBackdrop();
            _current = window;
            _suspended = false;
            window.Root.SetParent(_ui.Windows, false);
            window.Root.gameObject.SetActive(true);
            _backdrop.gameObject.SetActive(true);
            _backdrop.transform.SetAsFirstSibling();
            _hero.Locked = true;
            window.OnOpened();
            ZdLog.Info("Window", "open " + window.Id);
            _session.Events.RaiseWindowOpened(window.Id);
        }

        /// <summary>Closes the open window (if <paramref name="id"/> is given, only that one).</summary>
        public void Close(string id = null)
        {
            if (_current == null || (id != null && _current.Id != id)) return;
            var w = _current;
            _current = null;
            _suspended = false;
            w.OnClosed();
            w.Root.gameObject.SetActive(false);
            if (_backdrop != null) _backdrop.gameObject.SetActive(false);
            _hero.Locked = false;
            ZdLog.Info("Window", "close " + w.Id);
            _session.Events.RaiseWindowClosed(w.Id);
        }

        public void CloseAll() => Close();

        /// <summary>The window hides for a moment (an item is dragged out of it) but stays "open": the hero stays stopped, the dim goes.</summary>
        public void Suspend()
        {
            if (_current == null || _suspended) return;
            _suspended = true;
            _current.Root.gameObject.SetActive(false);
            _backdrop.gameObject.SetActive(false);
        }

        public void Resume()
        {
            if (_current == null || !_suspended) return;
            _suspended = false;
            _current.Root.gameObject.SetActive(true);
            _backdrop.gameObject.SetActive(true);
            _backdrop.transform.SetAsFirstSibling();
        }

        private void EnsureBackdrop()
        {
            if (_backdrop != null) return;
            var look = _ui.Look;
            var go = UiKit.MakeRect(_ui.Windows, "Backdrop");
            UiKit.Stretch(go);
            _backdrop = go.gameObject.AddComponent<Image>();
            _backdrop.color = look != null ? look.Shade : new Color(0.05f, 0.04f, 0.03f, 0.55f);
            _backdrop.raycastTarget = true;
            _backdropButton = go.gameObject.AddComponent<Button>();
            _backdropButton.transition = Selectable.Transition.None;
            _backdropButton.onClick.AddListener(() => Close());
            go.gameObject.SetActive(false);
        }
    }
}
