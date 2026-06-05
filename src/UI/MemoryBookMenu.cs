using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.NpcProfiles;
using EchoesOfTheHollow.Systems.Journal;
using EchoesOfTheHollow.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace EchoesOfTheHollow.UI
{
    /// <summary>
    /// 记忆之书 — 布面装订的个人记忆册
    /// Cloth-bound memory book replacing the social/hearts tab.
    /// Purely code-drawn: fabric texture, ribbon bookmark, pressed-flower decorations.
    /// </summary>
    public class MemoryBookMenu : IClickableMenu
    {
        private readonly JournalSystem _journal;
        private readonly VoiceRegistry _voices;

        private List<string> _knownNpcs = new();
        private Dictionary<string, List<JournalEntry>> _entriesByNpc = new();
        private Dictionary<string, string> _npcsLatestEmotions = new();
        private int _selectedNpcIndex = -1;
        private string? _selectedNpcName;

        private Rectangle _npcListBounds;
        private Rectangle _entryDisplayBounds;
        private Rectangle _bookFrameBounds;
        private Rectangle _closeButton;
        private int _scrollOffset;
        private Rectangle _scrollUpButton;
        private Rectangle _scrollDownButton;
        private Rectangle _viewJournalLink;

        private const int NpcPerPage = 8;
        private float _time;
        private int _seed;

        // Ribbon bookmark
        private float _ribbonWave;
        private float _ribbonTargetY;
        private float _ribbonCurrentY;

        public MemoryBookMenu(JournalSystem journal, VoiceRegistry voices)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height)
        {
            _journal = journal;
            _voices = voices;
            _seed = (int)Game1.stats.DaysPlayed + Game1.year * 77;

            RefreshData();
            CalculateLayout();

            _ribbonCurrentY = _bookFrameBounds.Y;
            _ribbonTargetY = _bookFrameBounds.Y;
        }

        private void RefreshData()
        {
            _knownNpcs = _journal.GetKnownNpcs().Where(n => n != "我").ToList();
            _entriesByNpc = new Dictionary<string, List<JournalEntry>>();
            _npcsLatestEmotions = _journal.GetNpcLatestEmotions();

            foreach (string npc in _knownNpcs)
                _entriesByNpc[npc] = _journal.GetRecentByNpc(npc, 3);
        }

        private void CalculateLayout()
        {
            int leftW = 210;
            int rightW = 510;
            int bookH = 520;
            int bookX = (width - leftW - rightW) / 2;
            int bookY = (height - bookH) / 2;

            _bookFrameBounds = new Rectangle(bookX - 10, bookY - 10, leftW + rightW + 20, bookH + 20);
            _npcListBounds = new Rectangle(bookX, bookY, leftW, bookH);
            _entryDisplayBounds = new Rectangle(bookX + leftW + 15, bookY, rightW, bookH);
            _closeButton = new Rectangle(_bookFrameBounds.Right - 26, _bookFrameBounds.Y - 6, 32, 32);

            _scrollUpButton = new Rectangle(bookX + leftW - 24, bookY + 10, 20, 20);
            _scrollDownButton = new Rectangle(bookX + leftW - 24, bookY + bookH - 30, 20, 20);

            _viewJournalLink = new Rectangle(_entryDisplayBounds.X + 20, _entryDisplayBounds.Bottom - 35, 200, 25);
        }

        // ═══════════════════════════════════════════════════════════
        //  MAIN DRAW
        // ═══════════════════════════════════════════════════════════

        public override void draw(SpriteBatch b)
        {
            _time += 0.016f;
            _ribbonCurrentY += (_ribbonTargetY - _ribbonCurrentY) * 0.08f;

            // Dim background
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, width, height), Color.Black * 0.6f);

            // Book shadow
            DrawDropShadow(b, _bookFrameBounds, 6);

            // ── Cloth cover ──
            DrawClothCover(b);

            // ── Title on cover ──
            DrawTitle(b);

            // ── Ribbon bookmark ──
            DrawRibbonBookmark(b);

            // ── NPC list panel ──
            DrawNpcList(b);

            // ── Entry display ──
            DrawPaperPanel(b);
            if (_selectedNpcName != null)
                DrawNpcEntries(b);
            else
                DrawEmptyState(b);

            // ── Close button ──
            DrawCloseButton(b);

            drawMouse(b);
        }

        // ═══════════════════════════════════════════════════════════
        //  CLOTH COVER
        // ═══════════════════════════════════════════════════════════

        private void DrawClothCover(SpriteBatch b)
        {
            var r = _bookFrameBounds;
            Color clothBase = new Color(65, 80, 95);       // Deep blue-grey cloth
            Color clothHighlight = new Color(80, 95, 110);
            Color clothShadow = new Color(45, 55, 68);

            // Base
            b.Draw(Game1.fadeToBlackRect, r, clothBase);

            // Fabric weave — fine horizontal + vertical striping
            var rng = new Random(_seed);
            for (int y = r.Y + 4; y < r.Bottom - 4; y += 4)
            {
                float a = 0.015f + (float)rng.NextDouble() * 0.025f;
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(r.X + 4, y, r.Width - 8, 2),
                    clothHighlight * a);
            }
            for (int x = r.X + 5; x < r.Right - 5; x += 5)
            {
                float a = 0.01f + (float)rng.NextDouble() * 0.015f;
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(x, r.Y + 4, 2, r.Height - 8),
                    clothHighlight * a);
            }

            // Edge banding (leather corners on cloth cover)
            int bandW = 8;
            Color leatherCorner = new Color(100, 70, 40);
            // Top band
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X, r.Y, r.Width, bandW), leatherCorner * 0.6f);
            // Bottom band
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X, r.Bottom - bandW, r.Width, bandW), leatherCorner * 0.6f);

            // Corner leather patches
            int patchSize = 30;
            DrawLeatherCorner(b, new Rectangle(r.X, r.Y, patchSize, patchSize));
            DrawLeatherCorner(b, new Rectangle(r.Right - patchSize, r.Y, patchSize, patchSize));
            DrawLeatherCorner(b, new Rectangle(r.X, r.Bottom - patchSize, patchSize, patchSize));
            DrawLeatherCorner(b, new Rectangle(r.Right - patchSize, r.Bottom - patchSize, patchSize, patchSize));
        }

        private void DrawLeatherCorner(SpriteBatch b, Rectangle rect)
        {
            b.Draw(Game1.fadeToBlackRect, rect, new Color(110, 75, 45));
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Y, rect.Width, 1),
                new Color(160, 120, 70) * 0.3f);
        }

        private void DrawRibbonBookmark(SpriteBatch b)
        {
            // Silk ribbon flowing from the top of the book
            int ribbonX = _bookFrameBounds.Center.X + 20;
            int ribbonTop = _bookFrameBounds.Y - 8;
            int ribbonWidth = 14;

            _ribbonWave = MathF.Sin(_time * 1.5f) * 6f;

            // Ribbon tail
            Color ribbonColor = new Color(180, 85, 70); // Muted red silk
            Color ribbonDark = new Color(140, 55, 45);

            // Vertical rectangle with slight wave
            int tailLength = 45;
            for (int seg = 0; seg < tailLength; seg += 2)
            {
                float waveOffset = MathF.Sin((seg + _ribbonCurrentY) * 0.1f + _time) * 3f;
                int rx = ribbonX + (int)waveOffset;
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(rx, ribbonTop + seg, ribbonWidth, 2),
                    seg % 6 < 3 ? ribbonColor : ribbonDark);
            }

            // Ribbon top (tucked into pages)
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle(ribbonX, ribbonTop, ribbonWidth, 12), ribbonColor);
        }

        // ═══════════════════════════════════════════════════════════
        //  TITLE
        // ═══════════════════════════════════════════════════════════

        private void DrawTitle(SpriteBatch b)
        {
            string title = "记 忆 之 书";
            Vector2 ts = Game1.dialogueFont.MeasureString(title);
            float tx = _bookFrameBounds.Center.X - ts.X / 2;
            float ty = _bookFrameBounds.Y - 48;

            // Title plate (metal nameplate look)
            var plate = new Rectangle((int)tx - 14, (int)ty - 4, (int)ts.X + 28, (int)ts.Y + 14);
            b.Draw(Game1.fadeToBlackRect, plate, new Color(140, 120, 85));
            b.Draw(Game1.fadeToBlackRect, new Rectangle(plate.X, plate.Y, plate.Width, 2), new Color(200, 170, 120));
            b.Draw(Game1.fadeToBlackRect, new Rectangle(plate.X, plate.Bottom - 2, plate.Width, 2), new Color(200, 170, 120));

            b.DrawString(Game1.dialogueFont, title, new Vector2(tx + 1, ty + 6), Color.Black * 0.35f);
            b.DrawString(Game1.dialogueFont, title, new Vector2(tx, ty + 5), new Color(200, 180, 140));
        }

        // ═══════════════════════════════════════════════════════════
        //  NPC LIST
        // ═══════════════════════════════════════════════════════════

        private void DrawNpcList(SpriteBatch b)
        {
            var r = _npcListBounds;

            // Dark panel with inner glow
            b.Draw(Game1.fadeToBlackRect, r, new Color(38, 42, 52));
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, 1),
                new Color(80, 90, 110) * 0.3f);

            // "NPCs" label
            string label = "—— 村民 ——";
            Vector2 ls = Game1.tinyFont.MeasureString(label);
            b.DrawString(Game1.tinyFont, label,
                new Vector2(r.Center.X - ls.X / 2, r.Y + 6),
                new Color(140, 150, 170));

            // Scroll buttons
            if (_knownNpcs.Count > NpcPerPage)
            {
                DrawTinyButton(b, _scrollUpButton, "▲", _scrollOffset > 0);
                DrawTinyButton(b, _scrollDownButton, "▼", _scrollOffset + NpcPerPage < _knownNpcs.Count);
            }

            if (_knownNpcs.Count == 0)
            {
                string empty = "还没有\n记忆...";
                Vector2 es = Game1.smallFont.MeasureString(empty);
                b.DrawString(Game1.smallFont, empty,
                    new Vector2(r.Center.X - es.X / 2, r.Center.Y - es.Y / 2),
                    new Color(100, 110, 130));
                return;
            }

            int startY = r.Y + 26;
            for (int i = _scrollOffset; i < Math.Min(_knownNpcs.Count, _scrollOffset + NpcPerPage); i++)
            {
                string npc = _knownNpcs[i];
                int itemY = startY + (i - _scrollOffset) * 56;
                var itemRect = new Rectangle(r.X + 3, itemY, r.Width - 6, 50);

                bool selected = _selectedNpcName == npc;
                Color npcColor = GetNpcColor(npc);

                // Selection highlight
                if (selected)
                    b.Draw(Game1.fadeToBlackRect, itemRect, new Color(70, 80, 100, 180));

                // NPC color dot
                b.Draw(Game1.fadeToBlackRect, new Rectangle(itemRect.X + 6, itemRect.Y + 10, 5, 5), npcColor);

                // NPC name
                b.DrawString(Game1.smallFont, npc,
                    new Vector2(itemRect.X + 16, itemRect.Y + 6), npcColor, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);

                // Latest emotion
                if (_npcsLatestEmotions.TryGetValue(npc, out string emotion))
                {
                    string emoji = EmotionToSymbol(emotion);
                    b.DrawString(Game1.tinyFont, emoji,
                        new Vector2(itemRect.X + 16, itemRect.Y + 24), Color.White * 0.6f, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
                }

                // Entry count
                int count = _entriesByNpc.ContainsKey(npc) ? _entriesByNpc[npc].Count : 0;
                string countStr = $"{count}条";
                Vector2 cs = Game1.tinyFont.MeasureString(countStr);
                b.DrawString(Game1.tinyFont, countStr,
                    new Vector2(itemRect.Right - cs.X - 8, itemRect.Y + 6),
                    new Color(120, 130, 150), 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

                // Separator
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(itemRect.X + 8, itemRect.Bottom, itemRect.Width - 16, 1),
                    new Color(50, 58, 70, 100));
            }
        }

        private void DrawTinyButton(SpriteBatch b, Rectangle rect, string text, bool active)
        {
            Color c = active ? new Color(120, 130, 150) : new Color(60, 65, 80);
            b.Draw(Game1.fadeToBlackRect, rect, c * 0.5f);
            Vector2 s = Game1.tinyFont.MeasureString(text);
            b.DrawString(Game1.tinyFont, text,
                new Vector2(rect.Center.X - s.X / 2, rect.Center.Y - s.Y / 2), c);
        }

        // ═══════════════════════════════════════════════════════════
        //  ENTRY DISPLAY
        // ═══════════════════════════════════════════════════════════

        private void DrawPaperPanel(SpriteBatch b)
        {
            var r = _entryDisplayBounds;

            // Aged paper (warm cream)
            b.Draw(Game1.fadeToBlackRect, r, new Color(250, 243, 228));

            // Paper grain
            var rng = new Random(_seed + 500);
            for (int i = 0; i < 50; i++)
            {
                int gx = r.X + rng.Next(r.Width);
                int gy = r.Y + rng.Next(r.Height);
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(gx, gy, 2 + rng.Next(6), 1),
                    new Color(200, 180, 150) * (0.005f + (float)rng.NextDouble() * 0.015f));
            }

            // Inner border (pressed line)
            int pad = 8;
            Color line = new Color(180, 160, 130, 80);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X + pad, r.Y + pad, r.Width - pad * 2, 1), line);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X + pad, r.Bottom - pad, r.Width - pad * 2, 1), line);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X + pad, r.Y + pad, 1, r.Height - pad * 2), line);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.Right - pad, r.Y + pad, 1, r.Height - pad * 2), line);

            // Pressed flower (decorative)
            DrawPressedFlower(b,
                new Vector2(r.Right - 45, r.Y + 50),
                new Color(180, 130, 150, 40), 14f);
        }

        private void DrawPressedFlower(SpriteBatch b, Vector2 center, Color color, float size)
        {
            // Simple 5-petal flower
            for (int p = 0; p < 5; p++)
            {
                float angle = p * MathHelper.TwoPi / 5 - MathHelper.PiOver2;
                Vector2 petalCenter = center + new Vector2(MathF.Cos(angle) * size * 0.5f, MathF.Sin(angle) * size * 0.5f);
                DrawCircleFill(b, petalCenter, size * 0.35f, color, 8);
            }
            // Center
            DrawCircleFill(b, center, size * 0.2f, new Color(200, 170, 100, 60), 6);
            // Stem
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle((int)center.X - 1, (int)(center.Y + size * 0.3f), 2, (int)(size * 0.6f)),
                new Color(120, 150, 100, 40));
        }

        private void DrawNpcEntries(SpriteBatch b)
        {
            int x = _entryDisplayBounds.X + 20;
            int y = _entryDisplayBounds.Y + 16;
            int maxW = _entryDisplayBounds.Width - 40;
            int maxH = _entryDisplayBounds.Height - 20;

            // NPC name header with ornate styling
            Color npcColor = GetNpcColor(_selectedNpcName!);
            string header = _selectedNpcName!;
            float headerScale = 1.1f;

            // Decorative line left of name
            int nameY = y;
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle(x, nameY + 10, 30, 1), npcColor * 0.5f);
            b.DrawString(Game1.smallFont, header,
                new Vector2(x + 36, nameY + 3), npcColor, 0f, Vector2.Zero, headerScale, SpriteEffects.None, 0f);

            // Decorative line right of name
            float nameEnd = x + 36 + Game1.smallFont.MeasureString(header).X * headerScale;
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle((int)nameEnd + 6, nameY + 10, (int)(maxW - (nameEnd - x) - 6), 1),
                npcColor * 0.3f);

            y += 28;

            // Voice profile snippet
            var voice = _voices.GetProfile(_selectedNpcName!);
            if (voice != null && !string.IsNullOrEmpty(voice.PersonalityDescription))
            {
                string desc = $"\"{voice.PersonalityDescription}\"";
                Vector2 ds = Game1.tinyFont.MeasureString(desc);
                b.DrawString(Game1.tinyFont, desc,
                    new Vector2(x + 10, y), new Color(130, 110, 90), 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
                y += 22;
            }

            y += 8;

            // Entries
            if (!_entriesByNpc.ContainsKey(_selectedNpcName!) || _entriesByNpc[_selectedNpcName!].Count == 0)
            {
                string emptyMsg = "还没有写下关于你的记忆。\n去和他们说说话，或者只是在他们面前走过——\n他们会注意到的。";
                b.DrawString(Game1.smallFont, emptyMsg,
                    new Vector2(x + 10, y + 20), new Color(110, 90, 70), 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
                return;
            }

            var entries = _entriesByNpc[_selectedNpcName!];
            foreach (var entry in entries.Take(3))
            {
                if (y + 40 > _entryDisplayBounds.Bottom - 40) break;

                // Left accent bar
                Color accentColor = entry.EmotionTag switch
                {
                    "Warm" => new Color(220, 170, 80, 120),
                    "Melancholy" => new Color(140, 160, 190, 120),
                    "Curiosity" => new Color(160, 180, 100, 120),
                    "Longing" => new Color(180, 140, 200, 120),
                    "Wonder" => new Color(120, 190, 200, 120),
                    "Nostalgia" => new Color(190, 150, 130, 120),
                    "Reflection" => new Color(150, 170, 180, 120),
                    _ => new Color(150, 140, 120, 80)
                };
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(x - 2, y, 3, 36), accentColor);

                // Date
                b.DrawString(Game1.tinyFont, entry.DisplayDate,
                    new Vector2(x + 8, y), new Color(140, 110, 80));

                y += 16;

                // Entry text
                var lines = StringHelper.WrapText(entry.DisplayText, 46);
                foreach (string line in lines.Take(3))
                {
                    if (y + 16 < _entryDisplayBounds.Bottom - 40)
                    {
                        b.DrawString(Game1.smallFont, line,
                            new Vector2(x + 8, y),
                            new Color(55, 38, 22), 0f, Vector2.Zero, 0.82f, SpriteEffects.None, 0f);
                        y += 18;
                    }
                }

                // Divider
                y += 4;
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(x + 30, y, maxW - 60, 1),
                    new Color(190, 170, 150, 60));
                y += 8;
            }

            // "View full journal" link
            if (y < _entryDisplayBounds.Bottom - 50)
            {
                _viewJournalLink = new Rectangle(x + 10, _entryDisplayBounds.Bottom - 35, 200, 25);
                bool hover = _viewJournalLink.Contains(Game1.getMousePosition());
                string link = "→ 在回音日志中查看全部";
                b.DrawString(Game1.tinyFont, link,
                    new Vector2(_viewJournalLink.X, _viewJournalLink.Y),
                    hover ? new Color(140, 160, 200) : new Color(90, 120, 160));
            }
        }

        private void DrawEmptyState(SpriteBatch b)
        {
            int cx = _entryDisplayBounds.Center.X;
            int cy = _entryDisplayBounds.Center.Y;

            string line1 = "选择一位村民";
            string line2 = "阅读他们对你的记忆";
            string line3 = "每个人都有自己独特的声音";

            Vector2 s1 = Game1.smallFont.MeasureString(line1);
            Vector2 s2 = Game1.smallFont.MeasureString(line2);
            Vector2 s3 = Game1.tinyFont.MeasureString(line3);

            b.DrawString(Game1.smallFont, line1,
                new Vector2(cx - s1.X / 2, cy - 40), new Color(130, 110, 90));
            b.DrawString(Game1.smallFont, line2,
                new Vector2(cx - s2.X / 2, cy - 12), new Color(130, 110, 90));
            b.DrawString(Game1.tinyFont, line3,
                new Vector2(cx - s3.X / 2, cy + 20), new Color(170, 150, 130));

            // Small decorative flower
            DrawPressedFlower(b,
                new Vector2(cx, cy - 80),
                new Color(160, 140, 180, 35), 16f);
        }

        // ═══════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════

        private void DrawDropShadow(SpriteBatch b, Rectangle rect, int size)
        {
            for (int i = size; i >= 0; i -= 2)
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(rect.X + i, rect.Y + i, rect.Width, rect.Height),
                    Color.Black * (0.12f * (1f - (float)i / size)));
        }

        private void DrawCloseButton(SpriteBatch b)
        {
            bool hover = _closeButton.Contains(Game1.getMousePosition());
            Color bg = hover ? new Color(170, 60, 45) : new Color(110, 40, 30);
            b.Draw(Game1.fadeToBlackRect, _closeButton, bg);
            Vector2 xs = Game1.smallFont.MeasureString("✕");
            b.DrawString(Game1.smallFont, "✕",
                new Vector2(_closeButton.Center.X - xs.X / 2, _closeButton.Center.Y - xs.Y / 2 + 1),
                Color.White * 0.9f);
        }

        private void DrawCircleFill(SpriteBatch b, Vector2 center, float radius, Color color, int segments)
        {
            if (radius <= 0) return;
            for (int i = 0; i < segments; i++)
            {
                float a1 = (float)i / segments * MathHelper.TwoPi;
                float a2 = (float)(i + 1) / segments * MathHelper.TwoPi;
                Vector2 p1 = center + new Vector2(MathF.Cos(a1) * radius, MathF.Sin(a1) * radius);
                Vector2 p2 = center + new Vector2(MathF.Cos(a2) * radius, MathF.Sin(a2) * radius);
                Vector2 pMid = center;
                // Draw triangle from center to two edge points
                float minX = Math.Min(Math.Min(center.X, p1.X), p2.X);
                float minY = Math.Min(Math.Min(center.Y, p1.Y), p2.Y);
                float maxX = Math.Max(Math.Max(center.X, p1.X), p2.X);
                float maxY = Math.Max(Math.Max(center.Y, p1.Y), p2.Y);
                // Simplified: just draw thin pie slices
                Vector2 delta = p2 - p1;
                float angle = MathF.Atan2(delta.Y, delta.X);
                float len = delta.Length();
                if (len > 0 && len < radius * 3)
                    b.Draw(Game1.fadeToBlackRect,
                        new Rectangle((int)minX, (int)minY, Math.Max(1, (int)(maxX - minX)), Math.Max(1, (int)(maxY - minY))),
                        color);
            }
        }

        private static string EmotionToSymbol(string emotion) => emotion switch
        {
            "Warm" => "✦ 温暖", "Melancholy" => "☁ 惆怅", "Curiosity" => "◈ 好奇",
            "Humor" => "♧ 幽默", "Longing" => "☾ 牵挂", "Wonder" => "✧ 惊叹",
            "Nostalgia" => "❦ 怀旧", "Reflection" => "♢ 思索", "Neutral" => "·",
            _ => "·"
        };

        private Color GetNpcColor(string name) => name switch
        {
            "Abigail" => new Color(155, 65, 170), "Alex" => new Color(210, 125, 45),
            "Caroline" => new Color(85, 150, 105), "Clint" => new Color(95, 75, 55),
            "Demetrius" => new Color(50, 90, 130), "Elliott" => new Color(170, 125, 65),
            "Emily" => new Color(45, 145, 185), "Evelyn" => new Color(175, 130, 150),
            "George" => new Color(75, 90, 75), "Gus" => new Color(190, 130, 50),
            "Haley" => new Color(225, 140, 170), "Harvey" => new Color(45, 85, 110),
            "Jas" => new Color(190, 130, 170), "Jodi" => new Color(90, 110, 170),
            "Kent" => new Color(50, 70, 90), "Leah" => new Color(65, 125, 45),
            "Lewis" => new Color(110, 90, 130), "Linus" => new Color(85, 145, 65),
            "Marnie" => new Color(170, 110, 70), "Maru" => new Color(185, 45, 85),
            "Pam" => new Color(150, 90, 50), "Penny" => new Color(165, 125, 45),
            "Pierre" => new Color(70, 90, 130), "Robin" => new Color(185, 105, 25),
            "Sam" => new Color(45, 105, 185), "Sebastian" => new Color(30, 30, 50),
            "Shane" => new Color(65, 45, 65), "Vincent" => new Color(210, 170, 90),
            "Willy" => new Color(45, 85, 85), "Wizard" => new Color(85, 45, 165),
            _ => new Color(85, 65, 45)
        };

        // ═══════════════════════════════════════════════════════════
        //  INTERACTION
        // ═══════════════════════════════════════════════════════════

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (_closeButton.Contains(x, y)) { exitThisMenu(); Game1.playSound("bigSelect"); return; }

            if (_scrollUpButton.Contains(x, y) && _scrollOffset > 0)
            { _scrollOffset--; Game1.playSound("shwip"); return; }

            if (_scrollDownButton.Contains(x, y) && _scrollOffset + NpcPerPage < _knownNpcs.Count)
            { _scrollOffset++; Game1.playSound("shwip"); return; }

            // NPC selection
            int startY = _npcListBounds.Y + 26;
            for (int i = _scrollOffset; i < Math.Min(_knownNpcs.Count, _scrollOffset + NpcPerPage); i++)
            {
                int itemY = startY + (i - _scrollOffset) * 56;
                var rect = new Rectangle(_npcListBounds.X + 3, itemY, _npcListBounds.Width - 6, 50);
                if (rect.Contains(x, y))
                {
                    _selectedNpcIndex = i;
                    _selectedNpcName = _knownNpcs[i];
                    _ribbonTargetY = itemY;
                    Game1.playSound("shwip");
                    return;
                }
            }

            // "View full journal" link
            if (_viewJournalLink.Contains(x, y) && _selectedNpcName != null)
            {
                ModEntry.OpenJournalMenu();
                Game1.playSound("drumkit6");
            }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        public override void receiveScrollWheelAction(int direction)
        {
            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset - direction,
                Math.Max(0, _knownNpcs.Count - NpcPerPage)));
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            CalculateLayout();
        }
    }
}
