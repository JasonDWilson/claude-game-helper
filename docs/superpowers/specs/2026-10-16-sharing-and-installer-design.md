# Claude Game Helper — Sharing, Installer, and Any-Game Profiles — Design

**Date:** 2026-10-16
**Owner:** Jason Wilson
**Status:** Approved by Jason, 2026-10-16
**Builds on:** [2026-10-16-claude-game-helper-design.md](2026-10-16-claude-game-helper-design.md) (v1, built and
working on Jason's PC)

## 1. Intent

v1 works well for Jason. He wants to share it: first with family, by handing them an installer, and then
possibly with anyone, through GitHub. They play other games, not Fallout 76, so the helper has to be
useful for whatever game is in front, with nothing to set up by hand.

Success: someone runs one setup file, presses a hotkey in their game, and gets a useful answer. Later fixes
reach them without Jason sending anything. A stranger who finds the GitHub repo can do the same.

## 2. Goals

1. **One-file install.** A setup `.exe` from GitHub Releases. It installs for the current Windows user with
   no admin prompt, and needs no separately installed .NET. It adds a Start menu entry and a desktop
   shortcut.
2. **Automatic updates.** The helper checks GitHub Releases when it starts, downloads a newer version
   quietly, and applies it the next time it starts.
3. **Any game, zero setup.** The first time a hotkey is used on a game the helper has no profile for, it
   creates one for that game automatically, named from the game's window. Claude is told which game it
   is, searches the web for it, and keeps notes files for it across sessions. Fallout 76 still ships as a
   hand-tuned profile.
4. **Nothing personal in the product.** Instructions speak to "the player," not Jason. Each person's notes,
   screenshots, and conversations live on their own PC.
5. **A public GitHub repo** with a README that a stranger can follow, and one command for Jason to build
   and publish a release.

## 3. Non-goals

- **Helping recipients install or sign in to Claude Code.** They must already have it and be signed in to
  their own account. If the helper can't find it, the panel says so (as v1 already does).
- **Code signing.** The installer is unsigned at first, so Windows shows "Windows protected your PC" once;
  the README tells people to click "More info → Run anyway." A certificate can come later.
- **A screen for managing profiles.** Profiles are folders; people can edit or delete them by hand.
- **Watch mode** stays phase 2.
- **Mac or Linux.**

## 4. Anti-goals

- **An update that touches anyone's notes, screenshots, or conversations.**
- **Shipping Jason's personal data:** his character files, his Windows username or PC paths, his
  conversations.
- **Profiles appearing for things that aren't games** in ways that clutter or confuse (see §6.2).
- Everything in the v1 design's anti-goals still applies (nothing that risks a game ban, no focus
  stealing, game-specific content only in profiles).

## 5. Decided by Jason

- **Audience:** family first, then possibly anyone via GitHub. (§1)
- **Recipients bring their own Claude Code;** the installer doesn't help set it up. (§3)
- **Generic enough for general appeal:** automatic profiles for any game, Fallout 76 kept as the tuned
  example. (§2.3)
- **Updates arrive automatically** from GitHub. Jason has a GitHub account; the repo needs creating. (§2.2)
- **Public repo, tidied:** his character files, screenshots, and conversations never go in the repo; his
  Windows username and PC paths are replaced in recorded test data and notes. Design docs and handoff notes
  stay public, with **work and family details stripped**: day-job references, the OneDrive folder path, PC
  paths, and family members. Game history and design reasoning stay. (§6.6)
- **Commit identity:** the public history shows his **personal email address**, not his work email (he first
  chose the GitHub no-reply address, then asked for his personal address); existing commits are rewritten to
  it and the repo is configured to use it. (§6.6)
- **Starts with Windows by default,** with a "Start with Windows" checkmark in the tray menu to turn it off.
  (§6.8)
- **MIT license.** (§6.7)

## 6. Calls made by Claude (Jason has not explicitly agreed — check these)

### 6.1 Installer and updater: Velopack

Velopack (free, open source) builds the setup `.exe` from a self-contained .NET publish, installs per user
under `%LOCALAPPDATA%`, and updates from GitHub Releases. The app calls Velopack's startup hook first thing
in `Main` and checks for updates in the background after startup; a downloaded update is applied on the
next start. No update prompts during play.

### 6.2 When a game gets an automatic profile

