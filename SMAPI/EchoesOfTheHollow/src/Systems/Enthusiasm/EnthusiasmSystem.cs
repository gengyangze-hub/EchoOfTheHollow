using System;
using System.Collections.Generic;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.Utils;
using StardewModdingAPI;
using StardewValley;

namespace EchoesOfTheHollow.Systems.Enthusiasm
{
    /// <summary>
    /// 兴致系统 — replaces stamina/energy
    /// "Enthusiasm" decays with action repetition and recovers through
    /// daydreaming, wind listening, and activity variety.
    /// </summary>
    public class EnthusiasmSystem
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private EnthusiasmState _state = new();
        private int _lastUpdateTime;
        private const string SaveKey = "Enthusiasm/v1";

        public float CurrentValue => _state.CurrentValue;
        public bool IsDrained => _state.IsDrained;

        public EnthusiasmSystem(IModHelper helper, IMonitor monitor)
        {
            _helper = helper;
            _monitor = monitor;
        }

        /// <summary>Record a player action (farming, mining, etc.)</summary>
        public void RecordAction(string actionType)
        {
            if (_state.TodayActionTypes.Add(actionType))
                _state.UniqueActivitiesToday++;

            _state.TotalActionsToday++;

            // Decay enthusiasm based on repetition
            float decay = ModEntry.Config.EnthusiasmDecayRate;
            float repeatPenalty = _state.TotalActionsToday > 10
                ? 1.0f + (_state.TotalActionsToday - 10) * 0.05f
                : 1.0f;

            _state.CurrentValue = Math.Max(0, _state.CurrentValue - decay * repeatPenalty);
        }

        /// <summary>Recover enthusiasm through daydreaming</summary>
        public void DaydreamTick()
        {
            float recovery = ModEntry.Config.EnthusiasmDaydreamRecovery;
            _state.CurrentValue = Math.Min(1.0f, _state.CurrentValue + recovery);
        }

        /// <summary>Recover enthusiasm at wind listening spots</summary>
        public void WindListenTick(float multiplier = 1.0f)
        {
            float recovery = ModEntry.Config.EnthusiasmWindRecovery * multiplier;
            _state.CurrentValue = Math.Min(1.0f, _state.CurrentValue + recovery);
        }

        /// <summary>Called each update tick</summary>
        public void OnUpdateTicked()
        {
            // Passive recovery when idle
            if (!Game1.player.isMoving() && Game1.player.CurrentTool == null)
            {
                if (_state.TotalActionsToday > 0)
                {
                    _state.CurrentValue = Math.Min(1.0f,
                        _state.CurrentValue + 0.001f * _state.UniqueActivitiesToday);
                }
            }
        }

        /// <summary>Reset daily counters</summary>
        public void OnDayStarted()
        {
            _state.UniqueActivitiesToday = 0;
            _state.TotalActionsToday = 0;
            _state.TodayActionTypes.Clear();
            _state.CurrentValue = Math.Min(1.0f, _state.CurrentValue + 0.3f); // Morning refresh
        }

        public void OnDayEnding() { }

        // ── Save/Load ──

        public void OnSaveLoaded()
        {
            _state = ModDataHelper.Load<EnthusiasmState>(SaveKey) ?? new EnthusiasmState();
        }

        public void OnSaving()
        {
            ModDataHelper.Save(SaveKey, _state);
        }

        /// <summary>Get dashboard info for HUD</summary>
        public (float value, int uniqueActivities, int totalActions) GetDashboard()
        {
            return (_state.CurrentValue, _state.UniqueActivitiesToday, _state.TotalActionsToday);
        }
    }
}
