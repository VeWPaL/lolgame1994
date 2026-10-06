# 70-view.js — moved comments

Long comments moved out of `src/70-view.js`. The code keeps a one-line gist tagged `[h:70-view-N]`; search this file for that tag.

## [h:70-view-1]
near: `function c0(d){ return {N:[MIDX,ROOM_TOP-8],S:[MIDX,ROOM_BOTTOM+8],W:[ROOM_LEFT-8,MIDY],E:[ROOM_RIGH`

70-view  -  everything drawn

  The part Unity replaces wholesale. Read it for BEHAVIOUR, not for structure: what has to be
  legible, what must read in a glance, what the tells are. None of it ports; all of it is
  specification for the replacement.

## [h:70-view-2]
near: `function doorRect(d,wt){`

THE GAP IN THE WALL FOR EACH SIDE, as [x,y,w,h].

  One table, three readers: the frame fill, the boss gate, and the unlock sweep. It used to be three
  separate tables written out side by side, and that is the whole reason the boss door was broken in
  two different ways at once.

  The gate is the one that showed. Its copy of the vertical case had the BOTTOM wall's y baked into
  it, so the E and W portcullises were drawn 254px below the doors they were sealing - sitting on the
  floor in the corner of the room - while those doors showed nothing but a bare padlock. The unlock
  sweep had the mirror fault: it always drew a horizontal bar, so on a vertical door it swept across
  the thickness of the wall rather than along the gap it was supposed to be opening.

  Neither was findable by reading the code, and that is the part worth keeping. Two of the three
  tables agreed with each other and the collision code in 40-combat agrees with those two as well, so
  the odd one out was invisible from any single file and only appeared the moment all four sides were
  drawn at once - which a level almost never does, since the boss gate is one door in a layout. Any
  geometry that more than one piece of code needs belongs in exactly one place, and this is what it
  costs to leave it in three.

## [h:70-view-3]
near: `function drawFloor(type){`

THE FLOOR, and the cache is keyed by SIZE as well as by type - which it has to be now that a
  room is not one shape.

  It used to be keyed by `type` alone, on the reasoning that there was one room size so the size
  was constant. That reasoning stopped being true the moment bounds went on the room, and the
  failure is quiet rather than loud: a big room asks for its floor, the cache hands back the
  700x450 canvas built for a small one, and it gets stretched to cover 1600x1000. Everything looks
  present. The tile pattern doubles in size, the cave speckles smear, and nobody can say why the
  floor of the biggest room in the game looks like it is wearing a hat two sizes too small.

  So the key is type + width + height, and the radial vignette is sized from the room rather than
  from a literal 420. The vignette is the other half of it: at 420px it was most of the way across
  a 450px-tall room, and on a 1000px-tall room it would be a small bright disc in the middle of a
  large dark floor - the room would read as unlit beyond the disc rather than as a room. Scaling it
  off the diagonal keeps the proportion, which is what was actually being chosen by hand.

  THE AREA IS IN THE KEY TOO, and it is the same mistake one level out. A cache keyed by type and
  size alone is a cache that believes there is one dungeon, and descending a floor hands back the
  previous floor's baked canvas - which is the exact class of bug the size key exists to prevent,
  and it would have been invisible in every screenshot of a single floor. Three keys or nothing:
  what the room is, how big it is, and where in the climb it is.

## [h:70-view-4]
near: `g.fillStyle=g.createPattern(caveTile(pal.id),'repeat');`

TWO TINTS, IN THIS ORDER, and the order is the design.

      The room TYPE's tint goes on first at 0.34 and the area's wash on top at its own strength,
      because they answer different questions. Type says what the room is FOR - the item room
      glows, the boss room is red, and that read has to survive into every area or a themed floor
      silently deletes the one thing the floor colour was telling the player. The wash says where
      you are. Mixing them into one pre-baked colour would have made the second one win.

## [h:70-view-5]
near: `function clearFloorCache(){`

THE FLOOR CACHE IS EMPTIED HERE, BECAUSE ITS PIXELS BELONG TO A RUN AND NOT TO A SESSION.

  The key is three fields wide (type, size, area) and that is what stops one floor's room being
  painted in another area's stone. It is still not enough across a RUN boundary, because the canvas
  it stores was filled through `caveTile`, and that tile was baked from Rnd.art() - the stream 05-rng
  derives from the seed the player typed. Start a new run on a new seed and the key still matches, so
  the cache hands back the previous run's baked floor, speckle included.

  Which is why this sits next to drawFloor rather than in the art file: it is the same cache, the
  same key, and the same reasoning that produced the key in the first place, and splitting the
  argument for why a key is insufficient across two files is how the key stops being believed.

  `floorCache` is a const OBJECT rather than a Map, so it is emptied by deleting its keys rather than
  by reassigning it - reassigning would rebind nothing at all, and an empty-looking local next to a
  full cache is the quietest possible version of this bug.

  NOT CALLED FROM ANYWHERE YET. 50-run owns startGame and the run lifecycle, so the hook belongs to
  the agent that owns that file: one call before a new run draws anything. This file's job is to
  expose the door and say plainly that it is not yet wired up.

## [h:70-view-6]
near: `function clearArtCaches(){`

ALL THREE RUN-DEPENDENT ART CACHES, in the one call the run lifecycle should have to make.

  One entry point rather than three because the failure is collective: a run that clears the floor
  sprite but not the cave tile underneath it repaints the room and changes nothing, because the
  pattern it fills the canvas with is the cached one from the last run. That is a version of this bug
  that is invisible in a screenshot and obvious in the pixels, and it is exactly what three separate
  hooks invite.

  `clearTileCaches` lives in 10-art because the two tile caches are declared there; this file can
  call it because 10-art is loaded first, and nothing here is invoked at load time.

## [h:70-view-7]
near: `function drawWall(pal,wt){`

THE WALL, as four bands of one masonry pattern anchored to the room's own corner.

  It was four `fillRect`s of a flat colour, which is fine for exactly one area and reads as a frame
  around the game rather than as stone the room is built out of. Four palettes over a flat
  rectangle would have made that four times as obvious, so the courses come with the theming.

  ANCHORED, not tiled from the screen origin. A pattern fill starts at (0,0) of the current
  transform, and the room draw is inside the camera translate, so an unanchored pattern would slide
  the masonry when the camera moved and the wall would visibly crawl. Translating to the room's own
  corner first makes the pattern world-fixed: the same argument the lab's survey grid is anchored
  on, and for the same reason - a texture that is not where it was drawn is not a texture.

  One pattern object per draw, built from a tile baked once per area.

## [h:70-view-8]
near: `for(const f of hookFields){`

The hook's ground spell. Built from the same parts as the wand tip - a radial glow and a few
small square motes - so it reads as the same magic, but the motes spiral INWARD instead of
orbiting, which is what makes it look like something is draining the room rather than
decorating it. It sits on the floor, under everything, and fades out over its last third so its
end is legible before it stops holding.

## [h:70-view-9]
near: `Lab.draw();`

The lab's furniture goes over the floor and under the bodies: the grid and the braziers are
    marks ON the floor, the shelf is a rail on the wall, and a plinth is the thing a body stands
    ON - drawn over its own specimen it would hide the thing you came to look at. The damage
    readout is the other half and goes the other way, over everything; see Lab.drawNumbers.

## [h:70-view-10]
near: `const bg=roomTone(r.type,areaPalette().id);`

THE DOORWAY IS FILLED WITH THE COMPOSITED ROOM TINT, not the bare type tint, because it is a
    hole through the wall and what shows through it has to be the room it opens into. Filling it
    with ROOM_BG[type] alone meant the gap was the one patch of floor that ignored the area - a
    rectangle of Area1 stone punched through a Kiln Works wall on floor 6, which is exactly the
    kind of thing that reads as a rendering fault rather than as a door.

    roomTone() is the same function the floor composite is built from, so the gap cannot drift from
    the floor it sits in. It is deliberately still OPAQUE rather than a copy of the floor sprite:
    the doorway is meant to read as a darker way through, and pasting the whole floor into it would
    give the player a second, identical view of the room they are standing in.

## [h:70-view-11]
near: `const gate=doorRect(d,wt);`

ONE source of truth for where the gap is. This used to rebuild the rect here with the bottom
wall's y baked into the vertical case, which put the E and W portcullises 254px below the doors
they seal. doorRect is the same table the door frame itself is drawn from, so the gate cannot
disagree with the doorway even if the geometry changes.

## [h:70-view-12]
near: `const g=doorRect(d,wt);`

