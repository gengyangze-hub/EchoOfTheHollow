using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.Systems.Journal;
using EchoesOfTheHollow.Utils;
using StardewModdingAPI;

namespace EchoesOfTheHollow.Systems.Economy
{
    /// <summary>
    /// 漂流物箱 — 替代出货箱
    /// Items placed here drift to random town locations for NPCs to find.
    /// </summary>
    public class DriftBoxSystem
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly JournalSystem _journal;
        private List<DriftRecord> _driftItems = new();
        private const string SaveKey = "DriftBox/v1";

        // Common town drift locations
        private static readonly (string location, int x, int y)[] DriftSpots =
        {
            ("Town", 52, 62),       // Near tree
            ("Town", 40, 80),       // Near saloon
            ("Town", 75, 55),       // Near clinic
            ("Town", 95, 90),       // Near community center
            ("Forest", 20, 25),     // Near Marnie's
            ("Forest", 50, 15),     // Near wizard tower
            ("Mountain", 55, 8),     // Near Robin's
            ("Mountain", 90, 35),    // Near lake
            ("Beach", 30, 15),       // Near Elliott's
            ("Beach", 70, 10),       // Near pier
            ("BusStop", 15, 22),     // Near bus
        };

        public DriftBoxSystem(IModHelper helper, IMonitor monitor, JournalSystem journal)
        {
            _helper = helper;
            _monitor = monitor;
            _journal = journal;
        }

        /// <summary>Deposit an item into the drift box</summary>
        public void DepositItem(StardewValley.Item item)
        {
            if (item == null) return;

            var spot = DriftSpots[RandomHelper.Next(DriftSpots.Length)];
            int currentDay = (int)Game1.stats.DaysPlayed;

            var record = new DriftRecord
            {
                ItemId = item.QualifiedItemId,
                Quality = item.Quality,
                Count = item.Stack,
                OriginLocation = Game1.player?.currentLocation?.Name ?? "Farm",
                DriftDay = currentDay,
                ExpiryDay = currentDay + RandomHelper.Next(2, 5), // Stays 2-4 days
                DestinationLocation = spot.location,
                TileX = spot.x,
                TileY = spot.y
            };

            _driftItems.Add(record);
            _monitor.Log($"[DriftBox] Item {item.Name} will drift to {spot.location} ({spot.x},{spot.y})", LogLevel.Debug);
        }

        /// <summary>Check if any NPC finds drift items today</summary>
        public void CheckNpcFindings()
        {
            if (Game1.player == null) return;

            // Random NPC discovers a random drift item
            var activeItems = _driftItems.Where(d => !d.IsFound && d.DriftDay <= (int)Game1.stats.DaysPlayed).ToList();
            if (activeItems.Count == 0) return;

            // 30% chance per day of a discovery
            if (!RandomHelper.Chance(0.3)) return;

            var driftItem = activeItems[RandomHelper.Next(activeItems.Count)];
            driftItem.IsFound = true;

            // Pick a random NPC who "finds" it
            var npcs = new[] { "Linus", "Abigail", "Emily", "Leah", "Gus", "Willy", "Clint" };
            string finder = npcs[RandomHelper.Next(npcs.Length)];
            driftItem.FinderNpc = finder;

            // Generate journal entry
            var parms = new Dictionary<string, string>
            {
                ["playerName"] = Game1.player.Name,
                ["npcName"] = finder,
                ["item"] = driftItem.ItemId,
                ["location"] = driftItem.DestinationLocation
            };

            var entry = new Data.JournalEntry
            {
                NpcName = finder,
                DisplayText = $"在{driftItem.DestinationLocation}发现了一个{driftItem.ItemId}。它看起来是被什么人放出去的，想看看谁会捡到它。好吧，现在我捡到了。也许它的主人有一天会在日志里读到这段话。",
                EmotionTag = "Curiosity",
                Trigger = TriggerType.DriftFind,
                DaysPlayed = (int)Game1.stats.DaysPlayed,
                Season = Game1.currentSeason,
                Year = Game1.year,
                DayOfMonth = Game1.dayOfMonth,
                TimeOfDay = Game1.timeOfDay,
                Category = "DriftFind",
                Parameters = parms
            };
            _journal.AddEntry(entry);

            _monitor.Log($"[DriftBox] {finder} found a drifted {driftItem.ItemId} at {driftItem.DestinationLocation}", LogLevel.Info);
        }

        // ── Save/Load ──

        public void OnSaveLoaded()
        {
            _driftItems = ModDataHelper.LoadList<DriftRecord>(SaveKey);
        }

        public void OnSaving()
        {
            ModDataHelper.SaveList(SaveKey, _driftItems);
        }

        public void OnDayStarted()
        {
            CheckNpcFindings();
        }

        public void OnDayEnding() { }

        public List<DriftRecord> GetActiveDriftItems()
        {
            int today = (int)Game1.stats.DaysPlayed;
            return _driftItems.Where(d => !d.IsFound && d.ExpiryDay >= today).ToList();
        }
    }
}
