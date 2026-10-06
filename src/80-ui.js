/* 80-ui - boot, input, and the DOM overlays Also replaced by Unity, but one idea is worth carrying across: [h:80-ui-1] */
mouse={x:W/2,y:H/2}; mouseDown=false; altMouseDown=false; keys={}; state='start';
loadRecords();

/* Pressed buttons live in a set rather than two booleans, and every route by which a mouseup can be swallowed (native context menu, pointercancel,... [h:80-ui-2] */
const held=new Set();
function releaseButtons(){held.clear();mouseDown=false;altMouseDown=false;}
function trackButton(b,down){
  if(down) held.add(b); else held.delete(b);
  mouseDown=held.has(0); altMouseDown=held.has(2);
}


/* Every bug this build has had fixed, grouped, with the one-line reason it is pinned at all. [h:80-ui-3] */
const FIXES={
  /* ---- frame and input ---- */
  'mouse maps to canvas pixels inside the 2px border':['frame and input','the cursor was read against the integer clientWidth while the canvas was laid out on fractional pixels, so the nearest half pixel of pointer travel aimed off the left of the screen - invisible above 1280x720, which is why it lived'],
  'the descent banner does not bury the player, who stands at the centre of it':['presentation','the scrim that made the descent type legible dropped the player sprite to 17% of its brightness, because the camera clamp pins the player to the exact centre of the canvas on every descent and the banner is centred there too'],
  'starting a run clears the art caches, so one seed cannot speckle another seed\'s floor':['runs and records','the cave, wall and floor tiles are baked from the seeded art stream, so a cache that outlived its run painted the new run with the old run\'s speckle - and the cache key cannot see it, because the seed is not in the key'],
  'fixed timestep: 2s of wall clock runs the same ticks at 30 to 240Hz':['frame and input','the simulation advanced a different number of times depending on the monitor, so the same fight was a different fight'],
  'fixed timestep: a jittery 60Hz timer keeps a steady 3.5 updates per frame':['frame and input','a stuttering timer used to make the whole game stutter with it'],
  'fixed timestep: a 5s hitch replays at most 250ms':['frame and input','one slow frame used to teleport every enemy across the room at once'],
  'pause: Esc and P toggle it and nothing advances while paused':['frame and input','enemies kept walking while the game was paused'],
  'pause: a click resumes without casting':['frame and input','un-pausing with the mouse fired the wand as a side effect'],
  'pause: blur and hidden tab release held input and pause':['frame and input','alt-tabbing away left the keys held down and the run playing itself'],
  'pause: not available outside a run':['frame and input','the pause key did nothing on the title screen, which read as a dropped input'],
  'R restarts only while paused or after a run':['frame and input','R wiped a live run by accident'],
  'Space blinks like Shift':['frame and input','the second blink key was documented and not implemented'],
  'releasing both buttons at once cannot leave the wand firing':['frame and input','a fast click-and-drag latched the wand on permanently'],
  'mouse maps to canvas pixels inside the 2px border':['frame and input','the aim was offset by the frame thickness, so shots landed next to the cursor'],

  /* ---- weapons and damage ---- */
  'weapons: every gun kills a lunger fast at the range it is meant to be used at':['weapons and damage','every gun used to be usable everywhere, which is the same as no gun being right anywhere'],
  'every gun loses damage with range but stays worth using':['weapons and damage','a gun that does not fall off is a gun with no range, and a room has one'],
  'weapons actually do less damage to a far target':['weapons and damage','the falloff was drawn on paper and not actually in the damage'],
  'a hit slows the body and the HP increase pays for it':['weapons and damage','hitting something was pure upside, so there was never a reason not to shoot'],
  'the blast shares one damage budget between everyone it catches':['weapons and damage','a crowd was killed by a flat split, so more bodies meant more damage instead of less'],
  'the blast shoves hard up close and barely at all across the room':['weapons and damage','the shove was flat, so the blast was repositioning you the same way everywhere'],
  'the blast wounds the smallest Brunch group instead of deleting it':['weapons and damage','one click deleted a 4-pack, which is the most common thing in the game'],
  'right-click blast detonates on the first body it touches, no phasing':['weapons and damage','the blast flew through bodies to the cursor, so the weapon removed nothing'],
  'the blast detonates on the first body it touches, and the hook does not':['weapons and damage','a tie-break meant for piercing bolts was applied to the blast, which made it phase through the whole room'],
  'a bolt is resolved from the weapon it was thrown with':['weapons and damage','swapping right-clicks mid-flight made a hook detonate as a blast, shoving where it should have pulled'],
  'the voidball drills through a line of bodies and is the worst gun against one of them':['weapons and damage','the Voidball was a marginally better Bolt with no other identity, so there was no reason to pick it'],
  'the pierce discount follows the order the bolt actually reached bodies':['weapons and damage','the discount followed array order, so a line took less damage at the front than the back, invisibly'],
  'the bolt visibly spends itself as it travels, and only visually':['weapons and damage','the falloff was invisible until it landed, which is a number the player has to memorise'],
  'every weapon, spell and icon is the colour its own projectile is':['weapons and damage','the icon lookup defaulted to the blast, so the Bolt was drawn orange while the wand was violet'],
  'a hit tints an enemy instead of painting it white':['weapons and damage','a flashing enemy lost its own colours for a frame, which reads as a rendering fault'],
  'a shell is a chip off a lunger and knocks a body back without launching it':['weapons and damage','a shell could fling a lunger off the map at close range'],

  /* ---- the right click ---- */
  'the hook goes off exactly where the cursor was, deals nothing, and yanks bodies in':['the right click','the hook fired from a stale cursor position'],
  'the hook gathers what it caught into a knot, and it is what waits behind the fake wall':['the right click','a flat pull strength sent bodies through the point and barely moved ones caught close, so the crowd scattered instead of knotting'],
  'the hook leaves a ground spell that holds, drains and grinds':['the right click','a drag that scatters the crowd and lets it walk straight back to you buys nothing'],
  'a second right click detonates the hook in flight, and cannot be farmed':['the right click','you could not pull early, and a held button blew the hook up at your own feet'],
  'the hook cancels on a right click WHILE it is still on cooldown':['the right click','the cancel was behind the cooldown gate, and the hook cooldown outlives a long cast, so the button was on cooldown for most of the flight and the cancel silently did nothing'],
  'the shockwave wears the right colour and travels the way the weapon acts':['the right click','both right-clicks used the same orange expanding ring, so the hook looked like it was shoving'],
  'the secret is walled off, off the map, and only the right click opens it':['the right click','the secret could be reached and was visible before it was earned'],

  /* ---- enemies and AI ---- */
  'a lunger lunges, and it can be dodged by reacting to the tell':['enemies and AI','lungers homed on you directly, so counterstrafing - the answer to the gunners - was the worst possible move against them'],
  'a hook cancels a lunge outright, and stars mark the bodies it holds':['enemies and AI','a stun skipped the state machine without clearing it, so the glow stayed up, the aim line stayed drawn, and the attack you had already dodged landed anyway'],
  'a gunner telegraphs before it fires, and the tell previews the shot':['enemies and AI','a shell left the instant the cooldown ran out, so the only counter to a gunner was not being there - and the flash meant to warn you was drawn in the projectile loop, where a gunner never appears, so it was never visible at all'],
  'a gunner is slower and heavier than a shooter, and pays for it':['enemies and AI','the two gunners were the same gun at two sizes, so there was no reason to want the big one'],
  'Brunch: tiny, quick, in a knot, and the big packs are the rare ones':['enemies and AI','pack size was uniform, so a room could roll eight Brunch as often as four'],
  'a Brunch spends itself on touching you, and a pack eats itself':['enemies and AI','a knot of Brunch was a damage clock that never stopped, and i-frames shielded the pack from ever paying for a hit'],
  'the gunner is tougher than a shooter and slips, but only from range':['enemies and AI','the gunner was a slower, bigger shooter, so it had no answer of its own'],
  'the gunner is the shooter cloned: slower, double damage, bigger':['enemies and AI','the two gunners had drifted into the same body with different numbers'],
  'the gunners notice you well before they will shoot, and fight at close quarters':['enemies and AI','gunners shot from across the room at anything that moved'],
  'a hit wakes the room from any distance':['enemies and AI','a body you hit across the room did not know it had been hit'],
  'lungers that reach the player keep their bodies apart':['enemies and AI','a whole pack could stack on one pixel and land as a single hit'],
  'separation keeps a lunger pinned in a corner inside the room':['enemies and AI','pushing bodies apart could shove one out through a wall'],
  'the spawn plan spreads bodies out and keeps gunners off the entry door':['enemies and AI','bodies spawned in a knot or in the doorway, so the room decided the fight before you moved'],
  'counterstrafing beats a straight line, and a straight line is punished':['enemies and AI','the gunners led their shots perfectly, so moving well made their aim better and there was no way to dodge a straight line'],

  /* ---- movement and being hit ---- */
  'being hit is where the model is, and it is not the whole model':['movement and being hit','the hitbox sat across the hood and stopped at the waist, so you could not be hit for anything that reached your legs'],
  'a blink leaves the enemy targeting the old spot for a moment':['movement and being hit','a gunner that was already tracking you got a free intercept shot the instant you vanished'],
  'a blink is invulnerable for the whole move, not just the first frame of it':['movement and being hit','the invulnerability window started and finished inside the jump, so the half of the blink that actually happens afterwards was fair game'],
  'blink: charges come back faster, and faster still in a quiet room':['movement and being hit','blink charges only came back on a slow timer, so the escape tool ran out mid-fight'],
  'clearing a room still sprints the blink bar for the walk out':['movement and being hit','walking to the next room cost you the dodge you needed for it'],
  'walking into a live room hands back the whole kit, once per room':['movement and being hit','re-entering a fight could refill everything at once, so the room could be reset by stepping out and back'],
  'losing your last point of health ends the run, and nothing on the floor can save it':['movement and being hit','dying could be undone by a heart on the floor, so the health bar was not the source of truth'],
  'a zero-health player is never still playing, whatever the tick was doing':['movement and being hit','an early return could skip the death check, leaving a dead player running'],
  'fractional damage can never leave you alive on an empty health bar':['movement and being hit','repeated fractional subtraction landed health on 6.66e-16, which is not zero, so the death check never fired'],

  /* ---- rooms, keys and getting further ---- */
  'a seed replays the same FIGHT, not just the same dungeon':['rooms and progression','two cursors lived at module scope and survived startGame, so the second run from a seed began part-way round the flank walk. Same rooms, same bodies, every lunger circling from a slightly different angle - the first fight of a session played differently from the second, with no seed change to explain it'],
  'every dungeon: a branching tree with a reward at each of two ends, and a key in a branch':['rooms and progression','the map had loops in it, so a run could be all backtracking and no depth. The name of this entry drifted when the test was rewritten - it read "four winding runs" and matched nothing, which is how a pinned fix ends up looking verified while nothing checks it'],
  'the critical path is walkable: silver to the upgrade, gold to the boss':['rooms and progression','a generator could produce a floor whose key room could not be reached'],
  'the two keys gate different doors and are each spent once':['rooms and progression','one key could open both doors, so the progression was one gate with two props'],
  'clearing the two key rooms pays out the right metal':['rooms and progression','the two key rooms paid the same metal, so the boss door could open before the upgrade room did'],
  'a touch of the door starts the unlock and it finishes on its own':['rooms and progression','standing on the door did nothing, so unlocking needed a frame-perfect click'],
  'item room: two different weapons, never the one you hold':['rooms and progression','the upgrade room could offer the gun you already had, which is a dead pickup'],
  'weapon pickup swaps, and waits for you to step off':['rooms and progression','picking up a weapon could ping-pong forever with the one you dropped'],
  'hearts and armor stay on the floor when you are full':['rooms and progression','a full player deleted a heart instead of leaving it'],
  'blast kills go through killEnemy (kill count and loot)':['rooms and progression','kills were counted in one place and loot dropped in another, so the two disagreed'],
  'killing the boss opens a way out instead of ending the run':['rooms and progression','the results screen was up before you could decide whether the run was over'],
  'the boss warns you once, when it first shows on the map':['rooms and progression','the boss warning was re-shown for every room on the path to it'],

  /* ---- HUD and interface ---- */
  'the HUD is laid out as one block, and every plate is derived from the same margins':['HUD and interface','every plate was hand-placed against its neighbour, so raising max hp left the key plate floating in the middle of nowhere'],
  'the bug list opens on a key, in a normal game, with no query string':['HUD and interface','the change history was locked behind a test URL, so the one reader who needs it during a normal game is the one who cannot reach it'],
  'the controls sheet and the bug list take the keyboard away from the game':['HUD and interface','a DOM overlay on a canvas game is a click that casts and a key that walks you into a Brunch - and a suppressor that takes too much swallows the key that closes the overlay, which is a cancel you cannot reach, the same bug the hook cooldown gate was'],
  'a gunner shoots the gap it found, not the gap it wanted':['enemies and AI','the aim search sweeps a cone and returns the angle it cleared, but only its truthiness was being read - so whenever the straight line was blocked and a few degrees off was not, the gunner charged the blocked line and put the shell through the very Brunch it had just avoided'],
  'a lunge that cannot reach waits, and one that can is never dawdled with':['enemies and AI','distance is a reason the attack has not started, not a reason it fails; a lunger that commits past its own reach reads as the attack breaking for no reason the player can see'],
  'a lunger holds its distance instead of walking into you, and lunges across the gap':['enemies and AI','a lunger with no standoff closed the last thirty pixels and then lunged from zero range, where no read is worth anything because there is nothing left to dodge - every measurement of this attack came out a hundred percent for that reason, and not because the prediction was good'],
  'a pack of lungers arrives around you, not in a line':['enemies and AI','every lunger steered at the player exact position, so a pack came as one front - one line, one angle, one threat to read. The bodies now hold slots on a ring, spread by the golden angle so no two ever share one'],
  'two lungers that lunge into each other both come off worse':['enemies and AI','a lunge is aimed at a position, so two bodies reading the same player in the same instant were aimed at the same point and flew through each other; putting yourself between them is a real play and it now pays'],
  'a gunner in your face hits a straight line AND a counterstrafer, and only far away misses':['enemies and AI','counterstrafing in front of a gunner was free, and the reason was arithmetic rather than tuning. The aim was not a lead at all - it was a claim about where the player would be, fired AT a shell that only covers so much ground, so every shot missed by the margin of the claim. The half second of visible charging was not counted either, so the gunner predicted a target that had already moved. And it solved all of this from where it stood when the muzzle lit up, while walking a hundred and twenty pixels during the charge it had just announced. It now solves a real intercept, from a muzzle that holds still to charge, at the part of the player the hit test actually uses'],
  'the hook is devastating once and a nuisance the third time, and bodies forget':['the right click','the hook was a permanent answer - land it, wait out the field, land it again, and a body never recovered. The resistance is per body and it decays, so the first hook is untouched and that keeps it the answer to a lunge'],
  'a lunge reads a settled heading, and a thrashing one is read as unreliable':['enemies and AI','aiming from the raw velocity aims at a player one tick into a keypress, who is still nearly stationary, and at a player mid-reversal, who is momentarily pointing the wrong way'],
  'a lunge is aimed at where you are going, and running in a straight line is a hit':['enemies and AI','the lunge led by thirty-two pixels a shot that needed two hundred and ninety-four, and stopped at a hundred and sixty-six - so it fell a hundred and twenty-eight pixels short every single time. Running in a straight line was not a risk to be answered, it was a guaranteed escape, and no amount of reaction could change it'],
  'a lunge cannot be dodged by ignoring it, and can be dodged by answering it':['enemies and AI','fixing the intercept without checking the reaction window would have swapped a free escape for an unavoidable hit, which is the same unreadable threat pointing the other way'],
  'the blink lands with momentum, and only along the way it went':['movement and being hit','the burst was a level bleeding a fraction per tick, so it lasted ten seconds instead of the tenth of one the comment beside it promised - and a flat gain meant you could blink away and immediately walk back at full speed, which is a free displacement rather than momentum'],
  'every body obeys one armour rule, and the table declares it':['enemies and AI','the Warden took 1.52x more damage per hit than anything else in the game, because spawnEnemy read c.armour||1 and the boss row simply never mentioned armour - and the comment beside that row said the omission was deliberate. Every hit landed harder on the boss than on an identical hit anywhere else, so its effective health was 19x a lunger\'s rather than the 29x its HP implied, and because the ladder multiplies health without limit the gap GREW with depth instead of staying a property of the body. The C# port had already taken the opposite answer (0.66, because an omitted double there reads 0), so the two ports disagreed about the boss and neither marked the other wrong. Armour is now stated by every row, applied by SIZE rather than by name - a shooter is exactly as wide as a lunger, so treating it as chip would have been arbitrary - and the Brunch stays unarmoured because it is half a lunger\'s radius, is what ARMOUR\'s own comment calls "a big body" when it excludes it, and armouing it broke the alt blast\'s promise that one budget deletes a small group: a blast into three Brunch left all three standing'],
  'HUD draws one heart per 2 max hp, and always the full set of armour slots':['HUD and interface','the health plate grew with health but the slots were placed by hand'],
  'the heart plate never reaches the minimap, however much health there is':['HUD and interface','Vigor is unbounded, so past maxHp 56 the plate grew over the map and hid which rooms you had seen'],
  'a negative stat prints as a number, not as nothing':['HUD and interface','a -1 Luck printed as "even", so Glass Wands reported its own cost as neutrality'],
  'both keys always sit in the HUD, dimmed while unheld':['HUD and interface','a key you were not carrying was simply absent, which read as a broken frame'],
  'the minimap reveals only rooms a door actually leads to':['HUD and interface','the map showed rooms that were merely touching you on the grid, with no way in'],
  'an uncleared boss room is marked on the map, and a cleared one is not':['HUD and interface','the boss mark was left behind after the fight was over'],
  'time and heart formatting':['HUD and interface','a formatted value could print 0:60.0 or a negative heart'],

  /* ---- runs, records and the frame ---- */
  /* This entry used to read "records: [h:80-ui-4] */
  'a run ends in death and in nothing else, so there is no win to record':['runs and records',
    'the way out used to call endRun(true) and end the game, and a slow win overwrote the best time. '+
    'It goes down a floor now, so the win path is unreachable - which means the honest claim is not '+
    'about saving a win but about there being no way to end a run early'],
  'records: dying on the boss-kill frame is a death, and deaths save rooms explored':['runs and records','a tie on the last frame was resolved the wrong way, so a death was scored as a win'],
  'run stats count shots, hits, kills, damage and time exactly':['runs and records','the numbers on the results screen did not match what the game did'],
  'room fade: entering with enemies eases in across the whole ready window':['presentation','the fade was computed as the complement of a smoothstep, which is flat at both ends and cut to black on the first tick'],
  'room fade: a cleared room snaps back in and a doorway eases to black first':['presentation','rooms faded in at different rates depending on whether they were cleared'],
  'room entry fades in over the ready window and locks the enemies out':['presentation','enemies could act during the entry fade, before the player could see them'],
  'every screen renders without throwing':['presentation','a screen that has not been opened in a build throws the first time it is reached'],
  /* rooms bigger than the screen ---- These are the first entries here for something no player ever reported: [h:80-ui-5] */
  'a room can be larger than the screen and the walls are that room\'s own walls':['rooms and progression',
    'room geometry lived in four constants read in 169 places. One shape was the only shape, so a room '+
    'of any other size had its walls drawn by half the game at the old size while the other half used '+
    'the new one - and the two agreed perfectly right up until the first room that was not 700x450'],
  'the camera follows the player across a big room and stops at its edges':['presentation',
    'the clamp on the big-room branch was Math.min(b.l,...) with the bounds transposed, so it pinned '+
    'the view to the room\'s left edge and the camera never moved at all. It passed the entire suite '+
    'because every room in the game fits on screen, so that line had never executed once: the very '+
    'property that made the camera safe to add - it is the identity transform for a room that fits - '+
    'was the property that hid a total failure inside it'],
  'the wand points at the cursor, in the frame the cursor is actually in':['weapons and damage',
    'the cursor is in screen space and every body is in world space, and the aim subtracted one from '+
    'the other, so every shot was off by the camera offset - by up to 21.28 degrees, and it changed '+
    'sign across the frame, which is why it read as a few degrees off from any one seat. The suite '+
    'passed throughout because every fixture wrote a world position into the cursor variable and the '+
    'game read a world position out of it: the test and the bug agreed perfectly'],
  'the cursor is a screen position and the game asks for it in world space':['HUD and interface',
    'the other half of that fix, and the one a looser check would have let through: the conversion '+
    'has to go the right WAY, and the weapon bench is the control, because it has always hit-tested '+
    'the same variable in screen space correctly'],
  'the separation grid finds the same pairs as the double loop, to within a knockback slide':['enemies and AI',
    'every pair of bodies was tested every tick, so cost per body climbed from 1.30us at five bodies '+
    'to 8.35us at a hundred and sixty - a quadratic wearing a linear costume, invisible only because '+
    'a room tops out near 23 bodies. A grid is only faster if it visits pairs in the same ORDER, '+
    'because separation moves both bodies, and the first version did not: 76px of divergence'],
  'the separation cost does not go quadratic as a room fills':['enemies and AI',
    'the shape of the cost, asserted rather than a millisecond budget, because absolute timings mean '+
    'nothing off the machine that wrote them and a test with a hardcoded ms figure gets deleted '+
    'rather than fixed. It reports a number either way and only fails if timings came back at all'],
  'the room-scaled numbers scale with the room, and do not move when it does not':['enemies and AI',
    'the aggro range and the swerve deadzone were both derived from the room size ONCE, at parse '+
    'time, back when there was one room shape. A 1680-wide room wanted an aggro range of 1567 and '+
    'read 707, and a gunner read a reversing player at full strength from across the room. In a '+
    'standard room they return exactly what they always did, which is what makes this a fix and not a retune'],
  'a lunger in a big room comes at you from across it':['enemies and AI',
    'the consequence and not the constant: a number can follow the room and still not be read '+
    'anywhere, and only a body 1100px away in a 1680-wide room can tell the difference'],
  'the ladder is exponential, climbs unbroken, and its steps GROW':['rooms and progression',
    'the ladder was 1+growth*n/(n+tau): monotonic but SATURATING, so the steps shrank and the curve '+
    'flattened into an asymptote of 3.6x that floor 14 had already reached 2.5x of. Every floor after '+
    'that was spent approaching a number it had nearly hit. Exponential, because a logarithm decreas'+
    'es its increments - steep early, flat late, the opposite of what the brief asks - while an '+
    'exponential increases them, so every floor costs more than the one before it'],
  'the ladder stays playable and readable at every floor, which is why two dials are capped':['rooms and progression',
    'health is unbounded as asked, but an unbounded ladder is not a harder game, it is a broken one. A '+
    'ranged body allowed the old 1.95x ceiling would close at 1.264 px/tick against a player who '+
    'moves at 1.20 - faster than the player, and a threat you cannot outrun is not one you lost to. '+
    'Uncapped density is 246 bodies by floor 40 and 1869 by floor 50, which is a hang. The old ceiling '+
    'was never wrong so much as unreachable: its own saturation never got there before content ran out'],

  /* the HUD reshape: [h:80-ui-6] */
  'the bar is sized to the SCREEN, so the Lab gets one too and the floor is unchanged':['HUD and interface',
    'the bar was fixed from load-time room constants, then moved to bossBarRect() - which measured the '+
    'room per draw, and in the Lab that room is 1680px wide on a 960px screen. So 750px of the bar, both '+
    'phase notches\' surroundings and the flush-right PHASE n caption were drawn off the side of the '+
    'canvas, in the one place the Lab exists to let you look at the bar. Asserting a WIDTH against the '+
    'room had gone green over both defects in turn. The floor bar is unchanged at 660px, because the room '+
    'still decides where it sits and only a room wider than the screen gives up its edges'],
  'the Warden has a readable bar in the Lab, above the legend and below the band':['HUD and interface',
    'the Lab legend was a fixed 26px strip at y 574..600 and the boss bar\'s frame ends at 580 - a 6px '+
    'overlap, with the legend drawn last, so the bar was the thing that vanished. In a room wider than '+
    'the screen, where a player goes specifically to read the Warden\'s phase. The bar does not move and '+
    'the legend does not move up (it is screen space over a world-space shelf); the legend gives up the '+
    '6px, and its chips are positioned from the lane rather than from a y that was correct at 26px'],
  'each area has a palette, and a floor is painted in the one it is on':['presentation',
    'an area was identity by enemy mix alone, and mix is not something a player can see before the room '+
    'has emptied itself. The palette is the other half: a floor of stone and a wall of coursed masonry '+
    'per area, read from the moment the room fades in. Area1 is byte-for-byte the build that existed '+
    'before areas did, because every number and every screenshot measured in this project was measured '+
    'on floors 1-4 - so a theming pass that quietly re-tinted the first four floors would have invalidated '+
    'all of it while looking like a feature. Its wash is ZERO for exactly that reason, not because it '+
    'was forgotten'],
  'the area is the same colour in the floor, the wall, the doorway and the HUD, and only there':['presentation',
    'the theming was applied one surface at a time, which is how a floor goes green while the doorway '+
    'beside it stays Area1 grey - a rectangle of the wrong stone punched through a themed wall, which '+
    'reads as a rendering fault rather than as a door. One palette is read by all four surfaces, and the '+
    'assertion is that they agree, not that each one is themed'],
  'the wall is coursed masonry and not a flat rectangle':['presentation',
    'the wall was four fillRects of one colour, which is the largest single-colour region on screen. Four '+
    'palettes over a flat rectangle would have made that four times as obvious rather than fixing it. The '+
    'second course is offset by half a block, because a wall whose joints line up into continuous '+
    'verticals reads as tile - the same failure the lab\'s 120px floor lattice was deleted for'],
  'the floor cache is keyed by the area, so descending a floor repaints the room':['presentation',
    'the floor sprite was cached by room type and size, on the reasoning that there was one dungeon. '+
    'Descending handed back the previous floor\'s baked canvas, so the first room on a new floor carried '+
    'the old area\'s stone. This is the same class of bug as omitting the room SIZE from that key, and it '+
    'would have been invisible in any screenshot of a single floor'],
  'an area has a name and a flavour, and the character sheet says which one you are in':['HUD and interface',
    'the areas were colour with no words, and a filter is not a place. The names live in the content '+
    'registry addressed by the same string areaForFloor() returns, so the words, the colours and the '+
    'enemy mix are three answers to one question asked through one spelling of it - and a fifth area '+
    'is one entry rather than a code change'],
  'the top band owns the top of the screen, and nothing else is drawn in it':['HUD and interface',
    'the band is a full-width rail and the plate block moved down under it. The interesting part is what '+
    'it cost: the block is three rows (101px) and ROOM_TOP is 130, so the band can be at most 15px before '+
    'the depth plate sits on the room\'s top wall and hides bodies walking along it. Asserted as the '+
    'CONSTRAINT rather than the number, and read off the framebuffer rather than off the layout - '+
    '"the plate is at y 29" is true whether or not anything was drawn through it'],
};


