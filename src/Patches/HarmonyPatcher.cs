using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;

namespace EchoesOfTheHollow.Patches
{
    /// <summary>
    /// Central Harmony patch coordinator.
    /// Applies all patches in the correct order and handles failures gracefully.
    /// </summary>
    internal static class HarmonyPatcher
    {
        private static Harmony? _harmony;
        private static IMonitor? _monitor;

        public static void Apply(IModHelper helper, ModConfig config, IMonitor monitor)
        {
            _monitor = monitor;
            string harmonyId = helper.ModRegistry.ModID;
            _harmony = new Harmony(harmonyId);

            var patchGroups = new Dictionary<string, Action>
            {
                ["Money"] = () => FarmerPatches.Apply(_harmony, config),
                ["Skills"] = () => FarmerPatches.ApplySkills(_harmony, config),
                ["Friendship"] = () => NpcFriendshipPatches.Apply(_harmony, config),
                ["Achievements"] = () => AchievementPatches.Apply(_harmony, config),
                ["ShippingBin"] = () => ShippingBinPatches.Apply(_harmony, config),
                ["HUD"] = () => HudPatches.Apply(_harmony, config),
                ["Shops"] = () => ShopPatches.Apply(_harmony, config),
                ["QuestBoard"] = () => QuestBoardPatches.Apply(_harmony, config),
                ["SocialMenu"] = () => SocialMenuPatches.Apply(_harmony, config),
                ["Collections"] = () => CollectionsPatches.Apply(_harmony, config),
                ["Events"] = () => EventPatches.Apply(_harmony, config),
                ["Dialogue"] = () => DialoguePatches.Apply(_harmony, config),
            };

            int success = 0, fail = 0;
            foreach (var kv in patchGroups)
            {
                try
                {
                    kv.Value();
                    success++;
                    monitor.Log($"[Harmony] ✅ {kv.Key} patches applied.", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    fail++;
                    monitor.Log($"[Harmony] ❌ {kv.Key} patches FAILED: {ex.Message}", LogLevel.Error);
                }
            }

            monitor.Log($"[Harmony] Applied {success}/{patchGroups.Count} patch groups ({fail} failed).", LogLevel.Info);
        }
    }
}
