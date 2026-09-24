using System;
using System.Collections.Generic;

namespace Roguelike.Data
{
    // 简易 JSON 解析器（无依赖）
    public static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var parser = new Parser(json);
            return parser.ParseValue();
        }

        public static string Serialize(object obj, bool pretty = false)
        {
            var writer = new Writer(pretty);
            writer.WriteValue(obj);
            return writer.ToString();
        }

        private class Parser
        {
            private readonly string json;
            private int index;

            public Parser(string json) { this.json = json; }

            public object ParseValue()
            {
                SkipWhitespace();
                if (index >= json.Length) return null;

                char c = json[index];
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return ParseString();
                if (c == 't' || c == 'f') return ParseBool();
                if (c == 'n') return ParseNull();
                return ParseNumber();
            }

            private void SkipWhitespace()
            {
                while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
            }

            private Dictionary<string, object> ParseObject()
            {
                var dict = new Dictionary<string, object>();
                index++; // skip {
                SkipWhitespace();

                if (index < json.Length && json[index] == '}')
                {
                    index++;
                    return dict;
                }

                while (true)
                {
                    SkipWhitespace();
                    string key = ParseString();
                    SkipWhitespace();
                    index++; // skip :
                    SkipWhitespace();
                    object value = ParseValue();
                    dict[key] = value;
                    SkipWhitespace();

                    if (index < json.Length && json[index] == ',')
                    {
                        index++;
                        continue;
                    }
                    if (index < json.Length && json[index] == '}')
                    {
                        index++;
                        break;
                    }
                }
                return dict;
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                index++; // skip [
                SkipWhitespace();

                if (index < json.Length && json[index] == ']')
                {
                    index++;
                    return list;
                }

                while (true)
                {
                    SkipWhitespace();
                    list.Add(ParseValue());
                    SkipWhitespace();

                    if (index < json.Length && json[index] == ',')
                    {
                        index++;
                        continue;
                    }
                    if (index < json.Length && json[index] == ']')
                    {
                        index++;
                        break;
                    }
                }
                return list;
            }

            private string ParseString()
            {
                index++; // skip "
                int start = index;
                while (index < json.Length && json[index] != '"') index++;
                string result = json.Substring(start, index - start);
                index++; // skip "
                return result.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\t", "\t");
            }

            private bool ParseBool()
            {
                if (json.Substring(index, 4) == "true") { index += 4; return true; }
                index += 5; // false
                return false;
            }

            private object ParseNull()
            {
                index += 4; // null
                return null;
            }

            private object ParseNumber()
            {
                int start = index;
                while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '-' || json[index] == '.' || json[index] == 'e' || json[index] == 'E')) index++;
                string numStr = json.Substring(start, index - start);
                if (numStr.Contains(".") || numStr.Contains("e") || numStr.Contains("E"))
                    return double.Parse(numStr, System.Globalization.CultureInfo.InvariantCulture);
                return int.Parse(numStr, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private class Writer
        {
            private readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();
            private readonly bool pretty;
            private int indent = 0;

            public Writer(bool pretty) { this.pretty = pretty; }

            public void WriteValue(object obj)
            {
                if (obj == null) { sb.Append("null"); return; }

                var type = obj.GetType();
                if (type == typeof(string)) { WriteString((string)obj); return; }
                if (type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double) || type == typeof(bool))
                { sb.Append(Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture).ToLower()); return; }
                if (obj is Dictionary<string, object> dict) { WriteObject(dict); return; }
                if (obj is System.Collections.IList list) { WriteArray(list); return; }
                if (obj is object[] arr) { WriteArray(arr); return; }

                sb.Append("\"").Append(obj.ToString()).Append("\"");
            }

            private void WriteString(string s)
            {
                sb.Append("\"").Append(s.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\t", "\\t")).Append("\"");
            }

            private void WriteObject(Dictionary<string, object> dict)
            {
                sb.Append("{");
                if (pretty) { sb.Append("\n"); indent++; }

                bool first = true;
                foreach (var kvp in dict)
                {
                    if (!first) { sb.Append(","); if (pretty) sb.Append("\n"); }
                    first = false;
                    if (pretty) sb.Append(new string(' ', indent * 4));
                    WriteString(kvp.Key);
                    sb.Append(": ");
                    WriteValue(kvp.Value);
                }

                if (pretty) { sb.Append("\n"); indent--; if (dict.Count > 0) sb.Append(new string(' ', indent * 4)); }
                sb.Append("}");
            }

            private void WriteArray(System.Collections.IList list)
            {
                sb.Append("[");
                if (pretty) { sb.Append("\n"); indent++; }

                bool first = true;
                foreach (var item in list)
                {
                    if (!first) { sb.Append(","); if (pretty) sb.Append("\n"); }
                    first = false;
                    if (pretty) sb.Append(new string(' ', indent * 4));
                    WriteValue(item);
                }

                if (pretty) { sb.Append("\n"); indent--; if (list.Count > 0) sb.Append(new string(' ', indent * 4)); }
                sb.Append("]");
            }

            public override string ToString() => sb.ToString();
        }
    }
}