using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Roguelike.Data
{
    public static class ConfigLoader
    {
        private static AllConfig cachedConfig;
        private static bool isLoaded = false;

        public static AllConfig Config
        {
            get
            {
                if (!isLoaded) LoadAll();
                return cachedConfig;
            }
        }

        public static void LoadAll()
        {
            if (isLoaded) return;

            string configDir = Path.Combine(Application.streamingAssetsPath, "Config");
            if (!Directory.Exists(configDir))
            {
                Debug.LogError($"[ConfigLoader] Config directory not found: {configDir}");
                cachedConfig = new AllConfig();
                isLoaded = true;
                return;
            }

            cachedConfig = new AllConfig();

            // 加载每个配置文件
            LoadEnemies(configDir);
            LoadRelics(configDir);
            LoadEnchantments(configDir);
            LoadEvents(configDir);
            LoadPotions(configDir);
            LoadActs(configDir);
            LoadEncounters(configDir);

            // 建立查找字典
            BuildLookups();

            isLoaded = true;
            Debug.Log($"[ConfigLoader] 所有配置加载完成: Enemies={cachedConfig.enemies.Count}, Relics={cachedConfig.relics.Count}, Enchantments={cachedConfig.enchantments.Count}, Events={cachedConfig.events.Count}, Potions={cachedConfig.potions.Count}, Acts={cachedConfig.acts.Count}, Encounters={cachedConfig.encounters.Count}");

            // 加载完立刻校验一遍，把配置问题一次性报出来
            ConfigValidator.Validate(cachedConfig);
        }

        public static void Reload()
        {
            isLoaded = false;
            LoadAll();
        }

        private static void LoadEnemies(string dir)
        {
            string path = Path.Combine(dir, "enemies.json");
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var root = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("enemies", out var listObj)) return;

            var list = listObj as List<object>;
            foreach (var item in list)
            {
                var dict = item as Dictionary<string, object>;
                if (dict == null) continue;
                cachedConfig.enemies.Add(ParseChecked("enemies", dict, ParseEnemy));
            }
        }

        private static EnemyData ParseEnemy(Dictionary<string, object> d)
        {
            var e = new EnemyData
            {
                id = GetInt(d, "id"),
                name = GetString(d, "name"),
                image = GetString(d, "image"),
                frameRate = GetFloat(d, "frameRate"),
                burrowImage = GetString(d, "burrowImage"),
                emergeImage = GetString(d, "emergeImage"),
                imageWidth = GetFloat(d, "imageWidth"),
                imageHeight = GetFloat(d, "imageHeight"),
                imageScale = GetFloat(d, "imageScale"),
                imageOffsetX = GetFloat(d, "imageOffsetX"),
                imageOffsetY = GetFloat(d, "imageOffsetY"),
                hp = GetInt(d, "hp"),
                maxHp = GetInt(d, "maxHp"),
                relicDrop = GetBool(d, "relicDrop"),
                bossRelic = GetBool(d, "bossRelic"),
                isBoss = GetBool(d, "isBoss"),
                passive = GetString(d, "passive")
            };

            if (d.TryGetValue("frames", out var fr) && fr is List<object> frList)
            {
                e.frames = frList.ConvertAll(x => x.ToString());
            }

            if (d.TryGetValue("burrowFrames", out var bfr) && bfr is List<object> bfrList)
            {
                e.burrowFrames = bfrList.ConvertAll(x => x.ToString());
            }

            if (d.TryGetValue("emergeFrames", out var efr) && efr is List<object> efrList)
            {
                e.emergeFrames = efrList.ConvertAll(x => x.ToString());
            }

            if (d.TryGetValue("goldRange", out var gr) && gr is List<object> grList)
            {
                e.goldRange = grList.ConvertAll(x => Convert.ToInt32(x));
            }

            if (d.TryGetValue("pools", out var pl) && pl is List<object> plList)
            {
                e.pools = plList.ConvertAll(x => x.ToString());
            }

            if (d.TryGetValue("intents", out var intents) && intents is List<object> intentList)
            {
                foreach (var i in intentList)
                {
                    var idict = i as Dictionary<string, object>;
                    if (idict != null)
                        e.intents.Add(ParseChecked("intent", idict, ParseIntent));
                }
            }

            return e;
        }

        private static IntentData ParseIntent(Dictionary<string, object> d)
        {
            var intent = new IntentData
            {
                type = GetString(d, "type"),
                value = GetInt(d, "value"),
                multiHit = GetBool(d, "multiHit"),
                hitCount = GetInt(d, "hitCount", 1),
                description = GetString(d, "description"),
                weight = GetInt(d, "weight", 100),
                status = GetString(d, "status"),
                duration = GetInt(d, "duration", -1),
                battleStart = GetBool(d, "battleStart")
            };

            // type = Multi：依次执行的子行动
            if (d.TryGetValue("actions", out var acts) && acts is List<object> actList)
            {
                intent.actions = new List<IntentData>();
                foreach (var a in actList)
                {
                    var adict = a as Dictionary<string, object>;
                    if (adict != null) intent.actions.Add(ParseChecked("intent.action", adict, ParseIntent));
                }
            }

            return intent;
        }

        private static void LoadRelics(string dir)
        {
            string path = Path.Combine(dir, "relics.json");
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var root = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("relics", out var listObj)) return;

            var list = listObj as List<object>;
            foreach (var item in list)
            {
                var dict = item as Dictionary<string, object>;
                if (dict == null) continue;
                cachedConfig.relics.Add(ParseChecked("relics", dict, ParseRelic));
            }
        }

        private static RelicData ParseRelic(Dictionary<string, object> d)
        {
            var r = new RelicData
            {
                id = GetInt(d, "id"),
                name = GetString(d, "name"),
                description = GetString(d, "description"),
                rarity = GetString(d, "rarity"),
                maxSlots = GetInt(d, "maxSlots", 1),
                eventOnly = GetBool(d, "eventOnly", false),
                iconPath = GetString(d, "iconPath")
            };

            if (d.TryGetValue("effects", out var eff) && eff is List<object> effList)
            {
                foreach (var e in effList)
                {
                    var edict = e as Dictionary<string, object>;
                    if (edict != null)
                    {
                        var red = new RelicEffectData
                        {
                            trigger = GetString(edict, "trigger"),
                            type = GetString(edict, "type"),
                            value = edict.GetValueOrDefault("value"),
                            condition = GetString(edict, "condition"),
                            duration = GetInt(edict, "duration", -1)
                        };
                        r.effects.Add(red);
                        CheckParsedFields("relic.effect", edict, red);
                    }
                }
            }

            return r;
        }

        private static void LoadEnchantments(string dir)
        {
            string path = Path.Combine(dir, "enchantments.json");
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var root = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("enchantments", out var listObj)) return;

            var list = listObj as List<object>;
            foreach (var item in list)
            {
                var dict = item as Dictionary<string, object>;
                if (dict == null) continue;
                cachedConfig.enchantments.Add(ParseChecked("enchantments", dict, ParseEnchantment));
            }
        }

        private static EnchantmentData ParseEnchantment(Dictionary<string, object> d)
        {
            var e = new EnchantmentData
            {
                id = GetInt(d, "id"),
                name = GetString(d, "name"),
                description = GetString(d, "description"),
                trigger = GetString(d, "trigger"),
                rarity = GetEnchantmentRarity(d),
                weight = GetInt(d, "weight", 10),
                isUnique = GetBool(d, "isUnique", true),
                pool = GetString(d, "pool"),
                exclusiveGroup = GetString(d, "exclusiveGroup"),
                condition = GetString(d, "condition")
            };

            if (d.TryGetValue("effects", out var eff) && eff is List<object> effList)
            {
                        foreach (var e2 in effList)
                        {
                            var edict = e2 as Dictionary<string, object>;
                            if (edict != null)
                            {
                                var eed = new EnchantmentEffectData
                                {
                                    type = GetString(edict, "type"),
                                    value = edict.GetValueOrDefault("value"),
                                    duration = GetInt(edict, "duration", -1),
                                    status = GetString(edict, "status")
                                };
                                e.effects.Add(eed);
                                CheckParsedFields("enchantment.effect", edict, eed);
                            }
                        }
                    }

            return e;
        }

        private static void LoadEvents(string dir)
        {
            string path = Path.Combine(dir, "events.json");
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var root = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("events", out var listObj)) return;

            var list = listObj as List<object>;
            foreach (var item in list)
            {
                var dict = item as Dictionary<string, object>;
                if (dict == null) continue;
                cachedConfig.events.Add(ParseChecked("events", dict, ParseEvent));
            }
        }

        private static EventData ParseEvent(Dictionary<string, object> d)
        {
            var e = new EventData
            {
                id = GetInt(d, "id"),
                title = GetString(d, "title"),
                description = GetString(d, "description"),
                theme = GetString(d, "theme"),
                condition = GetString(d, "condition"),
                once = GetBool(d, "once", false)
            };

            if (d.TryGetValue("options", out var opts) && opts is List<object> optList)
            {
                foreach (var o in optList)
                {
                    var odict = o as Dictionary<string, object>;
                    if (odict != null)
                    {
                        var opt = new EventOptionData
                        {
                            text = GetString(odict, "text"),
                            condition = GetString(odict, "condition")
                        };
                        CheckParsedFields("event.option", odict, opt);

                        if (odict.TryGetValue("results", out var res) && res is List<object> resList)
                        {
                            foreach (var r in resList)
                            {
                                var rdict = r as Dictionary<string, object>;
                                if (rdict != null)
                                {
                                    opt.results.Add(ParseChecked("event.result", rdict, ParseEventResult));
                                }
                            }
                        }
                        e.options.Add(opt);
                    }
                }
            }

            return e;
        }

        private static EventResultData ParseEventResult(Dictionary<string, object> d)
        {
            var r = new EventResultData
            {
                type = GetString(d, "type"),
                value = d.GetValueOrDefault("value"),
                count = GetInt(d, "count", 1),
                tierMin = GetInt(d, "tierMin", 1),
                rarity = GetString(d, "rarity"),
                suit = GetString(d, "suit"),
                enemyId = GetInt(d, "enemyId"),
                require = GetString(d, "require"),
                status = GetString(d, "status"),
                preview = GetBool(d, "preview", false),
                weight = GetInt(d, "weight", 1)
            };

            if (d.TryGetValue("outcomes", out var oc) && oc is List<object> ocList)
            {
                r.outcomes = ocList.ConvertAll(x => ParseEventResult(x as Dictionary<string, object>));
            }
            if (d.TryGetValue("win", out var w) && w is List<object> wList)
            {
                r.win = wList.ConvertAll(x => ParseEventResult(x as Dictionary<string, object>));
            }
            if (d.TryGetValue("lose", out var l) && l is List<object> lList)
            {
                r.lose = lList.ConvertAll(x => ParseEventResult(x as Dictionary<string, object>));
            }

            return r;
        }

        private static void LoadPotions(string dir)
        {
            string path = Path.Combine(dir, "potions.json");
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var root = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("potions", out var listObj)) return;

            var list = listObj as List<object>;
            foreach (var item in list)
            {
                var dict = item as Dictionary<string, object>;
                if (dict == null) continue;
                cachedConfig.potions.Add(ParseChecked("potions", dict, ParsePotion));
            }
        }

        private static PotionData ParsePotion(Dictionary<string, object> d)
        {
            var p = new PotionData
            {
                id = GetInt(d, "id"),
                name = GetString(d, "name"),
                description = GetString(d, "description"),
                icon = GetString(d, "icon")
            };

            if (d.TryGetValue("effect", out var eff) && eff is Dictionary<string, object> effDict)
            {
                p.effect = new PotionEffectData
                {
                    type = GetString(effDict, "type"),
                    value = effDict.GetValueOrDefault("value"),
                    duration = GetInt(effDict, "duration", -1),
                    status = GetString(effDict, "status"),
                    target = GetString(effDict, "target")
                };
                CheckParsedFields("potion.effect", effDict, p.effect);
            }

            return p;
        }

        private static void LoadActs(string dir)
        {
            string path = Path.Combine(dir, "acts.json");
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var root = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("acts", out var listObj)) return;

            var list = listObj as List<object>;
            foreach (var item in list)
            {
                var dict = item as Dictionary<string, object>;
                if (dict == null) continue;
                cachedConfig.acts.Add(ParseChecked("acts", dict, ParseAct));
            }
        }

        private static void LoadEncounters(string dir)
        {
            string path = Path.Combine(dir, "encounters.json");
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var root = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("encounters", out var listObj)) return;

            var list = listObj as List<object>;
            if (list == null) return;

            foreach (var item in list)
            {
                var dict = item as Dictionary<string, object>;
                if (dict == null) continue;

                var enc = new EncounterData
                {
                    id = GetInt(dict, "id"),
                    name = GetString(dict, "name"),
                    pool = GetString(dict, "pool"),
                    weight = GetInt(dict, "weight", 1)
                };

                if (dict.TryGetValue("enemies", out var arr) && arr is List<object> ids)
                    enc.enemies = ids.ConvertAll(x => Convert.ToInt32(x));

                CheckParsedFields("encounters", dict, enc);
                cachedConfig.encounters.Add(enc);
            }
        }

        private static ActData ParseAct(Dictionary<string, object> d)
        {
            var a = new ActData
            {
                actId = GetInt(d, "actId"),
                name = GetString(d, "name"),
                bossEnemyId = GetInt(d, "bossEnemyId", 0),
                totalBattles = GetInt(d, "totalBattles", 0),
                commonPool = GetString(d, "commonPool"),
                elitePool = GetString(d, "elitePool"),
                commonPoolLate = GetString(d, "commonPoolLate")
            };

            if (d.TryGetValue("eliteChancePerBattle", out var ecp) && ecp is List<object> ecpList)
            {
                foreach (var v in ecpList)
                {
                    if (v == null) continue;
                    a.eliteChancePerBattle.Add(Convert.ToSingle(v));
                }
            }

            if (d.TryGetValue("guaranteedRestAt", out var gra) && gra is List<object> graList)
            {
                a.guaranteedRestAt = graList.ConvertAll(x => Convert.ToInt32(x));
            }
            if (d.TryGetValue("guaranteedShopAt", out var gsa) && gsa is List<object> gsaList)
            {
                a.guaranteedShopAt = gsaList.ConvertAll(x => Convert.ToInt32(x));
            }

            return a;
        }

        private static void BuildLookups()
        {
            // 这里可以建立 ID -> Data 的字典，方便运行时快速查找
        }

        // ===== Helper Methods =====
        private static int GetInt(Dictionary<string, object> dict, string key, int defaultValue = 0)
        {
            if (dict.TryGetValue(key, out var val))
            {
                if (val is int i) return i;
                if (val is long l) return (int)l;
                if (val is double d) return (int)d;
                if (val is float f) return (int)f;
                if (int.TryParse(val.ToString(), out var parsed)) return parsed;
            }
            return defaultValue;
        }

        private static string GetString(Dictionary<string, object> dict, string key, string defaultValue = "")
        {
            if (dict.TryGetValue(key, out var val) && val != null)
                return val.ToString();
            return defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> dict, string key, bool defaultValue = false)
        {
            if (dict.TryGetValue(key, out var val))
            {
                if (val is bool b) return b;
                if (bool.TryParse(val.ToString(), out var parsed)) return parsed;
            }
            return defaultValue;
        }

        private static float GetFloat(Dictionary<string, object> dict, string key, float defaultValue = 0f)
        {
            if (dict.TryGetValue(key, out var val))
            {
                if (val is float f) return f;
                if (val is double d) return (float)d;
                if (val is int i) return i;
                if (val is long l) return l;
                if (float.TryParse(val.ToString(), out var parsed)) return parsed;
            }
            return defaultValue;
        }

        /// <summary>附魔稀有度：优先读 rarity 字符串，兼容旧的 tier 整数</summary>
        private static string GetEnchantmentRarity(Dictionary<string, object> d)
        {
            var rarity = GetString(d, "rarity", "");
            if (!string.IsNullOrEmpty(rarity)) return rarity;
            return RarityUtil.FromTier(GetInt(d, "tier", 1));
        }

        // ===== 字段覆盖检查（防止「JSON 里有、Parse 方法漏读」）=====

        /// <summary>解析并顺带检查字段覆盖（新增字段忘了在 Parse 里读 → 报警）</summary>
        private static T ParseChecked<T>(string configName, Dictionary<string, object> d,
                                         Func<Dictionary<string, object>, T> parse) where T : class
        {
            var parsed = parse(d);
            CheckParsedFields(configName, d, parsed);
            return parsed;
        }

        /// <summary>
        /// 对比「JSON 的字段」和「解析出来的对象」：
        /// 1) JSON 里有、类上没有的 key → 拼写错误 / 字段已删除
        /// 2) 类上有、JSON 里有非空值、但解析结果是默认值 → Parse 方法漏读了这个字段
        /// 只在编辑器 / 开发版报警（正式包直接跳过）。
        /// </summary>
        private static void CheckParsedFields(string configName, Dictionary<string, object> d, object parsed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (d == null || parsed == null) return;

            var type = parsed.GetType();
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var fieldNames = new HashSet<string>();
            foreach (var f in fields) fieldNames.Add(f.Name);

            // 1) JSON 里多出来的 key（类里没有这个字段）
            foreach (var kv in d)
            {
                if (!fieldNames.Contains(kv.Key))
                    Debug.LogWarning($"[ConfigLoader] {configName}「{Describe(parsed)}」JSON 字段 '{kv.Key}' 在 {type.Name} 上没有对应字段（拼写错误？字段已删除？）");
            }

            // 2) 有值却没解析出来（最典型的「新增字段忘了解析」）
            foreach (var f in fields)
            {
                if (!d.TryGetValue(f.Name, out var raw)) continue;
                if (IsDefaultValue(f.GetValue(parsed)) && !IsDefaultValue(raw))
                    Debug.LogWarning($"[ConfigLoader] {configName}「{Describe(parsed)}」字段 '{f.Name}' 在 JSON 里有值（{raw}）但解析结果是默认值 —— Parse 方法很可能漏读了这个字段");
            }
#endif
        }

        /// <summary>值是否「空/默认」（null、空串、0、false、空集合）</summary>
        private static bool IsDefaultValue(object v)
        {
            if (v == null) return true;
            if (v is string s) return s.Length == 0;
            if (v is bool b) return !b;
            if (v is int i) return i == 0;
            if (v is long l) return l == 0L;
            if (v is float f) return Mathf.Approximately(f, 0f);
            if (v is double db) return Math.Abs(db) < 1e-9;
            if (v is System.Collections.ICollection c) return c.Count == 0;
            return false;
        }

        /// <summary>给日志用的友好标识（id / 名字）</summary>
        private static string Describe(object o)
        {
            var t = o.GetType();
            var idField = t.GetField("id") ?? t.GetField("actId");
            if (idField != null)
            {
                var id = idField.GetValue(o);
                if (id != null && !(id is int iid && iid == 0)) return $"id={id}";
            }
            var nameField = t.GetField("name") ?? t.GetField("title");
            if (nameField != null)
            {
                var name = nameField.GetValue(o);
                if (name != null && !string.IsNullOrEmpty(name.ToString())) return name.ToString();
            }
            return t.Name;
        }

        // ===== 快捷查找方法 =====
        public static EnemyData GetEnemy(int id) => cachedConfig?.enemies.Find(e => e.id == id);
        public static EnemyData GetEnemyByPool(string pool) => cachedConfig?.enemies.Find(e => e.pools.Contains(pool));
        public static List<EnemyData> GetEnemiesByPool(string pool) => cachedConfig?.enemies.FindAll(e => e.pools.Contains(pool));
        public static RelicData GetRelic(int id) => cachedConfig?.relics.Find(r => r.id == id);
        public static List<RelicData> GetRelicsByRarity(string rarity) => cachedConfig?.relics.FindAll(r => r.rarity == rarity);
        public static EnchantmentData GetEnchantment(int id) => cachedConfig?.enchantments.Find(e => e.id == id);
        public static List<EnchantmentData> GetEnchantmentsByTier(int tier) => cachedConfig?.enchantments.FindAll(e => RarityUtil.Tier(e.rarity) == tier);
        public static EventData GetEvent(int id) => cachedConfig?.events.Find(e => e.id == id);
        public static List<EventData> GetEventsByTheme(string theme) => cachedConfig?.events.FindAll(e => e.theme == theme) ?? new List<EventData>();
        public static PotionData GetPotion(int id) => cachedConfig?.potions.Find(p => p.id == id);
        public static ActData GetAct(int actId) => cachedConfig?.acts.Find(a => a.actId == actId);

        // ===== Encounter =====
        public static EncounterData GetEncounter(int id) => cachedConfig?.encounters.Find(e => e.id == id);

        public static List<EncounterData> GetEncountersByPool(string pool)
            => cachedConfig?.encounters.FindAll(e => e.pool == pool) ?? new List<EncounterData>();

        /// <summary>按池加权随机抽一个固定敌人组合（池里没有配置则返回 null）</summary>
        public static EncounterData PickEncounterByPool(string pool)
        {
            var list = GetEncountersByPool(pool);
            if (list.Count == 0) return null;

            int total = 0;
            foreach (var e in list) total += Mathf.Max(1, e.weight);

            int roll = UnityEngine.Random.Range(0, total);
            foreach (var e in list)
            {
                roll -= Mathf.Max(1, e.weight);
                if (roll < 0) return e;
            }
            return list[list.Count - 1];
        }

        /// <summary>把组合展开成敌人列表（跳过不存在的 id）</summary>
        public static List<EnemyData> ResolveEncounter(EncounterData enc)
        {
            var result = new List<EnemyData>();
            if (enc == null) return result;

            foreach (int id in enc.enemies)
            {
                var e = GetEnemy(id);
                if (e != null) result.Add(e);
                else Debug.LogWarning($"[ConfigLoader] 遭遇「{enc.name}」(id={enc.id}) 引用了不存在的敌人 id={id}");
            }
            return result;
        }
    }
}