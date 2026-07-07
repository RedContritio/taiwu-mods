using System;
using System.Collections.Generic;
using System.Text;

namespace EasyBridge.Frontend
{
    /// <summary>
    /// In-memory ring buffer of recent bridge requests so the in-game monitor can show what every
    /// interaction did: the agent's note (a plain-language 说明 attached per call), source (前端/后端 pipe),
    /// method/path/input, status, an error preview on failure, and duration. Backend-pipe requests are
    /// forwarded in via <c>/monitor/push</c> so one panel shows BOTH pipes. Monitor polls/pushes (mon=1)
    /// are skipped by the caller. <see cref="RenderRecent"/> formats entries as Unity rich text, copying
    /// out of the lock first; <see cref="Snapshot"/> serves <c>/log</c> (full data incl. success results).
    /// </summary>
    internal static class RequestLog
    {
        internal sealed class Entry
        {
            public long Seq;
            public string Time, Source, Method, Path, Query, Body, Note, Result;
            public int Status;
            public long Ms;
        }

        private static readonly object _lock = new object();
        private static readonly List<Entry> _entries = new List<Entry>();
        private static long _seq;
        private const int Max = 500;

        public static readonly DateTime StartedUtc = DateTime.UtcNow;

        public static void Add(string source, string method, string path, string query, string body, string note, int status, string resultJson, long ms)
        {
            var e = new Entry
            {
                Source = source,
                Method = method,
                Path = path,
                Query = Preview(query, 90),
                Body = Preview(body, 160),
                Note = Preview(note, 140),
                Status = status,
                Result = Preview(resultJson, 200),
                Ms = ms,
            };
            lock (_lock)
            {
                e.Seq = ++_seq;
                e.Time = DateTime.Now.ToString("HH:mm:ss");
                _entries.Add(e);
                if (_entries.Count > Max) _entries.RemoveAt(0);
            }
        }

        public static void Clear() { lock (_lock) _entries.Clear(); }

        public static Dictionary<string, object> Snapshot(long since, int max)
        {
            if (since < 0) since = 0;
            max = max <= 0 ? 200 : Math.Min(max, Max);
            var list = new List<object>();
            long total;
            lock (_lock)
            {
                total = _seq;
                foreach (var e in _entries)
                {
                    if (e.Seq <= since) continue;
                    list.Add(new Dictionary<string, object>
                    {
                        ["seq"] = e.Seq, ["time"] = e.Time, ["source"] = e.Source,
                        ["method"] = e.Method, ["path"] = e.Path, ["query"] = e.Query, ["body"] = e.Body,
                        ["note"] = e.Note, ["status"] = e.Status, ["result"] = e.Result, ["ms"] = e.Ms,
                    });
                }
            }
            if (list.Count > max) list = list.GetRange(list.Count - max, max);
            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["total"] = total,
                ["uptimeSec"] = (long)(DateTime.UtcNow - StartedUtc).TotalSeconds,
                ["entries"] = list,
            };
        }

        /// <summary>Last <paramref name="n"/> entries as Unity rich text, NEWEST FIRST (top). Lock held only
        /// to copy entry references; the string is built outside it. Newest-first means that when the text
        /// overflows the panel and the bottom is clipped, it's the OLDEST rows that drop — the latest activity
        /// is always visible. Success result JSON is omitted as noise — only failures show their error;
        /// full results live on <c>/log</c>.</summary>
        public static string RenderRecent(int n)
        {
            Entry[] slice;
            lock (_lock)
            {
                int start = Math.Max(0, _entries.Count - n);
                slice = new Entry[_entries.Count - start];
                _entries.CopyTo(start, slice, 0, slice.Length);
            }
            if (slice.Length == 0)
                return "<color=#8a8068>还没有任何交互。agent 通过 bridge 操作时会逐条出现在这里，每条带一句说明。</color>";

            var sb = new StringBuilder();
            for (int i = slice.Length - 1; i >= 0; i--)
            {
                var e = slice[i];
                bool ok = e.Status >= 200 && e.Status < 300;

                // Top (primary) line: the API endpoint — source · method · path · status · timing.
                sb.Append(SourceTag(e.Source)).Append(' ')
                  .Append(MethodColor(e.Method)).Append(e.Method).Append("</color> ")
                  .Append("<color=#d9cfb8>").Append(Esc(e.Path));
                if (!string.IsNullOrEmpty(e.Query)) sb.Append('?').Append(Esc(e.Query));
                sb.Append("</color> ")
                  .Append(ok ? "<color=#8fcf6d>✓ " : "<color=#ff6f5e>✕ ").Append(e.Status).Append("</color>")
                  .Append(" <color=#9a917a>").Append(e.Time).Append(" · ").Append(e.Ms).Append("ms</color>\n");

                // Inner line: the human description.
                if (!string.IsNullOrEmpty(e.Note))
                    sb.Append("   <color=#e9c07a>▸ ").Append(Esc(Cut(e.Note, 80))).Append("</color>\n");
                else
                    sb.Append("   <color=#b5604a>▸ (无说明)</color>\n");

                // Inner line: error preview, only on failure.
                if (!ok && !string.IsNullOrEmpty(e.Result))
                    sb.Append("   <color=#e08a7a>↳ ").Append(Esc(Cut(e.Result, 120))).Append("</color>\n");
            }
            return sb.ToString();
        }

        private static string SourceTag(string s)
            => s == "backend" ? "<color=#e0896a>[state]</color>"
             : s == "frontend" ? "<color=#7cc4e8>[ui]</color>"
             : "<color=#d07458>[" + Esc(s ?? "?") + "]</color>";

        private static string MethodColor(string m)
            => m == "GET" ? "<color=#7cc4e8>" : m == "POST" ? "<color=#d8a657>" : "<color=#cabfa8>";

        private static string Esc(string s)
            => string.IsNullOrEmpty(s) ? s : s.Replace("<", "‹").Replace(">", "›");

        private static string Preview(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return s;
            int take = Math.Min(s.Length, n + 1);
            if (take > 0 && take < s.Length && char.IsHighSurrogate(s[take - 1])) take--;
            var sb = new StringBuilder(take + 1);
            for (int i = 0; i < take; i++)
            {
                char c = s[i];
                sb.Append(c == '\r' || c == '\n' ? ' ' : c);
            }
            if (s.Length > n)
            {
                if (sb.Length > n) sb.Length = n;
                if (sb.Length > 0 && char.IsHighSurrogate(sb[sb.Length - 1])) sb.Length--;
                sb.Append("…");
            }
            return sb.ToString();
        }

        private static string Cut(string s, int n)
            => string.IsNullOrEmpty(s) || s.Length <= n ? s : Safe(s, n) + "…";

        // Never split a UTF-16 surrogate pair (a lone surrogate breaks Text mesh / JSON).
        private static string Safe(string s, int n)
        {
            if (n > 0 && n < s.Length && char.IsHighSurrogate(s[n - 1])) n--;
            return s.Substring(0, n);
        }
    }
}
