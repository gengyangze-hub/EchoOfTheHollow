using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.NpcProfiles;
using EchoesOfTheHollow.Utils;
using Newtonsoft.Json;
using StardewModdingAPI;

namespace EchoesOfTheHollow.Systems.Journal
{
    /// <summary>
    /// 记忆模板引擎 -- 选择模板、填充参数、渲染文本
    /// Memory Template Engine -- selects templates, fills parameters, renders text.
    ///
    /// 🔑 MEMORY BACK DOOR:
    /// This engine scans ALL .json files in assets/data/templates/ at startup.
    /// Users can add new templates by dropping .json files -- no recompilation needed.
    /// Format: identical to MemoryTemplates.json structure.
    /// Custom files OVERRIDE built-in templates with the same templateId.
    /// Files named "*templates*.json" in any subdirectory are auto-loaded.
    /// </summary>
    public class MemoryTemplateEngine
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly VoiceRegistry _voiceRegistry;

        // All templates indexed by (npcName, triggerType)
        private readonly Dictionary<string, List<MemoryTemplate>> _templateIndex = new();
        // Track recently used templates per NPC for cooldown
        private readonly Dictionary<string, Dictionary<string, int>> _recentUsage = new();
        // All templates flat list
        private readonly List<MemoryTemplate> _allTemplates = new();

        public int TemplateCount => _allTemplates.Count;

        public MemoryTemplateEngine(IModHelper helper, IMonitor monitor, VoiceRegistry voiceRegistry)
        {
            _helper = helper;
            _monitor = monitor;
            _voiceRegistry = voiceRegistry;

            LoadBuiltinTemplates();
            // 🔑 BACK DOOR: Load custom templates that override/extend built-in ones
            LoadCustomTemplates();
            BuildIndex();
        }

