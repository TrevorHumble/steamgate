# Issue #0003: Steam game detection

**Type:** ready. **Category:** product feature; adds new components (Steam library reader, game
matcher, game transitions, the worker snapshot, process scanner) and changes DESIGN.md, so it is a
system-level change.
**Review bar:** system-level change; two independent reviewers, both PASS; reviewer-documentation on
the DESIGN.md paragraph; plus the architecture gate at issue time and the design-philosophy gate at PR
time.
**GitHub:** #8.
**Depends on:** 0002.
**Blocks:** 0004, 0005, 0006, 0007.
**Touches:** `src/SteamGate.Core/Steam/**` (new), `src/SteamGate.Core/GateSnapshot.cs` (new),
`src/SteamGate.Service/ProcessScanner.cs` (new), `src/SteamGate.Service/SteamRootLocator.cs` (new),
`src/SteamGate.Service/GateWorker.cs`, `tests/SteamGate.Tests/Steam/**` (new),
`tests/SteamGate.Tests/Fixtures/**` (new), `tests/SteamGate.Tests/SteamGate.Tests.csproj`,
`src/SteamGate.Service/SnapshotHolder.cs` (new), `src/SteamGate.Service/Program.cs`,
`install/Test-Install.ps1`, `DESIGN.md`, `issues/0003-steam-game-detection.md`.

## User story
As the SteamGate service, I need to know at any moment which Steam games are open, leaving out
programs on the not-counted list, so that the time accounting in 0004 counts only gaming and the
enforcement in 0005 knows which processes to close.

## Approved spec this issue carries
"Steam game detection (read libraryfolders.vdf + appmanifest files, match running programs to
steamapps\common folders, exclude the not-counted list)." Not-counted list pre-filled with Blender,
OBS Studio, Wallpaper Engine, and Steamworks Shared.

