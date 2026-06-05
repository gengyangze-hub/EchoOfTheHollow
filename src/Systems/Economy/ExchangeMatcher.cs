using System;
using System.Collections.Generic;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.NpcProfiles;
using EchoesOfTheHollow.Utils;

namespace EchoesOfTheHollow.Systems.Economy
{
    /// <summary>
    /// 交换匹配器 — evaluates daily NPC exchange potential for basket items.
    /// Each NPC has unique preferences based on their personality and voice profile.
    /// </summary>
    internal static class ExchangeMatcher
    {
        // NPC exchange preference table: what they like to receive → what they give → their response
        private static readonly Dictionary<string, List<(string itemId, string responseItem, string responseText)>> NpcPreferences = new()
        {
            // ── Abigail ──
            ["Abigail"] = new()
            {
                ("(O)amethyst", "(O)quartz", "我换走了紫水晶。这块石英长得像颗牙，我给它起了名字。"),
                ("(O)cloth", "(O)bat_wing", "谢谢你的布料。我用它裹了一块最喜欢的石头。它是活的，我敢肯定。"),
                ("(O)pumpkin", "(O)void_essence", "南瓜我收了，放在床头当灯笼。虚无精华是矿洞里找到的，它让我想起你。"),
            },
            // ── Alex ──
            ["Alex"] = new()
            {
                ("(O)egg", "(O)field_snack", "两个跑者之间的交换。能量棒给你——训练加油。"),
                ("(O)complete_breakfast", "(O)protein_bar", "哇，丰盛早餐！给你我的蛋白质棒，互相喂饱。"),
            },
            // ── Caroline ──
            ["Caroline"] = new()
            {
                ("(O)tea_leaves", "(O)green_tea", "茶叶很好。这是我自己泡的绿茶，柳树下摘的叶子。午后一个人喝正好。"),
                ("(O)daffodil", "(O)blue_jazz", "黄水仙让人心情明亮。蓝色爵士是我在花盆里种的，它需要一个新主人。"),
            },
            // ── Clint ──
            ["Clint"] = new()
            {
                ("(O)ore", "(O)coal", "矿石不错。煤是锻造剩下的，还能用很久。"),
                ("(O)stone", "(O)iron_bar", "石头是好材料。这块铁锭刚打好，给你。"),
                ("(O)copper_bar", "(O)copper_ore", "铜锭我回炉加工了。原矿你留着，也许能敲出什么东西。"),
            },
            // ── Demetrius ──
            ["Demetrius"] = new()
            {
                ("(O)strawberry", "(O)salmonberry", "草莓的生长曲线很有意思。树莓是野外采的，自然生长的果实和人工栽培的不同。"),
                ("(O)mushroom", "(O)cave_carrot", "蘑菇标本我收下了。这棵山洞萝卜的根系结构很特别，给你研究。"),
            },
            // ── Elliott ──
            ["Elliott"] = new()
            {
                ("(O)squid_ink", "(O)cloth", "墨汁——写作的血液。这块布料是我在海边捡的，被海水漂成了这种颜色。它一直在等待一个故事。"),
                ("(O)pomegranate", "(O)feast", "石榴，粒粒如红宝石。这一页是我刚写完的手稿——你是第一个读到它的人。"),
            },
            // ── Emily ──
            ["Emily"] = new()
            {
                ("(O)cloth", "(O)wool", "布料的颜色像秋天最后一片叶子。这块羊毛刚染好，它告诉我它很喜欢你。"),
                ("(O)flower", "(O)fairy_rose", "花是今天最温柔的礼物。我把她放在枕头下了。送你一朵仙灵玫瑰，它的光会陪着你。"),
                ("(O)aquamarine", "(O)amethyst", "海蓝宝石很清澈。紫水晶借给你——它最近梦到了你。"),
            },
            // ── Evelyn ──
            ["Evelyn"] = new()
            {
                ("(O)tulip", "(O)cookie", "郁金香真美。小甜饼是刚烤的，放了葡萄干。拿去吃，不要客气。"),
                ("(O)beet", "(O)cranberry_sauce", "甜菜很新鲜。蔓越莓酱是我秋天做的，乔治不喜欢太甜的，我觉得你可能会喜欢。"),
            },
            // ── George ──
            ["George"] = new()
            {
                ("(O)leek", "(O)fried_mushroom", "野韭。炒蘑菇是我做的，也许没我老伴做的好吃，但比没东西好。"),
                ("(O)stone", "(O)geode", "石头？好吧。这晶洞是我在矿坑边捡的，我砸不开——也许你能。"),
            },
            // ── Gus ──
            ["Gus"] = new()
            {
                ("(O)egg", "(O)bread", "鸡蛋很好。面包是早上烤的，等他们来吃饭时正好热着。这半条给你。"),
                ("(O)vegetable", "(O)coffee", "蔬菜新鲜，今晚的汤里会用到。咖啡给帮手提神——别喝太晚。"),
                ("(O)milk", "(O)cheese", "牛奶很香浓。奶酪是上周做的，干得刚好，配什么都行。"),
            },
            // ── Haley ──
            ["Haley"] = new()
            {
                ("(O)sunflower", "(O)coconut", "向日葵确实很美。椰子是我不小心逛到沙漠时买的——在那个地方你是我唯一可能会想起的人。"),
                ("(O)pink_cake", "(O)sunflower_seeds", "粉红蛋糕！你是最好的。葵花籽给你——种下去，春天会有花的。"),
            },
            // ── Harvey ──
            ["Harvey"] = new()
            {
                ("(O)coffee", "(O)pickles", "咖啡——医生的燃料。腌菜是我闲暇时做的，少放盐多放了醋。"),
                ("(O)truffle_oil", "(O)energy_tonic", "松露油很精致。精力剂给你——注意休息，不要太勉强自己。"),
            },
            // ── Jas ──
            ["Jas"] = new()
            {
                ("(O)fairy_rose", "(O)clay", "仙灵玫瑰！我把它放在枕头下了。黏土是我在河边挖到的——老师说它可以做陶器。"),
                ("(O)doll", "(O)toy", "玩偶！我给它起了个名字——不告诉你。送你另一个玩具，它一直想认识新朋友。"),
            },
            // ── Jodi ──
            ["Jodi"] = new()
            {
                ("(O)pancakes", "(O)crispy_bass", "煎饼做得不错。脆皮鲈鱼是我昨天煎的，山姆没吃完。别饿着。"),
                ("(O)eggplant", "(O)vegetable_medley", "茄子很饱满。蔬菜杂烩是我做多了的——家里两个孩子，习惯多做一份。"),
            },
            // ── Kent ──
            ["Kent"] = new()
            {
                ("(O)hazelnut", "(O)torch", "榛子。从前在营地，战友们会捡这种坚果烤来吃。火把给你——光线总是好东西。"),
                ("(O)roasted_hazelnuts", "(O)war_memento", "烤榛子...味道和那时候一样。这个纪念品是一个沉默的老兵留给我的——我不想它蒙灰了。"),
            },
            // ── Leah ──
            ["Leah"] = new()
            {
                ("(O)driftwood", "(O)wood_sketch", "漂流木的纹路真好。我在上面找到了三张脸的轮廓。给你一幅我的木刻画——它花了整个冬天的晚上。"),
                ("(O)salad", "(O)salmonberry", "沙拉很新鲜。野莓是我在林子里摘的，没洗——但雨水洗过的东西最干净。"),
                ("(O)truffle", "(O)winter_root", "松露！你怎么找到的。冬根给你——挖的时候我顺便想的。"),
            },
            // ── Lewis ──
            ["Lewis"] = new()
            {
                ("(O)blueberry", "(O)autumns_bounty", "蓝莓很甜。秋天的馈赠是我自己做的——作为镇长，我觉得应该以身作则。"),
                ("(O)hot_pepper", "(O)pumpkin_soup", "辣椒不错。南瓜汤是玛妮做的，我喝不完。她总是做太多。"),
            },
            // ── Linus ──
            ["Linus"] = new()
            {
                ("(O)wild_plum", "(O)cave_carrot", "野莓很好吃。这块山洞萝卜是今早挖的——它长在一棵松树下面，阳光刚好照到。给你。"),
                ("(O)fiber", "(O)sap", "草绳我拿去补帐篷了。树液可以拿来做胶，也可以涂在手上——冬天不容易裂开。"),
                ("(O)forage_item", "(O)spring_onion", "谢谢你。山谷让我把这个给你——不是交易，只是...循环。"),
            },
            // ── Marnie ──
            ["Marnie"] = new()
            {
                ("(O)hay", "(O)milk", "干草很干爽。牛奶是今早挤的——那头花斑奶牛心情很好，奶特别甜。"),
                ("(O)egg", "(O)mayonnaise", "鸡蛋个头很大，鸡一定很开心。蛋黄酱是我自己做的，比皮埃尔店里卖的新鲜。"),
                ("(O)cheese", "(O)large_goat_milk", "奶酪发酵得刚好。山羊奶送给你——山羊叫了一声，我想它是同意的。"),
            },
            // ── Maru ──
            ["Maru"] = new()
            {
                ("(O)battery_pack", "(O)iron_bar", "电池！我正好需要这个给望远镜供电。铁锭是我熔的——它一直想被做成什么东西。"),
                ("(O)gold_bar", "(O)refined_quartz", "金条的导电性很好。精炼石英给你——做一个太阳能板试试？"),
                ("(O)strawberry", "(O)pepper_poppers", "草莓很好吃。辣椒爆米花是下午做的实验——辣度经过精密计算，刚好够。"),
            },
            // ── Pam ──
            ["Pam"] = new()
            {
                ("(O)pale_ale", "(O)parsnip", "淡啤酒不错。欧洲防风是自己种的——篱笆边那块地，没怎么管，它自己长。"),
                ("(O)mead", "(O)potato", "蜂蜜酒很烈。土豆是上周挖的——放这么久，皮都皱了。炖着吃还行。"),
                ("(O)beer", "(O)cauliflower", "啤酒干了。花椰菜是别人给的——我不吃蔬菜，给你。"),
            },
            // ── Penny ──
            ["Penny"] = new()
            {
                ("(O)poppy", "(O)book", "荷包花——我在一本书上读过，它代表安慰。这本书刚读完——讲的是一个旅人学会了暂停。"),
                ("(O)melon", "(O)sandfish", "西瓜的清甜很适合夏天。沙鱼是孩子们画给我的——他们画的东西有时候比真的更让人高兴。"),
                ("(O)diamond", "(O)emerald", "钻石太贵重了。祖母绿给你——它是我在旧书摊上换来的，书商说它曾是某个人写日记时放在桌上的石头。"),
            },
            // ── Pierre ──
            ["Pierre"] = new()
            {
                ("(O)sashimi", "(O)vinegar", "刺身不错。醋是店里进货多出来的——有时候批发商送来太多，我一个人用不完。"),
                ("(O)fried_calamari", "(O)oil", "炸鱿鱼圈很脆。油给你——做饭的手艺，油是关键。"),
            },
            // ── Robin ──
            ["Robin"] = new()
            {
                ("(O)wood", "(O)hardwood", "木料纹理很顺。硬木是我从废弃的旧农舍拆下来的——它的年轮比整个鹈鹕镇都老。"),
                ("(O)stone", "(O)wood", "石头砌墙很好用。木料是多出来的，给你。我家房子就是我一块块木板切出来的。"),
                ("(O)hardwood", "(O)wood_floor", "硬木！你知道这多难得。木地板给你——铺在小屋门口，早上踩上去不凉。"),
            },
            // ── Sam ──
            ["Sam"] = new()
            {
                ("(O)pizza", "(O)joja_cola", "披萨！你是懂我的。Joja可乐给你——我知道它不健康，但吉他手需要糖分。"),
                ("(O)maple_bar", "(O)cactus_fruit", "枫糖棒太好吃了。仙人掌果是我妈去沙漠带回来的——她总是带奇怪的水果。"),
            },
            // ── Sebastian ──
            ["Sebastian"] = new()
            {
                ("(O)frozen_tear", "(O)obsidian", "冰冻的眼泪。黑曜石给你——它是在岩浆和海水相遇时诞生的。很...安静。"),
                ("(O)void_egg", "(O)void_essence", "虚空蛋。虚无精华——不要碰太久，它会让手指发麻。但那种感觉挺好的。"),
                ("(O)sashimi", "(O)quartz", "刺身——你从哪里弄来的。石英是我在矿洞里捡的，它反射月光的时候最好看。"),
            },
            // ── Shane ──
            ["Shane"] = new()
            {
                ("(O)pizza", "(O)pepper_poppers", "披萨。辣椒爆米花是额外的——反正查理不吃。"),
                ("(O)pepper", "(O)egg", "辣椒够劲。鸡蛋是查理下的——好吧，是鸡下的，我只是叫它们查理。"),
                ("(O)beer", "(O)pizza", "啤酒。披萨是前几天买的——冷冻的，热一下还能吃。不要嫌弃。"),
            },
            // ── Vincent ──
            ["Vincent"] = new()
            {
                ("(O)grape", "(O)snail", "葡萄好甜！蜗牛送给你——我在草里找到的，它有漂亮的壳。贾斯说蜗牛是慢吞吞的信使。"),
                ("(O)cranberry_candy", "(O)clay", "糖果！黏土给你——我捏了一只青蛙，但看起来不太像。你可以自己捏一个。"),
            },
            // ── Willy ──
            ["Willy"] = new()
            {
                ("(O)fish", "(O)seaweed", "这鱼不错。海藻煮汤最鲜。海风说我们的交易会一直继续。"),
                ("(O)squid", "(O)bait", "鱿鱼！墨汁还没干呢。这鱼饵是我自己配的——老水手的秘方。"),
                ("(O)lobster", "(O)crab_pot", "龙虾是好兆头。蟹笼给你——放在码头边上，过几天来收。"),
            },
            // ── Wizard ──
            ["Wizard"] = new()
            {
                ("(O)solar_essence", "(O)void_essence", "太阳精华——光和影本是一体。虚无精华给你，它们需要在同一个维度保持平衡。"),
                ("(O)purple_mushroom", "(O)life_elixir", "紫蘑菇——你知道了森林的秘密。生命药水是我调制的——不保证味道，但保证有效。"),
                ("(O)super_cucumber", "(O)iridium_ore", "超级海参。这块铱矿来自另一个层面——在你手里它可能会发光。"),
            },
        };

