using System;
using CuoGuo;

namespace CuoGuo.Verify
{
    /// <summary>
    /// 《错过了那一秒》连续时间轴内核校验器。
    ///
    /// 它直接引用 Assets/Scripts/Core 下的真实源码（TimelineModel / DebtModel / StoryData / GameFlow），
    /// 这些文件刻意不引用 UnityEngine，所以可以脱离引擎真编译、真运行。
    ///
    /// 覆盖策划案第七部分的验收项：
    /// - 时间位置连续变化、不吸附到三个固定档位、松开后停在任意位置；
    /// - 债务只按最大回溯深度增加、拖回现在不退款、重复区域不重复收费、跨章继承、满额 100；
    /// - 线索只触发一次、在范围内保持可读、拖回现在自然消失；
    /// - 任何正常游玩路径都能进入同一个结局，且只播一次；
    /// - 重新开始后债务、章节、线索、结局状态全部清零。
    /// </summary>
    public static class Program
    {
        private static int _checks;
        private static int _failures;

        public static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=== 《错过了那一秒》连续时间轴内核校验 ===");

            DataSanity();
            InterpolationIsContinuous();
            NoSnappingToKeyFrames();
            SampleIsStateless();
            CurrentMemoryState();
            DeepestMemoryState();
            DebtBorrowsOnlyDeeper();
            DebtNoRefundAndNoRepeat();
            DebtCarriesAcrossChapters();
            DebtReachesExactlyOneHundred();
            CuesFireOnceOnly();
            CueTextPersistsInRange();
            EveryChapterHasAWayForward();
            EveryPathEndsInTheSameEnding();
            EndingPlaysOnceAndLocksSlider();
            RestartClearsEverything();
            SceneGeometry_ActorsStandOnDeck();
            SceneGeometry_RailStandsOnTheDock();
            SceneGeometry_KeyPropsSitOnTheBoat();

            Console.WriteLine("=== 共 " + _checks + " 组检查，失败 " + _failures + " 组 ===");
            return _failures == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------ 断言工具

        private static void Check(string name, bool ok, string detail)
        {
            _checks++;
            if (ok)
            {
                Console.WriteLine("  [通过] " + name);
                return;
            }

            _failures++;
            Console.WriteLine("  [失败] " + name + (string.IsNullOrEmpty(detail) ? string.Empty : "  → " + detail));
        }

        private static void CheckFloat(string name, float actual, float expected, float tolerance)
        {
            float diff = actual - expected;
            if (diff < 0f)
            {
                diff = -diff;
            }

            Check(name, diff <= tolerance, "实际 " + actual.ToString("0.0000") + "，期望 " + expected.ToString("0.0000"));
        }

        // ------------------------------------------------------------ 1. 数据自检

        private static void DataSanity()
        {
            Console.WriteLine("[1/19] 三章数据自检");

            float weightSum = 0f;
            bool finalCueExists = true;
            bool tracksSorted = true;
            bool enoughKeys = true;
            bool uniqueNames = true;
            string problem = string.Empty;

            for (int i = 0; i < StoryBook.ChapterCount; i++)
            {
                ChapterData c = StoryBook.Get(i);
                weightSum += c.DebtWeight;

                if (c.Timeline.IndexOfCue(c.Timeline.FinalCueId) < 0)
                {
                    finalCueExists = false;
                    problem = "第 " + (i + 1) + " 章的 FinalCueId 不在线索表里：" + c.Timeline.FinalCueId;
                }

                if (c.Timeline.Tracks.Count == 0)
                {
                    enoughKeys = false;
                    problem = "第 " + (i + 1) + " 章没有任何时间轨迹";
                }

                for (int t = 0; t < c.Timeline.Tracks.Count; t++)
                {
                    TimelineTrack track = c.Timeline.Tracks[t];
                    if (track.Keys.Count < 2)
                    {
                        enoughKeys = false;
                        problem = "轨迹 " + track.Name + " 少于两个关键帧，无法插值";
                    }

                    for (int k = 1; k < track.Keys.Count; k++)
                    {
                        if (track.Keys[k].Time <= track.Keys[k - 1].Time)
                        {
                            tracksSorted = false;
                            problem = "轨迹 " + track.Name + " 的关键帧没有按时间升序排列";
                        }
                    }

                    for (int t2 = t + 1; t2 < c.Timeline.Tracks.Count; t2++)
                    {
                        if (c.Timeline.Tracks[t2].Name == track.Name)
                        {
                            uniqueNames = false;
                            problem = "第 " + (i + 1) + " 章出现重复轨迹名：" + track.Name;
                        }
                    }
                }
            }

            Check("三章借时权重之和为 100", Math.Abs(weightSum - 100f) < 0.001f, "实际 " + weightSum);
            Check("每章都声明了最终线索且存在于线索表", finalCueExists, problem);
            Check("每章至少有轨迹，且每条轨迹至少有 2 个关键帧", enoughKeys, problem);
            Check("所有关键帧按时间升序（采样前提）", tracksSorted, problem);
            Check("同一章内轨迹名不重复", uniqueNames, problem);
        }

