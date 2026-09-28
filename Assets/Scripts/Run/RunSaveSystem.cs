using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Roguelike.Data;

namespace Roguelike
{
    /// <summary>存档所处阶段</summary>
    public enum SavePhase { Battle, Event, EventResult, Shop }

    /// <summary>存档数据（序列化为 JSON）</summary>
    public class RunSave
    {
        public int version;
        public SavePhase phase;
        public RunData run;

        /// <summary>UnityEngine.Random 状态（s0..s3）——读档原样恢复，保证整局后续随机一致</summary>
        public List<int> rng = new List<int>();

        // Battle 阶段：本场敌人 + 强化信息（原样还原本场战斗）
        public List<int> battleEnemyIds = new List<int>();
        public int battleBuff;
        public int battleDebuff;
        public bool battleIsElite;
        public bool battleIsBoss;

        // Event / EventResult 阶段：当前事件 id（多阶段事件用 RunData.currentEventStage）
        public int eventId;

        /// <summary>事件预抽物品（拍卖会等），读档原样还原，不再重新随机</summary>
        public List<PreparedItemSave> preparedItems = new List<PreparedItemSave>();

        // EventResult 阶段：结果内容 + 之后的走向
        public bool resultReopenEvent;          // true = 展示后回到事件（多阶段）
        public List<string> resultMessages = new List<string>();
        public List<int> resultPendingRelicIds = new List<int>();
        public List<int> resultPendingPotionIds = new List<int>();
        public List<GrantedEnchantment> resultEnchantments = new List<GrantedEnchantment>();

        // Shop 阶段：商店库存（含已售标记），读档原样还原，物品不变
        public ShopInventory shopInventory;
    }

    /// <summary>
    /// 存档系统：把 RunData + 当前阶段 + RNG 状态写入 persistentDataPath/run_save.json。
    /// 检查点：每场战斗开头 / 事件选项 / 事件结果 / 商店。
    /// 关键：读档恢复 RNG 状态（而不是 InitState(seed)），并把预抽物品/商店库存原样还原，
    ///       所以读档不会改变后续事件、敌人、拍品、商店内容。
    /// </summary>
    public static class RunSaveSystem
    {
        private const int Version = 2;
        private const string FileName = "run_save.json";
        private const string FolderName = "Saves";

        /// <summary>
        /// 存档目录：编辑器 = 项目根目录，打包 = 可执行文件所在目录（都是其下的 Saves 子文件夹）。
        /// Application.dataPath：编辑器=&lt;项目&gt;/Assets，打包=&lt;exe目录&gt;/&lt;游戏名&gt;_Data，取上一级即可。
        /// </summary>
        public static string SaveDir
        {
            get
            {
                string root = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(string.IsNullOrEmpty(root) ? Application.dataPath : root, FolderName);
            }
        }

        public static string SavePath => Path.Combine(SaveDir, FileName);

        public static bool HasSave()
        {
            try { return File.Exists(SavePath); }
            catch { return false; }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[存档] 删除失败: {e.Message}");
            }
        }

        // ============================================================
        //  UnityEngine.Random 状态（反射读写私有 s0..s3）
        // ============================================================

        private static readonly FieldInfo[] RngFields = InitRngFields();

        private static FieldInfo[] InitRngFields()
        {
            var t = typeof(UnityEngine.Random.State);
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return new[]
            {
                t.GetField("s0", F), t.GetField("s1", F),
                t.GetField("s2", F), t.GetField("s3", F)
            };
        }

        /// <summary>抓取当前全局随机状态（4 个 int）</summary>
        public static List<int> CaptureRng()
        {
            var state = UnityEngine.Random.state;
            var list = new List<int>(4);
            for (int i = 0; i < 4; i++)
                list.Add(RngFields[i] != null ? (int)RngFields[i].GetValue(state) : 0);
            return list;
        }

        /// <summary>恢复全局随机状态</summary>
        public static void RestoreRng(List<int> data)
        {
            if (data == null || data.Count < 4)
            {
                Debug.LogWarning("[存档] 缺少 RNG 状态，退回按种子重来");
                return;
            }
            object boxed = new UnityEngine.Random.State();
            for (int i = 0; i < 4; i++)
                if (RngFields[i] != null) RngFields[i].SetValue(boxed, data[i]);
            UnityEngine.Random.state = (UnityEngine.Random.State)boxed;
        }

