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

        /// <summary>Truncate text to max chars, adding ellipsis</summary>
        public static string Truncate(string text, int maxChars)
        {
            if (text.Length <= maxChars) return text;
            return text.Substring(0, maxChars - 3) + "...";
        }

        /// <summary>Wrap text to a max line width</summary>
        public static List<string> WrapText(string text, int maxWidth, Func<char, int>? charWidthFn = null)
        {
            var lines = new List<string>();
            string[] words = text.Split(' ');
            string currentLine = "";

            foreach (string word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                int lineLength = charWidthFn != null
                    ? testLine.Length // Use char width function if provided
                    : testLine.Length;

                if (lineLength > maxWidth && !string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);

            return lines;
        }
    }
}
