using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameData.Common;
using GameData.ArchiveData;
using GameData.Domains;
using GameData.Domains.Global;
using GameData.Domains.Mod;
using TaiwuModdingLib.Core.Plugin;

namespace EasyQuickSaveLoad.Backend
{
    [PluginConfig("EasyQuickSaveLoad", "RedContritio", "1.0.0.0")]
    public sealed class BackendPlugin : TaiwuRemakePlugin
    {
        private const string SaveWorldWithBackupMethod = "SaveWorldWithBackup";
        private const string ListManualSavesMethod = "ListManualSaves";
        private const string LoadManualSaveMethod = "LoadManualSave";
        private const string ManualSaveFilePrefix = "local.sav.qsl.";
        private const string SlotKey = "Slot";
        private const string TimestampKey = "Timestamp";
        private const string OkKey = "Ok";
        private const string CountKey = "Count";
        private const string CapacityKey = "Capacity";
        private const string ArchiveInfoKey = "ArchiveInfo";
        private const sbyte ArchiveStatusGood = 1;
        private const int MinimumSavePointRetentionCount = 1;
        private const int SavePointBackupWaitFrames = 600;

        public override void Initialize()
        {
            TryRegisterModMethod(() => DomainManager.Mod.AddModMethod(ModIdStr, SaveWorldWithBackupMethod, SaveWorldWithBackup));
            TryRegisterModMethod(() => DomainManager.Mod.AddModMethod(ModIdStr, ListManualSavesMethod, ListManualSaves));
            TryRegisterModMethod(() => DomainManager.Mod.AddModMethod(ModIdStr, LoadManualSaveMethod, LoadManualSave));
        }

        private static void TryRegisterModMethod(Action register)
        {
            try
            {
                register();
            }
            catch
            {
                // The method may already be registered during development reloads.
            }
        }

        public override void Dispose()
        {
        }

        private static void SaveWorldWithBackup(DataContext context)
        {
            sbyte archiveId = Common.GetCurrArchiveId();
            long timestamp = CreateManualSaveTimestamp();
            string archiveDataPath = GetManualSavePath(archiveId, timestamp);
            SaveWorldWithoutAutomaticBackup(context, archiveDataPath);
            GameData.GameDataBridge.GameDataBridge.StartNextFrame(
                () => CleanupManualSavesWhenSaveFinished(archiveId, SavePointBackupWaitFrames));
        }

        private static void SaveWorldWithoutAutomaticBackup(DataContext context, string archiveDataPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(archiveDataPath));
            InvokeGlobalPrivate(
                "SaveWorldAt",
                new[] { typeof(DataContext), typeof(string), typeof(bool) },
                context,
                archiveDataPath,
                true);
        }

        private static SerializableModData ListManualSaves(DataContext context, SerializableModData parameter)
        {
            SerializableModData result = new SerializableModData();
            if (!TryGetSlot(parameter, out sbyte archiveId))
            {
                archiveId = Common.GetCurrArchiveId();
            }

            ArchiveInfo archiveInfo = BuildManualArchiveInfo(archiveId);
            result.Set(OkKey, true);
            result.Set(CountKey, archiveInfo.BackupWorldsInfo.Count);
            result.Set(CapacityKey, GetSavePointRetentionCount());
            result.Set(ArchiveInfoKey, archiveInfo);
            return result;
        }

        private static void LoadManualSave(DataContext context, SerializableModData parameter)
        {
            if (!TryGetSlot(parameter, out sbyte archiveId))
            {
                throw new ArgumentException("Missing manual save archive slot.");
            }

            if (!TryGetManualTimestamp(parameter, out long timestamp))
            {
                throw new ArgumentException("Missing manual save timestamp.");
            }

            string archiveDataPath = GetManualSavePath(archiveId, timestamp);
            if (!File.Exists(archiveDataPath))
            {
                throw new FileNotFoundException("Manual save file not found.", archiveDataPath);
            }

            // Leave the current world HERE, inside this mod-method call, rather than relying on the frontend
            // calling GlobalOperations.LeaveWorld() first. That frontend call is a domain-method call (global
            // listener) while this runs as a mod-method call (listener -1); the two streams are NOT ordered,
            // so LoadManualSave can be processed while still in-world, and LoadWorldAt's `if (IsInWorld()) throw
            // "Need to exit the current world first"` guard then trips. Doing it synchronously here keeps the
            // pack/leave/load sequence ordered. (Pack mirrors native GameApp.LoadArchive.)
            if (Common.IsInWorld())
            {
                DomainManager.Global.PackAllCrossArchiveGameData();
                DomainManager.Global.LeaveWorld();
            }

            InvokeGlobalPrivate("SetLoadedAllArchiveData", new[] { typeof(bool), typeof(DataContext) }, false, null);
            InvokeGlobalPrivate("LoadWorldAt", new[] { typeof(DataContext), typeof(string) }, context, archiveDataPath);
            // LoadWorldAt begins with `if (IsInWorld()) throw "Need to exit the current world first"`, so the
            // archive id MUST be set AFTER it (every native loader checks IsInWorld first, then SetArchiveId).
            // It still runs synchronously here, before LoadWorldAt's next-frame Load delegate executes.
            Common.SetArchiveId(archiveId);
            // LoadWorldAt registers RegisterItemOwners -> FixAllAbnormalDomainArchiveData -> InitBuildingEffect.
            // Native GlobalDomain.LoadWorld appends InitAchievements LAST, and post-modification handlers run in
            // registration order, so InitAchievements must be added AFTER LoadWorldAt (not before) to match the
            // native ordering and avoid resolving achievements against not-yet-fixed/owned archive data.
            AddInitAchievementsHandler();
            GameData.GameDataBridge.GameDataBridge.StartNextFrame(
                () => InvokeGlobalPrivate("SetCurrGameWorldType", new[] { typeof(sbyte), typeof(DataContext) }, (sbyte)1, null));
        }

