# 35-devlab.js — moved comments

Long comments moved out of `src/35-devlab.js`. The code keeps a one-line gist tagged `[h:35-devlab-N]`; search this file for that tag.

## [h:35-devlab-1]
near: `const LAB_SPECIMENS=[`

35-devlab  -  a game state that is not a game

  THE LAB, and what it is for. Every other way of answering "what does 7 damage against a Brunch
  actually do" costs a run: you play to the room, you pick the gun, you fire, and you read the
  health bar afterwards if you were paying attention. That is a twenty-minute loop for a question
  that takes a tenth of a second, and it is why balance work in this project has leaned on the
  instrumented fixture in 99-tests.js - which works, but measures a SIMULATION and not the thing a
  player looks at.

  So this is a real game state with a real room and real bodies in it, and the only things missing
  are the reasons a real game is hard: there is nothing to die of, nothing to record, nowhere to
  go, and no run to spoil. F2 gets you in, F2 gets you out, and what you were playing before is
  untouched.

  THE ROOM IS BIGGER THAN THE SCREEN, deliberately and not incidentally. 1680x1040 against a 960x600
  viewport, so the camera has to move. That is the second reason the lab exists: a scrolling room
  is the only place the camera is ever exercised, and a camera that has never been exercised in a
  room you can see all of is a camera nobody has tested.

  THE TWO MODES THE LAB PROMISES, and they are different tools rather than one tool with a switch:

  the SPECIMEN ROW is still. Five bodies, one of every kind, on plinths, each wearing its own name
  and its own health bar. Still is the point: a target that walks changes where your shot lands, and
  a number read off a moving target is a number about your aim. Standing still turns the question
  "how much does this do" back into a question with one variable in it.

  the DROVE is the opposite: bodies in numbers, awake, coming at you, for frame cost, for
  separation, for whether the pack formation survives at forty.

  And the DAMAGE NUMBERS, which the game does not otherwise have. There is no floating number in
  Depths - damage is a health bar and a hit flash, which is right for a game and useless for
  tuning one. The lab derives them by COMPARING each body's health to what it was on the previous
  tick, so it reads damage wherever damage is actually dealt rather than duplicating the damage
  path and hoping the two do not drift. The duplication would be the bug: a second place that knows
  how much a weapon hits, disagreeing with the first the moment a trait or a falloff changes.

## [h:35-devlab-2]
near: `const LAB_W=1680, LAB_H=760;`

1680x760 against a 960x600 viewport. Bigger than the screen in BOTH axes, so the camera has to
  move on each, but only 160px of vertical travel - and that ratio is the whole of the layout.

  The first version was 1680x1040 and it could not work. The camera frames the player, the HUD eats
  the top ~225px of the frame, and a room 440px taller than the view leaves 375px of clear screen
  for a row of specimens, a player and a two-row shelf - and the player has to sit at the room's
  centre for the camera to frame any of it, which puts the player exactly where the row wants to
  be. Every arrangement of the three collided with something.

  160px of vertical travel is enough that the camera genuinely moves and clamps, and small enough
  that the room's whole height fits in the frame with the HUD's share taken out of it. The camera is
  exercised by the WIDTH, which is where the interesting scrolling is anyway.

## [h:35-devlab-3]
near: `let LAB_PACK_CURSOR=1e6;`

Every drove of Brunch the lab builds needs its OWN pack id, for the reason documented at the spawn
  site: the shield-target scan counts bodies that share an id, so reusing one id would merge two
  separate drives into one 24-body pack - which is a different object from the one the lab is showing
  off. A module counter, exactly like PACK_CURSOR in the world generator.

## [h:35-devlab-4]
near: `function build(){`

The lab room is built by REPLACING the bounds on the room the run already made, rather than by
    generating a new dungeon. That is deliberate: the lab has to be the ordinary room with a
    different size, not a special room, because the whole claim being tested is that an ordinary
    room works at another size. Anything special about the lab would hide the thing that is being
    checked.

    The plinths then go on the floor, and the grid and the braziers go in with them - a large flat
    floor with no features on it is not merely dull, it is UNREADABLE as a moving image, because
    with nothing to pass under the camera there is no way to tell the view has scrolled at all.
    The survey grid is not decoration: it is the instrument that makes the camera legible.

## [h:35-devlab-5]
near: `const rowY=b=>b.t+b.h/2-30, shelfY=b=>b.t+b.h/2+130, standY=b=>b.t+b.h/2;`

