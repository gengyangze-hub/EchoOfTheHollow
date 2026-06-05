using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.Utils;
using StardewModdingAPI;
using StardewValley;

namespace EchoesOfTheHollow.Systems.Invitation
{
    /// <summary>
    /// 邀约留言柱 — replaces the quest board
    /// NPCs post casual hangout invitations. No rewards, no penalties, no deadlines.
    /// </summary>
    public class InvitationSystem
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private List<InvitationData> _invitations = new();
        private List<InvitationData> _activeInvitations = new();
        private const string SaveKey = "Invitation/v1";

        private static readonly (string npc, string activity, string location)[] InvitationTemplates =
        {
            ("Abigail", "一起去矿洞边捡石头", "Mountain"),
            ("Emily", "在树下看云的形状", "Town"),
            ("Gus", "来红鹤食堂尝一道新菜", "Town"),
            ("Leah", "在河边画画，可以一起", "Forest"),
            ("Linus", "坐在帐篷外听风", "Mountain"),
            ("Willy", "去海边等那条传说中的鱼", "Beach"),
            ("Robin", "看看新做的木工活", "Mountain"),
            ("Maru", "测试一个故意不完美的装置", "Mountain"),
            ("Clint", "修复一件旧铁器", "Town"),
            ("Pam", "傍晚一起在河边坐坐", "Town"),
        };

        public InvitationSystem(IModHelper helper, IMonitor monitor)
        {
            _helper = helper;
            _monitor = monitor;
        }

        /// <summary>Generate daily invitations — called on DayStarted</summary>
        public void GenerateDailyInvitations()
        {
            _activeInvitations.Clear();
            int count = RandomHelper.Next(2, 5); // 2-4 invitations per day

            var template = InvitationTemplates;
            RandomHelper.Shuffle(template.ToList()); // Not ideal but works for small list

            for (int i = 0; i < Math.Min(count, template.Length); i++)
            {
                var (npc, activity, location) = template[i];

                var invitation = new InvitationData
                {
                    FromNpc = npc,
                    Activity = activity,
                    Location = location,
                    TimeOfDay = i % 2 == 0 ? "下午" : "傍晚",
                    GeneratedDay = (int)Game1.stats.DaysPlayed,
                    ExpiryDay = (int)Game1.stats.DaysPlayed + RandomHelper.Next(2, 5),
                    DisplayText = $"{npc}邀你去{location}{activity}。没有压力，来不来都可以。"
                };

                _activeInvitations.Add(invitation);
            }

            _monitor.Log($"[Invitation] Generated {_activeInvitations.Count} invitations.", LogLevel.Debug);
        }

        /// <summary>Accept an invitation</summary>
        public void AcceptInvitation(string invitationId)
        {
            var invitation = _activeInvitations.FirstOrDefault(i => i.Id == invitationId);
            if (invitation == null) return;

            invitation.IsAccepted = true;
            _invitations.Add(invitation);
            _activeInvitations.Remove(invitation);

            _monitor.Log($"[Invitation] Accepted: {invitation.FromNpc}'s {invitation.Activity}", LogLevel.Info);

            // Maybe trigger a journal entry later

            if (Game1.player != null)
                Game1.addHUDMessage(new HUDMessage($"{invitation.FromNpc}会在{invitation.Location}等你的。", HUDMessage.newQuest_type));
        }

        public List<InvitationData> GetActiveInvitations() => _activeInvitations.ToList();

        // ── Save/Load ──

        public void OnSaveLoaded()
        {
            _invitations = ModDataHelper.LoadList<InvitationData>(SaveKey);
        }

        public void OnSaving()
        {
            var allInvitations = new List<InvitationData>();
            allInvitations.AddRange(_invitations);
            allInvitations.AddRange(_activeInvitations);
            ModDataHelper.SaveList(SaveKey, allInvitations);
        }

        public void OnDayStarted()
        {
            GenerateDailyInvitations();
        }

        public void OnDayEnding() { }
    }
}
