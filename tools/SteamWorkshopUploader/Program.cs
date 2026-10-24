using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;
using Steamworks;

if (args.Length < 2 || args[0] is not ("status" or "publish-public" or "collection" or "verify" or "verify-series" or "require" or "require-app"))
{
    Console.Error.WriteLine("Commands: status GAME | verify GAME STATE | publish-public GAME MOD STATE DESCRIPTION | collection GAME PREVIEW_DIR STATE DESCRIPTION | require GAME PARENT_ID CHILD_ID | require-app GAME ITEM_ID APP_ID"); return 2;
}
string gameDir = Path.GetFullPath(args[1]);
NativeLibrary.SetDllImportResolver(typeof(SteamAPI).Assembly, (name, _, _) =>
    name.Contains("steam_api") ? NativeLibrary.Load(Path.Combine(gameDir, "steam_api64.dll")) : IntPtr.Zero);
Environment.SetEnvironmentVariable("SteamAppId", "294100"); Environment.SetEnvironmentVariable("SteamGameId", "294100");
if (!SteamAPI.Init()) { Console.Error.WriteLine("Start Steam and sign in with the RimWorld owner account."); return 3; }
try
{
    if (!SteamUser.BLoggedOn() || !SteamApps.BIsSubscribedApp(new AppId_t(294100)) || SteamUtils.GetAppID().m_AppId != 294100)
        throw new InvalidOperationException("A signed-in Steam session owning RimWorld is required.");
    if (args[0] == "status") { Console.WriteLine("READY: current Steam session owns RimWorld."); return 0; }
    if (args[0] == "verify") { Verify(ReadState(args[2]).PublishedFileId, true); return 0; }
    if (args[0] == "verify-series")
    {
        foreach (var item in JsonSerializer.Deserialize<List<Publication>>(File.ReadAllText(args[2]))!) Verify(item.Id, true, item);
        Console.WriteLine("PASS: all public publications, Workshop requirements and DLC dependencies match manifest."); return 0;
    }
    if (args[0] is "require" or "require-app")
    {
        var parent = ulong.Parse(args[2]); Verify(parent, false);
        if (args[0] == "require")
        {
            var result = Wait<AddUGCDependencyResult_t>(() => SteamUGC.AddDependency(new PublishedFileId_t(parent), new PublishedFileId_t(ulong.Parse(args[3]))));
            Require(result.m_eResult, "AddDependency");
        }
        else
        {
            var result = Wait<AddAppDependencyResult_t>(() => SteamUGC.AddAppDependency(new PublishedFileId_t(parent), new AppId_t(uint.Parse(args[3]))));
            Require(result.m_eResult, "AddAppDependency");
        }
        Verify(parent, true); return 0;
    }
    if (args.Length != 5) throw new ArgumentException("Publishing requires GAME CONTENT_DIR STATE DESCRIPTION.");
    bool collection = args[0] == "collection";
    string modDir = Path.GetFullPath(args[2]), stateFile = Path.GetFullPath(args[3]);
    string title;
    var versions = new List<string> { "1.6", "1.7" };
    if (collection) title = "Honor Mod Series — 1.6 / 1.7 (English / 한국어)";
    else
    {
        var about = XDocument.Load(Path.Combine(modDir, "About", "About.xml")).Root!;
        var package = about.Element("packageId")!.Value;
        if (!package.StartsWith("honor.series.")) throw new InvalidOperationException("Only Honor series content is supported.");
        if (package == "honor.series.contracts.preview") throw new InvalidOperationException("Contracts is a code-validated preview; Workshop publication is deferred.");
        versions = about.Element("supportedVersions")!.Elements("li").Select(v => v.Value).Distinct().ToList();
        if (versions.Count == 0 || versions.Any(v => v is not ("1.6" or "1.7"))) throw new InvalidOperationException("Unverified game version metadata.");
        title = about.Element("name")!.Value + " — " + string.Join(" / ", versions) + " Experimental (EN/KO)";
        foreach (var version in versions)
            if (!File.Exists(Path.Combine(modDir, version, "Assemblies", about.Element("name")!.Value + ".dll"))) throw new InvalidOperationException("All advertised version builds are required.");
        if (Directory.EnumerateFiles(modDir, "*.dll", SearchOption.AllDirectories).Any(p => !Path.GetFileName(p).StartsWith("Honor."))) throw new InvalidOperationException("Foreign DLL in upload.");
        if (Directory.EnumerateFiles(modDir, "*.pdb", SearchOption.AllDirectories).Any()) throw new InvalidOperationException("Debug symbols in upload: run tools/Build.ps1 first.");
    }
    var record = File.Exists(stateFile) ? ReadState(stateFile) : new UploadState();
    string idPath = Path.Combine(modDir, "About", "PublishedFileId.txt");
    if (!collection && record.PublishedFileId == 0 && File.Exists(idPath)) record.PublishedFileId = ulong.Parse(File.ReadAllText(idPath).Trim());
    if (record.PublishedFileId == 0)
    {
        var result = Wait<CreateItemResult_t>(() => SteamUGC.CreateItem(new AppId_t(294100), collection ? EWorkshopFileType.k_EWorkshopFileTypeCollection : EWorkshopFileType.k_EWorkshopFileTypeFirst));
        Require(result.m_eResult, "CreateItem"); record.PublishedFileId = result.m_nPublishedFileId.m_PublishedFileId;
        record.LegalAgreementRequired = result.m_bUserNeedsToAcceptWorkshopLegalAgreement; WriteState(stateFile, record);
        Console.WriteLine("CREATED DRAFT: " + record.PublishedFileId);
        if (record.LegalAgreementRequired) throw new InvalidOperationException("WORKSHOP_LEGAL_AGREEMENT_REQUIRED: user must accept personally before continuing.");
    }
    else Verify(record.PublishedFileId, false);
    var update = SteamUGC.StartItemUpdate(new AppId_t(294100), new PublishedFileId_t(record.PublishedFileId));
    Need(SteamUGC.SetItemTitle(update, title), "title"); Need(SteamUGC.SetItemDescription(update, File.ReadAllText(args[4])), "description");
    Need(SteamUGC.SetItemVisibility(update, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic), "public visibility");
    Need(SteamUGC.SetItemTags(update, versions), "tag");
    if (!collection) Need(SteamUGC.SetItemContent(update, modDir), "content");
    var preview = Path.Combine(modDir, "About", "Preview.png");
    var workshopPreview = Path.Combine(modDir, "About", "Preview-Workshop.jpg");
    if (File.Exists(workshopPreview)) preview = workshopPreview;
    if (File.Exists(preview) && new FileInfo(preview).Length >= 1000000) throw new InvalidOperationException("Workshop preview must be below 1 MB.");
    if (File.Exists(preview)) Need(SteamUGC.SetItemPreview(update, preview), "preview");
    var submitted = Wait<SubmitItemUpdateResult_t>(() => SteamUGC.SubmitItemUpdate(update, "0.2.2 compatibility: separate RimWorld 1.6 / 1.7 builds; 1.6 code/reference validation only."));
    Require(submitted.m_eResult, "SubmitItemUpdate");
    record.LegalAgreementRequired = submitted.m_bUserNeedsToAcceptWorkshopLegalAgreement; record.UploadedUtc = DateTime.UtcNow; record.Visibility = "Public"; WriteState(stateFile, record);
    if (!collection) File.WriteAllText(idPath, record.PublishedFileId + "\n");
    if (record.LegalAgreementRequired) throw new InvalidOperationException("WORKSHOP_LEGAL_AGREEMENT_REQUIRED: user must accept personally.");
    Verify(record.PublishedFileId, true);
    Console.WriteLine("PUBLISHED: https://steamcommunity.com/sharedfiles/filedetails/?id=" + record.PublishedFileId); return 0;
}
catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
finally { SteamAPI.Shutdown(); }

