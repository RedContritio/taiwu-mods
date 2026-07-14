using System;
using System.Reflection;
using GameData.Common;
using GameData.ArchiveData;
using GameData.Domains;
using GameData.Domains.Global;
using GameData.Domains.Mod;
using TaiwuModdingLib.Core.Plugin;

namespace EasyQuickSaveLoad.Backend
{
    [PluginConfig("EasyQuickSaveLoad", "RedContritio", "1.0.1.0")]
    public sealed class BackendPlugin : TaiwuRemakePlugin
    {
        private const string SaveToSlotMethod = "SaveToSlot";
        private const string LoadFromSlotMethod = "LoadFromSlot";
        private const string DeleteSlotMethod = "DeleteSlot";
        private const string ListSlotsMethod = "ListSlots";
        private const string SetSlotNoteMethod = "SetSlotNote";

        private const string SlotKey = "Slot";
        private const string NoteKey = "Note";
        private const string OkKey = "Ok";
        private const string QuickInfoKey = "QuickInfo";
        private const string SlotInfosKey = "SlotInfos";
        private const string SlotCountKey = "SlotCount";
        private const string SlotNotesKey = "SlotNotes";
        private const string PageCountSetting = "PageCount";
        private const sbyte ArchiveStatusGood = 1;

        public override void Initialize()
        {
            TryRegister(() => DomainManager.Mod.AddModMethod(ModIdStr, SaveToSlotMethod, SaveToSlot));
            TryRegister(() => DomainManager.Mod.AddModMethod(ModIdStr, LoadFromSlotMethod, LoadFromSlot));
            TryRegister(() => DomainManager.Mod.AddModMethod(ModIdStr, DeleteSlotMethod, DeleteSlot));
            TryRegister(() => DomainManager.Mod.AddModMethod(ModIdStr, ListSlotsMethod, ListSlots));
            TryRegister(() => DomainManager.Mod.AddModMethod(ModIdStr, SetSlotNoteMethod, SetSlotNote));
        }

        public override void Dispose()
        {
        }

        private static void TryRegister(Action register)
        {
            try { register(); }
            catch { /* 开发热重载时可能已注册 */ }
        }

        // 当前可操作栏位数 = 可用页数(1..10) × 每页 5。超出的旧存档暂时隐藏(不删)。
        private int GetSlotCount()
        {
            int pages = SlotStore.DefaultPageCount;
            DomainManager.Mod.GetSetting(ModIdStr, PageCountSetting, ref pages);
            if (pages < 1) pages = 1;
            if (pages > SlotStore.MaxPageCount) pages = SlotStore.MaxPageCount;
            return pages * SlotStore.SlotsPerPage;
        }

        // --- Save ---

        private void SaveToSlot(DataContext context, SerializableModData parameter)
        {
            int slot = ReadSlot(parameter);
            sbyte archiveId = Common.GetCurrArchiveId();
            SlotStore.EnsureDir(archiveId);
            string path = SlotStore.GetSlotPath(archiveId, slot);
            // Notes are per-save: a fresh save (incl. an overwrite) starts note-less, so the previous
            // occupant's 备注 never mislabels the new save. The player can rename it afterwards.
            NotesStore.DeleteNote(archiveId, slot);
            // completeNextFrame:true → 存独立文件、不占用原生 .bak
            InvokeGlobalPrivate("SaveWorldAt",
                new[] { typeof(DataContext), typeof(string), typeof(bool) },
                context, path, true);
        }

        // --- List ---

