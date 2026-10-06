# Depths — read me first

Top-down roguelite (inspired by Isaac, Nuclear Throne, Gungeon, Zelda). Free, never sold.
Read `CONVENTIONS.md` (rules) and `docs/STATUS.md` (plan + state) before doing anything.
Update `docs/STATUS.md` before ending any session.

## How the owner works with you
- **Cost-conscious.** Before each task, say which effort level (Low / Medium / High) it needs and
  wait for their go-ahead. If a step needs more effort than the session has, stop and say so.
- **Transparency.** Say what you are about to do and what you changed. No silent edits.
- **Consultant, not only coder.** Offer design opinions (what is more fun/logical/optimal), propose
  alternatives they can playtest, ask questions. They like trying different approaches.
- **Confirm the task order before changing files.**
- **Helper agents:** recommend them when useful (cheaper model for mechanical work, a parallel
  port slice, an independent review); start one only after the owner says yes.

## Direction
- **C# / Unity 6.3 LTS is the main game.** The JS (`depths.html`, `src/`) is a frozen reference,
  tagged `js-final`. No new features in the JS; C# may now differ from it on purpose.
- `csharp/Depths.Core` = engine-independent simulation (netstandard2.1, LangVersion 9.0).
  Unity code only presents and reads input.

## Session plan (details in docs/STATUS.md)
Done: JS frozen and tagged `js-final` (Brunch rule A+), Unity skeleton, main menu, the full port.
1. **Next:** heart values (owner to confirm the integer half-heart proposal in STATUS), then
   switch C# to 60 Hz with per-second units; re-baseline tests (Medium).
2. New sound engine in Unity: event-based (`play("hit", pan)`), sample assets, mixer (Medium).
3. Later: item overhaul (two items run on placeholders), difficulty curve (owner's direction in STATUS).

## Practical
- Verify: `.\verify.ps1` (hygiene, canary, JS suite at 3 viewports, C# tests, parity).
  JS suite alone: open `depths.html?test`. C#: `dotnet test csharp/Depths.sln`.
- Files are UTF-8, LF-only, no BOM (see CONVENTIONS.md hard constraints).
- In `src/`, comments tagged `[h:<file>-N]` have their full text in `docs/history/src/<file>.md`.
- C# files still carry long comments; move them out the same way when first touching them.
- Known: in headless Linux Chromium two JS tests fail for environmental reasons (font, audio);
  check they pass under the Windows gate.
