/* 00-balance - every dial in the game Nothing here draws, reads input or touches the DOM, so all of it ports to C# unchanged. [h:00-balance-1] */
const canvas=document.getElementById('c'),ctx=canvas.getContext('2d');
ctx.imageSmoothingEnabled=false;

const W=960, H=600;
/* pacing ---------- The sim used to tick at a fixed 60Hz. [h:00-balance-2] */
const SPEEDUP=3.5, TICK_HZ=Math.round(60*SPEEDUP);
const sec=s=>Math.round(s*TICK_HZ);
const GRID=7, START=3, DOORW=90;

/* THE STANDARD ROOM, and the fact that it is a DEFAULT rather than the world is new and load-bearing. [h:00-balance-3] */
const STD_ROOM={l:50,t:130,r:750,b:580};
const ROOM_W=STD_ROOM.r-STD_ROOM.l, ROOM_H=STD_ROOM.b-STD_ROOM.t;
let ROOM_LEFT=STD_ROOM.l, ROOM_RIGHT=STD_ROOM.r, ROOM_TOP=STD_ROOM.t, ROOM_BOTTOM=STD_ROOM.b;
let MIDX=(STD_ROOM.l+STD_ROOM.r)/2, MIDY=(STD_ROOM.t+STD_ROOM.b)/2;

/* THE ACCESSORS - the honest read, and what a new site should use. [h:00-balance-4] */
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

/* THE CAMERA, and it is a RENDER-TIME TRANSFORM rather than a change of coordinates. [h:00-balance-5] */
const cam={x:0,y:0,w:0,h:0};
/* Declared once. [h:00-balance-6] */
function updateCamera(){
  const t=cameraTarget();
  cam.x=t.x; cam.y=t.y; cam.w=W; cam.h=H;
  return cam;
}

/* THE POINTER, IN THE FRAME THE GAME IS IN. [h:00-balance-7] */
function screenToWorld(sx,sy){
  updateCamera();
  return {x:sx+cam.x, y:sy+cam.y};
}
function mouseWorld(){ return screenToWorld(mouse.x,mouse.y); }
function cameraTarget(){
  const b=boundsOf();
  /* A room that fits the view is CENTRED: [h:00-balance-8] */
  const x=(b.r-b.l)<=W?(b.l+b.r-W)/2:Math.max(b.l,Math.min(b.r-W,player.x-W/2));
  const y=(b.b-b.t)<=H?(b.t+b.b-H)/2:Math.max(b.t,Math.min(b.b-H,player.y-H/2));
  return {x,y};
}
const OPP={N:'S',S:'N',E:'W',W:'E'};
const ROOM_BG={start:'#1c2230',normal:'#191b22',item:'#2a2410',boss:'#2a1414'};
/* BLINK RECHARGE IS A DURATION, AND IT IS NOT SCALED BY TEMPO. [h:00-balance-9] */
/* AGGRO_RANGE IS A FUNCTION OF THE ROOM, and it was a number frozen at module load. [h:00-balance-10] */
const aggroRange=()=>Math.round(0.85*Math.hypot(roomW(),roomH()));
const AGGRO_TIME=sec(2.5), BLINK_DIST=116, BLINK_RECHARGE=sec(3.5), BLINK_FILL_CLEAR=9, BLINK_IFRAMES=sec(0.17);

/* THE BLINK GRACE: [h:00-balance-11] */
const BLINK_GRACE=sec(0.6);
/* The blink was 140px on an 8s recharge, and together those two made it a teleport with a long wait rather than an escape with a cost: [h:00-balance-12] */
const READY=sec(0.75), FADE_OUT=sec(0.35), FADE_CLEAR=sec(0.15), IFRAMES=sec(1), STRIDE=11*SPEEDUP, KNOCK_P_FRICTION=0.958;
const LUNGER_PAY=0.96, LUNGER_WALK=0.3*LUNGER_PAY, LUNGER_RUN=0.82*LUNGER_PAY, MAX_ARMOR=4;
const SHOOT_SLOW_MAX=0.7, SHOOT_SLOW_MAIN=0.35, SHOOT_SLOW_ALT=0.45, SLOW_EASE=0.079, SHOOT_SLOW_RECOVER=0.03*SPEEDUP;
const KNOCK_FRICTION=0.976, KNOCK_GAIN=0.294, KNOCK_P_GAIN=0.3, KNOCK_BOUNCE=0.6, KNOCK_STUN=sec(0.42), KNOCK_TRADE=0.09, KNOCK_MAX=5, KNOCK_CUT=0.006;
/* The ramp. A walker used to ease toward its top speed at one fixed rate, so its approach was a straight line you could measure in the first second... [h:00-balance-13] */
const LUNGER_ACCEL=0.0058, WANDER_SPEED=0.25, WANDER_TICKS=sec(1);

