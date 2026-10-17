# Watch Mode (Phase 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the helper watch the game on a timer and speak up when the player is about to miss what
they asked about or is heading the wrong way, plus occasional general tips, all switchable per game.

**Architecture:** Pure decision logic in Core (`WatchState`: goal, pacing, when to check; `WatchReplyParser`;
`FrameDiff`), a `WatchService` that runs one stateless, read-only Claude check per tick, and
`HelperController.OnWatchTickAsync` driven by a 5-second `DispatcherTimer` in `App`. Controls live in a tray
submenu; results show in the panel as "👀 Watch" messages.

**Tech Stack:** existing C# / .NET 10 / WPF app, Claude Code CLI 2.1.295 flags already verified (plus
`--no-session-persistence`, listed in the CLI's own help).

**Spec:** [docs/superpowers/specs/2026-10-16-watch-mode-design.md](../specs/2026-10-16-watch-mode-design.md)

## Global Constraints

- All earlier Global Constraints hold (anti-ban, isolation flags, stdin UTF-8 without BOM, opaque
  non-activating panel, game-neutral `src\`, American English, commit on `master`, never branch).
- Watch checks: fast model (`settings.FastModel`), effort **`low`**, **no web search, no Edit/Write**
  (`--tools Read`), one-turn and **not saved as a session** (`--no-session-persistence`). This is how the
  spec's "their own Claude conversation" is met: each check is its own one-turn conversation carrying the
  context it needs (recent questions, tips given) in the prompt, so screenshots never pile up in a growing
  context.
- Off by default. Switches **per game** (profile id): Follow my goal, General tips. Global: check every
  **20 / 30 / 60 / 120 s (default 30)**, Chime (default on).
- Goal checks every `CheckEverySeconds`; with no active goal, at most every **120 s**.
- Goal ends: Claude reports reached / no goal, ✕ Stop, or **30 min** after it was set (a new typed question
  resets it). The Screen hotkey's built-in question never sets a goal.
- General tips: at most one per **3 min**, never the same text twice per session. Same goal alert not
  repeated within **60 s**.
- Goal alerts: chime + panel shown without focus. Tips: chime + panel line + tray dot; never open the panel.
- Fallback profile and `allowWatchMode: false` profiles never watch.
- A check never delays the player's own question: no check while `IAskService.IsBusy`, and asking cancels a
  running check.
- Tests: filtered runs per task; the full suite once, in Task 7.

## Review Focus

1. **Player alt-tabs to a browser or Discord while watching** → no captures, no checks until the game is in
   front again. (Task 5)
2. **Player asks a question while a check is running** → the check is canceled and its result never
   appears; the question runs normally. (Task 5)
3. **Claude's reply isn't clean JSON** (code fence, extra words, unknown verdict, garbage) → treated as
   "nothing"; no crash, no stray message. (Task 1)
4. **Usage limit hit while watching** → one "paused" note, no further checks until the reset time. (Tasks 2, 3)
5. **Game minimized or capture fails on a tick** → that tick is skipped silently; no status-line spam every
   5 seconds. (Task 5)

---

## File Structure

```
src\ClaudeGameHelper.Core\Watch\
  WatchSettings.cs      WatchSettings, WatchSwitches (in AppSettings.Watch)
  WatchReply.cs         WatchVerdict, WatchReply, WatchReplyParser
  FrameDiff.cs          thumbnail difference
  WatchState.cs         goal, recent exchanges, pacing, Decide, Apply
  WatchPrompt.cs        check prompt text
  WatchService.cs       IWatchService, WatchService (runs a check)
src\ClaudeGameHelper\
  IGameCapture.cs        + CaptureForWatchAsync, WatchFrame
  Capture\WindowCapture.cs
  HelperController.cs    + OnWatchTickAsync, StopWatching, WatchDependencies
  Panel\*                + Watch role, "Watching:" header
  Tray\TrayIcon.cs       + Watch submenu, SetAttention;  Tray\WatchMenu.cs (IWatchMenu impl)
  App.xaml.cs            timer + wiring
```

---

### Task 1: Settings, reply parser, frame difference

**Files:**
- Create: `src/ClaudeGameHelper.Core/Watch/{WatchSettings,WatchReply,FrameDiff}.cs`; Modify: `Settings/AppSettings.cs`
- Test: `tests/ClaudeGameHelper.Tests/Watch/{WatchReplyParserTests,FrameDiffTests}.cs`, `Settings/SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record WatchSwitches(bool FollowGoal, bool GeneralTips);
  public sealed record WatchSettings
  {
      public int CheckEverySeconds { get; init; } = 30;
      public bool Chime { get; init; } = true;
      public IReadOnlyDictionary<string, WatchSwitches> Games { get; init; } = new Dictionary<string, WatchSwitches>();
      public WatchSwitches For(string profileId);          // missing → (false, false)
      public static readonly int[] Frequencies = [20, 30, 60, 120];
  }
  // AppSettings: + public WatchSettings Watch { get; init; } = new();
  public enum WatchVerdict { Nothing, GoalAlert, Tip, GoalReached, NoGoal }
  public sealed record WatchReply(WatchVerdict Verdict, string Message);
  public static class WatchReplyParser { public static WatchReply Parse(string resultText); }
  public static class FrameDiff
  {
      public const int Width = 64, Height = 36;
      public const double SkipBelow = 0.03;
      public static double Difference(byte[]? previous, byte[] current);   // 0..1, mean |a-b|/255; null or size mismatch → 1
  }
  ```

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  [Theory]
  [InlineData("""{"verdict":"goal_alert","message":"Wrong way. Turn around."}""", WatchVerdict.GoalAlert, "Wrong way. Turn around.")]
  [InlineData("""{"verdict":"tip","message":" Legendary enemy to the left. "}""", WatchVerdict.Tip, "Legendary enemy to the left.")]
  [InlineData("""{"verdict":"goal_reached","message":"You're there."}""", WatchVerdict.GoalReached, "You're there.")]
  [InlineData("""{"verdict":"no_goal","message":""}""", WatchVerdict.NoGoal, "")]
  [InlineData("""{"verdict":"nothing","message":""}""", WatchVerdict.Nothing, "")]
  public void ParsesVerdicts(string text, WatchVerdict v, string m) => Assert.Equal(new WatchReply(v, m), WatchReplyParser.Parse(text));
  [Fact] public void FindsJsonInsideACodeFenceOrProse()                     // Review Focus 3
  => Assert.Equal(WatchVerdict.Tip, WatchReplyParser.Parse("Here you go:\n```json\n{\"verdict\":\"tip\",\"message\":\"x\"}\n```").Verdict);
  [Theory] [InlineData("")] [InlineData("nothing to report")] [InlineData("{bad json")] [InlineData("""{"verdict":"party","message":"x"}""")]
  public void AnythingElseIsNothing(string text) => Assert.Equal(new WatchReply(WatchVerdict.Nothing, ""), WatchReplyParser.Parse(text));
  [Fact] public void TipWithoutMessageIsNothing() => Assert.Equal(WatchVerdict.Nothing, WatchReplyParser.Parse("""{"verdict":"tip","message":" "}""").Verdict);

  [Fact] public void IdenticalFramesDifferByZero();   [Fact] public void BlackVersusWhiteIsOne();
  [Fact] public void NoPreviousFrameCountsAsChanged() => Assert.Equal(1, FrameDiff.Difference(null, new byte[64 * 36]));
  [Fact] public void SizeMismatchCountsAsChanged();
  [Fact] public void SmallChangeIsBelowTheThreshold() { /* 50 of 2304 pixels change by 100 → < SkipBelow */ }

  [Fact] public void WatchSettingsDefaultsAndRoundTrip()
  { /* new AppSettings().Watch: 30 s, chime on, For("x") == (false,false); Save with Games["fallout-76"]=(true,false) → Load equals */ }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~Tests.Watch|FullyQualifiedName~SettingsStore"` → FAIL.