/* The bug list. [h:80-ui-7] */
/* DOM overlays and the input they take away --------------------------------------- The bug list and the controls sheet are real DOM on top of a... [h:80-ui-8] */
/* whether the character sheet is up. Read by the canvas so it can stop drawing its own dimmed PAUSED
   overlay underneath a backdrop that already dims - two dim layers stacked read as a bug, and the
   canvas cannot know a DOM element is covering it. */
let uiSheetOpen=false;
function uiOverlay(){
  const s=document.getElementById('ctlSheet'), b=document.getElementById('bugPanel'),
        d=document.getElementById('seedSheet'), c=document.getElementById('charSheet');
  if(s&&s.classList.contains('on')) return s;
  if(d&&d.classList.contains('on')) return d;
  if(c&&c.classList.contains('on')) return c;
  if(b&&b.style.display==='block') return b;
  return null;
}
/* True while the keystroke is aimed at the seed field. [h:80-ui-9] */
const seedTyping=e=>{const t=e.target;return !!(t&&t.id==='seedInput');};
/* the weapon bench, input side ---------------------------------------------------------------- F1 opens it, 1-4 swap, Escape or F1 closes, clicking... [h:80-ui-10] */
let devOpen=false;
function toggleDev(force){
  devOpen=(force===undefined)?!devOpen:!!force;
  if(devOpen){ keys={}; releaseButtons(); }
}
/* give the gun without pretending the player found it: the cooldown comes from the weapon rather than
   from whatever the previous one left behind, which is the same reasoning fireWeapon uses, so a
   bench swap behaves exactly like a pickup does. */