THE ROW, and where it goes is the whole argument.

    The first attempt placed the row and the shelf against the room's TOP edge, which is the obvious
    thing to do and the wrong thing: the camera frames the PLAYER, not the room. A player standing
    near the middle of a 1040-tall room puts the visible band at world y 420..1020, while the row was
    at 360 and the shelf at 216 - one hidden behind the HUD, one entirely off-screen. Four hundred
    pixels of carefully drawn furniture that nobody could see, and every test still passed, because
    "the shelf has thirteen entries" says nothing about whether the shelf is VISIBLE.

    So these are fractions of the room's height, chosen against the frame rather than against the
    room, and written down so the next person does not tidy them back to the top edge:
      - the camera's top is pinned near the room's top because the player starts low in the band;
      - the HUD eats roughly the first 120px of the band, so the row sits below that;
      - the shelf sits below the row, the player between them.
    They are fractions rather than pixels so a differently sized lab still works, and a test re-derives
    them from W and H so a change to either cannot quietly push the furniture off-screen again.

## [h:35-devlab-6]
near: `const rowY=b=>b.t+b.h/2-30, shelfY=b=>b.t+b.h/2+130, standY=b=>b.t+b.h/2;`

THE THREE BANDS, and they are placed in SCREEN terms and then converted, because that is the
    only coordinate system in which "visible" means anything.

    The camera frames the player, so if the player stands at the room's centre the visible band is
    the middle 600px of the room and the HUD's ~225px comes off the top of it. What is left is
    375px of clear screen, and the three things that have to fit in it are the specimen row, the
    player and a two-row shelf. Placing them as fractions of the room - which is what the first
    version did - cannot work, because the mapping from "fraction of the room" to "pixels of clear
    screen" depends on where the camera ended up.

    So: the player is AT the room's centre, which puts the camera exactly there, and the furniture
    is placed by how far below the centre it should sit ON SCREEN. The row goes just under the HUD,
    the shelf near the bottom, and the player is left on the centre line where the camera wants it.
    A test re-derives all of this from W, H and the HUD's depth, so a change to any of the three
    fails loudly rather than quietly putting a plinth behind the health bar.

## [h:35-devlab-7]
near: `let shelf=[];`

Every item in the roster, laid out as a shelf of alcoves along the top wall, and equipped on
    entry as well. Equipping them AND showing them is not redundant: the build is what a damage
    number is measured AGAINST, so a lab that shows you the item but does not give it to you would
    have you reading a number that describes a different character than the one you are holding.

    The shelf is lab-owned data and NOT pushed into r.pickups. A pickup is something the game
    consumes, and the game's pickup kinds are a closed list it switches on; an entry of a kind it
    does not know is at best inert and at worst falls into a default arm that does something to the
    player. The shelf wants to be looked at, not walked into - everything on it is already equipped
    - so it does not pretend to be a pickup.

## [h:35-devlab-8]
near: `const SHELF_ROWS=2;`

TWO ROWS, because thirteen alcoves do not fit in the frame and this is not a close call.

    The first shelf was one row of thirteen at 106px each - 1378px of rail in a 960px viewport, so
    the middle seven were on screen and the rest ran off both sides. Nothing about that was
    detectable from the data: thirteen entries, thirteen names, thirteen glyphs, all correct, and
    five of them in a place you could not look at. The test now measures screen position, and it
    caught this immediately, which is the argument for measuring screen position.

    Seven per row at 120px is 840px, so a full row fits inside the frame with room for the name
    plates under it. The row count is derived from the room width rather than fixed, so a narrower
    lab wraps to three rows instead of overflowing again.

## [h:35-devlab-9]
near: `const SHELF_RESPAWN_TICKS=sec(2.5);`

THE SHELF IS A PICKUP, not a picture of one.

    It was lab-owned data and deliberately not pushed into `r.pickups`, on the reasoning that the
    shelf is something to be LOOKED at - everything on it is already equipped, so walking into one
    should do nothing. That reasoning was about the shelf and missed the obvious: in a room with a
    roster on a rail, the first thing anyone does is walk up and take one, and the shelf was the one
    place in the game where items could not be picked up at all. A dev view that refuses to do the
    thing the game does is testing something other than the game.

    So each alcove is also a real pickup at the same position. Taking it equips it through exactly
    the same code path as a chest in a real room - `Items.give`, the same displacement of the item
    that was already held, the same drop-on-the-floor. Which means the lab now exercises the swap
    path, which is the single most intricate piece of the item system, on every walk past the rail.

    The alcoves RESPAWN. Without that the shelf empties itself the first time it is used and the
    thing you came to test is gone, so each alcove carries a flag and refills a short time after it
    is taken. The wait is in ticks rather than milliseconds because the game thinks in ticks.

## [h:35-devlab-10]
near: `for(let i=r.pickups.length-1;i>=0;i--){`

IN PLACE, never by reassignment. `r.pickups` has two owners: this and the game's pickup loop
      in 60-tick, which splices as it goes. Reassigning the field here replaced the array the loop
      was holding, so a shelf pickup could be consumed out of one array while the alcoves were
      rebuilt in another - and leaving the lab then filtered the new array, leaving the old one
      intact. One leftover shelf item survived the exit and the next run started with a free
      item on the floor. The symptom was three pickups left after F2 and no plausible cause; the
      cause is that two things thought they owned the field.

      So the shelf removes its own entries with splice, exactly as the game does, and the field
      keeps one owner for its identity.

