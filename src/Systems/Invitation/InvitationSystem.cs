using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.Systems.Journal;
using EchoesOfTheHollow.Utils;
using StardewModdingAPI;
using StardewValley;

namespace EchoesOfTheHollow.Systems.Invitation
{
    /// <summary>
    /// 邀约留言柱 -- 替代任务板。
    /// NPC 用自己的声音在留言柱上写下邀约。
    /// 只有对你记忆清晰度足够的 NPC 才会主动邀请。
    /// 接受后按时赴约会生成温暖的记忆；爽约也会被记住----但不带责备。
    /// </summary>
    public class InvitationSystem
    {
        private readonly IMonitor _monitor;
        private List<InvitationData> _history = new();
        private List<InvitationData> _activeInvitations = new();
        private const string SaveKey = "Invitation/v1";

        // ── 每个 NPC 的个性化邀约模板 ──
        // 每条包含: (activity, location, displayText, acceptResponse, fulfillMemory, missMemory)
        private static readonly Dictionary<string, List<(string activity, string location, int timeStart, int timeEnd,
            string displayText, string acceptResponse, string fulfillMemory, string missMemory)>> NpcInvitations = new()
        {
            ["Abigail"] = new()
            {
                ("在矿洞边捡石头", "Mountain", 1300, 1800,
                    "嘿----去矿洞边捡石头吗？听说昨天下雨又冲出了一颗紫水晶。一个人去也行，但两个人捡石头比较不无聊。在留言柱下面签个名就行。\n---- 阿比盖尔",
                    "在留言柱上看到了{{playerName}}的签名，旁边还画了一颗小小的紫水晶。那个水晶画歪了----但是我懂。下午矿洞见。",
                    "{{playerName}}真的来了矿洞。他还真的捡到了一块紫水晶----不过不是我说的那块。是另一块。他把它递给我，说'这颗应该是你的'。我把它放在枕头下面了。",
                    "在矿洞口等了一会儿。他没来。没关系----矿洞里本来就很黑，不太适合所有人。下次我自己去就行。紫水晶不会跑。"),
                ("一起去探险", "Mountain", 1400, 1900,
                    "你知道吗----矿洞第63层有一种发蓝光的蘑菇。我上次想摘但梯子坏了。两个人一起下去比较安全。其实一个人也行，但比较无聊。你决定。\n---- 阿比盖尔",
                    "{{playerName}}签了名，还在旁边写了个'63'。他知道我说的那个层数！我太兴奋了。准备好火把。",
                    "我们到了第63层。蓝蘑菇还在。{{playerName}}帮我拿着火把，我蹲下来摘。然后他说----'它比你描述的还好看。'他说的不是蘑菇。我说的也不是蘑菇。",
                    "在留言柱上看到了我的字条，也看到了他的签名。但矿洞入口没有人。也许他被什么事耽搁了。也许明天。")
            },
            ["Emily"] = new()
            {
                ("在树下看云的形状", "Town", 1200, 1700,
                    "今天的云很像棉花糖----不是那种松松的，是那种转着转着把自己卷起来的。想一起看吗？在广场中间那棵大树下面就行。不用准备什么，带眼睛过来。\n---- 艾米丽",
                    "{{playerName}}在留言柱上签了名字----名字旁边还加了一个小小的云朵图案。他画的那朵云更像一只羊。但这就是看云的乐趣：每个人看到的都不一样。",
                    "我们在树下待了两个小时。{{playerName}}说那一朵像龙，我说那一朵像刚洗好的床单。然后我们同时指了一朵----什么也没说。我们知道对方看到了同一朵。那种瞬间比一整句话还好。",
                    "云来了又走。我在树下坐了一会儿。他没来。没关系----云不等人，人也不该等云。只是今天的云比昨天重了一点。")
            },
            ["Gus"] = new()
            {
                ("来红鹤食堂尝新菜", "Town", 1700, 2200,
                    "厨房刚炖好了一锅新的----放了你们农场送来的南瓜。汤这种东西，一个人喝不完。所以我在留言柱贴了这张条子，来不来随你。来我就加一勺奶油。\n---- 格斯",
                    "{{playerName}}在留言柱下面签了名----然后居然在签名旁边画了一颗南瓜。那天下午我多买了三颗。以防万一。",
                    "{{playerName}}真的来了。他喝了三碗----第三碗的时候我说'够了'，他说'不够'。没有人对我的汤说过'不够'。那一瞬间我想到我已经做了四十年饭----就是为了等到有人说这句话。",
                    "汤还剩半锅。我煮得太多了。没关系----剩汤第二天反而更好喝，味道都浸透了。他要是明天来，还有。")
            },
            ["Leah"] = new()
            {
                ("在河边画画", "Forest", 1000, 1600,
                    "我在煤矿森林的河边找到了一棵倒下的树----上面长满了那种灰绿色的苔藓。想画下来。但一个人画画手会冷。你要是愿意带双手过来----不会画也没关系。我可以教你。\n---- 莉亚",
                    "{{playerName}}签了名。他的签名很用力----笔迹陷进了纸里。那种用力不是紧张的----是'我在了'。我把这事儿记下来：有人在留言柱上签了名，只为了来河边看一个人画画。",
                    "{{playerName}}来了。他真的不会画----他的树更像一把扫帚----但他画得很认真。认真的画和好的画是两回事，前者比后者珍贵得多得多。我把他的画收进了我的速写本里。",
                    "河边的光很好。我画完了苔藓。他没来，但那天下午有一只翠鸟停在树枝上----停了好久。它可能是来替他的。也可能只是来停一下。两种都可以。")
            },
            ["Linus"] = new()
            {
                ("坐在帐篷外听风", "Mountain", 1800, 2300,
                    "今晚风从西边来。那种风的声音不一样----不是吹，是说。如果你愿意，来山顶帐篷坐坐。不用带东西。带耳朵来就可以了。\n---- 莱纳斯",
                    "我在留言柱上写的字，风吹不跑。{{playerName}}在上面写了他的名字，比我的字写得好看。今晚，山上有两个人听风。\n---- 这样就够了。",
                    "{{playerName}}来了。我们在帐篷外面坐了很久，没有说话。风一直在说。它说了许多关于山谷的事----他听到了吗？他听到了。他走的时候说了一声谢谢----不是谢我，是谢风。这样的人，山谷不会忘记。",
                    "风来了，然后又走了。一个人听也是听。但风说我少了一个听众。没关系----风永远在，听众也永远在。也许明天晚上。")
            },
            ["Willy"] = new()
            {
                ("去海边等传说中的鱼", "Beach", 600, 1100,
                    "明天清晨码头----涨潮前。那条传说中的鱼只在涨潮前出现，我看到了两次，但没钓上来。一个人等鱼和两个人等鱼的区别是----两个人可以轮流盯着浮标，另一个人可以喝咖啡。我带咖啡。\n---- 威利",
                    "{{playerName}}一大清早就在留言柱上签了名。早上五点半。他还没睡还是已经醒了？不管是哪一种----这个年轻人有海钓的魂。",
                    "{{playerName}}来了。天还没亮。我们俩坐在码头上----两杯咖啡，一根鱼竿。鱼没出现。但太阳出来了。他把最后一口咖啡倒进了海里----说'给鱼喝'。一辈子没见过有人给鱼喝咖啡。鱼没喝到，但我喝了----这就够了。",
                    "清晨的码头只有我一个人。也好----那条鱼太狡猾，人多反而吓跑它。但咖啡我还是带了两杯。多的那杯我自己喝。有点苦。但海风是甜的。")
            },
            ["Robin"] = new()
            {
                ("看看新做的木工活", "Mountain", 900, 1500,
                    "刚做好了一批新的木椅----用的山谷里倒下的那棵老橡树。每把椅子都不一样，因为树的纹路决定椅子长什么样。想来看的话，来我家木工坊。门开着。不用敲门。\n---- 罗宾",
                    "{{playerName}}在留言柱上留了字----'下午来'。三个字。不多不少。木匠喜欢话不多的人。下午我多磨了一把椅子----万一他想坐。",
                    "{{playerName}}来了。他把手放在椅背上----不是摸，是'读'。他在读木头的纹路。然后他说：'这棵树活了很久'。很少有人会看出这个。木匠的东西被这种人看到----值了。",
                    "多磨的那把椅子空着。但放在那里也挺好----椅子的意义不全是被人坐。有时候是等。这把椅子在等一个会读木头的人。")
            },
            ["Maru"] = new()
            {
                ("测试一个故意不完美的装置", "Mountain", 1300, 1800,
                    "我做了一个会故意出错的机器人----它每走五步就会往左偏。不是故障----是我想看看'不完美'是不是一种可以编程的东西。需要一个旁观者帮我记录数据。你可以当旁观者。也可以当朋友。\n---- 玛鲁",
                    "{{playerName}}在留言柱上签了名----然后圈出了'不完美是一种可以编程的东西'这句话，在旁边打了一个问号。我喜欢那个问号。好问题比好答案稀有。",
                    "{{playerName}}来了。他帮我记录了每一组数据，然后在最后一行写：'偏左是故意的，但偏得刚刚好。'那不是数据。那是----我不知道是什么。但他说的对。装置偏左的时候----确实刚刚好。",
                    "我自己记录了数据。装置偏了117次。每次偏左的时候我都会想起那个问号。他没来。但问题还在----这就够了。")
            },
            ["Clint"] = new()
            {
                ("修复一件旧铁器", "Town", 1000, 1600,
                    "收了一件老旧的铁器----一把没人要的旧锄头，刃都卷了。但它用的铁是好铁。放在我这儿也是放着，不如人来帮我一起修。不会打铁也没事----你递锤子，我敲。\n---- 克林特",
                    "{{playerName}}愿意来递锤子。他的签名写得很重----像一锤落在铁上。打铁的人认得这种签名：这是认真的。",
                    "{{playerName}}来了。他递锤子的时候没有一次递错----我手伸出去，锤子就到了。递锤子很难----比看起来难。他能递好，说明他干过活。递锤子的人不需要会打铁----但需要懂打铁的人。他懂。",
                    "锄头我自己修好了。花的时间比两个人做长一倍----但时间对铁匠来说从来不是问题。锤子就在旁边----没人递，但也没有掉。")
            },
            ["Pam"] = new()
            {
                ("傍晚一起在河边坐坐", "Town", 1700, 2100,
                    "----我平时不怎么写字。但今天巴士没来，去了也是坐着。河边那条长椅空了好几天。有人想一起坐的话，傍晚来就行。不用说话----你坐那头，我坐这头。\n---- 潘姆",
                    "{{playerName}}签了名。他突然写得很大----占了留言柱上比别人的字大两倍。这把年纪还能有人愿意陪你坐长椅----潘姆觉得可以再活二十年。",
                    "{{playerName}}真的来了。他坐在长椅的那头。我们坐了快一小时。谁也没说话。然后他说----'河里有条鱼跳了三次'。他数了。他数了！这种话比一百句'你好吗'都好。",
                    "我在长椅上坐了一会儿。没有人来。但那条鱼还是跳了三次。我数了。下次他在的时候----我告诉他。跳了三次。他会相信的。")
            },
            ["Penny"] = new()
            {
                ("在图书馆读一本书", "Town", 1000, 1500,
                    "图书馆有一本书----《星露谷的野花图谱》----快被翻烂了。我想在你来之前把它重新装订好。然后我们可以一起读。不需要从头读----翻到哪页读哪页。那样更有意思。\n---- 潘妮",
                    "{{playerName}}在留言柱上签了字----还附了一句'翻到雏菊那页'。他知道这本书。他在图书馆坐过。他记得！我把雏菊那页夹了一张书签。",
                    "{{playerName}}来了。我们翻到了雏菊那页----他读一句，我读一句。然后他翻到了最后一页----空白的----问：'这里少了什么？'我说：'这里等你来写。'他拿起笔，画了一朵我没见过的花。",
                    "书重新装订好了。雏菊那页的书签还在。他没来----但书是完整的。空白页还在等他画那朵花。有些事不着急。书不急，花不急，我也不急。")
            },
            ["Sam"] = new()
            {
                ("在阳台上听音乐", "Town", 1400, 1900,
                    "练好了一首新曲子----自己写的。没弹给任何人听过。不是因为害羞----是还没找到对的人。找对人比找对音还难。你要是想听就来。我家阳台----音效最好。\n---- 山姆",
                    "{{playerName}}在留言柱上签了字----还画了一把很小的吉他。那把吉他只有三根弦。但我懂----他是认真的。下午，阳台。",
                    "他来了。我第一次在别人面前弹这首歌----弹错了两个音。他说'第三遍的时候对了'。他数了。他在听。一个会数遍数的人----比一百个说'好听'的人更重要。",
                    "我对着墙弹了那首歌。弹对了所有的音。一个人听自己的歌也是听----但感觉不一样。下次----下次我会先问他有没有空。")
            },
            ["Sebastian"] = new()
            {
                ("夜里一起骑车去海边", "Beach", 2000, 2500,
                    "晚上的海边没人。只有摩托的引擎声和海浪----那种反拍子。不想说话也行。就待在旁边。我在留言柱上放这个不太像我----但就这样吧。\n---- 塞巴斯蒂安",
                    "{{playerName}}签了名。没有多余的字。不画东西，不打问号----就签名。这种答复我最放心：不期待太多的人，反而让见面变得容易。",
                    "{{playerName}}准时到了。他坐在我摩托后座----没有吵着要骑。我们在海边待到凌晨。他问了一个问题：'海的尽头是什么。'我没回答。不是不知道----是这种问题不需要答案。他懂了。他再也没问。",
                    "一个人骑到海边。海还是那个海。他没来。也许他今晚想一个人。我懂的----有时候答应一件事需要比做那件事更大的力气。没关系。海浪的声音不用人陪也好听。")
            },
            ["Haley"] = new()
            {
                ("在花田里拍照", "Town", 1300, 1700,
                    "星露谷的向日葵开了----比去年早两周。光线好的时候，向日葵的金色会反映在人的皮肤上。想拍几张----需要一个人站在花田里。不用摆姿势。就站着。让花来做剩下的。\n---- 海莉",
                    "{{playerName}}签了名，然后贴了一小片干掉的向日葵花瓣在留言柱上。不知道他从哪儿找的。但----他知道我在说什么。下午。花田。",
                    "{{playerName}}站在花田里，像他本来就属于那里。我拍了七张----最后一张最好：风把一朵向日葵吹到他肩膀旁边，他没动。不是那种'不动'----是那种'我在这里，你想拍多久都可以'的不动。",
                    "花田的光比昨天还美。我没拍。有时候最好的光是没有人在里面的时候----但那种照片只能放在心里，不能给别人看。他没来。光替他来了。")
            },
            ["Shane"] = new()
            {
                ("在悬崖边看日落", "Forest", 1800, 2200,
                    "煤矿森林最高的那个悬崖----日落的时候往下看，整个镇子变成橙色的。我每天晚上都在那儿。你要来也可以。不用说话。日落不需要评论。\n---- 谢恩",
                    "{{playerName}}签了名----只是一个'好'字。好的意思是不多问。好的意思是知道了。好的意思是'我会来的但我不需要做任何保证'。我喜欢这个'好'。",
                    "{{playerName}}真的来了。他没说话----就是站在旁边。太阳落下去的时候他叹了一口气。那种叹气不是难过----是满足。日落会让人叹气----但他那种叹气让我觉得我也应该叹气。然后我真的叹了一口气。感觉很好。",
                    "一个人看了日落。还是橙色的。他没来。也许他今天不想看橙色。也许他看了另一种颜色。不该生气----日落每天都来，人不是。")
            },
            ["Evelyn"] = new()
            {
                ("一起烤饼干", "Town", 1000, 1400,
                    "老头子说面粉快过期了。过期了多可惜----不如拿来烤一批饼干。但饼干这种东西----一个人烤是烤，两个人烤是'一起'。你来了就帮你留一半----另一半给乔治。他牙不好但还是会偷吃。\n---- 艾芙琳",
                    "{{playerName}}签了名----一笔一划写得很整齐。一看就是礼貌孩子。我把最好的那袋面粉拿出来了----不是快过期的那袋。",
                    "{{playerName}}来了。他揉面的时候----面粉沾得满袖子都是。但他没管。他一直在揉。饼干出炉的时候他自己先掰了一块----然后马上掰了一半给我。这个动作----我老了，但我认识。这是'谢谢你'----不用嘴说的那种。",
                    "饼干烤好了。面粉没用完----还剩半袋。半袋也能放。他没来----但是面团的香气还在厨房里。有时候香气比人待得久。")
            },
            ["Harvey"] = new()
            {
                ("在诊所后面看星星", "Town", 2000, 2400,
                    "诊所天台上有我的一台旧望远镜。不是专业的----但看月亮够了。今晚天气晴，月亮会很亮。如果你不忙----上来坐坐。不用怕打扰。医生晚上不忙。\n---- 哈维",
                    "{{playerName}}签了名。他的字迹端正但不紧张----那种不让人担心血压的笔迹。今晚天文台。我备了热巧克力。",
                    "{{playerName}}来了。他对着望远镜看了很久。'那个坑----是月亮上最老的吗？'他问。我说是。他说：'那它最不孤独。它被看了最多次。'一个会把陨石坑和孤独联系起来的人----不是普通病人。他是朋友。",
                    "月亮自己升上来了。望远镜没人动。热巧克力凉了。他在忙吧？医生的加班只有一种原因----但农场主有二十种。明天再叫他。")
            },
            ["Elliott"] = new()
            {
                ("在沙滩上读一首还没写完的诗", "Beach", 1500, 2000,
                    "写了一首诗----副歌部分卡住了。诗的结尾需要第二个人的耳朵。不----不是说需要建议。是需要空气里有另一个人的呼吸，让诗的韵律找到落点。如果你刚好在海边----过来。我念，你听。\n---- 艾利欧特",
                    "{{playerName}}签了名----还用花体字写了自己的名字。他在配合我的语调。一个用花体字签名的人----我的诗已经找到了读者。",
                    "{{playerName}}来了。我念了那首诗。念到卡住的地方----他看着海说：'下一行在这。'然后他不说话了。我写了下一行。可能是整首诗里最好的一行。诗人一生都在等这一个人----不是缪斯，是那个帮你找到下一行的人。",
                    "诗还是没写完。副歌那行空着。海风替他听了，但海风不识字。也许下次----也许那行诗本来就不该有人接。也许空着也是一种诗。")
            },
            ["Alex"] = new()
            {
                ("在球场上扔球", "Town", 1400, 1800,
                    "下午球场空着。我有个旧橄榄球----有点瘪了但还能扔。一个人对着墙扔也行，但墙不会扔回来。你要是手痒过来就行----不用会。不会的人学得最快。\n---- 亚历克斯",
                    "{{playerName}}在留言柱上签了字----然后写'下午球场上见'。那个'见'字写得特别大。好。下午。球场。我多带了一个球----万一他想练。",
                    "{{playerName}}来了。他传球----第一次偏了，第二次偏了，第三次到我手里。然后第四次----他跑到了我没想到的位置。那个位置----职业球员才会站的位置。这个人学了三把就懂。他不是运动员----但他有运动员的眼睛。",
                    "球场空着。我一个人对着墙扔了一会儿。墙不会跑位----但它也不会抱怨。他没来。也许下次。球还在，墙还在，下午还会有。")
            }
        };

