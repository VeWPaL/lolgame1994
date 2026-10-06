# 06-stats.js — moved comments

Long comments moved out of `src/06-stats.js`. The code keeps a one-line gist tagged `[h:06-stats-N]`; search this file for that tag.

## [h:06-stats-1]
near: `const Stats=(function(){`

06-stats  -  the character sheet, and the rule that keeps it honest

  Six numbers, shown on the pause sheet, that items change and the game reads.

  THE ONE RULE: a stat is DERIVED, never assigned. There is no code anywhere that does
  player.speed *= 1.1. An item calls Stats.flat('strength', 2), and the value is recomputed from the
  base and the whole modifier set. This is not tidiness - it is the difference between an item
  system and a spreadsheet that happens to be in the game. After twenty additive items, an
  in-place model has multiplied in an order nobody can reproduce: removing one does not remove its
  effect, a build cannot be explained to the player or saved to disk, and "what does this item do"
  becomes unanswerable. Rebuilding from base every time costs nothing and makes the whole system
  explainable for free.

      value = min(cap, base + sum(flat) + earned)

  ADDITIVE, AND THAT IS THE WHOLE RULE. The first version had the usual two-part form,
  (base + flat) * product(1 + mult), and it had a silent degenerate case: a stat whose base is 0
  multiplied by anything is still 0. Speed's base IS 0 - the real base is PLAYER_MOVE and it lives
  on the player - so Stats.mult('speed', 0.06) produced exactly 0, a Speed item did nothing at all,
  and the only symptom was a bar that stayed empty. Twenty percent faster, invisibly slower.

  A rule with a silent failure mode is worse than a rule that is merely limited, and the limit here
  costs nothing real: a genuinely multiplicative effect - twenty percent faster casting, say - is a
  RATE, not a character attribute, and rates belong in named hooks. The stat model stays additive so
  that every number on the sheet means what it looks like it means.

  The clamp is applied to the DERIVED value, never to the inputs. That distinction matters: capping
  each modifier as it arrives means five Speed items quietly become worth less each and the player
  cannot reason about any of them. Capping the total means the budget is one number, SPEED_CAP,
  visible, tunable, and impossible to exceed by stacking.

  Each stat declares what KIND of number it is, because that is what stops it being used wrongly:

    add   a quantity you add up. Strength, health, arcana keys, and the speed BONUS - which is a
          fraction of a number that lives on the player, and so is itself additive.
    meter 0..1, earned by play rather than given by an item. Momentum.
    roll  a weight on a distribution. Luck.
    key   a threshold that opens content rather than a quantity. Intelligence.

  Momentum and Speed are ONE system, deliberately. Speed is what items give you; Momentum is what
  playing well gives you; they share one capped budget and one payoff. See 00-balance for why
  Momentum's weight sits on acceleration rather than top speed.

## [h:06-stats-2]
near: `strength:{label:'STRENGTH',kind:'add',base:0,cap:Infinity,`

flat damage, added BEFORE falloff. Flat and not multiplicative on purpose: falloff is what
      gives each of the four guns its shape, and a multiplier applied to it collapses all four
      into one gun with a different colour. Bolt's whole identity is one committed hit that hurts
      more than anything else; that only survives while damage is a thing you add.

## [h:06-stats-3]
near: `speed:{label:'SPEED',kind:'add',base:0,cap:SPEED_CAP,asPct:true,`

the top-speed half of the movement system, and a FRACTION rather than a rate: it is a bonus
      added to a base that lives on the player, which is exactly why it is `add` and not a
      multiplier. Capped at SPEED_CAP, the most items may give, so a Speed item always has a
      readable value; the SUM of items and Momentum is separately clamped at MOVE_SPEED_HARD_CAP.

## [h:06-stats-4]
near: `intelligence:{label:'INTELLIGENCE',kind:'key',base:0,cap:Infinity,`

arcana keys, not a damage stat. Once INT reaches a threshold the door that wants it opens, and
      STAYS open - which is a content gate rather than a multiplier, and means the stat can open a
      region of the game instead of nudging a number. The latching matters: a door that relocks
      because you later drank something that lowered your arcana would strand a run with no way
      back, and a player cannot plan around a stat that can go down.

## [h:06-stats-5]
near: `precision:{label:'PRECISION',kind:'add',base:0,cap:Infinity,`