/* BRUNCH MOMENTUM: [h:00-balance-14] */
const BRUNCH_ACCEL=0.09,      // ~11 ticks (52ms) to reach speed. Quick enough to still feel committed
      BRUNCH_DECEL=0.16,      // stops faster than it starts, so a wall settles rather than coasts
      BRUNCH_DEADZONE=6;     // the slot is "reached" inside this and the body settles into it
/* BRUNCH RAMP IS 1.5s, down from 2.2s, and the reason is that 2.2s was measured against the wrong number. [h:00-balance-15] */
const BRUNCH_RAMP=sec(1.5), BRUNCH_RAMP_GAIN=1.9;

/* BRUNCH AS COVER: [h:00-balance-16] */
const BRUNCH_ABSORB_R=15, BRUNCH_ABSORB_FLASH=sec(0.1), BRUNCH_ABSORB_PUFF=sec(0.14);

/* THE PACK FORMATION: [h:00-balance-17] */
const BRUNCH_WALL_GAP=19,     // along the wall, between columns. Brunch r is 8, so this clears 2*r
      BRUNCH_WALL_RANK=17,    // between the two ranks, along the approach. Also above 2*r
      BRUNCH_WALL_MIN=3;      // a pack smaller than this is a knot, not a wall, and walks straight in

/* BRUNCH AS A MOVABLE SHIELD: [h:00-balance-18] */
const BRUNCH_ARC_FLOOR=9*Math.PI/180,    // the narrowest a shield may be: enough to be an obstacle
                                           // rather than a post, and roughly one body's width
      BRUNCH_ARC_CEIL=60*Math.PI/180,    // the widest: past this it wraps the target and reads as the
                                           // mob this replaces, which is the failure this prevents
      BRUNCH_ARC_GAP=19,                    // along the arc between slots. Same job as WALL_GAP, on a curve
      BRUNCH_SHIELD_R=118,                  // the MINIMUM stand-off. Below this the wall is inside the
                                            // target's own hitbox and stops being cover; above it the
                                            // wall is placed proportionally to the player-target gap
      BRUNCH_SHIELD_FRAC=0.25,             /* a QUARTER of the way from the TARGET, so the pack hugs the enemy it is covering. [h:00-balance-19] */
      BRUNCH_SHIELD_MIN=2;                  // fewer than two bodies cannot cover anything
/* HOW OFTEN AN UNEMPLOYED PACK LOOKS FOR A SHOOTER TO PROTECT. [h:00-balance-20] */
const BRUNCH_SCAN_TICKS=20;
/* A GUARDED RANGED BODY HOLDS A LONGER STANDOFF, and the reason is that its own escort is in the way. [h:00-balance-21] */
const GUARD_STANDOFF_MULT=1.45;