        public InvitationSystem(IModHelper helper, IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>Generate daily invitations based on memory clarity</summary>
        public void GenerateDailyInvitations()
        {
            _activeInvitations.Clear();

            if (Game1.player == null) return;

            // Only NPCs who remember the player well enough will send invitations
            var eligibleNpcs = NpcInvitations.Keys
                .Where(npc => MemoryClaritySystem.GetClarityTier(npc) >= ClarityTier.Recognized)
                .ToList();

            if (eligibleNpcs.Count == 0)
            {
                _monitor.Log("[Invitation] No NPCs have sufficient memory clarity yet.", LogLevel.Debug);
                return;
            }

            int count = Math.Min(RandomHelper.Next(2, 5), eligibleNpcs.Count);
            RandomHelper.Shuffle(eligibleNpcs);
            eligibleNpcs = eligibleNpcs.Take(count).ToList();

            int today = (int)Game1.stats.DaysPlayed;

            foreach (string npc in eligibleNpcs)
            {
                var templates = NpcInvitations[npc];
                var t = templates[RandomHelper.Next(templates.Count)];

                // 30% chance to include this NPC's invitation today
                if (!RandomHelper.Chance(0.7)) continue;

                var invitation = new InvitationData
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    FromNpc = npc,
                    Activity = t.activity,
                    Location = t.location,
                    TimeOfDay = TimeRangeToPeriod(t.timeStart),
                    GameTimeStart = t.timeStart,
                    GameTimeEnd = t.timeEnd,
                    GeneratedDay = today,
                    ExpiryDay = today + 1, // 1 day to accept
                    DisplayText = t.displayText,
                    AcceptText = t.acceptResponse,
                    FulfillText = t.fulfillMemory,
                    MissText = t.missMemory,
                    IsAccepted = false,
                    IsFulfilled = false,
                    IsMissed = false
                };

                _activeInvitations.Add(invitation);
            }

            _monitor.Log($"[Invitation] Generated {_activeInvitations.Count} invitations from {eligibleNpcs.Count} candidates.", LogLevel.Debug);
        }

