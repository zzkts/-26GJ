using UnityEngine;
using UnityEngine.UI;

namespace CuoGuo
{
    /// <summary>
    /// 码头占位美术。这里是占位美术：全部造型都由 UGUI 纯色图元（方块、圆形的近似）拼成，
    /// 不加载任何贴图、不读取任何外部资源，全部通过 UiFactory 的公开 helper 创建。
    ///
    /// 屏幕坐标约定（画布参考分辨率 1920x1080，原点在画布中心，x 向右为正，y 向上为正）：
    /// - 海平线 y≈-140，海面从 -140 向下铺满；
    /// - 码头木面顶面 y≈-300（人物脚底站在这里），向下延伸到屏幕外；
    /// - 木船中心约 (280,-120)、尺寸 700x300，浮在水上；
    /// - 码头入口 / 远处小镇在屏幕左侧 x≈-800；天空是 y > -140 的部分。
    ///
    /// 节点约定：除 BuildBackdrop 返回铺满父级的节点外，所有 builder 的根都是
    /// anchorMin = anchorMax = (0.5,0.5)、pivot = (0.5,0.5) 的居中节点，内部元件相对根节点中心摆放，
    /// 根节点的 anchoredPosition 由时间轴系统在运行时整体覆盖。
    /// 除 BuildSliderKnob 的根（要接收拖动）之外，所有 Image 的 raycastTarget 都是 false。
    /// </summary>
    public static class DockArt
    {
        // ---------------------------------------------------------------- 局部色板（低饱和暖棕 / 琥珀 / 灰蓝）

        /// <summary>通用旧木色。</summary>
        private static readonly Color WoodTone = new Color(0.46f, 0.33f, 0.23f, 1f);

        /// <summary>深木色（阴影、梁柱）。</summary>
        private static readonly Color WoodDeep = new Color(0.28f, 0.19f, 0.13f, 1f);

        /// <summary>缆绳/麻绳。</summary>
        private static readonly Color RopeTone = new Color(0.72f, 0.61f, 0.42f, 1f);

        /// <summary>肤色。</summary>
        private static readonly Color SkinTone = new Color(0.97f, 0.86f, 0.74f, 1f);

        /// <summary>深发色。</summary>
        private static readonly Color HairDark = new Color(0.18f, 0.14f, 0.13f, 1f);

        /// <summary>老人花白的头发。</summary>
        private static readonly Color HairGrey = new Color(0.85f, 0.86f, 0.88f, 1f);

        /// <summary>鞋。</summary>
        private static readonly Color ShoeTone = new Color(0.22f, 0.17f, 0.15f, 1f);

        /// <summary>红线（第三章的关键颜色）。</summary>
        private static readonly Color ThreadRed = new Color(0.70f, 0.22f, 0.20f, 1f);

        /// <summary>泛黄的海图纸。</summary>
        private static readonly Color OldPaperTone = new Color(0.88f, 0.81f, 0.63f, 1f);

        /// <summary>罗盘玻璃。</summary>
        private static readonly Color GlassTone = new Color(0.79f, 0.85f, 0.86f, 1f);

        // ---------------------------------------------------------------- 局部小工具（只包装 UiFactory 的公开 helper）

        /// <summary>居中方块：宽高分开写，省得到处 new Vector2。</summary>
        private static Image Bar(Transform parent, string name, Color color, float width, float height, float x, float y)
        {
            return UiFactory.Block(parent, name, color, new Vector2(width, height), new Vector2(x, y));
        }

        /// <summary>给方块加一个绕 Z 轴的旋转（斜线、指针、翘起的船头都用它）。</summary>
        private static Image Tilt(Image image, float degrees)
        {
            image.transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
            return image;
        }

        /// <summary>居中一行文字（锚点居中，按给定宽高铺开）。</summary>
        private static Text CenteredText(Transform parent, string name, string content, int fontSize, Color color, float width, float height, float x, float y)
        {
            float left = x - width * 0.5f;
            float bottom = y - height * 0.5f;
            return UiFactory.MakeText(parent, name, content, fontSize, color, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(left, bottom), new Vector2(left + width, bottom + height));
        }

        /// <summary>用外圈 + 内芯两个方块拼出"环"的观感（铜环、绳圈）。</summary>
        private static void RingPair(Transform parent, string name, Color ringColor, Color holeColor, float diameter, Vector2 offset)
        {
            UiFactory.Round(parent, name + "_Outer", ringColor, diameter, offset);
            UiFactory.Round(parent, name + "_Hole", holeColor, diameter * 0.52f, offset);
        }

        // ---------------------------------------------------------------- 背景

