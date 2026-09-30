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
const ROOM_LEFT=50, ROOM_RIGHT=750, ROOM_TOP=130, ROOM_BOTTOM=580;
const MIDX=(ROOM_LEFT+ROOM_RIGHT)/2, MIDY=(ROOM_TOP+ROOM_BOTTOM)/2;
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
const AGGRO_RANGE=Math.round(0.85*Math.hypot(ROOM_RIGHT-ROOM_LEFT,ROOM_BOTTOM-ROOM_TOP)), AGGRO_TIME=sec(2.5), BLINK_DIST=116, BLINK_RECHARGE=sec(3.5), BLINK_FILL_CLEAR=9, BLINK_IFRAMES=sec(0.17);

/* THE BLINK GRACE: how long after a blink an incoming hit is still forgiven.

   Measured, because the request was for 0.1s and the game already grants 0.40s - BLINK_IFRAMES 0.17
   plus DASH_TRAIL 0.23, set after the teleport. A 0.1s grace on top of that would have been strictly
   shorter than immunity already in force and would have changed nothing at all.

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
  // cone. Damage per pellet and pellet count are unchanged, so every crowd number the Scatter has
  // earned still holds - this is a redistribution of where the damage lands, not a buff.
  {name:'Scatter',color:'#e8502a',cooldown:sec(1.1),speed:2.4,dmg:2.6,count:8,spread:0.045,fNear:80,fFar:300,fMin:0.45,
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
 {name:'Arcane Beam',color:'#3fa9ff',cooldown:5.2*SPEEDUP,speed:4,dmg:1.55,count:1,spread:0.16,spreadFromPrecision:true,r:4,fNear:170,fFar:470,fMin:0.6},
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

   Everything below is LINEAR in the floor, which is the simplest thing that could be right and the
   easiest to reason about when it turns out to be wrong. The steps are in three places rather than
   one because three different things are the right things to scale: bodies get tougher, so a fight
   takes longer; rooms get fuller, so there is less space to answer in; and packs get likelier, so
   the shape of a room changes as well as its size. Scaling only HP would make deep floors slow and
   empty, which is a worse game than either of the other two.

   THE DISCIPLINE, and the reason these are functions and not fields on the run object: nothing in
   here may read the player. Not a stat, not an item count, not hp, not Momentum. It is very easy to
   add a small mercy - a hair less HP when the player is hurting - and it would feel like good
   design and it would quietly destroy the thing the whole system is for, because a difficulty that
   reads the player is a difficulty that measures the player. The suite asserts it: the same floor
   with a naked player and with a full build has to produce byte-identical enemy stats. */

const DEPTH_HP_STEP=0.30,        // +30% body HP per floor
      DEPTH_BODY_STEP=0.34,      // +0.34 of a body per floor, on top of the existing 2..4 roll
      DEPTH_RATE_STEP=0.16,      // enemies act 16% faster per floor
      DEPTH_PACK_STEP=0.035,     // +3.5% chance of a Brunch pack per floor, capped
      DEPTH_PACK_CAP=0.85;       // never a certainty: a room that is always a pack is one shape

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
function depthTough(){ return 1+DEPTH_HP_STEP*depthSteps(); }
function depthRate(){ return 1+DEPTH_RATE_STEP*depthSteps(); }
function depthPack(){ return Math.min(DEPTH_PACK_CAP,BRUNCH.chance+DEPTH_PACK_STEP*depthSteps()); }
function depthBodies(rolled){ return rolled+DEPTH_BODY_STEP*depthSteps(); }

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
const MOMENTUM_GAIN=0.0042, MOMENTUM_STALL_DECAY=0.006, MOMENTUM_HIT_KEEP=0.45;
// below this the player is not going anywhere, so momentum neither charges nor bleeds - a body
// pinned in a corner by two lungers is not "standing still", it is losing, and the meter is right
// to be quiet about it
const MOMENTUM_MOVE_FLOOR=0.3;
// what a full meter is worth. The speed half is deliberately small: SPEED_CAP is shared with items,
// and a mechanic that could eat the whole budget would leave no room for a Speed item to mean
// anything. The acceleration half is where the reward actually lives.
const MOMENTUM_SPEED=0.10, MOMENTUM_ACCEL=0.55;
// Two ceilings, and the difference matters. SPEED_CAP is the most ITEMS may give, so a Speed item
// always has a readable value. MOVE_SPEED_HARD_CAP is the most ANYTHING may give, so no combination
// of a good build and a full meter produces a player who crosses rooms before the gunner has
// finished winding up. One number for the build, one for the world, both visible and both tunable.
const SPEED_CAP=0.15, MOVE_SPEED_HARD_CAP=0.22;

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
   It is emphatically not speed*N, and finding that out cost a day of a shooter missing a straight
   runner by forty pixels.

   The rule reverses at the body's own boundaries: it closes while further than `standoff`, stops in
   the band between `standoff` and `close`, and backs away inside `close`. So a shooter starting at
   200px with a close threshold of 150 walks in for fifty pixels, hits its own threshold, and spends
   the rest of the cast walking back out again - thirty-two pixels of net travel, not the sixty-eight
   that speed*CAST_TIME predicts. Aiming from a muzzle predicted with the naive figure put every
   shot forty pixels wide, because the error is exactly the difference between the two.

   Simulated rather than solved in closed form, because the answer is a triangle wave with a kink in
   it and the loop is a hundred iterations of arithmetic on the rarest thing in the game - a comment
   in the code already notes that shells are the rarest thing in the game, which makes this the
   cheapest place in the codebase to be exact. */
function standoffDrift(dist,close,standoff,speed,ticks){
  let d=dist,moved=0;
  for(let i=0;i<ticks;i++){
    const dir=d<close?-1:(d>standoff?1:0);
    if(!dir) return moved;              // in the band: it is holding, and will keep holding
    const room=dir>0?standoff-d:d-close;
    const step=Math.min(speed,room);
    d+=dir*step; moved+=dir*step;
  }
  return moved;
}
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
const SWERVE_DEADZONE=Math.round((ROOM_RIGHT-ROOM_LEFT)/2)-50, SWERVE_FULL=SWERVE_DEADZONE+130;
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