static T Wait<T>(Func<SteamAPICall_t> action) where T : struct
{
    T result = default; bool completed = false, failed = false;
    using var callback = CallResult<T>.Create((data, ioFailure) => { result = data; failed = ioFailure; completed = true; });
    callback.Set(action()); long deadline = Environment.TickCount64 + 180000;
    while (!completed && Environment.TickCount64 < deadline) { SteamAPI.RunCallbacks(); Thread.Sleep(25); }
    if (!completed || failed) throw new InvalidOperationException("Steam timeout/I/O failure. Check persisted state before retrying."); return result;
}
static void Verify(ulong itemId, bool dependencies, Publication expected = null)
{
    var handle = SteamUGC.CreateQueryUGCDetailsRequest([new PublishedFileId_t(itemId)], 1);
    try
    {
        Need(SteamUGC.SetReturnChildren(handle, true), "return children");
        Require(Wait<SteamUGCQueryCompleted_t>(() => SteamUGC.SendQueryUGCRequest(handle)).m_eResult, "QueryUGC");
        Need(SteamUGC.GetQueryUGCResult(handle, 0, out var details), "query result"); Require(details.m_eResult, "UGC details");
        if (details.m_nConsumerAppID.m_AppId != 294100 || details.m_ulSteamIDOwner != SteamUser.GetSteamID().m_SteamID) throw new InvalidOperationException("Item is not owned by the current user for RimWorld.");
        var children = new PublishedFileId_t[details.m_unNumChildren];
        if (children.Length > 0) Need(SteamUGC.GetQueryUGCChildren(handle, 0, children, (uint)children.Length), "children");
        if (details.m_bBanned) throw new InvalidOperationException("Steam reports the item is banned: " + itemId);
        if (expected != null && (details.m_eVisibility != ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic ||
            !children.Select(c => c.m_PublishedFileId).ToHashSet().SetEquals(expected.Dependencies))) throw new InvalidOperationException("Publication/required item mismatch: " + expected.Name);
        Console.WriteLine($"VERIFIED: item={itemId} app=294100 owner=current-user visibility={details.m_eVisibility} banned={details.m_bBanned} type={details.m_eFileType} size={details.m_nFileSize} children={string.Join(',', children.Select(c => c.m_PublishedFileId))}");
        if (dependencies)
        {
            var apps = Wait<GetAppDependenciesResult_t>(() => SteamUGC.GetAppDependencies(new PublishedFileId_t(itemId)));
            Require(apps.m_eResult, "GetAppDependencies");
            if (expected != null && !apps.m_rgAppIDs.Take((int)apps.m_nNumAppDependencies).Select(a => a.m_AppId).ToHashSet().SetEquals(expected.AppDependencies)) throw new InvalidOperationException("DLC mismatch: " + expected.Name);
            Console.WriteLine("APP DEPENDENCIES: " + string.Join(',', apps.m_rgAppIDs.Take((int)apps.m_nNumAppDependencies).Select(a => a.m_AppId)));
        }
    }
    finally { SteamUGC.ReleaseQueryUGCRequest(handle); }
}
static void Need(bool ok, string field) { if (!ok) throw new InvalidOperationException("Steam rejected " + field); }
static void Require(EResult result, string operation) { if (result != EResult.k_EResultOK) throw new InvalidOperationException(operation + ": " + result); }
static UploadState ReadState(string path) => JsonSerializer.Deserialize<UploadState>(File.ReadAllText(path)) ?? throw new InvalidOperationException("Invalid state.");
static void WriteState(string path, UploadState state) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true })); }
public sealed class UploadState
{
    public uint AppId { get; set; } = 294100;
    public ulong PublishedFileId { get; set; }
    public string Visibility { get; set; } = "Private";
    public bool LegalAgreementRequired { get; set; }
    public DateTime? UploadedUtc { get; set; }
}
public sealed class Publication
{
    public string Name { get; set; }
    public ulong Id { get; set; }
    public ulong[] Dependencies { get; set; } = [];
    public uint[] AppDependencies { get; set; } = [];
}