The sweep runs ALONG the gap, not across the wall.

        It used to be one unconditional horizontal bar, which is correct on a north or south door
        and wrong on an east or west one: there it drew a 90px-wide band through the thickness of
        the masonry while the gap it was supposed to be opening ran vertically beside it. The tell
        for "this door is opening" therefore pointed the wrong way on half the doors in the game,
        and the fix is the same one as the gate's - ask doorRect where the gap is rather than
        assuming which way up it happens to be.

## [h:70-view-13]
near: `const glow=(f.mine&&f.mom!==undefined)?momentumGlow(f.mom):DASH_GLOW;`

The player's own blink, tinted by the meter it was spent at. `f.mine` is the tag from
      doBlink; everything else in this array belongs to a body and keeps the white glow it always
      had. A green puff on a lunger's windup would be telling the player about THEIR meter in the
      middle of reading an ENEMY's, which is the one place a colour cue must not lie.

## [h:70-view-14]
near: `for(const e of r.enemies){`

The committed line. A lunge is a straight shot at a point, so the point gets drawn: from where
the lunger was when it planted, to where it is going. Without it the player has to infer the
direction from a glow, and with three lungers in a room, inferring which one aimed where is
exactly the bookkeeping that makes a fight feel like bookkeeping instead of a fight.

## [h:70-view-15]
near: `if(e.type!=='boss'){`

The floating bar over a body - every body EXCEPT the boss.

      The boss used to get one of these too, on top of the fixed bar at the bottom of the screen, and
      two health bars for the same health is one too many: they disagree the instant they are both
      on screen (the floating one is 4px tall and rounded to whole pixels, the fixed one is 10px and
      notched), and the player has to decide which one to believe. It is redundant with the player
      having their own bar as well - the fight has two health bars and one of them is the boss.

      So the boss's health is drawn once, in one place, by drawBossBar. Every other body keeps its
      floating sliver: at 4px over a 14px body it is a glance, not a reading, and that is the right
      amount of attention for a Brunch.

## [h:70-view-16]
near: `if(e.stun>0) drawStunStars(e.x,ey-e.r-6,e.stun,frameCount);`

Stars over a held body. The hook's entire value is that it holds a knot on the floor for a
second and a half, and until now nothing said "this one is stuck" - the walk simply stopped,
which is also what a body at the edge of its aggro does. Three little stars orbiting the head
read as stunned from across the room, and they are the cue that a lunger caught mid-charge
has lost that charge, so the glow going out is a reward rather than a glitch.

## [h:70-view-17]
near: `if(e.castT>0) drawCastFlash(e.x,e.y,e.castT,CAST_TIME,e.pcol);`

The cast tell, drawn with the BODY and not with the projectiles. It was originally drawn in
the projectile loop, which meant it was never drawn at all - a gunner is not a projectile, so
the condition could never be true, and the tell existed only in the timing. A mechanic nobody
can see is not a mechanic.
anchored to where the gunner IS, not to where the charge began: a gunner covers about thirty
pixels in half a second, so a flash pinned to the muzzle at the start of the cast spends the
whole of it trailing thirty pixels behind the body making it

## [h:70-view-18]
near: `Lab.drawNumbers();`

The lab's damage readout, over the bodies and over the projectiles. It is here rather than with
    the plinths because a number is the one thing in the lab that must never be behind something:
    it is the thing you are reading, and in a room this size the body that just got hit is often
    behind another body that did not.

## [h:70-view-19]
near: `const dr=drawR(p);`

A shrinking projectile is the gun telling you what it is worth before it lands. The size is
read off the SAME falloff number the damage will be, so the picture and the number can never
disagree - and because fMin is a floor, a bolt at the far end of the room is small and nearly
harmless rather than small and still lethal, which is the honest way round.

## [h:70-view-20]
near: `r.spawnPlan.forEach((s,i)=>{`

THE MARK IS DRAWN AT THE PLAN'S OWN POSITION, and the line goes to whatever body is standing there -
      NOT to `enemies[i]`.

      This paired `spawnPlan[i]` with `enemies[i]`, which is wrong in two independent ways:

      - a Brunch PACK is one entry in the plan and SEVERAL bodies in the room, so every body after the
        first is annotated with the plan line of some other spawn entirely;
      - dead bodies leave `enemies`, so the indices slide and every mark after the first kill points at
        the wrong body.

      Both produce the same result: a tuning overlay that confidently annotates the wrong enemy. It is
      debug-only, which is why it survived - but an overlay you cannot trust is worse than none, because
      it gets read as evidence about spacing. So the mark is where the plan says it is, the line goes to
      whatever actually occupies that spot, and a planned spawn with nobody on it says so rather than
      borrowing its neighbour's annotation.

## [h:70-view-21]
near: `let BENCH_ROW_W=0;`

THE ACTIVE PLATE. Not a weapon plate with different contents, because it is answering a different
  question: the weapon plates show a cooldown filling, and this one shows how many presses are LEFT,
  which for a stackable consumable is the number that decides whether to spend it now. So the count
  is drawn large in the corner rather than as a filling bar, and an item that never runs out shows a
  mark instead of a number, because "x0" would be a lie about an item that is always available.

  The empty state keeps the dotted outline `drawSlot` uses for a reserved plate, for the reason in
  that function: a frame you can see is a slot, and a gap is a mistake. The Q is drawn on the plate
  rather than only in the controls sheet, because this is the one key that acts on the thing in this
  frame and a player should not have to remember which frame it was.

## [h:70-view-22]
near: `function fitLabel(text,maxW){`

ONE LABEL, SHRUNK TO FIT THE WIDTH IT IS GIVEN, and only truncated as a last resort.

  This replaced a three-label row fitter, and the reason is worth keeping because both earlier
  versions of that fitter looked correct and were not.

  The first fitted each name into the 62px plate under it. Eleven of the thirteen item names are
  wider than that, and "Arcane Beam" was 73px, so the labels overlapped.

  The second fixed the overlap by shrinking and truncating each label against a budget derived from
  its neighbours, and a screenshot of it read:

      ARCAN...   UNTER'S ...   BLAST

  which is worse than the overlap, because a stub in the active-item slot removes the one piece of
  information the player cannot get anywhere else. Shrinking to 9-10px is the same failure wearing
  different clothes: a row of 10px type is unreadable in exactly the situation it matters.

  The third fitted all three as one line of text, which is correct arithmetic and still wrong: the
  row is 147px, and the three names at 12px come to 210px in the worst case. It also carried a test
  that asserted against a hardcoded row width of 190, so it passed while checking a geometry the game
  does not have - which is the failure mode that let all three versions look right.

  The row now belongs to the item name alone, so this fits ONE string, and the honest claim is simply
  that every name in the roster fits whole at full size. It does: the longest, "Lantern Friend", is
  92px in a 139px budget.

  Truncation remains for a future name that does not fit, and it is built from a prefix and
  re-measured each step. The first version dropped the last character and re-appended '...', which is
  an INFINITE LOOP - the string never gets shorter - and it was found by a mutation that stopped the
  suite responding rather than reporting a failure. A freeze is the worst defect this function could
  have, and no current name reaches that branch, so it would have shipped.

  Returns the text, the size it settled on, its width, and whether it had to cut, so a test can
  assert the last one rather than trusting that it never happens. Sets ctx.font and ctx.textAlign and
  leaves them set, as the drawing code already assumes.

## [h:70-view-23]
near: `const def=Content.has('item',held.id)?Content.get('item',held.id):null;`

GUARDED, like every sibling. `Content.get` throws on a missing id - loudly, and correctly, because
    a missing definition used to flow into a stat read and become a NaN three frames later. But this
    is the per-frame HUD path, so a throw here does not fail loudly once: it fails every frame, from
    inside render(), and the animation loop stops with the game frozen.

    The id comes from `loadout`, which is a plain data object a mod can write, and `Content.resetMods()`
    rebuilds the table from the pristine copy - so a held item whose id is not in the table is a
    reachable state, not a theoretical one.

    Four call sites read an item definition and this was the only one that did not check first:
    `10-art.js:803` and `:829` guard with `Content.has`, and `70-view.js:1410` — three lines away in
    the same file, drawing the same held item — guards too. The guard being present in one place and
    absent in its neighbour is the whole tell.

## [h:70-view-24]
near: `ctx.fillStyle='rgba(8,4,2,0.74)';`

A CAPTION STRIP, because the first version drew Q and the count straight onto the tile and they
    landed on its bottom corners - a 34px tile in a 50px plate leaves no room for a second row, and
    "Q" over the tile's lower-left bevel reads as part of the item's own artwork. The neighbours get
    to own their whole plate too: theirs fill a background, this gets a strip at the foot and the tile
    sits above it. The tile is 26 rather than 34 because it is a FILLED square and the weapon icons are
    sparse sprites - at equal nominal size the square carries more mass, not less, so this reads level
    with its neighbours rather than smaller.