        // ============================================================
        //  写入
        // ============================================================

        public static void SaveBattle(RunData run, List<int> baseEnemyIds, int buff, int debuff, bool isElite, bool isBoss)
        {
            if (run == null) return;
            var s = new RunSave
            {
                version = Version, phase = SavePhase.Battle, run = run, rng = CaptureRng(),
                battleEnemyIds = baseEnemyIds != null ? new List<int>(baseEnemyIds) : new List<int>(),
                battleBuff = buff, battleDebuff = debuff, battleIsElite = isElite, battleIsBoss = isBoss
            };
            Write(s);
        }

        public static void SaveEvent(RunData run, int eventId, List<PreparedItemSave> prepared)
        {
            if (run == null) return;
            var s = new RunSave
            {
                version = Version, phase = SavePhase.Event, run = run, rng = CaptureRng(),
                eventId = eventId,
                preparedItems = prepared != null ? new List<PreparedItemSave>(prepared) : new List<PreparedItemSave>()
            };
            Write(s);
        }

        public static void SaveEventResult(RunData run, int eventId, EventOutcome outcome, bool reopenEvent)
        {
            if (run == null) return;
            var s = new RunSave
            {
                version = Version, phase = SavePhase.EventResult, run = run, rng = CaptureRng(),
                eventId = eventId, resultReopenEvent = reopenEvent
            };
            if (outcome != null)
            {
                s.resultMessages = new List<string>(outcome.messages);
                s.resultPendingRelicIds = new List<int>(outcome.pendingRelicIds);
                s.resultPendingPotionIds = new List<int>(outcome.pendingPotionIds);
                s.resultEnchantments = new List<GrantedEnchantment>(outcome.enchantments);
            }
            Write(s);
        }

        public static void SaveShop(RunData run, ShopInventory inventory)
        {
            if (run == null || inventory == null) return;
            var s = new RunSave
            {
                version = Version, phase = SavePhase.Shop, run = run, rng = CaptureRng(),
                shopInventory = CloneShop(inventory)
            };
            Write(s);
        }

        private static void Write(RunSave s)
        {
            try
            {
                Directory.CreateDirectory(SaveDir);
                string json = MiniJson.Serialize(ToDict(s), true);
                File.WriteAllText(SavePath, json, new System.Text.UTF8Encoding(false));
                Debug.Log($"[存档] 已保存 phase={s.phase} → {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[存档] 保存失败: {e.Message}");
            }
        }

        // ============================================================
        //  读取
        // ============================================================

        public static RunSave Load()
        {
            try
            {
                if (!File.Exists(SavePath)) return null;
                string json = File.ReadAllText(SavePath, System.Text.Encoding.UTF8);
                var root = MiniJson.Deserialize(json) as Dictionary<string, object>;
                if (root == null) return null;

                int ver = GetInt(root, "version", 0);
                if (ver != Version)
                {
                    Debug.LogWarning($"[存档] 版本不符（{ver} != {Version}），忽略旧存档");
                    return null;
                }

                var s = new RunSave
                {
                    version = ver,
                    phase = ParsePhase(GetStr(root, "phase", "Battle")),
                    run = DictToRun(root.TryGetValue("run", out var rv) ? rv as Dictionary<string, object> : null),
                    rng = GetIntList(root, "rng"),
                    eventId = GetInt(root, "eventId", 0),
                    battleBuff = GetInt(root, "battleBuff", 0),
                    battleDebuff = GetInt(root, "battleDebuff", 0),
                    battleIsElite = GetBool(root, "battleIsElite", false),
                    battleIsBoss = GetBool(root, "battleIsBoss", false),
                    resultReopenEvent = GetBool(root, "resultReopenEvent", false)
                };

                s.battleEnemyIds = GetIntList(root, "battleEnemyIds");
                s.preparedItems = GetPreparedList(root, "preparedItems");
                s.resultMessages = GetStrList(root, "resultMessages");
                s.resultPendingRelicIds = GetIntList(root, "resultPendingRelicIds");
                s.resultPendingPotionIds = GetIntList(root, "resultPendingPotionIds");
                s.resultEnchantments = GetGrantedList(root, "resultEnchantments");
                s.shopInventory = ObjToShop(root.TryGetValue("shopInventory", out var si) ? si as Dictionary<string, object> : null);

                if (s.run == null)
                {
                    Debug.LogWarning("[存档] RunData 解析失败");
                    return null;
                }
                return s;
            }
            catch (Exception e)
            {
                Debug.LogError($"[存档] 读取失败: {e.Message}");
                return null;
            }
        }