        /// <summary>码头背景：天空、海面、木栈道、左侧远处的小镇（style = "dusk" 黄昏 / "dawn" 清晨 / "night" 夜晚）。</summary>
        public static RectTransform BuildBackdrop(Transform parent, string style, Color sky, Color sea, Color dock)
        {
            var root = UiFactory.MakeStretchedGroup(parent, "DockBackdrop");

            bool night = style == "night";
            bool dawn = style == "dawn";
            const float horizon = -140f;

            // ---- 天空：底色 + 靠近海平线的两层亮带，用叠加的色带假装渐变 ----
            UiFactory.MakePanel(root, "Sky", sky);
            UiFactory.Block(root, "SkyGlowHigh", UiFactory.Lighten(sky, night ? 0.03f : 0.05f), new Vector2(4200f, 180f), new Vector2(0f, horizon + 210f));
            UiFactory.Block(root, "SkyGlowLow", UiFactory.Lighten(sky, night ? 0.06f : 0.12f), new Vector2(4200f, 120f), new Vector2(0f, horizon + 60f));

            // ---- 星星（只有夜里才有）----
            if (night)
            {
                for (int i = 0; i < 26; i++)
                {
                    float sx = -940f + ((i * 173) % 1880);
                    float sy = 110f + ((i * 269) % 380);
                    float d = 2f + (i % 3);
                    UiFactory.Round(root, "Star" + i, UiFactory.WithAlpha(UiFactory.LightInk, 0.30f + 0.16f * (i % 3)), d, new Vector2(sx, sy));
                }
            }

            // ---- 月亮 / 太阳 ----
            if (night)
            {
                var moon = new Vector2(430f, 300f);
                UiFactory.Round(root, "MoonGlow", UiFactory.WithAlpha(UiFactory.LightInk, 0.08f), 240f, moon);
                UiFactory.Round(root, "MoonGlowMid", UiFactory.WithAlpha(UiFactory.LightInk, 0.12f), 156f, moon);
                UiFactory.Round(root, "Moon", new Color(0.96f, 0.94f, 0.86f, 1f), 96f, moon);
                UiFactory.Round(root, "MoonSpotA", new Color(0.90f, 0.88f, 0.80f, 0.9f), 22f, new Vector2(moon.x - 18f, moon.y + 14f));
                UiFactory.Round(root, "MoonSpotB", new Color(0.90f, 0.88f, 0.80f, 0.9f), 14f, new Vector2(moon.x + 16f, moon.y - 22f));
            }
            else
            {
                // 黄昏的落日贴着海平线，清晨的太阳更淡、更高一点
                Vector2 sunPos = dawn ? new Vector2(-420f, horizon + 96f) : new Vector2(-150f, horizon + 46f);
                float sunD = dawn ? 150f : 190f;
                Color sunCore = dawn ? new Color(0.98f, 0.94f, 0.84f, 0.95f) : new Color(0.98f, 0.78f, 0.45f, 0.95f);
                UiFactory.Round(root, "SunGlow", UiFactory.WithAlpha(sunCore, 0.09f), sunD * 2.4f, sunPos);
                UiFactory.Round(root, "SunGlowMid", UiFactory.WithAlpha(sunCore, 0.14f), sunD * 1.7f, sunPos);
                UiFactory.Round(root, "SunHalo", UiFactory.WithAlpha(sunCore, 0.26f), sunD * 1.25f, sunPos);
                UiFactory.Round(root, "SunCore", sunCore, sunD, sunPos);
            }

            // ---- 云：几条横向色带，越靠下越暖 ----
            for (int i = 0; i < 5; i++)
            {
                float cy = horizon + 120f + i * 78f;
                float cx = -760f + ((i * 421) % 1500) - (i % 2 == 0 ? 0f : 180f);
                UiFactory.Block(root, "Cloud" + i, UiFactory.WithAlpha(UiFactory.Lighten(sky, 0.14f), night ? 0.16f : 0.30f),
                    new Vector2(300f + (i % 3) * 130f, 20f + (i % 2) * 10f), new Vector2(cx, cy));
                UiFactory.Block(root, "CloudTop" + i, UiFactory.WithAlpha(UiFactory.Lighten(sky, 0.20f), night ? 0.12f : 0.24f),
                    new Vector2(180f + (i % 2) * 90f, 16f), new Vector2(cx - 60f, cy + 20f));
            }

            // ---- 左侧远处的小镇：海岸线 + 一排剪影房子 ----
            UiFactory.Block(root, "FarShore", UiFactory.Darken(sky, 0.22f), new Vector2(1500f, 16f), new Vector2(-560f, horizon + 4f));
            for (int i = 0; i < 6; i++)
            {
                float hx = -1080f + i * 96f;
                float hh = 46f + ((i * 53) % 40);
                float hw = 62f + ((i * 29) % 26);
                UiFactory.Block(root, "TownHouse" + i, UiFactory.Darken(sky, 0.34f), new Vector2(hw, hh), new Vector2(hx, horizon + 8f + hh * 0.5f));
                UiFactory.Block(root, "TownRoof" + i, UiFactory.Darken(sky, 0.46f), new Vector2(hw * 1.16f, 8f), new Vector2(hx, horizon + 8f + hh));
                Color window = night ? new Color(0.98f, 0.82f, 0.44f, 0.85f) : UiFactory.WithAlpha(UiFactory.Darken(sky, 0.52f), 0.7f);
                UiFactory.Block(root, "TownWindow" + i, window, new Vector2(12f, 14f), new Vector2(hx, horizon + 8f + hh * 0.5f));
            }

            // ---- 海面 ----
            UiFactory.Block(root, "Sea", sea, new Vector2(4200f, 440f), new Vector2(0f, horizon - 220f));
            UiFactory.Block(root, "SeaLine", UiFactory.Lighten(sea, night ? 0.10f : 0.16f), new Vector2(4200f, 4f), new Vector2(0f, horizon - 2f));

            // 水面的碎浪
            for (int i = 0; i < 12; i++)
            {
                float wy = horizon - 18f - i * 30f;
                float ww = 160f + ((i * 97) % 420);
                float wx = -760f + ((i * 313) % 1500);
                UiFactory.Block(root, "Wave" + i, UiFactory.WithAlpha(UiFactory.Lighten(sea, 0.14f), 0.20f + 0.04f * (i % 3)),
                    new Vector2(ww, 4f), new Vector2(wx, wy));
            }

            // 光源在水面上拖出的一条光路
            float pathX = night ? 430f : (dawn ? -420f : -150f);
            Color pathColor = night ? new Color(0.92f, 0.93f, 0.88f, 1f) : new Color(0.99f, 0.85f, 0.55f, 1f);
            for (int i = 0; i < 9; i++)
            {
                float pw = 150f - i * 12f;
                UiFactory.Block(root, "LightPath" + i, UiFactory.WithAlpha(pathColor, 0.20f - i * 0.015f),
                    new Vector2(Mathf.Max(24f, pw), 5f), new Vector2(pathX + (i % 2 == 0 ? -8f : 10f), horizon - 10f - i * 26f));
            }

            // ---- 码头木栈道：顶面 y = -300，向下铺到屏幕外 ----
            UiFactory.Block(root, "DockFace", dock, new Vector2(4200f, 300f), new Vector2(0f, -450f));
            UiFactory.Block(root, "DockTopEdge", UiFactory.Lighten(dock, 0.14f), new Vector2(4200f, 8f), new Vector2(0f, -304f));
            UiFactory.Block(root, "DockLipShade", UiFactory.Darken(dock, 0.18f), new Vector2(4200f, 10f), new Vector2(0f, -313f));
            UiFactory.Block(root, "DockUnderShade", UiFactory.WithAlpha(UiFactory.Darken(dock, 0.30f), 0.55f), new Vector2(4200f, 120f), new Vector2(0f, -500f));

            // 木板缝
            for (int i = 0; i < 21; i++)
            {
                UiFactory.Block(root, "DockSeam" + i, UiFactory.WithAlpha(UiFactory.Darken(dock, 0.20f), 0.75f),
                    new Vector2(4f, 240f), new Vector2(-1200f + i * 120f, -420f));
            }

            // 两道横着的板缝
            UiFactory.Block(root, "DockRowA", UiFactory.WithAlpha(UiFactory.Darken(dock, 0.14f), 0.65f), new Vector2(4200f, 5f), new Vector2(0f, -370f));
            UiFactory.Block(root, "DockRowB", UiFactory.WithAlpha(UiFactory.Darken(dock, 0.14f), 0.55f), new Vector2(4200f, 5f), new Vector2(0f, -470f));

            // 栈道边上的两个系船桩（船停在 x≈280，桩子放左边不挡船）
            for (int i = 0; i < 2; i++)
            {
                float px = -520f + i * 280f;
                Bar(root, "DockPost" + i, UiFactory.Darken(dock, 0.24f), 28f, 96f, px, -300f + 48f);
                Bar(root, "DockPostCap" + i, UiFactory.Lighten(dock, 0.06f), 36f, 10f, px, -300f + 96f);
            }

            // ---- 左侧码头入口：一根立柱挑着一块小木牌 ----
            Bar(root, "GatePost", WoodDeep, 16f, 150f, -802f, -225f);
            Bar(root, "GateBoard", UiFactory.Darken(dock, 0.30f), 124f, 36f, -802f, -180f);
            Bar(root, "GateBoardEdge", UiFactory.Lighten(dock, 0.06f), 124f, 4f, -802f, -196f);
            for (int i = 0; i < 3; i++)
            {
                // 木牌上的三笔字（占位，不写具体文字）
                Bar(root, "GateMark" + i, UiFactory.WithAlpha(UiFactory.Darken(dock, 0.55f), 0.8f), 24f, 4f, -838f + i * 36f, -180f);
            }

            if (night)
            {
                // 入口挂的一盏小灯，只在这一处破坏黑暗
                UiFactory.Round(root, "GateLampGlow", UiFactory.WithAlpha(UiFactory.Amber, 0.16f), 160f, new Vector2(-802f, -150f));
                UiFactory.Round(root, "GateLamp", new Color(0.99f, 0.86f, 0.58f, 1f), 22f, new Vector2(-802f, -150f));
            }
            else
            {
                UiFactory.Round(root, "GateLamp", UiFactory.WithAlpha(UiFactory.Amber, 0.55f), 18f, new Vector2(-802f, -150f));
            }

            return root;
        }

        // ---------------------------------------------------------------- 木船

        /// <summary>旧木船：三章共用的同一艘，船身三层木条 + 船舷 + 桨 + 船头绳圈（根节点中心对准船身中心，底边就是船底）。</summary>
        public static RectTransform BuildBoat(Transform parent, Vector2 size, Color wood)
        {
            var root = UiFactory.MakeGroup(parent, "WoodenBoat", size, Vector2.zero);

            float w = size.x;
            float h = size.y;
            Color woodLight = UiFactory.Lighten(wood, 0.14f);
            Color woodDark = UiFactory.Darken(wood, 0.18f);
            Color woodDeep = UiFactory.Darken(wood, 0.34f);
            Color inside = UiFactory.Darken(wood, 0.46f);

            // 水里的倒影
            UiFactory.Block(root, "BoatShadow", UiFactory.WithAlpha(new Color(0.06f, 0.08f, 0.11f), 0.20f), new Vector2(w * 1.02f, h * 0.09f), new Vector2(0f, -h * 0.47f));

            // 船体：底窄上宽的三段木条
            Bar(root, "Keel", woodDeep, w * 0.46f, h * 0.10f, 0f, -h * 0.44f);
            Bar(root, "HullBottom", woodDark, w * 0.74f, h * 0.22f, 0f, -h * 0.32f);
            Bar(root, "HullMid", wood, w * 0.90f, h * 0.22f, 0f, -h * 0.13f);
            Bar(root, "HullTop", wood, w * 1.00f, h * 0.24f, 0f, h * 0.07f);

            // 船板缝 + 肋骨
            UiFactory.Block(root, "SeamA", UiFactory.WithAlpha(woodDeep, 0.75f), new Vector2(w * 0.94f, 3f), new Vector2(0f, -h * 0.22f));
            UiFactory.Block(root, "SeamB", UiFactory.WithAlpha(woodDeep, 0.75f), new Vector2(w * 0.98f, 3f), new Vector2(0f, -h * 0.02f));
            UiFactory.Block(root, "SeamC", UiFactory.WithAlpha(woodDeep, 0.60f), new Vector2(w * 0.90f, 3f), new Vector2(0f, h * 0.16f));
            for (int i = 0; i < 5; i++)
            {
                UiFactory.Block(root, "Rib" + i, UiFactory.WithAlpha(woodDeep, 0.35f), new Vector2(w * 0.012f, h * 0.36f),
                    new Vector2(-w * 0.30f + i * w * 0.15f, -h * 0.06f));
            }

            // 船头 / 船尾翘起
            Tilt(Bar(root, "BowTip", woodLight, w * 0.10f, h * 0.36f, w * 0.475f, h * 0.24f), -16f);
            Tilt(Bar(root, "SternTip", woodDark, w * 0.09f, h * 0.30f, -w * 0.465f, h * 0.20f), 14f);

            // 船舷与船内的阴影
            Bar(root, "Gunwale", woodLight, w * 1.02f, h * 0.06f, 0f, h * 0.20f);
            Bar(root, "Inside", inside, w * 0.86f, h * 0.10f, 0f, h * 0.13f);
            Bar(root, "Thwart", woodLight, w * 0.24f, h * 0.07f, -w * 0.16f, h * 0.10f);
            Bar(root, "BowDeck", woodLight, w * 0.16f, h * 0.06f, w * 0.38f, h * 0.10f);

            // 船头卷着的旧缆绳
            RingPair(root, "RopeCoil", UiFactory.Darken(wood, 0.36f), woodLight, h * 0.13f, new Vector2(-w * 0.32f, h * 0.13f));
            Bar(root, "RopeTail", RopeTone, w * 0.07f, h * 0.02f, -w * 0.24f, h * 0.13f);

            // 斜靠在船里的一支桨
            var oar = UiFactory.MakeGroup(root, "Oar", new Vector2(w * 0.5f, h * 0.1f), new Vector2(-w * 0.02f, h * 0.27f));
            oar.localRotation = Quaternion.Euler(0f, 0f, 11f);
            Bar(oar, "OarShaft", UiFactory.Lighten(wood, 0.06f), w * 0.46f, h * 0.026f, 0f, 0f);
            Bar(oar, "OarBlade", UiFactory.Lighten(wood, 0.10f), w * 0.12f, h * 0.075f, -w * 0.27f, 0f);

            // 高光与铆钉
            UiFactory.Block(root, "HullShine", UiFactory.WithAlpha(new Color(1f, 0.96f, 0.86f), 0.18f), new Vector2(w * 0.52f, 4f), new Vector2(-w * 0.05f, h * 0.02f));
            for (int i = 0; i < 5; i++)
            {
                UiFactory.Round(root, "Nail" + i, UiFactory.WithAlpha(UiFactory.Darken(UiFactory.Brass, 0.20f), 0.8f), 7f,
                    new Vector2(-w * 0.34f + i * w * 0.17f, h * 0.20f));
            }

            return root;
        }

