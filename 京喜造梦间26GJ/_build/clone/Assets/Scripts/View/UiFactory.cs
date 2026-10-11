using UnityEngine;
using UnityEngine.UI;

namespace CuoGuo
{
    /// <summary>
    /// UGUI 工厂：本工程所有界面与占位美术都由代码创建，场景里没有预制体、没有 Inspector 引用。
    ///
    /// 技术边界（沿用工程基线）：
    /// - 不用 TextMeshPro（本工程未安装该包），文字统一用 UGUI Text + 内置 LegacyRuntime.ttf；
    /// - 不加载任何贴图文件，纯色块由一张 4x4 白图当精灵用；
    /// - 画布为 ScreenSpaceOverlay + CanvasScaler，参考分辨率 1920x1080。
    /// </summary>
    public static class UiFactory
    {
        // ---------------------------------------------------------------- 色板（策划案：低饱和暖棕 / 琥珀 / 灰蓝）

        /// <summary>正文墨色。</summary>
        public static readonly Color Ink = new Color(0.13f, 0.12f, 0.12f, 1f);

        /// <summary>纸面/留白。</summary>
        public static readonly Color Paper = new Color(0.97f, 0.95f, 0.91f, 1f);

        /// <summary>浅色文字（用在深色底上）。</summary>
        public static readonly Color LightInk = new Color(0.93f, 0.90f, 0.84f, 1f);

        /// <summary>次级文字。</summary>
        public static readonly Color Muted = new Color(0.58f, 0.55f, 0.52f, 1f);

        /// <summary>琥珀：暖光源、黄铜、刻痕高光。</summary>
        public static readonly Color Amber = new Color(0.87f, 0.62f, 0.28f, 1f);

        /// <summary>黄铜（滑块轨道、铜环）。</summary>
        public static readonly Color Brass = new Color(0.72f, 0.55f, 0.26f, 1f);

        /// <summary>回溯反馈色：淡紫。</summary>
        public static readonly Color RewindViolet = new Color(0.64f, 0.52f, 0.92f, 1f);

        /// <summary>回溯反馈色：青。</summary>
        public static readonly Color RewindCyan = new Color(0.45f, 0.78f, 0.82f, 1f);

        public const float CanvasWidth = 1920f;
        public const float CanvasHeight = 1080f;

        // ---------------------------------------------------------------- 基础图元

        private static Sprite _disc;

        /// <summary>一张 4x4 纯白精灵，供所有纯色块复用。</summary>
        public static Sprite Disc()
        {
            if (_disc == null)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(255, 255, 255, 255);
                }

                tex.SetPixels32(pixels);
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                _disc = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                _disc.hideFlags = HideFlags.HideAndDontSave;
            }

            return _disc;
        }

        /// <summary>铺满父级的面板。</summary>
        public static RectTransform MakePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = Disc();
            image.raycastTarget = false;
            return rt;
        }

        /// <summary>居中的纯色方块（相对父节点中心定位）。</summary>
        public static Image Block(Transform parent, string name, Color color, Vector2 size, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = Disc();
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>方块，但随父级尺寸的宽高比例定位（用于跟屏幕比例联动的装饰）。</summary>
        public static Image BlockAt(Transform parent, string name, Color color, Vector2 size, Vector2 anchoredPosition)
        {
            var image = Block(parent, name, color, size, Vector2.zero);
            image.rectTransform.anchoredPosition = anchoredPosition;
            return image;
        }

        /// <summary>圆（用同一张纯色精灵近似）。</summary>
        public static Image Round(Transform parent, string name, Color color, float diameter, Vector2 offset)
        {
            return Block(parent, name, color, new Vector2(diameter, diameter), offset);
        }

        /// <summary>创建一个纯容器（不绘制、不接管射线）。</summary>
        public static RectTransform MakeGroup(Transform parent, string name, Vector2 size, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        /// <summary>铺满父级的纯容器。</summary>
        public static RectTransform MakeStretchedGroup(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            return rt;
        }

        // ---------------------------------------------------------------- 文字与按钮

        public static Text MakeText(
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
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var text = go.GetComponent<Text>();
            text.font = BuiltinFont();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.lineSpacing = 1.25f;
            text.text = content;
            return text;
        }

        /// <summary>一行居中/靠边文字，按锚点定位（配合 MakeText 使用会过于啰嗦，这里给常用形态）。</summary>
        public static Text MakeLine(Transform parent, string name, string content, int fontSize, Color color, TextAnchor alignment)
        {
            var anchor = alignment == TextAnchor.UpperCenter || alignment == TextAnchor.MiddleCenter || alignment == TextAnchor.LowerCenter
                ? new Vector2(0.5f, alignment == TextAnchor.UpperCenter ? 1f : (alignment == TextAnchor.LowerCenter ? 0f : 0.5f))
                : new Vector2(0f, 0.5f);

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = new Vector2(CanvasWidth, 80f);
            rt.anchoredPosition = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.font = BuiltinFont();
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

        private static Font _font;

        private static Font BuiltinFont()
        {
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _font;
        }

        /// <summary>四态按钮：正常 / 悬停 / 按下 / 禁用。</summary>
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
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;

            var image = go.GetComponent<Image>();
            image.color = Color.white;
            image.sprite = Disc();

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Lighten(baseColor, 0.16f);
            colors.pressedColor = Darken(baseColor, 0.22f);
            colors.selectedColor = Lighten(baseColor, 0.08f);
            colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.25f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            MakeText(go.transform, "Label", label, 24, LightInk, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(10f, 4f), new Vector2(-10f, -4f));

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

            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        /// <summary>保证场景里有 EventSystem，否则 UGUI 收不到任何点击。</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        /// <summary>创建全屏画布（ScreenSpaceOverlay + CanvasScaler 1920x1080）。</summary>
        public static Canvas MakeCanvas(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        // ---------------------------------------------------------------- 工具

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
            return new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
        }

        /// <summary>按名字深度优先查找子节点。</summary>
        public static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                var found = FindDeepChild(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>把 0—1 的 RGB 载体转成 Unity 颜色。</summary>
        public static Color FromTone(float r, float g, float b, float a = 1f)
        {
            return new Color(r, g, b, a);
        }

        /// <summary>
        /// 检查内置字体能不能画出给定的文字。
        /// 本工程全部文字都是中文，而内置的是英文字体——这一条用于确认中文没有变成方框。
        /// </summary>
        public static bool FontSupports(string text)
        {
            Font font = BuiltinFont();
            if (font == null || string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ' || c == '\n' || c == '\r')
                {
                    continue;
                }

                if (!font.HasCharacter(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
