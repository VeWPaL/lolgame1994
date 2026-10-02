/* ==============================================================================================
   80-ui  -  boot, input, and the DOM overlays

   Also replaced by Unity, but one idea is worth carrying across: the uiHoldsInput / uiAllows pair,
   which is a whole input-suppression layer. Overlays must take the keyboard away from the game and
   hand it back cleanly on close, and that problem does not get smaller because the renderer
   changed - it gets worse, because Unity has its own event systems underneath.
   ============================================================================================== */
mouse={x:W/2,y:H/2}; mouseDown=false; altMouseDown=false; keys={}; state='start';
loadRecords();

// Pressed buttons live in a set rather than two booleans, and every route by which a mouseup can
// be swallowed (native context menu, pointercancel, blur, tab switch, mouseup outside the page)
// is treated as "all released". Two booleans can disagree: if one mouseup goes missing the other
// button stays latched, which is what used to leave the wand firing after letting go of both
// buttons in the same instant. The set also makes the release order irrelevant.
const held=new Set();
function releaseButtons(){held.clear();mouseDown=false;altMouseDown=false;}
function trackButton(b,down){
  if(down) held.add(b); else held.delete(b);
  mouseDown=held.has(0); altMouseDown=held.has(2);
}


/* Every bug this build has had fixed, grouped, with the one-line reason it is pinned at all.
   Kept as a table keyed by test name rather than as an argument on every call site: the call sites
   are the assertions, and burying a description inside them made them harder to read, not easier.
   Anything missing from this table falls into "unclassified" and is visible in the panel, so a new
   bug fix cannot quietly end up uncategorised. */
const FIXES={
  /* ---- frame and input ---- */
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
  /* This entry used to read "records: a win is saved, a slower win keeps the fastest time", and it
     was pinned with NO TEST behind it - not a failing test, not a renamed one, none at all. The
     panel scored an entry with no result as a pass, so it sat there in green indefinitely.

     It is retired rather than given a test, because the thing it describes no longer exists. The way
     out used to end the run; it goes DOWN now, and endRun() is only ever called with a death. There
     is no path through a normal run that wins, so "a slower win keeps the fastest time" is a
     sentence about a door that was taken out. Writing a test to keep a claim about it honest would be
     inventing a requirement.

     What replaced it is the claim that is actually load-bearing now, and that is pinned next door:
     a run ends when the player dies, and nothing else ends it. That one has a test. */
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
  /* ---- rooms bigger than the screen ----
     These are the first entries here for something no player ever reported: the camera and the room
     bounds are new work, not a regression. They belong anyway, because a table of fixes that stops
     recording fixes the day it starts recording new work is a table that goes stale quietly - and
     the camera clamp is the single best example in this project of a fix whose whole value was the
     note explaining why a green suite had not caught it. */
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
};


/* The bug list. Lives in the MAIN script, not the test one, because it is not a test - it is the
   change history, and a player who has run into something odd should be able to open it and see
   whether it is a known problem without installing a query string.

   `showBugPanel(results)` takes the test results when they exist. With no results it still lists
   every entry, marked fixed, which is the honest state: the table IS the record of what has been
   fixed, and the results only say whether the current build still holds. */
/* ---- DOM overlays and the input they take away ---------------------------------------
   The bug list and the controls sheet are real DOM on top of a canvas game, which means a click
   meant for a button is also a click that reaches the window and casts, and a key pressed while
   reading them walks the player into a Brunch. Suppressing that at each call site means every
   future listener has to remember to; suppressing it once, here, means it cannot be forgotten.

   Two halves, and the second is the one that matters. A CAPTURE-phase listener on window runs
   before the game's BUBBLE-phase listeners on the same node, so stopping propagation there stops
   the event ever reaching them - which covers the listeners nobody remembered. But capture-phase
   suppression alone would still let a key that was already held keep the player walking, and would
   leave the wand firing if the button was down before the sheet opened, so the game's own handlers
   ALSO ask uiHoldsInput() first. Belt and braces, and the second half is what stops the game being
   mid-swing when the overlay closes. */
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
/* True while the keystroke is aimed at the seed field. The suppressor below runs in the CAPTURE
   phase on window, which is ahead of every target in the tree - so stopPropagation there stops a
   key reaching the input at all, and a text field whose every keystroke is eaten accepts nothing,
   silently. The field has to be let through before the suppressor decides anything. */
