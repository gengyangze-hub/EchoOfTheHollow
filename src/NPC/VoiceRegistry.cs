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
    /// NPC语音档案注册表 -- 加载并提供所有NPC的写作风格配置
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

            // Gender mapping for built-in NPCs
            var genders = new Dictionary<string, string> {
                ["Abigail"]="Female",["Alex"]="Male",["Caroline"]="Female",["Clint"]="Male",
                ["Demetrius"]="Male",["Elliott"]="Male",["Emily"]="Female",["Evelyn"]="Female",
                ["George"]="Male",["Gus"]="Male",["Haley"]="Female",["Harvey"]="Male",
                ["Jas"]="Female",["Jodi"]="Female",["Kent"]="Male",["Leah"]="Female",
                ["Lewis"]="Male",["Linus"]="Male",["Marnie"]="Female",["Maru"]="Female",
                ["Pam"]="Female",["Penny"]="Female",["Pierre"]="Male",["Robin"]="Female",
                ["Sam"]="Male",["Sebastian"]="Male",["Shane"]="Male",["Vincent"]="Male",
                ["Willy"]="Male",["Wizard"]="Male"
            };

            foreach (string name in npcs)
            {
                _profiles[name] = new NpcVoiceProfile
                {
                    NpcName = name,
                    Gender = genders.ContainsKey(name) ? genders[name] : "Other",
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
        public string RenderEntry(string rawText, NpcVoiceProfile voice, Dictionary<string, string> parameters, float clarity = 0.5f)
        {
            string text = rawText;

            text = StringHelper.ReplaceParameters(text, parameters);
            text = ApplyGenderVariation(text, voice);

            // Vocabulary: clarity scales chance (30% at 0 → 90% at max)
            float vocabChance = 0.3f + clarity * 0.6f;
            if (voice.VocabularyMapping.Count > 0 && RandomHelper.Chance(vocabChance))
                text = StringHelper.ReplaceWords(text, voice.VocabularyMapping);

            // Metaphor: clarity scales frequency
            float effectiveMetaFreq = voice.MetaphorFrequency * (0.2f + clarity * 0.8f);
            if (effectiveMetaFreq > 0 && voice.MetaphorTemplates.Count > 0)
            {
                foreach (var concept in voice.MetaphorTemplates.Keys)
                {
                    if (text.Contains(concept, StringComparison.OrdinalIgnoreCase) &&
                        RandomHelper.Chance(effectiveMetaFreq))
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

            // Stylistic rules: clarity scales apply chance
            foreach (var rule in voice.StylisticRules)
            {
                float effectiveChance = rule.ApplyChance * (0.2f + clarity * 0.8f);
                if (RandomHelper.Chance(effectiveChance))
                {
                    try { text = System.Text.RegularExpressions.Regex.Replace(text, rule.MatchPattern, rule.Replacement); }
                    catch { }
                }
            }

            // Signature phrase: 10% at 0 clarity → 50% at max
            float sigChance = 0.1f + clarity * 0.4f;
            if (voice.SignaturePhrases.Count > 0 && RandomHelper.Chance(sigChance))
                text += " " + voice.SignaturePhrases[RandomHelper.Next(voice.SignaturePhrases.Count)];

            // Framing
            text = $"{voice.SalutationPattern}\n\n{text}\n\n{voice.ClosingPattern.Replace("{npcName}", voice.NpcName)}";

            // Clean Unicode
            text = StringHelper.CleanUnicode(text);
            return text;
        }

        /// <summary>Inject gender-appropriate pronouns and phrasing into rendered text.</summary>
        private static string ApplyGenderVariation(string text, NpcVoiceProfile voice)
        {
            var gendered = voice.Gender switch
            {
                "Male" => new Dictionary<string, string>
                {
                    ["他"] = "他", ["她"] = "他", ["它"] = "它",
                    ["那个男孩"] = "他", ["那个女孩"] = "他", ["那人"] = "他",
                    ["外乡人"] = "外乡人", ["旅人"] = "旅人",
                },
                "Female" => new Dictionary<string, string>
                {
                    ["他"] = "她", ["她"] = "她",
                    ["那个男孩"] = "她", ["那个女孩"] = "她", ["那人"] = "她",
                },
                _ => new Dictionary<string, string>()
            };

            // Apply gendered word substitutions within the text
            foreach (var kv in gendered)
            {
                if (kv.Key != kv.Value)
                    text = System.Text.RegularExpressions.Regex.Replace(
                        text, kv.Key, kv.Value);
            }

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
