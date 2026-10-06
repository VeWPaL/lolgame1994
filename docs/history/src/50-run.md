# 50-run.js — moved comments

Long comments moved out of `src/50-run.js`. The code keeps a one-line gist tagged `[h:50-run-N]`; search this file for that tag.

## [h:50-run-1]
near: `function getBlinkDir(){`

50-run  -  blink, run flow, records, pause

  The only thing here that is not pure simulation is the records pair, which touches localStorage.
  In C# that becomes an interface with a PlayerPrefs or file implementation; everything else moves
  across as-is.

## [h:50-run-2]
near: `const mom=Momentum.level();`

The trail is tagged, and the tag carries the meter as it was AT THE MOMENT OF THE BLINK.

    dashFX is shared - the lunge charge puff, the lunge trail and the Brunch absorb puff all live in
    it - so a tint applied to every particle in the array would paint enemy telegraphs green too,
    which is a lie about the enemy rather than a readout of the player. Tagging is what makes the
    ramp mean anything: green on a body you own, white on a body you are reading.

    Read once here rather than read live in the draw, because a trail that re-read the meter while it
    was still on screen would flicker up the whole ramp as the meter moved underneath it.

## [h:50-run-3]
near: `const fromX=player.x, fromY=player.y;`

THE TRAIL FOLLOWS WHERE THE PLAYER ACTUALLY ENDED UP, and it used to be six puffs laid along
    `BLINK_DIST` regardless of whether the blink went that far.

    `doBlink` moves the player by BLINK_DIST and then calls `clampPlayer()`, so a blink into a wall -
    or into a corner, or short of a door - travels much less than the full 140px. The trail was laid
    out BEFORE the move and on the assumption it completed, so every one of those puffs was placed
    at the position the player would have reached had the wall not been there. The result was a VFX
    drawn through the wall and out of the play area, which is what was reported.

    It is also the wrong read on the move itself: a blink that covers 40px should not draw the same
    140px of smoke as one that covers 140px, because the length of the trail is the only thing
    telling the player how far they went. So the trail is laid out after the move, over the distance
    actually covered.

    Two things it still has to do:

      - stay dense. Spacing the puffs over the travelled distance alone would make a short blink
        drop all six puffs in a clump and a long one spread them thin, which is backwards - so the
        spacing is capped, and a short blink gets a tight cluster rather than six overlapping copies
        of the same sprite.
      - never reach past the landing point. The interpolation runs to t=1 inclusive, so the last
        puff sits exactly where the player arrived.

## [h:50-run-4]
near: `const endX=Math.max(ROOM_LEFT,Math.min(ROOM_RIGHT,player.x));`

the puffs are laid after the clamp, over the distance that was actually covered. Computed from
    the clamped position rather than from `ux*BLINK_DIST`, so a blink into a wall draws a short
    trail and a blink stopped by a door gap draws what it covered.

    BUT NOT FROM THE CLAMPED POSITION DIRECTLY, and the first version of this did exactly that and
    was still wrong. `clampPlayer` ends with a deliberately loose outer bound - `Math.max(ROOM_LEFT-
    40, Math.min(ROOM_RIGHT+40, ...))` - so a player can sit up to 40px outside the wall line in a
    doorway, and a puff placed at that clamped position is 40px outside the play area. Measured:
    a blink into the left or bottom wall left two puffs 40px outside the room, while the right and
    top walls were clean because that fixture started from a different distance out.

    So the trail is clamped to the room box, not to the player's own (looser) bounds. The player is
    allowed the doorway overshoot for the sake of transitions; the VFX is not, because a puff drawn
    outside the room is drawn over the wall.

## [h:50-run-5]
near: `Sfx.blink();`

THE POSITION THE ENEMIES AIM AT STAYS PUT, and it stays at the PRE-MOVE position - which is now
    `fromX`/`fromY` rather than `player.x`, because the movement happens above so the trail can be
    laid over the distance actually covered. Setting it to the landed position instead looks harmless
    and is not: it makes the hitbox start easing out of where the player ended up, so a blink buys no
    reaction window at all, and a gunner that was already tracking you gets a free intercept shot the
    instant you vanish. That is the whole point of the lag, and there is a test pinning it.
    The trail block above is the only thing that needed the move to happen first.

