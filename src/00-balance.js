/* ==============================================================================================
   00-balance  -  every dial in the game

   Nothing here draws, reads input or touches the DOM, so all of it ports to C# unchanged. This is
   the highest-value file in the repo: ninety-odd numbers that ARE the game's feel, most of them
   with the measurement that produced them in the comment above them. When a number is wrong this
   is the file, and when a number is right this is why.
   ============================================================================================== */
const canvas=document.getElementById('c'),ctx=canvas.getContext('2d');
ctx.imageSmoothingEnabled=false;

const W=960, H=600;
/* ---------- pacing ----------
   The sim used to tick at a fixed 60Hz. It now ticks at 60*SPEEDUP, so the whole game advances
   3.5x faster in real time while every distance, size and speed stays in the same canvas pixels.
   NOTE: per-tick distances are deliberately NOT multiplied by SPEEDUP. A px/tick value already
   becomes 3.5x faster in px/s just because there are 3.5x more ticks per second, so scaling them
   as well would give 12.25x. Three families of number do need touching:
     - tick counters that are both written and decremented once per tick (cooldowns, stun, aggro
       memory, idle/shoot timers) are multiplied by SPEEDUP
     - per-tick rates whose budget is not itself in ticks (the shoot-slow bleed) scale by SPEEDUP
     - per-tick exponentials are rescaled with pow(...,1/SPEEDUP) so they decay in the same
       wall-clock time: friction 0.86 -> 0.958, 0.92 -> 0.976, smoothing 0.35 -> 0.116
   Knock impulses are multiplied by KNOCK_GAIN/KNOCK_P_GAIN because the gentler friction also
   shortens a slide: 0.92 -> 0.976 shrinks the run-out to 0.294x, so a force of F is matched by
   0.294F. Same story for the player (0.86 -> 0.958, run-out 0.30x). */
const SPEEDUP=3.5, TICK_HZ=Math.round(60*SPEEDUP);
const sec=s=>Math.round(s*TICK_HZ);
const GRID=7, START=3, DOORW=90;

/* THE STANDARD ROOM, and the fact that it is a DEFAULT rather than the world is new and load-bearing.

   There was exactly one room shape for the whole project: 700x450 at (50,130), and 169 call sites in
   seven gameplay files read those four numbers as the walls. A room is now DATA carrying its own
   bounds, so a room can be 700x450 or 1600x1000 or an L, and these are what a new room gets unless it
   asks for something else.

   WHY THE OLD NAMES SURVIVE, which is the judgement call in this change and the thing to argue with.

   The obvious move is to rewrite all 169 sites to call the accessors. That is the cleaner end state
   on paper, and it is also a change too wide to review, too wide to land in one sitting, and - worst
   of all - possible to leave HALF DONE, which is the state where some sites read the room and some
   read a 700x450 constant, every one of them individually plausible, and the bug only appearing in
   the first room that is a different size. A half-migrated geometry is worse than an unmigrated
   one, because an unmigrated one is at least uniformly wrong.

   So the four names stay, and become a SHORTHAND FOR THE CURRENT ROOM, written by exactly one
   function and by nothing else. There is still one place that knows the geometry - the room record -
   and syncRoomBounds is the single line of code that copies it out. All 169 sites now read the
   current room's bounds, with zero of them edited.

   THE COST, STATED PLAINLY, because the shorthand has a real one: a `let` read before
   syncRoomBounds has run for the room you are standing in is a stale number, where a function call
   could not be. That is why the sync is called from the one function that changes rooms
   (enterRoom) and from the two that build one, rather than being sprinkled at call sites where
   someone might forget it. A reader who wants to be certain is not reading the shorthand: they call
   roomL(), which reads the room every time and cannot be stale.

   MIDX and MIDY came along for the same reason. A bigger room's centre is not the screen's centre,
   and 60 sites wanted "the middle of the room" without saying so. */
const STD_ROOM={l:50,t:130,r:750,b:580};
const ROOM_W=STD_ROOM.r-STD_ROOM.l, ROOM_H=STD_ROOM.b-STD_ROOM.t;
let ROOM_LEFT=STD_ROOM.l, ROOM_RIGHT=STD_ROOM.r, ROOM_TOP=STD_ROOM.t, ROOM_BOTTOM=STD_ROOM.b;
let MIDX=(STD_ROOM.l+STD_ROOM.r)/2, MIDY=(STD_ROOM.t+STD_ROOM.b)/2;

/* THE ACCESSORS - the honest read, and what a new site should use.

   They answer "where is the wall of the room I am in" from the room itself, every time, so they
   cannot be stale. Each takes an OPTIONAL room, which is the escape hatch that makes the shorthand
   above safe: the day something needs to know a room that is not the current one - drawing the
   neighbouring room through an open door, or a corridor stub, or a second player's room - it passes
   that room in and is correct, rather than reaching for a global that only knows about one.

   They fall back to the standard room when there is no room yet, because the test harness pokes
   Stats and the player before the first startGame, and a helper that throws on a missing room is a
   helper every caller has to guard. */
const boundsOf=(r)=>{
  const q=r||((typeof currentRoom==='function')?currentRoom():null);
  return (q&&q.bounds)||STD_ROOM;
};
const roomL=(r)=>boundsOf(r).l, roomR=(r)=>boundsOf(r).r, roomT=(r)=>boundsOf(r).t, roomB=(r)=>boundsOf(r).b;
const roomW=(r)=>boundsOf(r).r-boundsOf(r).l, roomH=(r)=>boundsOf(r).b-boundsOf(r).t;

/* THE ONE WRITER. Everything above that is a `let` is written here and nowhere else, and this is the
   function to look at when a wall is in the wrong place. */
function syncRoomBounds(){
  const b=boundsOf();
  ROOM_LEFT=b.l; ROOM_RIGHT=b.r; ROOM_TOP=b.t; ROOM_BOTTOM=b.b;
  MIDX=(b.l+b.r)/2; MIDY=(b.t+b.b)/2;
  return b;
}

/* THE CAMERA, and it is a RENDER-TIME TRANSFORM rather than a change of coordinates.

   Nothing in the game moves to accommodate it. Every distance, every body, every projectile and all
   ~208 wall references stay in world space, and the view is `ctx.translate(-cam.x, -cam.y)` around
   the room draw ONLY - the HUD, the minimap and every overlay are drawn outside it, because a HUD
   that scrolled off the corner of a big room would be a HUD nobody can read.

   That choice is the reason the big-room work is cheap. The alternative - moving the world to the
   camera by rewriting coordinates - would touch every arithmetic expression in the game and every
   number in the test suite. This way the camera is one transform and the rest of the game is not
   aware of it.

   THE CLAMP IS WHAT MAKES IT SAFE TO INTRODUCE. A room that already fits on screen gets a camera
   landing exactly on the room's own origin, so the transform is the identity and not one pixel
   changes. Every existing room, every screenshot and all 154 checks are therefore unchanged by the
   camera's presence - which makes the addition provable rather than hopeful. Only a room LARGER than
   the screen moves the view at all. */
const cam={x:0,y:0,w:0,h:0};
/* Declared once. It was declared TWICE in this file with identical bodies - one here and one below
   `cameraTarget` - and function declarations hoist, so the second silently overwrote the first. That
   is harmless until the moment somebody edits one of them, which is the whole hazard: two copies of
   a camera transform, one of which stops being the camera. */
function updateCamera(){
  const t=cameraTarget();
  cam.x=t.x; cam.y=t.y; cam.w=W; cam.h=H;
  return cam;
}

/* THE POINTER, IN THE FRAME THE GAME IS IN.

   `mouse` is where the CURSOR is, which is a fact about the screen: the DOM handler scales clientX
   into the canvas's own 960x600 and stops there. Everything else in the game - the player, every
   body, every projectile, the walls - is in world space. So `mouse - player` was a vector from a
   screen point to a world point, and it was wrong by exactly the camera offset.

   It was wrong by a lot, and unevenly, which is why it read as "a few degrees off" rather than as an
   obvious failure. The camera sits at (-80, 55) in a standard room, and the error in the aim angle
   depends on where the cursor is: measured at six cursor positions around the frame it ran from
   -0.34 to +21.28 degrees, and it changes sign across the frame. Anyone testing by wiggling the
   cursor near one spot would see a small consistent error and conclude the wand was slightly off.

   THE CONVERSION IS AT THE POINT OF USE, not at the event, and that is the part that is not
   cosmetic. If the world position were baked into `mouse` on mousemove, then scrolling the camera
   without moving the cursor would leave `mouse` pointing at a world position that is no longer under
   the cursor - the aim would drift as the player walked, in a room the camera was following them
   through. Reading the camera at the moment of use is the only version of this that is correct when
   the view moves, and it is why this is a function and not a second pair of variables written by the
   event handler.

   updateCamera() is called here rather than trusting cam to be current, for the same reason the tick
   computes nothing it could have inherited: the tick runs before render, so a shot fired on the first
   tick after the camera moved would otherwise use the previous frame's view. Two subtractions and
   two clamps. */
function screenToWorld(sx,sy){
  updateCamera();
  return {x:sx+cam.x, y:sy+cam.y};
}
function mouseWorld(){ return screenToWorld(mouse.x,mouse.y); }
function cameraTarget(){
  const b=boundsOf();
  /* A room that fits the view is CENTRED: the 700x450 room in a 960x600 viewport used to sit at
     (50,130) with 210px of dead space to the right and 80 to the left, which is visible and wrong.

     A room that does NOT fit is the interesting branch, and it is written out longhand because it
     was wrong the first time and nothing caught it. The camera's left edge may range over
     [b.l, b.r-W] - the left edge of the room up to the left edge that puts the room's right edge on
     the screen's right. The clamp has to be to THOSE two numbers.

     It was written as Math.min(b.l, ...) with the player's offset on the inside, which pins the
     camera to the room's left edge whenever the player is right of centre - that is, most of the
     time, in most of the room. The view never moved at all. It passed 154 checks because every
     room in the game fits on screen, so this branch had never executed once: the property that
     made the camera safe to introduce is the same property that hid the bug in it, and the lab is
     the only reason it is now a bug that is fixed rather than a bug that is waiting.

     So the order matters and is the whole content: max(lower, min(upper, desired)). min-then-max
     with the bounds transposed reads plausibly and is what it was. */
  const x=(b.r-b.l)<=W?(b.l+b.r-W)/2:Math.max(b.l,Math.min(b.r-W,player.x-W/2));
  const y=(b.b-b.t)<=H?(b.t+b.b-H)/2:Math.max(b.t,Math.min(b.b-H,player.y-H/2));
  return {x,y};
}
const OPP={N:'S',S:'N',E:'W',W:'E'};
const ROOM_BG={start:'#1c2230',normal:'#191b22',item:'#2a2410',boss:'#2a1414'};
/* BLINK RECHARGE IS A DURATION, AND IT IS NOT SCALED BY TEMPO.

   BLINK_RECHARGE is in ticks, and a tick is 1/TICK_HZ of a second, so the constant is literally a
   count of seconds - 3.5s is 735 ticks. The fill rate in a fight is one tick per tick, which is one
   second of wall clock per tick, so the bar drains in exactly the number of seconds it names. There is
   no conversion at the point of use and no other file has to know the rate.

   It used to be multiplied by TEMPO.rate at the point of use, and that was the wrong coupling in both
   directions. It made this constant lie - sec(6.5) described a 4.33s escape, so the one number in the
   file that a designer would reach for when asking "how long is the blink" was off by a third and
   pointing at a dial that has nothing to do with the player's escape. And it made the escape the one
   cooldown whose real duration nobody could state without also stating TEMPO.

   The cost of dropping it, stated plainly: every OTHER player cooldown still divides by TEMPO.rate, so
   at the current build of TEMPO=1.5 they all read a third quicker than their nominal values while blink
   reads exactly 3.5s. Blink is now the slowest-reading escape relative to the rest of the kit. If TEMPO
   is ever turned into a runtime dial - which the design intends - blink should scale WITH it, and the
   right way is to divide the SECONDS here and leave this constant named for what it is, not to put a
   multiplier back on the fill rate.

   BLINK_FILL_CLEAR is the quiet-room fill, in ticks per tick, and is the only reason a charge feels
   free once a fight is over: 9 per tick against 1 in a fight makes the recharge 9x faster in a room
   with nothing in it. 735/9 = 82 ticks, about 0.39s. */
/* AGGRO_RANGE IS A FUNCTION OF THE ROOM, and it was a number frozen at module load.

   It used to read 0.85 * the room's diagonal, evaluated once when this file was parsed - when every
   room in the game was 700x450, so it was 707 and it was right. A room is now a thing with its own
   bounds, and a 1680x760 one wants 1567. Left as a constant it is a balance number that has quietly
   stopped describing the thing it names: a lunger 900px away in a big room is outside its aggro, so
   it stands still, and the room reads as a safe place to stand still in.

   It is a function now, and that costs one multiply and one hypot per body per tick, which the
   scaling measurement says is free next to what it replaces. In a standard room it returns exactly
   the 707 it always did, so nothing about the current balance moves - which is the property worth
   insisting on, since a fix that also retunes the game is two changes wearing one.

   AGGRO_TIME stays a constant because it is a duration, and a duration does not depend on the room. */