        private static SavePhase ParsePhase(string s)
        {
            switch (s)
            {
                case "Event": return SavePhase.Event;
                case "EventResult": return SavePhase.EventResult;
                case "Shop": return SavePhase.Shop;
                default: return SavePhase.Battle;
            }
        }

        // ============================================================
        //  顶层 ⇆ Dictionary
        // ============================================================

        private static Dictionary<string, object> ToDict(RunSave s)
        {
            return new Dictionary<string, object>
            {
                ["version"] = s.version,
                ["phase"] = s.phase.ToString(),
                ["eventId"] = s.eventId,
                ["rng"] = s.rng ?? new List<int>(),
                ["battleBuff"] = s.battleBuff,
                ["battleDebuff"] = s.battleDebuff,
                ["battleIsElite"] = s.battleIsElite,
                ["battleIsBoss"] = s.battleIsBoss,
                ["resultReopenEvent"] = s.resultReopenEvent,
                ["battleEnemyIds"] = s.battleEnemyIds ?? new List<int>(),
                ["preparedItems"] = PreparedToObj(s.preparedItems),
                ["resultMessages"] = s.resultMessages ?? new List<string>(),
                ["resultPendingRelicIds"] = s.resultPendingRelicIds ?? new List<int>(),
                ["resultPendingPotionIds"] = s.resultPendingPotionIds ?? new List<int>(),
                ["resultEnchantments"] = GrantedToObj(s.resultEnchantments),
                ["shopInventory"] = ShopToObj(s.shopInventory),
                ["run"] = RunToDict(s.run)
            };
        }

        // ============================================================
        //  商店库存
        // ============================================================

        private static ShopInventory CloneShop(ShopInventory inv)
        {
            if (inv == null) return null;
            return new ShopInventory
            {
                relics = CloneEntries(inv.relics),
                potions = CloneEntries(inv.potions),
                enchantments = CloneEnchants(inv.enchantments)
            };
        }

        private static List<ShopEntry> CloneEntries(List<ShopEntry> src)
        {
            var list = new List<ShopEntry>();
            if (src == null) return list;
            foreach (var e in src)
                list.Add(new ShopEntry { kind = e.kind, id = e.id, price = e.price, sold = e.sold, label = e.label });
            return list;
        }

        private static List<ShopEnchantEntry> CloneEnchants(List<ShopEnchantEntry> src)
        {
            var list = new List<ShopEnchantEntry>();
            if (src == null) return list;
            foreach (var e in src)
                list.Add(new ShopEnchantEntry { enchantmentId = e.enchantmentId, priceRandom = e.priceRandom, priceCustom = e.priceCustom, sold = e.sold });
            return list;
        }

        private static object ShopToObj(ShopInventory inv)
        {
            if (inv == null) return null;
            return new Dictionary<string, object>
            {
                ["relics"] = EntryListToObj(inv.relics),
                ["potions"] = EntryListToObj(inv.potions),
                ["enchantments"] = EnchantListToObj(inv.enchantments)
            };
        }

        private static List<object> EntryListToObj(List<ShopEntry> src)
        {
            var list = new List<object>();
            if (src == null) return list;
            foreach (var e in src)
                list.Add(new Dictionary<string, object>
                {
                    ["kind"] = e.kind ?? "", ["id"] = e.id, ["price"] = e.price,
                    ["sold"] = e.sold, ["label"] = e.label ?? ""
                });
            return list;
        }

        private static List<object> EnchantListToObj(List<ShopEnchantEntry> src)
        {
            var list = new List<object>();
            if (src == null) return list;
            foreach (var e in src)
                list.Add(new Dictionary<string, object>
                {
                    ["enchantmentId"] = e.enchantmentId, ["priceRandom"] = e.priceRandom,
                    ["priceCustom"] = e.priceCustom, ["sold"] = e.sold
                });
            return list;
        }

