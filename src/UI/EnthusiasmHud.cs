using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace EchoesOfTheHollow.UI
{
    /// <summary>
    /// 兴致HUD -- compact mood indicator replacing the vanilla stamina bar.
    /// Narrow vertical bar positioned at far right edge, above inventory area.
    /// Fully opaque background covers any vanilla stamina remnants.
    /// </summary>
    internal class EnthusiasmHud
    {
        private readonly Systems.Enthusiasm.EnthusiasmSystem _enthusiasm;

        private const int BarW = 10;
        private const int BarH = 90;
        private const int PanelW = 26;
        private const int PanelH = 120;
        private float _time;
        private Vector2 _pos;

        public EnthusiasmHud(IModHelper helper, IMonitor monitor, Systems.Enthusiasm.EnthusiasmSystem enthusiasm)
        {
            _enthusiasm = enthusiasm;
        }

        public void OnRenderingHud()
        {
            // Position at far right, well above the inventory bar
            _pos = new Vector2(Game1.uiViewport.Width - PanelW - 8, Game1.uiViewport.Height / 2 - PanelH / 2);
        }

        public void OnRenderedHud()
        {
            if (!ModEntry.Config.ShowEnthusiasmHud || !ModEntry.Config.EnableEnthusiasm) return;

            var (value, _, _) = _enthusiasm.GetDashboard();
            SpriteBatch b = Game1.spriteBatch;
            float x = _pos.X, y = _pos.Y;
            _time += 0.016f;

            // ══════════════════════════════════════════
            // Opaque cover strip -- hides vanilla stamina remnants on right edge
            // ══════════════════════════════════════════
            var cover = new Rectangle(Game1.uiViewport.Width - 320, Game1.uiViewport.Height - 120, 320, 120);
            b.Draw(Game1.fadeToBlackRect, cover, Color.Black * 0.85f);

            // ══════════════════════════════════════════
            // Leather panel
            // ══════════════════════════════════════════
            var panel = new Rectangle((int)x, (int)y, PanelW, PanelH);
            Color leatherDark = new Color(50, 34, 20);
            Color leatherLight = new Color(85, 58, 34);
            Color gold = new Color(180, 145, 85);

            b.Draw(Game1.fadeToBlackRect, panel, leatherDark);

            // Leather grain
            for (int gy = panel.Y + 2; gy < panel.Bottom - 2; gy += 3)
                b.Draw(Game1.fadeToBlackRect,
                    new Rectangle(panel.X + 2, gy, panel.Width - 4, 1),
                    leatherLight * 0.08f);

            // Gold border
            DrawRectBorder(b, panel, gold * 0.5f, 1);

            // ══════════════════════════════════════════
            // Label "兴致" at top (vertical writing)
            // ══════════════════════════════════════════
            string label = "兴 致";
            float labelY = panel.Y + 6;
            foreach (char c in label)
            {
                if (c != ' ')
                {
                    Vector2 cs = Game1.tinyFont.MeasureString(c.ToString());
                    b.DrawString(Game1.tinyFont, c.ToString(),
                        new Vector2(panel.Center.X - cs.X / 2, labelY),
                        new Color(215, 185, 125), 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
                }
                labelY += 14;
            }

            // ══════════════════════════════════════════
            // Vertical bar
            // ══════════════════════════════════════════
            int barX = panel.Center.X - BarW / 2;
            int barY = panel.Y + 30;

            // Track
            var track = new Rectangle(barX - 1, barY - 1, BarW + 2, BarH + 2);
            b.Draw(Game1.fadeToBlackRect, track, Color.Black * 0.4f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(barX, barY, BarW, BarH), new Color(25, 18, 10));

            // Fill (from bottom up)
            int fillH = (int)(BarH * Math.Max(0.03f, value));
            Color fillColor = GetBarColor(value);

            if (fillH > 0)
            {
                var fillRect = new Rectangle(barX, barY + BarH - fillH, BarW, fillH);
                b.Draw(Game1.fadeToBlackRect, fillRect, fillColor * 0.85f);

                // Bright edge on top of fill
                var edge = new Rectangle(barX, barY + BarH - fillH, BarW, 2);
                b.Draw(Game1.fadeToBlackRect, edge, Color.White * 0.2f);
            }

            // Bar border
            DrawRectBorder(b, new Rectangle(barX - 1, barY - 1, BarW + 2, BarH + 2), gold * 0.4f, 1);

            // ══════════════════════════════════════════
            // Percentage at bottom
            // ══════════════════════════════════════════
            string pct = $"{(int)(value * 100)}";
            Vector2 ps = Game1.tinyFont.MeasureString(pct);
            b.DrawString(Game1.tinyFont, pct,
                new Vector2(panel.Center.X - ps.X / 2, barY + BarH + 6),
                GetBarColor(value) * 0.7f, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

            // ══════════════════════════════════════════
            // Drained pulse
            // ══════════════════════════════════════════
            if (value < 0.15f)
            {
                float pulse = MathF.Sin(_time * 4f) * 0.25f + 0.25f;
                b.Draw(Game1.fadeToBlackRect, panel, Color.Red * pulse);
            }
        }

        private static Color GetBarColor(float value) => value switch
        {
            > 0.65f => new Color(95, 185, 75),
            > 0.45f => new Color(175, 165, 45),
            > 0.25f => new Color(200, 130, 35),
            > 0.10f => new Color(185, 75, 45),
            _ => new Color(165, 40, 35)
        };

        private static void DrawRectBorder(SpriteBatch b, Rectangle rect, Color color, int thickness)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }
    }
}
