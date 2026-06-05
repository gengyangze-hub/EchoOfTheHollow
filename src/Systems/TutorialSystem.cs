using System;
using System.Collections.Generic;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.Systems.Journal;
using EchoesOfTheHollow.Utils;
using StardewModdingAPI;
using StardewValley;

namespace EchoesOfTheHollow.Systems
{
    /// <summary>
    /// 新手引导系统 -- progressive tutorial with time-spaced messages.
    /// Each day's hints are shown one at a time with ~4 second gaps,
    /// preventing message spam in the HUD corner.
    /// </summary>
    public class TutorialSystem
    {
        private readonly IMonitor _monitor;
        private readonly JournalSystem _journal;
        private TutorialState _state = new();

        private const string SaveKey = "Tutorial/v2";

        // Delayed message queue
        private readonly Queue<(string message, int delayMs)> _messageQueue = new();
        private float _queueTimer;

        public TutorialSystem(IModHelper helper, IMonitor monitor, JournalSystem journal)
        {
            _monitor = monitor;
            _journal = journal;
        }

        // ═══════════════════════════════════════════════
        //  Save/Load
        // ═══════════════════════════════════════════════

        public void OnSaveLoaded()
        {
            _state = ModDataHelper.Load<TutorialState>(SaveKey) ?? new TutorialState();
        }

        public void OnSaving()
        {
            ModDataHelper.Save(SaveKey, _state);
        }

        public void OnReturnToTitle()
        {
            _messageQueue.Clear();
        }

        // ═══════════════════════════════════════════════
        //  Daily Logic
        // ═══════════════════════════════════════════════

        public void OnDayStarted()
        {
            _messageQueue.Clear();
            _state.DaysPlayed++;

            if (_state.IsComplete) return;

            switch (_state.DaysPlayed)
            {
                case 1: EnqueueDayOneHints(); break;
                case 2: EnqueueDayTwoHints(); break;
                case 3: EnqueueDayThreeHints(); break;
                case 5: EnqueueDayFiveHints(); break;
                case 7: EnqueueDaySevenHints(); _state.IsComplete = true; break;
            }

            _state.CurrentStage = Math.Min(_state.DaysPlayed, 7);
        }

        /// <summary>Called each tick to pump delayed messages</summary>
        public void OnUpdateTicked()
        {
            if (_messageQueue.Count == 0) return;

            _queueTimer += (float)Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds;

            if (_queueTimer >= 0)
            {
                var (message, _) = _messageQueue.Dequeue();
                Game1.addHUDMessage(new HUDMessage(message, HUDMessage.newQuest_type));

                // Set timer for next message
                if (_messageQueue.Count > 0)
                    _queueTimer = -_messageQueue.Peek().delayMs;
                else
                    _queueTimer = 0;
            }
        }

        public void OnDayEnding() { }

        // ═══════════════════════════════════════════════
        //  Helper: enqueue messages with spacing
        // ═══════════════════════════════════════════════

        private void EnqueueSpaced(string[] messages, int gapMs = 4200)
        {
            _messageQueue.Clear();
            for (int i = 0; i < messages.Length; i++)
                _messageQueue.Enqueue((messages[i], i * gapMs));
            _queueTimer = 0; // Show first immediately
        }

        // ═══════════════════════════════════════════════
        //  Day One: Narrative Intro
        // ═══════════════════════════════════════════════

        public void TriggerDayOneIntro()
        {
            string[] introLines = {
                "你来到了鹈鹕镇。这里没有人谈论金钱----",
                "这里的人用另一种方式记住彼此。",
                "他们会观察你，记住你，在日志里写下关于你的只言片语。",
                "打开回音日志（按 J 键），你会看到这个世界如何看你。",
                "不是数字，不是评分----而是故事，是记忆，是回音。"
            };

            EnqueueSpaced(introLines, 4500);

            _journal.AddPlayerEntry(
                "抵达鹈鹕镇",
                "我到了。这里很安静。没有人问我带了多少钱----他们甚至不提那个词。\n\n" +
                "罗宾带我看了房子。她说：'这里不需要别的，只需要你真心想住下来。'\n\n" +
                "我把这句话记在这里，作为在这个世界的第一条记录。"
            );
        }