        /// <summary>Player accepts an invitation from the board</summary>
        public void AcceptInvitation(string invitationId)
        {
            var invitation = _activeInvitations.FirstOrDefault(i => i.Id == invitationId);
            if (invitation == null) return;

            invitation.IsAccepted = true;
            _history.Add(invitation);
            _activeInvitations.Remove(invitation);

            if (ModEntry.Journal != null && Game1.player != null)
            {
                string text = StringHelper.ReplaceParameters(invitation.AcceptText,
                    new Dictionary<string, string>
                    {
                        ["playerName"] = Game1.player.Name,
                        ["npcName"] = invitation.FromNpc,
                        ["location"] = invitation.Location,
                        ["activity"] = invitation.Activity
                    });

                var entry = new JournalEntry
                {
                    NpcName = invitation.FromNpc,
                    DisplayText = text,
                    EmotionTag = "Warm",
                    Trigger = TriggerType.DirectInteraction,
                    DaysPlayed = (int)Game1.stats.DaysPlayed,
                    Season = Game1.currentSeason,
                    Year = Game1.year,
                    DayOfMonth = Game1.dayOfMonth,
                    TimeOfDay = Game1.timeOfDay,
                    LocationName = invitation.Location,
                    Category = "Invitation"
                };
                ModEntry.Journal.AddEntry(entry);
            }

            // Record clarity boost for the interaction
            MemoryClaritySystem.RecordInteraction(invitation.FromNpc, 2.0f);

            _monitor.Log($"[Invitation] Accepted: {invitation.FromNpc} -- {invitation.Activity} at {invitation.Location} ({invitation.TimeOfDay})", LogLevel.Info);
        }

