# 🔑 记忆后门 / Memory Back Door

在这里放 `.json` 文件来添加新的记忆模板，无需重新编译模组。

## 如何使用

1. 在这个目录下创建一个 `.json` 文件（任何名字都可以）
2. 按下面的格式填写
3. 重启游戏，新记忆自动生效

## 模板格式

```json
{
  "templates": [
    {
      "templateId": "my_custom_template_001",
      "npcName": "Abigail",
      "triggerType": "Environmental",
      "priority": 5,
      "cooldownDays": 3,
      "conditions": {
        "observedActions": ["farming"],
        "timeOfDay": ["morning"],
        "seasons": ["spring", "summer"],
        "weather": ["clear"],
        "locations": ["Farm"],
        "minOfflineHours": 12,
        "minEntriesWithPlayer": 0
      },
      "textVariants": [
        "这是第一条记忆文本变体。使用 {{playerName}} 代替玩家名字。",
        "这是第二条变体。系统会随机选择一条。可以使用 {{npcName}}、{{location}}、{{timeOfDay}}、{{season}}、{{weather}} 等变量。"
      ],
      "vocabularyHints": ["word1", "word2"],
      "emotionTag": "Warm",
      "extends": null
    }
  ]
}
```

## 触发类型

- `DirectInteraction` - 和NPC直接对话
- `Environmental` - 玩家在特定区域活动
- `CrossDayObservation` - 跨日观察
- `Absence` - NPC注意到你好几天没出现
- `SeasonalEvent` - 季节变化
- `WeatherEvent` - 天气事件
- `BasketExchange` - 互惠篮交换
- `DriftFind` - NPC捡到漂流物
- `OfflinePassage` - 现实时间离线后
- `Daydream` - 玩家发呆时

## 扩展/覆盖

- 要覆盖内置模板：使用相同的 `templateId`
- 要扩展一个模板：设置 `"extends": "parent_template_id"` ，只写要修改的字段
- 要禁用某个模板：设置 `"priority": 0`

## NPC名称列表

Abigail, Alex, Caroline, Clint, Demetrius, Elliott, Emily, Evelyn, George, Gus, Haley, Harvey, Jas, Jodi, Kent, Leah, Leo, Lewis, Linus, Marnie, Maru, Pam, Penny, Pierre, Robin, Sam, Sebastian, Shane, Vincent, Willy, Wizard

使用 "Any" 作为通用模板（适配所有NPC）。
