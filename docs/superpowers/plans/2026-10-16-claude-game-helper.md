# Claude Game Helper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Windows tray app that, on a hotkey, captures the game window or takes a typed question, runs
Claude Code in print mode inside the active game's profile folder, and streams the answer into a dark side
panel drawn over the game.

**Architecture:** A game-neutral class library (`ClaudeGameHelper.Core`: profiles, settings, the Claude
CLI wrapper, conversations, `AskService`) under a thin WPF shell (`ClaudeGameHelper`: Windows.Graphics.Capture,
global hotkeys, foreground tracking, the panel, the tray icon, and `HelperController`, which ties them
together). Everything game-specific lives in `profiles\<game>\`.

**Tech Stack:** C# / .NET 10 (SDK 10.0.302), WPF (+ WinForms for `NotifyIcon` only), Windows.Graphics.Capture
through the .NET Windows SDK projection, MdXaml for Markdown, xUnit.

**Spec:** [docs/superpowers/specs/2026-10-16-claude-game-helper-design.md](../specs/2026-10-16-claude-game-helper-design.md)

## Global Constraints

- Target frameworks: Core `net10.0`; app and tests `net10.0-windows10.0.22621.0`.
- **Anti-ban:** no reading game memory, no injecting into the game process, no keystrokes or mouse input sent
  to the game. The only touches on the game window: Windows.Graphics.Capture of it, and `SetForegroundWindow`
  to give focus back after the panel hides.
- **Game-neutral code:** no game names or game-specific rules under `src\`. A test enforces "Fallout" never
  appears there (Task 1).
- **Claude Code isolation (verified on this PC with 2.1.295 on 2026-10-16):** every run passes `--safe-mode`
  (no user plugins, hooks, global or project CLAUDE.md; normal OAuth login), `--restricted` (file tools stay
  inside the working dir and `--add-dir` dirs; no shell), `--disable-slash-commands`, `--permission-mode dontAsk`.
  The profile's instructions arrive through `--append-system-prompt-file <profile>\CLAUDE.md`. Do not use
  `--bare`: it refuses OAuth and requires an API key.
- Prompt goes to Claude on **stdin, UTF-8 without BOM**, never on the command line.
- Defaults (spec §6.6, §7.7, §7.8): hotkeys `Ctrl+Shift+F1` Screen, `Ctrl+Shift+F2` Ask, `Ctrl+Shift+F3`
  Toggle; fast model `sonnet` / effort `low`; deep model `opus` / effort `medium`; screenshots JPEG quality 90,
  long edge at most 1568 px (Claude's own resize limit, so nothing larger is worth sending); conversation
  rolls over at 100,000 context tokens.
- Panel: opaque window (never `AllowsTransparency="True"`, which costs the game frame rate), topmost, does
  not take focus unless in Ask mode.
- American English in code, comments, and UI text. Answers and UI copy lead with the verdict.
- Git: commit on `master`; never create a branch.
- Tests: mid-task run only the filter shown in each step. The full suite runs once, in Task 11.

## Review Focus

1. **Game window minimized, closed, or not capturable when a hotkey fires** → panel says
   "Couldn't capture the game window: …"; Ask still opens without a screenshot; nothing is sent. (Task 10)
2. **Hotkey pressed while an answer is still streaming** → no second Claude process; the status line says
   "Still answering — wait for it or press Stop." (Tasks 6, 10)
3. **Claude Code missing, crashing, or Jason hitting his usage limit mid-game** → a plain message in the
   panel (what happened, what to fix), never a silent hang. (Tasks 3, 6)
4. **Jason hand-edits `settings.json` and breaks it** (trailing comma, typo) → app still starts on
   defaults, tells him, and does not overwrite his file. (Task 2)
5. **Questions with quotes, line breaks, or non-ASCII** ("Grognak's Axe — worth it?") → reach Claude intact.
   (Task 4)

---

## File Structure

```
ClaudeGameHelper.slnx
src\ClaudeGameHelper.Core\           net10.0 class library, no UI
  Profiles\  GameProfile.cs  ProfileLoader.cs  ProfileMatcher.cs
  Settings\  AppSettings.cs  SettingsStore.cs  AppPaths.cs
  Hotkeys\   HotkeyChord.cs  HotkeyBindings.cs
  Claude\    ClaudeLocator.cs  ClaudeArgsBuilder.cs  ClaudeEvents.cs  StreamJsonParser.cs  ClaudeRunner.cs
  Conversations\ PromptBuilder.cs  ModelSelector.cs  TrackingFiles.cs  ConversationSession.cs
                 ConversationArchive.cs  AskService.cs
  Layout\    ScreenshotSizing.cs  PanelPlacement.cs
  Log.cs
src\ClaudeGameHelper\                WPF WinExe
  App.xaml(.cs)  app.manifest  HelperController.cs  IPanelView.cs  IGameCapture.cs
  Capture\   WindowCapture.cs  WgcInterop.cs
  Windows\   GlobalHotkeys.cs  ForegroundWatcher.cs  WindowInfo.cs  FocusHelper.cs  NativeMethods.cs
  Panel\     PanelWindow.xaml(.cs)  PanelViewModel.cs  ChatMessage.cs  MarkdownView.cs
  Tray\      TrayIcon.cs
tests\ClaudeGameHelper.Tests\        xUnit; references both projects
  Fixtures\  tools-and-denials.jsonl  resume-text-only.jsonl   (already committed: real 2.1.295 output)
