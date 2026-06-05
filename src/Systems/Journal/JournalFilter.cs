using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheHollow.Data;

namespace EchoesOfTheHollow.Systems.Journal
{
    /// <summary>
    /// Journal filtering and sorting utilities for the UI.
    /// </summary>
    internal static class JournalFilter
    {
        public enum SortMode { Newest, Oldest, ByNpcName, ByEmotion }

        public static List<JournalEntry> Filter(List<JournalEntry> entries,
            string? npc = null, TriggerType? trigger = null, string? emotion = null,
            string? season = null, int? year = null, string? search = null,
            SortMode sort = SortMode.Newest)
        {
            var filtered = entries.AsEnumerable();

            if (!string.IsNullOrEmpty(npc))
                filtered = filtered.Where(e => string.Equals(e.NpcName, npc, StringComparison.OrdinalIgnoreCase));
            if (trigger.HasValue)
                filtered = filtered.Where(e => e.Trigger == trigger.Value);
            if (!string.IsNullOrEmpty(emotion))
                filtered = filtered.Where(e => e.EmotionTag == emotion);
            if (!string.IsNullOrEmpty(season))
                filtered = filtered.Where(e => e.Season == season);
            if (year.HasValue)
                filtered = filtered.Where(e => e.Year == year.Value);
            if (!string.IsNullOrEmpty(search))
                filtered = filtered.Where(e => e.DisplayText.Contains(search, StringComparison.OrdinalIgnoreCase));

            return sort switch
            {
                SortMode.Newest => filtered.OrderByDescending(e => e.Year).ThenByDescending(e => e.DaysPlayed).ToList(),
                SortMode.Oldest => filtered.OrderBy(e => e.Year).ThenBy(e => e.DaysPlayed).ToList(),
                SortMode.ByNpcName => filtered.OrderBy(e => e.NpcName).ThenByDescending(e => e.DaysPlayed).ToList(),
                SortMode.ByEmotion => filtered.OrderBy(e => e.EmotionTag).ThenByDescending(e => e.DaysPlayed).ToList(),
                _ => filtered.OrderByDescending(e => e.DaysPlayed).ToList()
            };
        }

        public static List<string> GetUniqueValues(List<JournalEntry> entries, Func<JournalEntry, string> selector)
        {
            return entries.Select(selector).Distinct().OrderBy(x => x).ToList();
        }
    }
}