## [h:70-view-25]
near: `const HUD_MARGIN_X=15, HUD_MARGIN_Y=14, HUD_FRAME=6, HUD_GAP=1;`

THE HUD LAYOUT TABLE, hoisted out of drawHUD so the tests read these numbers instead of copying
  them.

  They were function-locals, and the suite re-declared the same nine integers to assert against -
  on the stated ground that it wanted to test "the HUD matches a stated layout" rather than "the HUD
  matches itself". The reasoning is not wrong and the consequence is worse: `ok(MARGIN_X!==MARGIN_Y)`
  compared the test's own two copies, so it could not fail whatever the game did, and a real change to
  a margin would fail nine assertions that all needed editing by hand.

  This is the boss-door bug and the pulse bug and the drift bug, in its purest form: the same geometry
  living in two places, and the copy is the one under test. One table, read by both, and the
  comparison becomes a real one - if the margins are ever collapsed back into a single number, the
  assertion now fires because it is reading the number the drawing is reading.

## [h:70-view-26]
near: `const HUD_TOP_BAND_H=15;`

THE TOP BAND, AND THE TOP OF THE PLATE BLOCK UNDER IT.

  The band is a full-width rail across the top of the screen and it OWNS that strip: nothing else
  draws there. It is drawn as wood, in the same material as the plates, because a band that is a
  different substance from the thing hanging off it reads as a second system rather than as a
  frame - and for now it carries no content of its own. That is a deliberate blank, not an
  oversight: the band's job here is to own the strip, and the strip has to be owned before anything
  is allowed to put an instrument in it.

  WHY THE BLOCK IS PUSHED DOWN RATHER THAN THE BAND ABSORBING IT: the band is additive, and the
  block below it is three rows tall. HP_H + ROW_H + GAP + ROW_H is 101px, and the left column has to
  end at or above ROOM_TOP (130) or the depth plate sits on the top wall of the room and hides bodies
  walking along it. So the band can be at most 29 - HUD_MARGIN_Y = 15px tall before the HUD would
  have to start eating the play area, which is a balance constant and not this file's to move.

  15 is therefore not a taste decision, it is the largest rail that costs the play area nothing.
  Both numbers are here rather than at the call site so the suite can read them; the tests assert the
  CONSTRAINT (the block still ends above the room) rather than the 15, because the 15 is what falls
  out of the constraint and not the other way round.

## [h:70-view-27]
near: `ctx.drawImage(woodPlate(W,HUD_TOP_BAND_H),0,0);`

No drop shadow under the rail, and there was one for a while. It was 3px of rgba(8,5,3,0.55) and
    it did nothing measurable: the band sits above ROOM_TOP on every map, so what is behind it is the
    empty space over the room and that is already rgb(0,0,0). A shadow over black is black, and the
    suite could not tell the two apart - removing it entirely left every check green, which is the
    signature of a decoration that was never visible. The rail's lower edge comes from woodPlate's
    own dark border, which is at y 13..14 against the void and does read.

    This is worth writing down because the obvious next move is to "put the shadow back, it must be
    doing something". It is not. If the band ever carries content with a hard bottom edge of its own,
    that is the thing that needs a shadow, and it should be added with the content.

## [h:70-view-28]
near: `function bossBarRect(){`

WHERE THE BAR GOES, worked out from the room being drawn right now AND from the screen it is being
  drawn on.

  A function rather than a constant, because there is more than one room: the Lab is 1680x760 and the
  floors are 700x450. The first version baked the rectangle out of ROOM_W and ROOM_TOP at load time,
  so a bar sized 660px wide for a 700px room was being drawn 660px wide inside a 1680px one, at a y
  captured before the Lab changed the room. A constant that describes a room is wrong the moment
  there is more than one room, and the Lab exists precisely to prove those are not the same thing.

  THE ROOM IS THEN CLAMPED TO THE SCREEN, which is the half that was still missing after the above.
  A per-draw rectangle that still measured ROOM_RIGHT-ROOM_LEFT drew a 1640px bar starting at x 70
  in the Lab - a bar whose right-hand 750px, both phase notches' surroundings and the flush-right
  "PHASE 3" caption were off the side of a 960px canvas. Nothing about that looks broken in a
  screenshot unless you are looking for it: you see a bar, it is at the bottom, and the part you
  cannot see is the part that was wrong.

  The Lab is the place every tell gets checked without playing a run, so a bar that runs off the
  screen there is a bar that is only really drawn in one of the two places it can be drawn - which is
  the same argument BOSS_BAR_MARGIN's own comment makes about the vertical edge, applied to the
  horizontal one.

  THE ROOM STILL DECIDES WHERE IT SITS. On a floor the room is 700px inside a 960px screen, so the
  clamp does nothing and the bar is 70..730 - the same 660px it has always been, aligned to the
  play area, because that alignment is the reason the bar is inset from the room's own walls. Only
  a room wider than the screen (the Lab, and only the Lab) gives up its edges: the bar then spans
  the screen's full width, inset BOSS_BAR_INSET_X each side, because there is no room wall there to
  line up with and a 20px offset from the left of the screen with nothing on the right of it reads
  as a mistake rather than as an alignment.

  Both thresholds are read from BOSS_PHASE_1/2 by the caller, and both land inside the bar in both
  rooms - which is the property that was true of the floor and false of the Lab.

## [h:70-view-29]
near: `function liveBossInRoom(){`

THE BOSS THE BAR IS ABOUT, or null. One predicate, because the bar's drawing and everything that
  has to make room for the bar are asking the same question - "is there a Warden in THIS room right
  now" - and the Lab's legend lane has to answer it identically or the two draw over each other.
  hp>0 is the whole of "alive": a body killed on the last tick is still in the array until the
  killer's sweep removes it, and a bar for a corpse is a bar lying about the fight.

## [h:70-view-30]
near: `function drawBossBar(e){`

THE WARDEN'S HEALTH, as the one piece of the fight that is fixed to the screen.

  The boss already had a health bar - a 56px sliver, twice a regular body's, floating above a body
  that walks around. It is not nothing, and this is not a claim that the boss had no bar at all; it
  is that a bar the player has to find, that moves with its owner, that carries no phase markers and
  no name, does not do the four jobs this one has to do:

    1. SHOW WHAT IS LEFT. A floating sliver answers "did that hit land". This answers "how much of
       this is there", which is the question a player is actually asking at 66% health.
    2. MARK THE PHASES. BOSS_PHASE_1 and BOSS_PHASE_2 are fractions of max HP at 0.66 and 0.33, and
       crossing them changes the move mix from 3 moves to 4 to 7 - the wall only appears in phase 2,
       and phase 3 is two thirds sweep. The fight visibly changes and NOTHING said so. A bar with the
       two thresholds drawn on it turns that from a thing that happens to you into a thing you can
       see coming, which is the whole fairness argument this boss was built on.
    3. NOT MOVE. Everything else in a fight is moving. A fixed thing is the thing you glance at
       between glances at the boss.
    4. NAME IT. The Warden had no name field - it was called THE WARDEN in a dozen comments and
       never on screen.

  Deliberately NOT a second visual language: the same green as every body bar, because a boss bar in
  a different colour reads as a different kind of object and this is the same quantity at a size you
  can read. What changes is the size, the fixed position, and the two notches.

  The fill is drawn from the LEFT edge so the bar empties toward the door the player came in by,
  which is where they are heading and where the fight will resume. It is not drawn as a shrinking
  width from both ends, which would keep the centre of mass fixed and make the change hardest to see.

  The phase notches are 2px of bone-white standing PROUD of the bar rather than cut into it, because
  a notch inside the bar disappears as the fill passes over it, and these need to stay visible after
  they have been crossed - at that point they are the record of how far the fight has already come.

## [h:70-view-31]
near: `for(const th of [BOSS_PHASE_1,BOSS_PHASE_2]){`

The phase notches, drawn proud of the bar so crossing one does not erase it. These read
    BOSS_PHASE_1 and BOSS_PHASE_2 rather than retyping the numbers, so the mark on the screen cannot
    drift from the threshold in the fight, and they are placed with the SAME expression that places
    the fill's edge: both are Math.round(w*f) on the same w.

    That is not a cosmetic detail. Comparing the two as fractions - round(w*th)/w against th - comes
    out 0.0006 apart, which reads as a mismatch and is not one, because both were rounded from the
    same width. Dividing a rounded pixel back out and comparing it to a raw fraction manufactures a
    disagreement that does not exist on screen. The claim that is actually true, and that the suite
    asserts, is that at exactly the threshold health the fill's edge IS the notch's pixel.

## [h:70-view-32]
near: `ctx.font='bold 11px monospace';`