profiles\fallout-76\  profile.json  CLAUDE.md  tracking\.gitkeep
profiles\general\     profile.json  CLAUDE.md
```

---

### Task 1: Solution scaffold and game profiles

**Files:**
- Create: `ClaudeGameHelper.slnx`, the three `.csproj` files, `src/ClaudeGameHelper.Core/Profiles/*.cs`
- Create: `profiles/fallout-76/{profile.json,CLAUDE.md,tracking/.gitkeep}`, `profiles/general/{profile.json,CLAUDE.md}`
- Test: `tests/ClaudeGameHelper.Tests/Profiles/ProfileLoaderTests.cs`, `ProfileMatcherTests.cs`, `GameNeutralityTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record WindowMatch(IReadOnlyList<string> ProcessNames, IReadOnlyList<string> Titles);   // exact, case-insensitive
  public sealed record HotkeyOverrides(string? Screen, string? Ask, string? Toggle);
  public sealed record ModelOverrides(string? FastModel, string? FastEffort, string? DeepModel, string? DeepEffort);
  public sealed record GameProfile(string Id, string DisplayName, string FolderPath, WindowMatch? WindowMatch,
      IReadOnlyList<string> ReadOnlyPaths, string? TrackingLabel, HotkeyOverrides? Hotkeys,
      ModelOverrides? Models, bool AllowWatchMode, bool IsFallback)
  {   // computed properties, absolute: <FolderPath>\CLAUDE.md, \tracking, \screenshots, \conversations
      public string InstructionsPath { get; } public string TrackingDir { get; }
      public string ScreenshotsDir { get; }   public string ConversationsDir { get; }
  }
  public sealed record ProfileLoadResult(IReadOnlyList<GameProfile> Profiles, GameProfile? Fallback, IReadOnlyList<string> Errors);
  public static class ProfileLoader { public static ProfileLoadResult LoadAll(string profilesRoot); }
  public static class ProfileMatcher { public static GameProfile? Match(IReadOnlyList<GameProfile> profiles, string processName, string windowTitle); }
  ```

- [ ] **Step 1: Scaffold.** `dotnet new sln -n ClaudeGameHelper`; `dotnet new classlib` at `src/ClaudeGameHelper.Core`
  (`net10.0`, nullable on); `dotnet new wpf` at `src/ClaudeGameHelper` (TFM `net10.0-windows10.0.22621.0`,
  `UseWPF` and `UseWindowsForms` true, `<Using Remove="System.Windows.Forms" />` to avoid name clashes);
  `dotnet new xunit` at `tests/ClaudeGameHelper.Tests` (same TFM, `UseWPF` true, references both projects).
  Add all three to the solution. `dotnet build` succeeds.

- [ ] **Step 2: Write the profile files.** `profiles/fallout-76/profile.json`:
  ```json
  {
    "displayName": "Fallout 76",
    "windowMatch": { "processNames": ["Project76_GamePass", "Fallout76"], "titles": ["Fallout76", "Fallout 76"] },
    "readOnlyPaths": ["{Documents}\\My Games\\Fallout 76"],
    "trackingLabel": "Character",
    "allowWatchMode": true
  }
  ```
  (`Project76_GamePass.exe` is the game executable named in the installed Store package's `MicrosoftGame.config`.)
  `profiles/general/profile.json`: `{ "displayName": "No game", "isFallback": true, "allowWatchMode": false }`.

  `profiles/fallout-76/CLAUDE.md`, exact text:
  ```markdown
  # Fallout 76 advisor

  You are Jason's Fallout 76 advisor, shown in a narrow side panel while he plays. He came back in October
  2026 after more than a year away: assume the game has changed since he last played and since your training.

  ## How to answer
  - Lead with the verdict or direction in one sentence. Add detail only if it helps, as a few short bullets.
  - Travel directions in plain words: compass direction and landmarks ("head northeast to the Whitespring,
    enter through the side gate").
  - Loot verdicts are one of keep, sell, scrap, scrip, or learn, followed by why in one sentence.
  - If unsure, say so and name the screenshot that would settle it.

  ## Check before you claim
  - Search the web before stating anything about current balance, perk or legendary perk effects, drop
    sources, events, vendors, prices, or season rewards. Prefer: the Fallout Wiki (fallout.fandom.com),
    Bethesda patch notes (fallout.bethesda.net), the Nukes & Dragons planner (nukesdragons.com), then
    community guides under a year old.
  - When it matters, say which patch or date your answer reflects.

  ## Never
  - Suggest exploits, glitches, duping, or anything else that risks an account ban.

  ## Character files
  - One file per character in `tracking/`, named after the character in lowercase with hyphens
    (`tracking/vault-dweller.md`). Each message names the active file; read it before answering anything
    about the character.
  - When a screenshot or the conversation shows something new about the character, update the file yourself
    without asking, then end your answer with one line: `Updated <character>: <what changed>`.
  - If no file is active and the screens show a character, create its file, reading the name from the
    screen. Ask for the name only if you can't read it.
  - Sections: Summary (level, build), SPECIAL, Perk cards, Legendary perks, Mutations, Weapons, Armor, Goals,
    Wishlist, Notable stash. Keep it current, not a history.
  ```
  `profiles/general/CLAUDE.md`, exact text:
  ```markdown
  # Game helper (no game detected)

  You are Jason's advisor in a narrow side panel while he plays PC games. No game profile matched the window
  in front, so you know the game only if he says or a screenshot shows it.
  - Lead with the answer in one sentence; add short bullets only if they help.
  - Search the web before stating anything about a game's current state (balance, patches, drops, events).
  - Never suggest exploits or anything else that risks an account ban.
  - Say when you're unsure.
  ```

- [ ] **Step 3: Write the failing tests.**
  ```csharp
  // ProfileLoaderTests — use the repo's real profiles folder, found by walking up from AppContext.BaseDirectory
  [Fact] public void LoadsFallout76AndFallback()
  {
      var r = ProfileLoader.LoadAll(RepoProfilesRoot);
      Assert.Empty(r.Errors);
      var f76 = Assert.Single(r.Profiles, p => p.Id == "fallout-76");
      Assert.Equal("Character", f76.TrackingLabel);
      Assert.Contains("Project76_GamePass", f76.WindowMatch!.ProcessNames);
      Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout 76"), f76.ReadOnlyPaths[0]);
      Assert.Equal("general", r.Fallback!.Id);
      Assert.DoesNotContain(r.Profiles, p => p.IsFallback);   // Profiles holds only matchable games
  }
  [Fact] public void BadJsonIsReportedAndOtherProfilesStillLoad()   // temp root: one good, one "{ nope"
  { /* Errors has one entry naming the bad folder; the good profile loads */ }
  [Fact] public void MissingClaudeMdIsAnError() { /* Errors entry contains "CLAUDE.md" */ }
  [Fact] public void NoFallbackIsAnError() { /* Fallback null, Errors contains "fallback" */ }

  // ProfileMatcherTests
  [Fact] public void MatchesProcessNameIgnoringCaseAndExe() => Assert.Equal("fallout-76", ProfileMatcher.Match(all, "project76_gamepass.exe", "x")!.Id);
  [Fact] public void FallsBackToTitle() => Assert.Equal("fallout-76", ProfileMatcher.Match(all, "unknown", "Fallout76")!.Id);
  [Fact] public void ProcessMatchBeatsTitleMatchOfAnotherProfile() { /* profile A title "Steam", profile B process "game" → B */ }
  [Fact] public void BrowserTabAboutTheGameIsNotTheGame() => Assert.Null(ProfileMatcher.Match(all, "chrome", "Fallout 76 wiki - Chrome"));
  ```
  Titles match **exactly** (ignoring case), not by "contains", so a wiki tab never counts as the game.

  ```csharp
  // GameNeutralityTests
  [Fact] public void SourceNeverNamesAGame()
  {   // every *.cs and *.xaml under <repo>\src
      Assert.Empty(SourceFiles().Where(f => File.ReadAllText(f).Contains("Fallout", StringComparison.OrdinalIgnoreCase)));
  }
  ```

- [ ] **Step 4: Run to verify they fail.** `dotnet test tests/ClaudeGameHelper.Tests --filter "FullyQualifiedName~Profiles"` → compile errors (types missing).

- [ ] **Step 5: Implement** the records, `ProfileLoader.LoadAll` and `ProfileMatcher.Match`. Loader: one subfolder per
  profile, `Id` = folder name; `System.Text.Json` with camelCase, comments and trailing commas allowed; `{Documents}`
  expands to `Environment.GetFolderPath(MyDocuments)` (this follows the OneDrive redirect), then
  `Environment.ExpandEnvironmentVariables`. Folders without `profile.json` are skipped silently.

- [ ] **Step 6: Run the filter again** → PASS.

- [ ] **Step 7: Commit** `feat: scaffold solution and add Fallout 76 and fallback profiles`.

---

### Task 2: Settings, paths, hotkey chords, log

**Files:**
- Create: `src/ClaudeGameHelper.Core/Settings/{AppSettings,SettingsStore,AppPaths}.cs`, `Hotkeys/{HotkeyChord,HotkeyBindings}.cs`, `Log.cs`
- Test: `tests/ClaudeGameHelper.Tests/Settings/SettingsStoreTests.cs`, `AppPathsTests.cs`, `Hotkeys/HotkeyChordTests.cs`

**Interfaces:**
- Consumes: `GameProfile`, `HotkeyOverrides` (Task 1).
- Produces:
  ```csharp
  public sealed record PanelBounds(double X, double Y, double Width, double Height);
  public sealed record AppSettings   // JSON camelCase; init-only properties with these defaults
  {
      public string? ClaudePath { get; init; }          public string? ProfilesFolder { get; init; }
      public string ScreenHotkey { get; init; } = "Ctrl+Shift+F1";
      public string AskHotkey { get; init; } = "Ctrl+Shift+F2";
      public string ToggleHotkey { get; init; } = "Ctrl+Shift+F3";
      public string FastModel { get; init; } = "sonnet";  public string FastEffort { get; init; } = "low";
      public string DeepModel { get; init; } = "opus";    public string DeepEffort { get; init; } = "medium";
      public int RolloverContextTokens { get; init; } = 100_000;
      public int ScreenshotMaxLongEdge { get; init; } = 1568;
      public int ScreenshotJpegQuality { get; init; } = 90;
      public PanelBounds? Panel { get; init; }
  }
  public sealed record SettingsLoadResult(AppSettings Settings, string? Error);
  public sealed class SettingsStore { public SettingsStore(string path); public SettingsLoadResult Load(); public void Save(AppSettings s); }
  public static class AppPaths
  {
      public static string SettingsFile { get; }   // %APPDATA%\ClaudeGameHelper\settings.json
      public static string LogsDir { get; }        // %LOCALAPPDATA%\ClaudeGameHelper\logs
      public static string? FindProfilesRoot(string? configured, string startDir);
  }
  [Flags] public enum HotkeyModifiers : uint { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }   // = Win32 MOD_*
  public sealed record HotkeyChord(HotkeyModifiers Modifiers, uint VirtualKey, string Text)
  { public static HotkeyChord Parse(string text); }   // FormatException naming the text on failure
  public sealed record HotkeyBindingSet(HotkeyChord Screen, HotkeyChord Ask, HotkeyChord Toggle);
  public static class HotkeyBindings { public static HotkeyBindingSet Resolve(AppSettings s, GameProfile? p); }
  public static class Log { public static void Init(string dir); public static void Info(string m); public static void Error(string m, Exception? ex = null); }
  ```

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  [Fact] public void MissingFileIsCreatedWithDefaults()
  { var r = new SettingsStore(tmp).Load(); Assert.Null(r.Error); Assert.Equal("Ctrl+Shift+F1", r.Settings.ScreenHotkey); Assert.True(File.Exists(tmp)); }
  [Fact] public void BrokenFileFallsBackToDefaultsAndIsNotOverwritten()           // Review Focus 4
  { File.WriteAllText(tmp, "{ \"fastModel\": \"haiku\", oops }"); var r = new SettingsStore(tmp).Load();
    Assert.NotNull(r.Error); Assert.Contains("settings.json", r.Error); Assert.Equal("sonnet", r.Settings.FastModel);
    Assert.Equal("{ \"fastModel\": \"haiku\", oops }", File.ReadAllText(tmp)); }
  [Fact] public void CommentsAndTrailingCommasAreAccepted()
  { File.WriteAllText(tmp, "{ // mine\n \"fastModel\": \"haiku\", }"); Assert.Equal("haiku", new SettingsStore(tmp).Load().Settings.FastModel); }
  [Fact] public void SaveRoundTripsPanelBounds() { /* Save with Panel = new(10,20,440,900) → Load equals */ }

  [Theory]
  [InlineData("Ctrl+Shift+F1", HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0x70u)]
  [InlineData(" control + alt + k ", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 0x4Bu)]
  [InlineData("Win+Shift+F24", HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x87u)]
  [InlineData("Ctrl+7", HotkeyModifiers.Ctrl, 0x37u)]
  public void ParsesChords(string text, HotkeyModifiers mods, uint vk) { var c = HotkeyChord.Parse(text); Assert.Equal(mods, c.Modifiers); Assert.Equal(vk, c.VirtualKey); }
  [Theory] [InlineData("")] [InlineData("Ctrl+")] [InlineData("Ctrl+Banana")] [InlineData("Ctrl+F1+F2")]
  public void RejectsBadChords(string text) => Assert.Contains($"\"{text}\"", Assert.Throws<FormatException>(() => HotkeyChord.Parse(text)).Message);
  [Fact] public void ProfileOverrideWinsPerKey() { /* profile Hotkeys(Screen:"Ctrl+Alt+S", null, null) → Screen overridden, Ask/Toggle defaults */ }

  [Fact] public void FindsRepoProfilesByWalkingUp() { /* startDir = AppContext.BaseDirectory → returns <repo>\profiles */ }
  [Fact] public void ConfiguredProfilesFolderWins() { /* configured existing dir → returned as-is */ }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~Settings|FullyQualifiedName~Hotkeys"` → FAIL (types missing).
