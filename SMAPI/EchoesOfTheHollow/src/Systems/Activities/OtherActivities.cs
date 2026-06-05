using System.Collections.Generic;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.Systems.Journal;
using EchoesOfTheHollow.Utils;

namespace EchoesOfTheHollow.Systems.Activities
{
    public class OldObjectInteraction
    {
        private readonly IMonitor _monitor;
        private readonly JournalSystem _journal;
        private readonly Dictionary<string, HashSet<string>> _foundRelics = new();

        public OldObjectInteraction(IModHelper helper, IMonitor monitor, JournalSystem journal)
        {
            _monitor = monitor;
            _journal = journal;
        }

        public void OnLocationChanged(GameLocation location)
        {
            if (!ModEntry.Config.EnableOldObjects) return;
        }

        public void InteractWithRelic(string relicId, string locationName)
        {
            if (!_foundRelics.ContainsKey(locationName))
                _foundRelics[locationName] = new HashSet<string>();

            if (_foundRelics[locationName].Contains(relicId)) return;
            _foundRelics[locationName].Add(relicId);

            var entry = new JournalEntry
            {
                NpcName = "Gunther",
                DisplayText = $"你触摸了{relicId}。它的表面是凉的，像承载了无数个被遗忘的故事。每一个旧物都是一段没有主人的记忆。",
                EmotionTag = "Nostalgia",
                Trigger = TriggerType.Environmental,
                DaysPlayed = (int)Game1.stats.DaysPlayed,
                Season = Game1.currentSeason,
                Year = Game1.year,
                DayOfMonth = Game1.dayOfMonth,
                TimeOfDay = Game1.timeOfDay,
                LocationName = locationName,
                Category = "Relic"
            };
            _journal.AddEntry(entry);
        }
    }

    public class AnimalTrackingSystem
    {
        public AnimalTrackingSystem(IModHelper helper, IMonitor monitor, JournalSystem journal) { }
        public void OnUpdateTicked() { }
    }

    public class MeaninglessItemSystem
    {
        private readonly JournalSystem _journal;

        public MeaninglessItemSystem(IModHelper helper, IMonitor monitor, JournalSystem journal)
        {
            _journal = journal;
        }

        public void CraftAndPlace(string itemName, string locationName, int tileX, int tileY)
        {
            var entry = new JournalEntry
            {
                NpcName = "我",
                DisplayText = $"我做了一个{itemName}，把它放在了{locationName}。它没有任何用途，但也许正是因为它没用，才变得特别。",
                EmotionTag = "Reflection",
                Trigger = TriggerType.Environmental,
                IsPlayerEntry = true,
                PlayerEntryTitle = $"制作了{itemName}",
                DaysPlayed = (int)Game1.stats.DaysPlayed,
                Season = Game1.currentSeason,
                Year = Game1.year,
                DayOfMonth = Game1.dayOfMonth,
                TimeOfDay = Game1.timeOfDay,
                LocationName = locationName,
                Category = "MeaninglessItem"
            };
            _journal.AddEntry(entry);
        }
    }
}
