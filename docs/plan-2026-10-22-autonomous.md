# Autonomous session plan (6 h), 2026-10-22

Approved by the owner 2026-10-22 (answers in "Owner answers" below), and for the working session to follow after a compaction. Branch
`opencode/feel-pools-0c15511b`, worktree `0c15511b`. Base: `13902a0` (pushed). `docs/STATUS.md` and `docs/DESIGN.md`
stay the source of truth; this file is the order of work and its bars.

## Rules for the session

- Commit and push after each unit, only on a green `.\verify.ps1 -Unity`; `tools/unity.sh publish` after each;
  a snapshot (`tools/snapshot.sh`) after units 2 and 4.
- Every behaviour change ships its tests; every new test is checked by removing its fix (a "mutant") and seeing
  it fail. NUnit trap found 2026-10-21: `Is.LessThan` passes on NaN, so assert not-NaN first.
- Bot numbers: seeds 101-200 x 3 profiles, 60 min; A/B on the same build with the unit's `--untune` switch off.
  Each unit's target is written below BEFORE building and not moved. A missed target is reported, not hidden.
- One cold critic pass per unit; tournaments (units 3 and 4) run builders in isolated worktrees and a blind critic.
- Stop and ask the owner (they may be away: then take the default named here and say so in STATUS) only on the
  open questions at the end.
- STATUS updated at the end of every unit.

## Unit 1: spike rooms (floors 5-12), about 1 h

Owner choices (2026-10-22): Elite and Crowded; a guaranteed drop; marked on the map.

- **Which rooms.** Rolled per floor when it is built, on its own stream from the seed (no other roll moves):
  normal rooms on floors 5-12, chance 0.20 at floor 5 rising evenly to 0.33 at 12, at most 2 a floor, never a room
  next to the start. `TuneSpike` switch; `--untune spike` for the bot.
- **Elite** (half of spikes): one body of the wave, the strongest kind present (never an egg, never a Brunch): 2.5x
  health, x1.2 pace (under the 1.34 cap), drawn with a gold outline and a slow aura. Its kill drops armour.
- **Crowded** (the other half): the wave plans 50% more bodies and one extra Brunch pack.
- **Reward.** Clearing a spike room always drops (ignores the 75% loot rule).
- **Map.** A small skull on the room on the ledger map once a neighbouring room has been visited (its door seen).
- **Tests.** Placement (the floor range, the cap, never next to the start, deterministic per seed, no other roll
  moves), elite stats and drop, crowded counts, the guaranteed drop, the map mark (HudTests), a `spike` screenshot.
- **Target (frozen).** Against the same build with `--untune spike`: average and skilled deaths on floors 5-12 rise
  from ~3 to 10-25 of 200; novice total deaths at most 65; Shallows and the Deep damage a floor within 5%; minutes a
  floor on 5-12 grow under 12%.

## Unit 2: the run's end (14 floors, a win), about 1 h

- **Structure.** Shallows 1-4, Kiln Works 5-8, Flooded Galleries 9-12, Warden's Deep 13-14. Floor 13 is a normal
  floor with its floor boss. Floor 14 is short: an antechamber room off the start, then the final boss's arena.
  Until unit 4 lands, a stand-in final boss (the Warden at final-floor strength) holds floor 14, so the run can end.
- **Win.** The final boss's death calls `EndRun(true)` (nothing calls it today). The account sheet shows the win,
  the time and the floor stats; `RunRecords` keeps wins and the best winning time (RECORDS / NEW).
- **The ledger.** The descent ladder shows 13 and 14 instead of "13 and down".
- **Bot.** A run can end `win`; the report adds wins, the minutes to a win, and deaths by floor up to 14. The Deep
  rows and the "timeout" reading change meaning: update `Report.cs` and its tests.
- **Tests.** Floor 14's layout (one antechamber, one arena, no other rooms, no stairs down), the win path (core and
  summary sheet), records, the bot's win end, the ladder (HudTests), a `won` screenshot.
- **Target.** A run is now 14 floors: average bot median run length 45-60 min (measured, not tuned here).

