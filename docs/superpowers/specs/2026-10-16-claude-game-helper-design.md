# Claude Game Helper — Design

**Date:** 2026-10-16 (revised same day: generalized from a Fallout 76-only helper to game profiles)
**Owner:** Jason Wilson
**Status:** Approved by Jason, 2026-10-16
**Location:** the `claude-game-helper` repo (personal project; renamed from `fallout-76-helper`)

## 1. Intent

Jason is returning to Fallout 76 after more than a year away. The game has changed in that time
(balance patches, new regions, new systems), and his memory of his own characters has faded. He wants
Claude Code running as a **sidecar while he plays**: a Windows tray app that, on a hotkey, captures the
game screen or takes a typed question, sends it to Claude Code, and shows the answer in a panel over
the game. Claude uses web search freely so its advice reflects the game as it is today, not as it
was when the model was trained.

Very little of that is specific to Fallout 76, and Jason expects to use the same helper for other games
— Mount & Blade II: Bannerlord, Valheim, Cyberpunk 2077, and Fallout 5 when it ships. So the helper is
a **general game sidecar with one profile per game**. Version 1 ships with the Fallout 76 profile only.

Success: during a normal play session he can press a key, get a useful answer within half a minute,
and keep playing — and over a few sessions the helper gets him back to an optimized character
without him alt-tabbing to wikis and build planners. Adding a second game later means adding a profile
folder, not changing the app.

## 2. Goals

For whichever game is running, the helper does four jobs. The Fallout 76 profile's version of each:

1. **Character optimization.** From screenshots of SPECIAL, perk cards, legendary perks, mutations,
   and gear, recommend changes: card swaps, where the next point goes, which legendary perks to work
   toward, and what recent patches changed about his build.
2. **Quest direction.** From a screenshot of the quest log or map, say where to go next and how to
   get there, in plain directions ("head northeast to the Whitespring, enter through the side gate").
3. **"Is this good?"** From a screenshot of loot, a vendor, a plan, or a stash screen, say keep,
   sell, scrap, scrip, or learn — and why, in a sentence.
4. **Ad hoc questions.** Anything he types, with or without the current screen attached
   ("what's the best way to farm treasury notes now?").

Across all four: **web search is central**. Claude checks current sources (for Fallout 76: the Fallout
Wiki, patch notes, community build guides) whenever an answer depends on the game's current state.

## 3. Non-goals (v1)

- **Profiles for games other than Fallout 76.** The structure supports them; none are written yet.
- **A screen for managing profiles.** Until a second game needs one, a profile is a folder Jason copies.
- **Watch mode** — Claude periodically watching the game and volunteering suggestions. Confirmed as
  phase 2 (§7.10); v1 is built so it can be added without rework.
- **Live position tracking.** The helper knows where he is only from a screenshot.
- **Mod-based data export** (Invent-O-Matic / Script Fallout Extender for Fallout 76). Considered and
  deferred; it needs DLL injection into the game and is a gray area.