const seedTyping=e=>{const t=e.target;return !!(t&&t.id==='seedInput');};
/* ---- the weapon bench, input side ----------------------------------------------------------------
   F1 opens it, 1-4 swap, Escape or F1 closes, clicking a row swaps. It HOLDS INPUT and freezes the
   simulation while it is up, because the numbers on it are the point and a number you cannot read
   because you were hit is not a number.

   It does not set paused, and that is deliberate. setPaused opens the character sheet, so reusing it
   would stack two cards and leave the player dismissing one to find the other. So the bench has its
   own flag and advance() asks about both - see the comment there.

   Swapping is deliberately instant rather than "next weapon": a bench that cycles needs three
   presses to reach the fourth gun and every one of those is a press you might fat-finger while a
   fight is waiting. Number keys go straight there, and so does clicking the row. */
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
function uiHoldsInput(){ return !!uiOverlay()||devOpen; }
/* The keys the overlays answer to. They have to get past the suppressor below, or the bug list
   cannot be closed with the key that opened it - the same class of bug as a cancel you cannot
   reach, and exactly the mistake the hook's cooldown gate used to be. */
/* The keys the overlays answer to. They have to get past the suppressor below, or an overlay cannot
   be closed with the key that opened it - the same class of bug as a cancel you cannot reach, and
   exactly the mistake the hook's cooldown gate used to be.

   P and R are here because the character sheet IS the pause screen, so pausing puts an overlay up and
   the keys that dismiss it are P and R. Without them, Escape worked and P did not: the sheet opened
   on pause, and then the suppressor ate the resume. Three tests failed and then left the sheet open,
   which broke the five after them. An overlay that traps its own dismiss key does not fail one test,
   it poisons the rest of the run. */
/* 'tab' is here for the same reason 'f1' and 'h' are: the sheet owns it, so the suppressor must let
   it through or TAB does nothing while the sheet is up - which is the one moment it is most wanted.
   'f7' is here and 'f5' deliberately is not: F5 reloads the page in every browser, so a lab key on F5
   was never a shortcut but a way to lose the room you were setting up. */
/* 'g' was missing, so the gold-key shortcut the bench footer advertised could never fire: while the
   bench was open the suppressor ate every key not on this list, and 'g' was not on it. A documented
   control that silently does nothing is the same failure as an undocumented one, except it looks like
   a bug in the game rather than in the docs. It is here because the lab is where the shortcuts now
   live. */
const UI_KEYS=['escape','h','b','s','p','r','g','tab','f1','f2','f3','f4','f6','f7','1','2','3','4'];
/* Lowercases before comparing, and that is a fix rather than a style choice. Every key in UI_KEYS is
   lowercase and the handler lowercases its own key immediately afterwards, so this one comparison was
   the only place in the input path that saw the raw `e.key` - which is "Tab", "Escape", "ArrowUp",
   every shifted letter and every F-key with its real capital. So the suppressor decided that NONE of
   the overlay's own keys were allowed, and while a sheet was up it ate all of them.

     TAB was the visible symptom because it is the only key here with no other route to its panel: the
     sheet could be opened by pausing and the suppressor still blocked TAB from closing it, so the one
     key that opens a sheet is the one key that cannot close it. Escape worked because the suppressor
     runs on a separate listener that checks `e.key==='Escape'` directly.

     The rule from here on: anything that compares against UI_KEYS lowercases first, and a test that
     dispatches a synthetic event uses the real `e.key` value ("Tab"), not the lowercase one, because a
     synthetic lowercase "tab" is not a key the browser can send and it would hide this class of bug
     rather than reveal it. */