function devSwap(i){
  if(i<0||i>=WEAPONS.length) return false;
  player.weaponIdx=i;
  player.cooldown=WEAPONS[i].cooldown/TEMPO.rate;
  player.cooldownMax=player.cooldown;
  player.muzzleTimer=0;
  return true;
}
/* WHAT COUNTS AS A PANEL THAT OWNS THE KEYBOARD. [h:80-ui-11] */
function uiHoldsInput(){ return !!uiOverlay(); }
/* The keys the overlays answer to. They have to get past the suppressor below, or the bug list
   cannot be closed with the key that opened it - the same class of bug as a cancel you cannot
   reach, and exactly the mistake the hook's cooldown gate used to be. */
/* The keys the overlays answer to. [h:80-ui-12] */
/* 'tab' is here for the same reason 'f1' and 'h' are: [h:80-ui-13] */
/* 'g' was missing, so the gold-key shortcut the bench footer advertised could never fire: [h:80-ui-14] */
const UI_KEYS=['escape','h','b','s','p','r','g','tab','f1','f2','f3','f4','f6','f7','1','2','3','4'];
/* Lowercases before comparing, and that is a fix rather than a style choice. [h:80-ui-15] */
function uiAllows(k){ return UI_KEYS.indexOf(String(k).toLowerCase())>=0; }
// Anything aimed at the overlay is consumed on the way down, before the game sees it.
/* the character sheet ----------------------------------------------------------------------- Pausing IS this sheet. [h:80-ui-16] */