const aggroRange=()=>Math.round(0.85*Math.hypot(roomW(),roomH()));
const AGGRO_TIME=sec(2.5), BLINK_DIST=116, BLINK_RECHARGE=sec(3.5), BLINK_FILL_CLEAR=9, BLINK_IFRAMES=sec(0.17);

/* THE BLINK GRACE: how long after a blink an incoming hit is still forgiven.

   Measured, because the request was for 0.1s and the game already grants 0.40s - BLINK_IFRAMES 36
   ticks plus DASH_TRAIL 48, 84 together, set after the teleport. Those are the real figures and not
   the 0.17 and 0.23 written into the sec() calls, because sec() rounds to whole ticks: 0.17 arrives
   as 0.1714s and 0.23 as 0.2286s. The sum lands on 0.40 either way, which is exactly why the
   error survived a cleanup pass, and a comment quoting the number typed into a function rather than
   the number the game runs on is the failure this note exists to prevent. A 0.1s grace on top of
   that would have been strictly shorter than immunity already in force and would have changed
   nothing at all.

   So the real question is what a 0.1s window was being asked to cover, and the answer is: shots that
   land LATER than the i-frames. Measured, by firing a shell to arrive N ticks after a blink:

       shell 1.2 px/tick   still connects up to 196 ticks (0.93s) after the blink
       shell 4.0 px/tick   still connects up to 120 ticks (0.57s)

   A lunge is worse still, because it is aimed at where the player WAS and crosses up to 280px at
   LUNGE_SPEED. The common case is therefore not "blinked too late" but "dodged the first thing and
   then got hit by the second while the meter was still down", which is what made a fast reaction feel
   unrewarded.

   0.6s covers the measured worst case for a projectile and most of a lunge's. It is a real buff to
   the escape and it is NOT free: BLINK_GRACE is separate from BLINK_IFRAMES, it does not stack with
   it, and the blink still costs a charge on a 3.5s recharge. What it must not become is a way to
   blink into a room and walk through a pack, so the suite measures a full room of lungers walked
   through on consecutive blinks rather than trusting that a longer number means a safer one. */
const BLINK_GRACE=sec(0.6);
// The blink was 140px on an 8s recharge, and together those two made it a teleport with a long
// wait rather than an escape with a cost: nothing about a 140px jump reads as movement, so the
// move is only ever "get me out of here", and an 8s wait means the correct play is to bank both
// charges for an emergency and never use one for ground. Shorter and quicker, plus the landing
// burst below, changes it into a thing you use to keep distance - which is the thing this game is
// actually about. 116px is still most of a body width past a Brunch, and 6.5s is roughly a third
// of a full room, so a panic blink and a positioning blink are different decisions.
const READY=sec(0.75), FADE_OUT=sec(0.35), FADE_CLEAR=sec(0.15), IFRAMES=sec(1), STRIDE=11*SPEEDUP, KNOCK_P_FRICTION=0.958;
const LUNGER_PAY=0.96, LUNGER_WALK=0.3*LUNGER_PAY, LUNGER_RUN=0.82*LUNGER_PAY, MAX_ARMOR=4;
const SHOOT_SLOW_MAX=0.7, SHOOT_SLOW_MAIN=0.35, SHOOT_SLOW_ALT=0.45, SLOW_EASE=0.079, SHOOT_SLOW_RECOVER=0.03*SPEEDUP;
const KNOCK_FRICTION=0.976, KNOCK_GAIN=0.294, KNOCK_P_GAIN=0.3, KNOCK_BOUNCE=0.6, KNOCK_STUN=sec(0.42), KNOCK_TRADE=0.09, KNOCK_MAX=5, KNOCK_CUT=0.006;
// The ramp. A walker used to ease toward its top speed at one fixed rate, so its approach was a
// straight line you could measure in the first second and then forget. Instead the rate itself grows
// with the chase: the first moments are slow and readable, and a body you keep ignoring keeps
// picking up speed behind you. BRUNCH_RAMP is roughly how long to full commitment, and the gain is
// how much faster the acceleration gets once you are there. This replaced a hard "lunge" step that
// jumped the speed up all at once, which read as a bug rather than as pressure. Any body that does
// not shoot back does not ramp, so a Brunch pack still closes at the speed it is supposed to.
const LUNGER_ACCEL=0.0058, WANDER_SPEED=0.25, WANDER_TICKS=sec(1);

/* BRUNCH MOMENTUM: velocity that persists between ticks, so a body that is steering somewhere has
   to stop being there.

   The ramp above changes SPEED - `curSpeed` climbs toward `runSpeed` over BRUNCH_RAMP ticks - but a
   Brunch still moved by adding `dir * curSpeed` to its position every tick with no velocity of its
   own. That makes three things true at once, and all three read as arithmetic:

     - turning is instantaneous. A body that is asked to face a different slot is on the new bearing
       the same tick, so a pack shuffling around a moving shooter snaps between facings rather than
       leaning into them.
     - stopping is instantaneous, and stopping is the common case: every Brunch reaches its slot, and
       then spends most of its life at `md <= 6` where the movement branch changes every tick.
     - overshoot cannot happen, because there is nothing to carry. Momentum is what makes a wall look
       like a body rather than a set of coordinates.

   `vx`/`vy` are the body's actual velocity and the ONLY thing that moves it. The response is
   deliberately asymmetric: accelerate over BRUNCH_ACCEL, decelerate over BRUNCH_DECEL, which is
   shorter. Bodies that get there fast and stop slowly read as heavy; symmetric smoothing makes
   everything feel like it is dragging its feet.

   The dead zone is unchanged and still matters: without it a body sitting on its slot integrates
   velocity toward zero forever and never quite arrives, which shimmers. Inside the dead zone the
   velocity is bled off over a few ticks instead of snapped, so arriving reads as settling. */
const BRUNCH_ACCEL=0.09,      // ~11 ticks (52ms) to reach speed. Quick enough to still feel committed
      BRUNCH_DECEL=0.16,      // stops faster than it starts, so a wall settles rather than coasts
      BRUNCH_DEADZONE=6;     // the slot is "reached" inside this and the body settles into it
// The Brunch ramp. It used to reach full commitment in 0.23s, which meant a pack was on you before
// you had finished looking at where it had come from - there was no interval in which to pick your
// ground, which is the one thing the pack is supposed to be asking of you. 2.2s with a lower peak
// gain is a pack that announces itself and then arrives, instead of one that is simply on top of
// you from the moment it was noticed. A Brunch is still faster than you once it commits - that is
// the whole reason it is frightening - it just commits in front of you now.
const BRUNCH_RAMP=sec(2.2), BRUNCH_RAMP_GAIN=1.9;

/* BRUNCH AS COVER: an incoming shell that touches a Brunch stops there and dies, and the Brunch is
   untouched. Impervious, not armoured - there is no HP to grind down and no counterplay to work out,
   and the pack is a wall that eats a shell a body. It is a big deal to the fight and it was
   invisible, because enemy projectiles only ever collided with lungers and with the player, so a
   shell sailed straight through a Brunch pack on its way to you and the only evidence was a number
   on a health bar.

   These three are the whole of the make-it-visible budget. The ring is drawn in the SHELL's own
   colour and collapses inward, so the player can see exactly which shot was eaten rather than
   inferring it from what did not arrive; an expanding burst would read as an explosion, which is the
   opposite of what happened. BRUNCH_ABSORB_FLASH is on the Brunch, not the shell, and is short
   enough that a pack absorbing three shots in a row looks like a wall working rather than a
   highlight the player has to read past. */
const BRUNCH_ABSORB_R=15, BRUNCH_ABSORB_FLASH=sec(0.1), BRUNCH_ABSORB_PUFF=sec(0.14);

/* THE PACK FORMATION: a Brunch pack advances as a WALL rather than as a crowd.

   Every Brunch used to steer straight at the player, so a pack of eight arrived as a loose mob that
   the player could walk into the middle of and pick off one at a time. That made the most numerous
   enemy in the game the least interesting one, and it wasted the thing a pack already is: a body of
   bodies. A wall is a different fight. It cannot be split by walking into it, it covers the line it
   stands on (which the shell-absorption rule above now makes real cover rather than a decoration),
   and it presents two ranks rather than one, so shooting through it costs you time you do not have.

   THE SLOTS ARE SET FROM THE PACK CENTROID, not from the player, and that is the whole trick. A slot
   is a position in the pack's own frame - across the approach vector and back along it - so the
   formation is a rigid body that turns to face the player as one thing. Steering each Brunch at the
   player instead would produce a crowd every time, because eight bodies converging on one point from
   eight directions is a crowd by geometry, not by accident.

   TWO RANKS, not one line. A single rank is a wall you can shoot down lengthwise, and a pack is
   supposed to be the thing that punishes standing still in front of it. Rank 0 is the front, rank 1
   is behind it, and the two are offset by half a column so a bolt that finds the gap in the front
   rank does not find a body behind it.

   BOTH SPACINGS ARE ABOVE r+r ON PURPOSE. bounceEnemies is a hard positional push rather than a
   force, so it fires whenever two bodies overlap and it will fight a formation slot that asks for
   less than 16px - the wall would shiver in place forever, pushed out by separation and pulled back
   by the slot, and read as a bug rather than as a formation. 19 and 17 leave the separation pass
   with nothing to do, which is what lets a formation hold its shape. */
const BRUNCH_WALL_GAP=19,     // along the wall, between columns. Brunch r is 8, so this clears 2*r
      BRUNCH_WALL_RANK=17,    // between the two ranks, along the approach. Also above 2*r
      BRUNCH_WALL_MIN=3;      // a pack smaller than this is a knot, not a wall, and walks straight in

/* BRUNCH AS A MOVABLE SHIELD: the arc, the target, and the commitment.

   The two-rank wall above is a good formation aimed at the wrong thing. Measured, an eight-body pack
   with a shooter and a gunner in the room, eight seconds: the pack held together well (mean nearest
   neighbour 15.8-19.3px, spread across its own centroid 20-32px) and sat 8-60px from the PLAYER while
   the shooter stood 132-229px away untouched. So it was never a formation failure - it was a wall
   built in front of the wrong body, which is the same thing as no wall at all.

   The shell-absorption rule above already does its half of the job and was verified working: a shell
   fired from a shooter through a five-body wall never reached the player and no Brunch lost HP. The
   cover exists. Nothing was standing where it could be used.

   THE ARC, and the geometry is the whole of it. Slots lie on a minor arc CENTRED ON THE TARGET'S
   HITBOX, not on a line through the pack centroid, because a shield that is centred on itself drifts
   off the thing it is shielding the moment the target moves. Radius is solved from the pack size so
   that every pack fits inside the same angular window:

       arc length  =  (n-1) * BRUNCH_ARC_GAP
       radius      =  arc length / (2 * sin(halfAngle))

   which is the chord-to-arc relation inverted - `n` points spaced `gap` apart along an arc of total
   angle `2*halfAngle` subtend a radius of exactly that. So the cone stays fixed at 45-60 degrees for
   every pack size and the pack grows outward along it, rather than a fixed radius that a big pack
   cannot fit into and a small pack wastes.

   HALF-ANGLE IS CLAMPED TO 45-60 DEGREES. Narrower than 45 and the wall is a post the player walks
   around in one step; wider than 60 and it stops being a shield facing the player and becomes a crowd
   wrapping the target, which is the mob this replaces. A pack of two cannot subtend 45 degrees without
   standing absurdly far out, so small packs sit at the near end of the band - the clamp is what keeps
   every size usable rather than one size correct.

   THE TARGET IS COMMITTED, not re-chosen. A pack that re-picks its shield whenever a nearer shooter
   walks past becomes a thing that oscillates between two enemies, and the player reads that as noise
   rather than as cover. The choice is made when the pack has no target, and it holds until either the
   shield or the target dies - at which point the pack picks again and the new wall is somewhere else
   on screen, which is a legible event rather than a twitch.

   ONLY RANGED ENEMIES COUNT: shooter, gunner and boss. A lunger is not something a Brunch can hide
   anything behind, because it is already on top of the player - shielding it would put the pack between
   the player and a threat that does not threaten from range, which is just a wall in the way.

   WITH NO RANGED ENEMY IN THE ROOM the pack has nothing to shield and advances on the player as a
   wall, which is the two-rank behaviour it already had. That fallback matters: a pack with nothing to
   cover milling around a corner would be strictly worse than the bum-rush it replaces. */
const BRUNCH_ARC_FLOOR=9*Math.PI/180,    // the narrowest a shield may be: enough to be an obstacle
                                           // rather than a post, and roughly one body's width
      BRUNCH_ARC_CEIL=60*Math.PI/180,    // the widest: past this it wraps the target and reads as the
                                           // mob this replaces, which is the failure this prevents
      BRUNCH_ARC_GAP=19,                    // along the arc between slots. Same job as WALL_GAP, on a curve
      BRUNCH_SHIELD_R=118,                  // the MINIMUM stand-off. Below this the wall is inside the
                                            // target's own hitbox and stops being cover; above it the
                                            // wall is placed proportionally to the player-target gap
      BRUNCH_SHIELD_FRAC=0.25,             // a QUARTER of the way from the TARGET, so the pack hugs
                                            // the enemy it is covering. 0.55 was a little over half
                                            // way, which is a barricade halfway to the player rather
                                            // than a shield round a body.
                                            // Close to the target also means more room between the
                                            // wall and the player for everything ELSE in the room to
                                            // get in front of you - which is the other half of why
                                            // this moved: a shield that owns the whole lane is a
                                            // shield the rest of the fight never gets to use
      BRUNCH_SHIELD_MIN=2;                  // fewer than two bodies cannot cover anything