## Design decisions (orchestrator)
- A game is identified by its install folder name under `<library>\steamapps\common\`. A running
  process belongs to a game when its full exe path, compared `OrdinalIgnoreCase` after
  `Path.GetFullPath`, starts with `Path.Join(root, "steamapps", "common", folder) + Path.DirectorySeparatorChar`
  for some library root and folder. The trailing separator matters:
  `common\Cyberpunk 2077 Mods\x.exe` is folder `Cyberpunk 2077 Mods`, never `Cyberpunk 2077`.
  `Path.Join` keeps a drive-root library such as `D:\` working.
- Every comparison of folder names (matching, not-counted, start and stop, later issues) uses
  `StringComparer.OrdinalIgnoreCase`. A game's `Folder` is the on-disk spelling from the installed
  list; a process under `common\<x>\` with no installed entry uses the spelling from the lowest pid's
  path. A game's `DisplayName` is the installed entry's name, or the folder when there is none.
- Matching does not require an `appmanifest_*.acf`: on this PC four installed games have no manifest
  (Arc Raiders, Cyberpunk 2077, Dying Light The Beast, Overwatch). Manifests only supply display names
  (`name`); a folder with no manifest shows its folder name. When several manifests name the same
  `installdir`, the lowest appid (the number in the file name `appmanifest_<appid>.acf`, compared as a
  number; a file whose name part is not a whole number is passed to `skipped`) wins; on this PC Red Dead Redemption 2 and Red
  Dead Online share one folder. A manifest whose folder is absent produces no entry.
- Libraries: `LibraryFolders.Resolve(string steamRoot, string? vdfText, Func<string, bool> isLocalDrive)` (Core) returns the root first,
  then each parsed `path`, each normalized with `Path.GetFullPath` and
  `Path.TrimEndingDirectorySeparator`, de-duplicated `OrdinalIgnoreCase` (on this PC the vdf's entry
  `"0"` repeats the root). `Resolve` never throws: it drops any parsed `path` that is empty, is not
  fully qualified (`!Path.IsPathFullyQualified`), starts with `\\`, makes `Path.GetFullPath` throw, or is
  not on a local drive (an unreachable network path would stall the tick and make the LocalSystem
  account sign in to another computer). Whether a path is local is decided by a
  `Func<string, bool> isLocalDrive` parameter: the service passes a check that accepts
  `DriveType.Fixed` and `DriveType.Removable` (Steam allows libraries on SD cards and USB drives), and
  tests pass a fake. The service's check walks every part of the path from the drive root down and
  drops the path if any part is a reparse point (junction or symbolic link) whose target is not
  itself on a Fixed or Removable drive. `Resolve` stops after 16 libraries in all (the root counts
  as one), applies the same checks to the root, and drops a library whose resolved final path
  equals one already kept. The vdf is read from `<root>\steamapps\libraryfolders.vdf` through
  `SteamFiles.ReadCapped(string path, long limit = MaxVdfBytes)` (Core), which returns `(string? Text, bool Locked)` and never
  throws: `Text` is null for a missing, locked, unreadable, or oversized (more than `MaxVdfBytes`)
  file, and `Locked` is true only on `IOException` or `UnauthorizedAccessException`, so the worker can
  keep the previous library list for a locked file. The root comes from registry `HKLM\SOFTWARE\WOW6432Node\Valve\Steam` value
  `InstallPath`, falling back to `C:\Program Files (x86)\Steam`. That registry value is writable
  by standard users, so it is untrusted too: `SteamRootLocator` uses it only when it passes the same
  path checks as a library (fully qualified, not `\\`, on a Fixed or Removable drive), and otherwise
  uses the fallback root. Steam's folder layout (`steamapps\common`, `steamapps\libraryfolders.vdf`,
  the manifests) is known to one Core type, `SteamLayout`, and every other module asks it.
- Steam data never ends the service. The Steam files are writable by standard users, so they are
  treated as untrusted input:
  - `VdfReader` is iterative (an explicit stack, no recursion) and throws `FormatException` on
    malformed text or nesting deeper than 32 (`MaxDepth`). `SteamFiles` reads no vdf or manifest
    larger than 1 MB (`SteamFiles.MaxVdfBytes`).
  - A missing, malformed, or oversized (more than `MaxVdfBytes`) `libraryfolders.vdf` gives the root
    alone. A locked or unreadable one
    (`IOException`, `UnauthorizedAccessException`) keeps the previous refresh's library list (the root
    alone on the first refresh). A missing or unreadable registry value uses the fallback root.
  - `InstalledApps.Read(string libraryRoot, Action<string> skipped, Func<string, bool> isLocalDrive,
    ManifestBudget budget)` never throws (all parameters required; one `ManifestBudget` is shared by
    a whole refresh): a missing
    root or `steamapps\common` returns an empty list, and every malformed, oversized, or unreadable
    manifest is passed to `skipped` and left out. It reads at most 2048 manifests and 4096 game
    folders per library, and one refresh reads at most 16 MB of manifests in all, counting the bytes
    actually read (`SteamFiles.ReadCapped` takes a byte limit, the smaller of the remaining budget and
    `MaxVdfBytes`, so a file that grows between a size check and the read cannot pass the budget); manifests past
    that budget are passed to `skipped`. `logSkipped` is called only for a path
    that was not skipped on the previous refresh (`SteamLibraries` decides this, see below), so a bad file is logged once, not every minute.
- Libraries and installed apps are owned by one Core class, `SteamLibraries`. The worker calls
  `SteamLibraries.Refresh(Func<string> locateRoot, TimeSpan sinceStart, Func<string, bool>
  isLocalDrive, Action<string> logSkipped)` every tick, and `locateRoot` is called only when a read is
  due, with a `Stopwatch` elapsed time, so a clock change cannot stop
  refreshes. It reads on the first call and again when at least 60 seconds have passed since its last
  read, and otherwise returns its current lists unchanged. A read takes `libraryfolders.vdf` through
  `SteamFiles.ReadCapped`, keeps the previous library list when `Locked` is true and the root is
  unchanged (the root alone on the first read or after the root changed), otherwise calls `LibraryFolders.Resolve`, then reads and merges each library's
  installed apps. It returns `(IReadOnlyList<string> Libraries, IReadOnlyList<InstalledApp>
  Installed, IReadOnlyList<string> CommonTargets, bool Refreshed)`, where `CommonTargets` are the final paths of the kept libraries' `steamapps\common` folders that a link moved, and the worker logs `libraries <count>` when `Refreshed` is true.
  `VdfReader.TryParse` returns null on malformed text, so callers never catch the parser's errors,
  and callers read the parse tree only through `VdfReader`'s accessors.
- Moved games are still seen. Steam users often move a game or a library with a junction, and
  Windows reports a process's path with the junction resolved. So the refresh records, for each kept
  library, the final path of its `steamapps\common` folder with every link resolved (a link on the
  library root, on `steamapps`, or on `steamapps\common`), and for each game folder under it that is
  itself a link, that game's final path. The matcher matches a process under either the listed path
  or the final path and gives it the Steam folder name. A link whose target fails the local-drive
  check makes that library (or that game) unused; the refresh also runs the local-drive check on
  `<library>\steamapps` and `<library>\steamapps\common` before reading them. One Core helper,
  `SteamPaths.FinalPath`, resolves links one link at a time and always takes the local-drive filter
  (there is no unfiltered form): it reads each link's stored target, refuses it before following it
  when it fails the filter, and never opens a final target. Every path the refresh opens (the vdf,
  each manifest, each folder it lists) is first resolved this way, file name included, so a link to a
  network share is refused before anything on the network is touched. A link swapped in between that
  check and the open is a known limit recorded in DESIGN.md: it needs the symbolic-link right
  (admin or Developer Mode) and exact timing. The drive-and-link rule lives in one
  type, `LocalPathPolicy`, which `SteamRootLocator` and the refresh both use. Manifests past 2048
  and folders past 4096 are left out without a log line; only bad manifests and those past the
  16 MB budget are passed to `skipped`. A moved target (a game's or a library's final path) is
  refused when it is a drive root, lies under `%SystemRoot%` or `%ProgramData%`, or lies under `%ProgramFiles%` or
  `%ProgramFiles(x86)%` other than under the fixed folder `%ProgramFiles(x86)%\Steam\steamapps\common`
  (a constant, never a user-written value). As a last guard the matcher never matches a process whose
  path is under `%SystemRoot%`, so no junction or library choice can make Windows' own programs or
  installed services count as a game. (Session is not used: a test runner may start games in
  session 0.) Test
  `GameMatcherTests.Match_NeverMatchesUnderSystemRoot` and an `InstalledAppsTests` case with a
  drive-root library cover this. Known limit,
  recorded in DESIGN.md: a junction to another of the user's own program folders makes that program
  count as the game; this only makes the rules stricter for the user who made it. `SteamLibraries` owns the once-per-bad-file logging
  (`SkipTracker`), so the worker only passes the log callback.
  A running process is a Core record `ProcessSample(int Pid, DateTime StartUtc, string ExePath)`.
- Process paths come from `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` plus
  `QueryFullProcessImageName`, not `Process.MainModule`, which fails on protected (anti-cheat)
  processes; the same handle gives the start time through `GetProcessTimes`. A process whose path
  cannot be read is skipped. Each `Process` from `GetProcesses()` is disposed. A game carries
  its `ProcessSample`s (pid, start time, path) so 0005 can confirm a process id was not reused before it acts.
- Decision logic (vdf reading, library resolution, manifest reading, matching, transitions, refresh
  timing) lives in `SteamGate.Core`, including the reading of Steam's own files (`SteamFiles.ReadCapped` and
  `InstalledApps.Read`), because those files are untrusted input whose parsing rules must sit under the
  Core tests and mutation testing; the service reads only the registry and the process list and calls
  Core inside `RunTickAsync`. Step 9 records this in DESIGN.md beside 0002's sentence about what the
  service does. `ProcessScanner` and `SteamRootLocator` are static classes with no service
  registration.
- The worker joins each library's `InstalledApps.Read` result in `Resolve` order and keeps the first
  entry per folder (`OrdinalIgnoreCase`); `Steamworks Shared` normally exists in every library.
  `GameMatcher.Match` also tolerates duplicate folders, uses the first, and gives each process to at
  most one game: the first library in `Resolve` order whose `steamapps\common` prefix it starts with.
- The matcher uses the `NotCounted` list from the settings the worker loaded at start (0006 changes
  this).
- This issue creates the worker snapshot that 0002 described:
  `public sealed record GateSnapshot(IReadOnlyList<RunningGame> Games, IReadOnlyList<InstalledApp> Installed)`
  in Core with a static `Empty`. The snapshot is published through a singleton `SnapshotHolder`
  (Service) registered in `Program.cs`: `Publish(GateSnapshot)` uses `Volatile.Write` and `Current`
  uses `Volatile.Read`; `GateWorker` receives it by constructor and publishes a new snapshot at the end
  of each tick, replaced and never changed; later issues add fields with `with`. The previous
  tick's games are kept only for the start and stop comparison, which logs `game started <folder>` and
  `game stopped <folder>`.
- The not-counted-name bypass below sits outside the approved spec's known-gaps list; the
  orchestrator reports it to Trevor in the run's hand-off note and does not edit the approved spec or
  CLAUDE.md Known gaps.
- Known limit, recorded in DESIGN.md: Steam's folders, `libraryfolders.vdf`, and the registry
  `InstallPath` are writable by standard users, so a game run from a copy outside
  `steamapps\common`, from a library removed from `libraryfolders.vdf` or pushed past the
  16-library cap, from a folder renamed or copied to a not-counted name, or from the real Steam
  folder after `InstallPath` is pointed at another local folder is not seen. The orchestrator reports the `InstallPath` case to Trevor in the
  run's hand-off note with the other not-seen cases.

## Acceptance criteria
A named section runs from its heading to the next heading of the same or higher level. Test fixture
trees (manifests and folders) are built at test run time in a fresh temp folder.

1. **Given** the fixture `Fixtures/libraryfolders.vdf` with two entries whose `path` values are
   `C:\\Program Files (x86)\\Steam` and `D:\\SteamLibrary`, **When** `LibraryFolders.Parse` reads it,
   **Then** it returns exactly `["C:\Program Files (x86)\Steam", "D:\SteamLibrary"]` (backslashes
   un-doubled). (Test `LibraryFoldersTests.Parse_TwoLibraries`.)
1b. **Given** root `C:\Steam\` and vdf paths `C:\Steam` and `D:\Lib\`, **When**
   `LibraryFolders.Resolve` runs, **Then** it returns exactly `["C:\Steam", "D:\Lib"]`; given
   `vdfText` null it returns `["C:\Steam"]` (all with an `isLocalDrive` fake that accepts `C:` and
   `D:`); given root `C:\Steam` and vdf paths `""`, `relative\lib`,
   `\\host\share`, and `E:\Lib` (the fake rejects `E:`), it returns exactly `["C:\Steam"]`. (Tests
   `LibraryFoldersTests.Resolve_DedupesAndFallsBack`, `LibraryFoldersTests.Resolve_DropsInvalidPaths`.)
2. **Given** a vdf text containing a value with an escaped quote `"a\"b"`, **When** `VdfReader` parses
   it, **Then** the value is `a"b`. (Test `VdfReaderTests.EscapedQuote`.)