PRECISION: how narrowly a shot leaves the wand. It was on LUCK, which was wrong twice over.

      Luck is a weight on a distribution - it decides what the dungeon contains and nothing else.
      Bolting "and your aim" onto it made one number mean two unrelated things, so a player could
      not reason about either: a Lucky Coin quietly became a damage item, and the stat that was
      meant to be about the run started being about the wand. Split, each can be tuned on its own
      curve, because they no longer have to move together.

      The lever lives in its own constants in 00-balance rather than inline in the firing code,
      because it is a LEVER and there will be more than one thing on it: the beam uses it now, and
      anything else wanting a stat-driven cone should read preciseSpread() rather than open-code
      its own, so there is exactly one place where "what does precision buy" is decided.

## [h:06-stats-6]
near: `const CLASSES={`

CHARACTER CLASSES, and the reason the starting numbers are not a DEFS field any more.

    A stat's BASE used to be a constant in DEFS, which quietly assumed there is exactly one
    character and always will be. The moment there is a second one, "what does a Warden start
    with" has no home: it either goes in DEFS, where two characters cannot both be written down, or
    it gets written inline at the call site, where two places will disagree about it.

    So the base is DATA on a class, and a class is a row. That is the whole shape, and it is built
    now rather than when the menu is, because retrofitting a second source of base values into a
    live stat pipeline is exactly the sort of change that quietly makes old runs unreadable.

    Anticipating the menu, and the three things it will need that are worth deciding NOW:

      - a class is identified by a stable string id, not by its position in a list, because a menu
        will sort them for readability and an index would renumber under it. Same rule as the
        weapon ids, which are derived from the display name for the same reason.
      - selecting a class is `applyClass`, and it REPLACES every base rather than adding to it.
        Additive would mean a character who picked the same class twice got a double-dipped start,
        and the fix for that is the kind of thing nobody finds until a save file does it for them.
      - a class carries a `blurb` and a `glyph` from the start, because both are menu furniture and
        inventing them later means going back to every class to fill them in.

    The sheet already renders a stat's breakdown, so a class change needs no new plumbing there at
    all - the base simply starts non-zero and the existing `fromItems` badge stays correct, because
    a class is not an item and must never claim to be one.

## [h:06-stats-7]
near: `wyrd:{name:'Wyrd', blurb:'Nobody in particular, which is the point.',`

THE STARTER. It is the plain human: competent at everything, expert in nothing. The numbers
      were chosen so the character sheet is a real sheet rather than a row of zeroes, because a
      stat that reads 0 tells the player nothing about what it does or what it is for.

        STRENGTH     3   flat damage on every hit, before falloff
        SPEED        25% a real baseline to build on; the meter is worth more ON TOP of this, which
                      is the only way the meter is a thing you watch
        INTELLIGENCE 1   one arcana key from the first room. It is a hidden system - the doors are
                      the only place it is ever visible - so a starting player should be able to
                      open exactly one thing and find out what it is for
        LUCK         0   nothing. It is a weight on a distribution, and a character that started
                      lucky would be a different character, not a better one
        VIGOR        8   THE HEALTH POOL, not a bonus on top of one. See applyVitals
        PRECISION    0   the Arcane Beam is unusable without it and a starter does not have it

## [h:06-stats-8]
near: `function applyClass(id){`

It does NOT call applyVitals, and that omission is load-bearing rather than an oversight.

    This file is loaded before the player exists, and `player` is declared with `let` in a LATER
    module - so the first time this runs, at load, touching it is a temporal dead zone ReferenceError
    rather than a readable `undefined`, and the `if(!player)` guard inside applyVitals cannot help
    because the throw happens on the way to evaluating it. It took out the rest of the module on the
    first attempt, and it was found by a console error rather than by a failing test, which is the
    worst way to find anything.

    So the rule from here on: this file decides NUMBERS, and the run decides BODIES. Whoever changes
    the character calls applyVitals afterwards, and startGame already does.

## [h:06-stats-9]
near: `const c=CLASSES[classId]||CLASSES.wyrd;`

The class RE-APPLIES, because a new run is played by the same character. Resetting only the
build would leave the previous character's bases in place, so a run started after a
hypothetical second class would silently inherit the first one's stats - and the sheet would
show them, so it would look correct.

## [h:06-stats-10]
near: `function check(k){`

every entry point validates, and breakdown() is no exception. It was the one that did not, and
    it returned a bag of NaNs for a misspelled stat instead of saying so - which is the exact failure
    the loud throws exist to prevent, hiding in the one function that had been written to be read
    rather than called.

## [h:06-stats-11]
near: `function breakdown(k){`