/* HOW OFTEN AN UNEMPLOYED PACK LOOKS FOR A SHOOTER TO PROTECT. 20 ticks is about 95ms: fast enough
   that a shooter walking into a room full of Brunch is covered almost immediately, slow enough that a
   room of packs is not re-deriving the same nearest-enemy answer 210 times a second. Only consulted
   when a pack has NO target - a pack already holding one never reconsiders, which is what keeps the
   wall from oscillating as a nearer shooter walks past. */
const BRUNCH_SCAN_TICKS=20;
/* A GUARDED RANGED BODY HOLDS A LONGER STANDOFF, and the reason is that its own escort is in the way.

   The standoff is where a gunner wants to be: far enough that the player has to come to it, close
   enough that the shot is worth taking. But a Brunch pack forms at BRUNCH_SHIELD_FRAC of the way from
   the TARGET to the player, so the wall sits at a fixed fraction of whatever gap exists. At 0.25 that
   is a quarter of the distance back toward the player - and when the body is standing at its normal
   standoff, that puts its own bodyguard inside the gun's minimum engagement range:

       measured, gap 229px, standoff unchanged: the wall formed 74px from the muzzle.

   A wall 74px in front of a gunner is not cover, it is a muzzle plug. The gunner cannot shoot past it
   - a shell aimed straight at the player now passes through, but the AIM sweep can only find a gap
   about 21 degrees off, which at 229px misses by 91px - and it will not fire into its own escort, so it
   holds its shot. Measured end to end: escorted, a shooter emitted 15 shells and landed 0; unguarded,
   18 shells and 9 hits. Before the barricade moved closer the same escorted shooter landed 9. The wall
   was making the enemy it protects harmless, which is the exact opposite of bodyguarding.

   So a body with guards backs off until the wall it carries is outside its own minimum range. The
   multiplier is on the STANDOFF, not on the wall: the wall still forms at 0.25 of the gap, it just
   forms further out, where there is room for the escort to be beside the gun rather than in its mouth.
   The standoff grows, so the gap grows, so the wall's absolute distance from the muzzle grows with it
   - which is the self-correcting part, and why a single multiplier is enough.

   1.45 is measured rather than guessed: in the fixture above it moves the wall from 74px to about
   150px from the muzzle while leaving the body well inside its `far` limit, so the body is
   repositioning rather than fleeing. A guarded body also stops closing entirely (see the movement
   branch in the tick), because walking toward the player is exactly what shrinks the gap the wall
   needs. */
const GUARD_STANDOFF_MULT=1.45;

/* THE ARC SOLVER: a pure function of (target, player, pack size, slot) returning a slot position, so
   it can be tested with no room, no player and no running fight.

   THE SHAPE. Slots lie on a minor arc centred on a point `BRUNCH_SHIELD_R` out from the target's
   centre along the bearing to the player - that is, on the target's near FACE. The arc is perpendicular
   to that bearing, so it bows across the line the player is shooting down and opens away from the
   target. Centring on the target's own centre instead would put half the shield behind it, which stops
   being cover the moment anything moves.

   THE CONE IS FIXED AND THE RADIUS IS PACK-DEPENDENT, which is the requirement. `n` points spaced
   `gap` apart along an arc subtending half-angle `h` subtend a chord of `2*r*sin(h)`, so for a pack
   whose widest rank is `w = (cols-1)*gap` across, the half-angle that fits it at radius `r` is
   `asin(w / (2r))`. Solving the other way - radius from half-angle - is what makes the CONE the fixed
   quantity and the radius grow with the pack, rather than a fixed radius a large pack cannot fit inside
   and a small pack wastes.

   So the half-angle is the pack's own (bigger pack, wider arc), clamped into the 45-60 degree band.
   Below 45 the wall is a post the player steps around in one move; above 60 it stops being a shield
   facing the player and becomes a crowd wrapping the target, which is the mob this replaces. A pack of
   two cannot subtend 45 degrees without standing absurdly far out, so small packs sit at the narrow end
   of the band and the clamp is what keeps every size usable rather than one size correct.

   The offset from the centreline is then `sin(half) * radius` - the chord, not the arc length, because
   what matters is how much WIDTH the wall subtends across the player's line of sight. The slots are
   placed on that chord, which bows them onto a circle of `radius` centred behind the target: a chord
   rather than a straight line, so the ends of the wall are further from the player than the middle.
   That is the difference between a shield and a barricade, and it is the whole reason for the curve.

   TWO RANKS ON THE CURVE. Rank 0 is the near arc, rank 1 stands `BRUNCH_WALL_RANK` further out along
   the SAME bearing, which keeps the property the two-rank wall was built for: a shot that finds the
   gap in the front rank does not find a body directly behind it. Rank 1 shares the target's bearing,
   so both ranks present the same face to the player rather than the back rank splaying outward.

   `null` for a pack too small to cover anything, which is what sends it back to advancing on the player
   instead of forming a degenerate one-body arc. */
function brunchArcSlot(tx,ty,px,py,n,slot,tgtR){
  if(n<BRUNCH_SHIELD_MIN) return null;
  /* bearing target -> player. Everything is measured along it. */
  const dx=px-tx, dy=py-ty, d=Math.hypot(dx,dy)||1;
  const ux=dx/d, uy=dy/d;                    // unit vector, target toward player
  const cols=Math.ceil(n/2);
  const rank=slot%2;
  const col=((slot/2)|0)-((cols-1)/2);       // symmetric about the centreline, in half-steps
  /* THE STAND-OFF IS A FRACTION OF THE GAP, NOT A CONSTANT, and the constant was badly wrong.

     `BRUNCH_SHIELD_R=118` sounds like a reasonable place for a shield to stand. It is not, because
     the thing it has to fit between is the distance from the player to the enemy being shielded - and
     that is not a fixed quantity. It is whatever the fight has produced so far, and it gets SMALL: a
     player who closes on a shooter is inside 118px inside a second.

     Measured before this was fixed: player 170,330 and shooter 334,330 is a 164px gap, and the shield
     formed at radius 118 - which is BEHIND the player. The wall was correctly built, correctly
     centred on the target and correctly angled, and it was on the wrong side of the person it was
     supposed to be protecting. Line of sight was blocked 27-39% of the way rather than being closed.

     So the stand-off is a share of the gap: far enough out to be a substantial obstacle, never so far
     out that it overshoots. At 0.55 the wall sits a little over half way from the target to the
     player, which leaves the player room to reposition around it rather than being sealed behind a
     wall they cannot get past - and at close range it converges on the target rather than passing
     through the player to stand somewhere unreachable.

     Measured after: player 170,330 and shooter 334,330 is a 164px gap, so the wall forms at 90px from
     the shooter - that is 80px from the player, well inside the room between them, instead of 118px
     which put it behind the player's own position. */
  const gap=Math.max(BRUNCH_SHIELD_R*0.35,d);
  const radius=Math.min(d*BRUNCH_SHIELD_FRAC, d-ENEMY.brunch.r-2)+(rank?BRUNCH_WALL_RANK:0);
  /* THE CONE IS SIZED TO THE THING BEING COVERED, and the 45-degree floor is GONE.

     The wall was specified as a minor arc subtending 45-60 degrees, and that shape is what it took -
     but measured against a shooter it was mostly empty:

         pack   wall width   slots actually blocking a 23px line   % of the wall doing anything
           3       140px                    0                              0%
           6       152px                    2                             33%
          12       152px                    4                             33%

     A Brunch stops a shell within `BRUNCH_ABSORB_R + r` = 23px of the line, so a 152px arc spends most
     of its bodies 60px off the shot path: a wall wide enough to walk around and too thin to block
     anything. The floor was the cause. A shooter's hitbox at the measured 164px gap subtends 22.8
     degrees, so `max(45, 22.8)` was always 45 - the floor could never lose, whatever the target.

     So the cone is the target's own apparent width, plus one body's margin either side, and the only
     remaining limit is the 60-degree ceiling - which stops a large boss at close range from wrapping
     into a mob, which is the thing this whole change exists to remove.

     The arc keeps its 45-60 degree CHARACTER for a large target, because a Warden across the room
     genuinely does present that much width. The difference is that it is now derived rather than
     assumed, so a small enemy at close range gets a shield sized to it rather than a barricade with
     three useful pixels. */
  const apparent=Math.atan2((tgtR||12)+BRUNCH_ARC_GAP*2,Math.max(1,d-radius));
  const half=Math.max(BRUNCH_ARC_FLOOR,Math.min(BRUNCH_ARC_CEIL,apparent));
  /* THE SLOT'S POSITION ON THE ARC, and this is where the pack-size dependence actually lives.

     The cone is the target's apparent width - a fixed thing, the same for a 6-pack and a 12-pack. What
     changes with pack size is how many bodies have to FIT ALONG it, and the original `frac = col /
     ((cols-1)/2)` spread them to the cone's edges regardless of whether they fit there:

         pack   wall width   slots blocking a 23px line
           3       121px                    0
           6       142px                    2
          12       142px                    4

     The width barely moved with pack size, which is the tell: bodies were being placed past the end
     of the wall rather than along it. 12 bodies on a wall that 6 bodies already overran means half
     the pack was standing in empty floor beside its own shield.

     So the columns are spaced along the cone at `BRUNCH_ARC_GAP` and CLIPPED to the cone's own arc
     length - and the surplus stacks BEHIND the front rank at the same angle instead of sliding
     outward. A shield that is two deep in the middle and thin at the ends is still a shield; one that
     is a single file with six stragglers beside it is a queue. */
  const arcLen=2*half*radius;
  const maxAlong=Math.max(0,arcLen/2-BRUNCH_ARC_GAP*0.5);
  const rawAlong=col*BRUNCH_ARC_GAP;
  const along=Math.max(-maxAlong,Math.min(maxAlong,rawAlong));
  /* rank 1 sits behind rank 0 at the same angle, so surplus bodies deepen the wall rather than
     widening it. The second rank repeats the angle because it is deliberately the same face. */
  const ang=along/Math.max(1,radius);
  const ca=Math.cos(ang), sa=Math.sin(ang);
  const qx=-uy, qy=ux;                        // perpendicular to the bearing
  return {x:tx+ux*radius*ca+qx*radius*sa, y:ty+uy*radius*ca+qy*radius*sa};
}


/* A lunger does not walk at you. It closes the distance, stops, TELLS you where it is going, and
   then commits to a straight line at that point. Every number here exists because of a specific way
   the old straight-line chase was unfair:

   LUNGE_CD  the gap between lunges. Without it a body that overshot simply turned around and wound
             up again at once, so a room of four lungers became four things committing on one
             shared clock - which is the definition of unfair rather than hard.
   WINDUP    the reaction window, and the number actually doing the fairness work. It has to be long
             enough that a player with the whole screen to themselves can read the tell, back off,
             and watch it miss. It is deliberately NOT longer than that: in a mixed room the OTHER
             enemies are what make a fair-length window insufficient, and that pressure belongs with
             them rather than inside the lunger's own timing.
   REACH    with LUNGE_SPEED, the shape of the commit. The lunge is not a fixed length: it is solved
             as an intercept, so it is exactly as long as the shot that actually lands. Short and
             fast is dodged by one decisive sidestep and is over; long and slow is a wall you cannot
             walk around - and a player running in a straight line is already standing on the
             solution, so they get the wall.
   RECOVER   what pays you for having dodged it. The point of a committed attack is the moment where
             the attacker is committed to having missed, and with no window where the lunger is slow
             and facing where it went, evading one only means standing in front of the next one.

   The direction is captured when the windup BEGINS, never when it ends. A lunge that re-aims mid
   charge cannot be dodged and makes the tell a lie.

   PREDICT is the one place a lunger is allowed to be clever, and it is deliberately much weaker
   than a gunner's. A shell leads you by your full velocity across its whole flight, so holding a
   heading is punished by arithmetic. The lunger leads by this fraction of ONE windup and only along
   your current heading, so a player who changes direction inside the window is not read at all -
   the same counter-strafe the gunners already reward, now being rewarded a second time. It aims at
   where you are plus a little, not at where you are going.

   LEN_MIN/MAX let the lunge stretch or shrink to try to reach you. A fixed length means a lunger
   that starts its charge too far away simply falls short every time, which reads as the attack
   whiffing for no reason at all. Scaling it to the gap means the charge is always visibly committed
   to covering the distance, and the variation is what makes it feel like an animal aiming rather
   than a timer firing. */