## [h:50-run-6]
near: `player.iframes=Math.max(player.iframes,BLINK_IFRAMES+DASH_TRAIL);`

Invulnerable for the whole travel, not just the first frame of it. BLINK_IFRAMES used to be a
flat 0.17s that began on the same tick as the jump, so a blink into a closing Brunch or past a
shell only protected the moment of crossing the line - the half of the blink that actually
happens afterwards was fair game, and the move reads as "get me out of here" precisely when
there is something to get out of. The blink is a two-charge escape with an 8s recharge; if the
window is shorter than the animation, the cooldown is the real punishment and the escape is not.
The starting frames are set after the move so they cover the travel rather than the step into it.
The blink's invulnerability covers the move and then the beat after it, which is what the trail
draws. Not the distance at walking speed: the blink is a teleport, it does not take 0.6s to
cross 140px, and sizing the window by the distance made a two-charge escape on an 8s recharge
worth 0.77s of immunity - long enough to walk through a room unharmed twice over.

## [h:50-run-7]
near: `player.blinkGrace=BLINK_GRACE;`

The grace starts here and is spent once, whatever the blink is used for. Resetting graceSpent
per blink rather than per room is what keeps it to one forgiven hit: blink into a pack and the
second body still connects, which is the whole reason this is forgiveness and not a longer
invulnerability window. See damagePlayer.

## [h:50-run-8]
near: `player.boost=BLINK_BOOST;`

Momentum. Landing on your feet and then walking out of the blink at the same speed you arrived
at is what makes it feel like a teleport rather than an escape - you spend the dodge and get
nothing for it, so the correct play is to bank the charges for emergencies instead of using one
to gain ground. A short burst ALONG THE DIRECTION YOU BLINKED gives the move a purpose beyond
survival: you can blink out of a shell and keep the ground you bought, and turning around on
landing throws the whole of it away. It decays fast on purpose - long enough to matter for the
next beat, not long enough to become the player's real top speed.

## [h:50-run-9]
near: `if(typeof Items!=='undefined') Items.reset(); else Stats.reset();`

A new run starts from nothing, and that means the BUILD and not merely the numbers. This used to
    call Stats.reset() alone, which zeroed every stat while leaving loadout.items populated - so after
    pressing R the character sheet listed the previous run's items with none of their effects
    applied. The same lie as an inert stat, in the other order: the sheet and the game disagreed, and
    the sheet was the one that had been right a moment earlier.

## [h:50-run-10]
near: `Rnd.set(root===undefined?Rnd.fresh():root);`

REPRODUCIBLE, WHICH TAKES AN ARGUMENT. Pressing R used to call this with nothing, and the
    generator simply carried on from wherever the previous run stopped. The seed on screen never
    changed - it still read back the same seven characters - so restarting produced a different
    dungeon under an identical code, and a player trying to improve on a run they had just died in
    was not repeating it at all.

    A seed is only worth having if it means something, and this is the line that gives it one: the
    generator is rewound to `root` before a single room is generated, so the same code produces the
    same dungeon every time, on this machine and on anyone else's.

    With no argument it takes a fresh seed, which is what starting from the title screen wants and
    what the seed sheet wants after it has decoded the code the player typed.

## [h:50-run-11]
near: `clearArtCaches();`

THE ART CACHES ARE RUN-SCOPED, and this is the line that makes them so.

    `caveCache`, `wallCache` and `floorCache` hold canvases that were baked through draws from
    `Rnd.art()` - the stream 05-rng derives from the seed the player typed. So what is inside those
    canvases is a function of THE SEED and nothing else, and a cache that outlives the run that
    filled it is carrying seed A's speckle into seed B: start a second run and the floor you are
    standing on is flecked with the previous run's flecks.

    It is invisible on any single screenshot - the texture is stone-coloured either way - which is
    exactly why it needs to be a call rather than a comment asking people to remember. And it has
    to happen HERE, immediately after the seed is set and before anything draws, because the next
    line generates a dungeon whose rooms will pull from these caches.

    Only the three tile caches. `spriteCache`, `glowCache`, `woodCache`, `paperCache`, `iconCache`
    and `glyphCache` are keyed by shape or colour, are the same pixels whatever the seed is, and
    clearing them would spend a re-bake per body per frame for nothing. The distinction is not which
    cache is convenient to clear but which one holds run-dependent pixels.

