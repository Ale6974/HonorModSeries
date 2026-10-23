# Honor Mod Series development

- Target RimWorld 1.7. Validate the user's actual Version.txt, runtimeconfig and game/Harmony DLLs before changing engine bindings. Record build-specific evidence in docs/API-VERIFICATION.md. Do not assume 1.6 signatures or stable 1.7 support.
- Keep game DLLs, decompiled game code, local paths/configuration, saves and downloaded tools out of Git and distributions.
- Honor is a Need. Natural decay stays within the current tier; events can lower it. Reputation uses vanilla social memories/relations, without a separate currency.
- The insult recipient is the challenger. Offenders cannot refuse. Minors, slaves and prisoners are excluded by default; incapacitation postpones staging. Challenger ideology takes precedence.
- Core must not depend on Society, Nobility, Diplomacy or Oaths. Keep unimplemented features explicit in docs/STATUS.md.
- Build and rule tests: tools/Build.ps1 with an installed .NET 10 SDK and local GamePaths.props. Test real engine changes with tools/Test-InGame.ps1, one process at a time, in separate save data. Never test on the user's real colony or overwrite ModsConfig.xml.
- Treat docs/TESTING.md as evidence, not a promise. Re-run only checks justified by changes and report actual limitations. Keep Korean/English player text aligned.
