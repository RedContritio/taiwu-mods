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

        /// <summary>
        /// Generic INSTANCE-method invoker: locate an object (UI element + optional id/component/member path),
        /// call one of its instance methods (public or non-public) by name + in-arg count, and return the
        /// result plus any out/ref values. Args are JSON values (coerced to the parameter type) or an object
        /// reference <c>{"$ref": {element/id/component/member | type/member}}</c> resolved to a live object.
        /// </summary>
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

            var selected = SelectMethodByInArg(target.GetType(), method, args.Count, isStatic: false);
            if (selected == null) return Error("instance method not found (by name + in-arg count): " + method + "/" + args.Count);

            try
            {
                var result = BuildAndInvoke(selected, target, args, depth, maxMembers);
                result["element"] = element;
                result["id"] = id;
                result["component"] = comp.GetType().FullName;
                result["member"] = member ?? "";
                return result;
            }
            catch (TargetInvocationException ex)
            {
                return Error("invoke failed: " + (ex.InnerException ?? ex).GetType().Name + ": " + (ex.InnerException ?? ex).Message);
            }
            catch (Exception ex)
            {
                return Error("invoke failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Generic static-method invoker: resolve a type by full name across loaded assemblies, call one of
        /// its static methods (public or non-public), and return the result plus any out/ref argument values.
        /// Not tied to any UI element — diagnostic for any loaded assembly's static logic.
        /// </summary>
        public static Dictionary<string, object> InvokeStatic(string requestBody, int depth, int maxMembers)
        {
            var parsed = Json.Parse(requestBody) as IDictionary<string, object>;
            if (parsed == null) return Error("body must be an object");

            string typeName = GetString(parsed, "type");
            string method = GetString(parsed, "method");
            if (string.IsNullOrEmpty(typeName)) return Error("missing type");
            if (string.IsNullOrEmpty(method)) return Error("missing method");

            var type = ResolveType(typeName);
            if (type == null) return Error("type not found: " + typeName);

            var args = parsed.TryGetValue("args", out var rawArgs) && rawArgs is IList list
                ? list
                : (IList)new List<object>();

            var selected = SelectMethodByInArg(type, method, args.Count, isStatic: true);
            if (selected == null) return Error("static method not found (by name + in-arg count): " + method + "/" + args.Count);

            try
            {
                var result = BuildAndInvoke(selected, null, args, depth, maxMembers);
                result["type"] = type.FullName;
                return result;
            }
            catch (TargetInvocationException ex)
            {
                return Error("invoke failed: " + (ex.InnerException ?? ex).GetType().Name + ": " + (ex.InnerException ?? ex).Message);
            }
            catch (Exception ex)
            {
                return Error("invoke failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Generic WRITE: set a field or property. Instance via <c>element</c>/<c>id</c>/<c>component</c> +
        /// <c>member</c> (dotted path, supports array indices); static via <c>type</c> + <c>member</c>. The
        /// value is coerced to the member type (numbers, bool, enum, string, Vector2/3, Color/hex). Returns the
        /// re-read value. (Limitation: cannot write a field of a struct held by value, e.g. transform.position.x.)
        /// </summary>
        public static Dictionary<string, object> Set(string requestBody, int depth, int maxMembers)
        {
            var h = ResolveSetter(requestBody);
            if (!h.Ok) return Error(h.Error);
            h.Apply();
            return new Dictionary<string, object> { ["ok"] = true, ["set"] = h.Descr, ["value"] = Snapshot(h.Read(), Math.Max(0, depth), Math.Max(1, maxMembers)) };
        }

        /// <summary>A resolved write target. Used by /reflect/set (apply once) and /pin (apply every frame);
        /// container + member + coerced value are resolved a single time.</summary>
        internal sealed class SetterHandle
        {
            public bool Ok;
            public string Error;
            public string Descr;
            public Action Apply;
            public Func<object> Read;
        }

        /// <summary>Resolves a write target (instance via element/id/component/member, or static via type/member;
        /// dotted path with array indices and properties), coerces the value, and returns closures to apply/read.</summary>
        public static SetterHandle ResolveSetter(string requestBody)
        {
            var parsed = Json.Parse(requestBody) as IDictionary<string, object>;
            if (parsed == null) return SetFail("body must be an object");

            string member = GetString(parsed, "member");
            if (string.IsNullOrEmpty(member)) return SetFail("missing member");
            if (!parsed.TryGetValue("value", out var rawValue)) return SetFail("missing value");

            var segs = member.Split('.');
            string last = segs[segs.Length - 1].Trim();

            object container;
            Type type;
            string descr;

            string typeName = GetString(parsed, "type");
            if (!string.IsNullOrEmpty(typeName))
            {
                var ty = ResolveType(typeName);
                if (ty == null) return SetFail("type not found: " + typeName);
                descr = typeName + "." + member;
                if (segs.Length == 1) { container = null; type = ty; }
                else
                {
                    var root = GetStaticMember(ty, segs[0].Trim());
                    if (root == null) return SetFail("static member null or not found: " + segs[0]);
                    object cont = root;
                    if (segs.Length > 2)
                    {
                        var tr = Traverse(root, string.Join(".", segs, 1, segs.Length - 2));
                        if (!tr.Ok) return SetFail(tr.Error);
                        cont = tr.Value;
                    }
                    if (cont == null) return SetFail("container is null");
                    container = cont; type = cont.GetType();
                }
            }
            else
            {
                string element = GetString(parsed, "element");
                var located = Locate(element, GetString(parsed, "id") ?? "");
                if (!located.Ok) return SetFail(located.Error);
                var comp = FindComponent(located.Transform.gameObject, GetString(parsed, "component"));
                if (comp == null) return SetFail("component not found");
                descr = (element ?? "") + "." + member;
                object cont = comp;
                if (segs.Length > 1)
                {
                    var tr = Traverse(comp, string.Join(".", segs, 0, segs.Length - 1));
                    if (!tr.Ok) return SetFail(tr.Error);
                    cont = tr.Value;
                }
                if (cont == null) return SetFail("container is null");
                container = cont; type = cont.GetType();
            }

            var flags = (container == null ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.Public | BindingFlags.NonPublic;
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(last, flags | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    object coerced;
                    try { coerced = ConvertArg(rawValue, field.FieldType); }
                    catch (Exception ex) { return SetFail("value conversion failed: " + ex.Message); }
                    var c = container;
                    return new SetterHandle { Ok = true, Descr = descr, Apply = () => field.SetValue(c, coerced), Read = () => field.GetValue(c) };
                }
                var prop = t.GetProperty(last, flags | BindingFlags.DeclaredOnly);
                if (prop != null)
                {
                    if (!prop.CanWrite) return SetFail("property has no setter: " + last);
                    object coerced;
                    try { coerced = ConvertArg(rawValue, prop.PropertyType); }
                    catch (Exception ex) { return SetFail("value conversion failed: " + ex.Message); }
                    var c = container;
                    return new SetterHandle { Ok = true, Descr = descr, Apply = () => prop.SetValue(c, coerced, null), Read = () => prop.CanRead ? prop.GetValue(c, null) : coerced };
                }
            }
            return SetFail("member not found (field or property): " + type.FullName + "." + last);
        }

        private static SetterHandle SetFail(string error) => new SetterHandle { Ok = false, Error = error };

        private static Type ResolveType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = a.GetType(fullName); if (t != null) return t; } catch { }
            }
            return null;
        }

        private static MethodInfo SelectMethodByInArg(Type type, string method, int inArgCount, bool isStatic)
        {
            var flags = (isStatic ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.Public | BindingFlags.NonPublic;
            var candidates = new List<MethodInfo>();
            foreach (var m in type.GetMethods(flags))
            {
                if (!string.Equals(m.Name, method, StringComparison.Ordinal)) continue;
                if (m.ContainsGenericParameters) continue;
                int inCount = 0;
                foreach (var pp in m.GetParameters())
                    if (!pp.IsOut) inCount++;
                if (inCount == inArgCount) candidates.Add(m);
            }
            return candidates.Count == 1 ? candidates[0] : null;
        }

        /// <summary>Builds the full argument array (out → null, in → coerced value or resolved $ref), invokes,
        /// and returns returnValue + outArgs. Throws on conversion/invoke failure (callers wrap).</summary>
        private static Dictionary<string, object> BuildAndInvoke(MethodInfo selected, object target, IList args, int depth, int maxMembers)
        {
            var ps = selected.GetParameters();
            var full = new object[ps.Length];
            int ai = 0;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].IsOut) { full[i] = null; continue; }
                var pType = ps[i].ParameterType.IsByRef ? ps[i].ParameterType.GetElementType() : ps[i].ParameterType;
                full[i] = ConvertArgOrRef(ai < args.Count ? args[ai] : null, pType);
                ai++;
            }

            var ret = selected.Invoke(target, full);
            var outs = new List<object>();
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].IsOut || ps[i].ParameterType.IsByRef)
                    outs.Add(new Dictionary<string, object>
                    {
                        ["name"] = ps[i].Name,
                        ["value"] = Snapshot(full[i], Math.Max(0, depth), Math.Max(1, maxMembers)),
                    });
            }

            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["method"] = selected.Name,
                ["returnValue"] = Snapshot(ret, Math.Max(0, depth), Math.Max(1, maxMembers)),
                ["outArgs"] = outs,
            };
        }

        /// <summary>Coerces a JSON arg to the parameter type, or resolves an <c>{"$ref": {...}}</c> object
        /// reference to a live object (so methods taking object params can be called).</summary>
        private static object ConvertArgOrRef(object value, Type target)
        {
            if (value is IDictionary<string, object> d && d.TryGetValue("$ref", out var refSpec))
            {
                if (!(refSpec is IDictionary<string, object> spec)) throw new Exception("$ref must be an object");
                return ResolveRef(spec);
            }
            return ConvertArg(value, target);
        }

        private static object ResolveRef(IDictionary<string, object> spec)
        {
            string member = GetString(spec, "member");
            string typeName = GetString(spec, "type");
            if (!string.IsNullOrEmpty(typeName))
            {
                var type = ResolveType(typeName);
                if (type == null) throw new Exception("$ref type not found: " + typeName);
                if (string.IsNullOrEmpty(member)) throw new Exception("$ref(type) needs member");
                var segs = member.Split(new[] { '.' }, 2);
                object root = GetStaticMember(type, segs[0].Trim());
                if (segs.Length > 1)
                {
                    var tr = Traverse(root, segs[1]);
                    if (!tr.Ok) throw new Exception(tr.Error);
                    return tr.Value;
                }
                return root;
            }

            var located = Locate(GetString(spec, "element"), GetString(spec, "id") ?? "");
            if (!located.Ok) throw new Exception(located.Error);
            var comp = FindComponent(located.Transform.gameObject, GetString(spec, "component"));
            if (comp == null) throw new Exception("$ref component not found");
            if (string.IsNullOrEmpty(member)) return comp;
            var t2 = Traverse(comp, member);
            if (!t2.Ok) throw new Exception(t2.Error);
            return t2.Value;
        }

        private static object GetStaticMember(Type type, string name)
        {
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            for (var t = type; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (f != null) return f.GetValue(null);
                var p = t.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                if (p != null && p.CanRead) return p.GetValue(null, null);
            }
            return null;
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

                // Array / list element by integer index (e.g. "_catchPlaceList.5").
                if (cur is IList list && int.TryParse(name, out var idx))
                {
                    if (idx < 0 || idx >= list.Count) return TraverseResult.Fail("index out of range: " + idx);
                    cur = list[idx];
                    continue;
                }

                var type = cur.GetType();
                var field = FindField(type, name);
                if (field != null)
                {
                    cur = field.GetValue(cur);
                    continue;
                }
                var prop = FindProperty(type, name);
                if (prop != null && prop.CanRead)
                {
                    cur = prop.GetValue(cur, null);
                    continue;
                }
                return TraverseResult.Fail("member not found: " + type.FullName + "." + name);
            }
            return TraverseResult.Success(cur);
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var prop = t.GetProperty(name, InstanceFlags | BindingFlags.DeclaredOnly);
                if (prop != null) return prop;
            }
            return null;
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
            if (value is Color col)
                return new Dictionary<string, object> { ["r"] = col.r, ["g"] = col.g, ["b"] = col.b, ["a"] = col.a, ["hex"] = ColorUtility.ToHtmlStringRGBA(col) };
            if (value is Color32 c32)
                return new Dictionary<string, object> { ["r"] = c32.r, ["g"] = c32.g, ["b"] = c32.b, ["a"] = c32.a };
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
            if (target.IsInstanceOfType(value)) return value;
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
            if (target == typeof(Vector2)) return ToVector2(value);
            if (target == typeof(Vector3)) return ToVector3(value);
            if (target == typeof(Color)) return ToColor(value);
            if (target == typeof(Color32)) { Color c = ToColor(value); return (Color32)c; }
            return value;
        }

        private static float Num(object v) => v == null ? 0f : Convert.ToSingle(v);

        private static Vector2 ToVector2(object value)
        {
            if (value is IDictionary<string, object> m)
                return new Vector2(m.TryGetValue("x", out var x) ? Num(x) : 0f, m.TryGetValue("y", out var y) ? Num(y) : 0f);
            if (value is IList l && l.Count >= 2) return new Vector2(Num(l[0]), Num(l[1]));
            float n = Num(value); return new Vector2(n, n); // scalar → uniform
        }

        private static Vector3 ToVector3(object value)
        {
            if (value is IDictionary<string, object> m)
                return new Vector3(m.TryGetValue("x", out var x) ? Num(x) : 0f, m.TryGetValue("y", out var y) ? Num(y) : 0f, m.TryGetValue("z", out var z) ? Num(z) : 0f);
            if (value is IList l && l.Count >= 3) return new Vector3(Num(l[0]), Num(l[1]), Num(l[2]));
            float n = Num(value); return new Vector3(n, n, n);
        }

        private static Color ToColor(object value)
        {
            if (value is string s)
            {
                var hex = s.StartsWith("#") ? s : "#" + s;
                if (ColorUtility.TryParseHtmlString(hex, out var parsed)) return parsed;
                return Color.white;
            }
            if (value is IDictionary<string, object> m)
                return new Color(
                    m.TryGetValue("r", out var r) ? Num(r) : 0f,
                    m.TryGetValue("g", out var g) ? Num(g) : 0f,
                    m.TryGetValue("b", out var b) ? Num(b) : 0f,
                    m.TryGetValue("a", out var a) ? Num(a) : 1f);
            return Color.white;
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
