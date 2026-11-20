using Verse;
namespace Honor.Contracts.Preview;
/// <summary>Only modules whose complete relevant native bodies were metadata-reviewed; reference signatures are not execution authority.</summary>
internal static class NativeTransferBodies
{
    internal static bool RoyalCustodyBodies => typeof(Thing).Module.ModuleVersionId == new Guid("3523c04c-c298-43ae-9571-20caaef22acc");
    internal static bool WorkObservationBodies => typeof(Thing).Module.ModuleVersionId == new Guid("3523c04c-c298-43ae-9571-20caaef22acc");
    internal static bool HaulFactionBodies => typeof(Thing).Module.ModuleVersionId == new Guid("eed7cbb5-7842-4c58-a9c5-47ec50d66c8c") ||
        typeof(Thing).Module.ModuleVersionId == new Guid("807b1578-71e0-4579-a898-49ecffd2762c") ||
        typeof(Thing).Module.ModuleVersionId == new Guid("c1b4bcf4-1ccc-4b94-b894-2abb586c6aa0");
    internal static bool HaulSurveyBodies => HaulFactionBodies ||
         typeof(Thing).Module.ModuleVersionId == new Guid("71185080-36f8-4a50-863e-e15aba81b822");
    // The haul survey modules match the reviewed Pawn and MapPawns roster reads.
    internal static bool SpawnedRosterRead => ExtendedReadBodies || HaulSurveyBodies;
    internal static bool ExtendedReadBodies => typeof(Thing).Module.ModuleVersionId == new Guid("6c268a95-d1a8-4d6f-ad81-e5dcf6949c0e") ||
        typeof(Thing).Module.ModuleVersionId == new Guid("3523c04c-c298-43ae-9571-20caaef22acc");
}
