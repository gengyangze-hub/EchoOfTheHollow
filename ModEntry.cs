using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley.Menus;
using EchoesOfTheHollow.Systems.Journal;
using EchoesOfTheHollow.Systems.Economy;
using EchoesOfTheHollow.Systems.Enthusiasm;
using EchoesOfTheHollow.Systems.Invitation;
using EchoesOfTheHollow.Systems.Offline;
using EchoesOfTheHollow.Systems.Activities;
using EchoesOfTheHollow.Systems.Festival;
using EchoesOfTheHollow.Systems;
using EchoesOfTheHollow.NpcProfiles;
using EchoesOfTheHollow.UI;
using EchoesOfTheHollow.Patches;
using EchoesOfTheHollow.Utils;
using HarmonyLib;

namespace EchoesOfTheHollow
{
    /// <summary>
    /// 空谷回音 - 主模组入口
    /// Echoes of the Hollow - Main mod entry point
    /// </summary>
    public class ModEntry : Mod
    {
        // --- Core Systems ---
        internal static ModEntry? Instance { get; private set; }
        internal static ModConfig Config { get; private set; } = null!;
        internal static IMonitor? StaticMonitor { get; private set; }

        private JournalSystem? _journalSystem;
        private BasketSystem? _basketSystem;
        private DriftBoxSystem? _driftBoxSystem;
        private EnthusiasmSystem? _enthusiasmSystem;
        private InvitationSystem? _invitationSystem;
        private OfflineTimeManager? _offlineTimeManager;
        private VoiceRegistry? _voiceRegistry;
        private MemoryTemplateEngine? _templateEngine;
        private TriggerDetector? _triggerDetector;
        private PlayerDiarySystem? _playerDiarySystem;

        // --- Activity Systems ---
        private DaydreamingSystem? _daydreamingSystem;
        private WindListeningSystem? _windListeningSystem;
        private OldObjectInteraction? _oldObjectInteraction;
        private AnimalTrackingSystem? _animalTrackingSystem;
        private MeaninglessItemSystem? _meaninglessItemSystem;
        private NpcActiveGreeting? _npcActiveGreeting;

        // --- Festival ---
        private FestivalReplacer? _festivalReplacer;

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Config = helper.ReadConfig<ModConfig>();
            ValidateConfig();
            StaticMonitor = Monitor;

            Log.Initialize(Monitor);

            // ── Phase 1: Initialize core infrastructure ──
            Log.Info("═══════════════════════════════════════════");
            Log.Info("  空谷回音 / Echoes of the Hollow v1.0.0");
            Log.Info("═══════════════════════════════════════════");
            Log.Info("Initializing systems...");

            // Register game event handlers
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.GameLoop.Saved += OnSaved;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
            helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Input.ButtonsChanged += OnButtonsChanged;
            helper.Events.Display.MenuChanged += OnMenuChanged;
            helper.Events.Player.Warped += OnWarped;
            helper.Events.World.LocationListChanged += OnLocationListChanged;

            Log.Info("Event handlers registered.");

            // ── Initialize NPC voice registry ──
            _voiceRegistry = new VoiceRegistry(helper, Monitor);

            // ── Initialize memory template engine (THE BACK DOOR) ──
            _templateEngine = new MemoryTemplateEngine(helper, Monitor, _voiceRegistry);

            // ── Initialize journal system ──
            _journalSystem = new JournalSystem(helper, Monitor, _templateEngine, _voiceRegistry);

            // ── Initialize player diary system ──
            _playerDiarySystem = new PlayerDiarySystem(_journalSystem);

            // ── Initialize economy systems ──
            GoodwillShopSystem.Initialize(Monitor);
            _basketSystem = new BasketSystem(helper, Monitor, _journalSystem);
            _driftBoxSystem = new DriftBoxSystem(helper, Monitor, _journalSystem);

            // ── Initialize enthusiasm system ──
            _enthusiasmSystem = new EnthusiasmSystem(helper, Monitor);

            // ── Initialize invitation system ──
            _invitationSystem = new InvitationSystem(helper, Monitor);