## [h:35-devlab-11]
near: `function tickShelf(){`

An alcove that has been emptied comes back, so the rail can be used again. Driven from the lab's
    own tick rather than from the pickup loop, because the pickup loop owns `r.pickups` and must not
    be made to know that a shelf exists.

    An alcove is emptied by the pickup loop removing its pickup, which happens the moment the player
    walks into it. So "has this alcove still got its pickup" is the question, and asking it is more
    robust than being told: a pickup consumed by any path at all - including one added later - refills
    the alcove, and nothing has to remember to report it.

## [h:35-devlab-12]
near: `if(--s.gone===0) changed=true;   // back on the rail this tick; 'gone' stays 0`

Counting down. When it reaches zero the alcove is put back on the rail on THIS tick, and
          `gone` is left at zero rather than immediately restarted. The first version did the refill
          and then fell through to the next tick, which found the alcove marked empty, found no pickup
          where one should be - because the refill had not happened yet - and started the timer again.
          The alcove therefore oscillated between one tick available and 522 ticks gone, and a player
          standing on the rail could never pick the same item up twice.

          The two states are kept distinct on purpose: `gone > 0` means "waiting to come back", and
          `gone === 0` with a pickup present means "on the rail". Refilling sets the pickup and stops
          there, so the next tick sees both and leaves it alone.

## [h:35-devlab-13]
near: `function drove(){`

Spawned in a RING around the player rather than at a point, because a drove spawned on top of
    you is a frame-cost spike and nothing else - the bodies are inside each other, the separation
    solver spends its first second unstacking them, and the first thing you measure is the solver
    rather than the fight. A ring puts them already spread, where the thing under test is the thing
    you came for.

    THE RING IS BELOW THE PLAYER, not on them, and that is not decoration. The first version centred
    it on the player, which put half the pack on top of the specimen row - and the row is FROZEN, so
    those five bodies are immovable. Twelve bodies pressing against a wall of statues do not go round
    it. So the ring opens downward, into the clear floor between the player and the shelf, which is
    also where you want a pack to arrive from: you see it coming.

    A DROVE THAT APPEARED NOT TO WORK, and never did. It was recorded as an open bug for a while,
    on the evidence that a dropped body travelled 1.0px in 40 ticks with curSpeed 0. Both numbers
    were true and the reading was wrong: that window is the LUNGE WINDUP, during which a lunger is
    planted and motionless on purpose, because stopping to telegraph is what makes the attack
    readable. The diagnostic that reported it printed lungeState='wind' and read past it. Over 120
    ticks a dropped body closes 279px to 83px and hits. The lesson is the third of its kind in this
    project: a fixture that measures a transient and reports it as a steady state, and the cure is
    always the same - run it longer, and read the field that says which phase you are in.

## [h:35-devlab-14]
near: `const packId=t.type==='brunch'?LAB_PACK_CURSOR++:undefined;`

A DROVE OF BRUNCH MUST BE ONE PACK, or it does not behave like the bodies the generator builds.

      `spawnEnemy` never sets `packId` - the world generator does that in `fillRoom`, once per pack,
      before the bodies (see 20-world.js). The shield-target scan skips any Brunch with no packId at
      all: `if(e.type!=='brunch'||e.packId===undefined) continue`, because a pack is counted by the
      bodies that share an id and a lone unlabelled body has no pack to count.

      So a drove of Brunch spawned from the lab had no ids, no pack, and no way to ever acquire a
      shield target - they defaulted to the advance-on-the-player fallback, always, which is exactly
      what they looked like they were doing: spawn a shooter and a drove of Brunch and the Brunch
      chased the player instead of covering the shooter. The generator-built packs all had ids, so the
      mechanic worked in real rooms and appeared broken in the one tool built to demonstrate it.

      One shared id for the whole drove, and slots in spawn order, so the lab builds the same kind of
      object the world generator does.

## [h:35-devlab-15]
near: `b.noticeTimer=0; b.alerted=true;`

AWAKE ON ARRIVAL. spawnEnemy hands a body up to 56 ticks of noticeTimer before it does
        anything at all, which in a real room is a grace period - you get a moment to read the room
        before it comes at you. In a lab it is two seconds of nothing, and a check that looks for
        movement is measuring the grace period rather than the fight.

        The specimen row silences its bodies for the opposite reason, and the difference is
        deliberate: a row you are reading damage off must not also be shooting you, and a drove you
        are stress-testing must not spend two seconds waking up.

## [h:35-devlab-16]
near: `startGame();`

