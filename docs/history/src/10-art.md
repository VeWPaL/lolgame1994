# 10-art.js — moved comments

Long comments moved out of `src/10-art.js`. The code keeps a one-line gist tagged `[h:10-art-N]`; search this file for that tag.

## [h:10-art-1]
near: `function hexRgb(h){`

10-art  -  every sprite, generated rather than loaded

  No asset files exist in this project. Each body is a pixel array baked to a canvas at load, which
  is why the whole game is 450KB and why there is no art pipeline to manage.

  In Unity the same arrays become a Texture2D built at runtime and handed to a SpriteRenderer, so
  this module ports nearly mechanically. That is the strongest argument for keeping the art
  procedural, and the reason a 15GB asset pipeline never has to exist.

## [h:10-art-2]
near: `function hexRgb(h){`

colour, as arithmetic rather than as a string each time ----------
  Three helpers and one reason: the area palette below is a table of hues, and the places that
  consume it need a WASH (the same hue at a fraction of its strength) and a BLEND (this hue mixed
  into that one). Both were being done by writing the resultant hex out by hand, which is how a
  palette stops being a palette and becomes sixteen unrelated literals that drift apart the first
  time one of them is nudged.

  Hex only, because everything in the existing vocabulary is written as hex. A three-digit form is
  expanded rather than rejected, so a hand-typed '#abc' is a legal argument and not a silent
  black.

  AND NOTHING ELSE IS ACCEPTED, which is the second half of the same rule and was the half that was
  missing. This used to hand the string to parseInt and check that a number came back, and
  parseInt is a much more forgiving thing than a colour:

      '#12'      -> 0x12       -> [0, 0, 18]      black with a whisper of blue
      '#12345'   -> 0x12345    -> [1, 35, 69]      a colour nobody chose
      '#xyz'     -> NaN        -> rejected
      'rebeccapurple' -> NaN   -> rejected

  So a malformed palette entry did not fail, it quietly resolved to the darkest colour available and
  drew an area in near-black - a whole floor rendered wrong, with the mistake sitting in a table
  entry rather than anywhere a stack trace could point. The check is now a SHAPE check, not a
  "did it parse" check: three or six hex digits and nothing else. That is stricter than parseInt and
  exactly as strict as the palette.

  Throwing rather than returning null is deliberate, and it is what this function already did for the
  cases it caught: `paletteForArea` throws on an unknown area and Content.get throws on an unknown id,
  so the vocabulary of this file is loud about its own mistakes. A null would propagate into
  mixHex, where it would become a NaN pixel rather than a message naming the entry that is wrong.

## [h:10-art-3]
near: `const AREA_PAL={`

AREA PALETTE, and the one function that reads it ----------
  `areaForFloor()` is core (00-balance) and decides WHICH area; this decides what that area looks
  like. The split is the point: identity is carried by the enemy mix and by the palette, and
  neither of them touches the depth ladder. A themed block is not a harder version of the climb,
  it is the same climb somewhere else.

  THE FIELDS, and each one earns its place by being read somewhere a player will look:

    stone      the wall. The single largest colour on screen after the floor, and the one the eye
               reads as "where am I" before it reads anything else.
    stoneLit   the lit edge of each course, so the wall is masonry and not a rectangle.
    mortar     the joint between courses. Without it a wall has no scale and reads as a border.
    floor      the base the cave speckle sits on. Close-up texture, not the room's colour.
    floorLight the pale speckle. One per area, because a fleck of the wrong hue is what makes a
               recoloured floor look like a filter over the old one.
    floorTint  the hue the room is WASHED in, over the room-type tint. See wash.
    wash       how strongly. 0 for Area1, deliberately: see THE ONE MEASURED CLAIM below.
    accent     UI ink struck into wood - the depth numeral, the descent banner, the area line on
               the character sheet.
    accentDim  the same ink unlit, for the numeral on floor 1 and the "+N" tally.
    mapWash    the near-black ink laid over the minimap's paper. Deliberately NOT `floorTint`: that
               is the hue of the stone under the player, and putting it on the board made the map
               three shades lighter in every channel, which eats the contrast an unvisited room
               needs to stay a light shell rather than a hole in the board.

  THE ONE MEASURED CLAIM: **Area1 is the build that existed before areas did.** Every number
  measured in this project's history - every TTK, every reaction window, every screenshot anyone
  looked at - was measured on floors 1-4 with this palette. So Area1's stone is the `#2b2f3a` the
  wall was already, its floor is the `#25211c` the cave was already, its accent is the `#e8b06a`
  the depth numeral was already, and its wash is ZERO, which is what makes the floor composite
  byte-identical rather than approximately right. A theming pass that quietly re-tinted the first
  four floors would invalidate all of it while looking like a feature.

