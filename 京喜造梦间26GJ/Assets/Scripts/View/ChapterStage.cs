using UnityEngine;
using UnityEngine.UI;

namespace CuoGuo
{
    /// <summary>
    /// 一段记忆的舞台：按 ChapterData 把布景、人物、道具和那枚藏在场景里的滑块搭出来，
    /// 并把时间轴轨迹绑到同名对象上。
    ///
    /// 三章用的是同一套代码，只有数据不同——策划案要求的"三章共享系统"就落在这里。
    /// </summary>
    public class ChapterStage
    {
        public int ChapterIndex;
        public ChapterData Data;
        public RectTransform Root;
        public TimelineBinder Binder;
        public TimeSliderController Slider;

        private Tintable[] _tintables;
        private CanvasGroup _group;

        public bool IsBuilt
        {
            get { return Root != null; }
        }

        /// <summary>
        /// 按 ChapterData 把这一章的布景、人物、道具和那枚藏在场景里的滑块搭出来，
        /// 并把时间轴轨迹绑到同名对象上。
        /// </summary>
        public void Build(RectTransform parent, ChapterData data, Canvas canvas)
        {
            Data = data;
            ChapterIndex = data.Index;

            Root = UiFactory.MakeStretchedGroup(parent, "Chapter_" + (data.Index + 1) + "_" + data.BackdropStyle);

            // 整章的淡入淡出（章节切换与结局闪回都用它），也顺手挡住演出期间的点击。
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;

            BuildContent(data);
            WireUp(data, canvas);
        }

        /// <summary>搭出这一章的全部内容：背景、底座、人物道具、船、暖光、环境色。</summary>
        private void BuildContent(ChapterData data)
        {
            // ---- 布景：有正式美术就用正式美术，没有就退回程序化几何 ----
            BuildBackdropFor(data).name = "Backdrop";

            // ---- 滑块的三处藏身处（世界内物件，不是 HUD）----
            // 底座必须画在人物之前：它嵌在码头边上，人物是在它前面走的，
            // 否则母亲经过时会被一条"浮在半空"的木头挡住。
            RectTransform rail = DockArt.BuildSliderRail(Root, data.SliderStyle,
                new Vector2(data.RailWidth, data.RailThickness), new Vector2(data.RailCenter.X, data.RailCenter.Y));
            rail.name = "SliderRail";
            Tag(rail, 0.6f);

            // ---- 章节专属的人物与关键道具 ----
            switch (data.Index)
            {
                case 0:
                    BuildFamily();
                    break;
                case 1:
                    BuildFriendship();
                    break;
                default:
                    BuildLove();
                    break;
            }

            // ---- 三章共用同一艘旧木船 ----
            // 美术方案要求三章是**同一艘船**（船头方向与船舷上的三角刻痕位置固定），
            // 所以优先用共享木船母版；取不到才退回程序化船体。
            // 宽度按船身宽度给，这样罗盘/刻字那些绝对定位的道具不会跑出船外。
            RectTransform boat = BuildSpritePropByWidth("Boat_Master", "Boat", 660f, 0.85f);
            if (boat == null)
            {
                boat = DockArt.BuildBoat(Root, new Vector2(SceneMetrics.BoatWidth, SceneMetrics.BoatHeight), Tone(data.WoodTone));
                boat.name = "Boat";
                Tag(boat, 0.85f);
            }

            // ---- 暖光层 ----
            Image glow = DockArt.BuildSoftGlow(Root, "Glow", new Color(1f, 0.86f, 0.58f, 0.5f), 420f, Vector2.zero);
            glow.raycastTarget = false;
            Tag(glow.rectTransform, 0.8f);

            // ---- 环境色（铺满整段记忆的一层薄色，做"越深越暖"）----
            RectTransform envTint = UiFactory.MakePanel(Root, "EnvTint", new Color(1f, 1f, 1f, 0f));
            Tag(envTint, 0f); // 它自己就是染色层，不再吃褪色
        }

        /// <summary>把搭好的对象接上逻辑：滑块、时间轴绑定、褪色清单。</summary>
        private void WireUp(ChapterData data, Canvas canvas)
        {
            BuildSlider(data, canvas);

            Binder = Root.gameObject.AddComponent<TimelineBinder>();
            Binder.Bind(data.Timeline, Root);

            _tintables = Root.GetComponentsInChildren<Tintable>(true);
            Hide();
        }

