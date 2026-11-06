using System.Collections.Generic;
using Roguelike.Data;

/// <summary>
/// BattleManager 教学模式（分部）：有序牌堆、跳过开局自动抽牌、脚本意图、直接下发手牌。
/// </summary>
public partial class BattleManager
{
    /// <summary>教学模式开关</summary>
    public bool TutorialMode;

    /// <summary>教学：有序牌堆（末尾=牌堆顶，先被抽到；不洗牌）</summary>
    public List<CardData> TutorialDeck = new List<CardData>();

    /// <summary>教学：固定敌人意图序列（每回合出队一个）</summary>
    public Queue<IntentData> TutorialIntentQueue;

    /// <summary>教学：当前是否放行"出牌/结束回合/重抽"（由教学控制器逐步开启，拦掉玩家乱点真实按钮）</summary>
    public bool TutorialAllow;

    /// <summary>一次出牌成功后触发（教学推进用）</summary>
    public event System.Action OnPlayerPlayedCards;

    /// <summary>一次弃牌重抽成功后触发（教学推进用）</summary>
    public event System.Action OnPlayerMulligan;

    /// <summary>教学：直接替换当前手牌（保留对象引用，便于教学精确选中）</summary>
    public void TutorialSetHand(List<CardData> cards)
    {
        if (handArea == null) return;
        var old = new List<CardData>(handArea.HandCards);
        if (old.Count > 0) handArea.RemoveCards(old);
        handArea.ClearSelection();
        if (cards != null && cards.Count > 0)
            handArea.AddCards(new List<CardData>(cards));

        // 把发到手上的牌从牌堆里移除，避免牌堆里还留着当前手牌
        if (deckPile != null && cards != null)
        {
            var deckCards = deckPile.GetCards();
            foreach (var c in cards)
            {
                if (c == null) continue;
                var match = deckCards.Find(d => d.rank == c.rank && d.suit == c.suit);
                if (match != null) { deckPile.RemoveFromDeck(match); deckCards.Remove(match); }
            }
        }
    }
}
