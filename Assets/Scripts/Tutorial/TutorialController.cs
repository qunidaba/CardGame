using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;

/// <summary>
/// 新手教学关卡（聚光灯强引导）：
///  - 不自动选牌，玩家自己点选；只把"该选的牌"区域留出亮区（其余压暗并拦截点击）。
///  - 选齐后，只把"出牌/结束回合/弃牌重抽"真实按钮留出亮区，由玩家点真实按钮。
/// 回合1 顺子→同花顺→同花（剩2张）；回合2 对子→三条→葫芦；回合3 杂牌→重抽→四条击杀；结束回主菜单。
/// </summary>
public class TutorialController : MonoBehaviour
{
    private BattleManager bm;
    private BattlePanel panel;
    private UIManager ui;
    private int turn;

    private GameObject overlay;
    private RectTransform overlayRt;
    private readonly List<Image> dimPool = new List<Image>();
    private bool _vfxDone;
    private TextMeshProUGUI titleTxt, bodyTxt;
    private TMP_FontAsset font;

    private GameObject bubbleObj;
    private GameObject centerBubble;
    private TextMeshProUGUI centerTitle, centerBody;
    private Button introCatcher;
    private bool _introClick;
    private bool _introDone;
    private bool _previewIntroduced;
    private CardData _pileD9, _pileDK, _pileDA;
    private List<RectTransform> _liveRects;

    private bool _selecting;
    private HashSet<CardData> _selectTargets;

    private bool _played, _mulliganed;
    private int _destinyPhase;   // 0=战斗教学 1=命格讲解 2=命格实战 3=结算
    private List<CardData> _destinyHand, _destinyFlush;

    private static readonly Suit S = Suit.Spade, H = Suit.Heart, Cc = Suit.Club, D = Suit.Diamond;
    private const float SpotPad = 14f;

    public static void Begin(UIManager uiManager)
    {
        var go = new GameObject("[Tutorial]");
        DontDestroyOnLoad(go);
        var c = go.AddComponent<TutorialController>();
        c.ui = uiManager;
        c.SetupBattle();
    }

    private static CardData C(int rank, Suit suit) => new CardData { rank = rank, suit = suit };

    private void SetupBattle()
    {
        BuildOverlay();

        var run = new RunData();
        run.MaxHp = 50;
        run.CurrentHp = 50;
        // 命运一览：给四花色各分配一个事件（教学里展示用）
        run.GenerateSuitEventMap(12345);

        var enemy = new EnemyData
        {
            id = 9001,
            name = "稻草人",
            hp = 77,
            maxHp = 77,
            intents = new List<IntentData> { new IntentData { type = "Attack", value = 6, hitCount = 1, weight = 100 } },
        };

        bm = new BattleManager { TutorialMode = true };
        bm.TutorialIntentQueue = new Queue<IntentData>(new[]
        {
            new IntentData { type = "Attack", value = 6, hitCount = 1, weight = 100 },
            new IntentData { type = "Attack", value = 19, hitCount = 1, weight = 100 },
            new IntentData { type = "Attack", value = 25, hitCount = 1, weight = 100 },
        });
        // 完整牌堆（固定牌序）：把要摸的 3 张放到堆顶，抽牌顺序 9♦→K♦→A♦（Draw 从末尾取）
        var deck = DeckBuilder.BuildStandardDeck();
        _pileD9 = deck.Find(c => c.suit == D && c.rank == 9);
        _pileDK = deck.Find(c => c.suit == D && c.rank == 13);
        _pileDA = deck.Find(c => c.suit == D && c.rank == 14);
        if (_pileD9 != null) deck.Remove(_pileD9);
        if (_pileDK != null) deck.Remove(_pileDK);
        if (_pileDA != null) deck.Remove(_pileDA);
        if (_pileDA != null) deck.Add(_pileDA);
        if (_pileDK != null) deck.Add(_pileDK);
        if (_pileD9 != null) deck.Add(_pileD9);
        bm.TutorialDeck = deck;

        RunDirector.Instance?.SetBattleManagerForTutorial(bm);

        bm.OnPlayerTurnStart += OnPlayerTurnStart;
        bm.OnPlayerTurnEnd += ClearOverlay;
        bm.OnBattleOver += OnBattleOver;
        bm.OnPlayerPlayedCards += () => _played = true;
        bm.OnPlayerMulligan += () => _mulliganed = true;

        bm.InitBattleWithData(new List<EnemyData> { enemy }, run, false, null);

        panel = (ui != null ? ui : UIManager.Instance)?.ShowPanel<BattlePanel>();
        panel?.SetBattleManager(bm);
        if (panel != null) panel.OnPlayVfxDone += () => _vfxDone = true;
    }

    private void OnPlayerTurnStart()
    {
        turn = bm != null ? bm.TurnsElapsed : 1;
        StartCoroutine(RunTurn(turn));
    }

    private void Update()
    {
        // 目标元素可能还在布局/动画中：每帧跟踪重算高亮，保证框始终贴在当前可见位置
        if (_liveRects != null && _liveRects.Count > 0 && BoundsOf(_liveRects, out var lmin, out var lmax))
            ShowSpotlightScreenRect(lmin, lmax);

        // 选牌阶段：把玩家误选的非目标牌自动取消，保证只能选"高亮的牌"
        if (!_selecting || _selectTargets == null || bm == null) return;
        var hand = bm.GetHandArea();
        foreach (var c in hand.GetSelectedCards())
        {
            if (!_selectTargets.Contains(c))
            {
                hand.ToggleSelect(c);
                bm.OnCardSelectionChanged();
                break;
            }
        }
    }

