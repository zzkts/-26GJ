using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JieNiYiMiao
{
    /// <summary>
    /// 《借你一秒》的视图与总控。
    ///
    /// 分工（刻意拆开，避免规则散落在两处）：
    /// - 流程规则全部在 GameFlow 里（关卡推进、倒流唯一性、变量累积、结局判定），
    ///   它不引用 UnityEngine，因此可以被独立编译与运行校验；
    /// - 这个类只做"把状态画出来"和"把演出播完"，以及把玩家输入转交给 GameFlow。
    ///
    /// 倒流实现遵循手册：不做真正的物理时间倒带，只做"预设状态切换"
    /// （事件发生后 / 倒流恢复 / 选择 A / 选择 B 四种状态，见 PropState）。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private readonly GameFlow _flow = new GameFlow();
        private Dictionary<string, ActorView> _actors = new Dictionary<string, ActorView>();

        private MemoryChoice _pendingChoice;
        private string _heartHint = string.Empty;

        private Transform _raycastRoot;
        private Transform _stageRoot;
        private Transform _overlayRoot;    // 倒流特效（不随舞台重建而消失）
        private Transform _uiRoot;

        private SceneBuilder _builder;

        private Text _chapterText;
        private Text _statsText;
        private Text _narrativeText;
        private Text _promptText;
        private Text _resultText;
        private Text _identityText;
        private Text _heartText;
        private Text _endingTitleText;
        private GroupFader _titleGroup;
        private GroupFader _hudGroup;
        private GroupFader _choiceGroup;
        private GroupFader _endingGroup;
        private GroupFader _revealGroup;
        private Button _rewindButton;
        private Button _choiceAButton;
        private Button _choiceBButton;
        private Button _nextLevelButton;
        private Button _restartMemoryButton;
        private Button _replayButton;

        // ------------------------------------------------------------ 生命周期

        private void Start()
        {
            BuildUi();
            EnterTitle();
        }

        // ------------------------------------------------------------ UI 搭建

        private void BuildUi()
        {
            // 背景层：始终存在，负责整体色调
            var backgroundCanvas = SceneBuilder.BuildCanvas("BackgroundCanvas", 0);
            var bg = PlaceholderArt.Block(backgroundCanvas.transform, "Background", new Color(0.08f, 0.09f, 0.12f), new Vector2(4000f, 2400f), Vector2.zero);
            bg.raycastTarget = false;

            // 舞台层：所有可点击造型都挂在这里
            var stageCanvas = SceneBuilder.BuildCanvas("StageCanvas", 10);
            _raycastRoot = stageCanvas.transform;
            _stageRoot = new GameObject("Stage", typeof(RectTransform)).transform;
            _stageRoot.SetParent(_raycastRoot, false);
            UiFactory.Stretch(_stageRoot as RectTransform);

            // UI 层
            var uiCanvas = SceneBuilder.BuildCanvas("UiCanvas", 20);
            _uiRoot = uiCanvas.transform;

            // 特效层
            var fxCanvas = SceneBuilder.BuildCanvas("FxCanvas", 30);
            _overlayRoot = fxCanvas.transform;

            _builder = new SceneBuilder(_raycastRoot, OnActorClicked, OnPropClicked);
            _builder.BuildLayers(_stageRoot);

            BuildTopBar();
            BuildBottomPanel();
            BuildChoiceBar();
            BuildEndingPanel();
            BuildRevealOverlay();
        }

        private void BuildTopBar()
        {
            var bar = new GameObject("TopBar", typeof(RectTransform));
            bar.transform.SetParent(_uiRoot, false);
            var barRt = bar.GetComponent<RectTransform>();
            UiFactory.Stretch(barRt);

            _chapterText = UiFactory.MakeText(bar.transform, "Chapter", string.Empty, 26, new Color(0.94f, 0.87f, 0.72f),
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -95f), new Vector2(900f, -50f));

            _statsText = UiFactory.MakeText(bar.transform, "Stats", string.Empty, 24, new Color(0.80f, 0.82f, 0.88f),
                TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-470f, -95f), new Vector2(-70f, -50f));

            _heartText = UiFactory.MakeText(bar.transform, "HeartHint", string.Empty, 17, new Color(0.55f, 0.62f, 0.72f),
                TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, -46f), new Vector2(-70f, -20f));

            // 常驻的两个辅助按钮
            _restartMemoryButton = UiFactory.MakeButton(bar.transform, "RestartMemory", "重开本段记忆", new Vector2(180f, 48f),
                new Vector2(0f, 0f), new Vector2(60f, 40f), new Color(0.22f, 0.24f, 0.30f), OnRestartMemoryClicked);

            _replayButton = UiFactory.MakeButton(bar.transform, "Replay", "从第一段记忆重新开始", new Vector2(240f, 48f),
                new Vector2(0f, 0f), new Vector2(260f, 40f), new Color(0.22f, 0.24f, 0.30f), OnRestartClicked);

            _titleGroup = bar.gameObject.AddComponent<GroupFader>();
            _titleGroup.Capture(true);
        }

        private void BuildBottomPanel()
        {
            var panel = new GameObject("BottomPanel", typeof(RectTransform));
            panel.transform.SetParent(_uiRoot, false);
            var panelRt = panel.GetComponent<RectTransform>();
            UiFactory.Stretch(panelRt);
            _hudGroup = panel.AddComponent<GroupFader>();

            var strip = PlaceholderArt.Block(panel.transform, "PanelStrip", new Color(0.06f, 0.07f, 0.10f, 0.86f), new Vector2(4000f, 340f), new Vector2(0f, -355f));
            strip.raycastTarget = false;

            _narrativeText = UiFactory.MakeText(panel.transform, "Narrative", string.Empty, 30, new Color(0.96f, 0.94f, 0.90f),
                TextAnchor.UpperLeft, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-880f, 234f), new Vector2(880f, 334f));

            _promptText = UiFactory.MakeText(panel.transform, "Prompt", string.Empty, 25, new Color(0.92f, 0.78f, 0.48f),
                TextAnchor.UpperLeft, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-880f, 180f), new Vector2(880f, 228f));

            _resultText = UiFactory.MakeText(panel.transform, "Result", string.Empty, 25, new Color(0.72f, 0.86f, 0.78f),
                TextAnchor.UpperLeft, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-880f, 128f), new Vector2(880f, 176f));
        }

        private void BuildChoiceBar()
        {
            var bar = new GameObject("ChoiceBar", typeof(RectTransform));
            bar.transform.SetParent(_uiRoot, false);
            var barRt = bar.GetComponent<RectTransform>();
            UiFactory.Stretch(barRt);
            _choiceGroup = bar.AddComponent<GroupFader>();

            _rewindButton = UiFactory.MakeButton(bar.transform, "Rewind", "借他一秒", new Vector2(340f, 86f),
                new Vector2(0.5f, 1f), new Vector2(-186f, -170f), UiFactory.RewindTint, OnRewindClicked);

            _choiceAButton = UiFactory.MakeButton(bar.transform, "ChoiceA", "选择 A", new Vector2(300f, 96f),
                new Vector2(0.5f, 0f), new Vector2(-170f, 96f), new Color(0.72f, 0.52f, 0.28f), OnChoiceAClicked);

            _choiceBButton = UiFactory.MakeButton(bar.transform, "ChoiceB", "选择 B", new Vector2(300f, 96f),
                new Vector2(1f, 0f), new Vector2(-80f, 96f), new Color(0.34f, 0.52f, 0.62f), OnChoiceBClicked);

            _nextLevelButton = UiFactory.MakeButton(bar.transform, "NextLevel", "进入下一段记忆", new Vector2(280f, 76f),
                new Vector2(1f, 1f), new Vector2(-80f, -160f), new Color(0.42f, 0.40f, 0.62f), OnNextLevelClicked);

            _choiceGroup.Capture(false);
        }

        private void BuildEndingPanel()
        {
            var panel = new GameObject("EndingPanel", typeof(RectTransform));
            panel.transform.SetParent(_uiRoot, false);
            var panelRt = panel.GetComponent<RectTransform>();
            UiFactory.Stretch(panelRt);
            _endingGroup = panel.AddComponent<GroupFader>();

            var dim = PlaceholderArt.Block(panel.transform, "EndingDim", new Color(0.05f, 0.06f, 0.09f, 0.92f), new Vector2(4000f, 2400f), Vector2.zero);
            dim.raycastTarget = false;

            _endingTitleText = UiFactory.MakeText(panel.transform, "EndingTitle", string.Empty, 56, new Color(0.97f, 0.90f, 0.72f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-700f, 230f), new Vector2(700f, 330f));

            _endingGroup.Capture(false);
        }

        private void BuildRevealOverlay()
        {
            var panel = new GameObject("RevealPanel", typeof(RectTransform));
            panel.transform.SetParent(_uiRoot, false);
            var panelRt = panel.GetComponent<RectTransform>();
            UiFactory.Stretch(panelRt);
            _revealGroup = panel.AddComponent<GroupFader>();

            var dim = PlaceholderArt.Block(panel.transform, "RevealDim", new Color(0.06f, 0.04f, 0.10f, 0.90f), new Vector2(4000f, 2400f), Vector2.zero);
            dim.raycastTarget = false;

            _identityText = UiFactory.MakeText(panel.transform, "Identity", string.Empty, 40, new Color(0.96f, 0.92f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-820f, 250f), new Vector2(820f, 380f));

            _revealGroup.Capture(false);
        }

        // ------------------------------------------------------------ 视图刷新

        /// <summary>标题界面。</summary>
        private void EnterTitle()
        {
            _flow.RestartFromBeginning();
            _pendingChoice = null;
            _heartHint = "操作：先点人物 → 点“借他一秒” → 在两个行动里选一个";

            SetNarrative("《借你一秒》\n生命最后的走马灯里，你没办法让自己的时间倒流。\n你只能替记忆里的其他人，退回几秒。");
            SetPrompt("点下面这个按钮开始。");
            ClearResult();
            _actors.Clear();
            _actors = _builder.RebuildStage(SceneBuilder.ContextFor(MemoryBook.Get(0), PropState.AfterEvent));
            SceneBuilder.ClearLayer(_builder.HintLayer);
            SceneBuilder.ClearLayer(_builder.FxLayer);

            _chapterText.text = "借你一秒  ·  26GJ 原型";
            SetHeartHint(_heartHint);
            UpdateStats();

            _titleGroup.SetVisible(false);
            _hudGroup.SetVisible(true);
            _endingGroup.SetVisible(false);
            _revealGroup.SetVisible(false);
            _replayButton.gameObject.SetActive(false);
            _restartMemoryButton.gameObject.SetActive(false);

            // 标题界面的主按钮就是"开始"，复用倒流按钮的位置与语义
            _choiceGroup.SetVisible(true);
            SetButtonVisible(_rewindButton, true);
            UiFactory.SetButtonLabel(_rewindButton, "开始这段走马灯");
            _rewindButton.interactable = true;
            SetChoiceButtons(false);
            SetNextButton(false);
        }

        /// <summary>根据当前 GameFlow 状态刷新全部视图。所有输入处理后都走这里。</summary>
        private void ApplyFlow(FlowEvent flowEvent)
        {
            if (!flowEvent.Changed)
            {
                return;
            }

            if (flowEvent.RefreshStage)
            {
                RefreshStage();
            }

            if (flowEvent.RefreshText)
            {
                RefreshTexts();
            }

            switch (_flow.Phase)
            {
                case GamePhase.MemoryIntro:
                    ConfigureMemoryButtons();
                    break;
                case GamePhase.RewindReady:
                    ConfigureMemoryButtons();
                    _rewindButton.interactable = true;
                    break;
                case GamePhase.AwaitingChoice:
                    ConfigureMemoryButtons();
                    _rewindButton.interactable = false;
                    SetButtonVisible(_rewindButton, false);
                    SetChoiceButtons(true);
                    SetNextButton(false);
                    break;
                case GamePhase.ShowingOutcome:
                    SetChoiceButtons(false);
                    SetButtonVisible(_rewindButton, false);
                    SetNextButton(true);
                    UiFactory.SetButtonLabel(_nextLevelButton,
                        _flow.Level != null && _flow.Level.Index >= MemoryBook.LevelCount - 1 ? "看最后一眼木船" : "进入下一段记忆");
                    break;
                case GamePhase.Ending:
                    EnterEnding();
                    break;
            }
        }

        private void ConfigureMemoryButtons()
        {
            _hudGroup.SetVisible(true);
            _endingGroup.SetVisible(false);
            _revealGroup.SetVisible(false);
            _replayButton.gameObject.SetActive(true);
            _restartMemoryButton.gameObject.SetActive(true);

            _choiceGroup.SetVisible(true);
            SetButtonVisible(_rewindButton, true);
            UiFactory.SetButtonLabel(_rewindButton, _flow.Level != null ? _flow.Level.RewindButtonLabel : "借他一秒");
            _rewindButton.interactable = _flow.Phase == GamePhase.RewindReady && !_flow.RewindUsed;
        }

        private void RefreshTexts()
        {
            var level = _flow.Level;
            if (level == null)
            {
                return;
            }

            switch (_flow.Phase)
            {
                case GamePhase.MemoryIntro:
                    _chapterText.text = level.ChapterLabel + "  ·  " + level.Title;
                    SetNarrative(level.IntroLine);
                    SetPrompt(level.PromptSelect);
                    ClearResult();
                    SetHeartHint(level.ChapterLabel + "  ·  " + level.Title);
                    break;
                case GamePhase.RewindReady:
                    SetPrompt(level.PromptRewind);
                    SetHeartHint(level.ChapterLabel + "  ·  已选中：" + level.ActorName);
                    break;
                case GamePhase.AwaitingChoice:
                    SetNarrative(level.AfterRewindLine);
                    SetPrompt("这一次，你想怎么做？");
                    ClearResult();
                    break;
                case GamePhase.ShowingOutcome:
                    if (_pendingChoice != null)
                    {
                        SetResult(_pendingChoice.OutcomeText);
                        SetHeartHint(level.ChapterLabel + "  ·  " + _pendingChoice.VisualNote);
                        SetPrompt("时间不会为这个选择停下来。");
                    }

                    break;
            }

            UpdateStats();
        }

        private void RefreshStage()
        {
            if (_flow.Level == null)
            {
                return;
            }

            _actors.Clear();
            _actors = _builder.RebuildStage(BuildStageContext());
            ApplySelectionVisual();
        }

        private StageContext BuildStageContext()
        {
            var level = _flow.Level;
            var context = new StageContext
            {
                Level = level,
                PropState = _flow.CurrentPropState,
                ActorSelected = _flow.ActorSelected,
                MoodOverride = _flow.CurrentPropState == PropState.AfterEvent ? level.ActorMoodAfterEvent : level.ActorMoodBeforeEvent,
                ActorVisualNote = _pendingChoice != null ? _pendingChoice.VisualNote : string.Empty,
                PropVisualNote = _heartHint,
                WorkProgress = _flow.WorkProgress(),
                ShowStationPhoto = _flow.ShowStationPhoto(),
                PhotoCaption = _flow.StationPhotoCaption()
            };

            return context;
        }

        private void ApplySelectionVisual()
        {
            foreach (var pair in _actors)
            {
                var view = pair.Value;
                if (view == null || view.Click == null || _flow.Level == null)
                {
                    continue;
                }

                bool isProtagonist = pair.Key == _flow.Level.ActorName;
                view.Click.SetSelected(isProtagonist && _flow.ActorSelected);
            }
        }

        // ------------------------------------------------------------ 输入入口

        private void OnActorClicked(string actorId)
        {
            if (_flow.Phase == GamePhase.Title)
            {
                return;
            }

            var before = _flow.Snapshot();
            var flowEvent = _flow.SelectActor(actorId);
            if (!flowEvent.Changed)
            {
                // 布景角色或阶段不允许时的提示，不改状态
                if (_flow.Level != null && actorId != null)
                {
                    SetHeartHint(_flow.Level.ChapterLabel + "  ·  " + actorId + " 只是这一幕的布景");
                }

                return;
            }

            ApplyFlow(flowEvent);
            if (before.Phase != _flow.Phase)
            {
                RefreshTexts();
            }
        }

        private void OnPropClicked(string propId)
        {
            if (_flow.Level == null || _flow.Phase == GamePhase.Title)
            {
                return;
            }

            SetHeartHint(_flow.Level.ChapterLabel + "  ·  " + propId);
        }

        private void OnRewindClicked()
        {
            if (_flow.Phase == GamePhase.Title)
            {
                ApplyFlow(_flow.StartGame());
                return;
            }

            var flowEvent = _flow.RequestRewind();
            if (!flowEvent.Changed)
            {
                return;
            }

            ApplyFlow(flowEvent);
            StartCoroutine(RewindRoutine());
        }

        private IEnumerator RewindRoutine()
        {
            ActorView actor = null;
            var level = _flow.Level;
            if (level != null && _actors.ContainsKey(level.ActorName))
            {
                actor = _actors[level.ActorName];
            }

            string label = level == null
                ? "时间倒流"
                : "时间倒流  ·  退回 " + (level.Index == 0 ? "半分钟前" : level.Index == 1 ? "电话响起的那一秒" : "你伸手之前");

            yield return RewindEffect.Play(_stageRoot, _overlayRoot, actor != null ? actor.Figure : null, label);

            ApplyFlow(_flow.CompleteRewind());
        }

        private void OnChoiceAClicked()
        {
            ApplyChoice(true);
        }

        private void OnChoiceBClicked()
        {
            ApplyChoice(false);
        }

        private void ApplyChoice(bool isChoiceA)
        {
            if (_flow.Phase != GamePhase.AwaitingChoice)
            {
                return;
            }

            var level = _flow.Level;
            if (level == null)
            {
                return;
            }

            _pendingChoice = isChoiceA ? level.ChoiceA : level.ChoiceB;
            ApplyFlow(_flow.ApplyChoice(isChoiceA));
        }

        private void OnNextLevelClicked()
        {
            if (_flow.Phase != GamePhase.ShowingOutcome)
            {
                return;
            }

            var flowEvent = _flow.Advance();
            if (!flowEvent.Changed)
            {
                return;
            }

            _pendingChoice = null;
            ApplyFlow(flowEvent);

            if (_flow.Phase == GamePhase.Reveal)
            {
                StartCoroutine(RevealRoutine());
            }
        }

        // ------------------------------------------------------------ 三段记忆重叠的反转

        private IEnumerator RevealRoutine()
        {
            _hudGroup.SetVisible(false);
            _choiceGroup.SetVisible(false);
            SetButtonVisible(_rewindButton, false);

            // 清空舞台，改成三个"自己"依次出现、逐渐重合
            SceneBuilder.ClearLayer(_builder.FxLayer);
            SceneBuilder.ClearLayer(_builder.HintLayer);
            SceneBuilder.ClearLayer(_builder.ActorLayer);
            SceneBuilder.ClearLayer(_builder.PropLayer);
            SceneBuilder.ClearLayer(_builder.BackdropLayer);
            _actors.Clear();

            var overlapLayer = new GameObject("OverlapLayer", typeof(RectTransform)).transform;
            overlapLayer.SetParent(_stageRoot, false);
            UiFactory.Stretch(overlapLayer as RectTransform);

            // 背景整体压暗，只留三个身影
            PlaceholderArt.Block(overlapLayer, "RevealDim", new Color(0.05f, 0.04f, 0.09f, 0.94f), new Vector2(4000f, 2400f), Vector2.zero);

            var figures = SceneBuilder.BuildIdentityOverlap(overlapLayer);
            _revealGroup.SetVisible(true);

            for (int i = 0; i < figures.Count; i++)
            {
                var rt = figures[i].Value;
                var fader = rt.gameObject.AddComponent<GroupFader>();
                fader.Capture(true);

                _identityText.text = MemoryBook.Get(i).IdentityClue;
                yield return FadeGroup(fader, true, 0.45f);
                yield return new WaitForSeconds(1.15f);
            }

            // 三个身影往中间靠，显示他们其实是同一个人
            float t = 0f;
            const float convergeTime = 1.4f;
            while (t < convergeTime)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / convergeTime));
                for (int i = 0; i < figures.Count; i++)
                {
                    var rt = figures[i].Value;
                    rt.anchoredPosition = new Vector2(Mathf.Lerp(SlotStartX(i), 0f, k), -20f);
                    float scale = Mathf.Lerp(1f, 0.96f, k);
                    rt.localScale = new Vector3(scale, scale, 1f);
                }

                yield return null;
            }

            _identityText.text = "三个角色，都不是别人。\n是你站在三个不同的岔路口，替自己走完的三条人生。";
            yield return new WaitForSeconds(2.0f);

            _revealGroup.SetVisible(false);
            ApplyFlow(_flow.FinishReveal());
        }

        private static float SlotStartX(int index)
        {
            return -470f + index * 470f;
        }

        // ------------------------------------------------------------ 结局

        private void EnterEnding()
        {
            var kind = _flow.ResolveEnding();

            BuildEndingVisual(kind);

            _endingTitleText.text = EndingTitle(kind) + "\n\n" + EndingBody(kind) + "\n\n" + _flow.Ledger.Summary();

            _hudGroup.SetVisible(true);
            _choiceGroup.SetVisible(true);
            SetButtonVisible(_rewindButton, false);

            _endingGroup.SetVisible(true);
            _replayButton.gameObject.SetActive(true);
            _restartMemoryButton.gameObject.SetActive(false);
            _replayButton.interactable = true;
            SetChoiceButtons(false);
            SetNextButton(false);

            _chapterText.text = "结局";
            SetNarrative("这一生的走马灯到这里就结束了。");
            SetPrompt("点“从第一段记忆重新开始”，可以换一条路再走一遍。");
            SetResult(string.Empty);
            SetHeartHint(_flow.Ledger.Summary());
        }

        private static string EndingTitle(EndingKind kind)
        {
            switch (kind)
            {
                case EndingKind.BrandNewBoat: return "结局一 · 全新的船";
                case EndingKind.MendedBoat: return "结局三 · 修补之船";
                default: return "结局二 · 旧船仍在";
            }
        }

        private static string EndingBody(EndingKind kind)
        {
            switch (kind)
            {
                case EndingKind.BrandNewBoat:
                    return "船身平整，没有一道刻痕。\n它比以前更好看，只是不再记得谁坐过它。";
                case EndingKind.MendedBoat:
                    return "你留下了那块写着字的旧木板，也允许别的地方被换掉。\n船有点歪，但所有你遇到过的人，都还在这条船上。";
                default:
                    return "你留下了那块旧木板。\n刻痕还在，船也还在——只是有些来不及说完的话，永远留在了那几秒里。";
            }
        }

        private void BuildEndingVisual(EndingKind kind)
        {
            // 结局不需要新场景：只切换物件状态与几行字。
            SceneBuilder.ClearLayer(_builder.FxLayer);
            SceneBuilder.ClearLayer(_builder.HintLayer);
            SceneBuilder.ClearLayer(_builder.ActorLayer);
            SceneBuilder.ClearLayer(_builder.PropLayer);
            SceneBuilder.ClearLayer(_builder.BackdropLayer);

            Color wood = SceneBuilder.ToColor(MemoryBook.Get(2).PropTone);
            SceneBuilder.BuildEndingBoat(_builder.FxLayer, kind, wood);
        }

        // ------------------------------------------------------------ 重新开始

        private void OnRestartMemoryClicked()
        {
            if (_flow.Level == null)
            {
                return;
            }

            _pendingChoice = null;
            SceneBuilder.ClearLayer(_builder.FxLayer);
            SceneBuilder.ClearLayer(_builder.HintLayer);
            ApplyFlow(_flow.RestartCurrentMemory());
        }

        private void OnRestartClicked()
        {
            _pendingChoice = null;
            SceneBuilder.ClearLayer(_builder.FxLayer);
            SceneBuilder.ClearLayer(_builder.HintLayer);
            _actors.Clear();
            _endingGroup.SetVisible(false);
            _revealGroup.SetVisible(false);

            var flowEvent = _flow.RestartFromBeginning();
            ApplyFlow(flowEvent);
            EnterTitle();
        }

        // ------------------------------------------------------------ 文本工具

        private void UpdateStats()
        {
            if (_statsText != null)
            {
                _statsText.text = _flow.Ledger.Summary();
            }
        }

        private void SetNarrative(string value)
        {
            if (_narrativeText != null)
            {
                _narrativeText.text = value;
            }
        }

        private void SetPrompt(string value)
        {
            if (_promptText != null)
            {
                _promptText.text = value;
            }
        }

        private void SetResult(string value)
        {
            if (_resultText != null)
            {
                _resultText.text = value;
            }
        }

        private void ClearResult()
        {
            SetResult(string.Empty);
        }

        private void SetHeartHint(string value)
        {
            _heartHint = value;
            if (_heartText != null)
            {
                _heartText.text = value;
            }
        }

        private void SetChoiceButtons(bool visible)
        {
            if (_choiceAButton == null || _choiceBButton == null)
            {
                return;
            }

            var level = _flow.Level;
            if (visible && level != null)
            {
                UiFactory.SetButtonLabel(_choiceAButton, level.ChoiceA.ButtonLabel);
                UiFactory.SetButtonLabel(_choiceBButton, level.ChoiceB.ButtonLabel);
            }

            SetButtonVisible(_choiceAButton, visible);
            SetButtonVisible(_choiceBButton, visible);
            _choiceAButton.interactable = visible;
            _choiceBButton.interactable = visible;
        }

        private void SetNextButton(bool visible)
        {
            if (_nextLevelButton == null)
            {
                return;
            }

            SetButtonVisible(_nextLevelButton, visible);
            _nextLevelButton.interactable = visible;
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.gameObject.SetActive(visible);
            }
        }

        private static IEnumerator FadeGroup(GroupFader fader, bool visible, float duration)
        {
            if (fader == null)
            {
                yield break;
            }

            fader.SetVisible(visible);
            yield return fader.FadeTo(visible ? 1f : 0f, duration);
        }
    }
}
