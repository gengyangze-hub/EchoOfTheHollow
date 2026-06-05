using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.NpcProfiles;
using EchoesOfTheHollow.Utils;
using StardewModdingAPI;

namespace EchoesOfTheHollow.Systems.Journal
{
    /// <summary>
    /// 核心日志系统 — 管理所有记忆条目
    /// Core Journal System — manages all journal entries.
    /// </summary>
    public class JournalSystem
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly MemoryTemplateEngine _templateEngine;
        private readonly VoiceRegistry _voiceRegistry;

        private List<JournalEntry> _entries = new();
        private readonly object _lock = new();

        public int EntryCount { get { lock (_lock) return _entries.Count; } }
        public event Action<JournalEntry>? OnEntryAdded;

        private const string SaveKey = "Journal/v1";

        public JournalSystem(IModHelper helper, IMonitor monitor,
            MemoryTemplateEngine templateEngine, VoiceRegistry voiceRegistry)
        {
            _helper = helper;
            _monitor = monitor;
            _templateEngine = templateEngine;
            _voiceRegistry = voiceRegistry;
        }

        /// <summary>Add a journal entry</summary>
        public void AddEntry(JournalEntry entry)
        {
            lock (_lock)
            {
                // Trim old entries if over limit
                while (_entries.Count >= ModEntry.Config.MaxJournalEntries)
                    _entries.RemoveAt(0);

                _entries.Add(entry);
            }

            OnEntryAdded?.Invoke(entry);

            if (ModEntry.Config.DebugMode)
                _monitor.Log($"[Journal] New entry: [{entry.NpcName}] {StringHelper.Truncate(entry.DisplayText, 80)}", LogLevel.Debug);
        }

        /// <summary>Add a player-written diary entry</summary>
        public void AddPlayerEntry(string title, string text)
        {
            AddEntry(new JournalEntry
            {
                NpcName = "我",
                DisplayText = text,
                PlayerEntryTitle = title,
                IsPlayerEntry = true,
                Trigger = TriggerType.PlayerDiary,
                EmotionTag = "Reflection",
                DaysPlayed = (int)Game1.stats.DaysPlayed,
                Season = Game1.currentSeason,
                Year = Game1.year,
                DayOfMonth = Game1.dayOfMonth,
                TimeOfDay = Game1.timeOfDay,
                LocationName = Game1.player?.currentLocation?.Name ?? ""
            });
        }

        /// <summary>Get all entries</summary>
        public List<JournalEntry> GetAllEntries()
        {
            lock (_lock) return _entries.OrderBy(e => e.Year).ThenBy(e => e.DaysPlayed).ToList();
        }

        /// <summary>Get entries filtered by criteria</summary>
        public List<JournalEntry> GetFiltered(string? npcName = null, TriggerType? trigger = null,
            string? season = null, int? year = null, int? maxAgeDays = null, string? searchText = null)
        {
            lock (_lock)
            {
                var query = _entries.AsEnumerable();

                if (!string.IsNullOrEmpty(npcName))
                    query = query.Where(e => e.NpcName == npcName);
                if (trigger.HasValue)
                    query = query.Where(e => e.Trigger == trigger.Value);
                if (!string.IsNullOrEmpty(season))
                    query = query.Where(e => e.Season == season);
                if (year.HasValue)
                    query = query.Where(e => e.Year == year.Value);
                if (maxAgeDays.HasValue)
                    query = query.Where(e => e.EntryAgeDays <= maxAgeDays.Value);
                if (!string.IsNullOrEmpty(searchText))
                    query = query.Where(e => e.DisplayText.Contains(searchText, StringComparison.OrdinalIgnoreCase));

                return query.OrderByDescending(e => e.Year).ThenByDescending(e => e.DaysPlayed).ToList();
            }
        }

        /// <summary>Get entries from a specific NPC</summary>
        public List<JournalEntry> GetEntriesByNpc(string npcName)
        {
            lock (_lock) return _entries.Where(e => e.NpcName == npcName)
                .OrderByDescending(e => e.DaysPlayed).ToList();
        }

