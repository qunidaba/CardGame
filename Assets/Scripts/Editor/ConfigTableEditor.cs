using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Roguelike.Data;

/// <summary>
/// 配置表编辑器窗口：在 Unity 编辑器中可视化编辑 StreamingAssets/Config 下的 JSON 配置
/// </summary>
public class ConfigTableEditor : EditorWindow
{
    private enum ConfigType
    {
        Enemies,
        Relics,
        Enchantments,
        Events,
        Potions,
        Acts,
        Encounters
    }

    private ConfigType currentType = ConfigType.Enemies;
    private Vector2 scrollPos;
    private string searchText = "";
    private object pendingDelete;

    // 字段筛选
    private string filterField = "";
    private string filterValue = "";
    private string[] filterFieldNames = new string[0];

    // 缓存数据
    private List<object> cachedItems = new List<object>();
    private Type currentItemType;
    private string configPath;
    private string jsonRootKey;

    // ===== 反射 / 枚举元数据缓存（避免每帧反射与字符串分配）=====
    private static readonly Dictionary<Type, FieldInfo[]> fieldCache = new Dictionary<Type, FieldInfo[]>();
    private static readonly Dictionary<Type, PropertyInfo[]> propCache = new Dictionary<Type, PropertyInfo[]>();
    private static readonly Dictionary<string, string[]> enumPopupCache = new Dictionary<string, string[]>();

    private static FieldInfo[] Fields(Type t)
    {
        if (t == null) return new FieldInfo[0];
        if (!fieldCache.TryGetValue(t, out var f))
        {
            f = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
            fieldCache[t] = f;
        }
        return f;
    }

    private static PropertyInfo[] Props(Type t)
    {
        if (t == null) return new PropertyInfo[0];
        if (!propCache.TryGetValue(t, out var p))
        {
            p = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            propCache[t] = p;
        }
        return p;
    }

    // ===== 过滤结果 / 搜索文本缓存 =====
    private readonly Dictionary<object, string> searchTextCache = new Dictionary<object, string>();
    private List<object> filteredCache = new List<object>();
    private bool filteredDirty = true;
    private string lastSearchText;
    private string lastFilterField;
    private string lastFilterValue;
    private string[] filterDisplayCache;

    // ===== 展开状态（默认全部折叠，只绘制展开的条目）=====
    private readonly HashSet<object> expandedItems = new HashSet<object>();
    private bool expandAllItems = false;
    private bool expandAllPending = false;

    /// <summary>数据或过滤条件变化时调用：清缓存，下一帧重算</summary>
    private void InvalidateFilter()
    {
        filteredDirty = true;
        searchTextCache.Clear();
    }

    [MenuItem("Tools/配置表编辑器")]
    public static void ShowWindow()
    {
        GetWindow<ConfigTableEditor>("配置表编辑器").Show();
    }

    private void OnEnable()
    {
        LoadConfig(ConfigType.Enemies);
    }