/* How wide a full bar is, per stat. [h:80-ui-17] */
const STAT_SPAN={strength:12,speed:1,momentum:1,intelligence:5,luck:6,vigor:14,precision:6};
const statSpan=s=>STAT_SPAN[s.key]||10;
/* asPct is declared on the definition, not guessed from the kind: Strength and Speed are both `add`
   and one of them prints as 3 while the other prints as 6%, and a number that changes units
   depending on how large it has become would be unreadable. */
const statIsPct=s=>!!s.asPct||s.kind==='meter';
function statDisplay(s){
  if(statIsPct(s)) return Math.round(s.value*100)+'%';
  /* A ROLL STAT PRINTS ITS SIGN, INCLUDING A NEGATIVE ONE. [h:80-ui-18] */
  if(s.kind==='roll'){
    const v=Math.round(s.value*10)/10;
    return v>0?'+'+v:v<0?String(v):'even';
  }
  return String(Math.round(s.value*100)/100);
}
function statFraction(s){
  return Math.max(0,Math.min(1,s.value/statSpan(s)));
}

/* THE AREA LINE, which is the one thing on this sheet the player did not bring with them. [h:80-ui-19] */
function renderAreaLine(){
  const host=document.getElementById('charArea');
  if(!host) return;
  const area=areaForFloor(), pal=areaPalette();
  const def=Content.has('area',area)?Content.get('area',area):null;
  const rule=host.querySelector('i'), name=host.querySelector('span'), flav=host.querySelector('em');
  if(rule) rule.style.background=pal.accent;
  if(name) name.textContent=(def&&def.name)||area;
  if(flav) flav.textContent=(def&&def.flavour)||'';
}
function renderCharSheet(){
  const host=document.getElementById('charStats');
  if(!host) return;
  host.textContent='';
  renderAreaLine();
  for(const s of Stats.sheet()){
    /* MOMENTUM IS NOT ON THIS SHEET, and it used to be. [h:80-ui-20] */
    if(s.key==='momentum') continue;
    const row=document.createElement('div');
    row.className='statRow'+(s.kind==='meter'?' earned':'');
    row.dataset.stat=s.key;

    const nm=document.createElement('div'); nm.className='nm';
    nm.appendChild(document.createTextNode(s.label));
    const em=document.createElement('em'); em.textContent=s.blurb; nm.appendChild(em);

    const bar=document.createElement('div'); bar.className='statBar';
    const notches=document.createElement('i'); notches.style.width='100%';
    const fill=document.createElement('u');
    fill.style.width=(statFraction(s)*100).toFixed(1)+'%';
    bar.appendChild(notches); bar.appendChild(fill);

    const val=document.createElement('div'); val.className='val';
    val.textContent=statDisplay(s);
    if(s.fromItems){
      /* Say WHERE a number came from, in the SAME UNITS as the number itself. A stat reading 7 with
         no explanation is a number the player has to trust, and an item they cannot connect to it is
         an item that feels like tax. A badge in different units to the value is worse than none. */
      const b=document.createElement('b');
      b.textContent=statIsPct(s)?' +'+Math.round(s.flat*100)+'%':' +'+Math.round(s.flat*100)/100;
      b.title='from items';
      val.appendChild(b);
    } else if(s.kind==='meter'){
      const b=document.createElement('b'); b.textContent=' play'; val.appendChild(b);
    }

    row.appendChild(nm); row.appendChild(bar); row.appendChild(val);
    host.appendChild(row);
  }

  const grid=document.getElementById('itemGrid'), head=document.querySelector('#charItems h3 span');
  if(grid){
    grid.textContent='';
    const carried=(typeof loadout!=='undefined'&&loadout.items)?loadout.items:[];
    if(carried.length) for(const it of carried){
      const chip=document.createElement('div'); chip.className='itemChip';
      chip.appendChild(itemIcon(it.id,22));
      const nm2=document.createElement('span'); nm2.textContent=it.name; chip.appendChild(nm2);
      if(it.charges!=null&&it.charges<Infinity){
        const sm=document.createElement('small'); sm.textContent='x'+it.charges; chip.appendChild(sm);
      }
      grid.appendChild(chip);
    }
    else{
      /* A designed empty state, not a missing feature. A dashed rule and a sentence in the game's
         own voice, because "nothing carried" is a normal thing to be looking at on tick one. */
      const em=document.createElement('div'); em.className='itemEmpty';
      em.textContent='Nothing carried. Something will turn up.';
      grid.appendChild(em);
    }
    if(head) head.textContent=carried.length?carried.length+' item'+(carried.length===1?'':'s'):'';
  }
}

