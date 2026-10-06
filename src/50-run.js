/* 50-run - blink, run flow, records, pause The only thing here that is not pure simulation is the records pair, which touches localStorage. [h:50-run-1] */
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
  /* The trail is tagged, and the tag carries the meter as it was AT THE MOMENT OF THE BLINK. [h:50-run-2] */
  const mom=Momentum.level();
  /* THE TRAIL FOLLOWS WHERE THE PLAYER ACTUALLY ENDED UP, and it used to be six puffs laid along `BLINK_DIST` regardless of whether the blink went... [h:50-run-3] */
  const fromX=player.x, fromY=player.y;
  player.x+=ux*BLINK_DIST; player.y+=uy*BLINK_DIST;
  player.blinkCharges--;
  clampPlayer();
  /* the puffs are laid after the clamp, over the distance that was actually covered. [h:50-run-4] */
  const endX=Math.max(ROOM_LEFT,Math.min(ROOM_RIGHT,player.x));
  const endY=Math.max(ROOM_TOP,Math.min(ROOM_BOTTOM,player.y));
  const trav=Math.hypot(endX-fromX,endY-fromY);
  /* SPACING IS CAPPED so a short blink is a tight cluster rather than six puffs stacked on one
     pixel, and a full-length blink is unchanged. 24px is roughly a body width: close enough that the
     six read as a continuous streak, far enough that they do not all land on the same sprite. */
  const puffs=Math.max(1,Math.min(6,Math.ceil(trav/24)));
  for(let i=0;i<puffs;i++){
    const t=puffs===1?1:i/(puffs-1);
    dashFX.push({x:fromX+(endX-fromX)*t,y:fromY+(endY-fromY)*t,life:DASH_TRAIL,mine:true,mom});
  }
  /* THE POSITION THE ENEMIES AIM AT STAYS PUT, and it stays at the PRE-MOVE position - which is now `fromX`/`fromY` rather than `player.x`, because... [h:50-run-5] */
  /* The blink's sound goes HERE, with the i-frames below it, because the i-frames ARE the action:
     the player needs to know they have committed before they can see where it ended. */
  Sfx.blink();
  player.lagX=fromX; player.lagY=fromY;
  checkDoorTransition();
  /* Invulnerable for the whole travel, not just the first frame of it. [h:50-run-6] */
  player.iframes=Math.max(player.iframes,BLINK_IFRAMES+DASH_TRAIL);
  /* The grace starts here and is spent once, whatever the blink is used for. [h:50-run-7] */
  player.blinkGrace=BLINK_GRACE;
  player.graceSpent=false;
  /* Momentum. Landing on your feet and then walking out of the blink at the same speed you arrived at is what makes it feel like a teleport rather... [h:50-run-8] */
  player.boost=BLINK_BOOST;
  player.boostX=ux; player.boostY=uy;
}

function startGame(root){
  /* A new run starts from nothing, and that means the BUILD and not merely the numbers. [h:50-run-9] */
  if(typeof Items!=='undefined') Items.reset(); else Stats.reset();
  /* REPRODUCIBLE, WHICH TAKES AN ARGUMENT. [h:50-run-10] */
  Rnd.set(root===undefined?Rnd.fresh():root);
  /* THE ART CACHES ARE RUN-SCOPED, and this is the line that makes them so. [h:50-run-11] */
  clearArtCaches();
  /* The flank walk and the pack-id counter live at module scope in 20-world.js, so they are part of what "start from nothing" has to mean. [h:50-run-12] */
  resetRunCursors();
  generateDungeon();
  cur={x:START,y:START};
  // the wall shorthand, re-synced before the player is placed: the line below puts the player at
  // MIDX/MIDY, and MIDX is the current room's centre, so it has to already be the new room's
  syncRoomBounds();
  player={x:MIDX,y:MIDY,r:13,speed:0.935*PLAYER_MOVE,vx:0,vy:0,kvx:0,kvy:0,lagX:MIDX,lagY:MIDY,hp:8,maxHp:8,armor:0,weaponIdx:0,cooldown:0,cooldownMax:WEAPONS[0].cooldown/TEMPO.rate,altCooldown:0,altCooldownMax:ALT_WEAPON.cooldown/TEMPO.rate,altMode:'blast',iframes:0,hasSilver:false,hasGold:false,
    blinkCharges:2,blinkRegen:0,blinkRestore:null,blinkGrace:0,graceSpent:false,anim:0,muzzleTimer:0,shootSlow:0,slowMult:1,dirX:0,dirY:0,swerve:0,boost:0,boostX:0,boostY:0,momentum:0,trendVx:0,trendVy:0,beliefVx:0,beliefVy:0};
  clearTransient(); bossUnlocked=false; itemUnlocked=false; trans=null; readyT=0;
  unlockDoor=null; unlockT=0; bossWarnT=0; bossWarned=false; secretFound=false;
  entryDir='N';
  roomFade=1; fadeTicks=sec(0.4); fadeT=fadeTicks;
  applyVitals();   // after the player exists, so a Vigor item and a fresh body agree
  /* `unlocked` is created WITH the run, which it was not until the lab handed this over by trying to equip every item in the game at once and throwing... [h:50-run-13] */
  run={floor:1,floorTicks:0,roomsThisFloor:0,rootSeed:Rnd.seed,ticks:0,kills:0,dmgTaken:0,shots:0,hits:0,secret:false,hook:false,unlocked:{}}; lastRun=null;
  paused=false; acc=0;
  state='playing';
}

