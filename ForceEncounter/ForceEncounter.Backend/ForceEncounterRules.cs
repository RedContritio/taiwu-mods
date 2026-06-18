namespace ForceEncounter.Backend
{
    internal static class ForceEncounterRules
    {
        public static bool IsResolvedSuccess(bool battleSucceeded, bool targetIsTaiwu, short actorFertility)
        {
            return !targetIsTaiwu && battleSucceeded && actorFertility > 50;
        }
    }
}