        // ------------------------------------------------------------ 2. 连续插值

        private static void InterpolationIsContinuous()
        {
            Console.WriteLine("[2/19] 时间位置连续变化（不是幻灯片跳切）");

            float biggestJump = 0f;
            string worst = string.Empty;

            for (int i = 0; i < StoryBook.ChapterCount; i++)
            {
                ChapterData c = StoryBook.Get(i);
                for (int t = 0; t < c.Timeline.Tracks.Count; t++)
                {
                    TimelineTrack track = c.Timeline.Tracks[t];
                    TrackSample prev = track.Sample(1f);
                    for (int s = 199; s >= 0; s--)
                    {
                        float p = s / 200f;
                        TrackSample now = track.Sample(p);
                        float jump = Math.Abs(now.Position.X - prev.Position.X) + Math.Abs(now.Position.Y - prev.Position.Y)
                                     + Math.Abs(now.Color.A - prev.Color.A) * 100f;
                        if (jump > biggestJump)
                        {
                            biggestJump = jump;
                            worst = c.Title + " / " + track.Name + " 在 " + p.ToString("0.000");
                        }

                        prev = now;
                    }
                }
            }

            // 200 等分下相邻采样不应该出现大跳变（每步最多跨一个关键帧区间）
            Check("以 0.005 为步长扫描全时间轴，相邻采样值没有突跳", biggestJump < 60f,
                "最大跳变 " + biggestJump.ToString("0.00") + "（" + worst + "）");
        }

        // ------------------------------------------------------------ 3. 不吸附

        private static void NoSnappingToKeyFrames()
        {
            Console.WriteLine("[3/19] 滑块不吸附到固定档位");

            ChapterData c1 = StoryBook.Get(0);
            TimelineTrack mother = c1.Timeline.FindTrack("Mother");

            // 母亲在 0.63 位于 x=-430，在 0.52 位于 x=-60。取正中间，应当停在两者之间。
            float mid = (0.63f + 0.52f) * 0.5f;
            TrackSample atMid = mother.Sample(mid);
            Check("两个关键帧正中间的位置落在两者之间，而不是吸附到最近的一帧",
                atMid.Position.X < -60f && atMid.Position.X > -430f,
                "x = " + atMid.Position.X.ToString("0.0"));

            // 采样值与线性插值一致（说明是真正的连续插值）
            float expected = -430f + (-60f - (-430f)) * ((mid - 0.63f) / (0.52f - 0.63f));
            CheckFloat("中间位置与线性插值结果一致", atMid.Position.X, expected, 0.01f);

            // 任意位置都能停：连续取 101 个位置，相邻位置的位移都不同且连续
            bool allDistinct = true;
            float last = mother.Sample(0.575f).Position.X;
            int sameCount = 0;
            for (int i = 0; i <= 100; i++)
            {
                float p = i / 100f;
                float x = mother.Sample(p).Position.X;
                if (Math.Abs(x - last) < 0.0001f)
                {
                    sameCount++;
                }

                last = x;
            }

            allDistinct = sameCount <= 30; // 只有落在关键帧之外的平台段才会重复
            Check("从 0 到 1 连续扫描，画面随位置持续变化", allDistinct, "重复采样点 " + sameCount + " 个");
        }