The name and the phase, ABOVE the bar. Below is the canvas edge - the bar hangs in the 20px strip
    below the room, so there is no room under it for a caption, and putting the labels there would
    push them off the bottom of the screen. Above, they sit over the floor in the bottom of the room,
    which is dead space during a boss fight, because the player is looking at the boss.

    The name is flush left and the phase flush right so a long name and a number cannot collide
    however wide the bar is, and the phase is shown as a number because the mix it selects is not
    legible from colour alone - three moves, then four, then seven.

## [h:70-view-33]
near: `const MARGIN_X=HUD_MARGIN_X, MARGIN_Y=HUD_MARGIN_Y, FRAME=HUD_FRAME, GAP=HUD_GAP;`

The HUD is one block, laid out from a single table of numbers so that nothing can drift.

    Everything here used to be placed by hand against the thing next to it - the key plate at 208
    because the heart plate ended near 200, the blink plate at y=56 because the heart plate was
    40 tall and started at 10. It looked fine on the numbers it was tuned on and fell apart the
    moment anything moved: raising maxHp widened the heart plate and left the key plate floating
    in the middle of nowhere, and the blink plate kept a gap under a plate it was supposed to be
    part of. Six plates and a map is a lot of hand-placed edges, and every one of them is a seam
    that does not line up.

    So: one MARGIN off the screen edge, one FRAME for the wood, one GAP between adjacent plates, and
    everything else measured off those. The plates are still separate frames - each keeps its own
    border and its own studs - because "one block" should read as a set of labelled things in one
    place, not as one long smeared bar.

    They touch, but they are NOT all the same width or the same height, and they should not be. The
    health plate is as wide as the health needs; the key plate is two small icons; the blink plate
    is two short bars. Forcing all three to one width - which is what an over-literal reading of
    "one block" produces - fills the narrow ones with empty wood, and empty wood inside a frame
    reads as a missing slot rather than as breathing room.

## [h:70-view-34]
near: `const MARGIN_X=HUD_MARGIN_X, MARGIN_Y=HUD_MARGIN_Y, FRAME=HUD_FRAME, GAP=HUD_GAP;`

MARGIN is split by axis, because the two are not the same decision. The left edge of the HUD is
    measured against the screen border and the room and wants a pixel more air than the top edge
    does - the plates sit in a corner, and a margin that is right on one axis looks wrong on the
    other whenever the two share a number.

    GAP is the space between the health plate and the key plate on its shoulder, and it is one
    pixel. Three read as two separate objects with a corridor between them; one reads as two plates
    in the same frame of reference, which is what they are.

## [h:70-view-35]
near: `drawTopBand();`

The band owns the top of the screen, so the block starts under it rather than on the top margin.
    One number for the whole block: the health plate, the key plate on its shoulder, the blink row,
    the Momentum plate beside it and the depth plate underneath are one object, and they move as
    one. Placing them off MARGIN_Y individually is the hand-placed-everything bug this table was
    written to end - a block that is pinned to the top margin in five places is five chances for
    the top band to arrive and only one of them to move.

## [h:70-view-36]
near: `const hearts=Math.ceil(player.maxHp/2), armorSlots=Math.ceil(MAX_ARMOR/2);`

THE HEART PLATE GROWS WITH maxHp, AND maxHp HAS NO CEILING - so it needs a ceiling of its OWN.

    Vigor is unbounded by design, so a build with enough Iron Ribs puts 28 hearts in the plate, and
    28 hearts at 26px is 810px from a margin of 15. The minimap plate begins at 798. From maxHp 56 the
    two overlap: the heart plate is drawn over the map, and the map - the thing that tells you where
    the boss is and which rooms you have seen - is unreadable for the rest of a run.

    This is theoretical in the way a lot of balance bugs are theoretical: it takes twenty-one Iron
    Ribs, no other sigils, and a floor deep enough to have found them. Nothing catches it because
    nothing plays that build.

    Two fixes, and both are needed. The plate CAPS its width at what the screen can hold, so it can
    never reach the minimap however much health there is - and the number of hearts actually DRAWN is
    capped with it, because 28 half-hearts at 26px is unreadable whatever space they have. The full
    health is still on the plate, because the HUD already prints it as a number next to the hearts and
    a player with 98 health who cannot see 28 of them still knows they have 98.

## [h:70-view-37]
near: `const LABEL_W=26;`

HOW MANY HEARTS FIT, AND HOW MUCH ROOM IS LEFT OVER.

    Solved in two steps rather than one, because the label and the heart budget both need the other's
    answer: the label only exists if hearts are hidden, and the hearts only fit if the label has been
    paid for. So the budget is computed with the label reserved, and if it turns out nothing is hidden
    the reserved width is handed back.

    The reservation is one 26px slot, which is what "+23" at 11px monospace needs with room to spare,
    and it is paid out of the heart budget rather than added to the total - so a wide plate still cannot
    reach the minimap however much health there is.

## [h:70-view-38]
near: `const insetX=hx+FRAME, insetW=healthW-FRAME*2, heartSlotW=heartSlot;`

The hearts are CENTRED in the inset, not started from a hardcoded 36. The old version measured
the left margin and the right margin with two different expressions, and the armour slots added
four more pixels on the right on top of that, so the last heart always sat closer to the frame
than the first did. One expression for the first slot, and the whole row follows from it.

## [h:70-view-39]
near: `for(let i=0;i<heartsDrawn;i++){`

EACH SLOT IS ITS OWN INDEX, AND HEALTH DRAINS LEFT TO RIGHT.

    `i` is the slot's own heart number, so the fill is `(hp - i*2)/2`: full hearts first, empties at
    the right. That is the direction the bar has always drained and the direction a player reads.

    This measured `heartsDrawn-1-i` instead - the slot's distance from the END of the row - which
    reverses the whole plate. At 2 health the row read `[empty x7, full]`: the one heart with blood in
    it sat in the far-right slot, and the bar counted backwards. It read as a deliberate choice
    because it was written to fix a real problem, so it is worth being explicit about what that problem
    was and why this is the right answer instead.

    The problem: when the plate is CAPPED and hearts are hidden, drawing only the first `heartsDrawn`
    slots means a player at 98 health sees 26 full hearts and no empties - which is correct - but a
    player at LOW health has their remaining health in the FIRST hearts, which are the ones being
    drawn, so the row correctly showed the damage. The original worry was that a capped row would be
    "full then empty", which is only wrong if it is drawn from the wrong end.

    Measured, at maxHp 98 with 23 hearts hidden, from the left:
        hp 98  26 full hearts              hp 46  23 full then 3 empty
        hp 2   1 full then 25 empty       hp 1   1 half then 25 empty
    Every one reads as the health it is, because a prefix of the row IS the player's remaining health
    and the "+23" says how much of the total is not on screen.

## [h:70-view-40]
near: `if(heartsHidden>0){`

"+N" GOES AFTER THE LAST HEART, not before the first one.

    The row now runs left to right with the health on it, so a label at the left end sits exactly where
    a player's remaining health is drawn - on top of the first heart, which is the one they are reading.
    The count of hidden hearts belongs on the far side, past the empties, where it reads as "and there
    are more of these".

## [h:70-view-41]
near: `if(Math.round((player.hp/2)*2)/2<=1&&player.hp>0){`

The last point of life gets a halo so "one heart left" can never be misread as "none left".
It matters more than it sounds: at 1hp the plate shows one half heart and five empties, and
beside two dimmed armour slots that is easy to read as an empty plate. The pulse is the same one
the boss warning uses, and it only exists on the heart that still has blood in it - not on the
empties, which would make the whole plate look live.

## [h:70-view-42]
near: `if(Math.round((player.hp/2)*2)/2<=1&&player.hp>0){`

"ONE HEART LEFT" MEANS ONE HEART ON THE PLATE, which is not the same as hp equal to one.

    This asked `player.hp===1`, an exact comparison, and almost nothing in the game ever produces an
    exact 1. Damage is fractional on purpose - SHOT_DMG 1.8, LUNGER_PAY 0.96, BOSS_SHELL_DMG 1.44 -
    so the health walks past 1 without landing on it: from 8, four shooter shots reach 0.8 and the
    fifth kills, four boss shells reach 1.24, and the pulse never fires at all. A warning that exists
    to catch you at the moment you are about to die, and does not, is worse than no warning: it is a
    promise the screen made and did not keep.

    The rule is now the one the heart plate itself draws by. `fmtHearts` rounds to the nearest half
    heart, because that is what a half-slot heart IS, and a player reading the plate sees 1 heart
    while their health is anywhere near it. So the halo asks the same question the plate answers
    rather than a different and stricter one.

## [h:70-view-43]
near: `let litLast=-1;`

