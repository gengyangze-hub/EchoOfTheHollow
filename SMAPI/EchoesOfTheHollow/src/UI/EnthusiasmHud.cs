using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace EchoesOfTheHollow.UI
{
    /// <summary>
    /// 兴致HUD — mood/enthusiasm bar displayed on the HUD
    /// Replaces the stamina bar with a softer, color-shifting mood indicator.
    /// </summary>
    internal class EnthusiasmHud
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly Systems.Enthusiasm.EnthusiasmSystem _enthusiasm;

        // Bar dimensions
        private const int BarWidth = 120;
        private const int BarHeight = 12;
        private const int IconSize = 16;

        // Bar position (adjusted in rendering)
        private Vector2 _barPosition;

        public EnthusiasmHud(IModHelper helper, IMonitor monitor, Systems.Enthusiasm.EnthusiasmSystem enthusiasm)
        {
            _helper = helper;
            _monitor = monitor;
            _enthusiasm = enthusiasm;
        }

        public void OnRenderingHud()
        {
            // Calculate position based on screen size and config
            float x = ModEntry.Config.EnthusiasmHudPosition == "Right"
                ? Game1.uiViewport.Width - BarWidth - 80
                : 80;
            float y = Game1.uiViewport.Height - 60;

            _barPosition = new Vector2(x, y);
        }

        public void OnRenderedHud()
        {
            if (!ModEntry.Config.ShowEnthusiasmHud || !ModEntry.Config.EnableEnthusiasm) return;

            var (value, uniqueActivities, totalActions) = _enthusiasm.GetDashboard();
            SpriteBatch b = Game1.spriteBatch;

            // ── Bar background ──
            var bgRect = new Rectangle((int)_barPosition.X, (int)_barPosition.Y, BarWidth, BarHeight);
            b.Draw(Game1.fadeToBlackRect, bgRect, Color.Black * 0.3f);

            // ── Bar fill (color shifts with value) ──
            int fillWidth = (int)(BarWidth * value);
            Color fillColor = value switch
            {
                > 0.7f => new Color(120, 200, 100),  // Green — vibrant
                > 0.4f => new Color(200, 180, 60),   // Yellow — fading
                > 0.2f => new Color(200, 140, 40),   // Orange — tired
                _ => new Color(180, 80, 60)           // Red — drained
            };

            if (fillWidth > 0)
            {
                var fillRect = new Rectangle((int)_barPosition.X, (int)_barPosition.Y, fillWidth, BarHeight);
                b.Draw(Game1.fadeToBlackRect, fillRect, fillColor * 0.7f);
            }

            // ── Bar border ──
            var borderRect = new Rectangle((int)_barPosition.X - 1, (int)_barPosition.Y - 1, BarWidth + 2, BarHeight + 2);
            // Simple border using rectangles
            b.Draw(Game1.fadeToBlackRect, new Rectangle(borderRect.X, borderRect.Y, borderRect.Width, 1), Color.White * 0.4f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(borderRect.X, borderRect.Bottom - 1, borderRect.Width, 1), Color.White * 0.4f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(borderRect.X, borderRect.Y, 1, borderRect.Height), Color.White * 0.4f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(borderRect.Right - 1, borderRect.Y, 1, borderRect.Height), Color.White * 0.4f);

            // ── Label ──
            string label = "兴致";
            Vector2 labelSize = Game1.tinyFont.MeasureString(label);
            b.DrawString(Game1.tinyFont, label,
                new Vector2(_barPosition.X, _barPosition.Y - labelSize.Y - 2),
                Color.Wheat * 0.8f);

            // ── Variety indicator ──
            if (uniqueActivities > 0)
            {
                string varietyText = $"✦{uniqueActivities}";
                Vector2 varietySize = Game1.tinyFont.MeasureString(varietyText);
                b.DrawString(Game1.tinyFont, varietyText,
                    new Vector2(_barPosition.X + BarWidth + 5, _barPosition.Y - 1),
                    Color.LightGoldenrodYellow * 0.7f);
            }
        }
    }
}