/* THE ARC SOLVER: [h:00-balance-22] */
function brunchArcSlot(tx,ty,px,py,n,slot,tgtR){
  if(n<BRUNCH_SHIELD_MIN) return null;
  /* bearing target -> player. Everything is measured along it. */
  const dx=px-tx, dy=py-ty, d=Math.hypot(dx,dy)||1;
  const ux=dx/d, uy=dy/d;                    // unit vector, target toward player
  const cols=Math.ceil(n/2);
  const rank=slot%2;
  const col=((slot/2)|0)-((cols-1)/2);       // symmetric about the centreline, in half-steps
  /* THE STAND-OFF IS A FRACTION OF THE GAP, NOT A CONSTANT, and the constant was badly wrong. [h:00-balance-23] */
  const gap=Math.max(BRUNCH_SHIELD_R*0.35,d);
  const radius=Math.min(d*BRUNCH_SHIELD_FRAC, d-ENEMY.brunch.r-2)+(rank?BRUNCH_WALL_RANK:0);
  /* THE CONE IS SIZED TO THE THING BEING COVERED, and the 45-degree floor is GONE. [h:00-balance-24] */
  const apparent=Math.atan2((tgtR||12)+BRUNCH_ARC_GAP*2,Math.max(1,d-radius));
  const half=Math.max(BRUNCH_ARC_FLOOR,Math.min(BRUNCH_ARC_CEIL,apparent));
  /* THE SLOT'S POSITION ON THE ARC, and this is where the pack-size dependence actually lives. [h:00-balance-25] */
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


/* A lunger does not walk at you. [h:00-balance-26] */
const LUNGE_CD=sec(1.15), LUNGE_WINDUP=sec(0.34), LUNGE_SPEED=4.2, LUNGE_RECOVER=sec(0.55);
const LUNGE_RANGE=178, LUNGER_NEAR=0.55, LUNGER_FAR=1.3;
const LUNGE_REACH=280, LUNGE_FLOOR=56;
/* The lunger holds this far off and lunges ACROSS the gap. [h:00-balance-27] */
const LUNGE_HOLD=78, LUNGE_MIN=34;
// Two lungers meeting mid-charge. Light enough that the loser is out of the fight for a moment
// rather than launched, heavy enough that it reads as a collision and not as a graze.
const LUNGE_CLASH=5.5;
/* How fast a lunger walks its own angle around the player, in px/tick, aimed tangentially so it can never change how far away it is. [h:00-balance-28] */
const LUNGER_SPREAD=0.9;
/* How the lunger tracks the player's heading over time. [h:00-balance-29] */
const LUNGE_TRACK=0.014;

/* WHICH WAY THE LUNGER THINKS THE PLAYER IS GOING. [h:00-balance-30] */
const LUNGE_BELIEF_TRACK=0.14, PLAYER_SPEED_EPS=0.05;
// The lunge reaches as far as the intercept solution needs and no further. A lunger whose solution
// is outside this does not commit at all - it keeps closing at its approach speed until the solution
// fits, which is what "too far" means: not that the attack fails, but that it has not started yet.
const LUNGE_ITER=3;
  /* The blink leaves six puffs that fade over DASH_TRAIL. [h:00-balance-31] */
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

/* THE WARDEN, the boss. [h:00-balance-32] */

const BOSS_VOLLEY_N=3,        // shells per volley. Three is a pattern with a shape; five is noise
      BOSS_VOLLEY_GAP=sec(0.55),  // between shells, and each is re-told, so one can be answered alone
      BOSS_PHASE_1=0.66, BOSS_PHASE_2=0.33,   // fractions of max HP
      /* The Warden's name, and the fact that it now exists at all. [h:00-balance-33] */
      BOSS_NAME='THE WARDEN',
      /* The health bar's own numbers, because a bar that governs the fight should be sized by the fight rather than by whatever fitted above the room. [h:00-balance-34] */
      BOSS_BAR_H=10,
      /* The bar's rectangle is anchored to the CANVAS, not to the room, and that is not a preference. [h:00-balance-35] */
      BOSS_BAR_MARGIN=23,   /* canvas bottom to the bottom of the BAR. [h:00-balance-36] */
      BOSS_BAR_INSET_X=20,  // inset each side from the room's own walls, so it lines up with the play area
      BOSS_RECOVER=sec(1.1),   // dead time after every move. The gap the player spends reading the next
      BOSS_CD_MIN=sec(3.2), BOSS_CD_VAR=sec(1.0),   // seconds BETWEEN moves. The single most
                                // important number on the boss, and the one that was wrong first.
      BOSS_SHELL_DMG=1.44,   /* was SHOT_DMG*0.8, which is 1.4400000000000002 in binary floating point - seventeen significant digits for a tuning number, and an arithmetic... [h:00-balance-37] */
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
 /* dmg is the point-blank value; fNear/fFar/fMin shape the falloff (see falloffMult) so a shot fired from across the room is a real commitment while... [h:00-balance-38] */
  {name:'Bolt',color:'#c79bff',cooldown:38*SPEEDUP,speed:2.8,dmg:7,count:1,spread:0.05,r:6,shrink:true,fNear:130,fFar:400,fMin:0.5},
  /* Scatter is a buckshot gun, not a hose: [h:00-balance-39] */
  {name:'Scatter',color:'#e8502a',cooldown:sec(1.1),speed:2.4,dmg:3.9,count:8,spread:0.045,
   /* dmg 3.9 is not a round choice and 3.835 would not do either. [h:00-balance-40] */
   /* The effective range is a CIRCLE of 180px radius - a QUARTER of a 700px room - and inside it the gun does FULL damage. [h:00-balance-41] */
   fNear:180, fFar:320, fMin:0.22,
   muzzleJitter:5,pelletAngle:0.008,pelletSpeedVar:0.25},

/* THE ARCANE BEAM RAISED 0.84 -> 1.55, and the reasoning is worth keeping because two earlier diagnoses were wrong. [h:00-balance-42] */
 /* THE BEAM IS 0.5, AND THAT IS THE POINT. [h:00-balance-43] */
  {name:'Arcane Beam',color:'#3fa9ff',cooldown:5.2*SPEEDUP,speed:4,dmg:0.5,count:1,spread:0.16,spreadFromPrecision:true,r:4,fNear:170,fFar:470,fMin:0.6},
  {name:'Voidball',color:'#3f8a4a',cooldown:24*SPEEDUP,speed:3.2,dmg:3.4,count:1,spread:0.02,fNear:150,fFar:440,fMin:0.55,pierce:3},
];
/* What each pass through a body is worth, as a fraction of the first. [h:00-balance-44] */
const PIERCE_FALLOFF=0.72;
/* The Bolt is the one gun whose damage visibly leaves the projectile as it travels. [h:00-balance-45] */
const BOLT_SIZE_MIN=0.65, BOLT_SIZE_MAX=1.05, BOLT_DRAW_R=6;
/* The right click has two forms, and you start with the one that ends things. [h:00-balance-46] */
const TOUGH=1.35;