        private void EnqueueDayOneHints()
        {
            if (!(Game1.year == 1 && Game1.currentSeason == "spring" && Game1.dayOfMonth == 1))
            {
                EnqueueSpaced(new[] { "欢迎回来。按 J 键打开回音日志，看看镇上的人对你的记忆。" }, 0);
            }
        }

        // ═══════════════════════════════════════════════
        //  Day Two: Enthusiasm & Journal
        // ═══════════════════════════════════════════════

        private void EnqueueDayTwoHints()
        {
            EnqueueSpaced(new[] {
                "在鹈鹕镇，驱动力不是体力，而是「兴致」。",
                "兴致会随着重复劳动而消减----试着每天做不同的事。",
                "屏幕右下角的皮质面板就是你今天的兴致。绿色饱满，红色枯竭。",
                "按 J 键打开回音日志。NPC们会在里面记录对你的印象。"
            });

            _journal.AddPlayerEntry(
                "关于兴致",
                "今天注意到一个有趣的事----重复做同一件事会让兴致下降。\n\n" +
                "但换一件事做，兴致似乎又回来了。也许在这个地方，生活的方式不是拼命干活，而是保持新鲜感。"
            );
        }

        // ═══════════════════════════════════════════════
        //  Day Three: Basket & Exchange
        // ═══════════════════════════════════════════════

        private void EnqueueDayThreeHints()
        {
            EnqueueSpaced(new[] {
                "你注意到镇上有一个互惠篮（按 B 键打开）。",
                "把不需要的东西放进去，写下你想要什么----也许有人会来交换。",
                "在这里，没有人用金钱交易。一切都通过好意来流转。",
                "和NPC交谈吧。他们会记住你----不是好感度，而是真实的记忆。"
            });

            _journal.AddPlayerEntry(
                "互惠篮",
                "发现了一个篮子。上面写着「互惠篮」----把东西放进去，写下想交换的物品，会有人来取走并留下他们的东西。\n\n" +
                "没有价格，没有货币。只有人和人之间的需要与给予。"
            );
        }

        // ═══════════════════════════════════════════════
        //  Day Five: Memory Book & NPCs
        // ═══════════════════════════════════════════════

        private void EnqueueDayFiveHints()
        {
            EnqueueSpaced(new[] {
                "按 H 键打开记忆之书----这是取代好感度面板的东西。",
                "在这里，你可以看到每位村民对你的记忆。每个人都有自己独特的声音。",
                "有的记忆温暖，有的惆怅，有的带着好奇----这些就是他们对你的全部了解。",
                "每天结束时，NPC们会在日志中写下关于你的新观察。去看看他们都注意到了什么。"
            });
        }

        // ═══════════════════════════════════════════════
        //  Day Seven: Full Systems Overview
        // ═══════════════════════════════════════════════

        private void EnqueueDaySevenHints()
        {
            EnqueueSpaced(new[] {
                "一周过去了。你已经了解了这个世界的基本运作方式。",
                "J 键 → 回音日志 / H 键 → 记忆之书 / B 键 → 互惠篮",
                "商店里的物品不需要金钱----而是需要你与店主的好意。",
                "重复做同一件事会让兴致枯竭。试试听风，或者只是发呆。",
                "邀约留言柱会出现在镇上----那是NPC们想和你一起做的事。",
                "这个世界不记录数字。它记录回音。而你，正在创造回音。"
            });

            _journal.AddPlayerEntry(
                "一周",
                "在这里住了一周了。慢慢开始理解这个地方的规则。\n\n" +
                "没有人问我赚了多少钱，没有人给我打分。他们只是----看着我，然后记住。\n\n" +
                "我想这就是这里最珍贵的东西：不是数据，而是人与人之间真实的、细微的、会变化的印象。"
            );
        }

        // ═══════════════════════════════════════════════
        //  Public API
        // ═══════════════════════════════════════════════

        public bool IsComplete => _state.IsComplete;
        public int DaysPlayed => _state.DaysPlayed;
        internal TutorialState State => _state;
    }

    internal class TutorialState
    {
        public int DaysPlayed { get; set; }
        public int CurrentStage { get; set; }
        public bool IsComplete { get; set; }
        public bool HasOpenedJournal { get; set; }
        public bool HasUsedBasket { get; set; }
        public bool HasMetNpc { get; set; }
    }
}