        /// <summary>Get the most recent entries (for memory book display)</summary>
        public List<JournalEntry> GetRecentByNpc(string npcName, int count = 3)
        {
            lock (_lock) return _entries.Where(e => e.NpcName == npcName)
                .OrderByDescending(e => e.DaysPlayed).Take(count).ToList();
        }

        /// <summary>Get all NPCs that have entries about the player</summary>
        public List<string> GetKnownNpcs()
        {
            lock (_lock) return _entries.Select(e => e.NpcName).Distinct().ToList();
        }

        /// <summary>Get unique NPC+emotion pairs for memory book</summary>
        public Dictionary<string, string> GetNpcLatestEmotions()
        {
            lock (_lock)
            {
                return _entries
                    .GroupBy(e => e.NpcName)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.DaysPlayed).First().EmotionTag);
            }
        }

        /// <summary>Generate nightly cross-day observations, governed by memory clarity</summary>
        public void GenerateNightlyMemories()
        {
            if (Game1.player == null) return;

            int count = RandomHelper.Next(1, ModEntry.Config.NightlyMemoryCount + 1);
            var npcs = _voiceRegistry.GetAllNpcNames()
                .Where(n => MemoryClaritySystem.GetClarityTier(n) >= ClarityTier.Recognized)
                .OrderByDescending(n => MemoryClaritySystem.GetClarity(n))
                .ToList();
            RandomHelper.Shuffle(npcs);

            int generated = 0;
            var todayNpcCounts = new Dictionary<string, int>();

            foreach (string npc in npcs)
            {
                if (generated >= count) break;

                // Respect per-NPC entry frequency based on clarity
                int maxForNpc = MemoryClaritySystem.GetEntryFrequency(npc);
                todayNpcCounts.TryGetValue(npc, out int npcCount);
                if (npcCount >= maxForNpc) continue;

                string? location = Game1.player.currentLocation?.Name;
                string weather = Game1.isRaining ? "rain" : Game1.isSnowing ? "snow" : "clear";

                var template = _templateEngine.SelectTemplate(npc, TriggerType.CrossDayObservation,
                    location: location, weather: weather, season: Game1.currentSeason, gameTime: Game1.timeOfDay);

                if (template != null)
                {
                    var parameters = new Dictionary<string, string>
                    {
                        ["playerName"] = Game1.player.Name,
                        ["location"] = location ?? "镇上",
                        ["weather"] = weather,
                        ["season"] = Game1.currentSeason,
                        ["timeOfDay"] = StringHelper.TimeOfDayToPeriod(Game1.timeOfDay),
                        ["dayOfMonth"] = Game1.dayOfMonth.ToString(),
                        ["year"] = Game1.year.ToString()
                    };

                    // Inject detail hint for high-clarity NPCs
                    if (MemoryClaritySystem.ShouldWriteDetailedEntry(npc))
                        parameters["detail"] = "vivid";

                    var entry = _templateEngine.GenerateEntry(template, npc, parameters, Game1.timeOfDay, location);
                    AddEntry(entry);

                    todayNpcCounts[npc] = npcCount + 1;
                    generated++;
                }
            }

            if (ModEntry.Config.DebugMode)
                _monitor.Log($"[Journal] Generated {generated} nightly memories.", LogLevel.Debug);
        }

        // ── Save/Load ──

        public void OnSaveLoaded()
        {
            _entries = ModDataHelper.LoadList<JournalEntry>(SaveKey);
            _monitor.Log($"[Journal] Loaded {_entries.Count} entries from save.");
        }

        public void OnSaving()
        {
            lock (_lock)
            {
                ModDataHelper.SaveList(SaveKey, _entries);
            }
            _monitor.Log($"[Journal] Saved {_entries.Count} entries.");
        }

        /// <summary>Remove a player diary entry by ID</summary>
        public bool RemoveEntry(string entryId)
        {
            lock (_lock)
            {
                var entry = _entries.FirstOrDefault(e => e.Id == entryId && e.IsPlayerEntry);
                if (entry != null)
                {
                    _entries.Remove(entry);
                    return true;
                }
                return false;
            }
        }

        public void OnDayStarted() { /* Reset daily tracking */ }
        public void OnDayEnding() { /* Handled by GenerateNightlyMemories */ }
    }
}