## [h:50-run-12]
near: `resetRunCursors();`

The flank walk and the pack-id counter live at module scope in 20-world.js, so they are part of
    what "start from nothing" has to mean. Neither was reset, so a second run from the same seed began
    with the golden-angle walk already part-way round: identical dungeon, identical bodies, every lunger
    circling from a slightly different angle. The first fight of a session played differently from the
    second, which is the one thing a seed cannot be asked to absorb.

## [h:50-run-13]
near: `run={floor:1,floorTicks:0,roomsThisFloor:0,rootSeed:Rnd.seed,ticks:0,kills:0,dmgTaken:0,shots:0,hits`

`unlocked` is created WITH the run, which it was not until the lab handed this over by trying to
    equip every item in the game at once and throwing on the first artifact.

    The bucket is a property of the run - "what has this run unlocked" - so it belongs in the run's
    own literal. It used to be created by Items.reset(), and that worked by accident and only in
    one direction: startGame calls Items.reset() on line 78, which is BEFORE this literal, so
    reset() was writing the bucket onto the PREVIOUS run and this one was born without it. Nothing
    noticed because the one test that reads the bucket calls Items.reset() itself immediately
    beforehand, which recreates the precondition it is supposed to be checking. A test that
    establishes the thing it is about to assert is not a test of that thing.

    The visible symptom in a real run is a crash the moment an artifact is picked up, which is a
    legendary - the rarest thing in the game, and the one thing most likely to be deliberately
    farmed for.

## [h:50-run-14]
near: `const RECORDS_KEY='depths_records';`

THE RECORDS LIVE IN ONE KEY, and the four flat ones are the previous format.

  They are still read, so a player carrying records from an earlier build keeps them, and they are
  removed on the first successful save so the format retires itself without a migration step.

## [h:50-run-15]
near: `let single=null;`

THE SINGLE-KEY RECORD IS THE CURRENT FORMAT, and the flat keys are the previous one.

    Read order matters and it is new-first: if a build ships with a half-finished migration, or a
    player's browser has both forms from different sessions, the newest complete record is the one to
    believe. Reading the legacy keys first would let a stale `depths_deepest` overwrite a good
    single-key record, which is the exact failure the single-key write was introduced to prevent.

    A malformed JSON blob is treated as absent rather than thrown, because a corrupt value must not
    stop the game from starting - it just means this player begins again from zero, which is the same
    outcome as having no records and is recoverable by playing one run.

## [h:50-run-16]
near: `try{`

ONE KEY, WRITTEN ONCE. The five separate `setItem` calls could leave the store half-written, and
    the half that matters is the dangerous one.

    Measured by making the third `setItem` throw, which is what a quota error does:

        depths_best      written
        depths_fastest   written
        depths_wins      stale
        depths_deepest   stale
        depths_tickhz    stale

    A partial write is not a smaller version of the record, it is a FALSE one. `depths_deepest` is the
    number the player is told they have reached; if it silently keeps an old value because the write
    before it threw, the summary reports a personal best that is not one, and nothing anywhere says so.
    The old value also survives a reload, so it is not a display glitch for one screen - it is a lie
    that persists.

    So the records go in as a single JSON value under one key. One `setItem` is atomic per key: it
    either lands or it does not, and there is no in-between where half the record is new and half is
    from three runs ago. That is the whole fix.

    The old flat keys are still READ on load, so a player who has records from a previous build does
    not lose them to a format change, and they are then REMOVED on the first successful save - which
    retires them without a separate migration step. Removal is per-key and individually guarded: a
    browser that refuses `removeItem` would otherwise throw out of the try block and be reported as a
    failed save when the record itself landed perfectly.

    The catch is still silent in the sense that it does not throw, because a failed save must not take
    the run down with it. What it does now is say so, once, on the console: a swallowed exception here
    means a lost run record, and the cost of finding that out later is far higher than the cost of one
    line in a log. `RECORD_SAVE_WARNED` keeps it to one line rather than one per frame.

## [h:50-run-17]
near: `localStorage.setItem(TICK_KEY,String(TICK_HZ));`