        /// <summary>Check each tick: warp NPCs to invitation spots, detect player arrival</summary>
        public void OnUpdateTicked()
        {
            if (Game1.player == null || !Context.IsWorldReady) return;

            var activeToday = _history.Where(i =>
                i.IsAccepted && !i.IsFulfilled && !i.IsMissed &&
                i.GeneratedDay <= (int)Game1.stats.DaysPlayed &&
                i.GeneratedDay >= (int)Game1.stats.DaysPlayed - 1
            ).ToList();

            foreach (var inv in activeToday)
            {
                int now = Game1.timeOfDay;

                // Outside the time window -- nothing to do
                if (now < inv.GameTimeStart || now > inv.GameTimeEnd) continue;

                // Find or warp the NPC to the invitation location
                var npc = Game1.getCharacterFromName(inv.FromNpc);
                if (npc == null) continue;

                var targetLocation = Game1.getLocationFromName(inv.Location);
                if (targetLocation == null) continue;

                // Warp NPC to the invitation spot if they're elsewhere
                if (npc.currentLocation?.Name != inv.Location)
                {
                    try
                    {
                        Game1.warpCharacter(npc, inv.Location,
                            new Microsoft.Xna.Framework.Vector2(10, 10));
                        _monitor.Log($"[Invitation] Warped {inv.FromNpc} to {inv.Location}.", LogLevel.Debug);
                    }
                    catch (Exception ex)
                    {
                        _monitor.Log($"[Invitation] Failed to warp {inv.FromNpc}: {ex.Message}", LogLevel.Debug);
                    }
                }

                // Check if player is nearby
                float distance = Microsoft.Xna.Framework.Vector2.Distance(
                    Game1.player.Position, npc.Position);

                if (distance < 180f)
                {
                    // NPC notices player's arrival -- personality-based greeting
                    if (!inv.IsFulfilled && !_greetedInvitations.Contains(inv.Id))
                    {
                        _greetedInvitations.Add(inv.Id);
                        string greetLine = GetArrivalGreeting(inv.FromNpc, inv.Activity);
                        Game1.drawObjectDialogue(greetLine);
                    }

                    // Mark as fulfilled when player interacts or stays nearby
                    if (distance < 100f && !inv.IsFulfilled)
                    {
                        // If player is very close and facing NPC, trigger fulfillment
                        if (IsPlayerFacing(Game1.player, npc))
                        {
                            inv.IsFulfilled = true;
                            string responseLine = GetFulfillmentDialogue(inv.FromNpc, inv.Activity);
                            Game1.drawObjectDialogue(responseLine);
                            Game1.afterDialogues = () => GenerateFulfilledMemory(inv);
                            _monitor.Log($"[Invitation] Player met {inv.FromNpc} at {inv.Location}!", LogLevel.Info);
                        }
                    }
                }
            }
        }