## [h:10-art-4]
near: `Area2:{ id:'Area2',`

THE FOUR AREAS AS HUES, and the temperatures are the design rather than a by-product.

    TWO COOL AND TWO WARM, deliberately and 2:2 rather than a gradient from cold to hot. A palette
    that ramps monotonically - slate, then olive, then amber, then red - reads as a slider the
    player is being dragged along, and makes each floor look like a slightly worse version of the
    next. Alternating means every descent is a CHANGE, and the two warm areas then have to be told
    apart from each other rather than from the cold ones:

      Area1  blue-grey  b(58) > g(47) > r(43)     cold slate. The shipped look, untouched.
      Area3  green      g(56) > b(51) > r(31)     cold, and unmistakably not Area1: green, and
                                                       a channel Area1 never leads on.
      Area2  amber      r(61) > g(42) > b(32)     warm, and orange - r/g is 1.45, which is brick.
      Final  oxblood    r(51) > g(22) > b(29)     warm, and red - r/g is 2.32. It is the same
                                                       temperature as Area2 and half the green, so
                                                       the two warm areas are distinguishable on
                                                       a channel rather than on brightness.

    Every pair is 42 apart in summed RGB on the wall and 49 on the floor wash (measured, visual pass), and those
    are the measured minimums; the test floors sit below them. The wall is the largest
    thing on screen and the wall floor sits exactly at its minimum, so any nudge breaks the bar.

## [h:10-art-5]
near: `function paletteForArea(area){`

LOUD on a missing area, for the same reason Content.get is loud: an unknown id here would
  otherwise return undefined and the first thing anybody would see is a room painted in the colour
  of null. The message names the id and lists what exists, because the mistake is always a typo in
  a new area or a rename somebody did in one place.

## [h:10-art-6]
near: `function areaPalette(){ return paletteForArea(areaForFloor()); }`

The palette of the floor being played. Every drawing site reads this rather than reaching for the
  table, so there is exactly one place that turns "which floor am I on" into "what does it look
  like". Pure: it reads the floor and a table, spends no RNG, and changes nothing - which is what
  makes it safe to call from a draw path without disturbing a seeded run.

## [h:10-art-7]
near: `function areaFloorTint(type,area){`

THE COMPOSITED FLOOR TINT, and the single place the two tints are mixed.

  It is a function rather than a pre-mixed colour because the floor and the DOORWAY both need it and
  they are drawn in different places: one inside the baked floor sprite, one as an opaque rect over
  the wall gap. Two copies of this expression is the doorRect bug in a different costume - two
  rectangles that agreed perfectly right up until the first room that was not 700x450, except the
  thing that disagreed here would be a room that is not on floor 1.

  ROOM TYPE FIRST, WASH SECOND, because that is the order the two are composited in on the floor, and
  a mix that reversed its arguments would be a subtly different colour in the doorway than in the
  room it opens into - a difference no screenshot of a single room would show.

  The endpoints are exact and the suite pins both: mixHex(a,b,0)===a and mixHex(a,b,1)===b. A helper
  whose endpoints drift is not usable for the job it has here, which is keeping Area1 identical to
  the build every earlier measurement was taken against - "0.52 of the way" has to be 0.52 of the
  way, not near enough.

## [h:10-art-8]
near: `const caveCache=new Map();`