const LUNGE_CD=sec(1.15), LUNGE_WINDUP=sec(0.34), LUNGE_SPEED=4.2, LUNGE_RECOVER=sec(0.55);
const LUNGE_RANGE=178, LUNGER_NEAR=0.55, LUNGER_FAR=1.3;
const LUNGE_REACH=280, LUNGE_FLOOR=56;
// The lunger holds this far off and lunges ACROSS the gap. It used to have no standoff at all: it
// walked straight at the player forever and then "lunged" from zero distance, which is why the hit
// rate was a hundred percent and the quality of the prediction was irrelevant - you cannot dodge an
// attack that starts on top of you. The gunner has always had a close/far band and holds station in
// it. The lunger is the only walker that did not, and it is the one whose whole mechanic is a read.
const LUNGE_HOLD=78, LUNGE_MIN=34;
// Two lungers meeting mid-charge. Light enough that the loser is out of the fight for a moment
// rather than launched, heavy enough that it reads as a collision and not as a graze.
const LUNGE_CLASH=5.5;
// How fast a lunger walks its own angle around the player, in px/tick, aimed tangentially so it can
// never change how far away it is. This is the whole of the anti-front mechanic and it is a single
// number on purpose: at a third of a pixel a tick the bodies take a second or two to fan out, which
// reads as encircling rather than as teleporting into position. Scaling it by the approach speed
// instead does nothing at all, because the approach speed is zero inside the standoff - which is
// exactly where a pack spends most of its time.
const LUNGER_SPREAD=0.9;
// How the lunger tracks the player's heading over time. This is the SLOW average, kept for the
// shooters' belief about speed and as the fallback when a player is too slow to have a heading at all.
// It is NOT what the lunge is aimed from any more - see solveIntercept and LUNGE_BELIEF_TRACK below,
// because an average with a third of a second of memory lags a human reversal and aims lunges at
// where the player has just been. LUNGE_CONF_MIN used to sit here and is gone: see below.
const LUNGE_TRACK=0.014;

/* WHICH WAY THE LUNGER THINKS THE PLAYER IS GOING. LUNGE_TRACK above smooths a VELOCITY over about
   0.34s, which is the wrong question for an attack that has to commit inside a 0.34s windup: a
   person reverses faster than the average can follow, so it reports the direction they have just
   left. Measured at the instant of each commit, that put 289 of 900 lunges - nearly a third - aimed
   against the player's actual heading.

   LUNGE_BELIEF_TRACK is a unit heading (not a velocity) and is deliberately quicker, so the belief
   has turned over by the time the lunge lands rather than partway through it. It is not as fast as
   it could be because a heading read from a single tick flips on any jitter. PLAYER_SPEED_EPS is
   the floor below which the player is treated as stationary and the old long average is used
   instead, which is the one case a slow average is genuinely good for. */
const LUNGE_BELIEF_TRACK=0.14, PLAYER_SPEED_EPS=0.05;
// The lunge reaches as far as the intercept solution needs and no further. A lunger whose solution
// is outside this does not commit at all - it keeps closing at its approach speed until the solution
// fits, which is what "too far" means: not that the attack fails, but that it has not started yet.
const LUNGE_ITER=3;
  // The blink leaves six puffs that fade over DASH_TRAIL. A lunge is a little shorter than the
  // blink's 140px and a fraction of the time, so it gets the same sprite, a shorter life, and a
  // pulse that GROWS through the windup - which is the part that carries the information. A static
  // glow for a fixed duration tells the player that something is about to happen; a swelling one
  // tells them when, and when is the part they can act on.
  const LUNGE_TRAIL=sec(0.16), LUNGE_CHARGE_TRAIL=sec(0.34);
// how long the hook must have been in the air before a second right click can detonate it early.
// Below this the click is eaten rather than acted on, so that the cast and the cancel cannot be the
// same input - otherwise a held button blows the hook up at your own feet one tick after throwing it.
const HOOK_EARLY_MIN=sec(0.18);
// How fast it closes before it is close enough to care. A lunger that simply arrives fast takes
// away the player's ability to choose the ground before the fight starts, which is where most of
// the "there was no room to do anything" feeling comes from. So: far is quick, near is deliberate.
function approachSpeed(d){ return LUNGER_NEAR+(LUNGER_FAR-LUNGER_NEAR)*Math.min(1,d/LUNGE_RANGE); }

// every landed hit drags the body down for a moment; base speeds pay for it with LUNGER_PAY (-4%)
const HIT_SLOW_MULT=0.62, HIT_SLOW_TICKS=sec(0.55);
// the enemy-facing position trails the real one and converges in ~0.4s, so a bullet aimed at the
// spot the player just left is not a guaranteed hit
const HITBOX_LAG_EASE=0.05, HIT_FLASH=sec(0.13), BURST_TICKS=sec(0.2), MUZZLE_TICKS=sec(0.1), DASH_TRAIL=sec(0.23), IFRAME_FLICKER=sec(0.067);
// a hit is a translucent wash over the body, not a white cut-out: it fades from max to min as the
// flash decays, so a body under the Beam's fifth-of-a-second cadence still reads as a creature
const HIT_FLASH_MAX=0.62, HIT_FLASH_MIN=0.38;
const SHOT_DMG=1.8;   // a chip off a lunger's HP rather than half a heart

/* THE WARDEN, the boss. Tuning only - the behaviour is in 60-tick.js beside the tick that runs it.

   It was a large lunger with the boss explicitly excluded from firing: 50 HP of walking, which is not
   a fight. Everything here exists to make it one, and every number is chosen so that a player who
   READS it takes very little damage and a player who reads nothing takes all of it. That gap is the
   fight, and it is the only thing a boss is allowed to do to make itself hard.

   PHASE thresholds are a FRACTION of max HP, not an absolute, because the depth ladder scales body
   health and an absolute threshold would mean something different on every floor - phase 2 arriving
   at 40 HP on floor 3 and at 300 HP on floor 12. */

const BOSS_VOLLEY_N=3,        // shells per volley. Three is a pattern with a shape; five is noise
      BOSS_VOLLEY_GAP=sec(0.55),  // between shells, and each is re-told, so one can be answered alone
      BOSS_PHASE_1=0.66, BOSS_PHASE_2=0.33,   // fractions of max HP
      /* The Warden's name, and the fact that it now exists at all. The boss was called THE WARDEN in
         a dozen comments and had no name field, so the name was never on screen during the one fight it
         was written for. A player is asked to learn a boss, and a boss that is never named cannot be
         referred to, remembered, or argued about afterwards. */
      BOSS_NAME='THE WARDEN',
      /* The health bar's own numbers, because a bar that governs the fight should be sized by the
         fight rather than by whatever fitted above the room. It spans the room's own width, so it
         lines up with the play area instead of floating in the middle of the screen, and it is 10px
         tall where a body's is 4 - this is the one health bar the player is meant to watch rather than
         glance at, and it is the only one that shows what is LEFT rather than what just happened. */
      BOSS_BAR_H=10,
      /* The bar's rectangle is anchored to the CANVAS, not to the room, and that is not a preference.

         A room-anchored bar at the bottom works on a floor, where the room ends 20px above the canvas
         bottom and the camera is pinned - but the Lab is 1680x760 and its room runs to y 890 with the
         camera scrolling, so a room-anchored bar lands 290px off-screen. The Lab is where every tell
         gets checked without playing a run, so a bar that disappears there is a bar that is only
         really drawn in one of the two places it can be drawn.

         So it hangs off the canvas bottom edge, which is fixed in both, in the 20px strip below the
         room. 20px is not much: the bar is 10 tall with a 3px frame, and the name and phase go ABOVE
         it rather than below, because below is the canvas edge. Everything else about it is unchanged
         and it still spans the room's width so the two notches stay legible across 660px. */
      BOSS_BAR_MARGIN=23,   // canvas bottom to the bottom of the BAR. 23 rather than the 14 it started at,
                       // because the bar carries a 3px frame and at 14 the frame straddled the room's
                       // bottom wall (wall at y 580, frame ending at 589) so the bar looked bolted to
                       // the masonry. 23 puts the frame's bottom edge exactly on the wall line.
      BOSS_BAR_INSET_X=20,  // inset each side from the room's own walls, so it lines up with the play area
      BOSS_RECOVER=sec(1.1),   // dead time after every move. The gap the player spends reading the next
      BOSS_CD_MIN=sec(3.2), BOSS_CD_VAR=sec(1.0),   // seconds BETWEEN moves. The single most
                                // important number on the boss, and the one that was wrong first.
      BOSS_SHELL_DMG=1.44,   // was SHOT_DMG*0.8, which is 1.4400000000000002 in binary floating point -
                                // seventeen significant digits for a tuning number, and an arithmetic
                                // accident that then propagates into everything derived from it. The
                                // intent was always 1.44, so that is what is written here.
                                //
                                // 1.44, not 2.88. A body firing THREE shells at a time cannot
                                // also hit for 36% of the player's health bar per shell, or the
                                // player has to dodge 94% of what is fired and reading the tells
                                // stops mattering - which is the one thing a boss may not do.
      BOSS_WALL_HP=5,          // bodies in the wall the boss calls
      BOSS_WALL_ID=-1,         // a pack id no room generator can hand out, so a called wall is its own
      BOSS_SWEEP_DIST=150,     // how far the sweep carries it
      BOSS_PHASE_FLASH=sec(0.8);  // the banner, which is not skippable - a phase change the player
                                  // cannot see is a difficulty spike wearing a disguise
