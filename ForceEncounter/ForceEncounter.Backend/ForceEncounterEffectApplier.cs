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

            BackendPlugin.DebugLog(
                "ApplySuccess begin actor=" + actorId +
                ", target=" + targetId +
                ", addHatredRelation=" + addHatredRelation +
                ", favorabilityDelta=" + favorabilityDelta +
                ", createSecret=" + createSecret +
                ", currDate=" + currDate);
            actor.MakeLove(context, target, isRape: true);
            BackendPlugin.DebugLog("ApplySuccess MakeLove isRape=True actor=" + actorId + ", target=" + targetId);
            DomainManager.LifeRecord.GetLifeRecordCollection().AddRapeSucceed(actorId, currDate, targetId, location);
            BackendPlugin.DebugLog("ApplySuccess LifeRecord=RapeSucceed actor=" + actorId + ", target=" + targetId);

            if (addHatredRelation)
            {
                ApplyNativeBecomeEnemy(target, actor);
            }

            ApplyForcedFavorabilityDelta(context, target, actor, favorabilityDelta);

            if (createSecret)
            {
                int dataOffset = DomainManager.Information.GetSecretInformationCollection().AddRape(actorId, targetId);
                DomainManager.Information.AddSecretInformation(context, dataOffset);
                BackendPlugin.DebugLog(
                    "ApplySuccess SecretInformation=Rape actor=" + actorId +
                    ", target=" + targetId +
                    ", dataOffset=" + dataOffset);
            }

            BackendPlugin.DebugLog("ApplySuccess end actor=" + actorId + ", target=" + targetId);
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
            BackendPlugin.DebugLog(
                "ApplyFailure begin actor=" + actorId +
                ", target=" + targetId +
                ", addHatredRelation=" + addHatredRelation +
                ", favorabilityDelta=" + favorabilityDelta +
                ", currDate=" + currDate);
            if (targetId == DomainManager.Taiwu.GetTaiwuCharId())
            {
                DomainManager.World.GetMonthlyNotificationCollection().AddRapeFailure(actorId, location, targetId);
                BackendPlugin.DebugLog("ApplyFailure MonthlyNotification=RapeFailure actor=" + actorId + ", target=" + targetId);
            }

            DomainManager.LifeRecord.GetLifeRecordCollection().AddRapeFail(actorId, currDate, targetId, location);
            BackendPlugin.DebugLog("ApplyFailure LifeRecord=RapeFail actor=" + actorId + ", target=" + targetId);
            if (addHatredRelation)
            {
                ApplyNativeBecomeEnemy(target, actor);
            }

            ApplyForcedFavorabilityDelta(context, target, actor, favorabilityDelta);
            BackendPlugin.DebugLog("ApplyFailure end actor=" + actorId + ", target=" + targetId);
        }

        private static void ApplyNativeBecomeEnemy(Character target, Character actor)
        {
            BackendPlugin.DebugLog(
                "ApplyNativeBecomeEnemy target=" + target.GetId() +
                ", actor=" + actor.GetId());
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
                BackendPlugin.DebugLog(
                    "ApplyFavorability skipped target=" + target.GetId() +
                    ", actor=" + actor.GetId() +
                    ", delta=0");
                return;
            }

            BackendPlugin.DebugLog(
                "ApplyFavorability target=" + target.GetId() +
                ", actor=" + actor.GetId() +
                ", delta=" + favorabilityDelta +
                ", path=ChangeFavorabilityOptionalMonthlyEvolution");
            DomainManager.Character.ChangeFavorabilityOptionalMonthlyEvolution(context, target, actor, favorabilityDelta);
        }
    }
}
