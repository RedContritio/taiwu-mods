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
            if (father.GetId() != taiwuId && mother.GetId() != taiwuId)
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

            bool setAll = GetToggle("SetAllFertility");
            if (setAll)
            {
                if (!PassChildLimit(father, mother, fatherFertility, motherFertility, ref __result))
                    return false;

                int val = GetSlider("AllFertilityValue");
                int roll = isRape ? 20 : 60;
                __result = random.CheckPercentProb(roll * val / 10000);
                Log($"CheckPregnant override (total): fertility={val}, base={roll}%, result={__result}");
                return false;
            }

            if (GetToggle("SetFatherFertility"))
                fatherFertility = (short)GetSlider("FatherFertilityValue");
            if (GetToggle("SetMotherFertility"))
                motherFertility = (short)GetSlider("MotherFertilityValue");

            bool fertilityOverridden = GetToggle("SetFatherFertility") || GetToggle("SetMotherFertility");
            bool rateOverridden = GetToggle("SetPregnancyRate");

            if (!fertilityOverridden && !rateOverridden)
                return true;

            if (!PassChildLimit(father, mother, fatherFertility, motherFertility, ref __result))
                return false;

            if (fatherFertility <= 0 || motherFertility <= 0)
            {
                __result = false;
                return false;
            }

            int prob;
            if (rateOverridden)
            {
                prob = GetSlider("PregnancyRate");
            }
            else
            {
                int baseChance = isRape ? 20 : 60;
                prob = (int)((long)baseChance * fatherFertility * motherFertility / 10000);
            }

            __result = random.CheckPercentProb(prob);
            Log($"CheckPregnant override: fFert={fatherFertility}, mFert={motherFertility}, prob={prob}%, result={__result}");
            return false;
        }

        private static bool PassChildLimit(Character father, Character mother, short fatherFertility, short motherFertility, ref bool result)
        {
            int fatherChildren = DomainManager.Character.GetRelatedCharIds(father.GetId(), 2).Count;
            if (fatherFertility / 20 < fatherChildren)
            {
                result = false;
                return false;
            }

            int motherChildren = DomainManager.Character.GetRelatedCharIds(mother.GetId(), 2).Count;
            if (motherFertility / 20 < motherChildren)
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

        private static void Log(string msg)
        {
            if (GetToggle("DebugMode"))
                AdaptableLog.Info("[FertilityControl] " + msg);
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
            if (!disable)
                return true;

            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (charId != taiwuId && relatedCharId != taiwuId)
                return true;

            __result = false;

            bool debug = false;
            DomainManager.Mod.GetSetting(BackendPlugin.ModId, "DebugMode", ref debug);
            if (debug)
                AdaptableLog.Info($"[FertilityControl] Inbreeding blocked: {charId} x {relatedCharId}");

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
            public int CricketLuckPoint;
        }

        [HarmonyPrefix]
        public static void Prefix(CharacterDomain __instance, Character mother, ref PatchState __state)
        {
            __state = new PatchState
            {
                HadPregnantState = __instance.TryGetPregnantState(mother.GetId(), out _),
                CurrDate = DomainManager.World.GetCurrDate(),
                CricketLuckPoint = DomainManager.Taiwu.GetCricketLuckPoint()
            };
        }

        [HarmonyPostfix]
        public static void Postfix(CharacterDomain __instance, DataContext context, Character mother, Character father, PatchState __state)
        {
            bool setCricket = false;
            DomainManager.Mod.GetSetting(BackendPlugin.ModId, "SetCricketRate", ref setCricket);
            if (!setCricket)
                return;
            if (__state.HadPregnantState)
                return;

            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (father.GetId() != taiwuId && mother.GetId() != taiwuId)
                return;

            if (!__instance.TryGetPregnantState(mother.GetId(), out PregnantState state))
                return;

            int rate = 0;
            DomainManager.Mod.GetSetting(BackendPlugin.ModId, "CricketRate", ref rate);
            bool wasHuman = state.IsHuman;
            state.IsHuman = !context.Random.CheckPercentProb(rate);
            if (state.IsHuman)
            {
                state.ExpectedBirthDate = __state.CurrDate + context.Random.Next(6, 10);
                DomainManager.Taiwu.SetCricketLuckPoint(__state.CricketLuckPoint, context);
            }
            else
            {
                state.ExpectedBirthDate = __state.CurrDate + 42;
                DomainManager.Taiwu.SetCricketLuckPoint(0, context);
            }

            bool debug = false;
            DomainManager.Mod.GetSetting(BackendPlugin.ModId, "DebugMode", ref debug);
            if (debug)
                AdaptableLog.Info($"[FertilityControl] Cricket override: rate={rate}%, wasHuman={wasHuman}, isHuman={state.IsHuman}");
        }
    }
}