const SPAWN_MARGIN=76, SPAWN_MID=104, SPAWN_SEP=158, SPAWN_DOOR=130, SPAWN_FAR=210;
// the simulation ticks at a fixed TICK_HZ whatever the monitor refresh rate; STEP_TOL absorbs rAF
// timer jitter so a 60Hz screen gets a steady 3.5 ticks per frame, MAX_CATCHUP_MS caps the run
// after a hitch
const STEP_MS=1000/TICK_HZ, STEP_TOL=STEP_MS*0.12, MAX_CATCHUP_MS=250;
const WEAPONS=[
 // dmg is the point-blank value; fNear/fFar/fMin shape the falloff (see falloffMult) so a shot
 // fired from across the room is a real commitment while close work is untouched.
 //
 // What separates one gun from another is never the damage number, it is the shape of the problem
 // the gun solves. Each of these has exactly one:
 //
 //   Bolt      the single-target answer. One committed shot, the biggest hit in the game, and the
 //             one gun that is unambiguously right when there is one thing that must die now.
 //   Scatter   the crowd answer at knife range. Falls off faster than anything else, so the room
 //             it works in is the room you are standing in.
 //   Beam      sustained fire. Fastest cadence, tiniest hits, the only gun you hold the trigger on.
 //   Voidball  the line answer. Passes through bodies, so what it is worth depends on how many are
 //             standing in a row - which makes it a utility gun rather than a damage gun, and the
 //             reason it is the weakest of the four on any one body.
 //
 // The Bolt and the Voidball were both "purple, one pellet, gradual falloff" and that is exactly
 // why they read as the same weapon. The damage difference between them is deliberately small -
// roughly a third - because the point of the Voidball losing the single-target fight is to make it
// a choice rather than an option, and a Voidball that was merely slightly ahead of the Bolt would
// be the Bolt with a different name.
 //
 // None of them are meant to carry a run on their own. The stat upgrades have to have somewhere to
 // go, and a weapon that is already the right answer at wave one leaves them nothing to buy.
  {name:'Bolt',color:'#c79bff',cooldown:38*SPEEDUP,speed:2.8,dmg:7,count:1,spread:0.05,r:6,shrink:true,fNear:130,fFar:400,fMin:0.5},
  // Scatter is a buckshot gun, not a hose: a lot of pellets in a narrow cone, and a long wait
  // between pulls. Everything about it is built backwards from a rapid-fire gun - the spread is
  // tight enough that the whole bunch lands as one hole at close quarters, and the cooldown is
  // four times the old one so you cannot lean on it. Per-pellet damage is small, so what it is
  // worth swings hard with range: up close it is the biggest thing you own, across the room it is
  // pellets, not the width of the cone. The spread is now three numbers instead of one, because the
  // old single `spread` could only fan the pellets by ANGLE from a single point, and a fan from a
  // point is a V that opens with range - the same shape as the beam, which is what made this read as
  // a wave shot rather than a shell of shot. The three replace it:
  //   muzzleJitter     px of random muzzle position. Constant with range, so this is the floor the
  //                    column never drops below, and the term that stops it ever becoming a cone.
  //   pelletAngle      rad of random aim error per pellet. Small enough to stay a column.
  //   pelletSpeedVar   fraction of speed variance per pellet. The chaos, and the reason the column
  //                    opens with range without any angle involved at all.
  // Measured column width: 11px at 60px, 13px at 200px, 17px at 450px, against 19/63/142 for the old
  // cone. The pellet COUNT is unchanged at eight; the damage per pellet is not, because the gun had
  // to be able to one-shot the biggest normal body with a full volley - see dmg below.
  {name:'Scatter',color:'#e8502a',cooldown:sec(1.1),speed:2.4,dmg:3.9,count:8,spread:0.045,
   /* dmg 3.9 is not a round choice and 3.835 would not do either. It is the narrowest value that
      makes the stated contract true: EIGHT pellets kill a lunger and SEVEN do not.

        8 x 3.9 x ARMOUR 0.66 = 20.59   vs lunger 20.25   kills, by 0.34
        7 x 3.9 x ARMOUR 0.66 = 18.02                     short by 2.23

      3.835 is the exact knife-edge - 8 pellets land precisely 20.25, with nothing to spare and no
      tolerance for a single pellet missing. 3.9 keeps the "all eight" requirement intact while
      leaving the gun a sliver of margin, and it is still below the 4.5 at which SEVEN would kill,
      which would quietly delete the requirement instead of satisfying it.

      This is a real buff - 3.9 against 2.6 - and it was forced rather than chosen. ARMOUR is 0.66,
      so eight pellets at 2.6 land 13.73 on a lunger and the weapon could not kill one at ANY cone
      angle, at any range, including point blank: 0 kills in 24 trials from 0.92 to 22.92 degrees. A
      shotgun whose pattern has to be tuned to reach its own damage threshold is not a shotgun with
      a range, it is a shotgun with a puzzle. The damage had to clear the body first; the cone angle
      is then free to be only about how much of the volley survives the trip.

      Note the Brunch needs exactly ONE pellet (2.7 hp, no armour), so this does nothing to them -
      a shotgun killing a chip body should be free, and the pack numbers still hold. */
   /* The effective range is a CIRCLE of 180px radius - a QUARTER of a 700px room - and inside it the
      gun does FULL damage. A room is 700x450 (measured: every room, every type, five seeds), so a
      quarter is 175, a third is 233 and a half is 350. falloffMult is already radial (it measures
      hypot from the shot origin), so fNear IS that radius and no new machinery was needed.

      This number has been walked down twice and the reasons are the point. It was 175 to begin with,
      which was measured against a room half the size and was really a quarter of one - the comment
      beside it called it "half a room" for a room that is 700 wide. At a 0.45 floor the Scatter
      one-shot EVERY normal body out to 350px, and the gunner, shooter and Brunch all the way to the
      far wall, because even the floor was worth more than their health. A shotgun that clears a room
      from the far corner has no range and no room in which another gun is the right choice.

      Then 233 with a 0.30 floor, which was still reported too easy at medium range: a lunger at 350px
      - a body in the middle of the left of the room, the player in the middle of the right - still
      died in 2 volleys / 2.2 seconds. So the threshold came back DOWN to a quarter and the floor to
      0.22, and the lunger at 350px costs three.

      fFar 320 is where the curve reaches its floor, comfortably inside half a room, so a shot is
      never caught mid-curve against plaster. fMin 0.22 is the number that actually decides the 350px
      case: measured live, 0.30 gives 2 volleys there and 0.22 gives 3.

        180px and in  one shot against everything, including a 20.25 hp lunger
        350px       shooter 2   gunner 3   lunger 3   Brunch still 1

      MEASURED IN THE SIMULATION, and the table is not enough: a lunger WALKS TOWARD THE PLAYER while
      the volley is in the air - 350px when fired, ~318px when the last pellet arrives - and
      falloffMult is read at the moment of the hit. The eight pellets of one volley are taxed at eight
      different distances, multipliers running 0.302 then 0.376 then 0.491. The static table says a
      lunger takes 5 volleys at 350px; it takes 3. Any fairness claim about this gun has to be
      measured, because the table describes a fight that does not happen.

      The Brunch needing one pellet at close range is deliberate and unchanged: they are 2.70 hp with
      no armour and a shotgun should not have to work for them. */
   fNear:180, fFar:320, fMin:0.22,
   muzzleJitter:5,pelletAngle:0.008,pelletSpeedVar:0.25},

/* THE ARCANE BEAM RAISED 0.84 -> 1.55, and the reasoning is worth keeping because two earlier
   diagnoses were wrong.

   The report was that the beam took six to seven seconds of near-perfect tracking against one lunger,
   that every shot landed, and that it was by far the worst weapon in the game. The spread was ruled
   out by the player directly and again by measurement - tightening the cone from 0.16 to 0.08 rad moves
   the time from 3.04s to 2.75s, which is inside the spread error bar and nowhere near the complaint.
   Raising the projectile speed was measured too, because the beam flying at 4.0 px/tick against a
   lunger lunging at 4.2 looked like a genuine race it could not win. It is real, and it is worth
   almost nothing: 4.0 -> 9.0 px/tick changed the time by 3.04s -> 3.10s, because time-to-kill is set by
   how many shots LAND and the gun fires on a cooldown either way.

   Falloff was checked three ways and is applied once, correctly, from the muzzle. It is not the cause
   either, and the beam is in fact the best long-range gun in the roster: fMin 0.60 against the Bolt's
   0.50, so at 450px it keeps 60% of its damage where the Bolt keeps half.

   What the buff is really buying is CHUNK. 0.84 was the only sub-one-damage number in the game - a
   lunger lunge does 2, a shell 1.8 - so a 24 HP body took twenty-nine hits to kill, and a gun that
   asks for twenty-nine hits is a gun whose time-to-kill is dominated by how long you can afford to
   stand still. 1.55 takes it to sixteen, and it lands the beam in the same window as the others when
   the fight is the one that was described: a target, a melee lunger, a gunner behind, dodging. Measured
   there, at 250px and 350px:

       dmg 0.84   3.53s / 3.60s     (the Bolt, same scenario: 2.97s / 3.00s)
       dmg 1.55   2.28s / 2.73s     (the Bolt: 3.39s / 3.00s)

   It also eases a distortion the old number caused. Strength is added FLAT, so a +4 is +476% on a 0.84
   gun and +258% on a 1.55 one. The beam was the weapon a single item broke, which is the opposite of
   what a low base is supposed to do.

   The cone is deliberately unchanged. 0.16 rad is the widest in the roster and it is what the beam is:
   Precision narrows it from 9.2 degrees to 0.4, a twenty-three-fold range that no other gun has, and
   that range is the whole reason Precision exists as a stat. Tightening it would have cost the weapon
   its identity to buy a tenth of a second. */
 /* THE BEAM IS 0.5, AND THAT IS THE POINT. It was 1.55. This reverses the last change to this
   number and the earlier reasoning is kept above so the disagreement is visible rather than buried.

   It was raised off 0.84 because a low base made one item break the weapon: "Strength is added FLAT,
   so a +4 is +476% on a 0.84 gun and +258% on a 1.55 one. The beam was the weapon a single item
   broke, which is the opposite of what a low base is supposed to do."

   That is a real problem and it is also the identity. Strength is a per-SHOT flat bonus, and the beam
   fires 17.31 times a second against the Bolt's 2.37 - so a flat bonus is a per-SECOND bonus in
   disguise and the rate is the multiplier. A low base is therefore not weakness, it is headroom: the
   gun starts as the worst thing you can hold and becomes the best thing you have put work into.

   Measured damage per second, 20 trials, one immortal lunger at 200px, as Strength sigils go in:

     sigils   strength    Bolt     Beam    Beam/Bolt
         0       +3       13.9     19.0      x1.37
         1       +4       15.3     25.3      x1.65
         2       +5       16.7     30.4      x1.82
         3       +6       18.1     34.5      x1.91
         4       +7       19.5     40.8      x2.09
         6       +9       22.3     51.2      x2.30
         9      +12       26.4     68.9      x2.61

   So the first sigil is worth +35% of the beam's damage and +10% of the Bolt's - 3.5x, not the 7.3x
   the raw arithmetic suggests, because the beam misses 57% of its shots at this range. That is the
   canvas, and it is measured rather than argued.

   NOTE WHAT TTK HIDES. Judged by time-to-kill the first sigil looks worth -21% to BOTH guns, which is
   how this nearly got recorded as a non-effect. TTK is quantised by whole shots - a gun needing four
   hits and a gun needing twenty-one both step in chunks - so it cannot show a scaling curve. The
   damage-per-second table above is the one that answers the question, and the two disagree because
   they are measuring different things.

   Tuned at 200px, one body, mean of 25 trials, on a lunger:

     Bolt 2.00s     Beam at 1.55: 1.17s   x0.58   (1.7x faster than the Bolt)
     Bolt 2.00s     Beam at 0.50: 1.41s   x0.70   (1.4x faster - slightly ahead, not dominant)

   0.5 is the measured value and not a rounder one nearby: 0.25 measures x0.72 and 0.10 measures
   x0.80, which is past the point where the gun stops feeling like a gun at all.

   The floor is worth stating because it is not a tuning failure. With the beam's damage at ZERO it
   still measures x0.77 - still faster than the Bolt - because Strength alone is already larger per
   shot than the whole per-shot budget the Bolt needs. No damage number reaches parity, because the
   rate alone guarantees it. 0.5 is as close to "slightly faster" as this weapon can be made without
   taking the trigger off it, and the trigger is the weapon.

   THE NICHE THIS BUYS, which does not exist yet: anything applied PER BULLET - poison, bleeding, a
   stacking burn - is worth 17.31 applications a second here and 2.37 on the Bolt. That is the larger
   half of the canvas, and right now only Strength sigils feed it. Balance is provisional on that
   content arriving; if it never does, this number should be revisited upward.

   200px is the range this was tuned at and it is a choice, not a fact. The beam's hit rate is 81% at
   100px and 27% at 300px, so it is a considerably different weapon at either end and the balance is
   only pinned for the middle. */
  {name:'Arcane Beam',color:'#3fa9ff',cooldown:5.2*SPEEDUP,speed:4,dmg:0.5,count:1,spread:0.16,spreadFromPrecision:true,r:4,fNear:170,fFar:470,fMin:0.6},
  {name:'Voidball',color:'#3f8a4a',cooldown:24*SPEEDUP,speed:3.2,dmg:3.4,count:1,spread:0.02,fNear:150,fFar:440,fMin:0.55,pierce:3},
];
// What each pass through a body is worth, as a fraction of the first. A pierced bolt that does full
// damage to four bodies is simply a better Bolt with a bigger number on it; the falloff is what
// makes it a distinct thing to watch travel, and it also means the bodies at the back of the line
// are the ones that survive the shot - so lining the pack up matters more than hitting it at all.
const PIERCE_FALLOFF=0.72;
// The Bolt is the one gun whose damage visibly leaves the projectile as it travels. fMin is a floor,
// not zero, so a spent Bolt never quite disappears - it just stops being the thing that ends a
// lunger, and a shrinking bolt is a much earlier and more honest warning of that than a number the
// player has to remember.
//
// The band is deliberately narrow. The first attempt drew it at 10.2px at the muzzle against a
// collision radius of 6, which read as a different gun rather than as a visual tell, and shrank it
// to 2.5px, which read as the shot being switched off. What is wanted is a change the player can
// feel without being startled by: a shade bigger than the plain bolt, and a shade smaller at the far
// end than at the near end. So 6.3px down to 3.9px, against a 5px bolt before any of this.
//
// It only scales what is DRAWN. The collision radius is fixed, so the game does not become harder to
// aim with a gun that was already the easiest to aim - if a player misses a far shot, that is a
// positioning problem, not one they were secretly given by a rendering change.
const BOLT_SIZE_MIN=0.65, BOLT_SIZE_MAX=1.05, BOLT_DRAW_R=6;
// The right click has two forms, and you start with the one that ends things. The blast is a
// killshot and a shove, and it is the only weapon in the game that actually removes bodies outright,
// so a run that goes badly is usually a run where you have not found the secret room yet. The hook is
// behind the fake wall: it phases through everything on the way in, goes off exactly where you
// clicked, drags what it caught into that point, and leaves a spell on the floor that holds the knot
// while you shoot into it. It deals no damage of its own. Swapping to it is a real trade - you give
// up the thing that deletes a body and get the thing that buys you two seconds - and the fact that
// neither is strictly better is the point, because the second one is behind a wall most runs never
// find, so the run has something to be about.
// damage taken by a big body is multiplied by ARMOUR, which is what makes the kit matter: sustained
// chaff fire is the wrong answer to an armoured target and a single committed shot is the right one,
// so the Bolt and the Voidball keep most of their value while the Scatter and especially the Beam
// lose most of theirs. It is a per-hit multiplier rather than a subtraction so it stays correct
// through the Voidball's falloff, and it changes no timing - a shot that would have hit still hits,
// it just counts for less. The damage pools divide by it so they keep the job they had before it
// existed: one budget, shared out, still enough to kill a lone heavy outright.
// 0.66 is deliberately past the point where my own test bot could finish a run. That is the call
// being made here: playtesting from simulation has a ceiling, and a bot with perfect kiting and no
// route planning is a poor stand-in for someone who actually wants to get to the bottom. The armour
// is a single constant, so if it turns out to be too much it is one number to turn back.
const TOUGH=1.35;

