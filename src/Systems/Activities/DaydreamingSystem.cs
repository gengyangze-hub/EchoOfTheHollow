using System;
using System.Collections.Generic;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.Systems.Enthusiasm;
using EchoesOfTheHollow.Utils;
using StardewModdingAPI;
using StardewValley;
using Microsoft.Xna.Framework;

namespace EchoesOfTheHollow.Systems.Activities
{
    /// <summary>
    /// 发呆系统 -- hold a position to auto-sit, camera slowly zooms out
    /// </summary>
    public class DaydreamingSystem
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly EnthusiasmSystem _enthusiasm;

        private bool _isDaydreaming;
        private int _holdTicks;
        private int _requiredTicks = 120; // ~2 seconds
        private Vector2 _daydreamPosition;

        public bool IsDaydreaming => _isDaydreaming;

        public DaydreamingSystem(IModHelper helper, IMonitor monitor, EnthusiasmSystem enthusiasm)
        {
            _helper = helper;
            _monitor = monitor;
            _enthusiasm = enthusiasm;
        }

        public void OnButtonPressed(SButton button)
        {
            if (!ModEntry.Config.EnableDaydreaming) return;

            // Start daydreaming when player holds still
        }

        public void OnUpdateTicked()
        {
            if (!ModEntry.Config.EnableDaydreaming || Game1.player == null || !Context.IsWorldReady) return;

            bool isStill = !Game1.player.isMoving() && Game1.player.CurrentTool == null;

            if (isStill && !_isDaydreaming)
            {
                _holdTicks++;
                if (_holdTicks >= _requiredTicks)
                {
                    StartDaydreaming();
                }
            }
            else if (!isStill && _isDaydreaming)
            {
                StopDaydreaming();
            }

            if (_isDaydreaming)
            {
                _enthusiasm.DaydreamTick();
            }
        }

        private void StartDaydreaming()
        {
            _isDaydreaming = true;
            _daydreamPosition = Game1.player!.Position;

            // Generate a journal entry from an observer NPC
            var parms = new Dictionary<string, string>
            {
                ["playerName"] = Game1.player.Name,
                ["location"] = Game1.currentLocation?.Name ?? "unknown",
                ["timeOfDay"] = StringHelper.TimeOfDayToPeriod(Game1.timeOfDay)
            };

            // Observant NPCs might notice
            string observer = new[] { "Linus", "Abigail", "Leah", "Emily" }[RandomHelper.Next(4)];

            if (ModEntry.Journal != null)
            {
                var entry = new Data.JournalEntry
                {
                    NpcName = observer,
                    DisplayText = $"看到{Game1.player.Name}在{parms["location"]}停下脚步，静静站着。他们望着远方，好像在看什么不存在的东西。有时候，发呆也是一种重要的事。",
                    EmotionTag = "Warm",
                    Trigger = TriggerType.Daydream,
                    DaysPlayed = (int)Game1.stats.DaysPlayed,
                    Season = Game1.currentSeason,
                    Year = Game1.year,
                    DayOfMonth = Game1.dayOfMonth,
                    TimeOfDay = Game1.timeOfDay,
                    LocationName = Game1.currentLocation?.Name ?? "",
                    Category = "Daydream"
                };
                ModEntry.Journal.AddEntry(entry);
            }

            if (ModEntry.Config.DebugMode)
                _monitor.Log($"[Daydream] Player is daydreaming at {Game1.currentLocation?.Name}", LogLevel.Debug);
        }

        private void StopDaydreaming()
        {
            _isDaydreaming = false;
            _holdTicks = 0;
        }
    }
}
