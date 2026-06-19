using System.Collections.Generic;
using Config.EventConfig;
using GameData.Domains.TaiwuEvent.EventHelper;

namespace ForceEncounter.Events
{
    public sealed class ForceEncounterEventPackage : EventPackage
    {
        public ForceEncounterEventPackage()
        {
            NameSpace = "ForceEncounter";
            Author = "RedContritio";
            Group = "Interaction";
            EventList = new List<TaiwuEventItem>
            {
                new ForceEncounterEvent(),
                new ForceEncounterConsentChoiceEvent(),
                new ForceEncounterGuardInterceptEvent(),
                new ForceEncounterCombatResultEvent()
            };

            foreach (TaiwuEventItem item in EventList)
            {
                item.Package = this;
            }

            EventHelper.AddOptionToEvent(
                ForceEncounterEventIds.事件.原生敌对菜单,
                ForceEncounterEventIds.事件.外层入口,
                ForceEncounterEventIds.选项.情难自已.Key);
        }
    }
}
