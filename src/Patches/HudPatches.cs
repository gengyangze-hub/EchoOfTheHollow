using HarmonyLib;
using StardewValley;
using StardewValley.Menus;

namespace EchoesOfTheHollow.Patches
{
    /// <summary>
    /// HUD modifications:
    /// - Remove gold icon/counter (DrawMoneyBox_Prefix → return false)
    /// - Stamina bar is covered visually by EnthusiasmHud overlay (RenderedHud event)
    /// </summary>
    internal static class HudPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchHud) return;

            // ── Hide gold/money counter ──
            var drawMoneyBox = AccessTools.Method(
                typeof(DayTimeMoneyBox), "drawMoneyBox");
            var moneyPrefix = typeof(HudPatches).GetMethod(nameof(DrawMoneyBox_Prefix));
            if (drawMoneyBox != null && moneyPrefix != null)
                harmony.Patch(drawMoneyBox, new HarmonyMethod(moneyPrefix));
        }

        /// <summary>Suppress the gold counter HUD element entirely</summary>
        public static bool DrawMoneyBox_Prefix()
        {
            return false;
        }
    }
}
