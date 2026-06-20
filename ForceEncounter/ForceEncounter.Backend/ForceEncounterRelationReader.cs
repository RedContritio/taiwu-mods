using ForceEncounter.Shared;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Character.Relation;

namespace ForceEncounter.Backend
{
    internal static class ForceEncounterRelationReader
    {
        public static bool TryCreateSnapshot(
            Character actor,
            Character target,
            out ForceEncounterRelationSnapshot snapshot)
        {
            snapshot = default;
            int actorId = actor.GetId();
            int targetId = target.GetId();
            if (!DomainManager.Character.TryGetRelation(actorId, targetId, out RelatedCharacter actorToTarget) ||
                !DomainManager.Character.TryGetRelation(targetId, actorId, out RelatedCharacter targetToActor))
            {
                return false;
            }

            bool actorAdoresTarget = RelationType.HasRelation(actorToTarget.RelationType, ForceEncounterConstants.Relations.Adored);
            bool targetAdoresActor = RelationType.HasRelation(targetToActor.RelationType, ForceEncounterConstants.Relations.Adored);
            snapshot = new ForceEncounterRelationSnapshot(
                isSpouse: RelationType.HasRelation(actorToTarget.RelationType, ForceEncounterConstants.Relations.Spouse) &&
                          RelationType.HasRelation(targetToActor.RelationType, ForceEncounterConstants.Relations.Spouse),
                isMutualLover: actorAdoresTarget && targetAdoresActor,
                targetAdoresActor: targetAdoresActor,
                targetIsDeepValleyCloseFriend: IsDeepValleyCloseFriendToActor(actor, target),
                targetIsTaiwuVillager: IsTaiwuVillager(target),
                targetHasExclusiveAttachmentToOther: HasExclusiveLivingAttachmentToOther(target, actorId),
                actorFavorabilityType: actorToTarget.GetFavorabilityType(),
                targetFavorabilityType: targetToActor.GetFavorabilityType(),
                actorBehaviorType: actor.GetBehaviorType());
            return true;
        }

        public static bool IsTaiwuVillager(Character character)
        {
            return character.GetOrganizationInfo().OrgTemplateId == ForceEncounterConstants.Gameplay.TaiwuVillageOrgTemplateId;
        }

        private static bool IsDeepValleyCloseFriendToActor(
            Character actor,
            Character target)
        {
            return target.GetFeatureIds().Contains(ForceEncounterConstants.Features.DeepValleyCloseFriend) &&
                   actor.GetId() == DomainManager.Taiwu.GetTaiwuCharIdForCloseFriend();
        }

        private static bool HasExclusiveLivingAttachmentToOther(Character target, int actorId)
        {
            int targetId = target.GetId();
            int spouseId = DomainManager.Character.GetAliveSpouse(targetId);
            if (spouseId >= 0 && spouseId != actorId)
            {
                return true;
            }

            int livingAdoredCount = 0;
            bool onlyLivingAdoredIsActor = false;
            foreach (int adoredCharId in DomainManager.Character.GetRelatedCharIds(targetId, ForceEncounterConstants.Relations.Adored))
            {
                if (!DomainManager.Character.IsCharacterAlive(adoredCharId))
                {
                    continue;
                }

                livingAdoredCount++;
                onlyLivingAdoredIsActor = adoredCharId == actorId;
                if (livingAdoredCount > 1)
                {
                    return false;
                }
            }

            return livingAdoredCount == 1 && !onlyLivingAdoredIsActor;
        }
    }
}
