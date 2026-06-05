using System;
using System.Collections.Generic;

namespace EchoesOfTheHollow.Utils
{
    /// <summary>
    /// Scheduler for deferred and delayed action execution.
    /// Actions execute on the next UpdateTicked when their delay expires.
    /// </summary>
    internal class Scheduler
    {
        private readonly List<ScheduledAction> _actions = new();
        private readonly object _lock = new();

        /// <summary>Schedule an action to run after a delay (in-game minutes)</summary>
        public void After(int gameMinutes, Action action, string? label = null)
        {
            lock (_lock)
            {
                _actions.Add(new ScheduledAction
                {
                    RemainingMinutes = gameMinutes,
                    Action = action,
                    Label = label
                });
            }
        }

        /// <summary>Schedule an action to run on next tick</summary>
        public void NextTick(Action action)
        {
            After(0, action);
        }

        /// <summary>Process scheduled actions — call from UpdateTicked</summary>
        public void Update(int elapsedGameMinutes)
        {
            lock (_lock)
            {
                for (int i = _actions.Count - 1; i >= 0; i--)
                {
                    _actions[i].RemainingMinutes -= elapsedGameMinutes;
                    if (_actions[i].RemainingMinutes <= 0)
                    {
                        try
                        {
                            _actions[i].Action?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Scheduler action '{_actions[i].Label}' failed: {ex.Message}");
                        }
                        _actions.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>Clear all pending actions</summary>
        public void Clear()
        {
            lock (_lock) { _actions.Clear(); }
        }

        public int PendingCount { get { lock (_lock) return _actions.Count; } }

        private class ScheduledAction
        {
            public int RemainingMinutes;
            public Action? Action;
            public string? Label;
        }
    }
}
