using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using EchoesOfTheHollow.Systems.Economy;

namespace EchoesOfTheHollow.Patches
{
    /// <summary>
    /// Shop patches: zero price display during draw, restore after.
    /// Purchase interception for goodwill system.
    /// </summary>
    internal static class ShopPatches
    {
        [ThreadStatic]
        private static int _prePurchaseItemCount;
        private static readonly Dictionary<string, int> _originalPrices = new();

        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchShops) return;

            // Strategy: temp-zero prices during draw, restore after
            var drawMethod = AccessTools.Method(typeof(ShopMenu), "draw", new[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch) });
            if (drawMethod != null)
            {
                var drawPrefix = typeof(ShopPatches).GetMethod(nameof(Draw_Prefix));
                var drawPostfix = typeof(ShopPatches).GetMethod(nameof(Draw_Postfix));
                harmony.Patch(drawMethod, new HarmonyMethod(drawPrefix), new HarmonyMethod(drawPostfix));
            }

            // Purchase interception
            var receiveLeftClick = AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.receiveLeftClick));
            if (receiveLeftClick != null)
            {
                var prefix = typeof(ShopPatches).GetMethod(nameof(ReceiveLeftClick_Prefix));
                var postfix = typeof(ShopPatches).GetMethod(nameof(ReceiveLeftClick_Postfix));
                harmony.Patch(receiveLeftClick, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
            }
        }

        // ═══════════════════════════════════════════════
        //  TEMP-ZERO PRICES DURING DRAW
        // ═══════════════════════════════════════════════

        /// <summary>Get the price dictionary from ShopMenu, however it's stored</summary>
        private static IDictionary? GetPrices(ShopMenu shop)
        {
            var field = AccessTools.Field(typeof(ShopMenu), "itemPriceAndStock");
            return field?.GetValue(shop) as IDictionary;
        }

        public static void Draw_Prefix(ShopMenu __instance, out Dictionary<object, int[]>? __state)
        {
            __state = null;
            if (Game1.player == null) return;

            var prices = GetPrices(__instance);
            if (prices == null || prices.Count == 0) return;

            // Check currency — only gold shops
            var currencyField = AccessTools.Field(typeof(ShopMenu), "currency");
            int currency = currencyField?.GetValue(__instance) as int? ?? 0;
            if (currency != 0) return;

            // Save and zero
            __state = new Dictionary<object, int[]>();
            _originalPrices.Clear();
            foreach (DictionaryEntry kv in prices)
            {
                if (kv.Value is int[] arr && arr.Length > 0 && arr[0] > 0)
                {
                    __state[kv.Key] = (int[])arr.Clone();
                    string key = (kv.Key as ISalable)?.QualifiedItemId ?? kv.Key?.ToString() ?? "?";
                    _originalPrices[key] = arr[0];
                    arr[0] = 0;
                }
            }
        }

        public static void Draw_Postfix(ShopMenu __instance, Dictionary<object, int[]>? __state)
        {
            if (__state == null || __state.Count == 0) return;

            var prices = GetPrices(__instance);
            if (prices == null) return;

            // Restore original prices
            foreach (var kv in __state)
            {
                if (prices.Contains(kv.Key) && prices[kv.Key] is int[] arr)
                {
                    arr[0] = kv.Value[0];
                }
            }
        }

        // ═══════════════════════════════════════════════
        //  PURCHASE INTERCEPTION
        // ═══════════════════════════════════════════════

        public static void ReceiveLeftClick_Prefix(ShopMenu __instance)
        {
            if (!ModEntry.Config.EnableShopGoodwill || Game1.player == null) return;
            var currencyField = AccessTools.Field(typeof(ShopMenu), "currency");
            int currency = currencyField?.GetValue(__instance) as int? ?? 0;
            if (currency != 0) return;
            _prePurchaseItemCount = Game1.player.Items.Count(i => i != null);
        }

        public static void ReceiveLeftClick_Postfix(ShopMenu __instance)
        {
            if (!ModEntry.Config.EnableShopGoodwill || Game1.player == null) return;
            var currencyField = AccessTools.Field(typeof(ShopMenu), "currency");
            int currency = currencyField?.GetValue(__instance) as int? ?? 0;
            if (currency != 0) return;

            int postCount = Game1.player.Items.Count(i => i != null);
            int newItems = postCount - _prePurchaseItemCount;
            if (newItems <= 0) return;

            string? shopOwner = GoodwillShopSystem.GetShopOwner(__instance);
            var newInventoryItems = Game1.player.Items
                .Where(i => i != null)
                .OrderByDescending(i => Game1.player.Items.IndexOf(i))
                .Take(newItems).ToList();

            foreach (var item in newInventoryItems)
            {
                if (item == null) continue;
                int vanillaPrice = _originalPrices.TryGetValue(item.QualifiedItemId, out int p) ? p : 50;
                string? denyReason;
                if (!GoodwillShopSystem.TryPurchase(item, vanillaPrice, shopOwner, out denyReason))
                {
                    Game1.player.Items.Remove(item);
                    Game1.playSound("cancel");
                    if (denyReason != null)
                        Game1.addHUDMessage(new HUDMessage(denyReason, HUDMessage.error_type));
                    return;
                }
            }
        }
    }
}