        private static ShopInventory ObjToShop(Dictionary<string, object> d)
        {
            if (d == null) return null;
            return new ShopInventory
            {
                relics = ObjToEntries(d.TryGetValue("relics", out var r) ? r as List<object> : null),
                potions = ObjToEntries(d.TryGetValue("potions", out var p) ? p as List<object> : null),
                enchantments = ObjToEnchants(d.TryGetValue("enchantments", out var e) ? e as List<object> : null)
            };
        }

        private static List<ShopEntry> ObjToEntries(List<object> raw)
        {
            var list = new List<ShopEntry>();
            if (raw == null) return list;
            foreach (var item in raw)
            {
                if (item is Dictionary<string, object> dd)
                    list.Add(new ShopEntry
                    {
                        kind = GetStr(dd, "kind", ""), id = GetInt(dd, "id", 0),
                        price = GetInt(dd, "price", 0), sold = GetBool(dd, "sold", false),
                        label = GetStr(dd, "label", "")
                    });
            }
            return list;
        }

        private static List<ShopEnchantEntry> ObjToEnchants(List<object> raw)
        {
            var list = new List<ShopEnchantEntry>();
            if (raw == null) return list;
            foreach (var item in raw)
            {
                if (item is Dictionary<string, object> dd)
                    list.Add(new ShopEnchantEntry
                    {
                        enchantmentId = GetInt(dd, "enchantmentId", 0),
                        priceRandom = GetInt(dd, "priceRandom", 0),
                        priceCustom = GetInt(dd, "priceCustom", 0),
                        sold = GetBool(dd, "sold", false)
                    });
            }
            return list;
        }

        // ============================================================
        //  预抽物品 / 附魔发放
        // ============================================================

        private static List<object> PreparedToObj(List<PreparedItemSave> src)
        {
            var list = new List<object>();
            if (src == null) return list;
            foreach (var p in src)
                list.Add(new Dictionary<string, object>
                {
                    ["optionIndex"] = p.optionIndex, ["resultIndex"] = p.resultIndex,
                    ["isPotion"] = p.isPotion, ["id"] = p.id
                });
            return list;
        }

        private static List<PreparedItemSave> GetPreparedList(Dictionary<string, object> d, string key)
        {
            var list = new List<PreparedItemSave>();
            if (d == null || !(d.TryGetValue(key, out var v) && v is List<object> raw)) return list;
            foreach (var item in raw)
            {
                if (item is Dictionary<string, object> dd)
                    list.Add(new PreparedItemSave
                    {
                        optionIndex = GetInt(dd, "optionIndex", 0), resultIndex = GetInt(dd, "resultIndex", 0),
                        isPotion = GetBool(dd, "isPotion", false), id = GetInt(dd, "id", 0)
                    });
            }
            return list;
        }

        private static List<object> GrantedToObj(List<GrantedEnchantment> src)
        {
            var list = new List<object>();
            if (src == null) return list;
            foreach (var g in src)
                list.Add(new Dictionary<string, object>
                {
                    ["cardKey"] = g.cardKey ?? "", ["cardDisplayName"] = g.cardDisplayName ?? "",
                    ["enchantmentId"] = g.enchantmentId
                });
            return list;
        }

        private static List<GrantedEnchantment> GetGrantedList(Dictionary<string, object> d, string key)
        {
            var list = new List<GrantedEnchantment>();
            if (d == null || !(d.TryGetValue(key, out var v) && v is List<object> raw)) return list;
            foreach (var item in raw)
            {
                if (item is Dictionary<string, object> dd)
                    list.Add(new GrantedEnchantment
                    {
                        cardKey = GetStr(dd, "cardKey", ""), cardDisplayName = GetStr(dd, "cardDisplayName", ""),
                        enchantmentId = GetInt(dd, "enchantmentId", 0)
                    });
            }
            return list;
        }

        // ============================================================
        //  RunData ⇆ Dictionary
        // ============================================================

