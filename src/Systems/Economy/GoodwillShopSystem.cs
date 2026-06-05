using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace EchoesOfTheHollow.Systems.Economy
{
    /// <summary>
    /// 好感商店系统 — replaces money with goodwill derived from friendship.
    /// Shop items are obtained through the Basket exchange paradigm:
    ///   1. You "request" an item from a shop (daily limit applies)
    ///   2. Goodwill cost = vanilla price × multiplier, paid from friendship with shop owner
    ///   3. If insufficient goodwill → denied; NPCs don't share with strangers
    ///   4. Goodwill regenerates naturally as friendship grows
    ///
    /// Shops mapped to NPCs: Pierre→Pierre, Robin→Robin, Clint→Clint,
    /// Marnie→Marnie, Willy→Willy, Gus→Gus, Harvey→Harvey,
    /// Sandy→Sandy, Krobus→Krobus, Dwarf→Dwarf, Wizard→Wizard
    /// Traveling Merchant → random pool of all NPCs
    /// </summary>
    internal static class GoodwillShopSystem
    {
        private static IMonitor? _monitor;
        private static int _todayRequestCount;
        private static int _todayDate;

        // Shop context → NPC owner mapping (by shop context or location)
        private static readonly Dictionary<string, string> ShopOwnerMap = new()
        {
            ["Pierre"] = "Pierre", ["Robin"] = "Robin", ["Clint"] = "Clint",
            ["Marnie"] = "Marnie", ["Willy"] = "Willy", ["Gus"] = "Gus",
            ["Harvey"] = "Harvey", ["Sandy"] = "Sandy", ["Krobus"] = "Krobus",
            ["Dwarf"] = "Dwarf", ["Wizard"] = "Wizard", ["Marlon"] = "Marlon",
            ["HatMouse"] = "Linus", // Hat Mouse is a friend of Linus
        };

        // NPC display name for the HUD message
        private static readonly Dictionary<string, string> ShopDisplayNames = new()
        {
            ["Pierre"] = "皮埃尔", ["Robin"] = "罗宾", ["Clint"] = "克林特",
            ["Marnie"] = "玛妮", ["Willy"] = "威利", ["Gus"] = "格斯",
            ["Harvey"] = "哈维", ["Sandy"] = "桑迪", ["Krobus"] = "科罗布斯",
            ["Dwarf"] = "矮人", ["Wizard"] = "法师", ["Marlon"] = "马龙",
        };

        public static void Initialize(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>Reset daily counter on day start</summary>
        public static void OnDayStarted()
        {
            _todayRequestCount = 0;
            _todayDate = Game1.year * 112 + Utility.getSeasonNumber(Game1.currentSeason) * 28 + Game1.dayOfMonth;
        }

        /// <summary>Get the NPC owner of the current shop</summary>
        public static string? GetShopOwner(ShopMenu? shop)
        {
            if (shop == null) return null;

            // Try to identify shop by its NPC owner
            string? shopId = null;

            // Check the shop's context (storeOwnerName, shopId, etc.)
            try
            {
                var contextField = AccessTools.Field(typeof(ShopMenu), "storeContext");
                shopId = contextField?.GetValue(shop) as string;
            }
            catch { }

            if (string.IsNullOrEmpty(shopId))
            {
                try
                {
                    var ownerField2 = AccessTools.Field(typeof(ShopMenu), "storeContext");
                    var ownerObj = ownerField2?.GetValue(shop);
                    if (ownerObj != null) shopId = ownerObj.ToString();
                }
                catch { }
            }

            // Fallback: try to identify by who the player is talking to
            if (string.IsNullOrEmpty(shopId) && Game1.player?.currentLocation != null)
            {
                // Check if there's an NPC nearby
                foreach (var npc in Game1.player.currentLocation.characters)
                {
                    if (npc is NPC shopkeeper && shopkeeper.Name != Game1.player.Name)
                    {
                        if (ShopOwnerMap.ContainsKey(shopkeeper.Name))
                            return shopkeeper.Name;
                    }
                }
            }

            // Map shop ID to NPC name
            if (!string.IsNullOrEmpty(shopId) && ShopOwnerMap.TryGetValue(shopId, out string? owner))
                return owner;

            // Default: try common shop names
            if (!string.IsNullOrEmpty(shopId))
            {
                foreach (var kv in ShopOwnerMap)
                    if (shopId.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                        return kv.Value;
            }

            return "Linus"; // Fallback: the kindest stranger
        }

        /// <summary>Calculate available goodwill with an NPC</summary>
        public static int GetGoodwill(string npcName)
        {
            if (Game1.player == null) return 0;

            try
            {
                if (Game1.player.friendshipData.TryGetValue(npcName, out Friendship? friendship))
                {
                    // Friendship points (0 to 2500+ for 0 to 10+ hearts)
                    return friendship.Points;
                }
            }
            catch { }

            // No friendship data → NPC is not a social NPC (e.g., Marlon).
            // Return a high value so these shops work without goodwill barriers.
            // The player still needs gold (which is tracked silently).
            return int.MaxValue;
        }

        /// <summary>Calculate goodwill cost for an item based on its vanilla price</summary>
        public static int GetGoodwillCost(ISalable item, int vanillaPrice, string npcOwner)
        {
            float multiplier = ModEntry.Config.GoodwillCostMultiplier;
            int baseCost = (int)(vanillaPrice * multiplier);

            // Minimum cost of 1 goodwill per item
            if (baseCost < 1) baseCost = 1;

            // Stack size affects cost
            if (item.Stack > 1)
                baseCost = (int)(baseCost * (1 + (item.Stack - 1) * 0.5f));

            // Cap at reasonable maximum
            if (baseCost > 500) baseCost = 500;

            return baseCost;
        }

        /// <summary>Try to purchase an item from a shop. Returns true if allowed.</summary>
        public static bool TryPurchase(ISalable item, int vanillaPrice, string? npcOwner, out string? denyReason)
        {
            denyReason = null;

            if (!ModEntry.Config.EnableShopGoodwill)
                return true; // Goodwill system disabled — free economy mode

            // ── Check daily limit ──
            if (_todayRequestCount >= ModEntry.Config.MaxDailyShopRequests)
            {
                denyReason = $"今天已经请求了{ModEntry.Config.MaxDailyShopRequests}件物品。明天再来吧。";
                return false;
            }

            // ── Calculate cost ──
            string owner = npcOwner ?? "Linus";
            int goodwillCost = GetGoodwillCost(item, vanillaPrice, owner);
            int available = GetGoodwill(owner);

            if (available < goodwillCost)
            {
                string displayName = ShopDisplayNames.TryGetValue(owner, out string? name) ? name : owner;
                string tierDesc = DescribeFriendship(available);
                string neededDesc = DescribeFriendship(goodwillCost);
                denyReason = $"{displayName}{tierDesc}。\n要想换到这件东西，你们的关系需要到{DescribeFriendshipThreshold(goodwillCost)}的程度。\n多一些相处，少一些交换——慢慢来。";
                return false;
            }

            // ── HUD message: narrative exchange confirmation ──
            string shopName = ShopDisplayNames.TryGetValue(owner, out string? dn) ? dn : owner;
            string verb = GetExchangeVerb(available);
            Game1.addHUDMessage(new HUDMessage(
                $"{shopName}{verb}，把{item.DisplayName}递给了你。",
                HUDMessage.newQuest_type));

            _todayRequestCount++;

            // ── Actually deduct goodwill (friendship points) ──
            if (Game1.player != null && Game1.player.friendshipData.TryGetValue(owner, out Friendship? f))
            {
                f.Points = Math.Max(0, f.Points - goodwillCost);
            }

            if (ModEntry.Config.DebugMode)
                _monitor?.Log($"[Goodwill] Purchase: {item.DisplayName} ×{item.Stack} from {owner} for {goodwillCost} goodwill (was: {available})", LogLevel.Debug);

            return true;
        }

        /// <summary>Check remaining daily requests for display</summary>
        public static int RemainingRequests =>
            Math.Max(0, ModEntry.Config.MaxDailyShopRequests - _todayRequestCount);

        // ═══════════════════════════════════════════════
        //  Narrative descriptions — no numbers visible
        // ═══════════════════════════════════════════════

        /// <summary>Describe a friendship level in narrative terms</summary>
        private static string DescribeFriendship(int points) => points switch
        {
            < 100 => "对你还很陌生",
            < 250 => "对你略有印象",
            < 500 => "开始认识你了",
            < 750 => "和你有些交情",
            < 1000 => "对你印象不错",
            < 1250 => "和你是好朋友",
            < 1500 => "和你非常亲近",
            < 2000 => "对你信任有加",
            _ => "与你心照不宣"
        };

        /// <summary>Describe the needed friendship threshold for an item</summary>
        private static string DescribeFriendshipThreshold(int cost) => cost switch
        {
            <= 1 => "点头之交",
            <= 3 => "偶尔打招呼",
            <= 5 => "能聊上几句",
            <= 8 => "互相比较了解",
            <= 15 => "彼此信任",
            <= 30 => "非常好的朋友",
            <= 80 => "重要的羁绊",
            _ => "很深很深的联结"
        };

        /// <summary>Get a narrative verb for how the NPC gives the item</summary>
        private static string GetExchangeVerb(int friendship) => friendship switch
        {
            < 250 => "犹豫了一下",
            < 500 => "点了点头",
            < 1000 => "笑了笑",
            < 1500 => "自然地",
            < 2000 => "开心地",
            _ => "二话不说"
        };
    }
}