function openCharSheet(){
  const s=document.getElementById('charSheet');
  if(!s) return;
  renderCharSheet();
  s.classList.add('on');
  uiSheetOpen=true;
  uiTakeInput();
}
function closeCharSheet(){
  const s=document.getElementById('charSheet');
  if(s) s.classList.remove('on');
  uiSheetOpen=false;
  uiReleaseFocus();
}
/* TAB, as a toggle, and it answers the TOP overlay rather than only ever opening its own. [h:80-ui-21] */
function toggleSheet(){
  const s=document.getElementById('charSheet');
  if(s&&s.classList.contains('on')){ closeCharSheet(); return; }
  openCharSheet();
}
window.addEventListener('keydown',e=>{
  if(seedTyping(e)) return;
  if(uiHoldsInput()&&!uiAllows(e.key)){ e.preventDefault(); e.stopPropagation(); }
},true);
window.addEventListener('keyup',e=>{
  if(seedTyping(e)) return;
  if(uiHoldsInput()&&!uiAllows(e.key)){ e.preventDefault(); e.stopPropagation(); }
},true);
/* A click on an overlay's own dim backdrop belongs to that overlay, and must be let through. [h:80-ui-22] */
const overlayBackdrop=e=>{const t=e.target;return !!(t&&(t.id==='charSheet'||t.id==='ctlSheet'));};
for(const type of ['mousedown','mouseup','mousemove','contextmenu','wheel','pointercancel']){
  window.addEventListener(type,e=>{
    if(!uiOverlay()) return;
    if(overlayBackdrop(e)) return;
    e.preventDefault(); e.stopPropagation();
  },true);
}
/* And the game lets go of anything it was already holding, once, on the way in. Holding a
   direction into a pause and then closing the pause used to leave the player walking. */
