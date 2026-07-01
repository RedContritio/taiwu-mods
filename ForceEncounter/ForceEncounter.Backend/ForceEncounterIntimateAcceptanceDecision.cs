namespace ForceEncounter.Backend
{
    internal readonly struct ForceEncounterIntimateAcceptanceDecision
    {
        public ForceEncounterIntimateAcceptanceDecision(
            bool accepted,
            string reason,
            bool hasIntimateSource,
            int requiredFavorabilityType,
            bool targetFavorabilityPassed)
        {
            Accepted = accepted;
            Reason = reason;
            HasIntimateSource = hasIntimateSource;
            RequiredFavorabilityType = requiredFavorabilityType;
            TargetFavorabilityPassed = targetFavorabilityPassed;
        }

        public bool Accepted { get; }

        public string Reason { get; }

        public bool HasIntimateSource { get; }

        public int RequiredFavorabilityType { get; }

        public bool TargetFavorabilityPassed { get; }
    }
}