    // ===== 回合脚本 =====
    private IEnumerator RunTurn(int t)
    {
        if (t == 1) yield return Turn1();
        else if (t == 2) yield return Turn2();
        else if (t == 3) yield return Turn3();
    }

    private IEnumerator Turn1()
    {
        var straight = new List<CardData> { C(3, S), C(4, H), C(5, Cc) };
        var sf = new List<CardData> { C(9, Cc), C(10, Cc), C(11, Cc) };
        var f2 = C(2, D); var f6 = C(6, D);
        var d9 = _pileD9; var dK = _pileDK; var dA = _pileDA;
        var lA = C(2, S); var lB = C(13, H);

        bm.TutorialSetHand(new List<CardData> { straight[0], straight[1], straight[2], sf[0], sf[1], sf[2], f2, f6, lA, lB });

        if (!_introDone) { yield return IntroRoutine(); _introDone = true; }

        // 第一步：顺子（先气泡说明→点击→只留遮罩选牌），选完后首次介绍牌型/效果预览
        yield return GuideSelect(straight, "顺子（3~5 张连续点数）",
            "顺子非常重要：可选 3~5 张连续点数的牌，很常见，而且打出可以摸牌。试着选中高亮的这 3 张（3♠ 4♥ 5♣）。（可以点击选中或者滑动快速选中）");
        if (!_previewIntroduced)
        {
            _previewIntroduced = true;
            yield return IntroRegionStep(PreviewRects(), "牌型预览 / 效果预览", "选中牌后，这里会显示【牌型】与预计效果：伤害 / 防御 / 抽牌。");
        }
        yield return GuideClickPlay();

        // 打出一个顺子后：介绍牌堆/弃牌堆，并让玩家点弃牌堆查看内容
        yield return ExplainPiles();

        yield return GuidePlay(sf, "同花顺（3~5 张连续点数同花色）", "同花顺在触发各种效果时同时算作同花和顺子，伤害高的同时拥有比顺子更高的摸牌量！选中高亮的 3 张（9♣ 10♣ J♣）。");
        yield return GuidePlay(new List<CardData> { f2, f6, d9, dK, dA }, "同花（3~5 张同花色）", "同花没有摸牌的能力，但是拥有比顺子更高的伤害和防御。选中高亮的 5 张。");
        yield return GuideEndTurn("基本上每回合都是尽可能多的利用上手牌，出牌的次数没有限制，可以打到手牌无法再凑出任何牌型为止，现在剩余的牌出不出去了，点击回合结束");
    }

    private IEnumerator Turn2()
    {
        var pair = new List<CardData> { C(3, S), C(3, H) };
        var triple = new List<CardData> { C(8, S), C(8, H), C(8, D) };
        var fh = new List<CardData> { C(5, Cc), C(5, D), C(5, H), C(9, S), C(9, H) };

        bm.TutorialSetHand(new List<CardData>
        {
            pair[0], pair[1], triple[0], triple[1], triple[2], fh[0], fh[1], fh[2], fh[3], fh[4]
        });
        yield return WaitHandSettled();

        yield return IntroRegionStep(EnemyRects(), "注意：敌方攻击很高",
            "这回合敌人会攻击 19 点！硬吃扛不住——要善用【高防御牌型】（对子 / 三条 / 葫芦）先叠盾挡住。");

        yield return GuidePlay(pair, "对子（2张相同点数的牌）", "点选 3♠3♥ 两张，打出【对子】，获得防御。");
        yield return GuidePlay(triple, "三条（3张相同点数的牌）", "点选 8♠8♥8♦ 三张，打出【三条】，继续叠防御。");
        yield return GuidePlay(fh, "葫芦（三条+对子）", "点选 5♣5♦5♥+9♠9♥，打出【葫芦】，防御拉满。");
        yield return GuideEndTurn("防御叠完了，点结束回合，看看敌人的攻击能否打光你的护盾。");
    }

    private IEnumerator Turn3()
    {
        // 一堆对子 + 连不起来的单牌（手牌不强，引导弃牌重抽）
        var junk = new List<CardData>
        {
            C(3, S), C(3, H),    // 对子 3
            C(7, D), C(7, Cc),   // 对子 7
            C(9, S), C(9, H),    // 对子 9
            C(2, D), C(5, Cc), C(11, D), C(13, S)   // 散牌 2 / 5 / J / K
        };
        bm.TutorialSetHand(junk);

        yield return GuideSelect(junk, "弃牌重抽", "这手牌不好，这时候要善用弃牌重抽功能，可以弃置任意数量的手牌并摸相同数量的牌，每回合限2次。现在试着选中所有的手牌。");
        yield return ExplainBubble(panel != null && panel.mulliganButton != null ? panel.mulliganButton.transform : null,
            "弃牌重抽", "点高亮的【弃牌重抽】按钮，换一手好牌。");
        ShowSpotlightRect(Rt(panel.mulliganButton));
        _mulliganed = false;
        yield return new WaitUntil(() => _mulliganed);
        ClearOverlay();

        var quad = new List<CardData> { C(10, S), C(10, H), C(10, D), C(10, Cc) };
        bm.TutorialSetHand(new List<CardData>
        {
            quad[0], quad[1], quad[2], quad[3],
            C(2, S), C(4, H), C(6, Cc), C(8, D), C(13, S), C(14, H)
        });

        yield return GuidePlay(quad, "四条（四张相同点数的牌）", "抽中了四条！四条非常稀有，不过拥有高伤、高防、回血、摸牌的强大能力，现在打出四条10，终结敌人！");
    }