2b. **Given** a well-formed vdf text of 32 nested `"a" { ... }` blocks, all closed, **When**
   `VdfReader` parses it, **Then** it returns without throwing; **given** 33 nested closed blocks,
   **Then** it throws `FormatException`; **given** 100 000 nested closed blocks, **Then** it throws
   `FormatException` and the test process survives. (Test `VdfReaderTests.DepthLimit_32`.)
3. **Given** `libraryRoots` `["C:\Steam", "D:\Lib"]`; installed `[("Cyberpunk 2077","Cyberpunk 2077"),
   ("Blender","Blender")]` as (folder, display name); not-counted `["Blender"]`; and running processes
   `C:\Steam\steamapps\common\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe` (pid 10),
   `D:\Lib\steamapps\common\Blender\blender.exe` (pid 11),
   `C:\Windows\explorer.exe` (pid 12), and
   `c:\steam\STEAMAPPS\common\cyberpunk 2077\launcher.exe` (pid 13),
   **When** `GameMatcher.Match` runs, **Then** it returns exactly one game, folder `Cyberpunk 2077`,
   display name `Cyberpunk 2077`, with process ids exactly `[10, 13]`. (Test
   `GameMatcherTests.Match_ExcludesNotCountedAndNonGames`.)
4. **Given** `libraryRoots` `["C:\Steam", "D:\"]`, no installed entries, and processes
   `C:\Steam\steamapps\common\Cyberpunk 2077 Mods\x.exe` (pid 5) and
   `D:\steamapps\common\Viewfinder\v.exe` (pid 6), **When** `GameMatcher.Match` runs with no
   not-counted entries, **Then** it returns exactly the folders `Cyberpunk 2077 Mods` and `Viewfinder`;
   **and given** `libraryRoots` `["C:\Steam"]`, no installed entries, and processes
   `C:\Steam\steamapps\common\game x\a.exe` (pid 31) and `C:\Steam\steamapps\common\Game X\b.exe`
   (pid 30), **Then** it returns one game, folder `Game X`, ids `[30, 31]`. (Tests
   `GameMatcherTests.Match_FolderBoundaryAndDriveRoot`, `GameMatcherTests.Match_UninstalledSpellingFromLowestPid`.)
