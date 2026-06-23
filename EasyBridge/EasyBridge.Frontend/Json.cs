using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EasyBridge.Frontend
{
    /// <summary>
    /// 极简 JSON 读写，避免对游戏内 Newtonsoft 等程序集的硬依赖。
    /// 写：支持 Dictionary&lt;string,object&gt;、IEnumerable、string、bool、整数/浮点、null。
    /// 读：支持对象/数组/字符串/数字/bool/null，足够解析小型 POST 请求体。
    /// </summary>
    internal static class Json
    {
        // ---------- 写 ----------
        public static string Write(object value)
        {
            var sb = new StringBuilder(256);
            WriteValue(sb, value);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case string s:
                    WriteString(sb, s);
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case float f:
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case IDictionary<string, object> map:
                    WriteObject(sb, map);
                    break;
                case IEnumerable seq when !(value is string):
                    WriteArray(sb, seq);
                    break;
                default:
                    // 整数及其它 IConvertible 直接走不变文化输出
                    if (value is int || value is long || value is short || value is byte ||
                        value is sbyte || value is uint || value is ulong || value is ushort)
                    {
                        sb.Append(System.Convert.ToString(value, CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        WriteString(sb, value.ToString());
                    }
                    break;
            }
        }

        private static void WriteObject(StringBuilder sb, IDictionary<string, object> map)
        {
            sb.Append('{');
            bool first = true;
            foreach (var kv in map)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(sb, kv.Key);
                sb.Append(':');
                WriteValue(sb, kv.Value);
            }
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IEnumerable seq)
        {
            sb.Append('[');
            bool first = true;
            foreach (var item in seq)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteValue(sb, item);
            }
            sb.Append(']');
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------- 读 ----------
        public static object Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int i = 0;
            var v = ParseValue(text, ref i);
            return v;
        }

        /// <summary>把 Parse 结果当作对象取字符串字段（缺失返回 null）。</summary>
        public static string GetString(object obj, string key)
        {
            if (obj is IDictionary<string, object> m && m.TryGetValue(key, out var v) && v != null)
                return v as string ?? v.ToString();
            return null;
        }

        public static bool TryGetDouble(object obj, string key, out double val)
        {
            val = 0;
            if (obj is IDictionary<string, object> m && m.TryGetValue(key, out var v))
            {
                if (v is double d) { val = d; return true; }
                if (v is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p)) { val = p; return true; }
                if (v is bool b) { val = b ? 1 : 0; return true; }
            }
            return false;
        }

        public static bool TryGetBool(object obj, string key, out bool val)
        {
            val = false;
            if (obj is IDictionary<string, object> m && m.TryGetValue(key, out var v))
            {
                if (v is bool b) { val = b; return true; }
                if (v is string s && bool.TryParse(s, out var p)) { val = p; return true; }
                if (v is double d) { val = d != 0; return true; }
            }
            return false;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': i += 4; return true;   // true
                case 'f': i += 5; return false;  // false
                case 'n': i += 4; return null;   // null
                default: return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var map = new Dictionary<string, object>();
            i++; // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return map; }
            while (i < s.Length)
            {
                SkipWs(s, ref i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                var val = ParseValue(s, ref i);
                map[key] = val;
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; break; }
                break;
            }
            return map;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (i < s.Length)
            {
                var val = ParseValue(s, ref i);
                list.Add(val);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; break; }
                break;
            }
            return list;
        }

        private static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            if (i < s.Length && s[i] == '"') i++;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < s.Length)
                {
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 <= s.Length)
                            {
                                string hex = s.Substring(i, 4);
                                i += 4;
                                if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp))
                                    sb.Append((char)cp);
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            string num = s.Substring(start, i - start);
            if (double.TryParse(num, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                return d;
            return null;
        }
    }
}
