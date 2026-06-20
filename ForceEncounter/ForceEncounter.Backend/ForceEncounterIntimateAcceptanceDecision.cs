namespace ForceEncounter.Backend
{
    internal readonly struct ForceEncounterIntimateAcceptanceDecision
    {
        public ForceEncounterIntimateAcceptanceDecision(
            bool accepted,
            string reason,
            bool hasIntimateSource,
            bool deepValleyAttachedRule,
            bool villagerLeniencyApplied,
            bool nonLoverMinimumApplied,
            int baseRequiredFavorabilityType,
            int requiredFavorabilityType,
            bool actorFavorabilityPassed,
            bool targetFavorabilityPassed)
        {
            Accepted = accepted;
            Reason = reason;
            HasIntimateSource = hasIntimateSource;
            DeepValleyAttachedRule = deepValleyAttachedRule;
            VillagerLeniencyApplied = villagerLeniencyApplied;
            NonLoverMinimumApplied = nonLoverMinimumApplied;
            BaseRequiredFavorabilityType = baseRequiredFavorabilityType;
            RequiredFavorabilityType = requiredFavorabilityType;
            ActorFavorabilityPassed = actorFavorabilityPassed;
            TargetFavorabilityPassed = targetFavorabilityPassed;
        }

        public bool Accepted { get; }

        public string Reason { get; }

        public bool HasIntimateSource { get; }

        public bool DeepValleyAttachedRule { get; }

        public bool VillagerLeniencyApplied { get; }

        public bool NonLoverMinimumApplied { get; }

        public int BaseRequiredFavorabilityType { get; }

        public int RequiredFavorabilityType { get; }

        public bool ActorFavorabilityPassed { get; }

        public bool TargetFavorabilityPassed { get; }
    }
}