function uiTakeInput(){
  if(!uiHoldsInput()) return;
  keys={}; releaseButtons();
  mouseDown=false; altMouseDown=false;
}
function toggleControls(){
  const s=document.getElementById('ctlSheet');
  if(!s) return;
  s.classList.toggle('on');
  if(s.classList.contains('on')) uiTakeInput();
  else uiReleaseFocus();
}
/* Escape closes whatever is on top, sheet first, and only then the bug list. One key that means
   "get me out of here" rather than one key per overlay, because a player reaching for Escape is not
   thinking about which of the two they opened. */
function uiCloseTop(){
  const s=document.getElementById('ctlSheet');
  if(s&&s.classList.contains('on')){ s.classList.remove('on'); uiReleaseFocus(); return; }
  const d=document.getElementById('seedSheet');
  if(d&&d.classList.contains('on')){ d.classList.remove('on'); uiReleaseFocus(); return; }
  const c=document.getElementById('charSheet');
  // Escape out of the character sheet RESUMES rather than merely hiding it. Hiding it would leave the
  // game paused behind a card that is no longer there, and the player would be stuck in a paused run
  // they cannot see, which is the worst state this game can be left in.
  if(c&&c.classList.contains('on')){ setPaused(false); return; }
  const p=document.getElementById('bugPanel');
  if(p&&p.style.display==='block'){ p.style.display='none'; releaseButtons(); keys={}; return; }
  toggleControls();
}

/* the seed sheet ---------------------------------------------------------------------------- Opened on S from the title screen or from a pause. [h:80-ui-23] */
function seedClose(){
  const d=document.getElementById('seedSheet');
  if(d) d.classList.remove('on');
  uiReleaseFocus();
}
function toggleSeedSheet(){
  const d=document.getElementById('seedSheet');
  if(!d) return;
  if(d.classList.contains('on')){ seedClose(); return; }
  d.classList.add('on');
  uiTakeInput();
  const inp=document.getElementById('seedInput');
  const err=document.getElementById('seedErr');
  if(err) err.textContent='';
  if(inp){ inp.value=Rnd.seedText; setTimeout(()=>{inp.focus();inp.select();},0); }
}
function seedDescend(){
  const inp=document.getElementById('seedInput'), err=document.getElementById('seedErr');
  const raw=(inp&&inp.value||'').trim();
  let n;
  if(!raw) n=Rnd.fresh();
  else{
    n=Rnd.decode(raw);
    if(n===null){
      /* Refused LOUDLY and in place, keeping what was typed. [h:80-ui-24] */
      const shaped=String(raw).toUpperCase().replace(/[^0-9A-Z]/g,'');
      const tooBig=shaped.length<=Rnd.WIDTH&&parseInt(shaped,36)>0xFFFFFFFF;
      if(err) err.textContent=tooBig
        ? 'That code is larger than this game can store. Seeds run from 0000000 to 1Z141Z3 - try one of those.'
        : 'That is not a seed. Seven letters and digits, like '+Rnd.seedText+'.';
      if(inp){ inp.focus(); inp.select(); }
      return;
    }
  }
  // startGame(n) reseeds to exactly what was typed, so the dungeon on screen is the one the code names
  seedClose();
  startGame(n);
}
document.getElementById('seedGo').addEventListener('click',seedDescend);
/* "New seed" must NOT touch the live run. [h:80-ui-25] */
document.getElementById('seedRand').addEventListener('click',()=>{
  const inp=document.getElementById('seedInput'), err=document.getElementById('seedErr');
  if(inp) inp.value=Rnd.encode(Rnd.fresh());
  if(err) err.textContent='';
});
document.getElementById('seedClose').addEventListener('click',seedClose);
document.getElementById('seedSheet').addEventListener('mousedown',e=>{
  if(e.target.id==='seedSheet') seedClose();
});
/* Typed keys must reach the field and must NOT reach the game. WASD is movement, and on the title
   screen ANY key starts a run - so a field that leaked either would start a dungeon per letter. */
