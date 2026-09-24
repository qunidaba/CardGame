# 扑克肉鸽 设计文档

> 基于现有卡牌战斗系统扩展为 Roguelike
> **核心循环：战斗 → 事件选择 → 战斗 → 事件选择... → Boss → 通关**

---

## 1. 核心循环

```
[新游戏] → [初始化: 固定52张牌 + 空遗物栏 + 空药水栏 + 30血 + 0金]
    ↓
[第1场战斗: 普通怪] → 胜利获得 [金币 + 附魔三选一]
    ↓
[事件选择界面: 随机3-4个选项]
    ├─ 固定事件 (如: 休息点、商店、精英预告)
    ├─ 随机事件 (文本冒险/风险收益)
    └─ 特殊节点 (宝箱、升级台、祭坛...)
    ↓ 玩家选1个 → 结算效果
[第2场战斗] → ...
    ↓
... 循环 N 场 ...
    ↓
[Boss 战] → 胜利 → [结算/解锁/无限模式]
```

---

## 2. 运行时架构

```
RunDirector (单例, DontDestroyOnLoad)
├── RunData              // 当前运行所有状态
├── BattleSequence       // 战斗顺序/难度曲线
├── EventPool            // 事件池管理(固定+随机)
├── RewardSystem         // 战斗奖励: 金币 + 附魔三选一
├── ShopSystem           // 商店: 遗物/药水/付费附魔
├── RestSystem           // 休息点: 回血/升级附魔/其他
├── RelicSystem          // 遗物效果管理(槽位制6格)
├── EnchantmentSystem    // 附魔效果管理/触发器
├── EventSystem          // 事件执行器
└── SaveSystem           // JSON 存档/读档
```

### 2.2 数据结构

#### RunData (核心存档对象)
```csharp
class RunData {
    // 基础
    int seed;
    int floor;                    // 当前层数 (1-?)
    int act;                      // 当前章节
    NodeId currentNode;           // 当前节点
    
    // 玩家状态
    int maxHp, currentHp;
    int gold;
    // 牌组固定 52 张，只存每张牌的附魔列表
    Dictionary<string, List<CardEnchantment>> cardEnchantments;  // key: "Suit_Rank" 如 "Spade_14"
    List<Relic> relics;           // 遗物列表 (槽位制，最多6)
    List<Potion> potions;         // 药水槽(最多3)
    
    // 地图进度
    List<NodeId> visitedNodes;    // 已访问节点
    List<string> unlockedContent; // 解锁内容(遗物/附魔/敌人等)
    
    // 统计
    int enemiesKilled;
    int elitesKilled;
    int bossesKilled;
    int totalDamageDealt;
    int totalGoldGained;
}
```

#### Node (地图节点)
```csharp
enum NodeType { Combat, Elite, Event, Shop, Rest, Boss, Treasure }

class MapNode {
    NodeId id;
    NodeType type;
    List<NodeId> connections;  // 可前往的下层节点
    bool visited;
    // 类型特定数据
    EncounterData encounter;   // 战斗/精英
    EventData event;           // 事件
    ShopData shop;             // 商店
}
```

---

## 3. 核心系统详细设计

### 3.1 遗物系统

**设计原则**：被动效果，改变战斗/地图/资源规则

```csharp
class Relic {
    string id;
    string name;
    string description;
    Rarity rarity;          // Common/Uncommon/Rare/Boss/Shop
    RelicEffect[] effects;  // 效果列表
}

enum RelicTrigger {
    OnCombatStart,      // 战斗开始
    OnTurnStart,        // 回合开始
    OnCardPlayed,       // 出牌后
    OnHandType,         // 打出特定牌型
    OnTakeDamage,       // 受伤
    OnKillEnemy,        // 击杀
    OnGoldGain,         // 获金
    OnShopEnter,        // 进入商店
    OnRest,             // 休息
    OnMapNodeEnter,     // 进入节点
}

// 示例遗物
- "皇家同花顺": 打出同花顺5时，抽3牌+获得5防御
- "赌徒护腕": 战斗开始时随机弃1牌抽2牌
- "金币满溢": 获得金币+25%，但最大生命-10
- "牌堆压缩器": 商店移除牌花费-50%
- "二段跳": 地图可跳过1个节点(每层1次)
```

