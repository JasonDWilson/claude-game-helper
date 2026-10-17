# Claude Game Helper — Watch Mode (Phase 2) — Design

**Date:** 2026-10-16
**Owner:** Jason Wilson
**Status:** Approved by Jason, 2026-10-16
**Builds on:** [v1 design](2026-10-16-claude-game-helper-design.md) §7.10 (the watch-mode sketch) and
[sharing design](2026-10-16-sharing-and-installer-design.md). Current release: v0.2.1.

## 1. Intent

Today the helper only speaks when asked. Jason wants it to also keep an eye on the game and speak up in
the moments he'd otherwise lose: **when he's looking for something but likely to miss it, or going the
wrong direction**, plus **general help** he didn't think to ask for. He's coming back to games after more
than a year away, which is exactly when unprompted help is worth the most.

Success: he asks "where's the Whitespring?", turns on watching, and a minute later the helper says "wrong
way, turn around and follow the road east" before he's wasted ten minutes, without him pressing anything
and without it getting in the way of play.

## 2. Goals

1. **Goal-following.** Watching starts from his **last question**: Claude keeps an eye out for that goal
   (a place, an item, a quest step) and speaks up when he's about to miss it or heading the wrong way.
2. **General help.** While watching, Claude also volunteers tips, but only ones that clear the bar in §5.3.
3. **He controls it.** Each kind can be switched off, and he sets how often it checks.
4. **Cheap and quiet.** Checks skip frames that barely changed, and watching ends on its own when there's
   nothing to watch, so it doesn't drain his Claude plan.

## 3. Non-goals

- Real-time coaching. A check takes 10–30 seconds; this is commentary, not a co-pilot.
- Voice or read-aloud.
- Watching games whose profile sets `allowWatchMode: false` (competitive multiplayer).
- Changing how asked questions work.

## 4. Anti-goals (carried from v1, still binding)

- Anything that could get an account banned: watch mode only uses the same window capture as the hotkeys.
- Stealing focus, or covering the game when he didn't ask for it.
- A chatty panel he learns to ignore. Missing a tip costs less than noise.
- Advice about current game balance stated from memory when it should have been checked.

## 5. Decided by Jason

### 5.1 What it's for
Catching "looking for something but likely to miss it" and "going the wrong direction", plus general help.

### 5.2 Where the goal comes from
**His last question.** Asking a new question replaces the goal.

### 5.3 The bar for a general tip (proposed by Claude; Jason: "sounds good")
A tip must be at least one of:
- **Easy to miss and worth having**: rare loot or plan, a legendary enemy, an event starting nearby, a
  wishlist item on a vendor.
- **A mistake about to happen**: underleveled for a fight, about to be overencumbered, about to scrap or
  sell something valuable.
- **Tied to his tracking file**: his goals or wishlist.

Never lore, trivia, generic advice, or anything already said this session. At most one general tip every
few minutes; when unsure, stay quiet.

### 5.4 Controls
Watch mode can be **disabled**, and he can **set the frequency** of checks.

### 5.5 Goal alerts may show the panel
For a goal alert (wrong way, about to miss it), the panel **slides in without taking focus** if it was
hidden. This is a deliberate exception to v1's "never a popup over the game", and only for goal alerts
while he has watching switched on. General tips never open the panel.

## 6. Calls made by Claude (Jason has not explicitly agreed — check these)

### 6.1 Turning it on and off
- Off by default. A **"Watch mode"** submenu in the tray: **Follow my goal** (checkmark), **General tips**
  (checkmark), **Check every** 20 s / 30 s / 1 min / 2 min (default 30 s), **Chime** (checkmark, on).
  Tray changes take effect immediately and are saved.
- **The two switches are per game** (remembered per profile id in `settings.json`): turning watching on
  for Fallout 76 doesn't turn it on for another game, such as a competitive shooter. Frequency and chime
  are global. Games whose profile sets `allowWatchMode: false` show the switches disabled; the fallback
  ("No game") never watches.
