# 空谷回音 / Echoes of the Hollow

一个星露谷物语 SMAPI 模组，将鹈鹕镇改造为一个没有金钱、技能等级、成就和好感度数值的世界。取而代之的是 **多棱镜日志系统**——每个 NPC 用自己独特的视角和语言风格记录关于你的主观记忆碎片。

> "好感度心形完全移除。代之以每个NPC对你的'记忆清晰度'——仅后台使用，影响日志的细节和频率。" —— 设计文档

---

## 目录

- [核心理念](#核心理念)
- [游戏系统](#游戏系统)
  - [回音日志 — 多棱镜记忆](#回音日志--多棱镜记忆)
  - [记忆之书 — 社交界面替代](#记忆之书--社交界面替代)
  - [互惠篮 — 礼物经济](#互惠篮--礼物经济)
  - [漂流物箱 — 出货箱替代](#漂流物箱--出货箱替代)
  - [好感商店 — 善意交换](#好感商店--善意交换)
  - [兴致系统 — 体力替代](#兴致系统--体力替代)
  - [记忆清晰度 — 好感度替代](#记忆清晰度--好感度替代)
  - [NPC 主动互动](#npc-主动互动)
  - [邀约留言柱 — 任务板替代](#邀约留言柱--任务板替代)
  - [节日重制](#节日重制)
  - [离线记忆](#离线记忆)
  - [活动系统](#活动系统)
- [安装与配置](#安装与配置)
- [技术架构](#技术架构)
  - [整体架构](#整体架构)
  - [Harmony 补丁系统](#harmony-补丁系统)
  - [数据流与生命周期](#数据流与生命周期)
  - [modData 存储策略](#moddata-存储策略)
  - [记忆模板引擎 — "后门"系统](#记忆模板引擎--后门系统)
  - [NPC 语音渲染管线](#npc-语音渲染管线)
  - [UI 纯代码渲染](#ui-纯代码渲染)
  - [GMCM 反射集成](#gmcm-反射集成)
- [代码结构](#代码结构)
- [数据文件](#数据文件)
- [开发与构建](#开发与构建)
- [存档安全](#存档安全)
- [许可证](#许可证)

---

## 核心理念

模组的设计遵循以下原则：

| 原则 | 说明 |
|------|------|
| **无数字评判** | 移除金币计数器、技能等级、好感度心形、成就——玩家通过NPC的文字来感知自己的进展 |
| **主观叙事** | 同一个事件会被不同的NPC以各自独特的语言风格记录——没有"客观"的日志 |
| **仅后台数值** | 所有机制数据（好感度、记忆清晰度、善意成本）仅后台使用，从不向玩家显示数字 |
| **叙事替代机制** | 每一种被移除的香草机制都有一个叙事化的替代品——不是隐藏，是转化 |
| **存档零侵入** | 所有模组状态存储在 `player.modData` 中，不修改香草存档数据 |
| **数据驱动** | 记忆模板、NPC语音档案、听风点均可通过 JSON 扩展，无需重新编译 |

---

## 游戏系统

### 回音日志 — 多棱镜记忆

**快捷键: `J`**

一本手订皮质日志本。每个 NPC 从自己的视角、用自己的语言风格写下关于你的记忆。打开日志，你会看到谁在什么时候想到了你、观察到了你——这不是一本客观的记录，而是一本由整个镇子共同书写的印象集。

**UI 特性 (纯代码绘制):**
- 皮质封面 + 缝线纹理
- 做旧纸张（随时间泛黄，速度可配置）
- 随机墨水渍和水渍
- 书脊脊带装饰
- 角花装饰 + 装饰性分割线
- 📌 **记忆标记**: 给重要日期别上大头针，届时自动 HUD 提醒

**操作:**
- `◀ ▶` 翻页 / 滚轮
- 🔍 按 NPC 筛选
- ↓↑ 按时间排序
- ✎ 写日记（玩家自己的条目）
- 📌 标记/取消标记当前页日期

### 记忆之书 — 社交界面替代

**快捷键: `H`**

替代香草社交菜单。左侧是村民列表（颜色编码），右侧展示选中 NPC 写下的记忆。不是心形和数字——而是他们如何记住你。

**UI 特性:**
- 布纹封面 + 丝带书签（波浪动画）
- 干花装饰
- NPC 按情绪符号分组
- 语音档案片段展示（"艾芙琳奶奶用饼干和旧照片来理解世界"）

### 互惠篮 — 礼物经济

**快捷键: `B`**

替代金币商店。放入一件物品，附一张纸条说明想要什么作为交换——然后等。某人可能会来换，也可能不会。

**三个标签页:**
1. **放入物品** — 从背包选择 + 可写交换纸条
2. **等待中** — 显示活跃的交换请求和剩余天数
3. **已完成** — 历史交换记录

**机制:**
- 物品在 `BasketReturnDays` 天后无人认领则退回
- 每天早上 `BasketExchangeCheckHour` 点处理交换
- `ExchangeMatcher` 根据 NPC 偏好表评估匹配
- 每个 NPC 有独特的交换回应文本（~120 条中文叙事项）

### 漂流物箱 — 出货箱替代

将物品放入出货箱时，它们不会被"卖掉"——而是漂流到镇上的随机角落，可能被某个 NPC 捡到。发现者会写一篇日志记录。

- 11 个漂流目的地（城镇、森林、山区、海滩、巴士站）
- 30% 每日发现概率
- 2-4 天漂流时间
- 14 天自动清理旧记录

### 好感商店 — 善意交换

在商店购买物品时，不花金币——而是消耗与该商店 NPC 的好感度。后台计算，叙事化反馈。

**流程:**
1. 玩家点击购买（金币价格已被 Harmony patch 归零）
2. `ShopPatches` 拦截点击，计算 `goodwillCost = price × GoodwillCostMultiplier`
3. 检查对该店主的好感度是否足够
4. 足够 → 扣除好感度，显示叙事化确认消息
5. 不够 → 显示叙事化拒绝消息（"多一些相处，少一些交换——慢慢来"）
6. 每日请求上限 `MaxDailyShopRequests`

**好感度叙事层级 (DescribeFriendship):**
- 0-100: "对你还很陌生"
- 250-500: "开始认识你了"
- 750-1000: "对你印象不错"
- 1250-1500: "和你非常亲近"
- 2000+: "与你心照不宣"

**商店-NPC 映射:** Pierre/皮埃尔, Robin/罗宾, Clint/克林特, Marnie/玛妮, Willy/威利, Gus/格斯, Harvey/哈维, Sandy/桑迪, Krobus/科罗布斯, Dwarf/矮人, Wizard/法师, Marlon/马龙

### 兴致系统 — 体力替代

替代体力值 / 能量条。兴致因活动重复而衰减，通过活动多样性、发呆和听风恢复。

**HUD 显示:** 一个小标签"兴致"，替换原体力条位置。从不显示数值。

**机制:**
- `RecordAction(actionType)` — 每次劳作衰减兴致
- 重复惩罚: 10 次同样行动后每额外行动 +5% 衰减
- 被动恢复: 站立不动时每小时微量恢复
- 发呆恢复: `EnthusiasmDaydreamRecovery` / tick
- 听风恢复: `EnthusiasmWindRecovery × spot multiplier` / tick

### 记忆清晰度 — 好感度替代

**仅后台使用，从不显示为数字。** 每个 NPC 对玩家有一个 0-100 的"记忆清晰度"值，影响日志条目的细节程度和触发频率。

**7 个叙事层级:**
| 层级 | 范围 | 叙事描述 |
|------|------|----------|
| Forgotten | 0-5 | "似乎已经忘记了你的存在" |
| Blurred | 5-15 | "对你只有一个模糊的影子" |
| Faint | 15-30 | "记得一点点关于你的事，像隔着一层雾" |
| Recognized | 30-50 | "清楚地记得你是谁" |
| Clear | 50-70 | "能回忆起很多关于你的细节" |
| Vivid | 70-90 | "对你的记忆鲜活而具体" |
| DeeplyEngraved | 90-100 | "把你记得很深——像刻在木头上的名字" |

**机制:**
- 每次互动 +1.5～3.0 清晰度
- 每日衰减 0.5
- 60 天无互动 → 条目从追踪中移除
- 清晰度 > 50 → 可能生成详细条目
- 清晰度影响每日条目生成频率 (0～3 条)

### NPC 主动互动

NPC 根据自己的性格主动与玩家互动——走在路上，他们可能会对你招手、打招呼、聊天、送小东西，或者只是看你一眼。

**33 个 NPC 交互档案**，每个定义:
- `frequency` — 互动频率 (0.1 Sebastian 躲着人 ~ 0.8 Sandy 渴望访客)
- `approachChance` — 接近概率
- `prefersDistance` — 是否保持距离

**5 种互动类型:**
1. **Wave** — 远处招手（频率最高）
2. **Greet** — 走近打招呼，带 NPC 特定对话
3. **Chat** — 停下来聊几句
4. **Gift** — 送一个小东西（频率最低）
5. **Notice** — 看了一眼，然后继续做自己的事

**冷却机制:**
- 全局冷却: 游戏内 30 分钟
- 每个 NPC 冷却: 游戏内 120 分钟
- 每日总计上限: 可配置

### 邀约留言柱 — 任务板替代

替代求助任务板。NPC 在留言柱上留下想一起做的事——没有奖励、没有截止日期、没有压力。

**10 个预设邀约模板:**
| NPC | 活动 | 地点 |
|-----|------|------|
| Abigail | 一起去矿洞边捡石头 | Mountain |
| Emily | 在树下看云的形状 | Town |
| Gus | 来红鹤食堂尝一道新菜 | Town |
| Leah | 在河边画画，可以一起 | Forest |
| Linus | 坐在帐篷外听风 | Mountain |
| Willy | 去海边等那条传说中的鱼 | Beach |
| Robin | 看看新做的木工活 | Mountain |
| Maru | 测试一个故意不完美的装置 | Mountain |
| Clint | 修复一件旧铁器 | Town |
| Pam | 傍晚一起在河边坐坐 | Town |

### 节日重制

4 个香草节日被替换为叙事化活动:
| 原节日 | 替换为 | 叙事描述 |
|--------|--------|----------|
| 复活节 (spring13) | 拼图节 | 每人带来有意义的小物件，拼出一幅星露谷的图画 |
| 花舞节 (spring24) | 共缝花毡 | 用干花和布片一起缝一张巨大的花毡 |
| 月光水母节 (summer28) | 放灯拾愿 | 在海边放灯，看光在水上走 |
| 冬日星盛宴 (winter25) | 秘名交换 | 抽签送礼物，把秘密留在盒子里 |

### 离线记忆

当你在现实中离开游戏一段时间后回来，NPC 会注意到你"不在"。`OfflineTimeManager` 记录上次游玩时间戳，下次加载存档时生成"你不在时"的记忆。

- `MinOfflineHoursForMemories`: 最少离线小时（默认 12）
- `MaxOfflineMemories`: 最多生成条数（默认 5）
- 模板选择基于离线时长 + NPC 个性

### 活动系统

**发呆 (Daydreaming)** — 站立不动 2 秒自动触发。兴致恢复。观察性 NPC 可能写下日志。

**听风 (WindListening)** — 8 个听风点分布在星露谷各处（柳树下、山顶、海边悬崖、广场中央、法师塔旁、农场角落、湖边、巴士站），每个有不同的恢复倍率。站上去触发听风，兴致恢复，生成日志。

**触摸旧物 (OldObjectInteraction)** — 与隐藏遗迹互动，格鲁德风格的日志条目。

**追踪动物 (AnimalTrackingSystem)** — 检测附近农场动物和野生动物，生成温暖的观察日志。每日最多 3 条。

**无意义物品 (MeaninglessItemSystem)** — 制作并放置"无用途"的物品，反思"无用之美"。

---

## 安装与配置

### 安装

1. 安装 [SMAPI](https://smapi.io) 4.0+
2. 下载本模组，放入 `Stardew Valley/Mods/EchoesOfTheHollow/`
3. 启动游戏

### 配置

通过 **Generic Mod Config Menu**（推荐）或手动编辑 `config.json`。共 50+ 可配置选项，详见 [ModConfig.cs](ModConfig.cs)。

**核心开关:**
```json
{
  "EnableJournal": true,
  "EnableBasket": true,
  "EnableDriftBox": true,
  "EnableEnthusiasm": true,
  "EnableInvitations": true,
  "EnableFestivalOverrides": true,
  "EnableOfflineMemories": true
}
```

**Harmony 补丁开关 (兼容性):** 12 个独立开关 (`PatchMoney`, `PatchSkills`, `PatchFriendship`, ...)，当与其他模组冲突时可选择性关闭。

---

## 技术架构

### 整体架构

```
EchoesOfTheHollow.dll
│
├── ModEntry.cs              ← 入口点 / 编排器 (单例)
│   ├── 配置验证 (clamp 到安全范围)
│   ├── 14 个 SMAPI 事件处理程序
│   └── 系统初始化 (按依赖顺序)
│
├── src/NPC/
│   └── VoiceRegistry.cs     ← NPC 语音档案加载与查询
│
├── src/Systems/Journal/
│   ├── JournalSystem.cs      ← 核心日志 CRUD (线程安全 lock)
│   ├── JournalFilter.cs      ← 日志筛选/排序
│   ├── MemoryTemplateEngine.cs ← 模板选择 + {{参数}} 替换引擎
│   ├── MemoryClaritySystem.cs  ← 后台好感度替代 (静态全局)
│   ├── TriggerDetector.cs    ← 事件触发 → 模板匹配 → 日志生成
│   └── PlayerDiarySystem.cs  ← 玩家日记写入
│
├── src/Systems/Economy/
│   ├── BasketSystem.cs       ← 互惠篮存取/交换处理
│   ├── DriftBoxSystem.cs     ← 漂流物追踪/NPC发现
│   ├── ExchangeMatcher.cs    ← NPC交换偏好匹配表
│   └── GoodwillShopSystem.cs ← 商店好感度购买逻辑
│
├── src/Systems/Enthusiasm/
│   └── EnthusiasmSystem.cs   ← 兴致值追踪 (替代体力)
│
├── src/Systems/Activities/
│   ├── DaydreamingSystem.cs  ← 站立不动检测 → 发呆状态
│   ├── WindListeningSystem.cs← 特定坐标 → 听风状态
│   ├── NPCActiveGreeting.cs  ← NPC主动互动AI
│   └── OtherActivities.cs    ← 旧物/动物/无意义物品
│
├── src/Systems/Invitation/
│   └── InvitationSystem.cs   ← 留言柱邀约系统
│
├── src/Systems/Festival/
│   └── FestivalReplacer.cs   ← 节日名称/描述替换
│
├── src/Systems/Offline/
│   └── OfflineTimeManager.cs ← 现实时间流逝检测
│
├── src/Systems/
│   └── GMCMHelper.cs         ← 反射式GMCM集成 (无硬依赖)
│
├── src/Patches/              ← 12 组 Harmony 补丁
│   ├── HarmonyPatcher.cs     ← 补丁协调器 (逐个注册+统计)
│   ├── FarmerPatches.cs      ← 金币getter保留 + XP获取→false
│   ├── HudPatches.cs         ← DrawMoneyBox→return false
│   ├── NpcFriendshipPatches.cs ← 心形显示补丁
│   ├── AchievementPatches.cs ← 成就通知抑制
│   ├── ShippingBinPatches.cs ← 出货→DriftBox重路由
│   ├── ShopPatches.cs        ← 价格归零 + 好感度拦截
│   └── RemainingPatches.cs   ← 事件/对话/社交菜单/收集/任务板
│
├── src/UI/                   ← 纯代码UI (无纹理)
│   ├── EchoJournalMenu.cs    ← 皮质日志本 (~780行绘制代码)
│   ├── MemoryBookMenu.cs     ← 布纹记忆书 (~630行绘制代码)
│   ├── BasketMenu.cs         ← 互惠篮界面 (~420行)
│   └── EnthusiasmHud.cs      ← 兴致HUD覆盖层
│
├── src/Utils/
│   ├── Log.cs                ← 日志包装器
│   ├── ModDataHelper.cs      ← modData JSON序列化 (key前缀)
│   ├── RandomHelper.cs       ← 带重复避免的种子随机
│   ├── StringHelper.cs       ← 文本处理/参数替换/换行
│   ├── Scheduler.cs          ← 游戏内时间调度器
│   └── ColorHelper.cs        ← 颜色插值工具
│
├── src/Data/                 ← POCO 数据模型 (全部 nullable enabled)
│   ├── JournalEntry.cs       ← 日志条目 (含计算属性 DisplayDate/EntryAgeDays)
│   ├── MemoryTemplate.cs     ← 模板数据模型 + 条件匹配
│   ├── NpcVoiceProfile.cs    ← NPC语音配置模型
│   ├── BasketItem.cs         ← 互惠篮物品模型
│   └── OtherModels.cs        ← DriftRecord, EnthusiasmState, Invitation等
│
└── assets/data/
    ├── NpcVoices.json         ← 35 个NPC语音档案 (~1000行)
    ├── MemoryTemplates.json   ← 130 个记忆模板 (~1400行)
    ├── WindListeningSpots.json ← 8 个听风点坐标
    └── templates/              ← "后门"自定义模板目录
```

### Harmony 补丁系统

所有补丁通过 `HarmonyPatcher.Apply()` 集中注册，逐个 try/catch 并统计成功/失败数量。

| 补丁组 | 目标方法 | 类型 | 效果 |
|--------|----------|------|------|
| **Money** | `Farmer.money` getter | — | 不补丁（保留后端金币，只隐藏UI） |
| **Skills** | `Farmer.gainExperience` | Prefix→false | 阻止所有技能经验获取 |
| **Friendship** | 心形渲染 | — | 心形不显示（好感度数据完整保留） |
| **Achievements** | 成就解锁通知 | Prefix→false | 抑制成就弹出 |
| **ShippingBin** | `ShipItem` | Prefix | 物品转向 DriftBox |
| **HUD** | `DayTimeMoneyBox.drawMoneyBox` | Prefix→false | 隐藏金币计数器 |
| **Shops** | `ShopMenu` 构造+点击 | Postfix | 价格归零 + 善意拦截 |
| **QuestBoard** | 任务板交互 | Prefix→false | 重定向到留言柱 |
| **SocialMenu** | `GameMenu.changeTab` | Prefix→false | 社交页→记忆之书 |
| **Collections** | 收集/成就页 | Prefix→false | 重定向到日志 |
| **Events** | `Event.preconditions` | Prefix (反射) | 移除 `/f` `/m` token，保留其余 |
| **Dialogue** | 对话文本 | Prefix | 移除 `$g` `$h` token，保留 `$q` `$r` 等 |

**关键设计决策:**
- `Farmer.money` **不补丁** — 金币在后端正常运作（美人鱼吊坠购买需5000g检查、Joja会员需50000g）。仅通过 `HudPatches` 隐藏UI。
- `Event.preconditions` 使用**反射**读取/修改条件字符串 — SDV 1.6 字段名可能是 `eventConditions` 或 `preconditions`。
- 任务板补丁和社交菜单补丁返回 `false` 以**完整阻断**原方法，防止竞态覆盖。

### 数据流与生命周期

```
游戏启动
  └→ Entry()
       ├→ ReadConfig() → ValidateConfig() → WriteConfig()
       ├→ 注册 14 个 SMAPI 事件处理程序
       ├→ 初始化 VoiceRegistry (加载 NpcVoices.json)
       ├→ 初始化 MemoryTemplateEngine (加载 MemoryTemplates.json + 自定义)
       ├→ 初始化 JournalSystem (注入引擎+语音)
       ├→ 初始化所有子系统 (按依赖顺序)
       └→ HarmonyPatcher.Apply() (注册 12 组补丁)

存档加载
  └→ OnSaveLoaded()
       ├→ MemoryClaritySystem.OnSaveLoaded()
       ├→ EchoJournalMenu.LoadMarks()
       └→ 每个子系统 .OnSaveLoaded() (从 player.modData 反序列化)

每日开始
  └→ OnDayStarted()
       ├→ GoodwillShopSystem.OnDayStarted() (重置每日计数器)
       ├→ MemoryClaritySystem.OnDayStarted() (应用清晰度衰减)
       ├→ ShowBasketMorningPrompt() (HUD消息)
       ├→ EchoJournalMenu.CheckDayMark() (📌日期提醒)
       ├→ BasketSystem.OnDayStarted() (清理+处理交换)
       ├→ DriftBoxSystem.OnDayStarted() (清理+NPC发现检测)
       └→ 各子系统 .OnDayStarted()

游戏循环 (每帧)
  └→ OnUpdateTicked()
       ├→ EnthusiasmSystem (检测空闲→被动恢复)
       ├→ DaydreamingSystem (检测静止→触发发呆)
       ├→ WindListeningSystem (检测位置→触发听风)
       ├→ AnimalTrackingSystem (检测附近动物→生成日志)
       └→ NpcActiveGreeting (检测附近NPC→概率互动)

每日结束
  └→ OnDayEnding()
       ├→ TriggerDetector.GenerateNightlyMemories()
       │    ├→ JournalSystem.GenerateNightlyMemories() (跨日观察模板)
       │    └→ 缺席检测 (3+天未互动 → 生成缺席记忆)
       └→ AnimalTrackingSystem.GenerateNightlyAnimalMemories()

存档保存
  └→ OnSaving()
       └→ 每个子系统 .OnSaving() (序列化到 player.modData)
```

### modData 存储策略

所有模组持久化数据存储在 `Game1.player.modData` 字典中，key 前缀 `EchoesHollow/`：

| Key | 内容 | 清理策略 |
|-----|------|----------|
| `EchoesHollow/Journal/v1` | `List<JournalEntry>` JSON | 条数超过 MaxJournalEntries 时裁剪 |
| `EchoesHollow/Basket/v1` | `List<BasketItem>` JSON | 30 天后清理非活跃记录 |
| `EchoesHollow/DriftBox/v1` | `List<DriftRecord>` JSON | 14 天已发现 / 30 天过期 |
| `EchoesHollow/Enthusiasm/v1` | `EnthusiasmState` JSON | 每日重置计数器 |
| `EchoesHollow/MemoryClarity/v1` | `ClaritySaveData` JSON | 60 天无互动条目移除 |
| `EchoesHollow/OfflineTime/v1` | 时间戳 | 每次存档更新 |
| `EchoesHollow/Invitation/v1` | 邀约状态 | 每日刷新 |
| `EchoesHollow/MemoryMarks/v1` | `HashSet<string>` (日期) | 手动管理 |

**`ModDataHelper` 工具类:**
```csharp
// 所有操作自动添加 "EchoesHollow/" 前缀，try/catch 防崩溃
ModDataHelper.Save<T>(key, value);     // 序列化为 JSON 写入 modData
ModDataHelper.Load<T>(key);            // 从 modData 反序列化
ModDataHelper.LoadList<T>(key);        // 列表安全初始化
ModDataHelper.HasKey(key);             // 存在检查
ModDataHelper.Remove(key);             // 删除
```

### 记忆模板引擎 — "后门"系统

这是模组最核心的可扩展性机制。任何人在 `assets/data/templates/` 下放入一个 `.json` 文件即可添加新记忆模板，**无需修改一行代码或重新编译**。

**模板结构:**
```json
{
  "templateId": "leah_player_forest_001",
  "npcName": "Leah",
  "triggerType": "Environmental",
  "priority": 5,
  "cooldownDays": 3,
  "conditions": {
    "locations": ["Forest"],
    "seasons": ["spring", "fall"],
    "weather": ["sunny"],
    "timeOfDay": ["morning", "afternoon"],
    "minOfflineHours": 12,
    "observedActions": ["farming", "mining"]
  },
  "textVariants": [
    "{{playerName}}在煤矿森林遇到了我。他蹲在一棵树旁边……",
    "另一段不同的文字变体……"
  ],
  "vocabularyHints": ["wood", "forest", "quiet"],
  "emotionTag": "Curiosity"
}
```

**模板选择算法 (MemoryTemplateEngine.SelectTemplate):**

1. 筛选匹配 `npcName`（或 `"Any"`）且 `triggerType` 匹配的模板
2. 检查 `conditions` — 位置、天气、季节、时间段、
3. 按 `priority` 降序排列
4. 过滤冷却中的模板（`cooldownDays`）
5. 使用 `RandomHelper.NextAvoiding()` 从候选中选择（避免重复）
6. 无匹配时回退到内置默认模板

**支持的触发器类型 (10 种):**
`DirectInteraction`, `Environmental`, `CrossDayObservation`, `Absence`, `SeasonalEvent`, `WeatherEvent`, `BasketExchange`, `DriftFind`, `OfflinePassage`, `Daydream`

**当前数据规模:**
- 130 个记忆模板
- 35 个 NPC 语音档案
- 10 种触发器类型各有 ≥3 个模板
- 覆盖所有香草好感 NPC + Gunther

### NPC 语音渲染管线

每个 NPC 有一个完整的"语音档案"定义其写作风格。日志条目生成时，`VoiceRegistry` 和 `MemoryTemplateEngine` 协作渲染文本：

```
触发事件 → TriggerDetector
  → MemoryTemplateEngine.SelectTemplate(npc, triggerType, conditions...)
    → 匹配模板 + 随机选择文本变体
    → StringHelper.ReplaceParameters(text, {playerName, location, weather...})
    → VoiceRegistry.GetProfile(npc).vocabularyMapping → StringHelper.ReplaceWords(text, wordMap)
    → 应用 stylisticRules (如有)
  → 生成 JournalEntry { NpcName, DisplayText, EmotionTag, Trigger... }
  → JournalSystem.AddEntry(entry)
```

**语音档案字段 (每个NPC 13 个字段):**
```json
{
  "npcName": "Leah",
  "writingStyle": "ArtisanDiary",
  "baseTone": "WarmIndependent",
  "metaphorFrequency": 0.55,
  "metaphorDomain": "WoodForest",
  "valueCore": "创作的孤独与分享",
  "personalityDescription": "...",
  "salutationPattern": "今天又刻了一会儿木头。",
  "closingPattern": "- 莉亚，于小屋",
  "vocabularyMapping": { "person": "过路人", "beautiful": "有纹理的" },
  "metaphorTemplates": { "creation": ["每块木头上都有一张脸..."], ... },
  "signaturePhrases": ["要不要看看我在刻的东西？", ...],
  "observedQualities": ["对美的敏感", "和自然的亲近"],
  "stylisticRules": ["多用木质和自然纹理的意象来描述人"]
}
```

### UI 纯代码渲染

所有 UI 使用 `SpriteBatch` 直接绘制——无外部纹理、无图像资源、无 Content Patcher 依赖。

**绘制技术:**
| 效果 | 实现方式 |
|------|----------|
| 皮质封面 | 多层半透明矩形 + 随机水平纹理线 |
| 缝线 | 虚线循环 (drawLine 6px dash / 4px gap) |
| 做旧纸张 | 乳白底色 + 40 个随机小矩形模拟纹理 + 边缘阴影 |
| 水滴渍 | 同心圆环 (5 层, 递减 alpha) |
| 墨水渍 | 伪随机位置 + 飞溅小点 |
| 书脊 | 深色皮革 + 4 条横带 |
| 阴影 | 多层偏移填充矩形 (递减 alpha) |
| 圆/弧/线 | 分段三角函数 + 旋转矩形 |
| 丝带书签 | 波浪动画 (sin(time) 偏移) |
| 角花 | 弧线 + 小圆点 |
| 装饰分割线 | 左右横线 + 中心菱形 + 两侧小方块 |

### GMCM 反射集成

`GMCMHelper` 使用**纯反射**调用 Generic Mod Config Menu API，避免硬依赖 `IManifest` 类型（SMAPI 4.0 中该类型位于 `SMAPI.Toolkit.CoreInterfaces`，不可直接引用）。

```csharp
// 不使用: gmcm.Register(modManifest, ...)
// 而是:  反射调用 GMCM 的 Register/AddBool/AddText/AddInt/AddFloat 等方法
//        所有参数通过 object[] 传递，绕过类型检查
```

共注册 50+ 配置选项，分组为：核心开关、活动开关、补丁开关、日志设置、互惠篮设置、商店设置、兴致设置、离线设置、模板后门、NPC设置、视觉设置、调试。

---

## 代码结构

```
EchoesOfTheHollow/
├── ModEntry.cs              (385行) 入口点 + 事件处理
├── ModConfig.cs             (33个配置属性)
├── EchoesOfTheHollow.csproj (net6.0, Nullable enabled, Harmony enabled)
├── manifest.json
├── README.md
├── src/
│   ├── GlobalUsings.cs      (StardewValley, SMAPI, SMAPI Events)
│   ├── Data/                (5 文件) POCO 模型
│   ├── NPC/                 (1 文件) VoiceRegistry
│   ├── Patches/             (8 文件) Harmony 补丁
│   ├── Systems/             (18 文件)
│   │   ├── Activities/      (4 文件)
│   │   ├── Economy/         (4 文件)
│   │   ├── Enthusiasm/      (1 文件)
│   │   ├── Festival/        (1 文件)
│   │   ├── Invitation/      (1 文件)
│   │   ├── Journal/         (6 文件)
│   │   ├── Offline/         (1 文件)
│   │   └── GMCMHelper.cs
│   ├── UI/                  (4 文件) 纯代码UI
│   └── Utils/               (6 文件) 工具类
├── assets/data/
│   ├── MemoryTemplates.json (130模板)
│   ├── NpcVoices.json       (35语音档案)
│   ├── WindListeningSpots.json (8听风点)
│   └── templates/           (自定义模板"后门"目录)
└── i18n/
    ├── default.json         (52键, 中文)
    └── zh.json              (52键, 中文)
```

**代码统计:**
- 41 个 C# 源文件
- 130 个 JSON 记忆模板
- 35 个 NPC 语音档案
- 50+ 可配置选项
- 14 个 SMAPI 事件处理程序
- 12 组 Harmony 补丁
- 10 种触发器类型

---

## 数据文件

| 文件 | 用途 | 规模 |
|------|------|------|
| `assets/data/NpcVoices.json` | NPC语音档案 (writingStyle, vocabularyMapping, metaphorTemplates...) | 35 NPC, ~1000行 |
| `assets/data/MemoryTemplates.json` | 记忆模板 (条件匹配 + 文本变体) | 130 模板, ~1400行 |
| `assets/data/WindListeningSpots.json` | 听风点坐标与恢复倍率 | 8 位置 |
| `assets/data/templates/` | 用户自定义模板"后门"目录 | 按需扩展 |
| `i18n/default.json` | UI翻译键 (中文) | 52 键 |

---

## 开发与构建

### 前置条件

- .NET 6.0 SDK
- Stardew Valley 1.6+ (Steam 安装)
- SMAPI 4.0+

### 构建

```bash
dotnet build -c Release
```

构建输出: `bin/Release/net6.0/EchoesOfTheHollow.dll`

### 项目配置

- **Target Framework:** `net6.0`
- **Nullable:** enabled
- **ImplicitUsings:** disabled（所有 using 显式声明）
- **Harmony:** enabled（通过 `EnableHarmony` 构建属性）
- **依赖项:** 6 个游戏/SMAPI DLL 引用（无 NuGet 包）

### 添加新内容

**新记忆模板 (无需重新编译):**
1. 在 `assets/data/templates/` 中创建 `your_name.json`
2. 遵循模板结构定义
3. 重启游戏

**新 NPC 语音档案 (需重新编译):**
1. 在 `NpcVoices.json` 中添加新条目
2. 确保 `MemoryTemplateEngine` 有对应模板

---

## 存档安全

- ✅ 所有模组数据存储在 `Game1.player.modData["EchoesHollow/*"]` 中
- ✅ 香草存档数据（金钱、好感度、技能、成就）**完全不修改**
- ✅ `Farmer.money` getter 不补丁 — 后端金币正常运作（美人鱼吊坠、Joja会员）
- ✅ 好感度数据完整保留 — 仅心形 UI 移除
- ✅ 技能经验获取被阻止，但现有等级保留
- ✅ 卸载模组后，所有香草数值原样恢复
- ⚠️ 卸载后，modData 中的模组日志/记忆将丢失（不影响香草数据）

---

## 许可证

MIT License

Copyright (c) 2025 gengyangze-hub