### 3.2 卡牌附魔系统

**核心原则**：52 张牌固定不变，**通过附魔强化单张牌**

```csharp
class CardEnchantment {
    string id;                    // 唯一标识
    string name;                  // 显示名："烈火"、"专注"、"铁壁"...
    string description;           // "出牌时伤害+2"
    EnchantmentTrigger trigger;   // 触发时机
    EnchantmentEffect[] effects;  // 效果列表
    int tier;                     // 1~3 稀有度/强度
}

enum EnchantmentTrigger {
    OnPlay,           // 出牌时(结算牌型前)
    OnHandType,       // 打出特定牌型时
    OnCombatStart,    // 战斗开始
    OnTurnStart,      // 回合开始
    OnDraw,           // 抽到手牌时
    OnDiscard,        // 进弃牌堆时
    OnTakeDamage,     // 受伤时
    OnKill,           // 击杀时
}

// 效果类型
enum EnchantmentEffectType {
    AddDamage,        // 伤害+
    AddDefense,       // 防御+
    AddDraw,          // 抽牌+
    AddHeal,          // 治疗+
    AddMaxHp,         // 最大血+
    ModifyHandType,   // 修改牌型判定(如：对子视为三条)
    GrantTempRelic,   // 临时获得遗物效果
    TransformCard,    // 临时转化牌(本局生效)
}
```

**附魔获取方式**：
1. **战斗胜利**：随机 3 张牌各获得 1 个随机附魔 → 三选一保留（未选的附魔消失）
2. **商店**：花金币给指定牌附魔（可重复，同牌可叠加不同附魔）
3. **事件/遗物**：特定触发给牌附魔

**UI 体现**：CardUI 显示附魔图标/名称栏，鼠标悬停显示详情

### 3.3 地图生成

```
第1层:    [C] [C] [E] [S] [R]     (5节点, 3连通)
第2层:   [C] [E] [?] [S] [R]      (?=随机事件/宝箱)
第3层:  [E] [?] [B] [?] [E]       (B=精英)
...
Boss层:                    [BOSS]
```

**算法**：
1. 预设每层节点数/类型分布
2. 生成 DAG (有向无环图)，保证每层至少2条路径汇聚
3. 关键节点(商店/休息/精英)强制分布
4. 种子确定性生成

### 3.4 事件系统

```csharp
class Event {
    string id;
    string title;
    string description;
    EventOption[] options;
    Requirement[] requirements;  // 需特定遗物/金币/血量
}

class EventOption {
    string text;
    EventResult[] results;
    bool hidden;        // 满足条件显示
    int goldCost;
    int hpCost;
}

// 结果类型
enum EventResultType {
    GainGold, LoseGold,
    GainMaxHp, LoseMaxHp, Heal,
    AddCard, RemoveCard, UpgradeCard, TransformCard,
    GainRelic, LoseRelic,
    GainPotion,
    StartCombat,       // 触发战斗
    ModifyDeck,        // 牌组变换
    UnlockNode,        // 地图解锁捷径
}
```

**事件示例**：
- "流浪商人": 花50金买张稀有卡 / 偷窃(50%成功,失败战斗)
- "古老祭坛": 献祭1张卡获得遗物 / 失去10最大血获得Boss遗物
- "训练假人": 免费升级1张卡 / 练习牌型(给临时强化)

### 3.5 商店系统

```csharp
class ShopData {
    List<Relic> relicsForSale;        // 2-3 个遗物
    List<Potion> potionsForSale;      // 2 个药水
    List<EnchantmentService> enchantServices;  // 付费附魔服务
}

class EnchantmentService {
    CardEnchantment enchantment;      // 提供的附魔
    int price;                        // 基础价格 50-150
    int maxPurchases;                 // -1 无限，或限购次数
    int purchasedCount;
    bool targetSpecificCard;          // true=玩家选牌附魔，false=随机一张牌
}

// 价格公式
Relic:        基础 150-350 * 稀有度系数
Potion:       50-100
Enchantment:  基础 50-150 * tier系数 * (1 + purchasedCount*0.5)
```

### 3.6 休息站

```csharp
class RestSite {
    enum Action { Heal30, HealAll, UpgradeCard, PurgeCard, GainGold, GainRelic }
    // 遗物/事件可解锁新选项
}
```

