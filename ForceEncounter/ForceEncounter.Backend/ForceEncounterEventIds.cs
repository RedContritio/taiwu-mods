using ForceEncounter.Shared;

namespace ForceEncounter.Backend
{
    public static class ForceEncounterEventIds
    {
        public const short InteractionTemplateId = ForceEncounterConstants.Gameplay.InteractionTemplateId;
        public const string NativeEnemyInteractionEventGuid = ForceEncounterConstants.EventGuids.NativeEnemyInteraction;
        public const string EventGuid = ForceEncounterConstants.EventGuids.Entry;
        public const string NavigationOptionGuid = ForceEncounterConstants.Options.OpenGuid;
        public const string OptionGuid = ForceEncounterConstants.Options.ExecuteGuid;
        public const string OptionKey = ForceEncounterConstants.Options.ExecuteKey;
        public const string BattleSucceededParam = ForceEncounterConstants.Backend.BattleSucceeded;
        public const string ResolutionModeParam = ForceEncounterConstants.Backend.ResolutionMode;
        public const string ResolutionParam = ForceEncounterConstants.Backend.Resolution;
        public const int ResolutionModeProbe = ForceEncounterConstants.ResolutionMode.Probe;
        public const int ResolutionModeCombat = ForceEncounterConstants.ResolutionMode.Combat;
        public const int ResolutionModeAcceptedCommit = ForceEncounterConstants.ResolutionMode.AcceptedCommit;
        public const int ResolutionNeedCombatChoice = ForceEncounterConstants.ProbeResult.NeedCombatChoice;
        public const int ResolutionAccepted = ForceEncounterConstants.ProbeResult.Accepted;
    }
}
