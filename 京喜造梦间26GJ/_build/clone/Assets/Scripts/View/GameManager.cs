using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CuoGuo
{
    /// <summary>
    /// 唯一总控。整个游戏只有这一个挂在场景里的脚本，
    /// 画布、章节舞台、人物、道具、按钮、文字全部由代码在运行时生成，
    /// 所以场景文件里只有一个空物体，换机器或重新导入资源都不会丢引用。
    ///
    /// 它只做三件事：把输入交给 Core（GameFlow）、把 Core 的状态画出来、播放演出。
    /// 所有游戏规则都在 Assets/Scripts/Core 里，那部分可以脱离 Unity 跑断言。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _stageRoot;
        private RectTransform _uiRoot;

        private readonly ChapterStage[] _stages = new ChapterStage[StoryBook.ChapterCount];
        private readonly GameFlow _flow = new GameFlow();

        private MemoryDecayController _decay;
        private SoundKit _sound;
        private EndingSequence _ending;

        // ---- UI ----
        private RectTransform _titleGroup;
        private RectTransform _playGroup;
        private RectTransform _endingGroup;

        private Text _chapterLabel;
        private Text _narration;
        private Text _cueText;
        private Text _debtText;
        private Text _endingText;
        private Text _endingCarving;

        private Button _startButton;
        private Button _holdButton;
        private Button _continueButton;
        private Button _restartButton;

        private ChapterStage CurrentStage
        {
            get
            {
                int i = _flow.ChapterIndex;
                return i < 0 || i >= _stages.Length ? null : _stages[i];
            }
        }

        // ------------------------------------------------------------ 启动

        private void Awake()
        {
            UiFactory.EnsureEventSystem();

            _canvas = UiFactory.MakeCanvas(transform, "Canvas");
            _stageRoot = UiFactory.MakeStretchedGroup(_canvas.transform, "Stage");
            _uiRoot = UiFactory.MakeStretchedGroup(_canvas.transform, "UI");

            _sound = gameObject.AddComponent<SoundKit>();
            _sound.Init();

            _decay = gameObject.AddComponent<MemoryDecayController>();
            _decay.StageChanged += OnDecayStageChanged;

            BuildStages();
            BuildTitleScreen();
            BuildPlayUi();
            BuildEndingUi();

            _ending = new EndingSequence(this);
        }

        private void Start()
        {
            ShowTitle();
            _sound.PlayMusic();
        }

        /// <summary>三章舞台在启动时一次性搭好，之后只做启用/隐藏（策划案：单场景组织）。</summary>
        private void BuildStages()
        {
            for (int i = 0; i < _stages.Length; i++)
            {
                _stages[i] = new ChapterStage();
                _stages[i].HoverChanged += OnSliderHover;
                _stages[i].Build(_stageRoot, StoryBook.Get(i), _canvas);

                int index = i;
                _stages[i].Slider.PositionChanged += delegate (float p) { OnTimelineDragged(index, p); };
            }
        }

        // ------------------------------------------------------------ 标题

        private void BuildTitleScreen()
        {
            _titleGroup = UiFactory.MakeStretchedGroup(_uiRoot, "TitleGroup");

            // 开场画面：一艘旧木船，船底、船缝和夹层都藏在阴影里，只隐约露出三件东西的轮廓。
            var scene = UiFactory.MakeStretchedGroup(_stageRoot, "TitleScene");
            ChapterData first = StoryBook.Get(0);
            RectTransform backdrop = DockArt.BuildBackdrop(scene, "dusk",
                new Color(0.62f, 0.52f, 0.46f), new Color(0.30f, 0.33f, 0.40f), new Color(0.26f, 0.21f, 0.18f));
            backdrop.name = "TitleBackdrop";

            RectTransform boat = DockArt.BuildBoat(scene, new Vector2(760f, 330f), new Color(0.46f, 0.34f, 0.24f));
            boat.name = "TitleBoat";
            boat.anchoredPosition = new Vector2(40f, -110f);

            RectTransform compass = DockArt.BuildCompass(scene, new Vector2(66f, 66f));
            compass.anchoredPosition = new Vector2(-40f, -232f);

            RectTransform chart = DockArt.BuildChart(scene, new Vector2(104f, 80f));
            chart.anchoredPosition = new Vector2(176f, -200f);

            RectTransform paper = DockArt.BuildPaperBoat(scene, new Vector2(96f, 74f), false);
            paper.anchoredPosition = new Vector2(300f, -236f);

            // 阴影层：把三件道具压暗，只留下"好像有什么"的程度。
            UiFactory.MakePanel(scene, "TitleShadow", new Color(0.06f, 0.07f, 0.10f, 0.55f));

            UiFactory.MakeText(_titleGroup, "Title", StoryBook.GameTitle, 86, UiFactory.Paper, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.62f), new Vector2(1f, 0.86f), Vector2.zero, Vector2.zero);

            UiFactory.MakeText(_titleGroup, "Subtitle", StoryBook.TitleLine, 30, UiFactory.Muted, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.52f), new Vector2(1f, 0.60f), Vector2.zero, Vector2.zero);

            _startButton = UiFactory.MakeButton(_titleGroup, "StartButton", StoryBook.StartButtonLabel,
                new Vector2(220f, 68f), new Vector2(0.5f, 0.34f), Vector2.zero, UiFactory.Amber, OnStartClicked);

            UiFactory.MakeText(_titleGroup, "StartHint", StoryBook.TitleHint, 22, UiFactory.Muted, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.22f), new Vector2(1f, 0.30f), Vector2.zero, Vector2.zero);
        }

        // ------------------------------------------------------------ 游玩界面

        private void BuildPlayUi()
        {
            _playGroup = UiFactory.MakeStretchedGroup(_uiRoot, "PlayGroup");

            _chapterLabel = UiFactory.MakeText(_playGroup, "ChapterLabel", string.Empty, 26, UiFactory.WithAlpha(UiFactory.Amber, 0.85f),
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -96f), new Vector2(700f, -48f));

            // 线索短句：在画面下三分之一处，随线索出现而淡入，拖回现在自然淡出。
            _cueText = UiFactory.MakeText(_playGroup, "CueText", string.Empty, 34, UiFactory.Paper,
                TextAnchor.LowerCenter, new Vector2(0.12f, 0f), new Vector2(0.88f, 0f), new Vector2(0f, 150f), new Vector2(0f, 250f));
            _cueText.color = UiFactory.WithAlpha(UiFactory.Paper, 0f);

            // 旁白：开场与过渡用。
            _narration = UiFactory.MakeText(_playGroup, "Narration", string.Empty, 36, UiFactory.LightInk,
                TextAnchor.MiddleCenter, new Vector2(0.10f, 0.30f), new Vector2(0.90f, 0.62f), Vector2.zero, Vector2.zero);
            _narration.color = UiFactory.WithAlpha(UiFactory.LightInk, 0f);

            // 借时债务：一小行低对比度的字，让机制对玩家（和评委）可见，但不成其为 HUD 时间轴。
            _debtText = UiFactory.MakeText(_playGroup, "DebtText", string.Empty, 22, UiFactory.WithAlpha(UiFactory.RewindViolet, 0.75f),
                TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-360f, -96f), new Vector2(-56f, -48f));

            _holdButton = UiFactory.MakeButton(_playGroup, "HoldButton", StoryBook.HoldButtonLabel,
                new Vector2(230f, 62f), new Vector2(0.5f, 0f), new Vector2(-140f, 70f), UiFactory.Muted, OnHoldClicked);

            _continueButton = UiFactory.MakeButton(_playGroup, "ContinueButton", StoryBook.ContinueButtonLabel,
                new Vector2(190f, 62f), new Vector2(0.5f, 0f), new Vector2(150f, 70f), UiFactory.Amber, OnContinueClicked);
        }

        private void BuildEndingUi()
        {
            _endingGroup = UiFactory.MakeStretchedGroup(_uiRoot, "EndingGroup");

            _endingText = UiFactory.MakeText(_endingGroup, "EndingText", string.Empty, 42, UiFactory.Paper,
                TextAnchor.MiddleCenter, new Vector2(0.10f, 0.34f), new Vector2(0.90f, 0.62f), Vector2.zero, Vector2.zero);
            _endingText.color = UiFactory.WithAlpha(UiFactory.Paper, 0f);

            _endingCarving = UiFactory.MakeText(_endingGroup, "EndingCarving", StoryBook.EndingCarving, 40, UiFactory.Amber,
                TextAnchor.MiddleCenter, new Vector2(0.10f, 0.70f), new Vector2(0.90f, 0.80f), Vector2.zero, Vector2.zero);
            _endingCarving.color = UiFactory.WithAlpha(UiFactory.Amber, 0f);

            _restartButton = UiFactory.MakeButton(_endingGroup, "RestartButton", "再走一次",
                new Vector2(240f, 64f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), UiFactory.Muted, OnRestartClicked);
            _restartButton.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ 标题 → 开始

        private void ShowTitle()
        {
            _titleGroup.gameObject.SetActive(true);
            _playGroup.gameObject.SetActive(false);
            _endingGroup.gameObject.SetActive(false);
            FindTitleScene().SetActive(true);

            for (int i = 0; i < _stages.Length; i++)
            {
                _stages[i].Hide();
            }
        }

        private GameObject FindTitleScene()
        {
            Transform t = UiFactory.FindDeepChild(_stageRoot, "TitleScene");
            return t == null ? null : t.gameObject;
        }

        private void OnStartClicked()
        {
            _startButton.interactable = false;
            StartCoroutine(GameRoutine());
        }

        /// <summary>整局流程：三段记忆依次走一遍，然后进入唯一结局。</summary>
        private IEnumerator GameRoutine()
        {
            _titleGroup.gameObject.SetActive(false);
            GameObject titleScene = FindTitleScene();
            if (titleScene != null)
            {
                titleScene.SetActive(false);
            }

            _playGroup.gameObject.SetActive(true);

            for (int i = 0; i < StoryBook.ChapterCount; i++)
            {
                yield return ChapterRoutine(i);
            }

            _flow.EnterEnding();
            Debug.Log("[错过了那一秒] 三段记忆走完，进入唯一结局");
            _playGroup.gameObject.SetActive(false);
            _endingGroup.gameObject.SetActive(true);
            yield return _ending.Play();

            _flow.FinishEnding();
            _restartButton.gameObject.SetActive(true);
        }

        private IEnumerator ChapterRoutine(int index)
        {
            ChapterStage stage = _stages[index];
            ChapterData data = StoryBook.Get(index);

            Debug.Log("[错过了那一秒] 进入第 " + (index + 1) + " 段记忆：" + data.Title);

            HideAllStages();
            stage.Show();
            stage.SetSliderInteractable(false);
            stage.PoseAt(1f);
            stage.Slider.ResetIdle();

            _flow.EnterChapter(index);
            RefreshDecay(stage);
            _chapterLabel.text = data.ChapterLabel + "  ·  " + data.Title;
            _cueText.text = string.Empty;
            _debtText.text = string.Empty;
            UpdateButtons();

            // 开场旁白：逐条淡入淡出。第一章额外给一条操作提示。
            for (int i = 0; i < data.IntroLines.Length; i++)
            {
                yield return FadeText(_narration, data.IntroLines[i], 1.05f, 0.45f);
            }

            if (index == 0)
            {
                yield return FadeText(_narration, StoryBook.TitleHint, 1.35f, 0.5f);
            }
            else
            {
                yield return FadeText(_narration, string.Empty, 0.35f, 0.35f);
            }

            // 交给玩家：滑块解锁，可以任意拖动，停在任意位置。
            stage.SetSliderInteractable(true);
            _continueClicked = false;

            while (!_continueClicked)
            {
                UpdateButtons();
                yield return null;
            }

            stage.SetSliderInteractable(false);

            // 章节过渡
            if (data.TransitionLines != null)
            {
                for (int i = 0; i < data.TransitionLines.Length; i++)
                {
                    yield return FadeText(_narration, data.TransitionLines[i], 1.0f, 0.45f);
                }
            }

            yield return FadeText(_narration, string.Empty, 0.35f, 0.35f);
        }

        private void HideAllStages()
        {
            for (int i = 0; i < _stages.Length; i++)
            {
                _stages[i].Hide();
            }
        }

        // ------------------------------------------------------------ 拖动时间

        private float _lastRewindSoundAt;
        private float _lastDragPosition = 1f;

        private void OnTimelineDragged(int chapterIndex, float position)
        {
            if (_flow.Phase != GamePhase.Chapter || _flow.ChapterIndex != chapterIndex)
            {
                return;
            }

            TimelineStep step = _flow.SetTimelinePosition(position);
            if (!step.Changed)
            {
                return;
            }

            ChapterStage stage = _stages[chapterIndex];
            stage.Binder.Apply(position);

            // 记忆褪色：借得越深，眼前的现在越残破
            _decay.Report(_flow.Debt);
            stage.ApplyDecay(_decay.Amount);

            // 声音：拖动速度决定倒带声的力度
            float speed = Mathf.Abs(position - _lastDragPosition) / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _lastDragPosition = position;
            if (step.BorrowedDeeper && Time.unscaledTime - _lastRewindSoundAt > 0.16f)
            {
                _lastRewindSoundAt = Time.unscaledTime;
                _sound.PlayRewind(Mathf.Clamp01(speed * 0.06f));
            }
            else if (!step.BorrowedDeeper)
            {
                _sound.PlayTick();
            }

            if (step.NewCue != null)
            {
                _sound.PlayClue();
            }

            UpdateCueText();
            UpdateDebtLabel();
            UpdateButtons();
        }

        private void UpdateCueText()
        {
            string text = _flow.ActiveCueText;
            _cueText.text = text == null ? string.Empty : text;
            _cueTargetAlpha = text == null ? 0f : 1f;
        }

        private float _cueTargetAlpha;

        private void UpdateDebtLabel()
        {
            int debt = _flow.Debt;
            _debtText.text = debt <= 0 ? string.Empty : "借时 · " + debt;
        }

        private void RefreshDecay(ChapterStage stage)
        {
            _decay.Report(_flow.Debt);
            stage.ApplyDecay(_decay.Amount);
            UpdateDebtLabel();
        }

        private void OnDecayStageChanged(int stage)
        {
            if (stage > 0 && _flow.Phase == GamePhase.Chapter)
            {
                _sound.PlayPaper();
            }
        }

        // ------------------------------------------------------------ 按钮

        private bool _continueClicked;

        private void OnHoldClicked()
        {
            if (_flow.RequestHold())
            {
                _holdButton.gameObject.SetActive(false);
                StartCoroutine(FadeText(_narration, StoryBook.Get(_flow.ChapterIndex).HoldText, 1.1f, 0.4f));
                UpdateButtons();
            }
        }

        private void OnContinueClicked()
        {
            if (_flow.Phase != GamePhase.Chapter)
            {
                return;
            }

            // 先把这一章的滑块锁上，再推进流程：避免"点完继续的那一帧还能拖时间"。
            ChapterStage stage = CurrentStage;
            if (_flow.RequestContinue())
            {
                _continueClicked = true;
                if (stage != null)
                {
                    stage.SetSliderInteractable(false);
                }
            }
        }

        private void OnRestartClicked()
        {
            _restartButton.gameObject.SetActive(false);
            StopAllCoroutines();
            _flow.RestartAll();
            _decay.ResetTo(0);
            _ending.Reset();
            _endingText.text = string.Empty;
            _endingCarving.color = UiFactory.WithAlpha(UiFactory.Amber, 0f);
            _startButton.interactable = true;
            ShowTitle();
        }

        private void UpdateButtons()
        {
            bool exploring = _flow.Phase == GamePhase.Chapter;
            ChapterStage stage = CurrentStage;

            // "把现在留住"在第一条线索出现后出现；如果玩家迟迟不动，25 秒后也给它（避免卡在"不知道能干什么"）。
            bool holdVisible = exploring && !_flow.HoldRequested
                && (_flow.Cues != null && _flow.Cues.FiredCount > 0
                    || (stage != null && stage.Slider != null && stage.Slider.IdleSeconds > 25f));

            if (_holdButton.gameObject.activeSelf != holdVisible)
            {
                _holdButton.gameObject.SetActive(holdVisible);
            }

            bool canContinue = _flow.CanContinue;
            if (_continueButton.interactable != canContinue)
            {
                _continueButton.interactable = canContinue;
            }
        }

        private void OnSliderHover(bool hover)
        {
            if (hover)
            {
                _sound.PlayTick();
            }
        }

        // ------------------------------------------------------------ 文字演出

        /// <summary>把一行字淡入、停留、淡出。空字符串只做淡出。</summary>
        private IEnumerator FadeText(Text target, string content, float holdSeconds, float fadeSeconds)
        {
            // 自检模式会把演出整体加速，只改等待时长，不改任何规则。
            float speed = SpeedFactor <= 0.01f ? 1f : SpeedFactor;
            holdSeconds /= speed;
            fadeSeconds /= speed;

            float start = target.color.a;

            if (start < 0.01f && !string.IsNullOrEmpty(content))
            {
                target.text = content;
            }

            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                SetTextAlpha(target, Mathf.Lerp(start, string.IsNullOrEmpty(content) ? 0f : 1f, t / fadeSeconds));
                yield return null;
            }

            if (string.IsNullOrEmpty(content))
            {
                SetTextAlpha(target, 0f);
                yield break;
            }

            SetTextAlpha(target, 1f);
            yield return new WaitForSecondsRealtime(holdSeconds);

            t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                SetTextAlpha(target, Mathf.Lerp(1f, 0f, t / fadeSeconds));
                yield return null;
            }

            SetTextAlpha(target, 0f);
        }

        private static void SetTextAlpha(Text text, float alpha)
        {
            var c = text.color;
            text.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
        }

        private void Update()
        {
            // 线索文字自己平滑淡入淡出，和"停在某个时间位置"无关，纯表现。
            var c = _cueText.color;
            float a = Mathf.MoveTowards(c.a, _cueTargetAlpha, Time.unscaledDeltaTime * 2.4f);
            if (!Mathf.Approximately(a, c.a))
            {
                _cueText.color = new Color(c.r, c.g, c.b, a);
            }
        }

        // ------------------------------------------------------------ 给 EndingSequence 用的入口

        internal ChapterStage[] Stages
        {
            get { return _stages; }
        }

        internal RectTransform StageRoot
        {
            get { return _stageRoot; }
        }

        internal Canvas Canvas
        {
            get { return _canvas; }
        }

        internal SoundKit Sound
        {
            get { return _sound; }
        }

        internal MemoryDecayController Decay
        {
            get { return _decay; }
        }

        internal Text EndingText
        {
            get { return _endingText; }
        }

        internal Text EndingCarving
        {
            get { return _endingCarving; }
        }

        internal IEnumerator FadeTextRoutine(Text target, string content, float hold, float fade)
        {
            return FadeText(target, content, hold, fade);
        }

        // ------------------------------------------------------------ 无头自检入口
        //
        // 下面这些只在打包出来的播放器里带 -cuoguo-selfcheck 参数时才会被调用，
        // 用来在没有人工点按的情况下，把真实游戏代码跑一遍并断言关键状态。
        // 它们走的都是和玩家操作完全相同的代码路径（TestDrag 就是滑块回调本身）。

        /// <summary>自检模式下的演出倍速。默认 1，只影响等待时长。</summary>
        internal static float SpeedFactor = 1f;

        internal GameFlow Flow
        {
            get { return _flow; }
        }

        internal Button ContinueButton
        {
            get { return _continueButton; }
        }

        internal Button HoldButton
        {
            get { return _holdButton; }
        }

        internal Button RestartButton
        {
            get { return _restartButton; }
        }

        internal Text CueText
        {
            get { return _cueText; }
        }

        internal Text DebtText
        {
            get { return _debtText; }
        }

        /// <summary>等同于玩家点了标题上的"开始"。</summary>
        internal void TestStartGame()
        {
            if (_flow.Phase == GamePhase.Title)
            {
                OnStartClicked();
            }
        }

        /// <summary>等同于玩家把当前章的滑块拖到 position（走的是和真实拖动同一个回调）。</summary>
        internal void TestDrag(int chapterIndex, float position)
        {
            if (chapterIndex < 0 || chapterIndex >= _stages.Length)
            {
                return;
            }

            _stages[chapterIndex].Slider.SetPositionSilently(position);
            OnTimelineDragged(chapterIndex, position);
        }

        /// <summary>等同于玩家点了"把现在留住"。</summary>
        internal void TestHold()
        {
            OnHoldClicked();
        }

        /// <summary>等同于玩家点了"继续"。</summary>
        internal bool TestContinue()
        {
            int before = _flow.ChapterIndex;
            OnContinueClicked();
            return _flow.Phase != GamePhase.Chapter || _flow.ChapterIndex != before;
        }
    }
}