cave floor texture, ONE PER AREA ----------
  It was a single module-level canvas baked at load, so there was exactly one floor in the game and
  re-baking it per area is what gives an area a floor at all.

  Baked LAZILY rather than all four at load, for a reason that is about not disturbing things: the
  old bake ran during module load, immediately BEFORE the wood grain baked itself further down this
  file. Moving four tiles to load time would push four tiles' worth of art draws in front of the
  grain and change the wood in every HUD plate in the game. Lazy keeps the art stream in exactly
  the order the shipped build drew it - the grain is baked from the same first values it always
  was - and a floor is baked once per area per session either way.

  The counts (220 flecks, 36 soft blotches) are unchanged, so texture DENSITY is identical across
  areas and only the hue differs. A theming pass that also changed how busy the floor is would be
  two changes, and the second one would hide the first.

## [h:10-art-9]
near: `const WALL_TILE_W=64, WALL_TILE_H=32, WALL_COURSE=16;`

the wall, as coursed masonry ----------
  The wall used to be four `fillRect`s of one flat colour, which is the largest single-colour region
  on screen and reads as a frame around the game rather than as stone. Four area palettes over a
  flat rectangle would have made that four times as obvious, so the courses are here for the same
  reason the floor speckle is: a wall needs texture to have scale.

  TWO 16px COURSES PER TILE, and 16 is `wt`, the wall thickness drawRoom uses - so a tile is
  exactly as tall as one band of wall and the joints land on whole bands rather than being cut in
  half by the fill. The second course's joints are offset by half a block, which is the one piece
  of masonry knowledge that matters: a wall whose joints line up into continuous verticals reads
  as tile, and this project has already spent a day deleting a 120px floor lattice for exactly
  that reason.

  The tone variation is per-block and low, because the wall is behind the fight. It has to be
  legible as masonry at a glance and invisible as texture while you are aiming at something.

## [h:10-art-10]
near: `function clearTileCaches(){`

CLEARING THE TILES, because they are SEEDED PER RUN ----------
  These two caches, and the floor cache beside them in 70-view, hold canvases that were baked with
  draws from Rnd.art(). The art stream is derived from the seed the player typed (05-rng: OFF_ART), so
  what is in those canvases is a function of THE SEED and nothing else.

  Which means a cache that outlives the run that filled it is carrying seed A's speckle into seed B:
  start a second run on a new seed and the floor you are standing on is flecked with the previous
  run's flecks. It is invisible on any single screenshot - the texture is stone-coloured either way -
  and it is the reason this function exists rather than a comment asking people to remember.

  NOTHING CALLS THIS YET, and that is deliberate at this layer. 50-run owns the run lifecycle and is
  not mine to edit, so the hook into startGame is left for the agent that owns it: one call at the
  top of a new run, before anything draws. What this file owes is the DOOR and an honest statement of
  what walking through it does - a function nobody has wired up yet is a latent capability, and
  pretending otherwise by calling it from somewhere would be a run-lifecycle change wearing a hat.

  Tile caches only. `spriteCache`, `glowCache`, `woodCache`, `paperCache`, `iconCache` and
  `glyphCache` are keyed by shape or colour and are NOT seeded - they are the same pixels whatever
  the seed is - so clearing them would spend a re-bake per body per frame for nothing at all. The
  distinction is not which cache is convenient to clear but which one holds run-dependent pixels.

## [h:10-art-11]
near: `function roomTone(type,area){ return areaFloorTint(type,area); }`

THE COLOUR OF THE AIR IN A ROOM, in one place.

  Two things are layered on a floor: the room TYPE's tint (start / normal / item / boss, which
  says what the room is FOR) and the area's wash (which says where you are). Keeping them as two
  operations rather than one pre-mixed colour is what stops a themed area from erasing the room
  type - an item room has to go gold in every area, or the glow that tells you a room is an upgrade
  stops meaning anything.

  And the doorway has to show the same answer as the floor behind it, which is why this is one
  function rather than the flat type tint written out at the call site: the gap in the wall is
  filled opaquely, so it has to be the composited colour or every doorway reads as a differently
  coloured rectangle punched through a room.

  A one-line alias rather than a second name for the same thing at the call site, because the test
  reads `roomTone` and the drawing should not be able to answer a different function than the one
  that was asserted. Two names for one expression is fine; two EXPRESSIONS is what has bitten this
  project four times.