/* records: rooms and wins are unitless, but fastest is stored in ticks, so a run recorded under a
   different tick rate is converted on read (and TICK_KEY is rewritten on save) */
const TICK_KEY='depths_tps';
/* THE RECORDS LIVE IN ONE KEY, and the four flat ones are the previous format. [h:50-run-14] */
const RECORDS_KEY='depths_records';
const RECORD_LEGACY_KEYS=['depths_best','depths_fastest','depths_wins','depths_deepest'];
/* ONE console line, not one per frame. `saveRecords` is called on run end and on every record beat, so
   an unwarned failure in a loop is either invisible or a flood - and the case that matters is the first
   one, which is the one a player would report if they noticed at all. */
let RECORD_SAVE_WARNED=false;
function loadRecords(){
  const get=k=>{try{return parseInt(localStorage.getItem(k)||'0',10)||0;}catch(e){return 0;}};
  /* THE SINGLE-KEY RECORD IS THE CURRENT FORMAT, and the flat keys are the previous one. [h:50-run-15] */
  let single=null;
  try{
    const raw=localStorage.getItem(RECORDS_KEY);
    if(raw){ const o=JSON.parse(raw);
      if(o&&typeof o==='object') single={rooms:+o.rooms||0,fastest:+o.fastest||0,
                                          wins:+o.wins||0,deepest:+o.deepest||0}; }
  }catch(e){ single=null; }
  const oldRate=get(TICK_KEY)||60;
  if(single){
    let fastest=single.fastest;
    /* the tick-rate conversion still applies to a single-key record, because the key does not record
       which tick rate wrote it - that is what TICK_KEY is for, and it is why the version is written
       as its own key rather than inside the record */
    if(fastest&&oldRate!==TICK_HZ) fastest=Math.round(fastest*TICK_HZ/oldRate);
    records={rooms:single.rooms,fastest,wins:single.wins,deepest:single.deepest};
    return;
  }
  let fastest=get('depths_fastest');
  if(fastest&&oldRate!==TICK_HZ) fastest=Math.round(fastest*TICK_HZ/oldRate);
  records={rooms:get('depths_best'),fastest,wins:get('depths_wins'),deepest:get('depths_deepest')};
}
function saveRecords(){
  /* ONE KEY, WRITTEN ONCE. [h:50-run-16] */
  try{
    localStorage.setItem(RECORDS_KEY,JSON.stringify(records));
    /* retire the flat keys on the first successful save, and keep writing TICK_KEY separately because it is not part of the record - it is the schema... [h:50-run-17] */
    localStorage.setItem(TICK_KEY,String(TICK_HZ));
    for(const k of RECORD_LEGACY_KEYS){ try{localStorage.removeItem(k);}catch(e){} }
  }catch(e){
    if(!RECORD_SAVE_WARNED){
      RECORD_SAVE_WARNED=true;
      if(typeof console!=='undefined'&&console.warn)
        console.warn('[depths] could not save records ('+(e&&e.name||'unknown')+') - this run will '+
          'not be recorded. Storage may be full or blocked.');
    }
  }
}
// freeze the run's numbers for the summary screen and fold them into the records (death and win alike)
/* DESCEND. The boss dies, the way out appears, and walking into it takes you DOWN rather than out. [h:50-run-18] */
function descend(){
  const from=run.floor;
  run.floor=from+1;
  run.floorTicks=0;
  run.roomsThisFloor=0;
  /* A new seed per floor, derived from the ROOT the player typed rather than from the previous floor's seed, so every floor is a pure function of... [h:50-run-19] */
  Rnd.set(Rnd.floorSeed(run.rootSeed,run.floor));
  /* THE ART CACHES ARE CLEARED HERE TOO, and this is the second place that has to happen. [h:50-run-20] */
  clearArtCaches();
  generateDungeon();
  cur={x:START,y:START};
  // same reason as startGame: the line below re-centres the player on MIDX, and this is a freshly
  // generated floor whose rooms may not be the size the last one was
  syncRoomBounds();
  // the body carries down whole. Not the position, not the projectiles, not the room - the build.
  player.x=MIDX; player.y=MIDY; player.lagX=MIDX; player.lagY=MIDY;
  player.vx=0; player.vy=0; player.kvx=0; player.kvy=0;
  clearTransient();
  bossUnlocked=false; itemUnlocked=false; trans=null;
  unlockDoor=null; unlockT=0; bossWarnT=0; bossWarned=false; secretFound=false;
  // The blink comes back full, because arriving at a new floor with no escape is a punishment for
  // arriving at a new floor, and the depth ladder is supposed to be the only thing that is harder.
  player.blinkCharges=2; player.blinkRegen=0; player.blinkRestore=null;
  player.blinkGrace=0; player.graceSpent=false;
  entryDir='N';
  /* The fade is longer between floors than between rooms, and deliberately so: [h:50-run-21] */
  roomFade=1; fadeTicks=FADE_DESCEND; fadeT=fadeTicks;
  descendFrom=from; descendT=FADE_DESCEND;
  paused=false; acc=0;
  state='playing';
  if(records.deepest<run.floor){ records.deepest=run.floor; saveRecords(); }
}

