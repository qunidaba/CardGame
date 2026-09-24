using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Roguelike;

/// <summary>
/// 单个状态效果图标显示：名称取首字，层数+持续，鼠标悬停显示全名与效果说明
/// </summary>
public class StatusEffectIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image iconImage;
    public TextMeshProUGUI countText;      // 层数
    public TextMeshProUGUI durationText;   // 持续回合
    public TextMeshProUGUI nameText;

    [Header("提示")]
    public float tooltipDelay = 0.3f;

    private StatusEffectType currentType;
    private int currentAmount;
    private int currentDuration;
    private Suit currentSuit;
    private bool currentUseSuit;
    private BattleUnit currentOwner;

    private float hoverTimer;
    private bool isHovering;

    public void Setup(StatusEffectType type, int amount, int duration, Sprite icon, Suit suit = Suit.Spade, bool useSuit = false, BattleUnit owner = null)
    {
        currentType = type;
        currentAmount = amount;
        currentDuration = duration;
        currentSuit = suit;
        currentUseSuit = useSuit;
        currentOwner = owner;

        nameText.text = GetShortLabel(type, suit, useSuit);
        // 「挑战」层数为 0 时也要显示（表示还没失去生命）；「嘲讽」「弱点」的数值无意义，不显示
        bool showCount = (amount > 0 || type == StatusEffectType.Challenge)
                         && type != StatusEffectType.Taunt
                         && type != StatusEffectType.Weakness;
        countText.text = showCount ? amount.ToString() : "";

        if (duration <= 0)
        {
            // 无限持续：不显示回合数
            durationText.text = "";
            durationText.enabled = false;
        }
        else
        {
            durationText.text = duration.ToString();
            durationText.color = Color.white;
            durationText.enabled = true;
        }

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
    }

    // ===== 悬停提示 =====

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        hoverTimer = 0f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        TooltipManager.Instance?.Hide();
    }

    private void Update()
    {
        if (!isHovering) return;

        hoverTimer += Time.unscaledDeltaTime;
        if (hoverTimer >= tooltipDelay)
        {
            TooltipManager.Instance?.Show(
                GetDisplayName(currentType, currentSuit, currentUseSuit),
                GetDescription(currentType, currentAmount, currentSuit, currentOwner),
                GetComponent<RectTransform>());
        }
    }

    // ===== 文本 =====

    /// <summary>图标上显示的单字：同花顺之巅用花色符号，其余取名字首字</summary>
    private string GetShortLabel(StatusEffectType type, Suit suit, bool useSuit)
    {
        if (type == StatusEffectType.SuitDamageBonus) return SuitSymbol(suit);

        // 梅花事件修正：给互相冲突的首字单独指定
        switch (type)
        {
            case StatusEffectType.StraightDrawBonus: return "顺";
            case StatusEffectType.StraightDrawTemp: return "临";
            case StatusEffectType.FlushDrawBonus: return "同";
            case StatusEffectType.FlushDamageBonus: return "花";
            case StatusEffectType.DamageMultiplierMod: return "伤";
            case StatusEffectType.FatePowerBonus: return "运";
            case StatusEffectType.FateGainSeal: return "禁";
            case StatusEffectType.Challenge: return "挑";
        }

        string full = GetDisplayName(type, suit, useSuit);
        return string.IsNullOrEmpty(full) ? "?" : full.Substring(0, 1);
    }

    private string GetDisplayName(StatusEffectType type, Suit suit, bool useSuit)
    {
        switch (type)
        {
            case StatusEffectType.Poison: return "中毒";
            case StatusEffectType.Burn: return "灼烧";
            case StatusEffectType.Weaken: return "虚弱";
            case StatusEffectType.Strength: return "力量";
            case StatusEffectType.Dexterity: return "敏捷";
            case StatusEffectType.Focus: return "专注";
            case StatusEffectType.Artifact: return "神器";
            case StatusEffectType.Thorns: return "荆棘";
            case StatusEffectType.Regeneration: return "再生";
            case StatusEffectType.Metallicize: return "金属化";
            case StatusEffectType.Intangible: return "无形";
            case StatusEffectType.Vulnerable: return "易伤";
            case StatusEffectType.Rage: return "暴怒";
            case StatusEffectType.DefenseUp: return "坚壁";
            case StatusEffectType.TurnDamageBonus: return "顺风耳";
            case StatusEffectType.NextTurnDraw: return "同花之魂";
            case StatusEffectType.SuitDamageBonus: return "同花顺之巅" + (useSuit ? " " + SuitSymbol(suit) : "");
            case StatusEffectType.SelfDamage: return "双刃剑";
            case StatusEffectType.DrawBonus: return "抽牌+";
            case StatusEffectType.MulliganBonus: return "重抽+";
            case StatusEffectType.NoMulligan: return "封印";
            case StatusEffectType.SuitSeal: return "封禁 " + SuitSymbol(suit);
            case StatusEffectType.StraightDrawBonus: return "顺子抽牌";
            case StatusEffectType.StraightDrawTemp: return "顺子抽牌·临";
            case StatusEffectType.FlushDrawBonus: return "同花抽牌";
            case StatusEffectType.FlushDamageBonus: return "同花伤害";
            case StatusEffectType.DamageMultiplierMod: return "伤害倍率";
            case StatusEffectType.FatePowerBonus: return "命运积攒";
            case StatusEffectType.FateGainSeal: return "命运封禁";
            case StatusEffectType.Challenge: return "挑战";
            case StatusEffectType.Taunt: return "嘲讽";
            case StatusEffectType.Charge: return "蓄力";
            case StatusEffectType.Swallow: return "吞噬";
            case StatusEffectType.Weakness: return "弱点";
            case StatusEffectType.Burrow: return "遁地";
            default: return type.ToString();
        }
    }

    private string GetDescription(StatusEffectType type, int amount, Suit suit, BattleUnit owner = null)
    {
        switch (type)
        {
            case StatusEffectType.Poison: return $"回合开始时受到 {amount} 点伤害";
            case StatusEffectType.Burn: return $"回合开始时受到 {amount} 点伤害，之后层数减半";
            case StatusEffectType.Weaken: return $"造成伤害降低 {amount * 10}%";
            case StatusEffectType.Strength: return $"造成伤害 +{amount}";
            case StatusEffectType.Dexterity: return $"获得防御增加 {amount * 10}%";
            case StatusEffectType.Focus: return $"造成伤害增加 {amount * 10}%";
            case StatusEffectType.Artifact: return "抵消一次负面效果";
            case StatusEffectType.Thorns: return $"受到攻击时反伤 {amount} 点";
            case StatusEffectType.Regeneration: return $"回合开始时回复 {amount} 点生命";
            case StatusEffectType.Metallicize: return $"回合结束获得 {amount} 点防御";
            case StatusEffectType.Intangible: return "下次受到的攻击伤害变为 1";
            case StatusEffectType.Vulnerable: return $"受到伤害 +{amount * 10}%（每层 +10%）";
            case StatusEffectType.Rage: return "下一次出牌伤害翻倍（每层对应 1 次，出牌消耗 1 层）";
            case StatusEffectType.DefenseUp: return "本回合获得防御翻倍";
            case StatusEffectType.TurnDamageBonus: return $"本回合每次出牌额外造成 {amount} 点伤害（回合结束移除）";
            case StatusEffectType.NextTurnDraw: return $"下回合开始时额外抽 {amount} 张牌";
            case StatusEffectType.SuitDamageBonus: return $"本场战斗中，每打出一张{SuitName(suit)}，额外造成 {amount} 点伤害";
            case StatusEffectType.SelfDamage: return $"每回合结束时自伤 {amount} 点生命（永久）";
            case StatusEffectType.DrawBonus: return $"本场每回合额外抽 {amount} 张牌";
            case StatusEffectType.MulliganBonus: return $"本场弃牌重抽次数 +{amount}";
            case StatusEffectType.NoMulligan: return "本场无法弃牌重抽";
            case StatusEffectType.SuitSeal: return $"本场无法打出{SuitName(suit)}";
            case StatusEffectType.StraightDrawBonus: return $"打出顺子时额外抽 {amount} 张牌（永久）";
            case StatusEffectType.StraightDrawTemp: return $"本场战斗打出顺子时额外抽 {amount} 张牌（仅本场）";
            case StatusEffectType.FlushDrawBonus: return $"打出同花时额外抽 {amount} 张牌（永久）";
            case StatusEffectType.FlushDamageBonus: return $"打出同花时额外造成 {amount} 点伤害（永久）";
            case StatusEffectType.DamageMultiplierMod: return $"造成伤害 ×{amount / 100f:0.##}（永久）";
            case StatusEffectType.FatePowerBonus: return $"命运之力每次积攒 +{amount}（本场）";
            case StatusEffectType.FateGainSeal: return "本场战斗无法积攒命运之力";
            case StatusEffectType.Charge: return "下次攻击行动伤害翻倍";
            case StatusEffectType.Swallow:
            {
                // 一个 buff 汇总所有被吞的牌
                var bm = Roguelike.RunDirector.Instance != null ? Roguelike.RunDirector.Instance.BattleManager : null;
                var cards = bm != null ? bm.GetSwallowedCards(owner) : null;
                if (cards == null || cards.Count == 0)
                    return $"吞掉了 {amount} 张牌，该敌人死亡时归还";

                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < cards.Count; i++)
                {
                    if (i > 0) sb.Append("、");
                    sb.Append(CardData.RankLabel(cards[i].rank)).Append(SuitSymbol(cards[i].suit));
                }
                return $"吞掉了 {sb}，该敌人死亡时归还";
            }
            case StatusEffectType.Weakness:
            {
                var bm = Roguelike.RunDirector.Instance != null ? Roguelike.RunDirector.Instance.BattleManager : null;
                var list = bm != null ? bm.GetWeaknesses(owner) : null;
                if (list == null || list.Count == 0)
                    return "每回合刷新弱点牌型";

                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append("、");
                    sb.Append(WeaknessInfo.Name(list[i]));
                }
                return $"本回合弱点：{sb}（用其它牌型攻击它会使其 +1 力量）";
            }
            case StatusEffectType.Burrow:
                return $"受到的攻击伤害固定为 1；还需 {amount} 次攻击才会出来";
            case StatusEffectType.Taunt:
            {
                // amount 存的是 敌人下标 + 1
                string enemyName = "该敌人";
                var bm = Roguelike.RunDirector.Instance != null ? Roguelike.RunDirector.Instance.BattleManager : null;
                var list = bm != null ? bm.GetEnemies() : null;
                int idx = amount - 1;
                if (list != null && idx >= 0 && idx < list.Count && list[idx] != null)
                    enemyName = list[idx].Name;
                return $"只能选中「{enemyName}」作为目标";
            }
            case StatusEffectType.Challenge:
            {
                // 层数 = 已失去血量；上限从 RunData 实时读取
                int limit = -1, lost = amount;
                var director = Roguelike.RunDirector.Instance;
                if (director != null)
                {
                    if (director.RunData != null) limit = director.RunData.challengeHpLimit;
                    if (director.BattleManager != null) lost = director.BattleManager.PlayerHpLostThisBattle;
                }
                return limit < 0
                    ? "本场战斗的挑战已完成"
                    : $"本场战斗失去生命不超过 {limit}（已失去 {lost}）";
            }
            default: return "";
        }
    }

    private static string SuitSymbol(Suit s)
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

    private static string SuitName(Suit s)
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