        /// <summary>Handle player interacting with the waiting NPC directly</summary>
        public bool OnNpcInteract(NPC npc)
        {
            var active = _history.FirstOrDefault(i =>
                i.IsAccepted && !i.IsFulfilled && !i.IsMissed &&
                i.FromNpc == npc.Name &&
                Game1.timeOfDay >= i.GameTimeStart && Game1.timeOfDay <= i.GameTimeEnd);

            if (active == null) return false;

            active.IsFulfilled = true;
            string line = GetFulfillmentDialogue(npc.Name, active.Activity);
            Game1.drawObjectDialogue(line);
            Game1.afterDialogues = () => GenerateFulfilledMemory(active);
            return true;
        }

        private readonly HashSet<string> _greetedInvitations = new();

        private static bool IsPlayerFacing(Farmer player, NPC npc)
        {
            var dir = npc.Position - player.Position;
            int facing = player.FacingDirection;
            return facing switch
            {
                0 => dir.Y < 0 && Math.Abs(dir.X) < 64,   // Up
                1 => dir.X > 0 && Math.Abs(dir.Y) < 64,   // Right
                2 => dir.Y > 0 && Math.Abs(dir.X) < 64,   // Down
                3 => dir.X < 0 && Math.Abs(dir.Y) < 64,   // Left
                _ => false
            };
        }

