using UnityEngine;
using UnityEngine.UI;

namespace JieNiYiMiao
{
    /// <summary>
    /// UGUI 工厂：所有 UI 都由代码创建，场景里不需要任何预制体或 Inspector 引用。
    /// 只用 Text + Image + Button，不用 TextMeshPro（本项目未安装该包）。
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color Ink = new Color(0.11f, 0.12f, 0.16f, 1f);
        public static readonly Color Paper = new Color(0.97f, 0.95f, 0.91f, 1f);
        public static readonly Color Muted = new Color(0.45f, 0.47f, 0.55f, 1f);
        public static readonly Color Amber = new Color(0.87f, 0.60f, 0.26f, 1f);
        public static readonly Color RewindTint = new Color(0.60f, 0.45f, 0.95f, 1f);

        /// <summary>创建一个铺满父级的面板或装饰块。</summary>
        public static RectTransform MakePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            go.GetComponent<UnityEngine.UI.Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        public static UnityEngine.UI.Image MakeImage(Transform parent, string name, Color color, Vector2 size, Vector2 anchor, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.color = color;
            return img;
        }

        public static UnityEngine.UI.Text MakeText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            Color color,
            TextAnchor alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var text = go.GetComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        /// <summary>
        /// 创建一个四态按钮：正常 / 悬停 / 按下 / 禁用。手册要求按钮必须有这三种反馈状态。
        /// </summary>
        public static Button MakeButton(
            Transform parent,
            string name,
            string label,
            Vector2 size,
            Vector2 anchor,
            Vector2 offset,
            Color baseColor,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;

            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.white;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Lighten(baseColor, 0.16f);
            colors.pressedColor = Darken(baseColor, 0.22f);
            colors.selectedColor = Lighten(baseColor, 0.08f);
            colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.28f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var text = MakeText(go.transform, "Label", label, 22, new Color(1f, 0.99f, 0.96f), TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            text.raycastTarget = false;

            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            return button;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var text = button.GetComponentInChildren<UnityEngine.UI.Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        /// <summary>把 RectTransform 拉满父级。</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>悬浮在画布上方的容器（不接管点击）。</summary>
        public static RectTransform MakeOverlay(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            return rt;
        }

        /// <summary>保证场景里有 EventSystem，否则 UGUI 按钮点不动。</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        public static Color Lighten(Color c, float amount)
        {
            return new Color(Mathf.Clamp01(c.r + amount), Mathf.Clamp01(c.g + amount), Mathf.Clamp01(c.b + amount), c.a);
        }

        public static Color Darken(Color c, float amount)
        {
            return new Color(Mathf.Clamp01(c.r - amount), Mathf.Clamp01(c.g - amount), Mathf.Clamp01(c.b - amount), c.a);
        }

        public static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }
    }
}