## [h:10-art-12]
near: `const BRUNCH_CHASE_SPEED=2.1,      // no target to protect: 1.31x a chased player, which is a chase`

one row per enemy type. HP carries the +12% that pays for the hit-slowdown every body now gets,
  LUNGER_PAY takes 4% back out of the movement, and the gunner is the shooter cloned: half the
  rate of fire, double the damage per shell, a bigger frame, slower and heavier shells. It also
  carries enough HP to survive a miss or two and sidesteps incoming shots, but only so often.
  Brunch is the anti-solo enemy: a pack stands between you and whatever is shooting at you, and
  every body that reaches you spends half of itself doing it. That is what stops a swarm from
  spiralling into an unwinnable death loop, and it is why they are worth shooting rather than
  simply outrunning - they are now barely quicker than you (105%), so kiting buys you a little room
  but not safety, while shooting round them is the reliable answer.
  TOUGH is the whole of the difficulty budget. TEMPO makes the player deal damage half again as
  fast, and if HP did not rise to match, that alone would have quietly turned the game into an
  easier one. At 1.18 a body outlasts the extra rate, so the fights are the same length and cost
  more health, which is what the tempo was actually spent on.

## [h:10-art-13]
near: `const BRUNCH_CHASE_SPEED=2.1,      // no target to protect: 1.31x a chased player, which is a chase`

A walker's closing speed is the one enemy number a player reads off the screen and reacts to, so
it is set directly rather than scaled by PRESSURE - PRESSURE is about how many bodies turn up and
how often the guns open up, and folding it in here would quietly turn every lunger into a threat
nobody asked for. The lunger keeps its 77% of the player (the player got quicker, so the lunger
does too, and the matchup is unchanged).

BRUNCH HAS TWO SPEEDS, and which one a body uses depends on whether it has anything to protect. The
chase speed is the original 1.35, restored because a pack that cannot catch a kiting player stops
being a threat and becomes scenery. The shield speed is much slower, and the split is the whole
design rather than a concession.

The two jobs want opposite things. A wall forming in front of a shooter is walking to a MARK:
overshooting is a real failure, because a body past the line stops blocking and becomes a body in
the wrong place. A pack chasing a player has no mark to miss - the target is moving, and the answer
to being slightly wrong is another tick. Running one number for both is what made them read as
"arithmetic": at 1.18 the wall also moved at 1.18, and 1.18 for a body walking onto a fixed point is
eager rather than deliberate. At 0.72 the wall takes about 2.5 seconds to cross a 200px approach -
long enough to read as repositioning, short enough that the fight does not stall waiting for it.

BRUNCH_WALK is the idle speed a body falls to when it has arrived and is milling rather than
advancing.

## [h:10-art-14]
near: `const BRUNCH_CHASE_SPEED=2.1,      // no target to protect: 1.31x a chased player, which is a chase`

BRUNCH_CHASE_SPEED IS 2.1 and the ramp is 1.5s, and BOTH numbers were wrong for the same reason:
  they were being compared against the wrong player.

  THE PLAYER'S TOP SPEED IS NOT `PLAYER_MOVE`. Measured, holding one direction at steady state:

      empty room                1.4025      player.speed 1.122 * (1 + moveSpeedBonus 0.25)
      momentum meter full       1.6045      the same, plus Momentum.level()*MOMENTUM_SPEED

  And the meter fills BECAUSE you are being chased - being shot at and being chased are what charge
  it. So a chase is against 1.6045, not 1.2, and every ratio quoted against `PLAYER_MOVE` overstates
  the pack by about a third. At 1.75 the pack was 1.09x a chased player and closed at 30px/s.

  TIME TO CONTACT from a 450px gap, player holding one direction, pack alive throughout:

      run 1.75   37.2s
      run 2.10   30.3s     <- the pick
      run 2.60   29.3s
      run 3.20   28.3s

  The curve goes flat hard after 2.1: +83% of speed for 9s more. So the peak is 2.1 and the rest of
  the work is in the ramp, because the peak only sets how fast the pack closes once it is committed,
  and most of the wait happens BEFORE that.

  THE RAMP IS 1.5s, down from 2.2s, and it is the part that matches "bomb rush". At the old 2.2s the
  pack was already faster than a chased player by 0.74s, so shortening the ramp does not change
  whether it catches you - it changes how long you are being chased by something that has not arrived
  yet. The `curSpeed` curve is 0.62, 0.81, 0.98, 1.14, 1.28 ... reaching 2.1 over about 1.6s. The
  announcement still exists - a pack is still slower than you for its first half-second - but the
  interval in which you have to decide what to do about it is two thirds of what it was.

  Both together, measured: contact at 30.3s from 450px, and the pack closes at 0.50px/tick against a
  chased player, so a 200px mistake is survivable and a 400px one is not. That is the difference
  between a chase and a wall, and it is why 3.2 was not taken: it closes at 1.6px/tick, where every
  mistake is fatal and the pack stops being something you can read.

  The shield speed is untouched at 0.72. That one is a body walking onto a FIXED point, where
  overshooting is a real failure, and the two jobs want opposite things - which is the whole reason
  there are two numbers rather than one compromise between them.