        /// <summary>
        /// 本章背景。优先用美术管线产出的正式背景（Assets/Resources/Art 下）；
        /// 取不到就退回程序化几何占位——第三章那张夜港海报图（A17）因为带着大标题与搜索栏，
        /// 按美术方案的结论不进可玩场景，所以它永远走占位分支。
        /// </summary>
        private RectTransform BuildBackdropFor(ChapterData data)
        {
            // 第一章用的 A03 是"远景 Layer 0"（只有天海远山、没有地面），
            // 单独铺上去人物会站在水上——它必须配 A04/A05 的中景与前景才成立，
            // 而那两张还需要抠图分层。所以第一章本轮继续用程序化布景。
            // 第二章的 A11 是完整场景（自带码头平台），方案也允许"暂作整体场景"，可以直接用。
            string artName = null;
            switch (data.Index)
            {
                case 1: artName = "CH02_BG_Clean"; break;
            }

            RectTransform real = ArtLibrary.PlaceBackground(Root, artName);
            if (real != null)
            {
                Tag(real, 0.72f);
                return real;
            }

            RectTransform fallback = DockArt.BuildBackdrop(Root, data.BackdropStyle,
                Tone(data.SkyTone), Tone(data.SeaTone), Tone(data.DockTone));
            Tag(fallback, 0.72f);
            return fallback;
        }

