using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JieNiYiMiao
{
    /// <summary>舞台上一个人物的运行时句柄。</summary>
    public class ActorView
    {
        public string Id;
        public string Name;
        public ClickableActor Click;
        public RectTransform Root;
        public RectTransform Figure;
        public Image Highlight;
        public Text Mood;
    }

    /// <summary>舞台上关键物件的运行时句柄。</summary>
    public class PropView
    {
        public string Id;
        public string Name;
        public ClickableProp Click;
        public RectTransform Root;
        public Image Highlight;
    }

    /// <summary>某一段记忆的舞台外观参数，由 GameManager 根据"预设状态"算好后传给 SceneBuilder。</summary>
    public class StageContext
    {
        public MemoryLevelData Level;
        public PropState PropState;
        public bool ActorSelected;
        public string MoodOverride;
        public string ActorVisualNote;
        public string PropVisualNote;
        public bool ShowStationPhoto;
        public string PhotoCaption;
        public float WorkProgress = 0.35f;
        public bool MotherWalkingForward;
    }

    /// <summary>
    /// 场景搭建器：负责把"当前状态"翻译成屏幕上的几何造型。
    /// 每次状态变化都会重建舞台（造型很轻量），因此不需要做增量同步，也就不会出现残留状态。
    /// </summary>
    public class SceneBuilder
    {
        public const float CanvasWidth = 1920f;
        public const float CanvasHeight = 1080f;

        private readonly Transform _raycastRoot;
        private readonly Action<string> _onActorClicked;
        private readonly Action<string> _onPropClicked;

        private Transform _backdropLayer;
        private Transform _actorLayer;
        private Transform _propLayer;
        private Transform _fxLayer;
        private Transform _hintLayer;

        /// <summary>会参与射线检测的物体根节点（舞台），必须挂在带 Physics2DRaycaster 的摄像机下面。</summary>
        public Transform RaycastRoot
        {
            get { return _raycastRoot; }
        }

        public SceneBuilder(Transform raycastRoot, Action<string> onActorClicked, Action<string> onPropClicked)
        {
            _raycastRoot = raycastRoot;
            _onActorClicked = onActorClicked;
            _onPropClicked = onPropClicked;
        }

        // ------------------------------------------------------------ 构建

        /// <summary>创建一个屏幕空间画布，并补上 EventSystem。</summary>
        public static Canvas BuildCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            UiFactory.EnsureEventSystem();
            return canvas;
        }

        /// <summary>舞台自己的层：会被反复重建，所以放在独立根节点下。</summary>
        public void BuildLayers(Transform stageRoot)
        {
            _backdropLayer = NewLayer(stageRoot, "Backdrop");
            _actorLayer = NewLayer(stageRoot, "Actors");
            _propLayer = NewLayer(stageRoot, "Props");
            _fxLayer = NewLayer(stageRoot, "Effects");
            _hintLayer = NewLayer(stageRoot, "Hints");
        }

        private static Transform NewLayer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UiFactory.Stretch(go.GetComponent<RectTransform>());
            return go.transform;
        }

        public Transform FxLayer
        {
            get { return _fxLayer; }
        }

        public Transform HintLayer
        {
            get { return _hintLayer; }
        }

        public Transform ActorLayer
        {
            get { return _actorLayer; }
        }

        public Transform PropLayer
        {
            get { return _propLayer; }
        }

        public Transform BackdropLayer
        {
            get { return _backdropLayer; }
        }

        // ------------------------------------------------------------ 舞台

        /// <summary>重建整段记忆的舞台。返回场上的人物句柄。</summary>
        public Dictionary<string, ActorView> RebuildStage(StageContext context)
        {
            ClearLayer(_backdropLayer);
            ClearLayer(_actorLayer);
            ClearLayer(_propLayer);

            var level = context.Level;
            var result = new Dictionary<string, ActorView>();
            var sky = ToColor(level.SkyTone);
            var ground = ToColor(level.GroundTone);

            switch (level.BackdropStyle)
            {
                case "office":
                    PlaceholderArt.BuildOffice(_backdropLayer, sky, ground);
                    break;
                case "attic":
                    PlaceholderArt.BuildAttic(_backdropLayer, sky, ground);
                    break;
                default:
                    PlaceholderArt.BuildStation(_backdropLayer, sky, ground);
                    break;
            }

            // 位置全部来自 StageLayout（纯计算，可被 _build/verify 断言），视图只负责画
            Vector2 actorSize = new Vector2(StageLayout.ActorWidth, StageLayout.ActorHeight);
            Vector2 slot = SlotPosition(level.Slot);
            var actor = BuildActor(level.ActorName, level.ActorName, ToColor(level.ActorTone), ActorAge(level.Index), slot, actorSize, context.ActorSelected, context.MoodOverride);
            result[level.ActorName] = actor;

            // 每关都有第二个人在场：第一关的母亲、第二关的同事、第三关来访的孙女
            float sideX;
            float sideY;
            StageLayout.SideActorAnchor(level, out sideX, out sideY);
            var sideActor = BuildSideActor(level, new Vector2(sideX, sideY));
            if (sideActor != null)
            {
                result[sideActor.Name] = sideActor;
            }

            // 关键物件
            BuildProp(context, actor);

            // 场景细节：由前几关的选择决定是否出现
            if (context.ShowStationPhoto)
            {
                PlaceholderArt.BuildPhoto(_propLayer, new Vector2(690f, 150f));
                var caption = UiFactory.MakeText(_propLayer, "PhotoCaption", context.PhotoCaption, 18,
                    UiFactory.Ink, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(600f, -120f), new Vector2(780f, -70f));
                caption.color = new Color(0.95f, 0.93f, 0.86f, 0.92f);
            }

            return result;
        }

        private ActorView BuildActor(string id, string displayName, Color tone, int age, Vector2 position, Vector2 size, bool selected, string moodOverride)
        {
            var container = new GameObject("Actor_" + id, typeof(RectTransform));
            container.transform.SetParent(_actorLayer, false);
            var containerRt = container.GetComponent<RectTransform>();
            UiFactory.Stretch(containerRt);

            var highlight = PlaceholderArt.Block(container.transform, "SelectionGlow", new Color(1f, 0.87f, 0.55f, 0.34f), size * 1.35f, position + new Vector2(0f, 6f));
            highlight.gameObject.SetActive(selected);

            var figureHolder = new GameObject("Figure", typeof(RectTransform));
            figureHolder.transform.SetParent(container.transform, false);
            var figureRt = figureHolder.GetComponent<RectTransform>();
            figureRt.anchorMin = new Vector2(0.5f, 0.5f);
            figureRt.anchorMax = new Vector2(0.5f, 0.5f);
            figureRt.pivot = new Vector2(0.5f, 0.5f);
            figureRt.sizeDelta = size;
            figureRt.anchoredPosition = position;

            PlaceholderArt.BuildFigure(figureRt, "Body", tone, age, size);

            var mood = UiFactory.MakeText(container.transform, "Mood", moodOverride, 19, new Color(0.16f, 0.15f, 0.18f),
                TextAnchor.LowerCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position + new Vector2(-130f, size.y * 0.5f + 26f), position + new Vector2(130f, size.y * 0.5f + 62f));

            var click = figureRt.gameObject.AddComponent<ClickableActor>();
            click.Configure(id, displayName, _onActorClicked, highlight.rectTransform, mood);
            click.SetSelected(selected);
            StageClickProxy.Attach(figureRt);

            return new ActorView
            {
                Id = id,
                Name = displayName,
                Click = click,
                Root = containerRt,
                Figure = figureRt,
                Highlight = highlight,
                Mood = mood
            };
        }

        private ActorView BuildSideActor(MemoryLevelData level, Vector2 position)
        {
            string id;
            string name;
            Color tone;
            int age;
            Vector2 size = new Vector2(140f, 300f);

            switch (level.BackdropStyle)
            {
                case "office":
                    id = "colleague";
                    name = "还在加班的同事";
                    tone = new Color(0.48f, 0.52f, 0.60f);
                    age = 34;
                    break;
                case "attic":
                    id = "granddaughter";
                    name = "来送饭的孙女";
                    tone = new Color(0.86f, 0.58f, 0.52f);
                    age = 24;
                    break;
                default:
                    id = "mother";
                    name = "追着列车的母亲";
                    tone = new Color(0.80f, 0.46f, 0.44f);
                    age = 36;
                    break;
            }

            var view = BuildActor(id, name, tone, age, position, size, false, name);
            view.Mood.color = new Color(0.22f, 0.20f, 0.22f, 0.90f);

            if (level.BackdropStyle == "station")
            {
                // 母亲举在手里的小木船
                var small = PlaceholderArt.BuildBoat(_propLayer, new Vector2(96f, 64f), ToColor(level.PropTone), true, false, false, true);
                small.anchoredPosition = position + new Vector2(0f, 190f);
            }

            return view;
        }

        private void BuildProp(StageContext context, ActorView actor)
        {
            var level = context.Level;
            var container = new GameObject("Prop_" + level.PropName, typeof(RectTransform));
            container.transform.SetParent(_propLayer, false);
            UiFactory.Stretch(container.GetComponent<RectTransform>());

            // 位置与尺寸由 StageLayout 统一给出：校验程序断言的就是这一份数据，
            // 所以"人物和木船会不会互相挡住"是可以被自动检查的。
            float propX;
            float propY;
            float propWidth;
            float propHeight;
            StageLayout.PropAnchor(level, out propX, out propY, out propWidth, out propHeight);
            Vector2 propPos = new Vector2(propX, propY);
            Vector2 propSize = new Vector2(propWidth, propHeight);
            RectTransform visual;

            switch (level.BackdropStyle)
            {
                case "office":
                    bool ringing = context.PropState == PropState.BeforeEvent;
                    bool faceDown = context.PropState == PropState.ChoiceA;
                    visual = PlaceholderArt.BuildPhone(container.transform, ToColor(level.PropTone), ringing, faceDown, propSize);

                    // 屏幕上的工作进度：继续工作 → 进度条继续变长（画在显示器内部，不挡人物）
                    PlaceholderArt.UpdateProgressBar(_backdropLayer, context.WorkProgress);
                    break;
                case "attic":
                    bool hasOldPlank = context.PropState != PropState.ChoiceA;
                    bool scratched = hasOldPlank;
                    bool fresh = context.PropState == PropState.ChoiceA;
                    bool mended = context.PropState == PropState.ChoiceB;
                    visual = PlaceholderArt.BuildBoat(container.transform, propSize, ToColor(level.PropTone), scratched, fresh, mended, hasOldPlank);
                    break;
                default:
                    bool carried = context.PropState == PropState.BeforeEvent || context.PropState == PropState.ChoiceB;
                    visual = PlaceholderArt.BuildBoat(container.transform, propSize, ToColor(level.PropTone), true, false, false, carried);
                    break;
            }

            visual.anchoredPosition = propPos;

            // 物件高亮与点击范围
            var glowSize = propSize * 1.35f;
            var glow = PlaceholderArt.Block(container.transform, "PropGlow", new Color(0.55f, 0.85f, 0.98f, 0.30f), glowSize, propPos);
            glow.gameObject.SetActive(false);
            glow.transform.SetSiblingIndex(0);

            bool highlightProp = context.PropState == PropState.BeforeEvent || context.PropState == PropState.ChoiceA || context.PropState == PropState.ChoiceB;
            var click = visual.gameObject.AddComponent<ClickableProp>();
            click.Configure(level.PropName, level.PropName, _onPropClicked, glow.rectTransform);
            click.SetHighlighted(highlightProp);
            StageClickProxy.Attach(visual);
        }

        // ------------------------------------------------------------ 结局与反转

        /// <summary>反转演出用的"三个自己叠在一起"。</summary>
        public static List<KeyValuePair<StageSlot, RectTransform>> BuildIdentityOverlap(Transform layer)
        {
            var list = new List<KeyValuePair<StageSlot, RectTransform>>();
            var slots = new[] { StageSlot.Left, StageSlot.Center, StageSlot.Right };
            var tones = new[]
            {
                new Color(0.96f, 0.78f, 0.52f),
                new Color(0.62f, 0.72f, 0.86f),
                new Color(0.86f, 0.80f, 0.70f)
            };
            var ages = new[] { 8, 29, 76 };

            for (int i = 0; i < slots.Length; i++)
            {
                var holder = new GameObject("Overlap_" + slots[i], typeof(RectTransform));
                holder.transform.SetParent(layer, false);
                var rt = holder.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(210f, 420f);
                rt.anchoredPosition = new Vector2(-470f + i * 470f, -20f);
                PlaceholderArt.BuildFigure(rt, "Figure", tones[i], ages[i], new Vector2(210f, 420f));
                list.Add(new KeyValuePair<StageSlot, RectTransform>(slots[i], rt));
            }

            return list;
        }

        /// <summary>三个结局各自的视觉状态。</summary>
        public static RectTransform BuildEndingBoat(Transform layer, EndingKind kind, Color wood)
        {
            var holder = new GameObject("EndingBoat", typeof(RectTransform));
            holder.transform.SetParent(layer, false);
            var rt = holder.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(520f, 330f);
            rt.anchoredPosition = new Vector2(0f, 60f);

            switch (kind)
            {
                case EndingKind.BrandNewBoat:
                    PlaceholderArt.BuildBoat(rt, new Vector2(520f, 330f), new Color(0.90f, 0.79f, 0.58f), false, true, false, false);
                    // 换下来的旧木板被放进抽屉
                    PlaceholderArt.Block(rt, "Drawer", new Color(0.36f, 0.28f, 0.22f), new Vector2(360f, 46f), new Vector2(0f, -250f));
                    PlaceholderArt.Block(rt, "OldPlankInDrawer", new Color(0.55f, 0.40f, 0.28f), new Vector2(150f, 22f), new Vector2(-40f, -240f));
                    break;
                case EndingKind.OldBoatRemains:
                    PlaceholderArt.BuildBoat(rt, new Vector2(520f, 330f), wood, true, false, false, true);
                    // 有一处没补上的洞
                    PlaceholderArt.Block(rt, "Hole", new Color(0.16f, 0.13f, 0.11f), new Vector2(74f, 34f), new Vector2(120f, 46f));
                    break;
                default:
                    PlaceholderArt.BuildBoat(rt, new Vector2(520f, 330f), wood, true, false, true, true);
                    break;
            }

            return rt;
        }

        /// <summary>第三关最后那块旧木板：可以单独被"取走"或"放回"。</summary>
        public static RectTransform BuildLoosePlank(Transform layer, Color wood, Vector2 position)
        {
            var plank = PlaceholderArt.Block(layer, "LoosePlank", wood, new Vector2(240f, 56f), position);
            PlaceholderArt.Block(plank.transform, "CarveA", new Color(0.28f, 0.20f, 0.14f), new Vector2(42f, 8f), new Vector2(-30f, 4f));
            PlaceholderArt.Block(plank.transform, "CarveB", new Color(0.28f, 0.20f, 0.14f), new Vector2(42f, 8f), new Vector2(30f, 4f));
            return plank.rectTransform;
        }

        // ------------------------------------------------------------ 工具

        /// <summary>把与引擎无关的 MemoryTone 转成 Unity 颜色（数据层不依赖 UnityEngine）。</summary>
        public static Color ToColor(MemoryTone tone)
        {
            return ToColor(tone, 1f);
        }

        public static Color ToColor(MemoryTone tone, float alpha)
        {
            return new Color(tone.R, tone.G, tone.B, alpha);
        }

        /// <summary>舞台上下文：由 GameManager 根据当前状态算好后传给 SceneBuilder。</summary>
        public static StageContext ContextFor(MemoryLevelData level, PropState propState)
        {
            return new StageContext { Level = level, PropState = propState };
        }

        public static Vector2 SlotPosition(StageSlot slot)
        {
            float x;
            float y;
            StageLayout.SlotAnchor(slot, out x, out y);
            return new Vector2(x, y);
        }

        private static int ActorAge(int levelIndex)
        {
            switch (levelIndex)
            {
                case 0: return 8;
                case 1: return 29;
                default: return 76;
            }
        }

        public static void ClearLayer(Transform layer)
        {
            if (layer == null)
            {
                return;
            }

            for (int i = layer.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(layer.GetChild(i).gameObject);
            }
        }
    }
}
