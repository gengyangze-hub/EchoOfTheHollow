using System;
using System.Collections.Generic;
using EchoesOfTheHollow.Systems.Journal;
using EchoesOfTheHollow.Utils;
using StardewModdingAPI;

namespace EchoesOfTheHollow.Systems.Festival
{
    /// <summary>
    /// 节日替换协调器 — replaces vanilla festivals with new versions
    /// Egg Hunt → Puzzle Festival
    /// Flower Dance → Tapestry Sewing
    /// Moonlight Jelly → Lantern Release
    /// Winter Star → Secret Name Exchange
    /// </summary>
    public class FestivalReplacer
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly JournalSystem _journal;

        private static readonly Dictionary<string, string> FestivalReplacements = new()
        {
            ["spring13"] = "拼图节",  // Egg Festival → Puzzle Festival
            ["spring24"] = "共缝花毡", // Flower Dance → Flower Tapestry
            ["summer28"] = "放灯拾愿", // Moonlight Jelly → Lantern Release
            ["winter25"] = "秘名交换",  // Winter Star → Secret Name Exchange
        };

        public FestivalReplacer(IModHelper helper, IMonitor monitor, JournalSystem journal)
        {
            _helper = helper;
            _monitor = monitor;
            _journal = journal;
        }

        /// <summary>Get the replacement festival name for a date</summary>
        public string? GetReplacement(string season, int day)
        {
            string key = $"{season}{day}";
            return FestivalReplacements.TryGetValue(key, out string? name) ? name : null;
        }

        /// <summary>Generate festival-related journal entries</summary>
        public void OnFestivalDay(string festivalName)
        {
            if (Game1.player == null) return;

            var entry = new Data.JournalEntry
            {
                NpcName = "Lewis",
                DisplayText = $"今天是{festivalName}。镇上的每个人都带来了自己珍视的小东西。没有比赛，没有评比——只是大家一起做一件小事，然后各自回家。这种感觉很像风：来过，消失了，但你记得它吹过。",
                EmotionTag = "Warm",
                Trigger = Data.TriggerType.SeasonalEvent,
                DaysPlayed = (int)Game1.stats.DaysPlayed,
                Season = Game1.currentSeason,
                Year = Game1.year,
                DayOfMonth = Game1.dayOfMonth,
                TimeOfDay = Game1.timeOfDay,
                LocationName = "Town",
                Category = "Festival"
            };
            _journal.AddEntry(entry);

            _monitor.Log($"[Festival] {festivalName} is happening today.", LogLevel.Info);
        }
    }
}