        /// <summary>Personality-based greeting when player arrives at the spot</summary>
        private static string GetArrivalGreeting(string npc, string activity)
        {
            return npc switch
            {
                "Abigail" => "嘿！你来了！我正拿一块石英对着光看----你过来瞧瞧。",
                "Emily" => "你来了！今天的云刚刚变成了一条鲸鱼----快看，它还在变。",
                "Gus" => "哈哈，你果然来了！厨房的汤还热着呢----我专门等你到了才盛第二碗。",
                "Leah" => "（从速写本上抬起头）你来了。今天的苔藓颜色比昨天深了一点----你看。",
                "Linus" => "（微笑）风刚告诉我有人上山了。来，坐这儿----这块石头是暖的。",
                "Willy" => "哈！年轻人果然来了！咖啡还热----趁涨潮前。",
                "Robin" => "我就知道你会来。椅子给你留好了----那把橡木的，扶手最宽。",
                "Maru" => "啊你来了！装置已经偏了四十三次了----数据还没跑完。来帮我看看。",
                "Clint" => "（抬头）来了。锤子在那儿----不用递太多，递三把就够了。",
                "Pam" => "长椅那头给你留着呢。坐吧。今天河里有条鱼跳了两次了。",
                "Penny" => "你来了！雏菊那页的书签还在----我等你来翻。",
                "Sam" => "哟！吉他已经调好音了----就等你来。这首歌第三节最好听。",
                "Sebastian" => "（微微点头）你来了。海风今晚刚好----不冷不热。",
                "Haley" => "光线刚好！别动----不是让你别动，是让光别动。算了，你站那就行。",
                "Shane" => "（抬头看你一眼，然后继续看日落）坐吧。还有十分钟太阳就下去了。",
                "Evelyn" => "哎呀你来了！面粉已经筛好了----来，围裙给你。",
                "Harvey" => "你来了！望远镜已经对准了----今晚的月亮特别亮。热巧克力在保温杯里。",
                "Elliott" => "（合上笔记本）你来得正好----我刚写到副歌。海风把你的脚步声带过来了。",
                "Alex" => "来了！球已经准备好了----这次我多带了两个。来，先热热身。",
                _ => $"你来了！{activity}----为这一刻我准备了一整天。"
            };
        }