startGame() first, always. The lab is not a stripped-down world: it is a REAL run's state
      with the run's contents replaced, and building the player by hand is how a lab ends up
      testing a character nobody can actually play. Starting from a real start also means the
      character sheet, the bench and the pause overlay all work in here, which they otherwise
      would not, and a debug view where the menus are broken is a debug view that lies.

      And then it stops being a run. `state` is set to 'dev' AFTER startGame, not before, because
      startGame is what sets it to 'playing' - set it first and the call overwrites it, which would
      leave the lab running as a live run with a full health bar and a path to the records. Every
      `state==='playing'` test in the game is now false, so nothing counts a shot, nothing descends
      and nothing can be recorded, and the simulation still runs because the tick was taught to let
      'dev' through.

## [h:35-devlab-17]
near: `function leave(){`

Leaving goes back to the TITLE, not to a run. There is no run to go back to: the lab called
    startGame() and threw away whatever the player was in the moment they walked in, so the honest
    thing is to leave them somewhere they can start again rather than somewhere that looks like
    their floor 7 continues. Returning to a half-remembered run state is how a debug tool eats
    somebody's afternoon.

## [h:35-devlab-18]
near: `function tickNumbers(){`

THE DAMAGE NUMBERS, derived rather than reported. Each body is compared with what it was last
    tick and the difference is what floats. The alternative - having the damage path call the lab -
    would mean a second place that knows how much a weapon hits, and the two would drift the first
    time a trait, a falloff or a resistance curve changed and only one of them was updated. Here
    there is nothing to keep in step, because the lab is not told anything: it watches.

    A rise is printed too, and red rather than white, because a body that HEALS between two ticks is
    a thing worth seeing rather than a thing to quietly average away - a Brunch wall, a regen item,
    a hook that gives health back all show up here first.

## [h:35-devlab-19]
near: `function freeze(){`

FROZEN means the bodies stop being simulated, not that they are drawn still. Each tick their
    position and heading are written back, which is the difference between a target you can measure
    against and a target that is merely slow: a slowed body still drifts on knockback, still turns
    to face you, and still takes a frame of the fight into your reading of the number. Writing the
    position back is what makes the specimen row honest.

    It ALSO stops them acting, and that part was missing. Pinning a shooter's position while leaving
    its firing branch live gives you a stationary gunner shooting you from thirty pixels away, forever,
    which is not a still copy of anything - it is a fight you are trying to measure a number inside.
    noticeTimer is the game's own "has not noticed you yet" clock, and a body with it pinned high
    skips its whole update, so this is the existing silence rather than a new one.

    Which is why the row and the drove are opposites rather than two settings of one thing: a row
    you read damage off must be still AND harmless, and a drove you stress-test must be neither.

## [h:35-devlab-20]
near: `if(k==='f7'){ drove(); return true; }`

F7 DROPS A DOZEN. It was F5, which the browser owns.

    F5 is reload in every browser, so "drop a dozen" reloaded the page instead - which is the worst
    possible failure for a dev tool, because it throws away the room you were setting up to test. The
    key was never `preventDefault`-ed, so it was never even a conflict: the page reloaded first and
    the handler never ran.

    F7 is used because it is unbound in every major browser (F5 reload, F6 focus the address bar in
    some, F11 fullscreen, F12 devtools). The whole lab key block moved to F2-F4/F6/F7 for the same
    reason: F5 is not available and F1 is the weapon bench.

    `keys[k]=true` runs after this, so the key is still recorded - which is what stops a held key from
    firing twice, and `preventDefault` for the function keys is the game's business, not the lab's.

## [h:35-devlab-21]
near: `if(k==='f6'){ for(const s of shelf) s.gone=0; syncShelfPickups(); return true; }`

F6 REFILLS THE SHELF. The alcoves respawn on their own after two and a half seconds, which is
      right when you are walking past the rail collecting things, and useless when you have taken
      everything and want it back without waiting - which is what you want after changing a build and
      coming back to see the effect.

## [h:35-devlab-22]
near: `if(k==='g'){ player.hasGold=true; return true; }`

THE BENCH'S THREE SHORTCUTS LIVE HERE, because the lab is where they are safe.

      The weapon bench had H for a full heal, S for the silver key and G for the gold key, and it was
      reachable with F1 during an actual run - so the panel's own footer advertised a written-down way
      to hand yourself the run's objective and undo a bad room. Keys are the objective and hearts are
      the run; a tool that grants them is not a tool.

      Swapping guns stayed on the bench, because a weapon is a comparison rather than a reward and
      comparing needs both sides on screen. The three that change the RUN are here, where entering is a
      deliberate two-press action and leaving is the same key.

## [h:35-devlab-23]
near: `const engraved=(text,x,y,color,align,font)=>{`

Everything below is drawn in WORLD space, inside the camera transform, so it scrolls with the
    room - which is the point. A label pinned to the screen would be readable but would not tell you
    where anything is, and in a lab whose whole subject is "where is this body relative to that
    one" a label that does not move is worse than no label.

## [h:35-devlab-24]
near: `const MINOR=120, MAJOR=480, gridCache=new Map();`

