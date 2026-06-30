using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CricketSingGradeColor.Frontend
{
    /// <summary>
    /// 捕蛐蛐开局 <c>ViewCatchCricket.InitCatchPlace()</c> 一次性确定全部 21 个草丛蛐蛐。
    /// 其结束时各位置的 CricketColorId/CricketPartsId 均已填好——按位置顺序散列成本场盐 SessionSeed，
    /// 供盲盒「随机」形式做确定性取色：场次内不变(不闪)，换场次(组成不同)则变(强化盲盒感)。
    /// </summary>
    [HarmonyPatch(typeof(ViewCatchCricket), "InitCatchPlace")]
    internal static class SessionSeedPatch
    {
        private static FieldInfo _listField;

        public static void Postfix(ViewCatchCricket __instance)
        {
            try
            {
                _listField ??= AccessTools.Field(typeof(ViewCatchCricket), "_catchPlaceList");
                if (!(_listField?.GetValue(__instance) is Array places))
                {
                    BlindBoxConfig.SessionSeed = 0;
                    return;
                }

                int n = places.Length;
                int[] colorIds = new int[n];
                int[] partsIds = new int[n];
                for (int i = 0; i < n; i++)
                {
                    object place = places.GetValue(i);
                    if (place == null)
                    {
                        continue;
                    }

                    colorIds[i] = Convert.ToInt32(ReadField(place, "CricketColorId"));
                    partsIds[i] = Convert.ToInt32(ReadField(place, "CricketPartsId"));
                }

                BlindBoxConfig.SessionSeed = BlindBox.ComputeSessionSeed(colorIds, partsIds);
            }
            catch (Exception ex)
            {
                BlindBoxConfig.SessionSeed = 0;
                Debug.LogWarning("[CricketSingGradeColor] Could not derive session seed: " + ex);
            }
        }

        private static object ReadField(object obj, string name)
        {
            FieldInfo f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            return f != null ? f.GetValue(obj) : 0;
        }
    }
}
