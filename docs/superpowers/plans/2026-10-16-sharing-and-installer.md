# Sharing, Installer, and Any-Game Profiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Claude Game Helper shareable: a Velopack installer published to a public GitHub repo with
automatic updates, automatic profiles for any game, per-user data in Documents, and a tidied public history.

**Architecture:** Built-in instructions ship inside the app (`BuiltInProfiles\`), everyone's data lives in
`Documents\Claude Game Helper\<game>\`, and unknown games get a profile on first hotkey use via
`AutoProfiles`. Velopack wraps the self-contained publish into `Setup.exe`, runs install/uninstall hooks
(start-with-Windows), and updates from GitHub Releases; `release.ps1` builds and publishes.

**Tech Stack:** existing C# / .NET 10 / WPF app; Velopack 1.2.161 (NuGet `Velopack`, tool `vpk`); GitHub CLI.

**Spec:** [docs/superpowers/specs/2026-10-16-sharing-and-installer-design.md](../specs/2026-10-16-sharing-and-installer-design.md)
(builds on [the v1 design](../specs/2026-10-16-claude-game-helper-design.md)).

## Global Constraints

- All v1 Global Constraints still hold (anti-ban, Claude Code isolation flags, stdin UTF-8 without BOM, opaque
  non-activating panel, game-neutral `src\` code, American English, commit on `master`, never branch).
- Velopack NuGet package and `vpk` tool both pinned to **1.2.161**; `vpk` via a local tool manifest.
- Velopack `packId` **`ClaudeGameHelper`**, title **"Claude Game Helper"**, first version **`0.2.0`**,
  tag `v<version>`, release name `Claude Game Helper <version>`.
- Per-user install, no admin prompt, self-contained `win-x64` (no separate .NET install).
- Data root: **`Documents\Claude Game Helper\`** (`Environment.SpecialFolder.MyDocuments`, which follows
  OneDrive redirection); one subfolder per profile id; logs in `<data root>\logs`. (Departs from spec §6.4,
  which kept logs in `%LOCALAPPDATA%\ClaudeGameHelper`: Velopack installs the program into that same folder
  and deletes it on uninstall. Logs in Documents are also easy for someone to find and send to Jason.)
- Updates never write to the data root.
- Repo: **public**, `claude-game-helper` under Jason's GitHub account, **MIT** license, commits authored with
  his **personal email address** (changed by Jason during the build from the GitHub no-reply address).
  Claude asks Jason right before (a) rewriting history and (b) creating the repo / first push.
- Public files must not contain: his work email, Windows username, PC paths, day-job references, the
  OneDrive folder path, or family members (spec §5).
- Tests: filtered runs mid-task; the full suite runs once, in Task 6.

## Review Focus

1. **Jason's existing `settings.json` has `"profilesFolder"`** (a v1 key). After the upgrade it must load
   without the "misspelled key" error. (Task 1)
2. **A Java game's window title has characters Windows forbids in folder names or is very long**
   ("Minecraft* 1.21: Tricky Trials") → a safe, short folder name. (Task 2)
3. **No internet, GitHub down, or rate-limited at startup** → the update check fails silently into the log;
   the helper works normally. (Task 4)
4. **The game's data folder doesn't exist yet** (new install, new game, or the person deleted it) → it's
   created before Claude runs in it. (Task 1)
5. **A person put their own `CLAUDE.md` in a game's data folder** → it's used instead of the built-in one,
   and an update doesn't overwrite it. (Task 1)

---

## File Structure

```
src\ClaudeGameHelper\BuiltInProfiles\        (moved from repo-root profiles\; copied to output)
  fallout-76\  profile.json  CLAUDE.md
  general\     profile.json  CLAUDE.md
  _template\   CLAUDE.md                     generic advisor for automatic profiles
src\ClaudeGameHelper.Core\Profiles\AutoProfiles.cs          new
src\ClaudeGameHelper\Program.cs                               new: custom Main with VelopackApp
src\ClaudeGameHelper\Platform\StartupRegistration.cs          new: HKCU Run key
src\ClaudeGameHelper\Platform\UpdateChecker.cs                new
src\ClaudeGameHelper\app.ico                                 new: same "C" mark as the tray icon
.config\dotnet-tools.json   release.ps1   README.md   LICENSE   docs\images\panel.png
```

---

### Task 1: Built-in instructions, data folder in Documents, generic advisor

**Files:**
- Move: `profiles/fallout-76/{profile.json,CLAUDE.md}`, `profiles/general/{profile.json,CLAUDE.md}` →
  `src/ClaudeGameHelper/BuiltInProfiles/…`; delete repo-root `profiles/`
- Create: `src/ClaudeGameHelper/BuiltInProfiles/_template/CLAUDE.md`
- Modify: `GameProfile.cs`, `ProfileLoader.cs`, `AppSettings.cs`, `AppPaths.cs`, `PromptBuilder.cs`, `AskService.cs`, `App.xaml.cs`, app `.csproj`, `.gitignore`
- Test: `ProfileLoaderTests.cs` (rewrite), `AppPathsTests.cs`, `SettingsStoreTests.cs`, `PromptBuilderTests.cs`, `AskServiceTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  // GameProfile: two new init properties; positional parameters unchanged
  public string InstructionsPath { get; init; } = Path.Combine(FolderPath, "CLAUDE.md");
  public bool IsAuto { get; init; }
  public sealed record ProfileLoadResult(IReadOnlyList<GameProfile> Profiles, GameProfile? Fallback,
      IReadOnlyList<string> Errors, string TemplateInstructionsPath);
  public static ProfileLoadResult ProfileLoader.LoadAll(string builtInRoot, string dataRoot, IReadOnlyCollection<string> notGames);
  // AppSettings: + DataFolder (string?), + NotGames (IReadOnlyList<string>, default []);
  //              ProfilesFolder kept, [JsonIgnore(Condition = WhenWritingNull)], no longer used
  public static string AppPaths.DataRoot(string? configured);      // configured, else Documents\Claude Game Helper
  public static string AppPaths.BuiltInProfilesDir { get; }       // <AppContext.BaseDirectory>\BuiltInProfiles
  public static string AppPaths.LogsDir(string dataRoot);         // <dataRoot>\logs
  public static string PromptBuilder.Build(string question, IReadOnlyList<string> screenshotPaths,
      string? gameName, string? trackingLabel, string? activeTrackingName);   // gameName is new, third
  ```
  `AppPaths.FindProfilesRoot` is removed.

- [ ] **Step 1: Move Jason's data out of the repo.** Move his character file in `profiles/fallout-76/tracking/`,
  `profiles/fallout-76/screenshots/*`, and `profiles/fallout-76/conversations/*` into
  `Documents\Claude Game Helper\fallout-76\{tracking,screenshots,conversations}\` (create folders; move, never
  delete). Confirm the counts match (1 / 11 / 1 as of planning). Then `git mv` the four profile files into
  `src/ClaudeGameHelper/BuiltInProfiles/` and remove `profiles/`. Add to the app csproj:
  `<None Include="BuiltInProfiles\**" CopyToOutputDirectory="PreserveNewest" />`.

- [ ] **Step 2: Rewrite the instructions.** In `fallout-76/CLAUDE.md`, replace the first paragraph with:
  ```markdown
  You are the player's Fallout 76 advisor, shown in a narrow side panel while they play. Bethesda patches the
  game often: assume it has changed since your training.
  ```
  `general/CLAUDE.md`, exact text:
  ```markdown
  # Game helper (no game detected)

  You are the player's advisor in a narrow side panel while they play PC games. No game profile matched the
  window in front, so you know the game only if they say or a screenshot shows it.
  - Lead with the answer in one sentence; add short bullets only if they help.
  - Search the web before stating anything about a game's current state (balance, patches, drops, events).
  - Never suggest exploits or anything else that risks an account ban.
  - Say when you're unsure.
  ```
  `_template/CLAUDE.md`, exact text:
  ```markdown
  # Game advisor

  You are the player's game advisor, shown in a narrow side panel while they play. Each message names the
  game. Assume it may have changed since your training: patches, balance changes, new content.

  ## How to answer
  - Lead with the verdict or direction in one sentence. Add detail only if it helps, as a few short bullets.
  - Directions in plain words: compass direction and landmarks.
  - If unsure, say so and name the screenshot that would settle it.

  ## Check before you claim
  - Search the web before stating anything about the game's current state: balance, builds, drop sources,
    events, prices, patch changes. Prefer the game's official patch notes and its main wiki, then community
    guides under a year old.
  - When it matters, say which patch or date your answer reflects.

  ## Never
  - Suggest exploits, glitches, duping, cheats, or anything else that risks an account ban.

  ## Notes files
  - Keep notes for this game in `tracking/`: one Markdown file per character, save, or playthrough, whichever
    fits the game, named in lowercase with hyphens. Each message names the active file; read it before
    answering anything about the player's progress.
  - When a screenshot or the conversation shows something new (level, build, gear, quests, goals), update the
    active file yourself without asking, then end your answer with one line: `Updated <file>: <what changed>`.
  - If no file is active and the screens show progress worth keeping, create one, named from the screen.
    Ask only if you can't tell.
  - Keep files current, not a history.
  ```

- [ ] **Step 3: Write the failing tests.**
  ```csharp
  // ProfileLoaderTests — builtIn = <repo>\src\ClaudeGameHelper\BuiltInProfiles, data = temp dir
  [Fact] public void BuiltInProfileUsesDataFolderAndBuiltInInstructions()
  { var r = ProfileLoader.LoadAll(BuiltIn, data, []); var f = r.Profiles.Single(p => p.Id == "fallout-76");
    Assert.Equal(Path.Combine(data, "fallout-76"), f.FolderPath);
    Assert.Equal(Path.Combine(BuiltIn, "fallout-76", "CLAUDE.md"), f.InstructionsPath);
    Assert.Equal(Path.Combine(BuiltIn, "_template", "CLAUDE.md"), r.TemplateInstructionsPath);
    Assert.Equal("general", r.Fallback!.Id); Assert.DoesNotContain(r.Profiles, p => p.Id == "_template"); Assert.Empty(r.Errors); }
  [Fact] public void PersonalClaudeMdOverridesBuiltIn()                    // Review Focus 5
  { /* data\fallout-76\CLAUDE.md exists → InstructionsPath is that file */ }
  [Fact] public void AutoProfilesLoadFromDataFolderWithTemplate()
  { /* data\valheim\profile.json {"displayName":"Valheim","auto":true,"windowMatch":{"processNames":["valheim"]},"trackingLabel":"Notes"}
       → IsAuto, InstructionsPath = template, FolderPath = data\valheim */ }
  [Fact] public void NotGamesAreLeftOut() { /* notGames ["valheim"] → no valheim profile */ }
  [Fact] public void DataFolderWithoutProfileJsonIsIgnored() { /* data\fallout-76\tracking only → still exactly one fallout-76 */ }
  [Fact] public void MissingDataRootIsFine() { /* dataRoot doesn't exist → built-ins load, no errors */ }
  // keep: BadJsonIsReportedAndOtherProfilesStillLoad, MissingClaudeMdIsAnError, NoFallbackIsAnError (built-in root)

  [Fact] public void DataRootDefaultsToDocuments() =>
      Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Claude Game Helper"), AppPaths.DataRoot(null));
  [Fact] public void LogsLiveUnderTheDataRoot() => Assert.Equal(@"C:\d\logs", AppPaths.LogsDir(@"C:\d"));

  [Fact] public void V1SettingsWithProfilesFolderStillLoad()               // Review Focus 1
  { File.WriteAllText(_path, "{ \"profilesFolder\": \"C:\\\\x\", \"fastModel\": \"haiku\" }");
    var r = new SettingsStore(_path).Load(); Assert.Null(r.Error); Assert.Equal("haiku", r.Settings.FastModel); }
  [Fact] public void NotGamesRoundTrip() { /* Save NotGames ["launcher"] → Load equals */ }

  [Fact] public void PromptNamesTheGame() =>
      Assert.Equal("q\n\nGame: Valheim\n\nNo notes file is selected yet.", PromptBuilder.Build("q", [], "Valheim", "Notes", null));
  // existing PromptBuilder tests: pass gameName null; expected strings unchanged

  [Fact] public async Task MissingDataFolderIsCreatedBeforeClaudeRuns()   // Review Focus 4
  { /* profile.FolderPath doesn't exist → after AskAsync, Directory.Exists(FolderPath) and launch.WorkingDirectory == FolderPath */ }
  [Fact] public async Task GameNameGoesInThePromptExceptForTheFallback() { /* non-fallback → StdinText contains "Game: Game"; fallback → no "Game:" line */ }
  ```
- [ ] **Step 4: Run** `--filter "FullyQualifiedName~ProfileLoader|FullyQualifiedName~AppPaths|FullyQualifiedName~SettingsStore|FullyQualifiedName~PromptBuilder|FullyQualifiedName~AskService"` → FAIL (signatures).
- [ ] **Step 5: Implement.** Loader: built-ins from `builtInRoot` (skip `_template`; `profile.json` + `CLAUDE.md`
  required there, as in v1); `FolderPath = <dataRoot>\<id>`; `InstructionsPath` = data-folder `CLAUDE.md` if it
  exists, else the built-in one. Then each `<dataRoot>\<id>\profile.json` with `"auto": true` whose id isn't a
  built-in and isn't in `notGames` → `IsAuto = true`, instructions = override or template. PromptBuilder puts
  `Game: <name>` after the screenshot list and before the tracking line. AskService passes
  `profile.IsFallback ? null : profile.DisplayName` and calls `Directory.CreateDirectory(profile.FolderPath)`
  before running. App: load settings first, then `Log.Init(AppPaths.LogsDir(dataRoot))`; load profiles from
  `AppPaths.BuiltInProfilesDir` and `AppPaths.DataRoot(settings.DataFolder)`. Update existing tests that call
  the old signatures; `.gitignore` drops the `profiles/*` lines and adds `artifacts/` and `releases/`.
- [ ] **Step 6: Run the filter** → PASS. Run the app from the repo; the header says "Fallout 76" with his character
  in the picker when the game is in front (Jason can confirm later; at minimum the log shows "Ready" with
  `fallout-76` loaded).
- [ ] **Step 7: Commit** `feat: ship built-in instructions, keep data in Documents, add generic advisor`.

---

### Task 2: Automatic profiles and "Not a game"

**Files:**
- Create: `src/ClaudeGameHelper.Core/Profiles/AutoProfiles.cs`
- Modify: `HelperController.cs`, `PanelViewModel.cs`, `PanelWindow.xaml(.cs)`, `App.xaml.cs`
- Test: `tests/ClaudeGameHelper.Tests/Profiles/AutoProfilesTests.cs`, `HelperControllerTests.cs`, `PanelViewModelTests.cs`

**Interfaces:**
- Consumes: `ProfileLoader`, `GameProfile.IsAuto`, `ProfileLoadResult.TemplateInstructionsPath` (Task 1).
- Produces:
  ```csharp
  public static class AutoProfiles
  {
      public static string? KeyFor(string processName, string title);   // null = never a profile
      public static GameProfile? TryCreate(string dataRoot, string templatePath, string processName, string title,
          IReadOnlyCollection<string> notGames);                       // writes <dataRoot>\<key>\profile.json
  }
  // HelperController ctor gains two parameters (after ownProcessId):
  //   Func<WindowInfo, GameProfile?> createAutoProfile, Action<GameProfile> markNotAGame
  public void HelperController.NotAGame();
  public bool PanelViewModel.IsAutoProfile { get; }                    // set by SwitchProfile
  public event Action? PanelWindow.NotAGameRequested;
  ```

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  [Theory]
  [InlineData("valheim.exe", "Valheim", "valheim")]
  [InlineData("Cyberpunk2077", "Cyberpunk 2077 (C) 2020 by CD Projekt RED", "cyberpunk2077")]
  [InlineData("javaw", "Minecraft* 1.21: Tricky Trials", "minecraft-1-21-tricky-trials")]   // Review Focus 2
  public void KeysComeFromProcessOrForJavaFromTitle(string process, string title, string key) => Assert.Equal(key, AutoProfiles.KeyFor(process, title));
  [Theory] [InlineData("explorer")] [InlineData("chrome")] [InlineData("msedge")] [InlineData("firefox")] [InlineData("SearchHost")] [InlineData("ShellExperienceHost")]
  public void ShellAndBrowsersNeverGetProfiles(string process) => Assert.Null(AutoProfiles.KeyFor(process, "x"));
  [Fact] public void LongJavaTitlesAreCappedAt40Chars() => Assert.True(AutoProfiles.KeyFor("javaw", new string('a', 200))!.Length <= 40);
  [Fact] public void JavaWithEmptyTitleGetsNoProfile() => Assert.Null(AutoProfiles.KeyFor("javaw", "  "));

  [Fact] public void TryCreateWritesProfileJsonAndReturnsAutoProfile()
  { /* → IsAuto, Id "valheim", DisplayName "Valheim", TrackingLabel "Notes", InstructionsPath template,
       WindowMatch.ProcessNames ["valheim"]; reloading via ProfileLoader.LoadAll finds the same profile */ }
  [Fact] public void JavaProfilesMatchByTitle() { /* javaw → WindowMatch.Titles [title], ProcessNames [] */ }
  [Fact] public void NotGamesAreRefused() => Assert.Null(AutoProfiles.TryCreate(data, template, "launcher", "Launcher", ["launcher"]));
  [Fact] public void ExistingAutoProfileIsReused() { /* create twice → same profile, profile.json unchanged */ }

  // HelperControllerTests (fake createAutoProfile returns an auto profile for "newgame")
  [Fact] public async Task ScreenOnUnknownGameCreatesAndSwitchesToItsProfile()
  { /* foreground "newgame" → fallback; Screen → createAutoProfile called once; ActiveProfile is the new profile; request.Profile is it */ }
  [Fact] public void MerelyFocusingAnUnknownWindowCreatesNothing() { /* OnForegroundChanged only → createAutoProfile never called */ }
  [Fact] public async Task CreatedProfileIsMatchedNextTime() { /* after creation, foreground other app then "newgame" again → active without a second create */ }
  [Fact] public async Task NotAGameSwitchesToFallbackAndRemembers()
  { /* auto profile active → NotAGame() → markNotAGame got it; ActiveProfile fallback; refocusing "newgame" stays fallback;
       Screen on it calls createAutoProfile again (the App's notGames list is what refuses it) */ }
  [Fact] public void NotAGameIgnoredForBuiltIns() { /* fallout-76-style built-in active → no-op */ }

  [Fact] public void IsAutoProfileFollowsTheProfile() { /* SwitchProfile(auto) → true; built-in → false */ }
  ```
  Existing `HelperControllerTests` constructions get the two new arguments (`_ => null`, `_ => { }`).
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~AutoProfiles|FullyQualifiedName~HelperControllerTests|FullyQualifiedName~PanelViewModelTests"` → FAIL.
- [ ] **Step 3: Implement.** `KeyFor`: process name without `.exe`, lowercased; skip set (case-insensitive):
  `explorer, shellexperiencehost, startmenuexperiencehost, searchhost, searchapp, textinputhost, lockapp,
  applicationframehost, chrome, msedge, firefox, opera, brave, vivaldi`; host set `javaw, java` → key from the
  title: lowercase, runs of non-`[a-z0-9]` become `-`, trimmed of `-`, cut to 40 chars (then re-trim). Display
  name = title trimmed, else the process name. Profile JSON written with the loader's options:
  `{ displayName, auto: true, windowMatch: { processNames | titles }, trackingLabel: "Notes", allowWatchMode: true }`.
  Controller: remember the last non-own `WindowInfo`; at the start of `OnScreenHotkeyAsync` and
  `OnAskHotkeyAsync`, when the active profile is the fallback and that window isn't ours, call
  `createAutoProfile`; if it returns a profile, add it to the matchable list and switch to it (same path as a
  foreground switch, raising `ActiveProfileChanged`). `NotAGame()`: only for `IsAuto`; call `markNotAGame`,
  remove it from the list, switch to the fallback, `vm.AddNote($"Okay, {name} won't get a profile. Its notes are kept in {folder}.")`.
  Panel header: a flat "Not a game" button, visible when `IsAutoProfile`. App: `createAutoProfile` =
  `w => AutoProfiles.TryCreate(dataRoot, templatePath, w.ProcessName, w.Title, _settings.NotGames)`;
  `markNotAGame` adds `AutoProfiles.KeyFor(...)` of the profile (its `Id`) to `NotGames` and saves (same
  broken-file guard as panel bounds).
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat: create profiles automatically for any game`.

---

### Task 3: Start with Windows, version in the tray

**Files:**
- Create: `src/ClaudeGameHelper/Platform/StartupRegistration.cs`
- Modify: `Tray/TrayIcon.cs`, `App.xaml.cs`
- Test: `tests/ClaudeGameHelper.Tests/Platform/StartupRegistrationTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed class StartupRegistration(string runKeyPath = StartupRegistration.RunKey, string valueName = "ClaudeGameHelper")
  {
      public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";   // under HKCU
      public bool IsEnabled { get; }
      public void Enable(string exePath);    // value = "\"<exePath>\""
      public void Disable();                 // no-op when absent
  }
  // TrayIcon ctor gains: Func<bool> startWithWindows, Action<bool> setStartWithWindows, string versionText
  ```

- [ ] **Step 1: Write the failing tests** (run key `Software\ClaudeGameHelperTests\<guid>\Run`, deleted after each test).
  ```csharp
  [Fact] public void EnableWritesQuotedPathAndDisableRemovesIt()
  { var s = new StartupRegistration(testKey); Assert.False(s.IsEnabled);
    s.Enable(@"C:\Apps\Claude Game Helper\ClaudeGameHelper.exe"); Assert.True(s.IsEnabled);
    Assert.Equal("\"C:\\Apps\\Claude Game Helper\\ClaudeGameHelper.exe\"", Registry.CurrentUser.OpenSubKey(testKey)!.GetValue("ClaudeGameHelper"));
    s.Disable(); Assert.False(s.IsEnabled); s.Disable(); }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~StartupRegistrationTests"` → FAIL.
