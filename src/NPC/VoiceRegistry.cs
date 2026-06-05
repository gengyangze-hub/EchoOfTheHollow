using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.Utils;
using Newtonsoft.Json;
using StardewModdingAPI;

namespace EchoesOfTheHollow.NpcProfiles
{
    /// <summary>
    /// NPC语音档案注册表 — 加载并提供所有NPC的写作风格配置
    /// Loads and serves all NPC voice profiles.
    /// </summary>
    public class VoiceRegistry
    {
        private readonly Dictionary<string, NpcVoiceProfile> _profiles = new();
        private readonly IMonitor _monitor;

        public int VoiceCount => _profiles.Count;

        public VoiceRegistry(IModHelper helper, IMonitor monitor)
        {
            _monitor = monitor;
            LoadBuiltinProfiles(helper);
            LoadCustomProfiles(helper);
        }

        /// <summary>Load built-in voice profiles</summary>
        private void LoadBuiltinProfiles(IModHelper helper)
        {
            string path = Path.Combine(helper.DirectoryPath, "assets", "data", "NpcVoices.json");
            if (!File.Exists(path))
            {
                _monitor.Log($"[Echoes] Built-in voice file not found: {path}", LogLevel.Warn);
                LoadFallbackProfiles();
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var wrapper = JsonConvert.DeserializeObject<NpcVoicesWrapper>(json);
                if (wrapper?.Voices != null)
                {
                    foreach (var voice in wrapper.Voices)
                    {
                        if (!string.IsNullOrEmpty(voice.NpcName))
                            _profiles[voice.NpcName] = voice;
                    }
                }
                _monitor.Log($"[Echoes] Loaded {_profiles.Count} NPC voice profiles.", LogLevel.Info);

                // Load extended voice data from templates directory
                LoadTemplateVoiceData(helper);
            }
            catch (Exception ex)
            {
                _monitor.Log($"[Echoes] Failed to load built-in voices: {ex.Message}", LogLevel.Error);
                LoadFallbackProfiles();
            }
        }

