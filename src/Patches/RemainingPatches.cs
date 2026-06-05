using HarmonyLib;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Quests;

namespace EchoesOfTheHollow.Patches
{
    /// <summary>
    /// Quest Board → Invitation System redirect.
    /// Blocks vanilla daily quests so the InvitationSystem takes over.
    /// </summary>
    internal static class QuestBoardPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchQuestBoard) return;

            // Clear the daily quest so the billboard shows nothing
            var questOfTheDayGetter = AccessTools.PropertyGetter(
                typeof(Game1), nameof(Game1.questOfTheDay));
            var prefix = typeof(QuestBoardPatches).GetMethod(nameof(QuestOfTheDay_Prefix));
            if (questOfTheDayGetter != null && prefix != null)
                harmony.Patch(questOfTheDayGetter, new HarmonyMethod(prefix));

            // Block quest acceptance from the billboard
            var acceptQuest = AccessTools.Method(typeof(Billboard), "acceptQuest");
            var accPrefix = typeof(QuestBoardPatches).GetMethod(nameof(AcceptQuest_Prefix));
            if (acceptQuest != null && accPrefix != null)
                harmony.Patch(acceptQuest, new HarmonyMethod(accPrefix));
        }

        /// <summary>Return null for daily quest — invitations replace quests</summary>
        public static bool QuestOfTheDay_Prefix(ref Quest __result)
        {
            __result = null!;
            return false;
        }

        /// <summary>Block quest acceptance from billboard</summary>
        public static bool AcceptQuest_Prefix()
        {
            Game1.addHUDMessage(new HUDMessage("任务板已被留言柱取代。去看看今天有谁留下了想一起做的事。", HUDMessage.newQuest_type));
            return false;
        }
    }

    /// <summary>
    /// Social Menu → Memory Book redirect.
    /// When the player opens the social tab, show the Echo journal instead.
    /// </summary>
    internal static class SocialMenuPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchSocialMenu) return;

            // Patch GameMenu constructor to replace the social page
            var gameMenuCtor = AccessTools.Constructor(typeof(GameMenu), new[] { typeof(int) });
            var prefix = typeof(SocialMenuPatches).GetMethod(nameof(GameMenu_Prefix));
            if (gameMenuCtor != null && prefix != null)
                harmony.Patch(gameMenuCtor, new HarmonyMethod(prefix));

            // Also patch changeTab to intercept social tab switching
            var changeTab = AccessTools.Method(typeof(GameMenu), "changeTab");
            var ctPrefix = typeof(SocialMenuPatches).GetMethod(nameof(ChangeTab_Prefix));
            if (changeTab != null && ctPrefix != null)
                harmony.Patch(changeTab, new HarmonyMethod(ctPrefix));
        }

        /// <summary>If opening to social tab, redirect to memory book</summary>
        public static bool GameMenu_Prefix(GameMenu __instance, int startingTab)
        {
            // SocialPage tab index is usually 2 (inventory, skills, social, map, crafting, collections, options)
            // But in SDV 1.6 with achievements, it may vary
            if (startingTab == 2) // Social tab index
            {
                ModEntry.OpenMemoryBook();
                return false; // Don't create the GameMenu
            }
            return true;
        }

        /// <summary>If switching to social tab, redirect to memory book and block original</summary>
        public static bool ChangeTab_Prefix(GameMenu __instance, int whichTab)
        {
            if (whichTab == 2) // Social tab
            {
                ModEntry.OpenMemoryBook();
                return false; // Block original tab switch
            }
            return true;
        }
    }

    /// <summary>
    /// Collections tab blocker — prevent access to collections/achievements UI.
    /// </summary>
    internal static class CollectionsPatches
    {
        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchCollections) return;

            // Patch GameMenu to intercept collections tab
            var gameMenuCtor = AccessTools.Constructor(typeof(GameMenu), new[] { typeof(int) });
            var postfix = typeof(CollectionsPatches).GetMethod(nameof(GameMenu_CollectionsCheck));
            if (gameMenuCtor != null && postfix != null)
                harmony.Patch(gameMenuCtor, null, new HarmonyMethod(postfix));
        }

        /// <summary>If the starting tab is collections, open journal instead</summary>
        public static void GameMenu_CollectionsCheck(GameMenu __instance, int startingTab)
        {
            // Collections tab index is typically 5 in SDV 1.6
            if (startingTab >= 4) // Map=3, Crafting=4, Collections=5, etc.
            {
                // Allow map (3) but block collections (5)
                // Re-check actual tabs
                try
                {
                    var pages = AccessTools.Field(typeof(GameMenu), "pages");
                    if (pages != null)
                    {
                        var pageList = pages.GetValue(__instance) as System.Collections.Generic.List<IClickableMenu>;
                        if (pageList != null && pageList.Count > startingTab)
                        {
                            var page = pageList[startingTab];
                            if (page != null && page.GetType().Name.Contains("Collections"))
                            {
                                ModEntry.OpenJournalMenu();
                            }
                        }
                    }
                }
                catch { /* Allow normal behavior if check fails */ }
            }
        }
    }

    /// <summary>
    /// Event precondition bypass — ensures cutscenes aren't locked behind
    /// money or friendship heart requirements.
    /// In SDV 1.6, events use <c>Event.preconditionCheck</c> or similar.
    /// </summary>
    internal static class EventPatches
    {
        // Regex to selectively remove friendship and money preconditions
        // SDV 1.6 precondition format: "f NPC he##arts/m #####/z season/w weather/d day/t time/y year"
        // We ONLY remove /f (friendship) and /m (money) tokens; preserve all others
        private static readonly Regex FriendshipMoneyPrecondition = new(
            @"/[fm][^/]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchEvents) return;

            // Patch Event.tryEventPrecondition (or preconditionsCheck in some versions)
            var checkMethod = AccessTools.Method(typeof(StardewValley.Event),
                "tryEventPrecondition");
            if (checkMethod == null)
                checkMethod = AccessTools.Method(typeof(StardewValley.Event),
                    "preconditionCheck");

            // Use a prefix to strip money/friendship from the precondition string
            if (checkMethod != null)
            {
                var prefix = typeof(EventPatches).GetMethod(nameof(EventPrecondition_Prefix));
                harmony.Patch(checkMethod, new HarmonyMethod(prefix));
            }
        }

        /// <summary>
        /// Strip only money (/m) and friendship (/f) precondition tokens from the event.
        /// Uses reflection to find the preconditions field (name varies across SDV versions).
        /// </summary>
        public static void EventPrecondition_Prefix(StardewValley.Event __instance)
        {
            try
            {
                // The field name varies: "eventConditions" in SDV 1.6, "preconditions" elsewhere
                var field = AccessTools.Field(typeof(StardewValley.Event), "eventConditions")
                    ?? AccessTools.Field(typeof(StardewValley.Event), "preconditions");

                if (field == null) return;
                string? conditions = field.GetValue(__instance) as string;
                if (string.IsNullOrEmpty(conditions)) return;

                // Remove /f and /m tokens, clean up double slashes
                conditions = FriendshipMoneyPrecondition
                    .Replace(conditions, "")
                    .Replace("//", "/")
                    .Trim('/');

                field.SetValue(__instance, conditions);
            }
            catch { /* If reflection fails, fall through — original precondition logic runs */ }
        }
    }

    /// <summary>
    /// Dialogue token stripper — removes $g, $h, $b, $q, and other formatting
    /// tokens from NPC dialogue text, since friendship values are meaningless
    /// in Echoes of the Hollow.
    /// </summary>
    internal static class DialoguePatches
    {
        // Only strip $g (gender) and $h (heart-level) tokens — NEVER strip
        // $q (quest), $r (response), $b (branch), $p (prerequisite), $k/$s/$d (relationship)
        private static readonly Regex GenderHeartTokenRegex = new(
            @"\$[gh](?:\s*[^#$]*?#)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static void Apply(Harmony harmony, ModConfig config)
        {
            if (!config.PatchDialogue) return;

            // Patch Dialogue constructor to strip tokens from dialogue text
            var dialogueCtor = AccessTools.Constructor(typeof(Dialogue), new[]
                { typeof(string), typeof(NPC) });
            if (dialogueCtor != null)
            {
                var postfix = typeof(DialoguePatches).GetMethod(nameof(Dialogue_Constructor_Postfix));
                harmony.Patch(dialogueCtor, null, new HarmonyMethod(postfix));
            }

            // Patch NPC.showTextAboveHead to strip tokens
            var aboveHeadMethod = AccessTools.Method(typeof(NPC), "showTextAboveHead",
                new[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(int) });
            if (aboveHeadMethod == null)
                aboveHeadMethod = AccessTools.Method(typeof(NPC), "showTextAboveHead");

            if (aboveHeadMethod != null)
            {
                var prefix = typeof(DialoguePatches).GetMethod(nameof(ShowTextAboveHead_Prefix));
                harmony.Patch(aboveHeadMethod, new HarmonyMethod(prefix));
            }
        }

        /// <summary>Strip $g/$h tokens from dialogue text after construction</summary>
        public static void Dialogue_Constructor_Postfix(Dialogue __instance, string masterDialogue, NPC speaker)
        {
            try
            {
                var dialoguesField = AccessTools.Field(typeof(Dialogue), "dialogues");
                if (dialoguesField == null) return;

                var dialogues = dialoguesField.GetValue(__instance) as System.Collections.Generic.List<string>;
                if (dialogues == null) return;

                for (int i = 0; i < dialogues.Count; i++)
                    dialogues[i] = CleanDialogue(dialogues[i]);
            }
            catch { /* Non-critical */ }
        }

        /// <summary>Strip tokens from NPC text bubbles</summary>
        public static void ShowTextAboveHead_Prefix(ref string message)
        {
            if (!string.IsNullOrEmpty(message))
                message = CleanDialogue(message);
        }

        /// <summary>Remove ONLY $g (gender) and $h (heart-level) tokens from dialogue.
        /// Preserves $q (quest), $r (response), $b (branch), $p (prerequisite),
        /// $k (kiss), $s (spouse), $d (divorce), $1-$9 (param tokens) for game logic.</summary>
        public static string CleanDialogue(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = GenderHeartTokenRegex.Replace(text, "");
            text = Regex.Replace(text, @"\s{2,}", " ").Trim();
            return text;
        }
    }
}