function uiAllows(k){ return UI_KEYS.indexOf(String(k).toLowerCase())>=0; }
// Anything aimed at the overlay is consumed on the way down, before the game sees it.
/* ---- the character sheet -----------------------------------------------------------------------
   Pausing IS this sheet. A player who pauses mid-fight is asking what their build is and what they
   are carrying, and that deserves the whole screen rather than a box on a dimmed game.

   Built from Stats.sheet() in code, so a seventh stat is one entry in DEFS and nothing else. The
   rows are rebuilt on open rather than kept live, which is what lets a 40-tick pause cost nothing
   and means there is no update loop that can be out of step with the simulation.

   THE BARS ARE NOTCHED, and the notch count is the scale. Momentum is capped at 1 and Speed at 15% -
   a smooth bar with no graduations cannot answer "how much of this have I used", and for a stat whose
   whole design is a ceiling that cannot be exceeded, the ceiling is the interesting part. */

/* How wide a full bar is, per stat. For a stat with a real ceiling the ceiling IS the bar, so a
   nearly-full bar is visibly nearly full - which is the entire point of a capped stat, and the reason
   a bar is notched rather than smooth. For an uncapped one this is "as big as it usefully gets": a
   Vigor bar that can fill only at 400 health stops being information after the third pickup. */
const STAT_SPAN={strength:12,speed:1,momentum:1,intelligence:5,luck:6,vigor:14,precision:6};
const statSpan=s=>STAT_SPAN[s.key]||10;
/* asPct is declared on the definition, not guessed from the kind: Strength and Speed are both `add`
   and one of them prints as 3 while the other prints as 6%, and a number that changes units
   depending on how large it has become would be unreadable. */
const statIsPct=s=>!!s.asPct||s.kind==='meter';
function statDisplay(s){
  if(statIsPct(s)) return Math.round(s.value*100)+'%';
  /* A ROLL STAT PRINTS ITS SIGN, INCLUDING A NEGATIVE ONE.

     This read `value>0 ? '+'+value : 'even'`, so a -1 Luck printed as "even" - the same word as a
     zero. Glass Wands takes 1 Luck and is the only item in the roster with a cost, and the character
     sheet reported its cost as neutrality. A penalty that reads as no change is a penalty the player
     cannot weigh, on the one screen whose job is weighing them.

     Zero still prints "even", which is a real answer to a real question: what does +0 Luck mean. */
  if(s.kind==='roll'){
    const v=Math.round(s.value*10)/10;
    return v>0?'+'+v:v<0?String(v):'even';
  }
  return String(Math.round(s.value*100)/100);
}
function statFraction(s){
  return Math.max(0,Math.min(1,s.value/statSpan(s)));
}

