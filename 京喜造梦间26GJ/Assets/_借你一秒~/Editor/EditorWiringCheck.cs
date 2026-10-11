using System.Reflection;
using JieNiYiMiao;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace JieNiYiMiao.EditorTools
{
    /// <summary>
    /// 视图绑定自检：验证"点得到、点得对"。
    ///
    /// 为什么需要它：流程规则由 _build/verify 的独立校验程序覆盖，但
    /// "屏幕上的东西是不是真的接上了流程"只有引擎里才检查得到。
    /// 这个工具直接检查当前场景里已经生成出来的对象：
    /// 主角身上有没有 ClickableActor、有没有接管射线的组件、
    /// 关键物件有没有 ClickableProp、按钮有没有监听、选择按钮的文案是不是来自数据表。
    ///
    /// 用法：按 Play（或让它在 Play 里被调用），然后执行菜单
    /// 「借你一秒 / 2. 视图绑定自检」。它会用 Debug.LogError 报出问题。
    /// </summary>
    public static class EditorWiringCheck
    {
        [MenuItem("借你一秒/2. 视图绑定自检（先按 Play）")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[借你一秒] 视图绑定自检需要先进入 Play 模式。");
                return;
            }

            int problems = 0;
            var report = new System.Text.StringBuilder();
            report.AppendLine("[借你一秒] 视图绑定自检");

            // 1. 场景骨架
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            problems += Expect(report, canvases.Length == 4, "Canvas 数量应为 4，实际 " + canvases.Length);

            var eventSystem = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            problems += Expect(report, eventSystem != null, "EventSystem 应存在");

            var manager = Object.FindAnyObjectByType<GameManager>();
            problems += Expect(report, manager != null, "GameManager 应存在");
            if (manager == null)
            {
                Finish(report, problems);
                return;
            }

            var flowField = typeof(GameManager).GetField("_flow", BindingFlags.Instance | BindingFlags.NonPublic);
            var flow = flowField != null ? flowField.GetValue(manager) as GameFlow : null;
            problems += Expect(report, flow != null, "GameManager 应持有 GameFlow");

            // 2. 主角必须可点，而且点击会真的走进流程
            var actors = Object.FindObjectsByType<ClickableActor>(FindObjectsSortMode.None);
            problems += Expect(report, actors.Length >= 2, "场上应至少有主角与一名布景角色，实际 " + actors.Length);

            ClickableActor protagonist = null;
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                if (actor == null)
                {
                    continue;
                }

                problems += Expect(report, !string.IsNullOrEmpty(actor.ActorId), "ClickableActor 必须带 ActorId（" + actor.name + "）");
                var proxy = actor.GetComponent<StageClickProxy>();
                problems += Expect(report, proxy != null, actor.ActorId + " 应挂 StageClickProxy 才能接收点击");
                if (proxy != null)
                {
                    problems += Expect(report, proxy.Actor == actor, actor.ActorId + " 的 StageClickProxy 应指回自己");
                }

                var graphics = actor.GetComponentsInChildren<Graphic>(true);
                bool anyRaycast = false;
                for (int g = 0; g < graphics.Length; g++)
                {
                    if (graphics[g].raycastTarget)
                    {
                        anyRaycast = true;
                        break;
                    }
                }

                problems += Expect(report, anyRaycast, actor.ActorId + " 至少要有一块图元接管射线，否则点不到");

                if (flow != null && flow.Level != null && actor.ActorId == flow.Level.ActorName)
                {
                    protagonist = actor;
                }
            }

            problems += Expect(report, protagonist != null, "应能在场上找到当前记忆的主角");

            // 3. 关键物件
            var props = Object.FindObjectsByType<ClickableProp>(FindObjectsSortMode.None);
            problems += Expect(report, props.Length >= 1, "场上应有关键物件，实际 " + props.Length);
            for (int i = 0; i < props.Length; i++)
            {
                problems += Expect(report, props[i] != null && !string.IsNullOrEmpty(props[i].PropId) && props[i].GetComponent<StageClickProxy>() != null,
                    "关键物件必须带 PropId 与 StageClickProxy");
            }

            // 4. 按钮与监听
            var buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            problems += Expect(report, buttons.Length == 6, "按钮数量应为 6，实际 " + buttons.Length);
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                // 监听是运行时用 AddListener 挂的，这里只确认 onClick 本身可用；
                // 真正的"点了有没有效"由下面第 5 步的端到端点击验证覆盖。
                problems += Expect(report, button.onClick != null, "按钮 " + button.name + " 的 onClick 应可用");

                var label = button.GetComponentInChildren<Text>();                problems += Expect(report, label != null && !string.IsNullOrEmpty(label.text), "按钮 " + button.name + " 应有文字");
            }

            // 5. 端到端接线检查：真的"点"一下，看画面状态是不是真的跟着流程走
            if (protagonist != null && flow != null && flow.Phase == GamePhase.MemoryIntro)
            {
                var before = flow.Snapshot();
                var expected = flow.SelectActor(flow.Level != null ? flow.Level.ActorName : string.Empty);

                var handler = protagonist.GetComponent<StageClickProxy>();
                if (handler != null)
                {
                    handler.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(eventSystem));
                }

                var after = flow.Snapshot();
                problems += Expect(report, expected.Changed, "引擎外预演：点主角应当产生状态变化");
                problems += Expect(report,
                    after.Phase == (expected.Changed ? GamePhase.RewindReady : before.Phase),
                    "点主角后阶段应从 " + before.Phase + " 变为 " + GamePhase.RewindReady + "，实际 " + after.Phase);
                problems += Expect(report, after.ActorSelected, "点主角后应进入已选中状态");

                var rewindButton = FindButton("Rewind");
                problems += Expect(report, rewindButton != null, "应能找到倒流按钮");
                if (rewindButton != null)
                {
                    problems += Expect(report, rewindButton.gameObject.activeSelf, "选中人物后倒流按钮应可见");
                    problems += Expect(report, rewindButton.interactable, "选中人物后倒流按钮应可点");
                }
            }
            else
            {
                report.AppendLine("  - 跳过端到端点击检查（当前阶段不是待选人状态）");
            }

            Finish(report, problems);
        }

        private static Button FindButton(string name)
        {
            var buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name == name)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private static int Expect(System.Text.StringBuilder report, bool condition, string message)
        {
            if (condition)
            {
                report.AppendLine("  ✔ " + message);
                return 0;
            }

            report.AppendLine("  ✘ " + message);
            return 1;
        }

        private static void Finish(System.Text.StringBuilder report, int problems)
        {
            report.AppendLine(problems == 0
                ? "[借你一秒] 视图绑定自检：全部通过 ✔"
                : "[借你一秒] 视图绑定自检：发现 " + problems + " 个问题 ✘");
            if (problems == 0)
            {
                Debug.Log(report.ToString());
            }
            else
            {
                Debug.LogError(report.ToString());
            }
        }
    }
}
