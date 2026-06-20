namespace ForceEncounter.Shared
{
    public static class ForceEncounterConstants
    {
        public static class Mod
        {
            public const string Id = "ForceEncounter";
            public const string Author = "RedContritio";
        }

        public static class Settings
        {
            public const string Enabled = "Enabled";
            public const string DebugMode = "DebugMode";
            public const string ActionTimeCostDays = "ActionTimeCostDays";
            public const string ForcedFavorabilityPenalty = "ForcedFavorabilityPenalty";
            public const string TaiwuVillagerPenaltyPercent = "TaiwuVillagerPenaltyPercent";
            public const string ApplyAlertnessOnCombatStart = "ApplyAlertnessOnCombatStart";
            public const string EnableGuardInterception = "EnableGuardInterception";
            public const string EnableTaiwuVillagerGuardInterception = "EnableTaiwuVillagerGuardInterception";
            public const string BecomeEnemyOnForcedRoute = "BecomeEnemyOnForcedRoute";
            public const string CreateSecretOnForcedSuccess = "CreateSecretOnForcedSuccess";
            public const string CreateSecretOnAcceptedSuccess = "CreateSecretOnAcceptedSuccess";
            public const string 允许未成年 = "允许未成年";
        }

        public static class Gameplay
        {
            public const short InteractionTemplateId = 1099;
            public const sbyte TaiwuVillageOrgTemplateId = 16;
            public const int DefaultForcedFavorabilityPenalty = 30000;
            public const int MinForcedFavorabilityPenalty = 0;
            public const int MaxForcedFavorabilityPenalty = 30000;
            public const int DefaultTaiwuVillagerPenaltyPercent = 30;
            public const int MinTaiwuVillagerPenaltyPercent = 0;
            public const int MaxTaiwuVillagerPenaltyPercent = 100;
            public const int 婴儿年龄组 = 0;
            public const int 成年年龄组 = 2;
        }

        public static class Relations
        {
            public const ushort Spouse = 1024;
            public const ushort Adored = 16384;
        }

        public static class Features
        {
            public const short DeepValleyCloseFriend = 685;
        }

        public static class FavorabilityTypes
        {
            public const sbyte Favorite2 = 2;
        }

        public static class Costs
        {
            public const sbyte ActionTimeConsumeType = 8;
            public const int DefaultActionTimeDays = 5;
            public const int MinActionTimeDays = 0;
            public const int MaxActionTimeDays = 5;
            public const short ActionPointConditionType = 2;
        }

        public static class EventGuids
        {
            public const string NativeEnemyInteraction = "7c70ce0c-577a-4049-bcad-e593c63d62d4";
            public const string Entry = "dd8e7372-30c6-47e9-9e1a-0f6dd7f08919";
            public const string ConsentChoice = "a5ef5a51-2c64-40d2-a936-7c9d8ff7c6c6";
            public const string GuardIntercept = "27f95f25-e7bb-470a-970e-3fb4cf12f63b";
            public const string CombatResult = "ea75dd81-9054-4e54-9e6e-799669e2fb41";
            public const string AcceptedResult = "5af48fa3-2fa8-44c5-8a04-8ad9805d3f47";
        }

        public static class Options
        {
            public const string OpenKey = "ForceEncounter.Open";
            public const string OpenGuid = "ee1110a7-f974-4bc4-bf84-1ddcfcc04ae1";
            public const string ExecuteKey = "ForceEncounter.Execute";
            public const string ExecuteGuid = "8267b6bc-3a92-4b3f-a013-a3446272866f";
            public const string NormalEncounterKey = "ForceEncounter.NormalEncounter";
            public const string NormalEncounterGuid = "8e7a9f4b-6d2c-4e0f-8c63-49ecf7de6812";
            public const string ForceCombatKey = "ForceEncounter.ForceCombat";
            public const string ForceCombatGuid = "ce18f262-9df1-46df-a604-0fd77759d8f0";
            public const string AbandonKey = "ForceEncounter.Abandon";
            public const string AbandonGuid = "8904aa49-9a92-4b82-9045-dad4f125ff8b";
            public const string CombatResultContinueKey = "ForceEncounter.CombatResultContinue";
            public const string CombatResultContinueGuid = "2bf7c96a-d482-45ce-96d8-47dd4ecb795c";
            public const string AcceptedResultContinueKey = "ForceEncounter.AcceptedResultContinue";
            public const string AcceptedResultContinueGuid = "8d6c8b16-2eb7-46f4-b3f5-e7e9b7d5bf46";
            public const string GuardContinueKey = "ForceEncounter.GuardInterceptContinue";
            public const string GuardContinueGuid = "24d8d555-70c2-4c47-8518-1e74cfdbf2e9";
        }

        public static class WaitConfirm
        {
            public const string OuterPreview = "ForceEncounter.OuterPreview";
            public const string ConfirmSignal = "ConchShip_PresetKey_ConfirmWaitOptionSignal";
        }

        public static class ArgBox
        {
            public const string ActorId = "ForceEncounter.ActorId";
            public const string TargetId = "ForceEncounter.TargetId";
            public const string 未成年 = "ForceEncounter.未成年";
            public const string CombatResultHandled = "ForceEncounter.CombatResultHandled";
            public const string CombatSettleOk = "ForceEncounter.CombatResultOk";
            public const string CombatBranchSucceeded = "ForceEncounter.CombatResultSucceeded";
            public const string CombatTargetIsTaiwuVillager = "ForceEncounter.CombatResultTargetTaiwuVillager";
            public const string CombatAppliedEnmity = "ForceEncounter.CombatResultAppliedEnmity";
            public const string CombatSettleReason = "ForceEncounter.CombatResultReason";
            public const string AcceptedResultHandled = "ForceEncounter.AcceptedResultHandled";
            public const string AcceptedSettleOk = "ForceEncounter.AcceptedResultOk";
            public const string AcceptedBranchSucceeded = "ForceEncounter.AcceptedResultSucceeded";
            public const string AcceptedSettleReason = "ForceEncounter.AcceptedResultReason";
            public const string GuardInterceptActive = "ForceEncounter.GuardInterceptActive";
            public const string GuardTeamPrepared = "ForceEncounter.GuardTeamPrepared";
            public const string KillTargetAfterCombatResult = "ForceEncounter.KillTargetAfterCombatResult";
            public const string NativeCombatResult = "CombatResult";
            public const string NativeCombatType = "CombatType";
            public const string NativeSeizedCharacterId = "CharIdSeizedInCombat";
            public const string NativeSeizeItemKey = "ItemKeySeizeCharacterInCombat";
            public const string NativeMainEnemyId = "MainEnemyId";
            public const string NativeMainInteractionHeadEvent = "MainInteractionHeadEvent";
            public const string NativePartner = "Partner";
            public const string NativePrisoner = "Prisoner";
        }

        public static class RoleKeys
        {
            public const string Taiwu = "RoleTaiwu";
        }

        public static class Backend
        {
            public const string ExecuteMethod = "ExecuteForcedAction";
            public const string ActorId = "ActorId";
            public const string TargetId = "TargetId";
            public const string BattleSucceeded = "BattleSucceeded";
            public const string ResolutionMode = "ResolutionMode";
            public const string Resolution = "Resolution";
        }

        public static class Response
        {
            public const string Ok = "Ok";
            public const string Succeeded = "Succeeded";
            public const string ActorId = Backend.ActorId;
            public const string TargetId = Backend.TargetId;
            public const string TargetIsTaiwuVillager = "TargetIsTaiwuVillager";
            public const string AppliedEnmity = "AppliedEnmity";
            public const string Reason = "Reason";
        }

        public static class ResolutionMode
        {
            public const int Probe = 1;
            public const int Combat = 2;
            public const int AcceptedCommit = 3;
        }

        public static class ProbeResult
        {
            public const int NeedCombatChoice = 1;
            public const int Accepted = 2;
        }

        public static class Reasons
        {
            public const string Ok = "Ok";
            public const string Disabled = "Disabled";
            public const string ModDisabled = "ModDisabled";
            public const string Accepted = "Accepted";
            public const string NeedCombatChoice = "NeedCombatChoice";
            public const string Succeed = "Succeed";
            public const string Failed = "Failed";
            public const string NoResult = "NoResult";
            public const string NoArgBox = "NoArgBox";
            public const string MissingCharacterId = "MissingCharacterId";
            public const string MissingActorOrTarget = "MissingActorOrTarget";
            public const string MissingBattleResult = "MissingBattleResult";
            public const string SameActorAndTarget = "SameActorAndTarget";
            public const string NonTaiwuActorNotAllowed = "NonTaiwuActorNotAllowed";
            public const string TaiwuTargetNotAllowed = "TaiwuTargetNotAllowed";
            public const string ActorNotFound = "ActorNotFound";
            public const string TargetNotFound = "TargetNotFound";
            public const string ActorNotAlive = "ActorNotAlive";
            public const string TargetNotAlive = "TargetNotAlive";
            public const string CharacterNotAlive = "CharacterNotAlive";
            public const string ActorBaby = "ActorBaby";
            public const string TargetBaby = "TargetBaby";
            public const string BabyNotAllowed = "BabyNotAllowed";
            public const string 未成年不允许 = "未成年不允许";
            public const string TargetDirectFallen = "TargetDirectFallen";
            public const string GuardIntercepted = "GuardIntercepted";
            public const string TargetEscaped = "TargetEscaped";
            public const string ActorEscaped = "ActorEscaped";
            public const string TargetDiedInCombat = "TargetDiedInCombat";
        }
    }
}