        // ------------------------------------------------------------ 4. 采样无状态

        private static void SampleIsStateless()
        {
            Console.WriteLine("[4/19] 同一个时间位置永远给出同一幅画面（松开鼠标就停在那里）");

            bool same = true;
            string detail = string.Empty;

            for (int i = 0; i < StoryBook.ChapterCount && same; i++)
            {
                ChapterData c = StoryBook.Get(i);
                for (int t = 0; t < c.Timeline.Tracks.Count; t++)
                {
                    TimelineTrack track = c.Timeline.Tracks[t];
                    for (int s = 0; s <= 20; s++)
                    {
                        float p = s / 20f;
                        TrackSample a = track.Sample(p);
                        TrackSample b = track.Sample(p);
                        if (a.Position.X != b.Position.X || a.Color.A != b.Color.A)
                        {
                            same = false;
                            detail = c.Title + " / " + track.Name + " 在 " + p;
                            break;
                        }
                    }
                }
            }

            Check("重复采样同一位置结果完全一致（时间冻结，不会自动继续播放）", same, detail);
        }

        // ------------------------------------------------------------ 5. 现在的画面

        private static void CurrentMemoryState()
        {
            Console.WriteLine("[5/19] 每一章停在 1.00 时是策划案里的「当前画面」（关键道具藏起来）");

            ChapterData c1 = StoryBook.Get(0);
            ChapterData c2 = StoryBook.Get(1);
            ChapterData c3 = StoryBook.Get(2);

            Check("第一章现在：罗盘不可见", c1.Timeline.FindTrack("Compass").Sample(1f).Color.A < 0.01f, null);
            Check("第一章现在：船舷刻字仍模糊", c1.Timeline.FindTrack("Carving").Sample(1f).Color.A < 0.2f, null);
            Check("第二章现在：海图只露出一角", c2.Timeline.FindTrack("Chart").Sample(1f).Color.A > 0.3f && c2.Timeline.FindTrack("Chart").Sample(1f).Color.A < 0.9f, null);
            Check("第三章现在：立起的纸船不可见", c3.Timeline.FindTrack("Letter").Sample(1f).Color.A < 0.01f, null);
            Check("第三章现在：展开的信纸不可见", c3.Timeline.FindTrack("LetterOpen").Sample(1f).Color.A < 0.01f, null);
            Check("第三章现在：夹层里的红线露出一截", c3.Timeline.FindTrack("RedThread").Sample(1f).Color.A > 0.3f, null);
            Check("第一章现在：船底压着一块不自然的阴影（策划案明确要求的伏笔）",
                c1.Timeline.FindTrack("HullShadow").Sample(1f).Color.A > 0.7f, null);
            Check("第三章现在：工作灯照在木板上（光池在船身范围内）",
                Math.Abs(c3.Timeline.FindTrack("WorkLight").Sample(1f).Position.X - SceneMetrics.BoatX) < SceneMetrics.BoatWidth * 0.5f
                && c3.Timeline.FindTrack("WorkLight").Sample(1f).Color.A > 0.3f, null);
        }

        // ------------------------------------------------------------ 6. 最早处的画面

        private static void DeepestMemoryState()
        {
            Console.WriteLine("[6/19] 每一章停在 0.00 时能看清关键线索");

            ChapterData c1 = StoryBook.Get(0);
            ChapterData c2 = StoryBook.Get(1);
            ChapterData c3 = StoryBook.Get(2);

            Check("第一章最深处：罗盘清晰可见", c1.Timeline.FindTrack("Compass").Sample(0f).Color.A > 0.9f, null);
            Check("第一章最深处：刻字完整显现", c1.Timeline.FindTrack("Carving").Sample(0f).Color.A > 0.9f, null);
            Check("第一章最深处：母亲仍在画面里（变淡但没消失）",
                c1.Timeline.FindTrack("Mother").Sample(0f).Color.A > 0.3f, null);
            Check("第二章最深处：朋友出现在入口", c2.Timeline.FindTrack("Friend").Sample(0f).Color.A > 0.01f, null);
            Check("第二章最深处：海图完整可见", c2.Timeline.FindTrack("Chart").Sample(0f).Color.A > 0.9f, null);
            Check("第三章最深处：展开的信纸清晰可见", c3.Timeline.FindTrack("LetterOpen").Sample(0f).Color.A > 0.9f, null);
            Check("第三章最深处：女孩在船边", c3.Timeline.FindTrack("Woman").Sample(0f).Color.A > 0.9f, null);
            Check("第一章最深处：船底的阴影已经退开，罗盘浮了出来",
                c1.Timeline.FindTrack("HullShadow").Sample(0f).Color.A < 0.15f, null);
            Check("第三章最深处：工作灯更亮（记忆对准了那处夹层）",
                c3.Timeline.FindTrack("WorkLight").Sample(0f).Color.A
                > c3.Timeline.FindTrack("WorkLight").Sample(1f).Color.A + 0.15f, null);
        }

