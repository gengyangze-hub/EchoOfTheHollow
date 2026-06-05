using HarmonyLib;

namespace EchoesOfTheHollow.Patches
{
    internal static class ShopPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchShops) return;
            // Shop price modification will be set up once SDV 1.6 API is confirmed
        }
    }
}
