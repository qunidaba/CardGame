# 肉鸽系统开发任务表

> 基于现有扑克牌战斗系统扩展
> **原则：每步完成可独立运行测试，确认无报错后进行下一步**

---

## 阶段 0：核心数据结构（无 UI，纯逻辑）

### 任务 0.1：RunData 存档数据类
- [ ] 创建 `Scripts/Roguelike/RunData.cs`
- [ ] 字段：seed, actId, battleIndex, maxHp/currentHp, gold
- [ ] 字段：cardEnchantmentIds (Dictionary<string, List<int>>) - 52张牌的附魔ID
- [ ] 字段：relicIds (List<int>), potionIds (List<int>)
- [ ] 字段：visitedEventIds, unlockedContent, eliteKillCount
- [ ] 字段：nextBattleStartHpMod, nextBattleEnemyBuff
- [ ] 方法：GetCardKey(), Add/Remove/ClearEnchantment()
- [ ] **测试**：new RunData() 序列化/反序列化 JSON 不报错

### 任务 0.2：ConfigLoader 与数据类
- [ ] 创建 `Scripts/Roguelike/Data/ConfigData.cs` - 所有配置数据类
- [ ] 创建 `Scripts/Roguelike/Data/ConfigLoader.cs` - JSON 加载器
- [ ] 复用现有 MiniJson
- [ ] **测试**：ConfigLoader.LoadAll() 读取 StreamingAssets/Config/*.json 无报错

### 任务 0.3：配置文件（JSON）
- [ ] enemies.json - 敌人数据（hp, intents, goldRange, pools）
- [ ] relics.json - 遗物数据（id, name, effects[], rarity, maxSlots）
- [ ] enchantments.json - 附魔数据（id, name, trigger, effects[], tier, weight）
- [ ] events.json - 事件数据（id, title, options[text, results[]]）
- [ ] eventChoices.json - 事件选择池（id, type, weight, guaranteedAt, eventId）
- [ ] shop.json - 商店池与价格公式
- [ ] potions.json - 药水数据
- [ ] acts.json - 章节战斗序列（battles[], eventChoiceCount, guaranteedRestAt/ShopAt）
- [ ] **测试**：ConfigLoader 能加载所有文件，字段类型匹配

---

## 阶段 1：RunDirector 核心流程（无 UI，纯逻辑）

### 任务 1.1：RunDirector 单例框架
- [ ] 创建 `Scripts/Roguelike/RunDirector.cs`
- [ ] 单例模式，DontDestroyOnLoad
- [ ] 字段：RunData, BattleManager, UIManager 引用
- [ ] 方法：StartNewRun(seed), ContinueRun(RunData)
- [ ] **测试**：场景挂载 RunDirector，StartNewRun() 能初始化 RunData 无报错

### 任务 1.2：BattleSequence 战斗序列管理
- [ ] 创建 `Scripts/Roguelike/BattleSequence.cs`
- [ ] 读取 acts.json，按索引返回当前 BattleSequenceData
- [ ] 支持类型：Combat(普通/精英池), EventChoice, Boss
- [ ] 方法：GetCurrent(), Advance(), IsFinished
- [ ] **测试**：BattleSequence 能正确返回各阶段战斗数据

### 任务 1.3：RunDirector 接入 BattleSequence
- [ ] RunDirector.StartNewRun() 初始化 BattleSequence(actId=1)
- [ ] 方法：EnterNextBattle() 根据序列类型分发
  - Combat → 初始化 BattleManager
  - EventChoice → 显示事件选择面板
  - Boss → 标记 isBossBattle=true 进入战斗
- [ ] **测试**：RunDirector 能按序列推进：Combat → EventChoice → Combat...

---

## 阶段 2：附魔系统（核心成长机制）

### 任务 2.1：EnchantmentSystem 核心
- [ ] 创建 `Scripts/Roguelike/EnchantmentSystem.cs`
- [ ] 触发器枚举：OnPlay, OnHandType, OnCombatStart, OnTurnStart, OnDraw, OnDiscard, OnTakeDamage, OnKill, OnGoldGain
- [ ] 效果类型：AddDamage, AddDefense, AddDraw, AddHeal, AddGold, DuplicateHandType, TransformHandType, ApplyPoison 等
- [ ] 方法：OnCardPlayed(), OnHandTypePlayed(), OnCombatStart(), OnTurnStart(), OnDealDamage(), OnTakeDamage(), OnKillEnemy(), OnGoldGain()
- [ ] **测试**：给牌加附魔，出牌时触发效果修改 HandEffectTable 效果值

### 任务 2.2：BattleManager 接入 EnchantmentSystem
- [ ] BattleManager.InitBattleWithData() 接收 RunData、EnemyData、isElite
- [ ] 出牌时：调用 enchantmentSystem.OnCardPlayed() 修改 effects
- [ ] 回合开始：调用 OnTurnStart()
- [ ] 造成伤害/受伤/击杀：调用对应触发
- [ ] **测试**：带附魔的牌出牌时效果生效（如 +伤害、+抽牌）

### 任务 2.3：附魔奖励生成（三选一）
- [ ] 创建 `Scripts/Roguelike/RewardSystem.cs`
- [ ] GenerateEnchantmentOptions(runData, count=3)
  - 随机选 3 张不同的牌
  - 每张牌按权重随机 1 个附魔
  - 返回 EnchantmentRewardOption[] (cardKey, cardDisplayName, enchantmentId)
- [ ] ApplyEnchantmentReward() 写入 runData.cardEnchantmentIds
- [ ] **测试**：战斗胜利后生成 3 个选项，选中后 runData 记录附魔

---

## 阶段 3：遗物系统

### 任务 3.1：RelicSystem 核心
- [ ] 创建 `Scripts/Roguelike/RelicSystem.cs`
- [ ] 槽位制：MaxSlots = 6
- [ ] 方法：TryAddRelic(), TryRemoveRelic(), TryReplaceRelic()
- [ ] 效果查询：GetEffectsByTrigger(), GetMultiplier(), GetFlatBonus()
- [ ] **测试**：添加/移除/替换遗物，查询效果不报错

### 任务 3.2：遗物效果触发
- [ ] 触发器：OnCombatStart, OnTurnStart, OnCardPlayed, OnHandType, OnTakeDamage, OnKillEnemy, OnGoldGain, OnShopEnter, OnRest, OnBattleWin
- [ ] BattleManager/RunDirector 调用对应触发
- [ ] **测试**：遗物效果生效（如“每回合+3防御”、“击杀回血”）

---

## 阶段 4：事件选择系统

### 任务 4.1：EventChoiceSystem
- [ ] 创建 `Scripts/Roguelike/EventChoiceSystem.cs`
- [ ] GenerateChoices(runData) → List<EventChoiceData>
- [ ] 保底机制：guaranteedRestAt[], guaranteedShopAt[]
- [ ] 权重随机 + 去重
- [ ] 类型：Rest, Shop, FixedEvent, RandomEvent, Treasure, ElitePreview, Campfire
- [ ] **测试**：战斗后生成 3-4 个选项，含保底休息/商店

### 任务 4.2：EventSystem 事件执行
- [ ] 创建 `Scripts/Roguelike/EventSystem.cs`
- [ ] ExecuteEvent(eventData, runData, optionIndex)
- [ ] 结果类型：GainGold, LoseGold, Heal, LoseMaxHp, GainMaxHp, EnchantRandomCard, RemoveAllEnchantmentsRandomCard, GainRelic, LoseRelic, GainPotion, NextBattleStartHp, NextBattleEnemyBuff, Gamble
- [ ] **测试**：点击选项执行效果，修改 runData 正确

---

## 阶段 5：商店与休息

### 任务 5.1：ShopSystem
- [ ] 创建 `Scripts/Roguelike/ShopSystem.cs`
- [ ] GenerateShop(runData) → ShopInventory (relics, potions, enchantments[])
- [ ] 价格计算：遗物按稀有度、药水固定、附魔按 tier
- [ ] 购买方法：TryBuyRelic/Potion/Enchantment()
- [ ] **测试**：商店生成商品，购买扣金币、给遗物/药水/附魔

### 任务 5.2：RestSystem
- [ ] 创建 `Scripts/Roguelike/RestSystem.cs`
- [ ] GetOptions(runData) / GetCampfireOptions(runData)
- [ ] 选项：Heal30%, UpgradeEnchant, HealFullLoseGold, RemoveEnchantGetRandom, MaxHpUp, GainRelic
- [ ] ApplyUpgradeEnchantment() / ApplyRemoveEnchantGetRandom()
- [ ] **测试**：休息选项执行效果正确

---

## 阶段 6：药水系统

### 任务 6.1：PotionSystem
- [ ] 创建 `Scripts/Roguelike/PotionSystem.cs`
- [ ] 槽位：MaxSlots = 3
- [ ] TryAddPotion(), UsePotion()
- [ ] 效果：HealPercent, NextPlayDamageMult, GainDefense, DrawCards, ApplyPoison
- [ ] **测试**：战斗中使用药水生效

---

## 阶段 7：UI 面板（逐个接入）

### 任务 7.1：EventChoicePanel
- [ ] 显示 3-4 个选项按钮（displayName + description）
- [ ] 点击回调 → RunDirector.OnEventChoiceSelected()

### 任务 7.2：RewardPanel
- [ ] 显示 3 个附魔选项（卡牌名 + 附魔名 + 描述）
- [ ] 点击回调 → RewardSystem.ApplyEnchantmentReward()

### 任务 7.3：EventPanel
- [ ] 显示事件标题、描述、选项文本
- [ ] 点击执行 EventSystem，完成后回调

### 任务 7.4：ShopPanel
- [ ] 三列：遗物/药水/附魔服务
- [ ] 显示名称、价格、购买按钮
- [ ] 购买回调 ShopSystem

### 任务 7.5：RestPanel
- [ ] 显示休息选项列表
- [ ] 需二次确认的选项（升级附魔、移除附魔）打开 DeckViewer

### 任务 7.6：BattlePanel 扩展
- [ ] 显示遗物图标列表、药水槽、金币
- [ ] 显示敌人意图（Attack/Defense/Buff 值）
- [ ] 药水按钮 → 打开药水选择

### 任务 7.7：DeckViewer
- [ ] 显示 52 张牌网格，附魔计数标记
- [ ] 点击卡牌显示附魔详情
- [ ] 支持“升级附魔/移除附魔”模式选择牌

---

## 阶段 8：完善与存档

### 任务 8.1：SaveSystem
- [ ] 创建 `Scripts/Roguelike/SaveSystem.cs`
- [ ] SaveGame(RunData) → persistentDataPath/roguelike_save.json
- [ ] LoadGame() → RunData
- [ ] SaveMeta (时间、act、血量、金币、遗物数、胜场)
- [ ] **测试**：保存/读取/删除存档不报错

### 任务 8.2：RunDirector 接入 SaveSystem
- [ ] 关键节点自动存档（战斗胜利、事件完成、进入商店/休息后）
- [ ] 主菜单“继续游戏”调用 LoadGame()
- [ ] **测试**：中途退出游戏重进能恢复进度

### 任务 8.3：元进度解锁
- [ ] 通关解锁：新遗物/附魔/敌人/章节
- [ ] 统计面板

---

## 阶段 9：平衡与内容

### 任务 9.1：数值平衡
- [ ] 敌人 hp/攻击/意图权重
- [ ] 附魔效果数值/权重
- [ ] 遗物效果强度
- [ ] 金币/商店价格曲线
- [ ] Boss 难度

### 任务 9.2：内容扩充
- [ ] 更多敌人/精英/Boss
- [ ] 更多事件/选项
- [ ] 更多遗物/附魔/药水
- [ ] Act 2/3 配置

---

## 调试检查清单（每步完成后必跑）

- [ ] Unity 编译无错误
- [ ] 运行场景无红字报错
- [ ] 核心流程：战斗 → 奖励 → 事件选择 → 下一战 → ... → Boss 能跑通
- [ ] 存档/读取不丢数据
- [ ] 关键数据（血量、金币、附魔、遗物、药水）在流程中正确流转

---

## 文件结构规划

```
Scripts/
├── Roguelike/
│   ├── RunDirector.cs
│   ├── RunData.cs
│   ├── BattleSequence.cs
│   ├── EnchantmentSystem.cs
│   ├── RelicSystem.cs
│   ├── PotionSystem.cs
│   ├── EventChoiceSystem.cs
│   ├── EventSystem.cs
│   ├── RewardSystem.cs
│   ├── ShopSystem.cs
│   ├── RestSystem.cs
│   ├── SaveSystem.cs
│   └── Data/
│       ├── ConfigData.cs
│       ├── ConfigLoader.cs
│       └── MiniJson.cs
├── Battle/          # 现有战斗逻辑（已改为事件驱动）
├── CardData/        # 现有牌数据
└── UI/
    ├── BattlePanel.cs      # 已改为事件驱动
    ├── BattleUI.cs         # 已改为事件驱动
    ├── CardUI.cs
    ├── EventChoicePanel.cs (新)
    ├── RewardPanel.cs      (新)
    ├── EventPanel.cs       (新)
    ├── ShopPanel.cs        (新)
    ├── RestPanel.cs        (新)
    ├── DeckViewer.cs       (新)
    ├── ResultPanel.cs
    └── UIManager.cs
```

---

## 开始顺序

**先做 任务 0.1 - 0.3（数据层）**，确保配置加载无误，再做 1.1 RunDirector。

---

*文件位置：`Assets/Scripts/ROGUELIKE_TASKS.md`*
*按顺序逐项开发，每项完成后运行测试，确认无报错再进行下一项*