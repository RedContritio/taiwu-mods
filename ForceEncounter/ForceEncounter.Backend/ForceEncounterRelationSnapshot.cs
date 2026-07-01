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
            int targetFavorabilityType)
        {
            IsSpouse = isSpouse;
            IsMutualLover = isMutualLover;
            TargetAdoresActor = targetAdoresActor;
            TargetIsDeepValleyCloseFriend = targetIsDeepValleyCloseFriend;
            TargetIsTaiwuVillager = targetIsTaiwuVillager;
            TargetHasExclusiveAttachmentToOther = targetHasExclusiveAttachmentToOther;
            TargetFavorabilityType = targetFavorabilityType;
        }

        public bool IsSpouse { get; }

        public bool IsMutualLover { get; }

        public bool TargetAdoresActor { get; }

        public bool TargetIsDeepValleyCloseFriend { get; }

        public bool TargetIsTaiwuVillager { get; }

        public bool TargetHasExclusiveAttachmentToOther { get; }

        public int TargetFavorabilityType { get; }

        public bool TargetUnilaterallyAdoresActor => TargetAdoresActor && !IsSpouse && !IsMutualLover;

        public bool DeepValleyCloseFriendHasOtherAttachment =>
            TargetIsDeepValleyCloseFriend && TargetHasExclusiveAttachmentToOther;
    }
}