        private static ArchiveInfo BuildManualArchiveInfo(sbyte archiveId)
        {
            ArchiveInfo archiveInfo = TryReadArchiveInfo(Common.GetArchiveDataPath(archiveId), out ArchiveInfo currentInfo)
                ? currentInfo
                : new ArchiveInfo { Status = ArchiveStatusGood };

            archiveInfo.Status = ArchiveStatusGood;
            archiveInfo.BackupWorldsInfo = new List<(long timestamp, WorldInfo worldInfo)>();

            foreach (long timestamp in GetManualSaveTimestamps(archiveId))
            {
                if (!TryReadArchiveInfo(GetManualSavePath(archiveId, timestamp), out ArchiveInfo manualInfo) ||
                    manualInfo.WorldInfo == null)
                {
                    continue;
                }

                archiveInfo.BackupWorldsInfo.Add((timestamp, manualInfo.WorldInfo));
                if (archiveInfo.WorldInfo == null)
                {
                    archiveInfo.WorldInfo = manualInfo.WorldInfo;
                }
            }

            return archiveInfo;
        }

        private static bool TryReadArchiveInfo(string archiveDataPath, out ArchiveInfo archiveInfo)
        {
            archiveInfo = null;
            try
            {
                return File.Exists(archiveDataPath) &&
                    new LocalArchiveFile(archiveDataPath).TryGetArchiveInfo(out archiveInfo);
            }
            catch
            {
                archiveInfo = null;
                return false;
            }
        }

        private static void CleanupManualSavesWhenSaveFinished(sbyte archiveId, int retries)
        {
            if (DomainManager.Global.GetSavingWorld() && retries > 0)
            {
                GameData.GameDataBridge.GameDataBridge.StartNextFrame(() => CleanupManualSavesWhenSaveFinished(archiveId, retries - 1));
                return;
            }

            if (DomainManager.Global.GetSavingWorld())
            {
                return;
            }

            RemoveRedundantManualSaves(archiveId, GetSavePointRetentionCount());
        }

        private static List<long> GetManualSaveTimestamps(sbyte archiveId)
        {
            List<long> timestamps = new List<long>();
            string archiveDirectory = Common.GetArchiveDataDirectory(archiveId);
            if (!Directory.Exists(archiveDirectory))
            {
                return timestamps;
            }

            foreach (string filePath in Directory.GetFiles(archiveDirectory, ManualSaveFilePrefix + "*"))
            {
                string fileName = Path.GetFileName(filePath);
                if (fileName.Length <= ManualSaveFilePrefix.Length)
                {
                    continue;
                }

                string timestampText = fileName.Substring(ManualSaveFilePrefix.Length);
                if (long.TryParse(timestampText, out long timestamp))
                {
                    timestamps.Add(timestamp);
                }
            }

            timestamps.Sort();
            timestamps.Reverse();
            return timestamps;
        }

        private static void RemoveRedundantManualSaves(sbyte archiveId, int retentionCount)
        {
            List<long> timestamps = GetManualSaveTimestamps(archiveId);
            for (int i = retentionCount; i < timestamps.Count; i++)
            {
                try
                {
                    File.Delete(GetManualSavePath(archiveId, timestamps[i]));
                }
                catch
                {
                    // A failed cleanup should not make the just-created save fail.
                }
            }
        }

        private static string GetManualSavePath(sbyte archiveId, long timestamp)
        {
            return Path.Combine(Common.GetArchiveDataDirectory(archiveId), ManualSaveFilePrefix + timestamp);
        }

        private static long CreateManualSaveTimestamp()
        {
            return long.Parse(DateTime.Now.ToString("yyyyMMddHHmmssffff"));
        }

        private static bool TryGetSlot(SerializableModData parameter, out sbyte slot)
        {
            slot = -1;
            return parameter != null &&
                parameter.Get(SlotKey, out int slotValue) &&
                slotValue >= sbyte.MinValue &&
                slotValue <= sbyte.MaxValue &&
                Common.CheckArchiveId((sbyte)slotValue) &&
                (slot = (sbyte)slotValue) >= 0;
        }

        private static bool TryGetManualTimestamp(SerializableModData parameter, out long timestamp)
        {
            timestamp = 0L;
            return parameter != null &&
                parameter.Get(TimestampKey, out string timestampText) &&
                long.TryParse(timestampText, out timestamp);
        }

        private static void AddInitAchievementsHandler()
        {
            MethodInfo method = DomainManager.Global.GetType().GetMethod(
                "InitAchievements",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(DomainManager.Global.GetType().FullName, "InitAchievements");
            }

            DataModificationHandler handler = (DataModificationHandler)Delegate.CreateDelegate(
                typeof(DataModificationHandler),
                DomainManager.Global,
                method);
            DomainManager.Global.AddPostModificationHandler(
                new DataUid(0, 1, ulong.MaxValue),
                "InitAchievements",
                handler);
        }

        private static void InvokeGlobalPrivate(string methodName, Type[] parameterTypes, params object[] args)
        {
            MethodInfo method = DomainManager.Global.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            if (method == null)
            {
                throw new MissingMethodException(DomainManager.Global.GetType().FullName, methodName);
            }

            try
            {
                method.Invoke(DomainManager.Global, args);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        private static int GetSavePointRetentionCount()
        {
            int nativeBackupCount = DomainManager.World.GetArchiveFilesBackupCount();
            return Math.Max(MinimumSavePointRetentionCount, nativeBackupCount);
        }
    }
}