retire the flat keys on the first successful save, and keep writing TICK_KEY separately because
      it is not part of the record - it is the schema version `loadRecords` needs in order to convert
      an old fastest-time. Losing it is survivable; losing a record is not, which is why the order
      is record first and version second.

## [h:50-run-18]
near: `function descend(){`

DESCEND. The boss dies, the way out appears, and walking into it takes you DOWN rather than out.

  The whole point of a floor is that it is not the end. A run now ends in exactly one way, which is
  dying, and everything the player has - the weapon, the items, every stat, the health in the bank -
  is carried down. Nothing about the build is rebuilt. That is the deal the moment a second floor
  exists, and it is the deal that makes depth the only thing that matters.

  What is deliberately NOT carried: the map, the rooms, the per-floor counters, and the position.
  A new floor is a new dungeon from the same preset with a DIFFERENT seed, so the shape is familiar
  and the contents are not. Reusing the seed would make floor 2 a replay of floor 1 with a bigger
  number on the enemies, which is the cheapest possible version of this and the one that would make
  a hundred floors feel like one floor played badly.

  Per-floor counters reset because "how far did you get" is a fact about a floor, not a sum of
  floors. run.ticks deliberately does NOT reset: that is the run, and a player who dies on floor 12
  wants the total time they survived as much as they want the floor they reached.

## [h:50-run-19]
near: `Rnd.set(Rnd.floorSeed(run.rootSeed,run.floor));`

A new seed per floor, derived from the ROOT the player typed rather than from the previous
floor's seed, so every floor is a pure function of (root, floor) and the whole run replays from
the one seed on the luggage tag. Floor 1 keeps the root exactly, because the seed the player
typed should be the dungeon they actually get. See Rnd.floorSeed for why chaining would be wrong.

## [h:50-run-20]
near: `clearArtCaches();`

THE ART CACHES ARE CLEARED HERE TOO, and this is the second place that has to happen.

    `startGame` clears them because a new run is a new seed. But `descend()` is ALSO a new seed - it
    sets the floor seed from (root, floor) on the line above - and the three caches are keyed by
    (type, size, area) with no seed in the key, because there was nowhere to put one that would be
    correct for a cache whose contents are baked from `Rnd.art()`.

    So on floors 1-4 the key is byte-identical across descents and every one of them is handed floor
    1's baked cave tile, wall tile and floor canvas. Measured, seed 31337, hashing the floor canvas:
    floors 1, 2, 3 and 4 all read 3905066258 and the cache holds exactly one key throughout; floor 5
    rebakes because the area changes and reads 1783679762. The same two seeds on a cleared cache give
    different tiles (1698881801 against 4006777781), so the content genuinely is a function of the
    seed and the art stream is not the thing that is broken.

    Which means two players typing the same seed see a different-looking dungeon, and - the part that
    actually matters - the art is not a pure function of (root, floor), which is the property the
    whole seeding design rests on. It is invisible in a screenshot: stone is stone either way, and
    the first floor of every area is correct, so the error repeats on a period of four.

    It is called before `generateDungeon()` rather than after, because the generator is about to
    build rooms whose first draw will pull from these caches.

## [h:50-run-21]
near: `roomFade=1; fadeTicks=FADE_DESCEND; fadeT=fadeTicks;`

The fade is longer between floors than between rooms, and deliberately so: it is the one beat in
the game where nothing is trying to kill you, and it is where a player looks at the sheet.
FADE_DESCEND is the single source of its length - descend() uses it for the fade, drawDescent()
divides by it to time the banner, and update() counts it down. Three numbers for one duration is
how a banner ends up outliving the fade it was drawn on.

## [h:50-run-22]
near: `function endRun(won){`

A run now ends in exactly one way, which is dying, so `won` is nearly dead weight - but the
  parameter stays because the game-over screen reads it and a run that used to be winnable should
  still be able to SAY so if anything ever routes back here. What is new is the depth: the floor
  reached is the headline number now, because it is the one the whole ladder is built around and the
  one a player is actually trying to improve.

  rooms explored is per-floor, not per-run. Summing rooms across every floor would produce a number
  that grows without meaning anything - floor 12 alone has more rooms than floor 1, so a player who
  died early would show a respectable total for having seen very little.