- [ ] **Step 3: Implement.** Parser: take the substring from the first `{` to the last `}`, parse with
  `System.Text.Json`; verdict strings `nothing`, `goal_alert`, `tip`, `goal_reached`, `no_goal`; trim the
  message; a `goal_alert`/`tip`/`goal_reached` with an empty message becomes `Nothing`.
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat(watch): add settings, reply parser, and frame difference`.

---

### Task 2: WatchState — goal, pacing, when to check

**Files:**
- Create: `src/ClaudeGameHelper.Core/Watch/WatchState.cs`
- Test: `tests/ClaudeGameHelper.Tests/Watch/WatchStateTests.cs`

**Interfaces:**
- Consumes: `WatchReply`, `WatchVerdict`, `FrameDiff` (Task 1).
- Produces:
  ```csharp
  public sealed record WatchGoal(string Label, DateTime SetAt);            // Label: question, ≤ 60 chars + "…"
  public sealed record WatchExchange(string Question, string Answer);
  public sealed record WatchInputs(DateTime Now, WatchSwitches Switches, bool Allowed, bool GameInFront,
      bool Typing, bool AskBusy, int CheckEverySeconds);
  public enum WatchDecision { Check, Off, NotAllowed, NotInFront, Typing, AskBusy, Paused, NothingToWatch, NotDue }
  public enum WatchMessageKind { GoalAlert, Tip, GoalReached, Paused }
  public sealed record WatchDisplay(WatchMessageKind Kind, string Text);
  public sealed class WatchState
  {
      public WatchGoal? Goal { get; }
      public IReadOnlyList<WatchExchange> RecentExchanges { get; }         // last 3, oldest first
      public IReadOnlyList<string> TipsGiven { get; }
      public IReadOnlyList<string> RecentMessages { get; }                 // last 3 shown watch texts
      public DateTime? PausedUntil { get; }
      public void RecordExchange(string question, string answer, bool isScreenQuestion, bool followGoal, DateTime now);
      public void StopGoal();
      public WatchDecision Decide(WatchInputs inputs);                     // also expires a 30-min-old goal
      public bool FrameChanged(byte[] thumbnail);                          // vs last kept; keeps the new one when changed
      public void MarkChecked(DateTime now);
      public WatchDisplay? Apply(WatchReply reply, DateTime now);          // pacing filters; updates goal/tips/messages
      public WatchDisplay Pause(DateTime until, string text);
  }
  ```

- [ ] **Step 1: Write the failing tests** (now = 2026-10-16 20:00).
  ```csharp
  [Fact] public void TypedQuestionSetsGoalScreenQuestionDoesNot()
  { s.RecordExchange(PromptBuilder.ScreenQuestion, "a", isScreenQuestion: true, followGoal: true, now); Assert.Null(s.Goal);
    s.RecordExchange("Where's the Whitespring?", "Northeast.", false, true, now); Assert.Equal("Where's the Whitespring?", s.Goal!.Label); }
  [Fact] public void FollowGoalOffSetsNoGoal();
  [Fact] public void KeepsLastThreeExchanges();   [Fact] public void LongQuestionLabelIsCut() => /* 61+ chars → 60 + "…" */;
  [Fact] public void NewQuestionResetsGoalClock();
  [Theory]   // with a goal set at `now`, CheckEverySeconds 30, last check at now
  [InlineData(29, WatchDecision.NotDue)] [InlineData(30, WatchDecision.Check)]
  public void GoalChecksFollowTheFrequency(int secondsLater, WatchDecision d);
  [Fact] public void TipsOnlyChecksAtMostEveryTwoMinutes() { /* no goal, tips on, freq 30: +60 s NotDue, +120 s Check */ }
  [Fact] public void FirstCheckIsDueImmediately();
  [Fact] public void GoalExpiresAfterThirtyMinutes() { /* goal at now; Decide at +30 min → Goal null; tips off → NothingToWatch */ }
  [Fact] public void ReasonsNotToCheck()
  { /* switches (false,false) → Off; Allowed false → NotAllowed; GameInFront false → NotInFront; Typing → Typing; AskBusy → AskBusy;
       goal mode on but no goal and tips off → NothingToWatch */ }
  [Fact] public void PausedUntilResetTime()                                // Review Focus 4
  { s.Pause(now.AddMinutes(10), "x"); Assert.Equal(WatchDecision.Paused, Decide(at now+9m)); Assert.Equal(WatchDecision.Check, Decide(at now+10m)); }
  [Fact] public void FrameChangedComparesWithLastKept() { /* first true; same false; very different true */ }

  [Fact] public void GoalReachedClearsGoalAndShowsNote() => /* Apply(GoalReached,"You're there.") → Display(GoalReached, "You're there."), Goal null */;
  [Fact] public void NoGoalClearsGoalSilently() => /* → null, Goal null */;
  [Fact] public void NothingShowsNothing();
  [Fact] public void TipsArePacedThreeMinutesApart() { /* tip at now shown; tip at +2m → null; tip at +3m shown */ }
  [Fact] public void SameTipNeverRepeats() { /* same text at +10m → null (case-insensitive) */ }
  [Fact] public void SameAlertNotWithinAMinute() { /* alert at now shown; same at +30s null; same at +61s shown; different text at +5s shown */ }
  [Fact] public void ShownMessagesAreRemembered() { /* RecentMessages holds last 3 shown texts; TipsGiven holds every tip */ }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~WatchStateTests"` → FAIL.
