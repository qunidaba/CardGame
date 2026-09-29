using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// 图鉴收集：记录「已发现」的附魔 / 遗物 / 药水 id，跨局持久（PlayerPrefs）。
    /// 发现来源：战斗奖励中作为选项出现，或实际获得。
    /// </summary>
    public static class CodexData
    {
        private const string KeyEnchantment = "codex.enchantments";
        private const string KeyRelic = "codex.relics";
        private const string KeyPotion = "codex.potions";

        private static HashSet<int> enchants;
        private static HashSet<int> relics;
        private static HashSet<int> potions;

        private static void EnsureLoaded()
        {
            if (enchants != null) return;
            enchants = Load(KeyEnchantment);
            relics = Load(KeyRelic);
            potions = Load(KeyPotion);
        }

        private static HashSet<int> Load(string key)
        {
            var set = new HashSet<int>();
            string s = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(s)) return set;
            foreach (var part in s.Split(','))
                if (int.TryParse(part, out int id)) set.Add(id);
            return set;
        }

        private static void Save(string key, HashSet<int> set)
        {
            PlayerPrefs.SetString(key, string.Join(",", set));
            PlayerPrefs.Save();
        }

        public static bool IsEnchantmentKnown(int id) { EnsureLoaded(); return enchants.Contains(id); }
        public static bool IsRelicKnown(int id) { EnsureLoaded(); return relics.Contains(id); }
        public static bool IsPotionKnown(int id) { EnsureLoaded(); return potions.Contains(id); }

        public static void DiscoverEnchantment(int id)
        {
            if (id <= 0) return;
            EnsureLoaded();
            if (enchants.Add(id)) Save(KeyEnchantment, enchants);
        }

        public static void DiscoverRelic(int id)
        {
            if (id <= 0) return;
            EnsureLoaded();
            if (relics.Add(id)) Save(KeyRelic, relics);
        }

        public static void DiscoverPotion(int id)
        {
            if (id <= 0) return;
            EnsureLoaded();
            if (potions.Add(id)) Save(KeyPotion, potions);
        }

        public static int KnownEnchantmentCount { get { EnsureLoaded(); return enchants.Count; } }
        public static int KnownRelicCount { get { EnsureLoaded(); return relics.Count; } }
        public static int KnownPotionCount { get { EnsureLoaded(); return potions.Count; } }

        /// <summary>清空图鉴（调试用）</summary>
        public static void ClearAll()
        {
            EnsureLoaded();
            enchants.Clear();
            relics.Clear();
            potions.Clear();
            Save(KeyEnchantment, enchants);
            Save(KeyRelic, relics);
            Save(KeyPotion, potions);
        }
    }
}