THE SURVEY GRID, and this is the single most functional thing in the lab.

    A camera scrolling over a featureless floor is very hard to read: the eye has nothing to track,
    so a scroll of a hundred pixels looks like a scroll of ten. The grid gives the view something
    to move against, and because the lines are WORLD-fixed they are also a ruler - you can see that
    a body is 200px from the wall rather than inferring it.

    The major/minor split matters. Uniformly spaced lines read as wallpaper and stop being a
    ruler; a line every 120 with a brighter one every 480 gives both a fine reference and an obvious
    "one big square" to count, which is how you estimate a distance in a fight.

    BAKED, because it is WORLD-FIXED and therefore frame-invariant. It was two stroke passes and
    about twenty line segments every frame, to redraw a lattice that cannot change until the room
    does; a room 1680 wide has 14 verticals and 7 horizontals in it. One drawImage now, cached per
    room size and origin. The same argument as the floor, which was already cached for exactly this
    reason before the lab existed.

## [h:35-devlab-25]
near: `const GRID_MINOR_ALPHA=0;      // was 0.055 - a 120px lattice over a 1680px room is a tiled floor`

The grid is gone entirely, and the reason it existed is worth keeping.

    It was a measuring aid: 14 verticals at 120px over a 1680px room, for judging camera travel and
    distance. Over a floor that is otherwise a seamless speckle, a 120px lattice does not read as a
    ruler on the ground - it reads as TILES, and the complaint was exactly that: white lines dividing
    defined square shapes, where a dungeon floor is seamless.

    Dropping the minors was not enough. The majors are 480px apart, so a 960px viewport shows 2
    verticals and 1 horizontal - and a partial lattice does not read as a ruler either, it reads as
    the corner of a very large square. There is no alpha at which a line lattice stops being one.

    And the decisive point is comparative: **a dungeon room has no grid at all.** The lab was
    carrying a visual element the game does not have, drawn over the element the game does have. The
    camera movement it was meant to help judge is perfectly legible without it - the room is bigger
    than the screen, so the walls themselves are the ruler.

    Both alphas are zero rather than the code being deleted, so the grid is one number away from
    coming back if a future view genuinely wants it. The centre cross stays: it is one mark, not a
    lattice.

## [h:35-devlab-26]
near: `const BRAZIER_GEOM=()=>({bowlY:BRAZIER_BOWL_Y, flameBaseY:FLAME_BASE_Y, flameBaseRow:FLAME_BASE_ROW,`

The two brazier offsets and the flame's half-size, published so the suite can check the flame
    against the bowl without re-deriving numbers that only mean something together.

    It is a FUNCTION, not a constant, and that is not a style preference. The first version built the
    object here, at the top of the file, out of BRAZIER_BOWL_Y and FLAME_BASE_Y - which are declared
    70 lines further down. `const` does not hoist, so the Lab module threw a ReferenceError the
    moment it loaded, `Lab` never existed, and 32 tests failed for a reason that had nothing to do
    with any of them: an object literal reads its values at the moment it is built, so building it
    before the values are declared cannot work however far apart they are.

    Reading them inside a function defers that to the moment of the call, which is the only thing
    that makes the order irrelevant. The failure was silent in the worst way - the console showed a
    ReferenceError from a line that looked entirely reasonable.

## [h:35-devlab-27]
near: `const BRAZIER_R=168, BRAZIER_FRAMES=5, brazierCache=new Map();`

BRAZIERS on the walls. Warm pools of light on a cold floor, and - like the grid - fixed points
    the camera passes. They also do the job the room vignette cannot: the vignette darkens toward
    the edges, which on a room this size means the far corners go almost black, and a body fighting
    out there would be invisible. The braziers put light back where the play is.

## [h:35-devlab-28]
near: `const BRAZIER_R=168, BRAZIER_FRAMES=5, brazierCache=new Map();`

BRAZIERS, AND WHY THEY ARE A SPRITE.

    Measured, not assumed: a normal frame in this game allocates ZERO canvas gradients and renders
    30 bodies in 0.44ms. The lab allocated 29 gradients per frame - 15 radial, 14 linear - and
    rendered in 0.94ms. Every one of those was light that looks identical every frame being rebuilt
    from scratch every frame. createRadialGradient is not free, and twenty-nine of them is most of a
    millisecond.

    So they are baked, the way the floor and the sprites already are in this project. The pool and
    the column go into one canvas per brazier, drawn with a single drawImage; the flame goes into
    five baked frames picked by the same counter that drives everything else that flickers.

    The pool's brightness rides in globalAlpha rather than being baked in, because alpha is free and
    a second sprite per flicker step is not. And the sprites are built ONCE and cached in a Map,
    which is the same shape as spriteCache and glowCache in 10-art.js - the lab was the only place
    in the game still building its art from scratch on every frame.

    FREE-STANDING COLUMNS IN THE ROOM, not on its walls. The first version put all eight braziers on
    the walls, and in a room larger than the viewport the walls are never on screen, so all eight
    sat outside the frame at every camera position and the lab rendered with no light in it at all.
    A count of "eight braziers placed" says nothing about how many are visible.

