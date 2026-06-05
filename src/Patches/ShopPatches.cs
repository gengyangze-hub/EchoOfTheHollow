using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using EchoesOfTheHollow.Systems.Economy;

namespace EchoesOfTheHollow.Patches
{
    /// <summary>
    /// Shop patches for Echoes of the Hollow:
    /// - Zeroes gold-shop price DISPLAY (currency stays 0 — vanilla code processes purchases normally with 0g)
    /// - Intercepts purchases to enforce the goodwill system
    /// - Barter shops (Desert Trader) and non-gold currency (Qi coins, casino) are left untouched
    /// - Daily request limit per player
    /// </summary>
    internal static class ShopPatches
    {
        [ThreadStatic]
        private static int _prePurchaseItemCount;

        // Store original prices (item -> price) since we zero the display
        private static readonly Dictionary<string, int> _originalPrices = new();

        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchShops) return;

            // Patch ShopMenu constructors to zero out price display
            var shopCtor = AccessTools.Constructor(typeof(ShopMenu), new[]
                { typeof(Dictionary<ISalable, int[]>), typeof(int), typeof(string),
                  typeof(Func<ISalable, Farmer, int, bool>),
                  typeof(Func<ISalable, Farmer, int, bool>), typeof(string) });
            if (shopCtor != null)
            {
                var postfix = typeof(ShopPatches).GetMethod(nameof(ShopMenu_Constructor_Postfix));
                harmony.Patch(shopCtor, null, new HarmonyMethod(postfix));
            }

            var shopCtor2 = AccessTools.Constructor(typeof(ShopMenu), new[] { typeof(string) });
            if (shopCtor2 != null)
            {
                var postfix2 = typeof(ShopPatches).GetMethod(nameof(ShopMenu_ShopId_Postfix));
                harmony.Patch(shopCtor2, null, new HarmonyMethod(postfix2));
            }

            // ── GOODWILL PURCHASE INTERCEPTION ──
            var receiveLeftClick = AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.receiveLeftClick));
            if (receiveLeftClick != null)
            {
                var prefix = typeof(ShopPatches).GetMethod(nameof(ReceiveLeftClick_Prefix));
                var postfix = typeof(ShopPatches).GetMethod(nameof(ReceiveLeftClick_Postfix));
                harmony.Patch(receiveLeftClick, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
            }
        }

        // ═══════════════════════════════════════════════
        //  PRICE DISPLAY ZEROING (currency stays 0!)
        // ═══════════════════════════════════════════════

        public static void ShopMenu_Constructor_Postfix(ShopMenu __instance) => ZeroShopPrices(__instance);
        public static void ShopMenu_ShopId_Postfix(ShopMenu __instance) => ZeroShopPrices(__instance);

        private static void ZeroShopPrices(ShopMenu shop)
        {
            try
            {
                // ── Only process gold-currency shops ──
                var currencyField = AccessTools.Field(typeof(ShopMenu), "currency");
                int currency = currencyField?.GetValue(shop) as int? ?? 0;
                if (currency != 0) return; // Skip Qi coins, casino tokens, etc.

                var priceField = AccessTools.Field(typeof(ShopMenu), "itemPriceAndStock");
                if (priceField == null) return;
                var prices = priceField.GetValue(shop) as Dictionary<ISalable, int[]>;
                if (prices == null) return;

                // ── Detect barter shops (negative prices = barter item IDs) ──
                bool hasBarter = prices.Values.Any(v => v != null && v.Length > 0 && v[0] < 0);
                if (hasBarter) return; // Desert Trader — leave untouched

                // ── Store original prices and zero the display ──
                _originalPrices.Clear();
                foreach (var kv in prices)
                {
                    if (kv.Value != null && kv.Value.Length > 0 && kv.Value[0] > 0)
                    {
                        string key = kv.Key.QualifiedItemId;
                        _originalPrices[key] = kv.Value[0];  // Save original price
                        kv.Value[0] = 0;                      // Display zero
                        // Position 1 (stock) is LEFT UNTOUCHED
                    }
                }

                // IMPORTANT: Do NOT set currency to -1.
                // Currency stays 0 — vanilla code processes 0g purchase normally.
                // Goodwill validation happens in the postfix below.
            }
            catch (Exception ex)
            {
                ModEntry.StaticMonitor?.Log($"[ShopPatches] ZeroShopPrices error: {ex.Message}", StardewModdingAPI.LogLevel.Debug);
            }
        }

        // ═══════════════════════════════════════════════
        //  GOODWILL PURCHASE INTERCEPTION
        // ═══════════════════════════════════════════════

        /// <summary>Snapshot inventory before a shop click</summary>
        public static void ReceiveLeftClick_Prefix(ShopMenu __instance)
        {
            if (!ModEntry.Config.EnableShopGoodwill || Game1.player == null) return;

            // Only validate gold-currency shops
            var currencyField = AccessTools.Field(typeof(ShopMenu), "currency");
            int currency = currencyField?.GetValue(__instance) as int? ?? 0;
            if (currency != 0) return;

            // Skip barter shops
            var priceField = AccessTools.Field(typeof(ShopMenu), "itemPriceAndStock");
            var prices = priceField?.GetValue(__instance) as Dictionary<ISalable, int[]>;
            if (prices != null && prices.Values.Any(v => v != null && v.Length > 0 && v[0] < 0))
                return;

            _prePurchaseItemCount = Game1.player.Items.Count(i => i != null);
        }

        /// <summary>Check if a purchase happened, and if so validate goodwill</summary>
        public static void ReceiveLeftClick_Postfix(ShopMenu __instance)
        {
            if (!ModEntry.Config.EnableShopGoodwill || Game1.player == null) return;

            // Only validate gold-currency shops
            var currencyField = AccessTools.Field(typeof(ShopMenu), "currency");
            int currency = currencyField?.GetValue(__instance) as int? ?? 0;
            if (currency != 0) return;

            // Skip barter shops
            var priceField = AccessTools.Field(typeof(ShopMenu), "itemPriceAndStock");
            var prices = priceField?.GetValue(__instance) as Dictionary<ISalable, int[]>;
            if (prices != null && prices.Values.Any(v => v != null && v.Length > 0 && v[0] < 0))
                return;

            // Did a new item slot appear?
            int postCount = Game1.player.Items.Count(i => i != null);
            int newItems = postCount - _prePurchaseItemCount;
            if (newItems <= 0) return;

            // ── Goodwill validation ──
            string? shopOwner = GoodwillShopSystem.GetShopOwner(__instance);

            // Get the newest items in inventory
            var newInventoryItems = Game1.player.Items
                .Where(i => i != null)
                .OrderByDescending(i => Game1.player.Items.IndexOf(i))
                .Take(newItems)
                .ToList();

            foreach (var item in newInventoryItems)
            {
                if (item == null) continue;

                // Get original vanilla price from our backup
                int vanillaPrice = _originalPrices.TryGetValue(item.QualifiedItemId, out int p) ? p : 50;

                string? denyReason;
                if (!GoodwillShopSystem.TryPurchase(item, vanillaPrice, shopOwner, out denyReason))
                {
                    // DENIED — take the item back
                    Game1.player.Items.Remove(item);
                    Game1.playSound("cancel");

                    if (denyReason != null)
                    {
                        Game1.addHUDMessage(new HUDMessage(denyReason, HUDMessage.error_type));
                    }
                    return;
                }
            }
        }
    }
}