/* THE DEPTH LADDER ---------------------------------------------------------------------------------

   Difficulty in this game is a function of DEPTH. Not of the player's health, not of their build, not
   of how many items they are carrying, not of how well the last floor went. A no-item run to floor 5
   meets exactly the fight a five-item run to floor 5 meets. That is the rule, and it is a strange
   one: it means a strong build does not make a floor easier, it makes the same fight winnable more
   comfortably. The floor is the floor.

   The consequence that actually costs something is that the player CANNOT be rewarded for a good run
   by the game quietly easing off, so every good-run reward has to come from somewhere the depth
   ladder cannot reach - a better weapon, a tighter route, more health in the bank when it matters.
   That is the right shape for a roguelite, and it is also the only shape that makes "how deep have
   you gone" a number that means anything, because the number is not secretly a measure of how many
   hats you collected.

   It was LINEAR until it was measured, which is the whole reason this comment is here, and the
   measurement is still the best argument in the file. The brief asked for linear "for now", which
   was the right instinct - a linear ladder is the simplest thing that could be right and the easiest
   to reason about - and it turns out three linear ladders multiplied together is not a difficulty
   curve, it is a brick:

     floor  tough  rate  bodies  shells/s per gunner  time to kill one lunger on the Bolt
       1    1.00   1.00      6          1.3                    0.72s
      10    3.70   2.44     79          3.8                    2.35s
      20    6.70   4.04    159          7.4                    4.34s
      30    9.70   5.64    240         12.3                    6.15s
      50   15.70   8.84    400         24.8                    8.62s

   Fifty-five times the effective health pool and sixty-seven times the bodies, and the room on
   floor 50 was asking for FOUR HUNDRED of them. That is not a hard game, it is a game that cannot
   be finished, and "hundreds of hours" was the brief - so the wall cannot be at floor 20.

   IT WAS THEN SATURATING, which fixed that and introduced a quieter problem: `1 + growth*n/(n+tau)`
   rises toward an asymptote it never reaches, so floor 14 already sat at 2.5 of a 3.6 ceiling and
   every floor after it was spent approaching a number it had nearly hit. The deep floors were
   shallower than the table made them look, and the ceiling was a promise the content would
   eventually outgrow.

   IT IS NOW EXPONENTIAL, and unbounded on health. The table below is MEASURED, not derived, and it
   is the one to trust - the two above it are history and are kept because the reasoning still
   holds even though the numbers do not:

     floor  tough  rate  bodies   per-floor step on health
       1    1.000  1.000   4.08          -
       5    1.246  1.121   4.19        0.073
      10    1.778  1.324   4.53        0.133
      14    2.504  1.340   5.21        0.215
      20    4.511  1.340   8.10
      30   13.584  1.340  28.00
      50  143.724  1.340  28.00

   The step GROWS, 0.051 at the second floor to 0.215 at the fourteenth, and is still growing at the
   last one - which is the design and is asserted as a shape, because a saturating ladder is also
   monotonic while doing the exact opposite.

   THE RATE COLUMN IS THE ONE THAT REALLY MATTERS, and it is the one that is still capped, for the
   same reason as before. The brief says difficulty comes from rate and density and never from
   anything the player cannot read. `e.speed` is scaled by the same depthRate as the cadence, so an
   uncapped rate hands a ranged body an approach speed the player cannot answer: at the old 1.95
   ceiling it closes at 1.264 px/tick against a player who moves at 1.20. FASTER THAN THE PLAYER.
   A body that outruns you is a body you must pre-empt rather than respond to, and pre-empting is a
   different game from the one this is. The ceiling is now 1.34, derived rather than chosen: a
   ranged body starts at 0.648, the guarantee is 0.87, and 0.87/0.648 is 1.343.

   DENSITY is capped too, and for a different reason - not fairness but playability. Uncapped it is
   246 bodies by floor 40 and 1869 by floor 50. Past the point where the room stops being playable
   the ladder leans on health, which costs the player attention rather than the machine its frame
   budget.

   The rate ceiling is the one number here that is not taste. It is chosen so a gunner's worst
   case stays inside a human reaction time at a density a floor that deep can physically hold in
   the room.

   The three dials are in three places rather than one because three different things are the right
   things to scale: bodies get tougher, so a fight takes longer; rooms get fuller, so there is less
   space to answer in; and packs get likelier, so the shape of a room changes as well as its size.
   Scaling only HP would make deep floors slow and empty, which is a worse game than either of the
   other two.

   THE DISCIPLINE, unchanged, and still the reason these are functions and not fields on the run
   object: nothing in here may read the player. Not a stat, not an item count, not hp, not
   Momentum. It is very easy to add a small mercy - a hair less HP when the player is hurting - and
   it would feel like good design and it would quietly destroy the thing the whole system is for,
   because a difficulty that reads the player is a difficulty that measures the player. The suite
   asserts it: the same floor with a naked player and with a full build has to produce
   byte-identical enemy stats. */

/* THE LADDER IS EXPONENTIAL, and it has no ceiling.

   It was `1 + growth * n/(n+tau)` - a saturating curve that approaches 1+growth and flattens as it
   goes. That was chosen to put a lid on the deep floors, and the lid is the thing being removed: the
   brief is a difficulty that climbs UNBROKEN, so a floor 40 exists to be harder than floor 20 and
   not to approach some number from below. An asymptote is a promise the game cannot keep once the
   content outgrows the tuning.

   It is exponential rather than logarithmic, and the distinction is the whole design rather than a
   word. A logarithm's increments DECREASE, so a log curve is steep early and flat late - which is
   the opposite of what is wanted here, where the later floors are meant to ramp significantly more
   than the earlier ones. An exponential's increments increase, so every floor costs more than the
   one before it and the run never flattens out. Over floors 1-14 with the numbers below, the
   per-floor step grows from 0.05 to 0.22: a 4x increase, and still climbing past floor 14.

   The shape is one expression for all three dials, because three dials written three ways drift
   apart the moment one is retuned - the geometry-in-two-places failure this file has the most of,
   wearing a balance patch instead of a hitbox.

   WHY CADENCE IS STILL CAPPED, and this is the one place the ceiling survives. `depthRate` drives
   cadence AND approach speed, and the brief's other standing rule is that a threat must stay
   readable - a body that closes faster than the player can react is not difficulty, it is a
   cutscene. The measured guarantee is that a ranged body's approach never exceeds 0.87 px/tick
   against the player's 1.20, at ANY floor. An uncapped exponential breaks that by floor 20, so rate
   keeps a ceiling while HP and density climb without one. Growth and reaction time are different
   dials and only one of them is allowed to be unbounded. */
const DEPTH_GROWTH=0.40,     // the coefficient: how much of an exponential to add
      DEPTH_POW=0.12,        // the exponent RATE: larger is steeper later
      /* The reaction-time ceiling, and the only one in the file. It is 1.34 and NOT the 1.95 the
         saturating ladder used, and the number is derived rather than chosen: a ranged body starts
         at 0.648 px/tick against the player's 1.20, and the standing guarantee is that a body never
         closes faster than 0.87. 0.87/0.648 is 1.343. The old ceiling of 1.95 allowed 1.264 - faster
         than the player - and the old ladder only stayed honest because its saturation never
         actually reached its own asymptote before the content ran out.

         That is the whole argument for deriving a ceiling instead of writing one: the previous value
         was not wrong, it was UNREACHABLE, and a safety limit that safety never actually needed is
         indistinguishable from no limit at all until the day it is needed. */
      DEPTH_RATE_CAP=1.34,
      /* Density is capped too, for a different reason and it is not a fairness argument. Uncapped it
         is 246 bodies by floor 40 and 1869 by floor 50, which is not a hard fight, it is a hang -
         and the old ladder put floor 50 at 23. A deep floor has to be reachable on a machine, so
         past the point where the room stops being playable the ladder leans on HP, which costs the
         player attention rather than the machine its frame budget. */
      DEPTH_BODY_POW=0.085,
      DEPTH_BODY_CAP=28,
      DEPTH_PACK_STEP=0.035, // +3.5% chance of a Brunch pack per floor, capped
      DEPTH_PACK_CAP=0.85;   // never a certainty: a room that is always a pack is one shape

/* How long the fade between floors lasts, and therefore how long the descent banner is up. It is
   longer than a room transition on purpose: a room fade covers a door opening, and a floor fade
   covers a change of difficulty, and the second is worth a beat of the player's attention that the
   first is not. One constant, read by descend() for the fade, by drawDescent() to time the banner
   and by update() to count it down. */
const FADE_DESCEND=sec(0.9);

/* The one place the floor number is read. Everything else asks these. Reading run.floor directly
   anywhere else is the thing to watch for in review, for the reason in the comment above. */
function depthFloor(){ return (run&&run.floor)||1; }
function depthSteps(){ return Math.max(0,depthFloor()-1); }

/* AREA. Which themed block a floor is in. Pure: it reads the floor number and nothing else, spends
   no RNG, and is therefore safe to call anywhere without touching the draw order - a function of the
   floor alone is the only shape that keeps a seeded run replayable. Endless descent stays Final: the
   ladder is unbounded and the theme does not cycle back.

   IT TAKES THE FLOOR AS AN OPTIONAL ARGUMENT, and that is the whole point of the parameter. It used
   to take nothing and read `depthFloor()`, which reads `run.floor` - so the presentation layer could
   not ask "which area is floor N in?" without mutating simulation state to ask it. drawDescent wanted
   exactly that question: "did this descent cross a boundary?", meaning compare the area you left with
   the area you arrived in. The three ways to answer it were all bad:

     - write the 4/8/12 thresholds out a second time in 70-view.js. Two copies of a ladder boundary is
       a bug waiting: change one and the banner names the wrong area.
     - temporarily assign `run.floor`, ask, and put it back. A draw function mutating simulation state
       is the one-way data flow this project rules out outright.
     - leave the banner saying only the floor, and let the palette imply the area. Which is what it
       did, and it meant the descent beat - the one moment the game tells you where you are going -
       could not name the place.

   The C# signature always took the floor (`AreaRules.AreaForFloor(int floor)`). The JS not taking it
   was the drift, and it is now the same shape on both sides: `areaForFloor()` still reads the current
   floor for the hundred existing callers, and `areaForFloor(n)` answers for any floor without touching
   anything. */
function areaForFloor(floor){
  const f=(floor===undefined)?depthFloor():floor;
  return f<=4?'Area1':f<=8?'Area2':f<=12?'Area3':'Final';
}

/* THE ENEMY-MIX DIALS, per area. An area changes TWO things only: which bodies a normal slot
   rolls (mix.lunger is the probability a slot is a lunger, the rest are shooters), how often
   a room with enough bodies carries a heavy (mix.heavy), and the base the pack chance ramps
   from (mix.brunch). The depth ladder itself - toughness, rate, density, pack - has the SAME
   shape in every area: an area is an identity, not a harder or easier version of the climb.

   Area1 reproduces the numbers pinned before areas existed (lunger 0.5, heavy 0.55, brunch
   0.45 == BRUNCH.chance), so on floors 1-4 a room draws exactly what it drew. Area2/Area3/
   Final are explicit guesses awaiting a playtest, not values read out of the game - change
   them, then re-measure. */
const AREA_MIX={
  Area1:{lunger:0.5, heavy:0.55, brunch:0.45},
  Area2:{lunger:0.45,heavy:0.60, brunch:0.50},
  Area3:{lunger:0.55,heavy:0.50, brunch:0.60},
  Final:{lunger:0.50,heavy:0.65, brunch:0.55},
};
function areaMix(){ return AREA_MIX[areaForFloor()]; }

/* THE CURVE. 1 + growth * (e^(rate*steps) - 1), which is exactly 1 on floor one, strictly increasing
   for every floor after it, and unbounded - so "the ladder" is now a direction rather than a number
   the curve approaches. The -1 rather than a bare e^ is what makes floor one free. */
const ramp=(steps,growth,rate)=>1+growth*(Math.exp(rate*steps)-1);
function depthTough(){ return ramp(depthSteps(),DEPTH_GROWTH,DEPTH_POW); }
function depthRate(){ return Math.min(DEPTH_RATE_CAP,ramp(depthSteps(),DEPTH_GROWTH,DEPTH_POW*0.55)); }
function depthPack(){ return Math.min(DEPTH_PACK_CAP,areaMix().brunch+DEPTH_PACK_STEP*depthSteps()); }
/* Density is its own exponent rather than the HP one, because it is a different kind of lever: HP
   makes a fight longer and density makes it wider, and they should not move in lockstep or every
   deep floor would be the same fight with more health.

   The -1 makes floor one add NOTHING, which is the same reason the health curve has one: a first
   floor has to be exactly the base experience, and a dial that is already 0.085 away from zero on
   floor one is a dial that was retuned without anybody deciding to retune floor one. */
function depthBodies(rolled){
  return Math.min(DEPTH_BODY_CAP,rolled+DEPTH_BODY_POW*(Math.exp(DEPTH_POW*1.7*depthSteps())-1));
}

/* ADAPTIVE DIFFICULTY, and it is a PLACEHOLDER with the rules already written down.

   NOTE THE NAME, because it is a trap. `PRESSURE` is taken: it is the flat rate constant in 10-art,
   and `roomPressure()` above is something else again - a within-room dial that rises as a room
   empties. This is the CROSS-FLOOR one the brief asks for, and it is called adaptive so that the
   two can never be confused in a review. An earlier draft of this comment called it PRESSURE and
   was a duplicate declaration, which is a SyntaxError that takes the whole file with it.

   The intent is a value that floats on how the player is actually doing and nudges the fight either
   way, so a run going badly eases off and one going well tightens up. It is NOT wired to anything
   yet, and the two rules below are the ones it must obey when it is, because both are the mistakes
   it is most likely to be got wrong:

   NEVER the player's item count or stat total. A difficulty that reads the build punishes a good
   one - the player earns an item, gets harder, earns the next, gets harder again, and the reward
   arrives wearing the mask of a punishment. It has to read PERFORMANCE, which is a behaviour and
   not an inventory.

   and it must be MONOTONIC within a run, or the same fight gets harder while you are winning it,
   which reads as the game cheating and is worse than any amount of difficulty.

   Until it is live the honest thing is a named zero with its rules attached, rather than a comment
   somewhere hoping to be read later. Read ADAPT.value directly - there is deliberately no
   `adaptive()` wrapper, because a one-line accessor nothing calls is a second name for the same
   value and the day the two disagree nobody will know which one a dial was read from. */
