using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EasyBridge.Frontend
{
    /// <summary>
    /// Generic frontend UI reflection helpers for diagnostics. All entry points
    /// assume they run on the Unity main thread.
    /// </summary>
    internal static class ReflectionInspector
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static Dictionary<string, object> Inspect(string element, string id, int maxMembers)
        {
            var located = Locate(element, id);
            if (!located.Ok) return Error(located.Error);

            var go = located.Transform.gameObject;
            var result = BaseObjectInfo(located, go);
            result["components"] = ComponentList(go, Math.Max(1, maxMembers));
            result["rect"] = RectInfo(located.Transform as RectTransform);
            return result;
        }

        public static Dictionary<string, object> Reflect(string element, string id, string component,
            string member, int depth, int maxMembers)
        {
            var located = Locate(element, id);
            if (!located.Ok) return Error(located.Error);

            var comp = FindComponent(located.Transform.gameObject, component);
            if (comp == null) return Error("component not found: " + component);

            object target = comp;
            if (!string.IsNullOrEmpty(member))
            {
                var traversed = Traverse(target, member);
                if (!traversed.Ok) return Error(traversed.Error);
                target = traversed.Value;
            }

            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["element"] = element,
                ["id"] = id ?? "",
                ["component"] = comp.GetType().FullName,
                ["member"] = member ?? "",
                ["value"] = Snapshot(target, Math.Max(0, depth), Math.Max(1, maxMembers)),
            };
        }

        public static Dictionary<string, object> Invoke(string requestBody, int depth, int maxMembers)
        {
            var parsed = Json.Parse(requestBody) as IDictionary<string, object>;
            if (parsed == null) return Error("body must be an object");

            string element = GetString(parsed, "element");
            string id = GetString(parsed, "id") ?? "";
            string component = GetString(parsed, "component");
            string member = GetString(parsed, "member");
            string method = GetString(parsed, "method");
            if (string.IsNullOrEmpty(method)) return Error("missing method");

            var located = Locate(element, id);
            if (!located.Ok) return Error(located.Error);

            var comp = FindComponent(located.Transform.gameObject, component);
            if (comp == null) return Error("component not found: " + component);

            object target = comp;
            if (!string.IsNullOrEmpty(member))
            {
                var traversed = Traverse(target, member);
                if (!traversed.Ok) return Error(traversed.Error);
                target = traversed.Value;
            }
            if (target == null) return Error("target member is null");

            var args = parsed.TryGetValue("args", out var rawArgs) && rawArgs is IList list
                ? list
                : (IList)new List<object>();

            var selected = SelectMethod(target.GetType(), method, args);
            if (selected == null) return Error("method not found: " + method + "/" + args.Count);

            object[] converted;
            try
            {
                converted = ConvertArgs(args, selected.GetParameters());
            }
            catch (Exception ex)
            {
                return Error("argument conversion failed: " + ex.Message);
            }

            try
            {
                var value = selected.Invoke(target, converted);
                return new Dictionary<string, object>
                {
                    ["ok"] = true,
                    ["element"] = element,
                    ["id"] = id,
                    ["component"] = comp.GetType().FullName,
                    ["member"] = member ?? "",
                    ["method"] = selected.Name,
                    ["value"] = Snapshot(value, Math.Max(0, depth), Math.Max(1, maxMembers)),
                };
            }
            catch (TargetInvocationException ex)
            {
                return Error("invoke failed: " + (ex.InnerException ?? ex).GetType().Name + ": " +
                             (ex.InnerException ?? ex).Message);
            }
            catch (Exception ex)
            {
                return Error("invoke failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static Located Locate(string element, string id)
        {
            if (string.IsNullOrEmpty(element))
                return Located.Fail("missing element");

            UiElementCatalog.EnsureBuilt();
            var elem = UiElementCatalog.ElementByName(element);
            if (elem == null) return Located.Fail("unknown element: " + element);

            var uiBase = UiElementCatalog.UiBaseOf(elem);
            if (uiBase == null || uiBase.gameObject == null)
                return Located.Fail("element has no active root: " + element);

            var root = uiBase.transform;
            var target = string.IsNullOrEmpty(id) ? root : UiTree.ResolvePath(root, id);
            if (target == null) return Located.Fail("id not found under element: " + id);

            return Located.Success(element, id ?? "", root, target);
        }

        private static Dictionary<string, object> BaseObjectInfo(Located located, GameObject go)
        {
            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["element"] = located.Element,
                ["id"] = located.Id,
                ["name"] = go.name,
                ["activeSelf"] = go.activeSelf,
                ["activeInHierarchy"] = go.activeInHierarchy,
                ["path"] = BuildPath(located.Root, located.Transform),
            };
        }

        private static List<object> ComponentList(GameObject go, int max)
        {
            var list = new List<object>();
            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length && list.Count < max; i++)
            {
                var c = components[i];
                if (c == null)
                {
                    list.Add(new Dictionary<string, object> { ["index"] = i, ["missing"] = true });
                    continue;
                }
                list.Add(new Dictionary<string, object>
                {
                    ["index"] = i,
                    ["type"] = c.GetType().FullName,
                    ["name"] = c.GetType().Name,
                    ["enabled"] = EnabledOf(c),
                });
            }
            return list;
        }

        private static Dictionary<string, object> RectInfo(RectTransform rect)
        {
            if (rect == null) return null;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Camera camera = null;
            try { camera = UIManager.Instance != null ? UIManager.Instance.UiCamera : null; } catch { }

            var screen = new List<object>();
            foreach (var corner in corners)
            {
                var p = RectTransformUtility.WorldToScreenPoint(camera, corner);
                screen.Add(new Dictionary<string, object> { ["x"] = p.x, ["y"] = p.y });
            }

            return new Dictionary<string, object>
            {
                ["anchoredX"] = rect.anchoredPosition.x,
                ["anchoredY"] = rect.anchoredPosition.y,
                ["width"] = rect.rect.width,
                ["height"] = rect.rect.height,
                ["screenCorners"] = screen,
                ["eventCamera"] = camera != null ? camera.name : null,
            };
        }

        private static Component FindComponent(GameObject go, string component)
        {
            var components = go.GetComponents<Component>();
            if (string.IsNullOrEmpty(component))
                return components.Length > 0 ? components[0] : null;

            foreach (var c in components)
            {
                if (c == null) continue;
                var t = c.GetType();
                if (string.Equals(t.FullName, component, StringComparison.Ordinal) ||
                    string.Equals(t.Name, component, StringComparison.Ordinal) ||
                    t.FullName.EndsWith("." + component, StringComparison.Ordinal))
                    return c;
            }
            return null;
        }

        private static TraverseResult Traverse(object start, string path)
        {
            object cur = start;
            foreach (var raw in path.Split('.'))
            {
                var name = raw.Trim();
                if (name.Length == 0) continue;
                if (cur == null) return TraverseResult.Fail("null before member: " + name);

                var type = cur.GetType();
                var field = FindField(type, name);
                if (field != null)
                {
                    cur = field.GetValue(cur);
                    continue;
                }
                return TraverseResult.Fail("member not found: " + type.FullName + "." + name);
            }
            return TraverseResult.Success(cur);
        }

        private static object Snapshot(object value, int depth, int maxMembers)
        {
            if (value == null) return null;
            var type = value.GetType();

            if (IsSimple(type)) return value;
            if (type.IsEnum) return value.ToString();

            if (value is Vector2 v2)
                return new Dictionary<string, object> { ["x"] = v2.x, ["y"] = v2.y };
            if (value is Vector3 v3)
                return new Dictionary<string, object> { ["x"] = v3.x, ["y"] = v3.y, ["z"] = v3.z };
            if (value is Rect r)
                return new Dictionary<string, object> { ["x"] = r.x, ["y"] = r.y, ["width"] = r.width, ["height"] = r.height };
            if (value is UnityEngine.Object obj)
            {
                bool unityNull;
                try { unityNull = obj == null; } catch { unityNull = true; }

                var map = new Dictionary<string, object>
                {
                    ["type"] = type.FullName,
                    ["name"] = unityNull ? null : SafeUnityName(obj),
                    ["unityNull"] = unityNull,
                };
                if (!unityNull && value is Component c)
                {
                    map["gameObject"] = SafeGameObjectName(c);
                    map["enabled"] = EnabledOf(c);
                    if (c is RectTransform rt) map["rect"] = RectInfo(rt);
                }
                if (!unityNull && value is GameObject go)
                {
                    map["activeSelf"] = go.activeSelf;
                    map["activeInHierarchy"] = go.activeInHierarchy;
                }
                if (depth > 0) map["members"] = MemberSnapshot(value, depth - 1, maxMembers);
                return map;
            }

            if (value is IEnumerable seq && !(value is string))
            {
                var list = new List<object>();
                foreach (var item in seq)
                {
                    if (list.Count >= maxMembers) break;
                    list.Add(Snapshot(item, depth - 1, maxMembers));
                }
                return list;
            }

            var result = new Dictionary<string, object>
            {
                ["type"] = type.FullName,
                ["string"] = value.ToString(),
            };
            if (depth > 0) result["members"] = MemberSnapshot(value, depth - 1, maxMembers);
            return result;
        }

        private static List<object> MemberSnapshot(object value, int depth, int maxMembers)
        {
            var list = new List<object>();
            var type = value.GetType();

            foreach (var field in type.GetFields(InstanceFlags))
            {
                if (list.Count >= maxMembers) break;
                object fieldValue;
                try { fieldValue = field.GetValue(value); }
                catch (Exception ex) { fieldValue = "<" + ex.GetType().Name + ">"; }
                list.Add(new Dictionary<string, object>
                {
                    ["kind"] = "field",
                    ["name"] = field.Name,
                    ["type"] = field.FieldType.FullName,
                    ["value"] = Snapshot(fieldValue, depth, maxMembers),
                });
            }

            return list;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(name, InstanceFlags | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }

        private static MethodInfo SelectMethod(Type type, string method, IList args)
        {
            var candidates = new List<MethodInfo>();
            foreach (var m in type.GetMethods(InstanceFlags))
            {
                if (!string.Equals(m.Name, method, StringComparison.Ordinal)) continue;
                if (m.ContainsGenericParameters) continue;
                var parameters = m.GetParameters();
                if (parameters.Length != args.Count) continue;
                bool canUse = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType.IsByRef || parameters[i].IsOut ||
                        !CanConvertArg(args[i], parameters[i].ParameterType))
                    {
                        canUse = false;
                        break;
                    }
                }
                if (canUse) candidates.Add(m);
            }
            return candidates.Count == 1 ? candidates[0] : null;
        }

        private static object[] ConvertArgs(IList args, ParameterInfo[] parameters)
        {
            var converted = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                converted[i] = ConvertArg(args[i], parameters[i].ParameterType);
            return converted;
        }

        private static object ConvertArg(object value, Type target)
        {
            if (value == null) return null;
            if (target == typeof(string)) return value as string ?? value.ToString();
            if (target == typeof(bool)) return ToBool(value);
            if (target.IsEnum) return Enum.Parse(target, value.ToString());
            if (target == typeof(int)) return Convert.ToInt32(value);
            if (target == typeof(short)) return Convert.ToInt16(value);
            if (target == typeof(long)) return Convert.ToInt64(value);
            if (target == typeof(float)) return Convert.ToSingle(value);
            if (target == typeof(double)) return Convert.ToDouble(value);
            if (target == typeof(byte)) return Convert.ToByte(value);
            if (target == typeof(sbyte)) return Convert.ToSByte(value);
            return value;
        }

        private static bool CanConvertArg(object value, Type target)
        {
            if (value == null) return !target.IsValueType || Nullable.GetUnderlyingType(target) != null;
            if (target.IsInstanceOfType(value)) return true;
            if (target == typeof(string) || target == typeof(bool) || target.IsEnum) return true;
            return target == typeof(int) || target == typeof(short) || target == typeof(long) ||
                   target == typeof(float) || target == typeof(double) || target == typeof(byte) ||
                   target == typeof(sbyte);
        }

        private static bool ToBool(object value)
        {
            if (value is bool b) return b;
            if (value is double d) return Math.Abs(d) > double.Epsilon;
            return bool.Parse(value.ToString());
        }

        private static bool IsSimple(Type type)
        {
            return type == typeof(string) || type == typeof(bool) ||
                   type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal);
        }

        private static object EnabledOf(Component c)
        {
            if (c is Behaviour b) return b.enabled;
            return null;
        }

        private static object SafeUnityName(UnityEngine.Object obj)
        {
            try { return obj.name; }
            catch (Exception ex) { return "<" + ex.GetType().Name + ">"; }
        }

        private static object SafeGameObjectName(Component component)
        {
            try
            {
                var go = component.gameObject;
                return go != null ? go.name : null;
            }
            catch (Exception ex)
            {
                return "<" + ex.GetType().Name + ">";
            }
        }

        private static string BuildPath(Transform root, Transform target)
        {
            if (root == null || target == null) return "";
            if (root == target) return "";
            var parts = new List<string>();
            var cur = target;
            while (cur != null && cur != root)
            {
                parts.Add(UiTree.SegmentOf(cur));
                cur = cur.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static string GetString(IDictionary<string, object> map, string key)
        {
            return map.TryGetValue(key, out var value) && value != null
                ? value as string ?? value.ToString()
                : null;
        }

        private static Dictionary<string, object> Error(string message)
            => new Dictionary<string, object> { ["ok"] = false, ["error"] = message };

        private struct Located
        {
            public bool Ok;
            public string Error;
            public string Element;
            public string Id;
            public Transform Root;
            public Transform Transform;

            public static Located Success(string element, string id, Transform root, Transform transform)
                => new Located { Ok = true, Element = element, Id = id, Root = root, Transform = transform };

            public static Located Fail(string error)
                => new Located { Ok = false, Error = error };
        }

        private struct TraverseResult
        {
            public bool Ok;
            public string Error;
            public object Value;

            public static TraverseResult Success(object value)
                => new TraverseResult { Ok = true, Value = value };

            public static TraverseResult Fail(string error)
                => new TraverseResult { Ok = false, Error = error };
        }
    }
}
