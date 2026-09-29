using System.Collections.Generic;

namespace Roguelike.Data
{
    // ===== Enemy =====
    public class EnemyData
    {
        public int id;
        public string name;
        public string image;                              // 单张立绘 或 帧文件夹路径（Resources）
        public List<string> frames = new List<string>();  // 多帧名称（图集/切片），按顺序循环播放
        public float frameRate = 0f;                      // 动画帧率（0 = 用 BattlePanel 的默认值）
        public string burrowImage;                        // 遁地动画（Resources 文件夹路径，可选）
        public List<string> burrowFrames = new List<string>(); // 遁地动画显式帧名（图集用，可选）
        public string emergeImage;                        // 出来动画（Resources 文件夹路径，可选）
        public List<string> emergeFrames = new List<string>(); // 出来动画显式帧名（图集用，可选）
        public float imageWidth = 0f;                     // 立绘宽（0 = 用预制体默认）
        public float imageHeight = 0f;                    // 立绘高（0 = 用预制体默认）
        public float imageScale = 0f;                     // 统一缩放（0 = 不缩放；与宽高同时存在时叠加）
        public float imageOffsetX = 0f;                   // 立绘位置偏移
        public float imageOffsetY = 0f;
        public int hp;
        public int maxHp;
        public List<IntentData> intents = new List<IntentData>();
        public List<int> goldRange = new List<int> { 10, 20 };
        public bool relicDrop;
        public bool bossRelic;
        public bool isBoss;
        public List<string> pools = new List<string>();
        public string passive;                            // 被动（EnemyPassive 名称，如 Weakness；留空 = 无）
        public int polluteCount = 2;                       // 被动 Pollute：每回合污染的牌数
    }

    public class IntentData
    {
        public string type;           // Attack, Defense, Buff(强化自己), Debuff(给玩家上状态), MultiAttack, Multi, Special
        public int value;
        public bool multiHit;
        public int hitCount = 1;
        public string description;
        public int weight = 100;
        public string status;         // Buff/Debuff 施加的状态（StatusEffectType 名称，如 Strength/Weaken）
        public int duration = -1;     // 状态持续回合（-1=永久/战斗结束）
        public bool battleStart;      // 第一回合必定出场（后续回合也留在随机池里，仍可能抽到）
        public List<IntentData> actions;  // type = Multi 时依次执行的子行动
    }

    // ===== Relic =====
    public class RelicData
    {
        public int id;
        public string name;
        public string description;
        public string rarity;         // Common, Uncommon, Rare, Boss, Shop
        public List<RelicEffectData> effects = new List<RelicEffectData>();
        public int maxSlots = 1;
        public bool eventOnly = false;   // 事件专属：不进随机奖励/商店/随机遗物
        public string iconPath;       // Resources路径下的图标文件名（可选）
    }

    public class RelicEffectData
    {
        public string trigger;        // OnCombatStart, OnTurnStart, OnCardPlayed, OnHandType, OnTakeDamage, OnKillEnemy, OnGoldGain, OnShopEnter, OnRest, OnEventChoice, OnEnchantAdded, OnBattleWin, OnDealDamage, OnPotionUse, Passive
        public string type;           // EnchantRandomCard, AddDamage, AddDefense, AddDraw, AddHeal, AddGold, MultiplyGold, MultiplyPotionEffect, AddPotionSlot, AddMaxHp, HealOnDamage, ModifyShopPrice, NextBattleEnemyBuff, NextBattleStartHp, ApplyPoison, ApplyBurn, ApplyWeaken
        public object value;          // int, float, string, bool
        public string condition;      // 可选条件，如 "StraightFlush5", "DamageDealt"
        public int duration;          // 持续回合数（-1=战斗结束不移除，默认-1）
    }