A profile is created **only when a hotkey is pressed** with an unknown window in front, never just because a
window came to the front. This keeps chat apps and the like from getting profiles unless someone actually
uses the helper on them. A short skip list never gets a profile (the Windows desktop and taskbar, File
Explorer, common web browsers, and the helper itself); those keep using the "No game" mode.

- **Folder name:** the process name, lowercased (`valheim`). **Display name:** the window title, trimmed.
- **Programs that host many games** (Java, which runs Minecraft and others): told apart by window title
  instead of process name, so each game gets its own profile and notes.
- **Instructions:** a generic template (§6.3) with the game's name filled in.
- **Notes label:** "Notes." Claude decides how to organize notes files for that game (per character, per
  save, one file); the panel's picker works the same way as Fallout 76's character picker.
- **Wrong guesses** (a launcher, say): a "Not a game" button in the panel header, shown only for automatic
  profiles, adds that program to a per-person "never a game" list in settings and switches to "No game"
  mode. The folder isn't deleted (notes are kept), and the profile isn't recreated.

### 6.3 Generic instructions

A game-neutral advisor template replaces the "No game" text and seeds every automatic profile. It covers:
lead with the verdict; short answers for a narrow panel; search the web before stating anything about the
game's current state; say which patch or date an answer reflects; never suggest exploits that risk a ban;
keep notes files current and say in one line what changed. The Fallout 76 instructions are rewritten to
speak to "the player" and drop Jason's personal history.

### 6.4 Where things live

- **Program:** wherever Velopack installs it; replaced on every update.
- **Built-in instructions** (Fallout 76, the generic template, "No game"): shipped with the program, so
  updates can improve them.
- **Everyone's data:** `Documents\Claude Game Helper\<game>\` holds `tracking\`, `screenshots\`,
  `conversations\`, and (for automatic profiles) `profile.json`. Claude's working folder is this data
  folder, so its write permission (notes only) is unchanged.
- **Personal override:** if a `CLAUDE.md` exists in a game's data folder, it is used instead of the
  built-in instructions, so a person can tune a game without it being overwritten by updates.
- Settings and logs stay where v1 keeps them (`%APPDATA%` and `%LOCALAPPDATA%`).
- **Jason's existing data:** a one-time move of anything under the repo's `profiles\*\tracking`,
  `screenshots`, and `conversations` into the Documents folder. From then on, running from the repo uses
  the Documents folder too.

### 6.5 Releasing

A script in the repo (`release.ps1`) takes a version number, builds the self-contained app, packs it with
Velopack, and publishes a GitHub release with the setup file. It reads Jason's GitHub login from the `gh`
command-line tool. Versions start at `0.2.0` (v1 as built is treated as 0.1).

### 6.6 Repo tidy-up before the first push

- `.gitignore` covers each data folder; data no longer lives in the repo at all (§6.4).
- Jason's Windows username and PC paths in the recorded test streams and notes are replaced with
  placeholders; the handoff notes, specs, and plans lose the work and family details listed in §5.
- The existing local history (never pushed) is rewritten so the first public push carries neither those
  older copies nor his work email: every commit's author becomes his personal address, and the repo's git
  config is set to that address for future commits. Claude confirms with Jason right before rewriting.
- Repo name `claude-game-helper` under Jason's account; Claude confirms with Jason before creating it.

### 6.7 License

MIT, in a `LICENSE` file, copyright Jason Wilson. The README says so.

### 6.8 Starting with Windows

On by default after install: the helper registers itself to start at sign-in for the current user (no
admin). The tray menu gets a "Start with Windows" checkmark that turns it off or on. Uninstalling removes
the registration.

### 6.9 README

For strangers: what it does (with a screenshot of the panel), what you need (Windows 10/11, Claude Code
installed and signed in), install steps including the "Run anyway" warning, the three hotkeys, where your
notes live, how to tune a game's instructions, and a plain statement that it only looks at the screen:
no game memory reading, no input sent to the game.

## 7. Left to the builder

- Velopack versions, project wiring, and the exact `vpk` commands.
- Detecting the skip-list windows reliably.
- How the window title is cleaned up for the display name.
- Moving the data-folder logic out of the repo-relative `profiles\` lookup without breaking dev runs.
- Tests for profile creation, the override rule, and the data move.

## 8. Open questions

None.