/* Clicking the dim area around the character sheet resumes, the same as clicking the game does - the player is putting the game back down either... [h:80-ui-26] */
document.getElementById('charSheet').addEventListener('mousedown',e=>{
  if(e.target.id!=='charSheet') return;
  e.stopPropagation();
  setPaused(false);
});
document.getElementById('seedInput').addEventListener('keydown',e=>{
  e.stopPropagation();
  if(e.key==='Enter'){ e.preventDefault(); seedDescend(); return; }
  if(e.key==='Escape'){ e.preventDefault(); seedClose(); return; }
});
// focus has to go somewhere sensible so the sheet is reachable by keyboard, and come back after
let uiReturnFocus=null;
function uiReleaseFocus(){
  if(uiReturnFocus&&uiReturnFocus.focus){ try{ uiReturnFocus.focus(); }catch(e){} }
  uiReturnFocus=null;
}
function showBugPanel(results){
  if(document.getElementById('bugBtn')){ toggleBugPanel(); return; }
  const CAT_ORDER=['frame and input','weapons and damage','the right click','enemies and AI',
    'movement and being hit','rooms and progression','HUD and interface','runs and records','presentation','unclassified'];
  const live=results&&results.length;
  const byName={};
  if(live) for(const r of results) byName[r.name]=r.ok;
  /* With no results the table is the source of truth and everything in it is, by construction, fixed. [h:80-ui-27] */
  const items=Object.keys(FIXES).map(name=>{
    const m=FIXES[name];
    const known=live?name in byName:false;
    const ok=known?byName[name]:true;
    return {name,cat:m[0],why:m[1],ok,known,live:!!live,
      msg:live&&known&&!byName[name]?((results.find(r=>r.name===name)||{}).msg||''):''};
  });
  const byCat={};
  for(const c of CAT_ORDER) byCat[c]={ok:[],bad:[],orphan:[]};
  for(const it of items){
    /* A typo in a category string used to THROW here and take the entire panel down - which is the worst possible failure for the thing whose job is to... [h:80-ui-28] */
    const cat=(it.cat&&(byCat[it.cat]||CAT_ORDER.includes(it.cat)))?it.cat:'unclassified';
    byCat[cat][it.live&&!it.known?'orphan':(it.ok?'ok':'bad')].push(it);
  }
  const pass=items.filter(i=>i.ok).length, total=items.length, allOk=pass===total;
  /* Unverified counts as NOT ok for the headline. A panel that goes green while a third of its
     entries are unbacked is the failure being fixed, one level up. */
  const orphans=items.filter(i=>i.live&&!i.known);
  const allClear=allOk&&orphans.length===0;

  /* THE TWO NUMBERS, and the reason this panel says what each one is. [h:80-ui-29] */
  const suiteTotal=live?results.length:0;
  const unpinned=live?results.filter(r=>!(r.name in FIXES)).length:0;

  const btn=document.createElement('button');
  btn.id='bugBtn';
  /* THE BADGE LEADS WITH THE WHOLE SUITE, because a green light on the smaller number is a lie. [h:80-ui-30] */
  const suitePass=live?results.filter(r=>r.ok).length:0;
  const suiteRed=live&&suitePass<suiteTotal;
  btn.textContent=suiteRed?(suiteTotal-suitePass)+' FAILED'
    :orphans.length?orphans.length+' UNVERIFIED'
    :'ok '+suitePass+'/'+suiteTotal+'  ·  fixes '+pass+'/'+total;
  btn.title='Bug fixes (B)';
  btn.style.cssText='position:fixed;left:10px;bottom:10px;z-index:11;padding:7px 12px;cursor:pointer;'+
    'background:rgba(12,14,20,.92);color:'+(suiteRed?'#ff6b6b':orphans.length?'#e8c04a':'#5ee27a')+';border:1px solid '+
    (suiteRed?'#6b2f2f':orphans.length?'#6b5c2f':'#2f6b46')+';'+
    'border-radius:4px;font:12px ui-monospace,monospace;letter-spacing:.04em';
  document.body.appendChild(btn);

  const panel=document.createElement('div');
  panel.id='bugPanel';
  panel.style.cssText='position:fixed;left:10px;bottom:44px;width:min(460px,calc(100vw - 20px));max-height:min(72vh,760px);'+
    'overflow:auto;display:none;z-index:11;padding:10px 12px;background:rgba(10,12,18,.95);'+
    'border:1px solid #2b303c;border-radius:4px;font:12px ui-monospace,monospace;box-shadow:0 8px 30px rgba(0,0,0,.5)';

  const head=document.createElement('div');
  head.style.cssText='display:flex;justify-content:space-between;align-items:baseline;margin-bottom:8px;color:'+
    (orphans.length?'#e8c04a':(allClear?'#5ee27a':'#ff6b6b'));
  const title=document.createElement('span');
  title.textContent=(orphans.length?orphans.length+' FIXES HAVE NO TEST'
    :(allClear?'all pinned fixes hold':'STILL BROKEN'))+'  '+pass+'/'+total+' pinned'
    +(live?('   ·   '+suiteTotal+' checks, '+unpinned+' not in this list'):'');
  const close=document.createElement('button');
  close.textContent='close';
  close.style.cssText='background:none;border:1px solid #3a4150;color:#9aa4b5;border-radius:3px;padding:2px 8px;cursor:pointer;font:inherit';
  close.onclick=()=>{panel.style.display='none'; releaseButtons(); keys={};};
  head.appendChild(title); head.appendChild(close);
  panel.appendChild(head);
  btn.onclick=()=>{ panel.style.display=panel.style.display==='none'?'block':'none';
    if(panel.style.display==='block') uiTakeInput(); };

  for(const c of CAT_ORDER){
    const g=byCat[c]; if(!g.ok.length&&!g.bad.length&&!g.orphan.length) continue;
    const det=document.createElement('details');
    // a group with a failure inside starts open, so red is never hidden behind a triangle
    det.open=!!g.bad.length||!!g.orphan.length||c==='unclassified';
    det.style.cssText='margin:0 0 3px;border-bottom:1px solid #1e222c';
    const sum=document.createElement('summary');
    const mark=g.bad.length?'✖':(g.orphan.length?'?':'✔');
    sum.style.cssText='cursor:pointer;padding:5px 2px;list-style:none;color:'+
      (g.bad.length?'#ff6b6b':(g.orphan.length?'#e8c04a':'#8fd8a8'))+';user-select:none';
    sum.innerHTML='<span style="opacity:.8">'+mark+'</span>  '+c+
      ' <span style="opacity:.5">'+g.ok.length+'/'+(g.ok.length+g.bad.length+g.orphan.length)+'</span>';
    det.appendChild(sum);
    const ul=document.createElement('ul');
    ul.style.cssText='margin:0 0 6px;padding:0 0 0 20px;list-style:none';
    // the reason comes first in grey, because "why is this pinned" is the part worth reading and
    // the name of the test is only there to find it in the source
    for(const it of g.ok.concat(g.bad).concat(g.orphan)){
      const orph=it.live&&!it.known;
      const li=document.createElement('li');
      li.style.cssText='margin:0 0 5px;color:'+(orph?'#e8c04a':(it.ok?'#7fbf95':'#ff6b6b'));
      const nm=document.createElement('div');
      nm.textContent=it.name;
      if(orph){
        /* Say what is wrong, in the entry, rather than only in the heading. An amber row that reads
           like a green one is worse than no row: the reader is looking at a name in a list of fixed
           things, and the name is the whole of what they take away. */
        const w=document.createElement('span');
        w.textContent='  — no test with this name; this fix is unverified';
        w.style.cssText='color:#e8c04a;font-size:11px';
        nm.appendChild(w);
      }
      const why=document.createElement('div');
      why.textContent=it.why;
      why.style.cssText='color:'+(orph?'#8a7a4a':(it.ok?'#5d6b78':'#c98a8a'))+';font-size:11px;line-height:1.35';
      li.appendChild(nm); li.appendChild(why);
      if(!it.ok&&it.msg){
        const m=document.createElement('div');
        m.textContent=it.msg;
        m.style.cssText='color:#ff9a9a;font-size:11px;margin-top:2px';
        li.appendChild(m);
      }
      ul.appendChild(li);
    }
    det.appendChild(ul);
    panel.appendChild(det);
  }
  if(!live){
    const foot=document.createElement('div');
    foot.textContent='open the build with ?test to check these still hold';
    foot.style.cssText='color:#5d6b78;font-size:11px;margin-top:8px';
    panel.appendChild(foot);
  }
  document.body.appendChild(panel);
}
function toggleBugPanel(){
  const p=document.getElementById('bugPanel');
  if(!p) return;
  p.style.display=p.style.display==='none'?'block':'none';
  // same contract as the controls sheet: opening it takes the controls away, and a closed panel
  // must not leave the player walking or the wand firing because they were busy reading
  if(p.style.display==='block') uiTakeInput();
}

/* THE SOUND SYSTEM'S ONE GESTURE HOOK. [h:80-ui-31] */
/* REGISTER THE UNLOCK LISTENERS, DO NOT CALL THE UNLOCK. [h:80-ui-32] */
Sound.autoUnlock();