const ADAPT={ value:0, min:-0.25, max:0.25 };

// The armour multiplier. Per-hit, so it scales every pellet and every pierce pass rather than
// subtracting a flat chunk - a subtraction would quietly reward the Beam for spraying, which is
// exactly backwards.
const ARMOUR=0.66;
// The pool is a hair over a whole armoured heavy on purpose. Sizing it to land exactly on a heavy's
// HP puts the blast's central promise - one budget kills a lone heavy outright - on a float knife
// edge, where 18*1.35/0.75*0.75 comes out one ulp under 18*1.35 and the body survives with
// 3e-15 health. The 2% is the cheapest possible insurance on the one number the weapon is named for.
const ALT_WEAPON={cooldown:sec(3.6),speed:2.2*0.85,r:12,color:'#ff8a3d',aoeRadius:100,pool:18*TOUGH/ARMOUR*1.02,aoeKnock:2.7};
// The hook is the blast with the sign flipped. It flies through everything on the way in, goes off
// exactly where the cursor was when you pressed, and yanks whatever it caught into that point
// instead of throwing it back out. The yank itself deals no damage at all - the blast is a killshot
// and the hook is a panic button, and a panic button that also dealt damage would just be a worse
// blast. What it does leave behind is a ground spell, and that is where its value lives: the field
// holds what fell into it, walks stragglers in, and grinds them down slowly enough to finish what
// the wand started without ever being a weapon of its own. Its worth scales with how much pressure
// you are actually under, because there is nothing worth gathering when three bodies are loose in a
// room.
// The ground spell. The yank is one impulse and is over inside a second; the field is the part you
// actually fight around. Held bodies are refreshed every tick so they cannot walk out of it, and
// the damage is set far below any gun on purpose.
//
// These three numbers are really one dial, and they were measured together. A 2.6s field on a 1.7s
// cooldown is 1.5x uptime - the circle is on the floor more often than it is not - and on its own
// that measured as a 2.5x difficulty drop: a kiting bot went from 1.83 hearts a room to 0.72. Holding
// is worth far more than the damage is, so the cost comes back out of the uptime, not the numbers.
// 1.8s of field on a 2.0s cooldown is slightly LESS than permanent coverage, which makes the hook
// something you bring to a fight you are losing rather than a fixture you set up in advance. 3.5dps
// over 1.8s is about two thirds of a Bolt per body: enough to take a real bite out of something the
// wand cannot finish, not enough to be the thing doing the killing.
const HOOK_FIELD_TIME=sec(1.8), HOOK_SUCK=0.24, HOOK_DPS=3.5;
/* Resistance. The hook used to be a permanent answer: land it, wait out the 1.8s field, land it
   again, and a body never recovers. A longer cooldown was the obvious fix and it is the wrong one -
   it reduces the RATE and leaves the identity untouched (lock 1.8s, wait, lock again, forever), and
   it makes the hook unavailable at the moment it is most worth having, which is cancelling a lunge
   that has already started.

   So the resistance is per body and it decays. Each new hook on the same body is worth less of the
   first, and the body forgets if you leave it alone - so a hook is devastating once, good twice,
   a nuisance the third time, and something you stop wasting on. The FIRST hook on any body is
   untouched, which is what keeps the skilled play intact: the hook is still the answer to a lunge,
   because the lunge is the first thing you hook.

   Scaled together: the stun that cancels a lunge, the suck that drags a pack into a knot, and the
   drain. Scaling them separately would leave a body that is held and drawn in but not stopped, which
   is the worst of the three rather than a graded version of any of them. */
const HOOK_RESIST=[1, 0.70, 0.45, 0.20, 0.08];
// how long a body must be left alone before it forgets one hook. Long enough that re-hooking the
// same target inside a fight keeps costing you, short enough that a body you walk away from is
// worth full value again by the time you come back to it.
const HOOK_FORGET=sec(7);
let hookFieldId=0;
// The yank is a spring, not a shove. A knockback is one impulse and the body coasts at whatever
// speed it was handed, so a flat pull strength sends a body caught at the rim sailing THROUGH the
// point while a body caught up close barely moves - which is what made the gather read as bodies
// being nudged rather than pulled. A knock of speed v coasts v/(1-friction) pixels, so to cover a
// distance d you want v = d*(1-friction); `pull` is the overshoot on top, a little over 1 so bodies
// cross the centre and knot up instead of merely touching it. The force is scaled by the body's own
// mass, which knockEnemy divides straight back out, so a Brunch and a gunner arrive together.
const HOOK_PULL_GAIN=1-KNOCK_FRICTION;
// counterstrafing: how fast a reversal is forgotten, and how much a reversal widens a gunner's aim
/* SWERVE: the signal that says how UNSETTLED the player's movement is, and the only thing both
   ranged enemies read to decide how much to believe a lead.

   The decay was 0.011, which is a half-life of sixty-three ticks - about a third of a second. That
   number looks reasonable and is badly wrong, because it is only right about a rhythm no human has.
   Measured, holding a single direction and reversing every N ticks:

     reversal period   average swerve   a shooter's shells that landed at 300px
        none (straight)      0.000                    67%   (and 100% once the fixture was fixed)
        0.12s                0.851                   100%
        0.25s                0.167                    13%
        0.38s                0.111                     0%
        0.95s                0.043                     0%

   So the enemies were reading a player who reversed four times a second almost perfectly, and a
   player reversing at a human rhythm barely at all. A third of a second of memory is not enough to
   hold a signal across a reversal that takes half a second to come back from. At 0.0035 the
   half-life is a hundred and ninety-eight ticks, and the same measurement gives 0.91 at 0.25s and
   0.84 at 0.38s - the rhythm a person actually moves at is now the rhythm the enemies can read.

   The knock-on is the point rather than a side effect. SWERVE also scales how far a long-range
   gunner's aim opens up, so this does not merely make reversing costlier: it makes a settled
   straight line MORE readable and a reversing player MORE predictable, everywhere, for the lunger's
   lunge lead and the shooter's lead as well as the gunner's spread. One number, three consumers, and
   they had all been quietly agreeing about a player who does not exist. */
const SWERVE_GAIN=0.22, SWERVE_DECAY=0.0035, SWERVE_AIM=0.30;

/* --- how the player actually moves, and the Momentum meter -------------------------------------
   The movement model already has two separate levers, which is the fact the whole stat design rests
   on: `spd` is how fast you travel, and MOVE_ACCEL is how fast you GET there. Reaching a steady
   heading takes MOVE_ACCEL's worth of ticks either way, so a room is crossed in the same time no
   matter what MOVE_ACCEL is.

   That asymmetry is the whole reason Momentum is safe. Everything in this game that was tuned
   against a real measurement - the gunner's 105-tick cast, the lunger's lunge lead, the 350px
   shooter deadzone, SWERVE_FULL - is a function of DISTANCE and therefore of top speed only.
   Feeding the reward for playing well into acceleration rather than speed leaves every one of those
   numbers exactly where it was, and gives the player something that feels enormous: you stop
   sliding, and you can commit to a direction on the same tick you think of it. */
const MOVE_ACCEL=0.116;

/* Momentum is combat momentum, not walking momentum. It charges only while bodies are alive, so
   backtracking and shopping never bank it - a run is a series of fights and this is the meter for
   the one you are in. Three verbs, and the rule set is small enough to hold in the head:
     moving under pressure charges it
     standing still under pressure bleeds it
     getting hit costs most of it - not all of it
   That last one is the fairness decision. A meter that resets to zero on hit punishes the player
   for the exact moment they are already recovering, and turns a good run into a series of unrelated
   fights. Keeping 45% means a hit costs you your cushion, not your run. */
const MOMENTUM_GAIN=0.0056, MOMENTUM_STALL_DECAY=0.006, MOMENTUM_HIT_KEEP=0.55;
// below this the player is not going anywhere, so momentum neither charges nor bleeds - a body
// pinned in a corner by two lungers is not "standing still", it is losing, and the meter is right
// to be quiet about it
const MOMENTUM_MOVE_FLOOR=0.3;
/* MOMENTUM, RETUNED FROM MEASUREMENT, and two of the four numbers did not move - which is the
   interesting part.

   GAIN went 0.0042 -> 0.0056 and the number is SOLVED rather than chosen. The charge term is
   GAIN * displacement, and a player genuinely moving under pressure achieves about 1.07px/tick -
   measured, after a first bot was thrown out because it held one key until it walked into a wall and
   stood there, which made every conclusion a statement about a player leaning on plaster. So
   1/0.0056/1.07 = 167 ticks to fill and 1/0.006 = 167 ticks to empty: the meter takes the same 0.79s
   to build as it does to lose, and spends its life in the MIDDLE responding to what the player is
   doing this second. The old 0.0042 filled a third slower than it drained, so a moving player rode
   the ceiling; a first attempt at 0.0090 pinned them there 68% of the time. This is the value where
   neither happens, and it is the whole reason the bar is now watchable.

   HIT_KEEP went 0.45 -> 0.55. A hit still costs 45% of the meter, which is the tension and the reason
   to protect it, but at 167 ticks to rebuild a 45% cut is 75 ticks of playing well back - a setback
   rather than a run-ender.

   SPEED and ACCEL DID NOT MOVE, and working out why is the most useful thing this retune produced.

   The claim this file has carried since Momentum existed is that a full meter cannot help against a
   gunner, "because the gunner solves a real intercept rather than leading by a guess: a player who
   moves 10% faster is simply a faster target, solved correctly". With a character that now starts at
   25% speed, that claim is FALSE. A straight-line runner at 200px with a full meter is hit 0% of the
   time, and every clean sample misses by 36-54px. Raising the meter to 18% speed pushed the miss out
   to 70-88px and made a straight-line runner literally unhittable; raising acceleration to 0.60
   widened it further, and 0.70 collapsed the reversal fixture as well.

   And the speed is not the culprit - ACCELERATION is. The gunner solves a CONSTANT VELOCITY
   intercept, so a player who is still accelerating when the solution is taken has a velocity about to
   change, and no amount of solving repairs a stale assumption. The solution becomes correct again the
   moment the player stops accelerating. That is why 10% extra top speed on a 25% character is
   survivable and 60% extra acceleration is not: speed is a SOLVED quantity, acceleration is not.

   So this is not a tuning miss. Momentum as specified - a commitment bonus - is fundamentally an
   EVASIVENESS buff against enemies that lead by calculation, and the honest consequence is that the
   fix belongs in the gunner's solver rather than in the meter. That is a larger change than this
   pass, so the numbers stay where the claim holds and the tension is written down here instead of
   being tuned away in silence. If a player ends up feeling the meter is not worth watching, this
   paragraph is where to start, and the answer is not a bigger number. */
/* THE METER'S SPEED SHARE IS 0.05, and that number is the collision between two things the design
   both wants.

   The character starts at 25% and the meter used to add 10% on top, and the suite measured what that
   does: a straight-line runner at 200px with a full meter is hit 0% of the time, every clean sample
   missing by 36-54px. Not "harder to hit" - unhittable. The cause is that the gunner solves a
   CONSTANT VELOCITY intercept, and a target 35% quicker than the one the whole encounter was tuned
   against is past what that solution can carry. The character alone at 25% is still hit; it is the
   STACKING that breaks it.

   So the meter takes 5%, and the reward is carried almost entirely by ACCELERATION - the half the
   player feels anyway, and the half a constant-velocity solver genuinely cannot answer.

   And the honest recommendation, which is not a smaller number: the real fix belongs in the gunner.
   Its solver already runs fourteen iterations against an assumed velocity; teaching it to lead a
   target that is still accelerating would let the meter be worth more than 5% AND keep the gunner
   dangerous, and that is a change to Intercept.cs rather than to this file. Written down so the next
   pass starts there rather than re-deriving it. */
const MOMENTUM_SPEED=0.18, MOMENTUM_ACCEL=0.55;
// Two ceilings, and the difference matters. SPEED_CAP is the ceiling on the SPEED STAT, which a
// character now STARTS INSIDE - it is the most a build can add on top of a starting 25%, not the most
// a build can give. MOVE_SPEED_HARD_CAP is the ceiling on the SUM of everything, and it is set to
// 0.44 rather than a round 0.45 for a reason the suite checks: a maxed build plus a full meter can
// reach 0.45, so a hard cap AT 0.45 would be a ceiling on nothing - it could never bind, and the day
// somebody raised a stat by a point the cap would silently stop existing. It has to be reachable AND
// exceeded, or it is decoration.
const SPEED_CAP=0.40, MOVE_SPEED_HARD_CAP=0.44;

