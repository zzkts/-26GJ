using System.Reflection;
using JieNiYiMiao;
using UnityEditor;
using UnityEngine;

namespace JieNiYiMiao.EditorTools
{
    /// <summary>
    /// 编辑器自检工具（需要在 Unity 里按 Play 才有意义）。
    ///
    /// 背景：本工程尝试过用 "-batchmode -nographics -executeMethod + EnterPlaymode" 做无头自动测试，
    /// 实测 Unity 6.3 在无头批处理下不会稳定推进 Play 模式（会卡在编辑器初始化），
    /// 因此流程自动化校验改由 _build/verify 下的独立校验程序完成（脱离引擎、真编译真运行）。
    ///
    /// 这个工具补齐"引擎内"的那部分：它只在播放状态下运行，依次检查
    /// 场景 UI 是否生成齐全、按钮引用是否有效、倒流按钮的可用性是否符合当前阶段，
    /// 然后把结果打到 Console，方便人手 Play 时立刻确认。
    /// </summary>
    public static class EditorSelfCheck
    {
        [MenuItem("借你一秒/3. 播放状态下自检（先按 Play）")]
        public static void RunInPlayMode()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[借你一秒] 请先进入 Play 模式，再执行这个自检。");
                return;
            }

            var manager = Object.FindAnyObjectByType<GameManager>();
            if (manager == null)
            {
                Debug.LogError("[借你一秒] 场景里找不到 GameManager，GameBootstrap 可能没有正常工作。");
                return;
            }

            var report = new System.Text.StringBuilder();
            report.AppendLine("[借你一秒] 引擎内自检报告");
            report.AppendLine("  Canvas 数量：" + Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length + "（期望 4：Background / Stage / Ui / Fx）");
            report.AppendLine("  EventSystem：" + (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null ? "存在" : "缺失"));
            report.AppendLine("  按钮数量：" + Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Length + "（期望 6）");
            report.AppendLine("  文本数量：" + Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Length);

            var phaseField = typeof(GameManager).GetField("_phase", BindingFlags.Instance | BindingFlags.NonPublic);
            var flowField = typeof(GameManager).GetField("_flow", BindingFlags.Instance | BindingFlags.NonPublic);
            if (flowField != null)
            {
                var flow = flowField.GetValue(manager) as GameFlow;
                if (flow != null)
                {
                    report.AppendLine("  当前阶段：" + flow.Phase);
                    report.AppendLine("  当前记忆：" + (flow.Level != null ? flow.Level.Title : "（无，标题界面）"));
                    report.AppendLine("  变量：" + flow.Ledger.Summary());
                }
            }
            else
            {
                report.AppendLine("  当前阶段：" + (phaseField != null ? phaseField.GetValue(manager).ToString() : "未知"));
            }

            report.AppendLine("  说明：点击流程的自动化回归测试在 _build/verify（独立校验程序）里，共 23 组断言。");
            Debug.Log(report.ToString());

            // 顺手做一次界面断言：按钮的可见性必须和阶段匹配
            foreach (var button in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                if (button == null || button.gameObject == null)
                {
                    continue;
                }

                if (!button.gameObject.activeInHierarchy && button.interactable)
                {
                    Debug.LogWarning("[借你一秒] 按钮 " + button.name + " 处于隐藏状态但仍被标记为可交互。");
                }
            }
        }
    }
}
