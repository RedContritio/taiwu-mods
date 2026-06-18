using System;
using System.Collections.Generic;
using Config;
using Config.EventConfig;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Mod;
using GameData.Domains.Combat;
using GameData.Domains.TaiwuEvent.Enum;
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
                new ForceEncounterCombatResultEvent()
            };
        }
    }

    internal sealed class ForceEncounterEvent : TaiwuEventItem
    {
        public ForceEncounterEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.EventGuid);
            EventType = EEventType.ModEvent;
            IsHeadEvent = false;
            TriggerType = -1;
            MainRoleKey = "RoleTaiwu";
            TargetRoleKey = EventTriggerParameter.DefValue.CharacterId.ArgBoxKey;
            EscOptionKey = string.Empty;
            EventOptions = new[]
            {
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.OptionKey,
                    OptionGuid = ForceEncounterEventIds.OptionGuid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = true,
                    OnOptionVisibleCheck = IsEnabled,
                    OnOptionAvailableCheck = CanExecute,
                    OnOptionSelect = Execute
                }
            };
            EventOptions[0].SetContent("情难自已");
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
        }

        public override void OnEventExit()
        {
        }

        public override string GetReplacedContentString()
        {
            return string.Empty;
        }

        private bool IsEnabled()
        {
            bool enabled = true;
            DomainManager.Mod.GetSetting(ForceEncounterEventIds.ModId, "Enabled", ref enabled);
            return enabled;
        }

        private bool CanExecute()
        {
            if (!IsEnabled() || ArgBox == null)
            {
                return false;
            }

            int targetId = -1;
            if (!ArgBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId))
            {
                return false;
            }

            int actorId = DomainManager.Taiwu.GetTaiwuCharId();
            if (actorId == targetId)
            {
                return false;
            }

            bool allowTaiwuAsTarget = false;
            DomainManager.Mod.GetSetting(ForceEncounterEventIds.ModId, "AllowTaiwuAsTarget", ref allowTaiwuAsTarget);
            if (!allowTaiwuAsTarget && targetId == actorId)
            {
                return false;
            }

            if (!DomainManager.Character.TryGetElement_Objects(actorId, out var actor) ||
                !DomainManager.Character.TryGetElement_Objects(targetId, out var target))
            {
                return false;
            }

            return DomainManager.Character.IsCharacterAlive(actorId) &&
                   DomainManager.Character.IsCharacterAlive(targetId) &&
                   actor.GetAgeGroup() == 2 &&
                   target.GetAgeGroup() == 2;
        }

        private string Execute()
        {
            int targetId = -1;
            if (ArgBox == null || !ArgBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId))
            {
                return string.Empty;
            }

            int actorId = DomainManager.Taiwu.GetTaiwuCharId();
            ArgBox.Set(ForceEncounterEventIds.ActorArgKey, actorId);
            ArgBox.Set(ForceEncounterEventIds.TargetArgKey, targetId);

            EventHelper.StartCombat(
                targetId,
                CombatConfig.DefKey.DieNormal,
                ForceEncounterEventIds.CombatResultEventGuid,
                ArgBox,
                true);
            EventHelper.SetStopAutoNextEvent(true);

            return string.Empty;
        }
    }

    internal sealed class ForceEncounterCombatResultEvent : TaiwuEventItem
    {
        public ForceEncounterCombatResultEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.CombatResultEventGuid);
            EventType = EEventType.ModEvent;
            IsHeadEvent = false;
            TriggerType = -1;
            MainRoleKey = "RoleTaiwu";
            TargetRoleKey = EventTriggerParameter.DefValue.CharacterId.ArgBoxKey;
            EscOptionKey = string.Empty;
            EventOptions = Array.Empty<TaiwuEventOption>();
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
            EventHelper.SetStopAutoNextEvent(false);

            int actorId = DomainManager.Taiwu.GetTaiwuCharId();
            int targetId = -1;
            ArgBox?.Get(ForceEncounterEventIds.ActorArgKey, ref actorId);
            if (ArgBox == null ||
                !ArgBox.Get(ForceEncounterEventIds.TargetArgKey, ref targetId) &&
                !ArgBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId))
            {
                EventHelper.ToEvent(string.Empty);
                return;
            }

            sbyte combatResult = CombatResultType.EnemyWin;
            bool battleSucceeded = ArgBox.Get("CombatResult", ref combatResult) &&
                                   CombatResultType.IsPlayerWin(combatResult);

            var parameter = new SerializableModData();
            parameter.Set("ActorId", actorId);
            parameter.Set("TargetId", targetId);
            parameter.Set(ForceEncounterEventIds.BattleSucceededParam, battleSucceeded);
            DomainManager.Mod.CallModMethodWithParamAndRet(
                DataContextManager.GetCurrentThreadDataContext(),
                ForceEncounterEventIds.ModId,
                ForceEncounterEventIds.ExecuteMethod,
                parameter);

            EventHelper.ToEvent(string.Empty);
        }

        public override void OnEventExit()
        {
        }

        public override string GetReplacedContentString()
        {
            return string.Empty;
        }
    }
}