- [ ] **Step 3: Implement.** Settings JSON options: camelCase, `WriteIndented`, `ReadCommentHandling.Skip`,
  `AllowTrailingCommas`. `FindProfilesRoot`: the configured folder if it exists; otherwise walk up from
  `startDir` to the first `profiles` folder containing `general\profile.json`; otherwise null. Chord keys: F1–F24,
  A–Z, 0–9; modifier names `Ctrl`/`Control`, `Shift`, `Alt`, `Win`, any case, whitespace ignored; exactly one
  non-modifier key. `Log` appends `yyyy-MM-dd HH:mm:ss.fff LEVEL message` lines to `helper-yyyyMMdd.log` under a lock;
  before `Init` it is a no-op.
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat: add settings, hotkey chords, and logging`.

---

### Task 3: Claude CLI — locating, arguments, stream parsing

**Files:**
- Create: `src/ClaudeGameHelper.Core/Claude/{ClaudeLocator,ClaudeArgsBuilder,ClaudeEvents,StreamJsonParser}.cs`
- Modify: test `.csproj` — `<None Update="Fixtures\**" CopyToOutputDirectory="PreserveNewest" />`
- Test: `tests/ClaudeGameHelper.Tests/Claude/{ClaudeLocatorTests,ClaudeArgsBuilderTests,StreamJsonParserTests}.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record ClaudeLocateResult(string? Path, IReadOnlyList<string> Tried);
  public static class ClaudeLocator
  {   // probe(path) => true when "<path> --version" exits 0 and prints "Claude Code"
      public static ClaudeLocateResult Find(string? configuredPath, string? pathEnv, string vscodeExtensionsDir, Func<string, bool> probe);
      public static bool ProbeVersion(string path);   // real probe, 10 s timeout
  }
  public sealed record ClaudeRequest(string SessionId, bool IsFirstTurn, string Model, string Effort,
      string InstructionsPath, IReadOnlyList<string> ReadOnlyDirs);
  public static class ClaudeArgsBuilder { public static IReadOnlyList<string> Build(ClaudeRequest r); }

  public abstract record ClaudeEvent;
  public sealed record SessionStarted(string SessionId, string Model) : ClaudeEvent;
  public sealed record TextDelta(string Text) : ClaudeEvent;
  public sealed record Activity(string Text) : ClaudeEvent;            // status line copy
  public sealed record ContextSize(int Tokens) : ClaudeEvent;          // input + cache_read + cache_creation
  public sealed record ToolDenied(string ToolName, string Message) : ClaudeEvent;
  public sealed record UsageLimit(DateTimeOffset? ResetsAt) : ClaudeEvent;
  public sealed record Completed(string ResultText, bool IsError, string? Subtype) : ClaudeEvent;
  public static class StreamJsonParser { public static IReadOnlyList<ClaudeEvent> ParseLine(string line); }
  ```

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  // ClaudeLocatorTests (temp dirs; fake probe = set of "working" paths)
  [Fact] public void ConfiguredPathWinsWhenItWorks();
  [Fact] public void BrokenPathEntryIsSkippedForNewestExtension()
  {   // PATH dir holds claude.cmd (probe false); extensions dir holds anthropic.claude-code-2.1.284-win32-x64 and
      // anthropic.claude-code-2.1.295-win32-x64, each with resources\native-binary\claude.exe (both probe true)
      var r = ClaudeLocator.Find(null, pathDir, extDir, probe);
      Assert.EndsWith(@"anthropic.claude-code-2.1.295-win32-x64\resources\native-binary\claude.exe", r.Path);
      Assert.Contains(r.Tried, t => t.EndsWith("claude.cmd"));
  }
  [Fact] public void VersionsCompareNumericallyNotAsText() { /* 2.1.1000 beats 2.1.295 */ }
  [Fact] public void NothingWorksReturnsNullPathWithEverythingTried();

  // ClaudeArgsBuilderTests
  [Fact] public void FirstTurnArgsAreExact()
  {
      var a = ClaudeArgsBuilder.Build(new("11111111-1111-1111-1111-111111111111", true, "sonnet", "low", @"C:\p\CLAUDE.md", [@"C:\docs\Fallout 76"]));
      Assert.Equal(new[] { "-p", "--output-format", "stream-json", "--verbose", "--include-partial-messages",
          "--model", "sonnet", "--effort", "low", "--session-id", "11111111-1111-1111-1111-111111111111",
          "--safe-mode", "--restricted", "--disable-slash-commands",
          "--tools", "WebSearch,WebFetch,Read,Glob,Grep,Edit,Write",
          "--permission-mode", "dontAsk",
          "--allowedTools", "WebSearch,WebFetch,Read,Glob,Grep,Edit(./tracking/**),Write(./tracking/**)",
          "--add-dir", @"C:\docs\Fallout 76",
          "--append-system-prompt-file", @"C:\p\CLAUDE.md" }, a);
  }
  [Fact] public void LaterTurnsResume() { /* IsFirstTurn false → "--resume", id in place of "--session-id", id */ }
  [Fact] public void NoReadOnlyDirsMeansNoAddDir();

  // StreamJsonParserTests — fixtures are real output from claude 2.1.295
  [Fact] public void ToolsFixture()
  {
      var ev = File.ReadLines(Fixture("tools-and-denials.jsonl")).SelectMany(StreamJsonParser.ParseLine).ToList();
      Assert.Equal(new SessionStarted("97e4711d-90c5-4b5a-bc95-ed6aafca3d69", "claude-haiku-5-5"), ev[0]);
      Assert.Contains(new Activity("Updating a.md"), ev);
      Assert.Contains(new Activity("Looking at prefs.ini"), ev);
      Assert.Equal(3, ev.OfType<ToolDenied>().Count());
      var done = Assert.IsType<Completed>(ev[^1]); Assert.False(done.IsError); Assert.EndsWith("PINEAPPLE", done.ResultText);
  }
  [Fact] public void ResumeFixture()
  {
      var ev = File.ReadLines(Fixture("resume-text-only.jsonl")).SelectMany(StreamJsonParser.ParseLine).ToList();
      Assert.Equal("claude-sonnet-5-5", ev.OfType<SessionStarted>().Single().Model);
      Assert.Equal(10959, ev.OfType<ContextSize>().Last().Tokens);          // 2 + 4447 + 6510
      Assert.Equal("Step 4's answer was `iSize W=2560`, so the value is 2560.\n\nPINEAPPLE", string.Concat(ev.OfType<TextDelta>().Select(d => d.Text)));
  }
  [Theory]
  [InlineData("WebSearch", """{"query":"treasury notes farming"}""", "Searching the web: treasury notes farming")]
  [InlineData("WebFetch", """{"url":"https://fallout.fandom.com/wiki/Treasury_note","prompt":"x"}""", "Reading fallout.fandom.com")]
  [InlineData("Read", """{"file_path":"C:\\p\\screenshots\\20261016-201512-123.jpg"}""", "Looking at 20261016-201512-123.jpg")]
  [InlineData("Grep", """{"pattern":"x"}""", "Checking files")]
  [InlineData("Agent", """{}""", "Using Agent")]
  public void ToolUseBecomesActivity(string tool, string input, string expected) { /* assistant line with one tool_use block */ }
  [Fact] public void ThinkingBlockStartIsThinkingActivity() => /* content_block_start thinking */ Assert.Equal(new Activity("Thinking…"), …);
  [Fact] public void RejectedRateLimitIsUsageLimit()   // Review Focus 3
  { /* {"type":"rate_limit_event","rate_limit_info":{"status":"rejected","resetsAt":1792189800}} → UsageLimit(FromUnixTimeSeconds(1792189800)); status "allowed_warning" → no events */ }
  [Fact] public void ErrorResultIsCompletedWithError()  // Review Focus 3
  { /* {"type":"result","subtype":"success","is_error":true,"result":"You've hit your limit · resets 3pm"} → Completed(that text, true, "success") */ }
  [Fact] public void GarbageAndUnknownLinesYieldNothing() { Assert.Empty(StreamJsonParser.ParseLine("not json")); Assert.Empty(StreamJsonParser.ParseLine("""{"type":"system","subtype":"status"}""")); }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~Claude"` → FAIL.