- [ ] **Step 3: Implement** to the tests. `Decide` checks in this order: paused → switches off → not allowed →
  goal expiry → nothing to watch → not in front → typing → ask busy → due. Interval = goal active ?
  `CheckEverySeconds` : `Math.Max(CheckEverySeconds, 120)`; never-checked is due.
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat(watch): add watch state (goal, pacing, decisions)`.

---

### Task 3: WatchService — one Claude check

**Files:**
- Create: `src/ClaudeGameHelper.Core/Watch/{WatchPrompt,WatchService}.cs`; Modify: `Claude/ClaudeArgsBuilder.cs`
- Test: `tests/ClaudeGameHelper.Tests/Watch/{WatchPromptTests,WatchServiceTests}.cs`, `Claude/ClaudeArgsBuilderTests.cs`

**Interfaces:**
- Consumes: Tasks 1–2; `IClaudeRunner`, `ClaudeLaunch`, `ClaudeLocateResult`, `StreamJsonParser` events.
- Produces:
  ```csharp
  public static IReadOnlyList<string> ClaudeArgsBuilder.BuildWatch(string model, string instructionsPath);
  public sealed record WatchCheck(string GameName, string ScreenshotPath, WatchSwitches Switches, bool GoalActive,
      IReadOnlyList<WatchExchange> RecentExchanges, IReadOnlyList<string> TipsGiven, string? TrackingLabel, string? ActiveTrackingName);
  public static class WatchPrompt { public static string Build(WatchCheck check); }
  public interface IWatchService
  {
      WatchState For(GameProfile profile);
      Task<WatchDisplay?> CheckAsync(GameProfile profile, string screenshotPath, WatchSwitches switches,
          string? activeTrackingName, CancellationToken ct);
  }
  public sealed class WatchService(AppSettings settings, Func<ClaudeLocateResult> locate, IClaudeRunner runner, Func<DateTime> now) : IWatchService;
  ```

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  [Fact] public void WatchArgsAreExact() => Assert.Equal(new[] {
      "-p", "--output-format", "stream-json", "--verbose", "--model", "sonnet", "--effort", "low",
      "--no-session-persistence", "--safe-mode", "--restricted", "--disable-slash-commands",
      "--tools", "Read", "--permission-mode", "dontAsk", "--allowedTools", "Read",
      "--append-system-prompt-file", @"C:\p\CLAUDE.md" }, ClaudeArgsBuilder.BuildWatch("sonnet", @"C:\p\CLAUDE.md"));

  [Fact] public void PromptHasGoalBlockOnlyWithAGoal() { /* GoalActive → contains "Their recent questions" and each Q/A; else not */ }
  [Fact] public void PromptHasTipRulesOnlyWhenTipsAreOn() { /* contains "Only give a tip if it clears this bar"; lists TipsGiven; "(none)" when empty */ }
  [Fact] public void PromptEndsWithTheReplyFormat() => Assert.Contains("""{"verdict":""", WatchPrompt.Build(check));
  [Fact] public void PromptNamesTheScreenshotAndGame();

  [Fact] public async Task CheckRunsWatchArgsWithPromptOnStdinAndAppliesReply()
  { /* fake runner returns Completed("{\"verdict\":\"goal_alert\",\"message\":\"Wrong way.\"}") →
       Display(GoalAlert,"Wrong way."); launch.Args == BuildWatch(settings.FastModel, profile.InstructionsPath);
       StdinText == WatchPrompt.Build(...); WorkingDirectory == FolderPath; For(profile).Decide(...) right after → NotDue */ }
  [Fact] public async Task UsageLimitPausesWithOneNote()                  // Review Focus 4
  { /* runner emits UsageLimit(resets) then Completed(error) → Display(Paused, "Watch mode paused: Claude usage limit reached. It resumes at h:mm tt.");
       For(profile).PausedUntil == resets; unknown reset time → now + 30 min */ }
  [Fact] public async Task FailuresShowNothing() { /* runner throws Win32Exception, or exit 1 without Completed, or canceled → null */ }
  [Fact] public async Task ClaudeMissingShowsNothing() { /* locate Path null → null, runner never called */ }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~Watch|FullyQualifiedName~ClaudeArgsBuilder"` → FAIL.