what the sheet shows underneath a number: how much of it came from the build. Momentum reports
    fromItems false because its whole point is that no item contributed to it.

    A named function rather than a method on the object below, because sheet() needs to call it and
    a sibling method is not in scope inside the object literal that declares it.

## [h:06-stats-12]
near: `let momentumLocked=false, momentumHeld=null;`

Momentum, as a controller rather than a bare field, because the suite needs to be able to say
  which motion model it is measuring.

  107 of the checks walk a player around with live enemies, and Momentum charges on exactly that.
  Momentum raises ACCELERATION, and the gunner solves a CONSTANT-VELOCITY intercept - so a player
  who commits faster really is harder to hit, which is the mechanic working. It also means those
  107 numbers, taken before Momentum existed, are no longer what those tests say they are. The
  first honest move is not to loosen the numbers; it is to make the baseline explicit.

  So the suite LOCKS the meter the same way it seeds the RNG, and Momentum has its own tests that
  unlock it. A lock a test must opt out of beats a flag every test must remember to set, because
  the default is the thing that should happen.

## [h:06-stats-13]
near: `hold(v){ momentumHeld=(v==null?null:Math.max(0,Math.min(1,v))); if(momentumHeld!==null) Momentum.set`

hold the meter at a fixed value, immune even to the hit penalty. This is the measuring
    instrument, and it exists because the alternative is a comparison that means nothing: the same
    run at "momentum 0" and at "momentum 1" has to differ ONLY in acceleration, or the two numbers
    are not about the same thing. A held meter makes the A/B exact.

## [h:06-stats-14]
near: `level(){ return momentumHeld!==null? momentumHeld : Stats.value('momentum'); },`

THE value the game reads, and the only place a held meter is honoured.

    A hold has to be authoritative rather than a one-shot assignment, or it silently reverts the
    first time anything resets the player - which is exactly what happened when this was measured
    the first time: the hold was set, startGame() rebuilt the player, and the measurement reported
    an identical number for momentum 0 and momentum 1 without anybody noticing that both columns
    were the same column. A measurement instrument that can be invalidated by ordinary game flow is
    not an instrument.

## [h:06-stats-15]
near: `function moveSpeedBonus(){`

The one number the movement code reads, and the only place two different sources are combined.

  A Speed item and a full Momentum meter are bought in completely different currencies - one by
  picking something up, one by playing well - and they both spend the same budget. Clamping each
  separately would let a player with both exceed the speed the entire fight tuning was measured
  against, so the sum is clamped here instead. Every stat on the sheet is derived; this is the one
  derived VALUE rather than a modifier, because it is two stats added together and the addition
  needs its own ceiling.

## [h:06-stats-16]
near: `const loadout={items:[]};`

The build. Items write here and the sheet reads here, so there is exactly one place that answers
  "what is this player carrying". Empty until the first item exists - and the sheet's empty state is
  designed copy rather than a gap, because "nothing carried" is a perfectly normal thing to be
  looking at on the first tick of a run and it should not read as a feature that has not arrived.

## [h:06-stats-17]
near: `function applyVitals(){`

VIGOR IS MAXIMUM HEALTH, and this is where that stops being a lie.

  It was a stat with a name, a bar, a row on the character sheet and a blurb - and nothing read it.
  player.maxHp was the literal 8, written into the player object at spawn, so Iron Ribs put +2 Vigor
  on the sheet and changed nothing at all. That is the exact failure this file was built to make
  impossible, and it survived because the sheet renders from Stats while the game reads a constant,
  and nobody checked that those were the same number.

  Derived here rather than in the items module, because it is a stat about the player rather than
  about the build - but it is derived for the same reason and in the same place, so there is one
  answer to "what is this character's health" and it is not written down twice.

  Losing the current health when the ceiling drops is deliberate rather than convenient: an item
  that takes Vigor away should cost you hearts now, because a build that can be made worse by
  picking something up has to be able to do that visibly.

  VIGOR IS THE POOL, not a bonus on a constant beside it. It used to be `BASE_HP + vigor` with
  BASE_HP a literal 8, which is a second place that knows what the starting health is - and the
  starting health is now a CHARACTER's number. Two places that know it is the same bug as the two
  HUD layouts, the two RNG streams and the two copies of the margin, and every one of those cost
  something. So there is one number and it is vigor. A character with 8 vigor has 8 health, full
  stop, and "+2 Vigor" means two more hearts rather than two more hearts on top of eight.
