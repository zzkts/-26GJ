using System;

namespace CuoGuo
{
    /// <summary>
    /// 借时债务。
    ///
    /// 规则（策划案第三部分第 5 节，这里是唯一实现处）：
    /// - 每章记录 MaxPastDepthReached = 本章曾经到过的最深过去程度，只增不减；
    /// - ChapterDebt = round(本章权重 × MaxPastDepthReached)；
    /// - GlobalDebt = CommittedDebt + ChapterDebt，范围 0—100，跨章节继承、永不退款；
    /// - 重复经过已探索过的深度不再收费，向右拖回现在也不退钱；
    /// - 大幅快速拖动时直接按新深度算出应有数值，不逐帧累加。
    ///
    /// 本类不引用 UnityEngine，因此这些规则可以脱离引擎跑断言。
    /// </summary>
    public class DebtModel
    {
        public const int ChapterCount = 3;

        /// <summary>三章权重，和必须为 100：每章都探到最早位置时债务正好满。</summary>
        private readonly float[] _weights;

        /// <summary>每章到过的最深过去程度（0—1）。</summary>
        private readonly float[] _maxDepth = new float[ChapterCount];

        /// <summary>已经结算进账的历史章节债务总和。</summary>
        private int _committed;

        private int _chapterIndex = -1;

        public DebtModel()
            : this(new[] { 30f, 30f, 40f })
        {
        }

        public DebtModel(float[] weights)
        {
            if (weights == null || weights.Length != ChapterCount)
            {
                throw new ArgumentException("三章权重必须各有一个值（30/30/40）", "weights");
            }

            _weights = weights;
        }

        public int ChapterIndex
        {
            get { return _chapterIndex; }
        }

        /// <summary>已经结算的历史章节债务。</summary>
        public int CommittedDebt
        {
            get { return _committed; }
        }

        /// <summary>当前章的债务。</summary>
        public int ChapterDebt
        {
            get
            {
                if (_chapterIndex < 0)
                {
                    return 0;
                }

                return DebtOf(_chapterIndex);
            }
        }

        /// <summary>全局债务（0—100）。</summary>
        public int GlobalDebt
        {
            get
            {
                int total = _committed + ChapterDebt;
                if (total < 0)
                {
                    return 0;
                }

                return total > 100 ? 100 : total;
            }
        }

        public float ChapterWeight(int chapterIndex)
        {
            return _weights[chapterIndex];
        }

        /// <summary>某一章到过的最深过去程度。</summary>
        public float MaxPastDepth(int chapterIndex)
        {
            if (chapterIndex < 0 || chapterIndex >= ChapterCount)
            {
                return 0f;
            }

            return _maxDepth[chapterIndex];
        }

        /// <summary>某一章按当前最深深度应付的债务。</summary>
        public int DebtOf(int chapterIndex)
        {
            if (chapterIndex < 0 || chapterIndex >= ChapterCount)
            {
                return 0;
            }

            return (int)Math.Round(_weights[chapterIndex] * _maxDepth[chapterIndex], MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 进入某一章。进入新章时把上一章的债务结算进 CommittedDebt，
        /// 新章的探索深度从 0 重新记录（债务本身不清零）。
        /// </summary>
        public void EnterChapter(int index)
        {
            if (index < 0 || index >= ChapterCount)
            {
                return;
            }

            if (_chapterIndex >= 0 && index != _chapterIndex)
            {
                _committed += DebtOf(_chapterIndex);
            }

            _chapterIndex = index;
        }

        /// <summary>
        /// 汇报当前时间位置。PastDepth = 1 - position，只有刷新了本章最深记录才会让债务变化。
        /// 这就是"重复经过已探索区域不再收费、拖回现在不退款"的落点。
        /// </summary>
        public void Report(float timelinePosition)
        {
            if (_chapterIndex < 0)
            {
                return;
            }

            float depth = 1f - Clamp01(timelinePosition);
            if (depth > _maxDepth[_chapterIndex])
            {
                _maxDepth[_chapterIndex] = depth;
            }
        }

        /// <summary>债务越过滤阈值（25/50/75/100）时，视图层用它决定画面怎么坏。</summary>
        public static int DecayStage(int debt)
        {
            if (debt >= 100)
            {
                return 4;
            }

            if (debt >= 75)
            {
                return 3;
            }

            if (debt >= 50)
            {
                return 2;
            }

            return debt >= 25 ? 1 : 0;
        }

        /// <summary>把债务换算成 0—1 的"记忆损坏程度"，用于透明度与失色插值。</summary>
        public static float DecayAmount(int debt)
        {
            if (debt <= 0)
            {
                return 0f;
            }

            float t = debt > 100 ? 100f : debt;
            return t / 100f;
        }

        /// <summary>重新开始时彻底清零（策划案验收：重开后债务、章节记录都要清零）。</summary>
        public void Reset()
        {
            for (int i = 0; i < _maxDepth.Length; i++)
            {
                _maxDepth[i] = 0f;
            }

            _committed = 0;
            _chapterIndex = -1;
        }

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
