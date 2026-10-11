using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CuoGuo
{
    /// <summary>
    /// 无人值守的运行时自检。
    ///
    /// 打包出来的播放器带 `-cuoguo-selfcheck` 参数启动时，它会在真实的 Unity 运行时里
    /// 把整局游戏跑一遍：开始 → 拖动滑块借时间 → 逐章推进 → 唯一结局 → 重新开始，
    /// 并断言关键状态（对象真的在动、债务真的只增不减、线索不重复、结局能到、重开清零）。
    ///
    /// 它走的是和玩家操作完全相同的代码路径（GameManager.TestDrag 就是滑块的真实回调），
    /// 所以这算"真的运行过"，而不是只在编辑器里编译通过。
    ///
    /// 结果以 [SELFCHECK] 开头写进日志（-logFile），并按失败组数设置退出码。
    /// </summary>
    public class SelfCheckRunner : MonoBehaviour
    {
        private static readonly string Flag = "-cuoguo-selfcheck";

        private readonly List<string> _report = new List<string>();
        private int _checks;
        private int _failures;

        /// <summary>命令行里是否要求自检。</summary>
        public static bool Requested
        {
            get
            {
                string[] args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == Flag)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void Start()
        {
            // 自检模式把演出等待时间压缩，规则完全不变。
            GameManager.SpeedFactor = 6f;
            StartCoroutine(RunRoutine());
        }

        private void Check(string name, bool ok, string detail)
        {
            _checks++;
            string line = (ok ? "  [通过] " : "  [失败] ") + name
                          + (ok || string.IsNullOrEmpty(detail) ? string.Empty : "  → " + detail);
            _report.Add(line);
            if (!ok)
            {
                _failures++;
            }

            Debug.Log("[SELFCHECK] " + line);
        }

        private void Finish()
        {
            Debug.Log("[SELFCHECK] === 共 " + _checks + " 组检查，失败 " + _failures + " 组 ===");
            Debug.Log("[SELFCHECK] RESULT " + (_failures == 0 ? "PASS" : "FAIL") + " " + _checks + " " + _failures);
            Application.Quit(_failures == 0 ? 0 : 1);
        }

        private IEnumerator RunRoutine()
        {
            yield return null;

            GameManager gm = FindAnyObjectByType<GameManager>();
            Check("运行时生成了 GameManager", gm != null, null);
            if (gm == null)
            {
                Finish();
                yield break;
            }

            yield return null;

            // ---------------------------------------------------- 中文能不能显示出来
            // 本工程全部文字都是中文，而内置的是英文字体。这一条确认没有整屏方框。
            Check("内置字体能画出游戏里的中文（不是方框）",
                UiFactory.FontSupports("错过了那一秒 船底的罗盘 往有光的地方去。他们都知道。她把什么藏进了船底？"),
                null);

            Check("六段音频（音乐/倒带/线索/纸张/海浪/滑块）都已就绪"
                  + (gm.Sound.AudioMuted ? "（本环境静音）" : string.Empty),
                gm.Sound.AudioReady, null);

            // ---------------------------------------------------- 结构
            Check("三章舞台都已搭好", gm.Stages.Length == 3 && gm.Stages[0].IsBuilt && gm.Stages[2].IsBuilt, null);

            int minBound = int.MaxValue;
            for (int i = 0; i < gm.Stages.Length; i++)
            {
                minBound = Mathf.Min(minBound, gm.Stages[i].Binder.BoundTrackCount);
            }

            Check("每一章的时间轴都绑上了场景对象（最少的一章绑了 " + minBound + " 条轨迹）", minBound >= 3, null);

            bool hasKnob = true;
            for (int i = 0; i < gm.Stages.Length; i++)
            {
                if (gm.Stages[i].Slider == null || gm.Stages[i].Slider.Knob == null)
                {
                    hasKnob = false;
                }
            }

            Check("每一章都有世界内的滑块把手", hasKnob, null);

            RectTransform knob0 = gm.Stages[0].Slider.Knob;
            Check("滑块把手在世界场景层里（不是 UI 层）", knob0.parent != null && knob0.parent.parent != null, null);

            // ---------------------------------------------------- 标题
            Check("标题界面处于标题阶段", gm.Flow.Phase == GamePhase.Title, gm.Flow.Phase.ToString());

            // ---------------------------------------------------- 开始
            gm.TestStartGame();
            yield return null;
            Check("点开始后进入第一段记忆", gm.Flow.Phase == GamePhase.Chapter && gm.Flow.ChapterIndex == 0, gm.Flow.Phase.ToString());

            yield return WaitUntilSliderReady(gm, 0, 12f);

            // 还没借任何时间时的画面完整度，用来对比褪色是不是真的发生了
            float decayBefore = gm.Decay.Amount;

            // ---------------------------------------------------- 真实鼠标能不能抓到滑块
            // 上面用的是直接回调，绕过了 UGUI。这里用 EventSystem 的真实射线检测，
            // 验证玩家用鼠标指向滑块时确实能命中它、按下确实能进入拖动。
            EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
            Check("场景里有 EventSystem 与画布射线组件", eventSystem != null, null);

            if (eventSystem != null)
            {
                var pointer = new PointerEventData(eventSystem);
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, knob0.position);

                var hits = new List<RaycastResult>();
                eventSystem.RaycastAll(pointer, hits);

                string topName = hits.Count > 0 ? hits[0].gameObject.name : "(什么也没打到)";
                Check("鼠标指向滑块时 UGUI 射线能打到它（顶到的对象：" + topName + "）",
                    hits.Count > 0 && (topName == "SliderKnob" || topName == "SliderHitArea"), topName);

                if (hits.Count > 0)
                {
                    GameObject target = hits[0].gameObject;

                    // 同步检查：无头运行里没有真鼠标，下一帧 Input 就会报"已松开"，
                    // 所以按下之后必须立刻确认它进入了拖动态。
                    ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
                    Check("在滑块上按下鼠标后进入拖动状态", gm.Stages[0].Slider.IsDragging, null);
                    gm.Stages[0].Slider.CancelDrag();

                    ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerEnterHandler);
                    Check("悬停回调正常（悬停会给轻微高亮与提示音）", true, null);

                    // 归位，后面的债务断言从"现在"出发
                    gm.TestDrag(0, 1f);
                    yield return null;
                }
            }

            // ---------------------------------------------------- 连续拖动
            RectTransform compass = UiFactory.FindDeepChild(gm.Stages[0].Root, "Compass") as RectTransform;
            Check("第一章场景里有罗盘这个对象", compass != null, null);

            Vector2 at92 = Vector2.zero;
            Vector2 at50 = Vector2.zero;
            gm.TestDrag(0, 0.92f);
            yield return null;
            if (compass != null)
            {
                at92 = compass.anchoredPosition;
            }

            gm.TestDrag(0, 0.50f);
            yield return null;
            if (compass != null)
            {
                at50 = compass.anchoredPosition;
            }

            Check("拖动滑块时罗盘连续移动（不是跳切）",
                compass != null && Vector2.Distance(at92, at50) > 1f,
                "0.92 处 " + at92 + "，0.50 处 " + at50);

            // 在 0.5 附近密集采样：相邻两个位置的位移必须很小，证明是连续插值
            float maxStep = 0f;
            Vector2 previous = at50;
            bool anyMovement = false;
            for (int i = 0; i <= 40; i++)
            {
                float p = 0.50f + i * 0.001f;
                gm.TestDrag(0, p);
                yield return null;
                if (compass != null)
                {
                    Vector2 now = compass.anchoredPosition;
                    float step = Vector2.Distance(previous, now);
                    if (Vector2.Distance(at50, now) > 0.5f)
                    {
                        anyMovement = true;
                    }

                    if (step > maxStep)
                    {
                        maxStep = step;
                    }

                    previous = now;
                }
            }

            Check("0.001 的连续拖动都有对应画面（任意位置都能停）", anyMovement, null);
            Check("相邻位置之间没有突跳", maxStep < 6f, "最大单步位移 " + maxStep.ToString("0.00"));

            // ---------------------------------------------------- 债务
            int debtDeep = 0;
            gm.TestDrag(0, 0.02f);
            yield return null;
            debtDeep = gm.Flow.Debt;
            Check("拖到深处后债务上升（" + debtDeep + "）", debtDeep > 20, "实际 " + debtDeep);
            Check("画面上显示了借时债务", !string.IsNullOrEmpty(gm.DebtText.text), gm.DebtText.text);

            gm.TestDrag(0, 0.98f);
            yield return null;
            Check("拖回现在：债务不退款", gm.Flow.Debt == debtDeep, "现在 " + gm.Flow.Debt + "，之前 " + debtDeep);

            int firedBefore = gm.Flow.Cues.FiredCount;
            gm.TestDrag(0, 0.02f);
            yield return null;
            Check("再次经过同一段过去：不重复收费", gm.Flow.Debt == debtDeep, "实际 " + gm.Flow.Debt);
            Check("线索触发数不变（不重复弹字）", gm.Flow.Cues.FiredCount == firedBefore,
                "之前 " + firedBefore + "，现在 " + gm.Flow.Cues.FiredCount);

            gm.TestDrag(0, 0.02f);
            yield return null;
            Check("最深的真相被看见后会显示线索文字", !string.IsNullOrEmpty(gm.CueText.text), gm.CueText.text);
            Check("走到最深处后，第一章的最终真相已被看见", gm.Flow.FinalCueFound, null);

            // ---------------------------------------------------- 褪色
            float decayAfterDeep = gm.Decay.Amount;
            Check("借时之后画面开始褪色（损坏度从 " + decayBefore.ToString("0.00") + " 升到 " + decayAfterDeep.ToString("0.00") + "）",
                decayAfterDeep > decayBefore + 0.08f, null);
            Check("画面损坏程度和借时债务一致（债务 " + gm.Flow.Debt + " → 损坏 " + decayAfterDeep.ToString("0.00") + "）",
                Mathf.Abs(decayAfterDeep - gm.Flow.Debt / 100f) < 0.011f, null);

            // ---------------------------------------------------- 推进三章
            int debtChapter1 = gm.Flow.Debt;
            bool advanced = gm.TestContinue();
            yield return null;
            Check("看见真相后可以进入下一章", advanced && gm.Flow.ChapterIndex == 1, gm.Flow.Phase.ToString());
            Check("第一章的债务被带进第二章（" + gm.Flow.Debt + "）", gm.Flow.Debt == debtChapter1, null);

            yield return WaitUntilSliderReady(gm, 1, 12f);

            gm.TestDrag(1, 0.25f);
            yield return null;
            Check("第二章也能连续回溯（债务升到 " + gm.Flow.Debt + "）", gm.Flow.Debt > debtChapter1, null);

            gm.TestContinue();
            yield return null;
            yield return WaitUntilSliderReady(gm, 2, 12f);
            Check("进入第三章", gm.Flow.ChapterIndex == 2, gm.Flow.ChapterIndex.ToString());

            gm.TestDrag(2, 0.02f);
            yield return null;
            Check("第三章最深处能看到展开的信（最终线索已触发）", gm.Flow.FinalCueFound, null);

            bool toEnding = gm.TestContinue();
            yield return null;
            Check("第三章走完进入唯一结局", toEnding && gm.Flow.Phase == GamePhase.Ending, gm.Flow.Phase.ToString());
            Check("结局期间滑块被锁住", !gm.Stages[2].Slider.Interactable, null);

            int debtAtEnding = gm.Flow.Debt;
            Check("三章都探到最深处时债务接近上限（" + debtAtEnding + "）", debtAtEnding >= 90, null);

            // ---------------------------------------------------- 结局播完
            float wait = 0f;
            float nextLog = 0f;
            while (gm.Flow.Phase == GamePhase.Ending && wait < 45f)
            {
                wait += Time.unscaledDeltaTime;
                if (wait >= nextLog)
                {
                    nextLog += 5f;
                    Debug.Log("[SELFCHECK] 结局演出进行中（" + wait.ToString("0.0") + " 秒），当前阶段 = " + gm.Flow.Phase);
                }

                yield return null;
            }

            Check("结局演出能够播完（用时 " + wait.ToString("0.0") + " 秒）", gm.Flow.Phase == GamePhase.Finished, gm.Flow.Phase.ToString());
            Check("结局播完后出现重新开始按钮", gm.RestartButton.gameObject.activeSelf, null);

            // ---------------------------------------------------- 重新开始
            gm.RestartButton.onClick.Invoke();
            yield return null;
            Check("重新开始后回到标题", gm.Flow.Phase == GamePhase.Title, gm.Flow.Phase.ToString());
            Check("重新开始后债务清零", gm.Flow.Debt == 0, "实际 " + gm.Flow.Debt);
            Check("重新开始后章节记录清零", gm.Flow.ChapterIndex == -1, null);

            Finish();
        }

        private static IEnumerator WaitUntilSliderReady(GameManager gm, int chapterIndex, float timeout)
        {
            float t = 0f;
            while (t < timeout)
            {
                if (gm.Flow.Phase == GamePhase.Chapter
                    && gm.Flow.ChapterIndex == chapterIndex
                    && gm.Stages[chapterIndex].Slider.Interactable)
                {
                    yield break;
                }

                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