- [ ] **Step 3: Implement.** Locator order: configured path → each PATH dir's `claude.exe` then `claude.cmd` →
  `anthropic.claude-code-*-win32-x64` folders sorted by `Version.Parse` of the middle part, newest first,
  `resources\native-binary\claude.exe`. Parser mapping (from the fixtures): `system/init` → `SessionStarted`;
  `stream_event` with `content_block_delta`/`text_delta` → `TextDelta`; `stream_event` `content_block_start` of
  `thinking` → `Activity("Thinking…")`; `assistant` → `ContextSize` from `message.usage`, plus one `Activity` per
  `tool_use` block (WebSearch → `Searching the web: {query}`, WebFetch → `Reading {host}`, Read →
  `Looking at {file name}`, Edit/Write → `Updating {file name}`, Glob/Grep → `Checking files`, else `Using {name}`);
  `system/permission_denied` → `ToolDenied`; `rate_limit_event` with status `rejected` → `UsageLimit`;
  `result` → `Completed(result, is_error, subtype)`. Anything else, or invalid JSON, → empty list.
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat: locate Claude Code, build its arguments, parse its stream`.

---

### Task 4: Claude runner (process)

**Files:**
- Create: `src/ClaudeGameHelper.Core/Claude/ClaudeRunner.cs`
- Test: `tests/ClaudeGameHelper.Tests/Claude/ClaudeRunnerTests.cs`

**Interfaces:**
- Consumes: `StreamJsonParser`, `ClaudeEvent`, `Completed` (Task 3).
- Produces:
  ```csharp
  public sealed record ClaudeLaunch(string ExePath, IReadOnlyList<string> Args, string WorkingDirectory, string StdinText);
  public sealed record ClaudeRunResult(int? ExitCode, bool Canceled, string Stderr, Completed? Completion);
  public interface IClaudeRunner { Task<ClaudeRunResult> RunAsync(ClaudeLaunch launch, Action<ClaudeEvent> onEvent, CancellationToken ct); }
  public sealed class ClaudeRunner : IClaudeRunner { public static ProcessStartInfo CreateStartInfo(ClaudeLaunch launch); }
  ```

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  [Fact] public void StartInfoUsesArgumentListAndUtf8WithoutBom()          // Review Focus 5
  {
      var si = ClaudeRunner.CreateStartInfo(new("claude.exe", ["-p", "a \"quoted\" arg"], @"C:\p", "Grognak's Axe — worth it?\nline 2"));
      Assert.Equal(new[] { "-p", "a \"quoted\" arg" }, si.ArgumentList);
      Assert.Equal("", si.Arguments);
      Assert.Empty(si.StandardInputEncoding!.GetPreamble());
      Assert.Equal(Encoding.UTF8.WebName, si.StandardOutputEncoding!.WebName);
      Assert.True(si.RedirectStandardInput && si.RedirectStandardOutput && si.RedirectStandardError && si.CreateNoWindow);
      Assert.False(si.UseShellExecute);
      Assert.Equal(@"C:\p", si.WorkingDirectory);
  }
  [Fact] public async Task StreamsFixtureEventsAndCompletion()
  {   // cmd.exe /c type <fixture>
      var events = new List<ClaudeEvent>();
      var r = await new ClaudeRunner().RunAsync(new("cmd.exe", ["/c", "type", Fixture("resume-text-only.jsonl")], Path.GetTempPath(), ""), events.Add, default);
      Assert.Equal(0, r.ExitCode); Assert.NotNull(r.Completion); Assert.Contains(events, e => e is SessionStarted);
  }
  [Fact] public async Task CancelKillsTheProcessQuickly()
  {   // powershell.exe -NoProfile -Command "Start-Sleep -Seconds 30"; cancel after 300 ms
      Assert.True(r.Canceled); Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10));
  }
  [Fact] public async Task NonZeroExitReportsStderr()
  {   // cmd.exe /c "echo boom 1>&2 & exit /b 3"
      Assert.Equal(3, r.ExitCode); Assert.Contains("boom", r.Stderr); Assert.Null(r.Completion);
  }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~ClaudeRunnerTests"` → FAIL.
