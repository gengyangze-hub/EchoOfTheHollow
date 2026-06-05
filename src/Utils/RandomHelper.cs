using System;
using System.Collections.Generic;

namespace EchoesOfTheHollow.Utils
{
    /// <summary>
    /// Seeded random number generator with tracking for repeat avoidance.
    /// Used by MemoryTemplateEngine to ensure varied template selection.
    /// </summary>
    internal static class RandomHelper
    {
        private static Random _random = new Random();
        private static readonly Dictionary<string, Queue<int>> RecentSelections = new();

        /// <summary>Get random int [0, max)</summary>
        public static int Next(int max) => _random.Next(max);

        /// <summary>Get random int [min, max)</summary>
        public static int Next(int min, int max) => _random.Next(min, max);

        /// <summary>Get random float [0.0, 1.0)</summary>
        public static double NextDouble() => _random.NextDouble();

        /// <summary>Weighted random index from a list of weights</summary>
        public static int WeightedIndex(List<int> weights)
        {
            int total = 0;
            foreach (int w in weights) total += w;
            int roll = Next(total);
            int sum = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                sum += weights[i];
                if (roll < sum) return i;
            }
            return weights.Count - 1;
        }

        /// <summary>Get a non-repeating random index (avoids the last N)</summary>
        public static int NextAvoiding(int max, string context, int avoidCount)
        {
            string key = $"idx_{context}";
            if (!RecentSelections.ContainsKey(key))
                RecentSelections[key] = new Queue<int>();

            var recent = RecentSelections[key];
            int result;
            int maxAttempts = 50;
            do
            {
                result = _random.Next(max);
                maxAttempts--;
            } while (recent.Contains(result) && maxAttempts > 0 && max > avoidCount);

            recent.Enqueue(result);
            while (recent.Count > avoidCount)
                recent.Dequeue();

            return result;
        }

        /// <summary>Shuffle a list in place</summary>
        public static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Decimal chance (0.0 to 1.0) -- returns true if random is under threshold</summary>
        public static bool Chance(double probability)
        {
            return _random.NextDouble() < probability;
        }

        /// <summary>Reset tracking for a context</summary>
        public static void ResetContext(string context)
        {
            string key = $"idx_{context}";
            RecentSelections.Remove(key);
        }

        /// <summary>Reset seed (for debugging)</summary>
        public static void SetSeed(int seed)
        {
            _random = new Random(seed);
            RecentSelections.Clear();
        }
    }
}