## [h:50-run-23]
near: `if(state==='dev') return false;`

THE LAB CANNOT END A RUN, and the guard is HERE rather than at the call sites.

    There are four `endRun(false)` calls in the tick and the lab's immortality was enforced in one
    place - damagePlayer, which floors health at 1 - because that is the only thing in the shipped
    game that REDUCES health. That is true today and it is a fragile thing to rely on: any future
    system that writes hp directly, or any of the four checks reached by a path that bypasses
    damage, would reach this function and fold a death into the records. A probe setting hp to 0
    directly already does exactly that, which is how the hole was found.

    The failure mode is the reason to care. Nothing throws, nothing is drawn, and the only symptom
    is a records file that claims the player died in a debug view. That is silent, it is
    permanent, and it is the one thing a debug view must not be able to do.

    So the invariant lives at the one function that would break it, where it cannot be bypassed by
    adding a fifth call site. It is one line, and it is the difference between the guarantee being
    true and the guarantee being currently-unviolated.

    It returns `false` rather than silently no-oping so a caller that cared could tell - none does
    yet, and the comment is here for whoever writes the fifth one.

## [h:50-run-24]
near: `seed:Rnd.encode(run.rootSeed),`

THE ROOT SEED, NOT Rnd.seedText. This read the live generator, which after a descent holds
      floorSeed(root, floor) rather than the root itself - so the summary showed a code that
      reproduces a DIFFERENT dungeon than the one just played. Typing it in gives someone else's
      floor 3, not your floor 1.

      It is the one number the seed is FOR: it is what a player copies to a friend so both of them
      play the same run. A summary that prints anything else is a broken promise with a
      seven-character receipt.

## [h:50-run-25]
near: `function setPaused(on){`

pausing only exists during a run; acc is dropped so resuming never replays the paused time.
Pausing OPENS THE CHARACTER SHEET rather than dimming the game and stopping: a player who pauses in
a fight is asking what their build is, and an overlay that only says PAUSED makes them resume, walk
out of the room, and pause again somewhere safer to find out.

## [h:50-run-26]
near: `player.blinkRegen+=(currentRoom().enemies.length===0?BLINK_FILL_CLEAR:1);`

A charge comes back in BLINK_RECHARGE seconds of wall clock, in a fight, and that is the number
    a designer tunes. Everything below is arranged so that stays true:

      in a fight        the fill rate is 1 tick per tick, and a tick IS 1/TICK_HZ of a second, so the
                       bar drains in exactly BLINK_RECHARGE ticks = BLINK_RECHARGE seconds. The
                       constant means its own name and needs no conversion anywhere.
      in a quiet room   the fill rate is BLINK_FILL_CLEAR ticks per tick, so the bar drains
                       BLINK_RECHARGE/BLINK_FILL_CLEAR = 82 ticks sooner, about 0.39s. And a room
                       that has just gone quiet ALSO jumps the bar to half first (see the clear in
                       update), which leaves 368 ticks to fill and measures 42 ticks, about 0.20s.
                       Both figures are real and they are different situations, so both are here:
                       a charge eaten mid-fight comes back in 0.39s, and one you earned by winning
                       the fight comes back in 0.20s. Measured, not derived - the first version of
                       this comment quoted 0.39s and was wrong for the common case, which is the
                       exact failure the paragraph at the foot of this function is about.
      tempo             TEMPO.rate is deliberately NOT applied here. It used to be, and it made this
                       constant lie: sec(6.5) became 6.5*1.5 = 9.75s of bar, which at the old rate of
                       1.5 ticks per tick took 6.5s to drain... and at any other TEMPO it took
                       something else again, so the one number in the file describing how long an
                       escape took was a function of an unrelated dial. See the note on the constant.

    The comments on this function used to state three numbers, and all three were wrong: it said the
    clear-room fill was 7 (it is 9), that a charge came back in 1.1s in a quiet room (0.39s), and
    that BLINK_RECHARGE was 8s (it was 6.5s). Nobody was misled only because the real values were
    readable two lines below, which is exactly the situation a comment about numbers is supposed to
    prevent. Every figure quoted in a comment here is now a figure the code actually uses.