## Unit 3: the floor boss, a good placeholder fight (floors 1-13), about 1 h, one builder and a blind critic

The Warden today is every floor's boss: volleys, sweeps, a statue wall, three phases, about 64 s for the bot.
- **Brief to the builder.** A floor boss that reads as its area's (one shared body plan, attacks weighted per
  area), 3 telegraphed attacks plus one per phase, every attack readable at first sight (the Brunch/Lunger line
  language, ground marks), no unavoidable damage, a fight of 45-75 s for the average bot on floors 1-4 growing to
  60-90 s on 9-13. Placeholder art in the existing sprite style.
- **Reference.** Best-in-class analog: Enter the Gungeon's first-floor bosses (Bullet King, Gatling Gull):
  one readable gimmick each, three patterns, a short fight.
- **Frozen rubric for the critic.** Every attack has a tell of at least 0.3 s; a skilled bot that reads tells takes
  under 4 HP a fight on floors 1-4; the novice bot loses no more than 25% of its boss fights on 1-8; the fight
  ends between the times above; the critic, shown both fights as screenshot strips and bot traces with labels A/B,
  (the new boss vs today's Warden) must pick the new one and say it would ship it.
- The winner replaces the Warden as the floor boss; the Warden's statue wall and phases are kept for unit 4 if
  the final boss wants them.

## Unit 4: the final boss, floor 14, about 1.5 h, tournament of 2

- **Brief.** A full placeholder final fight: its own arena, 3 phases with a change of state each (not only faster),
  at least 5 distinct telegraphed attacks across the fight, one arena-scale mechanic (a moving safe zone, a floor
  that changes, adds summoned from the run's area pools), a boss bar with its name, a death sequence that ends the
  run in a win. Two to three minutes for the average bot.
- **Reference.** Nuclear Throne's Throne and Enter the Gungeon's Lich (phased finales that change the room), held
  to this game's readability rules.
- **Frozen rubric.** Every attack telegraphed at least 0.35 s; average bot wins 40-70% of the runs that reach floor
  14; skilled 70-90%; no single attack over 25% of the fight's damage; the critic picks one variant blind from
  screenshot strips per phase plus bot traces, and says it would ship it as the finale.
- Lab (F2) specimens for each phase; `final1/2/3` screenshot views.

## Unit 5: the item overhaul, design and the first 8 items, about 1.5 h

Items today: 13, mostly stat sticks; the active heals about 0.2 times a run. An overhaul is judgment-heavy and the
owner owns design direction, so the design comes first and the owner edits it after; the owner asked for the first 8 built too:
- a design doc (`docs/items.md`): 20-24 items in three tiers, every one changing how a fight is played (on-hit,
  on-kill, on-blink, on-room-clear, active effects with charges), synergies, a few cursed trade-offs, how drops and
  the item room offer them, and the bot work each needs; references: Isaac and Nuclear Throne mutations;
- the hook points in Core each item needs (`Items.cs` hooks), costed;
- built: the first 8 (the ones that change play most, over all three tiers, at least 3 actives), each with
  tests, art in the item icon style, the bot using actives sensibly, and a bot A/B (target: actives used and
  doing something in at least half the runs that hold one; no item alone moves deaths by more than 15%).

## Time budget

| Unit | Planned | Running total |
|---|---:|---:|
| 1 spike rooms | 1:00 | 1:00 |
| 2 the run's end | 1:00 | 2:00 |
| 3 floor boss (one builder, blind critic) | 1:00 | 3:00 |
| 4 final boss (tournament of 2) | 1:30 | 4:30 |
| 5 items: design and the first 8 | 1:30 | 6:00 |

If a unit runs over by half its time, it is cut to its minimum (unit 4: one builder; unit 5: fewer items, never
fewer than 4, the rest stays designed) rather than squeezing the next.

## Owner answers (2026-10-22)

1. Floor boss on 1-13: a new floor boss replaces the Warden (whose wall and phases stay available to unit 4).
2. Final boss identity: the tournament proposes.
3. Items: design and build the first 8 (the tournaments shrink to pay for it).
4. Floor 14: antechamber plus arena.
