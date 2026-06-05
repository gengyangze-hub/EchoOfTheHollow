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
    /// 回音日志主界面 -- 手订皮质日志本
    /// Hand-bound leather journal with aged paper, ink stains, ribbon bookmark,
    /// brass corner protectors, pressed flowers, and visible page edges.
    /// All visual effects are purely code-drawn -- no external textures needed.
    /// </summary>
    public class EchoJournalMenu : IClickableMenu
    {
        private readonly JournalSystem _journal;
        private readonly VoiceRegistry _voices;
        private readonly IModHelper _helper;

        private List<JournalEntry> _entries;
        private List<JournalEntry> _filteredEntries;
        private int _currentPage;
        private int _entriesPerPage = 2;

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
        private static HashSet<string> _markedDates = new();
        private const string MarkSaveKey = "MemoryMarks/v1";
        private bool _hasMarkedEntry;

        private static string DateKey(int year, string season, int day) => $"{year}-{season}-{day}";

        // Decorative elements (pseudo-random, stable per book instance)
        private readonly Vector2[] _inkStains = new Vector2[5];
        private readonly float[] _inkSizes = new float[5];
        private readonly Vector2[] _foxingSpots = new Vector2[12];
        private readonly float[] _foxingSizes = new float[12];
        private readonly Vector2 _pressedFlowerPos;
        private readonly int _pressedFlowerType;

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

            var rng = new Random(_seed);
            for (int i = 0; i < _inkStains.Length; i++)
            {
                _inkStains[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                _inkSizes[i] = 3f + (float)rng.NextDouble() * 14f;
            }
            for (int i = 0; i < _foxingSpots.Length; i++)
            {
                _foxingSpots[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                _foxingSizes[i] = 0.8f + (float)rng.NextDouble() * 2.5f;
            }
            _pressedFlowerPos = new Vector2(0.55f + (float)rng.NextDouble() * 0.3f, 0.65f + (float)rng.NextDouble() * 0.25f);
            _pressedFlowerType = rng.Next(4);

            CalculateLayout();
        }

        private void CalculateLayout()
        {
            int centerX = width / 2;
            int bookWidth = 780;
            int pageWidth = bookWidth / 2 - 24;
            int bookHeight = Math.Min(560, height - 120);
            int startY = (height - bookHeight) / 2;

            _bookFrameBounds = new Rectangle(centerX - bookWidth / 2 - 30, startY - 30,
                bookWidth + 60, bookHeight + 60);

            _leftPageBounds = new Rectangle(centerX - bookWidth / 2 + 4, startY + 4, pageWidth, bookHeight - 8);
            _rightPageBounds = new Rectangle(centerX + 20, startY + 4, pageWidth, bookHeight - 8);

            // Navigation buttons below the book
            int btnY = _bookFrameBounds.Bottom + 12;
            _prevButton = new Rectangle(_leftPageBounds.Left + 10, btnY, 130, 38);
            _nextButton = new Rectangle(_rightPageBounds.Right - 140, btnY, 130, 38);

            // Side panel -- a vertical strip to the left of the book
            int sideX = _leftPageBounds.Left - 150;
            _filterButton = new Rectangle(sideX, startY + 10, 130, 34);
            _sortButton = new Rectangle(sideX, startY + 50, 130, 34);
            _diaryButton = new Rectangle(sideX, startY + 90, 130, 34);
            _markButton = new Rectangle(sideX, startY + 130, 130, 34);

            // Close button top-right of book
            _closeButton = new Rectangle(_bookFrameBounds.Right - 6, startY - 14, 32, 32);
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

            // ── Warm dark background with subtle radial light ──
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, width, height), new Color(18, 14, 10) * 0.88f);
            DrawAmbientGlow(b);

            // ── Book drop shadow (multi-layer soft shadow) ──
            DrawSoftShadow(b, _bookFrameBounds, 16);

            // ── Visible page edges (side of the book block) ──
            DrawPageEdges(b);

            // ── Leather cover ──
            DrawLeatherCover(b);

            // ── Brass corner protectors ──
            DrawCornerBrass(b, _bookFrameBounds);

            // ── Stitching around cover ──
            DrawStitching(b, _bookFrameBounds, 14);

            // ── Book spine (center, with title text) ──
            DrawSpine(b);

            // ── Ribbon bookmark hanging from bottom ──
            DrawRibbonBookmark(b);

            // ── Pages (aged paper + subtle background decorations) ──
            DrawAgedPage(b, _leftPageBounds, isLeft: true);
            DrawAgedPage(b, _rightPageBounds, isLeft: false);

            // ── Subtle background: ruled lines ──
            DrawRuledLines(b, _leftPageBounds);
            DrawRuledLines(b, _rightPageBounds);

            // ── Very subtle background: pressed flower ──
            DrawPressedFlower(b, _leftPageBounds);

            // ── Very subtle background: foxing spots ──
            DrawFoxingSpots(b);

            // ── Very subtle background: ink stains ──
            DrawInkStains(b);

            // ── TEXT (drawn LAST -- always on top of decorations) ──
            DrawCurrentEntries(b);

            // ── Page corner flourishes ──
            DrawCornerFlourish(b, _leftPageBounds, mirror: true);
            DrawCornerFlourish(b, _rightPageBounds, mirror: false);

            // ── Header ──
            DrawHeader(b);

            // ── Buttons ──
            DrawButtons(b);

            drawMouse(b);
        }

        // ═══════════════════════════════════════════════════════════
        //  AMBIENT & SHADOW
        // ═══════════════════════════════════════════════════════════

        private void DrawAmbientGlow(SpriteBatch b)
        {
            int cx = _bookFrameBounds.Center.X;
            int cy = _bookFrameBounds.Center.Y;
            for (int ring = 0; ring < 6; ring++)
            {
                float radius = 180f + ring * 80f;
                float alpha = 0.04f - ring * 0.006f;
                int r = (int)radius;
                var glowRect = new Rectangle(cx - r, cy - r, r * 2, r * 2);
                b.Draw(Game1.fadeToBlackRect, glowRect, new Color(200, 160, 100) * alpha);
            }
        }

        private void DrawSoftShadow(SpriteBatch b, Rectangle rect, int depth)
        {
            for (int i = depth; i >= 0; i -= 2)
            {
                float alpha = 0.12f * (1f - (float)i / depth);
                float offsetX = i * 0.6f;
                float offsetY = i * 0.8f;
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle((int)(rect.X + offsetX), (int)(rect.Y + offsetY), rect.Width, rect.Height),
                    Color.Black * alpha);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  BOOK FRAME -- LEATHER COVER
        // ═══════════════════════════════════════════════════════════

        private void DrawPageEdges(SpriteBatch b)
        {
            // Visible page block edges on all four sides -- stacked paper look
            var r = _bookFrameBounds;
            Color pageEdge = new Color(225, 210, 175);
            int layers = 5;
            int inset = 6;

            for (int i = 0; i < layers; i++)
            {
                int off = inset + i;
                var edge = new Rectangle(r.X + off, r.Y + off, r.Width - off * 2, r.Height - off * 2);
                // Bottom edge (thickest -- pages)
                b.Draw(Game1.fadeToBlackRect, new Rectangle(edge.X, edge.Bottom - 2, edge.Width, 2), pageEdge * (0.15f - i * 0.02f));
                // Right edge
                b.Draw(Game1.fadeToBlackRect, new Rectangle(edge.Right - 2, edge.Y, 2, edge.Height), pageEdge * (0.12f - i * 0.02f));
                // Top edge
                b.Draw(Game1.fadeToBlackRect, new Rectangle(edge.X, edge.Y, edge.Width, 1), pageEdge * (0.10f - i * 0.02f));
            }
        }

        private void DrawLeatherCover(SpriteBatch b)
        {
            var r = _bookFrameBounds;

            // ── Base leather with subtle color variation ──
            Color leatherBase = new Color(115, 78, 42);
            Color leatherLight = new Color(155, 108, 58);
            Color leatherDark = new Color(72, 45, 22);
            Color leatherEdge = new Color(170, 130, 80);

            b.Draw(Game1.fadeToBlackRect, r, leatherBase);

            // ── Gradient overlay -- lighter toward center ──
            int gradientBands = 8;
            for (int i = 0; i < gradientBands; i++)
            {
                float t = (float)i / gradientBands;
                float alpha = 0.04f * (1f - Math.Abs(t - 0.5f) * 2f);
                int bandW = r.Width / gradientBands;
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(r.X + i * bandW, r.Y, bandW, r.Height),
                    leatherLight * alpha);
            }

            // ── Leather grain -- varied horizontal streaks ──
            var grainRng = new Random(_seed);
            for (int i = 0; i < r.Height; i += 2)
            {
                float alpha = 0.015f + (float)grainRng.NextDouble() * 0.05f;
                int grainW = r.Width - 12;
                int grainX = r.X + 6 + grainRng.Next(4);
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(grainX, r.Y + i, grainW, 1),
                    i % 6 == 0 ? leatherLight * alpha : Color.Black * alpha);
                // Occasional wider grain stripe
                if (i % 23 == 0)
                    b.Draw(Game1.fadeToBlackRect,
                        new Rectangle(grainX, r.Y + i, grainW, 2), Color.Black * (alpha * 1.5f));
            }

            // ── Edge burnishing -- lighter worn edges ──
            int burnish = 5;
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X, r.Y, r.Width, burnish), leatherEdge * 0.25f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X, r.Bottom - burnish, r.Width, burnish), leatherEdge * 0.25f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X, r.Y, burnish, r.Height), leatherEdge * 0.2f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.Right - burnish, r.Y, burnish, r.Height), leatherEdge * 0.2f);

            // ── Inner bevel recess -- where the pages sit ──
            int bevel = 10;
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X + bevel, r.Y + bevel, r.Width - bevel * 2, r.Height - bevel * 2), leatherDark * 0.35f);
            // Inner shadow line
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X + bevel, r.Y + bevel, r.Width - bevel * 2, 1), Color.Black * 0.2f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X + bevel, r.Bottom - bevel, r.Width - bevel * 2, 1), leatherLight * 0.2f);

            // ── Dark decorative bands at top and bottom ──
            int bandH = 16;
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X, r.Y + 6, r.Width, bandH), leatherDark * 0.45f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X, r.Bottom - 6 - bandH, r.Width, bandH), leatherDark * 0.45f);
            // Fine border lines on bands
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X + 10, r.Y + 6, r.Width - 20, 1), leatherEdge * 0.3f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X + 10, r.Y + 6 + bandH, r.Width - 20, 1), leatherEdge * 0.3f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X + 10, r.Bottom - 6 - bandH, r.Width - 20, 1), leatherEdge * 0.3f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(r.X + 10, r.Bottom - 6, r.Width - 20, 1), leatherEdge * 0.3f);
        }

        private void DrawCornerBrass(SpriteBatch b, Rectangle r)
        {
            // Brass corner protectors -- small metal triangles on each corner
            Color brass = new Color(190, 155, 80);
            Color brassDark = new Color(120, 90, 40);
            Color brassHighlight = new Color(230, 200, 130);
            int size = 18;
            int off = 4;

            DrawBrassCorner(b, r.X + off, r.Y + off, size, brass, brassDark, brassHighlight);
            DrawBrassCorner(b, r.Right - off - size, r.Y + off, size, brass, brassDark, brassHighlight, flipX: true);
            DrawBrassCorner(b, r.X + off, r.Bottom - off - size, size, brass, brassDark, brassHighlight, flipY: true);
            DrawBrassCorner(b, r.Right - off - size, r.Bottom - off - size, size, brass, brassDark, brassHighlight, flipX: true, flipY: true);
        }

        private void DrawBrassCorner(SpriteBatch b, int x, int y, int size, Color brass, Color dark, Color highlight, bool flipX = false, bool flipY = false)
        {
            int fx = flipX ? x + size : x;
            int fy = flipY ? y + size : y;
            int dx = flipX ? -1 : 1;
            int dy = flipY ? -1 : 1;

            // Triangle fill
            for (int i = 0; i < size; i++)
            {
                int w = size - i;
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(fx, fy + i * dy, w * dx, 1),
                    i == 0 ? highlight * 0.6f : (i < 3 ? brass : dark * (0.5f + i * 0.05f)));
            }
            // Rivet dot
            DrawCircle(b, new Vector2(fx + dx * (size / 2), fy + dy * (size / 2)), 2.5f, brass * 1.3f, 8);
        }

        private void DrawStitching(SpriteBatch b, Rectangle frame, int inset)
        {
            Color thread = new Color(185, 155, 115);
            Color threadDark = new Color(140, 110, 75);
            int dashLen = 7;
            int gapLen = 5;
            int cycle = dashLen + gapLen;

            // Double-thread effect: dark thread behind, light thread offset
            void DrawEdgeStitch(int x1, int y1, int x2, int y2, bool horizontal)
            {
                for (int p = horizontal ? x1 : y1; p < (horizontal ? x2 : y2); p += cycle)
                {
                    int remaining = (horizontal ? x2 : y2) - p;
                    int drawLen = Math.Min(dashLen, remaining);
                    var rect = horizontal
                        ? new Rectangle(p, y1, drawLen, 2)
                        : new Rectangle(x1, p, 2, drawLen);

                    b.Draw(Game1.fadeToBlackRect, rect, threadDark);
                    b.Draw(Game1.fadeToBlackRect,
                        new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 1, 1), thread);
                }
            }

            DrawEdgeStitch(frame.X + inset, frame.Y + inset, frame.Right - inset, frame.Y + inset, true);
            DrawEdgeStitch(frame.X + inset, frame.Bottom - inset, frame.Right - inset, frame.Bottom - inset, true);
            DrawEdgeStitch(frame.X + inset, frame.Y + inset, frame.X + inset, frame.Bottom - inset, false);
            DrawEdgeStitch(frame.Right - inset, frame.Y + inset, frame.Right - inset, frame.Bottom - inset, false);
        }

        private void DrawSpine(SpriteBatch b)
        {
            int spineW = 22;
            int spineX = width / 2 - spineW / 2;
            var spineRect = new Rectangle(spineX, _bookFrameBounds.Y + 8, spineW, _bookFrameBounds.Height - 16);

            // Spine base -- darker leather
            Color spineBase = new Color(78, 48, 28);
            b.Draw(Game1.fadeToBlackRect, spineRect, spineBase);

            // Spine vertical grain
            var rng = new Random(_seed + 5000);
            for (int i = 0; i < spineRect.Width; i += 2)
            {
                float alpha = 0.03f + (float)rng.NextDouble() * 0.04f;
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(spineRect.X + i, spineRect.Y, 1, spineRect.Height),
                    new Color(120, 85, 50) * alpha);
            }

            // Spine ribs -- raised horizontal bands
            int ribCount = 5;
            for (int i = 0; i < ribCount; i++)
            {
                int ribY = spineRect.Y + (i + 1) * spineRect.Height / (ribCount + 1) - 3;
                // Dark shadow below rib
                b.Draw(Game1.fadeToBlackRect, new Rectangle(spineRect.X, ribY + 4, spineRect.Width, 2), Color.Black * 0.3f);
                // Rib
                b.Draw(Game1.fadeToBlackRect, new Rectangle(spineRect.X, ribY, spineRect.Width, 4), new Color(150, 110, 60));
                // Highlight on top of rib
                b.Draw(Game1.fadeToBlackRect, new Rectangle(spineRect.X, ribY, spineRect.Width, 1), new Color(200, 160, 100) * 0.5f);
            }

            // Spine title text (vertical effect -- simplified to small horizontal text)
            string spineTitle = "回音日志";
            Vector2 stSize = Game1.tinyFont.MeasureString(spineTitle);
            float stY = spineRect.Y + spineRect.Height / 2 - stSize.X / 2;
            // Draw character by character vertically
            for (int i = 0; i < spineTitle.Length; i++)
            {
                char c = spineTitle[i];
                Vector2 charSize = Game1.tinyFont.MeasureString(c.ToString());
                b.DrawString(Game1.tinyFont, c.ToString(),
                    new Vector2(spineRect.Center.X - charSize.X / 2, stY + i * 14),
                    new Color(210, 180, 130), 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
            }

            // Center crease shadow between pages
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle(width / 2 - 1, _bookFrameBounds.Y + 2, 2, _bookFrameBounds.Height - 4),
                Color.Black * 0.1f);
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle(width / 2 - 3, _bookFrameBounds.Y + 2, 1, _bookFrameBounds.Height - 4),
                Color.Black * 0.06f);
        }

        private void DrawRibbonBookmark(SpriteBatch b)
        {
            // Silk ribbon hanging from bottom of spine -- gentle wave animation
            int ribbonW = 10;
            int ribbonX = width / 2 - ribbonW / 2;
            int ribbonTop = _bookFrameBounds.Bottom - 10;
            int ribbonLen = 80;

            Color ribbonColor = new Color(155, 55, 45);
            Color ribbonDark = new Color(110, 30, 25);
            Color ribbonLight = new Color(195, 90, 75);

            for (int i = 0; i < ribbonLen; i++)
            {
                float wave = MathF.Sin(_time * 1.8f + i * 0.08f) * (3f + i * 0.04f);
                int rx = ribbonX + (int)wave;
                int ry = ribbonTop + i;

                float t = (float)i / ribbonLen;
                Color col = Color.Lerp(ribbonLight, ribbonDark, t);

                b.Draw(Game1.fadeToBlackRect, new Rectangle(rx, ry, ribbonW, 1), col);

                // Edge shading
                if (i == 0)
                    b.Draw(Game1.fadeToBlackRect, new Rectangle(rx, ry, ribbonW, 2), ribbonColor * 0.8f);
            }

            // Small V-cut at ribbon end
            int endY = ribbonTop + ribbonLen;
            float endWave = MathF.Sin(_time * 1.8f + ribbonLen * 0.08f) * (3f + ribbonLen * 0.04f);
            int endX = ribbonX + (int)endWave;
            b.Draw(Game1.fadeToBlackRect, new Rectangle(endX + 2, endY - 2, 3, 6), ribbonDark);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(endX + 5, endY - 2, 3, 6), ribbonDark);
        }

        // ═══════════════════════════════════════════════════════════
        //  PAGES -- AGED PAPER
        // ═══════════════════════════════════════════════════════════

        private void DrawAgedPage(SpriteBatch b, Rectangle bounds, bool isLeft)
        {
            // Paper base -- warm cream with subtle variation
            Color paperBase = isLeft
                ? new Color(253, 248, 235)
                : new Color(249, 243, 228);

            b.Draw(Game1.fadeToBlackRect, bounds, paperBase);

            // ── Yellowed aging gradient toward edges ──
            int ageBorder = 30;
            Color ageColor = new Color(210, 180, 120);
            for (int i = 0; i < ageBorder; i++)
            {
                float t = 1f - (float)i / ageBorder;
                float alpha = 0.06f * t * t;
                // Top
                b.Draw(Game1.fadeToBlackRect, new Rectangle(bounds.X, bounds.Y + i, bounds.Width, 1), ageColor * alpha);
                // Bottom
                b.Draw(Game1.fadeToBlackRect, new Rectangle(bounds.X, bounds.Bottom - i, bounds.Width, 1), ageColor * alpha);
                // Left
                b.Draw(Game1.fadeToBlackRect, new Rectangle(bounds.X + i, bounds.Y, 1, bounds.Height), ageColor * alpha);
                // Right
                b.Draw(Game1.fadeToBlackRect, new Rectangle(bounds.Right - i, bounds.Y, 1, bounds.Height), ageColor * alpha);
            }

            // ── Paper grain -- subtle noise ──
            var rng = new Random(_seed + (isLeft ? 0 : 1000));
            for (int i = 0; i < 50; i++)
            {
                int gx = bounds.X + rng.Next(bounds.Width);
                int gy = bounds.Y + rng.Next(bounds.Height);
                int gw = 2 + rng.Next(6);
                int gh = 1 + rng.Next(2);
                float ga = 0.004f + (float)rng.NextDouble() * 0.015f;
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(gx, gy, gw, gh),
                    new Color(195, 175, 135) * ga);
            }

            // ── Page edge shadow (deckle edge / binding shadow) ──
            int shadowW = 5;
            Color bindingShadow = new Color(140, 110, 70);
            if (isLeft)
            {
                // Shadow on the right side (near binding)
                for (int i = 0; i < shadowW; i++)
                {
                    float alpha = 0.12f * (1f - (float)i / shadowW);
                    b.Draw(Game1.fadeToBlackRect,
                        new Rectangle(bounds.Right - i, bounds.Y, 1, bounds.Height), bindingShadow * alpha);
                }
                // Left edge light
                b.Draw(Game1.fadeToBlackRect, new Rectangle(bounds.X + 2, bounds.Y, 1, bounds.Height), new Color(240, 230, 200) * 0.3f);
            }
            else
            {
                // Shadow on the left side (near binding)
                for (int i = 0; i < shadowW; i++)
                {
                    float alpha = 0.12f * (1f - (float)i / shadowW);
                    b.Draw(Game1.fadeToBlackRect,
                        new Rectangle(bounds.X + i, bounds.Y, 1, bounds.Height), bindingShadow * alpha);
                }
                // Right edge light
                b.Draw(Game1.fadeToBlackRect, new Rectangle(bounds.Right - 2, bounds.Y, 1, bounds.Height), new Color(240, 230, 200) * 0.3f);
            }

            // Bottom page shadow
            b.Draw(Game1.fadeToBlackRect, new Rectangle(bounds.X, bounds.Bottom - 3, bounds.Width, 3), new Color(180, 150, 110) * 0.15f);

            // ── Water stains -- subtle circular discoloration ──
            DrawWaterStain(b, bounds, isLeft);

            // ── Coffee/tea ring stain ──
            if ((_seed + (isLeft ? 1 : 0)) % 7 == 0)
                DrawTeaRing(b, bounds);
        }

        private void DrawWaterStain(SpriteBatch b, Rectangle bounds, bool isLeft)
        {
            float sx = bounds.X + bounds.Width * (isLeft ? 0.7f : 0.3f);
            float sy = bounds.Y + bounds.Height * (isLeft ? 0.55f : 0.65f);

            for (int ring = 0; ring < 6; ring++)
            {
                float radius = 18f + ring * 22f;
                float alpha = 0.012f - ring * 0.002f;
                if (alpha > 0)
                    DrawCircle(b, new Vector2(sx, sy), radius, new Color(175, 155, 120) * alpha, 36);
            }
        }

        private void DrawTeaRing(SpriteBatch b, Rectangle bounds)
        {
            float rx = bounds.X + bounds.Width * 0.35f;
            float ry = bounds.Y + bounds.Height * 0.72f;
            float radius = 22f;

            // Ring outline
            DrawCircle(b, new Vector2(rx, ry), radius, new Color(155, 120, 70) * 0.08f, 32);
            DrawCircle(b, new Vector2(rx, ry), radius + 2f, new Color(155, 120, 70) * 0.04f, 32);
            // Filled center
            DrawCircle(b, new Vector2(rx, ry), radius - 4f, new Color(180, 150, 110) * 0.06f, 24);
        }

        private void DrawFoxingSpots(SpriteBatch b)
        {
            for (int i = 0; i < _foxingSpots.Length; i++)
            {
                bool isLeft = i < 6;
                var bounds = isLeft ? _leftPageBounds : _rightPageBounds;
                float x = bounds.X + 30 + _foxingSpots[i].X * (bounds.Width - 60);
                float y = bounds.Y + 50 + _foxingSpots[i].Y * (bounds.Height - 60);

                if (x > bounds.X + 25 && x < bounds.Right - 25 && y > bounds.Y + 20 && y < bounds.Bottom - 15)
                {
                    Color foxing = new Color(160, 110, 55, 7);
                    DrawCircle(b, new Vector2(x, y), _foxingSizes[i], foxing, 8);
                }
            }
        }

        private void DrawRuledLines(SpriteBatch b, Rectangle bounds)
        {
            int lineSpacing = 18;
            int startY = bounds.Y + 50;
            Color ruled = new Color(170, 155, 130, 8);

            for (int ly = startY; ly < bounds.Bottom - 30; ly += lineSpacing)
            {
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(bounds.X + 25, ly, bounds.Width - 50, 1), ruled);
            }

            Color margin = new Color(180, 100, 90, 5);
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle(bounds.X + 32, startY, 1, bounds.Height - 80), margin);
        }

        private void DrawPressedFlower(SpriteBatch b, Rectangle bounds)
        {
            float fx = bounds.X + bounds.Width * _pressedFlowerPos.X;
            float fy = bounds.Y + bounds.Height * _pressedFlowerPos.Y;
            var center = new Vector2(fx, fy);
            Color petalColor = _pressedFlowerType switch
            {
                0 => new Color(180, 120, 140, 18),
                1 => new Color(150, 140, 200, 18),
                2 => new Color(190, 170, 100, 18),
                _ => new Color(200, 150, 120, 18),
            };
            Color stemColor = new Color(120, 140, 90, 12);

            // Stem
            float stemAngle = _pressedFlowerType * 1.2f + 0.5f;
            Vector2 stemEnd = center + new Vector2(MathF.Cos(stemAngle) * 16f, MathF.Sin(stemAngle) * 16f);
            DrawLine(b, center, stemEnd, stemColor, 0.8f);

            // Petals (5 small dots arranged in a circle)
            for (int p = 0; p < 5; p++)
            {
                float angle = p * MathHelper.TwoPi / 5 + _pressedFlowerType;
                Vector2 petal = center + new Vector2(MathF.Cos(angle) * 5f, MathF.Sin(angle) * 5f);
                DrawCircle(b, petal, 2.0f, petalColor, 6);
            }
            // Center
            DrawCircle(b, center, 1.5f, new Color(160, 130, 60, 45), 5);
        }

        // ═══════════════════════════════════════════════════════════
        //  DECORATIVE ELEMENTS
        // ═══════════════════════════════════════════════════════════

        private void DrawCornerFlourish(SpriteBatch b, Rectangle bounds, bool mirror)
        {
            Color ink = new Color(110, 75, 40, 55);
            int pad = 20;
            int size = 24;
            float thick = 1.5f;

            int cx = mirror ? bounds.Right - pad : bounds.X + pad;
            int cy = bounds.Y + pad;

            // Corner arc
            DrawArc(b, new Vector2(cx, cy), size, mirror ? 0f : MathHelper.PiOver2, MathHelper.PiOver2, ink, thick, 10);

            // Inner smaller arc
            DrawArc(b, new Vector2(cx, cy), size - 6, mirror ? 0.4f : MathHelper.PiOver2 + 0.4f, MathHelper.PiOver2 - 0.8f, ink * 0.6f, 0.8f, 6);

            // Small dot at corner
            DrawCircle(b, new Vector2(cx, cy), 3f, ink * 1.3f, 10);

            // Tiny curl
            float curlAngle = mirror ? -0.6f : MathHelper.PiOver2 + 0.6f;
            Vector2 curlCenter = new Vector2(cx, cy) + new Vector2(MathF.Cos(curlAngle) * (size + 4), MathF.Sin(curlAngle) * (size + 4));
            DrawArc(b, curlCenter, 4f, curlAngle - 1f, 2f, ink * 0.7f, 0.8f, 5);
        }

        private void DrawInkStains(SpriteBatch b)
        {
            for (int i = 0; i < _inkStains.Length; i++)
            {
                bool isLeft = i < 3;
                var bounds = isLeft ? _leftPageBounds : _rightPageBounds;

                float x = bounds.X + 30 + _inkStains[i].X * (bounds.Width - 60);
                float y = bounds.Y + 50 + _inkStains[i].Y * (bounds.Height - 80);
                float size = _inkSizes[i];

                if (x > bounds.X + 20 && x < bounds.Right - 20 && y > bounds.Y + 40 && y < bounds.Bottom - 10)
                {
                    Color ink = new Color(55, 35, 25, 4);
                    DrawCircle(b, new Vector2(x, y), size, ink, 12);

                    DrawCircle(b, new Vector2(x + size * 0.3f, y + size * 0.2f), size * 0.6f, ink * 0.18f, 10);

                    if (size > 5)
                    {
                        var rng = new Random((int)(_seed + i * 137));
                        for (int s = 0; s < 4; s++)
                        {
                            float sx = x + (float)(rng.NextDouble() - 0.5) * size * 2f;
                            float sy = y + (float)(rng.NextDouble() - 0.5) * size * 2f;
                            float ss = 0.5f + (float)rng.NextDouble() * 2.5f;
                            DrawCircle(b, new Vector2(sx, sy), ss, ink * 0.12f, 6);
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
            float titleY = _bookFrameBounds.Y - 48;

            // ── Title plate -- ornate leather plaque with gold border ──
            int padH = 28, padV = 10;
            var plaqueRect = new Rectangle((int)titleX - padH, (int)titleY - padV, (int)titleSize.X + padH * 2, (int)titleSize.Y + padV * 2);

            // Plaque base -- dark leather
            b.Draw(Game1.fadeToBlackRect, plaqueRect, new Color(85, 50, 28));
            // Inner lighter area
            b.Draw(Game1.fadeToBlackRect, new Rectangle(plaqueRect.X + 4, plaqueRect.Y + 4, plaqueRect.Width - 8, plaqueRect.Height - 8), new Color(105, 68, 38));
            // Gold border -- double line
            Color gold = new Color(200, 165, 80);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(plaqueRect.X + 2, plaqueRect.Y + 2, plaqueRect.Width - 4, 1), gold * 0.6f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(plaqueRect.X + 2, plaqueRect.Bottom - 3, plaqueRect.Width - 4, 1), gold * 0.6f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(plaqueRect.X + 2, plaqueRect.Y + 2, 1, plaqueRect.Height - 4), gold * 0.5f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(plaqueRect.Right - 3, plaqueRect.Y + 2, 1, plaqueRect.Height - 4), gold * 0.5f);
            // Inner thin gold
            int innerOff = 5;
            b.Draw(Game1.fadeToBlackRect, new Rectangle(plaqueRect.X + innerOff, plaqueRect.Y + innerOff, plaqueRect.Width - innerOff * 2, 1), gold * 0.35f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(plaqueRect.X + innerOff, plaqueRect.Bottom - innerOff - 1, plaqueRect.Width - innerOff * 2, 1), gold * 0.35f);

            // ── Title text with embossed gold effect ──
            b.DrawString(Game1.dialogueFont, title, new Vector2(titleX + 2, titleY + 8), Color.Black * 0.4f);
            b.DrawString(Game1.dialogueFont, title, new Vector2(titleX, titleY + 6), new Color(215, 185, 125));

            // ── Ornamental dots flanking title ──
            float dotY = titleY + titleSize.Y / 2 + 5;
            DrawCircle(b, new Vector2(titleX - 16, dotY), 2f, gold * 0.5f, 6);
            DrawCircle(b, new Vector2(titleX + titleSize.X + 16, dotY), 2f, gold * 0.5f, 6);

            // ── Page number at bottom ──
            int maxPage = Math.Max(1, (int)Math.Ceiling((float)_filteredEntries.Count / _entriesPerPage));
            string pageNum = $"-- {_currentPage + 1} / {maxPage} --";
            Vector2 pageSize = Game1.tinyFont.MeasureString(pageNum);
            float pageNumX = width / 2 - pageSize.X / 2;
            float pageNumY = _bookFrameBounds.Bottom + 8;

            b.DrawString(Game1.tinyFont, pageNum, new Vector2(pageNumX, pageNumY), new Color(155, 125, 85));
        }

        // ═══════════════════════════════════════════════════════════
        //  ENTRIES
        // ═══════════════════════════════════════════════════════════

        private void DrawCurrentEntries(SpriteBatch b)
        {
            int startIdx = _currentPage * _entriesPerPage;
            int endIdx = Math.Min(startIdx + _entriesPerPage, _filteredEntries.Count);

            if (_filteredEntries.Count == 0)
            {
                string empty = "还没有记忆。去和镇上的人说说话，\n他们会开始记录关于你的故事。";
                Vector2 emptySize = Game1.smallFont.MeasureString(empty);
                b.DrawString(Game1.smallFont, empty,
                    new Vector2(_leftPageBounds.Center.X - emptySize.X / 2,
                        _leftPageBounds.Center.Y - emptySize.Y / 2),
                    new Color(140, 110, 80), 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
                return;
            }

            for (int i = startIdx; i < endIdx; i++)
            {
                var entry = _filteredEntries[i];
                int entryIdx = i - startIdx;

                // 2 entries = one per page (left and right)
                Rectangle pageBounds = entryIdx == 0 ? _leftPageBounds : _rightPageBounds;
                bool isRightPage = entryIdx != 0;

                DrawEntryOnPage(b, entry, pageBounds, isRightPage);
            }
        }

        private void DrawEntryOnPage(SpriteBatch b, JournalEntry entry, Rectangle pageBounds, bool isRightPage)
        {
            int padX = 32;
            int padY = 20;
            int x = pageBounds.X + padX;
            int y = pageBounds.Y + padY;
            int maxW = pageBounds.Width - padX * 2;

            // ── Memory mark pin ──
            if (_markedDates.Contains(DateKey(entry.Year, entry.Season, entry.DayOfMonth)))
            {
                b.DrawString(Game1.tinyFont, "\U0001f4cc",
                    new Vector2(pageBounds.Right - padX - 10, pageBounds.Y + padY),
                    new Color(185, 100, 75, 210));
            }

            // ── NPC name ──
            Color npcColor = ModEntry.Config.UseNpcColorCoding
                ? GetNpcColor(entry.NpcName)
                : new Color(85, 50, 25);

            string npcHeader = entry.NpcName;
            Vector2 nameSize = Game1.smallFont.MeasureString(npcHeader);

            // Emotion icon
            string emoji = entry.EmotionTag switch
            {
                "Warm" => "~", "Melancholy" => "-", "Curiosity" => "?",
                "Humor" => "!", "Longing" => "...", "Wonder" => "!",
                "Nostalgia" => "~", "Reflection" => ".", _ => ""
            };

            // Draw NPC name centered with emoji
            float totalNameW = nameSize.X + (emoji.Length > 0 ? 20 : 0);
            float nameStartX = pageBounds.Center.X - totalNameW / 2;
            b.DrawString(Game1.smallFont, npcHeader, new Vector2(nameStartX + (emoji.Length > 0 ? 10 : 0), y),
                npcColor, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
            if (emoji.Length > 0)
            {
                b.DrawString(Game1.tinyFont, emoji,
                    new Vector2(nameStartX, y + 2), npcColor * 0.5f, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);
            }

            y += (int)nameSize.Y + 2;

            // ── Thin decorative rule ──
            int ruleY = y;
            int ruleWidth = Math.Min(180, maxW);
            int ruleX = pageBounds.Center.X - ruleWidth / 2;
            b.Draw(Game1.fadeToBlackRect, new Rectangle(ruleX, ruleY, ruleWidth / 2 - 4, 1),
                npcColor * 0.2f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(ruleX + ruleWidth / 2 + 4, ruleY, ruleWidth / 2 - 4, 1),
                npcColor * 0.2f);
            // Center dot
            DrawCircle(b, new Vector2(pageBounds.Center.X, ruleY), 1.8f, npcColor * 0.35f, 8);

            y += 6;

            // ── Date ──
            string dateStr = entry.DisplayDate;
            Vector2 dateSize = Game1.tinyFont.MeasureString(dateStr);
            b.DrawString(Game1.tinyFont, dateStr,
                new Vector2(pageBounds.Center.X - dateSize.X / 2, y),
                new Color(155, 120, 85), 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            y += (int)dateSize.Y + 6;

            // ── Entry text ──
            float textScale = 0.85f;
            int availableWidth = maxW - 10; // small safety margin
            var lines = StringHelper.WrapText(entry.DisplayText, availableWidth, textScale);

            int age = entry.EntryAgeDays;
            float aging = Math.Min(0.7f, age / 200f * ModEntry.Config.JournalPageAgingSpeed);
            Color textColor = entry.IsPlayerEntry
                ? new Color(40, 65, 95)
                : new Color(
                    (byte)(58 - aging * 30),
                    (byte)(40 - aging * 20),
                    (byte)(22 - aging * 12));

            int maxLines = 10;
            int lineGap = 3;

            foreach (string line in lines.Take(maxLines))
            {
                Vector2 textSize = Game1.smallFont.MeasureString(line);
                if (y + textSize.Y * textScale < pageBounds.Bottom - padY - 10)
                {
                    b.DrawString(Game1.smallFont, line,
                        new Vector2(x, y), textColor, 0f, Vector2.Zero,
                        textScale, SpriteEffects.None, 0f);
                    y += (int)(textSize.Y * textScale) + lineGap;
                }
                else break;
            }

            // Truncation indicator
            if (lines.Count > maxLines)
            {
                b.DrawString(Game1.tinyFont, "...",
                    new Vector2(pageBounds.Right - padX - 12, pageBounds.Bottom - padY - 14),
                    new Color(140, 110, 80, 180), 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            }
            y += 8;
            int dividerY = Math.Min(y, pageBounds.Bottom - padY);
            DrawOrnamentalDivider(b, new Vector2(x, dividerY), maxW);
        }

        private void DrawOrnamentalDivider(SpriteBatch b, Vector2 pos, int width)
        {
            Color divider = new Color(155, 125, 95, 90);
            int midX = (int)pos.X + width / 2;

            // Left flourish (thin line + curl)
            int leftEnd = midX - 16;
            b.Draw(Game1.fadeToBlackRect, new Rectangle((int)pos.X + 12, (int)pos.Y, leftEnd - (int)pos.X - 12, 1), divider);
            DrawCircle(b, new Vector2((int)pos.X + 12, (int)pos.Y), 1.5f, divider, 5);

            // Right flourish
            int rightStart = midX + 16;
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rightStart, (int)pos.Y, (int)pos.X + width - 12 - rightStart, 1), divider);
            DrawCircle(b, new Vector2((int)pos.X + width - 12, (int)pos.Y), 1.5f, divider, 5);

            // Center ornament
            b.Draw(Game1.fadeToBlackRect, new Rectangle(midX - 5, (int)pos.Y - 2, 10, 4), divider * 1.4f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(midX - 1, (int)pos.Y - 6, 2, 12), divider * 0.6f);

            // Side dots
            DrawCircle(b, new Vector2(midX - 16, (int)pos.Y), 1.2f, divider * 0.8f, 4);
            DrawCircle(b, new Vector2(midX + 16, (int)pos.Y), 1.2f, divider * 0.8f, 4);
        }

        // ═══════════════════════════════════════════════════════════
        //  BUTTONS -- Ornate brass/leather styling
        // ═══════════════════════════════════════════════════════════

        private void DrawButtons(SpriteBatch b)
        {
            // Previous / Next -- leather-tab style
            int maxPage = Math.Max(1, (int)Math.Ceiling((float)_filteredEntries.Count / _entriesPerPage)) - 1;

            if (_currentPage > 0)
                DrawLeatherTab(b, _prevButton, "< 前一页", isActive: true, isLeft: true);
            else
                DrawLeatherTab(b, _prevButton, "< 前一页", isActive: false, isLeft: true);

            if (_currentPage < maxPage)
                DrawLeatherTab(b, _nextButton, "后一页 >", isActive: true, isLeft: false);
            else
                DrawLeatherTab(b, _nextButton, "后一页 >", isActive: false, isLeft: false);

            // Side buttons
            DrawBrassButton(b, _filterButton, _filterNpc != null ? $"> {_filterNpc}" : "筛选", _filterButton.Contains(Game1.getMousePosition()));
            DrawBrassButton(b, _sortButton, _sortMode == "newest" ? "最新在前" : "最早在前", _sortButton.Contains(Game1.getMousePosition()));
            if (ModEntry.Config.EnablePlayerDiary)
                DrawBrassButton(b, _diaryButton, "写日记", _diaryButton.Contains(Game1.getMousePosition()));
            DrawBrassButton(b, _markButton, _hasMarkedEntry ? "已标记" : "标记此页", _markButton.Contains(Game1.getMousePosition()));

            // Close button
            DrawCloseButton(b, _closeButton);
        }

        private void DrawLeatherTab(SpriteBatch b, Rectangle rect, string text, bool isActive, bool isLeft)
        {
            bool hover = rect.Contains(Game1.getMousePosition()) && isActive;

            // Tab shape -- rounded on the outer edge
            Color bg = isActive
                ? (hover ? new Color(155, 115, 68) : new Color(130, 95, 55))
                : new Color(75, 55, 35);

            b.Draw(Game1.fadeToBlackRect, rect, bg);

            // Leather texture on tab
            for (int i = 0; i < 3; i++)
            {
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(rect.X + 6, rect.Y + 4 + i * 10, rect.Width - 12, 1),
                    (i == 1 ? new Color(170, 130, 80) : new Color(80, 55, 30)) * 0.12f);
            }

            // Top/bottom bevel
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Color(190, 150, 100) * 0.4f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Color(55, 35, 18) * 0.5f);

            // Stitch dots near outer edge
            int stitchX = isLeft ? rect.Right - 6 : rect.X + 6;
            for (int sy = rect.Y + 6; sy < rect.Bottom - 4; sy += 8)
            {
                DrawCircle(b, new Vector2(stitchX, sy), 1.2f, new Color(180, 155, 120) * (isActive ? 0.5f : 0.25f), 4);
            }

            Color textCol = isActive
                ? (hover ? Color.Wheat : new Color(210, 180, 140))
                : new Color(110, 90, 70);

            Vector2 ts = Game1.smallFont.MeasureString(text);
            b.DrawString(Game1.smallFont, text,
                new Vector2(rect.Center.X - ts.X / 2, rect.Center.Y - ts.Y / 2),
                textCol, 0f, Vector2.Zero, 0.88f, SpriteEffects.None, 0f);
        }

        private void DrawBrassButton(SpriteBatch b, Rectangle rect, string text, bool hover)
        {
            Color bg = hover ? new Color(175, 140, 75) : new Color(140, 108, 55);
            Color border = hover ? new Color(210, 180, 120) : new Color(160, 130, 80);
            Color textCol = hover ? Color.White : new Color(225, 205, 165);

            // Button base
            b.Draw(Game1.fadeToBlackRect, rect, bg);

            // Border -- slightly raised
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Color(90, 65, 30));
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Y, 1, rect.Height), border * 0.7f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Color(90, 65, 30) * 0.7f);

            // Highlight band
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X + 2, rect.Y + 1, rect.Width - 4, 2), new Color(230, 200, 150) * 0.25f);

            // Text
            Vector2 ts = Game1.smallFont.MeasureString(text);
            b.DrawString(Game1.smallFont, text,
                new Vector2(rect.Center.X - ts.X / 2, rect.Center.Y - ts.Y / 2 + 1),
                textCol, 0f, Vector2.Zero, 0.82f, SpriteEffects.None, 0f);
        }

        private void DrawCloseButton(SpriteBatch b, Rectangle rect)
        {
            bool hover = rect.Contains(Game1.getMousePosition());

            // Round-ish button
            Color bg = hover ? new Color(175, 55, 35) : new Color(120, 40, 28);
            Color ring = hover ? new Color(210, 120, 90) : new Color(150, 80, 55);

            // Ring
            DrawCircle(b, new Vector2(rect.Center.X, rect.Center.Y), rect.Width / 2f, ring, 16);
            // Fill
            DrawCircle(b, new Vector2(rect.Center.X, rect.Center.Y), rect.Width / 2f - 3, bg, 14);

            Vector2 xSize = Game1.smallFont.MeasureString("✕");
            b.DrawString(Game1.smallFont, "✕",
                new Vector2(rect.Center.X - xSize.X / 2, rect.Center.Y - xSize.Y / 2 + 1),
                hover ? Color.White : new Color(225, 190, 170));
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
                DrawLine(b, p1, p2, color, 1.5f);
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
            Rectangle rect = new Rectangle((int)start.X, (int)start.Y, (int)length, (int)Math.Max(1, (int)thickness));
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
            {
                Game1.playSound("bigSelect");
                WriteDiaryEntry();
            }
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

        private void WriteDiaryEntry()
        {
            if (Game1.player == null) return;

            string season = StringHelper.SeasonDisplayName(Game1.currentSeason);
            string timeOfDay = StringHelper.TimeOfDayToPeriod(Game1.timeOfDay);
            string location = Game1.currentLocation?.Name ?? "星露谷";
            string weather = Game1.isRaining ? "雨" : Game1.isSnowing ? "雪" : "晴";

            string title = $"{season} {Game1.dayOfMonth}日 -- {timeOfDay}";
            string text = RandomHelper.Next(4) switch
            {
                0 => $"在{location}。今天{weather}。没什么特别的事----但不知道为什么会想记下来。也许是因为风刚刚刚好。",
                1 => $"第{Game1.year}年的{season}。{location}的{weather}天。在这里已经有一阵子了。今天想写点什么，又不知道从哪里写起。那就不写。就这样。",
                2 => $"今天做了很多事。也什么都没做。这两件事不矛盾----做很多事的人，也能什么都没做地待一会儿。{location}是个可以什么都不做的地方。",
                _ => $"{location}。{weather}。{timeOfDay}。有人说过'日记不要写太好'----因为写太好就不像日记了。所以我就写：今天还不错。完了。"
            };

            ModEntry.Diary?.WriteEntry(title, text);
            Game1.playSound("slimeHit");

            // Reload all entries so the new diary entry appears immediately
            _entries = _journal.GetAllEntries();
            ApplyFilters();
            Game1.addHUDMessage(new HUDMessage("写下了今天的日记。回音日志里多了一页属于你的。", HUDMessage.newQuest_type));
        }

        public static void CheckDayMark()
        {
            if (_markedDates.Contains(DateKey(Game1.year, Game1.currentSeason, Game1.dayOfMonth)))
            {
                Game1.addHUDMessage(new HUDMessage("[标记] 你在日志里标记过这一天。是时候去探望某个人了。", HUDMessage.newQuest_type));
            }
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            CalculateLayout();
        }
    }
}