### 3.7 药水系统 (可选，简化版)

```csharp
class Potion {
    string id;
    PotionEffect effect;      // 战斗中使用，即时生效
    int maxCharges = 1;
}
// 示例: 爆发药水(下次出牌伤害×2), 专注药水(抽3牌), 铁壁药水(获得15防御)
```

---

## 4. 战斗系统扩展

### 4.1 敌人数据驱动

```csharp
class EnemyData {
    string id;
    string name;
    int hp; int maxHp;
    int baseAttack;
    Intent[] intentPattern;   // 意图序列
    List<RewardTier> rewards; // 掉落奖励池
    List<string> immunities;  // 免疫效果
}

// 意图系统 (Slay the Spire 风格)
enum Intent { Attack, Defense, Buff, Debuff, MultiAttack, Special }
class IntentData {
    Intent type;
    int value;           // 攻击伤害/防御值
    bool multiHit;       // 多段
    string description;  // UI显示
}
```

### 4.2 战斗奖励

```csharp
class CombatReward {
    int gold;                          // 基础 10-50
    EnchantmentReward enchantReward;   // 3选1：每个选项=随机3张牌各+1随机附魔
    RelicReward relicReward;           // 精英/Boss 掉落遗物
    PotionReward potionReward;         // 可选掉落药水
    // 无移除卡选项
}

class EnchantmentReward {
    CardEnchantmentOption[] options;   // 长度=3
}

class CardEnchantmentOption {
    CardData targetCard;               // 目标牌
    CardEnchantment enchantment;       // 附魔内容
}
```

---

## 5. UI/流程新增需求

### 5.1 新增面板
| 面板 | 用途 |
|------|------|
| MapPanel | 地图选择路径 |
| RewardPanel | 战斗/事件后 3选1 选卡/遗物 |
| ShopPanel | 商店买卖/移除 |
| RestPanel | 休息选项 |
| EventPanel | 事件文本+选项 |
| RelicDisplay | 遗物详情/列表 |
| DeckViewer | 牌组查看/升级/移除 |
| RunEndPanel | 结算统计/解锁 |

### 5.2 现有面板扩展
- **BattlePanel**: 显示遗物图标、药水槽、意图预览
- **CardUI**: 显示升级标记(+1/+2)、耗尽/保留/天赋图标

---

## 6. 代码结构规划

```
Scripts/
├── Roguelike/              # 新增核心
│   ├── RunDirector.cs
│   ├── RunData.cs
│   ├── MapGenerator.cs
│   ├── MapNode.cs
│   ├── EventSystem.cs
│   ├── RewardSystem.cs
│   ├── ShopSystem.cs
│   ├── RestSystem.cs
│   ├── RelicSystem.cs
│   ├── CardUpgradeSystem.cs
│   └── SaveSystem.cs
├── Data/                   # ScriptableObject 配置
│   ├── EnemyData.cs
│   ├── RelicData.cs
│   ├── EventData.cs
│   ├── CardUpgradeData.cs
│   └── MapLayoutData.cs
├── Battle/                 # 扩展
│   ├── EnemyIntent.cs
│   ├── CombatRewards.cs
│   └── BattleManager.cs    # 注入 RunDirector/遗物效果
├── UI/                     # 新增面板 + 扩展
│   ├── MapPanel.cs
│   ├── RewardPanel.cs
│   ├── ShopPanel.cs
│   ├── RestPanel.cs
│   ├── EventPanel.cs
│   ├── RelicDisplay.cs
│   └── DeckViewer.cs
└── Common/
    ├── Rarity.cs
    ├── Intent.cs
    └── EventResult.cs
```

---

## 7. 遗物效果接入点 (BattleManager 注入)

```csharp
// BattleManager 新增
public List<Relic> PlayerRelics { get; private set; }

// 钩子调用示例
void StartPlayerTurn() {
    foreach (var relic in PlayerRelics)
        relic.OnTurnStart(this);
    
    // 原逻辑...
    if (relic.HasEffect(RelicTrigger.OnTurnStart, out var eff))
        ApplyRelicEffect(eff);
}

bool TryPlayCards() {
    // 原逻辑...
    foreach (var relic in PlayerRelics)
        relic.OnCardPlayed(this, playedCards, result.type);
}

void ApplyEffects(effects) {
    // 遗物可修改效果数值
    effects = RelicSystem.ModifyEffects(PlayerRelics, effects);
    // 原逻辑...
}
```

