using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Bridge.Frontend
{
    /// <summary>
    /// 维护游戏语义化窗口（UIElement 静态实例）与名字的双向映射，
    /// 并封装对 UIManager 私有字段的访问。
    ///
    /// 这里使用受控反射：UIManager._curElements / _uiStack 是 private，
    /// 按本仓库 skill「原生路径不足时反射作为最后手段」处理；范围仅限固定的
    /// 几个字段，并缓存 FieldInfo，避免每帧反射开销。
    /// 若后续需要更稳定，可改用 Publicizer 生成 Assembly-CSharp_public.dll。
    /// </summary>
    internal static class UiElementCatalog
    {
        private static readonly object _lock = new object();
        private static bool _built;

        private static readonly Dictionary<object, string> _nameByElem =
            new Dictionary<object, string>(ReferenceComparer.Instance);
        private static readonly Dictionary<string, object> _elemByName =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private static Type _uiElementType;
        private static Type _uiManagerType;
        private static PropertyInfo _uiManagerInstance;
        private static FieldInfo _curElementsField;
        private static FieldInfo _uiStackField;

        private static PropertyInfo _isShowingProp;
        private static PropertyInfo _isFullScreenProp;
        private static PropertyInfo _existProp;
        private static FieldInfo _uiBaseField;
        private static PropertyInfo _uiTypeProp;

        public static void EnsureBuilt()
        {
            if (_built) return;
            lock (_lock)
            {
                if (_built) return;
                Build();
                _built = true;
            }
        }

        private static void Build()
        {
            _uiElementType = AccessTools.TypeByName("UIElement");
            _uiManagerType = AccessTools.TypeByName("UIManager");

            if (_uiElementType != null)
            {
                _isShowingProp = AccessTools.Property(_uiElementType, "IsShowing");
                _isFullScreenProp = AccessTools.Property(_uiElementType, "IsFullScreen");
                _existProp = AccessTools.Property(_uiElementType, "Exist");
                _uiBaseField = AccessTools.Field(_uiElementType, "UiBase");

                // 扫描定义 UIElement 的程序集里所有 UIElement 类型的 static 字段实例。
                var asm = _uiElementType.Assembly;
                foreach (var t in SafeGetTypes(asm))
                {
                    FieldInfo[] fields;
                    try { fields = t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
                    catch { continue; }

                    foreach (var f in fields)
                    {
                        if (f.FieldType != _uiElementType) continue;
                        object inst;
                        try { inst = f.GetValue(null); }
                        catch { continue; }
                        if (inst == null) continue;
                        if (!_nameByElem.ContainsKey(inst))
                        {
                            _nameByElem[inst] = f.Name;
                            if (!_elemByName.ContainsKey(f.Name))
                                _elemByName[f.Name] = inst;
                        }
                    }
                }
            }

            if (_uiBaseField != null && _uiBaseField.FieldType != null)
                _uiTypeProp = AccessTools.Property(_uiBaseField.FieldType, "UiType")
                              ?? AccessTools.Property(_uiBaseField.FieldType.BaseType, "UiType");

            if (_uiManagerType != null)
            {
                _uiManagerInstance = AccessTools.Property(_uiManagerType, "Instance");
                _curElementsField = AccessTools.Field(_uiManagerType, "_curElements");
                _uiStackField = AccessTools.Field(_uiManagerType, "_uiStack");
            }
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                var list = new List<Type>();
                foreach (var t in ex.Types) if (t != null) list.Add(t);
                return list;
            }
            catch { return Array.Empty<Type>(); }
        }

        public static int KnownElementCount => _nameByElem.Count;

        public static string NameOf(object element)
        {
            if (element == null) return null;
            return _nameByElem.TryGetValue(element, out var n) ? n : null;
        }

        public static object ElementByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return _elemByName.TryGetValue(name, out var e) ? e : null;
        }

        public static IEnumerable<KeyValuePair<string, object>> AllElements()
        {
            return _elemByName;
        }

        // ---------- UIManager 状态 ----------
        public static object UiManagerInstance()
        {
            return _uiManagerInstance?.GetValue(null);
        }

        /// <summary>当前活动的 UIElement 列表（已转为 object 序列）。</summary>
        public static IList CurrentElements()
        {
            var mgr = UiManagerInstance();
            if (mgr == null || _curElementsField == null) return null;
            return _curElementsField.GetValue(mgr) as IList;
        }

        public static int StackDepth()
        {
            var mgr = UiManagerInstance();
            if (mgr == null || _uiStackField == null) return 0;
            var stack = _uiStackField.GetValue(mgr) as ICollection;
            return stack?.Count ?? 0;
        }

        // ---------- UIElement 读取 ----------
        public static bool IsShowing(object element)
        {
            try { return element != null && _isShowingProp != null && (bool)_isShowingProp.GetValue(element); }
            catch { return false; }
        }

        public static bool IsFullScreen(object element)
        {
            try { return element != null && _isFullScreenProp != null && (bool)_isFullScreenProp.GetValue(element); }
            catch { return false; }
        }

        public static bool Exist(object element)
        {
            try { return element != null && _existProp != null && (bool)_existProp.GetValue(element); }
            catch { return false; }
        }

        /// <summary>返回 UIElement.UiBase 这个 MonoBehaviour（作为 UnityEngine.Component）。</summary>
        public static UnityEngine.Component UiBaseOf(object element)
        {
            if (element == null || _uiBaseField == null) return null;
            return _uiBaseField.GetValue(element) as UnityEngine.Component;
        }

        /// <summary>UI 所在层（UILayer 枚举名，如 LayerMain/LayerPopUp）。</summary>
        public static string LayerOf(object element)
        {
            try
            {
                var uiBase = UiBaseOf(element);
                if (uiBase == null || _uiTypeProp == null) return null;
                var layer = _uiTypeProp.GetValue(uiBase);
                return layer?.ToString();
            }
            catch { return null; }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);
            int IEqualityComparer<object>.GetHashCode(object obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
