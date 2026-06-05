using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EchoesOfTheHollow.Utils
{
    /// <summary>
    /// String helper utilities for template parameter substitution and text processing.
    /// </summary>
    internal static class StringHelper
    {
        /// <summary>Replace {{key}} placeholders with values from a dictionary</summary>
        public static string ReplaceParameters(string template, Dictionary<string, string> parameters)
        {
            string result = template;
            foreach (var kv in parameters)
            {
                result = result.Replace($"{{{{{kv.Key}}}}}", kv.Value ?? "");
            }
            // Replace any remaining unmatched placeholders
            result = Regex.Replace(result, @"\{\{.*?\}\}", "...");
            return result;
        }

        /// <summary>Replace whole words (case-insensitive) in text</summary>
        public static string ReplaceWords(string text, Dictionary<string, string> wordMap)
        {
            string result = text;
            foreach (var kv in wordMap)
            {
                string pattern = $@"\b{Regex.Escape(kv.Key)}\b";
                result = Regex.Replace(result, pattern, kv.Value, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));
            }
            return result;
        }

        /// <summary>Convert game time (e.g. 630) to display string</summary>
        public static string TimeOfDayToString(int time)
        {
            int hours = time / 100;
            int minutes = time % 100;
            return $"{hours:D2}:{minutes:D2}";
        }

        /// <summary>Convert time to period name</summary>
        public static string TimeOfDayToPeriod(int time)
        {
            if (time < 600) return "深夜";
            if (time < 800) return "清晨";
            if (time < 1200) return "上午";
            if (time < 1600) return "午后";
            if (time < 2000) return "傍晚";
            if (time < 2400) return "夜晚";
            return "深夜";
        }

        /// <summary>Get capitalized display season name</summary>
        public static string SeasonDisplayName(string season) => season switch
        {
            "spring" => "春季",
            "summer" => "夏季",
            "fall" => "秋季",
            "winter" => "冬季",
            _ => season
        };

        /// <summary>Format date display</summary>
        public static string FormatDate(int year, string season, int day)
        {
            return $"{SeasonDisplayName(season)} {day}日, 第{year}年";
        }

        /// <summary>Strip Unicode characters not available in SDV's font</summary>
        public static string CleanUnicode(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text
                .Replace('--', '-')
                .Replace('--', '-')
                .Replace('-', '-')
                .Replace('-', '-')
                .Replace('-', '-')
                .Replace('-', '-')
                .Replace('...', "...")
                .Replace('...', "...")
                .Replace('‘', '\'')
                .Replace('‘', '\'')
                .Replace('’', '\'')
                .Replace('’', '\'')
                .Replace('"', '"')
                .Replace('"', '"')
                .Replace('"', '"')
                .Replace('"', '"')
                .Replace('「', '"')
                .Replace('」', '"')
                .Replace('★', '*')
                .Replace('☆', '*');
        }

        /// <summary>Truncate text to max chars, adding ellipsis</summary>
        public static string Truncate(string text, int maxChars)
        {
            if (text.Length <= maxChars) return text;
            return text.Substring(0, maxChars - 3) + "...";
        }

        /// <summary>Wrap text using the available pixel width for accurate line breaking.</summary>
        public static List<string> WrapText(string text, int availablePixelWidth, float fontScale = 0.85f)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) { lines.Add(""); return lines; }

            string[] paragraphs = text.Split('\n');

            foreach (string para in paragraphs)
            {
                if (string.IsNullOrEmpty(para))
                {
                    lines.Add("");
                    continue;
                }

                // Build lines character by character, measuring actual pixel width
                string currentLine = "";
                for (int i = 0; i < para.Length; i++)
                {
                    char c = para[i];
                    string candidate = currentLine + c;
                    float width = Game1.smallFont.MeasureString(candidate).X * fontScale;

                    if (width > availablePixelWidth && currentLine.Length > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = c.ToString();
                    }
                    else
                    {
                        currentLine = candidate;
                    }
                }
                if (currentLine.Length > 0)
                    lines.Add(currentLine);
            }

            return lines;
        }
    }
}