## [h:35-devlab-29]
near: `const BRAZIER_FRAME_TICKS=18;`

HOW FAST THE FIRE BURNS, and it was far too fast to read as fire.

    Measured, at TICK_HZ 210: the cycle was 5 frames every 3 ticks, so a full loop took 71ms and the
    flame changed 70 times a second. That is not a flicker, it is a strobe - and because consecutive
    frames differed ONLY in height (19, 20.25, 21.5, 22.75, 24 - the same shape, the same colours, a
    5px scale), the eye had nothing to track and read the whole thing as a buzz.

    BRAZIER_FRAME_TICKS 3 -> 18 makes the loop 429ms and the changes 11.7 a second, which is inside
    the range a real flame actually flickers at. The frames also differ in lean and waist now, so
    there is a shape change to follow rather than only a size one.

    The counter divides by a constant rather than stepping a modulus per brazier, so every brazier in
    the frame flickers in unison - correct, since they are one sprite, and drawing them out of step
    would need one sprite per brazier per phase.

## [h:35-devlab-30]
near: `const BRAZIER_BOWL_Y=21,   // the bowl is this many rows ABOVE the brazier sprite's middle`

WHERE THE FLAME GOES, and the sign convention that makes it mean something.

    Both sprites are baked centred on their own middle and drawn by their TOP-LEFT corner, so a
    feature at sprite-row F in a sprite of half-size R appears on screen at `spot - R + F`. Everything
    here follows from that one sentence.

    The bowl is baked at row `o - BRAZIER_BOWL_Y`, i.e. BRAZIER_BOWL_Y rows ABOVE the sprite's middle,
    so on screen it is at `spot - R + (R - BRAZIER_BOWL_Y)` = `spot - BRAZIER_BOWL_Y`. The R cancels.
    The flame's base is baked at row FLAME_BASE_Y inside its own sprite, so drawn at top T it lands at
    `T + FLAME_BASE_Y`. Setting those equal: **T = spot - BRAZIER_BOWL_Y - FLAME_BASE_Y**.

    The first attempt at this wrote `spot - BRAZIER_R - BRAZIER_BOWL_Y - FLAME_BASE_Y`, treating
    BRAZIER_BOWL_Y as a distance BELOW the sprite's middle rather than above it, and subtracting the
    sprite radius a second time on top of that. It put the flame 168px ABOVE the bowl - and the
    screenshot showed it floating over the stand. The original two constants were off by TWO pixels;
    the "fix" was off by 168. A screenshot is what caught it, and the arithmetic that claimed to have
    derived it was what made it worse.

    So the derivation is written out here, in full, in the file, where the next person can check it.

## [h:35-devlab-31]
near: `FLAME_BASE_ROW=61,`

THE FLAME'S BASE ROW, in absolute sprite rows - which is what the drawing needs, and is NOT
           the same number as the constant flameFrame bakes with.

           flameFrame draws `moveTo(o, o+FLAME_BASE_Y)` where `o` is already S/2 = FLAME_R. So the
           base lands at row `FLAME_R + FLAME_BASE_Y` = 61 in a 72-row sprite, and the 25 is a
distance from the sprite's middle*, not a row. Reading it as a row put the flame's base
           36px - one whole half-size - below where it belonged, which is why it kept landing on the
           base of the stand: 11px predicted, 14px measured, and the difference is the antialiased
           edge. Two attempts at this failed the same way, both by treating an offset-from-centre as an
           absolute row, so the derived row is a NAMED value now and the drawing cannot misuse it.

## [h:35-devlab-32]
near: `const FLAME_LEAN=[0,-1.6,0.9,1.7,-1.1];       // tip offset, px`

FIVE BAKED FLAMES, not a blend. The same reasoning as the momentum ramp: a blend is a colour the eye
    cannot name, and a flame that is always exactly one of five heights reads as a flame cycling
    rather than as a rendering artefact.

    Each frame varies in FOUR things rather than one. The original made five copies of the same
    silhouette at five heights, which is a zoom rather than a fire - and at the old 71ms cycle the eye
    had no time to read even that. So each frame leans by its own amount and shifts its waist, which
    is what actually distinguishes one instant of a flame from the next: the tip wanders sideways and
    the body narrows and widens, it does not simply get taller.

    The lean values are fixed per frame rather than random, because a random sprite re-baked per frame
    shimmers - the same reason the seed tag is cached and the floor is baked once. Five frames is
    enough for a loop that no longer runs at 14Hz.

## [h:35-devlab-33]
near: `const stepX=300;`