- **Voice input or read-aloud answers.** Mentioned early, not chosen. *(Builder's call: leave room
  for it, don't build it.)*
- **Multiple machines, other players, or sharing.** It runs on Jason's PC, for Jason.

## 4. Anti-goals — failure even if it technically works

- **Anything that could get a game account banned.** No reading game memory, no injecting into the
  game process, no sending keystrokes or mouse input to the game, no automation of play. The helper
  only looks at the screen through standard Windows capture and only acts in its own window.
- **Stale advice delivered confidently.** An answer about current game balance given from the model's
  training data, without checking, when the game has since changed.
- **Getting in the way of play.** Stealing focus and keeping it, popping up uninvited, covering the
  game when he didn't ask, or stalling the game's frame rate.
- **Walls of text.** Answers lead with the verdict in one sentence; detail follows only if useful.
- **Fallout 76 assumptions baked into the app.** Anything game-specific lives in the profile, not the
  code.

## 5. Constraints

- **Platform:** Windows 11 (his PC). Fallout 76 is the Microsoft Store version, run in **borderless
  window** mode at 2560×1440 (`Project76Prefs.ini`: `bBorderless=1`). An overlay window works over
  borderless; it does not over exclusive fullscreen. A game run in exclusive fullscreen needs the panel
  on the second monitor.
- **Monitors:** must work on a single monitor. A second monitor exists and is optional.
- **Claude Code:** must use Jason's existing Claude Code login — no separate API key or billing. It is
  the same subscription he uses for work (see §6.6).
  The `claude` command from npm is currently broken on this PC (its `claude.exe` is missing); a working
  binary ships with the VS Code extension at
  `%USERPROFILE%\.vscode\extensions\anthropic.claude-code-<version>-win32-x64\resources\native-binary\claude.exe`
  (2.1.295 verified, with `-p`, `--output-format stream-json`, `--include-partial-messages`, `--resume`,
  `--session-id`, `--allowedTools`, `--add-dir`, `--append-system-prompt`, `--model`). That path changes
  on every extension update. See §7.4.
- **Toolchain available:** .NET 10 SDK (10.0.302), git 2.53.
- **Fallout 76 data that exists locally** (read-only to the helper): settings at
  `Documents\My Games\Fallout 76\Project76Prefs.ini`. Character data
  is server-side and not on disk.

## 6. Decided by Jason

### 6.1 The answer window — side panel with history (mockup option B)

- A slim, dark panel docked to the right edge of the screen, drawn on top of the game.
- Keeps the whole conversation as a chat history so follow-ups have context.
- Works on one monitor; can be dragged to the second monitor and left open there.
- Answers stream in as Claude writes them.

The mockups are in `.superpowers/brainstorm/479-1792175238/content/answer-window.html`.

### 6.2 Three hotkeys

| Hotkey | What it does |
|---|---|
| **Screen** | Captures the game, opens the panel, and asks "what am I looking at, and what should I do?" |
| **Ask** | Opens the panel with the cursor in a text box and an "include screenshot" checkbox (checked by default). Enter sends; Esc hides the panel. |
| **Toggle** | Shows or hides the panel without asking anything. |

### 6.3 Behavior agreed in playback

- Hiding the panel returns keyboard focus to the game.
- Hotkeys are configurable; defaults avoid keys the game uses.
- Conversations are saved to files.
- Tray icon menu: show panel, new conversation, open tracking file, settings, quit.

### 6.4 Technology (proposed by Claude, accepted by Jason)

- **C# / .NET 10 / WPF** for the tray app and panel — his everyday stack, and WPF handles topmost
  windows, global hotkeys, and screen capture simply.
- **Claude Code CLI in print mode** (`claude -p`) as the brain: the app spawns it per question,
  passes the prompt and screenshot path, streams the response, and resumes the same session for
  follow-ups. (The Claude Agent SDK was considered; it adds work without changing what Jason sees.)
- **Capture the game window only**, not the whole desktop. Screenshots are kept in the profile folder.

### 6.5 Tracking files — Claude edits on its own

Claude updates the profile's tracking files directly when a screenshot or conversation reveals something
new, and says in the panel what it changed (one line, e.g. "Updated character: swapped Bloody Mess for
Tenderizer"). No approval step. Mistakes are fixed by editing the file.

### 6.6 Speed vs. quality — balanced

The helper shares Jason's work Claude subscription, so it is economical by default:

- A **fast model** for quick screen questions; a **stronger model** for build planning (§7.8).
- Screenshots are **shrunk before sending**, enough to read the game's text but no larger.
- Long play sessions **roll over to a fresh conversation** once the history gets large; the tracking
  files carry what matters across the rollover.

### 6.7 One profile per game

Jason's words: "a different profile and tracking file per game." Each game gets a profile folder; the
app's code is game-neutral. v1 ships the Fallout 76 profile only. Layout and contents in §7.1.

## 7. Calls made by Claude (Jason has not explicitly agreed — check these)

### 7.1 Profile layout

```
claude-game-helper\
  src\                         the app (game-neutral)
  profiles\
    fallout-76\
      profile.json             window match, read-only paths, hotkey/model overrides, flags
      CLAUDE.md                the advisor instructions for this game
      tracking\                files Claude reads and updates (for Fallout 76: one per character)
      screenshots\
      conversations\
```

- **`profile.json`**: display name; how to recognize the game window (process name, with window title
  as a fallback — builder verifies the Store version's process name); read-only local paths (for
  Fallout 76, the settings folder); optional hotkey and model overrides; and an `allowWatchMode` flag
  for phase 2.
- **`CLAUDE.md`**: the advisor persona and rules for this game — lead with the verdict; keep it short;
  **search the web before stating anything about current balance, drops, events, or vendors**; which
  sources to trust; say when unsure; never suggest exploits that risk a ban; read the active tracking
  file first.
- Claude runs with the **profile folder as its working directory**, so the profile's `CLAUDE.md` loads
  on its own.

### 7.2 Choosing the profile

The app picks the profile whose window match fits the game in the foreground when a hotkey is pressed.
If no profile matches, the panel says so and offers a plain "no game" conversation (ad hoc questions
still work). The panel header shows which profile is active.

### 7.3 Fallout 76 tracking files

**One Markdown file per character** (he has two), under `profiles\fallout-76\tracking\`. Each holds
level, SPECIAL, perk loadout, legendary perks, mutations, main weapons and armor, goals, wishlist, and
notable stash items. The panel has a picker for the active character; that file is the one Claude reads
and updates (§6.5). The tray menu's "open tracking file" opens the active one.

Other games decide their own tracking layout in their profile's `CLAUDE.md` (a colony file, a save
progress file, a build file).

**First run:** no files exist. Claude creates the file for a character the first time Jason shows it
that character's screens, asking only for the character's name if it can't read it from the screen.
Jason doesn't fill anything in by hand.

### 7.4 Sessions and saved conversations

Each app launch (each play session) starts a fresh conversation per profile; the tracking files carry
continuity across sessions. "New conversation" from the tray resets mid-session. The conversation also
rolls over on its own when it gets large (§6.6).

Saved conversations are an **archive only** in v1: written to the profile's `conversations\` folder for
Jason to read in any editor. No browsing or reopening them from the app.

### 7.5 Finding Claude Code

Resolve the executable in this order: a path set in settings → `claude` on PATH, verified by running
`--version` → the newest `anthropic.claude-code-*` VS Code extension binary. If none works, the panel
says so plainly, with what to fix. *(Jason may prefer to repair the standalone install instead; that's
independent of this design.)*

### 7.6 Claude's permissions

Restrict tools to what the advisor needs: web search, web fetch, reading files in the active profile
folder (screenshots, tracking files), creating and editing only files in that profile's `tracking\`
folder, and read-only access to the profile's declared local paths (so it can answer "fix my mouse
sensitivity"-type questions by reading, not writing, `Project76Prefs.ini`). No shell, no writing
anywhere else.

### 7.7 Default hotkeys

Ctrl+Shift+F1 (Screen), Ctrl+Shift+F2 (Ask), Ctrl+Shift+F3 (Toggle) — chosen because Fallout 76 does
not bind Ctrl+Shift chords or F1–F3 combinations by default. Builder should verify against the game's
current default keybindings. A profile can override them for a game that uses these keys.

### 7.8 Which model answers

- **Screen hotkey** and plain Ask questions use the **fast model**.
- A question with **two or more screenshots** (§7.9), or one sent with a **"Deep" toggle** in the Ask
  box switched on, uses the **stronger model**. Build planning naturally lands here.
- Both model choices are settings (Claude Code model aliases passed with `--model`), overridable per
  profile, so Jason can change them without a rebuild.

### 7.9 Several screens in one question

Character optimization needs five screens (SPECIAL, perk cards, legendary perks, mutations, gear).
**While the Ask box is open, pressing the Screen hotkey adds another screenshot to the pending question
instead of sending it.** The Ask box shows the pending screenshots as a strip of thumbnails, each
removable. Enter sends them all as one question. This keeps the three hotkeys from §6.2.

### 7.10 Watch mode — phase 2

Jason confirmed it as phase 2. Not built in v1. The shape, so v1 leaves room:

- A **Watch mode** switch, **off by default**, with an interval setting (a few minutes).
- A free, on-machine **change check** decides whether a frame is worth sending, so walking and combat
  don't spend his subscription.
- The fast model, and no web search unless a suggestion needs it.
- Suggestions are **quiet**: a line added to the panel and an unread dot on the tray icon, never a
  popup over the game (§4). The second monitor is where it shines.
- It's commentary, not real-time coaching — answers take 10–30 seconds.
- A profile can set `allowWatchMode: false` — for competitive multiplayer games, where live outside help
  may break the game's rules even though the helper only looks at the screen. All the games on Jason's
  list are single-player or co-op.

v1 requirement: the capture and "send to Claude" code is a reusable service that watch mode can call on
a timer.

### 7.11 Progress feedback

Web-searched answers may take 10–30 seconds. The panel shows a working indicator with what Claude is
doing (e.g. "searching the wiki…") from the stream, so a slow answer never looks like a hang.

## 8. Left to the builder

- Capture technique for a borderless DirectX window (Windows.Graphics.Capture vs. GDI), and image
  size/compression sent to Claude.
- How the panel avoids stealing focus when it opens from the Screen hotkey, and how focus is handed back
  to the game on hide.
- Stream-JSON parsing, session-id handling with `--resume`, and cancellation of an in-flight answer.
- The size threshold for rolling a conversation over (§6.6), and what the panel says when Jason hits his
  Claude usage limit mid-game.
- Exact screenshot downscale size (§6.6).
- Settings storage format and location; first-run setup.
- Markdown rendering in the panel.
- Project and folder layout under `src\`, logging, and tests.
- Initializing the git repo (with `.superpowers/` in `.gitignore`).

## 9. Open questions

None. Watch mode was confirmed as phase 2 (§7.10).
