using HarmonyLib;

namespace EchoesOfTheHollow.Patches
{
    internal static class FarmerPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchMoney) return;

            var moneyGetter = AccessTools.PropertyGetter(typeof(Farmer), nameof(Farmer.Money));
            var moneyGetterMethod = typeof(FarmerPatches).GetMethod(nameof(Money_Getter));
            if (moneyGetter != null && moneyGetterMethod != null)
                harmony.Patch(moneyGetter, new HarmonyMethod(moneyGetterMethod));
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

        public static bool Money_Getter(ref int __result)
        {
            __result = 0;
            return false;
        }

        public static bool GainExperience_Prefix(int which, ref int howMuch)
        {
            if (ModEntry.Instance != null)
            {
                string actionType = which switch
                {
                    0 => "farming",
                    1 => "fishing",
                    2 => "foraging",
                    3 => "mining",
                    4 => "combat",
                    _ => "other"
                };
                ModEntry.Triggers?.RecordAction(actionType);
            }
            return false; // No XP gain
        }
    }
}