    private void OnGUI()
    {
        // 顶部工具栏
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        var newType = (ConfigType)EditorGUILayout.EnumPopup(currentType, GUILayout.Width(120));
        if (newType != currentType)
            LoadConfig(newType);   // 切换配置类型立即重新加载
        if (GUILayout.Button("重新加载", EditorStyles.toolbarButton, GUILayout.Width(80)))
            LoadConfig(currentType);
        if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(60)))
            SaveConfig();
        // 新增入口放在左侧固定位置，避免被 FlexibleSpace 挤到窗口外看不见
        if (GUILayout.Button("＋ 新增条目", EditorStyles.toolbarButton, GUILayout.Width(90)))
            CreateNewItem();
        GUILayout.FlexibleSpace();
        searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField, GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();

        // 字段筛选栏
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("按字段筛选", EditorStyles.miniLabel, GUILayout.Width(64));

        if (filterDisplayCache == null || filterDisplayCache.Length != filterFieldNames.Length + 1)
        {
            filterDisplayCache = new string[filterFieldNames.Length + 1];
            filterDisplayCache[0] = "（全部字段）";
            Array.Copy(filterFieldNames, 0, filterDisplayCache, 1, filterFieldNames.Length);
        }

        int currentIndex = string.IsNullOrEmpty(filterField) ? 0 : Array.IndexOf(filterFieldNames, filterField) + 1;
        if (currentIndex < 0) currentIndex = 0;
        int newIndex = EditorGUILayout.Popup(currentIndex, filterDisplayCache, GUILayout.Width(150));
        filterField = newIndex == 0 ? "" : filterFieldNames[newIndex - 1];

        filterValue = EditorGUILayout.TextField(filterValue, GUILayout.Width(160));

        if (GUILayout.Button("清除", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            filterField = "";
            filterValue = "";
        }
        GUILayout.FlexibleSpace();

        // 全部展开 / 收起
        if (GUILayout.Button(expandAllItems ? "全部收起" : "全部展开", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            expandAllItems = !expandAllItems;
            expandedItems.Clear();
            expandAllPending = expandAllItems;   // 展开要等列表重算后处理
        }
        EditorGUILayout.EndHorizontal();

        // 搜索/筛选条件变化时才重算列表
        if (searchText != lastSearchText || filterField != lastFilterField || filterValue != lastFilterValue)
        {
            lastSearchText = searchText;
            lastFilterField = filterField;
            lastFilterValue = filterValue;
            filteredDirty = true;
        }

        // 列表
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        DrawItemList();
        EditorGUILayout.EndScrollView();
    }

    private void LoadConfig(ConfigType type)
    {
        currentType = type;
        configPath = GetConfigPath(type);
        currentItemType = GetItemType(type);
        jsonRootKey = GetJsonRootKey(type);

        filterFieldNames = GetPublicFieldNames(currentItemType);
        filterDisplayCache = null;
        filterField = "";

        // 切换配置类型：清空缓存与展开状态
        filteredDirty = true;
        searchTextCache.Clear();
        expandedItems.Clear();

        if (File.Exists(configPath))
        {
            string json = File.ReadAllText(configPath);
            var dict = Roguelike.Data.MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (dict != null && dict.TryGetValue(jsonRootKey, out var listObj) && listObj is List<object> list)
            {
                cachedItems = list.ConvertAll(item => ConvertDictToObject(item as Dictionary<string, object>, currentItemType));
            }
            else
            {
                cachedItems = new List<object>();
            }
        }
        else
        {
            cachedItems = new List<object>();
        }
    }

    private object ConvertDictToObject(Dictionary<string, object> dict, Type targetType)
    {
        if (dict == null) return Activator.CreateInstance(targetType);
        var obj = Activator.CreateInstance(targetType);
        foreach (var kvp in dict)
        {
            var field = targetType.GetField(kvp.Key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, ConvertValue(kvp.Value, field.FieldType));
            }
            else
            {
                var prop = targetType.GetProperty(kvp.Key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(obj, ConvertValue(kvp.Value, prop.PropertyType));
                }
            }
        }
        return obj;
    }

    private object ConvertValue(object value, Type targetType)
    {
        if (value == null) return null;
        if (targetType.IsPrimitive || targetType == typeof(string) || targetType == typeof(decimal))
        {
            return Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
        }
        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value.ToString());
        }
        if (targetType.IsArray)
        {
            var elementType = targetType.GetElementType();
            if (value is List<object> list)
            {
                var array = Array.CreateInstance(elementType, list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    array.SetValue(ConvertValue(list[i], elementType), i);
                }
                return array;
            }
        }
        if (targetType.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(targetType))
        {
            var genericArgs = targetType.GetGenericArguments();
            if (genericArgs.Length == 1 && value is List<object> list)
            {
                var listType = typeof(List<>).MakeGenericType(genericArgs[0]);
                var result = Activator.CreateInstance(listType);
                var addMethod = listType.GetMethod("Add");
                foreach (var item in list)
                {
                    addMethod.Invoke(result, new[] { ConvertValue(item, genericArgs[0]) });
                }
                return result;
            }
        }
        if (targetType.IsClass && targetType != typeof(string))
        {
            if (value is Dictionary<string, object> dict)
            {
                return ConvertDictToObject(dict, targetType);
            }
        }
        return value;
    }

    private void DrawItemList()
    {
        // 只在数据/过滤条件变化时重算（OnGUI 每帧会跑多次）
        if (filteredDirty)
        {
            filteredDirty = false;
            string q = string.IsNullOrEmpty(searchText) ? null : searchText.ToLowerInvariant();
            filteredCache = new List<object>();
            foreach (var item in cachedItems)
            {
                if (item == null) continue;
                if (!MatchesSearch(item, q)) continue;
                if (!MatchesFieldFilter(item)) continue;
                filteredCache.Add(item);
            }

            // 按 id 从小到大排序（Acts 用 actId）
            filteredCache.Sort((a, b) => GetSortId(a).CompareTo(GetSortId(b)));
        }

        // 「全部展开」：等列表重算完再展开
        if (expandAllPending)
        {
            expandAllPending = false;
            expandedItems.Clear();
            foreach (var it in filteredCache) expandedItems.Add(it);
        }

        foreach (var item in filteredCache)
        {
            EditorGUILayout.BeginVertical("box");

            // 折叠：未展开的条目完全不绘制内部字段（这是卡顿的主因）
            bool expanded = expandedItems.Contains(item);
            EditorGUILayout.BeginHorizontal();
            bool newExpanded = EditorGUILayout.Foldout(expanded, GetItemLabel(item), true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(expanded ? "收起" : "展开", EditorStyles.miniButton, GUILayout.Width(46)))
                newExpanded = !expanded;
            if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                pendingDelete = item;   // 循环结束后再移除，避免边遍历边改
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            if (newExpanded != expanded)
            {
                if (newExpanded) expandedItems.Add(item);
                else expandedItems.Remove(item);
            }

            if (newExpanded) DrawItemInspector(item);

            EditorGUILayout.EndVertical();
        }

        if (pendingDelete != null)
        {
            cachedItems.Remove(pendingDelete);
            expandedItems.Remove(pendingDelete);
            pendingDelete = null;
            InvalidateFilter();
        }

        if (filteredCache.Count == 0)
            EditorGUILayout.LabelField("无数据", EditorStyles.centeredGreyMiniLabel);
    }

    /// <summary>列表里显示的条目标题（[id] 名称/标题）</summary>
    private string GetItemLabel(object item)
    {
        if (item == null) return "(null)";

        var type = item.GetType();
        string id = null, name = null;
        foreach (var f in Fields(type))
        {
            if (f.Name == "id")
            {
                try { id = f.GetValue(item)?.ToString(); } catch { }
            }
            else if (name == null && (f.Name == "name" || f.Name == "title"))
            {
                try { name = f.GetValue(item) as string; } catch { }
            }
        }

        if (id != null && !string.IsNullOrEmpty(name)) return $"[{id}] {name}";
        if (!string.IsNullOrEmpty(name)) return name;
        if (id != null) return $"#{id}";
        return type.Name;
    }

    /// <summary>按字段内容（含嵌套对象与列表）匹配搜索文本；lowerQuery 需已转小写</summary>
    private bool MatchesSearch(object item, string lowerQuery)
    {
        if (string.IsNullOrEmpty(lowerQuery)) return true;
        return GetSearchText(item).ToLowerInvariant().Contains(lowerQuery);
    }

    /// <summary>条目的可搜索文本（缓存，编辑后由 InvalidateFilter 清空）</summary>
    private string GetSearchText(object item)
    {
        if (item == null) return "";
        if (searchTextCache.TryGetValue(item, out var cached)) return cached;
        string text = CollectSearchText(item, 0);
        searchTextCache[item] = text;
        return text;
    }

    private string CollectSearchText(object obj, int depth)
    {
        if (obj == null || depth > 3) return "";
        var type = obj.GetType();
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum || type == typeof(decimal))
            return obj.ToString();

        var sb = new System.Text.StringBuilder();
        var fields = Fields(type);
        foreach (var field in fields)
        {
            object value;
            try { value = field.GetValue(obj); }
            catch { continue; }
            if (value == null) continue;

            if (value is System.Collections.IEnumerable en && !(value is string))
            {
                foreach (var element in en)
                    sb.Append(' ').Append(CollectSearchText(element, depth + 1));
            }
            else
            {
                sb.Append(' ').Append(CollectSearchText(value, depth + 1));
            }
        }
        return sb.ToString();
    }

    private static string[] GetPublicFieldNames(Type type)
    {
        if (type == null) return new string[0];
        return Fields(type).Select(f => f.Name).ToArray();
    }

    private static readonly Dictionary<string, FieldInfo> fieldByNameCache = new Dictionary<string, FieldInfo>();

    private static FieldInfo FindField(Type t, string name)
    {
        if (t == null) return null;
        string key = t.FullName + "." + name;
        if (!fieldByNameCache.TryGetValue(key, out var f))
        {
            f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            fieldByNameCache[key] = f;
        }
        return f;
    }

    /// <summary>按选定字段筛选：基础/枚举精确匹配，字符串/复合包含匹配</summary>
    private bool MatchesFieldFilter(object item)
    {
        if (string.IsNullOrEmpty(filterField) || string.IsNullOrEmpty(filterValue)) return true;

        var field = FindField(item.GetType(), filterField);
        if (field == null) return true;

        object value;
        try { value = field.GetValue(item); }
        catch { return true; }
        if (value == null) return false;

        if (field.FieldType.IsPrimitive || field.FieldType.IsEnum)
            return string.Equals(value.ToString(), filterValue, StringComparison.OrdinalIgnoreCase);
        if (field.FieldType == typeof(string))
            return value.ToString().ToLowerInvariant().Contains(filterValue.ToLowerInvariant());
        return CollectSearchText(value, 0).ToLowerInvariant().Contains(filterValue.ToLowerInvariant());
    }

    private void DrawItemInspector(object item)
    {
        if (item == null) return;

        var type = item.GetType();
        var fields = Fields(type);
        var props = Props(type);

        foreach (var field in fields)
        {
            try
            {
                var value = field.GetValue(item);
                DrawEditableField(item, field.Name, value, field.FieldType, (newValue) => { field.SetValue(item, newValue); InvalidateFilter(); });
            }
            catch { }
        }
        foreach (var prop in props)
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            try
            {
                var value = prop.GetValue(item);
                DrawEditableField(item, prop.Name, value, prop.PropertyType, (newValue) => { prop.SetValue(item, newValue); InvalidateFilter(); });
            }
            catch { }
        }

        // 删除按钮
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("删除", GUILayout.Width(60)))
        {
            if (EditorUtility.DisplayDialog("确认删除", "确定要删除此项吗？", "删除", "取消"))
            {
                cachedItems.Remove(item);
                expandedItems.Remove(item);
                InvalidateFilter();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawEditableField(object parent, string name, object value, Type type, System.Action<object> setter)
    {
        EditorGUI.BeginChangeCheck();

        object newValue = null;

        if (type == typeof(int))
        {
            newValue = EditorGUILayout.IntField(name, value != null ? (int)value : 0);
        }
        else if (type == typeof(float))
        {
            newValue = EditorGUILayout.FloatField(name, value != null ? (float)value : 0f);
        }
        else if (type == typeof(double))
        {
            newValue = EditorGUILayout.DoubleField(name, value != null ? (double)value : 0.0);
        }
        else if (type == typeof(bool))
        {
            newValue = EditorGUILayout.Toggle(name, value != null && (bool)value);
        }
        else if (type == typeof(string))
        {
            if (TryGetEnum(parent?.GetType(), name, out var enumType, out var allowEmpty))
            {
                newValue = DrawEnumStringPopup(name, value as string, enumType, allowEmpty);

                // 新建对象的字符串枚举字段默认是 null：下拉框会显示第一项，
                // 但用户没动过控件就不会触发 EndChangeCheck，导致存下来还是 null。
                // 这里直接把"显示出来的那个值"补写一次（可空枚举保持 null 不动）。
                if (string.IsNullOrEmpty(value as string) && !string.IsNullOrEmpty(newValue as string))
                {
                    setter(newValue);
                    return;
                }
            }
            else
            {
                newValue = EditorGUILayout.TextField(name, value as string ?? "");
            }
        }
        else if (type == typeof(object))
        {
            DrawObjectField(name, value, setter);
            return;
        }
        else if (type.IsEnum)
        {
            newValue = EditorGUILayout.EnumPopup(name, value != null ? (Enum)value : (Enum)Enum.GetValues(type).GetValue(0));
        }
        else if (type.IsArray || (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type)))
        {
            var elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
            var list = value as System.Collections.IList;
            if (list == null && value != null)
            {
                // 尝试转换为 List
                var listType = typeof(List<>).MakeGenericType(elementType);
                var ctor = listType.GetConstructor(new[] { type });
                if (ctor != null)
                {
                    list = (System.Collections.IList)ctor.Invoke(new[] { value });
                }
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, $"[{elementType.Name}] Count: {list?.Count ?? 0}");
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                if (list == null)
                {
                    var newList = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                    setter(newList);
                    list = newList;
                }
                var newItem = Activator.CreateInstance(elementType);
                list.Add(newItem);
                InvalidateFilter();
            }
            EditorGUILayout.EndHorizontal();

            if (list != null)
            {
                // List<int>（如遭遇组合的敌人 id 列表）：额外给一个逗号分隔的快速输入框
                if (elementType == typeof(int))
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(list[i]);
                    }
                    string quick = sb.ToString();
                    string editedQuick = EditorGUILayout.TextField("快速输入（逗号分隔）", quick);
                    if (editedQuick != quick)
                    {
                        var parts = editedQuick.Split(new[] { ',', '，', ' ', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
                        list.Clear();
                        foreach (var p in parts)
                            if (int.TryParse(p.Trim(), out int v)) list.Add(v);
                        InvalidateFilter();
                    }
                }

                EditorGUI.indentLevel++;
                for (int i = 0; i < list.Count; i++)
                {
                    var elem = list[i];
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Element {i}", EditorStyles.boldLabel);
                    if (GUILayout.Button("-", GUILayout.Width(25)))
                    {
                        list.RemoveAt(i);
                        i--;
                        InvalidateFilter();
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        continue;
                    }
                    EditorGUILayout.EndHorizontal();

                    if (elem != null)
                    {
                        // 基元/字符串/枚举元素（如 List<int> 的怪物金币区间）：没有字段可反射，直接画一个输入框
                        if (elementType.IsPrimitive || elementType == typeof(string) || elementType.IsEnum)
                        {
                            var current = list[i];
                            var edited = DrawPrimitiveElement(elementType, current);
                            if (!Equals(edited, current))
                            {
                                list[i] = edited;
                                InvalidateFilter();
                            }
                        }
                        else
                        {
                            var fields = Fields(elementType);
                            foreach (var field in fields)
                            {
                                try
                                {
                                    var fieldValue = field.GetValue(elem);
                                    DrawEditableField(elem, field.Name, fieldValue, field.FieldType, (newVal) => { field.SetValue(elem, newVal); InvalidateFilter(); });
                                }
                                catch { }
                            }
                            var props = Props(elementType);
                            foreach (var prop in props)
                            {
                                if (prop.CanRead && prop.CanWrite)
                                {
                                    try
                                    {
                                        var propValue = prop.GetValue(elem);
                                        DrawEditableField(elem, prop.Name, propValue, prop.PropertyType, (newVal) => { prop.SetValue(elem, newVal); InvalidateFilter(); });
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUI.indentLevel--;
            }
            return;
        }
        else if (type.IsClass && type != typeof(string))
        {
            EditorGUILayout.LabelField(name, $"({type.Name})");
            EditorGUI.indentLevel++;
            if (value != null)
            {
                var fields = Fields(type);
                foreach (var field in fields)
                {
                    try
                    {
                        var fieldValue = field.GetValue(value);
                        DrawEditableField(value, field.Name, fieldValue, field.FieldType, (newVal) => { field.SetValue(value, newVal); InvalidateFilter(); });
                    }
                    catch { }
                }
                var props = Props(type);
                foreach (var prop in props)
                {
                    if (prop.CanRead && prop.CanWrite)
                    {
                        try
                        {
                            var propValue = prop.GetValue(value);
                            DrawEditableField(value, prop.Name, propValue, prop.PropertyType, (newVal) => { prop.SetValue(value, newVal); InvalidateFilter(); });
                        }
                        catch { }
                    }
                }
            }
            EditorGUI.indentLevel--;
            return;
        }
        else
        {
            EditorGUILayout.LabelField(name, value?.ToString() ?? "null");
            return;
        }

        if (EditorGUI.EndChangeCheck() && newValue != null)
        {
            setter(newValue);
        }
    }

    /// <summary>画一个基元/字符串/枚举输入框并返回新值（用于 List&lt;int&gt; 这类元素）</summary>
    private object DrawPrimitiveElement(Type type, object value)
    {
        if (type == typeof(int)) return EditorGUILayout.IntField("Value", value != null ? (int)value : 0);
        if (type == typeof(float)) return EditorGUILayout.FloatField("Value", value != null ? (float)value : 0f);
        if (type == typeof(double)) return EditorGUILayout.DoubleField("Value", value != null ? (double)value : 0.0);
        if (type == typeof(bool)) return EditorGUILayout.Toggle("Value", value != null && (bool)value);
        if (type == typeof(string)) return EditorGUILayout.TextField("Value", value as string ?? "");
        if (type.IsEnum)
            return EditorGUILayout.EnumPopup("Value", value != null ? (Enum)value : (Enum)Enum.GetValues(type).GetValue(0));
        return value;
    }

    /// <summary>排序用的 id（id 优先，其次 actId；都没有就排到最后）</summary>
    private static int GetSortId(object item)
    {
        if (item == null) return int.MaxValue;

        var type = item.GetType();
        foreach (var key in new[] { "id", "actId" })
        {
            var f = type.GetField(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(int))
            {
                try { return (int)f.GetValue(item); } catch { }
            }
        }
        return int.MaxValue;
    }

    /// <summary>新增一个条目（工具栏「＋ 新增条目」按钮调用）</summary>
    private void CreateNewItem()
    {
        var newItem = Activator.CreateInstance(currentItemType);
        AutoAssignId(newItem);
        cachedItems.Add(newItem);
        expandedItems.Add(newItem);

        // 清掉搜索/筛选，否则新条目可能被过滤掉、看起来像"没加上"
        searchText = "";
        filterField = "";
        filterValue = "";
        lastSearchText = searchText;
        lastFilterField = filterField;
        lastFilterValue = filterValue;
        InvalidateFilter();

        Debug.Log($"[配置表编辑器] 已新增 {currentItemType.Name}: {GetItemLabel(newItem)}（改完记得点「保存」）");
    }

    /// <summary>给新条目自动分配一个不重复的 id（避免多条都是 0）</summary>
    private void AutoAssignId(object item)
    {
        var field = currentItemType.GetField("id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (field == null || field.FieldType != typeof(int)) return;

        int max = 0;
        foreach (var it in cachedItems)
        {
            if (it == null || it.GetType() != currentItemType) continue;
            try { max = Mathf.Max(max, (int)field.GetValue(it)); } catch { }
        }
        field.SetValue(item, max + 1);
    }

    private void SaveConfig()
    {
        // 保存时也按 id 排序，保证 JSON 文件本身是整齐的
        var ordered = new List<object>(cachedItems);
        ordered.Sort((a, b) => GetSortId(a).CompareTo(GetSortId(b)));

        var list = ordered.ConvertAll(item => ConvertObjectToDict(item));
        var dict = new Dictionary<string, object> { [jsonRootKey] = list };
        string json = Roguelike.Data.MiniJson.Serialize(dict, true);
        File.WriteAllText(configPath, json);
        AssetDatabase.Refresh();
        Debug.Log($"配置已保存: {configPath}");

        // 保存后立刻重载 + 校验（会跑字段覆盖检查和 ConfigValidator），问题直接进 Console
        Roguelike.Data.ConfigLoader.Reload();
    }

    private Dictionary<string, object> ConvertObjectToDict(object obj)
    {
        var dict = new Dictionary<string, object>();
        if (obj == null) return dict;
        var type = obj.GetType();
        var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        foreach (var field in fields)
        {
            dict[field.Name] = ConvertValueForJson(field.GetValue(obj));
        }
        var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        foreach (var prop in props)
        {
            if (prop.CanRead && prop.CanWrite)
            {
                dict[prop.Name] = ConvertValueForJson(prop.GetValue(obj));
            }
        }
        return dict;
    }

    private object ConvertValueForJson(object value)
    {
        if (value == null) return null;
        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type.IsEnum)
        {
            return value;
        }
        if (type.IsArray)
        {
            var array = (Array)value;
            var list = new List<object>();
            foreach (var item in array)
            {
                list.Add(ConvertValueForJson(item));
            }
            return list;
        }
        if (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            var list = new List<object>();
            foreach (var item in (System.Collections.IEnumerable)value)
            {
                list.Add(ConvertValueForJson(item));
            }
            return list;
        }
        if (type.IsClass && type != typeof(string))
        {
            return ConvertObjectToDict(value);
        }
        return value.ToString();
    }

    private string GetConfigPath(ConfigType type)
    {
        string fileName = type switch
        {
            ConfigType.Enemies => "enemies.json",
            ConfigType.Relics => "relics.json",
            ConfigType.Enchantments => "enchantments.json",
            ConfigType.Events => "events.json",
            ConfigType.Potions => "potions.json",
            ConfigType.Acts => "acts.json",
            ConfigType.Encounters => "encounters.json",
            _ => "enemies.json"
        };
        return Path.Combine(Application.streamingAssetsPath, "Config", fileName);
    }

    private string GetJsonRootKey(ConfigType type)
    {
        return type switch
        {
            ConfigType.Enemies => "enemies",
            ConfigType.Relics => "relics",
            ConfigType.Enchantments => "enchantments",
            ConfigType.Events => "events",
            ConfigType.Potions => "potions",
            ConfigType.Acts => "acts",
            ConfigType.Encounters => "encounters",
            _ => "enemies"
        };
    }

    private Type GetItemType(ConfigType type)
    {
        return type switch
        {
            ConfigType.Enemies => typeof(EnemyData),
            ConfigType.Relics => typeof(RelicData),
            ConfigType.Enchantments => typeof(EnchantmentData),
            ConfigType.Events => typeof(EventData),
            ConfigType.Potions => typeof(PotionData),
            ConfigType.Acts => typeof(ActData),
            ConfigType.Encounters => typeof(EncounterData),
            _ => typeof(EnemyData)
        };
    }

    // ===== 下拉选项（从运行时枚举自动生成）=====

    /// <summary>按「所属类型 + 字段名」找到对应的枚举</summary>
    private static bool TryGetEnum(Type parentType, string fieldName, out Type enumType, out bool allowEmpty)
    {
        enumType = null;
        allowEmpty = false;
        if (parentType == null) return false;

        if (fieldName == "condition") { enumType = typeof(HandCondition); allowEmpty = true; return true; }
        if (fieldName == "theme") { enumType = typeof(Suit); allowEmpty = true; return true; }
        if (fieldName == "rarity")
        {
            // 事件结果里用筛选（随机/指定/稀有及以上），其余用具体稀有度
            enumType = parentType == typeof(EventResultData) ? typeof(RarityFilter) : typeof(Rarity);
            return true;
        }
        if (fieldName == "status" && (parentType == typeof(IntentData) || parentType == typeof(EnchantmentEffectData) || parentType == typeof(PotionEffectData))) { enumType = typeof(Roguelike.StatusEffectType); allowEmpty = true; return true; }
        if (fieldName == "pool" && parentType == typeof(EnchantmentData)) { enumType = typeof(EnchantPool); allowEmpty = true; return true; }
        if (fieldName == "passive" && parentType == typeof(EnemyData)) { enumType = typeof(EnemyPassive); allowEmpty = true; return true; }
        if (fieldName == "target" && parentType == typeof(PotionEffectData)) { enumType = typeof(StatusTarget); allowEmpty = true; return true; }

        if (fieldName == "trigger")
        {
            if (parentType == typeof(RelicEffectData)) { enumType = typeof(RelicTrigger); return true; }
            if (parentType == typeof(EnchantmentData)) { enumType = typeof(Roguelike.EnchantmentSystem.TriggerType); return true; }
        }

        if (fieldName == "type")
        {
            if (parentType == typeof(RelicEffectData)) { enumType = typeof(RelicEffectType); return true; }
            if (parentType == typeof(EnchantmentEffectData)) { enumType = typeof(Roguelike.EnchantmentSystem.EffectType); return true; }
            if (parentType == typeof(EventResultData)) { enumType = typeof(EventResultType); return true; }
            if (parentType == typeof(PotionEffectData)) { enumType = typeof(PotionEffectType); return true; }
            if (parentType == typeof(IntentData)) { enumType = typeof(IntentType); return true; }
        }

        return false;
    }

    /// <summary>用枚举名生成下拉，返回值仍是字符串（保留自定义值与空值）</summary>
    private string DrawEnumStringPopup(string label, string current, Type enumType, bool allowEmpty)
    {
        // 枚举名/显示名数组按 (类型, 是否可空) 缓存，避免每帧 Enum.GetNames + 分配
        string cacheKey = enumType.FullName + "|" + allowEmpty;
        if (!enumPopupCache.TryGetValue(cacheKey, out var names))
        {
            var raw = Enum.GetNames(enumType);
            int offset = allowEmpty ? 1 : 0;
            names = new string[raw.Length + offset];
            if (allowEmpty) names[0] = "";
            Array.Copy(raw, 0, names, offset, raw.Length);
            enumPopupCache[cacheKey] = names;
        }

        // 自定义值（不在枚举里）：走慢路径，单独拼一次
        if (!string.IsNullOrEmpty(current) && Array.IndexOf(names, current) < 0)
        {
            var custom = new string[names.Length + 1];
            custom[0] = current;
            Array.Copy(names, 0, custom, 1, names.Length);
            var customDisplay = Array.ConvertAll(custom, n => string.IsNullOrEmpty(n) ? "（无）" : EnumDisplayName(enumType, n));
            int ci = EditorGUILayout.Popup(label, 0, customDisplay);
            return custom[ci];
        }

        int index = current == null ? 0 : Array.IndexOf(names, current);
        if (index < 0) index = 0;

        var displayKey = cacheKey + "|disp";
        if (!enumPopupCache.TryGetValue(displayKey, out var display))
        {
            display = Array.ConvertAll(names, n => string.IsNullOrEmpty(n) ? "（无）" : EnumDisplayName(enumType, n));
            enumPopupCache[displayKey] = display;
        }

        int newIndex = EditorGUILayout.Popup(label, index, display);
        return names[newIndex];
    }

    /// <summary>枚举值在编辑器里的中文显示名（存储仍是英文值）</summary>
    private static string EnumDisplayName(Type enumType, string name)
    {
        // 稀有度 / 稀有度筛选
        switch (name)
        {
            case "Common": return "普通";
            case "Rare": return "稀有";
            case "Epic": return "史诗";
            case "Curse": return "诅咒";
            case "Event": return "事件";
            case "Any": return "随机";
            case "RareOrAbove": return "稀有及以上";
        }

        // 事件的随机附魔池
        if (enumType == typeof(EnchantPool))
        {
            switch (name)
            {
                case "HandType": return "牌型类";
                case "Buff": return "增益类";
            }
        }

        // 敌人意图类型
        if (enumType == typeof(IntentType))
        {
            switch (name)
            {
                case "Attack": return "攻击";
                case "Defense": return "防御";
                case "Buff": return "强化";
                case "Debuff": return "减益（给玩家上状态）";
                case "MultiAttack": return "多段攻击";
                case "Multi": return "多重行动（一回合多动作）";
                case "Sequence": return "序列行动（接下来几回合固定执行）";
                case "Special": return "特殊技能";
                case "Taunt": return "嘲讽（强制玩家只能选它，1回合）";
                case "Charge": return "蓄力（下次攻击伤害翻倍）";
                case "Sunder": return "破防（对防御伤害翻倍）";
                case "Curse": return "诅咒（给玩家随机牌加指定诅咒，本场战斗）";
                case "Swallow": return "吞噬（吞掉玩家抽牌堆随机牌，敌人死亡归还）";
                case "Burrow": return "遁地（受到的攻击伤害固定为1，被攻击 N 次后出来）";
                case "Summon": return "召唤（value=敌人id，hitCount=数量；上限3个，满员不能用）";
                case "HealAllies": return "全体回血（给所有友方回复 value 点生命）";
            }
        }

        // 敌人被动
        if (enumType == typeof(EnemyPassive))
        {
            switch (name)
            {
                case "None": return "无";
                case "Weakness": return "弱点（每回合刷新2个弱点牌型，被非弱点牌型攻击+1力量）";
            }
        }

        // Buff / 状态的作用目标
        if (enumType == typeof(StatusTarget))
        {
            switch (name)
            {
                case "enemy": return "敌人";
                case "self": return "自己";
            }
        }

        // 状态效果（Buff 选择）
        if (enumType == typeof(Roguelike.StatusEffectType))
        {
            switch (name)
            {
                case "None": return "无";
                case "Poison": return "中毒";
                case "Burn": return "灼烧";
                case "Weaken": return "虚弱";
                case "Strength": return "力量";
                case "Dexterity": return "敏捷";
                case "Focus": return "专注";
                case "Artifact": return "神器";
                case "Thorns": return "荆棘";
                case "Regeneration": return "再生";
                case "Metallicize": return "金属化";
                case "Intangible": return "无形";
                case "Vulnerable": return "易伤";
                case "Rage": return "暴怒";
                case "DefenseUp": return "坚壁";
                case "TurnDamageBonus": return "顺风耳（本回合出牌加伤）";
                case "NextTurnDraw": return "同花之魂（下回合抽牌）";
                case "SuitDamageBonus": return "同花顺之巅（花色加伤）";
                case "SelfDamage": return "双刃剑（每回合自伤）";
                case "DrawBonus": return "抽牌+";
                case "MulliganBonus": return "重抽+";
                case "NoMulligan": return "封印（不可重抽）";
                case "SuitSeal": return "封禁（不可出该花色）";
                case "StraightDrawBonus": return "顺子抽牌（永久）";
                case "StraightDrawTemp": return "顺子抽牌（仅本场）";
                case "FlushDrawBonus": return "同花抽牌（永久）";
                case "FlushDamageBonus": return "同花伤害（永久）";
                case "DamageMultiplierMod": return "伤害倍率";
                case "FatePowerBonus": return "命运积攒";
                case "FateGainSeal": return "命运封禁";
                case "Challenge": return "挑战";
                case "Taunt": return "嘲讽（敌人主动技，勿手动配）";
                case "Charge": return "蓄力（敌人主动技，勿手动配）";
            }
        }

        return name;
    }

    private void DrawObjectField(string name, object value, System.Action<object> setter)
    {
        var typeNames = new[] { "int", "float", "string", "bool" };
        var currentType = value?.GetType() ?? typeof(int);
        int currentIndex = Array.IndexOf(typeNames, GetTypeName(currentType));
        if (currentIndex < 0) currentIndex = 0;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(name, GUILayout.Width(EditorGUIUtility.labelWidth - 4));
        int newIndex = EditorGUILayout.Popup(currentIndex, typeNames, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        if (newIndex != currentIndex)
        {
            value = CreateDefaultValue(typeNames[newIndex]);
        }

        EditorGUI.indentLevel++;
        EditorGUI.BeginChangeCheck();

        object newValue = null;
        switch (typeNames[newIndex])
        {
            case "int":
                newValue = EditorGUILayout.IntField("Value", value != null ? Convert.ToInt32(value) : 0);
                break;
            case "float":
                newValue = EditorGUILayout.FloatField("Value", value != null ? Convert.ToSingle(value) : 0f);
                break;
            case "string":
                newValue = EditorGUILayout.TextField("Value", value as string ?? "");
                break;
            case "bool":
                newValue = EditorGUILayout.Toggle("Value", value != null && Convert.ToBoolean(value));
                break;
        }

        if (EditorGUI.EndChangeCheck())
        {
            setter(newValue);
        }
        EditorGUI.indentLevel--;
    }

    private string GetTypeName(Type type)
    {
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)) return "int";
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return "float";
        if (type == typeof(string)) return "string";
        if (type == typeof(bool)) return "bool";
        return "int";
    }

    private object CreateDefaultValue(string typeName)
    {
        switch (typeName)
        {
            case "int": return 0;
            case "float": return 0f;
            case "string": return "";
            case "bool": return false;
            default: return 0;
        }
    }

    // 用于序列化任意对象的包装器
    [Serializable]
    private class ObjectWrapper : UnityEngine.Object
    {
        public object Value;
    }
}