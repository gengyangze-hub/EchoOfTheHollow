using HarmonyLib;

namespace EchoesOfTheHollow.Patches
{
    internal static class FarmerPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchMoney) return;

            // Money display is hidden by HudPatches (DrawMoneyBox_Prefix returns false).
            // We do NOT patch the Money getter — keeping actual money intact allows:
            // - Mermaid Pendant purchase at Old Mariner (5000g check)
            // - JojaMart membership (50000g check)
            // - All other backend money checks that need genuine gold tracking
            // Money is never displayed to the player — it just works silently behind the scenes.
        }

        public static void ApplySkills(Harmony harmony, ModConfig config)
        {
            if (!config.PatchSkills) return;

            // Intercept experience gain
            var gainExp = AccessTools.Method(typeof(Farmer), "gainExperience",
                new[] { typeof(int), typeof(int) });
            var gainExpPrefix = typeof(FarmerPatches).GetMethod(nameof(GainExperience_Prefix));
            if (gainExp != null && gainExpPrefix != null)
                harmony.Patch(gainExp, new HarmonyMethod(gainExpPrefix));
        }

        public static bool GainExperience_Prefix(int which, ref int howMuch)
        {
            return false; // No XP gain
        }

    }
}
