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
    /// 回音日志主界面 — 手订皮质日志本
    /// Hand-bound leather journal with aged paper, ink stains, dried flowers, and stitching.
    /// All visual effects are purely code-drawn — no external textures needed.
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

        // UI layout
        private Rectangle _leftPageBounds;
        private Rectangle _rightPageBounds;
        private Rectangle _bookFrameBounds;
        private Rectangle _prevButton;
        private Rectangle _nextButton;
        private Rectangle _filterButton;
        private Rectangle _sortButton;
        private Rectangle _diaryButton;
        private Rectangle _markButton;
        private Rectangle _closeButton;

        // Visual state
        private float _pageTurnProgress = 1.0f;
        private bool _isClosing;
        private float _time;
        private int _seed;

        // Calendar memory marks
        private static HashSet<string> _markedDates = new(); // "Year-Season-Day" format
        private const string MarkSaveKey = "MemoryMarks/v1";
        private bool _hasMarkedEntry; // Cached — recomputed in ApplyFilters/ToggleMark

        private static string DateKey(int year, string season, int day) => $"{year}-{season}-{day}";

        // Decorative ink stain positions (pseudo-random, same every time)
        private readonly Vector2[] _inkStains = new Vector2[6];
        private readonly float[] _inkSizes = new float[6];
        private readonly float[] _inkRotations = new float[6];

        public EchoJournalMenu(JournalSystem journal, VoiceRegistry voices, IModHelper helper)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height)
        {
            _journal = journal;
            _voices = voices;
            _helper = helper;

            _entries = _journal.GetAllEntries();
            _filteredEntries = _entries;
            _currentPage = 0;
            _seed = (int)Game1.stats.DaysPlayed + Game1.year * 100;

            // Generate stable pseudo-random ink stains
            var rng = new Random(_seed);
            for (int i = 0; i < _inkStains.Length; i++)
            {
                _inkStains[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                _inkSizes[i] = 4f + (float)rng.NextDouble() * 12f;
                _inkRotations[i] = (float)rng.NextDouble() * MathHelper.TwoPi;
            }

            CalculateLayout();
        }

        private void CalculateLayout()
        {
            int centerX = width / 2;
            int bookWidth = 680;
            int pageWidth = bookWidth / 2 - 30;
            int bookHeight = Math.Min(520, height - 140);
            int startY = (height - bookHeight) / 2;

            _bookFrameBounds = new Rectangle(centerX - bookWidth / 2 - 35, startY - 35,
                bookWidth + 70, bookHeight + 70);

            _leftPageBounds = new Rectangle(centerX - bookWidth / 2, startY, pageWidth, bookHeight);
            _rightPageBounds = new Rectangle(centerX + 30, startY, pageWidth, bookHeight);

            // Navigation buttons below the book
            int btnY = _bookFrameBounds.Bottom + 8;
            _prevButton = new Rectangle(_leftPageBounds.Left, btnY, 120, 36);
            _nextButton = new Rectangle(_rightPageBounds.Right - 120, btnY, 120, 36);

            // Filter buttons above the book (left)
            _filterButton = new Rectangle(_leftPageBounds.Left - 140, startY, 120, 32);
            _sortButton = new Rectangle(_leftPageBounds.Left - 140, startY + 38, 120, 32);
            _diaryButton = new Rectangle(_leftPageBounds.Left - 140, startY + 76, 120, 32);
            _markButton = new Rectangle(_leftPageBounds.Left - 140, startY + 114, 120, 32);

            // Close button top-right
            _closeButton = new Rectangle(_bookFrameBounds.Right - 2, startY - 10, 34, 34);
        }

        // ═══════════════════════════════════════════════════════════
        //  MAIN DRAW
        // ═══════════════════════════════════════════════════════════

        public override void draw(SpriteBatch b)
        {
            _time += 0.016f;

            if (_isClosing)
            {
                _pageTurnProgress -= 0.08f;
                if (_pageTurnProgress <= 0) { exitThisMenu(); return; }
            }

            // ── Dim background ──
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, width, height), Color.Black * 0.65f);

            // ── Book drop shadow ──
            DrawShadow(b, _bookFrameBounds, 8);

            // ── Leather cover ──
            DrawLeatherCover(b);

            // ── Stitching around cover ──
            DrawStitching(b, _bookFrameBounds, 12);

            // ── Book spine (center) ──
            DrawSpine(b);

            // ── Left page (cream paper) ──
            DrawAgedPage(b, _leftPageBounds, isLeft: true);

            // ── Right page (slightly different aging) ──
            DrawAgedPage(b, _rightPageBounds, isLeft: false);

            // ── Page corner decorations ──
            DrawCornerFlourish(b, _leftPageBounds, mirror: true);
            DrawCornerFlourish(b, _rightPageBounds, mirror: false);

            // ── Ink stains (subtle) ──
            DrawInkStains(b);

            // ── Current entries ──
            DrawCurrentEntries(b);

            // ── Header ──
            DrawHeader(b);

            // ── Buttons ──
            DrawButtons(b);

            drawMouse(b);
        }

        // ═══════════════════════════════════════════════════════════
        //  BOOK FRAME — LEATHER COVER
        // ═══════════════════════════════════════════════════════════

        private void DrawLeatherCover(SpriteBatch b)
        {
            var r = _bookFrameBounds;

            // Main leather body
            Color leather = new Color(120, 85, 50);
            Color leatherDark = new Color(85, 55, 30);
            Color leatherEdge = new Color(150, 110, 65);

            // Base fill
            b.Draw(Game1.fadeToBlackRect, r, leather);

            // Inner bevel (darker edge)
            int bevel = 4;
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X + bevel, r.Y + bevel, r.Width - bevel * 2, r.Height - bevel * 2), leatherEdge * 0.3f);

            // Leather grain — subtle horizontal streaks
            var grainRng = new Random(_seed);
            for (int i = 0; i < r.Height; i += 3)
            {
                float alpha = 0.02f + (float)grainRng.NextDouble() * 0.04f;
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(r.X + 6, r.Y + i, r.Width - 12, 2),
                    Color.Black * alpha);
            }

            // Top & bottom bands (darker leather)
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X, r.Y, r.Width, 18), leatherDark * 0.5f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X, r.Bottom - 18, r.Width, 18), leatherDark * 0.5f);
        }

        private void DrawStitching(SpriteBatch b, Rectangle frame, int inset)
        {
            // Dashed line stitching around the book cover
            Color thread = new Color(180, 155, 120);
            int dashLen = 6;
            int gapLen = 4;
            int cycle = dashLen + gapLen;

            // Top edge
            for (int x = frame.X + inset; x < frame.Right - inset; x += cycle)
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(x, frame.Y + inset - 1, Math.Min(dashLen, frame.Right - inset - x), 2), thread);

            // Bottom edge
            for (int x = frame.X + inset; x < frame.Right - inset; x += cycle)
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(x, frame.Bottom - inset - 1, Math.Min(dashLen, frame.Right - inset - x), 2), thread);

            // Left edge
            for (int y = frame.Y + inset; y < frame.Bottom - inset; y += cycle)
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(frame.X + inset - 1, y, 2, Math.Min(dashLen, frame.Bottom - inset - y)), thread);

            // Right edge
            for (int y = frame.Y + inset; y < frame.Bottom - inset; y += cycle)
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(frame.Right - inset - 1, y, 2, Math.Min(dashLen, frame.Bottom - inset - y)), thread);
        }

        private void DrawSpine(SpriteBatch b)
        {
            int spineX = width / 2 - 8;
            var spineRect = new Rectangle(spineX, _bookFrameBounds.Y + 10, 16, _bookFrameBounds.Height - 20);

            // Spine leather (darker)
            Color spine = new Color(90, 60, 35);
            b.Draw(Game1.fadeToBlackRect, spineRect, spine);

            // Spine ribs (horizontal bands)
            int ribCount = 4;
            for (int i = 0; i < ribCount; i++)
            {
                int ribY = spineRect.Y + (i + 1) * spineRect.Height / (ribCount + 1);
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(spineRect.X, ribY - 1, spineRect.Width, 3),
                    new Color(140, 100, 60));
            }

            // Center crease shadow
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle(width / 2 - 1, _bookFrameBounds.Y, 2, _bookFrameBounds.Height),
                Color.Black * 0.15f);
        }

        // ═══════════════════════════════════════════════════════════
        //  PAGES — AGED PAPER
        // ═══════════════════════════════════════════════════════════

        private void DrawAgedPage(SpriteBatch b, Rectangle bounds, bool isLeft)
        {
            // Paper base — cream with slight variation
            Color paperBase = isLeft
                ? new Color(252, 246, 230)
                : new Color(248, 240, 222);

            b.Draw(Game1.fadeToBlackRect, bounds, paperBase);

            // Paper grain — very subtle noise
            var rng = new Random(_seed + (isLeft ? 0 : 1000));
            for (int i = 0; i < 40; i++)
            {
                int gx = bounds.X + rng.Next(bounds.Width);
                int gy = bounds.Y + rng.Next(bounds.Height);
                int gw = 2 + rng.Next(8);
                int gh = 1 + rng.Next(2);
                float ga = 0.005f + (float)rng.NextDouble() * 0.02f;
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(gx, gy, gw, gh),
                    new Color(200, 180, 140) * ga);
            }

            // Page edge shadow (deckle edge simulation)
            int edgeW = 3;
            Color edgeColor = new Color(180, 150, 110);
            if (isLeft)
            {
                b.Draw(Game1.fadeToBlackRect, new Rectangle(bounds.Right - edgeW, bounds.Y, edgeW, bounds.Height), edgeColor * 0.3f);
                b.Draw(Game1.fadeToBlackRect, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), edgeColor * 0.15f);
                b.Draw(Game1.fadeToBlackRect, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), edgeColor * 0.15f);
            }
            else
            {
                b.Draw(Game1.fadeToBlackRect, new Rectangle(bounds.X, bounds.Y, edgeW, bounds.Height), edgeColor * 0.3f);
                b.Draw(Game1.fadeToBlackRect, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), edgeColor * 0.15f);
                b.Draw(Game1.fadeToBlackRect, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), edgeColor * 0.15f);
            }

            // Water stain — subtle circular discoloration
            float stainX = bounds.X + bounds.Width * (isLeft ? 0.75f : 0.25f);
            float stainY = bounds.Y + bounds.Height * 0.7f;
            for (int ring = 0; ring < 5; ring++)
            {
                float radius = 20f + ring * 18f;
                float alpha = 0.03f - ring * 0.005f;
                DrawCircle(b, new Vector2(stainX, stainY), radius, new Color(180, 160, 130) * alpha, 32);
            }
        }

        private void DrawShadow(SpriteBatch b, Rectangle rect, int offset)
        {
            for (int i = offset; i >= 0; i -= 2)
            {
                float alpha = 0.15f * (1f - (float)i / offset);
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(rect.X + i, rect.Y + i, rect.Width, rect.Height),
                    Color.Black * alpha);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  DECORATIVE ELEMENTS
        // ═══════════════════════════════════════════════════════════

        private void DrawCornerFlourish(SpriteBatch b, Rectangle bounds, bool mirror)
        {
            // Simple corner ornament — small arcs
            Color ink = new Color(100, 70, 40, 60);

            int pad = 15;
            int size = 20;
            float thick = 1.5f;

            // Top-inner corner
            int cx = mirror ? bounds.Right - pad : bounds.X + pad;
            int cy = bounds.Y + pad;

            // Corner arc
            DrawArc(b, new Vector2(cx, cy), size, mirror ? 0f : MathHelper.PiOver2, MathHelper.PiOver2, ink, thick, 8);

            // Small dot at corner
            DrawCircle(b, new Vector2(cx, cy), 2.5f, ink * 1.5f, 8);
        }

        private void DrawInkStains(SpriteBatch b)
        {
            for (int i = 0; i < _inkStains.Length; i++)
            {
                bool isLeft = i < 3;
                var bounds = isLeft ? _leftPageBounds : _rightPageBounds;

                float x = bounds.X + _inkStains[i].X * bounds.Width;
                float y = bounds.Y + _inkStains[i].Y * bounds.Height;
                float size = _inkSizes[i];

                // Only draw if within page bounds
                if (x > bounds.X + 20 && x < bounds.Right - 20 && y > bounds.Y + 40 && y < bounds.Bottom - 10)
                {
                    Color ink = new Color(60, 40, 30, 15);
                    DrawCircle(b, new Vector2(x, y), size, ink, 12);

                    // Ink splatter dots
                    if (size > 6)
                    {
                        var rng = new Random((int)(_seed + i * 137));
                        for (int s = 0; s < 3; s++)
                        {
                            float sx = x + (float)(rng.NextDouble() - 0.5) * size * 1.5f;
                            float sy = y + (float)(rng.NextDouble() - 0.5) * size * 1.5f;
                            float ss = 0.8f + (float)rng.NextDouble() * 2f;
                            DrawCircle(b, new Vector2(sx, sy), ss, ink * 0.6f, 6);
                        }
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  HEADER
        // ═══════════════════════════════════════════════════════════

        private void DrawHeader(SpriteBatch b)
        {
            string title = "回 音 日 志";
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            float titleX = _bookFrameBounds.Center.X - titleSize.X / 2;
            float titleY = _bookFrameBounds.Y - 42;

            // Title on a small leather plaque
            var plaqueRect = new Rectangle((int)titleX - 20, (int)titleY - 5, (int)titleSize.X + 40, (int)titleSize.Y + 16);
            b.Draw(Game1.fadeToBlackRect, plaqueRect, new Color(100, 65, 35));
            b.Draw(Game1.fadeToBlackRect, new Rectangle(plaqueRect.X, plaqueRect.Y, plaqueRect.Width, 2), new Color(160, 120, 70));
            b.Draw(Game1.fadeToBlackRect, new Rectangle(plaqueRect.X, plaqueRect.Bottom - 2, plaqueRect.Width, 2), new Color(160, 120, 70));
            b.Draw(Game1.fadeToBlackRect, new Rectangle(plaqueRect.X, plaqueRect.Y, 2, plaqueRect.Height), new Color(160, 120, 70));
            b.Draw(Game1.fadeToBlackRect, new Rectangle(plaqueRect.Right - 2, plaqueRect.Y, 2, plaqueRect.Height), new Color(160, 120, 70));

            // Title text with gold-ish color
            b.DrawString(Game1.dialogueFont, title, new Vector2(titleX + 1, titleY + 7), Color.Black * 0.3f);
            b.DrawString(Game1.dialogueFont, title, new Vector2(titleX, titleY + 6), new Color(210, 180, 130));

            // Page number at bottom
            string pageNum = $"—— {_currentPage + 1} / {Math.Max(1, (int)Math.Ceiling((float)_filteredEntries.Count / _entriesPerPage))} ——";
            Vector2 pageSize = Game1.tinyFont.MeasureString(pageNum);
            float pageNumX = width / 2 - pageSize.X / 2;
            float pageNumY = _bookFrameBounds.Bottom + 5;

            b.DrawString(Game1.tinyFont, pageNum, new Vector2(pageNumX, pageNumY), new Color(160, 130, 90));
        }

        // ═══════════════════════════════════════════════════════════
        //  ENTRIES
        // ═══════════════════════════════════════════════════════════

        private void DrawCurrentEntries(SpriteBatch b)
        {
            int startIdx = _currentPage * _entriesPerPage;
            int endIdx = Math.Min(startIdx + _entriesPerPage, _filteredEntries.Count);

            for (int i = startIdx; i < endIdx; i++)
            {
                var entry = _filteredEntries[i];
                int entryIdx = i - startIdx;

                Rectangle pageBounds;
                bool isRightPage;

                if (entryIdx == 0) { pageBounds = _leftPageBounds; isRightPage = false; }
                else if (entryIdx == 1) { pageBounds = _rightPageBounds; isRightPage = true; }
                else
                {
                    // Third entry: bottom half of right page (only in certain layouts)
                    pageBounds = new Rectangle(_leftPageBounds.X, _leftPageBounds.Y + _leftPageBounds.Height / 2,
                        _leftPageBounds.Width, _leftPageBounds.Height / 2);
                    isRightPage = false;
                }

                DrawEntryOnPage(b, entry, pageBounds, isRightPage);
            }
        }

        private void DrawEntryOnPage(SpriteBatch b, JournalEntry entry, Rectangle pageBounds, bool isRightPage)
        {
            int padX = 22;
            int padY = 18;
            int x = pageBounds.X + padX;
            int y = pageBounds.Y + padY;
            int maxW = pageBounds.Width - padX * 2;
            int maxH = pageBounds.Height - padY * 2;

            // ── NPC name with decorative underline ──
            string npcHeader = isRightPage ? $"  {entry.NpcName}" : $"{entry.NpcName}  ";
            Color npcColor = ModEntry.Config.UseNpcColorCoding
                ? GetNpcColor(entry.NpcName)
                : new Color(90, 55, 30);

            Vector2 nameSize = Game1.smallFont.MeasureString(npcHeader);
            float nameX = isRightPage ? x : x + maxW - nameSize.X;
            b.DrawString(Game1.smallFont, npcHeader, new Vector2(nameX, y), npcColor);

            // Decorative underline
            int lineY = y + (int)nameSize.Y + 2;
            Color lineColor = npcColor * 0.4f;
            if (isRightPage)
                b.Draw(Game1.fadeToBlackRect, new Rectangle((int)nameX, lineY, (int)nameSize.X, 1), lineColor);
            else
                b.Draw(Game1.fadeToBlackRect, new Rectangle((int)nameX, lineY, (int)nameSize.X, 1), lineColor);

            // ── Date (small, top-right or top-left) ──
            string dateStr = entry.DisplayDate;
            Vector2 dateSize = Game1.tinyFont.MeasureString(dateStr);
            float dateX = isRightPage ? x + maxW - dateSize.X : x;
            b.DrawString(Game1.tinyFont, dateStr, new Vector2(dateX, y), new Color(150, 120, 90));

            // ── Emotion tag icon ──
            string emoji = entry.EmotionTag switch
            {
                "Warm" => "✦", "Melancholy" => "☁", "Curiosity" => "◈",
                "Humor" => "♧", "Longing" => "☾", "Wonder" => "✧",
                "Nostalgia" => "❦", "Reflection" => "♢", _ => ""
            };
            if (emoji.Length > 0)
            {
                float emojiX = isRightPage ? dateX - 16 : dateX + dateSize.X + 4;
                b.DrawString(Game1.tinyFont, emoji, new Vector2(emojiX, y), npcColor * 0.7f);
            }

            // ── Entry text ──
            y += (int)nameSize.Y + 10;
            var lines = StringHelper.WrapText(entry.DisplayText, isRightPage ? 32 : 34);

            int age = entry.EntryAgeDays;
            float aging = Math.Min(0.75f, age / 180f * ModEntry.Config.JournalPageAgingSpeed);
            Color textColor = entry.IsPlayerEntry
                ? new Color(45, 70, 100)
                : new Color(
                    (byte)(55 - aging * 25),
                    (byte)(38 - aging * 18),
                    (byte)(22 - aging * 10));

            foreach (string line in lines.Take(7))
            {
                Vector2 textSize = Game1.smallFont.MeasureString(line);
                if (y + textSize.Y < pageBounds.Bottom - padY)
                {
                    b.DrawString(Game1.smallFont, line,
                        new Vector2(x, y), textColor, 0f, Vector2.Zero,
                        0.88f, SpriteEffects.None, 0f);
                    y += (int)(textSize.Y * 0.88f) + 3;
                }
            }

            // ── Memory mark indicator ──
            if (_markedDates.Contains(DateKey(entry.Year, entry.Season, entry.DayOfMonth)))
            {
                b.DrawString(Game1.tinyFont, "📌",
                    new Vector2(pageBounds.Right - padX - 20, pageBounds.Y + padY),
                    new Color(180, 100, 80, 200));
            }

            // ── Ornamental divider after entry ──
            y += 6;
            int dividerY = Math.Min(y, pageBounds.Bottom - padY);
            DrawOrnamentalDivider(b, new Vector2(x, dividerY), maxW);
        }

        private void DrawOrnamentalDivider(SpriteBatch b, Vector2 pos, int width)
        {
            Color divider = new Color(160, 130, 100, 100);
            int midX = (int)pos.X + width / 2;

            // Left flourish
            b.Draw(Game1.fadeToBlackRect, new Rectangle((int)pos.X + 10, (int)pos.Y, width / 2 - 20, 1), divider);
            // Right flourish
            b.Draw(Game1.fadeToBlackRect, new Rectangle(midX + 10, (int)pos.Y, width / 2 - 20, 1), divider);
            // Center diamond
            b.Draw(Game1.fadeToBlackRect, new Rectangle(midX - 3, (int)pos.Y - 1, 6, 3), divider * 1.5f);
            // Small dots flanking center
            b.Draw(Game1.fadeToBlackRect, new Rectangle(midX - 12, (int)pos.Y, 4, 1), divider);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(midX + 8, (int)pos.Y, 4, 1), divider);
        }

        // ═══════════════════════════════════════════════════════════
        //  BUTTONS
        // ═══════════════════════════════════════════════════════════

        private void DrawButtons(SpriteBatch b)
        {
            // Previous / Next
            if (_currentPage > 0)
                DrawNavButton(b, _prevButton, "◀ 前一页", isActive: true);
            else
                DrawNavButton(b, _prevButton, "◀ 前一页", isActive: false);

            int maxPage = Math.Max(1, (int)Math.Ceiling((float)_filteredEntries.Count / _entriesPerPage)) - 1;
            if (_currentPage < maxPage)
                DrawNavButton(b, _nextButton, "后一页 ▶", isActive: true);
            else
                DrawNavButton(b, _nextButton, "后一页 ▶", isActive: false);

            // Side buttons
            DrawSideButton(b, _filterButton, _filterNpc != null ? $"✦ {_filterNpc}" : "🔍 筛选");
            DrawSideButton(b, _sortButton, _sortMode == "newest" ? "↓ 最新" : "↑ 最早");
            if (ModEntry.Config.EnablePlayerDiary)
                DrawSideButton(b, _diaryButton, "✎ 日记");

            DrawSideButton(b, _markButton, _hasMarkedEntry ? "📌 已标记" : "📌 标记此页");

            // Close button
            DrawCloseButton(b, _closeButton);
        }

        private void DrawNavButton(SpriteBatch b, Rectangle rect, string text, bool isActive)
        {
            bool hover = rect.Contains(Game1.getMousePosition()) && isActive;
            Color bg = isActive
                ? (hover ? new Color(150, 110, 65) : new Color(120, 85, 50))
                : new Color(80, 60, 40);
            Color textCol = isActive
                ? (hover ? Color.Wheat : new Color(200, 170, 130))
                : new Color(120, 100, 80);

            b.Draw(Game1.fadeToBlackRect, rect, bg);
            // Subtle bevel
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Color(180, 140, 90) * 0.5f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Color(60, 40, 20) * 0.5f);

            Vector2 ts = Game1.smallFont.MeasureString(text);
            b.DrawString(Game1.smallFont, text,
                new Vector2(rect.Center.X - ts.X / 2, rect.Center.Y - ts.Y / 2), textCol);
        }

        private void DrawSideButton(SpriteBatch b, Rectangle rect, string text)
        {
            bool hover = rect.Contains(Game1.getMousePosition());
            Color bg = hover ? new Color(140, 100, 60) : new Color(105, 75, 45);
            Color textCol = hover ? Color.Wheat : new Color(190, 160, 120);

            b.Draw(Game1.fadeToBlackRect, rect, bg);
            Vector2 ts = Game1.smallFont.MeasureString(text);
            b.DrawString(Game1.smallFont, text,
                new Vector2(rect.Center.X - ts.X / 2, rect.Center.Y - ts.Y / 2),
                textCol, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
        }

        private void DrawCloseButton(SpriteBatch b, Rectangle rect)
        {
            bool hover = rect.Contains(Game1.getMousePosition());
            Color bg = hover ? new Color(180, 60, 40) : new Color(120, 40, 30);
            Color fg = hover ? Color.White : new Color(220, 180, 160);

            b.Draw(Game1.fadeToBlackRect, rect, bg);
            Vector2 xSize = Game1.smallFont.MeasureString("✕");
            b.DrawString(Game1.smallFont, "✕",
                new Vector2(rect.Center.X - xSize.X / 2, rect.Center.Y - xSize.Y / 2 + 1), fg);
        }

        // ═══════════════════════════════════════════════════════════
        //  PRIMITIVE DRAWING HELPERS
        // ═══════════════════════════════════════════════════════════

        private void DrawCircle(SpriteBatch b, Vector2 center, float radius, Color color, int segments)
        {
            if (radius <= 0) return;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)i / segments * MathHelper.TwoPi;
                float angle2 = (float)(i + 1) / segments * MathHelper.TwoPi;
                Vector2 p1 = center + new Vector2(MathF.Cos(angle1) * radius, MathF.Sin(angle1) * radius);
                Vector2 p2 = center + new Vector2(MathF.Cos(angle2) * radius, MathF.Sin(angle2) * radius);
                DrawLine(b, p1, p2, color, 2f);
            }
        }

        private void DrawArc(SpriteBatch b, Vector2 center, float radius, float startAngle, float sweep, Color color, float thickness, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a1 = startAngle + (float)i / segments * sweep;
                float a2 = startAngle + (float)(i + 1) / segments * sweep;
                Vector2 p1 = center + new Vector2(MathF.Cos(a1) * radius, MathF.Sin(a1) * radius);
                Vector2 p2 = center + new Vector2(MathF.Cos(a2) * radius, MathF.Sin(a2) * radius);
                DrawLine(b, p1, p2, color, thickness);
            }
        }

        private void DrawLine(SpriteBatch b, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 delta = end - start;
            float angle = MathF.Atan2(delta.Y, delta.X);
            float length = delta.Length();
            Rectangle rect = new Rectangle((int)start.X, (int)start.Y, (int)length, (int)Math.Max(1, thickness));
            b.Draw(Game1.fadeToBlackRect, rect, null, color, angle, Vector2.Zero, SpriteEffects.None, 0);
        }

        // ═══════════════════════════════════════════════════════════
        //  INTERACTION
        // ═══════════════════════════════════════════════════════════

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (_isClosing) return;

            if (_closeButton.Contains(x, y)) { _isClosing = true; Game1.playSound("bigSelect"); return; }
            if (_prevButton.Contains(x, y) && _currentPage > 0) { _currentPage--; Game1.playSound("shwip"); }
            if (_nextButton.Contains(x, y) &&
                (_currentPage + 1) * _entriesPerPage < _filteredEntries.Count)
            { _currentPage++; Game1.playSound("shwip"); }
            if (_filterButton.Contains(x, y)) { CycleFilter(); Game1.playSound("drumkit6"); }
            if (_sortButton.Contains(x, y))
            { _sortMode = _sortMode == "newest" ? "oldest" : "newest"; ApplyFilters(); Game1.playSound("drumkit6"); }
            if (_diaryButton.Contains(x, y) && ModEntry.Config.EnablePlayerDiary)
            { WriteDiaryEntry(); }
            if (_markButton.Contains(x, y))
            { ToggleMarkCurrentPage(); Game1.playSound("drumkit6"); }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        public override void receiveScrollWheelAction(int direction)
        {
            if (direction > 0 && _currentPage > 0) _currentPage--;
            else if (direction < 0 && (_currentPage + 1) * _entriesPerPage < _filteredEntries.Count) _currentPage++;
        }

        public override void performHoverAction(int x, int y) { }

        private void CycleFilter()
        {
            var npcs = _journal.GetKnownNpcs();
            if (npcs.Count == 0) return;
            if (_filterNpc == null) _filterNpc = npcs[0];
            else
            {
                int idx = npcs.IndexOf(_filterNpc);
                _filterNpc = (idx >= 0 && idx < npcs.Count - 1) ? npcs[idx + 1] : null;
            }
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            _filteredEntries = JournalFilter.Filter(_entries,
                npc: _filterNpc, trigger: _filterTrigger,
                sort: _sortMode == "newest" ? JournalFilter.SortMode.Newest : JournalFilter.SortMode.Oldest);
            _currentPage = 0;
            _hasMarkedEntry = _filteredEntries.Any(e => _markedDates.Contains(DateKey(e.Year, e.Season, e.DayOfMonth)));
        }

        private Color GetNpcColor(string npcName) => npcName switch
        {
            "Abigail" => new Color(160, 70, 170), "Alex" => new Color(210, 130, 50),
            "Emily" => new Color(50, 150, 190), "Haley" => new Color(230, 140, 170),
            "Harvey" => new Color(50, 90, 110), "Leah" => new Color(70, 130, 50),
            "Linus" => new Color(90, 150, 70), "Maru" => new Color(190, 50, 90),
            "Penny" => new Color(170, 130, 50), "Robin" => new Color(190, 110, 30),
            "Sam" => new Color(50, 110, 190), "Sebastian" => new Color(35, 35, 55),
            "Shane" => new Color(70, 50, 70), "Elliott" => new Color(170, 130, 70),
            "Caroline" => new Color(90, 150, 110), "Willy" => new Color(50, 90, 90),
            "Wizard" => new Color(90, 50, 170), _ => new Color(90, 70, 50)
        };

        private void ToggleMarkCurrentPage()
        {
            // Mark or unmark the dates on the current page
            int startIdx = _currentPage * _entriesPerPage;
            int endIdx = Math.Min(startIdx + _entriesPerPage, _filteredEntries.Count);

            for (int i = startIdx; i < endIdx; i++)
            {
                var entry = _filteredEntries[i];
                string dateKey = DateKey(entry.Year, entry.Season, entry.DayOfMonth);
                if (_markedDates.Contains(dateKey))
                    _markedDates.Remove(dateKey);
                else
                    _markedDates.Add(dateKey);
            }
            _hasMarkedEntry = _filteredEntries.Any(e => _markedDates.Contains(DateKey(e.Year, e.Season, e.DayOfMonth)));
            SaveMarks();
        }

        public static void LoadMarks()
        {
            _markedDates = ModDataHelper.Load<HashSet<string>>(MarkSaveKey) ?? new HashSet<string>();
        }

        public static void SaveMarks()
        {
            ModDataHelper.Save(MarkSaveKey, new List<string>(_markedDates));
        }

        /// <summary>Check if today is marked and show a reminder</summary>
        /// <summary>Write a context-aware diary entry with one click</summary>
        private void WriteDiaryEntry()
        {
            if (Game1.player == null) return;

            string season = StringHelper.SeasonDisplayName(Game1.currentSeason);
            string timeOfDay = StringHelper.TimeOfDayToPeriod(Game1.timeOfDay);
            string location = Game1.currentLocation?.Name ?? "星露谷";
            string weather = Game1.isRaining ? "雨" : Game1.isSnowing ? "雪" : "晴";

            string title = $"{season} {Game1.dayOfMonth}日 — {timeOfDay}";
            string text = RandomHelper.Next(4) switch
            {
                0 => $"在{location}。今天{weather}。没什么特别的事——但不知道为什么会想记下来。也许是因为风刚刚刚好。",
                1 => $"第{Game1.year}年的{season}。{location}的{weather}天。在这里已经有一阵子了。今天想写点什么，又不知道从哪里写起。那就不写。就这样。",
                2 => $"今天做了很多事。也什么都没做。这两件事不矛盾——做很多事的人，也能什么都没做地待一会儿。{location}是个可以什么都不做的地方。",
                _ => $"{location}。{weather}。{timeOfDay}。有人说过'日记不要写太好'——因为写太好就不像日记了。所以我就写：今天还不错。完了。"
            };

            ModEntry.Diary?.WriteEntry(title, text);
            Game1.playSound("slimeHit");
            ApplyFilters();
            Game1.addHUDMessage(new HUDMessage("写下了今天的日记。回音日志里多了一页属于你的。", HUDMessage.newQuest_type));
        }

        public static void CheckDayMark()
        {
            if (_markedDates.Contains(DateKey(Game1.year, Game1.currentSeason, Game1.dayOfMonth)))
            {
                Game1.addHUDMessage(new HUDMessage("📌 你在日志里标记过这一天。是时候去探望某个人了。", HUDMessage.newQuest_type));
            }
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            CalculateLayout();
        }
    }
}
