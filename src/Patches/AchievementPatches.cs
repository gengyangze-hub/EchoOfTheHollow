using HarmonyLib;
using StardewValley;

namespace EchoesOfTheHollow.Patches
{
    /// <summary>
    /// Block all achievement-related checks and notifications.
    /// No achievement popups, no achievement tracking.
    /// </summary>
    internal static class AchievementPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchAchievements) return;

            var checkForAchievements = AccessTools.Method(
                typeof(Stats), nameof(Stats.checkForAchievements));
            var prefix = typeof(AchievementPatches).GetMethod(nameof(CheckAchievements_Prefix));
            if (checkForAchievements != null && prefix != null)
                harmony.Patch(checkForAchievements, new HarmonyMethod(prefix));
        }

        public static bool CheckAchievements_Prefix()
        {
            return false; // Never check achievements
        }
    }
}