        /// <summary>
        /// 尽量用正式美术：取到 Sprite 就按目标高度摆一个（等比，绝不拉伸），取不到返回 null，
        /// 由调用方退回程序化造型。这是"增量接入美术"的落点——
        /// 任何一张图缺失都只是那一件退回色块，游戏照常可通关。
        ///
        /// 高度是**从脚底反推的锚点**：Sprite 已按不透明内容裁边，根节点居中，
        /// 所以下边缘就是脚底，和程序化角色的定位规则完全一致，时间轴关键帧一行都不用改。
        /// </summary>
        private RectTransform BuildSpriteProp(string artName, string trackName, float targetHeight, float decayWeight)
        {
            Sprite sprite = ArtLibrary.Get(artName);
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject(trackName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(Root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            float aspect = sprite.rect.width / sprite.rect.height;
            rt.sizeDelta = new Vector2(targetHeight * aspect, targetHeight);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            Tag(rt, decayWeight);
            return rt;
        }

        /// <summary>按目标宽度摆 Sprite（木船这种横向主体用宽度控制更直观）。</summary>
        private RectTransform BuildSpritePropByWidth(string artName, string trackName, float targetWidth, float decayWeight)
        {
            Sprite sprite = ArtLibrary.Get(artName);
            if (sprite == null)
            {
                return null;
            }

            return BuildSpriteProp(artName, trackName, targetWidth * (sprite.rect.height / sprite.rect.width), decayWeight);
        }

        /// <summary>
        /// 造一个站在栈道上的角色。身高/身宽从 Core 的 GroundActors 读，
        /// 保证"布景建的尺寸"和"校验程序核对脚底用的尺寸"永远是同一份数字。
        /// </summary>
        private RectTransform BuildFigureFor(string track, Color cloth, int age, string artName)
        {
            GroundActor spec = Data.FindActor(track);
            float w = spec != null ? spec.Width : 160f;
            float h = spec != null ? spec.Height : 320f;

            // 优先用正式角色贴图：按 GroundActors 里的身高摆，脚底自然落在栈道上。
            RectTransform art = BuildSpriteProp(artName, track, h, 0.85f);
            if (art != null)
            {
                return art;
            }

            RectTransform figure = DockArt.BuildFigure(Root, track, cloth, age, new Vector2(w, h));
            Tag(figure, 0.85f);
            return figure;
        }

        /// <summary>道具的统一入口：优先用正式贴图，取不到退回程序化造型。</summary>
        private RectTransform BuildProp(string track, string artName, float artHeight, float decayWeight, System.Func<RectTransform> fallback)
        {
            RectTransform art = BuildSpriteProp(artName, track, artHeight, decayWeight);
            if (art != null)
            {
                return art;
            }

            RectTransform procedural = fallback();
            Tag(procedural, decayWeight);
            return procedural;
        }

        private void BuildFamily()
        {
            BuildFigureFor("Mother", new Color(0.78f, 0.50f, 0.44f), 38, "CH01_Mother");
            BuildFigureFor("Son", new Color(0.42f, 0.50f, 0.62f), 15, "CH01_Boy");

            RectTransform luggage = DockArt.BuildLuggage(Root, new Vector2(210f, 120f));
            luggage.name = "Luggage";
            Tag(luggage, 0.9f);

            // 关键道具：罗盘与刻字。它们在借时债务很高时也必须看得清（DecayWeight 很小）。
            BuildProp("Compass", "Compass", 84f, 0.18f,
                delegate { return DockArt.BuildCompass(Root, new Vector2(72f, 72f)); });

            RectTransform carving = DockArt.BuildCarving(Root, new Vector2(300f, 84f));
            carving.name = "Carving";
            Tag(carving, 0.15f);

            // 船底那块"不自然的阴影"（策划案第一章当前画面明确要求）。
            // 它盖住船底与船缝，让玩家隐约觉得"这里好像有什么"——
            // 时间往回走时阴影会退开，罗盘正是从那底下浮出来的。
            // 刻意用几块角度不一致的暗块叠出来，边缘不顺船板，才"不自然"。
            RectTransform shadow = UiFactory.MakeGroup(Root, "HullShadow", new Vector2(540f, 96f), new Vector2(286f, -236f));
            UiFactory.Block(shadow, "ShadowA", new Color(0.05f, 0.05f, 0.07f, 0.82f), new Vector2(548f, 86f), Vector2.zero)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 1.2f);
            UiFactory.Block(shadow, "ShadowB", new Color(0.05f, 0.05f, 0.07f, 0.62f), new Vector2(430f, 52f), new Vector2(-46f, 18f))
                .transform.localRotation = Quaternion.Euler(0f, 0f, -2.6f);
            UiFactory.Block(shadow, "ShadowC", new Color(0.04f, 0.04f, 0.06f, 0.52f), new Vector2(300f, 38f), new Vector2(64f, -14f))
                .transform.localRotation = Quaternion.Euler(0f, 0f, 3.4f);
            Tag(shadow, 0.95f);
        }

        private void BuildFriendship()
        {
            BuildFigureFor("Youth", new Color(0.52f, 0.58f, 0.66f), 27, "CH02_YoungMan");

            RectTransform companion = DockArt.BuildCompanion(Root, new Vector2(150f, 280f));
            companion.name = "Companion";
            Tag(companion, 0.95f);

            BuildFigureFor("Friend", new Color(0.60f, 0.48f, 0.38f), 27, "CH02_Friend");

            BuildProp("Chart", "CH02_SeaChart", 96f, 0.18f,
                delegate { return DockArt.BuildChart(Root, new Vector2(120f, 92f)); });
        }

        private void BuildLove()
        {
            // A18（中年男人）的抠图在深蓝外套的白色线稿缝上被啃出了缺口，暂不接入，
            // 按方案"先使用可接受的程序遮罩/占位替代并登记待优化"处理，等重抠。
            BuildFigureFor("Man", new Color(0.46f, 0.44f, 0.50f), 46, null);

            RectTransform companion = DockArt.BuildCompanion(Root, new Vector2(150f, 280f));
            companion.name = "Companion";
            Tag(companion, 0.95f);

            // A19 是剪影海报，不能承担角色动作（方案原文），女孩继续用程序化角色。
            BuildFigureFor("Woman", new Color(0.78f, 0.56f, 0.50f), 25, null);

            // 折起来的纸船、展开的信纸、红线：第三章的三件关键线索。
            BuildProp("Letter", "CH03_PaperBoat", 92f, 0.2f,
                delegate { return DockArt.BuildPaperBoat(Root, new Vector2(112f, 86f), false); });

            RectTransform open = DockArt.BuildPaperBoat(Root, new Vector2(190f, 150f), true);
            open.name = "LetterOpen";
            Tag(open, 0.15f);

            RectTransform thread = DockArt.BuildRedThread(Root, new Vector2(90f, 60f));
            thread.name = "RedThread";
            Tag(thread, 0.25f);

            RectTransform lamp = DockArt.BuildWorkLamp(Root, new Vector2(60f, -180f));
            Tag(lamp, 0.9f);

            // 工作灯的光池：只落在木板上（策划案第三章当前画面："工作灯只照亮木板"）。
            // 窄而亮的一团，压在船身中下部；越往过去越亮，像是记忆慢慢对准了那处夹层。
            Image pool = DockArt.BuildSoftGlow(Root, "WorkLight", new Color(1f, 0.86f, 0.54f, 0.55f), 340f, new Vector2(248f, -208f));
            pool.raycastTarget = false;
            Tag(pool.rectTransform, 0.85f);
        }

        // ------------------------------------------------------------ 滑块本体

        private void BuildSlider(ChapterData data, Canvas canvas)
        {
            Vector2 railCenter = new Vector2(data.RailCenter.X, data.RailCenter.Y);
            Vector2 knobSize = new Vector2(data.SliderKnobSize.X, data.SliderKnobSize.Y);

            // 命中区域：透明的可点区域，覆盖整条轨道。
            // 它保证"滑块不会被 UI 或遮罩挡住而无法操作"，同时让玩家点轨道任意处就能开始拖。
            Image hit = UiFactory.Block(Root, "SliderHitArea",
                new Color(1f, 1f, 1f, 0f),
                new Vector2(data.RailWidth + 120f, Mathf.Max(60f, data.RailHitHeight)),
                railCenter);
            hit.raycastTarget = true;
            var hitArea = hit.gameObject.AddComponent<TimeSliderController.RailHitArea>();

            // 发现提示的柔光（一开始几乎不可见）。
            Image hint = DockArt.BuildSoftGlow(Root, "SliderHint", new Color(1f, 0.86f, 0.48f, 0f), 190f, railCenter);
            hint.raycastTarget = false;

            // 把手：可拖动的那一枚黄铜滑块。放在命中区之后，射线优先打到它，
            // 这样"抓把手"和"点轨道"是两种手感：抓把手不会瞬移，点轨道会跳到那一点。
            RectTransform knob = DockArt.BuildSliderKnob(Root, knobSize);
            knob.name = "SliderKnob";
            Tag(knob, 0.0f); // 滑块本身不能被褪色吃掉，否则玩家会以为它消失了
            var grabber = knob.gameObject.AddComponent<TimeSliderController.KnobGrabber>();

            Slider = Root.gameObject.AddComponent<TimeSliderController>();
            Slider.Setup(Root, knob, railCenter, data.RailWidth, canvas);
            Slider.SetHintGlow(hint.rectTransform);

            grabber.Controller = Slider;
            hitArea.Controller = Slider;
            grabber.HoverChanged = SetHoverState;
            hitArea.HoverChanged = SetHoverState;

            Slider.SetPositionSilently(1f);

            // 新搭好的舞台默认锁住滑块：只有真正的开场旁白播完、玩家可以操作时才解锁，
            // 也不会出现"还藏在别的章节里就能被拖到"的情况。
            Slider.SetInteractable(false);
        }

        /// <summary>鼠标悬停到滑块上时给一点金属反光（由 GameManager 转发音效）。</summary>
        public System.Action<bool> HoverChanged;

        private void SetHoverState(bool hover)
        {
            if (HoverChanged != null)
            {
                HoverChanged(hover);
            }
        }

        // ------------------------------------------------------------ 显隐、褪色

        public void Show()
        {
            if (Root != null)
            {
                Root.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            SetSliderInteractable(false);
            if (Root != null)
            {
                Root.gameObject.SetActive(false);
            }
        }

        /// <summary>整章透明度（结局闪回与章节切换用）。</summary>
        public void SetAlpha(float alpha)
        {
            if (_group != null)
            {
                _group.alpha = Mathf.Clamp01(alpha);
            }
        }

        /// <summary>把借时债务的损坏程度铺到这一章的所有物件上。</summary>
        public void ApplyDecay(float decay01)
        {
            if (_tintables == null)
            {
                return;
            }

            for (int i = 0; i < _tintables.Length; i++)
            {
                if (_tintables[i] != null)
                {
                    _tintables[i].SetDecay(decay01);
                }
            }
        }

        /// <summary>结局演出用：把时间停在某个连续位置并刷新画面。</summary>
        public void PoseAt(float position)
        {
            if (Slider != null)
            {
                Slider.SetPositionSilently(position);
            }

            if (Binder != null)
            {
                Binder.Apply(position);
            }
        }

        public void SetSliderInteractable(bool value)
        {
            if (Slider != null)
            {
                Slider.SetInteractable(value);
            }
        }

        private static void Tag(RectTransform target, float decayWeight)
        {
            if (target == null)
            {
                return;
            }

            // 幂等：场景里已经烘焙好的对象身上已经有 Tintable，不要再挂一个。
            var tint = target.GetComponent<Tintable>();
            if (tint == null)
            {
                tint = target.gameObject.AddComponent<Tintable>();
            }

            tint.DecayWeight = decayWeight;
        }

        private static Color Tone(Rgba c)
        {
            return new Color(c.R, c.G, c.B, c.A);
        }
    }
}