/* A run now ends in exactly one way, which is dying, so `won` is nearly dead weight - but the parameter stays because the game-over screen reads it... [h:50-run-22] */
function endRun(won){
  /* THE LAB CANNOT END A RUN, and the guard is HERE rather than at the call sites. [h:50-run-23] */
  if(state==='dev') return false;
  /* The run's last sound, AFTER the lab guard above: the lab cannot end a run, so it must
     not make the sound of one either - the same reason, one line later. */
  Sfx.over();
  state=won?'win':'gameover';
  const all=Object.values(rooms), explored=all.filter(x=>x.visited).length;
  const s={won,ticks:run.ticks,floor:run.floor,floorTicks:run.floorTicks,explored,total:all.length,
    kills:run.kills,dmgTaken:run.dmgTaken,shots:run.shots,hits:run.hits,
    weapon:WEAPONS[player.weaponIdx].name,
    /* THE ROOT SEED, NOT Rnd.seedText. [h:50-run-24] */
    seed:Rnd.encode(run.rootSeed),
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

/* pausing only exists during a run; acc is dropped so resuming never replays the paused time. [h:50-run-25] */
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
  /* A charge comes back in BLINK_RECHARGE seconds of wall clock, in a fight, and that is the number a designer tunes. [h:50-run-26] */
  player.blinkRegen+=(currentRoom().enemies.length===0?BLINK_FILL_CLEAR:1);
  if(player.blinkRegen>=BLINK_RECHARGE){player.blinkCharges++;player.blinkRegen=0;}
}
function killEnemy(r,j){
  /* SOUND, IN killEnemy AND NOWHERE ELSE. Every way a body leaves a room - projectile, contact,
     sweep, the Brunch spending itself - goes through this function, so a sound here is a sound for
     every death rather than one for the deaths somebody remembered to add it to. */
  Sfx.kill(r.enemies[j]);
  const e=r.enemies[j];
  r.enemies.splice(j,1);
  // The Warden's wall goes with it. Only stepBoss expired the statues, so after the boss died they
  // stood until shot one by one, and the exit waits for an empty room. Not kills, so no loot.
  if(e.type==='boss') for(let i=r.enemies.length-1;i>=0;i--) if(r.enemies[i].packId===BOSS_WALL_ID) r.enemies.splice(i,1);
  run.kills++;
  const d=dropLoot(e.x,e.y); if(d) r.pickups.push(d);
}
function tickFX(){
  for(let i=dashFX.length-1;i>=0;i--) if(--dashFX[i].life<=0) dashFX.splice(i,1);
  for(let i=burstFX.length-1;i>=0;i--) if(--burstFX[i].life<=0) burstFX.splice(i,1);
  for(let i=hookFields.length-1;i>=0;i--) if(hookFields[i].life<=0) hookFields.splice(i,1);
}