- [ ] **Step 3: Implement.** Write `StdinText` and close stdin (Claude waits for EOF). Read stdout line by line
  on a background task, invoking `onEvent` for each parsed event in order; keep the last `Completed`. Collect
  stderr fully. On cancellation `Process.Kill(entireProcessTree: true)` and return `Canceled = true`. Log the
  exe and args (not the prompt) and the exit code.
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat: run Claude Code and stream its events`.

---

### Task 5: Conversation pieces

**Files:**
- Create: `src/ClaudeGameHelper.Core/Conversations/{PromptBuilder,ModelSelector,TrackingFiles,ConversationSession,ConversationArchive}.cs`
- Test: `tests/ClaudeGameHelper.Tests/Conversations/` — one test file per class

**Interfaces:**
- Consumes: `AppSettings` (Task 2), `GameProfile` (Task 1).
- Produces:
  ```csharp
  public static class PromptBuilder
  {
      public const string ScreenQuestion = "What am I looking at, and what should I do?";
      public static string Build(string question, IReadOnlyList<string> screenshotPaths, string? trackingLabel, string? activeTrackingName);
  }
  public sealed record ModelChoice(string Model, string Effort, bool IsDeep);
  public static class ModelSelector { public static ModelChoice Choose(AppSettings s, GameProfile p, int screenshotCount, bool deep); }
  public static class TrackingFiles
  {
      public static IReadOnlyList<string> List(GameProfile p);          // names without ".md", sorted, case-insensitive
      public static string RelativePath(string name);                    // "tracking/{name}.md"
      public static string FullPath(GameProfile p, string name);
  }
  public sealed class ConversationSession
  {
      public ConversationSession(GameProfile profile, int rolloverTokens, Func<Guid> newId, Func<DateTime> now);
      public string SessionId { get; }  public bool HasStarted { get; }  public string ArchivePath { get; }
      public bool BeginTurn();               // true if it rolled over to a fresh session first
      public void MarkStarted();             // call on SessionStarted
      public void RecordContext(int tokens); // call on ContextSize
      public void Reset();                   // "New conversation"
  }
  public static class ConversationArchive
  {
      public static void AppendTurn(string archivePath, string profileName, DateTime at, string question,
          IReadOnlyList<string> screenshotPaths, string model, string answer);
  }
  ```

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  [Fact] public void PromptWithScreenshotsAndActiveFile()
  {
      Assert.Equal(
          "Is this good?\n\nScreenshots of my game screen (view each with the Read tool):\n- C:\\p\\screenshots\\a.jpg\n- C:\\p\\screenshots\\b.jpg\n\nActive character file: tracking/vault-dweller.md",
          PromptBuilder.Build("Is this good?", [@"C:\p\screenshots\a.jpg", @"C:\p\screenshots\b.jpg"], "Character", "vault-dweller"));
  }
  [Fact] public void PromptWithNoActiveFileSaysSo() => Assert.EndsWith("\n\nNo character file is selected yet.", PromptBuilder.Build("q", [], "Character", null));
  [Fact] public void PromptWithoutTrackingLabelIsJustTheQuestion() => Assert.Equal("q", PromptBuilder.Build("q", [], null, null));

  [Theory]
  [InlineData(1, false, "sonnet", "low", false)]
  [InlineData(2, false, "opus", "medium", true)]
  [InlineData(0, true, "opus", "medium", true)]
  public void ChoosesModel(int shots, bool deep, string model, string effort, bool isDeep);
  [Fact] public void ProfileModelOverrideWins() { /* Models(FastModel:"haiku") → fast choice is haiku/low */ }

  [Fact] public void TrackingListsMarkdownNamesOnly() { /* a.md, B.md, notes.txt → ["a","B"]; missing dir → [] */ }

  [Fact] public void FirstTurnUsesNewSessionThenResumes()
  { var s = New(); Assert.False(s.BeginTurn()); Assert.False(s.HasStarted); s.MarkStarted(); Assert.True(s.HasStarted); }
  [Fact] public void RollsOverAtThreshold()
  { var s = New(rollover: 100_000); var first = s.SessionId; s.MarkStarted(); s.RecordContext(100_000);
    Assert.True(s.BeginTurn()); Assert.NotEqual(first, s.SessionId); Assert.False(s.HasStarted); }
  [Fact] public void ArchiveFileNamedForSessionStart() => Assert.Equal(Path.Combine(p.ConversationsDir, "2026-10-16_201500.md"), New(now: new DateTime(2026, 10, 16, 20, 15, 0)).ArchivePath);
  [Fact] public void ResetStartsFreshSessionAndArchive();

  [Fact] public void ArchiveWritesHeaderOnceThenTurns()
  {   // two AppendTurn calls; file text equals:
      // "# Fallout 76 — 2026-10-16 20:15\n\n## You · 20:15\nIs this good?\n\n![screenshot](../screenshots/a.jpg)\n\n## Claude · sonnet · 20:15\nScrip it.\n\n## You · 20:16\n…"
  }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~Conversations"` → FAIL.