    // ===== 引导动作 =====
    /// <summary>先气泡说明，玩家点击后气泡消失，只留下遮罩。</summary>
    private IEnumerator ExplainBubble(Transform anchor, string title, string body)
    {
        if (bubbleObj != null) bubbleObj.SetActive(false);
        ShowFullDim();
        centerBubble.SetActive(true);
        LayoutIntroBubble(anchor as RectTransform, title, body);
        yield return WaitIntroClick();
        centerBubble.SetActive(false);
        ClearOverlay();   // 没有说明文字时撤掉遮罩
    }

    private IEnumerator GuideSelect(List<CardData> cards, string title, string body)
    {
        yield return WaitHandSettled();   // 等牌完全落稳再开始指引
        _selectTargets = new HashSet<CardData>(cards);
        _selecting = true;

        // 先说明（点击消失），再只留遮罩让玩家自己选
        yield return ExplainBubble(panel != null ? panel.handContainer : null, title, body);

        ClearOverlay();   // 等牌抽完期间不需要遮罩
        int handCount = bm != null ? bm.GetHandArea().HandCards.Count : 0;
        float wait = 0f;
        // 等“全部”手牌都抽到手里，再开始选牌
        while (wait < 4f && (handCount <= 0 || HandRects().Count < handCount)) { wait += Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(0.15f);

        ShowSpotlightForCards(cards);
        yield return new WaitUntil(() => AllSelected(cards));
        _selecting = false;
        _selectTargets = null;
    }

    private bool CardsExist(List<CardData> cards)
    {
        if (panel == null || panel.handContainer == null) return false;
        var set = new HashSet<CardData>(cards);
        int found = 0;
        for (int i = 0; i < panel.handContainer.childCount; i++)
        {
            var cu = panel.handContainer.GetChild(i).GetComponent<CardUI>();
            if (cu != null && set.Contains(cu.GetCardData())) found++;
        }
        return found >= cards.Count;
    }

    private IEnumerator GuidePlay(List<CardData> cards, string title, string body)
    {
        yield return GuideSelect(cards, title, body);
        yield return GuideClickPlay();
    }

    /// <summary>选好后：先气泡说明，点击后只留遮罩在【出牌】按钮</summary>
    private IEnumerator GuideClickPlay()
    {
        yield return ExplainBubble(panel != null && panel.playButton != null ? panel.playButton.transform : null,
            "出牌", "选好了！点击高亮的【出牌】按钮结算。");
        ShowSpotlightRect(Rt(panel.playButton));
        _played = false;
        yield return new WaitUntil(() => _played);

        // 出牌一按下就清遮罩，让攻击动画可见；命中后再进下一步
        ClearOverlay();
        if (panel != null && panel.IsPlayVfxActive)
        {
            _vfxDone = false;
            float t = 0f;
            while (!_vfxDone && t < 3f) { t += Time.deltaTime; yield return null; }
        }

        // 出牌/抽牌/落位动画全部结束，再进行下一步指引
        yield return WaitHandSettled();
    }

    /// <summary>等手牌动画（抽牌、落位、平滑移动）全部结束，再推进教学。</summary>
    private IEnumerator WaitHandSettled()
    {
        yield return null;   // 让本帧的动画批次先排上
        float t = 0f;
        while (t < 5f && panel != null && panel.IsHandBusy)
        {
            t += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(0.08f);   // 收尾缓冲
    }

    private IEnumerator GuideEndTurn(string body)
    {
        yield return ExplainBubble(panel != null && panel.endTurnButton != null ? panel.endTurnButton.transform : null,
            "结束回合", body);
        ShowSpotlightRect(Rt(panel.endTurnButton));
        yield break;   // 下一回合由 OnPlayerTurnStart 驱动
    }

    // ===== 牌堆 / 弃牌堆 说明 + 引导查看弃牌堆 =====
    private static bool IsPileViewerOpen()
        => UIManager.Instance != null && UIManager.Instance.IsPanelActive("DiscardPickerPanel");

    private void HideDims()
    {
        foreach (var img in dimPool) if (img != null) img.gameObject.SetActive(false);
    }

    /// <summary>既没有高亮也没有说明时：把遮罩和气泡都收起来。</summary>
    private void ClearOverlay()
    {
        HideDims();
        if (centerBubble != null) centerBubble.SetActive(false);
    }

    private IEnumerator ExplainPiles()
    {
        LogFrame("deck", panel != null && panel.deckCountText != null ? panel.deckCountText.rectTransform : null);
        LogFrame("discard", panel != null && panel.discardCountText != null ? panel.discardCountText.rectTransform : null);

        // 用单孔分别框（单孔对准已验证），依次介绍牌堆、弃牌堆
        if (panel != null && panel.deckCountText != null)
            yield return IntroRegionStep(new List<RectTransform> { panel.deckCountText.rectTransform }, "牌堆",
                "左边是【牌堆】，抽牌会从这里来。点击可以查看里面的内容。");
        if (panel != null && panel.discardCountText != null)
            yield return IntroRegionStep(new List<RectTransform> { panel.discardCountText.rectTransform }, "弃牌堆",
                "右边是【弃牌堆】，打出的牌和重抽弃掉的牌会进这里。同样点击可以查看里面的内容。");

        yield return IntroRegionStep(null, "弃牌堆会洗回牌堆",
            "每回合结束后，弃牌堆的牌会重新洗回牌堆，第二回合在洗好的牌堆中抽牌。");
    }

    private void LogFrame(string name, RectTransform rt)
    {
        if (rt == null) { Debug.Log($"[TUT] {name} null"); return; }
        var wc = new Vector3[4];
        rt.GetWorldCorners(wc);
        var hole = HoleOf(rt, 0f);
        var canvas = panel != null ? panel.GetComponentInParent<Canvas>() : null;
        Debug.Log($"[TUT] {name} worldBL={wc[0]} worldTR={wc[2]} lossyScale={rt.lossyScale} | hole={hole} | overlayRect={overlayRt.rect} overlayLossyScale={overlayRt.lossyScale} canvasScaler={(canvas != null ? canvas.scaleFactor : 0f)} screen={Screen.width}x{Screen.height}");
    }

    private bool AllSelected(List<CardData> cards)
    {
        var sel = new HashSet<CardData>(bm.GetHandArea().GetSelectedCards());
        foreach (var c in cards) if (!sel.Contains(c)) return false;
        return true;
    }

    private void OnBattleOver(bool win)
    {
        if (!win) return;
        if (_destinyPhase == 2) { StartCoroutine(DestinySettleRoutine()); return; }
        if (_destinyPhase == 0) StartCoroutine(EnterDestinyRoutine());
    }

    // ===== 命格 / 命运花色 教学（接在战斗教学之后）=====

    private IEnumerator EnterDestinyRoutine()
    {
        _destinyPhase = 1;
        if (bm != null) bm.OnBattleOver -= OnBattleOver;
        yield return new WaitForSeconds(1.4f);
        ClearOverlay();
        yield return ExplainBubble(null, "战斗胜利！", "接下来进入第二场战斗，认识你的【命格】与【命运花色】。");
        StartDestinyBattle();
    }

    private void StartDestinyBattle()
    {
        _destinyPhase = 2;

        // 开场即给黑桃 Lv.1 命格：命格值 2、主命格=黑桃、命运之力 7/12（再打 5 张黑桃正好攒满）
        var run = bm != null ? bm.GetRunData() : null;
        if (run != null)
        {
            run.mainDestinySuit = (int)S;
            run.destinyPoints[(int)S] = 2;
            run.fatePower = 7;
            run.NotifyDestinyChanged();
        }

        // 黑桃同花（5 张同花色、点数不连续 → 判为同花，不是同花顺）
        var f2 = C(2, S); var f5 = C(5, S); var f8 = C(8, S); var fJ = C(11, S); var fK = C(13, S);
        _destinyFlush = new List<CardData> { f2, f5, f8, fJ, fK };
        _destinyHand = new List<CardData> { f2, f5, f8, fJ, fK };

        var bm2 = new BattleManager { TutorialMode = true };
        bm2.TutorialDeck = new List<CardData>(_destinyHand);
        bm2.TutorialIntentQueue = new Queue<IntentData>(new[]
        {
            new IntentData { type = "Attack", value = 3, hitCount = 1, weight = 100 },
        });
        bm2.OnBattleOver += OnBattleOver;
        bm2.OnPlayerPlayedCards += () => _played = true;

        // 血量够厚：黑桃 Lv1 的 +2 力量让同花打出 22，主动技再补刀
        var enemy = new EnemyData
        {
            id = 9002,
            name = "稻草人",
            hp = 30,
            maxHp = 30,
            intents = new List<IntentData> { new IntentData { type = "Attack", value = 3, hitCount = 1, weight = 100 } },
        };

        RunDirector.Instance?.SetBattleManagerForTutorial(bm2);
        bm2.InitBattleWithData(new List<EnemyData> { enemy }, run, false, null);

        panel = (ui != null ? ui : UIManager.Instance)?.ShowPanel<BattlePanel>();
        panel?.SetBattleManager(bm2);
        bm = bm2;

        StartCoroutine(DestinyBattleTurn());
    }

    private IEnumerator DestinyBattleTurn()
    {
        yield return new WaitUntil(() => bm != null && bm.CanPlayerAct);
        yield return new WaitForSeconds(0.3f);

        // 先发牌（黑桃同花），讲解期间牌会落到手上
        bm.TutorialSetHand(_destinyHand);
        yield return WaitHandSettled();

        // 1) 什么是命运花色（高亮右侧：花色命运文字 + 命运一览按钮）
        var rightRects = new List<RectTransform>();
        if (panel != null)
        {
            if (panel.suitHudText != null) rightRects.Add(panel.suitHudText.rectTransform);
            if (panel.FateButton != null) rightRects.Add(Rt(panel.FateButton));
        }
        yield return IntroRegionStep(rightRects, "命运花色",
            "右边是【花色命运】：每个花色都对应一个「命运」事件。你在一场战斗里打出最多的花色，叫「命运花色」——它会自动决定你下一站进入哪个事件。");

        // 2) 让玩家点开命运一览
        yield return ExplainBubble(panel != null ? panel.FateButton.transform : null, "命运一览",
            "点开【命运一览】，看看四个花色各自通向什么事件。");
        ClearOverlay();
        if (panel != null) ShowSpotlightRect(Rt(panel.FateButton));
        yield return new WaitUntil(() => ui != null && ui.IsPanelActive("FatePanel"));
        ClearOverlay();
        yield return new WaitUntil(() => ui == null || !ui.IsPanelActive("FatePanel"));

        // 3) 命格值 / 主命格（高亮左侧整块：命格文字 + 命格/主动技能按钮）
        var leftRects = new List<RectTransform>();
        if (panel != null)
        {
            if (panel.DestinyHudRect != null) leftRects.Add(panel.DestinyHudRect);
            if (panel.ActiveSkillButton != null) leftRects.Add(Rt(panel.ActiveSkillButton));
            if (panel.DestinyButton != null) leftRects.Add(Rt(panel.DestinyButton));
        }
        yield return IntroRegionStep(leftRects, "命格值 · 主命格",
            "左边是你的命格：每个花色有独立的「命格值」，达到 2 / 5 / 10 点依次解锁 Lv.1 / 2 / 3。第一个到达 Lv.1 的花色会成为「主命格」（你现在的主命格是 ♠黑桃 Lv.1）。");

        // 4) 主动技能 / 被动技能：让玩家点开命格面板查看
        yield return ExplainBubble(panel != null ? panel.DestinyButton.transform : null, "主动技能 · 被动技能",
            "点开【命格】，可以查看主命格的被动技能；打出一张主命格花色的牌会积攒命运之力，攒满就能释放主动技能。");
        ClearOverlay();
        if (panel != null) ShowSpotlightRect(Rt(panel.DestinyButton));
        yield return new WaitUntil(() => ui != null && ui.IsPanelActive("DestinyPanel"));
        ClearOverlay();
        yield return new WaitUntil(() => ui == null || !ui.IsPanelActive("DestinyPanel"));

        // 5) 打出黑桃同花 → 命运之力攒满 → 主动技击杀
        yield return GuidePlay(_destinyFlush, "同花（黑桃）",
            "现在打出一手黑桃同花——主命格花色的每张牌都会积攒命运之力，5 张正好从 7 攒满到 12。");

        yield return new WaitUntil(() => bm == null || bm.IsBattleOver
            || (bm.GetRunData() != null && bm.GetRunData().fatePower >= 12));
        if (bm == null || bm.IsBattleOver) yield break;

        yield return new WaitUntil(() => panel == null || panel.ActiveSkillButton == null || panel.ActiveSkillButton.interactable);
        yield return ExplainBubble(panel != null ? panel.ActiveSkillButton.transform : null, "主动技能",
            "命运之力攒满了！点击【主动技能】释放黑桃主动技【破军】，击败敌人！");
        ClearOverlay();
        if (panel != null) ShowSpotlightRect(Rt(panel.ActiveSkillButton));
        yield return new WaitUntil(() => bm == null || bm.IsBattleOver
            || (bm.GetRunData() != null && bm.GetRunData().fatePower < 12));
        yield return new WaitUntil(() => bm == null || bm.IsBattleOver);
    }

    private IEnumerator DestinySettleRoutine()
    {
        _destinyPhase = 3;
        yield return new WaitForSeconds(1.4f);
        ClearOverlay();

        var run = bm != null ? bm.GetRunData() : null;
        var suit = bm != null ? bm.GetDominantSuit() : S;
        if (run != null)
        {
            int before = run.destinyPoints[(int)suit];
            run.AddDestinyPoint(suit, 1);
            int after = run.destinyPoints[(int)suit];
            yield return ExplainBubble(panel != null ? panel.DestinyHudRect : null, "战斗胜利 · 命格",
                $"本场打出最多的花色是【{DestinyInfo.SuitName(suit)}】——它就是「命运花色」。战斗胜利让它的命格值 +1（{before} → {after}）。");
        }

        yield return ExplainBubble(panel != null ? panel.FateButton.transform : null, "命运花色决定下一站",
            "你的「命运花色」是【♠黑桃】，接下来会自动进入黑桃对应的那个事件（在【命运一览】里可以提前看到四花色各通向哪里）。");

        ShowFullDim();
        SetPrompt("教学结束", "你已经掌握了战斗、命格与命运花色的核心玩法，去冒险吧！");
        yield return new WaitForSeconds(2.4f);
        Close();
        RunDirector.Instance?.ReturnToMainMenu();
    }

    private void Close()
    {
        if (overlay != null) Destroy(overlay);
        overlay = null;
        Destroy(gameObject);
    }

    // ===== 覆盖层 / 聚光灯 =====
    private void SetPrompt(string title, string body)
    {
        if (bubbleObj != null) bubbleObj.SetActive(true);
        if (titleTxt != null) titleTxt.text = title;
        if (bodyTxt != null) bodyTxt.text = body;
    }

    // ===== 开场介绍（先手牌区→敌人区→牌型速览，点击前进）=====
    private IEnumerator IntroRoutine()
    {
        if (bubbleObj != null) bubbleObj.SetActive(false);

        // 手牌是逐张抽牌动画生成的：等“全部”手牌都生成，区域才准（否则只框到先落地的一张）
        int handCount = bm != null ? bm.GetHandArea().HandCards.Count : 0;
        float w = 0f;
        while (w < 4f && (handCount <= 0 || HandRects().Count < handCount)) { w += Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(0.2f);

        yield return IntroRegionStep(HandRects(), "手牌区", "屏幕底部是你的手牌。点击卡片来选择要打出的组合。");
        yield return IntroRegionStep(EnemyRects(), "敌人区", "这里是敌人：血条、护盾，以及它下回合的意图（会攻击或防御）。");

        yield return IntroRegionStep(null, "牌型效果", DescribeHandTypes(HandType.OnePair, HandType.TwoConsecutivePairs, HandType.ThreeOfAKind));
        yield return IntroRegionStep(null, "牌型效果", DescribeHandTypes(HandType.Straight3, HandType.Straight4, HandType.Straight5));
        yield return IntroRegionStep(null, "牌型效果", DescribeHandTypes(HandType.Flush3, HandType.Flush4, HandType.Flush5));
        yield return IntroRegionStep(null, "牌型效果", DescribeHandTypes(HandType.FullHouse, HandType.FourOfAKind));
        yield return IntroRegionStep(null, "牌型效果", DescribeHandTypes(HandType.StraightFlush3, HandType.StraightFlush4, HandType.StraightFlush5));
        yield return IntroRegionStep(null, "随时查看", "牌型效果可以在【暂停菜单 → 牌型效果】里随时查看。");

        yield return IntroRegionStep(null, "准备开始", "点击任意处，开始你的第一场战斗！");
        HideCenter();
    }

    /// <summary>多区域介绍：每个区域单独一个亮孔（不吞并中间区域）。</summary>
    private IEnumerator IntroStepMultiRects(List<RectTransform> rects, string title, string body)
    {
        centerBubble.SetActive(true);
        if (rects != null && rects.Count > 0 && BoundsOf(rects, out var sMin, out var sMax))
        {
            LayoutIntroBubbleAt(sMin, sMax, title, body);
        }
        else
        {
            LayoutIntroBubbleAtCenter(title, body);
        }

        var holes = new List<Rect>();
        var wc = new Vector3[4];
        if (rects != null)
        {
            foreach (var rt in rects)
            {
                if (rt == null) continue;
                rt.GetWorldCorners(wc);
                var hole = ScreenToLocalRect(new Vector2(wc[0].x, wc[0].y), new Vector2(wc[2].x, wc[2].y), 0f);
                if (hole.width > 0.5f && hole.height > 0.5f) holes.Add(hole);
            }
        }
        ApplySpotlightMulti(holes);

        yield return WaitIntroClick();
    }

    private IEnumerator IntroRegionStep(List<RectTransform> rects, string title, string body)
    {
        centerBubble.SetActive(true);
        if (rects != null && rects.Count > 0 && BoundsOf(rects, out var sMin, out var sMax))
        {
            LayoutIntroBubbleAt(sMin, sMax, title, body);
            _liveRects = rects;                     // 每帧跟踪（Update 里重算）
            ShowSpotlightScreenRect(sMin, sMax);
            yield return WaitIntroClick();
            _liveRects = null;
        }
        else
        {
            LayoutIntroBubbleAtCenter(title, body);
            ShowFullDim();
            yield return WaitIntroClick();
        }
        ClearOverlay();   // 说明文字消失后撤掉遮罩
    }

    private List<RectTransform> HandRects()
    {
        var list = new List<RectTransform>();
        if (panel != null && panel.handContainer != null)
        {
            for (int i = 0; i < panel.handContainer.childCount; i++)
            {
                var cu = panel.handContainer.GetChild(i).GetComponent<CardUI>();
                if (cu != null) list.Add((RectTransform)cu.transform);
            }
        }
        return list;
    }

    private List<RectTransform> EnemyRects()
    {
        var list = new List<RectTransform>();
        if (panel != null && panel.EnemySlots != null)
        {
            foreach (var s in panel.EnemySlots)
                if (s != null) list.Add((RectTransform)s.transform);
        }
        if (list.Count == 0 && panel != null)
        {
            if (panel.enemyIntentText != null) list.Add(panel.enemyIntentText.rectTransform);
            if (panel.enemyNameText != null) list.Add(panel.enemyNameText.rectTransform);
            if (panel.enemyHpText != null) list.Add(panel.enemyHpText.rectTransform);
        }
        return list;
    }

    private List<RectTransform> PreviewRects()
    {
        var list = new List<RectTransform>();
        if (panel != null)
        {
            if (panel.handTypeText != null) list.Add(panel.handTypeText.rectTransform);
            if (panel.effectPreviewText != null) list.Add(panel.effectPreviewText.rectTransform);
        }
        return list;
    }

    private static bool BoundsOf(List<RectTransform> rects, out Vector2 sMin, out Vector2 sMax)
    {
        bool any = false;
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        var wc = new Vector3[4];
        foreach (var rt in rects)
        {
            if (rt == null) continue;
            rt.GetWorldCorners(wc);
            for (int k = 0; k < 4; k++)
            {
                minX = Mathf.Min(minX, wc[k].x); minY = Mathf.Min(minY, wc[k].y);
                maxX = Mathf.Max(maxX, wc[k].x); maxY = Mathf.Max(maxY, wc[k].y);
            }
            any = true;
        }
        sMin = new Vector2(minX, minY);
        sMax = new Vector2(maxX, maxY);
        return any && maxX > minX && maxY > minY;
    }

    /// <summary>说明气泡：按文案自适应高度，并放到被介绍区域中心的对侧边缘</summary>
    private void LayoutIntroBubble(RectTransform target, string title, string body)
    {
        if (target == null) { LayoutIntroBubbleAtCenter(title, body); return; }
        var wc = new Vector3[4];
        target.GetWorldCorners(wc);
        LayoutIntroBubbleAt(new Vector2(wc[0].x, wc[0].y), new Vector2(wc[2].x, wc[2].y), title, body);
    }

    private void LayoutIntroBubbleAtCenter(string title, string body) => LayoutIntroBubbleAt(Vector2.zero, Vector2.zero, title, body, center: true);

    private void LayoutIntroBubbleAt(Vector2 sMin, Vector2 sMax, string title, string body, bool center = false)
    {
        centerTitle.text = title;
        centerBody.text = body;
        centerBody.ForceMeshUpdate();
        float bodyH = Mathf.Clamp(centerBody.preferredHeight, 44f, 360f);

        var crt = (RectTransform)centerBubble.transform;
        float h = 26f + 56f + bodyH + 22f + 36f;   // 上边距 + 标题 + 正文 + 间隔 + 提示
        crt.sizeDelta = new Vector2(900f, h);
        centerBody.rectTransform.sizeDelta = new Vector2(860f, bodyH);

        var r = overlayRt.rect;
        float y = 0f;
        if (!center)
        {
            float cy = (sMin.y + sMax.y) * 0.5f;
            bool lowerHalf = cy < Screen.height * 0.5f;
            y = lowerHalf ? (r.yMax - h * 0.5f - 24f) : (r.yMin + h * 0.5f + 24f);
        }

        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = new Vector2(0f, y);
    }

    private IEnumerator WaitIntroClick()
    {
        _introClick = false;
        introCatcher.gameObject.SetActive(true);
        yield return new WaitUntil(() => _introClick);
        introCatcher.gameObject.SetActive(false);
    }

    private void HideCenter()
    {
        if (centerBubble != null) centerBubble.SetActive(false);
        if (introCatcher != null) introCatcher.gameObject.SetActive(false);
    }

    private static RectTransform Rt(Component c) => c != null ? (RectTransform)c.transform : null;

    private void ShowSpotlightForCards(List<CardData> cards)
    {
        if (panel == null || panel.handContainer == null) { ShowFullDim(); return; }

        var holes = new List<Rect>();
        for (int i = 0; i < panel.handContainer.childCount; i++)
        {
            var cu = panel.handContainer.GetChild(i).GetComponent<CardUI>();
            if (cu == null || !cards.Contains(cu.GetCardData())) continue;
            var hole = HoleOf((RectTransform)cu.transform, 0f);
            if (hole.width > 0.5f && hole.height > 0.5f) holes.Add(hole);
        }
        ApplySpotlightMulti(holes);
    }

    private void ShowSpotlightRect(RectTransform target)
    {
        if (target == null) { ShowFullDim(); return; }
        ApplySpotlightMulti(new List<Rect> { HoleOf(target, 0f) });
    }

    /// <summary>取元素高亮孔（overlay 本地坐标）。用画布感知的 ScreenPointToLocalPointInRectangle（与卡片一致，已验证准）。</summary>
    private Rect HoleOf(RectTransform rt, float pad)
    {
        if (rt == null) return new Rect();
        var wc = new Vector3[4];
        rt.GetWorldCorners(wc);
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int k = 0; k < 4; k++)
        {
            minX = Mathf.Min(minX, wc[k].x); minY = Mathf.Min(minY, wc[k].y);
            maxX = Mathf.Max(maxX, wc[k].x); maxY = Mathf.Max(maxY, wc[k].y);
        }
        return ScreenToLocalRect(new Vector2(minX, minY), new Vector2(maxX, maxY), pad);
    }

    private void ShowSpotlightScreenRect(Vector2 screenMin, Vector2 screenMax)
    {
        if (overlayRt == null) return;
        // 精确边界（不加内边距），避免亮区扩到相邻按钮
        ApplySpotlightMulti(new List<Rect> { ScreenToLocalRect(screenMin, screenMax, 0f) });
    }

    private void ShowFullDim() => ApplySpotlightMulti(null);

    private Rect ScreenToLocalRect(Vector2 screenMin, Vector2 screenMax, float pad)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRt, screenMin, null, out var lmin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRt, screenMax, null, out var lmax);
        return Rect.MinMaxRect(lmin.x - pad, lmin.y - pad, lmax.x + pad, lmax.y + pad);
    }