ON THE LAST HEART THAT STILL HAS BLOOD IN IT, which is not the last slot on the plate.

      This drew the halo on `slotX + (heartsDrawn-1)*heartSlotW` - the last slot DRAWN - on the
      reasoning that "the last heart" meant the last heart. It does not: the row drains left to right,
      so at one heart the only lit slot is the FIRST one, and the halo was pulsing an empty heart at the
      far end of the plate. Measured: lit heart at x 43, halo at x 225. A warning that points at an
      empty slot while the one you are looking for sits unlit at the other end is worse than no halo,
      because it is a warning about the wrong thing.

      So the halo follows the fill: the last slot with `fill > 0`. At one heart that is slot 0; at two
      and a half it is slot 1; at full health on an uncapped plate it is the last slot, which is where
      it always was and why the mistake went unnoticed for a full plate.

## [h:70-view-44]
near: `const val=player.armor-i*2, cx=slotX+(hearts+i)*heartSlotW;`

on the SAME grid as the hearts, with no offset. There was a +4 here "to leave a gap", and it
was the only thing breaking this plate's symmetry: the row was computed as centred and then
the last slot was shoved four pixels right, so the hearts sat 11.5px from the left of the
inset and the armour sat 7.5px from the right. The gap it wanted is already 26-21=5px.

## [h:70-view-45]
near: `const kInsetX=kx+FRAME, kInsetY=hy+FRAME, kInsetW=KEY_W-FRAME*2, kInsetH=KEY_H-FRAME*2;`

The keys were the worst-aligned thing on the screen and both halves of it came from one
    mistake: drawing a sprite at a point that was not its centre. drawSprite anchors at the CENTRE.

    This placed the pair by its left edge and then added a vertical offset tuned by eye, which put
    the silver key 6.5px OUTSIDE the frame's inner border - sitting on the wood, which is exactly
    what the screenshot shows - and left the pair 8px low, with 11px of air above it and 3px below.
    It read as a health-bar problem because it is drawn on the same row as one, but nothing was
    inherited from the health plate: the two are measured independently and both were wrong.

    So the inset is measured, and the pair is centred on it in BOTH axes, with the gap between the
    keys being whatever the padding leaves. One rule, and the frame is what it should be.

## [h:70-view-46]
near: `const by=hy+HP_H;`

row 2: the blink charges, glued to the bottom of the health plate and back to the width they
were. Two short bars stretched across 186px of wood read as progress on something enormous. The
row is deliberately NARROWER than the plate above it, and that asymmetry is what makes the two
read as two different instruments stacked rather than as one long bar with a second row of
decoration on it.

## [h:70-view-47]
near: `const mX=hx+BLINK_W+GAP;`

THE MOMENTUM PLATE. On the blink's row, beside it, because these are the same kind of thing:
    both are the two numbers that describe how the player is doing THIS SECOND rather than what they
    have built. Blink is what you have left, Momentum is what you are worth right now, and pairing
    them says so without a word.

    It was on the character sheet before, and that was wrong for a reason worth writing down. The
    sheet is a pause screen: the player opens it to find out what they are CARRYING, and Momentum is
    the one number on it that they did not pick up. A row that answers a different question than the
    other seven reads as a mistake, and worse, a stat you cannot change is a stat you cannot act on -
    so the only time the player saw the number was when they were not playing.

    In the HUD it is something to watch, which is the entire point of it. It charges while you move
    with bodies in the room, bleeds when you stop, and a hit costs most of it, and every one of
    those is legible from the bar alone if you can see it change. The sheet said that in a sentence.
    The bar says it by being a bar.

    NO TUTORIAL. Deliberately. The plate carries its own name, engraved the same way the depth
    numeral is, and the bar carries the ceiling. A stat that has to be explained is a stat the
    explanation has to keep up with, and this one is legible: it goes up when you are doing well and
    down when you are not, which is the entire rule, visible in real time.

    The bar is NOTCHED for the same reason the character sheet's was. Momentum is capped at 1, and for
    a stat whose whole design is a ceiling that cannot be passed, the ceiling is the interesting
    part - a smooth fill cannot answer "how close am I", and a nearly-full notched bar can.

    The label and the bar split the plate the same way the depth plate splits it, so the row reads
    as one grammar: an engraved mark on the left, a measured indicator on the right.

## [h:70-view-48]
near: `const mTrackX=mX+FRAME+Math.floor(mInsetW*0.44), mTrackY=mCy-6;`

The bar takes everything the label does not, and it takes a LOT of it. This is the number the
player is meant to watch move, so it gets the width: at 150px of plate the label ate 40% of the
space and left a 40px bar, which is a decoration. A meter you cannot see travel is not a meter
you can learn from.

## [h:70-view-49]
near: `const dW=HUD_DEPTH_W, dy=by+ROW_H+HUD_GAP;`

THE DEPTH PLATE. Which floor you are on, in the same wood-and-inset vocabulary as the health,
    key and blink plates rather than as text painted on the floor.

    It has to be a plate and not a number drawn in the corner, for the same reason those three are
    plates: the HUD is a row of instruments, and a bare number sitting beside them reads as a
    leftover debug readout rather than as the fourth thing the player is watching. The floor is now
    the number the whole game is about - it is what the difficulty ladder reads and what the record
    is - so it belongs in the row, and it belongs there at a glance.

    The depth is drawn as a struck numeral with a tally of ticks beside it rather than as "FLOOR 7".
    The word costs a third of the plate and says nothing the numeral does not, and the tally is
    there because a depth that is only ever a number gives no sense of accumulating distance: three
    marks at floor 7 and one at floor 4 read as different places, which is what they are.

## [h:70-view-50]
near: `ctx.fillStyle='#0d0a08';`

THE NUMERAL IS STRUCK IN THE AREA'S INK, and this is the whole of the HUD's palette.

    The plate is wood and the wood is wood in every area - a themed HUD would mean re-baking every
    plate in every area, which is the one thing that would cost per-frame work for a signal the
    floor already gives for free. So the accent is ink on the existing wood: the numeral is the only
    thing on the plate that changes hue with the area, and it is the one element a player looks at
    to answer "how deep am I", which is the question the accent is answering.

    Lit above floor 1 and dim on floor 1 exactly as before - that rule is about the CLIMB rather
    than about the place, and it should not start meaning something else because the place now has
    a colour.

## [h:70-view-51]
near: `const cell=17,mapW=GRID*cell,pw=mapW+28,mx0=W-MARGIN_X-pw,my0=HUD_BLOCK_Y,mx=mx0+14,my=my0+14,ic=3;`

THE MAP IS NOT DRAWN IN THE LAB, and the reason is that in the lab it would be a lie.

    The lab is built on top of a real startGame(), so a real 18-room dungeon exists behind it -
    which is what gives the lab a valid player, a valid build and working menus. Its map is
    therefore a map of a dungeon that is not there. On screen that reads as a large dark grey
    rectangle with one white cell in it: the unvisited-room colour on the knocked-back paper, with
    nothing in it to look at. A player - or a designer - sees a grey box and reasonably concludes
    something failed to draw.

    A debug view that shows furniture from a world it is not in is worse than one that shows
    nothing, so it shows nothing. The lab's own survey grid and braziers are its map, and they are
    true.

    The guard wraps the map's DRAWING and not its layout arithmetic, which is deliberate and was
    wrong the first time: the weapon row underneath reuses pw and mx0 to sit flush against the
    bottom of the map, so closing the guard any earlier left those names undeclared and the HUD
    threw on the very first frame. The weapon row is also deliberately left OUTSIDE the guard - the
    bench works in the lab, and the lab is exactly where you want to be able to swap guns.

## [h:70-view-52]
near: `ctx.fillStyle=withAlpha(areaPalette().mapWash,0.52);ctx.fillRect(mx0+10,my0+10,pw-20,pw-20);`

Knock the paper back so the map reads as a lit board in a dark room: the white door frames and
    the room glyphs end up the brightest things on it instead of white-on-cream.

    The wash carries the AREA'S hue at the SAME alpha as the neutral it replaces, and that is the
    only thing about it that changed. The alpha is load-bearing - it is what keeps an unvisited room
    legible as a light shell rather than as a hole in the board - so a themed wash that also changed
    the alpha would have re-tuned the map's readability as a side effect of colouring it.

    IT IS ITS OWN FIELD rather than the floor's tint, and that is measured rather than tidied. The
    floor tint is the hue of the STONE under the player; the map wash is a near-black ink laid over
    cream paper. Using one for the other put rgb(37,33,28) where rgb(9,11,17) used to be - a map
    three shades lighter in every channel, which eats into the contrast between an unvisited shell
    and the board behind it. So `mapWash` is the ink, and Area1's is the literal that was there.

## [h:70-view-53]
near: `const known={};`