        // ---------------------------------------------------------------- 人物

        /// <summary>人形：age ≤12 是孩子（大头短腿）、≥70 是老人（驼背、白发、拄拐），其余为成年人；脚底踩在根节点的下边缘。</summary>
        public static RectTransform BuildFigure(Transform parent, string name, Color cloth, int age, Vector2 size)
        {
            var root = UiFactory.MakeGroup(parent, name, size, Vector2.zero);

            bool child = age <= 12;
            bool old = age >= 70;

            float w = size.x;
            float h = size.y;
            float feetY = -h * 0.5f;

            float headD = w * (child ? 0.48f : (old ? 0.31f : 0.34f));
            float legH = h * (child ? 0.30f : (old ? 0.36f : 0.40f));
            float bodyH = h * (child ? 0.28f : 0.32f);
            float bodyW = w * (child ? 0.50f : (old ? 0.46f : 0.44f));
            float lean = old ? 8f : 0f; // 老人向前驼一点

            Color clothDark = UiFactory.Darken(cloth, 0.14f);
            Color clothDeep = UiFactory.Darken(cloth, 0.28f);
            Color hair = old ? HairGrey : HairDark;
            Color eye = new Color(0.16f, 0.14f, 0.15f, 1f);

            float shoeH = h * 0.035f;
            float legTop = feetY + shoeH + legH;
            float torsoY = legTop + bodyH * 0.5f;
            float torsoTop = legTop + bodyH;
            float headY = torsoTop + headD * 0.62f;

            // 脚下的浅阴影，让脚底"踩住"地面
            UiFactory.Block(root, "FootShadow", UiFactory.WithAlpha(new Color(0.10f, 0.09f, 0.10f), 0.18f), new Vector2(w * 0.70f, h * 0.028f), new Vector2(0f, feetY + h * 0.012f));

            // 鞋与腿
            Bar(root, "ShoeL", ShoeTone, bodyW * 0.36f, shoeH, -bodyW * 0.20f, feetY + shoeH * 0.5f);
            Bar(root, "ShoeR", ShoeTone, bodyW * 0.36f, shoeH, bodyW * 0.20f, feetY + shoeH * 0.5f);
            Bar(root, "LegL", clothDeep, bodyW * 0.28f, legH, -bodyW * 0.20f, feetY + shoeH + legH * 0.5f);
            Bar(root, "LegR", clothDeep, bodyW * 0.28f, legH, bodyW * 0.20f, feetY + shoeH + legH * 0.5f);

            // 身体
            var torso = Bar(root, "Torso", cloth, bodyW, bodyH, lean * 0.4f, torsoY);
            torso.transform.localRotation = Quaternion.Euler(0f, 0f, lean * 0.35f);

            // 手臂与手
            float armH = bodyH * (child ? 0.62f : 0.86f);
            float armAng = old ? 14f : 6f;
            float armHalf = armH * 0.5f;
            float armY = torsoTop - armH * 0.42f;
            Tilt(Bar(root, "ArmL", clothDark, bodyW * 0.17f, armH, -bodyW * 0.60f, armY), -armAng);
            Tilt(Bar(root, "ArmR", clothDark, bodyW * 0.17f, armH, bodyW * 0.60f, armY), armAng);
            float handDx = Mathf.Sin(armAng * Mathf.Deg2Rad) * armHalf;
            float handDy = Mathf.Cos(armAng * Mathf.Deg2Rad) * armHalf;
            UiFactory.Round(root, "HandL", UiFactory.Darken(SkinTone, 0.06f), headD * 0.20f, new Vector2(-bodyW * 0.60f - handDx, armY - handDy));
            UiFactory.Round(root, "HandR", UiFactory.Darken(SkinTone, 0.06f), headD * 0.20f, new Vector2(bodyW * 0.60f + handDx, armY - handDy));

            // 脖子与头
            Bar(root, "Neck", UiFactory.Darken(SkinTone, 0.12f), headD * 0.34f, headD * 0.26f, lean * 0.8f, torsoTop + headD * 0.10f);
            UiFactory.Round(root, "EarL", UiFactory.Darken(SkinTone, 0.06f), headD * 0.18f, new Vector2(lean - headD * 0.5f, headY));
            UiFactory.Round(root, "EarR", UiFactory.Darken(SkinTone, 0.06f), headD * 0.18f, new Vector2(lean + headD * 0.5f, headY));
            UiFactory.Round(root, "Head", SkinTone, headD, new Vector2(lean, headY));

            // 头发：老人是花白、发际线后退，孩子有一撮呆毛
            Bar(root, "Hair", hair, headD * (old ? 1.00f : 0.94f), headD * (old ? 0.20f : 0.30f), lean, headY + headD * (old ? 0.28f : 0.22f));
            if (child)
            {
                Bar(root, "Fringe", hair, headD * 0.40f, headD * 0.14f, lean - headD * 0.18f, headY + headD * 0.16f);
                Bar(root, "Tuft", hair, headD * 0.12f, headD * 0.16f, lean + headD * 0.10f, headY + headD * 0.40f);
            }

            if (old)
            {
                Bar(root, "SideHairL", hair, headD * 0.16f, headD * 0.30f, lean - headD * 0.44f, headY + headD * 0.02f);
                Bar(root, "SideHairR", hair, headD * 0.16f, headD * 0.30f, lean + headD * 0.44f, headY + headD * 0.02f);
                Bar(root, "BrowL", hair, headD * 0.22f, Mathf.Max(1.6f, headD * 0.05f), lean - headD * 0.19f, headY + headD * 0.14f);
                Bar(root, "BrowR", hair, headD * 0.22f, Mathf.Max(1.6f, headD * 0.05f), lean + headD * 0.19f, headY + headD * 0.14f);
                Bar(root, "WrinkleL", UiFactory.Darken(SkinTone, 0.20f), headD * 0.16f, 1.6f, lean - headD * 0.12f, headY + headD * 0.20f);
                Bar(root, "WrinkleR", UiFactory.Darken(SkinTone, 0.20f), headD * 0.16f, 1.6f, lean + headD * 0.12f, headY + headD * 0.20f);

                // 拐杖：从脚边斜撑到腰
                float caneH = (torsoY - feetY) * 0.95f;
                Tilt(Bar(root, "Cane", UiFactory.Darken(WoodTone, 0.10f), Mathf.Max(4f, w * 0.03f), caneH, bodyW * 0.95f, feetY + caneH * 0.5f), -5f);
                Bar(root, "CaneGrip", UiFactory.Darken(WoodTone, 0.22f), w * 0.10f, Mathf.Max(4f, w * 0.03f), bodyW * 0.95f - w * 0.04f, feetY + caneH);
            }

            // 闭着的眼睛（画里的人都不看镜头）与嘴
            Bar(root, "EyeL", eye, headD * 0.18f, Mathf.Max(1.6f, headD * 0.05f), lean - headD * 0.19f, headY + headD * 0.02f);
            Bar(root, "EyeR", eye, headD * 0.18f, Mathf.Max(1.6f, headD * 0.05f), lean + headD * 0.19f, headY + headD * 0.02f);
            Bar(root, "Mouth", UiFactory.Darken(SkinTone, 0.28f), headD * (child ? 0.16f : 0.22f), Mathf.Max(1.6f, headD * 0.045f), lean, headY - headD * 0.28f);

            // 领口与腰带
            Bar(root, "Collar", UiFactory.Lighten(cloth, 0.14f), bodyW * 0.72f, Mathf.Max(2f, h * 0.020f), lean * 0.6f, torsoTop - h * 0.012f);
            if (child)
            {
                Bar(root, "Shorts", UiFactory.Darken(cloth, 0.30f), bodyW * 0.98f, h * 0.030f, lean * 0.5f, torsoY - bodyH * 0.36f);
            }
            else
            {
                Bar(root, "Belt", UiFactory.Darken(cloth, 0.34f), bodyW * 1.02f, h * 0.024f, lean * 0.5f, torsoY - bodyH * 0.20f);
            }

            return root;
        }