/* PRECISION TIGHTENS THE ARCANE BEAM, in fifths, and the floor is the point.

   The beam is the fastest gun in the game and it lands one body at a time, so its whole identity is
   that a shot is very nearly always a hit. At zero luck it is a CONE: wide enough that a body inside
   it is a body you hit, narrow enough that a body just outside it is one you have to wait for. That
   is a real high-fire-rate weapon rather than a laser with a small damage number.

   Each point of luck takes a fifth off the cone, so five points is a fifth of five - a beam. The
   floor stops it going all the way, and it has to: a shot that leaves at exactly the angle you were
   pointing is a shot that can be walked into, because the player's own body tells them where it will
   go before it has gone anywhere. The same reason a gunner is never perfectly deterministic. So the
   floor is four percent of the cone, which at knife range is a couple of pixels and at room range is
   not quite nothing - enough that a laser is something you have to be steady for.

   This is also the first thing in the game driven by a DERIVED stat rather than by a constant, which
   is the whole reason the stat model had to be additive and rebuilt from base. If Luck were stored as
   a multiplier on a live value, this would be the line that could not be written. */
const PRECISION_STEP=0.20, PRECISION_SPREAD_FLOOR=0.04;

/* HOW MUCH A CHARGING BODY BELIEVES A REVERSING PLAYER. One signal, two answers, and the split is
   the point rather than a correction.

   A GUNNER ROOTS ITSELF to charge, and a rooted body is watching: it sees you thrash and concludes
   you are going nowhere, so it stops leading and the shell arrives where you already are. That is
   right for it, and its 100% at two hundred pixels is measured.

   A SHOOTER KEEPS WALKING, and walking is a commitment - it is closing on a particular point in the
   room rather than observing the one in front of it. So it believes a reversing player more than half
   as much, and leads accordingly. Without this the moving shooter is punished for moving: it lands on
   a runner every time and on a quarter-second reverser about a fifth of the time, which makes reversing
   the correct answer to it and hands back the exploit the movement was meant to close.

   The value is not a fudge. One is "believe what you see", and a walking body does not see that. */
const SWERVE_TRUST_WALKING=2.2, SWERVE_TRUST_ROOTED=1.0;
function preciseSpread(base){
  return base*Math.max(PRECISION_SPREAD_FLOOR,1-PRECISION_STEP*Math.max(0,Stats.value('precision')));
}

/* ROOM PRESSURE ----------------------------------------------------------------------------------
   One number, from one thing: how many bodies are still standing in this room. It is HIGH when the
   count is LOW, because the last body in a room is the one that has to do all the work.

   This is the player's read on the shooter, and the measurement supports it. A ranged enemy with
   four bodies still alive is content to hold its 250px standoff and take its time, because the count
   is already doing the work. Alone in a room it has to come and get you - and a shell fired from
   150px has a flight short enough that reversing inside it is not an answer, which is the whole
   reason a lone shooter used to be free to stare at you indefinitely.

   It is a MERCY buffer as much as a threat, which is the part worth being deliberate about. Most
   rubber bands make a losing position worse; this one makes it sloppier, so the room you are losing
   is the room where the last body starts taking risks and a good player can still punish it. That is
   what keeps a bad room survivable long enough to be played properly rather than merely survived.

   It reads the ROOM and never the player - not their stats, not their build, not how well they are
   doing. Difficulty in this game is a function of DEPTH, and that system does not exist yet (there
   is no floor number anywhere in the game; TOUGH and PRESSURE.rate are flat constants), so nothing
   here may read the player until it does. This is about the shape of the last thirty seconds of one
   room, which is a different question from how hard the floor is. */
const PRESSURE_SPAN=4, PRESSURE_FLOOR=0.25;
function roomPressure(live){
  if(!(live>=1)) return 1;                 // an empty room is maximum pressure: nobody is stopping you
  if(live===1) return 1;
  if(live>=PRESSURE_SPAN+1) return PRESSURE_FLOOR;
  return PRESSURE_FLOOR+(1-PRESSURE_FLOOR)*(PRESSURE_SPAN+1-live)/PRESSURE_SPAN;
}
/* How much of its preferred standoff a pressured shooter gives up, and how much of its cooldown.
   Both are VISIBLE - it walks at you, and it shoots more often - which is the only reason this is
   fair. A hidden accuracy ramp on a lone enemy is indistinguishable from the game cheating. */
const PRESSURE_CLOSURE=0.85, PRESSURE_CADENCE=0.4;

/* HOW LONG THE BLINK BAR TAKES TO FILL AFTER A DOOR, in ticks.

   RESTORE_FX_SPAN is deliberately READY, the length of the arrival itself. The bar therefore reaches
   full on the same tick the player regains control, which is the only moment a full bar is worth
   anything to them - and it means the drawn value and the real value agree on every single frame of
   the animation, because the real value is always full and only the drawing is behind it.

   This replaces a ring that flashed on the wand and the alt. It is gone because a ring on a bar that
   has already snapped to full is decoration: the player saw the jump and the ring then announced
   something that had already happened. The ring was also invisible for its entire first life - the
   fade is flattest at its start, so roomFade was still 0.89 after a third of the arrival and the HUD
   was under 13% visible - which is worth remembering as the shape of the mistake: the test asserted
   the clock counted down and it did, and nobody asked whether anyone could see it. */
const RESTORE_FX_SPAN=READY;

/* HOW FAR a ranged body will actually have walked in N ticks, given the standoff rule it obeys.

   RECORDED, NOT USED. There was a function called `standoffDrift` here and it is gone, and the reason
   is worth more than the function was. It is named here so that a reader who goes looking for it
   finds this, which is the whole point - a comment that names a function must name one that can be
   called, and the comment in the tick used to send people to a function that could not be.

   It answered the question as a one-dimensional walk of the standoff DISTANCE, reversing at the two
   thresholds - which is right about the distance and useless about the bearing. "Toward the player" is
   a bearing, and a body walking a bearing while the player covers more ground underneath it is not
   the same as a body walking a distance. It got the radial number correct and every shot still went
   about twenty pixels wide, because the bearing rotates underneath it and a scalar cannot rotate.

   What replaced it is the twenty-four-step loop in 60-tick.js that walks the muzzle forward against
   the BELIEVED player position, obeying the same standoff rule that is moving the body. That is two
   dimensions and it is right for the reason the scalar version was not.

   Two things are kept here. The measurement, because the standoff rule genuinely does reverse and
   speed*N genuinely is the wrong answer - a shooter starting at 200px with a close threshold of 150
   walks in for fifty pixels, hits its own threshold and spends the rest of the cast walking back out
   again, so the net travel is a third of what speed*CAST_TIME predicts. And the warning: a comment
   in the tick used to point at the deleted function as the authority on this subject, which is the
   failure this file has the most of - geometry living in two places, one of them dead, and a reader
   sent to the wrong one.

   A comment that names a function must name one that can be called. */

// How far away a gunner has to be before counterstrafing buys anything. Half the width of the room
// is the line, and the bonus is fully open by the time a body is a room-and-a-half away.
/* Where the counterstrafe answer switches on, and where it is fully open.

   The deadzone is HALF THE ROOM WIDTH less fifty, not the half width itself. It has to clear the
   gunner's own 200px standoff with room to spare, or a gunner would spend the fight inside its own
   deadzone and never widen its aim at all - and at exactly half the width, the far case the suite
   measures (400px) was only 23% of the way up the ramp, so "a reversal is a real answer at range"
   was being asserted about a point where the design says the effect is still barely open. The
   spread could not be widened to compensate: the suite deliberately caps SWERVE_AIM below 0.5 rad so
   that a reversing player stays hittable, and that cap is the right thing to protect. So the ramp
   moves instead, and the number that is actually being tuned is where the benefit arrives - not how
   wide it gets at the far end. */
/* THE SWERVE RAMP IS A FUNCTION OF THE ROOM, for the same reason AGGRO_RANGE is.

   A gunner reads a reversing player less well the further away it is, and "how far away is far" is a
   property of the room - half its width, less a margin. Evaluated once at load it froze at 300 for a
   700px room and stayed there for a 1680px one, which means every gunner in a big room reads a
   reversal at full strength from across the room. That is not a subtle balance drift; it is the
   counter to the whole mechanic switching itself off at range, and it is silent.

   A function, for the same reason: it is re-derived per room rather than frozen at load.

   AND THE WINDOW IS NOW A WINDOW RATHER THAN A NOTCH. It used to be `roomW()/2 - 50` to
   `+ SWERVE_FULL_BASE(130)`, so in a 700px room the range in which distance opened the gunner's aim at
   all was 300px to 430px: 130px, and the spread multiplier `reach` was then pinned at 1.0 for every
   pixel beyond it. Measured, with the real input path:

       straight runner   100% hit at 200px, 300px, 400px and 500px
       counterstrafer     13% hit at 200px, 300px, 400px and 500px

   Flat. Counterstrafing was worth −87 points of hit rate at EVERY range, so the mechanic was not "worth
   nothing up close and something far away" - it was a flat 87-point dodge that the distance component
   did not touch. The design intent is in the test's own name ("only far away misses") and in the
   comment above the spread; the numbers contradicted both.

   Three assertions in that test were parked behind a guard for exactly this reason, as three
   `ok(true,'')` calls - passing assertions standing in for real ones. That is the shape of thing this
   file keeps warning about, and it is how a genuine design bug survives: the test went green and
   stopped checking the range where the mechanic claims to live.

   The window now runs from a quarter of the room to four fifths of it: 175px to 560px in a 700px room,
   and 385px of ramp rather than 130. Both bounds are still fractions of the room, so a big room gets a
   proportionally wide window and `reach` never saturates inside a room at all - which is the property
   that was missing, since a saturated multiplier cannot express "further is worse".

   SWERVE_TRUST_WALKING and the spread size are untouched: this changes WHERE the tactic starts being
   worth something, not how much it is worth when it does. */
const swerveDeadzone=()=>Math.round(roomW()*0.25), SWERVE_FULL_BASE=Math.round(roomW()*0.55);
const swerveFull=()=>swerveDeadzone()+SWERVE_FULL_BASE;
// The gunner's cast tell. This is the whole of the change: the shell used to leave the instant the
// gunner's cooldown ran out, so the player had nothing to read and the only counter was not being
// there. Half a second of swelling light at the muzzle turns that into a reaction. It is paid for
// by the cooldowns going up, not by the damage going down - a slower, heavier, more telegraphed
// shell is a better gun, and a faster, lighter, untelegraphed one is just a tax.
const CAST_TIME=sec(0.5);
// The blink's landing burst. GAIN is the speed it gives you when you hold the direction you blinked
// in, BURST is how long it lasts, and both are deliberately small: this is a nudge that makes the
// blink worth using for ground as well as for survival, not a dash that replaces walking. At 0.11s
// and 1.55x it is about two seconds of useful extra ground if you commit to it and nothing at all
// if you do not.
const BLINK_BOOST_GAIN=1.55, BLINK_BOOST=sec(0.11);
const HOOK_WEAPON={cooldown:sec(3.6),speed:2.4*0.85,r:14,color:'#2a6fc4',aoeRadius:118,pool:0,phase:true,pull:1.2,hold:sec(0.7),field:HOOK_FIELD_TIME,early:true};

// there used to be a lookup table mapping the two right-click names to their weapon objects, so
// bakeIcon could ask "which colour is this thing" with a string. Every real call site wants the
// weapon it is currently holding or the bolt it was actually thrown with, and the table was the
// reason the icon for one gun could be drawn in another gun's colour. activeAlt() is the one lookup
// the game still needs, and it reads the mode the player is holding.

/* Where being hit actually is.
   The sprite is 12x18 cells drawn at scale 2, so 24x36px on screen, and it is drawn at oy-4 with
   oy=player.y - which puts the model's top at player.y-4 and its feet at player.y+32. A circle
   centred on player.y with the old radius therefore sat across the hood and stopped dead at the
   waist: you could not be hit for anything that reached your legs, and a body standing on your feet
   was standing on a hitbox that ended twelve pixels higher. Enemies have to reach the torso before
   they can land, so the fight was not the one the player could see.

   DY slides the circle down onto the actual mass of the character - the chest and waist, which is
   rows 11-15 of the sprite, and the band a shot is aimed at.

   R is then deliberately SMALLER than the model: 10 gives a 20px hitbox against a 24px character,
   so it is 83% of the width and misses the edges of the hood and the hem. This is on purpose in both
   directions. An exact hitbox feels like the game is grading an exam, and it makes the two damage
   paths - the shell that grazes a shoulder and the body that walks into you - disagree at the
   boundary in a way players read as unfair. A forgiving one is the same forgiveness the whole game
   already runs on: readable, reactive, never a measurement. If the game gets faster, this is the
   dial that gets retuned first, and it should get smaller before anything telegraph gets shorter. */
const PLAYER_HIT_DY=10, PLAYER_HIT_R=10;
// The one place that asks "did that hit me", so the offset and the radius cannot drift apart. Both
// the shells and the bodies go through it, which is what keeps those two paths agreeing.
function playerHit(x,y,r){ return Math.hypot(x-player.lagX,y-(player.lagY+PLAYER_HIT_DY))<r+PLAYER_HIT_R; }
// the shove scales with how close you put it: a blast that goes off on top of a body is worth about
// twice what the old flat shove was, and one that only catches something out at the rim is worth
// about a third of that flat number
const ALT_KNOCK_NEAR=2, ALT_KNOCK_FAR=0.35;