## [h:10-art-15]
near: `const ENEMY={`

The two gunners. The change in here is about *where they stand*, not about how hard they hit.

  sense is how far off they notice you at all, and ange is where they will actually open up.
  Those used to be the same number, which quietly conflated "I can see you" with "I am willing to
  shoot from here" - and it is why a gunner would ignore a player standing at the edge of the room
  until you walked into its comfort band. Split them: they notice you well before they will fire,
  and while closing they hold a much tighter band than they used to, so the fight happens at close
  quarters instead of at whatever standoff the old numbers happened to allow.

  On rate: the shells are quick and the guns are busy, but not past the point a person can answer.
  At 2.9px a tick a round covers 300px in about half a second, which is inside the ~250ms it takes
  to see something and start moving, so a shot that is plainly on its way is still dodgeable if you
  commit. What makes it hard is the *rate* - a second gunner covering the gap - not any single shell
  being impossible. Neither ever fires through its own side: see clearShot.

  The Brunch deliberately have no armour, so a pack still dies to a hose and the gun you happen to
  be holding is never simply the wrong one.

## [h:10-art-16]
near: `const ENEMY={`

THE ENEMY TABLE. Every row states its own `armour`, explicitly, and that is a rule rather than a
  detail. It used to be `c.armour||1` at spawn, which meant a row that forgot the field silently
  became a body that takes FULL damage while every other body is multiplied by 0.66 - a 1.52x
  difference nobody had chosen and nobody could see. The Warden was one of the forgetters, and the
  comment beside it said the omission was deliberate, which is the worst state a tuning table can be
  in: a rule that is easy to get wrong, plus a note saying that getting it wrong is intended.

  THE RULE, which is about SIZE and not about any one body:

    every body at least as big as a lunger is armoured - lunger, shooter, gunner, and the boss.

  A shooter is exactly as wide as a lunger, so treating it as chip because it happens to be ranged
  would have been arbitrary. The boss is the largest body in the game and was taking 1.52x more
  damage per hit than anything else, which on an unbounded health ladder is a gap that grows with
  depth rather than staying a property of the body.

  The Brunch is the one exemption, and it is the ORIGINAL design rather than a carve-out made to
  keep a test green. It is half a lunger's radius and an eighth of its health, ARMOUR's own comment
  says it is a stat about "a big body", and the alt blast's pool is sized on the promise that one
  budget deletes a lone heavy OR wipes a small Brunch group outright. Armouring the chip body broke
  that promise, measurably: a blast into three Brunch left all three standing.

  So: stated everywhere, uniform everywhere except the one body the rule is about. `99-tests.js`
  asserts both halves - that no row is silent, and that the ones above the line agree.

## [h:10-art-17]
near: `boss:{mass:4,r:28,art:4,bar:40,hp:343*TOUGH,base:0.6*LUNGER_PAY,walk:0.42*PLAYER_MOVE,run:0.72*PLAYE`

