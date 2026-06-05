using System;

namespace EchoesOfTheHollow.Utils
{
    /// <summary>
    /// Color generation for wind listening effects.
    /// Colors shift based on season, time of day, and location.
    /// </summary>
    internal static class ColorHelper
    {
        /// <summary>Get wind color palette for current context</summary>
        public static (float r, float g, float b, float a) GetWindColor(string season, int timeOfDay, string? location = null)
        {
            return season switch
            {
                "spring" => MergeColors(
                    (0.6f, 0.8f, 0.5f, 0.3f),  // Fresh green
                    (1.0f, 0.7f, 0.8f, 0.2f),  // Cherry pink
                    timeOfDay
                ),
                "summer" => MergeColors(
                    (0.2f, 0.6f, 0.9f, 0.3f),  // Sky blue
                    (1.0f, 0.9f, 0.3f, 0.2f),  // Golden
                    timeOfDay
                ),
                "fall" => MergeColors(
                    (0.9f, 0.5f, 0.2f, 0.3f),  // Autumn orange
                    (0.6f, 0.2f, 0.2f, 0.2f),  // Deep red
                    timeOfDay
                ),
                "winter" => MergeColors(
                    (0.8f, 0.9f, 1.0f, 0.25f), // Ice blue
                    (0.5f, 0.5f, 0.7f, 0.2f),  // Lavender
                    timeOfDay
                ),
                _ => (0.5f, 0.5f, 0.5f, 0.2f)
            };
        }

        private static (float r, float g, float b, float a) MergeColors(
            (float r, float g, float b, float a) day,
            (float r, float g, float b, float a) night,
            int timeOfDay)
        {
            // Night: 2000+, Day: 600-2000, Dawn/Dusk transition
            float t = timeOfDay switch
            {
                < 600 => 0.0f,           // Deep night
                < 800 => 0.3f,           // Dawn
                < 1800 => 1.0f,          // Full day
                < 2000 => 0.5f,          // Dusk
                < 2400 => 0.2f,          // Night
                _ => 0.0f                // Late night
            };

            return (
                Lerp(night.r, day.r, t),
                Lerp(night.g, day.g, t),
                Lerp(night.b, day.b, t),
                Lerp(night.a, day.a, t)
            );
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Get journal page aging color (paper yellowing)</summary>
        public static (byte r, byte g, byte b) GetPaperColor(int entryAgeDays, float agingSpeed)
        {
            float t = Math.Min(1.0f, entryAgeDays / (100.0f / agingSpeed));
            return (
                (byte)255,
                (byte)Lerp(255, 220, t),  // Yellowing
                (byte)Lerp(230, 180, t)   // More yellow
            );
        }
    }
}