- [ ] **Step 3: Implement**, then the tray: a "Start with Windows" item with a checkmark refreshed on menu
  open (enable writes `Environment.ProcessPath`), and a disabled first item `Claude Game Helper <versionText>`.
  `versionText` comes from Task 4's `AppVersion.Current`; until then pass `"dev"`.
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat: start with Windows and show the version in the tray`.

---

### Task 4: Velopack installer, updates, and release script

**Files:**
- Create: `src/ClaudeGameHelper/Program.cs`, `Platform/UpdateChecker.cs`, `app.ico`, `.config/dotnet-tools.json`, `release.ps1`
- Modify: app `.csproj` (`StartupObject`, `ApplicationIcon`, `App.xaml` as `Page`, Velopack reference), `App.xaml.cs`
- Test: `tests/ClaudeGameHelper.Tests/Platform/UpdateCheckerTests.cs`

**Interfaces:**
- Consumes: `StartupRegistration` (Task 3).
- Produces:
  ```csharp
  public static class AppVersion { public static string Current { get; } }   // Velopack's installed version, else "dev"
  public static class UpdateChecker
  {
      public const string RepoUrl = "https://github.com/<login>/claude-game-helper";   // <login> from Step 1
      public static Task RunAsync();                       // installed builds only; never throws
      public static Task Safely(Func<Task> work);          // logs and swallows every exception
  }
  ```

- [ ] **Step 1: GitHub login.** Run `gh api user --jq .login`. If not signed in, ask Jason to run
  `! gh auth login` (interactive: GitHub.com, HTTPS, browser). Record the login in the ledger.
- [ ] **Step 2: Write the failing test.**
  ```csharp
  [Fact] public async Task SafelySwallowsAndLogsFailures()                 // Review Focus 3
  { await UpdateChecker.Safely(() => throw new HttpRequestException("offline")); }   // completes, no throw
  [Fact] public async Task RunAsyncIsANoOpWhenNotInstalled() { await UpdateChecker.RunAsync(); }   // test host isn't installed
  ```
- [ ] **Step 3: Run** `--filter "FullyQualifiedName~UpdateCheckerTests"` → FAIL.
- [ ] **Step 4: Implement.** `dotnet add src/ClaudeGameHelper package Velopack --version 1.2.161`;
  `dotnet new tool-manifest` + `dotnet tool install vpk --version 1.2.161`. `Program.Main` (`[STAThread]`):
  `VelopackApp.Build().OnAfterInstallFastCallback(_ => new StartupRegistration().Enable(Environment.ProcessPath!))
  .OnBeforeUninstallFastCallback(_ => new StartupRegistration().Disable()).Run();` then `new App()`,
  `InitializeComponent()`, `Run()`. csproj: `<StartupObject>ClaudeGameHelper.Program</StartupObject>`,
  `App.xaml` build action `Page`. `RunAsync`: `new UpdateManager(new GithubSource(RepoUrl, null, false))`; return
  if `!IsInstalled`; `CheckForUpdatesAsync` → `DownloadUpdatesAsync` →
  `WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false)` (applies when the helper
  exits, so the next start runs it); all inside `Safely`; log each step. `App.OnStartup` fires it after "Ready"
  without awaiting. `app.ico`: render the tray icon's drawing at 16/32/48/256 px (one-off script, commit the
  `.ico`); `<ApplicationIcon>app.ico</ApplicationIcon>`.
  `release.ps1 -Version <x.y.z> [-Publish]`: `dotnet tool restore`; `dotnet publish src/ClaudeGameHelper -c
  Release -r win-x64 --self-contained -o artifacts/publish`; if `-Publish`, `vpk download github --repoUrl
  <RepoUrl> --token (gh auth token) -o releases`; `vpk pack --packId ClaudeGameHelper --packVersion $Version
  --packDir artifacts/publish --mainExe ClaudeGameHelper.exe --packTitle "Claude Game Helper" --icon
  src/ClaudeGameHelper/app.ico -o releases`; if `-Publish`, `vpk upload github --repoUrl <RepoUrl> --token
  (gh auth token) --publish --releaseName "Claude Game Helper $Version" --tag v$Version -o releases`. Stop on
  the first failing command.
- [ ] **Step 5: Run the filter** → PASS.
- [ ] **Step 6: Installer check.** Quit any running helper; `./release.ps1 -Version 0.2.0`; run
  `releases\ClaudeGameHelper-win-Setup.exe`. Expected: no admin prompt; it installs and starts; Start menu
  and desktop shortcuts exist; `HKCU\…\Run\ClaudeGameHelper` points at the installed exe; the tray shows
  "Claude Game Helper 0.2.0"; the log (in `Documents\Claude Game Helper\logs`) shows "Ready" and an update
  check that finds nothing or fails quietly (the repo doesn't exist yet).
- [ ] **Step 7: Commit** `feat: add Velopack installer, automatic updates, and release script`.

---

### Task 5: Public-ready content

**Files:**
- Create: `README.md`, `LICENSE`, `docs/images/panel.png`
- Modify: test fixtures, `docs/HANDOFF.md`, both specs, both plans, `CLAUDE.md`

- [ ] **Step 1: LICENSE** — the standard MIT text, `Copyright (c) 2026 Jason Wilson`.
- [ ] **Step 2: Panel screenshot.** Render the panel with sample content (as the v1 visual check did, from a
  throwaway test that is not committed), capture it with `WindowCapture`, save as `docs/images/panel.png`.
- [ ] **Step 3: README** for strangers, sections: what it is (one paragraph + screenshot) · what you need
  (Windows 10/11; Claude Code installed and signed in to your own Claude account; uses your plan's usage) ·
  install (download `ClaudeGameHelper-win-Setup.exe` from Releases; "Windows protected your PC" → More info →
  Run anyway; starts with Windows, turn off in the tray) · use (the three hotkeys; Ask + Screen for several
  screens; Deep) · any game (profiles appear on first use; "Not a game"; tuning a game with your own
  `CLAUDE.md` in its folder) · where your stuff lives (`Documents\Claude Game Helper`, logs there; settings in
  `%APPDATA%\ClaudeGameHelper\settings.json`) · safety (only Windows screen capture of the game window; never
  reads game memory, injects, or sends input; Claude can write only its notes files) · building from source
  (`dotnet test`, `release.ps1`) · MIT license.
- [ ] **Step 4: Scrub personal details.** Fixtures: replace his Windows username (and its 8.3 short form)
  with `Player` (text replace; JSON stays valid; rerun `--filter "FullyQualifiedName~StreamJsonParser|FullyQualifiedName~ClaudeRunner"` → PASS).
  Docs (`docs/**`, `CLAUDE.md`): remove day-job references, the OneDrive folder path, PC paths with his
  username, and family members (rewrite §1 of the sharing spec as "first with family, then possibly
  anyone"). Keep game history and design reasoning. First name "Jason" is fine.
- [ ] **Step 5: Verify** with a case-insensitive search of all tracked files (`git grep -i -n`) for each
  personal term (work email domain, Windows username and short form, day-job folder and client names,
  OneDrive folder name, family words). Expected: no hits. The search terms come from this session's notes;
  don't write them into any committed file.
- [ ] **Step 6: Update `docs/HANDOFF.md`** status: sharing build done; how to release (`release.ps1`).
- [ ] **Step 7: Commit** `docs: add README and license; remove personal details`.

---

### Task 6: Publish

- [ ] **Step 1: Full suite.** `dotnet test` → all pass (the one full run for this plan).
- [ ] **Step 2: Ask Jason before rewriting history** (AskUserQuestion: proceed / not yet). On yes:
  `git bundle create ..\claude-game-helper-private-history.bundle --all` (backup outside the repo);
  `git config user.name "Jason Wilson"`; `git config user.email <his personal address>`;
  `git reset --soft <root commit>`; `git commit --amend --reset-author -m "Claude Game Helper 0.2.0"`.
  Expected: `git log --format='%an <%ae>'` shows one commit with his personal address; `git status` clean;
  the Step 5 search of Task 5 still finds nothing.
- [ ] **Step 3: Ask Jason before creating the repo and pushing** (AskUserQuestion). On yes:
  `gh repo create <login>/claude-game-helper --public --source . --remote origin --push --description
  "A side panel that answers questions about the game you're playing, powered by Claude Code."`
- [ ] **Step 4: Release.** `./release.ps1 -Version 0.2.0 -Publish`. Expected: the GitHub release `v0.2.0`
  lists `ClaudeGameHelper-win-Setup.exe`.
- [ ] **Step 5: Install from GitHub.** Download the setup file from the release page, run it, and confirm
  the Task 4 Step 6 checks, plus a log line showing the update check reached GitHub and found nothing newer.
- [ ] **Step 6: Update `docs/HANDOFF.md`** (repo URL, how releases work), commit `docs: record first public release`, `git push`.
