using System.Runtime.InteropServices;
using System.Text.Json;
using Steamworks;

if (args.Length < 2 || args[0] is not ("status" or "publish-private" or "verify"))
{
    Console.Error.WriteLine("Usage: status GAME_DIR | publish-private GAME_DIR MOD_DIR STATE_FILE DESCRIPTION_FILE | verify GAME_DIR STATE_FILE");
    return 2;
}
string gameDir = Path.GetFullPath(args[1]);
NativeLibrary.SetDllImportResolver(typeof(SteamAPI).Assembly, (name, assembly, search) =>
    name.Contains("steam_api") ? NativeLibrary.Load(Path.Combine(gameDir, "steam_api64.dll")) : IntPtr.Zero);
Environment.SetEnvironmentVariable("SteamAppId", "294100");
Environment.SetEnvironmentVariable("SteamGameId", "294100");
if (!SteamAPI.Init()) { Console.Error.WriteLine("Steam API could not initialize. Start Steam and sign in with the RimWorld owner account."); return 3; }
try
{
    if (!SteamUser.BLoggedOn() || !SteamApps.BIsSubscribedApp(new AppId_t(294100)) || SteamUtils.GetAppID().m_AppId != 294100)
        throw new InvalidOperationException("A signed-in Steam session owning RimWorld (294100) is required.");
    if (args[0] == "status") { Console.WriteLine("READY: logged-in Steam session owns RimWorld; native API initialized for 294100."); return 0; }
    if (args[0] == "verify")
    {
        var state = ReadState(args[2]); Verify(state.PublishedFileId); return 0;
    }
    if (args.Length != 5) throw new ArgumentException("publish-private requires GAME_DIR MOD_DIR STATE_FILE DESCRIPTION_FILE.");
    string modDir = Path.GetFullPath(args[2]), stateFile = Path.GetFullPath(args[3]);
    if (!File.Exists(Path.Combine(modDir, "About", "About.xml")) || !File.Exists(Path.Combine(modDir, "1.7", "Assemblies", "Honor.Core.dll")))
        throw new InvalidOperationException("Built Honor.Core mod directory required.");
    var record = File.Exists(stateFile) ? ReadState(stateFile) : new UploadState();
    string publishedIdPath = Path.Combine(modDir, "About", "PublishedFileId.txt");
    if (record.PublishedFileId == 0 && File.Exists(publishedIdPath))
    {
        record.PublishedFileId = ulong.Parse(File.ReadAllText(publishedIdPath).Trim());
        WriteState(stateFile, record);
    }
    if (record.PublishedFileId == 0)
    {
        var created = Wait<CreateItemResult_t>(() => SteamUGC.CreateItem(new AppId_t(294100), EWorkshopFileType.k_EWorkshopFileTypeFirst));
        Require(created.m_eResult, "CreateItem");
        record.PublishedFileId = created.m_nPublishedFileId.m_PublishedFileId;
        record.LegalAgreementRequired = created.m_bUserNeedsToAcceptWorkshopLegalAgreement;
        WriteState(stateFile, record); // Save ID BEFORE uploading so a retry cannot create a duplicate.
        Console.WriteLine("CREATED PRIVATE DRAFT: " + record.PublishedFileId);
        if (record.LegalAgreementRequired) throw new InvalidOperationException("WORKSHOP_LEGAL_AGREEMENT_REQUIRED: Open https://steamcommunity.com/sharedfiles/workshoplegalagreement and accept personally, then retry using the saved state file.");
    }
    else Verify(record.PublishedFileId);
    var id = new PublishedFileId_t(record.PublishedFileId);
    var update = SteamUGC.StartItemUpdate(new AppId_t(294100), id);
    Need(SteamUGC.SetItemTitle(update, "Honor.Core — Experimental MVP (1.7)"), "title");
    Need(SteamUGC.SetItemDescription(update, File.ReadAllText(args[4])), "description");
    Need(SteamUGC.SetItemVisibility(update, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate), "private visibility");
    Need(SteamUGC.SetItemTags(update, new List<string> { "1.7" }), "version tag");
    Need(SteamUGC.SetItemContent(update, modDir), "content");
    string preview = Path.Combine(modDir, "About", "Preview.png");
    if (File.Exists(preview)) Need(SteamUGC.SetItemPreview(update, preview), "preview");
    var submitted = Wait<SubmitItemUpdateResult_t>(() => SteamUGC.SubmitItemUpdate(update, "Private development test: English/Korean localization and local installation."));
    Require(submitted.m_eResult, "SubmitItemUpdate");
    record.LegalAgreementRequired = submitted.m_bUserNeedsToAcceptWorkshopLegalAgreement;
    record.UploadedUtc = DateTime.UtcNow;
    WriteState(stateFile, record);
    Verify(record.PublishedFileId);
    File.WriteAllText(Path.Combine(modDir, "About", "PublishedFileId.txt"), record.PublishedFileId.ToString());
    Console.WriteLine("PUBLISHED PRIVATE: https://steamcommunity.com/sharedfiles/filedetails/?id=" + record.PublishedFileId);
    if (record.LegalAgreementRequired) { Console.Error.WriteLine("Workshop legal agreement still requires the user's personal acceptance."); return 4; }
    return 0;
}
catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
finally { SteamAPI.Shutdown(); }

static T Wait<T>(Func<SteamAPICall_t> action) where T : struct
{
    T result = default; bool completed = false, failed = false;
    using var callback = CallResult<T>.Create((data, ioFailure) => { result = data; failed = ioFailure; completed = true; });
    callback.Set(action());
    long deadline = Environment.TickCount64 + 180000;
    while (!completed && Environment.TickCount64 < deadline) { SteamAPI.RunCallbacks(); Thread.Sleep(25); }
    if (!completed || failed) throw new InvalidOperationException("Steam request timed out or reported an I/O failure. Check saved item state before retrying.");
    return result;
}
static void Verify(ulong itemId)
{
    var result = Wait<SteamUGCRequestUGCDetailsResult_t>(() => SteamUGC.RequestUGCDetails(new PublishedFileId_t(itemId), 0));
    var details = result.m_details;
    Require(details.m_eResult, "RequestUGCDetails");
    if (details.m_nConsumerAppID.m_AppId != 294100 || details.m_ulSteamIDOwner != SteamUser.GetSteamID().m_SteamID)
        throw new InvalidOperationException("Existing item is not owned by the current Steam user for RimWorld.");
    if (details.m_eVisibility != ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate)
        throw new InvalidOperationException("Existing item is not private. Refusing to change a non-private publication with this test tool.");
    Console.WriteLine($"VERIFIED: item={itemId} app=294100 owner=current-user visibility=Private size={details.m_nFileSize}");
}
static void Need(bool ok, string field) { if (!ok) throw new InvalidOperationException("Steam rejected " + field); }
static void Require(EResult result, string operation) { if (result != EResult.k_EResultOK) throw new InvalidOperationException(operation + ": " + result); }
static UploadState ReadState(string path) => JsonSerializer.Deserialize<UploadState>(File.ReadAllText(path)) ?? throw new InvalidOperationException("Invalid upload state.");
static void WriteState(string path, UploadState state)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
}
public sealed class UploadState
{
    public uint AppId { get; set; } = 294100;
    public ulong PublishedFileId { get; set; }
    public string Visibility { get; set; } = "Private";
    public bool LegalAgreementRequired { get; set; }
    public DateTime? UploadedUtc { get; set; }
}
