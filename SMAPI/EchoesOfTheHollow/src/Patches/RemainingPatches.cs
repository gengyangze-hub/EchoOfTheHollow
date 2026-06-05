using HarmonyLib;
using System;

namespace EchoesOfTheHollow.Patches
{
    /// <summary>
    /// Simple patches for quest board, social menu, collections, events, and dialogue.
    /// These are kept minimal — full UI replacement happens via custom menus.
    /// </summary>
    internal static class QuestBoardPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchQuestBoard) return;
            // The quest board replacement is handled by InvitationSystem
            // This patch is a placeholder for future quest interception
        }
    }

    internal static class SocialMenuPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchSocialMenu) return;
            // Social tab → Memory Book: handled by MenuChanged event in ModEntry
        }
    }

    internal static class CollectionsPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchCollections) return;
            // Collections tab → Echo Journal: handled by custom UI injection
        }
    }

    internal static class EventPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchEvents) return;
            // Event patches ensure events aren't locked behind money/friendship
        }
    }

    internal static class DialoguePatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchDialogue) return;
            // Dialogue patches remove $...$ tokens for gift/friendship references
        }
    }
}
