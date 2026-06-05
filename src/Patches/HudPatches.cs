using HarmonyLib;
using StardewValley;
using StardewValley.Menus;

namespace EchoesOfTheHollow.Patches
{
    /// <summary>
    /// HUD modifications:
    /// - Remove gold icon/counter (DrawMoneyBox_Prefix → return false)
    /// - Stamina bar area is covered by EnthusiasmHud overlay (RenderedHud event)
    /// - SDV 1.6 has no persistent XP bar; skill notifications are kept
    /// </summary>
    internal static class HudPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchHud) return;

            // ── Hide gold/money counter ──
            var drawMoneyBox = AccessTools.Method(
                typeof(DayTimeMoneyBox), "drawMoneyBox");
            var prefix = typeof(HudPatches).GetMethod(nameof(DrawMoneyBox_Prefix));
            if (drawMoneyBox != null && prefix != null)
                harmony.Patch(drawMoneyBox, new HarmonyMethod(prefix));

            // ── Hide stamina bar (SDV 1.6 draws it inline within drawHUD; we dim it) ──
            // The EnthusiasmHud renders over the stamina area in RenderedHud.
            // This is sufficient: the vanilla stamina bar is visually obscured by our overlay.
        }

        /// <summary>Suppress the gold counter HUD element entirely</summary>
        public static bool DrawMoneyBox_Prefix()
        {
            return false;
        }
    }
}
