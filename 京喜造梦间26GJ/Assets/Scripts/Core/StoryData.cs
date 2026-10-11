using System.Collections.Generic;

namespace CuoGuo
{
    /// <summary>
    /// 场景的基准尺寸——**唯一的事实源**。
    ///
    /// 时间轴的坐标是画布坐标，如果人物/道具的坐标和布景不在同一套基准上，
    /// 就会出现"栏杆飘在海面上""人物陷进地板"这类问题（本项目第一版犯过）。
    /// 所以把这些基准值放在 Core，布景、人物、校验程序都读同一份数字。
    /// </summary>
    public static class SceneMetrics
    {
        /// <summary>木栈道顶面：所有站在码头上的人物，脚底都落在这条线上。</summary>
        public const float DeckY = -300f;

        /// <summary>海平线。</summary>
        public const float SeaLineY = -140f;

        /// <summary>三章共用那艘旧木船的中心（船身随滑块起伏，这是基准位）。</summary>
        public const float BoatX = 280f;
        public const float BoatY = -120f;
        public const float BoatWidth = 700f;
        public const float BoatHeight = 300f;

        /// <summary>码头入口 / 远处小镇方向。</summary>
        public const float EntranceX = -800f;

        /// <summary>人物脚底允许的偏差：最多陷进栈道 20，最多悬空 8。</summary>
        public const float FootToleranceBelow = 20f;
        public const float FootToleranceAbove = 8f;
    }

    /// <summary>一个站在码头上的角色。存在这里，是为了让"身高"只有一个来源：
    /// 布景按它建人，校验程序按它核对脚底有没有踩在栈道上。</summary>
    public class GroundActor
    {
        public string Track;
        public float Width;
        public float Height;