THE BOSS HP IS SIZED FROM MEASURED WEAPON DPS, which is the only way to size a health bar.

    It was 50*TOUGH = 67.5, and measured against the four guns that killed it in 2.20s, 2.66s, 3.81s
    and 5.24s. That is not a fight: it is the same walk with a health bar on it, and a player would
    learn in one attempt that the boss is a formality between the gold key and the next floor.

    Sized so the fight is a fight with the gun the player is actually holding, and re-measured every
    time a weapon changed. On the starting build, 400px, mean of 25 trials:

       Scatter 22.2s    Voidball 29.1s    Bolt 29.9s    Arcane Beam 12.5s

    a spread of x2.39. The spread is left alone deliberately - the slow gun SHOULD take longer, and
    flattening it would mean tuning the boss to the median weapon and telling the player their choice
    does not matter. The depth ladder multiplies all of it, so a floor 10 boss is a long fight by
    exactly the same rule that made everything else on that floor harder.

    The floor is the Arcane Beam with nothing invested in it, which is deliberate - it is a weapon
    whose base damage is low so that Strength sigils are worth several times more to it than to any
    other gun. At zero Strength that gun takes 87.2s here, which is a minute and a half, and the
    4x spread bound in the suite is what stops that from becoming the norm.

    NO ARMOUR, deliberately. Armour would flatten the read: a player watching a health bar fall in
    irregular chunks cannot tell how much of a volley landed, and the whole design is that a player
    who reads the tells takes very little damage.

    THAT ARGUMENT WAS RIGHT AND IT WAS STILL NOT WORTH THE PRICE, which is the part this row did not
    say when it was written. Armour does flatten the read - a bar that falls in regular chunks is
    easier to follow than one that falls in irregular ones. But it was being paid for with a body
    that ignored the single damage rule every other body obeys, and the bill came due three ways:

      - every hit on the boss landed 1/0.66 = 1.52x harder than the same hit anywhere else, so its
        effective health was 19x a lunger's for the same shot count rather than the 29x the numbers
        said;
      - the ladder multiplies health without limit, so the gap was a difference that GREW with depth,
        which is the opposite of what a per-body stat is for - it walked the Warden out of scale with
        the floor it was standing on;
      - and the C# port had already taken the OTHER answer, giving the Warden 0.66 while JavaScript
        gave it 1.0. Two ports, two rules, neither one marking the other wrong.

    Every row in this table now declares its armour, and the boss is one of them.

    Which raises the obvious question: if the boss now takes 0.66, does the fight get 1.52x longer?
    It does, and the first draft of this comment claimed otherwise. "Make the rule consistent" and
    "leave the balance alone" are separate asks, and pretending the first implies the second is how
    a fix becomes a surprise. Worse, the ladder multiplies boss HP without limit, so it compounds: at
    floor 13, where depthTough is 2.50, the Bolt fight would have gone from 111s to 168s.

    So HP absorbs the factor. 520*TOUGH becomes 343*TOUGH = 463.05, which at 0.66 is 701.6 damage
    units - the effective pool the boss had a moment ago (702.0), arrived at by obeying the same rule
    as everything else. That is not a fudge to keep a number still; it is exactly how lunger, gunner
    and shooter already differ from one another. All four are armoured. They have different HP.

    Measured, starting build, one body, firing for real:

      range      Bolt    Scatter   Arcane Beam   Voidball
      150px     30.2s      12.5s         10.4s      29.4s
      200px     31.4s      16.2s         10.2s      29.0s
      300px     41.5s      16.5s         13.3s      32.4s
      400px     40.4s      22.2s         19.7s      35.8s

    and one consequence worth stating plainly, because it was the actual defect: the boss's effective
    health is now 19.0x a lunger's, which is the ratio its HP has always implied on paper. Before
    this change it was 28.9x in the fight, because the number in the table and the number the game
    used were different numbers.

    The paragraph above this one still quotes 22.9 / 27.6 / 39.6 / 54.4s. Those are stale against
    BOTH states - they do not describe the fight as it ran before this change either - and they are
    left only because the sentence introducing them explains what they were for. The measured table
    above is the one to believe. A health bar sized by a figure nobody can reproduce is not a
    measurement, and the honest version of this comment is the one admitting that.