- With **Follow my goal** on, a typed question starts watching for it. The Screen hotkey's built-in
  question ("What am I looking at…") never becomes a goal. With **General tips** on, it watches while
  that game is in front, goal or not.
- The panel header shows **"Watching: <goal>"** with a **✕ Stop** button while a goal is active.

### 6.2 What the goal is, and when it ends
- **Not every question is a goal.** The first check for a new question decides whether there's anything
  to watch for; for "what's the best rifle build?" there isn't, and that one check ends it quietly.
- **Follow-ups keep the goal.** Checks see his last three typed questions and their answers and infer the
  current goal from them, so "which road?" after "where's the Whitespring?" continues the Whitespring
  goal, and a truly new question takes over. (This is how §5.2's "a new question replaces it" works.)
- Claude says it's done ("you've arrived", "you picked it up"): watching for that goal stops with a short
  note in the panel.
- **✕ Stop** in the panel, or the tray switch.
- **30 minutes** after the goal was set, it stops by itself so a forgotten goal doesn't keep spending.

### 6.3 Getting his attention
- **Goal alerts** (wrong way, about to miss it): a soft chime, and the panel **appears without taking
  focus** if it was hidden (Jason's decision, §5.5).
- **General tips**: a chime, a line in the panel, and a dot on the tray icon until the panel is next shown.
  They never open the panel on their own.
- Tips are visually marked as unprompted in the panel (a small "👀 Watch" label instead of "You/Claude").

### 6.4 What a check does
- Runs only when: watch mode has something to do (a goal, or general tips on), the game window is in
  front, the panel isn't being typed in, and no question of his is being answered (his questions always
  win; a check never queues behind or delays one).
- **Change check on the PC first:** a tiny grayscale thumbnail of the game is compared with the last one
  sent to Claude; if it barely changed (standing still, sitting in a menu), the check is skipped and costs
  nothing.
- Otherwise Claude gets the screenshot plus: the goal (his question and the answer he got), the active
  tracking file, the tips already given this session, and the rules from §5.3. It answers with either
  **nothing**, a **goal alert**, a **tip**, or **goal reached**, in a fixed format the app can read.
- Checks use the **fast model at low effort** and **no web search** (for speed and cost). The goal's
  answer already carries whatever was searched; if a tip needs fresh facts, Claude suggests he ask.
- Checks run in **their own Claude conversation** per game, separate from his questions, so they don't
  bloat his conversation or its archive. Only tips and alerts that were shown are written to the archive.
- **His next question sees recent watch messages:** the last few tips/alerts shown are added to the next
  question's prompt, so "where's that vendor you mentioned?" can be answered.
- **Checks are read-only:** they can't edit his tracking file (no Edit/Write tools). If a tip is worth
  noting, Claude says "ask me to note it".
- One screenshot file per game, overwritten each check (`screenshots\watch.jpg`), so watching doesn't fill
  the disk.

### 6.5 Pacing and limits
- General tips: at most one every **3 minutes**; the same tip never twice in a session.
- **Cost guard:** with no active goal (general tips only), checks run at most every **2 minutes**, whatever
  the frequency setting. The frequency setting governs goal checks.
- Goal alerts: no fixed spacing, but not the same alert twice within a minute.
- If Claude reports a usage limit, watch mode pauses, the panel says why, and it resumes after the reset
  time or when turned back on.
- The log counts checks sent vs. skipped, so cost is visible.

### 6.6 Where the work lives
A `WatchService` in the Core library (timer-free logic, testable) driven by a timer in the app, reusing
`AskService`'s Claude plumbing and the existing window capture (as the v1 design required).

## 7. Left to the builder

- The change-check thumbnail size and threshold.
- The exact reply format and how it's enforced (a JSON schema via the CLI, or a strict text format).
- The watch-check instructions text (built from §5.3 and §6.4).
- The chime sound (a built-in Windows sound is fine) and the tray dot drawing.
- Tests.

## 8. Open questions

None.
