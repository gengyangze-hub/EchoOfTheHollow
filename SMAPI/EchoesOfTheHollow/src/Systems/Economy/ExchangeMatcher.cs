using System;
using System.Collections.Generic;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.NpcProfiles;
using EchoesOfTheHollow.Utils;

namespace EchoesOfTheHollow.Systems.Economy
{
    /// <summary>
    /// 交换匹配器 — evaluates daily NPC exchange potential for basket items
    /// </summary>
    internal static class ExchangeMatcher
    {
        // NPC exchange preference table: what they like to receive → what they give
        private static readonly Dictionary<string, List<(string itemId, string responseItem, string responseText)>> NpcPreferences = new()
        {
            ["Abigail"] = new()
            {
                ("(O)amethyst", "(O)quartz", "我换走了紫水晶。这块石英长得像颗牙。"),
                ("(O)cloth", "(O)bat_wing", "谢谢你的布料。我用它裹了一块石头。"),
            },
            ["Linus"] = new()
            {
                ("(O)wild_plum", "(O)cave_carrot", "野莓很好吃。山洞萝卜是今早挖的，给你留了一根。"),
                ("(O)fiber", "(O)sap", "草绳我拿去补帐篷了。树液可以拿来做胶。"),
            },
            ["Emily"] = new()
            {
                ("(O)cloth", "(O)wool", "布料的颜色很美。这块羊毛刚染好，它喜欢你。"),
                ("(O)flower", "(O)flower", "花是今天最好的礼物。我把它放在枕头下了，送你一朵别的。"),
            },
            ["Gus"] = new()
            {
                ("(O)egg", "(O)bread", "鸡蛋很好。面包是早上烤的，还热着。"),
                ("(O)vegetable", "(O)coffee", "蔬菜新鲜。咖啡提神，给你一杯。"),
            },
            ["Clint"] = new()
            {
                ("(O)ore", "(O)coal", "矿石不错。煤是锻造剩下的，还能用。"),
                ("(O)stone", "(O)iron_bar", "石头是好材料。这块铁锭刚打好，给你。"),
            },
            ["Robin"] = new()
            {
                ("(O)wood", "(O)hardwood", "木料我收下了。这块硬木纹理很好，适合做点东西。"),
                ("(O)stone", "(O)wood", "石头我拿去砌墙了。木料是多余的，给你。"),
            },
        };

        private static readonly Dictionary<string, string> NpcReactTexts = new()
        {
            ["Abigail"] = "这个...我很喜欢。它让我想起了小时候在矿洞边捡到的东西。",
            ["Linus"] = "山谷让我把这个给你。不是交易，只是...循环。",
            ["Emily"] = "它的颜色在跳舞。希望你也喜欢我给你选的。",
            ["Gus"] = "谢谢你。我做菜的时候会多放一份的。",
            ["Clint"] = "收到。这个伤痕很美，我会留着。",
            ["Willy"] = "海风说有人会来。谢谢你，这个会是很好的等待伙伴。",
            ["Robin"] = "不错。我会用它给别人造东西。这个你留着。",
        };

        /// <summary>Generate any NPC's default preference based on their voice profile</summary>
        private static (string, string, string) GetDefaultPreference(string npcName, string givenItemId)
        {
            var voice = ModEntry.Voices?.GetProfile(npcName);

            // Default: NPC gives back a low-value item with a unique note
            string responseItem = npcName switch
            {
                "Willy" => "(O)fish",
                "Robin" => "(O)wood",
                "Gus" => "(O)bread",
                "Leah" => "(O)driftwood",
                _ => "(O)fiber"
            };

            string reactText = NpcReactTexts.ContainsKey(npcName)
                ? NpcReactTexts[npcName]
                : $"谢谢你放在篮子里的东西。这个给你，是我的一片心意。";

            return (responseItem, responseItem, reactText);
        }

        /// <summary>Evaluate whether any NPC will exchange with a basket item</summary>
        public static ExchangeResult Evaluate(BasketItem item)
        {
            // Check if this item matches any NPC's preferences
            float bestScore = 0;
            string? bestNpc = null;
            string? bestGiveItem = null;
            string? bestResponseText = null;

            foreach (var npcMeta in NpcPreferences)
            {
                string npcName = npcMeta.Key;

                // Skip if targeting specific NPC and this isn't them
                if (!string.IsNullOrEmpty(item.TargetNpcName) &&
                    npcName != item.TargetNpcName) continue;

                foreach (var (prefItem, responseItem, responseText) in npcMeta.Value)
                {
                    if (item.ItemId.Contains(prefItem, StringComparison.OrdinalIgnoreCase) ||
                        prefItem.Contains(item.ItemId, StringComparison.OrdinalIgnoreCase))
                    {
                        float score = 5f + (float)RandomHelper.NextDouble() * 3f;

                        // Bonus for matching quality
                        if (item.Quality > 0) score += item.Quality * 0.5f;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestNpc = npcName;
                            bestGiveItem = responseItem;
                            bestResponseText = responseText;
                        }
                    }
                }
            }

            // If no match found, give a small chance for any NPC to exchange anyway
            if (bestNpc == null && RandomHelper.Chance(0.3))
            {
                var npcs = new[] { "Linus", "Gus", "Emily", "Abigail", "Robin", "Willy", "Clint", "Marnie" };
                bestNpc = npcs[RandomHelper.Next(npcs.Length)];
                var (giveItem, _, responseText) = GetDefaultPreference(bestNpc, item.ItemId);
                bestGiveItem = giveItem;
                bestResponseText = responseText;
                bestScore = 2f;
            }

            if (bestNpc != null && bestScore > 0)
            {
                return new ExchangeResult
                {
                    IsMatched = true,
                    NpcName = bestNpc,
                    ReceivedItemId = bestGiveItem!,
                    ReceivedQuality = 0,
                    ReceivedCount = 1,
                    MatchScore = bestScore,
                    NpcResponseText = bestResponseText,
                    ExchangeNote = $"——{bestNpc}"
                };
            }

            return new ExchangeResult { IsMatched = false };
        }
    }
}
