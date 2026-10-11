namespace JieNiYiMiao
{
    /// <summary>
    /// 一段记忆里的一次选择。ChoiceA 一律对应增加 Ambition 的行动，ChoiceB 对应增加 Connection。
    /// </summary>
    public class MemoryChoice
    {
        public string ButtonLabel;
        /// <summary>选择后显示的结果文案。</summary>
        public string OutcomeText;
        /// <summary>选择后场景里发生的可见变化（由 SceneBuilder 解释成几何造型）。</summary>
        public string VisualNote;
        /// <summary>要加到 ChoiceLedger 上的数值。</summary>
        public int AmbitionDelta;
        public int ConnectionDelta;

        public static MemoryChoice Make(string label, string outcome, string visual, int ambition, int connection)
        {
            return new MemoryChoice
            {
                ButtonLabel = label,
                OutcomeText = outcome,
                VisualNote = visual,
                AmbitionDelta = ambition,
                ConnectionDelta = connection
            };
        }
    }

    /// <summary>
    /// 一段记忆的全部静态数据。文案和选项集中放在这里，
    /// 改剧情不用碰流程代码。视觉部分只给"画什么"的参数，不给贴图。
    ///
    /// 注意：本文件刻意不引用 UnityEngine（颜色用 MemoryTone），
    /// 因此 Core 目录可以脱离 Unity 独立编译与校验。
    /// </summary>
    public class MemoryLevelData
    {
        public int Index;
        public string ChapterLabel;
        public string Title;
        /// <summary>刚进场时的旁白（短句，手册要求克制）。</summary>
        public string IntroLine;
        /// <summary>提示玩家先点人物。</summary>
        public string PromptSelect;
        /// <summary>人物被选中后，提示去点倒流按钮。</summary>
        public string PromptRewind;
        /// <summary>倒流完成后的旁白。</summary>
        public string AfterRewindLine;
        /// <summary>倒流按钮上的文案（每关略有不同，避免机械感）。</summary>
        public string RewindButtonLabel;

        public string ActorName;
        public MemoryTone ActorTone;
        public StageSlot Slot;
        /// <summary>人物头顶上方的情绪气泡文字，用来表达"事发之后"的状态。</summary>
        public string ActorMoodAfterEvent;
        public string ActorMoodBeforeEvent;

        public string PropName;
        public MemoryTone PropTone;

        public MemoryChoice ChoiceA;
        public MemoryChoice ChoiceB;

        /// <summary>这段记忆暗示"同一个人"的线索，反转演出时依次闪现。</summary>
        public string IdentityClue;

        public MemoryTone SkyTone;
        public MemoryTone GroundTone;
        /// <summary>背景里重复出现的怀旧元素（站台/办公室/阁楼），由 SceneBuilder 解释。</summary>
        public string BackdropStyle;
    }

    /// <summary>三关的数据表。数值与文案改动集中在这一处。</summary>
    public static class MemoryBook
    {
        public const int LevelCount = 3;

        public static MemoryLevelData Get(int index)
        {
            switch (index)
            {
                case 0: return Level1();
                case 1: return Level2();
                default: return Level3();
            }
        }

        /// <summary>第一关：站台上的一秒。</summary>
        private static MemoryLevelData Level1()
        {
            return new MemoryLevelData
            {
                Index = 0,
                ChapterLabel = "第一段记忆",
                Title = "站台上的一秒",
                IntroLine = "八岁那年，你要离开家乡。母亲追着列车，把一艘木船举过头顶。",
                PromptSelect = "先点一下站台上的孩子。",
                PromptRewind = "他已经被你选中。点“借他一秒”，把这声再见退回去。",
                AfterRewindLine = "时间退回半分钟前。木船还热着，列车还没开动。",
                RewindButtonLabel = "借他一秒",
                ActorName = "年少的你",
                ActorTone = new MemoryTone(0.96f, 0.78f, 0.52f),
                Slot = StageSlot.Left,
                ActorMoodAfterEvent = "没有回头",
                ActorMoodBeforeEvent = "还在犹豫",
                PropName = "两人一起做的木船",
                PropTone = new MemoryTone(0.72f, 0.48f, 0.28f),
                ChoiceA = MemoryChoice.Make(
                    "追赶列车",
                    "你追上了那班车。Ambition +1 —— 你选择了更远的地方。",
                    "男孩冲向列车，木船被留在了站台的黄线外",
                    1, 0),
                ChoiceB = MemoryChoice.Make(
                    "留下好好告别",
                    "你留下来了。Connection +1 —— 你选择把这一秒留给母亲。",
                    "男孩站住，把木船抱回怀里，母亲的手停在他肩上",
                    0, 1),
                IdentityClue = "木船的船身上，刻着两笔歪歪扭扭的字",
                SkyTone = new MemoryTone(0.94f, 0.86f, 0.72f),
                GroundTone = new MemoryTone(0.42f, 0.33f, 0.26f),
                BackdropStyle = "station"
            };
        }

        /// <summary>第二关：没有接通的电话。</summary>
        private static MemoryLevelData Level2()
        {
            return new MemoryLevelData
            {
                Index = 1,
                ChapterLabel = "第二段记忆",
                Title = "没有接通的电话",
                IntroLine = "二十九岁，凌晨一点的办公室。手机亮了一下，然后安静下去。",
                PromptSelect = "先点一下坐在桌前的那个人。",
                PromptRewind = "他已经被你选中。点“借他一秒”，让那通电话再响一次。",
                AfterRewindLine = "时间退回电话响起的第一秒。屏幕上还亮着那个名字。",
                RewindButtonLabel = "借他一秒",
                ActorName = "二十九岁的你",
                ActorTone = new MemoryTone(0.62f, 0.72f, 0.86f),
                Slot = StageSlot.Center,
                ActorMoodAfterEvent = "没有接",
                ActorMoodBeforeEvent = "手指停在屏幕上",
                PropName = "响着的手机",
                PropTone = new MemoryTone(0.30f, 0.34f, 0.42f),
                ChoiceA = MemoryChoice.Make(
                    "继续工作",
                    "你把手机翻了过去。Ambition +1 —— 报告在明天早上六点要交。",
                    "手机被屏幕朝下扣在桌上，台灯的光圈重新收紧",
                    1, 0),
                ChoiceB = MemoryChoice.Make(
                    "接听这通电话",
                    "你接了起来。Connection +1 —— 那头的声音有点抖。",
                    "手机被拿起来贴到耳边，台灯的光圈散开",
                    0, 1),
                IdentityClue = "桌角压着一张站台的照片，黄线外站着一个孩子",
                SkyTone = new MemoryTone(0.20f, 0.24f, 0.33f),
                GroundTone = new MemoryTone(0.13f, 0.15f, 0.21f),
                BackdropStyle = "office"
            };
        }

        /// <summary>第三关：木船还是木船吗？</summary>
        private static MemoryLevelData Level3()
        {
            return new MemoryLevelData
            {
                Index = 2,
                ChapterLabel = "第三段记忆",
                Title = "木船还是木船吗？",
                IntroLine = "七十六岁，窗边的工作台上，只剩最后一块旧木板没换。",
                PromptSelect = "先点一下正在修船的老人。",
                PromptRewind = "他已经被你选中。点“借他一秒”，回到你伸手之前。",
                AfterRewindLine = "时间退回到你的手刚碰到那块木板的时候。",
                RewindButtonLabel = "借他一秒",
                ActorName = "七十六岁的你",
                ActorTone = new MemoryTone(0.86f, 0.80f, 0.70f),
                Slot = StageSlot.Right,
                ActorMoodAfterEvent = "手还举着",
                ActorMoodBeforeEvent = "手悬在木板上方",
                PropName = "刻着字的那块旧木板",
                PropTone = new MemoryTone(0.68f, 0.55f, 0.40f),
                ChoiceA = MemoryChoice.Make(
                    "换掉最后一块旧木板",
                    "船身终于平整了。刻着字的那块木板，被放进抽屉最底层。",
                    "木板被抽走，钉上崭新的木条，船身没有一处凹凸",
                    0, 0),
                ChoiceB = MemoryChoice.Make(
                    "保留刻着记忆的旧木板",
                    "你把新木板放回抽屉。船还是有点歪，但字还在。",
                    "新木条被放下，旧木板回到原位，刻痕重新对准窗光",
                    0, 0),
                IdentityClue = "船身上那两笔歪字，是所有人都在写的那两个字",
                SkyTone = new MemoryTone(0.90f, 0.84f, 0.74f),
                GroundTone = new MemoryTone(0.38f, 0.31f, 0.25f),
                BackdropStyle = "attic"
            };
        }
    }
}
