using ForceEncounter.Shared;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Map;
using GameData.Domains.TaiwuEvent.EventHelper;

namespace ForceEncounter.Backend
{
    internal static class ForceEncounterEffectApplier
    {
        public static void ApplySuccess(
            DataContext context,
            Character actor,
            Character target,
            bool addHatredRelation,
            int favorabilityDelta,
            bool createSecret)
        {
            int actorId = actor.GetId();
            int targetId = target.GetId();
            int currDate = DomainManager.World.GetCurrDate();
            Location location = actor.GetLocation();

            actor.MakeLove(context, target, isRape: true);
            DomainManager.LifeRecord.GetLifeRecordCollection().AddRapeSucceed(actorId, currDate, targetId, location);

            if (addHatredRelation)
            {
                ApplyNativeBecomeEnemy(target, actor);
            }

            ApplyForcedFavorabilityDelta(context, target, actor, favorabilityDelta);

            if (createSecret)
            {
                int dataOffset = DomainManager.Information.GetSecretInformationCollection().AddRape(actorId, targetId);
                DomainManager.Information.AddSecretInformation(context, dataOffset);
            }
        }

        public static void ApplyFailure(
            DataContext context,
            Character actor,
            Character target,
            bool addHatredRelation,
            int favorabilityDelta)
        {
            int actorId = actor.GetId();
            int targetId = target.GetId();
            int currDate = DomainManager.World.GetCurrDate();
            Location location = actor.GetLocation();
            if (targetId == DomainManager.Taiwu.GetTaiwuCharId())
            {
                DomainManager.World.GetMonthlyNotificationCollection().AddRapeFailure(actorId, location, targetId);
            }

            DomainManager.LifeRecord.GetLifeRecordCollection().AddRapeFail(actorId, currDate, targetId, location);
            if (addHatredRelation)
            {
                ApplyNativeBecomeEnemy(target, actor);
            }

            ApplyForcedFavorabilityDelta(context, target, actor, favorabilityDelta);
        }

        private static void ApplyNativeBecomeEnemy(Character target, Character actor)
        {
            EventHelper.ApplyRelationBecomeEnemy(target, actor);
        }

        private static void ApplyForcedFavorabilityDelta(
            DataContext context,
            Character target,
            Character actor,
            int favorabilityDelta)
        {
            if (favorabilityDelta == 0)
            {
                return;
            }

            DomainManager.Character.ChangeFavorabilityOptionalMonthlyEvolution(context, target, actor, favorabilityDelta);
        }
    }
}