        // ------------------------------------------------------------ 7. 债务只增不减

        private static void DebtBorrowsOnlyDeeper()
        {
            Console.WriteLine("[7/19] 债务只按「最大回溯深度」增加");

            var debt = new DebtModel();
            debt.EnterChapter(0);
            Check("刚到这一段记忆时债务为 0", debt.GlobalDebt == 0, "实际 " + debt.GlobalDebt);

            debt.Report(0.5f); // 深入一半：30 × 0.5 = 15
            Check("深入一半，第一章债务为 15", debt.GlobalDebt == 15, "实际 " + debt.GlobalDebt);

            debt.Report(0.2f); // 更深：30 × 0.8 = 24
            Check("继续深入，债务升到 24", debt.GlobalDebt == 24, "实际 " + debt.GlobalDebt);

            debt.Report(0.05f); // 30 × 0.95 = 28.5 → 29
            Check("走到很深的地方，债务升到 29", debt.GlobalDebt == 29, "实际 " + debt.GlobalDebt);

            // 一次大幅拖动直接落到最深，应当直接得到该深度的数值，而不是逐帧累加
            var fast = new DebtModel();
            fast.EnterChapter(0);
            fast.Report(0f);
            Check("同一帧从 1.0 直接拖到 0.0，只按新深度算一次（30）", fast.GlobalDebt == 30, "实际 " + fast.GlobalDebt);
        }

        // ------------------------------------------------------------ 8. 不退款、不重复收费

        private static void DebtNoRefundAndNoRepeat()
        {
            Console.WriteLine("[8/19] 拖回现在不退款，重复经过已探索区域不重复收费");

            var debt = new DebtModel();
            debt.EnterChapter(0);
            debt.Report(0.3f);
            int borrowed = debt.GlobalDebt;

            debt.Report(1f);
            Check("向右拖回现在：债务不变（不退款）", debt.GlobalDebt == borrowed, "实际 " + debt.GlobalDebt);

            for (int i = 0; i < 40; i++)
            {
                debt.Report(0.5f);
                debt.Report(0.3f);
                debt.Report(0.9f);
            }

            Check("在同一段区间来回拖动 40 次：债务一分钱都没多收", debt.GlobalDebt == borrowed,
                "实际 " + debt.GlobalDebt + "，原本 " + borrowed);
        }

        // ------------------------------------------------------------ 9. 跨章继承

        private static void DebtCarriesAcrossChapters()
        {
            Console.WriteLine("[9/19] 债务跨章节继承，进入新章不清零");

            var debt = new DebtModel();
            debt.EnterChapter(0);
            debt.Report(0.5f);
            Check("第一章借到 15", debt.GlobalDebt == 15, "实际 " + debt.GlobalDebt);

            debt.EnterChapter(1);
            Check("进入第二章：上一章的 15 已结算，新章从 0 开始", debt.GlobalDebt == 15 && debt.CommittedDebt == 15,
                "全局 " + debt.GlobalDebt + "，已结算 " + debt.CommittedDebt);

            debt.Report(0.5f);
            Check("第二章再借一半：15 + 15 = 30", debt.GlobalDebt == 30, "实际 " + debt.GlobalDebt);

            debt.EnterChapter(2);
            Check("进入第三章后旧债仍在（不清零）", debt.GlobalDebt == 30, "实际 " + debt.GlobalDebt);
        }

        // ------------------------------------------------------------ 10. 满额