        /// <summary>
        /// 🔑 BACK DOOR: Load additional voice profiles from templates directory.
        /// Users can add new NPC voices or override existing ones without recompiling.
        /// </summary>
        private void LoadCustomProfiles(IModHelper helper)
        {
            if (!ModEntry.Config.LoadCustomTemplates) return;

            string customDir = Path.Combine(helper.DirectoryPath, ModEntry.Config.CustomTemplatesPath);
            if (!Directory.Exists(customDir)) return;

            try
            {
                var voiceFiles = Directory.GetFiles(customDir, "*voices*.json", SearchOption.AllDirectories);
                foreach (var file in voiceFiles)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var wrapper = JsonConvert.DeserializeObject<NpcVoicesWrapper>(json);
                        if (wrapper?.Voices != null)
                        {
                            foreach (var voice in wrapper.Voices)
                            {
                                if (!string.IsNullOrEmpty(voice.NpcName))
                                {
                                    _profiles[voice.NpcName] = voice; // Override or add
                                    _monitor.Log($"[Echoes] Custom voice loaded: {voice.NpcName} from {Path.GetFileName(file)}", LogLevel.Debug);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _monitor.Log($"[Echoes] Failed to load custom voices from {Path.GetFileName(file)}: {ex.Message}", LogLevel.Warn);
                    }
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"[Echoes] Error scanning custom voice directory: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>Load voice hints from template files</summary>
        private void LoadTemplateVoiceData(IModHelper helper)
        {
            string templatePath = Path.Combine(helper.DirectoryPath, "assets", "data", "MemoryTemplates.json");
            if (!File.Exists(templatePath)) return;

            try
            {
                string json = File.ReadAllText(templatePath);
                var wrapper = JsonConvert.DeserializeObject<TemplateWrapper>(json);
                if (wrapper?.Templates == null) return;

                foreach (var t in wrapper.Templates)
                {
                    if (!string.IsNullOrEmpty(t.NpcName) && _profiles.TryGetValue(t.NpcName, out var profile))
                    {
                        // Merge vocabulary hints from templates into voice profiles
                        if (t.VocabularyHints?.Count > 0)
                        {
                            foreach (var hint in t.VocabularyHints)
                                if (!profile.VocabularyMapping.ContainsKey(hint))
                                    profile.VocabularyMapping[hint] = hint;
                        }
                    }
                }
            }
            catch { /* Non-critical */ }
        }

        /// <summary>Fallback when JSON is missing or corrupt</summary>
        private void LoadFallbackProfiles()
        {
            // Create minimal voice profiles from built-in NPC constants
            string[] npcs = { "Abigail", "Alex", "Caroline", "Clint", "Demetrius", "Elliott",
                "Emily", "Evelyn", "George", "Gus", "Haley", "Harvey", "Jas", "Jodi",
                "Kent", "Leah", "Lewis", "Linus", "Marnie", "Maru", "Pam", "Penny",
                "Pierre", "Robin", "Sam", "Sebastian", "Shane", "Vincent", "Willy", "Wizard" };

            foreach (string name in npcs)
            {
                _profiles[name] = new NpcVoiceProfile
                {
                    NpcName = name,
                    WritingStyle = "Prose",
                    BaseTone = "Neutral",
                    MetaphorFrequency = 0.3f,
                    MetaphorDomain = "Nature",
                    SalutationPattern = "...",
                    ClosingPattern = "- " + name,
                    VocabularyMapping = new Dictionary<string, string>(),
                    MetaphorTemplates = new Dictionary<string, List<string>>(),
                    SignaturePhrases = new List<string>(),
                    ObservedQualities = new List<string> { "presence", "silence" }
                };
            }
            _monitor.Log($"[Echoes] Created {_profiles.Count} fallback voice profiles.", LogLevel.Warn);
        }

        /// <summary>Get a voice profile by NPC name</summary>
        public NpcVoiceProfile? GetProfile(string npcName)
        {
            _profiles.TryGetValue(npcName, out var profile);
            return profile;
        }

        /// <summary>Get all known NPC names</summary>
        public IEnumerable<string> GetAllNpcNames() => _profiles.Keys;

        /// <summary>
        /// Render a journal entry through this NPC's voice.
        /// Pipeline: template fill → word replacement → metaphor injection → style rules → framing
        /// </summary>
        public string RenderEntry(string rawText, NpcVoiceProfile voice, Dictionary<string, string> parameters)
        {
            string text = rawText;

            // Step 1: Parameter substitution (already done by template engine, but re-apply for safety)
            text = StringHelper.ReplaceParameters(text, parameters);

            // Step 2: Vocabulary replacement
            text = StringHelper.ReplaceWords(text, voice.VocabularyMapping);

            // Step 3: Metaphor injection (probabilistic)
            if (voice.MetaphorFrequency > 0 && voice.MetaphorTemplates.Count > 0)
            {
                foreach (var concept in voice.MetaphorTemplates.Keys)
                {
                    if (text.Contains(concept, StringComparison.OrdinalIgnoreCase) &&
                        RandomHelper.Chance(voice.MetaphorFrequency))
                    {
                        var metaphors = voice.MetaphorTemplates[concept];
                        if (metaphors.Count > 0)
                        {
                            string metaphor = metaphors[RandomHelper.Next(metaphors.Count)];
                            if (!text.Contains(metaphor))
                                text += " " + metaphor;
                        }
                    }
                }
            }

            // Step 4: Stylistic rules
            foreach (var rule in voice.StylisticRules)
            {
                if (RandomHelper.Chance(rule.ApplyChance))
                {
                    try
                    {
                        text = System.Text.RegularExpressions.Regex.Replace(
                            text, rule.MatchPattern, rule.Replacement);
                    }
                    catch { /* Skip bad patterns */ }
                }
            }

            // Step 5: Signature phrase (30% chance)
            if (voice.SignaturePhrases.Count > 0 && RandomHelper.Chance(0.3))
            {
                string phrase = voice.SignaturePhrases[RandomHelper.Next(voice.SignaturePhrases.Count)];
                text += " " + phrase;
            }

            // Step 6: Framing
            text = $"{voice.SalutationPattern}\n\n{text}\n\n{voice.ClosingPattern.Replace("{npcName}", voice.NpcName)}";

            return text;
        }

        // ── JSON wrapper classes ──

        private class NpcVoicesWrapper
        {
            public List<NpcVoiceProfile> Voices { get; set; } = new();
        }

        private class TemplateWrapper
        {
            public List<MemoryTemplate> Templates { get; set; } = new();
        }
    }
}
