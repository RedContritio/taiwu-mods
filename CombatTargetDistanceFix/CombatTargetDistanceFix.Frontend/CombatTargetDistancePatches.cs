using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Game.Views.Combat;
using HarmonyLib;
using UnityEngine;

namespace CombatTargetDistanceFix.Frontend
{
    internal static class CombatTargetDistanceRefresher
    {
        private static readonly MethodInfo UpdateTargetDistanceInteract =
            AccessTools.Method(typeof(ViewCombat), "UpdateTargetDistanceInteract");

        internal static void Refresh(object viewCombat)
        {
            if (viewCombat == null || UpdateTargetDistanceInteract == null)
            {
                return;
            }

            try
            {
                UpdateTargetDistanceInteract.Invoke(viewCombat, null);
            }
            catch (Exception ex)
            {
                Debug.LogError("[CombatTargetDistanceFix] Failed to refresh target distance interact state: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(ViewCombat), "ChangeCombatChar")]
    internal static class ViewCombatChangeCombatCharPatch
    {
        private static void Postfix(object __instance, ref IEnumerator __result)
        {
            if (__result != null)
            {
                __result = RefreshAfterChange(__instance, __result);
            }
        }

        private static IEnumerator RefreshAfterChange(object viewCombat, IEnumerator original)
        {
            while (true)
            {
                bool moveNext;
                try
                {
                    moveNext = original.MoveNext();
                }
                catch
                {
                    CombatTargetDistanceRefresher.Refresh(viewCombat);
                    throw;
                }

                if (!moveNext)
                {
                    break;
                }

                yield return original.Current;
            }

            CombatTargetDistanceRefresher.Refresh(viewCombat);
        }
    }

    [HarmonyPatch(typeof(ViewCombat), "OnExecutingTeammateCommandChanged")]
    internal static class ViewCombatExecutingTeammateCommandChangedPatch
    {
        private static void Postfix(object __instance)
        {
            CombatTargetDistanceRefresher.Refresh(__instance);
        }
    }

    [HarmonyPatch(typeof(CombatDistanceBar), "UpdateInteractType", new Type[] { })]
    internal static class CombatDistanceBarUpdateInteractTypePatch
    {
        private static readonly MethodInfo HarmonyTargetForContract =
            AccessTools.Method(typeof(CombatDistanceBar), "UpdateInteractType", Type.EmptyTypes);

        private static readonly MethodInfo OriginalContainsScreenPoint =
            AccessTools.Method(
                typeof(RectTransformUtility),
                nameof(RectTransformUtility.RectangleContainsScreenPoint),
                new[] { typeof(RectTransform), typeof(Vector2) });

        private static readonly MethodInfo ContainsScreenPointWithUiCameraMethod =
            AccessTools.Method(
                typeof(CombatDistanceBarUpdateInteractTypePatch),
                nameof(ContainsScreenPointWithUiCamera));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(OriginalContainsScreenPoint))
                {
                    yield return new CodeInstruction(OpCodes.Call, ContainsScreenPointWithUiCameraMethod);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        private static bool ContainsScreenPointWithUiCamera(RectTransform rect, Vector2 screenPoint)
        {
            Camera camera = null;
            try
            {
                camera = UIManager.Instance != null ? UIManager.Instance.UiCamera : null;
            }
            catch
            {
            }

            return camera != null
                ? RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, camera)
                : RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint);
        }
    }
}