        private static readonly Dictionary<string, string> NpcReactTexts = new()
        {
            ["Abigail"] = "这个...我很喜欢。它让我想起了小时候在矿洞边捡到的东西。我把它放在枕头下了。",
            ["Alex"] = "谢了。运动之外的时间，收到东西的感觉也不错。送你一份——算是交流，不是交易。",
            ["Caroline"] = "谢谢你。午后收到这个，正好配一杯茶。柳树也说谢谢你。",
            ["Clint"] = "收到。这个伤痕很美，我会留着的。有时候修补的不是铁器，而是时间。",
            ["Demetrius"] = "有趣的样本。我会记录下这次交换的数据。自然界中的交换从来都是双向的。",
            ["Elliott"] = "谢谢你——这个会成为我下一章的一部分。文字比金币诚实得多。",
            ["Emily"] = "它的颜色在跳舞。希望你也喜欢我给你选的——每件东西都有它想去的地方。",
            ["Evelyn"] = "谢谢你，亲爱的。年纪大了才明白，最好的礼物是有人想着你。",
            ["George"] = "行了行了。东西放那儿吧。这个给你——不要到处说。",
            ["Gus"] = "谢谢你。今晚的厨房会因为这多一份味道。这个你拿着——好厨子不差这一口。",
            ["Haley"] = "真的吗？好吧这个我给你选的也不错——至少在阳光下很好看。",
            ["Harvey"] = "谢谢你。健康不只在身体——关心别人也是。请照顾好自己。",
            ["Jas"] = "谢谢你！跳房子的格子今天多了一格。这个给你——它在我枕头下放了三天。",
            ["Jodi"] = "谢谢你。做妈妈以后收到礼物的心情很不一样——像是被记得。",
            ["Kent"] = "收到。在战壕里我们也有这种交换——一块糖换一张家园的照片。谢谢你让我想起这些。",
            ["Leah"] = "谢谢你。我会把它变成一件作品。创作就是交换——用时间换意义。",
            ["Lewis"] = "谢谢你。作为镇长，我收到了很多礼物——但你的这一份不太一样。",
            ["Linus"] = "山谷让我把这个给你。不是交易，只是...循环。风从一棵树吹到另一棵树，果子落下来，长成新树。",
            ["Marnie"] = "谢谢你！动物们看到你来了会高兴的。这个给你——今早刚挤的。",
            ["Maru"] = "有趣的变量。我会做一个实验：把它放在显微镜下看和用肉眼看的区别。这个给你——是我做的。",
            ["Pam"] = "谢了。这个给你——不是什么好东西，但至少是真实的。",
            ["Penny"] = "谢谢你。我会在日记里写下今天的交换。有时候最普通的交换，是最值得记住的。",
            ["Pierre"] = "谢谢你。杂货店卖的从来不是东西——是人和人之间的信任。这个你拿着。",
            ["Robin"] = "不错。我会用它给别人造东西。木屑和刨花有自己的路——给你这个，它是从同一棵树上切下来的。",
            ["Sam"] = "太酷了！这个跟你换——弹吉他的手偶尔也给别人东西。",
            ["Sebastian"] = "嗯。谢谢。这个给你——它一直放在我桌上，也许换个地方更好。",
            ["Shane"] = "嗯。这个给你——我不擅长说这些，但...算了。",
            ["Vincent"] = "哇！这个送给我？太好啦！我拿我最喜欢的东西跟你换——要好好照顾它哦！",
            ["Willy"] = "海风说有人会来。谢谢你，这个会是很好的等待伙伴。大海从来不问它给的东西值不值钱。",
            ["Wizard"] = "元素层面产生了一次微弱的共振。你的存在在这个维度留下了一道痕迹。",
        };

