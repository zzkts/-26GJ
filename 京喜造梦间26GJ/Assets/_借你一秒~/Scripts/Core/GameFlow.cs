namespace JieNiYiMiao
{
    /// <summary>
    /// 一次"状态变更"的结果。视图层据此决定要不要重建舞台，
    /// 而不是自己再去判断一遍规则（规则只写在 GameFlow 里，避免两边不一致）。
    /// </summary>
    public struct FlowEvent
    {
        /// <summary>状态是否真的变化了。被过滤掉的重复点击会得到 false。</summary>
        public bool Changed;

        /// <summary>变化的原因，便于在 Console 里排查流程问题。</summary>
        public string Reason;

        /// <summary>是否需要重建舞台（人物/物件状态发生了变化）。</summary>
        public bool RefreshStage;

        /// <summary>是否需要刷新底部文案与按钮。</summary>
        public bool RefreshText;

        public static FlowEvent None()
        {
            return new FlowEvent { Changed = false, Reason = "ignored", RefreshStage = false, RefreshText = false };
        }

        public static FlowEvent Make(string reason, bool refreshStage, bool refreshText)
        {
            return new FlowEvent { Changed = true, Reason = reason, RefreshStage = refreshStage, RefreshText = refreshText };
        }
    }

    /// <summary>GameFlow 在每一步之后对外暴露的完整状态快照，用于视图刷新与自动化校验。</summary>
    public struct FlowSnapshot
    {
        public GamePhase Phase;
        public int LevelIndex;
        public PropState PropState;
        public bool ActorSelected;
        public bool RewindUsed;
        public int Ambition;
        public int Connection;
    }

    /// <summary>
    /// 《借你一秒》的流程内核。
    ///
    /// 它不引用任何 UnityEngine 类型：关卡推进、选择变量、倒流的唯一性、
    /// 前关影响后关、结局判定全部在这里，因此可以被独立地编译和运行校验
    /// （见 _build/verify）。视图层 GameManager 只负责把这里的状态画出来。
    ///
    /// 手册要求的技术边界在这里落地：
    /// - 不做真正的物理时间倒带，只用 PropState 的四态切换；
    /// - 每关最多一次正式回溯（RewindUsed），重复请求一律过滤；
    /// - 只有两个整数变量 Ambition / Connection，不是善恶分。
    /// </summary>
    public class GameFlow
    {
        private readonly ChoiceLedger _ledger = new ChoiceLedger();

        private GamePhase _phase = GamePhase.Title;
        private MemoryLevelData _level;
        private PropState _propState = PropState.AfterEvent;
        private bool _actorSelected;
        private bool _rewindUsed;
        private bool _flowBlocked;

        // ------------------------------------------------------------ 只读访问

        public GamePhase Phase
        {
            get { return _phase; }
        }

        public MemoryLevelData Level
        {
            get { return _level; }
        }

        public PropState CurrentPropState
        {
            get { return _propState; }
        }

        public bool ActorSelected
        {
            get { return _actorSelected; }
        }

        public bool RewindUsed
        {
            get { return _rewindUsed; }
        }

        public bool FlowBlocked
        {
            get { return _flowBlocked; }
        }

        public ChoiceLedger Ledger
        {
            get { return _ledger; }
        }

        /// <summary>过渡动画进行中（例如倒流演出）时，输入一律不响应。</summary>
        public bool InputLocked
        {
            get { return _flowBlocked; }
        }

        public FlowSnapshot Snapshot()
        {
            return new FlowSnapshot
            {
                Phase = _phase,
                LevelIndex = _level != null ? _level.Index : -1,
                PropState = _propState,
                ActorSelected = _actorSelected,
                RewindUsed = _rewindUsed,
                Ambition = _ledger.Ambition,
                Connection = _ledger.Connection
            };
        }

        // ------------------------------------------------------------ 流程入口

        /// <summary>标题界面的主按钮。</summary>
        public FlowEvent StartGame()
        {
            if (_phase != GamePhase.Title)
            {
                return FlowEvent.None();
            }

            return EnterMemoryInternal(0, "start");
        }

        /// <summary>选中舞台上的人物。只有当前记忆的主角才参与倒流。</summary>
        public FlowEvent SelectActor(string actorId)
        {
            if (_phase == GamePhase.Title || _level == null)
            {
                return FlowEvent.None();
            }

            if (actorId != _level.ActorName)
            {
                // 布景角色：只是提示，不算状态变化
                return FlowEvent.None();
            }

            if (_phase != GamePhase.MemoryIntro || _flowBlocked)
            {
                return FlowEvent.None();
            }

            _actorSelected = true;
            _phase = GamePhase.RewindReady;
            return FlowEvent.Make("actor-selected", true, true);
        }

        /// <summary>
        /// 请求倒流。本方法只做"能不能倒流"的判定与置位，
        /// 真正的状态切换由视图在演出结束后调用 CompleteRewind()。
        /// 这样重复点击在这里就被挡掉了，不会出现倒流两次。
        /// </summary>
        public FlowEvent RequestRewind()
        {
            if (_phase != GamePhase.RewindReady || _rewindUsed || _flowBlocked)
            {
                return FlowEvent.None();
            }

            _rewindUsed = true;
            _phase = GamePhase.Rewinding;
            _flowBlocked = true;
            return FlowEvent.Make("rewind-started", false, true);
        }

        /// <summary>倒流演出结束：把被选中的人与其物件切换到"事发之前"的预设状态。</summary>
        public FlowEvent CompleteRewind()
        {
            if (_phase != GamePhase.Rewinding || _level == null)
            {
                return FlowEvent.None();
            }

            _propState = PropState.BeforeEvent;
            _phase = GamePhase.AwaitingChoice;
            _flowBlocked = false;
            return FlowEvent.Make("rewind-done", true, true);
        }

        /// <summary>玩家做出选择。isChoiceA 对应"追求机会"的那一项。</summary>
        public FlowEvent ApplyChoice(bool isChoiceA)
        {
            if (_phase != GamePhase.AwaitingChoice || _level == null || _flowBlocked)
            {
                return FlowEvent.None();
            }

            MemoryChoice choice = isChoiceA ? _level.ChoiceA : _level.ChoiceB;
            if (choice == null)
            {
                return FlowEvent.None();
            }

            _propState = isChoiceA ? PropState.ChoiceA : PropState.ChoiceB;
            _phase = GamePhase.ShowingOutcome;
            RecordChoice(_level.Index, isChoiceA);
            _ledger.Ambition += choice.AmbitionDelta;
            _ledger.Connection += choice.ConnectionDelta;
            return FlowEvent.Make("choice-applied", true, true);
        }

        /// <summary>进入下一段记忆；已经是最后一段时进入反转演出。</summary>
        public FlowEvent Advance()
        {
            if (_phase != GamePhase.ShowingOutcome || _level == null)
            {
                return FlowEvent.None();
            }

            if (_level.Index >= MemoryBook.LevelCount - 1)
            {
                _phase = GamePhase.Reveal;
                return FlowEvent.Make("reveal-started", false, false);
            }

            return EnterMemoryInternal(_level.Index + 1, "next-level");
        }

        /// <summary>反转演出播放完毕。</summary>
        public FlowEvent FinishReveal()
        {
            if (_phase != GamePhase.Reveal)
            {
                return FlowEvent.None();
            }

            _phase = GamePhase.Ending;
            return FlowEvent.Make("ending-entered", false, true);
        }

        /// <summary>重开当前这段记忆：本关的倒流次数与选择重置，前几关的累计变量保留。</summary>
        public FlowEvent RestartCurrentMemory()
        {
            if (_level == null)
            {
                return FlowEvent.None();
            }

            int index = _level.Index;
            ClearChoiceRecord(index);
            RecomputeLedgerFromHistory();
            return EnterMemoryInternal(index, "restart-memory");
        }

        /// <summary>从第一段记忆重新开始：所有变量清零（手册验收第 7 条）。</summary>
        public FlowEvent RestartFromBeginning()
        {
            _ledger.Reset();
            _level = null;
            _actorSelected = false;
            _rewindUsed = false;
            _flowBlocked = false;
            _propState = PropState.AfterEvent;
            _phase = GamePhase.Title;
            return FlowEvent.Make("restart-all", false, true);
        }

        // ------------------------------------------------------------ 结局

        /// <summary>
        /// 结局判定（手册给出的规则，保持可读、可解释）：
        /// 1. 换掉最后一块旧木板 → 全新的船；
        /// 2. 保留旧木板且 Ambition == Connection → 修补之船；
        /// 3. 保留旧木板且两者不等 → 旧船仍在。
        /// 为了让三个结局都能被稳定触发，前两关的选择也参与判定。
        /// </summary>
        public EndingKind ResolveEnding()
        {
            if (_ledger.Level3ReplacedBoard)
            {
                return EndingKind.BrandNewBoat;
            }

            if (_ledger.Ambition == _ledger.Connection)
            {
                return EndingKind.MendedBoat;
            }

            // 两个岔路口都偏向同一边：船保住了，但有些遗憾补不回来
            if (_ledger.Level1ChasedTrain && _ledger.Level2KeptWorking)
            {
                return EndingKind.OldBoatRemains;
            }

            if (_ledger.Level1Stayed && _ledger.Level2AnsweredCall)
            {
                return EndingKind.MendedBoat;
            }

            return EndingKind.OldBoatRemains;
        }

        // ------------------------------------------------------------ 视图要用的派生信息

        /// <summary>第二关：第一关是否留下了站台照片。</summary>
        public bool ShowStationPhoto()
        {
            return _level != null && _level.Index == 1 && (_ledger.Level1Stayed || _ledger.Level1ChasedTrain);
        }

        /// <summary>第一章照片下的说明文字，直接反映第一关的选择。</summary>
        public string StationPhotoCaption()
        {
            if (_ledger.Level1Stayed)
            {
                return "站台 · 那一年你没有追上去";
            }

            if (_ledger.Level1ChasedTrain)
            {
                return "站台 · 那一年你追了上去";
            }

            return "站台 · 那一年你还没有选择";
        }

        /// <summary>第二关桌面上显示器的工作进度（纯表现，但由选择决定）。</summary>
        public float WorkProgress()
        {
            if (_level == null || _level.Index != 1)
            {
                return 0.35f;
            }

            switch (_propState)
            {
                case PropState.BeforeEvent: return 0.42f;
                case PropState.ChoiceA: return 0.86f;
                case PropState.ChoiceB: return 0.22f;
                default: return 0.68f;
            }
        }

        // ------------------------------------------------------------ 内部

        private FlowEvent EnterMemoryInternal(int index, string reason)
        {
            if (index < 0 || index >= MemoryBook.LevelCount)
            {
                _phase = GamePhase.Ending;
                return FlowEvent.Make("ending-entered", false, true);
            }

            _level = MemoryBook.Get(index);
            _propState = PropState.AfterEvent;
            _actorSelected = false;
            _rewindUsed = false;
            _flowBlocked = false;
            _phase = GamePhase.MemoryIntro;
            return FlowEvent.Make(reason, true, true);
        }

        private void RecordChoice(int levelIndex, bool isChoiceA)
        {
            switch (levelIndex)
            {
                case 0:
                    if (isChoiceA)
                    {
                        _ledger.Level1ChasedTrain = true;
                    }
                    else
                    {
                        _ledger.Level1Stayed = true;
                    }

                    break;
                case 1:
                    if (isChoiceA)
                    {
                        _ledger.Level2KeptWorking = true;
                    }
                    else
                    {
                        _ledger.Level2AnsweredCall = true;
                    }

                    break;
                default:
                    _ledger.Level3ReplacedBoard = isChoiceA;
                    break;
            }
        }

        private void ClearChoiceRecord(int levelIndex)
        {
            switch (levelIndex)
            {
                case 0:
                    _ledger.Level1ChasedTrain = false;
                    _ledger.Level1Stayed = false;
                    break;
                case 1:
                    _ledger.Level2KeptWorking = false;
                    _ledger.Level2AnsweredCall = false;
                    break;
                default:
                    _ledger.Level3ReplacedBoard = false;
                    break;
            }
        }

        /// <summary>
        /// 重开本段后按当前历史重算数值，避免"重开一次数值翻倍"。
        /// </summary>
        private void RecomputeLedgerFromHistory()
        {
            int ambition = 0;
            int connection = 0;

            if (_ledger.Level1ChasedTrain)
            {
                ambition += 1;
            }

            if (_ledger.Level1Stayed)
            {
                connection += 1;
            }

            if (_ledger.Level2KeptWorking)
            {
                ambition += 1;
            }

            if (_ledger.Level2AnsweredCall)
            {
                connection += 1;
            }

            _ledger.Ambition = ambition;
            _ledger.Connection = connection;
        }
    }
}
