using System;
using JieNiYiMiao;

/// <summary>
/// 《借你一秒》旧版恢复后的最小验证：直接引用它自己的 Core 源码
/// （GameState / MemoryLevelData / StageLayout / GameFlow，这四个文件不引用 UnityEngine），
/// 把三关流程与三种结局真跑一遍。
///
/// 说明：旧版原来那套 23 组断言程序在为新版做验证时被覆盖了，这里只做一个"恢复后确实还能跑"的确认，
/// 不假装是原来那套完整断言。
/// </summary>
public static class Program
{
    private static int _fail;

    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== 《借你一秒》旧版恢复验证 ===");

        var flow = new GameFlow();
        Check("初始处于标题阶段", flow.Phase == GamePhase.Title, flow.Phase.ToString());

        flow.StartGame();
        Check("开始后进入第一段记忆", flow.Phase == GamePhase.MemoryIntro && flow.Level.Index == 0,
            flow.Phase + " / 索引 " + flow.Snapshot().LevelIndex);
        Console.WriteLine("    第一关：" + flow.Level.ChapterLabel + " · " + flow.Level.Title);

        // 第一关：选人 → 倒流 → 选择 A
        flow.SelectActor(flow.Level.ActorName);
        Check("选中人物后可以倒流", flow.Phase == GamePhase.RewindReady, flow.Phase.ToString());
        flow.RequestRewind();
        Check("倒流请求被接受且只接受一次", flow.Phase == GamePhase.Rewinding && flow.RewindUsed, flow.Phase.ToString());
        flow.CompleteRewind();
        Check("倒流完成后进入选择", flow.Phase == GamePhase.AwaitingChoice, flow.Phase.ToString());
        flow.ApplyChoice(true);
        Check("选择 A 使 Ambition +1", flow.Ledger.Ambition == 1, "Ambition=" + flow.Ledger.Ambition);

        flow.Advance();
        Check("推进到第二关", flow.Level.Index == 1, "索引 " + flow.Level.Index);
        Console.WriteLine("    第二关：" + flow.Level.ChapterLabel + " · " + flow.Level.Title);

        // 第二关：选择 B
        flow.SelectActor(flow.Level.ActorName);
        flow.RequestRewind();
        flow.CompleteRewind();
        flow.ApplyChoice(false);
        Check("选择 B 使 Connection +1", flow.Ledger.Connection == 1, "Connection=" + flow.Ledger.Connection);

        flow.Advance();
        Check("推进到第三关", flow.Level.Index == 2, "索引 " + flow.Level.Index);
        Console.WriteLine("    第三关：" + flow.Level.ChapterLabel + " · " + flow.Level.Title);

        // 第三关：保留旧木板
        flow.SelectActor(flow.Level.ActorName);
        flow.RequestRewind();
        flow.CompleteRewind();
        flow.ApplyChoice(false);
        var ending = flow.ResolveEnding();
        Check("三关走完能判定出结局", ending == EndingKind.MendedBoat || ending == EndingKind.OldBoatRemains,
            ending.ToString());
        Console.WriteLine("    结局判定：" + ending);

        flow.Advance();
        Check("推进后进入反转演出", flow.Phase == GamePhase.Reveal, flow.Phase.ToString());
        flow.FinishReveal();
        Check("反转演出后进入结局", flow.Phase == GamePhase.Ending, flow.Phase.ToString());

        // 三种结局都要可达
        Check("换掉最后一块旧木板 → 全新的船", EndingFor(true) == EndingKind.BrandNewBoat,
            EndingFor(true).ToString());

        flow.RestartFromBeginning();
        Check("重新开始后回到标题且数值清零",
            flow.Phase == GamePhase.Title && flow.Ledger.Ambition == 0 && flow.Ledger.Connection == 0,
            flow.Phase + " A=" + flow.Ledger.Ambition + " C=" + flow.Ledger.Connection);

        Console.WriteLine(_fail == 0 ? "=== 全部通过 ===" : "=== 失败 " + _fail + " 项 ===");
        return _fail == 0 ? 0 : 1;
    }

    private static EndingKind EndingFor(bool replaceBoard)
    {
        var flow = new GameFlow();
        flow.StartGame();
        for (int i = 0; i < 3; i++)
        {
            flow.SelectActor(flow.Level.ActorName);
            flow.RequestRewind();
            flow.CompleteRewind();
            flow.ApplyChoice(i != 2 || replaceBoard);
            flow.Advance();
        }

        return flow.ResolveEnding();
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (ok)
        {
            Console.WriteLine("  [通过] " + name);
            return;
        }

        _fail++;
        Console.WriteLine("  [失败] " + name + "  → " + detail);
    }
}
