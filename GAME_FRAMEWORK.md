# Poker Card Battle Roguelike - 游戏框架文档

## 概览
- **类型**: 扑克牌构筑回合制 Roguelike
- **核心循环**: 战斗 → 奖励选择 → 事件/商店/休息 → 下一战斗 → Boss 战 → 通关
- **架构**: 纯逻辑层 + Unity UI 层分离，数据驱动（JSON 配置）

---

## 核心系统

### 1. RunDirector (`Roguelike/RunDirector.cs`)
**单例，全局流程控制**
- `StartNewRun()` / `ContinueRun()` - 新游戏/续玩
- `EnterNextBattle()` - 普通关卡进度控制（7 场进 Boss）
- `EnterBossBattle()` - Boss 战入口
- `OnBattleOver(bool isWin)` - 战斗结束回调，生成奖励
- `OnRewardConfirmed(CombatReward)` - 奖励确认，发放资源，进入下一场

**关键字段**:
- `battleCount` - 当前第几场
- `progression` - 关卡配置（totalBattles=7, eliteChancePerBattle 数组）
- `RunData` - 运行时存档数据

### 2. RunData (`Roguelike/RunData.cs`)
**可序列化的运行时数据（存档核心）**
- 基础: `seed`, `battleIndex`, `actId`, `maxHp`, `currentHp`, `gold`
- 收集: `relicIds`, `potionIds`, `cardEnchantmentIds`
- 战斗修正: `nextBattleEnemyBuff`, `nextBattleStartHpMod`, `isBossBattle`
- **事件属性**: `OnCurrentHpChanged`, `OnMaxHpChanged`, `OnGoldChanged`, `OnRelicsChanged` 等（UI 订阅）

### 3. BattleManager (`Battle/BattleManager.cs`)
**纯逻辑战斗核心，不持有 Unity 对象**
- 初始化: `InitBattleWithData(EnemyData, RunData, isElite, RelicSystem)`
- 回合流程: `StartPlayerTurn()` / `StartEnemyTurn()` (协程分阶段)
- 玩家操作: `TryPlayCards()` - 选牌→识别牌型→结算效果
- 事件系统: 30+ 个精确事件（携带 delta），UI 直接订阅

**关键状态**:
- `IsPlayerTurn`, `IsBattleOver`, `CanPlayerAct` (回合开始效果完成后才能操作)
- `currentEnemyIntent` - 敌人意图（玩家回合开始显示，敌方回合执行）

**回合阶段事件**:
- 玩家: `ClearDefense` → `DrawCards` → `TurnStartEffects` → `WaitInput` (`OnEnemyIntentReady` 触发显示)
- 敌人: `TurnStartEffects` → `DoT` → `Act` (执行已显示意图) → 选下一意图 → `TurnEndEffects`

### 4. BattleUnit (`Battle/BattleUnit.cs`)
**战斗单位（玩家/敌人通用）**
- 属性: `CurrentHp`, `MaxHp`, `Defense`（属性 setter 自动触发 `OnHpChanged` 等事件，携带 delta）
- 状态效果: `StatusEffects` (StatusEffectSystem)
- 回调: `OnTakeDamageCallback` (伤害触发附魔/遗物)

### 5. 卡牌与牌堆系统
- `CardData` - 牌数据（点数、花色、附魔 ID）
- `DeckPile` / `HandArea` / `DiscardPile` - 牌堆/手牌/弃牌堆
- `DeckBuilder.BuildDeckWithEnchantments(RunData)` - 根据附魔构建初始牌组
- `HandEvaluator.Evaluate(cards)` - 识别牌型（单张、对子、顺子、同花、葫芦、同花顺、五条等）
- `HandEffectTable.GetEffects(result)` - 牌型对应基础效果（伤害、护盾、抽牌等）