    // ===== Enchantment =====
    public class EnchantmentData
    {
        public int id;
        public string name;
        public string description;
        public string trigger;        // OnPlay, OnHandType, OnCombatStart, OnTurnStart, OnDraw, OnDiscard, OnTakeDamage, OnKill, OnGoldGain, OnPotionUse
        public List<EnchantmentEffectData> effects = new List<EnchantmentEffectData>();
        public string rarity = "Common";  // Common 普通 / Rare 稀有 / Epic 史诗
        public int weight = 10;
        public bool isUnique = true;
        public string pool;           // 事件随机附魔池：HandType 牌型类 / Buff 增益类（留空 = 不进事件池）
        public string exclusiveGroup; // 互斥组：同一张牌上同组只能存在一个（如 万能牌/变色/镜牌 = Modifier）
        public string condition;      // 可选条件，如 "StraightFlush5", "DamageDealt"
    }

    public class EnchantmentEffectData
    {
        public string type;           // AddDamage, AddDefense, AddDraw, AddHeal, AddGold, DrawSpecificCard, DuplicateHandType, TransformHandType, ApplyPoison, ApplyBurn, ApplyWeaken, HealOnDamage, NextPlayDamageMult, ...
        public object value;          // int, float
        public int duration = -1;     // 持续回合数（-1=永久/战斗结束，>0 为具体回合数）
        public string status;         // ApplySelfStatus/ApplyEnemyStatus 时指定 StatusEffectType 名称
    }

    // ===== Event =====
    public class EventData
    {
        public int id;
        public string title;
        public string description;
        public string theme;          // 花色命运主题：Spade/Heart/Club/Diamond
        public string condition;      // 出现条件（留空=无条件）：HasMainDestiny 等
        public bool once = false;     // 一局只出现一次
        public List<EventOptionData> options = new List<EventOptionData>();
    }

    public class EventOptionData
    {
        public string text;
        public string condition;      // 选项出现条件（留空=无条件）：如 "HasRelic:30"
        public List<EventResultData> results = new List<EventResultData>();
    }

    public class EventResultData
    {
        public string type;           // GainGold, LoseGold, Heal, LoseMaxHp, GainMaxHp, EnchantRandomCard, EnchantSpecificCard, UpgradeEnchantment, RemoveEnchantment, RemoveAllEnchantmentsRandomCard, GainRelic, LoseRelic, GainPotion, NextBattleStartHp, Gamble, StartCombat, NextBattleEnemyBuff
        public object value;          // int, float, string
        public int count;             // 用于 EnchantRandomCard count
        public int tierMin;           // 用于 EnchantRandomCard tierMin
        public string rarity;         // 用于 GainRelic rarity
        public string suit;           // 用于指定花色的效果（AddDestinyPointSuit / SealSuitNextBattle 等）
        public int enemyId;           // 用于 StartCombatWithReward：事件专属战斗的敌人 id
        public string require;        // 选牌限制：如 "Enchant:32"（只能选拥有该附魔的牌）
        public string status;         // 用于 NextBattleStartStatus：StatusEffectType 名称
        public bool preview = false;  // 是否在选项里提前展示具体物品（拍卖会等）
        public int weight = 1;        // RandomOutcome 分支权重
        public List<EventResultData> outcomes;  // RandomOutcome 的加权分支
        public List<EventResultData> win;   // Gamble win
        public List<EventResultData> lose;  // Gamble lose
    }

    // ===== Potion =====
    public class PotionData
    {
        public int id;
        public string name;
        public string description;
        public string rarity = "Common";   // Common 普通 / Rare 稀有 / Epic 史诗
        public PotionEffectData effect = new PotionEffectData();
        public string icon;
    }

    public class PotionEffectData
    {
        public string type;           // HealPercent, NextPlayDamageMult, GainDefense, DrawCards, ApplyStatus
        public object value;          // float, int
        public int duration = -1;     // 状态持续回合（-1=永久/战斗结束）
        public string status;         // ApplyStatus：StatusEffectType 名称（Poison/Strength/Regeneration/...）
        public string target;         // ApplyStatus：作用目标 "enemy" / "self"（默认 enemy）
    }

