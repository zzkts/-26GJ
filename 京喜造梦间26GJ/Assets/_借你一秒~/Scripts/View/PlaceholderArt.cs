using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JieNiYiMiao
{
    /// <summary>
    /// 占位美术：全部用 UGUI 的纯色矩形与圆形拼出可辨认的造型。
    /// 手册明确要求"没有正式素材时优先用简洁的几何形状"，所以这里不加载任何贴图文件。
    /// 造型只负责"看得懂"，精致度交给阶段 5 的打磨。
    /// </summary>
    public static class PlaceholderArt
    {
        private static Sprite _disc;

        /// <summary>把一张 4x4 白图当纯色精灵用，避免引入外部资源。</summary>
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

        /// <summary>一个居中的纯色方块，相对父节点中心定位。</summary>
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

        /// <summary>圆（用同一张纯色精灵近似）。</summary>
        public static Image Round(Transform parent, string name, Color color, float diameter, Vector2 offset)
        {
            return Block(parent, name, color, new Vector2(diameter, diameter), offset);
        }

        // ---------------------------------------------------------------- 人物

        /// <summary>按年龄段拼一个人形。age 只影响头身比、发色与姿态，保持一个函数可维护。</summary>
        public static RectTransform BuildFigure(Transform parent, string name, Color tone, int age, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            bool old = age >= 70;
            bool child = age <= 12;
            Color skin = new Color(0.98f, 0.87f, 0.76f);
            Color hair = old ? new Color(0.88f, 0.88f, 0.90f) : (child ? new Color(0.18f, 0.15f, 0.14f) : new Color(0.16f, 0.14f, 0.15f));
            Color cloth = tone;
            Color clothDark = UiFactory.Darken(tone, 0.14f);

            float height = size.y;
            float width = size.x;
            float headD = child ? width * 0.46f : width * 0.34f;
            float bodyH = height * (child ? 0.34f : 0.40f);
            float bodyW = width * (child ? 0.52f : 0.46f);
            float legH = height * (old ? 0.30f : 0.34f);
            float lean = old ? 6f : 0f; // 老人略微前倾

            // 腿
            Block(root.transform, "LegL", UiFactory.Darken(clothDark, 0.10f), new Vector2(bodyW * 0.30f, legH), new Vector2(-bodyW * 0.20f, -height * 0.5f + legH * 0.5f));
            Block(root.transform, "LegR", UiFactory.Darken(clothDark, 0.10f), new Vector2(bodyW * 0.30f, legH), new Vector2(bodyW * 0.20f, -height * 0.5f + legH * 0.5f));

            // 身体
            var body = Block(root.transform, "Body", cloth, new Vector2(bodyW, bodyH), new Vector2(lean * 0.5f, -height * 0.5f + legH + bodyH * 0.5f));
            body.transform.localRotation = Quaternion.Euler(0f, 0f, lean * 0.4f);

            // 手臂
            float armH = bodyH * (child ? 0.62f : 0.72f);
            var armL = Block(root.transform, "ArmL", clothDark, new Vector2(bodyW * 0.18f, armH), new Vector2(-bodyW * 0.62f, -height * 0.5f + legH + bodyH * 0.62f));
            armL.transform.localRotation = Quaternion.Euler(0f, 0f, old ? 14f : 6f);
            var armR = Block(root.transform, "ArmR", clothDark, new Vector2(bodyW * 0.18f, armH), new Vector2(bodyW * 0.62f, -height * 0.5f + legH + bodyH * 0.62f));
            armR.transform.localRotation = Quaternion.Euler(0f, 0f, old ? -14f : -6f);

            // 脖子 + 头
            float neckY = -height * 0.5f + legH + bodyH;
            Block(root.transform, "Neck", UiFactory.Darken(skin, 0.08f), new Vector2(headD * 0.34f, headD * 0.26f), new Vector2(lean, neckY));
            Round(root.transform, "Head", skin, headD, new Vector2(lean, neckY + headD * 0.62f));

            // 头发（老人的白发更靠下，形成秃顶+鬓角）
            float hairY = neckY + headD * 0.62f + headD * (old ? 0.20f : 0.16f);
            Block(root.transform, "Hair", hair,
                new Vector2(headD * (old ? 0.98f : 0.92f), headD * (old ? 0.22f : 0.34f)),
                new Vector2(lean, hairY));
            if (child)
            {
                Block(root.transform, "Fringe", hair, new Vector2(headD * 0.36f, headD * 0.14f), new Vector2(lean - headD * 0.20f, hairY + headD * 0.10f));
            }

            // 眼睛（闭着的一条线：走马灯里的人都不看镜头）
            Color eye = new Color(0.15f, 0.14f, 0.16f);
            Block(root.transform, "EyeL", eye, new Vector2(headD * 0.16f, Mathf.Max(1.5f, headD * 0.045f)), new Vector2(lean - headD * 0.18f, neckY + headD * 0.60f));
            Block(root.transform, "EyeR", eye, new Vector2(headD * 0.16f, Mathf.Max(1.5f, headD * 0.045f)), new Vector2(lean + headD * 0.18f, neckY + headD * 0.60f));

            // 嘴（老人的一条向下的线，孩子的很小）
            Block(root.transform, "Mouth", UiFactory.Darken(skin, 0.28f),
                new Vector2(headD * (old ? 0.26f : 0.18f), Mathf.Max(1.5f, headD * 0.04f)),
                new Vector2(lean, neckY + headD * 0.36f));

            if (old)
            {
                // 眉间的两道皱纹，表达一生的重量
                Block(root.transform, "WrinkleL", UiFactory.Darken(skin, 0.22f), new Vector2(headD * 0.14f, 1.5f), new Vector2(lean - headD * 0.10f, neckY + headD * 0.78f));
                Block(root.transform, "WrinkleR", UiFactory.Darken(skin, 0.22f), new Vector2(headD * 0.14f, 1.5f), new Vector2(lean + headD * 0.10f, neckY + headD * 0.78f));
            }

            return rt;
        }

        // ---------------------------------------------------------------- 场景道具

        /// <summary>第一关：站台、列车、黄线、母亲、木船。</summary>
        public static RectTransform BuildStation(Transform parent, Color sky, Color ground)
        {
            var root = new GameObject("StationBackdrop", typeof(RectTransform)).GetComponent<RectTransform>();
            root.transform.SetParent(parent, false);
            UiFactory.Stretch(root);

            // 天空 + 地面
            UiFactory.MakePanel(root, "Sky", sky);
            Block(root, "Ground", ground, new Vector2(4000f, 240f), new Vector2(0f, 80f));

            // 远处的柱子与顶棚
            for (int i = 0; i < 6; i++)
            {
                float x = -700f + i * 280f;
                Block(root, "Pillar" + i, UiFactory.Darken(sky, 0.16f), new Vector2(14f, 210f), new Vector2(x, 220f));
            }
            Block(root, "Canopy", UiFactory.Darken(sky, 0.10f), new Vector2(4000f, 26f), new Vector2(0f, 330f));

            // 列车：车头 + 三节车厢 + 车窗
            var train = new GameObject("Train", typeof(RectTransform)).GetComponent<RectTransform>();
            train.transform.SetParent(root, false);
            train.anchorMin = new Vector2(1f, 0f);
            train.anchorMax = new Vector2(1f, 0f);
            train.pivot = new Vector2(1f, 0f);
            train.anchoredPosition = new Vector2(-20f, 190f);
            train.sizeDelta = new Vector2(620f, 300f);
            Color trainBody = new Color(0.34f, 0.40f, 0.47f);
            Block(train, "Body", trainBody, new Vector2(620f, 190f), new Vector2(-310f, 95f));
            Block(train, "Stripe", new Color(0.86f, 0.67f, 0.34f), new Vector2(620f, 12f), new Vector2(-310f, 78f));
            for (int i = 0; i < 5; i++)
            {
                Block(train, "Window" + i, new Color(0.96f, 0.90f, 0.72f), new Vector2(72f, 54f), new Vector2(-560f + i * 118f, 122f));
            }
            Block(train, "Door", UiFactory.Darken(trainBody, 0.10f), new Vector2(58f, 150f), new Vector2(-40f, 78f));

            // 站台黄线
            Block(root, "SafetyLine", new Color(0.93f, 0.79f, 0.32f), new Vector2(1400f, 10f), new Vector2(-350f, 196f));
            for (int i = 0; i < 16; i++)
            {
                Block(root, "Dash" + i, new Color(0.86f, 0.60f, 0.22f), new Vector2(46f, 6f), new Vector2(-1000f + i * 92f, 176f));
            }

            return root;
        }

        /// <summary>第一关：追着列车跑的母亲（时间倒流时向前走回来的那一秒）。</summary>
        public static RectTransform BuildMother(Transform parent)
        {
            var root = new GameObject("Mother", typeof(RectTransform)).GetComponent<RectTransform>();
            root.transform.SetParent(parent, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(120f, 230f);
            root.anchoredPosition = new Vector2(470f, -120f);
            BuildFigure(root, "MotherFigure", new Color(0.78f, 0.46f, 0.42f), 36, new Vector2(120f, 230f));
            return root;
        }

        /// <summary>第二关：办公室（办公桌、显示器、台灯、窗外夜）。</summary>
        public static RectTransform BuildOffice(Transform parent, Color sky, Color ground)
        {
            var root = new GameObject("OfficeBackdrop", typeof(RectTransform)).GetComponent<RectTransform>();
            root.transform.SetParent(parent, false);
            UiFactory.Stretch(root);

            UiFactory.MakePanel(root, "Night", sky);
            // 窗
            Block(root, "WindowFrame", UiFactory.Darken(sky, 0.24f), new Vector2(430f, 260f), new Vector2(-520f, 200f));
            Block(root, "WindowGlass", UiFactory.Lighten(sky, 0.12f), new Vector2(400f, 232f), new Vector2(-520f, 200f));
            for (int i = 0; i < 9; i++)
            {
                Block(root, "CityLight" + i, new Color(0.96f, 0.80f, 0.42f),
                    new Vector2(16f + (i % 3) * 8f, 10f), new Vector2(-660f + i * 34f, 120f + (i % 4) * 38f));
            }
            Block(root, "DeskTop", ground, new Vector2(4000f, 26f), new Vector2(0f, 30f));
            Block(root, "DeskMass", UiFactory.Darken(ground, 0.06f), new Vector2(4000f, 240f), new Vector2(0f, -100f));

            // 显示器
            Block(root, "MonitorStand", new Color(0.20f, 0.22f, 0.26f), new Vector2(30f, 40f), new Vector2(430f, 60f));
            Block(root, "MonitorBody", new Color(0.16f, 0.18f, 0.22f), new Vector2(300f, 180f), new Vector2(430f, 165f));
            Block(root, "ScreenGlow", new Color(0.36f, 0.52f, 0.66f), new Vector2(276f, 156f), new Vector2(430f, 165f));

            // 工作报告的进度条画在屏幕内部（不占桌面空间，也不会挡住人物）
            BuildProgressBar(root, new Vector2(240f, 16f), new Vector2(430f, 120f), 0f, new Color(0.80f, 0.90f, 1f));

            // 台灯
            Block(root, "LampPole", new Color(0.24f, 0.25f, 0.29f), new Vector2(10f, 110f), new Vector2(-330f, 95f));
            Block(root, "LampShade", new Color(0.92f, 0.74f, 0.36f), new Vector2(96f, 26f), new Vector2(-300f, 150f));
            var halo = Block(root, "LampHalo", new Color(0.95f, 0.82f, 0.45f, 0.16f), new Vector2(300f, 300f), new Vector2(-250f, 110f));
            halo.raycastTarget = false;

            // 第一关留下来才会有的小相框（阶段 3 的可见差异之一）
            return root;
        }

        /// <summary>第二关：桌上那张站台旧照片（只有第一关"留下告别"才出现）。</summary>
        public static RectTransform BuildPhoto(Transform parent, Vector2 position)
        {
            var root = new GameObject("OldPhoto", typeof(RectTransform)).GetComponent<RectTransform>();
            root.transform.SetParent(parent, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(104f, 78f);
            root.anchoredPosition = position;
            Block(root, "Frame", new Color(0.74f, 0.62f, 0.44f), new Vector2(104f, 78f), Vector2.zero);
            Block(root, "Photo", new Color(0.86f, 0.80f, 0.68f), new Vector2(88f, 62f), Vector2.zero);
            Block(root, "PhotoTrain", new Color(0.40f, 0.44f, 0.50f), new Vector2(60f, 18f), new Vector2(0f, 12f));
            Block(root, "PhotoLine", new Color(0.90f, 0.72f, 0.26f), new Vector2(88f, 4f), new Vector2(0f, -2f));
            Block(root, "PhotoBoy", new Color(0.24f, 0.22f, 0.22f), new Vector2(10f, 20f), new Vector2(-18f, -12f));
            return root;
        }

        /// <summary>第三关：阁楼（窗、工作台、工具、木屑）。</summary>
        public static RectTransform BuildAttic(Transform parent, Color sky, Color ground)
        {
            var root = new GameObject("AtticBackdrop", typeof(RectTransform)).GetComponent<RectTransform>();
            root.transform.SetParent(parent, false);
            UiFactory.Stretch(root);

            UiFactory.MakePanel(root, "Warm", sky);
            // 大窗
            Block(root, "WindowFrame", UiFactory.Darken(sky, 0.26f), new Vector2(560f, 380f), new Vector2(300f, 190f));
            Block(root, "WindowGlass", new Color(0.98f, 0.91f, 0.74f), new Vector2(524f, 344f), new Vector2(300f, 190f));
            Block(root, "WindowBarV", UiFactory.Darken(sky, 0.20f), new Vector2(16f, 344f), new Vector2(300f, 190f));
            Block(root, "WindowBarH", UiFactory.Darken(sky, 0.20f), new Vector2(524f, 14f), new Vector2(300f, 190f));
            var glow = Block(root, "WindowGlow", new Color(1f, 0.92f, 0.70f, 0.20f), new Vector2(760f, 520f), new Vector2(260f, 160f));
            glow.raycastTarget = false;

            // 工作台
            Block(root, "BenchTop", UiFactory.Darken(ground, 0.02f), new Vector2(4000f, 30f), new Vector2(0f, 20f));
            Block(root, "BenchMass", UiFactory.Darken(ground, 0.10f), new Vector2(4000f, 220f), new Vector2(0f, -105f));

            // 木屑与工具
            for (int i = 0; i < 12; i++)
            {
                float x = -560f + i * 96f;
                Block(root, "Shaving" + i, new Color(0.80f, 0.68f, 0.48f, 0.85f), new Vector2(26f, 4f), new Vector2(x, 40f + (i % 3) * 7f));
            }
            Block(root, "ToolHandle", new Color(0.42f, 0.30f, 0.22f), new Vector2(70f, 12f), new Vector2(-430f, 46f));
            Block(root, "ToolHead", new Color(0.55f, 0.57f, 0.60f), new Vector2(34f, 22f), new Vector2(-378f, 46f));

            return root;
        }

        // ---------------------------------------------------------------- 关键物件

        /// <summary>
        /// 木船。参数化程度足够表达手册里的全部状态：
        /// scratched=船身有刻痕、fresh=全新平整、mended=有修补痕迹、visiblePlank=那块旧木板是否在船上。
        /// </summary>
        public static RectTransform BuildBoat(Transform parent, Vector2 size, Color wood, bool scratched, bool fresh, bool mended, bool hasOldPlank)
        {
            var root = new GameObject("WoodenBoat", typeof(RectTransform)).GetComponent<RectTransform>();
            root.transform.SetParent(parent, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = size;
            root.anchoredPosition = Vector2.zero;

            float w = size.x;
            float h = size.y;
            Color woodDark = UiFactory.Darken(wood, 0.16f);
            Color woodLight = UiFactory.Lighten(wood, 0.14f);
            Color plankTone = fresh ? new Color(0.86f, 0.76f, 0.58f) : (mended ? UiFactory.Lighten(wood, 0.06f) : wood);

            // 船身：上宽下窄的三层木条
            Block(root, "HullTop", plankTone, new Vector2(w, h * 0.20f), new Vector2(0f, h * 0.16f));
            Block(root, "HullMid", plankTone, new Vector2(w * 0.88f, h * 0.20f), new Vector2(0f, -h * 0.02f));
            Block(root, "HullBottom", woodDark, new Vector2(w * 0.68f, h * 0.18f), new Vector2(0f, -h * 0.20f));
            Block(root, "Keel", woodDark, new Vector2(w * 0.16f, h * 0.20f), new Vector2(0f, -h * 0.34f));

            // 船头船尾的翘起
            Block(root, "BowUp", plankTone, new Vector2(w * 0.14f, h * 0.16f), new Vector2(w * 0.44f, h * 0.30f));
            Block(root, "SternUp", plankTone, new Vector2(w * 0.14f, h * 0.16f), new Vector2(-w * 0.44f, h * 0.28f));

            // 帆与桅杆
            Block(root, "Mast", woodDark, new Vector2(Mathf.Max(3f, w * 0.03f), h * 0.72f), new Vector2(0f, h * 0.44f));
            Block(root, "Sail", new Color(0.96f, 0.93f, 0.86f), new Vector2(w * 0.34f, h * 0.46f), new Vector2(w * 0.18f, h * 0.52f));

            // 那块刻着字的旧木板
            if (hasOldPlank)
            {
                Color oldPlank = scratched ? UiFactory.Darken(wood, 0.22f) : woodDark;
                var plank = Block(root, "OldPlank", oldPlank, new Vector2(w * 0.42f, h * 0.14f), new Vector2(-w * 0.10f, h * 0.16f));
                plank.transform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);

                if (scratched)
                {
                    // 两笔歪歪扭扭的字：两个小方块拼出刻痕
                    for (int i = 0; i < 2; i++)
                    {
                        Block(plank.transform, "CarveStroke" + i, new Color(0.30f, 0.21f, 0.15f),
                            new Vector2(w * 0.06f, Mathf.Max(2f, h * 0.035f)),
                            new Vector2((i == 0 ? -1f : 1f) * w * 0.045f, 1f));
                    }
                }
            }

            if (mended)
            {
                // 补丁：一块颜色不同的新木条覆盖在旧板旁边
                var patch = Block(root, "Patch", new Color(0.90f, 0.82f, 0.64f), new Vector2(w * 0.34f, h * 0.16f), new Vector2(w * 0.12f, h * 0.16f));
                patch.transform.localRotation = Quaternion.Euler(0f, 0f, -2f);
            }

            if (fresh)
            {
                // 全新：船身平整、有反光高光
                var shine = Block(root, "Shine", new Color(1f, 0.97f, 0.88f, 0.35f), new Vector2(w * 0.26f, h * 0.06f), new Vector2(-w * 0.16f, h * 0.22f));
                shine.raycastTarget = false;
            }

            return root;
        }

        /// <summary>第二关的"手机"。响铃状态会显示一圈扩散的光晕。</summary>
        public static RectTransform BuildPhone(Transform parent, Color tone, bool ringing, bool faceDown, Vector2 size)
        {
            var root = new GameObject("Phone", typeof(RectTransform)).GetComponent<RectTransform>();
            root.transform.SetParent(parent, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = size;
            root.anchoredPosition = Vector2.zero;

            if (ringing)
            {
                var ring = Block(root, "RingHalo", new Color(0.98f, 0.86f, 0.44f, 0.28f), size * 1.9f, Vector2.zero);
                ring.raycastTarget = false;
            }

            Block(root, "Shell", tone, size, Vector2.zero);
            if (!faceDown)
            {
                Block(root, "Screen", new Color(0.90f, 0.94f, 0.98f), size * 0.82f, Vector2.zero);
                Block(root, "Caller", new Color(0.30f, 0.40f, 0.52f), new Vector2(size.x * 0.66f, size.y * 0.10f), new Vector2(0f, size.y * 0.12f));
                Block(root, "CallerLine", new Color(0.42f, 0.52f, 0.64f), new Vector2(size.x * 0.46f, size.y * 0.06f), new Vector2(0f, -size.y * 0.04f));
            }
            else
            {
                Block(root, "Back", UiFactory.Darken(tone, 0.10f), size * 0.86f, Vector2.zero);
            }

            return root;
        }

        /// <summary>进度条轨道（填充块由 UpdateProgressBar 找出来改宽度）。</summary>
        public static Image BuildProgressBar(Transform parent, Vector2 size, Vector2 position, float fill01, Color tone)
        {
            var track = Block(parent, "ProgressTrack", UiFactory.WithAlpha(tone, 0.30f), size, position);
            var fill = Block(track.transform, "ProgressFill", UiFactory.Lighten(tone, 0.10f),
                new Vector2(Mathf.Max(2f, size.x * Mathf.Clamp01(fill01)), size.y - 4f), Vector2.zero);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = new Vector2(-size.x * 0.5f + 2f, 0f);
            return track;
        }

        /// <summary>按当前进度重设填充宽度（0—1）。找不到轨道时静默返回，不会打断流程。</summary>
        public static void UpdateProgressBar(Transform root, float fill01)
        {
            var track = FindDeepChild(root, "ProgressTrack");
            if (track == null)
            {
                return;
            }

            var fill = FindDeepChild(track, "ProgressFill");
            var trackRt = track as RectTransform;
            var fillRt = fill as RectTransform;
            if (fillRt == null || trackRt == null)
            {
                return;
            }

            float width = Mathf.Max(2f, trackRt.sizeDelta.x * Mathf.Clamp01(fill01));
            fillRt.sizeDelta = new Vector2(width, fillRt.sizeDelta.y);
            fillRt.anchoredPosition = new Vector2(-trackRt.sizeDelta.x * 0.5f + 2f, 0f);
        }

        /// <summary>按名字深度优先查找子节点（不依赖 Transform.Find 的路径写法）。</summary>
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
    }
}
