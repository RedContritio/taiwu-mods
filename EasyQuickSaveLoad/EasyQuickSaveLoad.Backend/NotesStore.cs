using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GameData.ArchiveData;
using GameData.Common;
using GameData.Domains.Global;

namespace EasyQuickSaveLoad.Backend
{
    /// <summary>
    /// Persists an optional per-slot 备注 (note/nickname) alongside the slot save files, in a small text
    /// file <c>local.sav.qsl.notes</c> in the archive data directory. Format: one line per note,
    /// <c>&lt;slot&gt;=&lt;base64(utf8 note)&gt;</c> — base64 avoids any delimiter/newline issues. The quick
    /// slot (-1) can carry a note too. Empty/whitespace notes are removed. All I/O is best-effort (never
    /// throws): a missing or corrupt file just yields no notes.
    /// </summary>
    internal static class NotesStore
    {
        private const string NotesFileName = "local.sav.qsl.notes";

        private static string NotesPath(sbyte archiveId)
        {
            return Path.Combine(Common.GetArchiveDataDirectory(archiveId), NotesFileName);
        }

        public static Dictionary<int, string> ReadAll(sbyte archiveId)
        {
            var map = new Dictionary<int, string>();
            try
            {
                string path = NotesPath(archiveId);
                if (!File.Exists(path)) return map;
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    if (!int.TryParse(line.Substring(0, eq), out int slot)) continue;
                    string note = Decode(line.Substring(eq + 1));
                    if (!string.IsNullOrEmpty(note)) map[slot] = note;
                }
            }
            catch { /* missing / corrupt notes file → no notes */ }
            return map;
        }

        public static void SetNote(sbyte archiveId, int slot, string note)
        {
            try
            {
                var map = ReadAll(archiveId);
                if (string.IsNullOrWhiteSpace(note)) map.Remove(slot);
                else map[slot] = note.Trim();
                WriteAll(archiveId, map);
            }
            catch { /* best effort */ }
        }

        public static void DeleteNote(sbyte archiveId, int slot)
        {
            SetNote(archiveId, slot, null);
        }

        /// <summary>Drop notes whose slot is outside [0, slotCount). Callers pass the ABSOLUTE max slot count
        /// (never the current page-limited count), so notes for temporarily-hidden pages are preserved, only
        /// truly-unreachable orphans are removed.</summary>
        public static void Prune(sbyte archiveId, int slotCount)
        {
            try
            {
                var map = ReadAll(archiveId);
                var stale = new List<int>();
                foreach (var slot in map.Keys)
                {
                    if (slot < 0 || slot >= slotCount) stale.Add(slot);
                }
                if (stale.Count == 0) return;
                foreach (var slot in stale) map.Remove(slot);
                WriteAll(archiveId, map);
            }
            catch { /* best effort */ }
        }

        /// <summary>Serialize all notes into one blob (<c>slot=base64\n…</c>) for return to the frontend.</summary>
        public static string SerializeAll(sbyte archiveId)
        {
            var map = ReadAll(archiveId);
            var sb = new StringBuilder();
            foreach (var kv in map)
            {
                sb.Append(kv.Key).Append('=').Append(Encode(kv.Value)).Append('\n');
            }
            return sb.ToString();
        }

        private static void WriteAll(sbyte archiveId, Dictionary<int, string> map)
        {
            SlotStore.EnsureDir(archiveId);
            var sb = new StringBuilder();
            foreach (var kv in map)
            {
                sb.Append(kv.Key).Append('=').Append(Encode(kv.Value)).Append('\n');
            }
            File.WriteAllText(NotesPath(archiveId), sb.ToString(), Encoding.UTF8);
        }

        private static string Encode(string s)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? string.Empty));
        }

        private static string Decode(string s)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
            catch { return string.Empty; }
        }
    }
}
