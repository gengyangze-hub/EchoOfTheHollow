using HarmonyLib;
using StardewValley;
using StardewValley.Locations;

namespace EchoesOfTheHollow.Patches
{
    /// <summary>
    /// Redirects the shipping bin to the DriftBoxSystem.
    /// Items placed in the shipping bin drift to random town locations for NPCs to find,
    /// instead of being sold overnight for money.
    /// </summary>
    internal static class ShippingBinPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchShippingBin) return;

            // Patch Farm.shipItem to intercept shipping
            var shipItemMethod = AccessTools.Method(typeof(Farm), "shipItem");
            var prefix = typeof(ShippingBinPatches).GetMethod(nameof(ShipItem_Prefix));
            if (shipItemMethod != null && prefix != null)
                harmony.Patch(shipItemMethod, new HarmonyMethod(prefix));

            // NOTE: We intentionally do NOT block shipAll.
            // ShipItem_Prefix diverts items to DriftBox, so the shipping bin is always empty.
            // shipAll runs on an empty queue as a no-op, but stats tracking still functions.
            // This ensures shipping statistics (stats.ItemsShipped, stats.BasicShipped)
            // remain accurate if any items bypass our patch.
        }

        /// <summary>Intercept individual item shipping</summary>
        public static bool ShipItem_Prefix(Farm __instance, Item i)
        {
            if (ModEntry.DriftBox != null && i != null)
            {
                ModEntry.DriftBox.DepositItem(i);
                Game1.addHUDMessage(new HUDMessage("物品漂向了镇上的某个角落...有人可能会发现它。", HUDMessage.newQuest_type));
            }
            return false; // Skip normal shipping
        }

    }
}