    /// <summary>多孔遮罩：holes=要亮出的区域（dimRoot 本地坐标），其余用压暗块铺满。</summary>
    private void ApplySpotlightMulti(List<Rect> holes)
    {
        if (overlayRt == null) return;
        var r = overlayRt.rect;

        var hs = new List<Rect>();
        if (holes != null)
        {
            foreach (var h in holes)
            {
                float xMin = Mathf.Clamp(h.xMin, r.xMin, r.xMax);
                float xMax = Mathf.Clamp(h.xMax, r.xMin, r.xMax);
                float yMin = Mathf.Clamp(h.yMin, r.yMin, r.yMax);
                float yMax = Mathf.Clamp(h.yMax, r.yMin, r.yMax);
                if (xMax - xMin > 0.5f && yMax - yMin > 0.5f)
                    hs.Add(Rect.MinMaxRect(xMin, yMin, xMax, yMax));
            }
        }

        var dims = new List<Rect>();
        if (hs.Count == 0)
        {
            dims.Add(Rect.MinMaxRect(r.xMin, r.yMin, r.xMax, r.yMax));
        }
        else
        {
            var xs = new List<float> { r.xMin, r.xMax };
            foreach (var h in hs) { xs.Add(h.xMin); xs.Add(h.xMax); }
            xs.Sort();

            for (int i = 0; i < xs.Count - 1; i++)
            {
                float xa = xs[i], xb = xs[i + 1];
                if (xb - xa < 0.5f) continue;

                var opens = new List<Vector2>();
                foreach (var h in hs)
                    if (h.xMin <= xa + 0.5f && h.xMax >= xb - 0.5f)
                        opens.Add(new Vector2(h.yMin, h.yMax));

                opens.Sort((a, b) => a.x.CompareTo(b.x));
                var merged = new List<Vector2>();
                foreach (var o in opens)
                {
                    if (merged.Count > 0 && o.x <= merged[merged.Count - 1].y + 0.5f)
                        merged[merged.Count - 1] = new Vector2(merged[merged.Count - 1].x, Mathf.Max(merged[merged.Count - 1].y, o.y));
                    else
                        merged.Add(o);
                }

                float y = r.yMin;
                foreach (var o in merged)
                {
                    if (o.x > y + 0.5f) dims.Add(Rect.MinMaxRect(xa, y, xb, o.x));
                    y = Mathf.Max(y, o.y);
                }
                if (y < r.yMax - 0.5f) dims.Add(Rect.MinMaxRect(xa, y, xb, r.yMax));
            }
        }

        EnsureDimPool(dims.Count);
        var pr = overlayRt.rect;
        for (int i = 0; i < dimPool.Count; i++)
        {
            var img = dimPool[i];
            if (img == null) continue;
            if (i < dims.Count)
            {
                var d = dims[i];
                var rt = (RectTransform)img.transform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(d.x - pr.xMin, d.y - pr.yMin);
                rt.sizeDelta = new Vector2(d.width, d.height);
                img.gameObject.SetActive(true);
            }
            else
            {
                img.gameObject.SetActive(false);
            }
        }
    }