- [ ] **Step 3: Implement.** `WatchPrompt.Build`, exact text (blocks joined with a blank line, braces filled in):
  ```text
  WATCH CHECK. You are glancing at the player's screen while they play {GameName}. Use only the Read tool, to view the screenshot.

  Screenshot: {ScreenshotPath}

  [goal block, only when GoalActive]
  Their recent questions (oldest first) and your answers:
  Q: {question}
  A: {answer}
  ...
  Work out what they're trying to find or reach right now. If none of these is something you can watch for on screen (a place, an item, a quest step), reply no_goal.

  [tips block, only when Switches.GeneralTips]
  General tips are on. Only give a tip if it clears this bar: easy to miss and worth having (rare loot or a plan, a legendary enemy, an event starting nearby, a wishlist item on a vendor); a mistake about to happen (underleveled for a fight, about to be overencumbered, about to scrap or sell something valuable); or tied to their {tracking label lowercase} file's goals or wishlist ({tracking path}; read it only if a tip depends on it). Never lore, trivia, generic advice, or a repeat. When unsure, say nothing.
  Tips already given: {"; "-joined, or "(none)"}

  Reply with exactly one line of JSON and nothing else:
  {"verdict":"nothing|goal_alert|tip|goal_reached|no_goal","message":"one or two short sentences, verdict first"}
  goal_alert: they're heading the wrong way or about to miss what they're looking for. goal_reached: they've arrived or found it. Leave message empty for nothing and no_goal.
  ```
  (The tracking clause is omitted when the profile has no tracking label or no active file.) `CheckAsync`:
  locate (cached like `AskService`, off the calling thread); `Directory.CreateDirectory(FolderPath)`; run;
  on `UsageLimit` or an error `Completed` whose text contains "limit" → `Pause(...)`; otherwise
  `Apply(WatchReplyParser.Parse(result), now())`; `MarkChecked(now())` in all cases; catch and log exceptions →
  null.
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat(watch): run one read-only Claude check per tick`.

---

### Task 4: Capture for watching

**Files:**
- Modify: `src/ClaudeGameHelper/IGameCapture.cs`, `Capture/WindowCapture.cs`
- Test: `tests/ClaudeGameHelper.Tests/Capture/WindowCaptureTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record WatchFrame(string Path, byte[] Thumbnail);   // Thumbnail: FrameDiff.Width x Height, Gray8
  // IGameCapture: + Task<WatchFrame> CaptureForWatchAsync(IntPtr hwnd, string jpgPath);   // overwrites jpgPath
  ```

- [ ] **Step 1: Write the failing tests** (reuse the occluded red window setup).
  ```csharp
  [Fact] public async Task WatchCaptureOverwritesOneFileAndReturnsAGrayThumbnail()
  { var path = Path.Combine(_dir, "watch.jpg");
    var a = await Capture().CaptureForWatchAsync(red, path); var b = await Capture().CaptureForWatchAsync(red, path);
    Assert.Equal(path, b.Path); Assert.Single(Directory.GetFiles(_dir));
    Assert.Equal(FrameDiff.Width * FrameDiff.Height, b.Thumbnail.Length);
    Assert.True(FrameDiff.Difference(a.Thumbnail, b.Thumbnail) < FrameDiff.SkipBelow); }
  [Fact] public async Task WatchCaptureOfMinimizedWindowThrowsCaptureFailed();
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~WindowCaptureTests"` (display must be awake) → FAIL.
- [ ] **Step 3: Implement.** Existing test fakes of `IGameCapture` (`FakeCapture`, `GatedCapture` in
  `HelperControllerTests`) get a `CaptureForWatchAsync` that returns `new WatchFrame(jpgPath, thumbnail)` with a
  settable thumbnail. Share the grab-and-encode path with `CaptureAsync`; write to `jpgPath`; build the
  thumbnail by decoding the saved JPEG with `BitmapImage` (`DecodePixelWidth = 64`, `DecodePixelHeight = 36`,
  `CacheOption = OnLoad`), `FormatConvertedBitmap` to `Gray8`, `CopyPixels`.
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat(watch): capture a reusable watch frame with a thumbnail`.

---

### Task 5: Controller, panel, and asked-question context

**Files:**
- Modify: `HelperController.cs`, `Panel/{ChatMessage,PanelViewModel}.cs`, `Panel/PanelWindow.xaml(.cs)`,
  `Core/Conversations/{AskService,PromptBuilder,ConversationArchive}.cs`
- Test: `HelperControllerTests.cs`, `PanelViewModelTests.cs`, `PromptBuilderTests.cs`, `AskServiceTests.cs`

**Interfaces:**
- Consumes: `IWatchService`, `WatchState`, `WatchDisplay` (Tasks 2–3), `CaptureForWatchAsync` (Task 4).
- Produces:
  ```csharp
  public sealed record WatchDependencies(IWatchService Service, Func<WatchSettings> Settings, Action Chime, Action<bool> SetAttention);
  // HelperController ctor: + WatchDependencies? watch = null (last, optional)
  public Task HelperController.OnWatchTickAsync();
  public void HelperController.StopWatching();
  // ChatRole: + Watch;  ChatMessage: + bool IsAlert
  public void PanelViewModel.AddWatchMessage(WatchMessageKind kind, string text);
  public string? PanelViewModel.WatchGoalText { get; set; }
  public event Action? PanelWindow.StopWatchingRequested;
  // AskRequest: + IReadOnlyList<string>? RecentWatch = null (last, optional)
  // PromptBuilder.Build: + IReadOnlyList<string>? recentWatch = null (last, optional)
  // IAskService: + void ArchiveNote(GameProfile profile, string note);
  ```

- [ ] **Step 1: Write the failing tests** (fake `IWatchService` returning a scripted `WatchDisplay?`; real
  `WatchState` from `For`; fake capture gains `CaptureForWatchAsync` returning thumbnails you control).
  ```csharp
  [Fact] public async Task TickChecksAndShowsAGoalAlertWithChimeAndPanel()
  { /* switches (true,false) for the game; goal set by a typed question; tick → CaptureForWatchAsync(GameHwnd, <ScreenshotsDir>\watch.jpg);
       CheckAsync called; display GoalAlert "Wrong way." → vm last message Role Watch, IsAlert, Text "Wrong way."; panel ShowPassive; chime once */ }
  [Fact] public async Task TipChimesAndSetsAttentionButNeverOpensThePanel() { /* panel hidden → no ShowPassive; SetAttention(true) */ }
  [Fact] public async Task UnchangedFrameSkipsClaude() { /* same thumbnail twice → CheckAsync once */ }
  [Fact] public async Task NoChecksWhileAnotherAppIsInFront()           // Review Focus 1
  { /* foreground chrome → tick → no capture, no check */ }
  [Fact] public async Task AskingCancelsARunningCheckAndDropsItsResult()  // Review Focus 2
  { /* CheckAsync blocks on a gate honoring ct; start tick; SendAskBoxAsync → token canceled; release → no watch message added */ }
  [Fact] public async Task CaptureFailureOnATickIsSilent()                // Review Focus 5
  { /* CaptureForWatchAsync throws CaptureFailedException → StatusText unchanged (null), no check */ }
  [Fact] public async Task AnsweredTypedQuestionStartsTheGoalAndShowsIt() { /* after SendAskBoxAsync with follow-goal on → vm.WatchGoalText == question */ }
  [Fact] public async Task ScreenHotkeyAnswerDoesNotStartAGoal();
  [Fact] public void StopWatchingClearsGoalAndHeader();
  [Fact] public async Task GoalReachedClearsHeader();
  [Fact] public async Task RecentWatchMessagesRideAlongWithTheNextQuestion() { /* after a tip, next AskRequest.RecentWatch == ["tip text"] */ }
  [Fact] public async Task ShownWatchMessagesAreArchived() { /* ArchiveNote(profile, "👀 Watch: Wrong way.") */ }
  [Fact] public async Task FallbackNeverWatches();
  [Fact] public async Task TicksDontOverlap() { /* second tick while first is running → no second check */ }

  [Fact] public void WatchMessagesAreMarked() { /* AddWatchMessage(GoalAlert,"x") → Role Watch, IsAlert; Tip → !IsAlert; Paused/GoalReached → Role Watch */ }
  [Fact] public void PromptCarriesRecentWatchMessages() =>
      Assert.EndsWith("\n\nWatch mode recently told me:\n- Legendary enemy to the left.", PromptBuilder.Build("q", [], null, null, null, ["Legendary enemy to the left."]));
  [Fact] public async Task ArchiveNoteAppendsToTheCurrentConversation() { /* "## 👀 Watch · HH:mm\n<note>\n\n" in the session's archive */ }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~HelperControllerTests|FullyQualifiedName~PanelViewModelTests|FullyQualifiedName~PromptBuilder|FullyQualifiedName~AskService"` → FAIL.