/* THE DEPTH LADDER --------------------------------------------------------------------------------- Difficulty in this game is a function of DEPTH. [h:00-balance-47] */

/* THE LADDER IS EXPONENTIAL, and it has no ceiling. [h:00-balance-48] */
const DEPTH_GROWTH=0.40,     // the coefficient: how much of an exponential to add
      DEPTH_POW=0.12,        // the exponent RATE: larger is steeper later
      /* The reaction-time ceiling, and the only one in the file. [h:00-balance-49] */
      DEPTH_RATE_CAP=1.34,
      /* Density is capped too, for a different reason and it is not a fairness argument. [h:00-balance-50] */
      DEPTH_BODY_POW=0.085,
      DEPTH_BODY_CAP=28,
      DEPTH_PACK_STEP=0.035, // +3.5% chance of a Brunch pack per floor, capped
      DEPTH_PACK_CAP=0.85;   // never a certainty: a room that is always a pack is one shape

/* How long the fade between floors lasts, and therefore how long the descent banner is up. [h:00-balance-51] */
const FADE_DESCEND=sec(0.9);

/* The one place the floor number is read. Everything else asks these. Reading run.floor directly
   anywhere else is the thing to watch for in review, for the reason in the comment above. */
function depthFloor(){ return (run&&run.floor)||1; }
function depthSteps(){ return Math.max(0,depthFloor()-1); }

/* AREA. Which themed block a floor is in. [h:00-balance-52] */
function areaForFloor(floor){
  const f=(floor===undefined)?depthFloor():floor;
  return f<=4?'Area1':f<=8?'Area2':f<=12?'Area3':'Final';
}

/* THE ENEMY-MIX DIALS, per area. [h:00-balance-53] */
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
/* Density is its own exponent rather than the HP one, because it is a different kind of lever: [h:00-balance-54] */
function depthBodies(rolled){
  return Math.min(DEPTH_BODY_CAP,rolled+DEPTH_BODY_POW*(Math.exp(DEPTH_POW*1.7*depthSteps())-1));
}

/* ADAPTIVE DIFFICULTY, and it is a PLACEHOLDER with the rules already written down. [h:00-balance-55] */
const ADAPT={ value:0, min:-0.25, max:0.25 };

// The armour multiplier. Per-hit, so it scales every pellet and every pierce pass rather than
// subtracting a flat chunk - a subtraction would quietly reward the Beam for spraying, which is
// exactly backwards.
const ARMOUR=0.66;
/* The pool is a hair over a whole armoured heavy on purpose. [h:00-balance-56] */
const ALT_WEAPON={cooldown:sec(3.6),speed:2.2*0.85,r:12,color:'#ff8a3d',aoeRadius:100,pool:18*TOUGH/ARMOUR*1.02,aoeKnock:2.7};
/* The hook is the blast with the sign flipped. [h:00-balance-57] */
const HOOK_FIELD_TIME=sec(1.8), HOOK_SUCK=0.24, HOOK_DPS=3.5;
/* Resistance. The hook used to be a permanent answer: [h:00-balance-58] */
const HOOK_RESIST=[1, 0.70, 0.45, 0.20, 0.08];
// how long a body must be left alone before it forgets one hook. Long enough that re-hooking the
// same target inside a fight keeps costing you, short enough that a body you walk away from is
// worth full value again by the time you come back to it.
const HOOK_FORGET=sec(7);
let hookFieldId=0;
/* The yank is a spring, not a shove. [h:00-balance-59] */
const HOOK_PULL_GAIN=1-KNOCK_FRICTION;
// counterstrafing: how fast a reversal is forgotten, and how much a reversal widens a gunner's aim
/* SWERVE: the signal that says how UNSETTLED the player's movement is, and the only thing both ranged enemies read to decide how much to believe a lead. [h:00-balance-60] */
const SWERVE_GAIN=0.22, SWERVE_DECAY=0.0035, SWERVE_AIM=0.30;

