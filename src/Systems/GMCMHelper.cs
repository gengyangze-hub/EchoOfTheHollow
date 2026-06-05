using System;
using StardewModdingAPI;

namespace EchoesOfTheHollow.Systems
{
    /// <summary>
    /// Generic Mod Config Menu integration via reflection-based API access.
    /// Avoids hard IManifest dependency in case SMAPI 4.x has renamed types.
    /// </summary>
    internal static class GMCMHelper
    {
        public static void Register(IModRegistry registry, IModHelper helper, ModConfig config, IMonitor monitor)
        {
            if (!registry.IsLoaded("spacechase0.GenericModConfigMenu"))
            {
                monitor.Log("[GMCM] Generic Mod Config Menu not found — skipping.", LogLevel.Debug);
                return;
            }

            try
            {
                // Get GMCM API as object — avoid IManifest dependency issues
                var api = registry.GetApi<object>("spacechase0.GenericModConfigMenu");
                if (api == null)
                {
                    monitor.Log("[GMCM] Could not get GMCM API.", LogLevel.Warn);
                    return;
                }

                var apiType = api.GetType();

                // Use the helper's Reflection API to get the mod manifest safely
                // In SMAPI 4.x, the Mod class has a ModManifest property
                object? manifest = null;
                try
                {
                    // Try to get manifest via reflection from the ModEntry instance
                    var modType = typeof(ModEntry).BaseType; // StardewModdingAPI.Mod
                    var manifestProp = modType?.GetProperty("ModManifest");
                    if (manifestProp != null && ModEntry.Instance != null)
                    {
                        manifest = manifestProp.GetValue(ModEntry.Instance);
                    }
                }
                catch { /* Fall through — manifest may be null */ }

                // Fallback: try helper.ModRegistry
                if (manifest == null)
                {
                    try
                    {
                        var manifestProp = helper.GetType().GetProperty("ModManifest");
                        manifest = manifestProp?.GetValue(helper);
                    }
                    catch { }
                }

                // If still null, try using ModEntry.ModManifest via reflection
                if (manifest == null)
                {
                    try
                    {
                        manifest = typeof(Mod).GetProperty("ModManifest")?.GetValue(ModEntry.Instance);
                    }
                    catch { }
                }

                if (manifest == null)
                {
                    monitor.Log("[GMCM] Could not resolve mod manifest — GMCM integration skipped.", LogLevel.Warn);
                    return;
                }

                // Register the mod with GMCM
                Invoke(api, apiType, "Register", new object?[] { manifest, null, null, false });

                // ── Helper delegates for cleaner calls ──
                void AddSection(string text)
                    => Invoke(api, apiType, "AddSectionTitle", new object?[] { manifest, (Func<string>)(() => text), null });

                void AddBool(Func<bool> get, Action<bool> set, string name, string? tooltip = null)
                    => Invoke(api, apiType, "AddBoolOption", new object?[] { manifest, get, set, (Func<string>)(() => name), tooltip != null ? (Func<string>)(() => tooltip) : null, null });

                void AddText(Func<string> get, Action<string> set, string name, string? tooltip = null)
                    => Invoke(api, apiType, "AddTextOption", new object?[] { manifest, get, set, (Func<string>)(() => name), tooltip != null ? (Func<string>)(() => tooltip) : null, null, null, null });

                void AddInt(Func<int> get, Action<int> set, string name, string? tooltip = null, int? min = null, int? max = null, int? interval = null)
                    => Invoke(api, apiType, "AddNumberOption", new object?[] { manifest, get, set, (Func<string>)(() => name), tooltip != null ? (Func<string>)(() => tooltip) : null, min, max, interval, null, null });

                void AddFloat(Func<float> get, Action<float> set, string name, string? tooltip = null, float? min = null, float? max = null, float? interval = null)
                    => Invoke(api, apiType, "AddNumberOption", new object?[] { manifest, get, set, (Func<string>)(() => name), tooltip != null ? (Func<string>)(() => tooltip) : null, min, max, interval, null, null });

                // ═══════════════════════════════════════════
                //  Core Toggles
                // ═══════════════════════════════════════════
                AddSection("核心系统");
                AddBool(() => config.EnableJournal, v => config.EnableJournal = v, "启用日志系统", "多棱镜日志——NPC 以自己独特的视角记录关于你的记忆。");
                AddBool(() => config.EnableBasket, v => config.EnableBasket = v, "启用互惠篮", "替代金币系统的礼物交换。");
                AddBool(() => config.EnableDriftBox, v => config.EnableDriftBox = v, "启用漂流物箱", "替代出货箱，物品漂向镇上各个角落。");
                AddBool(() => config.EnableEnthusiasm, v => config.EnableEnthusiasm = v, "启用兴致系统", "替代体力值，追踪活动多样性。");
                AddBool(() => config.EnableInvitations, v => config.EnableInvitations = v, "启用邀约留言柱", "替代任务板，NPC 发布休闲邀约。");
                AddBool(() => config.EnableFestivalOverrides, v => config.EnableFestivalOverrides = v, "启用节日替换", "4 个传统节日回归简单共处。");
                AddBool(() => config.EnableOfflineMemories, v => config.EnableOfflineMemories = v, "启用离线记忆", "现实时间流逝后生成「你不在时」的记忆。");

                // ═══════════════════════════════════════════
                //  Activity Toggles
                // ═══════════════════════════════════════════
                AddSection("活动系统");
                AddBool(() => config.EnableDaydreaming, v => config.EnableDaydreaming = v, "发呆", "静止站立时自动坐下，镜头慢慢拉远。恢复兴致。");
                AddBool(() => config.EnableWindListening, v => config.EnableWindListening = v, "听风", "站在风景好的特定位置，感受到山谷的声音。");
                AddBool(() => config.EnableOldObjects, v => config.EnableOldObjects = v, "触摸旧物", "发现散落在各地的旧物件，倾听它们的故事。");
                AddBool(() => config.EnableAnimalTracking, v => config.EnableAnimalTracking = v, "追踪动物", "接近动物时生成观察日记。");
                AddBool(() => config.EnableMeaninglessItems, v => config.EnableMeaninglessItems = v, "无意义的物品", "制作并放置没有功能的物品，记录创作的心情。");
                AddBool(() => config.EnablePlayerDiary, v => config.EnablePlayerDiary = v, "玩家日记", "允许玩家在日志中写入日记。");

                // ═══════════════════════════════════════════
                //  Harmony Patch Toggles
                // ═══════════════════════════════════════════
                AddSection("补丁开关 (兼容性)");
                AddBool(() => config.PatchMoney, v => config.PatchMoney = v, "屏蔽金钱", "所有金币显示为 0。");
                AddBool(() => config.PatchSkills, v => config.PatchSkills = v, "屏蔽技能经验", "技能经验不再增长。");
                AddBool(() => config.PatchFriendship, v => config.PatchFriendship = v, "屏蔽好感度", "好感度心形不再显示。");
                AddBool(() => config.PatchAchievements, v => config.PatchAchievements = v, "屏蔽成就", "不弹出成就提示。");
                AddBool(() => config.PatchShippingBin, v => config.PatchShippingBin = v, "出货箱→漂流箱", "物品不再卖出，漂流到镇上各地。");
                AddBool(() => config.PatchQuestBoard, v => config.PatchQuestBoard = v, "任务板→留言柱", "每日任务被邀约替代。");
                AddBool(() => config.PatchSocialMenu, v => config.PatchSocialMenu = v, "社交界面→记忆之书", "社交标签变为 NPC 记忆浏览。");
                AddBool(() => config.PatchCollections, v => config.PatchCollections = v, "屏蔽收藏标签", "移除收藏/成就标签页。");
                AddBool(() => config.PatchHud, v => config.PatchHud = v, "屏蔽金钱 HUD", "屏幕右上角的金币图标和计数器不再显示。");
                AddBool(() => config.PatchShops, v => config.PatchShops = v, "商店→好感交换", "商店物品用好感度换，不是免费。");
                AddBool(() => config.PatchEvents, v => config.PatchEvents = v, "事件无前置条件", "节日和剧情事件不再需要金钱/好感度。");
                AddBool(() => config.PatchDialogue, v => config.PatchDialogue = v, "清理对话标记", "移除对话中的 $ 代币。");

                // ═══════════════════════════════════════════
                //  Journal Settings
                // ═══════════════════════════════════════════
                AddSection("日志设置");
                AddInt(() => config.MaxJournalEntries, v => config.MaxJournalEntries = v, "最大日志条数", "50-2000", 50, 2000, 50);
                AddInt(() => config.NightlyMemoryCount, v => config.NightlyMemoryCount = v, "每晚生成记忆数", "1-5", 1, 5, 1);
                AddBool(() => config.JournalAutoOpenOnNew, v => config.JournalAutoOpenOnNew = v, "新记忆自动打开日志");
                AddText(() => config.JournalKeybind, v => config.JournalKeybind = v, "日志快捷键", "按下此键打开回音日志（如 J）。");

                // ═══════════════════════════════════════════
                //  Basket Settings
                // ═══════════════════════════════════════════
                AddSection("互惠篮设置");
                AddInt(() => config.BasketReturnDays, v => config.BasketReturnDays = v, "物品归还天数", "1-7", 1, 7, 1);
                AddInt(() => config.BasketExchangeCheckHour, v => config.BasketExchangeCheckHour = v, "交换检查时间", "6-12", 6, 12, 1);
                AddBool(() => config.ShowBasketNotification, v => config.ShowBasketNotification = v, "互惠篮通知");

                // ═══════════════════════════════════════════
                //  Shop Goodwill Settings
                // ═══════════════════════════════════════════
                AddSection("商店好感设置");
                AddBool(() => config.EnableShopGoodwill, v => config.EnableShopGoodwill = v, "启用好感商店", "关闭后商店物品完全免费。");
                AddInt(() => config.MaxDailyShopRequests, v => config.MaxDailyShopRequests = v, "每日请求上限", "每天能从商店请求多少件物品。", 1, 20, 1);
                AddFloat(() => config.GoodwillCostMultiplier, v => config.GoodwillCostMultiplier = v, "好感消耗倍率", "原价×倍率=好感成本。0.01便宜，0.05昂贵。", 0.001f, 0.1f, 0.001f);
                AddBool(() => config.ShowGoodwillCost, v => config.ShowGoodwillCost = v, "显示好感成本", "商店提示中显示好感消耗。");

                // ═══════════════════════════════════════════
                //  Enthusiasm Settings
                // ═══════════════════════════════════════════
                AddSection("兴致设置");
                AddFloat(() => config.EnthusiasmDecayRate, v => config.EnthusiasmDecayRate = v, "兴致衰减速率", "0.001-0.05", 0.001f, 0.05f, 0.001f);
                AddFloat(() => config.EnthusiasmDaydreamRecovery, v => config.EnthusiasmDaydreamRecovery = v, "发呆恢复速率", "0.001-0.05", 0.001f, 0.05f, 0.001f);
                AddFloat(() => config.EnthusiasmWindRecovery, v => config.EnthusiasmWindRecovery = v, "听风恢复速率", "0.001-0.05", 0.001f, 0.05f, 0.001f);
                AddBool(() => config.ShowEnthusiasmHud, v => config.ShowEnthusiasmHud = v, "显示兴致 HUD");
                AddText(() => config.EnthusiasmHudPosition, v => config.EnthusiasmHudPosition = v, "兴致 HUD 位置", "Left 或 Right");

                // ═══════════════════════════════════════════
                //  Offline Settings
                // ═══════════════════════════════════════════
                AddSection("离线设置");
                AddInt(() => config.MinOfflineHoursForMemories, v => config.MinOfflineHoursForMemories = v, "最小离线时长(小时)", "1-168", 1, 168, 1);
                AddInt(() => config.MaxOfflineMemories, v => config.MaxOfflineMemories = v, "最大离线记忆数", "1-20", 1, 20, 1);

                // ═══════════════════════════════════════════
                //  Visual Settings
                // ═══════════════════════════════════════════
                AddSection("视觉设置");
                AddFloat(() => config.JournalPageAgingSpeed, v => config.JournalPageAgingSpeed = v, "日志纸张泛黄速度", "0-1", 0f, 1f, 0.1f);
                AddBool(() => config.ShowWeatherEffectsOnJournal, v => config.ShowWeatherEffectsOnJournal = v, "日志天气效果");
                AddBool(() => config.UseNpcColorCoding, v => config.UseNpcColorCoding = v, "NPC 颜色编码");
                AddFloat(() => config.UiScale, v => config.UiScale = v, "界面缩放", "0.5-2.0", 0.5f, 2.0f, 0.1f);

                // ═══════════════════════════════════════════
                //  Back Door
                // ═══════════════════════════════════════════
                AddSection("🔑 记忆后门");
                AddBool(() => config.LoadCustomTemplates, v => config.LoadCustomTemplates = v, "加载自定义模板");
                AddText(() => config.CustomTemplatesPath, v => config.CustomTemplatesPath = v, "模板目录");
                AddBool(() => config.AllowContentPatcherTemplates, v => config.AllowContentPatcherTemplates = v, "允许 Content Patcher 模板");
                AddBool(() => config.EnableAccessAPI, v => config.EnableAccessAPI = v, "启用公共 API");

                // ═══════════════════════════════════════════
                //  Debug
                // ═══════════════════════════════════════════
                AddSection("调试");
                AddBool(() => config.DebugMode, v => config.DebugMode = v, "调试模式");
                AddBool(() => config.ShowTriggerLog, v => config.ShowTriggerLog = v, "显示触发日志");

                monitor.Log("[GMCM] Successfully registered all config options.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                monitor.Log($"[GMCM] Failed to register config menu: {ex.Message}\n{ex.StackTrace}", LogLevel.Warn);
            }
        }

        /// <summary>Invoke a method on an object via reflection</summary>
        private static void Invoke(object target, Type targetType, string methodName, object?[] parameters)
        {
            var method = targetType.GetMethod(methodName);
            if (method != null)
            {
                // Find the right overload by parameter count
                var methods = targetType.GetMethods();
                foreach (var m in methods)
                {
                    if (m.Name == methodName && m.GetParameters().Length == parameters.Length)
                    {
                        m.Invoke(target, parameters);
                        return;
                    }
                }
            }
        }
    }
}