        public GroundActor(string track, float width, float height)
        {
            Track = track;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// 一章的全部静态数据：布景色、滑块藏身处、时间轨迹、线索、文案。
    /// 改剧情、调节奏都只动这个文件，流程与视图代码不需要改。
    ///
    /// 坐标约定（画布参考分辨率 1920x1080，原点在画布中心）：
    ///   海平线 y ≈ -140，码头木栈道顶面 y ≈ -300（人物脚底站在这里），
    ///   木船中心 ≈ (280, -120)，码头入口（远处小镇方向）在屏幕左侧 x ≈ -800。
    ///
    /// 时间方向：**1.00 = 现在，0.00 = 这段记忆最早的过去**，一律是真正的倒流——
    /// 滑块向左拖，人物沿原路退回、动作被一件件撤销。
    /// </summary>
    public class ChapterData
    {
        public int Index;
        public string ChapterLabel;
        public string Title;

        /// <summary>本章借时权重，三章之和为 100。</summary>
        public float DebtWeight;

        /// <summary>布景风格："dusk"（黄昏·亲情）/"dawn"（清晨·友情）/"night"（夜晚·爱情）。</summary>
        public string BackdropStyle;

        public Rgba SkyTone = new Rgba(0.94f, 0.86f, 0.72f, 1f);
        public Rgba SeaTone = new Rgba(0.45f, 0.52f, 0.58f, 1f);
        public Rgba DockTone = new Rgba(0.42f, 0.33f, 0.26f, 1f);
        public Rgba WoodTone = new Rgba(0.62f, 0.45f, 0.30f, 1f);

        /// <summary>开场旁白（短句，逐条淡入）。</summary>
        public string[] IntroLines;

        /// <summary>章节过渡句，进入下一章前显示。</summary>
        public string[] TransitionLines;

        /// <summary>把时间停在当前时的反馈（点了"把现在留住"或者一直不深入时的温柔收束）。</summary>
        public string HoldText;

        /// <summary>深入过去、真相浮出时的反馈。</summary>
        public string DeepenText;

        // ---- 世界内滑块：藏在哪里、多长、什么造型 ----

        /// <summary>滑块底座造型："railing"（栏杆里的黄铜槽）/"ring"（系船桩旧铜环）/"brass"（工作台黄铜轨道）。</summary>
        public string SliderStyle;

        /// <summary>滑轨中心位置（画布坐标）。</summary>
        public Vec2 RailCenter;

        /// <summary>滑轨全长与粗细。<see cref="RailWidth"/> 决定拖动到 0—1 的映射区间。</summary>
        public float RailWidth = 420f;

        /// <summary>滑轨（及底座）的粗细。布景按它画，校验程序按它核对底座有没有落到栈道上。</summary>
        public float RailThickness = 26f;

        /// <summary>
        /// 底座立柱的长度 = 轨道粗细 × 这个系数。
        /// 它必须和 DockArt.BuildSliderRail 里三种样式的立柱长度一致（栏杆 4.5 / 铜环 4.4 / 黄铜轨道 2.0）。
        /// </summary>
        public float RailPostDropFactor = 4.5f;

        /// <summary>本章站在栈道上的角色。用于核对"脚底踩在栈道上"。</summary>
        public List<GroundActor> GroundActors = new List<GroundActor>();

        public void Actor(string track, float width, float height)
        {
            GroundActors.Add(new GroundActor(track, width, height));
        }

        public GroundActor FindActor(string track)
        {
            for (int i = 0; i < GroundActors.Count; i++)
            {
                if (GroundActors[i].Track == track)
                {
                    return GroundActors[i];
                }
            }

            return null;
        }

        /// <summary>发现滑块用的悬停区域高度。</summary>
        public float RailHitHeight = 90f;

        /// <summary>滑块初始（也是"现在"）在右端。</summary>
        public Vec2 SliderKnobSize = new Vec2(54f, 46f);

        public ChapterTimeline Timeline = new ChapterTimeline();

        public TimelineTrack Track(string name)
        {
            var track = new TimelineTrack(name);
            Timeline.Tracks.Add(track);
            return track;
        }

        public ChapterTimeline BuildTimeline()
        {
            Timeline.Index = Index;
            Timeline.ChapterLabel = ChapterLabel;
            Timeline.Title = Title;
            Timeline.DebtWeight = DebtWeight;
            return Timeline.Bake();
        }
    }

    /// <summary>
    /// 《错过了那一秒》三章的唯一数据源。
    /// 文案总量按策划案控制在 1500 字以内，单条不超过 50 字。
    /// </summary>
    public static class StoryBook
    {
        public const int ChapterCount = 3;

        // ---- 序幕 ----
        public const string GameTitle = "错过了那一秒";
        public const string TitleLine = "有些事，晚一秒就再也来不及了。";
        public const string TitleHint = "找到那道刻痕。拖动时间，看看当时。";
        public const string StartButtonLabel = "开始";

        // ---- 唯一结局 ----
        public const string HoldButtonLabel = "把现在留住";
        public const string ContinueButtonLabel = "继续";

        /// <summary>结局前的三段闪回，依次出现。</summary>
        public static readonly string[] EndingFlashbacks =
        {
            "母亲刻完最后一笔，笑了。",
            "朋友在入口站了很久，才转身。",
            "纸船上，有一个小小的笑脸。"
        };

        public static readonly string[] EndingLines =
        {
            "你借走了那么多秒。",
            "现在，把它们还回去。"
        };

        /// <summary>结局木船特写上的刻字。</summary>
        public const string EndingCarving = "往有光的地方去。";

        /// <summary>最后一行。</summary>
        public const string FinalLine = "他们都知道。";

        private static ChapterData[] _chapters;

        public static ChapterData Get(int index)
        {
            if (_chapters == null)
            {
                _chapters = new[] { Chapter1(), Chapter2(), Chapter3() };

                // 关键帧在源码里是按"从今到昔"的阅读顺序写的（1.00 → 0.00），
                // 采样需要升序，所以这里统一排一次序。加载期做，运行期不再排序。
                for (int i = 0; i < _chapters.Length; i++)
                {
                    _chapters[i].BuildTimeline();
                }
            }

            return _chapters[index];
        }

        // ================================================================
        // 第一章 · 亲情《船底的罗盘》—— 少年离开家乡前的黄昏码头
        // ================================================================
        private static ChapterData Chapter1()
        {
            var c = new ChapterData
            {
                Index = 0,
                ChapterLabel = "第一章 · 亲情",
                Title = "船底的罗盘",
                DebtWeight = 30f,
                BackdropStyle = "dusk",
                SkyTone = new Rgba(0.95f, 0.78f, 0.55f, 1f),
                SeaTone = new Rgba(0.44f, 0.47f, 0.55f, 1f),
                DockTone = new Rgba(0.40f, 0.31f, 0.24f, 1f),
                WoodTone = new Rgba(0.64f, 0.45f, 0.29f, 1f),
                IntroLines = new[]
                {
                    "那年，海风很轻。",
                    "少年把行李搬上船，以为远方比故乡更重要。",
                    "母亲没有喊他回来。"
                },
                TransitionLines = new[] { "他带走了船，却没有带走那一刻。" },
                HoldText = "刻痕仍模糊着。黄昏的光，替她留住了笑容。",
                DeepenText = "罗盘浮出船底。母亲的身影开始变淡。",
                SliderStyle = "railing",
                RailCenter = new Vec2(-520f, -183f)
            };

            // 站在栈道上的角色：身高/身宽只有这一处来源，布景与校验程序都读它。
            c.Actor("Mother", 170f, 330f);
            c.Actor("Son", 150f, 300f);

            // 母亲：远处修船 → 停下手 → 望向少年 → 走到船边蹲下 → 藏罗盘 → 拿刻刀 → 刻完微笑。
            // 越往过去，她的身影越淡（策划案：深入过去时母亲开始变淡）。
            // 身高 330，脚底必须落在木栈道 y=-300 上：根节点 y = -300 + 330×缩放/2 - 下沉量。
            c.Track("Mother")
                .Add(TimelineTrack.Key(1.00f, -700f, -135f, 1f))
                .Add(TimelineTrack.Key(0.82f, -690f, -135f, 1f).WithRotation(-4f))
                .Add(TimelineTrack.Key(0.70f, -560f, -135f, 1f))
                .Add(TimelineTrack.Key(0.63f, -430f, -135f, 1f))
                .Add(TimelineTrack.Key(0.52f, -60f, -138f, 0.98f).WithScale(0.98f))
                .Add(TimelineTrack.Key(0.43f, 190f, -155f, 0.92f).WithScale(0.94f))
                .Add(TimelineTrack.Key(0.32f, 300f, -164f, 0.86f).WithScale(0.90f).WithRotation(-6f))
                .Add(TimelineTrack.Key(0.24f, 318f, -164f, 0.76f).WithScale(0.90f).WithRotation(-8f))
                .Add(TimelineTrack.Key(0.10f, 322f, -164f, 0.66f).WithScale(0.90f).WithRotation(-8f))
                .Add(TimelineTrack.Key(0.03f, 322f, -154f, 0.60f).WithScale(0.92f).WithRotation(-3f))
                .Add(TimelineTrack.Key(0.00f, 322f, -154f, 0.58f).WithScale(0.92f));

            // 少年：背对母亲整理行李，全程没有回头。他只在原地小幅移动，衬托出母亲的那一段路。
            c.Track("Son")
                .Add(TimelineTrack.Key(1.00f, 512f, -150f, 1f).WithRotation(0f))
                .Add(TimelineTrack.Key(0.63f, 508f, -150f, 1f))
                .Add(TimelineTrack.Key(0.43f, 505f, -150f, 1f).WithRotation(1f))
                .Add(TimelineTrack.Key(0.00f, 500f, -158f, 0.95f).WithScale(0.95f).WithRotation(2f));

            // 行李：越往过去，行李越少，说明他还远没有准备离开。箱笼下沿贴着栈道。
            c.Track("Luggage")
                .Add(TimelineTrack.Key(1.00f, 560f, -240f, 1f).WithScale(1f))
                .Add(TimelineTrack.Key(0.63f, 556f, -244f, 1f).WithScale(0.94f))
                .Add(TimelineTrack.Key(0.43f, 552f, -253f, 0.9f).WithScale(0.78f))
                .Add(TimelineTrack.Key(0.00f, 548f, -267f, 0.75f).WithScale(0.55f));

            AddBoatTide(c, 0f);

            // 罗盘：藏在船底 → 母亲手里 → 放进船底暗处。
            c.Track("Compass")
                .Add(TimelineTrack.Key(1.00f, 330f, -232f, 0f))
                .Add(TimelineTrack.Key(0.56f, 320f, -226f, 0f))
                .Add(TimelineTrack.Key(0.50f, 244f, -178f, 1f).WithRotation(-12f))
                .Add(TimelineTrack.Key(0.43f, 268f, -196f, 1f).WithRotation(6f))
                .Add(TimelineTrack.Key(0.34f, 322f, -230f, 1f).WithRotation(0f))
                .Add(TimelineTrack.Key(0.00f, 322f, -230f, 1f));

            // 船舷刻字：从几乎看不见到"往有光的地方去"完整显现。
            c.Track("Carving")
                .Add(TimelineTrack.Key(1.00f, 322f, -150f, 0.05f))
                .Add(TimelineTrack.Key(0.28f, 322f, -150f, 0.10f))
                .Add(TimelineTrack.Key(0.24f, 322f, -150f, 0.30f))
                .Add(TimelineTrack.Key(0.14f, 322f, -150f, 0.65f))
                .Add(TimelineTrack.Key(0.05f, 322f, -150f, 0.95f))
                .Add(TimelineTrack.Key(0.00f, 322f, -150f, 1f));

            // 船底那块不自然的阴影：现在压得很实（88%），时间往回走就退开，
            // 罗盘正是从它底下浮出来的——这是第一章最重要的一个视觉伏笔。
            var hullShadow = c.Track("HullShadow");
            hullShadow.Ease = true;
            hullShadow.Add(TimelineTrack.Key(1.00f, 286f, -236f, 0.88f))
                .Add(TimelineTrack.Key(0.63f, 285f, -234f, 0.82f))
                .Add(TimelineTrack.Key(0.45f, 283f, -240f, 0.46f))
                .Add(TimelineTrack.Key(0.34f, 282f, -238f, 0.20f))
                .Add(TimelineTrack.Key(0.10f, 280f, -235f, 0.08f))
                .Add(TimelineTrack.Key(0.00f, 279f, -233f, 0.05f));

            // 环境色：越深越暖（黄昏的光替她留住笑容）。
            var dusk = c.Track("EnvTint");
            dusk.Ease = true;
            dusk.Add(KeyColor(1.00f, 0.36f, 0.44f, 0.58f, 0.10f))
                .Add(KeyColor(0.50f, 0.74f, 0.55f, 0.34f, 0.16f))
                .Add(KeyColor(0.00f, 0.98f, 0.70f, 0.36f, 0.24f));

            // 船边暖光：越深越亮，和母亲变淡形成对照。（颜色由美术层给，轨迹只驱动透明度）
            var glow = c.Track("Glow");
            glow.Ease = true;
            glow.Add(TimelineTrack.Key(1.00f, 330f, -150f, 0.05f))
                .Add(TimelineTrack.Key(0.45f, 328f, -152f, 0.18f))
                .Add(TimelineTrack.Key(0.00f, 326f, -154f, 0.32f));

            c.Timeline.Cues = new List<TimelineCue>
            {
                new TimelineCue("c1_hide", 0.63f, "她把什么藏进了船底？"),
                new TimelineCue("c1_carve", 0.24f, "再靠近一点。她刻下的是什么？"),
                new TimelineCue("c1_words", 0.05f, "往有光的地方去。")
            };
            c.Timeline.FinalCueId = "c1_words";
            return c;
        }

        // ================================================================
        // 第二章 · 友情《没有赴约的人》—— 多年后的清晨码头
        // ================================================================
        private static ChapterData Chapter2()
        {
            var c = new ChapterData
            {
                Index = 1,
                ChapterLabel = "第二章 · 友情",
                Title = "没有赴约的人",
                DebtWeight = 30f,
                BackdropStyle = "dawn",
                SkyTone = new Rgba(0.78f, 0.85f, 0.90f, 1f),
                SeaTone = new Rgba(0.50f, 0.60f, 0.65f, 1f),
                DockTone = new Rgba(0.44f, 0.39f, 0.33f, 1f),
                WoodTone = new Rgba(0.66f, 0.52f, 0.36f, 1f),
                IntroLines = new[]
                {
                    "后来，他有了自己的生活。",
                    "青年独自坐在船头，等一个约好远行的朋友。",
                    "潮水退了又涨，朋友始终没有来。"
                },
                TransitionLines = new[] { "后来，他学会了不再等人。", "只是有些等待，从未真正结束。" },
                HoldText = "入口空了，只剩风吹动那张地图。",
                DeepenText = "海图露出完整的一角，青年的轮廓开始褪色。",
                SliderStyle = "ring",
                RailCenter = new Vec2(-236f, -200f)
            };

            c.RailPostDropFactor = 4.4f; // 系船桩：桩身比栏杆长一点
            c.Actor("Youth", 170f, 320f);
            c.Actor("Companion", 150f, 280f);
            c.Actor("Friend", 170f, 330f);

            // 青年：一直坐在船头等，全程几乎没有动过。越往过去他越清晰。
            // 身高 320，坐着时整体压低一档；脚底仍落在栈道上。
            c.Track("Youth")
                .Add(TimelineTrack.Key(1.00f, 520f, -161f, 1f).WithScale(0.92f).WithRotation(6f))
                .Add(TimelineTrack.Key(0.64f, 520f, -161f, 1f).WithScale(0.92f).WithRotation(5f))
                .Add(TimelineTrack.Key(0.00f, 518f, -161f, 0.96f).WithScale(0.92f).WithRotation(3f));

            // 青年身边那个模糊的人影：朋友停下来的真正原因。
            // 现在只勉强看得出"有个人"，越往过去越清楚——那才是他停步的那一秒。
            var companion = c.Track("Companion");
            companion.Ease = true;
            companion.Add(TimelineTrack.Key(1.00f, 396f, -174f, 0.18f).WithScale(0.9f))
                .Add(TimelineTrack.Key(0.64f, 396f, -174f, 0.42f).WithScale(0.9f))
                .Add(TimelineTrack.Key(0.42f, 392f, -174f, 0.60f).WithScale(0.9f))
                .Add(TimelineTrack.Key(0.00f, 388f, -174f, 0.50f).WithScale(0.9f));

            // 朋友：真正的倒流——从"已经走了"退回到"站在入口犹豫"。
            // 他做过的事按倒序被一件件撤销：转身离开 → 在远处站了很久 → 走到船边塞海图
            // → 退回停步处抬手又放下 → 慢慢退回入口 → 还没出现。
            c.Track("Friend")
                .Add(TimelineTrack.Key(1.00f, -620f, -135f, 0f).WithRotation(0f))
                .Add(TimelineTrack.Key(0.90f, -560f, -135f, 0.20f).WithRotation(-26f))
                .Add(TimelineTrack.Key(0.84f, -520f, -135f, 0.75f).WithRotation(-18f))
                .Add(TimelineTrack.Key(0.76f, -486f, -135f, 1f).WithRotation(-6f))
                .Add(TimelineTrack.Key(0.66f, -330f, -135f, 1f).WithRotation(0f))
                .Add(TimelineTrack.Key(0.58f, 150f, -148f, 1f).WithScale(0.96f).WithRotation(8f))
                .Add(TimelineTrack.Key(0.50f, 196f, -153f, 1f).WithScale(0.94f).WithRotation(14f))
                .Add(TimelineTrack.Key(0.42f, -96f, -135f, 1f).WithRotation(-2f))
                .Add(TimelineTrack.Key(0.34f, -180f, -135f, 1f).WithRotation(0f))
                .Add(TimelineTrack.Key(0.22f, -380f, -135f, 1f).WithRotation(0f))
                .Add(TimelineTrack.Key(0.12f, -560f, -135f, 1f).WithRotation(0f))
                .Add(TimelineTrack.Key(0.05f, -706f, -135f, 1f).WithRotation(0f))
                .Add(TimelineTrack.Key(0.00f, -736f, -135f, 1f).WithRotation(0f));

            // 海图：现在只从船缝里露出一角；倒回去，它从船缝回到朋友手里、再攥紧。
            c.Track("Chart")
                .Add(TimelineTrack.Key(1.00f, 250f, -196f, 0.55f).WithRotation(-4f).WithScale(0.8f))
                .Add(TimelineTrack.Key(0.58f, 236f, -192f, 1f).WithRotation(0f).WithScale(0.9f))
                .Add(TimelineTrack.Key(0.50f, 210f, -184f, 1f).WithRotation(-16f).WithScale(1f))
                .Add(TimelineTrack.Key(0.42f, -70f, -150f, 1f).WithRotation(-8f).WithScale(0.92f))
                .Add(TimelineTrack.Key(0.22f, -360f, -146f, 1f).WithRotation(-4f).WithScale(0.95f))
                .Add(TimelineTrack.Key(0.12f, -548f, -148f, 1f).WithRotation(-2f).WithScale(0.98f))
                .Add(TimelineTrack.Key(0.00f, -716f, -150f, 1f).WithRotation(0f).WithScale(1f));

            AddBoatTide(c, 0.5f);

            var dawn = c.Track("EnvTint");
            dawn.Ease = true;
            dawn.Add(KeyColor(1.00f, 0.44f, 0.56f, 0.68f, 0.10f))
                .Add(KeyColor(0.50f, 0.82f, 0.78f, 0.62f, 0.14f))
                .Add(KeyColor(0.00f, 0.92f, 0.86f, 0.70f, 0.20f));

            var glow = c.Track("Glow");
            glow.Ease = true;
            glow.Add(TimelineTrack.Key(1.00f, 300f, -170f, 0.04f))
                .Add(TimelineTrack.Key(0.40f, 298f, -172f, 0.15f))
                .Add(TimelineTrack.Key(0.00f, 296f, -174f, 0.24f));

            c.Timeline.Cues = new List<TimelineCue>
            {
                new TimelineCue("c2_visited", 0.88f, "有人来过这里。"),
                new TimelineCue("c2_why", 0.42f, "再看一眼。他为什么没有走近？"),
                new TimelineCue("c2_map", 0.30f, "海图是他塞进船缝的。")
            };
            c.Timeline.FinalCueId = "c2_map";
            return c;
        }

        // ================================================================
        // 第三章 · 爱情《没有寄出的信》—— 多年后的夜晚码头
        // ================================================================
        private static ChapterData Chapter3()
        {
            var c = new ChapterData
            {
                Index = 2,
                ChapterLabel = "第三章 · 爱情",
                Title = "没有寄出的信",
                DebtWeight = 40f,
                BackdropStyle = "night",
                SkyTone = new Rgba(0.14f, 0.17f, 0.26f, 1f),
                SeaTone = new Rgba(0.16f, 0.22f, 0.31f, 1f),
                DockTone = new Rgba(0.26f, 0.24f, 0.24f, 1f),
                WoodTone = new Rgba(0.48f, 0.36f, 0.26f, 1f),
                IntroLines = new[]
                {
                    "再后来，他回到了海边。",
                    "那个曾经追上码头的女孩，也有一句话没有说完。",
                    "她把信写好，却一直没有交出去。"
                },
                TransitionLines = new[] { "他把船修好，海还是要出的。" },
                HoldText = "红线仍在风中摆动，女孩的脸消失在夜色里。",
                DeepenText = "信上的字逐渐清晰，整个码头开始褪色。",
                SliderStyle = "brass",
                RailCenter = new Vec2(-150f, -244f)
            };

            c.RailPostDropFactor = 2.0f; // 工作台：底座矮，黄铜轨道直接架在台面上
            c.Actor("Man", 170f, 330f);
            c.Actor("Companion", 150f, 280f);
            c.Actor("Woman", 160f, 320f);

            // 男人：俯身修船，工作灯只照亮木板。越往过去他越淡，视线始终在船上。
            // 身高 330，俯身时压低一档，脚底仍踩在栈道上。
            c.Track("Man")
                .Add(TimelineTrack.Key(1.00f, 214f, -162f, 1f).WithScale(0.9f).WithRotation(10f))
                .Add(TimelineTrack.Key(0.64f, 214f, -162f, 0.98f).WithScale(0.9f).WithRotation(10f))
                .Add(TimelineTrack.Key(0.00f, 212f, -165f, 0.88f).WithScale(0.88f).WithRotation(9f));

            // 男人身边那个"似乎有"的另一个人。女孩因此停住脚步。
            var companion = c.Track("Companion");
            companion.Ease = true;
            companion.Add(TimelineTrack.Key(1.00f, 88f, -177f, 0.10f).WithScale(0.88f))
                .Add(TimelineTrack.Key(0.45f, 88f, -177f, 0.42f).WithScale(0.88f))
                .Add(TimelineTrack.Key(0.00f, 84f, -177f, 0.34f).WithScale(0.88f));

            // 女孩：从码头入口出现 → 走近 → 放慢 → 停住攥紧信 → 折成纸船 → 系红线 → 塞进夹层。
            c.Track("Woman")
                .Add(TimelineTrack.Key(1.00f, -760f, -140f, 0f).WithRotation(0f))
                .Add(TimelineTrack.Key(0.88f, -700f, -140f, 0.30f).WithRotation(0f))
                .Add(TimelineTrack.Key(0.82f, -664f, -140f, 1f).WithRotation(0f))
                .Add(TimelineTrack.Key(0.72f, -520f, -140f, 1f).WithRotation(-2f))
                .Add(TimelineTrack.Key(0.64f, -360f, -140f, 1f).WithRotation(-3f))
                .Add(TimelineTrack.Key(0.52f, -160f, -140f, 1f).WithRotation(-4f))
                .Add(TimelineTrack.Key(0.45f, -96f, -140f, 1f).WithRotation(-5f))
                .Add(TimelineTrack.Key(0.34f, -70f, -156f, 1f).WithScale(0.95f).WithRotation(-8f))
                .Add(TimelineTrack.Key(0.27f, 12f, -163f, 1f).WithScale(0.93f).WithRotation(-10f))
                .Add(TimelineTrack.Key(0.14f, 112f, -168f, 1f).WithScale(0.91f).WithRotation(-12f))
                .Add(TimelineTrack.Key(0.05f, 120f, -168f, 1f).WithScale(0.91f).WithRotation(-12f))
                .Add(TimelineTrack.Key(0.00f, 120f, -168f, 1f).WithScale(0.91f).WithRotation(-12f));

            AddBoatTide(c, 1f);

            // 折起来的纸船（系着红线）：从她手里到木船夹层。
            c.Track("Letter")
                .Add(TimelineTrack.Key(1.00f, 150f, -236f, 0f))
                .Add(TimelineTrack.Key(0.30f, 140f, -232f, 0f))
                .Add(TimelineTrack.Key(0.27f, 30f, -200f, 1f).WithRotation(-10f))
                .Add(TimelineTrack.Key(0.14f, 132f, -234f, 1f).WithRotation(4f))
                .Add(TimelineTrack.Key(0.00f, 132f, -234f, 1f).WithRotation(2f));

            // 展开的信纸：只有走到 0.05 以内才看得见"我一直都在。"和那个小笑脸。
            c.Track("LetterOpen")
                .Add(TimelineTrack.Key(1.00f, 150f, -236f, 0f).WithScale(0.9f))
                .Add(TimelineTrack.Key(0.06f, 150f, -236f, 0f).WithScale(0.9f))
                .Add(TimelineTrack.Key(0.04f, 148f, -238f, 0.85f).WithScale(1f))
                .Add(TimelineTrack.Key(0.00f, 148f, -238f, 1f).WithScale(1f));

            // 红线：现在是夹层里露出的那一截；倒回去，它绕在纸船上、再回到她手里。
            c.Track("RedThread")
                .Add(TimelineTrack.Key(1.00f, 186f, -230f, 0.7f).WithRotation(6f))
                .Add(TimelineTrack.Key(0.30f, 176f, -228f, 0.6f).WithRotation(4f))
                .Add(TimelineTrack.Key(0.27f, 44f, -196f, 0.9f).WithRotation(-8f))
                .Add(TimelineTrack.Key(0.14f, 148f, -230f, 1f).WithRotation(0f))
                .Add(TimelineTrack.Key(0.00f, 148f, -230f, 0.85f).WithRotation(0f));

            // 工作灯的光池：只落在木板上。越往过去越亮、越聚焦——
            // 像是记忆慢慢对准了那处夹层。
            var workLight = c.Track("WorkLight");
            workLight.Ease = true;
            workLight.Add(TimelineTrack.Key(1.00f, 250f, -206f, 0.50f))
                .Add(TimelineTrack.Key(0.45f, 248f, -208f, 0.68f))
                .Add(TimelineTrack.Key(0.00f, 246f, -210f, 0.85f));

            var night = c.Track("EnvTint");
            night.Ease = true;
            night.Add(KeyColor(1.00f, 0.20f, 0.26f, 0.38f, 0.16f))
                .Add(KeyColor(0.50f, 0.40f, 0.36f, 0.46f, 0.20f))
                .Add(KeyColor(0.00f, 0.62f, 0.46f, 0.42f, 0.28f));

            var glow = c.Track("Glow");
            glow.Ease = true;
            glow.Add(TimelineTrack.Key(1.00f, 226f, -206f, 0.10f))
                .Add(TimelineTrack.Key(0.45f, 222f, -208f, 0.20f))
                .Add(TimelineTrack.Key(0.00f, 220f, -210f, 0.30f));

            c.Timeline.Cues = new List<TimelineCue>
            {
                new TimelineCue("c3_hand", 0.82f, "她手里握着什么？"),
                new TimelineCue("c3_last", 0.45f, "最后一秒。看看她想说什么。"),
                new TimelineCue("c3_words", 0.06f, "我一直都在。")
            };
            c.Timeline.FinalCueId = "c3_words";
            return c;
        }

        /// <summary>
        /// 三章共用的旧木船：随着滑块轻轻起伏，像那一天的潮水在退涨。
        ///
        /// 加上它的意义是让"拖动时间"改变整个场景，而不只是几个人物在动——
        /// 船是画面的锚，它一浮沉，整幅记忆就跟着活了。
        /// 幅度刻意压得很小（±7 像素级别），保证船底罗盘、船舷刻字这些绝对定位的关键道具
        /// 不会和船身错开。phase 让三章的潮水不是同一个节拍。
        /// </summary>
        private static void AddBoatTide(ChapterData c, float phase)
        {
            float p = phase;
            c.Track("Boat")
                .Add(TimelineTrack.Key(1.00f, 286f, -118f - p * 5f, 1f).WithRotation(0.8f + p * 0.5f))
                .Add(TimelineTrack.Key(0.72f, 283f, -126f + p * 3f, 1f).WithRotation(-0.6f - p * 0.4f))
                .Add(TimelineTrack.Key(0.44f, 281f, -113f - p * 4f, 1f).WithRotation(1.1f + p * 0.3f))
                .Add(TimelineTrack.Key(0.18f, 279f, -124f + p * 4f, 1f).WithRotation(-0.9f))
                .Add(TimelineTrack.Key(0.00f, 277f, -115f - p * 3f, 1f).WithRotation(0.4f + p * 0.4f));
        }

        private static KeyFrame KeyColor(float time, float r, float g, float b, float a)
        {
            return new KeyFrame
            {
                Time = time,
                Position = Vec2.Zero,
                Rotation = 0f,
                Scale = 1f,
                Color = new Rgba(r, g, b, a)
            };
        }
    }
}
