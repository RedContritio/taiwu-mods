using System.Collections.Generic;
using ForceEncounter.Shared;
using GameData.Domains.TaiwuEvent.EventOption;

namespace ForceEncounter.Events
{
    internal static class ForceEncounterEventCosts
    {
        public static List<OptionConsumeInfo> BuildPreviewCosts()
        {
            return new List<OptionConsumeInfo>
            {
                new OptionConsumeInfo(ForceEncounterConstants.Costs.ActionTimeConsumeType, ForceEncounterConstants.Costs.ActionTimeDays, false)
            };
        }

        public static List<TaiwuEventOptionConditionBase> BuildCommitConditions()
        {
            return new List<TaiwuEventOptionConditionBase>
            {
                new OptionConditionSbyte(ForceEncounterConstants.Costs.ActionPointConditionType, ForceEncounterConstants.Costs.ActionPointConditionValue, OptionConditionMatcher.MovePointMore)
            };
        }

        public static List<OptionConsumeInfo> BuildCommitCosts()
        {
            return new List<OptionConsumeInfo>
            {
                new OptionConsumeInfo(ForceEncounterConstants.Costs.ActionTimeConsumeType, ForceEncounterConstants.Costs.ActionTimeDays, true)
            };
        }
    }
}
