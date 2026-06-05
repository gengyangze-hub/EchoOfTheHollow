using System.Collections.Generic;
using EchoesOfTheHollow.Utils;
using StardewModdingAPI;
using StardewValley;

namespace EchoesOfTheHollow.Systems.Journal
{
    /// <summary>
    /// 记忆清晰度系统 -- 替代好感度数字。
    /// 每个NPC对玩家有一个"记忆清晰度"值（仅后台使用，从不显示为数字）。
    /// 清晰度影响：
    ///   - 日志条目的文本细节和情感深度
    ///   - 触发记忆的频率
    ///   - NPC主动互动的概率
    ///   - 记忆的质量（模糊 → 清晰 → 深刻）
    ///
    /// 设计原则（来自设计文档）：
    ///   "好感度心形完全移除。代之以每个NPC对你的'记忆清晰度'（仅后台使用），影响日志的细节和频率。"
    /// </summary>
    internal static class MemoryClaritySystem
    {
        // Clarity range: 0 (forgotten) to 100 (vividly remembered)
        // Scale is deliberately non-linear and opaque to the player
        private static readonly Dictionary<string, float> _clarity = new();
        private static readonly Dictionary<string, int> _lastClarityUpdate = new();
        private const string SaveKey = "MemoryClarity/v1";

        // Decay: per game-day, clarity drifts toward a baseline
        private const float DailyDecayRate = 0.5f;
        // Growth: per meaningful interaction, clarity increases
        private const float InteractionGain = 3.0f;
        // Baseline: NPCs who have met you retain at least this much
        private const float BaselineClarity = 10f;

        /// <summary>Get the narrative clarity tier for an NPC</summary>
        public static ClarityTier GetClarityTier(string npcName)
        {
            float value = GetClarity(npcName);
            return value switch
            {
                < 5 => ClarityTier.Forgotten,    // 几乎忘记了你的存在
                < 15 => ClarityTier.Blurred,     // 对你只有模糊的轮廓
                < 30 => ClarityTier.Faint,       // 记得一点，但不确切
                < 50 => ClarityTier.Recognized,  // 认出你是谁，记得一些事
                < 70 => ClarityTier.Clear,       // 对你的记忆很清晰
                < 90 => ClarityTier.Vivid,       // 关于你的记忆生动而具体
                _ => ClarityTier.DeeplyEngraved  // 你在他/她的记忆里留下了深刻的痕迹
            };
        }

        /// <summary>Narrative description of clarity level (no numbers)</summary>
        public static string DescribeClarity(string npcName)
        {
            return GetClarityTier(npcName) switch
            {
                ClarityTier.Forgotten => "似乎已经忘记了你的存在",
                ClarityTier.Blurred => "对你只有一个模糊的影子",
                ClarityTier.Faint => "记得一点点关于你的事，像隔着一层雾",
                ClarityTier.Recognized => "清楚地记得你是谁",
                ClarityTier.Clear => "能回忆起很多关于你的细节",
                ClarityTier.Vivid => "对你的记忆鲜活而具体",
                ClarityTier.DeeplyEngraved => "把你记得很深----像刻在木头上的名字",
                _ => "记得你"
            };
        }

        /// <summary>Get raw clarity value (0-100)</summary>
        public static float GetClarity(string npcName)
        {
            _clarity.TryGetValue(npcName, out float v);
            // Also factor in actual friendship as a base
            float friendshipBase = 0;
            if (Game1.player != null && Game1.player.friendshipData.TryGetValue(npcName, out Friendship? f))
                friendshipBase = f.Points / 25f; // 2500 points → 100 clarity
            return v > 0 ? (v + friendshipBase) / 2f : friendshipBase;
        }

        /// <summary>Record an interaction that makes the NPC remember you better</summary>
        public static void RecordInteraction(string npcName, float impact = 1.0f)
        {
            if (!_clarity.ContainsKey(npcName))
                _clarity[npcName] = BaselineClarity;

            _clarity[npcName] = System.Math.Min(100f, _clarity[npcName] + InteractionGain * impact);
            _lastClarityUpdate[npcName] = Game1.year * 112 + Utility.getSeasonNumber(Game1.currentSeason) * 28 + Game1.dayOfMonth;
        }

        /// <summary>Apply daily decay to all tracked NPCs and prune stale entries</summary>
        public static void ApplyDailyDecay()
        {
            int today = Game1.year * 112 + Utility.getSeasonNumber(Game1.currentSeason) * 28 + Game1.dayOfMonth;
            var keys = new List<string>(_clarity.Keys);

            foreach (string npc in keys)
            {
                if (!_lastClarityUpdate.TryGetValue(npc, out int lastDay))
                    continue;

                int daysSince = today - lastDay;

                // Apply daily decay
                if (daysSince > 0)
                {
                    _clarity[npc] = System.Math.Max(BaselineClarity,
                        _clarity[npc] - DailyDecayRate * daysSince);
                }

                // Prune fully decayed entries not updated in 60+ days
                if (_clarity[npc] <= BaselineClarity + 1 && daysSince > 60)
                {
                    _clarity.Remove(npc);
                    _lastClarityUpdate.Remove(npc);
                }
            }
        }

        /// <summary>Determines if an NPC's journal entry should be extra detailed</summary>
        public static bool ShouldWriteDetailedEntry(string npcName)
        {
            float clarity = GetClarity(npcName);
            return clarity > 50 && RandomHelper.Chance(clarity / 100.0);
        }

        /// <summary>How many journal entries this NPC tends to generate per day</summary>
        public static int GetEntryFrequency(string npcName)
        {
            float clarity = GetClarity(npcName);
            return clarity switch
            {
                < 10 => 0,      // Forgotten -- rare entries
                < 50 => 1,      // Faint or Recognized -- one
                < 75 => 2,      // Clear -- up to two
                _ => 3           // Vivid+ -- up to three
            };
        }

        // ── Save/Load ──

        public static void OnSaveLoaded()
        {
            var saved = ModDataHelper.Load<ClaritySaveData>(SaveKey);
            if (saved != null)
            {
                foreach (var kv in saved.Values)
                    _clarity[kv.Key] = kv.Value;
                foreach (var kv in saved.LastUpdates)
                    _lastClarityUpdate[kv.Key] = kv.Value;
            }
        }

        public static void OnSaving()
        {
            ModDataHelper.Save(SaveKey, new ClaritySaveData
            {
                Values = new Dictionary<string, float>(_clarity),
                LastUpdates = new Dictionary<string, int>(_lastClarityUpdate)
            });
        }

        public static void OnDayStarted()
        {
            ApplyDailyDecay();
        }

        // ── Save wrapper ──

        private class ClaritySaveData
        {
            public Dictionary<string, float> Values { get; set; } = new();
            public Dictionary<string, int> LastUpdates { get; set; } = new();
        }
    }

    public enum ClarityTier
    {
        Forgotten,      // Almost forgot you exist
        Blurred,        // Only a vague outline
        Faint,          // Remembers a little, through fog
        Recognized,     // Knows who you are
        Clear,          // Many details remembered
        Vivid,          // Memories are alive and specific
        DeeplyEngraved  // Carved deep -- like a name in wood
    }
}