    private void BuildOverlay()
    {
        font = Res.Load<TMP_FontAsset>("Fonts/simhei SDF");
        EnsureEventSystem();

        overlay = new GameObject("[TutorialOverlay]", typeof(RectTransform));
        DontDestroyOnLoad(overlay);
        overlayRt = (RectTransform)overlay.transform;

        var canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        overlay.AddComponent<GraphicRaycaster>();

        // 压暗层（多孔）：只亮出目标元素；dim 图片会被置于最底层，位于气泡之下
        // 一开始不压暗（等有气泡/高亮时再显示）

        // 顶部提示气泡（在遮罩之上）
        var bubble = NewImage(overlayRt, new Color(0.10f, 0.11f, 0.16f, 0.97f));
        var brt = bubble.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0f, -16f);
        brt.sizeDelta = new Vector2(1400f, 170f);

        titleTxt = NewText(bubble.transform, 38, TextAlignmentOptions.Center, "新手教学");
        SetRect(titleTxt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(1360f, 48f));

        bodyTxt = NewText(bubble.transform, 28, TextAlignmentOptions.TopLeft, "");
        SetRect(bodyTxt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(1360f, 96f));
        bubbleObj = bubble.gameObject;
        bubbleObj.SetActive(false);

        // 居中说明气泡（开场逐个介绍战斗界面用）
        var center = NewImage(overlayRt, new Color(0.10f, 0.11f, 0.16f, 0.98f));
        var crt = center.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(900f, 300f);
        centerTitle = NewText(center.transform, 42, TextAlignmentOptions.Center, "");
        SetRect(centerTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(860f, 60f));
        centerBody = NewText(center.transform, 30, TextAlignmentOptions.Top, "");
        SetRect(centerBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(860f, 150f));
        var hint = NewText(center.transform, 22, TextAlignmentOptions.Center, "（点击任意处继续）");
        SetRect(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(860f, 30f));
        hint.color = new Color(0.62f, 0.62f, 0.7f);
        centerBubble = center.gameObject;
        centerBubble.SetActive(false);

