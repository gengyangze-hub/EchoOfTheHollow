using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
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

        // --- UI ---
        private EnthusiasmHud? _enthusiasmHud;

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
            helper.Events.Display.RenderingHud += OnRenderingHud;
            helper.Events.Display.RenderedHud += OnRenderedHud;
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

            // ── Initialize UI ──
            _enthusiasmHud = new EnthusiasmHud(helper, Monitor, _enthusiasmSystem);

            Log.Info("✅ All systems initialized. Mod ready!");
            Log.Info("   Memory templates loaded: " + _templateEngine.TemplateCount);
            Log.Info("   NPC voices loaded: " + _voiceRegistry.VoiceCount);
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

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // Open journal with 'J' key
            if (e.Button == SButton.J && Context.IsPlayerFree)
            {
                OpenJournalMenu();
            }

            // Open basket with 'B' key
            if (e.Button == SButton.B && Context.IsPlayerFree)
            {
                OpenBasketMenu();
            }

            // Open memory book with 'H' key (social/hearts replacement)
            if (e.Button == SButton.H && Context.IsPlayerFree)
            {
                OpenMemoryBook();
            }

            // Daydreaming: hold a position key
            _daydreamingSystem?.OnButtonPressed(e.Button);
        }

        private void OnRenderingHud(object? sender, RenderingHudEventArgs e)
        {
            _enthusiasmHud?.OnRenderingHud();
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            _enthusiasmHud?.OnRenderedHud();
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

        /// <summary>Clamp config values to safe ranges to prevent crashes</summary>
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
            if (Instance == null || Instance._journalSystem == null) return;

            var menu = new EchoJournalMenu(
                Instance._journalSystem,
                Instance._voiceRegistry!,
                Instance.Helper
            );
            Game1.activeClickableMenu = menu;
        }

        /// <summary>Open the memory book (social tab replacement)</summary>
        public static void OpenMemoryBook()
        {
            if (Instance == null || Instance._journalSystem == null) return;

            var menu = new MemoryBookMenu(
                Instance._journalSystem,
                Instance._voiceRegistry!
            );
            Game1.activeClickableMenu = menu;
        }

        /// <summary>Open the basket interaction UI</summary>
        public static void OpenBasketMenu()
        {
            if (Instance == null || Instance._basketSystem == null || Instance._journalSystem == null) return;

            var menu = new BasketMenu(
                Instance._basketSystem,
                Instance._journalSystem
            );
            Game1.activeClickableMenu = menu;
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
