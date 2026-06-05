using System.Collections.Generic;
using Newtonsoft.Json;

namespace EchoesOfTheHollow.Utils
{
    /// <summary>
    /// Handles save/load of mod data via the game's modData dictionary.
    /// All keys are prefixed with "EchoesHollow/" for safety.
    /// </summary>
    internal static class ModDataHelper
    {
        private const string Prefix = "EchoesHollow";

        /// <summary>Save data to player's modData</summary>
        public static void Save<T>(string key, T value) where T : class
        {
            if (Game1.player == null) return;
            string fullKey = $"{Prefix}/{key}";
            try
            {
                string json = JsonConvert.SerializeObject(value, Formatting.None);
                Game1.player.modData[fullKey] = json;
            }
            catch (System.Exception ex)
            {
                Log.Error($"Failed to save modData key '{fullKey}': {ex.Message}");
            }
        }

        /// <summary>Load data from player's modData</summary>
        public static T? Load<T>(string key) where T : class
        {
            if (Game1.player == null) return null;
            string fullKey = $"{Prefix}/{key}";
            try
            {
                if (Game1.player.modData.TryGetValue(fullKey, out string? raw) && !string.IsNullOrEmpty(raw))
                {
                    return JsonConvert.DeserializeObject<T>(raw);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Failed to load modData key '{fullKey}': {ex.Message}");
            }
            return null;
        }

        /// <summary>Check if a key exists</summary>
        public static bool HasKey(string key)
        {
            if (Game1.player == null) return false;
            string fullKey = $"{Prefix}/{key}";
            return Game1.player.modData.ContainsKey(fullKey);
        }

        /// <summary>Remove a key</summary>
        public static void Remove(string key)
        {
            if (Game1.player == null) return;
            string fullKey = $"{Prefix}/{key}";
            Game1.player.modData.Remove(fullKey);
        }

        /// <summary>Load a list of entries, safely initializing if missing</summary>
        public static List<T> LoadList<T>(string key) where T : class
        {
            return Load<List<T>>(key) ?? new List<T>();
        }

        /// <summary>Save a list of entries</summary>
        public static void SaveList<T>(string key, List<T> list) where T : class
        {
            Save(key, list);
        }
    }
}