        /// <summary>Load built-in templates from MemoryTemplates.json</summary>
        private void LoadBuiltinTemplates()
        {
            string path = Path.Combine(_helper.DirectoryPath, "assets", "data", "MemoryTemplates.json");
            if (!File.Exists(path))
            {
                _monitor.Log("[Echoes] Built-in template file not found. Using fallback templates.", LogLevel.Warn);
                CreateFallbackTemplates();
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var wrapper = JsonConvert.DeserializeObject<TemplateFileWrapper>(json);
                if (wrapper?.Templates != null)
                {
                    foreach (var t in wrapper.Templates)
                    {
                        t.SourceFile = "builtin";
                        _allTemplates.Add(t);
                    }
                    _monitor.Log($"[Echoes] Loaded {_allTemplates.Count} built-in templates.", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"[Echoes] Failed to load built-in templates: {ex.Message}", LogLevel.Error);
                CreateFallbackTemplates();
            }
        }

        /// <summary>
        /// 🔑 BACK DOOR: Scan templates directory for custom JSON files.
        /// Users can create any .json file with template data and it auto-loads.
        /// </summary>
        private void LoadCustomTemplates()
        {
            if (!ModEntry.Config.LoadCustomTemplates) return;

            string customDir = Path.Combine(_helper.DirectoryPath, ModEntry.Config.CustomTemplatesPath);
            if (!Directory.Exists(customDir))
            {
                // Create the directory so users know where to put files
                try { Directory.CreateDirectory(customDir); } catch { }
                return;
            }

            try
            {
                var jsonFiles = Directory.GetFiles(customDir, "*.json", SearchOption.AllDirectories);
                int customCount = 0;

                foreach (var file in jsonFiles)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var wrapper = JsonConvert.DeserializeObject<TemplateFileWrapper>(json);
                        if (wrapper?.Templates == null) continue;

                        foreach (var t in wrapper.Templates)
                        {
                            t.SourceFile = Path.GetFileName(file);
                            t.IsCustom = true;

                            // 🔑 OVERRIDE: Remove existing template with same ID
                            var existing = _allTemplates.FirstOrDefault(x => x.TemplateId == t.TemplateId);
                            if (existing != null)
                            {
                                _allTemplates.Remove(existing);
                                _monitor.Log($"[Echoes] Custom template OVERRIDES: {t.TemplateId} from {t.SourceFile}", LogLevel.Debug);
                            }

                            // 🔑 EXTENDS: Inherit from parent template
                            if (!string.IsNullOrEmpty(t.Extends))
                            {
                                var parent = _allTemplates.FirstOrDefault(x => x.TemplateId == t.Extends);
                                if (parent != null) MergeTemplate(t, parent);
                            }

                            _allTemplates.Add(t);
                            customCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _monitor.Log($"[Echoes] Failed to load custom templates from {Path.GetFileName(file)}: {ex.Message}", LogLevel.Warn);
                    }
                }

                if (customCount > 0)
                    _monitor.Log($"[Echoes] 🔑 Loaded {customCount} custom templates from {customDir}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"[Echoes] Error scanning custom templates: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>Merge a child template with its parent (child wins on conflict)</summary>
        private void MergeTemplate(MemoryTemplate child, MemoryTemplate parent)
        {
            if (child.Conditions.ObservedActions == null)
                child.Conditions.ObservedActions = parent.Conditions.ObservedActions?.ToList();
            if (child.Conditions.TimeOfDay == null)
                child.Conditions.TimeOfDay = parent.Conditions.TimeOfDay?.ToList();
            if (child.Conditions.Seasons == null)
                child.Conditions.Seasons = parent.Conditions.Seasons?.ToList();
            if (child.Conditions.Weather == null)
                child.Conditions.Weather = parent.Conditions.Weather?.ToList();
            if (child.Conditions.Locations == null)
                child.Conditions.Locations = parent.Conditions.Locations?.ToList();

            if (child.TextVariants.Count == 0)
                child.TextVariants = parent.TextVariants.ToList();
            if (child.VocabularyHints.Count == 0)
                child.VocabularyHints = parent.VocabularyHints.ToList();
            if (child.EmotionTag == "Neutral" && parent.EmotionTag != "Neutral")
                child.EmotionTag = parent.EmotionTag;
            if (child.Priority == 5) child.Priority = parent.Priority;
            if (child.CooldownDays == 3) child.CooldownDays = parent.CooldownDays;
        }

        /// <summary>Create fallback templates when JSON is missing</summary>
        private void CreateFallbackTemplates()
        {
            string[] npcs = { "Abigail", "Alex", "Caroline", "Clint", "Demetrius", "Elliott",
                "Emily", "Gus", "Haley", "Harvey", "Jas", "Leah", "Lewis", "Linus",
                "Marnie", "Maru", "Pam", "Penny", "Pierre", "Robin", "Sam", "Sebastian",
                "Shane", "Vincent", "Willy", "Wizard" };

            foreach (string npc in npcs)
            {
                _allTemplates.Add(new MemoryTemplate
                {
                    TemplateId = $"fallback_{npc}_presence",
                    NpcName = npc,
                    TriggerType = TriggerType.Environmental,
                    Priority = 3,
                    CooldownDays = 2,
                    TextVariants = new List<string> {
                        $"今天在镇上看到了{{playerName}}。他们静静地走过，像一阵熟悉的微风。",
                        $"{{playerName}}在{{location}}待了一会儿。我没上前打扰。"
                    },
                    EmotionTag = "Neutral",
                    SourceFile = "fallback"
                });

                _allTemplates.Add(new MemoryTemplate
                {
                    TemplateId = $"fallback_{npc}_interaction",
                    NpcName = npc,
                    TriggerType = TriggerType.DirectInteraction,
                    Priority = 5,
                    CooldownDays = 2,
                    TextVariants = new List<string> {
                        $"今天{{playerName}}来找我了。我们简单地聊了几句。这种感觉很好----不需要说什么特别的话，只是待在一起就够了。",
                        $"和{{playerName}}说了一会儿话。有时候，哪怕只是短短几句话，也能让一天变得不一样。"
                    },
                    EmotionTag = "Warm",
                    SourceFile = "fallback"
                });
            }

            // Add absence templates
            _allTemplates.Add(new MemoryTemplate
            {
                TemplateId = "fallback_any_absence",
                NpcName = "Any",
                TriggerType = TriggerType.OfflinePassage,
                Priority = 4,
                CooldownDays = 1,
                Conditions = new TemplateConditions { MinOfflineHours = 12 },
                TextVariants = new List<string> {
                    "{{playerName}}已经有段时间没见了。希望他们一切都好。",
                    "那个外乡人消失了一阵。山谷好像少了点什么。",
                    "{{daysAway}}天没见过{{playerName}}了。风告诉我他们还会回来。"
                },
                EmotionTag = "Longing",
                SourceFile = "fallback"
            });

            _monitor.Log($"[Echoes] Created {_allTemplates.Count} fallback templates.", LogLevel.Warn);
        }

        /// <summary>Build search index for fast template lookup</summary>
        private void BuildIndex()
        {
            _templateIndex.Clear();
            foreach (var t in _allTemplates)
            {
                string key = MakeIndexKey(t.NpcName, t.TriggerType);
                if (!_templateIndex.ContainsKey(key))
                    _templateIndex[key] = new List<MemoryTemplate>();
                _templateIndex[key].Add(t);
            }
        }

        private static string MakeIndexKey(string npc, TriggerType trigger) => $"{npc}|{trigger}";

        /// <summary>
        /// Select the best template for a given context.
        /// Returns null if no suitable template found.
        /// </summary>
        public MemoryTemplate? SelectTemplate(string npcName, TriggerType triggerType,
            string? actionType = null, string? location = null, string? weather = null,
            int? gameTime = null, string? season = null)
        {
            // 1. Get candidates for this NPC + trigger type, plus "Any" NPC
            var candidates = new List<MemoryTemplate>();

            string specificKey = MakeIndexKey(npcName, triggerType);
            string anyKey = MakeIndexKey("Any", triggerType);

            if (_templateIndex.TryGetValue(specificKey, out var specificList))
                candidates.AddRange(specificList);
            if (_templateIndex.TryGetValue(anyKey, out var anyList))
                candidates.AddRange(anyList);

            if (candidates.Count == 0) return null;

            // 2. Filter by conditions
            var filtered = candidates.Where(t => MatchConditions(t.Conditions, actionType, location, weather, gameTime, season)).ToList();
            if (filtered.Count == 0)
            {
                // Fallback: use templates without strict conditions
                filtered = candidates.Where(t =>
                    (t.Conditions.ObservedActions == null || t.Conditions.ObservedActions.Count == 0) &&
                    (t.Conditions.Locations == null || t.Conditions.Locations.Count == 0) &&
                    (t.Conditions.Weather == null || t.Conditions.Weather.Count == 0)
                ).ToList();
            }
            if (filtered.Count == 0) filtered = candidates;

            // 3. Check cooldown
            filtered = filtered.Where(t => IsOffCooldown(t, npcName)).ToList();
            if (filtered.Count == 0) return null;

            // 4. Score by priority
            var scored = filtered.Select(t => new
            {
                Template = t,
                Score = t.Priority
                    + (MatchLocation(t, location) ? 3 : 0)
                    + (MatchSeason(t, season) ? 2 : 0)
                    + (MatchWeather(t, weather) ? 2 : 0)
                    - GetRecentPenalty(t, npcName)
            }).OrderByDescending(x => x.Score).ToList();

            // 5. Weighted random from top 5
            int topN = Math.Min(5, scored.Count);
            var top = scored.Take(topN).ToList();
            int totalWeight = top.Sum(x => Math.Max(1, x.Score));
            int roll = RandomHelper.Next(totalWeight);
            int cumulative = 0;

            foreach (var entry in top)
            {
                cumulative += Math.Max(1, entry.Score);
                if (roll < cumulative)
                {
                    TrackUsage(entry.Template, npcName);
                    return entry.Template;
                }
            }

            // Fallthrough: return top pick
            var pick = top[0];
            TrackUsage(pick.Template, npcName);
            return pick.Template;
        }

        private bool MatchConditions(TemplateConditions cond, string? action, string? location,
            string? weather, int? time, string? season)
        {
            if (cond.ObservedActions != null && cond.ObservedActions.Count > 0 && action != null)
                if (!cond.ObservedActions.Contains(action, StringComparer.OrdinalIgnoreCase))
                    return false;

            if (cond.TimeOfDay != null && cond.TimeOfDay.Count > 0 && time.HasValue)
            {
                string period = time.Value switch
                {
                    < 600 => "night",
                    < 800 => "dawn",
                    < 1200 => "morning",
                    < 1600 => "afternoon",
                    < 2000 => "evening",
                    _ => "night"
                };
                if (!cond.TimeOfDay.Contains(period, StringComparer.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private bool MatchLocation(MemoryTemplate t, string? loc) =>
            loc != null && t.Conditions.Locations != null &&
            t.Conditions.Locations.Contains(loc, StringComparer.OrdinalIgnoreCase);

        private bool MatchSeason(MemoryTemplate t, string? season) =>
            season != null && t.Conditions.Seasons != null &&
            t.Conditions.Seasons.Contains(season, StringComparer.OrdinalIgnoreCase);

        private bool MatchWeather(MemoryTemplate t, string? weather) =>
            weather != null && t.Conditions.Weather != null &&
            t.Conditions.Weather.Contains(weather, StringComparer.OrdinalIgnoreCase);

        private bool IsOffCooldown(MemoryTemplate t, string npcName)
        {
            if (!_recentUsage.TryGetValue(npcName, out var used)) return true;
            if (!used.TryGetValue(t.TemplateId, out int lastUse)) return true;

            int daysSince = (Game1.year * 112 + Utility.getSeasonNumber(Game1.currentSeason) * 28 + Game1.dayOfMonth) - lastUse;
            return daysSince >= t.CooldownDays;
        }

        private int GetRecentPenalty(MemoryTemplate t, string npcName)
        {
            if (!_recentUsage.TryGetValue(npcName, out var used)) return 0;
            if (!used.TryGetValue(t.TemplateId, out int lastUse)) return 0;

            int daysSince = (Game1.year * 112 + Utility.getSeasonNumber(Game1.currentSeason) * 28 + Game1.dayOfMonth) - lastUse;
            return daysSince < 5 ? 3 : 0;
        }

        private void TrackUsage(MemoryTemplate t, string npcName)
        {
            if (!_recentUsage.ContainsKey(npcName))
                _recentUsage[npcName] = new Dictionary<string, int>();
            _recentUsage[npcName][t.TemplateId] =
                Game1.year * 112 + Utility.getSeasonNumber(Game1.currentSeason) * 28 + Game1.dayOfMonth;
        }

        /// <summary>Generate a journal entry from a template, with NPC-personalized content</summary>
        public Data.JournalEntry GenerateEntry(MemoryTemplate template, string npcName,
            Dictionary<string, string> parameters, int gameTime, string? location = null)
        {
            // Pick a text variant
            string variant = template.TextVariants.Count > 0
                ? template.TextVariants[RandomHelper.Next(template.TextVariants.Count)]
                : "今天在镇上看到了{{playerName}}。";

            // Get NPC voice and clarity
            var voice = _voiceRegistry.GetProfile(npcName);
            float clarity = MemoryClaritySystem.GetClarity(npcName);

            // Fill parameters
            variant = StringHelper.ReplaceParameters(variant, parameters);

            // ── Append NPC-personalized sentence to the raw text ──
            string npcFlavor = BuildNpcFlavor(voice, npcName, template.TriggerType, template.EmotionTag, clarity);
            if (!string.IsNullOrEmpty(npcFlavor) && !variant.Contains(npcFlavor))
                variant += " " + npcFlavor;

            // Render through NPC voice with clarity-adjusted intensity
            string text = voice != null
                ? _voiceRegistry.RenderEntry(variant, voice, parameters, clarity)
                : variant;

            // Strip ALL * characters and Unicode not in SDV font
            text = text.Replace("*", "");
            text = StringHelper.CleanUnicode(text);

            var entry = new Data.JournalEntry
            {
                NpcName = npcName,
                DisplayText = text,
                RawTemplateText = variant,
                TemplateId = template.TemplateId,
                EmotionTag = template.EmotionTag,
                Trigger = template.TriggerType,
                DaysPlayed = (int)Game1.stats.DaysPlayed,
                Season = Game1.currentSeason,
                Year = Game1.year,
                DayOfMonth = Game1.dayOfMonth,
                TimeOfDay = gameTime,
                LocationName = location ?? Game1.player?.currentLocation?.Name ?? "",
                Parameters = parameters
            };

            return entry;
        }

        // ═══════════════════════════════════════════════
        //  NPC-personalized parameter generators
        // ═══════════════════════════════════════════════

        /// <summary>Build a single NPC-personalized sentence based on voice profile, trigger, emotion, and clarity.</summary>
        private static string BuildNpcFlavor(NpcVoiceProfile? voice, string npcName, TriggerType trigger, string emotion, float clarity)
        {
            if (voice == null) return "";

            // Observation: what this NPC notices
            string quality = (voice.ObservedQualities != null && voice.ObservedQualities.Count > 0)
                ? voice.ObservedQualities[RandomHelper.Next(voice.ObservedQualities.Count)]
                : "一种说不清的东西";

            // Detail: trigger-specific context
            string detail = trigger switch
            {
                TriggerType.Environmental => RandomHelper.Next(3) switch {
                    0 => $"风吹过的时候{npcName}正好在看这边",
                    1 => $"{npcName}放下了手头的事",
                    _ => $"那一刻{npcName}觉得周围都安静了下来"
                },
                TriggerType.DirectInteraction => RandomHelper.Next(3) switch {
                    0 => $"说完话后{npcName}在原地站了一会儿",
                    1 => $"对话过后{npcName}想了想刚才的话",
                    _ => $"几句话的功夫，但{npcName}觉得不一样了"
                },
                TriggerType.Daydream => RandomHelper.Next(3) switch {
                    0 => $"看到你发呆的样子，{npcName}没上前打扰",
                    1 => $"静静站着的样子让{npcName}想起了一些事",
                    _ => $"发呆也是一种语言--{npcName}懂这个"
                },
                TriggerType.CrossDayObservation => RandomHelper.Next(3) switch {
                    0 => $"入睡前{npcName}又想起了白天",
                    1 => $"今天npcName比平时多留意了一些",
                    _ => $"日子一天天过，但有些瞬间会留下来"
                },
                _ => RandomHelper.Next(3) switch {
                    0 => $"{npcName}把这件事记在了心里",
                    1 => $"这件事让{npcName}想了很久",
                    _ => $"有些东西写下来才不会忘"
                }
            };

            // Feeling: emotion-specific inner state
            string feeling = emotion switch
            {
                "Warm" => RandomHelper.Next(3) switch { 0 => "心里暖了一下", 1 => "嘴角不自觉上扬", _ => "觉得今天是个好日子" },
                "Melancholy" => RandomHelper.Next(3) switch { 0 => "心里有点软", 1 => "忽然安静了一会儿", _ => "想起了很远的事" },
                "Curiosity" => RandomHelper.Next(3) switch { 0 => "想了解更多", 1 => "心里打了个问号--但善意的", _ => "觉得这个人有点不一样" },
                "Longing" => RandomHelper.Next(3) switch { 0 => "希望下次不会太久", 1 => "有点想再见一面", _ => "时间过得太快了" },
                "Wonder" => RandomHelper.Next(3) switch { 0 => "心里亮了一下", 1 => "好像发现了什么重要的东西", _ => "觉得这世界比想象的大" },
                "Nostalgia" => RandomHelper.Next(3) switch { 0 => "想起了从前", 1 => "旧时光的味道", _ => "记忆忽然涌上来" },
                "Reflection" => RandomHelper.Next(3) switch { 0 => "想了很多", 1 => "觉得应该记下来", _ => "有些事需要慢慢想" },
                _ => RandomHelper.Next(3) switch { 0 => "觉得今天不太一样", 1 => "心里记下了这一笔", _ => "这一天会留在记忆里" }
            };

            // Assemble based on clarity: higher clarity = richer detail
            if (clarity < 0.3f)
                return $"{detail}。{npcName}觉得{quality}。";
            else if (clarity < 0.6f)
                return $"{detail}——{feeling}。{npcName}注意到了{quality}。";
            else
                return $"{detail}。{feeling}。{npcName}特别留意到{quality}——也许该记下来。";
        }

        /// <summary>🔑 Public API: Register templates at runtime (for other mods)</summary>
        public void RegisterTemplates(List<MemoryTemplate> templates)
        {
            foreach (var t in templates)
            {
                t.SourceFile = "api";
                t.IsCustom = true;
                _allTemplates.Add(t);
            }
            BuildIndex();
            _monitor.Log($"[Echoes] API: Registered {templates.Count} templates from external source.", LogLevel.Debug);
        }

        /// <summary>🔑 Public API: Get all template IDs (for debugging)</summary>
        public List<string> GetAllTemplateIds() => _allTemplates.Select(t => t.TemplateId).ToList();

        // ── JSON wrapper ──
        private class TemplateFileWrapper
        {
            public List<MemoryTemplate> Templates { get; set; } = new();
        }
    }
}
