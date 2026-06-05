using System;
using HarmonyLib;
using StardewValley;
using EchoesOfTheHollow.Systems.Enthusiasm;

namespace EchoesOfTheHollow.Patches
{
    internal static class FarmerPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchMoney) return;
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

            // Stamina patches -- each wrapped separately so one failure doesn't kill others
            TryPatchStaminaGetter(harmony, config);
            TryPatchStaminaSetter(harmony, config);
            TryPatchToolBegin(harmony, config);
            TryPatchToolAction(harmony, config);
            TryPatchExhaustion(harmony, config);
        }

        private static void TryPatchStaminaGetter(Harmony harmony, ModConfig config)
        {
            if (!config.EnableEnthusiasm) return;
            try
            {
                var getter = AccessTools.PropertyGetter(typeof(Farmer), nameof(Farmer.Stamina));
                var prefix = typeof(FarmerPatches).GetMethod(nameof(Stamina_Getter_Prefix));
                if (getter != null && prefix != null)
                    harmony.Patch(getter, new HarmonyMethod(prefix));
            }
            catch (Exception ex)
            {
                ModEntry.StaticMonitor?.Log($"[Farmer] Stamina getter patch failed: {ex.Message}", StardewModdingAPI.LogLevel.Warn);
            }
        }

        private static void TryPatchStaminaSetter(Harmony harmony, ModConfig config)
        {
            if (!config.EnableEnthusiasm) return;
            try
            {
                var setter = AccessTools.PropertySetter(typeof(Farmer), nameof(Farmer.Stamina));
                var prefix = typeof(FarmerPatches).GetMethod(nameof(Stamina_Setter_Prefix));
                if (setter != null && prefix != null)
                    harmony.Patch(setter, new HarmonyMethod(prefix));
            }
            catch (Exception ex)
            {
                ModEntry.StaticMonitor?.Log($"[Farmer] Stamina setter patch failed: {ex.Message}", StardewModdingAPI.LogLevel.Warn);
            }
        }

        private static void TryPatchToolBegin(Harmony harmony, ModConfig config)
        {
            if (!config.EnableEnthusiasm) return;
            try
            {
                var beginTool = AccessTools.Method(typeof(Farmer), "performBeginUsingTool");
                if (beginTool != null)
                {
                    var prefix = typeof(FarmerPatches).GetMethod(nameof(BeginTool_Prefix));
                    harmony.Patch(beginTool, new HarmonyMethod(prefix));
                }
            }
            catch (Exception ex)
            {
                ModEntry.StaticMonitor?.Log($"[Farmer] BeginTool patch failed: {ex.Message}", StardewModdingAPI.LogLevel.Debug);
            }
        }

        private static void TryPatchToolAction(Harmony harmony, ModConfig config)
        {
            if (!config.EnableEnthusiasm) return;
            try
            {
                var toolAction = AccessTools.Method(typeof(Farmer), "performToolAction");
                if (toolAction != null)
                {
                    var postfix = typeof(FarmerPatches).GetMethod(nameof(ToolAction_Postfix));
                    harmony.Patch(toolAction, null, new HarmonyMethod(postfix));
                }
            }
            catch (Exception ex)
            {
                ModEntry.StaticMonitor?.Log($"[Farmer] ToolAction patch failed: {ex.Message}", StardewModdingAPI.LogLevel.Debug);
            }
        }

        private static void TryPatchExhaustion(Harmony harmony, ModConfig config)
        {
            if (!config.EnableEnthusiasm) return;
            try
            {
                var exhaustCheck = AccessTools.Method(typeof(Farmer), "checkForExhaustion");
                var prefix = typeof(FarmerPatches).GetMethod(nameof(CheckExhaustion_Prefix));
                if (exhaustCheck != null && prefix != null)
                    harmony.Patch(exhaustCheck, new HarmonyMethod(prefix));
            }
            catch (Exception ex)
            {
                ModEntry.StaticMonitor?.Log($"[Farmer] Exhaustion patch failed: {ex.Message}", StardewModdingAPI.LogLevel.Debug);
            }
        }

        // ═══════════════════════════════════════════════
        //  PATCH METHODS
        // ═══════════════════════════════════════════════

        /// <summary>Map enthusiasm to stamina so the vanilla bar shows enthusiasm level</summary>
        public static bool Stamina_Getter_Prefix(Farmer __instance, ref float __result)
        {
            if (!ModEntry.Config.EnableEnthusiasm) return true;
            var sys = ModEntry.Enthusiasm;
            if (sys == null) return true;
            __result = sys.CurrentValue * __instance.MaxStamina;
            return false;
        }

        public static bool Stamina_Setter_Prefix(Farmer __instance, ref float value)
        {
            if (!ModEntry.Config.EnableEnthusiasm) return true;

            var sys = ModEntry.Enthusiasm;
            if (sys == null) return true;

            var staminaField = AccessTools.Field(typeof(Farmer), "stamina");
            float currentStamina = staminaField != null ? (float)staminaField.GetValue(__instance) : __instance.MaxStamina;

            if (value >= currentStamina) return true;

            float loss = currentStamina - value;
            int actionCount = Math.Min((int)(loss / 2f), 10);
            for (int i = 0; i < Math.Max(1, actionCount); i++)
                sys.RecordAction("Physical");

            value = __instance.MaxStamina;

            if (sys.IsDrained)
            {
                __instance.doEmote(36);
                return false;
            }

            return true;
        }

        public static bool BeginTool_Prefix()
        {
            if (!ModEntry.Config.EnableEnthusiasm) return true;
            return EnthusiasmSystem.CanAct();
        }

        public static void ToolAction_Postfix()
        {
            if (!ModEntry.Config.EnableEnthusiasm) return;
            ModEntry.Enthusiasm?.RecordAction("Tool");
        }

        public static bool CheckExhaustion_Prefix()
        {
            return false; // Never run vanilla exhaustion -- enthusiasm handles it
        }

        public static bool GainExperience_Prefix(int which, ref int howMuch)
        {
            return false;
        }
    }
}
