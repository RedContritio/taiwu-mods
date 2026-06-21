using System;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Item;
using GameData.Domains.TaiwuEvent.Enum;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Domains.TaiwuEvent.EventOption;

namespace ForceEncounter.Events
{
    internal sealed class ForceEncounterCapturedTargetDispositionEvent : TaiwuEventItem
    {
        public ForceEncounterCapturedTargetDispositionEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.事件.擒获处置);
            EventType = EEventType.ModEvent;
            IsHeadEvent = false;
            TriggerType = -1;
            MainRoleKey = ForceEncounterConstants.RoleKeys.Taiwu;
            TargetRoleKey = ForceEncounterConstants.ArgBox.NativePrisoner;
            EscOptionKey = ForceEncounterEventIds.选项.关押擒获目标.Key;
            EventOptions = new[]
            {
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.关押擒获目标.Key,
                    OptionGuid = ForceEncounterEventIds.选项.关押擒获目标.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OnOptionSelect = KeepCapturedTarget
                },
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.秘密关押擒获目标.Key,
                    OptionGuid = ForceEncounterEventIds.选项.秘密关押擒获目标.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OnOptionSelect = KeepCapturedTargetSecretly
                },
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.释放擒获目标.Key,
                    OptionGuid = ForceEncounterEventIds.选项.释放擒获目标.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OnOptionSelect = ReleaseCapturedTarget
                }
            };
            EventOptions[0].SetContent(ForceEncounterEventText.按钮.公开关押擒获目标);
            EventOptions[1].SetContent(ForceEncounterEventText.按钮.秘密关押擒获目标);
            EventOptions[2].SetContent(ForceEncounterEventText.按钮.放其离开擒获目标);
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
            ForceEncounterEventRuntime.DebugLog(GetRuntimeModId(), "CapturedTargetDisposition enter");
        }

        public override void OnEventExit()
        {
        }

        public override string GetReplacedContentString()
        {
            return EventHelper.HandleStringTag(
                ForceEncounterEventText.BuildCapturedTargetDispositionContent(),
                ArgBox,
                TaiwuEvent);
        }

        private string KeepCapturedTarget()
        {
            if (TryGetActorAndPrisoner(out Character actor, out Character prisoner))
            {
                RecordNativePublicKidnap(actor, prisoner);
                ForceEncounterEventRuntime.DebugLog(
                    GetRuntimeModId(),
                    "Captured target kept prisoner=" + prisoner.GetId() +
                    ", actor=" + actor.GetId());
            }
            else
            {
                ForceEncounterEventRuntime.DebugLog(GetRuntimeModId(), "Keep captured target skipped: missing actor/prisoner");
            }

            EventHelper.ToEvent(string.Empty);
            return string.Empty;
        }

        private string KeepCapturedTargetSecretly()
        {
            if (TryGetActorAndPrisoner(out Character actor, out Character prisoner))
            {
                RecordNativePrivateKidnap(actor, prisoner);
                ForceEncounterEventRuntime.DebugLog(
                    GetRuntimeModId(),
                    "Captured target kept secretly prisoner=" + prisoner.GetId() +
                    ", actor=" + actor.GetId());
            }
            else
            {
                ForceEncounterEventRuntime.DebugLog(GetRuntimeModId(), "Keep captured target secretly skipped: missing actor/prisoner");
            }

            EventHelper.ToEvent(string.Empty);
            return string.Empty;
        }

        private string ReleaseCapturedTarget()
        {
            if (TryGetActorAndPrisoner(out Character actor, out Character prisoner))
            {
                EventHelper.RemovePrisonerFromCharacter(prisoner.GetId(), actor.GetId(), false);
                EventHelper.HandleCombatResultReleaseEnemy(actor, prisoner);
                EventHelper.SetOnHandlingMonthlyEventBlock(false);
                ForceEncounterEventRuntime.DebugLog(
                    GetRuntimeModId(),
                    "Captured target released prisoner=" + prisoner.GetId() +
                    ", actor=" + actor.GetId());
            }
            else
            {
                ForceEncounterEventRuntime.DebugLog(GetRuntimeModId(), "Release captured target skipped: missing actor/prisoner");
            }

            EventHelper.ToEvent(string.Empty);
            return string.Empty;
        }

        private bool TryGetActorAndPrisoner(out Character actor, out Character prisoner)
        {
            actor = null;
            prisoner = null;
            int actorId = DomainManager.Taiwu.GetTaiwuCharId();
            int prisonerId = -1;
            if (ArgBox == null ||
                !ArgBox.Get(ForceEncounterConstants.ArgBox.NativePrisoner, ref prisonerId))
            {
                return false;
            }

            return DomainManager.Character.TryGetElement_Objects(actorId, out actor) &&
                   DomainManager.Character.TryGetElement_Objects(prisonerId, out prisoner);
        }

        private void RecordNativePublicKidnap(Character actor, Character prisoner)
        {
            ItemKey seizeItemKey = default;
            ArgBox.Get<ItemKey>(ForceEncounterConstants.ArgBox.NativeSeizeItemKey, out seizeItemKey);
            EventHelper.GetLifeRecordCollection().AddKidnapInPublic(
                actor.GetId(),
                EventHelper.GetGameDate(),
                prisoner.GetId(),
                actor.GetLocation(),
                seizeItemKey.ItemType,
                seizeItemKey.TemplateId);
            EventHelper.CreateSecretInformationMetaData(
                EventHelper.GetSecretInformationCollection().AddKidnapInPublic(actor.GetId(), prisoner.GetId()),
                true);
        }

        private void RecordNativePrivateKidnap(Character actor, Character prisoner)
        {
            ItemKey seizeItemKey = default;
            ArgBox.Get<ItemKey>(ForceEncounterConstants.ArgBox.NativeSeizeItemKey, out seizeItemKey);
            EventHelper.GetLifeRecordCollection().AddKidnapInPrivate(
                actor.GetId(),
                EventHelper.GetGameDate(),
                prisoner.GetId(),
                actor.GetLocation(),
                seizeItemKey.ItemType,
                seizeItemKey.TemplateId);
            EventHelper.CreateSecretInformationMetaData(
                EventHelper.GetSecretInformationCollection().AddKidnapInPrivate(actor.GetId(), prisoner.GetId()),
                true);
        }

        private static string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(ForceEncounterEventIds.事件.擒获处置);
        }
    }
}