        /// <summary>身边的另一个人：一抹几乎看不清的浅淡剪影（长发、裙摆），让人一眼看出"有个人"，但认不出是谁。</summary>
        public static RectTransform BuildCompanion(Transform parent, Vector2 size)
        {
            var root = UiFactory.MakeGroup(parent, "Companion", size, Vector2.zero);

            float w = size.x;
            float h = size.y;
            Color tone = new Color(0.95f, 0.93f, 0.87f, 1f);
            Color a1 = UiFactory.WithAlpha(tone, 0.30f);
            Color a2 = UiFactory.WithAlpha(tone, 0.22f);
            Color a3 = UiFactory.WithAlpha(tone, 0.14f);

            // 外圈柔光：先给一团光，再往里放人
            UiFactory.Round(root, "Halo", UiFactory.WithAlpha(tone, 0.07f), w * 1.45f, new Vector2(0f, h * 0.05f));
            UiFactory.Round(root, "HaloInner", UiFactory.WithAlpha(tone, 0.09f), w * 1.05f, new Vector2(0f, h * 0.02f));

            // 头与长发
            UiFactory.Round(root, "Head", a1, w * 0.26f, new Vector2(0f, h * 0.34f));
            Bar(root, "HairL", a2, w * 0.10f, h * 0.30f, -w * 0.17f, h * 0.24f);
            Bar(root, "HairR", a2, w * 0.10f, h * 0.30f, w * 0.17f, h * 0.24f);

            // 肩与裙：上窄下宽，裙摆淡出
            Bar(root, "Shoulder", a2, w * 0.44f, h * 0.10f, 0f, h * 0.16f);
            Bar(root, "Torso", a1, w * 0.34f, h * 0.26f, 0f, h * 0.00f);
            Bar(root, "SkirtA", a2, w * 0.46f, h * 0.16f, 0f, -h * 0.18f);
            Bar(root, "SkirtB", a3, w * 0.56f, h * 0.16f, 0f, -h * 0.34f);
            Bar(root, "Hem", UiFactory.WithAlpha(tone, 0.08f), w * 0.66f, h * 0.10f, 0f, -h * 0.46f);

            return root;
        }

        // ---------------------------------------------------------------- 第一章：旧罗盘

        /// <summary>旧罗盘：铜壳、玻璃面、指针与刻度，顶部一个挂环（第一章的关键道具）。</summary>
        public static RectTransform BuildCompass(Transform parent, Vector2 size)
        {
            var root = UiFactory.MakeGroup(parent, "Compass", size, Vector2.zero);

            float d = Mathf.Min(size.x, size.y);
            Color brassDark = UiFactory.Darken(UiFactory.Brass, 0.22f);
            Color brassDeep = UiFactory.Darken(UiFactory.Brass, 0.42f);

            // 壳与铜圈
            UiFactory.Round(root, "Case", brassDeep, d, Vector2.zero);
            UiFactory.Round(root, "Body", UiFactory.Brass, d * 0.90f, Vector2.zero);
            UiFactory.Round(root, "Bezel", UiFactory.Lighten(UiFactory.Brass, 0.10f), d * 0.82f, Vector2.zero);
            UiFactory.Round(root, "Glass", UiFactory.WithAlpha(GlassTone, 0.92f), d * 0.66f, Vector2.zero);
            UiFactory.Round(root, "GlassInner", UiFactory.WithAlpha(UiFactory.Lighten(GlassTone, 0.08f), 0.55f), d * 0.44f, Vector2.zero);

            // 八个方位的刻度（正北那一条更长，顶端点一颗琥珀）
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f;
                float rad = ang * Mathf.Deg2Rad;
                bool cardinal = (i % 2) == 0;
                float len = cardinal ? d * 0.10f : d * 0.06f;
                var tick = Bar(root, "Tick" + i, UiFactory.WithAlpha(brassDark, 0.75f), len, Mathf.Max(2f, d * 0.022f),
                    Mathf.Cos(rad) * d * 0.34f, Mathf.Sin(rad) * d * 0.34f);
                Tilt(tick, ang);
            }

            UiFactory.Round(root, "NorthDot", new Color(0.86f, 0.42f, 0.30f, 0.95f), d * 0.08f, new Vector2(0f, d * 0.40f));

            // 指针（整组转 28°，避免一看就是正十字）
            var needle = UiFactory.MakeGroup(root, "Needle", new Vector2(d * 0.3f, d * 0.7f), Vector2.zero);
            needle.localRotation = Quaternion.Euler(0f, 0f, 28f);
            Bar(needle, "NeedleNorth", new Color(0.74f, 0.28f, 0.24f, 1f), d * 0.045f, d * 0.30f, 0f, d * 0.15f);
            Bar(needle, "NeedleSouth", new Color(0.60f, 0.60f, 0.62f, 1f), d * 0.045f, d * 0.30f, 0f, -d * 0.15f);
            UiFactory.Round(needle, "NeedlePin", UiFactory.Lighten(UiFactory.Brass, 0.16f), d * 0.10f, Vector2.zero);

            // 玻璃上的一道反光
            Tilt(Bar(root, "GlassShine", UiFactory.WithAlpha(Color.white, 0.30f), d * 0.30f, Mathf.Max(2f, d * 0.030f), -d * 0.14f, d * 0.16f), 38f);

            // 顶部挂环与铰链
            Bar(root, "Crown", brassDark, d * 0.12f, d * 0.10f, 0f, d * 0.50f);
            UiFactory.Round(root, "CrownRing", UiFactory.Brass, d * 0.15f, new Vector2(0f, d * 0.58f));
            UiFactory.Round(root, "CrownHole", UiFactory.WithAlpha(brassDeep, 0.9f), d * 0.07f, new Vector2(0f, d * 0.58f));
            Bar(root, "Hinge", brassDark, d * 0.06f, d * 0.16f, d * 0.47f, 0f);

            return root;
        }

        // ---------------------------------------------------------------- 第二章：泛黄海图

        /// <summary>泛黄海图：折过的旧图纸 + 海岸线 + 一道虚线航线 + 罗盘玫瑰（第二章的关键道具）。</summary>
        public static RectTransform BuildChart(Transform parent, Vector2 size)
        {
            var root = UiFactory.MakeGroup(parent, "Chart", size, Vector2.zero);

            float w = size.x;
            float h = size.y;

            // 整张纸稍微歪一点，像是随手摊在桌上
            var sheet = UiFactory.MakeGroup(root, "Sheet", size, Vector2.zero);
            sheet.localRotation = Quaternion.Euler(0f, 0f, -3.5f);

            // 纸与影子
            Bar(sheet, "Drop", UiFactory.WithAlpha(new Color(0.10f, 0.09f, 0.08f), 0.22f), w, h, 4f, -5f);
            Bar(sheet, "PaperEdge", UiFactory.Darken(OldPaperTone, 0.14f), w, h, 0f, 0f);
            Bar(sheet, "Paper", OldPaperTone, w * 0.985f, h * 0.982f, 0f, 0f);

            // 茶渍与水痕
            UiFactory.Round(sheet, "StainA", UiFactory.WithAlpha(UiFactory.Amber, 0.09f), h * 0.46f, new Vector2(-w * 0.26f, h * 0.18f));
            UiFactory.Round(sheet, "StainB", UiFactory.WithAlpha(new Color(0.62f, 0.50f, 0.32f), 0.10f), h * 0.34f, new Vector2(w * 0.28f, -h * 0.22f));

            // 折痕：两竖一横（折过四折的样子）
            for (int i = 0; i < 3; i++)
            {
                bool vertical = i < 2;
                float pos = i == 0 ? -w * 0.17f : (i == 1 ? w * 0.17f : 0f);
                if (vertical)
                {
                    Bar(sheet, "Crease" + i, UiFactory.WithAlpha(UiFactory.Darken(OldPaperTone, 0.24f), 0.55f), 3f, h * 0.98f, pos, 0f);
                    Bar(sheet, "CreaseHi" + i, UiFactory.WithAlpha(Color.white, 0.28f), 2f, h * 0.98f, pos + 3f, 0f);
                }
                else
                {
                    Bar(sheet, "CreaseFold", UiFactory.WithAlpha(UiFactory.Darken(OldPaperTone, 0.24f), 0.50f), w * 0.98f, 3f, 0f, pos);
                    Bar(sheet, "CreaseFoldHi", UiFactory.WithAlpha(Color.white, 0.24f), w * 0.98f, 2f, 0f, pos + 3f);
                }
            }

            // 海与陆
            Bar(sheet, "Water", UiFactory.WithAlpha(new Color(0.50f, 0.60f, 0.64f), 0.38f), w * 0.90f, h * 0.34f, -w * 0.02f, -h * 0.30f);
            Color coast = new Color(0.66f, 0.60f, 0.42f, 0.75f);
            for (int i = 0; i < 14; i++)
            {
                float cx = -w * 0.44f + i * w * 0.062f;
                float ch = 14f + ((i * 37) % 26);
                Bar(sheet, "Coast" + i, coast, w * 0.066f, ch, cx, -h * 0.155f + ch * 0.5f);
            }

            // 一座小岛
            Bar(sheet, "IslandA", coast, w * 0.10f, h * 0.06f, w * 0.26f, -h * 0.30f);
            Bar(sheet, "IslandB", coast, w * 0.05f, h * 0.05f, w * 0.31f, -h * 0.25f);

            // 航线：一串小点从左边画到右上方
            for (int i = 0; i < 14; i++)
            {
                float t = i / 13f;
                float px = Mathf.Lerp(-w * 0.30f, w * 0.28f, t);
                float py = Mathf.Lerp(-h * 0.02f, h * 0.24f, t) - Mathf.Sin(t * Mathf.PI) * h * 0.06f;
                UiFactory.Round(sheet, "Route" + i, UiFactory.WithAlpha(new Color(0.60f, 0.28f, 0.22f), 0.85f), 6f + (i % 2) * 2f, new Vector2(px, py));
            }

            // 航线终点画一个红叉
            Tilt(Bar(sheet, "CrossA", new Color(0.70f, 0.24f, 0.20f, 0.95f), w * 0.05f, 5f, w * 0.29f, h * 0.25f), 45f);
            Tilt(Bar(sheet, "CrossB", new Color(0.70f, 0.24f, 0.20f, 0.95f), w * 0.05f, 5f, w * 0.29f, h * 0.25f), -45f);

            // 罗盘玫瑰（右下角）
            var rose = UiFactory.MakeGroup(sheet, "Rose", new Vector2(h * 0.3f, h * 0.3f), new Vector2(w * 0.30f, -h * 0.30f));
            for (int i = 0; i < 4; i++)
            {
                Tilt(Bar(rose, "RoseSpoke" + i, UiFactory.WithAlpha(UiFactory.Darken(OldPaperTone, 0.45f), 0.8f), h * 0.26f, 3f, 0f, 0f), i * 45f);
            }

            UiFactory.Round(rose, "RosePin", UiFactory.WithAlpha(new Color(0.60f, 0.28f, 0.22f), 0.9f), h * 0.035f, Vector2.zero);

            // 比例尺与几笔手写
            Bar(sheet, "ScaleBar", UiFactory.WithAlpha(UiFactory.Darken(OldPaperTone, 0.50f), 0.8f), w * 0.20f, 4f, -w * 0.26f, h * 0.30f);
            for (int i = 0; i < 4; i++)
            {
                Bar(sheet, "ScaleTick" + i, UiFactory.WithAlpha(UiFactory.Darken(OldPaperTone, 0.50f), 0.8f), 3f, 12f, -w * 0.36f + i * w * 0.0665f, h * 0.30f);
            }

            for (int i = 0; i < 4; i++)
            {
                Tilt(Bar(sheet, "Note" + i, UiFactory.WithAlpha(new Color(0.30f, 0.27f, 0.24f), 0.45f), w * 0.16f, 3f, w * 0.20f, h * 0.40f - i * 12f), i % 2 == 0 ? -1.5f : 1.2f);
            }

            return root;
        }

