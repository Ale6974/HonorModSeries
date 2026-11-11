using Verse;
namespace Honor.Contracts.Preview;
/// <summary>Only modules whose complete relevant native bodies were metadata-reviewed; reference signatures are not execution authority.</summary>
internal static class NativeTransferBodies
{
    internal static bool WorkObservationBodies => typeof(Thing).Module.ModuleVersionId == new Guid("3523c04c-c298-43ae-9571-20caaef22acc");
    internal static bool ExtendedReadBodies => typeof(Thing).Module.ModuleVersionId == new Guid("6c268a95-d1a8-4d6f-ad81-e5dcf6949c0e") ||
        typeof(Thing).Module.ModuleVersionId == new Guid("3523c04c-c298-43ae-9571-20caaef22acc");
}
