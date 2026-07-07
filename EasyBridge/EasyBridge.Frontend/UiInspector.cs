using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EasyBridge.Frontend
{
    /// <summary>
    /// 把当前活动 UI 转成语义化数据。所有方法都假定运行在 Unity 主线程上
    /// （由 MainThreadDispatcher 保证）。
    /// </summary>
    internal static class UiInspector
    {
        public struct Options
        {
            public string Detail;
            public bool Full;   // true=尽量完整；false=精简（默认）
            public int Max;     // 单窗口控件+文本上限
            public int ScanBudget;
            public string Section;
            public bool IncludeControls;
            public bool IncludeTexts;
        }

        private const int SnapshotScanBudget = 3000;

        // ---------- 全局快照（轻量，先看全貌再下钻）----------
        public static Dictionary<string, object> Snapshot()
        {
            UiElementCatalog.EnsureBuilt();
            var elements = new List<object>();

            var cur = UiElementCatalog.CurrentElements();
            if (cur != null)
            {
                foreach (var elem in cur)
                {
                    if (elem == null) continue;
                    if (!UiElementCatalog.IsShowing(elem)) continue;

                    var name = UiElementCatalog.NameOf(elem);
                    var root = RootOf(elem);
                    int controls = 0, texts = 0, budget = SnapshotScanBudget;
                    if (root != null) CountTree(root, ref controls, ref texts, ref budget);

                    elements.Add(new Dictionary<string, object>
                    {
                        ["name"] = name ?? "(unknown)",
                        ["layer"] = UiElementCatalog.LayerOf(elem),
                        ["fullScreen"] = UiElementCatalog.IsFullScreen(elem),
                        ["controls"] = controls,
                        ["texts"] = texts,
                    });
                }
            }

            return new Dictionary<string, object>
            {
                ["stackDepth"] = UiElementCatalog.StackDepth(),
                ["knownElements"] = UiElementCatalog.KnownElementCount,
                ["activeCount"] = elements.Count,
                ["elements"] = elements,
            };
        }

        // ---------- 单窗口语义树（按区域分组）----------
        public static Dictionary<string, object> Element(string name, Options opts)
        {
            UiElementCatalog.EnsureBuilt();
            var elem = UiElementCatalog.ElementByName(name);
            if (elem == null)
                return Error($"unknown element '{name}'");
            if (!UiElementCatalog.IsShowing(elem))
                return new Dictionary<string, object> { ["name"] = name, ["showing"] = false };

            var root = RootOf(elem);
            if (root == null)
                return new Dictionary<string, object> { ["name"] = name, ["showing"] = true, ["error"] = "no active root" };

            if (string.Equals(opts.Detail, "title", StringComparison.OrdinalIgnoreCase))
            {
                return new Dictionary<string, object>
                {
                    ["name"] = name,
                    ["layer"] = UiElementCatalog.LayerOf(elem),
                    ["fullScreen"] = UiElementCatalog.IsFullScreen(elem),
                    ["showing"] = true,
                    ["detail"] = "title",
                    ["title"] = FindTitle(root, Math.Max(100, Math.Min(opts.ScanBudget, 500))),
                };
            }

            if (!opts.Full)
            {
                return ElementSummary(name, elem, root, opts);
            }

            var controls = new List<object>();
            var texts = new List<object>();
            if (opts.ScanBudget <= 0) opts.ScanBudget = 1600;
            int scanned = 0;
            bool truncated = Collect(root, "", false, controls, texts, opts, ref scanned);

            if (!opts.IncludeControls) controls.Clear();
            if (!opts.IncludeTexts) texts.Clear();
            var sections = GroupIntoSections(controls, texts, opts.Section);

            return new Dictionary<string, object>
            {
                ["name"] = name,
                ["layer"] = UiElementCatalog.LayerOf(elem),
                ["fullScreen"] = UiElementCatalog.IsFullScreen(elem),
                ["showing"] = true,
                ["detail"] = "full",
                ["max"] = opts.Max,
                ["section"] = opts.Section ?? "",
                ["fields"] = FieldsLabel(opts),
                ["sections"] = sections,
                ["truncated"] = truncated,
                ["scanned"] = scanned,
            };
        }

        // ---------- 跨窗口文本搜索 ----------
        public static Dictionary<string, object> Find(string query, int limit, int scanBudget)
        {
            UiElementCatalog.EnsureBuilt();
            var hits = new List<object>();
            if (string.IsNullOrEmpty(query)) return new Dictionary<string, object> { ["query"] = query, ["hits"] = hits };
            bool truncated = false;
            int scannedTotal = 0;

            var cur = UiElementCatalog.CurrentElements();
            if (cur != null)
            {
                foreach (var elem in cur)
                {
                    if (elem == null || !UiElementCatalog.IsShowing(elem)) continue;
                    var root = RootOf(elem);
                    if (root == null) continue;
                    var name = UiElementCatalog.NameOf(elem) ?? "(unknown)";

                    var controls = new List<object>();
                    var texts = new List<object>();
                    int scanned = 0;
                    truncated |= Collect(root, "", false, controls, texts,
                        new Options { Full = true, Max = Math.Max(80, limit * 4), ScanBudget = scanBudget },
                        ref scanned);
                    scannedTotal += scanned;

                    foreach (Dictionary<string, object> c in controls)
                    {
                        if (Match(c, "label", query) || Match(c, "value", query))
                        {
                            c["element"] = name;
                            hits.Add(c);
                            if (hits.Count >= limit) goto done;
                        }
                    }
                    foreach (Dictionary<string, object> t in texts)
                    {
                        if (Match(t, "text", query))
                        {
                            t["element"] = name;
                            hits.Add(t);
                            if (hits.Count >= limit) goto done;
                        }
                    }
                }
            }
            done:
            return new Dictionary<string, object>
            {
                ["query"] = query,
                ["count"] = hits.Count,
                ["hits"] = hits,
                ["truncated"] = truncated,
                ["scanned"] = scannedTotal,
            };
        }

        // ---------- 分区 ----------
        private static Dictionary<string, object> ElementSummary(string name, object elem, Transform root, Options opts)
        {
            if (opts.ScanBudget <= 0) opts.ScanBudget = 900;
            int scanned = 0;
            string title = null;
            var summaries = new Dictionary<string, SectionSummary>(StringComparer.Ordinal);
            bool truncated = CollectSummary(root, "", false, summaries, opts, ref scanned, ref title);

            var sections = new List<object>();
            foreach (var entry in summaries.Values)
            {
                if (!string.IsNullOrEmpty(opts.Section) &&
                    !string.Equals(opts.Section, entry.Name, StringComparison.Ordinal))
                    continue;
                sections.Add(new Dictionary<string, object>
                {
                    ["section"] = entry.Name,
                    ["controls"] = entry.Controls,
                    ["texts"] = entry.Texts,
                });
            }

            int controls = 0, texts = 0;
            foreach (var entry in summaries.Values)
            {
                controls += entry.Controls;
                texts += entry.Texts;
            }

            return new Dictionary<string, object>
            {
                ["name"] = name,
                ["layer"] = UiElementCatalog.LayerOf(elem),
                ["fullScreen"] = UiElementCatalog.IsFullScreen(elem),
                ["showing"] = true,
                ["detail"] = "summary",
                ["title"] = title,
                ["counts"] = new Dictionary<string, object>
                {
                    ["controls"] = controls,
                    ["texts"] = texts,
                },
                ["sections"] = sections,
                ["truncated"] = truncated,
                ["scanned"] = scanned,
            };
        }

        private static List<object> GroupIntoSections(List<object> controls, List<object> texts, string onlySection)
        {
            var order = new List<string>();
            var secControls = new Dictionary<string, List<object>>(StringComparer.Ordinal);
            var secTexts = new Dictionary<string, List<object>>(StringComparer.Ordinal);

            foreach (Dictionary<string, object> c in controls)
            {
                var sec = SectionFromId(c["id"] as string);
                if (!string.IsNullOrEmpty(onlySection) && !string.Equals(onlySection, sec, StringComparison.Ordinal))
                    continue;
                if (!secControls.ContainsKey(sec)) { secControls[sec] = new List<object>(); order.Add(sec); }
                secControls[sec].Add(c);
            }
            foreach (Dictionary<string, object> t in texts)
            {
                var sec = SectionFromId(t["id"] as string);
                if (!string.IsNullOrEmpty(onlySection) && !string.Equals(onlySection, sec, StringComparison.Ordinal))
                    continue;
                if (!secTexts.ContainsKey(sec))
                {
                    secTexts[sec] = new List<object>();
                    if (!secControls.ContainsKey(sec)) order.Add(sec);
                }
                secTexts[sec].Add(t);
            }

            var sections = new List<object>();
            foreach (var sec in order)
            {
                var entry = new Dictionary<string, object> { ["section"] = sec };
                if (secControls.ContainsKey(sec)) entry["controls"] = secControls[sec];
                if (secTexts.ContainsKey(sec)) entry["texts"] = secTexts[sec];
                sections.Add(entry);
            }
            return sections;
        }

        private static string SectionFromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "(root)";
            var segments = id.Split('/');
            // 取第 2 段作为分区（跳过 Root@0 这类外层容器），取 @ 前的名字部分
            var seg = segments.Length > 1 ? segments[1] : segments[0];
            int at = seg.LastIndexOf('@');
            return at > 0 ? seg.Substring(0, at) : seg;
        }

        // ---------- 内部 ----------
        private static Transform RootOf(object element)
        {
            var uiBase = UiElementCatalog.UiBaseOf(element);
            if (uiBase == null) return null;
            var go = uiBase.gameObject;
            if (go == null || !go.activeInHierarchy) return null;
            return uiBase.transform;
        }

        /// <summary>返回 truncated。</summary>
        private static bool Collect(Transform t, string path, bool underControl,
            List<object> controls, List<object> texts, Options opts, ref int scanned)
        {
            if (controls.Count + texts.Count >= opts.Max) return true;
            if (scanned++ >= opts.ScanBudget) return true;

            var go = t.gameObject;
            var ctrl = UiTree.DetectControl(go);
            bool nowUnderControl = underControl;

            if (ctrl.Component != null)
            {
                var rawLabel = ctrl.Type == "button" ? UiTree.AllTexts(t) : UiTree.FirstText(t);
                var label = UiTree.StripRichText(rawLabel);
                var entry = new Dictionary<string, object>
                {
                    ["id"] = path.Length == 0 ? "" : path,
                    ["type"] = ctrl.Type,
                    ["label"] = label,
                };
                var val = UiTree.ValueOf(ctrl);
                if (val != null) entry["value"] = val;
                if (!UiTree.IsInteractable(ctrl)) entry["interactable"] = false;
                controls.Add(entry);
                nowUnderControl = true; // 其内部文本作为标签，不再单列
            }
            else if (!underControl)
            {
                var own = UiTree.GetOwnText(go);
                if (own != null)
                {
                    var cleaned = UiTree.StripRichText(own);
                    if (string.IsNullOrEmpty(cleaned)) cleaned = own;
                    // 精简模式：去掉与上一条完全相同的连续文本（常见于列表行）
                    if (opts.Full || texts.Count == 0 ||
                        !string.Equals((string)((Dictionary<string, object>)texts[texts.Count - 1])["text"], cleaned, StringComparison.Ordinal))
                    {
                        texts.Add(new Dictionary<string, object>
                        {
                            ["id"] = path.Length == 0 ? "" : path,
                            ["text"] = cleaned,
                        });
                    }
                }
            }

            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                var childPath = path.Length == 0 ? UiTree.SegmentOf(child) : path + "/" + UiTree.SegmentOf(child);
                if (Collect(child, childPath, nowUnderControl, controls, texts, opts, ref scanned))
                    return true;
            }
            return false;
        }

        private static bool CollectSummary(Transform t, string path, bool underControl,
            Dictionary<string, SectionSummary> sections, Options opts, ref int scanned, ref string title)
        {
            if (scanned++ >= opts.ScanBudget) return true;

            var go = t.gameObject;
            var ctrl = UiTree.DetectControl(go);
            bool nowUnderControl = underControl;
            string section = SectionFromId(path);
            var summary = GetSection(sections, section);

            if (ctrl.Component != null)
            {
                summary.Controls++;
                var rawLabel = ctrl.Type == "button" ? UiTree.AllTexts(t) : UiTree.FirstText(t);
                RememberTitle(go.name, rawLabel, ref title);
                nowUnderControl = true;
            }
            else if (!underControl)
            {
                var own = UiTree.GetOwnText(go);
                if (own != null)
                {
                    summary.Texts++;
                    RememberTitle(go.name, own, ref title);
                }
            }

            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                var childPath = path.Length == 0 ? UiTree.SegmentOf(child) : path + "/" + UiTree.SegmentOf(child);
                if (CollectSummary(child, childPath, nowUnderControl, sections, opts, ref scanned, ref title))
                    return true;
            }
            return false;
        }

        private static void CountTree(Transform t, ref int controls, ref int texts, ref int budget)
        {
            if (budget <= 0) return;
            budget--;
            var go = t.gameObject;
            var ctrl = UiTree.DetectControl(go);
            if (ctrl.Component != null) controls++;
            else if (UiTree.GetOwnText(go) != null) texts++;

            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                CountTree(child, ref controls, ref texts, ref budget);
                if (budget <= 0) return;
            }
        }

        private static bool Match(Dictionary<string, object> d, string key, string query)
        {
            if (!d.TryGetValue(key, out var v) || v == null) return false;
            var s = v as string ?? v.ToString();
            return s.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Dictionary<string, object> Error(string msg)
            => new Dictionary<string, object> { ["error"] = msg };

        private static string FindTitle(Transform root, int budget)
        {
            string title = null;
            string fallback = null;
            FindTitleRecursive(root, ref budget, ref title, ref fallback);
            return title ?? fallback;
        }

        private static bool FindTitleRecursive(Transform t, ref int budget, ref string title, ref string fallback)
        {
            if (budget-- <= 0) return true;
            var own = UiTree.GetOwnText(t.gameObject);
            if (own != null)
            {
                var cleaned = UiTree.StripRichText(own);
                if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length <= 80)
                {
                    if (fallback == null) fallback = cleaned;
                    if (LooksLikeTitle(t.gameObject.name))
                    {
                        title = cleaned;
                        return true;
                    }
                }
            }

            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                if (FindTitleRecursive(child, ref budget, ref title, ref fallback))
                    return true;
            }
            return false;
        }

        private static void RememberTitle(string objectName, string rawText, ref string title)
        {
            if (title != null || rawText == null) return;
            var cleaned = UiTree.StripRichText(rawText);
            if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Length > 80) return;
            if (LooksLikeTitle(objectName))
                title = cleaned;
        }

        private static bool LooksLikeTitle(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("tittle", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static SectionSummary GetSection(Dictionary<string, SectionSummary> sections, string name)
        {
            if (!sections.TryGetValue(name, out var entry))
            {
                entry = new SectionSummary { Name = name };
                sections[name] = entry;
            }
            return entry;
        }

        private static string FieldsLabel(Options opts)
        {
            if (opts.IncludeControls && opts.IncludeTexts) return "controls,texts";
            if (opts.IncludeControls) return "controls";
            if (opts.IncludeTexts) return "texts";
            return "";
        }

        private sealed class SectionSummary
        {
            public string Name;
            public int Controls;
            public int Texts;
        }
    }
}