        // ---------------------------------------------------------------- 第三章：纸船

        /// <summary>系红线的纸船：unfolded=false 是折好的小船（船身缠着红线），true 时展开成信纸，写着"我一直都在。"并画了个小笑脸。</summary>
        public static RectTransform BuildPaperBoat(Transform parent, Vector2 size, bool unfolded)
        {
            var root = UiFactory.MakeGroup(parent, unfolded ? "PaperLetter" : "PaperBoat", size, Vector2.zero);

            float w = size.x;
            float h = size.y;

            if (!unfolded)
            {
                Color paper = UiFactory.Paper;
                Color paperShade = UiFactory.Darken(paper, 0.10f);
                Color paperDeep = UiFactory.Darken(paper, 0.22f);

                // 水面上的浅影
                UiFactory.Block(root, "BoatShadow", UiFactory.WithAlpha(new Color(0.10f, 0.10f, 0.12f), 0.16f), new Vector2(w * 0.90f, h * 0.06f), new Vector2(0f, -h * 0.45f));

                // 船中央立起来的折角
                Tilt(Bar(root, "FoldL", paperDeep, w * 0.58f, h * 0.055f, -w * 0.10f, h * 0.10f), 38f);
                Tilt(Bar(root, "FoldR", paperDeep, w * 0.58f, h * 0.055f, w * 0.10f, h * 0.10f), -38f);

                // 船身：底窄上宽
                Bar(root, "HullBottom", paperShade, w * 0.72f, h * 0.14f, 0f, -h * 0.36f);
                Bar(root, "HullMid", paper, w * 0.94f, h * 0.16f, 0f, -h * 0.20f);
                Tilt(Bar(root, "BowFold", paper, w * 0.07f, h * 0.24f, w * 0.44f, -h * 0.10f), -22f);
                Tilt(Bar(root, "SternFold", paper, w * 0.07f, h * 0.24f, -w * 0.44f, -h * 0.10f), 22f);

                // 折缝
                Bar(root, "SeamV", UiFactory.WithAlpha(paperDeep, 0.55f), 3f, h * 0.34f, 0f, h * 0.06f);
                Bar(root, "SeamH", UiFactory.WithAlpha(paperDeep, 0.45f), w * 0.88f, 2.5f, 0f, -h * 0.20f);

                // 缠在船身上的红线
                Bar(root, "ThreadWrap", ThreadRed, w * 1.04f, Mathf.Max(3f, h * 0.045f), 0f, -h * 0.22f);
                Tilt(Bar(root, "ThreadBowA", ThreadRed, w * 0.10f, Mathf.Max(3f, h * 0.035f), -w * 0.44f, -h * 0.15f), 35f);
                Tilt(Bar(root, "ThreadBowB", ThreadRed, w * 0.10f, Mathf.Max(3f, h * 0.035f), -w * 0.44f, -h * 0.15f), -35f);
                UiFactory.Round(root, "ThreadKnot", UiFactory.Darken(ThreadRed, 0.12f), Mathf.Max(4f, h * 0.05f), new Vector2(-w * 0.44f, -h * 0.15f));
                Tilt(Bar(root, "ThreadTail", ThreadRed, w * 0.03f, h * 0.14f, -w * 0.48f, -h * 0.28f), 16f);

                return root;
            }

            // ---- 展开的信纸 ----
            Bar(root, "Drop", UiFactory.WithAlpha(new Color(0.10f, 0.09f, 0.08f), 0.20f), w, h, 4f, -5f);
            Bar(root, "PaperEdge", UiFactory.Darken(UiFactory.Paper, 0.12f), w, h, 0f, 0f);
            Bar(root, "Paper", UiFactory.Paper, w * 0.985f, h * 0.982f, 0f, 0f);

            // 折痕（先折成四折再摊开的印子）
            Bar(root, "CreaseV", UiFactory.WithAlpha(UiFactory.Darken(UiFactory.Paper, 0.20f), 0.55f), 3f, h * 0.98f, 0f, 0f);
            Bar(root, "CreaseVHi", UiFactory.WithAlpha(Color.white, 0.5f), 2f, h * 0.98f, 3f, 0f);
            Bar(root, "CreaseH", UiFactory.WithAlpha(UiFactory.Darken(UiFactory.Paper, 0.20f), 0.50f), w * 0.98f, 3f, 0f, 0f);
            Bar(root, "CreaseHHi", UiFactory.WithAlpha(Color.white, 0.45f), w * 0.98f, 2f, 0f, 3f);
            Bar(root, "CreaseL", UiFactory.WithAlpha(UiFactory.Darken(UiFactory.Paper, 0.16f), 0.35f), w * 0.98f, 2f, 0f, -h * 0.32f);
            Bar(root, "CreaseR", UiFactory.WithAlpha(UiFactory.Darken(UiFactory.Paper, 0.16f), 0.35f), w * 0.98f, 2f, 0f, h * 0.32f);

            // 正文
            int fontSize = Mathf.RoundToInt(Mathf.Clamp(h * 0.13f, 20f, 60f));
            CenteredText(root, "Line", "我一直都在。", fontSize, UiFactory.Ink, w * 0.9f, fontSize * 1.6f, 0f, h * 0.14f);

            // 小笑脸：两只眼睛加一段弧
            float eyeD = Mathf.Max(4f, h * 0.030f);
            UiFactory.Round(root, "SmileEyeL", UiFactory.Ink, eyeD, new Vector2(-w * 0.05f, -h * 0.08f));
            UiFactory.Round(root, "SmileEyeR", UiFactory.Ink, eyeD, new Vector2(w * 0.05f, -h * 0.08f));

            Vector2 smileCenter = new Vector2(0f, -h * 0.10f);
            float smileR = Mathf.Max(8f, h * 0.055f);
            for (int i = 0; i < 5; i++)
            {
                float ang = 205f + i * 32.5f;
                float rad = ang * Mathf.Deg2Rad;
                var seg = Bar(root, "SmileSeg" + i, UiFactory.Ink, smileR * 0.60f, Mathf.Max(2f, h * 0.014f),
                    smileCenter.x + Mathf.Cos(rad) * smileR, smileCenter.y + Mathf.Sin(rad) * smileR);
                Tilt(seg, ang + 90f);
            }

            // 一小截还系在信纸上的红线
            Tilt(Bar(root, "ThreadLine", UiFactory.WithAlpha(ThreadRed, 0.9f), w * 0.66f, Mathf.Max(3f, h * 0.012f), -w * 0.06f, -h * 0.36f), -12f);
            Tilt(Bar(root, "ThreadEndA", ThreadRed, w * 0.03f, h * 0.07f, -w * 0.39f, -h * 0.43f), 28f);
            Tilt(Bar(root, "ThreadEndB", ThreadRed, w * 0.03f, h * 0.07f, -w * 0.40f, -h * 0.40f), -20f);

            return root;
        }

        // ---------------------------------------------------------------- 船舷刻字