rooms next to somewhere you have been are shown as unlit shells: enough shape to plan a route
with, not enough to skip the room. only rooms an actual door leads to count, otherwise the map
shows a room that is merely touching you on the grid and there is no way in. built from the
cached room array and a fixed four-direction walk, because this runs every frame

## [h:70-view-54]
near: `if(rr.type==='boss'&&!rr.cleared){`

A boss you have walked into and not finished gets its own mark. The padlock only tells you the
door is shut, which stops being useful the moment you are standing in the doorway with the key
in your hand - and backing out of a half-fought boss with no marker is the one place on the map
you have no idea where to come back to. The ring breathes so it reads as live rather than as a
permanent fixture, and it is the loudest thing in that cell while the fight is on.

## [h:70-view-55]
near: `const slotY=my0+pw+GAP, slotH=50, slotGap=2;`

The weapon row is three plates on one line: left hand, middle, right hand, touching, and glued
to the bottom of the map. The middle one is the ACTIVE SLOT - the item Q presses - and it was a
drawn empty frame for most of this file's life on the grounds that a consumable wants to be a
different shape from a weapon. The shape argument was right and the conclusion was wrong: an
empty plate between two weapons reads as a hole in the HUD, and the plate was already reserved,
so leaving it empty only made the reservation visible.

It is drawn with the same wood, border and inset as its neighbours because an undrawn gap between
two plates reads as a mistake and a drawn one reads as a slot. The map is exactly three slots wide
to within a pixel, so the row cannot be off-centre.

## [h:70-view-56]
near: `BENCH_ROW_W=pw;`

The row width the labels are fitted into, published so the test can assert against the value the
    drawing actually uses. It was a hardcoded 190 in the suite, and the real value is 147 - so the
    test was checking a geometry the game does not have, and passed while doing it. A test that
    cannot read the number it is about has to guess it, and a guess that is comfortably larger than
    the truth is the worst kind: it fails nothing.

## [h:70-view-57]
near: `const labelY=slotY+slotH+14;`

Only the ITEM is named on this row, and it is named across the whole row.

    Three names on one line was tried and it does not work, and the measurements are the reason
    rather than taste: the row is 147px, and the three of them at 12px come to 210px for the worst
    case ("Arcane Beam" + "Lantern Friend" + "Blast"), 197px for the next, 184px for the next. Only
    "Bolt" + "Tin Cup" + "Blast" fits unshrunk. Everything else had to shrink to 9-10px or lose
    letters, and a row of 10px type is the same legibility problem as a row of stubs - it just
    fails more quietly.

    So the middle plate gets the row to itself, and the two weapons keep only what the player cannot
    already read off them. That is not a loss of information: the weapon plate carries its icon and
    its cooldown sweep, the alt plate carries its icon and the footer already says "LMB cast /
    RMB blast" and "SHIFT blink" every frame, and the character sheet (F1) lists both weapon names in
    full. The item name is the one that is new, unbounded in length, and absent from the sheet
    until you go looking for it - so it is the one that gets the space.

    The row is drawn as a single centred line rather than under the middle plate, because the middle
    plate is 47px wide and "Lantern Friend" is 92px. Anchoring it to the plate is what produced the
    original overlap; letting it use the row is what fits it at 12px with 8px to spare either side.

## [h:70-view-58]
near: `const warden=liveBossInRoom();`

The Warden's bar, drawn here rather than in drawRoom so it sits with the other HUD plates and
    inherits the same frame. It is drawn LAST so it is over the plates if the layout ever puts them
    in the same place, and it is drawn only while a boss is actually alive in THIS room - a bar for a
    boss that is not here would be a lie about the current fight, and the depths after the boss room
    would carry it forever if nothing took it away.

## [h:70-view-59]
near: `const tagCache={};`

the seed tag ------------------------------------------------------------------------------
  A luggage tag: chamfered corner, punched hole, the word stamped above the number.

  It is the one thing on screen whose entire job is to be READ OFF and sent to somebody else, so
  it is drawn as a physical object you could pick up rather than as a line of text floating on a
  background. Baked per text and cached, for the reason paperTex had to be: an object re-baked from
  a random stream every frame shimmers rather than sitting still, and a tag is read - not glanced
  at - so it has to hold perfectly still while it is being read.

## [h:70-view-60]
near: `function stampRight(g,text,rx,y,size,color,gap){`

Right-aligned, for the sheet. stampText CENTRES on its x, and canvas has no tracking-aware
  measureText, so centring a value where every other row is flush to the right margin hangs it
  half its width over the paper. The first version of this row did exactly that and the seed read
  "000039" instead of "000039U" - a truncated seed is worse than no seed, because the missing
  character is the difference between a dungeon that replays and one that does not.

## [h:70-view-61]
near: `stampText(g,text,w/2+12,h*0.62+1,h*0.40,'rgba(255,214,160,0.32)',3.6);`

sized off h rather than written in, so the tag cannot be resized without the type coming with
it - which is how the number and the caption ended up sitting on top of each other.
The number is filled twice, a pixel apart: a pale one down-right, then the ink over it. That one
extra fill is the whole difference between PRINTED on a tag and PRESSED into one, and a tag is
only worth drawing at all if it looks like it has been through something.

## [h:70-view-62]
near: `function drawDescent(){`

THE DESCENT BEAT. Drawn over the room fade, during the one moment in a run where nothing is
  trying to kill the player.

  It earns the 0.9s fade that descend() asks for, and that fade is the reason it is here rather than
  somewhere in the HUD: the transition between floors is the only place in the game where the player
  is guaranteed a clear look at the screen, and a beat that exists only to show a number has to use
  it or the beat is wasted.

  The line under it says what is actually about to change, because "FLOOR 7" alone does not tell a
  player that the thing they are carrying is about to matter more. The three numbers are the three
  levers of the depth ladder, stated as ratios against floor 1 so they are readable without knowing
  what any of them mean internally.

## [h:70-view-63]
near: `const scrimTop=H/2-92, scrimBot=H/2+68;`

THE SCRIM, and it is here because of the one thing the fade above cannot do.

    roomFade dims the WHOLE frame uniformly, so it dims these four lines exactly as much as it dims
    the room - it cannot make one more legible than the other. Meanwhile the player is placed at the
    room's centre, and every room on a floor is centred on the canvas, so in EVERY descent the
    player is standing on the middle line of this banner, with the wand flash beside it. A uniform
    fade leaves that collision precisely where it was, only darker: the screenshot of floor 5 shows
    "floor 4 -> 5" with the player's hat sitting on the 4.

    So the beat carries its own ground - a band that is flat behind the type and falls off above and
    below it, which lifts the four lines off whatever they are printed over without drawing a
    visible box on the screen. FULL WIDTH rather than fitted to the text, because the levers line
    is the widest of the four and a band that tracked the widest line would still leave the narrower
    ones floating on the room at the ends. Cheap: it exists for the length of one descent and is a
    gradient built per draw while descendT is live, which is a fraction of a second.

## [h:70-view-64]
near: `const scrimTop=H/2-92, scrimBot=H/2+68;`

The band grew when the area name arrived at a boundary: five lines need more room than four, and
    the rule under the last one is now at H/2+50, which was the old scrimBot exactly - so the rule
    itself would have sat on the fade-out. scrimTop moves up 18px to keep the area name (H/2-42)
    well inside the flat, and scrimBot moves down 18px to keep the rule (H/2+50) inside it too.
    The ramps keep their 0.22/0.74 shape, so the fully-opaque region is now roughly y 249..332.

## [h:70-view-65]
near: `ctx.save();`

THE PLAYER IS REDRAWN OVER THE SCRIM, and this is the fix for the thing the scrim itself caused.

    The scrim exists because the player is pinned to the exact centre of the canvas on every descent
    - the camera clamp takes its "room fits on screen" branch for a 450px room in a 600px canvas, so
    camY is (130+580-600)/2 = 55 and the room's midpoint lands on y 300 = H/2, with the four banner
    lines centred on W/2. Measured: the player's near-white sprite reads 236,232,245 before the scrim
    and 46,44,46 after it, so the character is knocked to 17% of its brightness for the length of the
    beat. The type is legible; the player is a grey smudge standing on it.

    So the sprite and its wand are put back, at full brightness, over the top of the band. Not the
    whole player pass - the lag ghost and the ground shadow stay under the scrim, because they are
    part of the room, and drawing them again would double them. The wand and muzzle flash come with
    it, because a character pointing at nothing while the banner is up reads as a broken frame.

    This is one extra sprite draw for the length of one descent (FADE_DESCEND, 0.9s), inside the same
    guard that already draws the banner.

## [h:70-view-66]
near: `const readyProg2=readyT>0?1-readyT/READY:1;`

