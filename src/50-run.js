/* ==============================================================================================
   50-run  -  blink, run flow, records, pause

   The only thing here that is not pure simulation is the records pair, which touches localStorage.
   In C# that becomes an interface with a PlayerPrefs or file implementation; everything else moves
   across as-is.
   ============================================================================================== */
function getBlinkDir(){
  let dx=0,dy=0;
  if(keys['w']||keys['arrowup'])dy-=1;
  if(keys['s']||keys['arrowdown'])dy+=1;
  if(keys['a']||keys['arrowleft'])dx-=1;
  if(keys['d']||keys['arrowright'])dx+=1;
  let len=Math.hypot(dx,dy);
  if(len>0) return [dx/len,dy/len];
  len=Math.hypot(player.vx,player.vy);
  if(len>0.1) return [player.vx/len,player.vy/len];
  const aim=mouseWorld();
  const a=Math.atan2(aim.y-player.y,aim.x-player.x);
  return [Math.cos(a),Math.sin(a)];
}
function doBlink(){
  const [ux,uy]=getBlinkDir();
  /* The trail is tagged, and the tag carries the meter as it was AT THE MOMENT OF THE BLINK.

     dashFX is shared - the lunge charge puff, the lunge trail and the Brunch absorb puff all live in
     it - so a tint applied to every particle in the array would paint enemy telegraphs green too,
     which is a lie about the enemy rather than a readout of the player. Tagging is what makes the
     ramp mean anything: green on a body you own, white on a body you are reading.

     Read once here rather than read live in the draw, because a trail that re-read the meter while it
     was still on screen would flicker up the whole ramp as the meter moved underneath it. */
  const mom=Momentum.level();
  for(let i=0;i<6;i++){const t=i/5;dashFX.push({x:player.x+ux*BLINK_DIST*t,y:player.y+uy*BLINK_DIST*t,
    life:DASH_TRAIL,mine:true,mom});}
  // the position the enemies aim at stays put and eases across over ~0.4s, which is the reaction
  // window a blink is supposed to buy. without it a gunner that was already tracking you gets a
  // free intercept shot the instant you vanish
  player.lagX=player.x; player.lagY=player.y;
  player.x+=ux*BLINK_DIST; player.y+=uy*BLINK_DIST;
  player.blinkCharges--;
  clampPlayer();
  checkDoorTransition();
  // Invulnerable for the whole travel, not just the first frame of it. BLINK_IFRAMES used to be a
  // flat 0.17s that began on the same tick as the jump, so a blink into a closing Brunch or past a
  // shell only protected the moment of crossing the line - the half of the blink that actually
  // happens afterwards was fair game, and the move reads as "get me out of here" precisely when
  // there is something to get out of. The blink is a two-charge escape with an 8s recharge; if the
  // window is shorter than the animation, the cooldown is the real punishment and the escape is not.
  // The starting frames are set after the move so they cover the travel rather than the step into it.
  // The blink's invulnerability covers the move and then the beat after it, which is what the trail
  // draws. Not the distance at walking speed: the blink is a teleport, it does not take 0.6s to
  // cross 140px, and sizing the window by the distance made a two-charge escape on an 8s recharge
  // worth 0.77s of immunity - long enough to walk through a room unharmed twice over.
  player.iframes=Math.max(player.iframes,BLINK_IFRAMES+DASH_TRAIL);
  // The grace starts here and is spent once, whatever the blink is used for. Resetting graceSpent
  // per blink rather than per room is what keeps it to one forgiven hit: blink into a pack and the
  // second body still connects, which is the whole reason this is forgiveness and not a longer
  // invulnerability window. See damagePlayer.
  player.blinkGrace=BLINK_GRACE;
  player.graceSpent=false;
  // Momentum. Landing on your feet and then walking out of the blink at the same speed you arrived
  // at is what makes it feel like a teleport rather than an escape - you spend the dodge and get
  // nothing for it, so the correct play is to bank the charges for emergencies instead of using one
  // to gain ground. A short burst ALONG THE DIRECTION YOU BLINKED gives the move a purpose beyond
  // survival: you can blink out of a shell and keep the ground you bought, and turning around on
  // landing throws the whole of it away. It decays fast on purpose - long enough to matter for the
  // next beat, not long enough to become the player's real top speed.
  player.boost=BLINK_BOOST;
  player.boostX=ux; player.boostY=uy;
}

