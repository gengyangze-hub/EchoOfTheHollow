using System.Collections.Generic;

namespace EchoesOfTheHollow.Data
{
    /// <summary>
    /// 记忆模板 — 可扩展的记忆生成蓝图
    /// Memory template — the blueprint for generating journal entries.
    ///
    /// 🔑 BACK DOOR: Templates can be added/overridden via JSON files in assets/data/templates/
    /// without recompiling the mod. The MemoryTemplateEngine scans all .json files in that
    /// directory and merges them with built-in templates.
    /// </summary>
    public class MemoryTemplate
    {
        public string TemplateId { get; set; } = string.Empty;
        public string NpcName { get; set; } = string.Empty;    // NPC name or "Any"
        public TriggerType TriggerType { get; set; }
        public int Priority { get; set; } = 5;
        public int CooldownDays { get; set; } = 3;

        public TemplateConditions Conditions { get; set; } = new();
        public List<string> TextVariants { get; set; } = new();
        public List<string> VocabularyHints { get; set; } = new();
        public string EmotionTag { get; set; } = "Neutral";

        // 🔑 Extensibility: extends another template, only overriding specified fields
        public string? Extends { get; set; }

        // 🔑 Source tracking: which file this template came from
        public string SourceFile { get; set; } = "builtin";
        public bool IsCustom { get; set; } = false;
    }

    /// <summary>Conditions that must be met for this template to fire</summary>
    public class TemplateConditions
    {
        public List<string>? ObservedActions { get; set; }
        public List<string>? TimeOfDay { get; set; }
        public List<string>? Seasons { get; set; }
        public List<string>? Weather { get; set; }
        public List<string>? Locations { get; set; }
        public int? MinDaysSinceLastTrigger { get; set; }
        public int? MinOfflineHours { get; set; }
        public int? MinEntriesWithPlayer { get; set; }
    }
}
