using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.UI
{
    /// <summary>A speech plate: paper background, text inside, sized to the text when it is set (docs/demo/unity-architecture.md §2.6).</summary>
    public sealed class BubbleWidget
    {
        public RectTransform Root { get; }
        public Image Background { get; }
        public TextMeshProUGUI Label { get; }
        private readonly float _maxWidth;
        private readonly float _padding;

        public BubbleWidget(RectTransform root, Image background, TextMeshProUGUI label, float maxWidth, float padding)
        {
            Root = root;
            Background = background;
            Label = label;
            _maxWidth = maxWidth;
            _padding = padding;
        }

        public bool Visible => Root.gameObject.activeSelf;

        /// <summary>The text shown, or null while the bubble is hidden.</summary>
        public string Text => Visible ? Label.text : null;

        /// <summary>Shows the text (re-measures only when it changed) and the plate fits around it.</summary>
        public void Show(string text)
        {
            if (!Root.gameObject.activeSelf) Root.gameObject.SetActive(true);
            if (Label.text == text) return;
            Label.text = text;
            var v = Label.GetPreferredValues(text, _maxWidth - 2f * _padding, 0f);
            Root.sizeDelta = new Vector2(Mathf.Min(_maxWidth, v.x + 2f * _padding), v.y + 2f * _padding);
        }

        public void Hide()
        {
            if (Root.gameObject.activeSelf) Root.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Building blocks of the interface made in code on a <see cref="UiLook"/> (no prefabs): text, plate, icon, button, slot, bubble.
    /// Everything that needs no clicks has <c>raycastTarget = false</c>. Without a look the plate and the colours are the built-in ones and the
    /// font is TextMeshPro's default.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color DefaultInk = new Color(0.20f, 0.14f, 0.09f, 1f);
        public static readonly Color DefaultPaper = new Color(0.93f, 0.88f, 0.76f, 0.96f);

        public static Sprite PlateSprite(UiLook look) => look != null && look.Paper != null ? look.Paper : PlaceholderSprites.PaperPlate;

        public static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float margin = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        public static TextMeshProUGUI MakeText(Transform parent, string name, UiLook look, string text, float size, TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null)
        {
            var rt = MakeRect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (look != null && look.Font != null) t.font = look.Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? (look != null ? look.Ink : DefaultInk);
            t.raycastTarget = false;
            return t;
        }

        public static Image MakePlate(Transform parent, string name, UiLook look, Color? tint = null, bool raycast = false)
        {
            var rt = MakeRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = PlateSprite(look);
            img.type = Image.Type.Sliced;
            img.color = tint ?? (look != null ? look.PaperColor : DefaultPaper);
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>An icon: the registry's sprite, or a paper plate with the first letter when the picture is missing.</summary>
        public static RectTransform MakeIcon(Transform parent, string name, IconRegistry icons, string id, UiLook look, float size)
        {
            var sprite = icons != null ? icons.Get(id) : null;
            if (sprite != null)
            {
                var rt = MakeRect(parent, name);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                rt.sizeDelta = new Vector2(size, size);
                return rt;
            }
            var plate = MakePlate(parent, name, look);
            plate.rectTransform.sizeDelta = new Vector2(size, size);
            string letter = string.IsNullOrEmpty(id) ? "?" : id.Substring(0, 1).ToUpperInvariant();
            var label = MakeText(plate.transform, "Letter", look, letter, size * 0.6f);
            Stretch(label.rectTransform);
            return plate.rectTransform;
        }

        /// <summary>A button: a paper plate that takes clicks, with a text label.</summary>
        public static Button MakeButton(Transform parent, string name, UiLook look, string label, Vector2 size, Action onClick)
        {
            var plate = MakePlate(parent, name, look, null, true);
            plate.rectTransform.sizeDelta = size;
            var text = MakeText(plate.transform, "Label", look, label, look != null ? look.TextSmall : 34f);
            Stretch(text.rectTransform, 8f);
            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>An inventory cell: a plate that takes clicks, an icon area and a count label (hidden while empty or 1).</summary>
        public static Image MakeSlot(Transform parent, string name, UiLook look, float size)
        {
            var plate = MakePlate(parent, name, look, null, true);
            plate.rectTransform.sizeDelta = new Vector2(size, size);
            return plate;
        }

        public static BubbleWidget MakeBubble(Transform parent, string name, UiLook look, float maxWidth, float fontSize = 0f)
        {
            float pad = look != null ? look.Padding : 24f;
            var plate = MakePlate(parent, name, look);
            var root = plate.rectTransform;
            root.pivot = new Vector2(0.5f, 0f);
            var label = MakeText(plate.transform, "Text", look, "", fontSize > 0f ? fontSize : (look != null ? look.TextNormal : 44f));
            Stretch(label.rectTransform, pad);
            plate.gameObject.SetActive(false);
            return new BubbleWidget(root, plate, label, maxWidth, pad);
        }
    }
}