FREE-STANDING COLUMNS IN THE ROOM, not on its walls. The first version put all eight braziers
      on the walls - corners and midpoints - and in a room larger than the viewport the walls are
      NEVER on screen, so all eight sat outside the frame at every camera position and the lab
      rendered with no light in it at all. Nothing was broken; there was nothing there to see, and a
      count of "eight braziers placed" says nothing about how many are visible. They are set in from
      the walls so the ones in the middle are always in frame, and spaced off the room so the
      density per screen stays constant as it grows.

## [h:35-devlab-34]
near: `ctx.drawImage(flame, x-FLAME_R, y-FLAME_BASE_ROW-BRAZIER_BOWL_Y);`

THE FLAME SITS IN THE BOWL, and it used to sit on the floor beside the stand.

        Both sprites are baked around their own centre, so their features live at an offset inside
        them, and the two offsets were never reconciled: the bowl is drawn at `o-21` within the
        brazier sprite (so `y - BRAZIER_R - 21` on screen) while the flame's BASE is baked at `o+25`
        within the flame sprite (so `y - 48 + 25 = y - 23`). That put the flame's base 166px below the
        bowl it belongs to - which is a fifth of a brazier's height - and read as a lit line on the
        floor beside a stand, rather than as fire in a dish.

        The offsets are now named and derived from the sprites themselves rather than from whatever
        two numbers were typed next to each other, so moving the bowl in the brazier sprite moves the
        flame with it. BRAZIER_BOWL_Y is where the bowl sits inside the brazier sprite, and
        FLAME_BASE_Y where the flame's base sits inside the flame sprite.

## [h:35-devlab-35]
near: `ctx.drawImage(flame, x-FLAME_R, y-FLAME_BASE_ROW-BRAZIER_BOWL_Y);`

The flame's BASE must land on the bowl's centre, which is `spot - BRAZIER_BOWL_Y` (the brazier
    radius cancels: the bowl is baked at row `R - BRAZIER_BOWL_Y` in a sprite drawn at `spot - R`).
    Drawn at top T, the base is at `T + FLAME_BASE_ROW`. So:

        T + FLAME_BASE_ROW = -BRAZIER_BOWL_Y
        T = -(FLAME_BASE_ROW + BRAZIER_BOWL_Y)

    One term, each with a reason to exist: the row because that is where the fire's bottom actually
    is in its sprite, and the bowl because that is where the dish actually is in the other sprite.
    Neither is a fudge and neither is a distance-from-centre pretending to be something else.

## [h:35-devlab-36]
near: `const shadowCache=new Map();`

A SPECIMEN PLINTH: a stone drum with a lit top face, and a brass nameplate on the front.

    The plinth earns its place twice over. It tells you which body is which without a floating label
    that would fight the sprites, and it is why the row reads as a curated display rather than as
    five things that happen to be in a room - which matters, because the whole claim of this view is
    that it is a considered arrangement rather than a level. The top face catches light and the
    front face is in shadow, which is the same two-value read the game's own stone uses.

## [h:35-devlab-37]
near: `const shadowCache=new Map();`

The shadow under a plinth, baked per radius. Same measurement as the braziers: this was one
    createRadialGradient per specimen per frame, for a shape that is a circle with a soft edge and
    never changes. The cache is keyed by radius because the boss's plinth is a different size, and a
    sprite drawn at the wrong scale would be a soft circle of the wrong softness.

## [h:35-devlab-38]
near: `const RARITY_TINT={common:'#6f7889',uncommon:'#6fbf9a',rare:'#7fb2f0',legendary:'#f0c86a'};`

THE SHELF: an alcove per item, cut into a stone rail along the top wall, with the item's OWN
    glyph in the item's OWN colour. That is why it does not read as a row of placeholders - there
    are thirteen different marks in thirteen different colours because each one is the item's, and
    you recognise the build you are holding by looking at the rail. Rarity tints the alcove's inner
    shadow, so a legendary is visibly seated in a warmer, deeper niche.

## [h:35-devlab-39]
near: `function draw(){`

Everything, in the order it should stack. TWO calls rather than one, because one number-free
    draw is not enough and neither is one number-bearing draw:

    draw() is the furniture and goes UNDER the bodies - the survey grid and the braziers are marks
    on the floor, the shelf is a rail on the wall, and a plinth is the thing a body stands ON. A
    plinth drawn over its own specimen would hide the thing you came to look at, and a brazier's
    glow drawn under the floor would be a brazier that does not light anything.

    drawNumbers() is the readout and goes OVER the bodies, because a damage number half-hidden
    behind a lunger is a number you have to move the camera to read, and the camera is the thing
    this lab is also testing.

## [h:35-devlab-40]
near: `const LEGEND=[`

