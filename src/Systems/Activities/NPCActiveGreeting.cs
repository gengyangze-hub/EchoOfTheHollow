using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.NpcProfiles;
using EchoesOfTheHollow.Systems.Journal;
using EchoesOfTheHollow.Utils;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace EchoesOfTheHollow.Systems.Activities
{
    /// <summary>
    /// NPC主动互动系统 -- NPCs proactively greet, wave, or chat with the player.
    /// Based on NPC personality (from VoiceRegistry), friendship level, and location context.
    /// Creates a living, breathing community feel without relying on player initiation.
    /// </summary>
    public class NpcActiveGreeting
    {
        private readonly IMonitor _monitor;
        private readonly JournalSystem _journal;
        private readonly VoiceRegistry _voices;

        // Cooldown: minimum game-minutes between NPC interactions
        private readonly Dictionary<string, int> _npcLastInteraction = new();
        private readonly Dictionary<string, int> _npcGreetCountToday = new();
        private int _todayGreetingTotal;

        // Config constants
        private const int DetectionRange = 300;  // pixels (about 5 tiles)
        private const int ChatRange = 150;       // pixels for closer chat
        private const int GlobalCooldown = 30;   // game minutes between ANY NPC greetings
        private const int NpcCooldown = 90;      // game minutes between same NPC greetings
        private const int MaxDailyGreetings = 12; // total greetings per day
        private int _lastGlobalGreetingTime;

        // NPC interaction personality profiles
        private static readonly Dictionary<string, (float frequency, float approachChance, bool prefersDistance)> InteractionProfiles = new()
        {
            //  NPC        frequency  approach   prefers distance (wave vs chat)
            ["Abigail"]  = ( 0.4f,     0.3f,     false ),  // Curious but shy-ish
            ["Alex"]     = ( 0.6f,     0.7f,     false ),  // Confident, approaches
            ["Caroline"] = ( 0.4f,     0.1f,     true  ),  // Watches from garden
            ["Clint"]    = ( 0.2f,     0.1f,     true  ),  // Shy, avoids
            ["Demetrius"]= ( 0.3f,     0.2f,     true  ),  // Distracted scientist
            ["Elliott"]  = ( 0.5f,     0.6f,     false ),  // Dramatic greeter
            ["Emily"]    = ( 0.7f,     0.8f,     false ),  // Warm, always says hi
            ["Evelyn"]   = ( 0.5f,     0.6f,     false ),  // Sweet grandmother
            ["George"]   = ( 0.15f,    0.05f,    true  ),  // Grumpy, stays put
            ["Gus"]      = ( 0.7f,     0.9f,     false ),  // Friendliest person in town
            ["Haley"]    = ( 0.3f,     0.2f,     true  ),  // Selective about who she greets
            ["Harvey"]   = ( 0.4f,     0.5f,     false ),  // Polite but reserved
            ["Jas"]      = ( 0.5f,     0.4f,     false ),  // Child, curious but nervous
            ["Jodi"]     = ( 0.5f,     0.6f,     false ),  // Friendly mom energy
            ["Kent"]     = ( 0.2f,     0.1f,     true  ),  // Keeps to himself
            ["Leah"]     = ( 0.4f,     0.4f,     false ),  // Friendly but independent
            ["Lewis"]    = ( 0.5f,     0.7f,     false ),  // Mayor energy
            ["Linus"]    = ( 0.3f,     0.1f,     true  ),  // Watches from afar
            ["Marnie"]   = ( 0.5f,     0.7f,     false ),  // Warm, approachable
            ["Maru"]     = ( 0.4f,     0.4f,     false ),  // Friendly but absorbed
            ["Pam"]      = ( 0.2f,     0.3f,     true  ),  // Depends on mood
            ["Penny"]    = ( 0.35f,    0.3f,     false ),  // Shy but wants to
            ["Pierre"]   = ( 0.4f,     0.5f,     false ),  // Shopkeeper energy
            ["Robin"]    = ( 0.6f,     0.7f,     false ),  // Confident, friendly
            ["Sam"]      = ( 0.7f,     0.8f,     false ),  // Always waving
            ["Sebastian"]= ( 0.1f,     0.05f,    true  ),  // Avoids everyone
            ["Shane"]    = ( 0.1f,     0.05f,    true  ),  // "Don't talk to me"
            ["Vincent"]  = ( 0.7f,     0.9f,     false ),  // Runs up to everyone
            ["Willy"]    = ( 0.4f,     0.5f,     false ),  // Friendly old sailor
            ["Wizard"]   = ( 0.1f,     0.02f,    true  ),  // Almost never seen
            ["Sandy"]    = ( 0.8f,     0.95f,    false ),  // DESPERATE for visitors
            ["Krobus"]   = ( 0.2f,     0.1f,     true  ),  // Hides from most
            ["Dwarf"]    = ( 0.3f,     0.2f,     false ),  // Curious about surface dwellers
        };

        public NpcActiveGreeting(IMonitor monitor, JournalSystem journal, VoiceRegistry voices)
        {
            _monitor = monitor;
            _journal = journal;
            _voices = voices;
        }

        /// <summary>Check for nearby NPCs and trigger interactions each tick</summary>
        public void OnUpdateTicked()
        {
            if (Game1.player == null || !Context.IsWorldReady) return;
            if (_todayGreetingTotal >= MaxDailyGreetings) return;

            // Global cooldown between greetings
            if (Game1.timeOfDay - _lastGlobalGreetingTime < GlobalCooldown) return;

            var playerPos = Game1.player.Position;
            var location = Game1.player.currentLocation;
            if (location == null) return;

            // Check each NPC
            foreach (var npc in location.characters)
            {
                if (npc is not NPC gameNpc || gameNpc.Name == Game1.player.Name) continue;
                if (string.IsNullOrEmpty(gameNpc.Name)) continue;

                float distance = Vector2.Distance(playerPos, gameNpc.Position);
                if (distance > DetectionRange) continue;

                // Check if this NPC should interact
                if (!ShouldInteract(gameNpc, distance)) continue;

                // Trigger the interaction
                TriggerInteraction(gameNpc, distance);
                _lastGlobalGreetingTime = Game1.timeOfDay;
                break; // One interaction per check
            }
        }

        private bool ShouldInteract(NPC npc, float distance)
        {
            string name = npc.Name;

            // Get NPC's interaction profile
            if (!InteractionProfiles.TryGetValue(name, out var profile))
            {
                // Default profile for unlisted NPCs
                profile = (0.3f, 0.4f, false);
            }

            var (baseFrequency, approachChance, prefersDistance) = profile;

            // Friendship modifier: higher friendship = more likely to interact
            float friendshipMod = 1.0f;
            if (Game1.player != null && Game1.player.friendshipData.TryGetValue(name, out Friendship? friendship))
            {
                // At 0 hearts: -30% frequency. At 10 hearts: +50% frequency
                friendshipMod = 0.7f + (friendship.Points / 2500f) * 0.5f;
            }

            float effectiveFrequency = baseFrequency * friendshipMod;

            // Check global cooldown and NPC-specific cooldown
            if (_npcLastInteraction.TryGetValue(name, out int lastTime))
            {
                if (Game1.timeOfDay - lastTime < NpcCooldown) return false;
            }

            // Check NPC-specific daily limit (max 2 per NPC per day)
            if (_npcGreetCountToday.TryGetValue(name, out int count) && count >= 2)
                return false;

            // Roll for interaction
            float roll = (float)RandomHelper.NextDouble();

            // Distance factor: closer = more likely to interact (reversed for distance-preferring NPCs)
            float distanceFactor = prefersDistance
                ? 1.0f - (distance / DetectionRange) * 0.5f  // Less affected by distance
                : 1.0f - (distance / DetectionRange) * 0.8f; // More likely when closer

            float threshold = effectiveFrequency * distanceFactor;

            return roll < threshold;
        }

        private void TriggerInteraction(NPC npc, float distance)
        {
            string name = npc.Name;
            bool isClose = distance < ChatRange;

            if (!InteractionProfiles.TryGetValue(name, out var profile))
                profile = (0.3f, 0.4f, false);

            var (_, _, prefersDistance) = profile;

            // Determine interaction type
            string interactionType = DetermineInteractionType(npc, distance, prefersDistance);

            // Update tracking
            _npcLastInteraction[name] = Game1.timeOfDay;
            if (!_npcGreetCountToday.ContainsKey(name))
                _npcGreetCountToday[name] = 0;
            _npcGreetCountToday[name]++;
            _todayGreetingTotal++;

            // Record in memory clarity system
            float impact = interactionType switch { "chat" => 1.5f, "gift" => 2.0f, "greet" => 1.0f, _ => 0.5f };
            MemoryClaritySystem.RecordInteraction(name, impact);

            // Execute the interaction
            switch (interactionType)
            {
                case "wave":
                    DoWave(npc);
                    break;
                case "greet":
                    DoGreet(npc);
                    break;
                case "chat":
                    DoChat(npc);
                    break;
                case "gift":
                    DoGift(npc);
                    break;
                case "notice":
                    DoNotice(npc); // NPC notices you but doesn't interact directly
                    break;
            }
        }

        private string DetermineInteractionType(NPC npc, float distance, bool prefersDistance)
        {
            string name = npc.Name;

            // Friendship determines available types
            int friendship = 0;
            if (Game1.player != null && Game1.player.friendshipData.TryGetValue(name, out Friendship? f))
                friendship = f.Points;

            // Very low friendship + shy personality = just notice (journal only, no HUD)
            if (friendship < 250 && prefersDistance && RandomHelper.Chance(0.6))
                return "notice";

            // Low friendship + distant = wave
            if (distance > ChatRange || (prefersDistance && !RandomHelper.Chance(0.3)))
                return "wave";

            // Medium friendship = greet
            if (friendship < 750 || !RandomHelper.Chance(0.4))
                return "greet";

            // High friendship = chat with personal touch
            if (friendship < 1500 || !RandomHelper.Chance(0.15))
                return "chat";

            // Very high friendship = rare gift
            return "gift";
        }

        private void DoWave(NPC npc)
        {
            string name = npc.Name;
            string waveText = GetWaveText(name);
            Game1.addHUDMessage(new HUDMessage(waveText, HUDMessage.newQuest_type));

            GenerateJournalEntry(npc, "wave", $"{name}在远处向你招了招手。");
        }

        private void DoGreet(NPC npc)
        {
            string name = npc.Name;
            string greetText = GetGreetText(name);
            Game1.addHUDMessage(new HUDMessage(greetText, HUDMessage.newQuest_type));

            GenerateJournalEntry(npc, "greet", $"{name}对你打了个招呼----\"{greetText}\"");
        }

        private void DoChat(NPC npc)
        {
            string name = npc.Name;
            string chatText = GetChatText(name);
            Game1.addHUDMessage(new HUDMessage(chatText, HUDMessage.newQuest_type));

            GenerateJournalEntry(npc, "chat", $"{name}和你聊了几句。{chatText}");
        }

        private void DoGift(NPC npc)
        {
            string name = npc.Name;
            if (Game1.player == null) return;

            // Simple NPC-initiated gift -- a small token
            string[] possibleGifts = GetNpcGifts(name);
            if (possibleGifts.Length == 0) return;

            string giftId = possibleGifts[RandomHelper.Next(possibleGifts.Length)];
            try
            {
                var item = ItemRegistry.Create(giftId, 1, 0);
                if (item != null)
                {
                    Game1.player.addItemToInventoryBool(item);
                    string msg = GetGiftText(name);
                    Game1.addHUDMessage(new HUDMessage(msg, HUDMessage.newQuest_type));

                    GenerateJournalEntry(npc, "gift", $"{name}{msg}");
                }
            }
            catch { /* Item creation failed -- skip the gift */ }
        }

        private void DoNotice(NPC npc)
        {
            // Silent observation -- journal entry only, no HUD
            string name = npc.Name;
            string location = Game1.currentLocation?.Name ?? "镇上";

            string[] noticeThoughts = {
                $"{name}看了你一眼，然后继续做自己的事了。",
                $"{name}注意到你经过，没有说什么。",
                $"{name}的目光在你身上停了片刻。",
            };
            string thought = noticeThoughts[RandomHelper.Next(noticeThoughts.Length)];

            GenerateJournalEntry(npc, "notice", thought);
        }

        // ═══════════════════════════════════════════════
        //  NPC-specific dialogue generation
        // ═══════════════════════════════════════════════

        private string GetWaveText(string npc) => npc switch
        {
            "Abigail" => "艾比盖尔从远处挥了挥手，然后继续低头看她的石头。",
            "Alex" => "亚历克斯远远地竖起大拇指。",
            "Caroline" => "卡洛琳在柳树下轻轻抬起手，像在招呼一缕风。",
            "Clint" => "克林特抬头看了一眼，僵硬地点了点头。",
            "Demetrius" => "德米特里厄斯推了推眼镜，似乎在确认是你----然后点了下头。",
            "Elliott" => "艾利欧特举起手中的羽毛笔向你致意，差点把墨水洒在衬衫上。",
            "Emily" => "艾米丽远远地挥手，手上的颜色好像也在跳舞。",
            "Evelyn" => "艾芙琳奶奶在花园里对你温柔地招了招手。",
            "George" => "乔治从窗口看到了你。他几乎没动----但那几乎就是他的招呼了。",
            "Gus" => "格斯挥动着一条擦桌子的毛巾向你打招呼。",
            "Haley" => "海莉从取景器后面抬起头，歪了歪嘴角----算是招呼。",
            "Harvey" => "哈维医生放下听诊器，礼貌地点了点头。",
            "Jas" => "贾斯害羞地晃了晃手里的小花环。",
            "Jodi" => "乔迪从厨房窗边看到了你，匆忙擦了把手然后挥了挥。",
            "Kent" => "肯特在门廊上微微抬了抬下巴。对他来说，这已经是热烈了。",
            "Leah" => "莉亚从木雕上抬起头，用拿凿子的手和你招了招。",
            "Lewis" => "刘易斯庄严地举起一只手，镇长的派头很足。",
            "Linus" => "莱纳斯远远地看到了你。他没有挥手----只是静静地看着，像在看一件自然的事。",
            "Marnie" => "玛妮在一群动物中间冲你使劲挥手，动作大得鸡都跑了几只。",
            "Maru" => "玛鲁从工作台上抬起头，用螺丝刀朝你晃了一下。",
            "Pam" => "潘姆从公交车上看了你一眼，叼着烟晃了一下头。",
            "Penny" => "潘妮把书合上，轻轻地向你招了招手，手很小。",
            "Pierre" => "皮埃尔在杂货店门口探出头来，冲你点了点。",
            "Robin" => "罗宾从屋顶上朝你挥了挥锤子----差点没站稳。",
            "Sam" => "山姆踩着滑板经过，单手对你比了个摇滚手势。",
            "Sebastian" => "塞巴斯蒂安从地下室的窗户瞥见你。他没有挥手----但他看你的方向。这已经很少见了。",
            "Shane" => "肖恩把头转开了。那是他的招呼方式----你知道他看到你了。",
            "Vincent" => "文森特拼命挥手----整个手臂都在晃，好像怕你看不到。",
            "Willy" => "威利从船舷上抬起一只手，像是在为你祈福一阵顺风。",
            "Wizard" => "远处塔楼的窗户里，一个身影微微点头。或者只是窗帘在动。",
            "Sandy" => "桑迪在绿洲门口跳了起来，拼命挥手----今天有人来了！",
            "Krobus" => "阴影里有一双眼睛亮了一下，然后消失了。你知道是谁。",
            "Dwarf" => "矮人从矿洞的石堆后面探出头，举起了他收集的第89个地表词汇的小牌子。",
            _ => $"{npc}远远地朝你招了招手。"
        };

        private string GetGreetText(string npc) => npc switch
        {
            "Gus" => "格斯喊道：\"今天过来坐坐！厨房的汤刚炖好。\"",
            "Emily" => "艾米丽小跑过来：\"今天的云特别好看----你看到了吗？\"",
            "Sam" => "山姆滑到你旁边停下来：\"嘿！我写了一首新歌----下次弹给你听。\"",
            "Vincent" => "文森特跑过来：\"我今天抓到了一只超大的甲虫！想看吗？\"",
            "Robin" => "罗宾擦了擦汗：\"刚从屋顶上下来。你农场那边的木头需要换吗？\"",
            "Alex" => "亚历克斯路过时拍了拍你的肩：\"状态不错！继续保持。\"",
            "Elliott" => "艾利欧特郑重地握着你的手：\"我的朋友！今天的海浪特别有诗意。\"",
            "Leah" => "莉亚笑了笑：\"我正在做一个新木雕----你想看看草图吗？\"",
            "Abigail" => "艾比盖尔从口袋里掏出一块石头：\"你看这块----里面的纹路像不像闪电？\"",
            "Jodi" => "乔迪抱着一袋菜：\"今天忙吗？不要饿着自己。\"",
            "Evelyn" => "艾芙琳奶奶拉过你的手：\"饼干刚出炉。等一下来拿两块。\"",
            "Penny" => "潘妮抱着几本书：\"我刚读完一本很好的----你可能会喜欢。\"",
            "Harvey" => "哈维医生推了推眼镜：\"最近睡眠好吗？别太累了。\"",
            "Maru" => "玛鲁兴奋地比划着：\"我那个故意设计得不完美的装置----它居然开始自己修正了！\"",
            "Sandy" => "桑迪拉着你的手：\"你能来真是太好了！今天沙漠的日落准备了一个特别版本。\"",
            _ => $"{npc}对你打了个招呼。"
        };

        private string GetChatText(string npc) => npc switch
        {
            "Linus" => "莱纳斯慢慢地走过来说：\"山谷今天的风是温暖的那种----你知道我在说什么。\"他没有等你回应，只是在你旁边站了一会儿。",
            "Abigail" => "艾比盖尔走到你旁边：\"我在矿洞听见你敲石头的声音。你的节奏和我的不太一样----但很好。\"她走了，像一片紫色的雾。",
            "Emily" => "艾米丽停下来仔细看着你：\"嗯----你周围的颜色今天偏橙。是发生了什么好事，还是你想事情想得太用力了？\"",
            "Sebastian" => "塞巴斯蒂安居然主动开口了：\"昨晚看到你窗口还亮着。我也睡不着。\"就这样。说完他就走了。",
            "Shane" => "肖恩走到你旁边，看着地面。过了很久：\"蓝色小鸡今天生了一个蛋。\"他没有等你回应，但这已经是他最多的话了。",
            "Penny" => "潘妮犹豫了一下，然后说：\"我读到这里有一句话----'善良的人往往不知道自己善良'。\"她脸红了一点，走了。",
            "Elliott" => "艾利欧特深吸了一口气：\"我正在写的这一章----主角变得有点像你。\"他顿了顿，\"不是故意的。但有时候角色会自己找到原型。\"",
            _ => $"{npc}停下来和你聊了几句。"
        };

        private string GetGiftText(string npc) => npc switch
        {
            "Evelyn" => "艾芙琳奶奶把刚烤好的饼干塞进你手里：\"多吃点。\"",
            "Gus" => "格斯端出一小碗汤：\"试一下----新的配方。你是第一个尝的。\"",
            "Linus" => "莱纳斯把一颗洗干净的野莓放在你手心：\"山谷让我给你的。\"",
            "Caroline" => "卡洛琳递来一小袋茶叶：\"柳树下采的。一个人喝的时候，想着午后的光。\"",
            "Robin" => "罗宾切了一小块硬木：\"纹理很漂亮----给你。可以做成手柄。\"",
            "Marnie" => "玛妮递来一个还温热的鸡蛋：\"今天那只花斑鸡特别感激你。\"",
            "Willy" => "威利从箱子底翻出一个小海螺：\"在海上漂了很久才到这里的----它想上陆地了。\"",
            "Emily" => "艾米丽放了一块染好的蓝色布料在你手里：\"这是今天天空的颜色。它想和你在一起。\"",
            _ => $"{npc}给了你一个小东西。不是什么值钱的----但那是他/她自己挑的。"
        };

        private string[] GetNpcGifts(string npc) => npc switch
        {
            "Evelyn" => new[] { "(O)cookie", "(O)tulip" },
            "Gus" => new[] { "(O)bread", "(O)coffee" },
            "Linus" => new[] { "(O)wild_plum", "(O)cave_carrot" },
            "Caroline" => new[] { "(O)green_tea", "(O)daffodil" },
            "Robin" => new[] { "(O)wood", "(O)hardwood" },
            "Marnie" => new[] { "(O)egg", "(O)milk" },
            "Willy" => new[] { "(O)seaweed", "(O)bait" },
            "Emily" => new[] { "(O)cloth", "(O)wool" },
            _ => new[] { "(O)fiber", "(O)sap" } // Generic humble gift
        };

        // ═══════════════════════════════════════════════
        //  Journal Entry Generation
        // ═══════════════════════════════════════════════

        private void GenerateJournalEntry(NPC npc, string type, string displayText)
        {
            if (Game1.player == null) return;

            var voice = _voices.GetProfile(npc.Name);
            if (voice != null)
            {
                // Run the display text through the NPC's voice for authenticity
                var parms = new Dictionary<string, string>
                {
                    ["playerName"] = Game1.player.Name,
                    ["npcName"] = npc.Name,
                    ["location"] = Game1.currentLocation?.Name ?? "镇上"
                };
                displayText = _voices.RenderEntry(displayText, voice, parms);
            }

            var entry = new JournalEntry
            {
                NpcName = npc.Name,
                DisplayText = displayText,
                EmotionTag = type switch { "wave" => "Warm", "greet" => "Warm", "chat" => "Curiosity", "gift" => "Warm", _ => "Neutral" },
                Trigger = TriggerType.DirectInteraction,
                DaysPlayed = (int)Game1.stats.DaysPlayed,
                Season = Game1.currentSeason,
                Year = Game1.year,
                DayOfMonth = Game1.dayOfMonth,
                TimeOfDay = Game1.timeOfDay,
                LocationName = Game1.currentLocation?.Name ?? "",
                Category = "NpcGreeting"
            };
            _journal.AddEntry(entry);

            if (ModEntry.Config.DebugMode)
                _monitor.Log($"[Greeting] {npc.Name} ({type}) at {Game1.currentLocation?.Name}", LogLevel.Debug);
        }

        /// <summary>Reset daily counters</summary>
        public void OnDayStarted()
        {
            _npcLastInteraction.Clear();
            _npcGreetCountToday.Clear();
            _todayGreetingTotal = 0;
            _lastGlobalGreetingTime = 0;
        }
    }
}
