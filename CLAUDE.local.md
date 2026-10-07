# Depths - project rules

These win over `CLAUDE.md` for this repository where the two conflict.

Top-down roguelite (inspired by Isaac, Nuclear Throne, Gungeon, Zelda). Free, never sold.
Read `CONVENTIONS.md` (rules) and `docs/STATUS.md` (plan + state) before doing anything.
Update `docs/STATUS.md` before ending any session.

## Working with the owner
- **Consultant, not only coder.** Offer design opinions (what is more fun/logical/optimal), propose
  alternatives they can playtest, ask questions. They like trying different approaches.
- **Git in cloud sessions:** `git config claude.mode solo`, and work on the branch the session was
  given. Commit and push only when told to work autonomously; otherwise ask at the end of each task.
  The container is wiped when the session ends, so say plainly when work is not pushed yet.

## Direction
- **C# / Unity 6.3 LTS is the main game.** The JS (`depths.html`, `src/`) is a frozen reference,
  tagged `js-final`. No new features in the JS; C# may differ from it on purpose.
- `csharp/Depths.Core` = engine-independent simulation (netstandard2.1, LangVersion 9.0).
  Unity code only presents and reads input.
- Plan and next steps: `docs/STATUS.md`.

## Practical
- Verify: `.\verify.ps1` (hygiene, canary, JS suite at 3 viewports, C# tests, parity).
  JS suite alone: open `depths.html?test`. C#: `dotnet test csharp/Depths.sln`.
- Linux cloud sessions: `apt-get install dotnet-sdk-10.0`, then
  `DOTNET_ROLL_FORWARD=Major dotnet test csharp/Depths.sln`.
- Files are UTF-8, LF-only, no BOM (see CONVENTIONS.md hard constraints).
- In `src/`, comments tagged `[h:<file>-N]` have their full text in `docs/history/src/<file>.md`.
- C# files still carry long comments; move them out the same way when first touching them.
- Known: in headless Linux Chromium one JS test fails for environmental reasons (font);
  check it passes under the Windows gate.