- [ ] **Step 3: Implement** to the tests. Archive creates `ConversationsDir` on first write; screenshot links are
  relative to the archive file.
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat: add prompts, model choice, sessions, and conversation archive`.

---

### Task 6: AskService (the reusable "send to Claude" service)

This is the service phase 2's watch mode will call on a timer (spec §7.10). No UI types.

**Files:**
- Create: `src/ClaudeGameHelper.Core/Conversations/AskService.cs`
- Test: `tests/ClaudeGameHelper.Tests/Conversations/AskServiceTests.cs`

**Interfaces:**
- Consumes: everything in Tasks 3–5; `IClaudeRunner` (Task 4).
- Produces:
  ```csharp
  public sealed record AskRequest(GameProfile Profile, string Question, IReadOnlyList<string> ScreenshotPaths, bool Deep, string? ActiveTrackingName);
  public sealed record AskOutcome(string AnswerText, bool IsError, bool Canceled, bool RolledOver, string Model);
  public interface IAskService
  {
      bool IsBusy { get; }
      Task<AskOutcome> AskAsync(AskRequest request, Action<ClaudeEvent> onEvent, CancellationToken ct);
      void NewConversation(GameProfile profile);
  }
  public sealed class AskService : IAskService
  { public AskService(AppSettings settings, Func<ClaudeLocateResult> locateClaude, IClaudeRunner runner, Func<DateTime> now); }
  ```

- [ ] **Step 1: Write the failing tests** (fake runner records `ClaudeLaunch`es and replays scripted events/results; profile in a temp folder).
  ```csharp
  [Fact] public async Task FirstAskUsesSessionIdThenResumes()
  { /* two asks; launch 1 args contain "--session-id" X (runner emits SessionStarted); launch 2 contain "--resume" X;
       WorkingDirectory == profile.FolderPath; StdinText == PromptBuilder.Build(...) */ }
  [Fact] public async Task OnlyExistingReadOnlyDirsArePassed() { /* one existing, one missing → one --add-dir */ }
  [Fact] public async Task SuccessReturnsResultTextAndArchivesTurn() { /* Completed("Scrip it.") → AnswerText "Scrip it.", archive contains it */ }
  [Fact] public async Task ClaudeMissingIsAPlainError()            // Review Focus 3
  { /* locate → Path null, Tried ["a","b"]; AnswerText == "Claude Code wasn't found. Tried: a, b. Set \"claudePath\" in settings.json to a working claude.exe."; runner never called */ }
  [Fact] public async Task UsageLimitResultIsShownAsError()        // Review Focus 3
  { /* Completed("You've hit your limit · resets 3pm", true, "success") → IsError, AnswerText is that text */ }
  [Fact] public async Task CrashWithoutResultReportsExitAndStderr()  // Review Focus 3
  { /* ExitCode 1, Stderr "boom\n", Completion null → "Claude Code stopped unexpectedly (exit 1): boom" */ }
  [Fact] public async Task SecondAskWhileBusyIsRefused()           // Review Focus 2
  { /* first ask blocked on a TaskCompletionSource; second returns IsError "Still answering — wait for it or press Stop."; runner called once */ }
  [Fact] public async Task CancelReturnsStopped() { /* Canceled → AnswerText "Stopped.", Canceled true, IsError false */ }
  [Fact] public async Task RolloverIsReported() { /* settings.RolloverContextTokens 10; runner emits ContextSize(10); next ask RolledOver true and uses --session-id with a new id */ }
  [Fact] public async Task SessionsAreSeparatePerProfile();
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~AskServiceTests"` → FAIL.
- [ ] **Step 3: Implement.** Locate once and cache a found path; retry the locate on a later ask if it failed.
  Flow: busy check → locate → `session.BeginTurn()` → `ModelSelector` → `PromptBuilder` → `ClaudeArgsBuilder` →
  run, forwarding every event to `onEvent` and feeding `SessionStarted`/`ContextSize` to the session → map the
  result (messages exactly as in the tests; stderr trimmed, first line, at most 300 chars) → archive the turn
  (all outcomes except busy and not-found).
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat: add AskService`.

---

### Task 7: Window capture

**Files:**
- Create: `src/ClaudeGameHelper/IGameCapture.cs` (interface + `CaptureFailedException`), `src/ClaudeGameHelper/Capture/{WindowCapture,WgcInterop}.cs`, `src/ClaudeGameHelper.Core/Layout/ScreenshotSizing.cs`
- Test: `tests/ClaudeGameHelper.Tests/Capture/WindowCaptureTests.cs`, `Layout/ScreenshotSizingTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public static class ScreenshotSizing { public static (int Width, int Height) Fit(int width, int height, int maxLongEdge); }  // never upscales
  public sealed class CaptureFailedException(string message) : Exception(message);
  public interface IGameCapture { Task<string> CaptureAsync(IntPtr hwnd, string screenshotsDir); }   // returns the saved .jpg path
  public sealed class WindowCapture : IGameCapture { public WindowCapture(Func<AppSettings> settings); }
  ```

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  [Theory] [InlineData(2560, 1440, 1568, 1568, 882)] [InlineData(1440, 2560, 1568, 882, 1568)] [InlineData(800, 600, 1568, 800, 600)]
  public void Fits(int w, int h, int max, int ew, int eh) => Assert.Equal((ew, eh), ScreenshotSizing.Fit(w, h, max));

  [Fact] public async Task CapturesAnOccludedRedWindow()
  {   // On an STA thread: show a 400x300 WPF window, Background red; then a topmost white window over it.
      // Capture the red window's HWND with maxLongEdge 200.
      // Assert: file is .jpg under screenshotsDir named yyyyMMdd-HHmmss-fff.jpg; decoded long edge == 200;
      // center pixel R > 200, G < 60, B < 60 (the window on top is not in the picture).
  }
  [Fact] public async Task MinimizedWindowThrowsCaptureFailed()       // Review Focus 1
  { /* minimized window → CaptureFailedException whose message says the window is minimized */ }
  [Fact] public async Task ClosedWindowThrowsCaptureFailed() { /* IntPtr of a closed window → CaptureFailedException */ }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~Capture|FullyQualifiedName~ScreenshotSizing"` → FAIL.
- [ ] **Step 3: Implement** with Windows.Graphics.Capture, which captures the window's own content even when
  the panel covers it and works with DirectX borderless windows:
  - Device: `D3D11CreateDevice` (BGRA support flag) → `IDXGIDevice` → `CreateDirect3D11DeviceFromDXGIDevice`
    → `MarshalInterface<IDirect3DDevice>.FromAbi`.
  - Item: activation factory of `Windows.Graphics.Capture.GraphicsCaptureItem` as `IGraphicsCaptureItemInterop`
    (IID `3628E81B-3CAC-4C60-B7F4-23CE0E0C3356`), `CreateForWindow(hwnd, IID_IGraphicsCaptureItem
    79C3F95B-31F7-4EC2-A464-632EF5D30760)`.
  - Before the first capture, `GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless)` once;
    session `IsBorderRequired = false` (ignore failure: a yellow border flash is acceptable) and
    `IsCursorCaptureEnabled = false`.
  - `Direct3D11CaptureFramePool.CreateFreeThreaded(device, B8G8R8A8UIntNormalized, 1, item.Size)`; take the first
    `FrameArrived` frame (timeout 2 s → `CaptureFailedException("The game window didn't produce a picture.")`);
    `SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface)`; encode with `BitmapEncoder` (JPEG,
    `ImageQuality` = quality / 100f, `BitmapTransform` scaled to `ScreenshotSizing.Fit`, `Fant` interpolation).
  - Check `IsIconic`/`IsWindow` first and throw `CaptureFailedException` with "The game window is minimized." /
    "The game window is closed."
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat: capture a window with Windows.Graphics.Capture`.

---

### Task 8: Hotkeys, foreground tracking, focus

**Files:**
- Create: `src/ClaudeGameHelper/Windows/{NativeMethods,GlobalHotkeys,ForegroundWatcher,WindowInfo,FocusHelper}.cs`
- Test: `tests/ClaudeGameHelper.Tests/Windows/{GlobalHotkeysTests,ForegroundWatcherTests}.cs`

**Interfaces:**
- Consumes: `HotkeyChord`, `HotkeyBindingSet` (Task 2).
- Produces:
  ```csharp
  public enum HotkeyAction { Screen = 1, Ask = 2, Toggle = 3 }
  public sealed class GlobalHotkeys : IDisposable
  {   // message-only HwndSource on the creating (UI) thread
      public IReadOnlyList<string> Apply(HotkeyBindingSet set);   // unregisters all, registers 3; returns chord texts that failed
      public event Action<HotkeyAction>? Pressed;
  }
  public sealed record WindowInfo(IntPtr Hwnd, int ProcessId, string ProcessName, string Title);
  public sealed class ForegroundWatcher : IDisposable
  { public event Action<WindowInfo>? Changed; public static WindowInfo? Current(); }
  public static class FocusHelper
  { public static void ReturnTo(IntPtr hwnd); public static void SetNoActivate(Window w, bool on); }
  ```

- [ ] **Step 1: Write the failing tests** (STA thread with a Dispatcher).
  ```csharp
  [Fact] public void RegistersAndReportsConflicts()
  {   // a = new GlobalHotkeys(); a.Apply(set of Ctrl+Alt+Shift+F22/F23/F24) → empty
      // b = new GlobalHotkeys(); b.Apply(same set) → ["Ctrl+Alt+Shift+F22","Ctrl+Alt+Shift+F23","Ctrl+Alt+Shift+F24"]
      // a.Dispose(); b.Apply(same) → empty
  }
  [Fact] public void CurrentForegroundDescribesOurOwnWindow()
  { /* show and activate a test window; Current()!.ProcessId == Environment.ProcessId, ProcessName == Process.GetCurrentProcess().ProcessName, Title == window title */ }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~Windows"` → FAIL.
