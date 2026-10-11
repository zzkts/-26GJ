namespace CuoGuo
{
    /// <summary>游戏所处的大阶段。</summary>
    public enum GamePhase
    {
        /// <summary>标题界面。</summary>
        Title,

        /// <summary>某一段记忆中：玩家可以拖动世界里的滑块，借时间回到过去。</summary>
        Chapter,

        /// <summary>唯一结局演出：滑块被锁住，玩家不能操作。</summary>
        Ending,

        /// <summary>结局播完，可以重新开始。</summary>
        Finished
    }

    /// <summary>玩家拖动滑块之后的结果，视图层据此决定弹什么字、按钮能不能点。</summary>
    public struct TimelineStep
    {
        /// <summary>时间位置是否真的变化了。</summary>
        public bool Changed;

        /// <summary>本次新触发的线索（要弹一条短句）；没有新线索时为 null。</summary>
        public TimelineCue NewCue;

        /// <summary>线索是否发生了变化（含"从深处拖回来，文字应该消失"的情况）。</summary>
        public bool CueTextChanged;

        /// <summary>这次拖动是否刷新了本章最深记录（即是否新借了一笔时间）。</summary>
        public bool BorrowedDeeper;
    }

    /// <summary>
    /// 流程内核。三章的推进、借时债务、线索触发、唯一结局、重新开始全在这里。
    ///
    /// 它不引用任何 UnityEngine 类型，因此可以被独立编译并跑断言
    /// （见 _build/verify）——"债务不退款""重复区域不重复收费""任何路径都能进唯一结局"
    /// 这些规则是真的在命令行里跑过的，不是靠肉眼点出来的。
    /// </summary>
    public class GameFlow
    {
        private readonly DebtModel _debt = new DebtModel();

        private GamePhase _phase = GamePhase.Title;
        private int _chapterIndex = -1;
        private CueBoard _cues;
        private float _position = 1f;
        private bool _holdRequested;
        private bool _inputLocked;

        // ------------------------------------------------------------ 只读访问

        public GamePhase Phase
        {
            get { return _phase; }
        }

        public int ChapterIndex
        {
            get { return _chapterIndex; }
        }

        /// <summary>当前章的静态数据；标题界面时为 null。</summary>
        public ChapterData Chapter
        {
            get { return _chapterIndex < 0 ? null : StoryBook.Get(_chapterIndex); }
        }

        public ChapterTimeline Timeline
        {
            get { return Chapter == null ? null : Chapter.Timeline; }
        }

        /// <summary>连续时间位置：1 = 现在，0 = 这段记忆最早的过去。</summary>
        public float TimelinePosition
        {
            get { return _position; }
        }

        /// <summary>全局借时债务 0—100，跨章继承。</summary>
        public int Debt
        {
            get { return _debt.GlobalDebt; }
        }

        public DebtModel DebtModel
        {
            get { return _debt; }
        }

        public CueBoard Cues
        {
            get { return _cues; }
        }

        /// <summary>玩家是否已经点了"把现在留住"。</summary>
        public bool HoldRequested
        {
            get { return _holdRequested; }
        }

        /// <summary>演出进行中：滑块与按钮一律不响应。</summary>
        public bool InputLocked
        {
            get { return _inputLocked; }
        }

        /// <summary>滑块此刻能不能拖（结局演出期间锁死）。</summary>
        public bool CanDragSlider
        {
            get { return _phase == GamePhase.Chapter && !_inputLocked; }
        }

        /// <summary>玩家能否结束本章、进入下一段记忆。</summary>
        public bool CanContinue
        {
            get
            {
                if (_phase != GamePhase.Chapter || _inputLocked)
                {
                    return false;
                }

                return _holdRequested || FinalCueFound;
            }
        }

        /// <summary>本章最深处那条真相是否已被看见。</summary>
        public bool FinalCueFound
        {
            get
            {
                if (_cues == null || Timeline == null)
                {
                    return false;
                }

                int index = _cues.IndexOf(Timeline.FinalCueId);
                return index >= 0 && _cues.IsFired(index);
            }
        }

        /// <summary>当前应当显示在屏幕上的线索文字；没有则为 null。</summary>
        public string ActiveCueText
        {
            get
            {
                TimelineCue cue = _cues == null ? null : _cues.ActiveCue(_position);
                return cue == null ? null : cue.Text;
            }
        }

        /// <summary>是否已经走到最后一段记忆。</summary>
        public bool IsLastChapter
        {
            get { return _chapterIndex >= StoryBook.ChapterCount - 1; }
        }

        // ------------------------------------------------------------ 标题与章节

        /// <summary>标题界面的开始按钮。</summary>
        public void StartGame()
        {
            if (_phase != GamePhase.Title)
            {
                return;
            }

            EnterChapter(0);
        }

        /// <summary>
        /// 进入某一段记忆。新章的滑块从右端（现在）出发，
        /// 上一章的债务已经结算进 CommittedDebt，债务不清零。
        /// </summary>
        public void EnterChapter(int index)
        {
            if (index < 0 || index >= StoryBook.ChapterCount)
            {
                return;
            }

            ChapterData data = StoryBook.Get(index);
            _chapterIndex = index;
            _debt.EnterChapter(index);
            _cues = new CueBoard(data.Timeline.Cues);
            _position = 1f;
            _holdRequested = false;
            _inputLocked = false;
            _phase = GamePhase.Chapter;
        }

        // ------------------------------------------------------------ 拖动时间

        /// <summary>
        /// 玩家拖动世界里的滑块。position 是 0—1 的连续值，不吸附到任何档位。
        /// 债务只按"本章最深记录"变化，所以来回拖动不会反复收费，向右拖回也不退款。
        /// </summary>
        public TimelineStep SetTimelinePosition(float position)
        {
            var step = new TimelineStep();
            if (_phase != GamePhase.Chapter)
            {
                return step;
            }

            float clamped = Clamp01(position);
            if (clamped > _position - 0.0000001f && clamped < _position + 0.0000001f)
            {
                return step;
            }

            float previousDepth = _debt.MaxPastDepth(_chapterIndex);
            _position = clamped;

            // 债务：只认"更深的新纪录"
            _debt.Report(_position);
            step.BorrowedDeeper = _debt.MaxPastDepth(_chapterIndex) > previousDepth + 0.0000001f;

            // 线索：只在第一次越过阈值时触发，触发后记住状态
            string before = ActiveCueText;
            step.NewCue = _cues.Update(_position);
            string after = ActiveCueText;
            step.CueTextChanged = before != after;
            step.Changed = true;

            if (step.NewCue != null)
            {
                step.CueTextChanged = true;
            }

            return step;
        }

        /// <summary>
        /// "把现在留住"：结束本章的深入探索并启用"继续"。
        /// 它不锁死滑块——玩家想再往深处看几眼仍然可以，只是不再重复收费。
        /// </summary>
        public bool RequestHold()
        {
            if (_phase != GamePhase.Chapter || _inputLocked || _holdRequested)
            {
                return false;
            }

            _holdRequested = true;
            return true;
        }

        // ------------------------------------------------------------ 推进与结局

        /// <summary>
        /// 点"继续"。最后一段记忆走完就进入唯一结局。
        /// 只要本章的最终线索被看见过、或者玩家按过"把现在留住"，就一定能继续——
        /// 任何正常游玩路径都不会卡在无法操作的状态。
        /// </summary>
        public bool RequestContinue()
        {
            if (!CanContinue)
            {
                return false;
            }

            if (IsLastChapter)
            {
                EnterEnding();
                return true;
            }

            EnterChapter(_chapterIndex + 1);
            return true;
        }

        /// <summary>进入唯一结局：锁住滑块，等下由 EndingSequence 播完演出。</summary>
        public void EnterEnding()
        {
            _phase = GamePhase.Ending;
            _inputLocked = true;
            _holdRequested = false;
        }

        /// <summary>结局演出播完：解锁按钮以便重新开始。</summary>
        public void FinishEnding()
        {
            if (_phase != GamePhase.Ending)
            {
                return;
            }

            _phase = GamePhase.Finished;
            _inputLocked = false;
        }

        // ------------------------------------------------------------ 重新开始

        /// <summary>
        /// 从标题重新开始：债务、章节记录、线索触发标记、结局状态全部清零。
        /// </summary>
        public void RestartAll()
        {
            _debt.Reset();
            if (_cues != null)
            {
                _cues.Reset();
            }

            _cues = null;
            _chapterIndex = -1;
            _position = 1f;
            _holdRequested = false;
            _inputLocked = false;
            _phase = GamePhase.Title;
        }

        // ------------------------------------------------------------ 工具

        private static float Clamp01(float v)
        {
            if (v < 0f)
            {
                return 0f;
            }

            return v > 1f ? 1f : v;
        }
    }
}
