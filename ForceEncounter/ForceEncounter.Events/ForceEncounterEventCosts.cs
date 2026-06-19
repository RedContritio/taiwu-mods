using System.Collections.Generic;
using GameData.Domains.TaiwuEvent.EventOption;

namespace ForceEncounter.Events
{
    internal static class ForceEncounterEventCosts
    {
        public static List<OptionConsumeInfo> BuildPreviewCosts()
        {
            return new List<OptionConsumeInfo>
            {
                new OptionConsumeInfo((sbyte)8, 5, false)
            };
        }

        public static List<TaiwuEventOptionConditionBase> BuildCommitConditions()
        {
            return new List<TaiwuEventOptionConditionBase>
            {
                new OptionConditionSbyte((short)2, (sbyte)5, OptionConditionMatcher.MovePointMore)
            };
        }

        public static List<OptionConsumeInfo> BuildCommitCosts()
        {
            return new List<OptionConsumeInfo>
            {
                new OptionConsumeInfo((sbyte)8, 5, true)
            };
        }
    }
}