5. **Given** `libraryRoots` `["C:\Steam"]` and a process `C:\Steam\steam.exe`, **When**
   `GameMatcher.Match` runs, **Then** it returns no games; **and** a process `C:\Steam\steamapps\common\foo.exe` (loose in
   `common`) returns no games. (Test `GameMatcherTests.Match_SteamClientIsNotAGame`.)
6. **Given** not-counted `["blender"]` (lower case) and a process under `common\Blender\`, **When**
   `GameMatcher.Match` runs, **Then** it returns no games. (Test `GameMatcherTests.Match_NotCountedIgnoresCase`.)
7. **Given** a library folder containing `steamapps\appmanifest_1382070.acf` (`name` `Viewfinder`,
   `installdir` `Viewfinder`), `steamapps\appmanifest_1174180.acf` (`name` `Red Dead Redemption 2`)
   and `steamapps\appmanifest_1404210.acf` (`name` `Red Dead Online`), both with `installdir`
   `Red Dead Redemption 2`, plus `steamapps\common\Viewfinder\`,
   `steamapps\common\Red Dead Redemption 2\`, and `steamapps\common\Arc Raiders\` with no manifest,
   **When** `InstalledApps.Read` runs, **Then** it returns exactly
   `[("Arc Raiders","Arc Raiders"), ("Red Dead Redemption 2","Red Dead Redemption 2"), ("Viewfinder","Viewfinder")]`
   as (folder, display name), sorted by display name with `StringComparer.OrdinalIgnoreCase`. (Test
   `InstalledAppsTests.Read_ManifestAndBareFolder`.)
7b. **Given** a library with `appmanifest_99.acf` (`name` `Zed`, `installdir` `Shared`),
   `appmanifest_1000.acf` (`name` `Alpha`, `installdir` `Shared`), `appmanifest_5.acf` (`installdir`
   `Gone`, no folder), and `appmanifest_6.acf` containing `not vdf`, plus `steamapps\common\Shared\`,
   **When** `InstalledApps.Read` runs, **Then** it returns exactly `[("Shared","Zed")]` and `skipped`
   received the path of `appmanifest_6.acf`. (Test `InstalledAppsTests.Read_LowestAppIdWins_SkipsAbsentAndMalformed`.)
7c. **Given** a library with `appmanifest_7.acf` of `MaxVdfBytes + 1` bytes and
   `steamapps\common\Big\`, **When** `InstalledApps.Read` runs, **Then** it returns exactly
   `[("Big","Big")]` and `skipped` received the path of `appmanifest_7.acf`. (Test
   `InstalledAppsTests.Read_OversizedManifest_Skipped`.)
7d. **Given** two library results that both contain folder `Steamworks Shared` (the first with display
   name `Steamworks Common Redistributables`), **When** `InstalledApps.Merge` runs on them in order,
   **Then** it returns exactly one `Steamworks Shared` entry, the first library's. (Test
   `InstalledAppsTests.Merge_KeepsFirstPerFolder`.)
8. **Given** the running test process, **When** `ProcessScanner.Scan()` runs, **Then** the result
   contains the test process's own id with a path equal (`OrdinalIgnoreCase`) to
   `Environment.ProcessPath`. (Test `ProcessScannerTests.Scan_FindsSelf`, Windows only.)
8b. **Given** a temp file held open with `FileShare.None`, **When** `SteamFiles.ReadCapped` runs,
   **Then** it returns `Text` null and `Locked` true; **given** a missing path, `Text` null and
   `Locked` false; **given** a file of `MaxVdfBytes + 1` bytes, `Text` null and `Locked` false. (Test
   `SteamFilesTests.ReadCapped_LockedMissingOversized`.)
9. **Given** the vdf text `"libraryfolders" {` (truncated), **When** `LibraryFolders.Parse` runs,
   **Then** it returns an empty list without throwing; **and given** a library root that does not
   exist, **When** `InstalledApps.Read` runs, **Then** it returns an empty list without throwing.
   (Tests `LibraryFoldersTests.Parse_Truncated_ReturnsEmpty`, `InstalledAppsTests.Read_MissingRoot_ReturnsEmpty`.)
9b. **Given** a library whose `steamapps\common\Game` folder is a junction to a local folder
   `<temp>\moved\Game`, **When** a process whose path is `<temp>\moved\Game\g.exe` is matched after
   a refresh, **Then** it is the game `Game`; **and given** a library root that is a junction to a
   local folder `<temp>\real`, **When** a process under `<temp>\real\steamapps\common\Game\` is
   matched, **Then** it is the game `Game`; **and given** `steamapps\common` itself is the junction,
   the same holds; **and given** a file `appmanifest_.acf` in `steamapps`, **When** the library is
   read, **Then** the other manifests are still read. (Tests `SteamLibrariesTests.JunctionedGameIsSeen`,
   `SteamLibrariesTests.JunctionedLibraryIsSeen`, `SteamLibrariesTests.JunctionedCommonIsSeen`,
   `SteamLayoutTests.BadNameInSteamapps_DoesNotBlankTheLibrary`.)
9c. **Given** a game folder that is a junction to a drive root or to a folder under `%SystemRoot%`,
   **When** the library is read, **Then** that game has no moved target; **and given** a game folder
   that is a junction to `%ProgramData%`, **Then** it has no moved target, and a path inside
   `%ProgramData%` is refused by the moved-target rule; **and given** a link whose stored target is `\\host\share`, **When** `SteamPaths.FinalPath`
   resolves it with a local-only filter, **Then** it returns null without opening the share. (Tests
   `InstalledAppsTests.MovedTargetIntoSystemFolder_IsRefused`,
   `LocalPathPolicyTests.MovedTargetAllowed_RealFolders_RefusesDriveRootSystemAndProgramFoldersExceptTheDefaultSteamLibrary`,
   `SteamPathsTests.NetworkTarget_RefusedWithoutOpening`.)
10. **Given** previous folders `["A","B"]` and current `["B","C"]`, **When** `GameTransitions.Diff`
   runs, **Then** Started is `["C"]` and Stopped is `["A"]`; **given** previous `["Cyberpunk 2077"]`
   and current `["cyberpunk 2077"]`, **Then** both lists are empty. (Tests
   `GameTransitionsTests.Diff_StartedAndStopped`, `GameTransitionsTests.Diff_IgnoresCase`.)
11. **Given** a fake Steam tree, **When** `SteamLibraries.Refresh` runs at 0 s, 59 s, and 60 s,
   **Then** `Refreshed` is true, false, true; **given** a first read that found two libraries and a
   `libraryfolders.vdf` then held open with `FileShare.None`, **When** it refreshes at 60 s, **Then**
   `Libraries` still holds the two libraries; **given** a root of `\\host\share`, **When**
   `LibraryFolders.Resolve` runs, **Then** the root is dropped. (Tests
   `SteamLibrariesTests.RefreshesEvery60Seconds`, `SteamLibrariesTests.LockedVdfKeepsPrevious`,
   `LibraryFoldersTests.Resolve_DropsNetworkRoot`.)
12. **Given** the CI Windows runner, after `Reset-SteamGate @{}` and `New-FakeSteamRoot`, **When**
   `Test-Install.ps1` starts `FakeGame.exe -n 300 127.0.0.1` and waits up to 20 seconds, **Then**
   `service.log` contains `game started FakeGame` and the script prints `DETECTED FakeGame`; after the
   test ends `FakeGame.exe` with `Stop-Process -Force`, it prints `STOPPED FakeGame` when
   `service.log` shows `game stopped FakeGame` within 20 seconds; **and** after
   `Reset-SteamGate @{ notCounted = @('FakeGame') }` and a fresh `New-FakeSteamRoot`, starting the fake
   game again produces no `game started FakeGame` line within 20 seconds, counted from the first
   `libraries <count>` line written after the restart, and the script prints `NOT COUNTED FakeGame`. It exits non-zero at any failed check and ends with `Remove-FakeSteamRoot` and then an uninstall,
   both in a `finally` block.
13. **Given** `DESIGN.md`, **When** a reader greps
   `## SteamGate service design (orchestrator decisions, not part of the approved spec)`, **Then** it
   contains `How games are recognized`, `steamapps\common`, `not seen`, and `not-counted name`.

## Implementation plan
1. Create `src/SteamGate.Core/Steam/VdfReader.cs`: an iterative parser (explicit stack, no recursion)
   of Valve KeyValues text into nested `OrdinalIgnoreCase` dictionaries; handles `\\`, `\"`, `\n`, `\t`
   escapes and `//` comments; throws `FormatException` on malformed text and on nesting deeper than
   `MaxDepth` (32). Define `MaxVdfBytes` (1 MB) in `SteamFiles`.
2. Create `src/SteamGate.Core/Steam/SteamFiles.cs` (`ReadCapped` as in Design decisions),
   `src/SteamGate.Core/Steam/LibraryFolders.cs` (`Parse(string vdfText)` returns library paths, or an
   empty list when malformed; `Resolve(string steamRoot, string? vdfText, Func<string, bool>
   isLocalDrive)` as in Design decisions) and `src/SteamGate.Core/Steam/InstalledApps.cs` (`InstalledApp(string Folder, string
   DisplayName, string? MovedTo)`; `Read` as in Design decisions;
   `Merge(IEnumerable<IReadOnlyList<InstalledApp>>)` keeps the first entry per folder).
3. Create `src/SteamGate.Core/Steam/GameMatcher.cs`: `RunningGame(string Folder, string DisplayName,
   IReadOnlyList<ProcessSample> Processes)` and
   `Match(IEnumerable<ProcessSample> processes, IReadOnlyList<string> libraryRoots,
   IReadOnlyCollection<InstalledApp> installed, IEnumerable<string> notCounted, IEnumerable<string>? commonTargets = null, string? systemRoot = null)` (`systemRoot` is for tests; null means `%SystemRoot%`) using the rules in
   Design decisions, sorted by folder `OrdinalIgnoreCase`, processes by pid, no repeats.
4. Create `src/SteamGate.Core/Steam/GameTransitions.cs` (`Diff` returns sorted `Started` and `Stopped`,
   compared `OrdinalIgnoreCase`), `src/SteamGate.Core/Steam/SteamLibraries.cs`,
   `src/SteamGate.Core/Steam/SteamLayout.cs`, `src/SteamGate.Core/Steam/ProcessSample.cs`, and `src/SteamGate.Core/GateSnapshot.cs` as in Design decisions.
5. Create `src/SteamGate.Service/ProcessScanner.cs` (P/Invoke `OpenProcess`,
   `QueryFullProcessImageName`, `GetProcessTimes`, `CloseHandle`; enumerates `Process.GetProcesses()`
   ids and disposes each) and `src/SteamGate.Service/SteamRootLocator.cs` (registry read with
   fallback).
6. In `src/SteamGate.Service/GateWorker.cs`, inside `RunTickAsync` (no new loop or timer): read the
   root through `SteamRootLocator`, call `SteamLibraries.Refresh` with the real local-drive check and
   a callback that logs `manifest skipped <path>`,
   and log `libraries <count>` when it
   refreshed, catching every failure as in Design decisions; run the matcher
   with the current `notCounted` list; log `GameTransitions.Diff` results; publish a new
   `GateSnapshot` with `Volatile.Write`.
7. Add the tests in criteria 1 to 11 under `tests/SteamGate.Tests/Steam/`, plus further
   value-asserting tests on the new Core code (at least the `\\`, `\n`, `\t` escapes, a `//` comment
   line, and a nested lookup with different key case) as needed to keep the 80% Stryker break
   threshold. Put `libraryfolders.vdf` in `tests/SteamGate.Tests/Fixtures/` and add
   `<None Include="Fixtures\**" CopyToOutputDirectory="PreserveNewest" />` to
   `tests/SteamGate.Tests/SteamGate.Tests.csproj`. Build the criterion 7, 7b, and 9 library trees at
   test run time in a fresh temp folder (manifests written from string constants, folders created with
   `Directory.CreateDirectory`); never commit empty folders.
8. In `install/Test-Install.ps1`, add three shared helpers:
   - `Reset-SteamGate([hashtable]$Settings = @{})`: tear down any installed copy directly (clear
     recovery with `sc.exe failure`, end the service process and every process whose image path is
     under `%ProgramFiles%\SteamGate` and wait with `Wait-Process -Timeout 30`, `sc.exe delete SteamGate`, remove both
     SteamGate folders) without calling `Uninstall-SteamGate.ps1`; run a fresh install; wait up to 30
     seconds for `settings.json` to exist; read it, apply `$Settings`, and write it back (as the admin
     test runner); end the service process and wait up to 60 seconds for a Running service with a new
     pid.
   - `New-FakeSteamRoot`: first call `Remove-FakeSteamRoot` if a fake root from this script exists;
     then record the original `InstallPath` value, or the marker `absent`, in
     `$script:OriginalInstallPath` (after the Remove-first step, so a second call records the real
     original again); create the `HKLM\SOFTWARE\WOW6432Node\Valve\Steam` key with
     `New-Item -Force` when missing (noting that the script created it); create a fake root at
     `Join-Path $env:RUNNER_TEMP 'FakeSteam'` (falling back to `$env:TEMP`) with `steamapps\libraryfolders.vdf` (its only `path` is
     the fake root) and `steamapps\common\FakeGame\FakeGame.exe` (a copy of
     `C:\Windows\System32\ping.exe`), set `InstallPath` to the fake root,
     then end the service process and wait for the restart so the first tick reads it.
   - `Remove-FakeSteamRoot`: end `FakeGame.exe`, delete the root, restore `InstallPath` from
     `$script:OriginalInstallPath` (or remove it, and remove the key if the script created it), and clear
     the saved value. When `$script:OriginalInstallPath` is `$null` (nothing was recorded), it ends
     `FakeGame.exe` and deletes the fake root but leaves the registry untouched.
   Then add the criterion 12 group using them, ending in a `finally` block that runs
   `Remove-FakeSteamRoot` and then `Uninstall-SteamGate.ps1`, so the script ends uninstalled. Later
   issues' check groups reuse these helpers.
9. In `DESIGN.md` `## SteamGate service design (orchestrator decisions, not part of the approved spec)`,
   add a "How games are recognized" paragraph (also noting, beside 0002's sentence that the service
   only connects Core to Windows, that Core reads Steam's own files because they are untrusted input
   kept under the Core tests): the folder rule under `steamapps\common`,
   case-insensitive comparison, manifests only for names, the not-counted list holds folder names,
   Steam files are untrusted input that never stop the service (depth and size caps), the
   `GateSnapshot` record, and the known limit that a game run from a copy outside `steamapps\common`,
   from a library removed from `libraryfolders.vdf` or pushed past the 16-library cap, from a folder
   renamed or copied to a not-counted name, or from the real Steam folder after `InstallPath` is
   pointed elsewhere is not seen (causing this needs no admin), and that a game moved with a
   junction is still seen. Say the libraries are read again once at least 60 seconds have passed
   since the last read. State the library limit as "at most 16 libraries in all; each
   path from `libraryfolders.vdf` and the registry root must be on a fixed or removable drive", and
   say a bad appmanifest is skipped and logged while a bad `libraryfolders.vdf` gives the root alone.
   In `## Testing` (or wherever DESIGN.md says what the tests cover), add `ProcessScanner`,
   `SnapshotHolder`, and `SteamRootLocator` to the service classes the tests cover.

## Out of scope
- Counting time (0004), closing games (0005), editing the not-counted list (0006, 0007).
