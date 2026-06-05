using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheHollow.Data;
using EchoesOfTheHollow.Systems.Economy;
using EchoesOfTheHollow.Systems.Journal;
using EchoesOfTheHollow.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace EchoesOfTheHollow.UI
{
    /// <summary>
    /// 互惠篮界面 -- the basket interaction menu.
    /// Players deposit items with optional requests for NPCs.
    /// Shows active exchanges, pending items, and return notifications.
    /// </summary>
    public class BasketMenu : IClickableMenu
    {
        private readonly BasketSystem _basket;
        private readonly JournalSystem _journal;

        private List<BasketItem> _pendingItems;
        private List<BasketItem> _historyItems;
        private int _currentTab; // 0 = Deposit, 1 = Pending, 2 = History
        private int _selectedInventoryIndex = -1;

        // UI layout
        private Rectangle _depositTab;
        private Rectangle _pendingTab;
        private Rectangle _historyTab;
        private Rectangle _closeButton;
        private Rectangle _depositButton;
        // Scroll
        private int _scrollOffset;
        private Rectangle _scrollUpButton;
        private Rectangle _scrollDownButton;

        private const int ItemsPerPage = 5;

        public BasketMenu(BasketSystem basket, JournalSystem journal)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height)
        {
            _basket = basket;
            _journal = journal;
            _pendingItems = _basket.GetPendingItems();
            _historyItems = _basket.GetAllItems().Where(i => i.Status == ExchangeStatus.Matched).ToList();

            CalculateLayout();
        }

        private void CalculateLayout()
        {
            int menuW = 700;
            int menuH = 500;
            int menuX = (width - menuW) / 2;
            int menuY = (height - menuH) / 2;

            // Tabs
            _depositTab = new Rectangle(menuX, menuY - 40, 120, 40);
            _pendingTab = new Rectangle(menuX + 122, menuY - 40, 120, 40);
            _historyTab = new Rectangle(menuX + 244, menuY - 40, 120, 40);
            _closeButton = new Rectangle(menuX + menuW - 40, menuY - 40, 40, 40);

            // Deposit button
            _depositButton = new Rectangle(menuX + menuW - 160, menuY + menuH + 5, 150, 35);

            // Scroll
            _scrollUpButton = new Rectangle(menuX + menuW + 5, menuY + 10, 25, 25);
            _scrollDownButton = new Rectangle(menuX + menuW + 5, menuY + menuH - 35, 25, 25);
        }

        public override void draw(SpriteBatch b)
        {
            int menuW = 700;
            int menuH = 500;
            int menuX = (width - menuW) / 2;
            int menuY = (height - menuH) / 2;

            // ── Background ──
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, width, height), Color.Black * 0.5f);

            // ── Menu frame ──
            var menuRect = new Rectangle(menuX, menuY, menuW, menuH);
            DrawTexturedBox(b, menuRect, new Color(100, 80, 55));

            // ── Tabs ──
            DrawTab(b, _depositTab, "放入物品", _currentTab == 0);
            DrawTab(b, _pendingTab, $"等待中 ({_pendingItems.Count})", _currentTab == 1);
            DrawTab(b, _historyTab, $"已完成 ({_historyItems.Count})", _currentTab == 2);

            // ── Close button ──
            DrawButton(b, _closeButton, "X");

            // ── Title ──
            string title = "互 惠 篮";
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            b.DrawString(Game1.dialogueFont, title,
                new Vector2(menuX + menuW / 2 - titleSize.X / 2, menuY + 10),
                new Color(220, 190, 140));

            // ── Content by tab ──
            switch (_currentTab)
            {
                case 0: DrawDepositTab(b, menuX, menuY, menuW, menuH); break;
                case 1: DrawPendingTab(b, menuX, menuY, menuW, menuH); break;
                case 2: DrawHistoryTab(b, menuX, menuY, menuW, menuH); break;
            }

            drawMouse(b);
        }

        private void DrawDepositTab(SpriteBatch b, int menuX, int menuY, int menuW, int menuH)
        {
            int y = menuY + 50;
            int x = menuX + 20;

            // ── Instruction ──
            string instr = "从背包中选择一件物品放入互惠篮。明天可能会有人来交换。";
            b.DrawString(Game1.smallFont, instr, new Vector2(x, y), new Color(200, 180, 150), 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            y += 30;

            // ── Player inventory ──
            if (Game1.player != null)
            {
                var items = Game1.player.Items.Where(i => i != null).ToList();
                for (int i = 0; i < Math.Min(items.Count, ItemsPerPage * 2); i++)
                {
                    var item = items[i + _scrollOffset];
                    int col = i % 2;
                    int row = i / 2;
                    int itemX = x + col * (menuW / 2 - 20);
                    int itemY = y + row * 50;

                    bool selected = _selectedInventoryIndex == i + _scrollOffset;
                    var itemRect = new Rectangle(itemX, itemY, menuW / 2 - 30, 40);

                    // Item background
                    Color bgColor = selected ? new Color(140, 120, 80) : new Color(80, 60, 40, 150);
                    b.Draw(Game1.fadeToBlackRect, itemRect, bgColor);

                    // Item icon (if available)
                    item.drawInMenu(b, new Vector2(itemX + 2, itemY + 2), 0.7f);

                    // Item name
                    string itemName = $"{item.DisplayName} x{item.Stack}";
                    if (item.Quality > 0) itemName += " +";
                    b.DrawString(Game1.smallFont, itemName, new Vector2(itemX + 60, itemY + 10),
                        selected ? Color.White : new Color(200, 180, 150), 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
                }
            }

            // ── Deposit button ──
            DrawButton(b, _depositButton, "放入互惠篮");
        }

        private void DrawPendingTab(SpriteBatch b, int menuX, int menuY, int menuW, int menuH)
        {
            int y = menuY + 50;
            int x = menuX + 20;

            if (_pendingItems.Count == 0)
            {
                b.DrawString(Game1.smallFont, "互惠篮里还没有等待交换的物品。放入一些东西，也许明天会有人需要它。",
                    new Vector2(x, y), new Color(180, 160, 130));
                return;
            }

            for (int i = _scrollOffset; i < Math.Min(_pendingItems.Count, _scrollOffset + ItemsPerPage); i++)
            {
                var item = _pendingItems[i];
                int itemY = y + (i - _scrollOffset) * 45;

                string itemName = ResolveItemName(item.ItemId);
                string display = $"- {itemName} x{item.Count}";
                if (item.Quality > 0) display += " +";
                display += $" -- 还有 {item.DaysUntilReturn} 天等待时间";

                b.DrawString(Game1.smallFont, display, new Vector2(x, itemY), new Color(200, 180, 150), 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);

                if (!string.IsNullOrEmpty(item.RequestNote))
                {
                    b.DrawString(Game1.tinyFont, $"  纸条: \"{StringHelper.Truncate(item.RequestNote, 40)}\"",
                        new Vector2(x + 10, itemY + 20), new Color(160, 140, 120));
                }
            }
        }

        private void DrawHistoryTab(SpriteBatch b, int menuX, int menuY, int menuW, int menuH)
        {
            int y = menuY + 50;
            int x = menuX + 20;

            if (_historyItems.Count == 0)
            {
                b.DrawString(Game1.smallFont, "还没有完成过交换。把物品放进互惠篮，也许有人会来交换。",
                    new Vector2(x, y), new Color(180, 160, 130));
                return;
            }

            for (int i = _scrollOffset; i < Math.Min(_historyItems.Count, _scrollOffset + ItemsPerPage); i++)
            {
                var item = _historyItems[i];
                int itemY = y + (i - _scrollOffset) * 40;

                string givenName = ResolveItemName(item.ItemId);
                string receivedName = ResolveItemName(item.ReceivedItemId ?? "");
                string display = $"{item.MatchedNpcName} 换走了 {givenName}，留下了 {receivedName}";
                b.DrawString(Game1.smallFont, display, new Vector2(x, itemY), new Color(200, 180, 150), 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);

                if (!string.IsNullOrEmpty(item.ExchangeNote))
                {
                    b.DrawString(Game1.tinyFont, item.ExchangeNote,
                        new Vector2(x + 10, itemY + 20), new Color(160, 140, 120), 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawTab(SpriteBatch b, Rectangle rect, string text, bool active)
        {
            Color bgColor = active ? new Color(160, 130, 90) : new Color(100, 80, 55);
            b.Draw(Game1.fadeToBlackRect, rect, bgColor);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2),
                active ? new Color(220, 180, 120) : new Color(80, 60, 40));

            Vector2 textSize = Game1.smallFont.MeasureString(text);
            b.DrawString(Game1.smallFont, text,
                new Vector2(rect.Center.X - textSize.X / 2, rect.Center.Y - textSize.Y / 2),
                active ? Color.White : new Color(180, 150, 120));
        }

        private void DrawButton(SpriteBatch b, Rectangle rect, string text)
        {
            bool hover = rect.Contains(Game1.getMousePosition());
            Color bgColor = hover ? new Color(180, 150, 110) : new Color(140, 110, 70);
            b.Draw(Game1.fadeToBlackRect, rect, bgColor);

            Vector2 textSize = Game1.smallFont.MeasureString(text);
            b.DrawString(Game1.smallFont, text,
                new Vector2(rect.Center.X - textSize.X / 2, rect.Center.Y - textSize.Y / 2),
                Color.White * 0.9f);
        }

        private void DrawTexturedBox(SpriteBatch b, Rectangle rect, Color color)
        {
            b.Draw(Game1.fadeToBlackRect, rect, color);
            // Border
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Color(180, 150, 110));
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Color(180, 150, 110));
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Color(180, 150, 110));
            b.Draw(Game1.fadeToBlackRect, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Color(180, 150, 110));
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            // Tabs
            if (_depositTab.Contains(x, y)) { _currentTab = 0; _scrollOffset = 0; Game1.playSound("drumkit6"); return; }
            if (_pendingTab.Contains(x, y)) { _currentTab = 1; _scrollOffset = 0; Game1.playSound("drumkit6"); return; }
            if (_historyTab.Contains(x, y)) { _currentTab = 2; _scrollOffset = 0; Game1.playSound("drumkit6"); return; }
            if (_closeButton.Contains(x, y)) { exitThisMenu(); Game1.playSound("bigSelect"); return; }

            // Deposit button
            if (_depositButton.Contains(x, y) && _selectedInventoryIndex >= 0 && Game1.player != null)
            {
                var items = Game1.player.Items.Where(i => i != null).ToList();
                if (_selectedInventoryIndex < items.Count)
                {
                    var item = items[_selectedInventoryIndex];
                    bool success = _basket.DepositItem(item.QualifiedItemId, item.Quality, 1);

                    if (success)
                    {
                        Game1.playSound("coin");
                        Game1.addHUDMessage(new HUDMessage($"{item.DisplayName} 放入了互惠篮。", HUDMessage.newQuest_type));
                    }
                    _selectedInventoryIndex = -1;
                }
                return;
            }

            // Inventory item click (deposit tab)
            if (_currentTab == 0 && Game1.player != null)
            {
                int menuW = 700;
                int menuX = (width - menuW) / 2;
                int menuY = (height - 500) / 2;
                int startY = menuY + 80;

                var items = Game1.player.Items.Where(i => i != null).ToList();
                for (int i = _scrollOffset; i < Math.Min(items.Count, _scrollOffset + ItemsPerPage * 2); i++)
                {
                    int col = (i - _scrollOffset) % 2;
                    int row = (i - _scrollOffset) / 2;
                    int itemX = menuX + 20 + col * (menuW / 2 - 20);
                    int itemY = startY + row * 50;

                    if (new Rectangle(itemX, itemY, menuW / 2 - 30, 40).Contains(x, y))
                    {
                        _selectedInventoryIndex = i;
                        Game1.playSound("shwip");
                        return;
                    }
                }
            }

        }

        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        public override void receiveScrollWheelAction(int direction)
        {
            _scrollOffset = Math.Max(0, _scrollOffset - direction);
        }

        public override void receiveKeyPress(Microsoft.Xna.Framework.Input.Keys key) { }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            CalculateLayout();
        }

        /// <summary>Resolve a qualified item ID to a human-readable display name</summary>
        private static string ResolveItemName(string qualifiedId)
        {
            if (string.IsNullOrEmpty(qualifiedId)) return "某样东西";
            try
            {
                var item = ItemRegistry.Create(qualifiedId);
                return item?.DisplayName ?? qualifiedId;
            }
            catch
            {
                return qualifiedId;
            }
        }
    }
}