`readyProg` IS RECOMPUTED HERE, and it has to be. It is a `const` local of drawRoom() - the
      room's own arrival hop - and drawDescent() is a different function, so reaching for it would be
      reaching across a scope boundary into a variable that is undefined here. Reading it threw
      ReferenceError: readyProg is not defined, which kills render() and with it the rAF loop, so
      the game froze permanently.

      It was not reachable in normal play and that is the part worth recording: `descend()` always
      lands the player in the new floor's START room, which generateDungeon builds already spawned
      and empty, so `enterRoom` never sets `readyT` during the 0.9s the banner is up. Measured 160,000
      ticks of real random-walk play across 40 seeds: zero frames with both `descendT>0` and
      `readyT>0`. Forcing the overlap by hand reproduces the throw immediately.

      So it was a loaded gun rather than a live bug - and a loaded gun is still a defect, because the
      one line that makes it live is a single `readyT=READY` added to some future path, and nothing
      here would look wrong. The hop and the wand glow are recomputed exactly as drawRoom computes
      them, from the same two constants, so the redraw cannot disagree with the room pass about what
      the player is doing.

## [h:70-view-67]
near: `const aim2=mouseWorld();`

The aim angle is recomputed rather than read from the room pass: `a` is a local of drawRoom()
      and this is a different function, so reaching for it would be reaching across a boundary into
      a variable that only happens to be in scope while nothing has re-entered. It is two lines and
      it cannot go stale.

## [h:70-view-68]
near: `const fromArea=areaForFloor(descendFrom||f-1), toArea=areaForFloor(f);`

THE FLOOR NUMBER IS STRUCK IN THE AREA'S ACCENT, like the depth numeral, so the beat and the
    plate agree about where you are. It was its own literal (`#e8b06a`), which was Area1's accent
    written out in a second place - the exact arrangement that lets two things that mean the same
    number drift apart, and which would have meant four accents in one file after the next area
    was added.

    AND THE AREA IS NAMED AT A BOUNDARY, which this beat could not do until `areaForFloor` took the
    floor as an argument. The question is "did this descent cross a boundary", which means comparing
    the area being left with the area being arrived in - and the only function that maps a floor to an
    area used to take no argument and read `run.floor`. So the three available answers were all bad:
    write the 4/8/12 thresholds out a second time here (two copies of a ladder boundary, and change
    one and the banner names the wrong area); temporarily assign `run.floor` to ask it (a draw
    function mutating simulation state, which this project rules out outright); or leave the banner
    saying only the floor and let the palette imply the area, which is what it did.

    The core change was one optional parameter. Now the beat asks `areaForFloor(f)` about two floors
    at once, touches nothing, and the thresholds live in exactly one place.

    It names the area ONLY on a boundary, because that is the only time the name is news. Every
    descent is already saying which area you are in by looking like it; repeating "Area2" on floor 6
    through 8 would be a caption for a caption.

## [h:70-view-69]
near: `ctx.fillText(an.name,W/2,H/2-42);`

INSIDE THE SCRIM, which is the whole difficulty of placing a fifth line. The band runs
      scrimTop H/2-74 to scrimBot H/2+50 with a flat 0.90 only between 0.22 and 0.74 of its height,
      so the opaque part is roughly y 253..318. The first draft put this line at H/2-52 = 248 -
      five pixels ABOVE that, in the 0.50-alpha ramp - and a screenshot showed "THE KILN WORKS" in
      near-invisible grey over the dusty floor.

      Which is the argument for looking at the picture and not only at the numbers: the pixel test
      passed, because it asked whether the ink was in the area's ACCENT and the answer was yes. It
      never asked whether the ink was legible, and a pixel can be the right colour and still be
      unreadable.

      So the line goes at H/2-42, inside the flat, and the FLOOR numeral moves down to H/2-14 to
      keep the gap between them even. Everything below shifts 20px to stay clear of the new line.

## [h:70-view-70]
near: `let s='deepest floor: '+records.deepest+' · best rooms explored: '+records.rooms;`

Depth first, because it is the number the game is about now. Rooms explored and dungeons
cleared are both historical - they describe a version of the game where one dungeon WAS the
game - and they stay because they are still true and a player who has them set should not lose
them, but they are no longer the headline.

## [h:70-view-71]
near: `function showNum(v,dp){`

A DISPLAYED NUMBER, at the precision the reader can act on.

    `toFixed(2)` on a derived value prints the binary rounding error along with the number: a per-pellet
    damage of 2.6 * 1.0 comes out as "2.60" by luck, but 0.5 + 0.66 and 1.8 * 0.8 produce "1.1600000000000001"
    and "1.44" from the same expression. Those digits are not information - they are the mantissa, and
    a player reading a damage panel is reading a number they intend to compare against a fight.

    So every player-facing number goes through here, with the precision chosen per quantity rather than
    applied as a blanket default: a tenth where the number is a rate or a time, none where it is a
    multiplier the player reads as "roughly double". The value is NOT rounded in the simulation - this
    is presentation, and rounding a hit to 1 damage would be a balance change.

## [h:70-view-72]
near: `function fmtHearts(half){`

HEARTS, for the player. Rounded to the nearest HALF heart, and that is the whole argument.

    Health in this game is measured in half-hearts and displayed as hearts, so a half-heart is the
    smallest thing the player has ever been shown - quoting anything finer than that is quoting a
    number the interface has no way to represent. The damage that goes in is genuinely fractional:
    LUNGER_PAY 0.96 through ARMOUR 0.66 is 0.6336 half-hearts a hit, accumulated over a floor, so an
    unrounded total reaches things like "10.625 hearts" and "17.375 hearts" - which read as a bug
    rather than as a measurement, and invite the player to check the arithmetic instead of reading
    the result.

    NOT rounded at the simulation. Damage stays exact; only the display is rounded. Rounding health
    itself would make every hit worth at least one heart, because the smallest hit in the game is
    under half a heart - that is a balance change wearing a formatting costume, and this game's whole
    difficulty ladder is fractional on purpose (ARMOUR 0.66, LUNGER_PAY 0.96, TEMPO.rate).

    Rounded to the half rather than the whole, because a heart with a half in it is what the game
    already draws: the heart plate has an armour row of half-slots.

## [h:70-view-73]
near: `function devLayout(){`

the weapon bench (F1) ------------------------------------------------------------------------
  A panel for swapping guns mid-playtest without going back to the title screen for each attempt.

  WHY IT EXISTS, in one line: weapon balance could not be judged because every attempt at a new
  gun cost a run, and a run costs twenty minutes. The numbers below are the ones the balance
  argument is actually about, so the panel shows them rather than making the player remember them.

  THE NUMBERS ARE LIVE. Every figure reads the current build - the weapon's own damage PLUS
  Stats.value('strength'), which is the term that decides how a gun behaves under a buff. A bench
  that printed each weapon's own damage would have said the Arcane Beam was weak when the real
  complaint is that a flat +4 Strength nearly triples it while it barely touches the Bolt. Showing
  the buffed figure next to the base one is the whole point of the panel.

  THE GEOMETRY IS COMPUTED ONCE, here, and both the drawing and the click handler read it. The
  boss gate was broken for a week because the gate and the doorway each had their own copy of the
  same rectangle and they drifted apart; a hit box that is calculated a second time at the click
  site is that exact bug waiting to happen, and it fails silently - the button draws, the click
  lands somewhere else, and it looks like the game is not listening.

## [h:70-view-74]
near: `const pw=608, ph=464, px=Math.round((W-pw)/2), py=Math.round((H-ph)/2);`

Every vertical position in the panel is a band measured from the top of the card, and the card is
    tall enough to hold them all with air between. The first version of this panel overlapped itself
    three ways - the subtitle ran into the held-weapon block, the column headers sat on the first row,
    and the footer fought the ESC hint - because the numbers were written into each draw call as it
    was needed instead of being budgeted once. So they are budgeted once, here, and every band is
    named. Nothing below is allowed to invent a y coordinate.

      head    the title, and what is held, on the same baseline
      sub     two lines of explanation, clear of the head's right-hand column
      colHdr  the column titles, above the rule and above the first row
      rows    four of them, 60px each
      foot    the keys and the build readout, below a rule

## [h:70-view-75]
near: `const footRule=py+370, foot=py+386;`

The footer band is measured from its TALLEST element, not its rule. It used to be budgeted from
the rule, which put the ESC key cap three pixels past the bottom edge of the card - and the gap
check that should have caught it was measuring the rule too, so it agreed with itself. The two
tallest things down there are the caps (17px) and the baseline 13px under the last one.

## [h:70-view-76]
near: `const spreadRad=w.spreadFromPrecision?preciseSpread(w.spread):w.spread;`