        /// <summary>Dialogue when the player fully interacts with the waiting NPC</summary>
        private static string GetFulfillmentDialogue(string npc, string activity)
        {
            return npc switch
            {
                "Abigail" => "跟你一起捡石头比一个人捡有趣多了。你找到的那块紫水晶----我帮你放在背包里了。下次再来！",
                "Emily" => "那条云鲸鱼你看到了吧？它游走了----但我们看到了。这就够了。下次再一起看云！",
                "Gus" => "三碗汤----你喝了三碗。厨房这辈子值了。以后想来随时来----不用留言柱。",
                "Leah" => "你的那棵'扫帚树'我夹在速写本里了。认真的画比好的画珍贵。谢谢你来。",
                "Linus" => "风今晚说了很多。它说你是个好听众。山谷记得好听众。帐篷外随时欢迎你。",
                "Willy" => "那条鱼今天还是没来。但咖啡喝完了----这就够了。海钓的人不在乎鱼来不来，只在乎有没有人一起喝咖啡。",
                "Robin" => "你坐过那把椅子了。木头记得你的温度。下次来工坊----我教你做一把。",
                "Maru" => "数据跑完了。偏左117次----每次偏左都在提醒我：不完美是最好的实验条件。谢谢你帮我记录。",
                "Clint" => "锄头修好了。你递的锤子----每一把都在刃上。递锤子的人比打铁的人难找。",
                "Pam" => "鱼跳了三次。我数了。下次你在的时候它还会跳的。长椅永远有你的位置。",
                "Penny" => "你画的那朵花还在最后一页。书现在完整了----因为有人读了它。来图书馆随时找我。",
                "Sam" => "第三节你听出来了----就是那个升key的地方。以后写了新歌，你先听。",
                "Sebastian" => "今晚的引擎声和海浪合在一起了。你没说话----但你在。这就够了。",
                "Haley" => "那张照片----风把向日葵吹到你肩膀上的那张----我洗了两份。一份给你。",
                "Shane" => "太阳下去了。你看到了。不说也知道。明天傍晚还在这儿。不一定约----但你可以来。",
                "Evelyn" => "饼干好吃吗？那个配方我用了四十年。下次教你----不难，但要用心。带一盒回去给乔治吧。",
                "Harvey" => "那个陨石坑----它被看了最多次。今晚你也是看它的人之一了。天台的门一直开着。",
                "Elliott" => "诗写完了。副歌那一行----是你帮我找到的。谢谢你。下一首诗的第一句是你的名字。",
                "Alex" => "第四次传球----你站到了职业球员的位置。我看到了。下次我们练三步上篮。",
                _ => $"谢谢你今天来。{activity}----因为有你在，这一天变得不一样了。"
            };
        }

