using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace EchoesOfTheHollow.Data
{
    /// <summary>
    /// 日志条目 -- 每条代表一个NPC对玩家的一次主观记忆
    /// A single journal entry -- one NPC's subjective memory about the player.
    /// </summary>
    public class JournalEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string NpcName { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string RawTemplateText { get; set; } = string.Empty;
        public string TemplateId { get; set; } = string.Empty;
        public string EmotionTag { get; set; } = "Neutral";
        public string Category { get; set; } = "General";

        public TriggerType Trigger { get; set; } = TriggerType.Environmental;

        // ── Game time ──
        public int DaysPlayed { get; set; }
        public string Season { get; set; } = "spring";
        public int Year { get; set; } = 1;
        public int TimeOfDay { get; set; }
        public int DayOfMonth { get; set; }
        public string LocationName { get; set; } = string.Empty;

        // ── Player diary ──
        public bool IsPlayerEntry { get; set; }
        public string PlayerEntryTitle { get; set; } = string.Empty;

        // ── Template parameters (for debugging) ──
        public Dictionary<string, string> Parameters { get; set; } = new();

        // ── Computed properties ──
        [JsonIgnore] public string DisplayDate =>
            $"{Utils.StringHelper.SeasonDisplayName(Season)} {DayOfMonth}日, 第{Year}年";

        [JsonIgnore] public string DisplayTime =>
            Utils.StringHelper.TimeOfDayToString(TimeOfDay);

        [JsonIgnore] public int EntryAgeDays
        {
            get
            {
                try
                {
                    int currentDayTotal = (Game1.year * 112) + (Utility.getSeasonNumber(Game1.currentSeason) * 28) + Game1.dayOfMonth;
                    int entryDayTotal = (Year * 112) + (Utility.getSeasonNumber(Season) * 28) + DayOfMonth;
                    return currentDayTotal - entryDayTotal;
                }
                catch
                {
                    return 0;
                }
            }
        }
    }

    /// <summary>Types of events that trigger journal entries</summary>
    public enum TriggerType
    {
        DirectInteraction,
        Environmental,
        CrossDayObservation,
        Absence,
        SeasonalEvent,
        WeatherEvent,
        BasketExchange,
        DriftFind,
        AnimalEncounter,
        OfflinePassage,
        PlayerDiary,
        Daydream
    }

    /// <summary>Emotion tags for filtering/visual styling</summary>
    public static class EmotionTags
    {
        public const string Warm = "Warm";
        public const string Melancholy = "Melancholy";
        public const string Curiosity = "Curiosity";
        public const string Humor = "Humor";
        public const string Longing = "Longing";
        public const string Wonder = "Wonder";
        public const string Nostalgia = "Nostalgia";
        public const string Neutral = "Neutral";
        public const string Reflection = "Reflection";
    }
}