    // ===== Acts =====
    public class ActData
    {
        public int actId;
        public string name;
        public int bossEnemyId;       // Boss 敌人 ID
        public int totalBattles = 0;              // 本章打几场进 Boss（0 = 用 RunDirector 里的默认配置）
        public string commonPool;                 // 前半普通怪池
        public string elitePool;                  // 精英怪池（不分前后半）
        public string commonPoolLate;             // 后半普通怪池（留空则沿用前半）
        public List<float> eliteChancePerBattle = new List<float>();  // 本章每场精英概率
        public List<int> guaranteedRestAt = new List<int>();
        public List<int> guaranteedShopAt = new List<int>();
    }

    // ===== Encounter（固定敌人组合）=====
    public class EncounterData
    {
        public int id;
        public string name;                          // 显示名（日志/调试）
        public string pool;                          // act1_common / act1_elite / act2_common / act2_elite / act1_boss ...
        public int weight = 1;                       // 同池内加权随机
        public List<int> enemies = new List<int>();  // 敌人 id，按出场顺序（可重复）
    }

    // ===== 完整配置容器 =====
    public class AllConfig
    {
        public List<EnemyData> enemies = new List<EnemyData>();
        public List<RelicData> relics = new List<RelicData>();
        public List<EnchantmentData> enchantments = new List<EnchantmentData>();
        public List<EventData> events = new List<EventData>();
        public List<PotionData> potions = new List<PotionData>();
        public List<ActData> acts = new List<ActData>();
        public List<EncounterData> encounters = new List<EncounterData>();
    }

    /// <summary>稀有度（Common 普通 / Rare 稀有 / Epic 史诗）的名称、金币价值、显示颜色</summary>
    public static class RarityUtil
    {
        public static string Name(string rarity)
        {
            switch (rarity)
            {
                case "Epic": return "史诗";
                case "Rare": return "稀有";
                case "Curse": return "诅咒";
                case "Event": return "事件";
                case "Common": return "普通";
                default: return string.IsNullOrEmpty(rarity) ? "普通" : rarity;
            }
        }

        /// <summary>遗物槽满/放弃时折算的金币</summary>
        public static int GoldValue(string rarity)
        {
            switch (rarity)
            {
                case "Epic": return 150;
                case "Rare": return 80;
                case "Curse": return 0;
                case "Event": return 100;
                default: return 40;
            }
        }

        public static string ColorHex(string rarity)
        {
            switch (rarity)
            {
                case "Epic": return "#FF8C00";   // 橙
                case "Rare": return "#00D8FF";   // 青
                case "Curse": return "#B04CFF";  // 紫
                case "Event": return "#E8C86A";  // 金
                default: return "#FFFFFF";       // 白
            }
        }

        /// <summary>稀有度是否满足筛选条件（filter 为空 / Any = 任意；RareOrAbove = 稀有及以上）</summary>
        public static bool Matches(string rarity, string filter)
        {
            // 诅咒只被「诅咒」筛选选中，不参与随机池
            if (rarity == "Curse") return filter == "Curse";
            // 事件专属只被「事件」筛选选中，不参与任何随机池
            if (rarity == "Event") return filter == "Event";

            if (string.IsNullOrEmpty(filter) || filter == "Any") return true;
            if (filter == "RareOrAbove") return Tier(rarity) >= 2;
            if (filter == "CommonOrAbove") return Tier(rarity) >= 1;
            return rarity == filter;
        }

        /// <summary>稀有度 → 等级（普通 1 / 稀有 2 / 史诗 3；诅咒 0）</summary>
        public static int Tier(string rarity)
        {
            switch (rarity)
            {
                case "Epic": return 3;
                case "Rare": return 2;
                case "Curse": return 0;
                case "Event": return 0;
                default: return 1;
            }
        }

        /// <summary>等级 → 稀有度</summary>
        public static string FromTier(int tier)
        {
            if (tier >= 3) return "Epic";
            if (tier == 2) return "Rare";
            return "Common";
        }
    }
}