### 6. 遗物系统
- `RelicSystem` (`Roguelike/RelicSystem.cs`) - **纯逻辑层，无 UI 引用**
  - 数据: `ownedRelics` (List<int>), `relicData` (Dictionary<int, RelicRuntimeData>)
  - CRUD: `TryAddRelic`, `TryRemoveRelic`, `TryReplaceRelic`, `HasRelic`, `GetOwnedRelics`
  - 效果处理: `ApplyEffect(trigger, context)` - 按触发时机应用所有遗物效果
  - 事件: `OnDataChanged`, `OnSpecificDataChanged` (UI 订阅刷新)

- `RelicEffectProcessor` (`Roguelike/RelicEffectProcessor.cs`) - 具体效果实现
  - `OnCombatStart`, `OnTurnStart`, `OnCardPlayed`, `OnHandType`, `OnTakeDamage`, `OnKillEnemy`, `OnGoldGain`, `OnShopEnter`, `OnRest`, `OnBattleWin` 等
  - 通过 `RelicEffectContext` 传递上下文（player, enemy, handTypeResult 等）

### 7. 附魔系统
- `EnchantmentSystem` (`Roguelike/EnchantmentSystem.cs`)
  - `OnCardPlayed`, `OnTurnStart`, `OnTurnEnd`, `OnCombatStart`, `OnTakeDamage`, `OnDealDamage`
  - 附魔效果: `AddDamage`, `AddDefense`, `AddDraw`, `AddHeal`, `ApplyPoison`, `ApplyBurn`, `ApplyWeaken`, `TransformHandType`, `DuplicateHandType` 等
  - 支持 `duration` 字段（持续回合数）

### 8. 状态效果系统
- `StatusEffectSystem` / `StatusEffectData`
- 类型: `Poison`, `Burn`, `Regeneration`, `Weaken`, `Strength`, `Dexterity`, `Vulnerable` 等
- 支持层数堆叠、持续回合、回合结束递减

### 9. 奖励系统
- `RewardSystem.GenerateCombatReward(isElite, isBoss)` - 金币、遗物、药水、附魔三选一
- `RewardPanel` - UI 显示，点击选择附魔后回调 `OnRewardConfirmed`

---

## UI 系统

### UIManager (`UI/UIManager.cs`)
- 单例，面板栈管理: `ShowPanel<T>()`, `Hide<T>()`, `HideAll()`

### 面板
| 面板 | 职责 |
|------|------|
| `BattlePanel` | 战斗主界面：血条、防御、手牌、敌人意图显示、飘字、Tooltip |
| `RewardPanel` | 战斗胜利奖励选择（遗物展示 + 附魔三选一） |
| `ResultPanel` | 胜利/失败结算，重新开始按钮 |
| `RelicSlotUI` | 遗物槽位（6 个固定位），鼠标悬停显示 Tooltip |
| `StatusEffectBar` / `StatusEffectIcon` | 状态效果图标栏 |
| `TooltipManager` | 通用悬浮提示（0.5s 延迟显示） |

### BattlePanel 关键订阅
```csharp
battleManager.OnPlayerHpChanged += OnPlayerHpChanged;
battleManager.OnPlayerDefenseChanged += OnPlayerDefenseChanged;
battleManager.OnEnemyHpChanged += OnEnemyHpChanged;
battleManager.OnPlayerTurnStart += OnPlayerTurnStart;
battleManager.OnEnemyIntentReady += OnEnemyIntentReady;  // 玩家回合显示意图
battleManager.OnEnemyIntentChanged += OnEnemyIntentChanged; // 更新意图文本
battleManager.OnCanPlayerActChanged += OnCanPlayerActChanged; // 按钮可交互
```

---

## 配置系统 (Assets/StreamingAssets/Config/)

| 文件 | 内容 | 关键字段 |
|------|------|----------|
| `enemies.json` | 敌人数据 | `id`, `hp`, `intents[]`(type/value/hitCount/weight), `pools[]` |
| `relics.json` | 遗物数据 | `id`, `rarity`, `effects[]`(trigger/type/value/condition/duration) |
| `enchantments.json` | 附魔数据 | `id`, `tier`, `effects[]` |
| `acts.json` | 章节配置 | `actId`, `bossEnemyId`, `guaranteedRestAt`, `guaranteedShopAt` |
| `events.json` / `choices.json` | 事件系统 | (预留) |
| `potions.json` | 药水数据 | (预留) |
| `shop.json` | 商店配置 | (预留) |

