using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.NpcProfiles;
using EchoesOfTheHollow.Utils;

namespace EchoesOfTheHollow.Systems.Journal
{
    public class TriggerDetector
    {
        private readonly IMonitor _monitor;
        private readonly JournalSystem _journal;
        private readonly VoiceRegistry _voices;
        private readonly MemoryTemplateEngine _engine;

        private readonly Dictionary<string, int> _lastInteraction = new();
        private readonly HashSet<string> _npcsMetToday = new();
        private readonly Dictionary<string, int> _consecutiveDaysInLocation = new();

        public TriggerDetector(IModHelper helper, IMonitor monitor, JournalSystem journal,
            VoiceRegistry voices, MemoryTemplateEngine engine)
        {
            _monitor = monitor;
            _journal = journal;
            _voices = voices;
            _engine = engine;
        }

        public void OnNpcInteraction(StardewValley.NPC npc)
        {
            string name = npc.Name;
            int today = Game1.year * 112 + Utility.getSeasonNumber(Game1.currentSeason) * 28 + Game1.dayOfMonth;

            if (_lastInteraction.TryGetValue(name, out int lastDay) && lastDay == today)
                return;

            _lastInteraction[name] = today;
            _npcsMetToday.Add(name);

            // Record clarity for memory system
            MemoryClaritySystem.RecordInteraction(name, 1.5f);

            var template = _engine.SelectTemplate(name, TriggerType.DirectInteraction,
                location: Game1.currentLocation?.Name, gameTime: Game1.timeOfDay,
                season: Game1.currentSeason);

            if (template != null && Game1.player != null)
            {
                var parms = new Dictionary<string, string>
                {
                    ["playerName"] = Game1.player.Name,
                    ["npcName"] = name,
                    ["location"] = Game1.currentLocation?.Name ?? "镇上",
                    ["timeOfDay"] = StringHelper.TimeOfDayToPeriod(Game1.timeOfDay),
                    ["season"] = Game1.currentSeason,
                    ["weather"] = Game1.isRaining ? "rain" : "clear"
                };

                var entry = _engine.GenerateEntry(template, name, parms, Game1.timeOfDay,
                    Game1.currentLocation?.Name);
                _journal.AddEntry(entry);
            }
        }

        public void OnLocationChanged(GameLocation newLocation)
        {
            string name = newLocation.Name;
            if (!_consecutiveDaysInLocation.ContainsKey(name))
                _consecutiveDaysInLocation[name] = 0;
            _consecutiveDaysInLocation[name]++;
        }

        /// <summary>Hook for time-change events. Reserved for future time-conditional template triggers.</summary>
        public void OnTimeChanged(int newTime) { }

        public void GenerateNightlyMemories()
        {
            _journal.GenerateNightlyMemories();

            foreach (var npcName in _voices.GetAllNpcNames())
            {
                if (!_npcsMetToday.Contains(npcName))
                {
                    int daysSinceLast = GetDaysSinceLastInteraction(npcName);
                    if (daysSinceLast >= 3 && Game1.player != null)
                    {
                        var template = _engine.SelectTemplate(npcName, TriggerType.Absence);
                        if (template != null)
                        {
                            var parms = new Dictionary<string, string>
                            {
                                ["playerName"] = Game1.player.Name,
                                ["npcName"] = npcName,
                                ["daysAway"] = daysSinceLast.ToString()
                            };
                            _journal.AddEntry(_engine.GenerateEntry(template, npcName, parms,
                                Game1.timeOfDay));
                        }
                    }
                }
            }

            _npcsMetToday.Clear();
        }

        public void OnDayStarted()
        {
            _npcsMetToday.Clear();
            foreach (var key in _consecutiveDaysInLocation.Keys.ToList())
            {
                if (Game1.currentLocation?.Name != key)
                    _consecutiveDaysInLocation.Remove(key);
            }
        }

        private int GetDaysSinceLastInteraction(string npcName)
        {
            if (_lastInteraction.TryGetValue(npcName, out int lastDay))
            {
                int today = Game1.year * 112 + Utility.getSeasonNumber(Game1.currentSeason) * 28 + Game1.dayOfMonth;
                return today - lastDay;
            }
            return 999;
        }

    }
}
