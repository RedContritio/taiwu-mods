using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Map;
using GameData.Domains.Mod;
using GameData.Utilities;
using TaiwuModdingLib.Core.Plugin;

namespace ForceEncounter.Backend
{
    [PluginConfig("ForceEncounter", "RedContritio", "2.0.0")]
    public class BackendPlugin : TaiwuRemakePlugin
    {
        private const string ExecuteMethod = "ExecuteForcedAction";
        private const int HatredRelationType = 32768;
        private const int RapeFavorabilityDelta = -30000;

        internal static string ModId;

        public override void Initialize()
        {
            ModId = ModIdStr;
            AddExecuteMethod(ModId);
            AddExecuteMethod("ForceEncounter");
        }

        public override void Dispose()
        {
        }

        public override void OnModSettingUpdate()
        {
            // Settings are read live from DomainManager.Mod so existing saves pick up changes immediately.
        }

        private static SerializableModData ExecuteForcedAction(DataContext context, SerializableModData parameter)
        {
            var result = new SerializableModData();
            if (!GetBoolSetting("Enabled", true))
            {
                return Fail(result, "ModDisabled");
            }

            if (!TryGetInt(parameter, "ActorId", out int actorId))
            {
                actorId = DomainManager.Taiwu.GetTaiwuCharId();
            }

            if (!TryGetInt(parameter, "TargetId", out int targetId))
            {
                return Fail(result, "MissingActorOrTarget");
            }

            bool allowTaiwuAsTarget = GetBool(parameter, "AllowTaiwuAsTarget", GetBoolSetting("AllowTaiwuAsTarget", false));
            if (!TryGetBool(parameter, ForceEncounterEventIds.BattleSucceededParam, out bool battleSucceeded))
            {
                return Fail(result, "MissingBattleResult");
            }

            if (actorId == targetId)
            {
                return Fail(result, "SameActorAndTarget");
            }

            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (!allowTaiwuAsTarget && targetId == taiwuId)
            {
                return Fail(result, "TaiwuTargetNotAllowed");
            }

            if (!DomainManager.Character.TryGetElement_Objects(actorId, out Character actor))
            {
                return Fail(result, "ActorNotFound");
            }

            if (!DomainManager.Character.TryGetElement_Objects(targetId, out Character target))
            {
                return Fail(result, "TargetNotFound");
            }

            if (!DomainManager.Character.IsCharacterAlive(actorId) || !DomainManager.Character.IsCharacterAlive(targetId))
            {
                return Fail(result, "CharacterNotAlive");
            }

            if (actor.GetAgeGroup() != 2 || target.GetAgeGroup() != 2)
            {
                return Fail(result, "NotAdult");
            }

            bool success = ExecuteResolvedAction(context, actor, target, battleSucceeded);

            result.Set("Ok", true);
            result.Set("Succeeded", success);
            result.Set("ActorId", actorId);
            result.Set("TargetId", targetId);
            result.Set("Reason", success ? "Succeed" : "Failed");
            DebugLog($"Execute {actorId}->{targetId}, battleSucceeded={battleSucceeded}, success={success}");
            return result;
        }

        private static bool ExecuteResolvedAction(
            DataContext context,
            Character actor,
            Character target,
            bool battleSucceeded)
        {
            int actorId = actor.GetId();
            int targetId = target.GetId();
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            int currDate = DomainManager.World.GetCurrDate();
            Location location = actor.GetLocation();
            bool success = ForceEncounterRules.IsResolvedSuccess(
                battleSucceeded,
                targetId == taiwuId,
                actor.GetFertility());

            if (success)
            {
                actor.MakeLove(context, target, isRape: true);
                DomainManager.LifeRecord.GetLifeRecordCollection().AddRapeSucceed(actorId, currDate, targetId, location);
                DomainManager.Character.AddRelation(context, targetId, actorId, HatredRelationType, currDate);
                DomainManager.Character.ChangeFavorabilityOptionalMonthlyEvolution(context, target, actor, RapeFavorabilityDelta);
                int dataOffset = DomainManager.Information.GetSecretInformationCollection().AddRape(actorId, targetId);
                DomainManager.Information.AddSecretInformation(context, dataOffset);
            }
            else
            {
                if (targetId == taiwuId)
                {
                    DomainManager.World.GetMonthlyNotificationCollection().AddRapeFailure(actorId, location, targetId);
                }

                DomainManager.LifeRecord.GetLifeRecordCollection().AddRapeFail(actorId, currDate, targetId, location);
                DomainManager.Character.AddRelation(context, targetId, actorId, HatredRelationType, currDate);
                DomainManager.Character.ChangeFavorabilityOptionalMonthlyEvolution(context, target, actor, RapeFavorabilityDelta);
            }

            return success;
        }

        private static SerializableModData Fail(SerializableModData result, string reason)
        {
            result.Set("Ok", false);
            result.Set("Succeeded", false);
            result.Set("Reason", reason);
            DebugLog($"Failed: {reason}");
            return result;
        }

        private static bool TryGetInt(SerializableModData data, string key, out int value)
        {
            value = 0;
            return data != null && data.Get(key, out value);
        }

        private static bool TryGetBool(SerializableModData data, string key, out bool value)
        {
            value = false;
            return data != null && data.Get(key, out value);
        }

        private static bool GetBool(SerializableModData data, string key, bool fallback)
        {
            if (data != null && data.Get(key, out bool value))
            {
                return value;
            }

            return fallback;
        }

        private static bool GetBoolSetting(string key, bool fallback)
        {
            bool value = fallback;
            DomainManager.Mod.GetSetting(ModId, key, ref value);
            return value;
        }

        private static void AddExecuteMethod(string modId)
        {
            try
            {
                DomainManager.Mod.AddModMethod(modId, ExecuteMethod, ExecuteForcedAction);
            }
            catch
            {
                // The development mod id and runtime mod id can be identical.
            }

            try
            {
                DomainManager.Mod.AddModMethod(modId, ExecuteMethod, ExecuteForcedActionNoReturn);
            }
            catch
            {
                // The development mod id and runtime mod id can be identical.
            }
        }

        private static void ExecuteForcedActionNoReturn(DataContext context, SerializableModData parameter)
        {
            ExecuteForcedAction(context, parameter);
        }

        internal static void DebugLog(string message)
        {
            if (GetBoolSetting("DebugMode", false))
            {
                AdaptableLog.Info($"[ForceEncounter] {message}");
            }
        }
    }
}
