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
    /// 互惠篮系统 — 替代金币经济
    /// Mutual Aid Basket — replaces the gold economy.
    /// Players deposit items; NPCs may exchange them for other items.
    /// </summary>
    public class BasketSystem
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly JournalSystem _journal;
        private List<BasketItem> _items = new();
        private const string SaveKey = "Basket/v1";

        public int PendingCount => _items.Count(i => i.Status == ExchangeStatus.Pending);

        public BasketSystem(IModHelper helper, IMonitor monitor, JournalSystem journal)
        {
            _helper = helper;
            _monitor = monitor;
            _journal = journal;
        }

        /// <summary>Player deposits an item in the basket</summary>
        public bool DepositItem(string itemId, int quality, int count, string? requestNote = null,
            string? requestItemId = null, string? targetNpc = null)
        {
            // Check if we have this item to give
            if (Game1.player == null) return false;

            var item = Game1.player.Items.FirstOrDefault(i =>
                i != null && i.QualifiedItemId == itemId && i.Quality == quality);

            if (item == null || item.Stack < count) return false;

            // Remove from inventory (SDV 1.6)
            item.Stack -= count;
            if (item.Stack <= 0)
                Game1.player.Items.Remove(item);

            var basketItem = new BasketItem
            {
                ItemId = itemId,
                Quality = quality,
                Count = count,
                RequestNote = requestNote,
                RequestedItemId = requestItemId,
                TargetNpcName = targetNpc,
                DepositedDay = (int)Game1.stats.DaysPlayed,
                DaysUntilReturn = ModEntry.Config.BasketReturnDays
            };

            _items.Add(basketItem);
            _monitor.Log($"[Basket] Deposited: {itemId} x{count} (quality {quality})", LogLevel.Info);

            return true;
        }

        /// <summary>Process daily exchanges — called on DayStarted</summary>
        public void ProcessDailyExchanges()
        {
            if (Game1.player == null) return;

            var pendingItems = _items.Where(i => i.Status == ExchangeStatus.Pending).ToList();

            foreach (var item in pendingItems)
            {
                item.DaysUntilReturn--;

                if (item.DaysUntilReturn <= 0)
                {
                    // Time's up — return the item
                    ReturnItem(item);
                    continue;
                }

                // Attempt to find a match
                var result = ExchangeMatcher.Evaluate(item);

                if (result.IsMatched)
                {
                    CompleteExchange(item, result);
                }
            }
        }

        private void CompleteExchange(BasketItem item, ExchangeResult result)
        {
            item.Status = ExchangeStatus.Matched;
            item.MatchedNpcName = result.NpcName;
            item.ReceivedItemId = result.ReceivedItemId;
            item.ReceivedItemQuality = result.ReceivedQuality;
            item.ReceivedItemCount = result.ReceivedCount;
            item.ExchangeNote = result.ExchangeNote;

            // Give the received item to the player
            if (!string.IsNullOrEmpty(result.ReceivedItemId) && Game1.player != null)
            {
                try
                {
                    var receivedItem = ItemRegistry.Create(result.ReceivedItemId, result.ReceivedCount, result.ReceivedQuality);
                    if (receivedItem != null)
                    {
                        Game1.player.addItemToInventoryBool(receivedItem);
                        _monitor.Log($"[Basket] Exchange: {item.ItemId} → {result.ReceivedItemId} (with {result.NpcName})", LogLevel.Info);

                        // Generate journal entry
                        var parms = new Dictionary<string, string>
                        {
                            ["playerName"] = Game1.player.Name,
                            ["npcName"] = result.NpcName,
                            ["givenItem"] = item.ItemId,
                            ["receivedItem"] = result.ReceivedItemId,
                            ["note"] = result.ExchangeNote ?? ""
                        };

                        var entry = new Data.JournalEntry
                        {
                            NpcName = result.NpcName,
                            DisplayText = result.NpcResponseText ?? $"在互惠篮里收到了{item.ItemId}，{result.NpcName}留下了{result.ReceivedItemId}和一张纸条。",
                            EmotionTag = "Warm",
                            Trigger = TriggerType.BasketExchange,
                            DaysPlayed = (int)Game1.stats.DaysPlayed,
                            Season = Game1.currentSeason,
                            Year = Game1.year,
                            DayOfMonth = Game1.dayOfMonth,
                            TimeOfDay = Game1.timeOfDay,
                            Category = "Exchange",
                            Parameters = parms
                        };
                        _journal.AddEntry(entry);
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[Basket] Failed to create received item: {ex.Message}", LogLevel.Error);
                    ReturnItem(item);
                }
            }
        }

        private void ReturnItem(BasketItem item)
        {
            item.Status = ExchangeStatus.Returned;

            if (Game1.player != null)
            {
                try
                {
                    var returnItem = ItemRegistry.Create(item.ItemId, item.Count, item.Quality);
                    if (returnItem != null)
                    {
                        Game1.player.addItemToInventoryBool(returnItem);
                        _monitor.Log($"[Basket] Returned: {item.ItemId} (no match in {ModEntry.Config.BasketReturnDays} days)", LogLevel.Debug);

                        // System message
                        Game1.addHUDMessage(new HUDMessage("互惠篮里有东西被退回了，篮子空了。也许明天会有人需要它。", HUDMessage.newQuest_type));
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[Basket] Failed to return item: {ex.Message}", LogLevel.Error);
                }
            }
        }

        public void OnInteract()
        {
            // Opens the basket UI — handled by UI layer
            if (Game1.player?.currentLocation?.Name == "Town" && Context.IsPlayerFree)
            {
                ModEntry.OpenBasketMenu();
            }
        }

        // ── Save/Load ──

        public void OnSaveLoaded()
        {
            _items = ModDataHelper.LoadList<BasketItem>(SaveKey);
        }

        public void OnSaving()
        {
            ModDataHelper.SaveList(SaveKey, _items);
        }

        public void OnDayStarted()
        {
            ProcessDailyExchanges();
        }

        public void OnDayEnding() { }

        public List<BasketItem> GetPendingItems()
        {
            return _items.Where(i => i.Status == ExchangeStatus.Pending).ToList();
        }

        public List<BasketItem> GetAllItems()
        {
            return _items.ToList();
        }
    }
}