## [h:10-art-18]
near: `const GUNNER_DODGE={kick:0.6, cd:sec(0.6), chance:0.5, sight:250};`

The general pace of the game, in three dials that are only meaningful together.

  TEMPO is how fast the game READS. It quickens the player: their two attack cooldowns, the alt and
  the hook, and the rate the blink refills. PLAYER_MOVE is the same idea applied to their feet.
  PRESSURE is how much is coming at you: more bodies per room, walkers that close faster, guns that
  open up sooner and reposition quicker.

  The reason this is three dials and not one number is a measurement. A room's clear time is
  roughly (total enemy HP) / (your DPS), and TEMPO is a straight multiplier on your DPS, so tempo
  alone is not a difficulty control at all - it just trades time for safety. On a kiting bot:

      TEMPO alone, 1.0 -> 1.75   clear 5.1s -> 3.4s,  damage 1.0h -> 0.4h

  i.e. speeding the player up makes the game strictly easier, because incoming pressure does not
  scale with it. Enemy fire cadence alone is nearly inert (they already miss a lot at range).
  Enemy HP is the only lever that genuinely raises difficulty. Which means tempo and toughness
  trade off almost exactly one for one, and you cannot buy "faster AND harder" out of the same
  budget - you can only decide which one the wall-clock speed is spent on. This build spends it on
  feel: the game reads half again as fast and you move a fifth quicker, and the ground that gives
  back is paid for in tougher, denser, faster-closing bodies, so a room costs about the same
  wall-clock time as before while costing ~15% more health.

  Nothing here shortens a reaction window. i-frames, blink distance and its i-frames, the ready
  window, the unlock, projectile speeds, enemy engage ranges and the aim assist on a body are all
  untouched, so a faster game means less dead time, never less warning.

## [h:10-art-19]
near: `const spriteCache={}, glowCache={}, floorCache={}, woodCache={}, paperCache={}, iconCache={}, glyphC`

paperCache was missing: paperTex was reading and writing woodCache under a 'paper' key, which can
  never collide with a 'wood' key, so it re-baked every call. drawRunSummary calls it once per
  frame - 3360 noise iterations and about ten thousand draws, sixty times a second, on the death
  screen. Worse than the cost: the speckle came from the art stream each time, so the paper
  visibly SHIMMERED rather than sitting still.

## [h:10-art-20]
near: `const MOMENTUM_STEPS=5;`

THE MOMENTUM RAMP - the blink trail, tinted by what the meter was when you blinked.

  Momentum is the one stat that measures what you did rather than what you picked up, and a stat
  like that cannot be taught with a sentence without becoming the thing the sentence is about. So it
  is taught by the art instead: the blink you spend at a full meter leaves a green streak, and one
  you spend at nothing leaves the white streak it always did. A player sees the colour two or three
  times before they see the bar, and connects them on their own, which is the only way anything
  sticks.

  It is read off the blink's OWN momentum rather than live, baked into the trail when it is made. A
  trail that re-reads the meter while it is still on screen would flicker through the whole ramp as
  the meter moved, and a colour that changes under you is not a readout of anything.

  FIVE steps, not a continuous blend, and the reason is that it has to be noticed. A smooth
  interpolation between white and a pale green is a colour the eye cannot name, so it reads as
  "something is different" and never becomes "I am moving well". Five discrete steps is a set of
  states the player can learn, and the jump between them is a thing they can watch happen.

  The green is a spring green rather than the Voidball's deep one, because this is a glow, and a
  glow wants to be brighter than the thing it is lighting.

## [h:10-art-21]
near: `const HEART_ROWS=[".##.##.","#######","#######",".#####.","..###..","...#..."];`

Pixel hearts, filled by FRACTION rather than by one of three states.

  This used to be 'full' / 'half' / 'empty', and that was a real bug waiting to happen. Health is
  not a multiple of two - a shell does 1.8, so a hit can leave you on 0.4 - and the three-state
  heart had a special case for *exactly* 1. Anything below that rendered as a completely empty
  heart, so a player on 0.4 health was staring at a plate with no hearts in it while very much
  alive. Worse, repeated 1.8 damage can leave hp at a float epsilon just above zero, where
  `hp<=0` is false and the run does not end. A continuous fill removes the whole class: a living
  player always has a visible amount in the plate, and the thing on screen is the number that the
  death check is testing.

