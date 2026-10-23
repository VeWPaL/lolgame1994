# Autonomous session plan (6-7 h), 2026-10-22 (second)

Written before any work; approved by the owner 2026-10-22 with the answers in "Owner answers" below. Branch `opencode/feel-pools-0c15511b`,
worktree `0c15511b`, base `a1f00e3` (pushed). `docs/STATUS.md`, `docs/DESIGN.md` and `docs/bosses.md` stay the source
of truth; this file is the order of work and its bars, and it survives a compaction.

## What the owner decided (2026-10-22)

- **Decision 1 (`docs/bosses.md`): B**, the King's length and structure on every region boss, lethality budgeted.
  But the bot is weaker than the owner: the owner soloed the King with the starting kit faster than the bot's
  average profile does with a run's kit. So **the bot must get better, while still playing like a human**, and the
  difficulty targets are then set against that stronger bot, so the game ends up as hard as the owner wants.
- **Decision 2**: runs of about 73 minutes are fine for now; revisit later.
- Boss health stays flat across depth; stronger items, stats and guns come later.
- **Playtest notes**: two red lines on both pages of the start menu that should not be there; remove every subtext;
  the difficulty curve becomes standard (no switch); no game references in the screen shake options; no hints under
  the difficulty curve and the frame rate.

## Rules for the session

- Commit and push after each unit, only on a green `.\verify.ps1 -Unity`; `tools/unity.sh publish` after each unit
  that changes the game; a snapshot (`tools/snapshot.sh`) after units 3 and 5.
- Every behaviour change ships its tests; every new test is checked by breaking its rule (a mutant) and seeing it
  fail, then the file is restored and touched so MSBuild rebuilds it.
- Bot numbers: seeds 101-200 x every profile, 90 min, A/B on the same build. Each unit's target is written here
  before building and not moved; a missed target is reported, not hidden.
- One cold critic pass per unit; unit 3 (the bot) runs a tournament of two builders in isolated worktrees, judged
  on numbers by a critic who never sees the builders' reasoning.
- STATUS is updated at the end of every unit. If the owner is away when a question comes up, the default named here
  is taken and written into STATUS.

## Unit 0: land what is waiting, about 10 min

