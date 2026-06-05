using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.Systems.Enthusiasm;
using EchoesOfTheHollow.Utils;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewValley;
using Microsoft.Xna.Framework;

namespace EchoesOfTheHollow.Systems.Activities
{
    /// <summary>
    /// 听风系统 — stand in specific scenic spots, see color swirls, hear the valley
    /// </summary>
    public class WindListeningSystem
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly EnthusiasmSystem _enthusiasm;
        private List<WindListeningSpot> _spots = new();

        private bool _isListening;
        private int _listenTicks;
        private WindListeningSpot? _currentSpot;
        private int _requiredTicks = 180; // ~3 seconds to start

        public WindListeningSystem(IModHelper helper, IMonitor monitor, EnthusiasmSystem enthusiasm)
        {
            _helper = helper;
            _monitor = monitor;
            _enthusiasm = enthusiasm;
            LoadSpots(helper);
        }

        private void LoadSpots(IModHelper helper)
        {
            string path = Path.Combine(helper.DirectoryPath, "assets", "data", "WindListeningSpots.json");
            if (File.Exists(path))
            {
                try
                {
                    _spots = JsonConvert.DeserializeObject<List<WindListeningSpot>>(
                        File.ReadAllText(path)) ?? new List<WindListeningSpot>();
                }
                catch { }
            }

            // Default spots if JSON missing
            if (_spots.Count == 0)
            {
                _spots = new List<WindListeningSpot>
                {
                    new() { LocationName = "Forest", TileX = 20, TileY = 30, DisplayName = "柳树下", RecoveryMultiplier = 1.2f },
                    new() { LocationName = "Mountain", TileX = 50, TileY = 8, DisplayName = "山顶", RecoveryMultiplier = 1.5f },
                    new() { LocationName = "Beach", TileX = 70, TileY = 8, DisplayName = "海边悬崖", RecoveryMultiplier = 1.3f },
                    new() { LocationName = "Town", TileX = 45, TileY = 65, DisplayName = "广场中央", RecoveryMultiplier = 1.0f },
                    new() { LocationName = "Forest", TileX = 55, TileY = 15, DisplayName = "法师塔旁", RecoveryMultiplier = 1.8f },
                };
            }
        }

        public void OnLocationChanged(GameLocation location)
        {
            _isListening = false;
            _listenTicks = 0;
            _currentSpot = null;
        }

        public void OnUpdateTicked()
        {
            if (!ModEntry.Config.EnableWindListening || Game1.player == null) return;

            // Check if player is at a wind spot
            var spot = _spots.FirstOrDefault(s =>
                s.LocationName == Game1.currentLocation?.Name &&
                Math.Abs(Game1.player.TilePoint.X - s.TileX) <= 2 &&
                Math.Abs(Game1.player.TilePoint.Y - s.TileY) <= 2);

            if (spot == null)
            {
                if (_isListening) StopListening();
                return;
            }

            bool isStill = !Game1.player.isMoving() && Game1.player.CurrentTool == null;

            if (isStill && spot != _currentSpot)
            {
                _currentSpot = spot;
                _listenTicks++;
                if (_listenTicks >= _requiredTicks)
                {
                    StartListening(spot);
                }
            }
            else if (!isStill)
            {
                if (_isListening) StopListening();
                _currentSpot = null;
                _listenTicks = 0;
            }
            else if (isStill && spot == _currentSpot)
            {
                _listenTicks++;
                _enthusiasm.WindListenTick(spot.RecoveryMultiplier);
            }
        }

        private void StartListening(WindListeningSpot spot)
        {
            _isListening = true;

            if (ModEntry.Journal != null && Game1.player != null)
            {
                var entry = new Data.JournalEntry
                {
                    NpcName = "Linus",
                    DisplayText = $"山谷的风穿过{Game1.player.Name}的发丝。风告诉我他很轻——风很少这么说一个外来人。在{spot.DisplayName}，风的声音最为清晰。",
                    EmotionTag = "Wonder",
                    Trigger = TriggerType.Environmental,
                    DaysPlayed = (int)Game1.stats.DaysPlayed,
                    Season = Game1.currentSeason,
                    Year = Game1.year,
                    DayOfMonth = Game1.dayOfMonth,
                    TimeOfDay = Game1.timeOfDay,
                    LocationName = spot.LocationName,
                    Category = "WindListening"
                };
                ModEntry.Journal.AddEntry(entry);
            }

            if (ModEntry.Config.DebugMode)
                _monitor.Log($"[Wind] Player is listening at {spot.DisplayName}", LogLevel.Debug);
        }

        private void StopListening()
        {
            _isListening = false;
            _listenTicks = 0;
        }
    }
}