        /// <summary>船舷刻字叠加层：木纹上的一行刻痕"往有光的地方去"，整组可以一起淡入淡出。</summary>
        public static RectTransform BuildCarving(Transform parent, Vector2 size)
        {
            var root = UiFactory.MakeGroup(parent, "Carving", size, Vector2.zero);

            float w = size.x;
            float h = size.y;
            Color grain = UiFactory.WithAlpha(UiFactory.Darken(WoodTone, 0.16f), 0.30f);
            Color grainHi = UiFactory.WithAlpha(UiFactory.Lighten(WoodTone, 0.16f), 0.18f);
            Color cut = new Color(0.28f, 0.19f, 0.13f, 0.95f);
            Color cutHi = new Color(0.95f, 0.85f, 0.65f, 0.42f);

            // 木纹
            for (int i = 0; i < 3; i++)
            {
                Tilt(Bar(root, "Grain" + i, grain, w, 2.5f, 0f, (i - 1) * h * 0.30f), i % 2 == 0 ? 1.2f : -1.0f);
            }

            Tilt(Bar(root, "GrainHi", grainHi, w * 0.9f, 2f, w * 0.04f, h * 0.12f), 0.8f);

            // 刻出来的字：先铺一层浅色（凿口的受光边），再压上深色（刻进去的槽）
            int fontSize = Mathf.RoundToInt(Mathf.Clamp(h * 0.44f, 18f, 56f));
            CenteredText(root, "CutHi", "往有光的地方去", fontSize, cutHi, w * 0.96f, fontSize * 1.7f, 2f, -2f);
            CenteredText(root, "CutInk", "往有光的地方去", fontSize, cut, w * 0.96f, fontSize * 1.7f, 0f, 0f);

            // 刻痕两端的凿子印与木屑
            Tilt(Bar(root, "ChiselL", cut, h * 0.34f, 3f, -w * 0.45f, 0f), 18f);
            Tilt(Bar(root, "ChiselR", cut, h * 0.34f, 3f, w * 0.45f, 0f), -18f);
            for (int i = 0; i < 4; i++)
            {
                Bar(root, "Chip" + i, UiFactory.WithAlpha(UiFactory.Darken(WoodTone, 0.05f), 0.5f), w * 0.035f, 2.5f,
                    -w * 0.30f + i * w * 0.19f, -h * 0.40f);
            }

            return root;
        }

        // ---------------------------------------------------------------- 红线

        /// <summary>一截红线：用十几段小方块接出一条微微下垂的曲线，两头散开，端点还留一个结。</summary>
        public static RectTransform BuildRedThread(Transform parent, Vector2 size)
        {
            var root = UiFactory.MakeGroup(parent, "RedThread", size, Vector2.zero);

            float w = size.x;
            float h = size.y;
            float thickness = Mathf.Max(3f, h * 0.12f);
            const int segments = 16;

            float prevX = 0f;
            float prevY = 0f;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float x = -w * 0.5f + w * t;
                float y = -Mathf.Sin(t * Mathf.PI) * h * 0.40f + Mathf.Sin(t * Mathf.PI * 2f) * h * 0.05f;

                if (i > 0)
                {
                    float dx = x - prevX;
                    float dy = y - prevY;
                    float len = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    Color c = (i % 2 == 0) ? ThreadRed : UiFactory.Lighten(ThreadRed, 0.06f);
                    // 先画一道浅影子，让线看起来是搭在东西上的
                    Tilt(Bar(root, "ThreadShadow" + i, UiFactory.WithAlpha(new Color(0.10f, 0.08f, 0.08f), 0.16f), len * 1.1f, thickness, (prevX + x) * 0.5f, (prevY + y) * 0.5f - thickness * 0.8f), ang);
                    Tilt(Bar(root, "ThreadSeg" + i, c, len * 1.12f, thickness, (prevX + x) * 0.5f, (prevY + y) * 0.5f), ang);
                }

                prevX = x;
                prevY = y;
            }

            // 右端打个结，左端散成三股线头
            UiFactory.Round(root, "ThreadKnot", UiFactory.Darken(ThreadRed, 0.10f), thickness * 2.1f, new Vector2(w * 0.5f, prevY));
            UiFactory.Round(root, "ThreadKnotHole", UiFactory.WithAlpha(UiFactory.Darken(ThreadRed, 0.35f), 0.85f), thickness * 0.9f, new Vector2(w * 0.5f, prevY));
            for (int i = 0; i < 3; i++)
            {
                float ang = -34f + i * 26f;
                Tilt(Bar(root, "ThreadFray" + i, ThreadRed, w * 0.10f, Mathf.Max(2f, thickness * 0.7f), -w * 0.5f - w * 0.04f, -h * 0.02f + i * thickness), ang);
            }

