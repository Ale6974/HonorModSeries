# Honor Mod Series development

- Target RimWorld 1.7. Validate the user's actual Version.txt, runtimeconfig and game/Harmony DLLs before changing engine bindings. Record build-specific evidence in docs/API-VERIFICATION.md. Do not assume 1.6 signatures or stable 1.7 support.
- Keep game DLLs, decompiled game code, local paths/configuration, saves and downloaded tools out of Git and distributions.
- Honor is a Need. Natural decay stays within the current tier; events can lower it. Reputation uses vanilla social memories/relations, without a separate currency.
- The insult recipient is the challenger. Offenders cannot refuse. Minors, slaves and prisoners are excluded by default; incapacitation postpones staging. Challenger ideology takes precedence.
- Core must not depend on Society, Nobility, Diplomacy or Oaths. Keep unimplemented features explicit in docs/STATUS.md.
- Build and rule tests: tools/Build.ps1 with an installed .NET 10 SDK and local GamePaths.props. Test real engine changes with tools/Test-InGame.ps1, one process at a time, in separate save data. Tests must never use the user's real colony or modify ModsConfig.xml. The user-authorized local installer may install and activate the user-requested Honor suite after backing up the configuration and preserving other mod entries/order.
- Treat docs/TESTING.md as evidence, not a promise. Re-run only checks justified by changes and report actual limitations. Keep Korean/English player text aligned.
- The user authorized publishing all subsequent RimWorld development to the public GitHub repository https://github.com/Ale6974/HonorModSeries. Publish completed, verified source changes there and keep the local main branch aligned. Do not upload local settings, full engine logs, saves, credentials, third-party/game DLLs, or build/tool caches.
- On 2026-10-24 the user explicitly authorized PUBLIC Steam Workshop publishing for Core and the four expansion modules, and a PUBLIC collection. Publish verified functionality with accurate limitations and dependency/DLC metadata. Reuse persisted item IDs. Ask the user to personally handle sign-in, Steam Guard or a new legal agreement when required.
- All Honor modules support English and Korean through the game's native language system. Keep keyed strings and DefInjected labels/descriptions covered; default English definitions are the fallback. Never change the user's language preference to select the mod language.

- Collection 3825922644 has API Public visibility and six children, but the web Publish Collection step remains pending. The user said they are away and will click Publish later. Do not claim the collection is publicly accessible until the anonymous page is verified.
