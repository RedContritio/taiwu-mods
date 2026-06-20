using System.Collections.Generic;
using ForceEncounter.Shared;
using GameData.Domains.TaiwuEvent.EventOption;
using TaiwuMod.Common;

namespace ForceEncounter.Events
{
    internal static class ForceEncounterEventCosts
    {
        public static List<OptionConsumeInfo> BuildPreviewCosts(string modId)
        {
            int days = GetActionTimeCostDays(modId);
            if (days == 0)
            {
                return new List<OptionConsumeInfo>();
            }

            return new List<OptionConsumeInfo>
            {
                new OptionConsumeInfo(ForceEncounterConstants.Costs.ActionTimeConsumeType, days, false)
            };
        }

        public static List<TaiwuEventOptionConditionBase> BuildCommitConditions(string modId)
        {
            int days = GetActionTimeCostDays(modId);
            if (days == 0)
            {
                return new List<TaiwuEventOptionConditionBase>();
            }

            return new List<TaiwuEventOptionConditionBase>
            {
                new OptionConditionSbyte(ForceEncounterConstants.Costs.ActionPointConditionType, (sbyte)days, OptionConditionMatcher.MovePointMore)
            };
        }

        public static List<OptionConsumeInfo> BuildCommitCosts(string modId)
        {
            int days = GetActionTimeCostDays(modId);
            if (days == 0)
            {
                return new List<OptionConsumeInfo>();
            }

            return new List<OptionConsumeInfo>
            {
                new OptionConsumeInfo(ForceEncounterConstants.Costs.ActionTimeConsumeType, days, true)
            };
        }

        public static int GetActionTimeCostDays(string modId)
        {
            return TaiwuModSettings.GetClampedInt(
                modId,
                ForceEncounterConstants.Settings.ActionTimeCostDays,
                ForceEncounterConstants.Costs.DefaultActionTimeDays,
                ForceEncounterConstants.Costs.MinActionTimeDays,
                ForceEncounterConstants.Costs.MaxActionTimeDays);
        }
    }
}