The F3 floor skip (`GameView.cs`), `docs/bosses.md` (with the owner's answers to Decisions 1 and 2 folded in), the
STATUS lines. Gate, commit, push. No publish (the playtest build already has F3).

## Unit 1: the menu and the settings, about 40 min

- **The red lines**: the two red margin rules drawn on each page of the book (`LedgerArt.Book`, `LedgerArt.cs:328-331`,
  `Margin` ink). First a `shot` of the menu to confirm these are the lines the owner means, then remove them (the
  blue header rule stays). If the screenshot shows other red lines, those go too.
- **Every subtext goes** (`MainMenu.uxml`): "the seed decides the dungeon", "Being a true account...", the entry notes
  ("descend", "keys, sound, pacing", "close the book"), "Arrows or Tab move the ribbon...", the Audio page's "More
  options will live here...", the keyboard layout's hint (`presetHint`), the curve's hint and the frame rate's hint.
  The `.subtitle`, `.caption`, `.entry-note`, `.foot` and `.hint` styles are removed if nothing uses them; the left
  page is re-spaced so the title and the portrait sit centred without the text under them.
- **Screen shake** choices become "Off", "Medium", "High", "Insanity".
- **The difficulty curve is standard**: the toggle and its settings key go; `Curve.On` is true in the game, the bot
  and the tests' tuned fixtures (`[TunedOn(true)]`); untuned tests keep it off as they keep every tuning off. The
  old saved setting is ignored. The screenshot views that tick the curve (`gameplayon`) are removed.
- **Check the curve against the 14-floor run** before turning it on: its bands (start 1-2, middle 3-10, ramp 11-12,
  end from 13) were written before the run had an end; floors 13-14 are now the Deep and the King. The curve's
  "no drops on floors 1-2" and its health and pace multipliers are listed in STATUS with the numbers they produce.
- Tests: the menu (`MainMenuTests` / the screenshot checks: no subtext labels, four shake names without brackets, no
  curve toggle), the curve on by default in a tuned run, `CurveCheck` still green.
- **Target (frozen)**: the menu screenshots show no margin lines and no subtext; the gate is green.

## Unit 2: the new baseline, curve on, about 25 min (mostly bot time, run in the background during unit 3)

Every number so far was measured with the curve off. A full A/B (`--curve off` vs on, every profile) gives the new
baseline: deaths per area, damage a floor, the King's win rates and fight length. No target: it is a measurement,
written into STATUS, and it is the "before" for unit 3.

## Unit 3: a better bot that plays like a human, about 2 h (tournament of 2)

**Why**: the owner beat the King with the starting kit faster than the average bot beats it with a run's kit
(163 s); with the starting kit the bot takes 242 s and wins 34%. The difficulty can only be judged against a bot as
good as a strong human.

**The new profile, `expert`**, added beside the three (they stay, so old numbers stay comparable). Its strength
comes from playing better, not from superhuman inputs.

The human limits (frozen, a test checks each):
- Reaction to a new tell or shell at least 0.15 s, a top human's (the owner asked for a bot stronger than
  themselves, but still human: the skilled profile's 0.114 s is faster than a person, so it is not the model).
- Aim noise at least the skilled profile's; the aim turns at a capped rate (a mouse, not a snap).
- It reads only what the screen shows: what is drawn (bodies, shells, tells, marks) and nothing from the game's
  random streams or a body's hidden timers that have no tell.
- At most about 8 decisions a second; inputs held at least 2 ticks.

What it does better (the builders choose how; these are the measured gaps):
- **Damage uptime**: the right gun for the range (the Scatter is about 2.5x the Bolt's damage inside 180 px), firing
  whenever it is safe, standing at the range where its gun is best.
- **Reading the field**: choosing where to stand from where the drawn shells and marks will be in the next half
  second (a short look ahead over what is visible), not only stepping off the nearest threat.
- **The blink as a tool**: through a shell wall, out of a closing pattern, keeping one charge in hand.
- **Momentum**: keeping moving when bodies are alive, as a human does.
- **Boss sense**: punishing the windows (a winded Gatekeeper, a stunned King), staying near good ground.

**Targets (frozen)**, measured on 100 seeds:
- The King with the starting kit (the owner's test, `bossnums king`): the owner beat it in under 2 minutes and
  wants the expert stronger than that, so the expert wins at least 75% and its median won fight is at most 120 s
  (the average bot: 34%, 242 s).
- Full runs, curve on: the expert's run win is above the skilled profile's, with no stuck runs and no errors.
- Every human limit above has a test that fails when the limit is broken (a mutant per limit).
- Tournament: two builders, each with the same limits and targets, in isolated worktrees; the critic sees only
  the numbers table and the limit tests, labelled A and B. The better one is merged; the loser's best idea may be
  grafted if the critic names one.

## Unit 4: the boss kit and Old Mudmouth, about 1.5 h

The shared kit from `docs/bosses.md` section 8, built once:
- **Selection**: each area rolls 4 of its 6 on its own stream from the seed, no repeats; a boss not built yet falls
  back to the Gatekeeper, so the game stays playable while the roster grows. Floor 13 rolls 1 of 3. The dev line
  shows the run's draws.
- **Per-area health** (1750 / 2100 / 2150, 1150 on floor 13) with pace from `DepthRate`.
- **Phases with room changes**, a told 2.4 s phase change that calls off the move in hand, the death beat, the drop.
- **Per-attack damage booking** for every boss (the King's `finalBy`, generalised), so the report's boss table shows
  each attack's share.
- Then **S5 Old Mudmouth**, the cheapest Shallows boss: the Burrower's leap, a blast on its 0.3 s crack unearths it
  (new), soft mud in phase 2, tunnels in phase 3, its brood. Its art, the bot's reading of it, a `shot` view.
- **Target (frozen)**, with the expert bot and the average bot, floors 1-4, run's kit: fight 150-180 s for the
  average bot; no attack over 25%; the average bot survives it at least 97% of the time (Decision 1, B).

## Unit 5: the Foreman and the Warden, Returned, about 1.5 h

- **K1 The Foreman** (rides the room's rail; the track changes in phase 2; derails in phase 3) on the kit.
- **D1 The Warden, Returned** for floor 13 (its code exists; rebuilt to the miniboss standard: about 90 s, two
  phases, a heart and an item drop).
- Same targets as unit 4 for the Foreman (Kiln Works); for the Warden: 75-105 s for the average bot, survives 97%.
- If time is left: **G5 The Choir of Echoes**, so every region has one new boss. If not, it is next session's
  first unit.

## Unit 6: numbers, docs and the hand-back, about 30 min

The full A/B (start of session vs end, every profile including the expert), STATUS, DESIGN (the new bosses, the bot,
the curve standard), CONVENTIONS test counts, PORTED.md for new Core files, the plan's Outcome section, a snapshot, a
publish, and a short report to the owner with what to playtest first.

## Time budget

| Unit | Budget |
|---|---:|
| 0 Land the waiting work | 0:10 |
| 1 Menu and settings | 0:40 |
| 2 Baseline, curve on (in the background of unit 3) | 0:25 |
| 3 The expert bot (tournament) | 2:00 |
| 4 The boss kit and Old Mudmouth | 1:30 |
| 5 The Foreman and the Warden | 1:30 |
| 6 Numbers, docs, hand-back | 0:30 |
| **Total** | **6:45** |

If a unit runs long, unit 5 shrinks first (the Warden before the Foreman), then the Choir is dropped. The bot unit
is not cut: the bosses' targets are measured with it.

## Not in this session

The other 15 region bosses and 2 minibosses; items 9-24; the Gatekeeper follow-ups in STATUS (it retires to a
fallback); the run length (Decision 2, revisited later); tuning the whole game's difficulty to the expert bot (this
session builds the bot and measures; the retune comes after the owner has played the new bosses).

## Owner answers (2026-10-22)

1. The King with the starting kit took the owner **under 2 minutes**: the expert's bar is 75%+ wins, median won
   fight at most 120 s.
2. The expert bot is to be **stronger than the owner**, within human limits (reaction at least 0.15 s).
3. **Every subtext goes**, the controls line included.
4. Bosses this session: **the kit, Old Mudmouth, the Foreman and the Warden, Returned**; the Choir only if time is
   left.

## Outcome

(written at the end of the session)
