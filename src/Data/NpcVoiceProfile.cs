using System.Collections.Generic;

namespace EchoesOfTheHollow.Data
{
    /// <summary>
    /// NPC语音档案 -- 定义每个NPC独特的写作风格
    /// Defines each NPC's unique voice: vocabulary, metaphors, stylistic rules.
    /// </summary>
    public class NpcVoiceProfile
    {
        public string NpcName { get; set; } = string.Empty;
        public string Gender { get; set; } = "Other";
        public string WritingStyle { get; set; } = "Prose";
        public string BaseTone { get; set; } = "Neutral";
        public float MetaphorFrequency { get; set; } = 0.3f;
        public string MetaphorDomain { get; set; } = "Nature";

        public string SalutationPattern { get; set; } = "Dear diary,";
        public string ClosingPattern { get; set; } = "- {npcName}";

        /// <summary>Word → NPC's preferred vocabulary</summary>
        public Dictionary<string, string> VocabularyMapping { get; set; } = new();

        /// <summary>Concept → list of metaphor templates</summary>
        public Dictionary<string, List<string>> MetaphorTemplates { get; set; } = new();

        /// <summary>Randomly inserted signature phrases</summary>
        public List<string> SignaturePhrases { get; set; } = new();

        /// <summary>What this NPC particularly notices about others</summary>
        public List<string> ObservedQualities { get; set; } = new();

        /// <summary>Stylistic transformation rules</summary>
        public List<StylisticRule> StylisticRules { get; set; } = new();

        /// <summary>Value core -- what this NPC cares about most (for exchange matching)</summary>
        public string ValueCore { get; set; } = string.Empty;

        /// <summary>Personality description for exchange preference generation</summary>
        public string PersonalityDescription { get; set; } = string.Empty;
    }

    public class StylisticRule
    {
        public string MatchPattern { get; set; } = string.Empty;
        public string Replacement { get; set; } = string.Empty;
        public float ApplyChance { get; set; } = 1.0f;
    }
}
