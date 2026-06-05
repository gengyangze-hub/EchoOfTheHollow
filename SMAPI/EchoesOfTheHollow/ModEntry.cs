using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using EchoesOfTheHollow.Systems.Journal;
using EchoesOfTheHollow.Systems.Economy;
using EchoesOfTheHollow.Systems.Enthusiasm;
using EchoesOfTheHollow.Systems.Invitation;
using EchoesOfTheHollow.Systems.Offline;
using EchoesOfTheHollow.Systems.Activities;
using EchoesOfTheHollow.Systems.Festival;
using EchoesOfTheHollow.NpcProfiles;
using EchoesOfTheHollow.UI;
using EchoesOfTheHollow.Patches;
using EchoesOfTheHollow.Utils;

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

        // --- Activity Systems ---
        private DaydreamingSystem? _daydreamingSystem;
        private WindListeningSystem? _windListeningSystem;
        private OldObjectInteraction? _oldObjectInteraction;
        private AnimalTrackingSystem? _animalTrackingSystem;
        private MeaninglessItemSystem? _meaninglessItemSystem;

        // --- Festival ---
        private FestivalReplacer? _festivalReplacer;

        // --- UI ---
        private EnthusiasmHud? _enthusiasmHud;

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Config = helper.ReadConfig<ModConfig>();
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
            helper.Events.Display.MenuChanged += OnMenuChanged;
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

            // ── Initialize economy systems ──
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
            _oldObjectInteraction = new OldObjectInteraction(helper, Monitor, _journalSystem);
            _animalTrackingSystem = new AnimalTrackingSystem(helper, Monitor, _journalSystem);
            _meaninglessItemSystem = new MeaninglessItemSystem(helper, Monitor, _journalSystem);

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

            // Register Generic Mod Config Menu integration if available
            // (To be implemented in Phase 11)
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            Log.Info("Save loaded - loading mod data...");

            _journalSystem?.OnSaveLoaded();
            _basketSystem?.OnSaveLoaded();
            _driftBoxSystem?.OnSaveLoaded();
            _enthusiasmSystem?.OnSaveLoaded();
            _offlineTimeManager?.OnSaveLoaded();
            _invitationSystem?.OnSaveLoaded();

            Log.Info("Save data restored.");
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            Log.Debug("Saving...");

            _journalSystem?.OnSaving();
            _basketSystem?.OnSaving();
            _driftBoxSystem?.OnSaving();
            _enthusiasmSystem?.OnSaving();
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

            _journalSystem?.OnDayStarted();
            _basketSystem?.OnDayStarted();
            _driftBoxSystem?.OnDayStarted();
            _enthusiasmSystem?.OnDayStarted();
            _invitationSystem?.OnDayStarted();
            _triggerDetector?.OnDayStarted();
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            Log.Debug("Day ending - generating cross-day observations...");

            _triggerDetector?.GenerateNightlyMemories();
            _journalSystem?.OnDayEnding();
            _basketSystem?.OnDayEnding();
            _enthusiasmSystem?.OnDayEnding();
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            _enthusiasmSystem?.OnUpdateTicked();
            _daydreamingSystem?.OnUpdateTicked();
            _windListeningSystem?.OnUpdateTicked();
            _animalTrackingSystem?.OnUpdateTicked();
        }

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            _triggerDetector?.OnTimeChanged(e.NewTime);
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // Open journal with 'J' key
            if (e.Button == SButton.J && Context.IsPlayerFree && Config.JournalKeybind == "J")
            {
                OpenJournalMenu();
            }

            // Open basket with 'B' key when near the basket
            if (e.Button == SButton.B && Context.IsPlayerFree)
            {
                _basketSystem?.OnInteract();
            }

            // Daydreaming: hold a position key
            _daydreamingSystem?.OnButtonPressed(e.Button);
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            // Track menu state for HUD/UI management
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
        }

        private void OnLocationListChanged(object? sender, LocationListChangedEventArgs e)
        {
            // Track any newly added locations
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            Log.Info("Returned to title - resetting state.");
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
            // Will be implemented in Phase 6
        }

        /// <summary>Open the basket interaction UI</summary>
        public static void OpenBasketMenu()
        {
            if (Instance == null || Instance._basketSystem == null) return;
            // Will be implemented in Phase 5
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
    }
}
