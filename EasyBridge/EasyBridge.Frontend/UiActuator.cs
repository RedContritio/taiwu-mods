using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EasyBridge.Frontend
{
    /// <summary>
    /// 在主线程对指定控件执行动作：click / toggle / set / select。
    /// 控件用 元素名 + id 路径 定位（id 与 UiInspector 输出一致）。
    /// </summary>
    internal static class UiActuator
    {
        public static System.Collections.Generic.Dictionary<string, object> Perform(
            string element, string id, string action, object rawValue)
        {
            UiElementCatalog.EnsureBuilt();

            if (string.IsNullOrEmpty(action))
                return Fail("missing 'action'");

            var elem = UiElementCatalog.ElementByName(element);
            if (elem == null) return Fail($"unknown element '{element}'");
            if (!UiElementCatalog.IsShowing(elem)) return Fail($"element '{element}' is not showing");

            var uiBase = UiElementCatalog.UiBaseOf(elem);
            if (uiBase == null || uiBase.gameObject == null || !uiBase.gameObject.activeInHierarchy)
                return Fail($"element '{element}' has no active root");

            var root = uiBase.transform;
            var target = string.IsNullOrEmpty(id) ? root : UiTree.ResolvePath(root, id);
            if (target == null) return Fail($"control id '{id}' not found in '{element}'");

            var ctrl = UiTree.DetectControl(target.gameObject);
            if (ctrl.Component == null)
                return Fail($"no interactable control at '{id}' (got GameObject '{target.name}')");

            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "click": return Click(element, id, ctrl);
                    case "toggle": return DoToggle(element, id, ctrl, rawValue);
                    case "set": return DoSet(element, id, ctrl, rawValue);
                    case "select": return DoSelect(element, id, ctrl, rawValue);
                    default: return Fail($"unknown action '{action}'");
                }
            }
            catch (Exception ex)
            {
                return Fail($"action threw: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static System.Collections.Generic.Dictionary<string, object> Click(string element, string id, UiTree.Control ctrl)
        {
            switch (ctrl.Type)
            {
                case "button":
                    var btn = (Button)ctrl.Component;
                    if (!btn.interactable) return Fail("button not interactable");
                    btn.onClick.Invoke();
                    return Ok(element, id, "click", null);
                case "toggle":
                    var tg = (Toggle)ctrl.Component;
                    SimulatePointerClick(tg.gameObject);
                    return Ok(element, id, "click", tg.isOn);
                default:
                    return Fail($"'click' not supported on '{ctrl.Type}'; use set/select/toggle");
            }
        }

        private static System.Collections.Generic.Dictionary<string, object> DoToggle(string element, string id, UiTree.Control ctrl, object rawValue)
        {
            if (ctrl.Type != "toggle") return Fail($"'toggle' requires a toggle, got '{ctrl.Type}'");
            var tg = (Toggle)ctrl.Component;
            if (TryBool(rawValue, out bool b))
            {
                if (tg.isOn != b)
                    SimulatePointerClick(tg.gameObject);
            }
            else
            {
                SimulatePointerClick(tg.gameObject);
            }
            return Ok(element, id, "toggle", tg.isOn);
        }

        private static System.Collections.Generic.Dictionary<string, object> DoSet(string element, string id, UiTree.Control ctrl, object rawValue)
        {
            switch (ctrl.Type)
            {
                case "input":
                    string text = rawValue?.ToString() ?? "";
                    if (ctrl.Component is InputField legacy)
                    {
                        legacy.text = text;
                        legacy.onValueChanged.Invoke(text);
                        legacy.onEndEdit.Invoke(text);
                    }
                    else
                    {
                        SetStringProp(ctrl.Component, "text", text);
                        InvokeUnityEvent(ctrl.Component, "onValueChanged", text);
                        InvokeUnityEvent(ctrl.Component, "onEndEdit", text);
                        InvokeUnityEvent(ctrl.Component, "onSubmit", text);
                    }
                    return Ok(element, id, "set", text);

                case "slider":
                    if (!TryFloat(rawValue, out float f)) return Fail("'set' on slider needs numeric value");
                    var sl = (Slider)ctrl.Component;
                    sl.value = f;
                    return Ok(element, id, "set", sl.value);

                case "toggle":
                    return DoToggle(element, id, ctrl, rawValue);

                case "dropdown":
                    return DoSelect(element, id, ctrl, rawValue);

                default:
                    return Fail($"'set' not supported on '{ctrl.Type}'");
            }
        }

        private static System.Collections.Generic.Dictionary<string, object> DoSelect(string element, string id, UiTree.Control ctrl, object rawValue)
        {
            if (ctrl.Type != "dropdown") return Fail($"'select' requires a dropdown, got '{ctrl.Type}'");
            if (!TryInt(rawValue, out int idx)) return Fail("'select' needs an integer option index");

            if (ctrl.Component is Dropdown dd)
            {
                dd.value = idx;
                dd.RefreshShownValue();
                dd.onValueChanged.Invoke(idx);
                return Ok(element, id, "select", idx);
            }
            // TMP_Dropdown 反射
            SetIntProp(ctrl.Component, "value", idx);
            var refresh = ctrl.Component.GetType().GetMethod("RefreshShownValue");
            refresh?.Invoke(ctrl.Component, null);
            InvokeUnityEventInt(ctrl.Component, "onValueChanged", idx);
            return Ok(element, id, "select", idx);
        }

        private static void SimulatePointerClick(GameObject go)
        {
            var evtData = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(go, evtData, ExecuteEvents.pointerClickHandler);
        }

        // ---------- 反射小工具 ----------
        private static void SetStringProp(object obj, string prop, string val)
        {
            var p = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            p?.SetValue(obj, val);
        }

        private static void SetIntProp(object obj, string prop, int val)
        {
            var p = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            p?.SetValue(obj, val);
        }

        private static void InvokeUnityEvent(object obj, string memberName, string arg)
        {
            var evt = GetMemberValue(obj, memberName);
            if (evt == null) return;
            var m = evt.GetType().GetMethod("Invoke", new[] { typeof(string) });
            m?.Invoke(evt, new object[] { arg });
        }

        private static void InvokeUnityEventInt(object obj, string memberName, int arg)
        {
            var evt = GetMemberValue(obj, memberName);
            if (evt == null) return;
            var m = evt.GetType().GetMethod("Invoke", new[] { typeof(int) });
            m?.Invoke(evt, new object[] { arg });
        }

        private static object GetMemberValue(object obj, string name)
        {
            var t = obj.GetType();
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) return f.GetValue(obj);
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return p?.GetValue(obj);
        }

        private static bool TryBool(object v, out bool b)
        {
            b = false;
            if (v is bool bb) { b = bb; return true; }
            if (v is double d) { b = d != 0; return true; }
            if (v is string s && bool.TryParse(s, out var p)) { b = p; return true; }
            return false;
        }

        private static bool TryFloat(object v, out float f)
        {
            f = 0;
            if (v is double d) { f = (float)d; return true; }
            if (v is float ff) { f = ff; return true; }
            if (v is string s && float.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p)) { f = p; return true; }
            return false;
        }

        private static bool TryInt(object v, out int i)
        {
            i = 0;
            if (v is double d) { i = (int)Math.Round(d); return true; }
            if (v is int ii) { i = ii; return true; }
            if (v is string s && int.TryParse(s, out var p)) { i = p; return true; }
            return false;
        }

        private static System.Collections.Generic.Dictionary<string, object> Ok(string element, string id, string action, object value)
        {
            var d = new System.Collections.Generic.Dictionary<string, object>
            {
                ["ok"] = true,
                ["element"] = element,
                ["id"] = id,
                ["action"] = action,
            };
            if (value != null) d["value"] = value;
            return d;
        }

        private static System.Collections.Generic.Dictionary<string, object> Fail(string msg)
            => new System.Collections.Generic.Dictionary<string, object> { ["ok"] = false, ["error"] = msg };
    }
}