function renderCharSheet(){
  const host=document.getElementById('charStats');
  if(!host) return;
  host.textContent='';
  for(const s of Stats.sheet()){
    /* MOMENTUM IS NOT ON THIS SHEET, and it used to be.

       The sheet answers one question: what is this player CARRYING. Every other row is something that
       was picked up, and a badge under the number says which item put it there. Momentum is the one
       stat on the list that you did not pick up - there is no item that hands it to you - so its row
       was the only one that could not be acted on. You cannot go and find the thing that raises it.

       Worse, the sheet is a PAUSE screen. The only time the number was visible was when the player
       was not playing, which is the worst possible place to put a stat whose entire appeal is that
       you can see it move while you are fighting. It is on the HUD now, where it is legible in real
       time, and the tutorial that explained it is gone - see the plate in drawHUD.

       The stat itself is untouched. Momentum still exists, still charges, still costs you most of
       itself on a hit, and still feeds acceleration and a share of top speed. Only the row moved. */
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
/* TAB, as a toggle, and it answers the TOP overlay rather than only ever opening its own.

     A naive `if(uiSheetOpen) close else open` is wrong whenever another sheet is on top: with the
     controls panel open, the first TAB closes a sheet that was never visible and leaves the controls
     panel up, which reads as TAB doing nothing at all. So the sheet only closes when it is the thing
     actually on screen; otherwise it opens. Escape still closes from anywhere, because Escape means
     "go back" and TAB means "show me this".

     It works in the lab as well as a run, which is the point: the sheet is where a build is read, and
     the lab is where builds are made. */
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
/* A click on an overlay's own dim backdrop belongs to that overlay, and must be let through.

   This is the THIRD time the suppressor has eaten something the UI needed, and the pattern is worth
   naming: it runs in the CAPTURE phase on window, which is ahead of every element in the tree, so its
   stopPropagation is unconditional and total. The seed field lost every keystroke. The character sheet
   lost the click that dismisses it, which is the mouse equivalent of a cancel button you cannot reach
   - the pause screen could be opened with the keyboard and closed with nothing. The suppressor exists
   to stop the GAME seeing input while an overlay is up, and it was doing that so thoroughly that it
   stopped the overlay seeing it either.

   Anything an overlay needs to receive has to be exempted here by name. There is no way to write this
   so that new UI is safe by default, which is the real lesson. */
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

/* ---- the seed sheet ----------------------------------------------------------------------------
   Opened on S from the title screen or from a pause. No persistence, deliberately: the run summary
   already prints the seed a run used, so the loop is "read it off the sheet, type it here". Quietly
   reloading the previous seed would instead mean two people opening the game saw a different number
   for reasons neither of them could see, which is the exact confusion seeds are supposed to remove.

   An EMPTY field means "surprise me", not "seed zero" - and zero is a perfectly good seed that a
   player can type deliberately, so the two must not collide. */
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
      /* Refused LOUDLY and in place, keeping what was typed. Silently starting a random dungeon
         because a letter was mistyped is the one outcome that makes this feel broken.

         Two different refusals share this box, and they say different things because they mean
         different things. A code with the wrong SHAPE is a typo. A code with the right shape but a
         number too large for the generator - anything above 1Z141Z3 - is a code this game cannot
         store at all, and telling such a player "that is not a seed" when it plainly looks like one
         is the confusing half of the pair. */
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
/* "New seed" must NOT touch the live run.

   This called Rnd.set(Rnd.fresh()) on the click, which reseeded the generator underneath a run in
   progress. Everything already rolled - this floor's rooms, the enemies in them - stayed as it was,
   and everything not yet rolled came from the new seed instead. The result is a run that is half of
   one seed and half of another, which nobody can reproduce by typing anything, including the seed the
   summary shows at the end.

   The button now only fills the field. Going on that seed is what seedDescend() does, and it does it
   deliberately, the same way typing a code does. */
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
/* Clicking the dim area around the character sheet resumes, the same as clicking the game does -
   the player is putting the game back down either way, and making them find the key first would be
   the game disagreeing with its own convention. A click ON the card does not resume.

   stopPropagation is load-bearing and was not here first time. Resuming closes the sheet, so by the
   time this same click bubbled up to the window handler there was no overlay up and no pause in
   force - and it fell through to the cast branch. One click that dismissed the pause screen and
   started firing at whatever the wand was pointing at. The window handler is a BUBBLE listener, so
   stopping propagation here is enough to keep the event to the overlay that consumed it. */
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
  /* With no results the table is the source of truth and everything in it is, by construction, fixed.

     THREE states, not two, and the third is the one this used to fold into the first.

     An entry here whose test no longer exists - because the test was renamed, or deleted, or never
     existed - used to be scored `byName[name]!==false`, which is TRUE for a name that is not in the
     results at all. So a fix with nothing behind it was reported as a fix that holds, in green,
     forever. That is the worst way for this panel to be wrong: not a false alarm, but a false all
     clear, on exactly the entries somebody had every reason to assume were covered.

     It is not hypothetical. Two entries were in that state - one whose test had been renamed out from
     under it, and one, 'a win is saved', that has no test at all. A scoreboard that cannot say "I do
     not know" will always prefer the comfortable answer, so it is taught to say it. */
  const items=Object.keys(FIXES).map(name=>{
    const m=FIXES[name];
    const known=live?name in byName:false;
    const ok=known?byName[name]:true;
    return {name,cat:m[0],why:m[1],ok,known,live:!!live,
      msg:live&&known&&!byName[name]?((results.find(r=>r.name===name)||{}).msg||''):''};
  });
  const byCat={};
  for(const c of CAT_ORDER) byCat[c]={ok:[],bad:[],orphan:[]};
  for(const it of items) byCat[it.cat][it.live&&!it.known?'orphan':(it.ok?'ok':'bad')].push(it);
  const pass=items.filter(i=>i.ok).length, total=items.length, allOk=pass===total;
  /* Unverified counts as NOT ok for the headline. A panel that goes green while a third of its
     entries are unbacked is the failure being fixed, one level up. */
  const orphans=items.filter(i=>i.live&&!i.known);
  const allClear=allOk&&orphans.length===0;

  /* THE TWO NUMBERS, and the reason this panel says what each one is.

     `total` is the number of ENTRIES IN THIS TABLE - bugs that were found and pinned. The suite runs
     a good deal more checks than that, most of which are not bug fixes at all but standing
     guarantees: that a seed round-trips, that a stat is derived from base, that the registry refuses
     a missing id. Those belong in the suite and not in a change history, and back-filling the table
     with them would be inventing a history in which each one was once a bug somebody reported.

     So the two numbers are genuinely different and the panel used to show only the smaller one, under
     a heading that read like a test result. A player comparing "94 ok" against a suite that says 160
     has no way to tell which is stale, and the likeliest reading - that 66 checks are missing or
     broken - is the wrong one. Both are printed, each named, and the unpinned count is printed too,
     because a check that exists in neither list is the thing that would actually be worth knowing. */
  const suiteTotal=live?results.length:0;
  const unpinned=live?results.filter(r=>!(r.name in FIXES)).length:0;

  const btn=document.createElement('button');
  btn.id='bugBtn';
  /* THE BADGE LEADS WITH THE WHOLE SUITE, because a green light on the smaller number is a lie.

     It read `fixes: pass/total pinned`, where both numbers come from the FIXES table - 104 historical
     bug fixes. The suite runs 197 checks, so 93 of them could fail and the badge would sit there green
     saying everything is fine. That is the worst possible shape for a status light: it is green, it is
     on screen at all times, and the failures it hides are the ones nobody is looking for.

     The suite is the thing that is actually green or red, so it is what the badge says. The pinned
     count is still shown - it is the interesting number, and it is the one that means "this build has
     been through something" - but as a second clause, after the part that can be red.

     Ordering, most alarming first: a red suite, then an unverified fix (a fix with no test behind it),
     then green. */
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

window.addEventListener('keydown',e=>{
  if(uiHoldsInput()&&!uiAllows(e.key)) return;
  const k=e.key.toLowerCase(), first=!e.repeat&&!keys[k];
  // The weapon bench answers BEFORE the game does, and before the overlays below, because it is
  // the one panel whose keys are also game keys: 1-4 are the bench, but they are not movement, and
  // F1 is nothing at all to the game. It has to be asked first or a swap silently also fires the
  // wand at whatever is under the cursor.
  if(devOpen){
    if(k==='escape'||k==='f1'){ toggleDev(false); return; }
    if(first&&k>='1'&&k<='4'){ devSwap(parseInt(k,10)-1); return; }
    /* THE THREE SHORTCUTS BELOW ARE A DEBUG TOOL, NOT A GAME RULE, AND THEY WERE REACHABLE DURING A
       REAL RUN. With the bench open - which is F1, in any game, at any depth, with the run in progress
       behind it - H refilled the health, S handed over the silver key and G the gold key. The panel's own
       footer advertised all three, so it was a written-down cheat available to anyone who pressed F1.

       Swapping guns is the one thing the bench legitimately does, because a weapon is not a reward: it
       is a comparison, and comparing needs both sides. Keys are the objective and hearts are the run,
       and neither is a comparison - a panel that can grant them is not a panel.

       They live in the lab instead (F2), which is a state built for exactly this and is entered
       deliberately rather than by pressing a function key mid-fight. */
    if(state==='dev'){
      if(first&&k==='g'){ player.hasGold=true; return; }
      if(first&&k==='s'){ player.hasSilver=true; return; }
      if(first&&k==='h'){ player.hp=player.maxHp; player.cooldown=0; player.altCooldown=0;
        player.blinkCharges=2; player.blinkRegen=0; return; }
    }
    return;
  }
  if(k==='f1'&&first&&player){ toggleDev(true); return; }
  /* THE LAB, asked BEFORE the game and before the overlays below, for the same reason the bench is:
     its keys are function keys that mean nothing to the game, and F2 in particular has to be able
     to both enter and leave. If the lab only answered on the way in it would be a one-way door, and
     the only way out of a debug view that cannot be left is a browser refresh - which throws away
     the very run you opened it from.

     It is asked before the state overlay keys as well, so F2 leaves the lab even while something is
     open over it. */
  if(Lab.key(k,first)) return;
  /* Q presses the active item. Asked HERE, in the input layer, and routed through Items.useActive so
     nothing outside 25-items.js knows what an active slot is or what number it is - there is one, and
     it is that file's business. Q is not a movement key and never was, so it needed no UI_KEYS entry
     and no preventDefault; it also deliberately does nothing at all when the slot is empty, because a
     key that opens something every time you press it with empty hands is worse than a key that is
     simply not there yet. */
  if(k==='q'&&first){ Items.useActive(); return; }
  /* TAB IS THE CHARACTER SHEET, and TAB was NOT BOUND AT ALL.

     Pressing it ran the browser's default - move focus to the next focusable element - which is the
     bug-list button. So the sheet had no key, and the one thing TAB reliably did was focus the change
     history. `preventDefault` did not include 'tab' either, because nothing had ever claimed it.

     It is bound here, and the preventDefault is unconditional for it: a key the game claims must
     never fall through to focus movement, because focus landing on a button is precisely what made it
     look like the game was highlighting the wrong thing.

     F1 is the weapon bench and is untouched - they are different panels, which is the whole reason
     TAB needed a key of its own rather than sharing F1's. */
    if(k==='tab'&&first){ e.preventDefault(); toggleSheet(); return; }
    if(['arrowup','arrowdown','arrowleft','arrowright','w','a','s','d',' ','shift','tab'].includes(k)) e.preventDefault();
    /* A FUNCTION KEY THE GAME CLAIMS MUST NOT ALSO REACH THE BROWSER.

       The game binds F1 (bench), F2/F3/F4/F6/F7 (lab) and none of them were preventDefault-ed, so each
       also did whatever the browser does with it: F1 is Help in Chrome and Edge, F3 is find-in-page,
       F6 is the address bar in several, F7 is caret-browsing help. The lab's own comment admits F5
       reloads the page "and uses the key anyway" - F5 was moved to F7 for exactly that reason, and the
       class of bug was left in place for the others.

       The list is the keys the game actually binds. F5 is deliberately absent: it is not a game key,
       and claiming it would break reload for everyone who wants it. Note this cannot be verified in a
       headless browser, which does not implement the browser-level actions - so it is written from what
       the platform documents rather than from a measurement, and the comment says so rather than
       implying it was tested. */
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
  /* "PRESS ANY KEY" DOES NOT MEAN ANY KEY, AND THESE ARE NOT KEYS.

     Alt+Tab to change window delivers a keydown while the title screen is up, and it started a run. The
     player switched away, switched back, and found a game already paused part-way into a floor nobody
     chose, under a seed nobody agreed to. Control, Shift and Meta are the modifiers themselves, so
     pressing and releasing one is not a request to play either - and on some layouts a modifier on its
     own arrives as 'Dead' or 'Unidentified', which is a title screen that starts the game by itself.

     The click path below deliberately keeps its own unconditional `startGame`: clicking IS a keypress
     as far as a player is concerned, and there is no modifier that makes a click ambiguous. */
  if(state==='start'){
    if(e.altKey||e.ctrlKey||e.metaKey) return;
    if(k==='alt'||k==='control'||k==='shift'||k==='meta'||k==='os'||k==='capslock') return;
    if(k==='dead'||k==='unidentified') return;
    startGame();
    return;
  }
  if(!first) return;
  if((k==='escape'||k==='p')&&state==='playing'){setPaused(!paused);return;}
  if(k==='r'&&(paused||state==='gameover'||state==='win')){startGame(run.rootSeed);return;}
  /* BLINK, AND WHY IT CHECKES FOR A RUN RATHER THAN FOR A STATE.

     This asked for `state==='playing'`, which is true of a run and false of the lab - so blink did
     nothing in the lab. The lab is a room the player is standing in, moving through, dodging in, and
     a blink that will not fire there is not a lab feature missing, it is a lab that cannot be used to
     test the thing the lab exists to test. Every other input that moves the player already asks
     `state==='dev' || state==='playing'`, and this one line was the odd one out.

     Deliberately NOT `state!=='start'`: on the title screen and after a win there is no run to blink
     out of, and `!trans` and `readyT<=0` already refuse it during a room transition or before the
     player exists. The two states that have a player in a room are the two that can blink. */
  const inRoom=state==='playing'||state==='dev';
  if((k==='shift'||k===' ')&&inRoom&&!paused&&!trans&&readyT<=0&&player.blinkCharges>0) doBlink();
});
window.addEventListener('keyup',e=>keys[e.key.toLowerCase()]=false);
window.addEventListener('blur',autoPause);
document.addEventListener('visibilitychange',()=>{if(document.hidden) autoPause();});
// clicking the dim area around the card closes the sheet, and a click ON the card does not. Both are
// decided from the event target rather than by stopping propagation, so the capture-phase suppressor
// above and this one cannot disagree about who saw the click.
document.getElementById('ctlSheet').addEventListener('mousedown',e=>{
  // same stopPropagation reason as the character sheet: closing the sheet and then letting the click
  // carry on into the game is how a dismiss-click becomes an attack. This handler could not fire at
  // all until overlayBackdrop was taught about ctlSheet - the suppressor had been eating it, so
  // clicking outside the controls sheet has never closed it, only Escape ever has.
  if(e.target.id!=='ctlSheet') return;
  e.stopPropagation();
  toggleControls();
});
canvas.addEventListener('mousemove',e=>{
  if(uiHoldsInput()) return;
  // measure from the content box: getBoundingClientRect includes the 2px CSS border
  const rect=canvas.getBoundingClientRect();
  mouse.x=(e.clientX-rect.left-canvas.clientLeft)*(canvas.width/canvas.clientWidth);
  mouse.y=(e.clientY-rect.top-canvas.clientTop)*(canvas.height/canvas.clientHeight);
});
// on the whole window, not just the canvas: the canvas is letterboxed, and a context menu opened
// over the dead space around it eats the mouseup that should have released the wand
window.addEventListener('contextmenu',e=>{e.preventDefault(); releaseButtons();});
/* A click on the weapon bench selects a row. It is handled here, on window, rather than on the
   canvas, for one reason: the bench is a canvas panel and there is no element to listen on, so the
   hit test has to live where the pointer already is. The rectangle it tests against comes from
   devLayout() - the same call the drawing uses - so a button cannot be drawn in one place and
   clicked in another, which is how the boss gate spent a week broken.

   The bench consumes the click rather than letting it fall through to trackButton, or dismissing
   the panel would also cast the wand at whatever was under the cursor. */
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
  if(state==='start'){startGame();return;}
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
