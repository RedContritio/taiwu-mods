using HarmonyLib;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Utilities;
using Redzen.Random;

namespace FertilityControl.Backend
{
    [HarmonyPatch(typeof(PregnantState), "CheckPregnant")]
    public static class CheckPregnantPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(IRandomSource random, Character father, Character mother, bool isRape, ref bool __result)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            int fatherId = father.GetId();
            int motherId = mother.GetId();
            if (!FertilityRules.IsTaiwuInvolved(taiwuId, fatherId, motherId))
                return true;

            if (!Character.CheckMakeLoveRole(father, mother))
            {
                __result = false;
                return false;
            }
            if (mother.GetFeatureIds().Contains((short)198))
            {
                __result = false;
                return false;
            }
            if (DomainManager.Character.TryGetElement_PregnancyLockEndDates(mother.GetId(), out _))
            {
                __result = false;
                return false;
            }

            short fatherFertility = father.GetFertility();
            short motherFertility = mother.GetFertility();

            bool ignoreChildLimit = GetToggle("IgnoreChildLimit");
            if (GetToggle("SetFatherFertility"))
                fatherFertility = (short)GetSlider("FatherFertilityValue");
            if (GetToggle("SetMotherFertility"))
                motherFertility = (short)GetSlider("MotherFertilityValue");

            bool fertilityOverridden = GetToggle("SetFatherFertility") || GetToggle("SetMotherFertility");
            bool rateOverridden = GetToggle("AlwaysPregnant");

            if (!FertilityRules.ShouldOverridePregnancyCheck(fertilityOverridden, rateOverridden, ignoreChildLimit))
                return true;

            if (!PassChildLimit(father, mother, fatherFertility, motherFertility, ignoreChildLimit, ref __result))
                return false;

            if (FertilityRules.ShouldBlockForZeroFertility(fatherFertility, motherFertility, rateOverridden))
            {
                __result = false;
                return false;
            }

            int prob = FertilityRules.CalculatePregnancyChance(
                isRape,
                fatherFertility,
                motherFertility,
                rateOverridden,
                100); // 必定怀孕 = 100% 成功