        private static void DebtReachesExactlyOneHundred()
        {
            Console.WriteLine("[10/19] 三章都探到最早位置，债务正好达到 100");

            var debt = new DebtModel();
            for (int i = 0; i < DebtModel.ChapterCount; i++)
            {
                debt.EnterChapter(i);
                debt.Report(0f);
            }

            Check("债务上限正好是 100（30 + 30 + 40）", debt.GlobalDebt == 100, "实际 " + debt.GlobalDebt);
            Check("债务不会超过 100", debt.GlobalDebt <= 100, null);
            Check("100 对应最高一档画面损耗", DebtModel.DecayStage(100) == 4, null);
            Check("0 对应画面完整", DebtModel.DecayStage(0) == 0, null);
            Check("25/50/75 分别跨过三档阈值",
                DebtModel.DecayStage(25) == 1 && DebtModel.DecayStage(50) == 2 && DebtModel.DecayStage(75) == 3, null);
            Check("24 还没到第一档", DebtModel.DecayStage(24) == 0, null);
        }

        // ------------------------------------------------------------ 11. 线索只触发一次

        private static void CuesFireOnceOnly()
        {
            Console.WriteLine("[11/19] 线索只在第一次越过阈值时触发，来回拖动不重复弹字");

            var flow = new GameFlow();
            flow.StartGame();

            TimelineStep step = flow.SetTimelinePosition(0.9f);
            Check("刚到现在的状态，没有线索弹出", step.NewCue == null && flow.ActiveCueText == null, null);

            step = flow.SetTimelinePosition(0.6f);
            Check("越过 0.63 时第一次弹出线索", step.NewCue != null && step.NewCue.Id == "c1_hide",
                step.NewCue == null ? "没有触发" : step.NewCue.Id);

            flow.SetTimelinePosition(0.9f);
            step = flow.SetTimelinePosition(0.6f);
            Check("拖回现在再拖回来：不再重复弹字", step.NewCue == null, "又触发了 " + (step.NewCue == null ? "-" : step.NewCue.Id));
            Check("但线索文字仍然可读（不是只闪一帧）", flow.ActiveCueText == "她把什么藏进了船底？", flow.ActiveCueText);

            flow.SetTimelinePosition(0.1f);
            step = flow.SetTimelinePosition(0.02f);
            Check("走到最深处触发最后一条真相", step.NewCue != null && step.NewCue.Id == "c1_words",
                step.NewCue == null ? "没有触发" : step.NewCue.Id);
            Check("所有线索只被记为已发现一次", flow.Cues.FiredCount == 3, "实际 " + flow.Cues.FiredCount);
        }

        // ------------------------------------------------------------ 12. 文字在范围内保持

        private static void CueTextPersistsInRange()
        {
            Console.WriteLine("[12/19] 线索文字在一段范围内保持可读，拖回现在就消失");

            var flow = new GameFlow();
            flow.StartGame();
            flow.SetTimelinePosition(0.5f);

            string at05 = flow.ActiveCueText;
            flow.SetTimelinePosition(0.4f);
            string at04 = flow.ActiveCueText;
            Check("同一条线索在 0.4—0.5 的范围内一直显示", at05 != null && at05 == at04, at05 + " / " + at04);

            flow.SetTimelinePosition(0.95f);
            Check("向右拖回现在后文字消失（因为那一秒还没被借到）", flow.ActiveCueText == null, flow.ActiveCueText);

            flow.SetTimelinePosition(0.5f);
            Check("再拖回过去，文字重新出现", flow.ActiveCueText == at05, flow.ActiveCueText);
        }

        // ------------------------------------------------------------ 13. 不会卡关