            return root;
        }

        // ---------------------------------------------------------------- 行李

        /// <summary>行李堆：少年离开家乡时的箱笼——皮箱、小木箱、布包袱和一卷铺盖（第一章）。</summary>
        public static RectTransform BuildLuggage(Transform parent, Vector2 size)
        {
            var root = UiFactory.MakeGroup(parent, "Luggage", size, Vector2.zero);

            float w = size.x;
            float h = size.y;
            float groundY = -h * 0.5f;

            Color leather = new Color(0.47f, 0.33f, 0.24f, 1f);
            Color leatherDark = UiFactory.Darken(leather, 0.16f);
            Color boxWood = new Color(0.62f, 0.48f, 0.32f, 1f);
            Color clothTone = new Color(0.45f, 0.49f, 0.57f, 1f);

            // 地上的影子
            UiFactory.Block(root, "GroundShadow", UiFactory.WithAlpha(new Color(0.10f, 0.09f, 0.10f), 0.20f), new Vector2(w * 0.98f, h * 0.055f), new Vector2(0f, groundY + h * 0.02f));

            // 后面竖着的一卷铺盖
            Tilt(Bar(root, "Roll", UiFactory.Lighten(boxWood, 0.06f), w * 0.52f, h * 0.11f, w * 0.04f, groundY + h * 0.16f), -7f);
            UiFactory.Round(root, "RollEndL", UiFactory.Darken(boxWood, 0.18f), h * 0.11f, new Vector2(w * 0.04f - w * 0.25f, groundY + h * 0.13f));
            UiFactory.Round(root, "RollEndR", UiFactory.Darken(boxWood, 0.18f), h * 0.11f, new Vector2(w * 0.04f + w * 0.25f, groundY + h * 0.20f));

            // 主皮箱
            float caseW = w * 0.62f;
            float caseH = h * 0.46f;
            float caseX = -w * 0.14f;
            float caseY = groundY + caseH * 0.5f;
            Bar(root, "Case", leather, caseW, caseH, caseX, caseY);
            Bar(root, "CaseLid", UiFactory.Lighten(leather, 0.10f), caseW, caseH * 0.20f, caseX, caseY + caseH * 0.40f);
            Bar(root, "CaseBase", leatherDark, caseW, caseH * 0.10f, caseX, caseY - caseH * 0.45f);

            // 皮箱上的两道皮带与铜扣
            for (int i = 0; i < 2; i++)
            {
                float sx = caseX + (i == 0 ? -caseW * 0.26f : caseW * 0.26f);
                Bar(root, "Strap" + i, leatherDark, caseW * 0.09f, caseH * 0.98f, sx, caseY);
                UiFactory.Round(root, "Buckle" + i, UiFactory.Brass, caseH * 0.11f, new Vector2(sx, caseY + caseH * 0.04f));
                UiFactory.Round(root, "BuckleHole" + i, UiFactory.WithAlpha(UiFactory.Darken(UiFactory.Brass, 0.40f), 0.9f), caseH * 0.05f, new Vector2(sx, caseY + caseH * 0.04f));
            }

            // 提手与行李牌
            Bar(root, "Handle", leatherDark, caseW * 0.20f, Mathf.Max(4f, h * 0.035f), caseX, caseY + caseH * 0.62f);
            Bar(root, "TagString", UiFactory.WithAlpha(UiFactory.Muted, 0.9f), 2.5f, h * 0.06f, caseX + caseW * 0.16f, caseY + caseH * 0.50f);
            Bar(root, "Tag", UiFactory.Lighten(UiFactory.Paper, 0.02f), w * 0.06f, h * 0.075f, caseX + caseW * 0.16f, caseY + caseH * 0.40f);
            Bar(root, "TagLine", UiFactory.WithAlpha(UiFactory.Muted, 0.9f), w * 0.04f, 2f, caseX + caseW * 0.16f, caseY + caseH * 0.40f);

            // 皮箱上叠着的小木箱
            float boxW = w * 0.32f;
            float boxH = h * 0.24f;
            float smallX = caseX + caseW * 0.26f;
            float smallY = caseY + caseH * 0.5f + boxH * 0.5f;
            Bar(root, "SmallBox", boxWood, boxW, boxH, smallX, smallY);
            Bar(root, "SmallBoxLid", UiFactory.Lighten(boxWood, 0.12f), boxW, boxH * 0.18f, smallX, smallY + boxH * 0.40f);
            Bar(root, "SmallBoxRopeV", RopeTone, boxW * 0.08f, boxH * 1.02f, smallX, smallY);
            Bar(root, "SmallBoxRopeH", RopeTone, boxW * 1.02f, boxH * 0.09f, smallX, smallY);

            // 左边地上的布包袱
            float bundleD = h * 0.24f;
            UiFactory.Round(root, "Bundle", clothTone, bundleD, new Vector2(-w * 0.40f, groundY + bundleD * 0.45f));
            UiFactory.Round(root, "BundleTop", UiFactory.Lighten(clothTone, 0.10f), bundleD * 0.62f, new Vector2(-w * 0.40f, groundY + bundleD * 0.62f));
            Bar(root, "BundleCord", UiFactory.Darken(clothTone, 0.30f), bundleD * 1.04f, Mathf.Max(3f, h * 0.018f), -w * 0.40f, groundY + bundleD * 0.45f);
            UiFactory.Round(root, "BundleKnot", UiFactory.Darken(clothTone, 0.40f), bundleD * 0.16f, new Vector2(-w * 0.40f, groundY + bundleD * 0.62f));

            return root;
        }

        // ---------------------------------------------------------------- 工作灯

        /// <summary>工作灯与光晕：吊着的铁罩灯，光只落在下方木板上（第三章的夜晚，根节点放在 offset 处，尺寸固定 260x460）。</summary>
        public static RectTransform BuildWorkLamp(Transform parent, Vector2 offset)
        {
            var root = UiFactory.MakeGroup(parent, "WorkLamp", new Vector2(260f, 460f), offset);

            Color metal = new Color(0.33f, 0.29f, 0.25f, 1f);
            Color metalDark = UiFactory.Darken(metal, 0.12f);
            Color bulb = new Color(1f, 0.95f, 0.80f, 1f);

            // 吊线
            Bar(root, "Cord", UiFactory.WithAlpha(metalDark, 0.95f), 6f, 130f, 0f, 150f);

            // 灯罩：一层层加宽的方块拼出倒梯形
            Bar(root, "ShadeTop", metal, 46f, 14f, 0f, 92f);
            Bar(root, "ShadeMid", metal, 80f, 14f, 0f, 80f);
            Bar(root, "ShadeBot", metal, 112f, 16f, 0f, 66f);
            Bar(root, "ShadeRim", UiFactory.WithAlpha(UiFactory.Lighten(UiFactory.Amber, 0.10f), 0.85f), 112f, 5f, 0f, 58f);

            // 铁笼：三根竖条 + 两道圈
            for (int i = 0; i < 3; i++)
            {
                Bar(root, "Cage" + i, metalDark, 5f, 56f, (i - 1) * 32f, 28f);
            }

            Bar(root, "CageRingA", metalDark, 88f, 5f, 0f, 52f);
            Bar(root, "CageRingB", metalDark, 96f, 5f, 0f, 6f);

            // 灯泡与光晕
            UiFactory.Round(root, "GlowFar", UiFactory.WithAlpha(UiFactory.Amber, 0.06f), 340f, new Vector2(0f, 10f));
            UiFactory.Round(root, "GlowMid", UiFactory.WithAlpha(UiFactory.Amber, 0.12f), 220f, new Vector2(0f, 18f));
            UiFactory.Round(root, "GlowNear", UiFactory.WithAlpha(UiFactory.Amber, 0.22f), 130f, new Vector2(0f, 24f));
            UiFactory.Round(root, "Bulb", bulb, 30f, new Vector2(0f, 26f));
            UiFactory.Round(root, "BulbCore", Color.white, 14f, new Vector2(0f, 26f));

            // 落在木板上的那一摊光（只照亮下面）
            UiFactory.Block(root, "PoolWide", UiFactory.WithAlpha(UiFactory.Amber, 0.10f), new Vector2(420f, 40f), new Vector2(0f, -152f));
            UiFactory.Block(root, "PoolMid", UiFactory.WithAlpha(UiFactory.Amber, 0.14f), new Vector2(320f, 28f), new Vector2(0f, -146f));
            UiFactory.Block(root, "PoolCore", UiFactory.WithAlpha(new Color(0.99f, 0.88f, 0.62f, 1f), 0.20f), new Vector2(200f, 16f), new Vector2(0f, -140f));

            // 被照亮的那几块木板
            Bar(root, "LitPlankA", UiFactory.WithAlpha(UiFactory.Amber, 0.16f), 300f, 6f, 0f, -168f);
            Bar(root, "LitPlankB", UiFactory.WithAlpha(UiFactory.Amber, 0.12f), 360f, 6f, 0f, -186f);

            return root;
        }

        // ---------------------------------------------------------------- 隐藏滑块的三处藏身处

        /// <summary>隐藏滑块的底座：style = "railing" 栏杆里的黄铜槽 / "ring" 系船桩上的旧铜环 / "brass" 工作台上的黄铜轨道（根节点放在 offset 处）。</summary>
        public static RectTransform BuildSliderRail(Transform parent, string style, Vector2 size, Vector2 offset)
        {
            var root = UiFactory.MakeGroup(parent, "SliderRail_" + style, size, offset);

            float w = size.x;
            float t = Mathf.Max(8f, size.y);

            if (style == "ring")
            {
                // ---- 系船桩上的旧铜环 ----
                Color postWood = new Color(0.34f, 0.24f, 0.17f, 1f);

                // 一根绷紧的旧缆绳横穿根节点中心：滑块把手就是沿着它走的
                Bar(root, "RopeRun", RopeTone, w * 0.96f, t * 0.30f, 0f, 0f);
                Bar(root, "RopeRunShade", UiFactory.WithAlpha(UiFactory.Darken(RopeTone, 0.22f), 0.7f), w * 0.96f, t * 0.08f, 0f, -t * 0.12f);
                UiFactory.Round(root, "RopeKnotL", RopeTone, t * 0.46f, new Vector2(-w * 0.46f, 0f));
                UiFactory.Round(root, "RopeKnotR", RopeTone, t * 0.46f, new Vector2(w * 0.46f, 0f));

                // 系船桩：桩身一直伸到画面下方的木栈道里
                Bar(root, "Bollard", postWood, t * 1.1f, t * 5.0f, 0f, -t * 1.9f);
                Bar(root, "BollardShade", UiFactory.Darken(postWood, 0.14f), t * 0.35f, t * 5.0f, -t * 0.40f, -t * 1.9f);
                Bar(root, "BollardCap", UiFactory.Lighten(postWood, 0.10f), t * 1.5f, t * 0.45f, 0f, t * 0.55f);

                // 铜环：12 段小方块围一圈，环心正对根节点中心（滑块从环里穿过），上半圈受光偏亮
                float ringD = Mathf.Min(Mathf.Max(w * 0.5f, t * 2.0f), t * 3.6f);
                float ringY = 0f;
                for (int i = 0; i < 12; i++)
                {
                    float ang = i * 30f;
                    float rad = ang * Mathf.Deg2Rad;
                    Color c = (ang > 30f && ang < 150f)
                        ? UiFactory.Lighten(UiFactory.Brass, 0.16f)
                        : ((ang > 200f && ang < 320f) ? UiFactory.Darken(UiFactory.Brass, 0.14f) : UiFactory.Brass);
                    var seg = Bar(root, "RingSeg" + i, c, ringD * 0.30f, ringD * 0.125f,
                        Mathf.Cos(rad) * ringD * 0.42f, ringY + Mathf.Sin(rad) * ringD * 0.42f);
                    Tilt(seg, ang + 90f);
                }

                // 环上磨亮的一道反光（只在边缘留一点）
                var shine = Bar(root, "RingShine", UiFactory.WithAlpha(new Color(1f, 0.95f, 0.80f), 0.45f), ringD * 0.24f, ringD * 0.035f, 0f, ringY + ringD * 0.44f);
                Tilt(shine, 12f);
                UiFactory.Round(root, "RingPit", UiFactory.WithAlpha(UiFactory.Darken(UiFactory.Brass, 0.45f), 0.7f), ringD * 0.09f, new Vector2(0f, ringY - ringD * 0.42f));

                return root;
            }

            if (style == "brass")
            {
                // ---- 工作台上的黄铜轨道 ----
                Color benchWood = new Color(0.30f, 0.22f, 0.16f, 1f);

                Bar(root, "Bench", benchWood, w * 1.10f, t * 1.6f, 0f, -t * 1.2f);
                Bar(root, "BenchTop", UiFactory.Lighten(benchWood, 0.10f), w * 1.10f, t * 0.22f, 0f, -t * 0.45f);
                UiFactory.Block(root, "BenchShadow", UiFactory.WithAlpha(UiFactory.Darken(benchWood, 0.30f), 0.45f), new Vector2(w * 1.10f, t * 0.25f), new Vector2(0f, -t * 1.9f));

                // 外壳 → 轨道面 → 中间的滑槽
                Bar(root, "TrackCase", UiFactory.Darken(UiFactory.Brass, 0.34f), w, t, 0f, 0f);
                Bar(root, "TrackFace", UiFactory.Brass, w * 0.99f, t * 0.72f, 0f, t * 0.06f);
                Bar(root, "TrackSlot", UiFactory.Darken(UiFactory.Brass, 0.52f), w * 0.94f, t * 0.26f, 0f, t * 0.02f);
                Bar(root, "TrackShine", UiFactory.WithAlpha(UiFactory.Lighten(UiFactory.Brass, 0.26f), 0.65f), w * 0.99f, t * 0.10f, 0f, t * 0.40f);

                // 两端封头与一排螺钉
                Bar(root, "CapL", UiFactory.Darken(UiFactory.Brass, 0.30f), t * 0.5f, t * 1.02f, -w * 0.5f + t * 0.25f, 0f);
                Bar(root, "CapR", UiFactory.Darken(UiFactory.Brass, 0.30f), t * 0.5f, t * 1.02f, w * 0.5f - t * 0.25f, 0f);
                for (int i = 0; i < 5; i++)
                {
                    UiFactory.Round(root, "Screw" + i, UiFactory.WithAlpha(UiFactory.Darken(UiFactory.Brass, 0.45f), 0.9f), t * 0.24f,
                        new Vector2(-w * 0.40f + i * w * 0.20f, -t * 0.30f));
                }

                // 磨出来的一道亮痕与旁边的木屑
                Bar(root, "Wear", UiFactory.WithAlpha(new Color(1f, 0.95f, 0.80f), 0.35f), w * 0.42f, t * 0.06f, w * 0.10f, t * 0.24f);
                Bar(root, "ShavingA", UiFactory.WithAlpha(UiFactory.Lighten(benchWood, 0.30f), 0.5f), w * 0.10f, 3f, -w * 0.62f, -t * 0.55f);
                Bar(root, "ShavingB", UiFactory.WithAlpha(UiFactory.Lighten(benchWood, 0.30f), 0.4f), w * 0.08f, 3f, w * 0.66f, -t * 0.70f);

                return root;
            }

            // ---- 默认：栏杆里的黄铜槽 ----
            Color railWood = new Color(0.38f, 0.27f, 0.19f, 1f);
            Bar(root, "PostL", UiFactory.Darken(railWood, 0.10f), t * 0.9f, t * 5.0f, -w * 0.40f, -t * 2.0f);
            Bar(root, "PostR", UiFactory.Darken(railWood, 0.10f), t * 0.9f, t * 5.0f, w * 0.40f, -t * 2.0f);
            Bar(root, "RailLower", UiFactory.Darken(railWood, 0.16f), w * 0.96f, t * 0.55f, 0f, -t * 3.0f);
            Bar(root, "RailWood", railWood, w, t * 2.4f, 0f, t * 0.2f);
            Bar(root, "RailWoodTop", UiFactory.Lighten(railWood, 0.08f), w, t * 0.35f, 0f, t * 1.25f);
            Bar(root, "RailGroove", UiFactory.Darken(railWood, 0.16f), w * 0.94f, t * 1.0f, 0f, t * 0.15f);

            // 槽底的黄铜条：只在边缘留一点反光
            Bar(root, "RailBrass", UiFactory.Brass, w * 0.92f, t * 0.62f, 0f, t * 0.12f);
            Bar(root, "RailBrassShine", UiFactory.WithAlpha(UiFactory.Lighten(UiFactory.Brass, 0.22f), 0.7f), w * 0.92f, t * 0.10f, 0f, t * 0.36f);
            Bar(root, "RailWear", UiFactory.WithAlpha(UiFactory.Darken(UiFactory.Brass, 0.30f), 0.35f), w * 0.50f, t * 0.07f, -w * 0.12f, t * 0.22f);

            for (int i = 0; i < 3; i++)
            {
                UiFactory.Round(root, "Rivet" + i, UiFactory.WithAlpha(UiFactory.Darken(UiFactory.Brass, 0.10f), 0.9f), t * 0.34f,
                    new Vector2(-w * 0.30f + i * w * 0.30f, t * 0.90f));
            }

            return root;
        }

        // ---------------------------------------------------------------- 滑块把手

        /// <summary>滑块把手的可见造型：黄铜小滑块（根节点就是可点区域，比本体大一圈，方便拖动）。</summary>
        public static RectTransform BuildSliderKnob(Transform parent, Vector2 size)
        {
            // 根节点 = 点击区域：做成一圈很淡的暖色，既提示"这里有东西"，又留出宽裕的抓取范围
            var hit = UiFactory.Block(parent, "SliderKnob", UiFactory.WithAlpha(UiFactory.Amber, 0.10f), size, Vector2.zero);
            hit.raycastTarget = true; // 全工程唯一需要接收拖动的 Image
            var root = hit.rectTransform;

            float w = size.x * 0.60f;
            float h = size.y * 0.60f;

            Bar(root, "KnobBody", UiFactory.Brass, w, h, 0f, 0f);
            Bar(root, "KnobTop", UiFactory.WithAlpha(UiFactory.Lighten(UiFactory.Brass, 0.24f), 0.85f), w, h * 0.18f, 0f, h * 0.36f);
            Bar(root, "KnobBottom", UiFactory.Darken(UiFactory.Brass, 0.20f), w, h * 0.16f, 0f, -h * 0.40f);
            Bar(root, "KnobFlange", UiFactory.Darken(UiFactory.Brass, 0.30f), w * 1.22f, h * 0.16f, 0f, -h * 0.56f);

            for (int i = 0; i < 3; i++)
            {
                Bar(root, "KnobGrip" + i, UiFactory.Darken(UiFactory.Brass, 0.14f), w * 0.08f, h * 0.46f, (i - 1) * w * 0.22f, 0f);
            }

            UiFactory.Round(root, "KnobScrew", UiFactory.WithAlpha(UiFactory.Lighten(UiFactory.Brass, 0.10f), 0.9f), w * 0.14f, Vector2.zero);

            return root;
        }

        // ---------------------------------------------------------------- 柔光

        /// <summary>一团柔光：光晕、记忆褪色的光斑（颜色带 alpha，由调用方给）。</summary>
        public static Image BuildSoftGlow(Transform parent, string name, Color color, float diameter, Vector2 offset)
        {
            return UiFactory.Round(parent, name, color, diameter, offset);
        }

        // ---------------------------------------------------------------- 结局陈列台

        /// <summary>结局的纪念品陈列台：一块小木搁板上并排摆着罗盘、海图、纸船。</summary>
        public static RectTransform BuildKeepsakeShelf(Transform parent, Vector2 size, Vector2 offset)
        {
            var root = UiFactory.MakeGroup(parent, "KeepsakeShelf", size, offset);

            float w = size.x;
            float h = size.y;
            Color shelfWood = new Color(0.42f, 0.30f, 0.21f, 1f);
            float boardY = -h * 0.40f;
            float boardTop = boardY + h * 0.045f;

            // 背板：压暗一点，让三件东西跳出来
            Bar(root, "ShelfBack", UiFactory.WithAlpha(UiFactory.Darken(shelfWood, 0.24f), 0.35f), w * 1.02f, h * 0.86f, 0f, h * 0.02f);
            UiFactory.Round(root, "ShelfWarm", UiFactory.WithAlpha(UiFactory.Amber, 0.10f), h * 0.95f, new Vector2(0f, h * 0.05f));

            // 搁板：板面 + 上沿高光 + 板下阴影
            Bar(root, "ShelfBoard", shelfWood, w, h * 0.09f, 0f, boardY);
            Bar(root, "ShelfEdge", UiFactory.Lighten(shelfWood, 0.14f), w, h * 0.02f, 0f, boardY + h * 0.045f);
            Bar(root, "ShelfShadow", UiFactory.WithAlpha(UiFactory.Darken(shelfWood, 0.30f), 0.55f), w, h * 0.03f, 0f, boardY - h * 0.07f);

            // 两个小托架
            Bar(root, "BracketL", UiFactory.Darken(shelfWood, 0.12f), w * 0.05f, h * 0.06f, -w * 0.34f, boardY - h * 0.075f);
            Bar(root, "BracketR", UiFactory.Darken(shelfWood, 0.12f), w * 0.05f, h * 0.06f, w * 0.34f, boardY - h * 0.075f);

            // 三件纪念品都摆在板面上（复用各自的 builder）
            float itemD = Mathf.Min(w * 0.20f, h * 0.34f);

            var compass = BuildCompass(root, new Vector2(itemD, itemD));
            compass.anchoredPosition = new Vector2(-w * 0.30f, boardTop + itemD * 0.52f);

            var chart = BuildChart(root, new Vector2(w * 0.26f, h * 0.30f));
            chart.anchoredPosition = new Vector2(w * 0.01f, boardTop + h * 0.16f);
            chart.localRotation = Quaternion.Euler(0f, 0f, -4f);

            var boat = BuildPaperBoat(root, new Vector2(w * 0.22f, h * 0.24f), false);
            boat.anchoredPosition = new Vector2(w * 0.30f, boardTop + h * 0.12f);

            // 每件东西脚下的一小圈暖光，让陈列台看起来是被灯照着的
            UiFactory.Round(root, "GlowCompass", UiFactory.WithAlpha(UiFactory.Amber, 0.10f), itemD * 1.5f, new Vector2(-w * 0.30f, boardTop + itemD * 0.5f));
            UiFactory.Round(root, "GlowChart", UiFactory.WithAlpha(UiFactory.Amber, 0.08f), h * 0.42f, new Vector2(w * 0.01f, boardTop + h * 0.15f));
            UiFactory.Round(root, "GlowBoat", UiFactory.WithAlpha(UiFactory.Amber, 0.08f), h * 0.34f, new Vector2(w * 0.30f, boardTop + h * 0.10f));

            // 板上的浮尘
            for (int i = 0; i < 5; i++)
            {
                UiFactory.Round(root, "Mote" + i, UiFactory.WithAlpha(UiFactory.LightInk, 0.16f + 0.05f * (i % 3)),
                    4f + (i % 3) * 2f, new Vector2(-w * 0.34f + i * w * 0.17f, h * 0.10f + (i % 3) * h * 0.10f));
            }

            return root;
        }
    }
}
