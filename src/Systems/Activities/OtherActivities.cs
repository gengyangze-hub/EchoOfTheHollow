using System.Collections.Generic;
using System.Linq;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.Systems.Journal;
using EchoesOfTheHollow.Utils;
using Microsoft.Xna.Framework;
using StardewModdingAPI;

namespace EchoesOfTheHollow.Systems.Activities
{
    /// <summary>
    /// 触摸旧物 -- interact with hidden relics around the valley.
    /// Each location has scattered objects that tell silent stories.
    /// </summary>
    public class OldObjectInteraction
    {
        private readonly IMonitor _monitor;
        private readonly JournalSystem _journal;
        private readonly Dictionary<string, HashSet<string>> _foundRelics = new();

        public OldObjectInteraction(IMonitor monitor, JournalSystem journal)
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

    /// <summary>
    /// 追踪动物 -- detect when the player is near farm animals or town pets.
    /// Generates gentle journal entries from observing NPCs.
    /// </summary>
    public class AnimalTrackingSystem
    {
        private readonly IMonitor _monitor;
        private readonly JournalSystem _journal;
        private readonly Dictionary<string, int> _lastTrackedAnimal = new();
        private int _todayTrackingCount;

        // Max tracking entries per day
        private const int MaxDailyTrackings = 3;
        // Minimum in-game minutes between entries about the same animal type
        private const int CooldownMinutes = 120;

        public AnimalTrackingSystem(IMonitor monitor, JournalSystem journal)
        {
            _monitor = monitor;
            _journal = journal;
        }

        /// <summary>Check for nearby animals each tick</summary>
        public void OnUpdateTicked()
        {
            if (!ModEntry.Config.EnableAnimalTracking || Game1.player == null) return;
            if (_todayTrackingCount >= MaxDailyTrackings) return;

            var currentLocation = Game1.player.currentLocation;
            if (currentLocation == null) return;

            // Check nearby farm animals
            var nearbyAnimals = currentLocation.animals.Values
                .Where(a => a != null && Vector2.Distance(Game1.player.Position, a.Position) < 200f)
                .ToList();

            foreach (var animal in nearbyAnimals)
            {
                string key = $"{animal.type.Value}_{Game1.timeOfDay / 100}"; // e.g., "White Chicken_14"
                if (!_lastTrackedAnimal.TryGetValue(key, out int lastTime) ||
                    Game1.timeOfDay - lastTime >= CooldownMinutes)
                {
                    _lastTrackedAnimal[key] = Game1.timeOfDay;
                    GenerateAnimalEntry(animal);
                    _todayTrackingCount++;
                    if (_todayTrackingCount >= MaxDailyTrackings) break;
                }
            }

            // Check for wild animals / critters
            if (_todayTrackingCount < MaxDailyTrackings && currentLocation.critters != null)
            {
                foreach (var critter in currentLocation.critters)
                {
                    if (critter == null) continue;
                    if (Vector2.Distance(Game1.player.Position, critter.position) < 150f)
                    {
                        string critterType = critter.GetType().Name;
                        string key = $"critter_{critterType}_{Game1.timeOfDay / 100}";
                        if (!_lastTrackedAnimal.ContainsKey(key))
                        {
                            _lastTrackedAnimal[key] = Game1.timeOfDay;
                            GenerateCritterEntry(critterType);
                            _todayTrackingCount++;
                            if (_todayTrackingCount >= MaxDailyTrackings) break;
                        }
                    }
                }
            }
        }

        private void GenerateAnimalEntry(FarmAnimal animal)
        {
            if (Game1.player == null) return;

            string animalName = string.IsNullOrEmpty(animal.displayName) ? animal.type.Value : animal.displayName;
            string location = Game1.currentLocation?.Name ?? "farm";

            // Observant NPC who might notice
            string observer = new[] { "Marnie", "Jas", "Shane", "Linus", "Leah" }[RandomHelper.Next(5)];

            var entry = new JournalEntry
            {
                NpcName = observer,
                DisplayText = $"看到{Game1.player.Name}在{location}靠近一只{animalName}。他们之间有一种不需要语言的对话----只是静静地站在彼此旁边。有时候我觉得动物比人更懂得这种沉默。",
                EmotionTag = "Warm",
                Trigger = TriggerType.AnimalEncounter,
                DaysPlayed = (int)Game1.stats.DaysPlayed,
                Season = Game1.currentSeason,
                Year = Game1.year,
                DayOfMonth = Game1.dayOfMonth,
                TimeOfDay = Game1.timeOfDay,
                LocationName = location,
                Category = "Animal"
            };
            _journal.AddEntry(entry);

            if (ModEntry.Config.DebugMode)
                _monitor.Log($"[Animal] Tracked: {animalName} at {location}", LogLevel.Debug);
        }

        private void GenerateCritterEntry(string critterType)
        {
            if (Game1.player == null) return;

            string location = Game1.currentLocation?.Name ?? "unknown";
            string observer = "Linus";

            var entry = new JournalEntry
            {
                NpcName = observer,
                DisplayText = $"一只野生的{critterType}在{location}出现了。{Game1.player.Name}停下了脚步看着它。山谷里这样的小生命从不要求什么，只是存在。也许这就是它们给我们的礼物。",
                EmotionTag = "Wonder",
                Trigger = TriggerType.AnimalEncounter,
                DaysPlayed = (int)Game1.stats.DaysPlayed,
                Season = Game1.currentSeason,
                Year = Game1.year,
                DayOfMonth = Game1.dayOfMonth,
                TimeOfDay = Game1.timeOfDay,
                LocationName = location,
                Category = "Wildlife"
            };
            _journal.AddEntry(entry);
        }

        /// <summary>Called at midnight to detect pet sleeping near player</summary>
        public void GenerateNightlyAnimalMemories()
        {
            if (Game1.player == null || _todayTrackingCount >= MaxDailyTrackings) return;

            // Check if the player's pet is nearby
            if (Game1.player.getPet() != null)
            {
                var pet = Game1.player.getPet();
                var entry = new JournalEntry
                {
                    NpcName = "我",
                    DisplayText = $"我的{pet.displayName}在我旁边睡着了。它的呼吸很轻，像山谷的风穿过门缝。",
                    EmotionTag = "Warm",
                    Trigger = TriggerType.AnimalEncounter,
                    IsPlayerEntry = true,
                    PlayerEntryTitle = $"和{pet.displayName}在一起",
                    DaysPlayed = (int)Game1.stats.DaysPlayed,
                    Season = Game1.currentSeason,
                    Year = Game1.year,
                    DayOfMonth = Game1.dayOfMonth,
                    TimeOfDay = Game1.timeOfDay,
                    LocationName = "Farm",
                    Category = "Pet"
                };
                _journal.AddEntry(entry);
            }
        }

        public void OnDayStarted()
        {
            _todayTrackingCount = 0;
            _lastTrackedAnimal.Clear();
        }
    }

    /// <summary>
    /// 无意义的物品 -- craft and place items that serve no purpose,
    /// generating reflective journal entries.
    /// </summary>
    public class MeaninglessItemSystem
    {
        private readonly JournalSystem _journal;

        public MeaninglessItemSystem(JournalSystem journal)
        {
            _journal = journal;
        }

        /// <summary>Record the creation and placement of a 'meaningless' item</summary>
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
