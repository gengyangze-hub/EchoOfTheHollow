using HarmonyLib;
using StardewValley;
using StardewValley.Menus;

namespace EchoesOfTheHollow.Patches
{
    /// <summary>
    /// HUD modifications:
    /// - Remove gold icon/counter
    /// - Remove stamina bar (replaced by enthusiasm bar)
    /// - Hide XP bar
    /// </summary>
    internal static class HudPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchHud) return;

            var drawMoneyBox = AccessTools.Method(
                typeof(DayTimeMoneyBox), "drawMoneyBox");
            var prefix = typeof(HudPatches).GetMethod(nameof(DrawMoneyBox_Prefix));
            if (drawMoneyBox != null && prefix != null)
                harmony.Patch(drawMoneyBox, new HarmonyMethod(prefix));
        }

        public static bool DrawMoneyBox_Prefix()
        {
            return false; // Don't draw gold/money
        }
    }
}