THE LEGEND, and it is drawn in SCREEN space, unlike everything above.

    Every other thing in here scrolls, and that is right - a nameplate pinned to the world belongs
    to the world. But the key list is a property of the VIEW, not of the room, and worse: a legend
    that scrolls away is a legend you have to walk back to, which in a room four screens wide means
    walking back. So it sits in the corner, always, and it is also the only place the lab's state is
    written down - frozen or not, and which body the dropper is armed with - because those are the
    two things you would otherwise have to remember or guess, and a debug view that makes you guess
    is a debug view that produces a wrong number and blames the game.

## [h:35-devlab-41]
near: `const lane=legendLane();`

A HORIZONTAL STRIP ALONG THE BOTTOM, and it was a panel in the bottom corner - which put it on
      top of the item shelf both times it was tried, once in the bottom-left and once in the
      bottom-right, because the shelf is a wide rail across the lower band of the room and the
      lower band is where the bottom of the screen is.

      A strip is also the shape this game's own furniture already uses: the binds bar under the
      canvas is a row of key chips. So the lab legend reads as part of the same language rather
      than as a debug overlay parked on the picture, and it cannot collide with anything, because
      the only thing it shares its row with is the shelf ABOVE it.

## [h:35-devlab-42]
near: `const chipTop=y+Math.max(2,Math.min(6,Math.round((h-15)/2))), baseline=chipTop+12;`

THE CONTENTS ARE CENTRED IN WHATEVER LANE THERE IS, not pinned to a y that was measured against
      a 26px strip. The strip is 26px when no Warden is on the row and 20px when one is, because the
      bar's frame is in the way (see legendLane), and a chip row pinned to the old y would hang 3px
      out of the bottom of its own background the moment the Warden spawned - which is to say, in the
      lab, on the row you built the lab to look at.

      The chip is 15px and the text baseline sits 12px below the chip's top, which is the pair that
      made the 26px lane read. Both are derived from the lane, so the one thing that can change - the
      lane's height - is the only thing that has to be re-measured when the boss bar moves.

## [h:35-devlab-43]
near: `const LEGEND_H=26, LEGEND_MIN_H=20;`

THE LEGEND'S LANE, and it is a function of whether a Warden is alive in this room.

    THE COLLISION, MEASURED. The boss bar hangs 23px off the bottom of the canvas, so its bar is at
    y 567..577 and its FRAME at 564..580. The legend was a fixed 26px strip at y 574..600. Those
    overlap by 6px, and they overlapped for the whole of the lab's life - which is worse than a bug
    in a game, because the lab is where you go to look at the bar.

    Which of the two should move is not a free choice, so the reasons are both here:

    - THE BAR DOES NOT MOVE. It is anchored to the canvas bottom, it is the one thing in a fight that
      is not moving, and it is already at the bottom because below it is the canvas edge.
    - THE LEGEND DOES NOT MOVE UP. Moving it up would put it across the SHELF, which is the item rail
      across the lower band of the lab room and the thing the lab exists to check - and the shelf is
      world-space, so an up-shifted legend would sit on it at some camera positions and not at
      others, which is worse than a collision you can predict.

    So the legend gives up the 6px. The lane SHRINKS to whatever is left below the bar's frame, which
    here is 20px: enough for a 15px chip and its baseline, because those are the only two things in
    it that have a height. With no Warden on the row the lane is its full 26px again, so the legend is
    pixel-for-pixel what it has always been in every frame that is not a boss fight.

    A minimum is stated rather than assumed: a lane thinner than a chip plus a baseline cannot draw
    the thing it exists to draw, and silently overflowing it would put the chips back on top of the
    bar - which is the defect this whole function is about, re-introduced by a different route.

## [h:35-devlab-44]
near: `top=Math.min(H-LEGEND_MIN_H,Math.max(full,Math.min(H,b.y+b.h+3)));`

MAX, not min. The lane's top moves DOWN to clear the bar's frame - and when the bar happens to
        sit entirely above the full lane there is nothing to clear, in which case max leaves the lane
        where it was. min got this exactly backwards: it would have picked whichever of the two was
        HIGHER, which is the one that overlaps, and the strip would have been pinned over the bar
        again.

## [h:35-devlab-45]
near: `top=Math.min(H-LEGEND_MIN_H,Math.max(full,Math.min(H,b.y+b.h+3)));`

b.y-3 .. b.y+b.h+3 is the FRAME, because drawBossBar fills the recess at (x-3, y-3, w+6,
        h+6) - so the frame's bottom edge is b.y+b.h+3 and not b.y+b.h. Getting that 3 wrong puts
        the legend's rule line exactly on the frame's last pixel row, which is a 1px overlap that no
        width comparison would have caught.

## [h:35-devlab-46]
near: `const h=Math.max(LEGEND_MIN_H,H-top);`

and the floor is applied to the TOP, not the height: a lane that cannot be as tall as a chip is
      anchored to the bottom of the screen and overhangs upward rather than downward, so the failure
      mode is a strip that is a few px short against the top of its own background rather than five
      rows of chips printed off the bottom of the canvas.