        private static Dictionary<string, object> RunToDict(RunData r)
        {
            return new Dictionary<string, object>
            {
                ["seed"] = r.seed,
                ["actId"] = r.actId,
                ["battleIndex"] = r.battleIndex,
                ["isBossBattle"] = r.isBossBattle,

                ["maxHp"] = r.MaxHp,
                ["currentHp"] = r.CurrentHp,
                ["gold"] = r.Gold,

                ["currentEventStage"] = r.currentEventStage,
                ["eliteKillCount"] = r.eliteKillCount,

                ["nextBattleStartHpMod"] = r.nextBattleStartHpMod,
                ["nextBattleEnemyBuff"] = r.nextBattleEnemyBuff,
                ["nextBattleEnemyDebuff"] = r.nextBattleEnemyDebuff,

                ["permanentStartStrength"] = r.permanentStartStrength,
                ["permanentTurnEndSelfDamage"] = r.permanentTurnEndSelfDamage,
                ["nextBattleMulliganDelta"] = r.nextBattleMulliganDelta,
                ["nextBattleNoMulligan"] = r.nextBattleNoMulligan,
                ["nextBattleDrawBonus"] = r.nextBattleDrawBonus,
                ["nextBattleGuaranteeSuit"] = r.nextBattleGuaranteeSuit,
                ["nextBattleGuaranteeCount"] = r.nextBattleGuaranteeCount,
                ["nextBattleRevealAll"] = r.nextBattleRevealAll,
                ["nextBattleBonusRelicId"] = r.nextBattleBonusRelicId,
                ["pendingEventEnemyId"] = r.pendingEventEnemyId,

                ["permanentDamageMultiplier"] = r.permanentDamageMultiplier,
                ["permanentStraightDrawBonus"] = r.permanentStraightDrawBonus,
                ["permanentFlushDrawBonus"] = r.permanentFlushDrawBonus,
                ["permanentFlushDamageBonus"] = r.permanentFlushDamageBonus,
                ["nextBattleStraightDraw"] = r.nextBattleStraightDraw,
                ["nextBattleFateFull"] = r.nextBattleFateFull,
                ["nextBattleFateBonus"] = r.nextBattleFateBonus,
                ["fateGainDisabledBattles"] = r.fateGainDisabledBattles,

                ["challengeHpLimit"] = r.challengeHpLimit,

                ["spadeEventId"] = r.spadeEventId,
                ["heartEventId"] = r.heartEventId,
                ["clubEventId"] = r.clubEventId,
                ["diamondEventId"] = r.diamondEventId,
                ["shopSuitIndex"] = r.shopSuitIndex,

                ["mainDestinySuit"] = r.mainDestinySuit,
                ["fatePower"] = r.fatePower,
                ["heartDeathSaveUsed"] = r.heartDeathSaveUsed,
                ["phoenixUsed"] = r.phoenixUsed,
                ["battlesSinceShop"] = r.battlesSinceShop,

                ["battlesWon"] = r.battlesWon,
                ["totalDamageDealt"] = r.totalDamageDealt,
                ["totalGoldGained"] = r.totalGoldGained,

                ["destinyPoints"] = r.destinyPoints ?? new List<int>(),
                ["hiddenSuitIndices"] = r.hiddenSuitIndices ?? new List<int>(),
                ["nextBattleSealedSuits"] = r.nextBattleSealedSuits ?? new List<int>(),
                ["visitedEventIds"] = r.visitedEventIds ?? new List<int>(),
                ["unlockedContent"] = r.unlockedContent ?? new List<string>(),
                ["relicIds"] = new List<int>(r.RelicIds),
                ["potionIds"] = new List<int>(r.PotionIds),

                ["cardEnchantmentIds"] = DictListIntToObj(r.cardEnchantmentIds),
                ["relicCounters"] = DictIntToObj(r.relicCounters),
                ["nextBattleStartStatuses"] = StatusesToObj(r.nextBattleStartStatuses)
            };
        }