- [ ] **Step 3: Implement.** Controller tracks `_gameInFront` (true when the last foreground window wasn't ours
  and matched any profile or the fallback, false when ours or none). `OnWatchTickAsync`: return if no watch
  deps or a tick is already running; build `WatchInputs` (switches from `settings.For(ActiveProfile.Id)`,
  `Allowed = AllowWatchMode && !IsFallback`, `Typing = vm.IsAskMode`, `AskBusy = ask.IsBusy || _sending`);
  `Decide` (and refresh `vm.WatchGoalText` from `state.Goal`); on `Check`: capture (catch
  `CaptureFailedException` → `MarkChecked` and return), `FrameChanged` false → `MarkChecked` and return,
  else `CheckAsync` with a CTS stored in `_watchCts`; drop the result if canceled. Show: GoalAlert →
  `AddWatchMessage` + `ShowPassive` (if hidden) + chime; Tip → `AddWatchMessage` + chime + `SetAttention(true)`
  if panel hidden; GoalReached/Paused → `AddWatchMessage`; every shown message → `ArchiveNote`. Log each tick
  that gets past `Decide`: `Watch check sent (n sent, m skipped this session)` or `Watch check skipped: screen
  unchanged (…)`, so the cost is visible (spec §6.5). `SendAsync`
  cancels `_watchCts` first, passes `RecentWatch = state.RecentMessages`, and after the answer calls
  `RecordExchange(question, answer, question == PromptBuilder.ScreenQuestion, switches.FollowGoal, now)`.
  Panel: Watch role renders like Claude (Markdown) with header "👀 Watch", alerts tinted with the accent
  color; header row shows `👀 Watching: {WatchGoalText}` + a "✕" button (`StopWatchingRequested`) when set.
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat(watch): run checks from the controller and show them in the panel`.

---

### Task 6: Tray controls, attention dot, timer, README

**Files:**
- Create: `src/ClaudeGameHelper/Tray/WatchMenu.cs`; Modify: `Tray/TrayIcon.cs`, `App.xaml.cs`, `README.md`
- Test: `tests/ClaudeGameHelper.Tests/Tray/WatchMenuTests.cs`, `TrayIconTests.cs`

**Interfaces:**
- Consumes: `WatchSettings` (Task 1), `HelperController.OnWatchTickAsync/StopWatching` (Task 5).
- Produces:
  ```csharp
  public interface IWatchMenu
  {
      string? GameName { get; }  bool Allowed { get; }
      bool FollowGoal { get; set; }  bool GeneralTips { get; set; }
      int CheckEverySeconds { get; set; }  bool Chime { get; set; }
  }
  public sealed class WatchMenu(Func<AppSettings> get, Action<AppSettings> save, Func<GameProfile> activeProfile) : IWatchMenu;
  // TrayIcon ctor: + IWatchMenu watchMenu (before quit);  public void SetAttention(bool on);
  public static Icon TrayIcon.WithDot(Icon icon);
  ```

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  [Fact] public void SwitchesArePerGame()
  { /* active fallout-76: FollowGoal = true → saved Games["fallout-76"] == (true,false); switch active to "valheim" → FollowGoal false */ }
  [Fact] public void FrequencyAndChimeAreGlobalAndSaved();
  [Fact] public void FrequencyOutsideTheChoicesSnapsToNearest() { /* set 45 → 30; 500 → 120 */ }
  [Fact] public void FallbackAndDisallowedGamesAreNotAllowed() { /* fallback → Allowed false, GameName null; allowWatchMode false → Allowed false */ }
  [Fact] public void DotIconKeepsSizeAndChangesPixels() { /* WithDot(LoadAppIcon(32)) → 32x32; top-right pixel differs from original */ }
  ```
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~WatchMenuTests|FullyQualifiedName~TrayIconTests"` → FAIL.
- [ ] **Step 3: Implement.** Tray submenu "Watch mode" (rebuilt on menu open): a disabled header
  `For {GameName}` (or "No game in front" with the switches disabled when `!Allowed`), **Follow my goal**,
  **General tips**, a separator, **Check every** 20 s / 30 s / 1 min / 2 min (radio checks), **Chime**.
  `WithDot` draws a cream (#FFF4E8) dot with a dark (#1E1F22) ring in the top-right quarter. App: build
  `WatchService` (same `LocateClaude`, `ClaudeRunner`), pass `WatchDependencies(service, () => _settings.Watch,
  () => { if (_settings.Watch.Chime) SystemSounds.Asterisk.Play(); }, _tray.SetAttention)`; a
  `DispatcherTimer` every 5 s → `Run(_controller.OnWatchTickAsync)`; `PanelWindow.IsVisibleChanged` → visible
  clears the dot; `StopWatchingRequested` → `StopWatching`. README: a "Watch mode" section (what it does,
  per-game switches in the tray, frequency, that each check uses your Claude plan, and not to use it in
  competitive multiplayer games).
- [ ] **Step 4: Run the filter** → PASS.
- [ ] **Step 5: Commit** `feat(watch): tray controls, attention dot, timer, and README`.

---

### Task 7: Full suite, live check, release

- [ ] **Step 1: Full suite.** `dotnet test` with the display awake → all pass.
- [ ] **Step 2: Smoke run from source** (quit the installed copy first): tray shows the Watch mode submenu;
  turn on Follow my goal for the game in front; ask a question; the header shows "👀 Watching: …"; the log shows
  checks sent/skipped. Note anything off for Jason's live check with a game.
- [ ] **Step 3: Update `docs/HANDOFF.md`** (watch mode built; how to try it; release 0.3.0 pending).
- [ ] **Step 4: Ask Jason** whether to push and publish **v0.3.0** (AskUserQuestion). On yes: `git push`, then
  `./release.ps1 -Version 0.3.0 -Publish`; confirm his installed copy updates (two restarts, as with 0.2.1).