window.addEventListener('keydown',e=>{
  if(uiHoldsInput()&&!uiAllows(e.key)) return;
  const k=e.key.toLowerCase(), first=!e.repeat&&!keys[k];
  /* The weapon bench answers BEFORE the game does, and before the overlays below, because it is the one panel whose keys are also game keys: [h:80-ui-33] */
  /* THE BENCH IS NOT MODAL, AND THAT IS THE WHOLE OF THIS BLOCK. [h:80-ui-34] */
  if(devOpen){
    if(k==='escape'||k==='f1'){ toggleDev(false); return; }
    if(first&&k>='1'&&k<='4'){ devSwap(parseInt(k,10)-1); return; }
    /* the lab's own shortcuts, which are debug tools and never reachable during a run */
    if(state==='dev'){
      if(first&&k==='g'){ player.hasGold=true; return; }
      if(first&&k==='s'){ player.hasSilver=true; return; }
      if(first&&k==='h'){ player.hp=player.maxHp; player.cooldown=0; player.altCooldown=0;
        player.blinkCharges=2; player.blinkRegen=0; return; }
      /* but S is also DOWN. [h:80-ui-35] */
      if(first&&k==='y'){ player.hasSilver=true; return; }
    }
    /* NOT `return` at the end. The bench does not own the keyboard; it borrows five keys. */
    /* THE THREE SHORTCUTS BELOW ARE A DEBUG TOOL, NOT A GAME RULE, AND THEY WERE REACHABLE DURING A REAL RUN. [h:80-ui-36] */
  }
  if(k==='f1'&&first&&player){ toggleDev(true); return; }
  /* THE LAB, asked BEFORE the game and before the overlays below, for the same reason the bench is: [h:80-ui-37] */
  if(Lab.key(k,first)) return;
  /* Q presses the active item. [h:80-ui-38] */
  if(k==='q'&&first){ Items.useActive(); return; }
  /* TAB IS THE CHARACTER SHEET, and TAB was NOT BOUND AT ALL. [h:80-ui-39] */
    if(k==='tab'&&first){ e.preventDefault(); toggleSheet(); return; }
    if(['arrowup','arrowdown','arrowleft','arrowright','w','a','s','d',' ','shift','tab'].includes(k)) e.preventDefault();
    /* A FUNCTION KEY THE GAME CLAIMS MUST NOT ALSO REACH THE BROWSER. [h:80-ui-40] */
    if(/^f([1-9]|1[0-2])$/.test(k)&&k!=='f5') e.preventDefault();
  keys[k]=true;
  if(k==='f'&&first) showPerf=!showPerf;
  if(k==='g'&&first) showSpawn=!showSpawn;
  // the bug list is a key, not a URL. it lives in the main script and opens in a normal game, so
  // anyone who hits something odd can read the change history without a query string
  if(k==='b'&&first){ if(document.getElementById('bugBtn')) toggleBugPanel(); else showBugPanel(); return; }
  if(k==='h'&&first){ toggleControls(); return; }
  // S opens the seed sheet, but only where it means something: on the title screen and from a
  // pause. During play it stays strafe-down, because taking a movement key to open a dialog is how
  // a player dies while reading your menu.
  if(k==='s'&&first&&(state==='start'||paused)){ toggleSeedSheet(); return; }
  if(k==='escape'&&uiHoldsInput()){ uiCloseTop(); return; }
  /* "PRESS ANY KEY" DOES NOT MEAN ANY KEY, AND THESE ARE NOT KEYS. [h:80-ui-41] */
  if(state==='start'){
    if(e.altKey||e.ctrlKey||e.metaKey) return;
    if(k==='alt'||k==='control'||k==='shift'||k==='meta'||k==='os'||k==='capslock') return;
    if(k==='dead'||k==='unidentified') return;
    startGame(Rnd.seed);   // the seed on the title tag, not a new one: "the seed decides this dungeon"
    return;
  }
  if(!first) return;
  if((k==='escape'||k==='p')&&state==='playing'){setPaused(!paused);return;}
  if(k==='r'&&(paused||state==='gameover'||state==='win')){startGame(run.rootSeed);return;}
  /* BLINK, AND WHY IT CHECKES FOR A RUN RATHER THAN FOR A STATE. [h:80-ui-42] */
  const inRoom=state==='playing'||state==='dev';
  if((k==='shift'||k===' ')&&inRoom&&!paused&&!trans&&readyT<=0&&player.blinkCharges>0) doBlink();
});
window.addEventListener('keyup',e=>keys[e.key.toLowerCase()]=false);
/* The mouse half of the unlock, for a player who only ever clicks. Without it a game played entirely
   with the pointer never creates its context and is silent. */
window.addEventListener('pointerdown',()=>Sound.autoUnlock(),true);
window.addEventListener('blur',autoPause);
document.addEventListener('visibilitychange',()=>{if(document.hidden) autoPause();});
// clicking the dim area around the card closes the sheet, and a click ON the card does not. Both are
// decided from the event target rather than by stopping propagation, so the capture-phase suppressor
// above and this one cannot disagree about who saw the click.
document.getElementById('ctlSheet').addEventListener('mousedown',e=>{
  /* same stopPropagation reason as the character sheet: [h:80-ui-43] */
  if(e.target.id!=='ctlSheet') return;
  e.stopPropagation();
  toggleControls();
});
canvas.addEventListener('mousemove',e=>{
  if(uiHoldsInput()) return;
  /* MEASURE FROM THE CONTENT BOX, AND THE CONTENT BOX IS NOT clientWidth. [h:80-ui-44] */
  const rect=canvas.getBoundingClientRect(), cs=getComputedStyle(canvas);
  const bx=parseFloat(cs.borderLeftWidth)+parseFloat(cs.borderRightWidth);
  const by=parseFloat(cs.borderTopWidth)+parseFloat(cs.borderBottomWidth);
  mouse.x=(e.clientX-rect.left-parseFloat(cs.borderLeftWidth))*(canvas.width/(rect.width-bx));
  mouse.y=(e.clientY-rect.top-parseFloat(cs.borderTopWidth))*(canvas.height/(rect.height-by));
});
// on the whole window, not just the canvas: the canvas is letterboxed, and a context menu opened
// over the dead space around it eats the mouseup that should have released the wand
window.addEventListener('contextmenu',e=>{e.preventDefault(); releaseButtons();});
/* A click on the weapon bench selects a row. [h:80-ui-45] */
window.addEventListener('mousedown',e=>{
  if(devOpen){
    const G=devLayout();
    if(mouse.x<G.px||mouse.x>G.px+G.pw||mouse.y<G.py||mouse.y>G.py+G.ph){ toggleDev(false); return; }
    for(let i=0;i<WEAPONS.length;i++){
      const y=G.rowY(i);
      if(mouse.x>=G.rowX&&mouse.x<G.rowX+G.rowW&&mouse.y>=y&&mouse.y<y+G.rowH){
        devSwap(i); return;
      }
    }
    return;
  }
  if(uiHoldsInput()) return;
  if(state==='start'){startGame(Rnd.seed);return;}   // the seed the title is showing
  if(paused){setPaused(false);return;}   // the resume click does not cast
  if(e.button===0||e.button===2) trackButton(e.button,true);
});
window.addEventListener('mouseup',e=>{
  if(!e.buttons){releaseButtons();return;}   // both buttons left in the same instant: one event clears both
  trackButton(e.button,false);
});
// e.buttons is 0 on any pointer move made with nothing held, so a latched button cannot survive
// the player moving the mouse again
window.addEventListener('mousemove',e=>{if(!e.buttons) releaseButtons();});
window.addEventListener('pointercancel',releaseButtons);
window.addEventListener('dragstart',e=>e.preventDefault());

requestAnimationFrame(loop);