        private static RunData DictToRun(Dictionary<string, object> d)
        {
            if (d == null) return null;

            var r = new RunData
            {
                seed = GetInt(d, "seed", 0),
                actId = GetInt(d, "actId", 1),
                battleIndex = GetInt(d, "battleIndex", 0),
                isBossBattle = GetBool(d, "isBossBattle", false),

                currentEventStage = GetInt(d, "currentEventStage", 1),
                eliteKillCount = GetInt(d, "eliteKillCount", 0),

                nextBattleStartHpMod = GetInt(d, "nextBattleStartHpMod", 0),
                nextBattleEnemyBuff = GetInt(d, "nextBattleEnemyBuff", 0),
                nextBattleEnemyDebuff = GetInt(d, "nextBattleEnemyDebuff", 0),

                permanentStartStrength = GetInt(d, "permanentStartStrength", 0),
                permanentTurnEndSelfDamage = GetInt(d, "permanentTurnEndSelfDamage", 0),
                nextBattleMulliganDelta = GetInt(d, "nextBattleMulliganDelta", 0),
                nextBattleNoMulligan = GetBool(d, "nextBattleNoMulligan", false),
                nextBattleDrawBonus = GetInt(d, "nextBattleDrawBonus", 0),
                nextBattleGuaranteeSuit = GetInt(d, "nextBattleGuaranteeSuit", -1),
                nextBattleGuaranteeCount = GetInt(d, "nextBattleGuaranteeCount", 0),
                nextBattleRevealAll = GetBool(d, "nextBattleRevealAll", false),
                nextBattleBonusRelicId = GetInt(d, "nextBattleBonusRelicId", 0),
                pendingEventEnemyId = GetInt(d, "pendingEventEnemyId", 0),

                permanentDamageMultiplier = GetFloat(d, "permanentDamageMultiplier", 1f),
                permanentStraightDrawBonus = GetInt(d, "permanentStraightDrawBonus", 0),
                permanentFlushDrawBonus = GetInt(d, "permanentFlushDrawBonus", 0),
                permanentFlushDamageBonus = GetInt(d, "permanentFlushDamageBonus", 0),
                nextBattleStraightDraw = GetInt(d, "nextBattleStraightDraw", 0),
                nextBattleFateFull = GetBool(d, "nextBattleFateFull", false),
                nextBattleFateBonus = GetInt(d, "nextBattleFateBonus", 0),
                fateGainDisabledBattles = GetInt(d, "fateGainDisabledBattles", 0),

                challengeHpLimit = GetInt(d, "challengeHpLimit", -1),

                spadeEventId = GetInt(d, "spadeEventId", 0),
                heartEventId = GetInt(d, "heartEventId", 0),
                clubEventId = GetInt(d, "clubEventId", 0),
                diamondEventId = GetInt(d, "diamondEventId", 0),
                shopSuitIndex = GetInt(d, "shopSuitIndex", -1),

                mainDestinySuit = GetInt(d, "mainDestinySuit", -1),
                fatePower = GetInt(d, "fatePower", 0),
                heartDeathSaveUsed = GetBool(d, "heartDeathSaveUsed", false),
                phoenixUsed = GetBool(d, "phoenixUsed", false),
                battlesSinceShop = GetInt(d, "battlesSinceShop", 0),

                battlesWon = GetInt(d, "battlesWon", 0),
                totalDamageDealt = GetInt(d, "totalDamageDealt", 0),
                totalGoldGained = GetInt(d, "totalGoldGained", 0)
            };

            r.MaxHp = GetInt(d, "maxHp", 30);
            r.CurrentHp = GetInt(d, "currentHp", r.MaxHp);
            r.Gold = GetInt(d, "gold", 0);

            r.destinyPoints = GetIntList(d, "destinyPoints");
            while (r.destinyPoints.Count < 4) r.destinyPoints.Add(0);
            r.hiddenSuitIndices = GetIntList(d, "hiddenSuitIndices");
            r.nextBattleSealedSuits = GetIntList(d, "nextBattleSealedSuits");
            r.visitedEventIds = GetIntList(d, "visitedEventIds");
            r.unlockedContent = GetStrList(d, "unlockedContent");

            r.relicIds = GetIntList(d, "relicIds");
            r.potionIds = GetIntList(d, "potionIds");

            r.cardEnchantmentIds = ObjToDictListInt(d.TryGetValue("cardEnchantmentIds", out var ce) ? ce as Dictionary<string, object> : null);
            r.relicCounters = ObjToDictInt(d.TryGetValue("relicCounters", out var rc) ? rc as Dictionary<string, object> : null);
            r.nextBattleStartStatuses = ObjToStatuses(d.TryGetValue("nextBattleStartStatuses", out var st) ? st as List<object> : null);

            return r;
        }