            __result = random.CheckPercentProb(prob);
            return false;
        }

        private static bool PassChildLimit(
            Character father,
            Character mother,
            short fatherFertility,
            short motherFertility,
            bool ignoreChildLimit,
            ref bool result)
        {
            int fatherChildren = DomainManager.Character.GetRelatedCharIds(father.GetId(), 2).Count;
            int motherChildren = DomainManager.Character.GetRelatedCharIds(mother.GetId(), 2).Count;
            if (!FertilityRules.PassChildLimit(fatherChildren, motherChildren, fatherFertility, motherFertility, ignoreChildLimit))
            {
                result = false;
                return false;
            }

            return true;
        }

        private static bool GetToggle(string key)
        {
            bool val = false;
            DomainManager.Mod.GetSetting(BackendPlugin.ModId, key, ref val);
            return val;
        }

        private static int GetSlider(string key)
        {
            int val = 0;
            DomainManager.Mod.GetSetting(BackendPlugin.ModId, key, ref val);
            return val;
        }
    }

    [HarmonyPatch(typeof(Character), "GetMakeLoveRole")]
    public static class MakeLoveRolePatch
    {
        private const short FlexibleRoleFeatureId = 170;

        [HarmonyPrefix]
        public static bool Prefix(IRandomSource random, ref Character father, ref Character mother, ref bool __result)
        {
            if (!ShouldBypassNativeRoleAssignment(father, mother))
                return true;

            __result = true;
            return false;
        }

        [HarmonyPostfix]
        public static void Postfix(ref Character father, ref Character mother, bool __result)
        {
            if (!__result || !ShouldSwapPregnancyCarrier(father, mother))
                return;

            Character originalFather = father;
            father = mother;
            mother = originalFather;
        }

        private static bool ShouldBypassNativeRoleAssignment(Character first, Character second)
        {
            bool allowNonstandardRoles = false;
            DomainManager.Mod.GetSetting(BackendPlugin.ModId, "AllowNonstandardPregnancyRoles", ref allowNonstandardRoles);

            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            bool nativeAssignmentCanResolve = FertilityRules.CanResolveNativeMakeLoveRole(
                first.GetGender(),
                HasFlexibleRole(first),
                second.GetGender(),
                HasFlexibleRole(second));

            return FertilityRules.ShouldBypassNativeRoleGate(
                allowNonstandardRoles,
                FertilityRules.IsTaiwuInvolved(taiwuId, first.GetId(), second.GetId()),
                nativeAssignmentCanResolve);
        }

        private static bool ShouldSwapPregnancyCarrier(Character father, Character mother)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            int fatherId = father.GetId();
            int motherId = mother.GetId();
            int pregnancyCarrierMode = GetPregnancyCarrierMode();
            return FertilityRules.ShouldUseConfiguredCarrier(
                       pregnancyCarrierMode,
                       FertilityRules.IsTaiwuInvolved(taiwuId, fatherId, motherId)) &&
                   FertilityRules.ShouldSwapForPregnancyCarrier(
                       pregnancyCarrierMode,
                       taiwuId,
                       fatherId,
                       motherId);
        }

        private static int GetPregnancyCarrierMode()
        {
            int val = FertilityRules.PregnancyCarrierNativeMother;
            DomainManager.Mod.GetSetting(BackendPlugin.ModId, "PregnancyCarrierMode", ref val);
            return FertilityRules.NormalizePregnancyCarrierMode(val);
        }

        private static bool HasFlexibleRole(Character character)
        {
            return character.GetFeatureIds().Contains(FlexibleRoleFeatureId);
        }
    }

    [HarmonyPatch(typeof(Character), "CheckMakeLoveRole")]
    public static class CheckMakeLoveRolePatch
    {
        private const short FlexibleRoleFeatureId = 170;

        [HarmonyPrefix]
        public static bool Prefix(Character father, Character mother, ref bool __result)
        {
            if (!ShouldBypassNativeRoleCheck(father, mother))
                return true;

            __result = true;
            return false;
        }

        private static bool ShouldBypassNativeRoleCheck(Character father, Character mother)
        {
            bool allowNonstandardRoles = false;
            DomainManager.Mod.GetSetting(BackendPlugin.ModId, "AllowNonstandardPregnancyRoles", ref allowNonstandardRoles);

            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            int pregnancyCarrierMode = GetPregnancyCarrierMode();
            bool nativeRoleAllows = FertilityRules.CanUseNativePregnancyRole(
                father.GetGender(),
                HasFlexibleRole(father),
                mother.GetGender(),
                HasFlexibleRole(mother));
            bool nativeAssignmentCanResolve = FertilityRules.CanResolveNativeMakeLoveRole(
                father.GetGender(),
                HasFlexibleRole(father),
                mother.GetGender(),
                HasFlexibleRole(mother));

            return FertilityRules.ShouldBypassPregnancyRoleCheck(
                allowNonstandardRoles,
                FertilityRules.IsTaiwuInvolved(taiwuId, father.GetId(), mother.GetId()),
                nativeRoleAllows,
                nativeAssignmentCanResolve,
                pregnancyCarrierMode);
        }

        private static int GetPregnancyCarrierMode()
        {
            int val = FertilityRules.PregnancyCarrierNativeMother;
            DomainManager.Mod.GetSetting(BackendPlugin.ModId, "PregnancyCarrierMode", ref val);
            return FertilityRules.NormalizePregnancyCarrierMode(val);
        }

        private static bool HasFlexibleRole(Character character)
        {
            return character.GetFeatureIds().Contains(FlexibleRoleFeatureId);
        }
    }

    [HarmonyPatch(typeof(CharacterDomain), "IsInsect")]
    public static class InbreedingPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(int charId, int relatedCharId, ref bool __result)
        {
            bool disable = false;
            DomainManager.Mod.GetSetting(BackendPlugin.ModId, "DisableInbreeding", ref disable);
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (!FertilityRules.ShouldOverrideInbreeding(
                disable,
                FertilityRules.IsTaiwuInvolved(taiwuId, charId, relatedCharId)))
                return true;

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(CharacterDomain), "CreatePregnantState")]
    public static class CricketRatePatch
    {
        public struct PatchState
        {
            public bool HadPregnantState;
            public int CurrDate;
        }

        [HarmonyPrefix]
        public static void Prefix(CharacterDomain __instance, Character mother, ref PatchState __state)
        {
            __state = new PatchState
            {
                HadPregnantState = __instance.TryGetPregnantState(mother.GetId(), out _),
                CurrDate = DomainManager.World.GetCurrDate()
            };
        }

        [HarmonyPostfix]
        public static void Postfix(CharacterDomain __instance, DataContext context, Character mother, Character father, PatchState __state)
        {
            bool alwaysCricket = false;
            DomainManager.Mod.GetSetting(BackendPlugin.ModId, "AlwaysCricket", ref alwaysCricket);
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (!FertilityRules.ShouldOverrideCricketRate(
                alwaysCricket,
                __state.HadPregnantState,
                FertilityRules.IsTaiwuInvolved(taiwuId, father.GetId(), mother.GetId())))
                return;

            if (!__instance.TryGetPregnantState(mother.GetId(), out PregnantState state))
                return;

            state.IsHuman = false;                                // 必定蛐蛐
            state.ExpectedBirthDate = __state.CurrDate + 42;
            DomainManager.Taiwu.SetCricketLuckPoint(0, context);  // 生蛐蛐消耗促织缘归零，与原生一致
        }
    }
}