THE CONE, IN DEGREES, ONCE.

      This read `spread*spread` - it multiplied the spread by itself and then squared the result, by
      the look of it - so the Arcane Beam's 0.16 rad printed as 0.1 degrees instead of 18.3, and
      three of the four guns printed 0.0. The column existed to answer "can I aim this", and it
      answered "they are all the same, and all zero". It is now the full opening angle in degrees,
      which is what the label says and what a player can feel the difference between.

## [h:70-view-77]
near: `const base=w.dmg*w.count;`

STRENGTH IS ADDED ONCE PER SHOT, so this is base + str and not base + str*count.

      The game does `count*dmg + strength` and shares it across the pellets (40-combat.js, with a long
      note on why: a flat +4 is +57% on the Bolt and +476% on the Beam, so a per-pellet bonus would
      have made the shotgun eight times stronger per sigil than everything else). The panel added the
      Strength once per PELLET, so the Scatter showed 55.2 where the game deals 34.2 - the bench told
      the player the gun they already own is 60% stronger than it is, on the screen whose entire job
      is helping them choose.

## [h:70-view-78]
near: `const lunger=ENEMY.lunger;`

TIME TO KILL A LUNGER, AGAINST THE LUNGER THE GAME ACTUALLY HAS.

      This divided by `18*TOUGH` - the lunger's health before it was reduced - and by nothing else, so
      it ignored ARMOUR entirely. Both halves were wrong in the same direction, and the bench said a
      gun kills a tanky armoured body faster than it does.

      Both are now read from the table rather than written down, so a body buff or a health change
      cannot leave the panel quoting a number the game is not using. ARMOUR is applied because every
      shot pays it; it is also the reason the figure is a FLOOR rather than a promise, since a shotgun
      at range is not landing every pellet - which the panel says nowhere, and should.

## [h:70-view-79]
near: `const fy=G.foot;   // the band devLayout budgeted, not a second guess at where the footer is`

footer: the two keys and a heal - IN THE LAB ONLY, because a playtest that has to be restarted
every time you spend your charges is a playtest you stop doing. Outside the lab there is nothing to
advertise: granting a key or a heart during a real run is not a shortcut, it is a cheat with a
label on it, and this panel is reachable from any game at any depth with F1.

## [h:70-view-80]
near: `function devDps(w,str,dist){`

hearts per second for a weapon at a range, under the current build. This is the number the balance
  argument is about and the panel exists to show it, so it is derived from the same terms the game
  fires with: base damage plus Strength, times pellets, times rate, times the same falloff the
  projectile itself uses.

## [h:70-view-81]
near: `const SUMMARY_MARGIN_X=36,   // inner text margin from each edge of the card`

THE CARD'S SHAPE, decided by its contents rather than by a number someone wrote down.

  The card used to be `pw=440, ph=380` - three literals - while everything inside it was a runtime
  stack of rows at 26px steps. Sixteen rows, a seed line, a rule, a RECORDS heading and four more rows
  come to a last baseline of y 534 against a card bottom of y 512, so "Dungeons cleared" was printed
  on the wood BELOW the paper. Twenty-two pixels of overflow, and nothing anywhere was wrong: the
  arithmetic was correct and the box was a guess.

  That is the whole failure mode - a height that is a constant beside content that is computed. It
  cannot be caught by reading, it survives every test that does not measure the last row against the
  card, and it comes straight back the moment somebody adds a statistic. So the rows are collected
  first, their extent is measured, and the card is sized to fit them.

  The constants are now named rather than scattered through the function, because five different
  increments (26, 20, 26, 24, and the 2px rule) inside one draw call is a layout nobody can change
  without re-deriving it, and this card will be changed again.

## [h:70-view-82]
near: `SUMMARY_SEED_AIR=10,`

Air AROUND a structural break rather than inside it. The seed gets a little because it is a
        different kind of line and the gap is what says so; the rule and the RECORDS heading get a
        lot because they separate two groups and the card would read as one long list without it.

        The first version of this drew the heading at y+RULE_GAP+14 while giving its item only
        RULE_GAP*2+2 of height, so the heading's baseline fell 6px past the end of its own slot and
        printed through the first record row. The fix is structural rather than a bigger number:
        every item now draws against the y it is given, and its height is the air plus the text.

## [h:70-view-83]
near: `const contentH=items.reduce((a,it)=>a+it.h,0);`

TWO ROWS THAT COULD NOT EVER CHANGE, REPLACED BY TWO THAT DO.

    "Fastest clear" and "Dungeons cleared" were written when killing the boss ended the run, and they
    printed `records.fastest` and `records.wins`. Nothing increments those any more - a cleared boss
    room opens a way out and the run continues - so `endRun(true)` has no callers left and both rows
    printed 'not yet' and '0' on every summary in the game, forever. A record the player cannot move
    is not a record, and a permanent zero reads as "you have done nothing" rather than "this line is
    from a version that no longer exists".

    WHAT REPLACES THEM. The first attempt added "Bodies killed" and "Accuracy" - which fixed the
    unchangeable pair and immediately created a different problem: both were already on the sheet
    higher up, as "Enemies defeated" and as an accuracy row that also shows the raw hits/shots. The
    card then printed the kill count twice and the accuracy twice, in two different formats, once as
    "42% (17/40)" and once as "42%". Two numbers that must agree, formatted differently, on the same
    card, is worse than either one alone: a reader comparing them has no way to tell whether they are
    meant to differ.

    So nothing is added here. The two rows that could never change are gone, the two rows that
    replaced them turned out to be duplicates of rows already higher up, and what remains is four
    records that all move and none of which appears twice. Accuracy and kills stay in the run
    section, once each, in the format that carries the raw numbers with the percentage.

## [h:70-view-84]
near: `const titleH=108;`

The card is CENTRED VERTICALLY on whatever space is left under the title, rather than placed at a
    fixed y. A fixed y worked while ph was a constant and stops working the moment the content is a
    variable - and a card that is 440px wide on a 960px canvas but 500px tall on a 700px canvas will
    run off the bottom if its top is a literal. Both edges are now computed from the measured height,
    so a longer card grows in both directions and stays inside the canvas.

## [h:70-view-85]
near: `ctx.fillText(s.won?'press R to descend again':'press R to try again',W/2,Math.min(H-14,py+ph+34));`

"press R for a new dungeon" was correct when a dungeon was the whole game. The run now ends only
by dying, and what R starts is a fresh run at floor 1 - which is a different thing to ask for and
worth saying plainly, because the player has just spent an hour going down and the prompt used
to imply that was the shape of a completed run.

## [h:70-view-86]
near: `updateCamera();`

THE CAMERA, applied to the ROOM AND NOTHING ELSE.

      The transform is inside a save/restore pair that closes before the HUD, so the HUD, the
      minimap and every overlay draw in screen space exactly as they always did. A HUD that scrolled
      with the room would be a HUD that walks off the corner of a big room, and a minimap that
      scrolled would be a minimap of a window rather than of the floor.

      For every room that fits on screen this translate is the identity, because the camera clamps
      to the room's own origin. That is deliberate: it means the camera can be added to a game that
      is already green without any of its existing numbers moving, and the only thing being tested
      on day one is a transform that is currently a no-op.

      It is computed HERE, immediately before it is used, rather than in the tick. That is the
      lesson of every stale-derived-value bug in this file: a value updated somewhere else is a
      value that can be wrong at the moment it matters, and the first symptom is a one-frame
      offset nobody can reproduce. Computing it at the point of use makes it impossible to be stale,
      and it is one subtraction and two clamps.

## [h:70-view-87]
near: `Lab.drawLegend();`

The lab's legend is screen space, so it is drawn out here with the HUD rather than in the room
      pass - see Lab.drawLegend for why it does not scroll with the world.

      It is drawn AFTER the HUD, and that ordering is load-bearing rather than incidental: the two
      share the bottom strip of the screen whenever a Warden is on the row, and the legend gives up
      the 6px it overlaps by rather than the bar giving up its fixed place. See Lab.legendLane for
      the measurement and for why neither of them can simply move.

## [h:70-view-88]
near: `if(devOpen) drawDevMenu();`

also over the fade, and for the same reason: the fade is what it is drawn on
the bench is drawn over everything, after the fade and the boss warning, because it is the one
thing that has to be readable at any moment - and it carries its own dim, so the PAUSED
overlay beneath it would be a second dim layer and two of those read as a rendering fault

## [h:70-view-89]
near: `if(paused||devOpen){acc=0;return 0;}`

panel the player READS, and a fight that keeps running underneath numbers they are trying to
read is a fight they lose for having tried to understand the weapon. It is deliberately not
routed through setPaused, because that opens the character sheet and two cards stacked is worse
than either alone.
