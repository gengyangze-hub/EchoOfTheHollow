using HarmonyLib;

namespace EchoesOfTheHollow.Patches
{
    internal static class ShippingBinPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchShippingBin) return;
            // Shipping bin redirection is handled by DriftBoxSystem
            // The actual patch targets will be set up once SDV 1.6 API is confirmed
        }
    }
}
