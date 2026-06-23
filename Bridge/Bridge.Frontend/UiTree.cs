using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Bridge.Frontend
{
    /// <summary>
    /// GameObject 层级遍历、文本读取、控件识别与路径解析的共享工具。
    /// 控件 id 形如 "Panel@0/Buttons@2/Confirm@1"：每段是 子物体名@兄弟索引，
    /// 兄弟索引保证唯一且可逆，便于动作注入时精确定位到同一控件。
    /// </summary>
    internal static class UiTree
    {
        public struct Control
        {
            public string Type;        // button/toggle/slider/dropdown/input
            public Component Component; // 命中的 UI 组件（可能是 TMP 反射对象）
        }

        // ---------- 路径 ----------
        public static string SegmentOf(Transform child)
        {
            string name = child.name ?? "";
            // 去掉斜杠和 @，避免破坏路径语法
            name = name.Replace('/', '_').Replace('@', '_');
            return name + "@" + child.GetSiblingIndex().ToString();
        }

        public static int IndexFromSegment(string seg)
        {
            int at = seg.LastIndexOf('@');
            if (at < 0) return -1;
            return int.TryParse(seg.Substring(at + 1), out int idx) ? idx : -1;
        }

        /// <summary>按 id 路径从根 Transform 解析到目标 Transform；失败返回 null。</summary>
        public static Transform ResolvePath(Transform root, string id)
        {
            if (root == null || string.IsNullOrEmpty(id)) return null;
            Transform cur = root;
            foreach (var seg in id.Split('/'))
            {
                if (seg.Length == 0) continue;
                int idx = IndexFromSegment(seg);
                if (idx < 0 || idx >= cur.childCount) return null;
                cur = cur.GetChild(idx);
            }
            return cur;
        }

        // ---------- 文本 ----------
        /// <summary>读取该 GameObject 自身的文本（Text 或 TMP），无则返回 null。</summary>
        public static string GetOwnText(GameObject go)
        {
            var components = go.GetComponents<Component>();
            foreach (var c in components)
            {
                if (c == null) continue;
                if (c is Text t)
                {
                    var s = t.text;
                    if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
                }
            }
            // TMP：通过反射读取 .text，避免硬依赖 Unity.TextMeshPro
            foreach (var c in components)
            {
                if (c == null) continue;
                var typeName = c.GetType().Name;
                if (typeName == "TextMeshProUGUI" || typeName == "TextMeshPro" || typeName == "TMP_Text")
                {
                    var s = ReadStringProp(c, "text");
                    if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
                }
            }
            return null;
        }

        /// <summary>DFS 找到子树中第一段非空文本（用作控件标签）。</summary>
        public static string FirstText(Transform t, int maxDepth = 4)
        {
            var own = GetOwnText(t.gameObject);
            if (own != null) return own;
            if (maxDepth <= 0) return null;
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                var r = FirstText(child, maxDepth - 1);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>收集子树中所有非空文本并用空格连接（用作按钮等复合控件的标签）。</summary>
        public static string AllTexts(Transform t, int maxDepth = 4)
        {
            var parts = new System.Collections.Generic.List<string>();
            CollectAllTexts(t, parts, maxDepth);
            return parts.Count > 0 ? string.Join(" ", parts) : null;
        }

        private static void CollectAllTexts(Transform t, System.Collections.Generic.List<string> parts, int maxDepth)
        {
            var own = GetOwnText(t.gameObject);
            if (own != null) parts.Add(own);
            if (maxDepth <= 0) return;
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                CollectAllTexts(child, parts, maxDepth - 1);
            }
        }

        // ---------- 控件识别 ----------
        public static Control DetectControl(GameObject go)
        {
            // 输入框（优先级最高，避免被内部 Button 抢占）
            var input = go.GetComponent<InputField>();
            if (input != null) return new Control { Type = "input", Component = input };
            var tmpInput = FindComponentByTypeName(go, "TMP_InputField");
            if (tmpInput != null) return new Control { Type = "input", Component = tmpInput };

            var dropdown = go.GetComponent<Dropdown>();
            if (dropdown != null) return new Control { Type = "dropdown", Component = dropdown };
            var tmpDropdown = FindComponentByTypeName(go, "TMP_Dropdown", "CDropdownLegacy");
            if (tmpDropdown != null) return new Control { Type = "dropdown", Component = tmpDropdown };

            var toggle = go.GetComponent<Toggle>();
            if (toggle != null) return new Control { Type = "toggle", Component = toggle };

            var slider = go.GetComponent<Slider>();
            if (slider != null) return new Control { Type = "slider", Component = slider };

            var button = go.GetComponent<Button>();
            if (button != null) return new Control { Type = "button", Component = button };

            return default;
        }

        /// <summary>取控件的值描述：toggle->bool, slider->float, dropdown->选项文本, input->当前文本。</summary>
        public static object ValueOf(Control ctrl)
        {
            switch (ctrl.Type)
            {
                case "toggle":
                    return ((Toggle)ctrl.Component).isOn;
                case "slider":
                    return ((Slider)ctrl.Component).value;
                case "input":
                    if (ctrl.Component is InputField legacy) return legacy.text;
                    return ReadStringProp(ctrl.Component, "text");
                case "dropdown":
                    if (ctrl.Component is Dropdown dd)
                    {
                        if (dd.options != null && dd.value >= 0 && dd.value < dd.options.Count)
                            return dd.options[dd.value].text;
                        return dd.value;
                    }
                    return ReadIntProp(ctrl.Component, "value");
                default:
                    return null;
            }
        }

        public static bool IsInteractable(Control ctrl)
        {
            if (ctrl.Component is Selectable sel) return sel.interactable;
            var p = ctrl.Component.GetType().GetProperty("interactable");
            if (p != null && p.PropertyType == typeof(bool))
            {
                try { return (bool)p.GetValue(ctrl.Component); } catch { }
            }
            return true;
        }

        // ---------- 富文本清理 ----------
        private static readonly System.Text.RegularExpressions.Regex RichTextTag =
            new System.Text.RegularExpressions.Regex(@"<[^>]+>", System.Text.RegularExpressions.RegexOptions.Compiled);

        public static string StripRichText(string s)
        {
            if (s == null) return null;
            return RichTextTag.Replace(s, "").Trim();
        }

        // ---------- 反射小工具 ----------
        public static Component FindComponentByTypeName(GameObject go, params string[] names)
        {
            var components = go.GetComponents<Component>();
            foreach (var c in components)
            {
                if (c == null) continue;
                var n = c.GetType().Name;
                foreach (var want in names)
                    if (n == want) return c;
            }
            return null;
        }

        public static string ReadStringProp(object obj, string prop)
        {
            try
            {
                var p = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                return p?.GetValue(obj) as string;
            }
            catch { return null; }
        }

        public static int ReadIntProp(object obj, string prop)
        {
            try
            {
                var p = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                if (p != null) return Convert.ToInt32(p.GetValue(obj));
            }
            catch { }
            return -1;
        }
    }
}
