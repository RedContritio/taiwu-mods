using System.Collections.Generic;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Domains.TaiwuEvent.EventHelper;

namespace ForceEncounter.Events
{
    public sealed class ForceEncounterEventPackage : EventPackage
    {
        public ForceEncounterEventPackage()
        {
            NameSpace = ForceEncounterConstants.Mod.Id;
            Author = ForceEncounterConstants.Mod.Author;
            Group = ForceEncounterConstants.Mod.EventGroup;
            EventList = new List<TaiwuEventItem>
            {
                new ForceEncounterEvent(),
                new ForceEncounterConsentChoiceEvent(),
                new ForceEncounterGuardInterceptEvent(),
                new ForceEncounterCombatResultEvent(),
                new ForceEncounterCapturedTargetDispositionEvent(),
                new ForceEncounterAcceptedResultEvent(),
                new ForceEncounterPrisonerEntryEvent(),
                new ForceEncounterPrisonerConsentChoiceEvent(),
                new ForceEncounterPrisonerForcedResultEvent(),
                new ForceEncounterPrisonerAcceptedResultEvent()
            };

            foreach (TaiwuEventItem item in EventList)
            {
                item.Package = this;
            }

            EventHelper.AddOptionToEvent(
                ForceEncounterEventIds.事件.原生敌对菜单,
                ForceEncounterEventIds.事件.外层入口,
                ForceEncounterEventIds.选项.情难自已.Key);

            EventHelper.AddOptionToEvent(
                ForceEncounterEventIds.事件.原生关押菜单,
                ForceEncounterEventIds.事件.关押外层入口,
                ForceEncounterEventIds.选项.情难自已关押.Key);
        }
    }
}
