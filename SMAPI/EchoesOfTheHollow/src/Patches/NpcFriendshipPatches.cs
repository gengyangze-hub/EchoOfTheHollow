using HarmonyLib;
using StardewValley;

namespace EchoesOfTheHollow.Patches
{
    /// <summary>
    /// Remove friendship hearts. Instead, NPC interactions trigger journal entries.
    /// Friendship data remains in the save — we only intercept at runtime.
    /// </summary>
    internal static class NpcFriendshipPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchFriendship) return;

            // Intercept NPC gift receiving
            var tryToReceiveActiveObject = AccessTools.Method(
                typeof(StardewValley.NPC), nameof(StardewValley.NPC.tryToReceiveActiveObject));
            var prefixReceive = typeof(NpcFriendshipPatches).GetMethod(nameof(TryReceiveActiveObject_Prefix));
            if (tryToReceiveActiveObject != null && prefixReceive != null)
                harmony.Patch(tryToReceiveActiveObject, new HarmonyMethod(prefixReceive));

            // Intercept friendship heart level display
            var getHeartLevel = AccessTools.Method(
                typeof(StardewValley.NPC), "getFriendshipHeartLevelForFarmer");
            var heartLevelPrefix = typeof(NpcFriendshipPatches).GetMethod(nameof(GetHeartLevel_Prefix));
            if (getHeartLevel != null && heartLevelPrefix != null)
                harmony.Patch(getHeartLevel, new HarmonyMethod(heartLevelPrefix));
        }

        public static bool TryReceiveActiveObject_Prefix(StardewValley.NPC __instance, Farmer who)
        {
            if (who == Game1.player)
            {
                ModEntry.Triggers?.OnNpcInteraction(__instance);
            }
            return true;
        }

        public static void GetHeartLevel_Prefix(ref int __result)
        {
            __result = 10; // Always show full hearts so events/conditions still pass
        }
    }
}