        private static void EveryChapterHasAWayForward()
        {
            Console.WriteLine("[13/19] 任何一章都不会因为玩家谨慎而无法继续");

            // 路径一：一步不拖，直接点"把现在留住"
            var cautious = new GameFlow();
            cautious.StartGame();
            bool ok = true;
            for (int i = 0; i < StoryBook.ChapterCount; i++)
            {
                if (cautious.CanContinue)
                {
                    ok = false;
                }

                if (!cautious.RequestHold())
                {
                    ok = false;
                }

                if (!cautious.CanContinue || !cautious.RequestContinue())
                {
                    ok = false;
                }
            }

            Check("完全不拖动时间、每章点「把现在留住」：也能一路走到底", ok, null);

            // 路径二：只借一点点时间，看到最终线索
            var shallow = new GameFlow();
            shallow.StartGame();
            bool ok2 = true;
            for (int i = 0; i < StoryBook.ChapterCount; i++)
            {
                float deep = i == 0 ? 0.04f : (i == 1 ? 0.28f : 0.04f);
                shallow.SetTimelinePosition(deep);
                if (!shallow.CanContinue || !shallow.RequestContinue())
                {
                    ok2 = false;
                }
            }

            Check("只借到刚好看清最终线索：也能一路走到底", ok2, null);

            // 路径三：拖到最深再继续
            var deepest = new GameFlow();
            deepest.StartGame();
            bool ok3 = true;
            for (int i = 0; i < StoryBook.ChapterCount; i++)
            {
                deepest.SetTimelinePosition(0f);
                if (!deepest.RequestContinue())
                {
                    ok3 = false;
                }
            }

            Check("每章都拖到最早位置：也能一路走到底", ok3, null);
        }

        // ------------------------------------------------------------ 14. 唯一结局

        private static void EveryPathEndsInTheSameEnding()
        {
            Console.WriteLine("[14/19] 借多借少都进入同一个结局");

            var flow = new GameFlow();
            flow.StartGame();
            for (int i = 0; i < StoryBook.ChapterCount; i++)
            {
                flow.SetTimelinePosition(0f);
                flow.RequestContinue();
            }

            Check("第三章走完后进入结局阶段", flow.Phase == GamePhase.Ending, "实际 " + flow.Phase);

            var flow2 = new GameFlow();
            flow2.StartGame();
            for (int i = 0; i < StoryBook.ChapterCount; i++)
            {
                flow2.RequestHold();
                flow2.RequestContinue();
            }

            Check("一分钟都不借的玩家也进入同一个结局", flow2.Phase == GamePhase.Ending, "实际 " + flow2.Phase);
            Check("没有分支结局：结局阶段只有一种", flow.Phase == flow2.Phase, null);
        }

        // ------------------------------------------------------------ 15. 结局锁输入

        private static void EndingPlaysOnceAndLocksSlider()
        {
            Console.WriteLine("[15/19] 结局期间滑块被锁住，演出只播一次");

            var flow = new GameFlow();
            flow.StartGame();
            for (int i = 0; i < StoryBook.ChapterCount; i++)
            {
                flow.SetTimelinePosition(0f);
                flow.RequestContinue();
            }

            Check("进入结局后滑块不可拖动", !flow.CanDragSlider, null);

            TimelineStep step = flow.SetTimelinePosition(0.5f);
            Check("结局期间拖动时间不生效", !step.Changed && Math.Abs(flow.TimelinePosition - 0f) < 0.001f,
                "位置变成了 " + flow.TimelinePosition);

            Check("结局期间不能再次点继续", !flow.RequestContinue(), null);

            flow.FinishEnding();
            Check("演出播完后进入可重新开始的状态", flow.Phase == GamePhase.Finished && !flow.InputLocked, null);
            Check("结局不会重复播放", !flow.RequestContinue(), null);
        }

        // ------------------------------------------------------------ 16. 重开清零

        private static void RestartClearsEverything()
        {
            Console.WriteLine("[16/19] 重新开始后所有数据清零");

            var flow = new GameFlow();
            flow.StartGame();
            flow.SetTimelinePosition(0f);
            flow.RequestContinue();
            flow.SetTimelinePosition(0f);
            flow.RequestContinue();
            flow.EnterEnding();
            flow.FinishEnding();

            int debtBefore = flow.Debt;
            flow.RestartAll();

            Check("重开后回到标题界面", flow.Phase == GamePhase.Title, "实际 " + flow.Phase);
            Check("重开后债务清零（原来借了 " + debtBefore + "）", flow.Debt == 0, "实际 " + flow.Debt);
            Check("重开后章节记录清零", flow.ChapterIndex == -1, "实际 " + flow.ChapterIndex);
            Check("重开后滑块回到现在的一端", Math.Abs(flow.TimelinePosition - 1f) < 0.001f, "实际 " + flow.TimelinePosition);
            Check("重开后线索触发标记清零", flow.Cues == null, null);

            flow.StartGame();
            Check("重开后能重新开始第一段记忆", flow.Phase == GamePhase.Chapter && flow.ChapterIndex == 0, null);
            Check("重开后第一章的线索重新可触发", flow.SetTimelinePosition(0.1f).NewCue != null, null);
            Check("重开后债务从 0 重新计（借到 0.1 得 27）", flow.Debt == 27, "实际 " + flow.Debt);
        }