## [h:10-art-22]
near: `const col=idx==='hook'?HOOK_WEAPON.color`

The colour of an icon is the colour of the THING, never a default. It used to reach for the alt
weapon's colour for every icon at all, which is how the Bolt - a violet spell cast from a violet
wand - ended up drawn in blast-orange. Anything the player reads as "this is what I am holding"
has to be the same colour as the projectile, the wand tip and the pickup glow, or the icon is
actively lying about which gun they picked up.
'alt' is the HUD's name for "whichever right-click you are holding", and it is the same icon as
the blast, so it has to resolve to a real weapon rather than indexing WEAPONS with a string

## [h:10-art-23]
near: `function drawCastFlash(x,y,t,total,col){`

The gunner's cast tell. A ring at the muzzle that swells as the charge runs out, in the shell's
  own colour, so it is obviously the same weapon it is about to use. Deliberately small and
  deliberately at the GUNNER rather than at the player: it has to be visible in peripheral vision
  while you are looking at something else, which means close to the thing making it.

## [h:10-art-24]
near: `function drawStunStars(x,y,stun,fc){`

Three stars orbiting a held body. Deliberately not a ring and not a countdown: a ring reads as a
  timer and a countdown reads as damage, and both are things this is not. They orbit because a
  still shape is easy to miss against a busy floor, and they slow down as the hold runs out so the
  player can see a knot about to come loose - which is exactly when they want to start shooting.

## [h:10-art-25]
near: `const s=scale||1, w=Math.round(c.width*s);`

Icons are baked at 34px into a slot whose inner area is 35px wide, so a full-size draw sat
half a pixel off both inner edges and the blast's rays read as touching the wood. Scaled down
inside the slot it has real air around it, and the offset is what puts the blast's mass a
little left of centre: its rays are the widest part of any icon and they were what filled the
frame, so nudging them in is the difference between "an icon in a frame" and "a frame with
something crammed into it".

## [h:10-art-26]
near: `const itemIconCache={};`

An item's face, for the character sheet. Drawn rather than labelled, because a list of words is a
  list of words and a player scanning a build is looking for SHAPES - the same reason the HUD uses
  baked icons instead of printing weapon names.

  A beveled tile in the item's own colour with a stamped initial, so even the first dozen items read
  as a set of objects rather than as a column of text. An item definition supplies `glyph` (a short
  mark) and `color`; everything else here is the tile. Memoised per id+size, like every other baker in
  this file - a sheet that re-bakes on every open shimmers while you are trying to read it.

## [h:10-art-27]
near: `function drawItemIcon(id,cx,cy,px){`

THE SAME TILE ON THE FLOOR, which is not the same thing as the same function.

  `itemIcon` allocates a fresh canvas and copies into it on every call, which is right for the sheet
  (a handful of icons, drawn once per open) and wrong here: an item pickup is drawn EVERY FRAME, and
  a canvas allocation per frame per pickup is the kind of cost that shows up as stutter exactly when
  the player is walking over the thing they came for. So this reads the memoised bake directly.

  The tile is also the right SHAPE to use on the floor. A weapon pickup is a drawn silhouette that
  reads as a tool; thirteen items in their own colours with their own stamped marks read as objects
  laid out on a shelf, and the player can tell two apart from across the room without a label - which
  matters in a playtest, where the first question about any item is "which one was that".

  Kept plain, and this is a decision that was reversed once. A rarity bar went under the tile and the
  tiles were baked at 34 to match the weight of the weapons beside them; both were argued for on the
  grounds that rarity is the axis being judged and that a smaller tile reads as clutter. Having both
  on screen at once settled it the other way - the plain squares are cleaner, and they read as a set
  rather than as thirteen badges competing for attention. Rarity is on the character sheet, which is
  where the player goes when they actually want to know.