        // ============================================================
        //  集合 ⇆ object
        // ============================================================

        private static List<object> IntListToObj(List<int> list)
        {
            var outList = new List<object>();
            if (list != null) foreach (var v in list) outList.Add(v);
            return outList;
        }

        private static Dictionary<string, object> DictListIntToObj(Dictionary<string, List<int>> dict)
        {
            var d = new Dictionary<string, object>();
            if (dict == null) return d;
            foreach (var kvp in dict) d[kvp.Key] = IntListToObj(kvp.Value);
            return d;
        }

        private static Dictionary<string, object> DictIntToObj(Dictionary<string, int> dict)
        {
            var d = new Dictionary<string, object>();
            if (dict == null) return d;
            foreach (var kvp in dict) d[kvp.Key] = kvp.Value;
            return d;
        }

        private static List<object> StatusesToObj(List<RunData.BattleStartStatus> list)
        {
            var outList = new List<object>();
            if (list == null) return outList;
            foreach (var s in list)
                outList.Add(new Dictionary<string, object> { ["status"] = s.status ?? "", ["stacks"] = s.stacks, ["turns"] = s.turns });
            return outList;
        }

        private static Dictionary<string, List<int>> ObjToDictListInt(Dictionary<string, object> d)
        {
            var dict = new Dictionary<string, List<int>>();
            if (d == null) return dict;
            foreach (var kvp in d) dict[kvp.Key] = ObjToIntList(kvp.Value as List<object>);
            return dict;
        }

        private static Dictionary<string, int> ObjToDictInt(Dictionary<string, object> d)
        {
            var dict = new Dictionary<string, int>();
            if (d == null) return dict;
            foreach (var kvp in d) dict[kvp.Key] = ToInt(kvp.Value);
            return dict;
        }

        private static List<RunData.BattleStartStatus> ObjToStatuses(List<object> list)
        {
            var outList = new List<RunData.BattleStartStatus>();
            if (list == null) return outList;
            foreach (var item in list)
                if (item is Dictionary<string, object> dd)
                    outList.Add(new RunData.BattleStartStatus
                    {
                        status = GetStr(dd, "status", ""), stacks = GetInt(dd, "stacks", 0), turns = GetInt(dd, "turns", -1)
                    });
            return outList;
        }

        // ============================================================
        //  基本取值
        // ============================================================

        private static int ToInt(object v)
        {
            if (v == null) return 0;
            if (v is int i) return i;
            if (v is long l) return (int)l;
            if (v is double db) return (int)db;
            if (v is float f) return (int)f;
            if (int.TryParse(v.ToString(), out var p)) return p;
            return 0;
        }

        private static int GetInt(Dictionary<string, object> d, string key, int def)
            => d != null && d.TryGetValue(key, out var v) && v != null ? ToInt(v) : def;

        private static float GetFloat(Dictionary<string, object> d, string key, float def)
        {
            if (d == null || !d.TryGetValue(key, out var v) || v == null) return def;
            if (v is double db) return (float)db;
            if (v is float f) return f;
            if (v is int i) return i;
            if (float.TryParse(v.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p)) return p;
            return def;
        }

        private static bool GetBool(Dictionary<string, object> d, string key, bool def)
        {
            if (d == null || !d.TryGetValue(key, out var v) || v == null) return def;
            if (v is bool b) return b;
            if (bool.TryParse(v.ToString(), out var p)) return p;
            return def;
        }

        private static string GetStr(Dictionary<string, object> d, string key, string def)
            => d != null && d.TryGetValue(key, out var v) && v != null ? v.ToString() : def;

        private static List<int> ObjToIntList(List<object> raw)
        {
            var list = new List<int>();
            if (raw == null) return list;
            foreach (var item in raw) list.Add(ToInt(item));
            return list;
        }

        private static List<int> GetIntList(Dictionary<string, object> d, string key)
            => d != null && d.TryGetValue(key, out var v) ? ObjToIntList(v as List<object>) : new List<int>();

        private static List<string> GetStrList(Dictionary<string, object> d, string key)
        {
            var list = new List<string>();
            if (d == null || !(d.TryGetValue(key, out var v) && v is List<object> raw)) return list;
            foreach (var item in raw) list.Add(item != null ? item.ToString() : "");
            return list;
        }
    }
}