function startGame(){
  /* A new run starts from nothing, and that means the BUILD and not merely the numbers. This used to
     call Stats.reset() alone, which zeroed every stat while leaving loadout.items populated - so after
     pressing R the character sheet listed the previous run's items with none of their effects
     applied. The same lie as an inert stat, in the other order: the sheet and the game disagreed, and
     the sheet was the one that had been right a moment earlier. */
  if(typeof Items!=='undefined') Items.reset(); else Stats.reset();
  generateDungeon();
  cur={x:START,y:START};
  // the wall shorthand, re-synced before the player is placed: the line below puts the player at
  // MIDX/MIDY, and MIDX is the current room's centre, so it has to already be the new room's
  syncRoomBounds();
  player={x:MIDX,y:MIDY,r:13,speed:0.935*PLAYER_MOVE,vx:0,vy:0,kvx:0,kvy:0,lagX:MIDX,lagY:MIDY,hp:8,maxHp:8,armor:0,weaponIdx:0,cooldown:0,cooldownMax:WEAPONS[0].cooldown/TEMPO.rate,altCooldown:0,altCooldownMax:ALT_WEAPON.cooldown/TEMPO.rate,altMode:'blast',iframes:0,hasSilver:false,hasGold:false,
    blinkCharges:2,blinkRegen:0,blinkRestore:null,blinkGrace:0,graceSpent:false,anim:0,muzzleTimer:0,shootSlow:0,slowMult:1,dirX:0,dirY:0,swerve:0,boost:0,boostX:0,boostY:0,momentum:0,trendVx:0,trendVy:0,beliefVx:0,beliefVy:0};
  projectiles=[]; dashFX=[]; burstFX=[]; hookFields=[]; bossUnlocked=false; itemUnlocked=false; trans=null; readyT=0;
  unlockDoor=null; unlockT=0; bossWarnT=0; bossWarned=false; secretFound=false;
  entryDir='N';
  roomFade=1; fadeTicks=sec(0.4); fadeT=fadeTicks;
  applyVitals();   // after the player exists, so a Vigor item and a fresh body agree
  /* `unlocked` is created WITH the run, which it was not until the lab handed this over by trying to
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
     farmed for. */
  run={floor:1,floorTicks:0,roomsThisFloor:0,rootSeed:Rnd.seed,ticks:0,kills:0,dmgTaken:0,shots:0,hits:0,secret:false,hook:false,unlocked:{}}; lastRun=null;
  paused=false; acc=0;
  state='playing';
}

/* records: rooms and wins are unitless, but fastest is stored in ticks, so a run recorded under a
   different tick rate is converted on read (and TICK_KEY is rewritten on save) */
const TICK_KEY='depths_tps';
function loadRecords(){
  const get=k=>{try{return parseInt(localStorage.getItem(k)||'0',10)||0;}catch(e){return 0;}};
  const oldRate=get(TICK_KEY)||60;
  let fastest=get('depths_fastest');
  if(fastest&&oldRate!==TICK_HZ) fastest=Math.round(fastest*TICK_HZ/oldRate);
  records={rooms:get('depths_best'),fastest,wins:get('depths_wins'),deepest:get('depths_deepest')};
}
function saveRecords(){
  try{
    localStorage.setItem('depths_best',String(records.rooms));
    localStorage.setItem('depths_fastest',String(records.fastest));
    localStorage.setItem('depths_wins',String(records.wins));
    localStorage.setItem('depths_deepest',String(records.deepest));
    localStorage.setItem(TICK_KEY,String(TICK_HZ));
  }catch(e){}
}
// freeze the run's numbers for the summary screen and fold them into the records (death and win alike)
/* DESCEND. The boss dies, the way out appears, and walking into it takes you DOWN rather than out.

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
   wants the total time they survived as much as they want the floor they reached. */
