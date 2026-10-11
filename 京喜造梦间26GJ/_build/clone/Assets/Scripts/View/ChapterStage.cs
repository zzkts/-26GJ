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

        public void Build(RectTransform parent, ChapterData data, Canvas canvas)
        {
            Data = data;
            ChapterIndex = data.Index;

            Root = UiFactory.MakeStretchedGroup(parent, "Chapter_" + (data.Index + 1) + "_" + data.BackdropStyle);

            // 整章的淡入淡出（章节切换与结局闪回都用它），也顺手挡住演出期间的点击。
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;

            // ---- 布景 ----
            RectTransform backdrop = DockArt.BuildBackdrop(Root, data.BackdropStyle,
                Tone(data.SkyTone), Tone(data.SeaTone), Tone(data.DockTone));
            backdrop.name = "Backdrop";
            Tag(backdrop, 0.72f);

            // ---- 三章共用同一艘旧木船 ----
            RectTransform boat = DockArt.BuildBoat(Root, new Vector2(SceneMetrics.BoatWidth, SceneMetrics.BoatHeight), Tone(data.WoodTone));
            boat.name = "Boat";
            Tag(boat, 0.85f);

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

            // ---- 暖光层 ----
            Image glow = DockArt.BuildSoftGlow(Root, "Glow", new Color(1f, 0.86f, 0.58f, 0.5f), 420f, Vector2.zero);
            glow.raycastTarget = false;
            Tag(glow.rectTransform, 0.8f);

            // ---- 环境色（铺满整段记忆的一层薄色，做"越深越暖"）----
            RectTransform envTint = UiFactory.MakePanel(Root, "EnvTint", new Color(1f, 1f, 1f, 0f));
            Tag(envTint, 0f); // 它自己就是染色层，不再吃褪色

            BuildSlider(data, canvas);

            Binder = Root.gameObject.AddComponent<TimelineBinder>();
            Binder.Bind(data.Timeline, Root);

            _tintables = Root.GetComponentsInChildren<Tintable>(true);
            Hide();
        }

        /// <summary>
        /// 造一个站在栈道上的角色。身高/身宽从 Core 的 GroundActors 读，
        /// 保证"布景建的尺寸"和"校验程序核对脚底用的尺寸"永远是同一份数字。
        /// </summary>
        private RectTransform BuildFigureFor(string track, Color cloth, int age)
        {
            GroundActor spec = Data.FindActor(track);
            float w = spec != null ? spec.Width : 160f;
            float h = spec != null ? spec.Height : 320f;

            RectTransform figure = DockArt.BuildFigure(Root, track, cloth, age, new Vector2(w, h));
            Tag(figure, 0.85f);
            return figure;
        }

        private void BuildFamily()
        {
            BuildFigureFor("Mother", new Color(0.78f, 0.50f, 0.44f), 38);
            BuildFigureFor("Son", new Color(0.42f, 0.50f, 0.62f), 15);

            RectTransform luggage = DockArt.BuildLuggage(Root, new Vector2(210f, 120f));
            luggage.name = "Luggage";
            Tag(luggage, 0.9f);

            // 关键道具：罗盘与刻字。它们在借时债务很高时也必须看得清（DecayWeight 很小）。
            RectTransform compass = DockArt.BuildCompass(Root, new Vector2(72f, 72f));
            compass.name = "Compass";
            Tag(compass, 0.18f);

            RectTransform carving = DockArt.BuildCarving(Root, new Vector2(300f, 84f));
            carving.name = "Carving";
            Tag(carving, 0.15f);
        }

        private void BuildFriendship()
        {
            BuildFigureFor("Youth", new Color(0.52f, 0.58f, 0.66f), 27);

            RectTransform companion = DockArt.BuildCompanion(Root, new Vector2(150f, 280f));
            companion.name = "Companion";
            Tag(companion, 0.95f);

            BuildFigureFor("Friend", new Color(0.60f, 0.48f, 0.38f), 27);

            RectTransform chart = DockArt.BuildChart(Root, new Vector2(120f, 92f));
            chart.name = "Chart";
            Tag(chart, 0.18f);
        }

        private void BuildLove()
        {
            BuildFigureFor("Man", new Color(0.46f, 0.44f, 0.50f), 46);

            RectTransform companion = DockArt.BuildCompanion(Root, new Vector2(150f, 280f));
            companion.name = "Companion";
            Tag(companion, 0.95f);

            BuildFigureFor("Woman", new Color(0.78f, 0.56f, 0.50f), 25);

            // 折起来的纸船、展开的信纸、红线：第三章的三件关键线索。
            RectTransform letter = DockArt.BuildPaperBoat(Root, new Vector2(112f, 86f), false);
            letter.name = "Letter";
            Tag(letter, 0.2f);

            RectTransform open = DockArt.BuildPaperBoat(Root, new Vector2(190f, 150f), true);
            open.name = "LetterOpen";
            Tag(open, 0.15f);

            RectTransform thread = DockArt.BuildRedThread(Root, new Vector2(90f, 60f));
            thread.name = "RedThread";
            Tag(thread, 0.25f);

            RectTransform lamp = DockArt.BuildWorkLamp(Root, new Vector2(60f, -180f));
            Tag(lamp, 0.9f);
        }

        // ------------------------------------------------------------ 滑块本体

        private void BuildSlider(ChapterData data, Canvas canvas)
        {
            Vector2 railCenter = new Vector2(data.RailCenter.X, data.RailCenter.Y);

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
            RectTransform knob = DockArt.BuildSliderKnob(Root, new Vector2(data.SliderKnobSize.X, data.SliderKnobSize.Y));
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

            var tint = target.gameObject.AddComponent<Tintable>();
            tint.DecayWeight = decayWeight;
        }

        private static Color Tone(Rgba c)
        {
            return new Color(c.R, c.G, c.B, c.A);
        }
    }
}
