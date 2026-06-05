namespace EchoesOfTheHollow
{
    /// <summary>
    /// Mod configuration model.
    /// Supports Generic Mod Config Menu for in-game editing.
    /// </summary>
    public class ModConfig
    {
        // ── Core Toggles ──
        public bool EnableJournal { get; set; } = true;
        public bool EnableBasket { get; set; } = true;
        public bool EnableDriftBox { get; set; } = true;
        public bool EnableEnthusiasm { get; set; } = true;
        public bool EnableInvitations { get; set; } = true;
        public bool EnableFestivalOverrides { get; set; } = true;
        public bool EnableOfflineMemories { get; set; } = true;

        // ── Activity Toggles ──
        public bool EnableDaydreaming { get; set; } = true;
        public bool EnableWindListening { get; set; } = true;
        public bool EnableOldObjects { get; set; } = true;
        public bool EnableAnimalTracking { get; set; } = true;
        public bool EnableMeaninglessItems { get; set; } = true;
        public bool EnablePlayerDiary { get; set; } = true;

        // ── Harmony Patch Toggles (for compatibility) ──
        public bool PatchMoney { get; set; } = true;
        public bool PatchSkills { get; set; } = true;
        public bool PatchFriendship { get; set; } = true;
        public bool PatchAchievements { get; set; } = true;
        public bool PatchShippingBin { get; set; } = true;
        public bool PatchQuestBoard { get; set; } = true;
        public bool PatchSocialMenu { get; set; } = true;
        public bool PatchCollections { get; set; } = true;
        public bool PatchHud { get; set; } = true;
        public bool PatchShops { get; set; } = true;
        public bool PatchEvents { get; set; } = true;
        public bool PatchDialogue { get; set; } = true;

        // ── Journal Settings ──
        public int MaxJournalEntries { get; set; } = 500;      // Keeps performance stable
        public int NightlyMemoryCount { get; set; } = 3;        // 1-5 entries generated each night
        public bool JournalAutoOpenOnNew { get; set; } = false; // Don't disrupt gameplay
        public string JournalKeybind { get; set; } = "J";

        // ── Basket Settings ──
        public int BasketReturnDays { get; set; } = 3;          // Days before unclaimed items return
        public int BasketExchangeCheckHour { get; set; } = 7;   // 7 AM exchange check
        public bool ShowBasketNotification { get; set; } = true;

        // ── Enthusiasm Settings ──
        public float EnthusiasmDecayRate { get; set; } = 0.008f;
        public float EnthusiasmDaydreamRecovery { get; set; } = 0.015f;
        public float EnthusiasmWindRecovery { get; set; } = 0.012f;
        public bool ShowEnthusiasmHud { get; set; } = true;
        public string EnthusiasmHudPosition { get; set; } = "Right"; // Left or Right

        // ── Offline Settings ──
        public int MinOfflineHoursForMemories { get; set; } = 12;
        public int MaxOfflineMemories { get; set; } = 5;

        // ── Memory Template Back Door Settings ──
        public bool LoadCustomTemplates { get; set; } = true;
        public string CustomTemplatesPath { get; set; } = "assets/data/templates";
        public bool AllowContentPatcherTemplates { get; set; } = true;
        public bool EnableAccessAPI { get; set; } = true;

        // ── NPC Settings ──
        public bool ShowNpcNamesInJournal { get; set; } = true;
        public bool UseNpcColorCoding { get; set; } = true;

        // ── Visual Settings ──
        public float JournalPageAgingSpeed { get; set; } = 0.3f;   // Paper yellowing over time
        public bool ShowWeatherEffectsOnJournal { get; set; } = true;
        public float UiScale { get; set; } = 1.0f;

        // ── Debug ──
        public bool DebugMode { get; set; } = false;
        public bool ShowTriggerLog { get; set; } = false;
    }
}