        // 开场介绍用：全屏透明点击捕捉（点任意处前进）
        var catcherImg = NewImage(overlayRt, new Color(0f, 0f, 0f, 0f));
        Stretch(catcherImg.rectTransform);
        catcherImg.raycastTarget = true;
        var catcherBtn = catcherImg.gameObject.AddComponent<Button>();
        catcherBtn.transition = Selectable.Transition.None;
        catcherBtn.onClick.AddListener(() => _introClick = true);
        introCatcher = catcherBtn;
        introCatcher.gameObject.SetActive(false);

        // 跳过教学（右下角）
        var skip = NewButton(overlayRt, Vector2.zero, new Vector2(180f, 46f), new Color(0.30f, 0.30f, 0.36f, 1f), out var skipLabel);
        var srt = (RectTransform)skip.transform;
        srt.anchorMin = srt.anchorMax = new Vector2(1f, 0f);
        srt.pivot = new Vector2(1f, 0f);
        srt.anchoredPosition = new Vector2(-30f, 30f);
        skipLabel.text = "跳过教学";
        skip.onClick.AddListener(() => { Close(); RunDirector.Instance?.ReturnToMainMenu(); });
    }

    private void EnsureDimPool(int n)
    {
        while (dimPool.Count < n)
        {
            var img = NewImage(overlayRt, new Color(0f, 0f, 0f, 0.68f));
            img.name = "Dim";
            img.raycastTarget = true;
            img.transform.SetAsFirstSibling();   // 置于最底（在气泡之下）
            dimPool.Add(img);
        }
    }

    private static string DescribeHandTypes(params HandType[] types)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var t in types)
        {
            var fx = HandEffectTable.GetEffects(HandTypeResult.Create(t));
            sb.Append("<b>").Append(HandTypeNames.Full(t)).Append("</b>：");
            if (fx == null || fx.Count == 0) sb.Append("无");
            else
            {
                for (int i = 0; i < fx.Count; i++)
                {
                    if (i > 0) sb.Append("，");
                    sb.Append(HandEffectTable.Describe(fx[i].effectType, fx[i].value));
                }
            }
            sb.Append("\n");
        }
        return sb.ToString();
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        new GameObject("[EventSystem]", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
    }

    private Image NewImage(Transform parent, Color color)
    {
        var go = new GameObject("Img", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private TextMeshProUGUI NewText(Transform parent, int size, TextAlignmentOptions align, string text)
    {
        var go = new GameObject("Txt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.text = text;
        t.color = Color.white;
        t.enableWordWrapping = true;
        return t;
    }

    private Button NewButton(Transform parent, Vector2 pos, Vector2 size, Color color, out TextMeshProUGUI label)
    {
        var go = new GameObject("Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        label = NewText(go.transform, 30, TextAlignmentOptions.Center, "OK");
        label.enableWordWrapping = false;
        Stretch(label.rectTransform);
        return go.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