            // ── Initialize trigger detector ──
            _triggerDetector = new TriggerDetector(helper, Monitor, _journalSystem, _voiceRegistry, _templateEngine);

            // ── Initialize activity systems ──
            _daydreamingSystem = new DaydreamingSystem(helper, Monitor, _enthusiasmSystem);
            _windListeningSystem = new WindListeningSystem(helper, Monitor, _enthusiasmSystem);
            _oldObjectInteraction = new OldObjectInteraction(Monitor, _journalSystem);
            _animalTrackingSystem = new AnimalTrackingSystem(Monitor, _journalSystem);
            _meaninglessItemSystem = new MeaninglessItemSystem(_journalSystem);

            // ── Initialize NPC active greeting system ──
            _npcActiveGreeting = new NpcActiveGreeting(Monitor, _journalSystem, _voiceRegistry);

            // ── Initialize festival replacer ──
            _festivalReplacer = new FestivalReplacer(helper, Monitor, _journalSystem);

            // ── Initialize offline time manager ──
            _offlineTimeManager = new OfflineTimeManager(helper, Monitor, _journalSystem, _templateEngine, _voiceRegistry);

            // ── Initialize Harmony patches ──
            HarmonyPatcher.Apply(helper, Config, Monitor);

            // ── Suppress vanilla keybindings that conflict with our UI ──
            // J opens vanilla quest journal, H has no default, B has no default
            // We suppress after Harmony patches so Harmony takes priority
            try { helper.Input.Suppress(SButton.J); } catch { }
            try { helper.Input.Suppress(SButton.H); } catch { }

            Log.Info("✅ All systems initialized. Mod ready!");
            Log.Info("   Memory templates loaded: " + _templateEngine.TemplateCount);
            Log.Info("   NPC voices loaded: " + _voiceRegistry.VoiceCount);
            Log.Info("   Debug mode: ENABLED — check SMAPI console for diagnostics");
        }

        // ═══════════════════════════════════════════════
        //  Game Event Handlers
        // ═══════════════════════════════════════════════

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            Log.Info("Game launched - Echoes of the Hollow ready.");

            // ── Register Generic Mod Config Menu integration ──
            GMCMHelper.Register(Helper.ModRegistry, Helper, Config, Monitor);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            Log.Info("Save loaded - loading mod data...");

            MemoryClaritySystem.OnSaveLoaded();
            EchoJournalMenu.LoadMarks();
            _journalSystem?.OnSaveLoaded();
            _basketSystem?.OnSaveLoaded();
            _driftBoxSystem?.OnSaveLoaded();
            _enthusiasmSystem?.OnSaveLoaded();
            _offlineTimeManager?.OnSaveLoaded();
            _invitationSystem?.OnSaveLoaded();
            _animalTrackingSystem?.OnDayStarted();

            Log.Info("Save data restored.");
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            Log.Debug("Saving...");

