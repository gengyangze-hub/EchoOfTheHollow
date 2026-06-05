using System;

namespace EchoesOfTheHollow.Data
{
    /// <summary>
    /// 互惠篮中的物品记录
    /// </summary>
    public class BasketItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string ItemId { get; set; } = string.Empty;
        public int Quality { get; set; }
        public int Count { get; set; } = 1;

        public string? RequestNote { get; set; }
        public string? RequestedItemId { get; set; }
        public string? TargetNpcName { get; set; }

        public int DepositedDay { get; set; }
        public int DaysUntilReturn { get; set; } = 3;
        public ExchangeStatus Status { get; set; } = ExchangeStatus.Pending;

        // ── Result ──
        public string? MatchedNpcName { get; set; }
        public string? ReceivedItemId { get; set; }
        public int ReceivedItemQuality { get; set; }
        public int ReceivedItemCount { get; set; }
        public string? ExchangeNote { get; set; }
    }

    public enum ExchangeStatus
    {
        Pending,
        Matched,
        Returned
    }

    /// <summary>
    /// 交换匹配结果
    /// </summary>
    public class ExchangeResult
    {
        public bool IsMatched { get; set; }
        public string NpcName { get; set; } = string.Empty;
        public string ReceivedItemId { get; set; } = string.Empty;
        public int ReceivedQuality { get; set; }
        public int ReceivedCount { get; set; }
        public float MatchScore { get; set; }
        public string? NpcResponseText { get; set; }
        public string? ExchangeNote { get; set; }
    }
}
