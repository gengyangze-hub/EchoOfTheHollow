using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.NpcProfiles;
using EchoesOfTheHollow.Systems.Journal;
using EchoesOfTheHollow.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace EchoesOfTheHollow.UI
{
    /// <summary>
    /// 回音日志主界面 — the main journal book UI (IClickableMenu)
    /// Appearance: hand-bound paper journal with rough edges, dried flowers, ink stains.
    /// </summary>
    public class EchoJournalMenu : IClickableMenu
    {
        private readonly JournalSystem _journal;
        private readonly VoiceRegistry _voices;
        private readonly IModHelper _helper;

        private List<JournalEntry> _entries;
        private List<JournalEntry> _filteredEntries;
        private int _currentPage;
        private int _entriesPerPage = 3;

        // Filters
        private string? _filterNpc;
        private TriggerType? _filterTrigger;
        private string _sortMode = "newest";

        // UI state
        private Rectangle _leftPageBounds;
        private Rectangle _rightPageBounds;
        private Rectangle _prevButton;
        private Rectangle _nextButton;
        private Rectangle _filterButton;
        private Rectangle _sortButton;
        private Rectangle _diaryButton;
        private Rectangle _closeButton;
        private Rectangle _scrollUpButton;
        private Rectangle _scrollDownButton;

        // Scroll
        private int _scrollOffset;
        private int _maxScroll;

        // Visual
        private float _pageTurnProgress = 1.0f;
        private bool _isClosing;

        public EchoJournalMenu(JournalSystem journal, VoiceRegistry voices, IModHelper helper)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height)
        {
            _journal = journal;
            _voices = voices;
            _helper = helper;

            _entries = _journal.GetAllEntries();
            _filteredEntries = _entries;
            _currentPage = 0;

            CalculateLayout();
        }

        private void CalculateLayout()
        {
            int centerX = width / 2;
            int bookWidth = width - 200;
            int pageWidth = bookWidth / 2 - 20;
            int bookHeight = height - 100;
            int startY = 50;

            _leftPageBounds = new Rectangle(centerX - bookWidth / 2, startY, pageWidth, bookHeight);
            _rightPageBounds = new Rectangle(centerX + 10, startY, pageWidth, bookHeight);

            _prevButton = new Rectangle(_leftPageBounds.Left, _leftPageBounds.Bottom + 10, 100, 40);
            _nextButton = new Rectangle(_rightPageBounds.Right - 100, _rightPageBounds.Bottom + 10, 100, 40);
            _filterButton = new Rectangle(_leftPageBounds.Left - 120, startY, 100, 40);
            _sortButton = new Rectangle(_leftPageBounds.Left - 120, startY + 45, 100, 40);
            _diaryButton = new Rectangle(_leftPageBounds.Left - 120, startY + 90, 100, 40);
            _closeButton = new Rectangle(_rightPageBounds.Right + 20, startY, 40, 40);

            _scrollUpButton = new Rectangle(_rightPageBounds.Right + 5, _rightPageBounds.Top, 25, 25);
            _scrollDownButton = new Rectangle(_rightPageBounds.Right + 5, _rightPageBounds.Bottom - 25, 25, 25);
        }

        public override void draw(SpriteBatch b)
        {
            if (_isClosing)
            {
                _pageTurnProgress -= 0.1f;
                if (_pageTurnProgress <= 0)
                {
                    exitThisMenu();
                    return;
                }
            }

            // ── Background ──
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, width, height), Color.Black * 0.6f);

            // ── Journal book frame ──
            DrawBookFrame(b);

            // ── Current page display ──
            DrawCurrentEntries(b);

            // ── UI buttons ──
            DrawButtons(b);

            // ── Header ──
            DrawHeader(b);

            drawMouse(b);
        }

        private void DrawBookFrame(SpriteBatch b)
        {
            // Left cover
            var leftCover = new Rectangle(_leftPageBounds.X - 30, _leftPageBounds.Y - 30,
                _leftPageBounds.Width + 30, _leftPageBounds.Height + 60);
            DrawTexturedBox(b, leftCover, new Color(180, 150, 120)); // Leather brown

            // Right cover
            var rightCover = new Rectangle(_rightPageBounds.X, _rightPageBounds.Y - 30,
                _rightPageBounds.Width + 30, _rightPageBounds.Height + 60);
            DrawTexturedBox(b, rightCover, new Color(180, 150, 120));

            // Left page (cream paper)
            DrawTexturedBox(b, _leftPageBounds, new Color(255, 248, 230));

            // Right page (slightly more aged)
            DrawTexturedBox(b, _rightPageBounds, new Color(245, 238, 220));

            // Page spine shadow (center fold)
            var spine = new Rectangle(width / 2 - 2, _leftPageBounds.Y, 4, _leftPageBounds.Height);
            b.Draw(Game1.fadeToBlackRect, spine, new Color(100, 80, 60, 80));
        }

        private void DrawTexturedBox(SpriteBatch b, Rectangle rect, Color color)
        {
            // Simple textured box with rough edges
            b.Draw(Game1.fadeToBlackRect, rect, color);

            // Border
            int borderWidth = 3;
            var topBorder = new Rectangle(rect.X, rect.Y, rect.Width, borderWidth);
            var bottomBorder = new Rectangle(rect.X, rect.Bottom - borderWidth, rect.Width, borderWidth);
            var leftBorder = new Rectangle(rect.X, rect.Y, borderWidth, rect.Height);
            var rightBorder = new Rectangle(rect.Right - borderWidth, rect.Y, borderWidth, rect.Height);

            Color borderColor = new Color(140, 110, 80);
            b.Draw(Game1.fadeToBlackRect, topBorder, borderColor);
            b.Draw(Game1.fadeToBlackRect, bottomBorder, borderColor);
            b.Draw(Game1.fadeToBlackRect, leftBorder, borderColor);
            b.Draw(Game1.fadeToBlackRect, rightBorder, borderColor);
        }

        private void DrawHeader(SpriteBatch b)
        {
            string title = "回 音 日 志";
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            Vector2 titlePos = new Vector2(width / 2 - titleSize.X / 2, 10);

            // Title shadow
            b.DrawString(Game1.dialogueFont, title, titlePos + new Vector2(1, 1), Color.Black * 0.3f);
            b.DrawString(Game1.dialogueFont, title, titlePos, new Color(80, 50, 30));

            // Subtitle
            string subtitle = $"—— 第{_currentPage + 1}页 / {Math.Max(1, (int)Math.Ceiling((float)_filteredEntries.Count / _entriesPerPage))}页 ——";
            Vector2 subSize = Game1.smallFont.MeasureString(subtitle);
            b.DrawString(Game1.smallFont, subtitle, new Vector2(width / 2 - subSize.X / 2, 40), new Color(120, 80, 50));
        }

        private void DrawCurrentEntries(SpriteBatch b)
        {
            int startIdx = _currentPage * _entriesPerPage;
            int endIdx = Math.Min(startIdx + _entriesPerPage, _filteredEntries.Count);

            for (int i = startIdx; i < endIdx; i++)
            {
                var entry = _filteredEntries[i];
                int entryIndex = i - startIdx;

                Rectangle pageBounds = entryIndex switch
                {
                    0 => _leftPageBounds,
                    1 => _rightPageBounds,
                    2 => _leftPageBounds, // Second page (if showing 3)
                    _ => _rightPageBounds
                };

                if (entryIndex >= 2)
                    pageBounds = new Rectangle(pageBounds.X, pageBounds.Y + pageBounds.Height / 2,
                        pageBounds.Width, pageBounds.Height / 2);

                DrawEntryOnPage(b, entry, pageBounds);
            }
        }

        private void DrawEntryOnPage(SpriteBatch b, JournalEntry entry, Rectangle pageBounds)
        {
            int padding = 20;
            int x = pageBounds.X + padding;
            int y = pageBounds.Y + padding;
            int maxWidth = pageBounds.Width - padding * 2;

            // NPC name header
            string npcHeader = entry.NpcName;
            if (ModEntry.Config.UseNpcColorCoding)
            {
                Color npcColor = GetNpcColor(entry.NpcName);
                b.DrawString(Game1.smallFont, npcHeader, new Vector2(x, y), npcColor);
            }
            else
            {
                b.DrawString(Game1.smallFont, npcHeader, new Vector2(x, y), new Color(100, 60, 30));
            }

            // Date
            string dateStr = $"<{entry.DisplayDate}>";
            Vector2 dateSize = Game1.tinyFont.MeasureString(dateStr);
            b.DrawString(Game1.tinyFont, dateStr, new Vector2(x + maxWidth - dateSize.X, y), new Color(150, 120, 90));

            // Entry text (wrapped)
            y += 30;
            var lines = StringHelper.WrapText(entry.DisplayText, 35); // ~35 chars per line
            foreach (string line in lines.Take(8)) // Max 8 lines per entry
            {
                Vector2 textSize = Game1.smallFont.MeasureString(line);
                if (y + textSize.Y < pageBounds.Bottom - padding)
                {
                    // Paper aging effect on text color
                    int age = entry.EntryAgeDays;
                    float aging = Math.Min(0.8f, age / 200f * ModEntry.Config.JournalPageAgingSpeed);
                    Color textColor = new Color(
                        (byte)(60 - aging * 30),
                        (byte)(40 - aging * 20),
                        (byte)(20 - aging * 10)
                    );

                    if (entry.IsPlayerEntry)
                        textColor = new Color(40, 80, 120); // Blue-tinted for player entries

                    b.DrawString(Game1.smallFont, line, new Vector2(x, y), textColor, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
                    y += (int)(textSize.Y * 0.9f) + 4;
                }
            }

            // Line separator
            y += 5;
            b.Draw(Game1.fadeToBlackRect, new Rectangle(x, y, maxWidth, 1), new Color(180, 160, 140, 100));
        }

        private void DrawButtons(SpriteBatch b)
        {
            // Previous page
            if (_currentPage > 0)
                DrawButton(b, _prevButton, "◀ 上一页");

            // Next page
            if ((_currentPage + 1) * _entriesPerPage < _filteredEntries.Count)
                DrawButton(b, _nextButton, "下一页 ▶");

            // Filter button
            DrawButton(b, _filterButton, _filterNpc != null ? $"NPC: {_filterNpc}" : "🔍 筛选");

            // Sort button
            DrawButton(b, _sortButton, _sortMode == "newest" ? "最新优先" : "最早优先");

            // Diary button
            if (ModEntry.Config.EnablePlayerDiary)
                DrawButton(b, _diaryButton, "✎ 写日记");

            // Close button
            DrawButton(b, _closeButton, "✕");
        }

        private void DrawButton(SpriteBatch b, Rectangle rect, string text)
        {
            // Button background
            Color bgColor = rect.Contains(Game1.getMousePosition()) ? new Color(200, 170, 130) : new Color(180, 150, 110);
            b.Draw(Game1.fadeToBlackRect, rect, bgColor);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Color(140, 110, 80));

            // Button text
            Vector2 textSize = Game1.smallFont.MeasureString(text);
            Vector2 textPos = new Vector2(rect.Center.X - textSize.X / 2, rect.Center.Y - textSize.Y / 2);
            b.DrawString(Game1.smallFont, text, textPos, Color.Black * 0.8f);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (_isClosing) return;

            if (_closeButton.Contains(x, y))
            {
                _isClosing = true;
                Game1.playSound("bigSelect");
                return;
            }

            if (_prevButton.Contains(x, y) && _currentPage > 0)
            {
                _currentPage--;
                Game1.playSound("shwip");
            }

            if (_nextButton.Contains(x, y) && (_currentPage + 1) * _entriesPerPage < _filteredEntries.Count)
            {
                _currentPage++;
                Game1.playSound("shwip");
            }

            if (_filterButton.Contains(x, y))
            {
                CycleFilter();
                Game1.playSound("drumkit6");
            }

            if (_sortButton.Contains(x, y))
            {
                _sortMode = _sortMode == "newest" ? "oldest" : "newest";
                ApplyFilters();
                Game1.playSound("drumkit6");
            }

            if (_diaryButton.Contains(x, y) && ModEntry.Config.EnablePlayerDiary)
            {
                // Open diary writing UI
                Game1.playSound("slimeHit");
            }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        public override void receiveScrollWheelAction(int direction)
        {
            if (direction > 0 && _currentPage > 0)
                _currentPage--;
            else if (direction < 0 && (_currentPage + 1) * _entriesPerPage < _filteredEntries.Count)
                _currentPage++;
        }

        public override void performHoverAction(int x, int y) { }

        private void CycleFilter()
        {
            var npcs = _journal.GetKnownNpcs();
            if (npcs.Count == 0) return;

            if (_filterNpc == null)
                _filterNpc = npcs[0];
            else
            {
                int idx = npcs.IndexOf(_filterNpc);
                if (idx >= 0 && idx < npcs.Count - 1)
                    _filterNpc = npcs[idx + 1];
                else
                    _filterNpc = null;
            }

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            _filteredEntries = JournalFilter.Filter(_entries,
                npc: _filterNpc,
                trigger: _filterTrigger,
                sort: _sortMode == "newest" ? JournalFilter.SortMode.Newest : JournalFilter.SortMode.Oldest);
            _currentPage = 0;
        }

        private Color GetNpcColor(string npcName)
        {
            return npcName switch
            {
                "Abigail" => new Color(160, 80, 180),   // Purple
                "Alex" => new Color(220, 140, 60),      // Orange
                "Emily" => new Color(60, 160, 200),     // Blue
                "Haley" => new Color(240, 150, 180),    // Pink
                "Harvey" => new Color(60, 100, 120),    // Teal
                "Leah" => new Color(80, 140, 60),       // Green
                "Linus" => new Color(100, 160, 80),     // Forest green
                "Maru" => new Color(200, 60, 100),      // Red
                "Penny" => new Color(180, 140, 60),     // Gold
                "Robin" => new Color(200, 120, 40),     // Amber
                "Sam" => new Color(60, 120, 200),       // Bright blue
                "Sebastian" => new Color(40, 40, 60),   // Dark
                "Shane" => new Color(80, 60, 80),       // Murky purple
                _ => new Color(100, 80, 60)             // Brown (default)
            };
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            CalculateLayout();
        }
    }
}
