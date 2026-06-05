using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheHollow.Data;

namespace EchoesOfTheHollow.Systems.Journal
{
    /// <summary>
    /// 玩家日记系统 — allows the player to write their own entries in the journal.
    /// </summary>
    internal class PlayerDiarySystem
    {
        private readonly JournalSystem _journal;

        public PlayerDiarySystem(JournalSystem journal)
        {
            _journal = journal;
        }

        public void WriteEntry(string title, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _journal.AddPlayerEntry(title.Length > 0 ? title : "无题", text);
        }

        public List<JournalEntry> GetPlayerEntries()
        {
            return _journal.GetFiltered(npcName: "我", trigger: TriggerType.PlayerDiary);
        }

        public void DeleteEntry(string entryId)
        {
            _journal.RemoveEntry(entryId);
        }
    }
}