- [ ] **Step 3: Implement.** `RegisterHotKey` with `MOD_NOREPEAT` (0x4000) | chord modifiers; `WM_HOTKEY` id =
  `(int)HotkeyAction`. `ForegroundWatcher`: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, …, WINEVENT_OUTOFCONTEXT)`;
  keep the delegate in a field so it isn't collected. Process name: `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)`
  + `QueryFullProcessImageName`, file name without extension. `SetNoActivate` toggles `WS_EX_NOACTIVATE` on the
  window's HWND. `ReturnTo` calls `SetForegroundWindow` when the HWND is still a window.
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat: add global hotkeys, foreground tracking, and focus helpers`.

---

### Task 9: The panel

**Files:**
- Create: `src/ClaudeGameHelper/Panel/{PanelWindow.xaml,PanelWindow.xaml.cs,PanelViewModel,ChatMessage,MarkdownView}.cs`, `src/ClaudeGameHelper/IPanelView.cs`, `src/ClaudeGameHelper.Core/Layout/PanelPlacement.cs`
- Package: `dotnet add src/ClaudeGameHelper package MdXaml`
- Test: `tests/ClaudeGameHelper.Tests/Panel/PanelViewModelTests.cs`, `Layout/PanelPlacementTests.cs`

**Interfaces:**
- Consumes: `ClaudeEvent` family (Task 3), `GameProfile`, `TrackingFiles` (Tasks 1, 5), `PanelBounds` (Task 2), `FocusHelper` (Task 8).
- Produces:
  ```csharp
  public sealed record RectD(double X, double Y, double Width, double Height);
  public static class PanelPlacement
  {   public const double DefaultWidth = 440;
      public static RectD Choose(PanelBounds? saved, IReadOnlyList<RectD> workAreas, RectD gameWorkArea); }

  public enum ChatRole { You, Claude, Note }
  public sealed class ChatMessage : INotifyPropertyChanged
  { public ChatRole Role { get; } public string Text { get; set; } public string? ModelLabel { get; set; }
    public bool IsError { get; set; } public IReadOnlyList<string> Screenshots { get; } }

  public sealed class PanelViewModel : INotifyPropertyChanged
  {
      public string ProfileName { get; }  public string? TrackingLabel { get; }
      public ObservableCollection<ChatMessage> Messages { get; }          // the active profile's history
      public ObservableCollection<string> TrackingNames { get; }  public string? ActiveTrackingName { get; set; }
      public ObservableCollection<string> PendingScreenshots { get; }
      public string QuestionText { get; set; }  public bool IncludeScreenshot { get; set; }  public bool Deep { get; set; }
      public bool IsAskMode { get; set; }  public bool IsBusy { get; set; }  public string? StatusText { get; set; }
      public void SwitchProfile(GameProfile p, IReadOnlyList<string> trackingNames); // keeps one history per profile id
      public void UpdateTrackingNames(IReadOnlyList<string> names);  // selects a newly added name when none is active
      public ChatMessage BeginExchange(string question, IReadOnlyList<string> screenshots); // adds You + empty Claude message, returns the Claude one
      public void Apply(ChatMessage answer, ClaudeEvent e);
      public void Finish(ChatMessage answer, AskOutcome outcome);
      public void AddPending(string path);   public void RemovePending(string path);   public void ClearAsk();
      public void AddNote(string text);
  }
  public interface IPanelView { bool IsShown { get; } bool IsActive { get; } void ShowPassive(); void ShowForAsk(); void HidePanel(); }
  ```

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  [Fact] public void DefaultsToRightEdgeOfGameMonitor()
  => Assert.Equal(new RectD(2120, 0, 440, 1400), PanelPlacement.Choose(null, [new(0,0,2560,1400)], new(0,0,2560,1400)));
  [Fact] public void SavedBoundsOnAMonitorAreKept() { /* saved on second monitor work area → returned as-is */ }
  [Fact] public void SavedBoundsOffEveryMonitorFallBackToDefault() { /* monitor unplugged */ }

  [Fact] public void TextDeltasStreamThenResultReplaces()
  { var a = vm.BeginExchange("q", []); vm.Apply(a, new TextDelta("Let me check… ")); vm.Apply(a, new TextDelta("Scrip"));
    Assert.Equal("Let me check… Scrip", a.Text);
    vm.Finish(a, new AskOutcome("Scrip it.", false, false, false, "sonnet")); Assert.Equal("Scrip it.", a.Text); Assert.Equal("sonnet", a.ModelLabel);
    Assert.False(vm.IsBusy); Assert.Null(vm.StatusText); }
  [Fact] public void ActivitySetsStatus() { vm.Apply(a, new Activity("Searching the web: x")); Assert.Equal("Searching the web: x", vm.StatusText); }
  [Fact] public void UsageLimitSetsStatus() { /* UsageLimit(resets) → StatusText starts "Claude usage limit reached — resets at " + local h:mm tt */ }
  [Fact] public void ErrorOutcomeMarksMessage() { /* IsError → a.IsError true, text is the error */ }
  [Fact] public void RolloverAddsNote() { /* RolledOver → Note message before the You message: "Started a fresh conversation (the last one got long). Your tracking files carry over." */ }
  [Fact] public void AddingAScreenshotRechecksInclude() { vm.IncludeScreenshot = false; vm.AddPending("a.jpg"); Assert.True(vm.IncludeScreenshot); }
  [Fact] public void HistoriesAreKeptPerProfile() { /* switch A → add message → switch B (empty) → switch A (1 message) */ }
  [Fact] public void ActiveTrackingDefaultsToOnlyFile() { /* SwitchProfile with ["vault-dweller"] → ActiveTrackingName "vault-dweller"; with 2 files and none chosen → null */ }
  [Fact] public void NewTrackingNameIsSelectedWhenNoneActive() { /* names [] → UpdateTrackingNames(["vault-dweller"]) → active "vault-dweller"; with "a" active, adding "b" keeps "a" */ }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~Panel"` → FAIL.
- [ ] **Step 3: Implement the view model and placement** to the tests.
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Build `PanelWindow`** (implements `IPanelView`), checked by eye in Task 11:
  - `WindowStyle="None"`, `Topmost="True"`, `ShowInTaskbar="False"`, `ShowActivated="False"`, resizable,
    opaque background `#1E1F22`, text `#E6E6E6`, accent `#D97757`; draggable by its header.
  - Header: `ProfileName`; when `TrackingLabel` is set, a `{TrackingLabel}:` combo box bound to
    `TrackingNames`/`ActiveTrackingName`.
  - Body: `ScrollViewer` of `Messages`; each Claude message rendered by `MarkdownView` (read-only `RichTextBox`
    holding `new MdXaml.Markdown().Transform(text)`, re-rendered at most every 150 ms while streaming), model label
    in small muted text, error messages tinted. Auto-scroll to the bottom while streaming.
  - Status row: thin indeterminate `ProgressBar` + `StatusText`, visible while `IsBusy`; a Stop button.
  - Ask area: thumbnail strip of `PendingScreenshots` (64 px high, × on each), multi-line `TextBox` (Enter sends,
    Shift+Enter new line, Esc hides), "Include screenshot" check box, "Deep" toggle.
  - `ShowPassive`: `SetNoActivate(true)`, `Show()` without activating. `ShowForAsk`: `SetNoActivate(false)`,
    `Show()`, `Activate()`, focus the text box. Clicking the text box while passive switches to Ask mode.
  - Placement from `PanelPlacement.Choose`; the window converts monitor pixels to DIPs with its DPI. Moves and
    resizes are saved to `settings.Panel` (debounced 500 ms).
  - Events raised to the controller: `SendRequested`, `StopRequested`, `HideRequested`.
- [ ] **Step 6: Commit** `feat: add the side panel`.

---

### Task 10: Controller, tray, and app startup

