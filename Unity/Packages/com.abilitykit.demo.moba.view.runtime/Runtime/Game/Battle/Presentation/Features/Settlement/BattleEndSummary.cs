using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Battle.Presentation.Features.Settlement
{
    /// <summary>
    /// 单个玩家结算数据。来源：<see cref="BattleStartPlan.LaunchSpec.Players"/> + 实时死亡次数 / 击杀数 / 助攻数。
    /// 由 <c>BattleEndSummaryRecorder</c> 在 <c>Battle.InMatch.OnDetach</c> 时聚合，
    /// 由 <c>BattleEndSettlementFeature</c> 在 <c>Battle.End.OnAttach</c> 时读取。
    /// </summary>
    public sealed class BattleEndPlayerRow
    {
        public string PlayerId { get; set; } = string.Empty;
        public int TeamId { get; set; }
        public int HeroId { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public int FinalHp { get; set; }
        public int MaxHp { get; set; }
        public bool IsLocalPlayer { get; set; }
        public bool IsAlive { get; set; } = true;

        public float Kda => Deaths <= 0 ? Kills + Assists : (Kills + Assists) / (float)Deaths;
    }

    /// <summary>
    /// 结算快照。包含所有玩家 + 胜负结果 + 时长。
    /// </summary>
    public sealed class BattleEndSummary
    {
        public int WinningTeamId { get; set; }       // 0 = 平局
        public bool LocalPlayerVictory { get; set; }
        public int MatchDurationFrames { get; set; }
        public int MatchDurationSeconds { get; set; }
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;

        public List<BattleEndPlayerRow> Players { get; } = new List<BattleEndPlayerRow>(10);

        public static BattleEndSummary Empty { get; } = new BattleEndSummary();
    }

    /// <summary>
    /// 静态缓存：跨阶段持久化的结算数据。
    /// 因为 <c>Battle.End</c> 配置了 <c>clearBeforeEnter: true</c>，所有 Battle.InMatch feature 都会被
    /// 卸载，<c>IGameFeatureStore</c> 也会被清空；这里用静态注册中心让 recorder → settlement 跨阶段通信。
    /// </summary>
    public static class BattleEndSummaryCache
    {
        private static BattleEndSummary _current = BattleEndSummary.Empty;

        public static BattleEndSummary Current
        {
            get => _current;
            set => _current = value ?? BattleEndSummary.Empty;
        }

        public static void Reset()
        {
            _current = BattleEndSummary.Empty;
        }
    }
}