**ConfigLoader** (`Roguelike/Data/ConfigLoader.cs`) - 启动时 `LoadAll()` 解析 JSON → `AllConfig` 缓存，提供静态查找方法。

---

## 数据流向

```
RunDirector.StartNewRun()
    → new RunData() + relicSystem.Initialize()
    → EnterNextBattle() [battleCount=1]
        → StartCombat(pool, isElite)
            → CreateBattleManager(enemyData, RunData, isElite)
                → BattleManager.InitBattleWithData()
                    → new BattleUnit(player/enemy), DeckPile, HandArea
                    → currentEnemyIntent = SelectEnemyIntent()  // 初始化首个意图
                    → StartPlayerTurn() 协程
                        → ClearDefense → DrawCards → TurnStartEffects
                        → OnEnemyIntentReady?.Invoke()  // 显示敌人意图
                        → CanPlayerAct = true
Player 操作: TryPlayCards() → 牌型识别 → 效果结算 → 遗物/附魔触发
    → 玩家结束回合 → StartEnemyTurn() 协程
        → TurnStartEffects → DoT → Act(执行 currentEnemyIntent)
        → currentEnemyIntent = SelectEnemyIntent()  // 选下一意图
        → 回合结束 → StartPlayerTurn() 循环

战斗结束 → OnBattleOver(isWin)
    → 保存 player.CurrentHp 到 RunData.currentHp
    → 生成奖励 → RewardPanel 显示
    → OnRewardConfirmed → 发放金币/遗物/药水/附魔
    → 如果 Boss 战胜利 → ShowVictory()
    → 否则 EnterNextBattle() [battleCount++]
```

---

## 敌人意图系统（修正后的流程）

```
游戏开始          → 隐藏意图 (BattlePanel.SetBattleManager 清空文本)
    ↓
InitBattleWithData → currentEnemyIntent = SelectEnemyIntent()  // 选好首个
    ↓
玩家回合1开始      → OnEnemyIntentReady 触发
    ↓
BattlePanel.OnEnemyIntentReady → OnEnemyIntentChanged(显示名字+意图)
    ↓
玩家根据意图决策
    ↓
敌方回合1          → 直接执行 currentEnemyIntent (无显示阶段)
    ↓
敌方回合1结束      → currentEnemyIntent = SelectEnemyIntent()  // 选下一回合意图
    ↓ (不触发显示)
玩家回合2开始      → OnEnemyIntentReady 触发 → 显示新意图
    ↓ ... 循环
```

---

## 关键设计原则

1. **逻辑/UI 分离**: BattleManager/RelicSystem 纯 C#，无 UnityEngine 依赖（除 Mathf/Random），通过事件回调通知 UI
2. **精确事件**: 所有数值变化事件携带 `delta`（如 `OnPlayerHpChanged(unit, delta)`），UI 无需重新查询
3. **数据驱动**: 遗物/附魔/敌人意图全部 JSON 配置，`ConfigTableEditor` 支持编辑器内可视化编辑
4. **回合阶段控制**: `CanPlayerAct` 防止回合开始效果未完成时玩家操作
5. **HP 持久化**: 战斗结束保存 `RunData.currentHp`，下一场战斗初始化时读取

---

## 编译/运行

```bash
cd "C:\Users\Op\My project"
dotnet build Assembly-CSharp.csproj
```

---

## 待办/已知问题

- [ ] 事件/商店/休息站系统未实现（acts.json 有配置字段）
- [ ] 药水系统仅有数据结构，无使用逻辑
- [ ] 存档/读档（RunData 序列化）未接入
- [ ] ConfigLoader 部分字段解析兼容性（如 acts.json battles 数组已废弃）
- [ ] BattleManager 未使用字段警告: `enemyAttackMin/Max`, `enemyDefenseValue`, `OnEnemyIntentChanged` 事件