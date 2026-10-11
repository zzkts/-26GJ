using System;

namespace JieNiYiMiao
{
    /// <summary>
    /// 一个与引擎无关的颜色载体（0—1 的 rgb）。
    /// 流程与数据层只用它，视图层再转成 UnityEngine.Color。
    /// 这样做的目的：Core 目录不引用 UnityEngine，就能脱离 Unity 独立编译与运行校验。
    /// </summary>
    [Serializable]
    public struct MemoryTone
    {
        public float R;
        public float G;
        public float B;

        public MemoryTone(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }
    }
    /// <summary>
    /// 全局极简状态层：只放枚举和两个选择变量的载体。
    /// 刻意不做剧情引擎——手册要求"简单状态枚举 + 布尔/整数变量"。
    /// </summary>
    public enum GamePhase
    {
        /// <summary>标题 / 开始界面。</summary>
        Title,

        /// <summary>刚进入某段记忆：人物还没有被选中，不能倒流。</summary>
        MemoryIntro,

        /// <summary>人物已选中，可以点"借他一秒"倒流。</summary>
        RewindReady,

        /// <summary>倒流正在进行中的过渡态，用来吞掉重复点击。</summary>
        Rewinding,

        /// <summary>倒流完成，正在展示两个选择。每关只能到达这里一次。</summary>
        AwaitingChoice,

        /// <summary>玩家已经点了某个选择，正在播放结果反馈。</summary>
        ShowingOutcome,

        /// <summary>三段记忆重叠的反转演出。</summary>
        Reveal,

        /// <summary>结局展示，可重新开始。</summary>
        Ending
    }

    /// <summary>三关共用的四种物件状态，直接对应手册的"预设状态切换"。</summary>
    public enum PropState
    {
        /// <summary>A · 事发之后。</summary>
        AfterEvent,

        /// <summary>B · 倒流恢复的预设状态。</summary>
        BeforeEvent,

        /// <summary>C · 玩家选择 A 之后。</summary>
        ChoiceA,

        /// <summary>D · 玩家选择 B 之后。</summary>
        ChoiceB
    }

    /// <summary>这个角色在场景里的位置。</summary>
    public enum StageSlot
    {
        /// <summary>画布中偏左，第一关的主角位置。</summary>
        Left,

        /// <summary>画布正中，第二关。</summary>
        Center,

        /// <summary>画布中偏右，第三关。</summary>
        Right
    }

    /// <summary>结局编号，和手册的三种结局一一对应。</summary>
    public enum EndingKind
    {
        /// <summary>全新的船：换掉最后一块旧木板，刻痕消失。</summary>
        BrandNewBoat,

        /// <summary>旧船仍在：保留旧木板，但人生仍有无法弥补的遗憾。</summary>
        OldBoatRemains,

        /// <summary>修补之船：留住关键记忆，也允许其他部分改变。</summary>
        MendedBoat
    }

    /// <summary>
    /// 玩家这次游玩留下的痕迹。两个整数就是手册规定的全部数值：
    /// Ambition（追求机会/事业/改变现状）与 Connection（珍惜关系/陪伴/共同记忆）。
    /// 两者都不是善恶分。
    /// </summary>
    [Serializable]
    public class ChoiceLedger
    {
        public int Ambition;
        public int Connection;

        /// <summary>第二关读这个：第一关有没有"追赶列车"。</summary>
        public bool Level1ChasedTrain;

        /// <summary>第二关读这个：第一关有没有"留下告别"。</summary>
        public bool Level1Stayed;

        /// <summary>第三关读这个：第二关有没有把电话接起来。</summary>
        public bool Level2AnsweredCall;

        /// <summary>第三关读这个：第二关是不是继续工作。</summary>
        public bool Level2KeptWorking;

        /// <summary>第三关最终选择：是否换掉最后一块旧木板。</summary>
        public bool Level3ReplacedBoard;

        /// <summary>重新开始时把一切清零（手册验收第 7 条）。</summary>
        public void Reset()
        {
            Ambition = 0;
            Connection = 0;
            Level1ChasedTrain = false;
            Level1Stayed = false;
            Level2AnsweredCall = false;
            Level2KeptWorking = false;
            Level3ReplacedBoard = false;
        }

        /// <summary>给 HUD 用的一行摘要。</summary>
        public string Summary()
        {
            return "Ambition " + Ambition + "   ·   Connection " + Connection;
        }

#if DSH_VERIFY
        /// <summary>仅供独立校验程序构造历史状态，Unity 构建里不存在。</summary>
        internal bool VerifySetHistory(bool l1Chased, bool l1Stayed, bool l2Worked, bool l2Answered)
        {
            Level1ChasedTrain = l1Chased;
            Level1Stayed = l1Stayed;
            Level2KeptWorking = l2Worked;
            Level2AnsweredCall = l2Answered;
            return true;
        }
#endif
    }
}
