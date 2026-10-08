using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Game;
using ZeldaDaughter.Rendering;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// What the D-15 windows share (station, trade, notebook): a paper frame with a title, an item cell, a scrolling column, positions from the
    /// top-left corner, and the reach check for stations and beds. Built on <see cref="UiKit"/>; nothing here knows a rule of the game.
    /// </summary>
    public static class ScreenParts
    {
        /// <summary>Places a rectangle by its top-left corner inside its parent (pixels of the reference canvas, y grows downwards).</summary>
        public static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>A paper window in the middle of the screen with a title; it takes touches (so a tap on it does not reach the dim behind, which closes the window).</summary>
        public static RectTransform Frame(Transform parent, string name, UiLook look, Vector2 size, string title, out TextMeshProUGUI titleLabel)
        {
            var plate = UiKit.MakePlate(parent, name, look, null, true);
            var root = plate.rectTransform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = size;
            float big = look != null ? look.TextBig : 60f;
            titleLabel = UiKit.MakeText(root, "Title", look, title, big);
            TopLeft(titleLabel.rectTransform, 0f, 26f, size.x, big * 1.3f);
            return root;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, UiLook look, string text, float size, float x, float y, float w, float h,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = UiKit.MakeText(parent, name, look, text, size, align);
            TopLeft(t.rectTransform, x, y, w, h);
            return t;
        }

        /// <summary>
        /// An item cell that takes a tap: the icon (or a paper plate with a letter), the name, the count top-right when above 1 and a small line below
        /// (a price). Children take no touches, so the cell's plate is what the finger meets.
        /// </summary>
        public static Button Cell(Transform parent, string name, UiLook look, IconRegistry icons, string itemId, string label, int count, string sub,
            Vector2 size, Action onClick, bool dim = false)
        {
            var tint = dim ? new Color(0.62f, 0.58f, 0.5f, 0.9f) : (look != null ? look.PaperColor : UiKit.DefaultPaper);
            var plate = UiKit.MakePlate(parent, name, look, tint, true);
            plate.rectTransform.sizeDelta = size;
            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            float icon = Mathf.Min(size.x - 24f, size.y * 0.5f);
            var ic = UiKit.MakeIcon(plate.transform, "Icon", icons, itemId, look, icon);
            ic.anchorMin = ic.anchorMax = new Vector2(0.5f, 1f);
            ic.pivot = new Vector2(0.5f, 1f);
            ic.anchoredPosition = new Vector2(0f, -10f);
            ic.sizeDelta = new Vector2(icon, icon);

            float small = look != null ? Mathf.Max(22f, look.TextSmall * 0.72f) : 24f;
            float subH = string.IsNullOrEmpty(sub) ? 0f : small * 1.25f;
            Label(plate.transform, "Name", look, label, small, 4f, 14f + icon, size.x - 8f, size.y - icon - 18f - subH);
            if (count > 1)
            {
                var c = Label(plate.transform, "Count", look, "x" + count, small * 1.15f, size.x - 84f, 6f, 78f, small * 1.4f, TextAlignmentOptions.Right);
                c.fontStyle = FontStyles.Bold;
            }
            if (subH > 0f) Label(plate.transform, "Sub", look, sub, small, 4f, size.y - subH - 6f, size.x - 8f, subH);
            return button;
        }

        /// <summary>A vertical scroll area at a place of its parent; returns the content rectangle (set its height with <see cref="SetContentHeight"/>).</summary>
        public static RectTransform Scroll(Transform parent, string name, float x, float y, float w, float h)
        {
            var viewport = UiKit.MakeRect(parent, name);
            TopLeft(viewport, x, y, w, h);
            var hit = viewport.gameObject.AddComponent<Image>(); // a target for drags; fully clear
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiKit.MakeRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, h);
            var sr = viewport.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = viewport;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40f;
            return content;
        }

        public static void SetContentHeight(RectTransform content, float height)
        {
            var viewport = (RectTransform)content.parent;
            content.sizeDelta = new Vector2(0f, Mathf.Max(height, viewport.rect.height > 0f ? viewport.rect.height : viewport.sizeDelta.y));
            content.anchoredPosition = Vector2.zero;
        }

        /// <summary>Removes the children (hidden at once, destroyed at the end of the frame).</summary>
        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var c = parent.GetChild(i).gameObject;
                c.SetActive(false);
                UnityEngine.Object.Destroy(c);
                parent.GetChild(i).SetParent(null, false);
            }
        }

        /// <summary>The hero is within reach of the thing (the same radius as the tap hints, data/session.json tappableHintRadius).</summary>
        public static bool InReach(GameSession session, GameState g, Component target)
        {
            var h = session.Hero.transform.position;
            var t = target.transform.position;
            float d = new Vector2(h.x - t.x, h.z - t.z).magnitude;
            return g.Data.Session.IsNear(d);
        }

        public static string ItemName(GameState g, string id) => g.Data.Items.TryGetValue(id, out var d) ? d.Name : id;
    }
}