function descend(){
  const from=run.floor;
  run.floor=from+1;
  run.floorTicks=0;
  run.roomsThisFloor=0;
  // A new seed per floor, derived from the ROOT the player typed rather than from the previous
  // floor's seed, so every floor is a pure function of (root, floor) and the whole run replays from
  // the one seed on the luggage tag. Floor 1 keeps the root exactly, because the seed the player
  // typed should be the dungeon they actually get. See Rnd.floorSeed for why chaining would be wrong.
  Rnd.set(Rnd.floorSeed(run.rootSeed,run.floor));
  generateDungeon();
  cur={x:START,y:START};
  // same reason as startGame: the line below re-centres the player on MIDX, and this is a freshly
  // generated floor whose rooms may not be the size the last one was
  syncRoomBounds();
  // the body carries down whole. Not the position, not the projectiles, not the room - the build.
  player.x=MIDX; player.y=MIDY; player.lagX=MIDX; player.lagY=MIDY;
  player.vx=0; player.vy=0; player.kvx=0; player.kvy=0;
  projectiles=[]; dashFX=[]; burstFX=[]; hookFields=[];
  bossUnlocked=false; itemUnlocked=false; trans=null; respawnT=0;
  unlockDoor=null; unlockT=0; bossWarnT=0; bossWarned=false; secretFound=false;
  // The blink comes back full, because arriving at a new floor with no escape is a punishment for
  // arriving at a new floor, and the depth ladder is supposed to be the only thing that is harder.
  player.blinkCharges=2; player.blinkRegen=0; player.blinkRestore=null;
  player.blinkGrace=0; player.graceSpent=false;
  entryDir='N';
  // The fade is longer between floors than between rooms, and deliberately so: it is the one beat in
  // the game where nothing is trying to kill you, and it is where a player looks at the sheet.
  // FADE_DESCEND is the single source of its length - descend() uses it for the fade, drawDescent()
  // divides by it to time the banner, and update() counts it down. Three numbers for one duration is
  // how a banner ends up outliving the fade it was drawn on.
  roomFade=1; fadeTicks=FADE_DESCEND; fadeT=fadeTicks;
  descendFrom=from; descendT=FADE_DESCEND;
  paused=false; acc=0;
  state='playing';
  if(records.deepest<run.floor){ records.deepest=run.floor; saveRecords(); }
}

/* A run now ends in exactly one way, which is dying, so `won` is nearly dead weight - but the
   parameter stays because the game-over screen reads it and a run that used to be winnable should
   still be able to SAY so if anything ever routes back here. What is new is the depth: the floor
   reached is the headline number now, because it is the one the whole ladder is built around and the
   one a player is actually trying to improve.

   rooms explored is per-floor, not per-run. Summing rooms across every floor would produce a number
   that grows without meaning anything - floor 12 alone has more rooms than floor 1, so a player who
   died early would show a respectable total for having seen very little. */
function endRun(won){
  /* THE LAB CANNOT END A RUN, and the guard is HERE rather than at the call sites.

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
     yet, and the comment is here for whoever writes the fifth one. */
  if(state==='dev') return false;
  state=won?'win':'gameover';
  const all=Object.values(rooms), explored=all.filter(x=>x.visited).length;
  const s={won,ticks:run.ticks,floor:run.floor,floorTicks:run.floorTicks,explored,total:all.length,
    kills:run.kills,dmgTaken:run.dmgTaken,shots:run.shots,hits:run.hits,
    weapon:WEAPONS[player.weaponIdx].name,seed:Rnd.seedText,
    newRooms:false,newFastest:false,newDepth:false};
  if(explored>records.rooms){records.rooms=explored;s.newRooms=true;}
  if(run.floor>records.deepest){records.deepest=run.floor;s.newDepth=true;}
  if(won){
    records.wins++;
    if(!records.fastest||s.ticks<records.fastest){records.fastest=s.ticks;s.newFastest=true;}
  }
  saveRecords();
  lastRun=s;
}

// pausing only exists during a run; acc is dropped so resuming never replays the paused time.
// Pausing OPENS THE CHARACTER SHEET rather than dimming the game and stopping: a player who pauses in
// a fight is asking what their build is, and an overlay that only says PAUSED makes them resume, walk
// out of the room, and pause again somewhere safer to find out.
function setPaused(on){
  paused=!!on&&state==='playing';
  acc=0;
  if(paused){ if(typeof openCharSheet==='function') openCharSheet(); }
  else if(typeof closeCharSheet==='function') closeCharSheet();
}
function autoPause(){
  keys={}; releaseButtons();   // the matching keyup/mouseup may never arrive
  setPaused(true);
}

function tickBlink(){
  if(player.blinkCharges>=2) return;
  /* A charge comes back in BLINK_RECHARGE seconds of wall clock, in a fight, and that is the number
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
     prevent. Every figure quoted in a comment here is now a figure the code actually uses. */
  player.blinkRegen+=(currentRoom().enemies.length===0?BLINK_FILL_CLEAR:1);
  if(player.blinkRegen>=BLINK_RECHARGE){player.blinkCharges++;player.blinkRegen=0;}
}
function killEnemy(r,j){
  const e=r.enemies[j];
  r.enemies.splice(j,1);
  run.kills++;
  const d=dropLoot(e.x,e.y); if(d) r.pickups.push(d);
}
function tickFX(){
  for(let i=dashFX.length-1;i>=0;i--) if(--dashFX[i].life<=0) dashFX.splice(i,1);
  for(let i=burstFX.length-1;i>=0;i--) if(--burstFX[i].life<=0) burstFX.splice(i,1);
  for(let i=hookFields.length-1;i>=0;i--) if(hookFields[i].life<=0) hookFields.splice(i,1);
}