/* how the player actually moves, and the Momentum meter ------------------------------------- The movement model already has two separate levers,... [h:00-balance-61] */
const MOVE_ACCEL=0.116;

/* Momentum is combat momentum, not walking momentum. [h:00-balance-62] */
const MOMENTUM_GAIN=0.0056, MOMENTUM_STALL_DECAY=0.006, MOMENTUM_HIT_KEEP=0.55;
// below this the player is not going anywhere, so momentum neither charges nor bleeds - a body
// pinned in a corner by two lungers is not "standing still", it is losing, and the meter is right
// to be quiet about it
const MOMENTUM_MOVE_FLOOR=0.3;
/* MOMENTUM, RETUNED FROM MEASUREMENT, and two of the four numbers did not move - which is the interesting part. [h:00-balance-63] */
/* THE METER'S SPEED CONTRIBUTION IS MOMENTUM_SPEED=0.18, used as a MULTIPLIER FACTOR, and this paragraph used to call it "5%" and name the number 0.05. [h:00-balance-64] */
const MOMENTUM_SPEED=0.18, MOMENTUM_ACCEL=0.55;
/* Two ceilings, and the difference matters. [h:00-balance-65] */
const SPEED_CAP=0.40, MOVE_SPEED_HARD_CAP=0.44;

/* PRECISION TIGHTENS THE ARCANE BEAM, in fifths, and the floor is the point. [h:00-balance-66] */
const PRECISION_STEP=0.20, PRECISION_SPREAD_FLOOR=0.04;

/* HOW MUCH A CHARGING BODY BELIEVES A REVERSING PLAYER. [h:00-balance-67] */
const SWERVE_TRUST_WALKING=2.2, SWERVE_TRUST_ROOTED=1.0;
function preciseSpread(base){
  return base*Math.max(PRECISION_SPREAD_FLOOR,1-PRECISION_STEP*Math.max(0,Stats.value('precision')));
}

/* ROOM PRESSURE ---------------------------------------------------------------------------------- One number, from one thing: [h:00-balance-68] */
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

/* HOW LONG THE BLINK BAR TAKES TO FILL AFTER A DOOR, in ticks. [h:00-balance-69] */
const RESTORE_FX_SPAN=READY;

/* HOW FAR a ranged body will actually have walked in N ticks, given the standoff rule it obeys. [h:00-balance-70] */

// How far away a gunner has to be before counterstrafing buys anything. Half the width of the room
// is the line, and the bonus is fully open by the time a body is a room-and-a-half away.
/* Where the counterstrafe answer switches on, and where it is fully open. [h:00-balance-71] */
/* THE SWERVE RAMP IS A FUNCTION OF THE ROOM, for the same reason AGGRO_RANGE is. [h:00-balance-72] */
const swerveDeadzone=()=>Math.round(roomW()*0.25),
      swerveFullBase=()=>Math.round(roomW()*0.55);
const swerveFull=()=>swerveDeadzone()+swerveFullBase();
/* The gunner's cast tell. [h:00-balance-73] */
const CAST_TIME=sec(0.5);
/* The blink's landing burst. [h:00-balance-74] */
const BLINK_BOOST_GAIN=1.55, BLINK_BOOST=sec(0.11);
const HOOK_WEAPON={cooldown:sec(3.6),speed:2.4*0.85,r:14,color:'#2a6fc4',aoeRadius:118,pool:0,phase:true,pull:1.2,hold:sec(0.7),field:HOOK_FIELD_TIME,early:true};

/* there used to be a lookup table mapping the two right-click names to their weapon objects, so bakeIcon could ask "which colour is this thing" with... [h:00-balance-75] */

/* Where being hit actually is. [h:00-balance-76] */
const PLAYER_HIT_DY=10, PLAYER_HIT_R=10;
// The one place that asks "did that hit me", so the offset and the radius cannot drift apart. Both
// the shells and the bodies go through it, which is what keeps those two paths agreeing.
function playerHit(x,y,r){ return Math.hypot(x-player.lagX,y-(player.lagY+PLAYER_HIT_DY))<r+PLAYER_HIT_R; }
// the shove scales with how close you put it: a blast that goes off on top of a body is worth about
// twice what the old flat shove was, and one that only catches something out at the rim is worth
// about a third of that flat number
const ALT_KNOCK_NEAR=2, ALT_KNOCK_FAR=0.35;

