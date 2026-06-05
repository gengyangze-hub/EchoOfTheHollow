using System;
using System.Collections.Generic;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.NpcProfiles;
using EchoesOfTheHollow.Systems.Journal;
using EchoesOfTheHollow.Utils;
using StardewModdingAPI;

namespace EchoesOfTheHollow.Systems.Offline
{
    /// <summary>
    /// 离线时间管理 — tracks real-world time between sessions.
    /// When players return after being away, NPCs generate "while you were away" memories.
    /// </summary>
    public class OfflineTimeManager
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly JournalSystem _journal;
        private readonly MemoryTemplateEngine _templateEngine;
        private readonly VoiceRegistry _voiceRegistry;

        private OfflineRecord _record = new();
        private const string SaveKey = "Offline/v1";

        public OfflineTimeManager(IModHelper helper, IMonitor monitor, JournalSystem journal,
            MemoryTemplateEngine templateEngine, VoiceRegistry voiceRegistry)
        {
            _helper = helper;
            _monitor = monitor;
            _journal = journal;
            _templateEngine = templateEngine;
            _voiceRegistry = voiceRegistry;
        }

        public void OnSaveLoaded()
        {
            if (!ModEntry.Config.EnableOfflineMemories)
            {
                // Still record timestamp for future use
                _record = new OfflineRecord
                {
                    LastPlayedUtc = DateTime.UtcNow,
                    LastGameDay = (int)Game1.stats.DaysPlayed,
                    LastSeason = Game1.currentSeason,
                    LastYear = Game1.year
                };
                return;
            }

            var saved = ModDataHelper.Load<OfflineRecord>(SaveKey);
            if (saved == null)
            {
                _record = new OfflineRecord();
                return;
            }

            TimeSpan elapsed = DateTime.UtcNow - saved.LastPlayedUtc;
            double hoursAway = elapsed.TotalHours;

            _record = saved;
            _record.TotalOfflineHours += hoursAway;

            float minHours = ModEntry.Config.MinOfflineHoursForMemories;
            float maxMemories = ModEntry.Config.MaxOfflineMemories;

            if (hoursAway >= minHours)
            {
                GenerateOfflineMemories(hoursAway, (int)maxMemories);
            }

            // Update record
            _record.LastPlayedUtc = DateTime.UtcNow;
        }

        private void GenerateOfflineMemories(double hoursAway, int maxMemories)
        {
            if (Game1.player == null) return;

            int daysAway = (int)(hoursAway / 24);
            int count = Math.Min(maxMemories, (int)(hoursAway / 12)); // 1 memory per 12 hours away

            var npcs = _voiceRegistry.GetAllNpcNames();
            var npcList = new List<string>(npcs);
            RandomHelper.Shuffle(npcList);

            int generated = 0;
            foreach (string npcName in npcList)
            {
                if (generated >= count) break;

                var template = _templateEngine.SelectTemplate(npcName, TriggerType.OfflinePassage);
                if (template != null)
                {
                    var parms = new Dictionary<string, string>
                    {
                        ["playerName"] = Game1.player.Name,
                        ["npcName"] = npcName,
                        ["daysAway"] = daysAway.ToString(),
                        ["hoursAway"] = ((int)hoursAway).ToString(),
                        ["season"] = Game1.currentSeason
                    };

                    var entry = _templateEngine.GenerateEntry(template, npcName, parms,
                        Game1.timeOfDay, Game1.currentLocation?.Name);

                    // 💫 Special offline touch: the entry is marked with real-world passage
                    entry.Category = "OfflineMemory";
                    entry.EmotionTag = "Longing";

                    _journal.AddEntry(entry);
                    generated++;
                }
            }

            _monitor.Log($"[Offline] Player was away {hoursAway:F1}h real-time. Generated {generated} offline memories.",
                LogLevel.Info);
        }

        public void OnSaving()
        {
            _record.LastPlayedUtc = DateTime.UtcNow;
            _record.LastGameDay = (int)Game1.stats.DaysPlayed;
            _record.LastSeason = Game1.currentSeason;
            _record.LastYear = Game1.year;
            ModDataHelper.Save(SaveKey, _record);
        }

        public OfflineRecord GetRecord() => _record;
    }
}