---

## 8. 开发里程碑

| 阶段 | 目标 | 交付物 |
|------|------|--------|
| **M1: 地图+运行框架** | 可跑完 1 章地图(3层+Boss) | RunDirector, MapGenerator, MapPanel, 节点跳转 |
| **M2: 奖励+商店+休息** | 战斗后选卡、买卖、休息回血 | RewardPanel, ShopPanel, RestPanel, 金币系统 |
| **M3: 遗物系统** | 10+ 遗物生效、拾取/丢弃 | RelicSystem, RelicData, 遗物UI, 效果钩子 |
| **M4: 卡牌升级/事件** | 升级界面、事件系统、转化 | CardUpgradeSystem, EventSystem, EventPanel |
| **M5: 敌人意图/精英/Boss** | 意图预览、多阶段Boss、精英掉遗物 | EnemyData, IntentSystem, BossAI |
| **M6: 存档/解锁/平衡** | 完整跑通、成就、难度调节 | SaveSystem, 统计面板, 平衡表 |

---

## 9. 配置表 (JSON)

> 统一放 `Assets/StreamingAssets/Config/`，启动时加载

| 文件 | 关键字段 |
|------|----------|
| `enemies.json` | id, name, hp, intents[], relicDropTable, goldRange |
| `relics.json` | id, name, desc, rarity, effects[trigger/type/value], maxSlots, sprite |
| `enchantments.json` | id, name, desc, trigger, effects[type/value], tier, weight(掉率权重) |
| `events.json` | id, title, desc, options[text/requirements/results[]], background |
| `mapLayout.json` | acts[layers[nodeDistribution/connectionRules]], nodeTypeWeights |
| `shop.json` | relicPools[tier], potionPool, enchantmentPool[tier], priceFormulas |
| `potions.json` | id, name, desc, effect, sprite |

---

## 10. 现有代码改动点清单

| 文件 | 改动 |
|------|------|
| `BattleManager.cs` | 注入 RunDirector/RelicSystem/EnchantmentSystem；StartPlayerTurn/TryPlayCards/ApplyEffects 加钩子；InitBattle 读取 RunData.cardEnchantments；出牌时结算附魔效果 |
| `BattleUI.cs` | 持有 RunDirector；战斗结束调用 RewardSystem(附魔奖励) 而非直接结算 |
| `DeckBuilder.cs` | 移除 BuildStandardDeck → BuildFixedDeckWithEnchantments(RunData) 返回带附魔的 52 张牌 |
| `CardData.cs` | 添加 `List<CardEnchantment> enchantments` 字段（运行时动态挂载） |
| `HandEffectTable.cs` | 效果值改为可修改结构，供附魔/遗物在结算前修改 |
| `UIManager.cs` | 注册新面板预制体 |
| `BattlePanel.cs` | 显示遗物/药水/意图/金币；CardUI 显示附魔图标/栏；DeckViewer 入口 |
| `HandArea.cs` | 无需改动，牌对象复用 CardData 引用 |

---

## 11. 设计决策已定

| 项 | 决策 |
|----|------|
| **牌组** | 固定 52 张标准牌组（无大小王），**不增不减**，全程不变 |
| **删牌** | **无删牌服务** |
| **卡牌成长** | **附魔系统**：战斗胜利/商店/事件给「随机 3 张牌各加 1 个随机附魔」→ 三选一保留 |
| **附魔类型** | +伤害、+抽牌、+防御、+治疗、特殊触发(如：打出顺子额外效果) |
| **遗物上限** | **槽位制**：6 格（满则需替换/丢弃） |
| **药水** | **需要**：3 格药水槽，战斗中即时使用 |
| **难度/职业** | **后续扩展**，先做单角色单难度 |
| **商店服务** | 售卖遗物/药水 + **付费给指定牌附魔**（可选多次） |
| **配置表** | **JSON**（不再用 ScriptableObject） |
| **存档** | **JSON 全量序列化 RunData** |

---

## 12. 后续

请审阅并给出修改建议，我会按优先级拆任务开始落地 M1。