# Handoff — Claude Game Helper

Written 2026-10-16 at the end of the first conversation, which ran in a Claude Code session started from
an unrelated repo. This file carries everything a new session in this repo needs to continue without
starting over.

## Where things stand

- **Phase:** v1 built and played with Fallout 76 (works well). **Sharing build done** per
  [the sharing plan](superpowers/plans/2026-10-16-sharing-and-installer.md): Velopack installer, automatic
  updates from GitHub Releases, automatic profiles for any game, data in `Documents\Claude Game Helper`,
  README and MIT license. **Published:** https://github.com/JasonDWilson/claude-game-helper, release
  v0.2.0 (2026-10-16). Installed from that release on Jason's PC; its update check reaches GitHub.
- **Designs:** [v1](superpowers/specs/2026-10-16-claude-game-helper-design.md) (watch mode confirmed as
  phase 2) and [sharing](superpowers/specs/2026-10-16-sharing-and-installer-design.md).

## How to run it

- **Installed:** `ClaudeGameHelper-win-Setup.exe` from GitHub Releases; it starts with Windows.
- **From source:** `dotnet run --project src/ClaudeGameHelper`. Quit the installed copy first (one copy
  at a time).
- Tray icon: orange tile with a dark game controller (from `tools/make-icon.ps1`). Ctrl+Shift+F1 =
  Screen, F2 = Ask, F3 = show/hide.
- Data and logs: `Documents\Claude Game Helper\` (`<game>\`, `logs\`). Settings:
  `%APPDATA%\ClaudeGameHelper\settings.json`. Built-in instructions:
  `src/ClaudeGameHelper/BuiltInProfiles/`.
- Tests: `dotnet test` from the repo root.

## Releasing

`./release.ps1 -Version x.y.z` builds `releases\ClaudeGameHelper-win-Setup.exe`; add `-Publish` to upload a
GitHub release (needs `gh auth login`). Installed copies pick it up on their next start.

## Next steps

1. Share https://github.com/JasonDWilson/claude-game-helper/releases/latest.
2. Deferred minors from the sharing review: fall back to `%LOCALAPPDATA%` if Documents is missing; Minecraft
   Java version bumps create a new profile per version; a personal `CLAUDE.md` added mid-session needs a
   restart (README doesn't say so).
3. **Watch mode (phase 2) is built** per [its plan](superpowers/plans/2026-10-16-watch-mode.md) and
   [design](superpowers/specs/2026-10-16-watch-mode-design.md): tray → Watch mode → Follow my goal / General
   tips (per game), Check every, Chime. Not yet tried with a game running; try it, then release v0.3.0.

Commits: author Jason Wilson <jason@jwpro.com> (set in this repo's git config). The pre-publish private
history is backed up outside the repo in `claude-game-helper-private-history.bundle`.

## Verified in the second session (Claude Code 2.1.295)

- `--safe-mode --restricted --disable-slash-commands --permission-mode dontAsk` with `--tools` and
  `--allowedTools "…,Edit(./tracking/**),Write(./tracking/**)"` works with his normal login. No user plugins,
  hooks, or global CLAUDE.md load. Writes outside `tracking/` are denied. Reads outside the working dir
  and `--add-dir` are denied. `--append-system-prompt-file` delivers the profile's CLAUDE.md.
  `--resume <id> --model <other>` keeps the session.
- `--bare` is unusable: it accepts only an API key, not his subscription login.
- Fallout 76's Store executable is `Project76_GamePass.exe` (from the package's `MicrosoftGame.config`).
- Real stream output is saved as test fixtures in `tests/ClaudeGameHelper.Tests/Fixtures/`.

## How the design got here (decisions in Jason's words, briefly)

- He's returning to Fallout 76 after more than a year away and wanted Claude Code as a sidecar to plan
  his play.
- Chose a **hotkey tray app** over a simple screenshot-folder watcher, in its own repo folder.
- Jobs he wants: character and perk optimization, a "direction finder for quests," judging loot, and
  **ad hoc questions**. "Claude Code doesn't have to be limited to memory — it can search the web for
  recent knowledge."
- A second monitor exists, but "it shouldn't be required."
- Picked **mockup B: the side panel with history** (mockups in
  `.superpowers/brainstorm/479-1792175238/content/`).
- Accepted Claude's tech defaults: C# / .NET 10 / WPF, with `claude -p` as the brain.
- Character files: **Claude updates on its own** (not ask-first). Speed vs. quality: **balanced**.
- Asked about Claude **watching the game and making suggestions** periodically → proposed as phase 2.
- "This would be generally useful for a lot of different games. We could just have a different profile
  and tracking file per game." → renamed to **claude-game-helper**. Games he named: **Bannerlord,
  Valheim, Fallout 5 (when it comes out), Cyberpunk**.

## Facts about this PC (gathered in the first session)

**Claude Code**
- `claude` on PATH is **broken**: `%APPDATA%\npm\claude.cmd` points to
  `node_modules\@anthropic-ai\claude-code\bin\claude.exe`, which doesn't exist.
- Working binary: `%USERPROFILE%\.vscode\extensions\anthropic.claude-code-2.1.295-win32-x64\resources\native-binary\claude.exe`
  (an older 2.1.284 copy sits beside it). Supports `-p`, `--output-format stream-json`,
  `--include-partial-messages`, `--input-format`, `--resume`, `--session-id`, `--allowedTools` /
  `--disallowedTools`, `--add-dir`, `--append-system-prompt`, `--model`.
- Jason runs Claude Code inside **Cursor** (`%LOCALAPPDATA%\Programs\cursor`).

**Toolchain:** .NET SDKs 3.1, 7.0, 9.0, **10.0.302**; git 2.53; Windows 11 Pro.

**Fallout 76**
- **Microsoft Store** version, package `BethesdaSoftworks.Fallout76-PC` 1.0.171.0, installed under
  `C:\Program Files\WindowsApps\` (locked folder).
- Settings and logs: `Documents\My Games\Fallout 76\` (Documents is OneDrive-redirected on this PC;
  files can be online-only placeholders that fail to read until downloaded).
  The Store version names its files `Project76.ini` / `Project76Prefs.ini`, not `Fallout76*.ini`.
- Display: 2560×1440, **borderless window**, High preset, TAA, V-Sync on; FOV 80 (85 first-person);
  controller enabled; master volume ~20%.
- **Two characters** (portrait folders created May and July 2024). Neither has played as a ghoul
  (`bHasSeenGhoulToHumanTutorial=0`).
- Last real play was before June 2025; a 3-minute launch on 2026-06-12 only reached the main menu.
  Newest in-game photo is from October 2024.
- Character data (level, perks, inventory) is **server-side** — the only way in is screenshots.

## How Jason likes to work

- **Plain English.** Spell terms out; no shorthand or process jargon in questions and summaries.
- **One question at a time**, with a recommendation. Use the AskUserQuestion tool for choices.
- Once a task is underway, **don't re-ask for approval** on obvious sub-steps.
- **Keep scope minimal.** Don't let review loops expand what gets built.
- **American English** spelling everywhere.
- **Never create a git branch** unless he says so.
- **Build and run the full test suite once, at the end of a task**, not after each step.
- When he says "just talking," he's thinking out loud. Discuss the idea; don't build.
- Personal project, so the rules from his day-job repos **do not apply here**.