        private SerializableModData ListSlots(DataContext context, SerializableModData parameter)
        {
            sbyte archiveId = Common.GetCurrArchiveId();
            int slotCount = GetSlotCount();
            // 只清永不可达(≥MaxSlotCount=50)的孤儿备注；调小页数被临时隐藏的靠后页备注绝不删——
            // 数据保留在磁盘，调大页数即恢复。
            NotesStore.Prune(archiveId, SlotStore.MaxSlotCount);

            // BackupWorldsInfo MUST be non-null: ArchiveInfo.GetSerializedSize/Serialize dereference
            // BackupWorldsInfo.Count with no null guard, so a null list NREs (and crashes the backend)
            // when this result is serialized back to the frontend. Give the quick-slot info an empty list.
            var quick = new ArchiveInfo
            {
                Status = ArchiveStatusGood,
                WorldInfo = SlotStore.TryReadWorldInfo(archiveId, SlotStore.QuickSlot),
                BackupWorldsInfo = new System.Collections.Generic.List<(long timestamp, WorldInfo worldInfo)>()
            };

            var slots = new ArchiveInfo
            {
                Status = ArchiveStatusGood,
                BackupWorldsInfo = new System.Collections.Generic.List<(long timestamp, WorldInfo worldInfo)>()
            };
            for (int i = 0; i < slotCount; i++)
            {
                // timestamp 字段复用为槽号 i；worldInfo 为该槽摘要或 null（空栏）
                slots.BackupWorldsInfo.Add((i, SlotStore.TryReadWorldInfo(archiveId, i)));
            }

            var result = new SerializableModData();
            result.Set(OkKey, true);
            result.Set(SlotCountKey, slotCount);
            result.Set(QuickInfoKey, quick);
            result.Set(SlotInfosKey, slots);
            result.Set(SlotNotesKey, NotesStore.SerializeAll(archiveId));
            return result;
        }

        // --- Note ---

        private void SetSlotNote(DataContext context, SerializableModData parameter)
        {
            int slot = ReadSlot(parameter);
            string note = parameter != null && parameter.Get(NoteKey, out string n) ? n : null;
            NotesStore.SetNote(Common.GetCurrArchiveId(), slot, note);
        }

        // --- Delete ---

        private void DeleteSlot(DataContext context, SerializableModData parameter)
        {
            int slot = ReadSlot(parameter);
            sbyte archiveId = Common.GetCurrArchiveId();
            SlotStore.DeleteSlotFile(archiveId, slot);
            // Clear the 备注 too, so an emptied slot doesn't resurrect a stale note on the next save.
            NotesStore.DeleteNote(archiveId, slot);
        }

        // --- Load ---

        private void LoadFromSlot(DataContext context, SerializableModData parameter)
        {
            int slot = ReadSlot(parameter);
            sbyte archiveId = Common.GetCurrArchiveId();
            string path = SlotStore.GetSlotPath(archiveId, slot);
            if (!System.IO.File.Exists(path))
            {
                throw new System.IO.FileNotFoundException("Slot save file not found.", path);
            }

            // 与已验证的加载序列一致（顺序修复保留）：
            if (Common.IsInWorld())
            {
                DomainManager.Global.PackAllCrossArchiveGameData();
                DomainManager.Global.LeaveWorld();
            }
            InvokeGlobalPrivate("SetLoadedAllArchiveData", new[] { typeof(bool), typeof(DataContext) }, false, null);
            InvokeGlobalPrivate("LoadWorldAt", new[] { typeof(DataContext), typeof(string) }, context, path);
            Common.SetArchiveId(archiveId);
            AddInitAchievementsHandler();
            GameData.GameDataBridge.GameDataBridge.StartNextFrame(
                () => InvokeGlobalPrivate("SetCurrGameWorldType", new[] { typeof(sbyte), typeof(DataContext) }, (sbyte)1, null));
        }

        // --- helpers ---

        private static int ReadSlot(SerializableModData parameter)
        {
            if (parameter == null || !parameter.Get(SlotKey, out int slot))
            {
                throw new ArgumentException("Missing slot.");
            }
            return slot;
        }

        private static void AddInitAchievementsHandler()
        {
            MethodInfo method = DomainManager.Global.GetType().GetMethod(
                "InitAchievements", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(DomainManager.Global.GetType().FullName, "InitAchievements");
            }
            DataModificationHandler handler = (DataModificationHandler)Delegate.CreateDelegate(
                typeof(DataModificationHandler), DomainManager.Global, method);
            DomainManager.Global.AddPostModificationHandler(new DataUid(0, 1, ulong.MaxValue), "InitAchievements", handler);
        }

        private static void InvokeGlobalPrivate(string methodName, Type[] parameterTypes, params object[] args)
        {
            MethodInfo method = DomainManager.Global.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic, null, parameterTypes, null);
            if (method == null)
            {
                throw new MissingMethodException(DomainManager.Global.GetType().FullName, methodName);
            }
            try { method.Invoke(DomainManager.Global, args); }
            catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        }
    }
}