        // ------------------------------------------------------------ 17. 人物站在栈道上

        /// <summary>
        /// 时间轴里的人物坐标必须和布景在同一套基准上：
        /// 人物根节点在 y、身高 H、缩放 s 时，脚底落在 y - H×s/2。
        /// 它必须落在木栈道那条线上（允许蹲下时陷进去一点、站立时误差几像素）。
        ///
        /// 本项目第一版就是这里错了：蹲下时加了缩放却没重算 y，人物整只陷进栈道 40 多像素。
        /// </summary>
        private static void SceneGeometry_ActorsStandOnDeck()
        {
            Console.WriteLine("[17/19] 时间轴里的人物脚底都踩在木栈道上");
            float deepestSink = 0f;   // 脚底低于栈道的最大值（画布 y 向下为负值）
            float highestFloat = 0f;  // 脚底高于栈道的最大值
            string worst = string.Empty;
            bool ok = true;

            for (int i = 0; i < StoryBook.ChapterCount; i++)
            {
                ChapterData c = StoryBook.Get(i);
                for (int a = 0; a < c.GroundActors.Count; a++)
                {
                    GroundActor actor = c.GroundActors[a];
                    TimelineTrack track = c.Timeline.FindTrack(actor.Track);
                    if (track == null)
                    {
                        ok = false;
                        worst = c.Title + " 里没有 " + actor.Track + " 的轨迹";
                        continue;
                    }

                    for (int k = 0; k < track.Keys.Count; k++)
                    {
                        KeyFrame key = track.Keys[k];
                        if (key.Color.A < 0.1f)
                        {
                            continue; // 还没出场的位置不必站在地上
                        }

                        // 脚底 = 根节点 y - 身高×缩放/2。画布 y 轴向上，所以脚底比栈道小就是陷进去了。
                        float feet = key.Position.Y - actor.Height * key.Scale * 0.5f;
                        float deviation = feet - SceneMetrics.DeckY;
                        float sink = deviation < 0f ? -deviation : 0f;
                        float floating = deviation > 0f ? deviation : 0f;

                        if (sink > deepestSink)
                        {
                            deepestSink = sink;
                            worst = c.Title + " / " + actor.Track + " 在 " + key.Time.ToString("0.00")
                                    + "：脚底 y=" + feet.ToString("0") + "，比栈道低 " + sink.ToString("0.0") + " 像素";
                        }

                        if (floating > highestFloat)
                        {
                            highestFloat = floating;
                        }

                        if (sink > SceneMetrics.FootToleranceBelow || floating > SceneMetrics.FootToleranceAbove)
                        {
                            ok = false;
                        }
                    }
                }
            }

            Check("最深陷进 " + deepestSink.ToString("0.0") + " 像素（上限 "
                  + SceneMetrics.FootToleranceBelow.ToString("0") + "）、最高悬空 " + highestFloat.ToString("0.0")
                  + " 像素（上限 " + SceneMetrics.FootToleranceAbove.ToString("0") + "）", ok, worst);
        }

        // ------------------------------------------------------------ 18. 滑块底座立在码头上

