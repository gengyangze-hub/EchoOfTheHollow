using HarmonyLib;
using StardewValley;

namespace EchoesOfTheHollow.Patches
{
    /// <summary>
    /// Remove friendship hearts. Instead, NPC interactions trigger journal entries.
    /// Friendship data remains in the save -- we only intercept at runtime.
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

            // Friendship heart levels are NOT patched.
            // The save file preserves actual friendship data (we never modify it).
            // getFriendshipHeartLevelForFarmer returns the real value, which is needed for:
            // - Heart events to fire at the correct friendship thresholds
            // - NPC dialogue to use appropriate heart-level variants
            // The social/hearts TAB is already redirected to MemoryBook via SocialMenuPatches.
        }

        public static bool TryReceiveActiveObject_Prefix(StardewValley.NPC __instance, Farmer who)
        {
            if (who == Game1.player)
            {
                ModEntry.Triggers?.OnNpcInteraction(__instance);
            }
            return true;
        }

    }
}