**Files:**
- Create: `src/ClaudeGameHelper/HelperController.cs`, `Tray/TrayIcon.cs`; Modify: `App.xaml`, `App.xaml.cs`; Create: `app.manifest` (PerMonitorV2 DPI awareness)
- Test: `tests/ClaudeGameHelper.Tests/HelperControllerTests.cs`

**Interfaces:**
- Consumes: `IAskService` (6), `IGameCapture` (7), `GlobalHotkeys`, `ForegroundWatcher`, `WindowInfo`, `FocusHelper` (8), `PanelViewModel`, `IPanelView` (9), `ProfileMatcher`, `HotkeyBindings`.
- Produces:
  ```csharp
  public sealed class HelperController
  {
      public HelperController(IReadOnlyList<GameProfile> profiles, GameProfile fallback, IAskService ask, IGameCapture capture,
          IPanelView panel, PanelViewModel vm, Action<IntPtr> returnFocus, int ownProcessId);
      public GameProfile ActiveProfile { get; }
      public event Action<GameProfile>? ActiveProfileChanged;
      public void OnForegroundChanged(WindowInfo w);
      public Task OnScreenHotkeyAsync();  public Task OnAskHotkeyAsync();  public void OnToggleHotkey();
      public Task SendAskBoxAsync();  public void Stop();  public void HidePanel();  public void NewConversation();
  }
  ```

- [ ] **Step 1: Write the failing tests** (fakes for `IAskService`, `IGameCapture`, `IPanelView`; no `SynchronizationContext`, so events apply inline).
  ```csharp
  [Fact] public async Task ScreenSendsScreenQuestionWithOneShotAndShowsPassively()
  { /* foreground game window → Screen → ask got (ScreenQuestion, ["shot1.jpg"], Deep false); panel.ShowPassive called; ShowForAsk not */ }
  [Fact] public async Task ScreenInAskModeAddsPendingAndDoesNotSend() { /* Ask, then Screen → PendingScreenshots 2, ask not called */ }
  [Fact] public async Task ScreenWhileBusyDoesNotSend()               // Review Focus 2
  { /* ask.IsBusy true → no capture, no ask; StatusText "Still answering — wait for it or press Stop." */ }
  [Fact] public async Task ScreenCaptureFailureIsShownAndNothingSent() // Review Focus 1
  { /* capture throws CaptureFailedException("The game window is minimized.") → StatusText "Couldn't capture the game window: The game window is minimized."; ask not called */ }
  [Fact] public async Task AskCapturesIntoPendingAndShowsForAsk() { /* IsAskMode, IncludeScreenshot true, 1 pending, ShowForAsk called */ }
  [Fact] public async Task AskStillOpensWhenCaptureFails()             // Review Focus 1
  { /* 0 pending, ShowForAsk called, StatusText set */ }
  [Fact] public async Task SendUsesBoxAndPendingThenHandsFocusBack()
  { /* QuestionText "Is this good?", 2 pending, Deep → ask got both shots, Deep true; box & pending cleared;
       IsAskMode false; ShowPassive called; returnFocus(gameHwnd) called */ }
  [Fact] public async Task SendWithIncludeUncheckedSendsNoShots();
  [Fact] public async Task EmptyQuestionWithShotsUsesScreenQuestion(); 
  [Fact] public async Task EmptyQuestionWithoutShotsSendsNothing();
  [Fact] public void ToggleShowsPassiveThenHidesAndReturnsFocusWhenActive();
  [Fact] public void ForegroundOwnProcessKeepsProfile() { /* game, then own pid → ActiveProfile still fallout-76, no event */ }
  [Fact] public void ForegroundOtherAppSwitchesToFallback() { /* event raised with fallback */ }
  [Fact] public void ActiveTrackingFileFlowsIntoRequest() { /* vm.ActiveTrackingName "vault-dweller" → request.ActiveTrackingName */ }
  [Fact] public async Task NewTrackingFileBecomesActiveAfterAnswer() { /* none active; after ask a tracking/x.md exists → TrackingNames has x, ActiveTrackingName "x" */ }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~HelperControllerTests"` → FAIL.
- [ ] **Step 3: Implement `HelperController`.** Keeps the last game `WindowInfo` (the last foreground window that
  wasn't ours) as the capture target. Captures go to `ActiveProfile.ScreenshotsDir`. Claude events are posted to the
  UI `SynchronizationContext` captured in the constructor (inline when null). After each answer, call
  `vm.UpdateTrackingNames(TrackingFiles.List(ActiveProfile))`.
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Wire up `App`** (manual check in Task 11):
  - Single instance: named mutex `ClaudeGameHelper.SingleInstance`; a second launch exits silently.
  - Startup: `Log.Init(AppPaths.LogsDir)`; load settings (an `Error` → tray balloon with it); find profiles
    (`AppPaths.FindProfilesRoot`; none → balloon "Profiles folder not found. Set \"profilesFolder\" in settings.json."
    and quit); `ProfileLoader.LoadAll` (each error → balloon); build `AskService` with
    `() => ClaudeLocator.Find(settings.ClaudePath, PATH, %USERPROFILE%\.vscode\extensions, ClaudeLocator.ProbeVersion)`;
    create panel, controller, `ForegroundWatcher`, `GlobalHotkeys`.
  - Hotkeys: apply `HotkeyBindings.Resolve(settings, controller.ActiveProfile)` at start and on
    `ActiveProfileChanged` when the set differs. Each failed chord → balloon
    "{chord} is already used by another app. Change it in settings.json and restart the helper."
  - Tray (`NotifyIcon`, icon drawn at runtime: orange `#D97757` circle with a dark "C"; no binary assets). Menu:
    Show panel · New conversation · Open {TrackingLabel} file (disabled when none active) · Settings · Quit.
    Double-click shows the panel. "Settings" opens `settings.json` with the shell (Notepad if that fails) and shows
    the balloon "Changes take effect the next time the helper starts."
  - Unhandled exceptions (dispatcher, `TaskScheduler.UnobservedTaskException`, AppDomain): log, balloon
    "Something went wrong. Details are in the log.", keep running (dispatcher: `Handled = true`).
- [ ] **Step 6: Commit** `feat: wire hotkeys, panel, tray, and Claude together`.

---

### Task 11: Live check with Fallout 76, full test run, handoff

- [ ] **Step 1: Full suite.** `dotnet test` from the repo root → all pass. (The one full run for this plan; reviewers reuse it.)
- [ ] **Step 2: Live check** (`dotnet run --project src/ClaudeGameHelper`, Fallout 76 running borderless). Confirm and note results:
  1. The game's process name matches `profile.json`; the panel header says "Fallout 76". Fix `processNames` if not.
  2. Ctrl+Shift+F1/F2/F3 aren't bound in Fallout 76's current keybindings (Settings → Controls). If one is, add a
     `hotkeys` override to `profiles/fallout-76/profile.json`.
  3. Screen hotkey: the game keeps focus and keeps rendering; the status line shows activity; an answer streams in
     within about 30 seconds; the screenshot in `profiles/fallout-76/screenshots/` is the game, not the panel.
  4. Ask hotkey → type a question → Enter: focus returns to the game.
  5. Ask hotkey, then Screen ×4 on SPECIAL/perks/legendary perks/mutations/gear → Enter with Deep: answer uses
     `opus`; a character file appears in `tracking/` and becomes active; the answer ends with an "Updated …" line.
  6. "Fix my mouse sensitivity"-style question: Claude reads `Project76Prefs.ini` and does not change it.
  7. The archive under `conversations/` reads cleanly.
- [ ] **Step 3: Update `docs/HANDOFF.md`**: status "v1 built", how to run it, live-check results, next step (phase 2 watch mode, spec §7.10).
- [ ] **Step 4: Commit** `docs: record v1 live check and update handoff`.
