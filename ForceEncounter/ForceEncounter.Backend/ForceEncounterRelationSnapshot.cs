namespace ForceEncounter.Backend
{
    internal readonly struct ForceEncounterRelationSnapshot
    {
        public ForceEncounterRelationSnapshot(
            bool isSpouse,
            bool isMutualLover,
            bool targetAdoresActor,
            bool targetIsDeepValleyCloseFriend,
            bool targetIsTaiwuVillager,
            bool targetHasExclusiveAttachmentToOther,
            int actorFavorabilityType,
            int targetFavorabilityType,
            int actorBehaviorType)
        {
            IsSpouse = isSpouse;
            IsMutualLover = isMutualLover;
            TargetAdoresActor = targetAdoresActor;
            TargetIsDeepValleyCloseFriend = targetIsDeepValleyCloseFriend;
            TargetIsTaiwuVillager = targetIsTaiwuVillager;
            TargetHasExclusiveAttachmentToOther = targetHasExclusiveAttachmentToOther;
            ActorFavorabilityType = actorFavorabilityType;
            TargetFavorabilityType = targetFavorabilityType;
            ActorBehaviorType = actorBehaviorType;
        }

        public bool IsSpouse { get; }

        public bool IsMutualLover { get; }

        public bool TargetAdoresActor { get; }

        public bool TargetIsDeepValleyCloseFriend { get; }

        public bool TargetIsTaiwuVillager { get; }

        public bool TargetHasExclusiveAttachmentToOther { get; }

        public int ActorFavorabilityType { get; }

        public int TargetFavorabilityType { get; }

        public int ActorBehaviorType { get; }

        public bool TargetUnilaterallyAdoresActor => TargetAdoresActor && !IsSpouse && !IsMutualLover;

        public bool DeepValleyCloseFriendHasOtherAttachment =>
            TargetIsDeepValleyCloseFriend && TargetHasExclusiveAttachmentToOther;
    }
}