        /// <summary>Check at end of day -- mark unfulfilled invitations as missed</summary>
        public void CheckFulfillment()
        {
            if (Game1.player == null) return;

            foreach (var inv in _history.Where(i => i.IsAccepted && !i.IsFulfilled && !i.IsMissed))
            {
                // Past the invitation day? Mark as missed
                if (inv.GeneratedDay < (int)Game1.stats.DaysPlayed - 1)
                {
                    inv.IsMissed = true;
                    GenerateMissedMemory(inv);
                }
            }
            _greetedInvitations.Clear();
        }

        private void GenerateFulfilledMemory(InvitationData inv)
        {
            if (ModEntry.Journal == null || Game1.player == null) return;

            string text = StringHelper.ReplaceParameters(inv.FulfillText,
                new Dictionary<string, string>
                {
                    ["playerName"] = Game1.player.Name,
                    ["npcName"] = inv.FromNpc,
                    ["location"] = inv.Location,
                    ["activity"] = inv.Activity
                });

            var entry = new JournalEntry
            {
                NpcName = inv.FromNpc,
                DisplayText = text,
                EmotionTag = "Warm",
                Trigger = TriggerType.DirectInteraction,
                DaysPlayed = (int)Game1.stats.DaysPlayed,
                Season = Game1.currentSeason,
                Year = Game1.year,
                DayOfMonth = Game1.dayOfMonth,
                TimeOfDay = Game1.timeOfDay,
                LocationName = inv.Location,
                Category = "Invitation"
            };
            ModEntry.Journal.AddEntry(entry);

            // Big clarity boost for actually showing up
            MemoryClaritySystem.RecordInteraction(inv.FromNpc, 3.0f);

            _monitor.Log($"[Invitation] Fulfilled: {inv.FromNpc} -- {inv.Activity}", LogLevel.Info);
        }

        private void GenerateMissedMemory(InvitationData inv)
        {
            if (ModEntry.Journal == null || Game1.player == null) return;

            string text = StringHelper.ReplaceParameters(inv.MissText,
                new Dictionary<string, string>
                {
                    ["playerName"] = Game1.player.Name,
                    ["npcName"] = inv.FromNpc,
                    ["location"] = inv.Location,
                    ["activity"] = inv.Activity
                });

            var entry = new JournalEntry
            {
                NpcName = inv.FromNpc,
                DisplayText = text,
                EmotionTag = "Melancholy",
                Trigger = TriggerType.Absence,
                DaysPlayed = (int)Game1.stats.DaysPlayed,
                Season = Game1.currentSeason,
                Year = Game1.year,
                DayOfMonth = Game1.dayOfMonth,
                TimeOfDay = Game1.timeOfDay,
                LocationName = inv.Location,
                Category = "Invitation"
            };
            ModEntry.Journal.AddEntry(entry);

            // Small clarity loss for standing someone up
            MemoryClaritySystem.RecordInteraction(inv.FromNpc, -0.5f);

            _monitor.Log($"[Invitation] Missed: {inv.FromNpc} -- {inv.Activity}", LogLevel.Info);
        }

        public List<InvitationData> GetActiveInvitations() => _activeInvitations.ToList();
        public List<InvitationData> GetAcceptedInvitations() => _history.Where(i => i.IsAccepted && !i.IsFulfilled && !i.IsMissed).ToList();

        /// <summary>Helper -- check if player visited a specific location today</summary>
        internal static class TriggerDetector
        {
            private static readonly HashSet<string> _visitedLocationsToday = new();

            public static void RecordLocationVisit(string locationName)
            {
                _visitedLocationsToday.Add(locationName);
            }

            public static bool CheckLocationVisit(string locationName)
            {
                return _visitedLocationsToday.Contains(locationName);
            }

            public static void Reset()
            {
                _visitedLocationsToday.Clear();
            }
        }

        // ── Save/Load ──

        public void OnSaveLoaded()
        {
            _history = ModDataHelper.LoadList<InvitationData>(SaveKey);
            _activeInvitations.Clear();
        }

        public void OnSaving()
        {
            var all = new List<InvitationData>();
            all.AddRange(_history);
            all.AddRange(_activeInvitations);
            ModDataHelper.SaveList(SaveKey, all);
        }

        public void OnDayStarted()
        {
            TriggerDetector.Reset();
            GenerateDailyInvitations();
        }

        public void OnDayEnding()
        {
            CheckFulfillment();
        }

        /// <summary>Convert game time to period name</summary>
        private static string TimeRangeToPeriod(int time) => time switch
        {
            < 1000 => "清晨",
            < 1200 => "上午",
            < 1700 => "下午",
            < 2000 => "傍晚",
            _ => "夜晚"
        };

        /// <summary>Record that the player visited a location (called from OnWarped)</summary>
        public void RecordVisit(string locationName)
        {
            TriggerDetector.RecordLocationVisit(locationName);
        }
    }
}
