using System;
using System.IO;
using GameData.ArchiveData;
using GameData.Common;
using GameData.Domains.Global;

namespace EasyQuickSaveLoad.Backend
{
    /// <summary>栏位存档文件管理：快捷栏 slot=-1 → local.sav.qsl.quick；普通栏 N → local.sav.qsl.slot.N。</summary>
    internal static class SlotStore
    {
        public const int QuickSlot = -1;
        private const string QuickFileName = "local.sav.qsl.quick";
        private const string SlotFilePrefix = "local.sav.qsl.slot.";
        // 存档栏位按「页」配置：每页 5 个，可用页数 1..10。
        public const int SlotsPerPage = 5;
        public const int DefaultPageCount = 4;
        public const int MaxPageCount = 10;
        // 备注裁剪只清理"永不可达"(≥此值)的孤儿；调小页数被临时隐藏的页(仍在此范围内)绝不删。
        public const int MaxSlotCount = MaxPageCount * SlotsPerPage; // 50

        public static string GetSlotPath(sbyte archiveId, int slot)
        {
            string dir = Common.GetArchiveDataDirectory(archiveId);
            string name = slot == QuickSlot ? QuickFileName : SlotFilePrefix + slot;
            return Path.Combine(dir, name);
        }

        public static void EnsureDir(sbyte archiveId)
        {
            Directory.CreateDirectory(Common.GetArchiveDataDirectory(archiveId));
        }

        /// <summary>读某栏的 WorldInfo 摘要；无文件/读失败返回 null。</summary>
        public static WorldInfo TryReadWorldInfo(sbyte archiveId, int slot)
        {
            string path = GetSlotPath(archiveId, slot);
            try
            {
                if (File.Exists(path) &&
                    new LocalArchiveFile(path).TryGetArchiveInfo(out ArchiveInfo info) &&
                    info != null)
                {
                    return info.WorldInfo;
                }
            }
            catch
            {
                // 读损坏文件不应崩溃
            }
            return null;
        }

        public static bool SlotExists(sbyte archiveId, int slot)
        {
            return File.Exists(GetSlotPath(archiveId, slot));
        }

        public static void DeleteSlotFile(sbyte archiveId, int slot)
        {
            try
            {
                string path = GetSlotPath(archiveId, slot);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 删除失败静默（文件可能被占用）
            }
        }
    }
}
