using System;
using System.Collections.Generic;

namespace EchoesOfTheHollow.Data
{
    /// <summary>
    /// 漂流物箱物品记录 -- items that drift to random town locations
    /// </summary>
    public class DriftRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string ItemId { get; set; } = string.Empty;
        public int Quality { get; set; }
        public int Count { get; set; } = 1;
        public string OriginLocation { get; set; } = "ShippingBin";

        public int DriftDay { get; set; }
        public int ExpiryDay { get; set; }
        public string DestinationLocation { get; set; } = string.Empty;
        public int TileX { get; set; }
        public int TileY { get; set; }
        public string? FinderNpc { get; set; }
        public bool IsFound { get; set; }

        /// <summary>Visual offset for floating animation</summary>
        public float DriftOffset { get; set; }
    }

    /// <summary>
    /// 兴致状态 -- replaces stamina
    /// </summary>
    public class EnthusiasmState
    {
        public float CurrentValue { get; set; } = 1.0f;
        public float MaxValue { get; set; } = 1.0f;
        public float DecayRate { get; set; } = 0.008f;
        public float DaydreamRecoveryRate { get; set; } = 0.015f;
        public float WindRecoveryRate { get; set; } = 0.012f;
        public int UniqueActivitiesToday { get; set; }
        public int TotalActionsToday { get; set; }
        public HashSet<string> TodayActionTypes { get; set; } = new();

        public bool IsDrained => CurrentValue <= 0.01f;
    }

    /// <summary>
    /// 邀约留言柱数据
    /// </summary>
    public class InvitationData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string FromNpc { get; set; } = string.Empty;
        public string Activity { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string TimeOfDay { get; set; } = "午后";
        public int GameTimeStart { get; set; }
        public int GameTimeEnd { get; set; }
        public int GeneratedDay { get; set; }
        public int ExpiryDay { get; set; }
        public bool IsAccepted { get; set; }
        public bool IsFulfilled { get; set; }
        public bool IsMissed { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public string AcceptText { get; set; } = string.Empty;
        public string FulfillText { get; set; } = string.Empty;
        public string MissText { get; set; } = string.Empty;
    }

    /// <summary>
    /// 离线时间记录
    /// </summary>
    public class OfflineRecord
    {
        public DateTime LastPlayedUtc { get; set; } = DateTime.UtcNow;
        public int LastGameDay { get; set; }
        public string LastSeason { get; set; } = "spring";
        public int LastYear { get; set; } = 1;
        public double TotalOfflineHours { get; set; }
    }

    /// <summary>
    /// 听风地点
    /// </summary>
    public class WindListeningSpot
    {
        public string LocationName { get; set; } = string.Empty;
        public int TileX { get; set; }
        public int TileY { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public float RecoveryMultiplier { get; set; } = 1.0f;
    }

    /// <summary>
    /// 旧物件交互数据
    /// </summary>
    public class OldObjectData
    {
        public string Id { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public int TileX { get; set; }
        public int TileY { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RewardItemId { get; set; } = string.Empty;
        public string MemoryText { get; set; } = string.Empty;
        public string NpcReaction { get; set; } = string.Empty;
    }

    /// <summary>
    /// 节日替换配置
    /// </summary>
    public class FestivalConfig
    {
        public string VanillaFestivalId { get; set; } = string.Empty;
        public string ReplacementName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public int Day { get; set; }
        public List<string> ActivitySteps { get; set; } = new();
    }

    /// <summary>
    /// 活动记录 -- 追踪玩家行为多样性
    /// </summary>
    public class ActivityRecord
    {
        public List<ActivityLogEntry> RecentActions { get; set; } = new();
        public Dictionary<string, int> ActionTypeCounts { get; set; } = new();
        public int TotalActionsToday { get; set; }
    }

    public class ActivityLogEntry
    {
        public string ActionType { get; set; } = string.Empty;
        public int GameTime { get; set; }
        public string Location { get; set; } = string.Empty;
    }
}
