# RimWorld 1.6 / 1.7 support

2026-10-25: Core/Society/Nobility/Diplomacy/Oaths 0.2.2 and Contracts Preview 0.3.10 have separate version builds. This is code/reference compatibility, not new gameplay proof. Current Steam items remain at 0.2.1; source and the local bundle contain these changes.

| Target | API source | Framework | Harmony inspected | Execution evidence |
|---|---|---|---|---|
| 1.6 | Krafs.Rimworld.Ref 1.6.4871, pinned | net472 | installed 1.6 DLL, 2.4.3 | rules on Windows .NET Framework; no RimWorld/Mono execution |
| 1.7 | installed 1.7.4985 rev1269 | net10.0 | installed 1.7 DLL, 3.0.0-rc.3 | rules on .NET 10; no new game execution |

[RimRef](https://github.com/krafs/RimRef) is a signature-only reference package, published with Ludeon permission. Its 1.6 version maps to the game version and targets net472. It is not an installed 1.6 game, implementation decompilation or runtime validation. The installed game remains on 1.7; it was not switched or overwritten.

## Build and layout

Use a .NET 10 SDK and GamePaths.props copied from the example. Set RimWorldDir/HarmonyDll for the installed 1.7 game and Harmony16Dll for its 1.6 Workshop assembly. NuGet restores pinned 1.6 references, framework references, PolySharp 1.16.0 and Contracts' JSON dependencies. No proprietary game or Harmony DLLs are committed or packaged.

```powershell
./tools/Build.ps1 -GameVersion 1.6 -DotnetPath '<SDK>/dotnet.exe'
./tools/Build.ps1 -GameVersion 1.7 -DotnetPath '<SDK>/dotnet.exe'
./tools/Test-PackageLayout.ps1
./tools/Package.ps1 -IncludeContractsPreview
```

Common contains Defs/translations. 1.6/Assemblies contains net472 DLLs; 1.7/Assemblies contains net10 DLLs. LoadFolders selects Common and exactly one runtime folder; Core's optional Ideology definitions are in Common-Ideology. Intermediate build paths are separate. Package validation reads PE metadata without executing game DLLs, checks framework targets and DLL allowlists, and rejects mixed runtime folders or proprietary DLLs. Contracts' nine JSON runtime libraries are allowed only in its 1.6 folder. The 1.7 engine-test assemblies are compiled only, and are not 1.6 tests.

Shared compatibility methods replace unavailable clamp/enum/null/finite/SHA APIs. PolySharp supplies compiler polyfills at build time. Both frameworks run the same rule assertions, including known SHA vectors, NaN/infinity/clamp boundaries and caller-name behavior. JSON and ownership journal rules retain their schemas. An attempt to run net472 tests inside a net10 host exposed a JSON assembly-version conflict; the supported test setup uses a native net472 test executable instead. This is not evidence that RimWorld's Mono loader or another mod's JSON version will cooperate.

Install complete matching folders from the ZIP into RimWorld/mods. Existing installers read Version.txt and check that version's DLLs before replacing an owned package; they back up old folders and do not launch the game. Do not mix local and Workshop copies. No save conversion between game versions is supplied.

## Cover

Contracts' [PNG](../TestMods/Honor.Contracts.Preview/About/Preview.png) follows the existing charcoal/gold family and adds an original charter/seal/frontier-envoy illustration. [JPEG](../TestMods/Honor.Contracts.Preview/About/Preview-Workshop.jpg) is the same composition encoded below Steam's 1 MB limit. See [generation prompt and tool](CONTRACTS-COVER.md). Contracts stays a development preview and was not published to Workshop by this change.
