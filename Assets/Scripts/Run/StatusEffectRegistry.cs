using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// 单个状态效果的展示信息（名字/短标签/描述/是否显示层数/是否负面）。
    /// 动态内容（依赖花色、层数、拥有者、战斗实时数据）用委托处理。
    /// </summary>
    public sealed class StatusEffectInfo
    {
        public StatusEffectType Type;

        public string Name;          // 基础名（无动态部分时用）
        public string ShortLabel;    // 图标单字（留空 = 取名字首字）
        public bool ShowCount = true;   // 图标是否显示层数
        public bool ShowDuration = true; // 是否允许显示回合数
        public bool IsNegative;         // 负面状态（会被神器抵消）

        // 动态生成（可为 null，用默认实现）
        public Func<Suit, bool, string> NameFunc;
        public Func<Suit, bool, string> ShortLabelFunc;
        public Func<int, Suit, BattleUnit, string> DescriptionFunc;

        public string GetName(Suit suit, bool useSuit)
            => NameFunc != null ? NameFunc(suit, useSuit) : (Name ?? Type.ToString());

        public string GetShortLabel(Suit suit, bool useSuit)
        {
            if (ShortLabelFunc != null) return ShortLabelFunc(suit, useSuit);
            if (!string.IsNullOrEmpty(ShortLabel)) return ShortLabel;
            var n = GetName(suit, useSuit);
            return string.IsNullOrEmpty(n) ? "?" : n.Substring(0, 1);
        }

        public string GetDescription(int amount, Suit suit, BattleUnit owner)
            => DescriptionFunc != null ? DescriptionFunc(amount, suit, owner) : "";
    }

    /// <summary>
    /// 状态效果注册表：状态的名字 / 短标签 / 描述 / 图标行为的唯一来源。
    /// 新增状态：在这里注册一条即可，UI / 意图文本 / 编辑器下拉都会自动跟着变。
    /// （旧的 StatusEffectIcon 78 个 case、StatusEffectNames、BattlePanel.StatusLabel 已收敛到这里）
    /// </summary>
    public static class StatusEffectRegistry
    {
        private static readonly Dictionary<StatusEffectType, StatusEffectInfo> _infos =
            new Dictionary<StatusEffectType, StatusEffectInfo>();
        private static bool _initialized;

        public static StatusEffectInfo Get(StatusEffectType type)
        {
            EnsureInitialized();
            return _infos.TryGetValue(type, out var i) ? i : null;
        }

        public static bool TryGet(StatusEffectType type, out StatusEffectInfo info)
        {
            EnsureInitialized();
            return _infos.TryGetValue(type, out info);
        }

        public static IReadOnlyCollection<StatusEffectInfo> All
        {
            get { EnsureInitialized(); return _infos.Values; }
        }

        /// <summary>状态显示名（找不到时回退到枚举名）</summary>
        public static string Name(StatusEffectType type)
        {
            var info = Get(type);
            return info != null ? info.GetName(Suit.Spade, false) : type.ToString();
        }

        /// <summary>是否负面状态（会被神器抵消）</summary>
        public static bool IsNegative(StatusEffectType type)
            => Get(type)?.IsNegative ?? false;

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            Reg(StatusEffectType.Poison, "中毒", "中", desc: (a, s, o) => $"回合开始时受到 {a} 点伤害", neg: true);
            Reg(StatusEffectType.Burn, "灼烧", "灼", desc: (a, s, o) => $"回合开始时受到 {a} 点伤害，之后层数减半", neg: true);
            Reg(StatusEffectType.Weaken, "虚弱", "弱", desc: (a, s, o) => $"造成伤害降低 {a * 10}%", neg: true);
            Reg(StatusEffectType.Strength, "力量", "力", desc: (a, s, o) => $"造成伤害 +{a}");
            Reg(StatusEffectType.Dexterity, "敏捷", "敏", desc: (a, s, o) => $"获得防御增加 {a * 10}%");
            Reg(StatusEffectType.Focus, "专注", "专", desc: (a, s, o) => $"造成伤害增加 {a * 10}%");
            Reg(StatusEffectType.Artifact, "神器", "神", desc: (a, s, o) => "抵消一次负面效果");
            Reg(StatusEffectType.Thorns, "荆棘", "荆", desc: (a, s, o) => $"受到攻击时反伤 {a} 点");
            Reg(StatusEffectType.Regeneration, "再生", "再", desc: (a, s, o) => $"回合开始时回复 {a} 点生命");
            Reg(StatusEffectType.Metallicize, "金属化", "金", desc: (a, s, o) => $"回合结束获得 {a} 点防御");
            Reg(StatusEffectType.Intangible, "无形", "无", desc: (a, s, o) => "下次受到的攻击伤害变为 1");
            Reg(StatusEffectType.Vulnerable, "易伤", "易", desc: (a, s, o) => $"受到伤害 +{a * 10}%（每层 +10%）", neg: true);
            Reg(StatusEffectType.Rage, "暴怒", "怒", desc: (a, s, o) => "下一次出牌伤害翻倍（每层对应 1 次，出牌消耗 1 层）");
            Reg(StatusEffectType.DefenseUp, "坚壁", "壁", desc: (a, s, o) => "本回合获得防御翻倍");

            // 附魔遗留增益
            Reg(StatusEffectType.TurnDamageBonus, "顺风耳", "顺", desc: (a, s, o) => $"本回合每次出牌额外造成 {a} 点伤害（回合结束移除）");
            Reg(StatusEffectType.NextTurnDraw, "同花之魂", "魂", desc: (a, s, o) => $"下回合开始时额外抽 {a} 张牌");
            Reg(StatusEffectType.SuitDamageBonus, name: "同花顺之巅", shortLabel: "花",
                nameFunc: (suit, useSuit) => useSuit ? "同花顺之巅 " + SuitSymbol(suit) : "同花顺之巅",
                shortLabelFunc: (suit, useSuit) => SuitSymbol(suit),
                desc: (a, s, o) => $"本场战斗中，每打出一张{SuitName(s)}，额外造成 {a} 点伤害");
            Reg(StatusEffectType.SelfDamage, "双刃剑", "刃", desc: (a, s, o) => $"每回合结束时自伤 {a} 点生命（永久）");
            Reg(StatusEffectType.DrawBonus, "抽牌+", "抽", desc: (a, s, o) => $"本场每回合额外抽 {a} 张牌");
            Reg(StatusEffectType.MulliganBonus, "重抽+", "重", desc: (a, s, o) => $"本场弃牌重抽次数 +{a}");
            Reg(StatusEffectType.NoMulligan, "封印重抽", "封", desc: (a, s, o) => "本场无法弃牌重抽");
            Reg(StatusEffectType.SuitSeal, name: "封禁", shortLabel: "禁",
                nameFunc: (suit, useSuit) => useSuit ? "封禁 " + SuitSymbol(suit) : "封禁",
                shortLabelFunc: (suit, useSuit) => SuitSymbol(suit),
                desc: (a, s, o) => $"本场无法打出{SuitName(s)}");

            // 梅花事件：永久 / 下场修正
            Reg(StatusEffectType.StraightDrawBonus, "顺子抽牌", "顺", desc: (a, s, o) => $"打出顺子时额外抽 {a} 张牌（永久）");
            Reg(StatusEffectType.FlushDrawBonus, "同花抽牌", "同", desc: (a, s, o) => $"打出同花时额外抽 {a} 张牌（永久）");
            Reg(StatusEffectType.FlushDamageBonus, "同花伤害", "花", desc: (a, s, o) => $"打出同花时额外造成 {a} 点伤害（永久）");
            Reg(StatusEffectType.DamageMultiplierMod, "伤害倍率", "伤", desc: (a, s, o) => $"造成伤害 ×{a / 100f:0.##}（永久）");
            Reg(StatusEffectType.FatePowerBonus, "命运积攒", "运", desc: (a, s, o) => $"命运之力每次积攒 +{a}（本场）");
            Reg(StatusEffectType.FateGainSeal, "命运封禁", "禁", desc: (a, s, o) => "本场战斗无法积攒命运之力");
            Reg(StatusEffectType.StraightDrawTemp, "顺子抽牌·临", "临", desc: (a, s, o) => $"本场战斗打出顺子时额外抽 {a} 张牌（仅本场）");
            Reg(StatusEffectType.Challenge, "挑战", "挑",
                desc: (a, s, o) =>
                {
                    int limit = -1, lost = a;
                    var director = Roguelike.RunDirector.Instance;
                    if (director != null)
                    {
                        if (director.RunData != null) limit = director.RunData.challengeHpLimit;
                        if (director.BattleManager != null) lost = director.BattleManager.PlayerHpLostThisBattle;
                    }
                    return limit < 0 ? "本场战斗的挑战已完成" : $"本场战斗失去生命不超过 {limit}（已失去 {lost}）";
                });

            // 敌人主动行动产生的状态
            Reg(StatusEffectType.Taunt, "嘲讽", "嘲", showCount: false,
                desc: (a, s, o) =>
                {
                    string enemyName = "该敌人";
                    var bm = Roguelike.RunDirector.Instance != null ? Roguelike.RunDirector.Instance.BattleManager : null;
                    var list = bm != null ? bm.GetEnemies() : null;
                    int idx = a - 1;
                    if (list != null && idx >= 0 && idx < list.Count && list[idx] != null)
                        enemyName = list[idx].Name;
                    return $"只能选中「{enemyName}」作为目标";
                });
            Reg(StatusEffectType.Charge, "蓄力", "蓄", desc: (a, s, o) => "下次攻击行动伤害翻倍");
            Reg(StatusEffectType.Swallow, "吞噬", "吞",
                desc: (a, s, o) =>
                {
                    var bm = Roguelike.RunDirector.Instance != null ? Roguelike.RunDirector.Instance.BattleManager : null;
                    var cards = bm != null ? bm.GetSwallowedCards(o) : null;
                    if (cards == null || cards.Count == 0)
                        return $"吞掉了 {a} 张牌，该敌人死亡时归还";
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < cards.Count; i++)
                    {
                        if (i > 0) sb.Append("、");
                        sb.Append(CardData.RankLabel(cards[i].rank)).Append(SuitSymbol(cards[i].suit));
                    }
                    return $"吞掉了 {sb}，该敌人死亡时归还";
                });
            Reg(StatusEffectType.Weakness, "弱点", "弱", showCount: false,
                desc: (a, s, o) =>
                {
                    var bm = Roguelike.RunDirector.Instance != null ? Roguelike.RunDirector.Instance.BattleManager : null;
                    var list = bm != null ? bm.GetWeaknesses(o) : null;
                    if (list == null || list.Count == 0) return "每回合刷新弱点牌型";
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append("、");
                        sb.Append(WeaknessInfo.Name(list[i]));
                    }
                    return $"本回合弱点：{sb}（用其它牌型攻击它会使其 +1 力量）";
                });
            Reg(StatusEffectType.Burrow, "遁地", "遁", desc: (a, s, o) => $"受到的攻击伤害固定为 1；还需 {a} 次攻击才会出来");
        }

        // ===== 注册辅助 =====

        private static void Reg(StatusEffectType type,
            string name = null, string shortLabel = null,
            Func<Suit, bool, string> nameFunc = null,
            Func<Suit, bool, string> shortLabelFunc = null,
            Func<int, Suit, BattleUnit, string> desc = null,
            bool showCount = true, bool showDuration = true, bool neg = false)
        {
            _infos[type] = new StatusEffectInfo
            {
                Type = type,
                Name = name,
                ShortLabel = shortLabel,
                NameFunc = nameFunc,
                ShortLabelFunc = shortLabelFunc,
                DescriptionFunc = desc,
                ShowCount = showCount,
                ShowDuration = showDuration,
                IsNegative = neg
            };
        }

        // ===== 花色符号 / 名称（UI 共用）=====

        public static string SuitSymbol(Suit s)
        {
            switch (s)
            {
                case Suit.Spade: return "♠";
                case Suit.Heart: return "♥";
                case Suit.Club: return "♣";
                case Suit.Diamond: return "♦";
                default: return "?";
            }
        }

        public static string SuitName(Suit s)
        {
            switch (s)
            {
                case Suit.Spade: return "黑桃";
                case Suit.Heart: return "红桃";
                case Suit.Club: return "梅花";
                case Suit.Diamond: return "方块";
                default: return "?";
            }
        }
    }
}