        /// <summary>Generate any NPC's default preference based on their voice profile</summary>
        private static (string, string, string) GetDefaultPreference(string npcName, string givenItemId)
        {
            var voice = ModEntry.Voices?.GetProfile(npcName);

            string responseItem = npcName switch
            {
                "Alex" => "(O)field_snack",
                "Caroline" => "(O)green_tea",
                "Demetrius" => "(O)salmonberry",
                "Elliott" => "(O)cloth",
                "Evelyn" => "(O)cookie",
                "George" => "(O)stone",
                "Haley" => "(O)sunflower",
                "Harvey" => "(O)energy_tonic",
                "Jas" => "(O)clay",
                "Jodi" => "(O)vegetable_medley",
                "Kent" => "(O)torch",
                "Lewis" => "(O)autumns_bounty",
                "Marnie" => "(O)milk",
                "Maru" => "(O)iron_bar",
                "Pam" => "(O)parsnip",
                "Penny" => "(O)book",
                "Pierre" => "(O)vinegar",
                "Sam" => "(O)joja_cola",
                "Sebastian" => "(O)obsidian",
                "Shane" => "(O)egg",
                "Vincent" => "(O)clay",
                _ => "(O)fiber"
            };

            string reactText = NpcReactTexts.ContainsKey(npcName)
                ? NpcReactTexts[npcName]
                : $"谢谢你放在篮子里的东西。这个给你——是我的一片心意。";

            return (responseItem, responseItem, reactText);
        }

        /// <summary>Evaluate whether any NPC will exchange with a basket item</summary>
        public static ExchangeResult Evaluate(BasketItem item)
        {
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

                        // Bonus for matching request note
                        if (!string.IsNullOrEmpty(item.RequestNote))
                            score += 2f;

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

            // If no match found, give a chance for any NPC to exchange anyway (the "kind stranger" effect)
            if (bestNpc == null && RandomHelper.Chance(0.3))
            {
                var npcs = new[] {
                    "Linus", "Gus", "Emily", "Abigail", "Robin", "Willy", "Clint", "Marnie",
                    "Leah", "Penny", "Elliott", "Maru", "Harvey", "Caroline", "Jodi", "Sam",
                    "Sebastian", "Shane", "Haley", "Alex"
                };
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