        /// <summary>
        /// 滑块底座（栏杆 / 系船桩 / 工作台）必须真的立在码头上：
        /// 立柱长度 = 轨道粗细 × 底座系数，柱底要落到 y=-300 附近。
        /// 不能被放到半空——那会变成"一块飘在海面上的木头"，玩家一眼就出戏。
        /// </summary>
        private static void SceneGeometry_RailStandsOnTheDock()
        {
            Console.WriteLine("[18/19] 三处滑块底座都立在码头上，没有飘在半空");

            bool ok = true;
            string detail = string.Empty;

            for (int i = 0; i < StoryBook.ChapterCount; i++)
            {
                ChapterData c = StoryBook.Get(i);
                float postBottom = c.RailCenter.Y - c.RailThickness * c.RailPostDropFactor;
                // 画布 y 向上：柱底比栈道小 = 扎进栈道里（正常，桩子就该扎进去）；
                // 比栈道大 = 悬在半空（就是"一块飘在海面上的木头"）。
                float intoDock = SceneMetrics.DeckY - postBottom;
                float aboveDock = -intoDock;

                // 柱底允许扎进栈道最多 60 像素，最多只能高出栈道 10 像素
                bool stands = intoDock <= 60f && aboveDock <= 10f;
                // 轨道本身要在栈道上方，玩家才看得到、够得着
                bool visible = c.RailCenter.Y > SceneMetrics.DeckY + 20f;
                // 不能伸出画面外
                bool inside = Abs(c.RailCenter.X) + c.RailWidth * 0.5f < 960f;

                bool fine = stands && visible && inside;
                if (!fine)
                {
                    ok = false;
                }

                detail += (i == 0 ? string.Empty : "；")
                          + c.Title + " 柱底 " + (intoDock >= 0f
                              ? "扎进栈道 " + intoDock.ToString("0") + " 像素"
                              : "悬空 " + aboveDock.ToString("0") + " 像素")
                          + (fine ? " ✓" : " ✗");
            }

            Check("三处底座：柱底落在栈道上、轨道在栈道上方、整体不超出画面", ok, detail);
        }

        // ------------------------------------------------------------ 19. 关键道具在船上

        /// <summary>
        /// 三章的关键道具必须真的停在木船上（船底 / 船缝 / 夹层），
        /// 而不是浮在旁边的空气里。船身会随滑块起伏 ±10 像素，所以判定框放宽这么多。
        /// </summary>
        private static void SceneGeometry_KeyPropsSitOnTheBoat()
        {
            Console.WriteLine("[19/19] 关键道具确实停在木船上（船底/船缝/夹层）");

            float halfW = SceneMetrics.BoatWidth * 0.5f + 14f;
            float halfH = SceneMetrics.BoatHeight * 0.5f + 16f;

            var cases = new[]
            {
                new object[] { 0, "Compass", 0.00f, "船底的罗盘" },
                new object[] { 0, "Carving", 0.00f, "船舷的刻字" },
                new object[] { 1, "Chart", 1.00f, "船缝里露出的海图" },
                new object[] { 2, "Letter", 0.00f, "夹层里的纸船" },
                new object[] { 2, "LetterOpen", 0.00f, "展开的信" },
                new object[] { 2, "RedThread", 0.00f, "系在纸船上的红线" }
            };

            bool ok = true;
            string detail = string.Empty;

            for (int i = 0; i < cases.Length; i++)
            {
                int chapterIndex = (int)cases[i][0];
                string trackName = (string)cases[i][1];
                float position = (float)cases[i][2];
                string label = (string)cases[i][3];

                ChapterData c = StoryBook.Get(chapterIndex);
                TimelineTrack track = c.Timeline.FindTrack(trackName);
                if (track == null)
                {
                    ok = false;
                    detail += "；缺少轨迹 " + trackName;
                    continue;
                }

                TrackSample sample = track.Sample(position);
                float dx = sample.Position.X - SceneMetrics.BoatX;
                float dy = sample.Position.Y - SceneMetrics.BoatY;
                bool onBoat = Abs(dx) <= halfW && Abs(dy) <= halfH;
                if (!onBoat || sample.Color.A < 0.5f)
                {
                    ok = false;
                    detail += "；" + label + " 在 (" + sample.Position.X.ToString("0") + ","
                              + sample.Position.Y.ToString("0") + ") 透明度 " + sample.Color.A.ToString("0.00");
                }
            }

            Check("罗盘/刻字/海图/纸船/信/红线六件都在船身范围内且可见", ok, detail);
        }

        private static float Abs(float v)
        {
            return v < 0f ? -v : v;
        }
    }
}