            _journalSystem?.OnSaving();
            _basketSystem?.OnSaving();
            _driftBoxSystem?.OnSaving();
            _enthusiasmSystem?.OnSaving();
            MemoryClaritySystem.OnSaving();
            _offlineTimeManager?.OnSaving();
            _invitationSystem?.OnSaving();
        }

        private void OnSaved(object? sender, SavedEventArgs e)
        {
            Log.Debug("Save complete.");
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            Log.Info($"Day started - {Game1.currentSeason} {Game1.dayOfMonth}, Year {Game1.year}");

            GoodwillShopSystem.OnDayStarted();
            MemoryClaritySystem.OnDayStarted();
            ShowBasketMorningPrompt();
            EchoJournalMenu.CheckDayMark();
            _journalSystem?.OnDayStarted();
            _basketSystem?.OnDayStarted();
            _driftBoxSystem?.OnDayStarted();
            _enthusiasmSystem?.OnDayStarted();
            _invitationSystem?.OnDayStarted();
            _festivalReplacer?.CheckAndTriggerToday();
            _triggerDetector?.OnDayStarted();
            _animalTrackingSystem?.OnDayStarted();
            _npcActiveGreeting?.OnDayStarted();

            // ── Day 1 welcome letter ──
            if (Game1.year == 1 && Game1.currentSeason == "spring" && Game1.dayOfMonth == 1)
            {
                ShowWelcomeLetter();
            }
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            Log.Debug("Day ending - generating cross-day observations...");

            _triggerDetector?.GenerateNightlyMemories();
            _animalTrackingSystem?.GenerateNightlyAnimalMemories();
            _journalSystem?.OnDayEnding();
            _basketSystem?.OnDayEnding();
            _enthusiasmSystem?.OnDayEnding();
            _invitationSystem?.OnDayEnding();
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            _enthusiasmSystem?.OnUpdateTicked();
            _daydreamingSystem?.OnUpdateTicked();
            _windListeningSystem?.OnUpdateTicked();
            _animalTrackingSystem?.OnUpdateTicked();
            _npcActiveGreeting?.OnUpdateTicked();
            _invitationSystem?.OnUpdateTicked();
        }

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            _triggerDetector?.OnTimeChanged(e.NewTime);
        }

        /// <summary>SMAPI ButtonsChanged — more reliable than ButtonPressed for custom UIs</summary>
        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (Game1.activeClickableMenu != null) return; // Don't intercept when a menu is already open
            if (!Context.IsWorldReady || Game1.player == null) return;

            foreach (var button in e.Pressed)
            {
                Log.Debug($"[Input] Button pressed: {button} | ActiveMenu={Game1.activeClickableMenu?.GetType().Name ?? "null"} | IsPlayerFree={Context.IsPlayerFree}");

                if (button == SButton.J)
                {
                    Log.Debug("[Input] J pressed — opening Echo Journal");
                    OpenJournalMenu();
                    Helper.Input.SuppressActiveKeybinds(new KeybindList(new Keybind(SButton.J)));
                }
                else if (button == SButton.B)
                {
                    Log.Debug("[Input] B pressed — opening Basket Menu");
                    OpenBasketMenu();
                    Helper.Input.SuppressActiveKeybinds(new KeybindList(new Keybind(SButton.B)));
                }
                else if (button == SButton.H)
                {
                    Log.Debug("[Input] H pressed — opening Memory Book");
                    OpenMemoryBook();
                    Helper.Input.SuppressActiveKeybinds(new KeybindList(new Keybind(SButton.H)));
                }
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // Dual handler — both ButtonPressed and ButtonsChanged fire for reliability
            Log.Debug($"[Input] ButtonPressed: {e.Button} | IsPlayerFree={Context.IsPlayerFree} | ActiveMenu={Game1.activeClickableMenu?.GetType().Name ?? "null"}");

            if (Game1.activeClickableMenu != null) return; // Don't intercept when a menu is open

            if (e.Button == SButton.J && Context.IsPlayerFree)
            {
                Log.Debug("[Input] ButtonPressed J → opening Echo Journal");
                OpenJournalMenu();
            }
            else if (e.Button == SButton.B && Context.IsPlayerFree)
            {
                Log.Debug("[Input] ButtonPressed B → opening Basket Menu");
                OpenBasketMenu();
            }
            else if (e.Button == SButton.H && Context.IsPlayerFree)
            {
                Log.Debug("[Input] ButtonPressed H → opening Memory Book");
                OpenMemoryBook();
            }

            // Daydreaming: hold a position key
            _daydreamingSystem?.OnButtonPressed(e.Button);
        }

        /// <summary>MenuChanged — diagnostic logging only</summary>
        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is ShopMenu && Config.DebugMode)
            {
                Log.Debug($"[MenuChanged] ShopMenu opened. Prices handled by draw patch.");
            }
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            _triggerDetector?.OnLocationChanged(e.NewLocation);
            _windListeningSystem?.OnLocationChanged(e.NewLocation);
            _oldObjectInteraction?.OnLocationChanged(e.NewLocation);
            _invitationSystem?.RecordVisit(e.NewLocation.Name);
        }

        private void OnLocationListChanged(object? sender, LocationListChangedEventArgs e)
        {
            // Track any newly added locations
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            Log.Info("Returned to title - resetting state.");
        }

        /// <summary>Day 1 welcome letter — a single detailed intro</summary>
        private void ShowWelcomeLetter()
        {
            // Write a detailed intro letter as the first journal entry
            _journalSystem?.AddPlayerEntry(
                "一封来自鹈鹕镇的信",
                "欢迎来到鹈鹕镇。\n\n" +
                "在这里，没有人会谈论金钱、技能等级、好感度——这些东西在别处很重要，但在这里，它们被收起来了。\n\n" +
                "取而代之的是「回音」——每个人观察你、记住你、在日志里写下关于你的只言片语。按 J 键打开回音日志，你会看到他们眼中的你。\n\n" +
                "按 H 键打开记忆之书——那里可以按村民浏览他们对你的记忆，每个人都有自己独特的声音。\n\n" +
                "按 B 键打开互惠篮——把不需要的东西放进去，写上你想交换的物品。第二天可能会有人来取走，并留下他们的东西。没有金钱，只有好意。\n\n" +
                "右下角的体力条现在显示的是你的「兴致」——重复做同一件事会让它下降，尝试不同的活动会让它恢复。\n\n" +
                "商店里的物品需要的是你与店主的好意，而非金币。多和他们相处，他们会愿意与你分享。\n\n" +
                "邀约留言柱会出现在镇上——那是NPC们想和你一起做的事。\n\n" +
                "这个世界不记录数字。它记录回音。而你，正在创造回音。\n\n" +
                "—— 鹈鹕镇的每一个人"
            );

            Game1.addHUDMessage(new HUDMessage(
                "你收到了一封信。按 J 打开回音日志查看。",
                HUDMessage.newQuest_type));
        }

        /// <summary>Clamp config values</summary>
        private void ValidateConfig()
        {
            Config.MaxJournalEntries = Math.Clamp(Config.MaxJournalEntries, 50, 2000);
            Config.NightlyMemoryCount = Math.Clamp(Config.NightlyMemoryCount, 1, 5);
            Config.BasketReturnDays = Math.Clamp(Config.BasketReturnDays, 1, 7);
            Config.MaxDailyShopRequests = Math.Clamp(Config.MaxDailyShopRequests, 1, 20);
            Config.GoodwillCostMultiplier = Math.Clamp(Config.GoodwillCostMultiplier, 0.001f, 0.1f);
            Config.EnthusiasmDecayRate = Math.Clamp(Config.EnthusiasmDecayRate, 0.001f, 0.05f);
            Config.EnthusiasmDaydreamRecovery = Math.Clamp(Config.EnthusiasmDaydreamRecovery, 0.001f, 0.05f);
            Config.EnthusiasmWindRecovery = Math.Clamp(Config.EnthusiasmWindRecovery, 0.001f, 0.05f);
            Config.JournalPageAgingSpeed = Math.Clamp(Config.JournalPageAgingSpeed, 0f, 1f);
            Config.MinOfflineHoursForMemories = Math.Clamp(Config.MinOfflineHoursForMemories, 1, 168);
            Config.MaxOfflineMemories = Math.Clamp(Config.MaxOfflineMemories, 1, 20);
            Config.BasketExchangeCheckHour = Math.Clamp(Config.BasketExchangeCheckHour, 6, 12);
            Helper.WriteConfig(Config);
        }

        // ═══════════════════════════════════════════════
        //  Internal Helpers
        // ═══════════════════════════════════════════════

        /// <summary>
        /// Daily morning prompt — replaces the gold counter HUD message.
        /// Design doc: "替换为'今天互惠篮里有没有给你的东西？'的每日提示。"
        /// </summary>
        private void ShowBasketMorningPrompt()
        {
            if (!Config.EnableBasket) return;

            int pending = _basketSystem?.PendingCount ?? 0;
            int matched = _basketSystem?.GetAllItems().Count(i => i.Status == Data.ExchangeStatus.Matched) ?? 0;

            if (pending > 0 && matched > 0)
                Game1.addHUDMessage(new HUDMessage($"互惠篮里有{pending}件东西在等待交换，还有{matched}次交换已经完成了。", HUDMessage.newQuest_type));
            else if (pending > 0)
                Game1.addHUDMessage(new HUDMessage($"互惠篮里有{pending}件东西在等待。今天会不会有人来交换呢？", HUDMessage.newQuest_type));
            else if (RandomHelper.Chance(0.4))
                Game1.addHUDMessage(new HUDMessage("互惠篮今天空着。也许放些什么进去，会有人需要的。", HUDMessage.newQuest_type));
        }

        // ═══════════════════════════════════════════════
        //  Public API Methods
        // ═══════════════════════════════════════════════

        /// <summary>Open the main Echo Journal menu</summary>
        public static void OpenJournalMenu()
        {
            if (Instance == null)
            {
                StaticMonitor?.Log("[UI] OpenJournalMenu failed: Instance is null", LogLevel.Debug);
                return;
            }
            if (Instance._journalSystem == null)
            {
                StaticMonitor?.Log("[UI] OpenJournalMenu failed: _journalSystem is null", LogLevel.Debug);
                return;
            }

            StaticMonitor?.Log("[UI] Opening Echo Journal...", LogLevel.Debug);
            var menu = new EchoJournalMenu(
                Instance._journalSystem,
                Instance._voiceRegistry!,
                Instance.Helper
            );
            Game1.activeClickableMenu = menu;
            Game1.playSound("bigSelect");
            StaticMonitor?.Log("[UI] Echo Journal opened successfully!", LogLevel.Debug);
        }

        /// <summary>Open the memory book (social tab replacement)</summary>
        public static void OpenMemoryBook()
        {
            if (Instance == null)
            {
                StaticMonitor?.Log("[UI] OpenMemoryBook failed: Instance is null", LogLevel.Debug);
                return;
            }
            if (Instance._journalSystem == null)
            {
                StaticMonitor?.Log("[UI] OpenMemoryBook failed: _journalSystem is null", LogLevel.Debug);
                return;
            }

            StaticMonitor?.Log("[UI] Opening Memory Book...", LogLevel.Debug);
            var menu = new MemoryBookMenu(
                Instance._journalSystem,
                Instance._voiceRegistry!
            );
            Game1.activeClickableMenu = menu;
            Game1.playSound("bigSelect");
            StaticMonitor?.Log("[UI] Memory Book opened successfully!", LogLevel.Debug);
        }

        /// <summary>Open the basket interaction UI</summary>
        public static void OpenBasketMenu()
        {
            if (Instance == null)
            {
                StaticMonitor?.Log("[UI] OpenBasketMenu failed: Instance is null", LogLevel.Debug);
                return;
            }
            if (Instance._basketSystem == null || Instance._journalSystem == null)
            {
                StaticMonitor?.Log("[UI] OpenBasketMenu failed: _basketSystem or _journalSystem is null", LogLevel.Debug);
                return;
            }

            StaticMonitor?.Log("[UI] Opening Basket Menu...", LogLevel.Debug);
            var menu = new BasketMenu(
                Instance._basketSystem,
                Instance._journalSystem
            );
            Game1.activeClickableMenu = menu;
            Game1.playSound("bigSelect");
            StaticMonitor?.Log("[UI] Basket Menu opened successfully!", LogLevel.Debug);
        }

        // ── Public accessors for patches ──

        internal static JournalSystem? Journal => Instance?._journalSystem;
        internal static BasketSystem? Basket => Instance?._basketSystem;
        internal static DriftBoxSystem? DriftBox => Instance?._driftBoxSystem;
        internal static EnthusiasmSystem? Enthusiasm => Instance?._enthusiasmSystem;
        internal static InvitationSystem? Invitation => Instance?._invitationSystem;
        internal static OfflineTimeManager? Offline => Instance?._offlineTimeManager;
        internal static TriggerDetector? Triggers => Instance?._triggerDetector;
        internal static VoiceRegistry? Voices => Instance?._voiceRegistry;
        internal static PlayerDiarySystem? Diary => Instance?._playerDiarySystem;
    }
}
