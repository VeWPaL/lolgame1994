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
  const a=Math.atan2(mouse.y-player.y,mouse.x-player.x);
  return [Math.cos(a),Math.sin(a)];
}
function doBlink(){
  const [ux,uy]=getBlinkDir();
  for(let i=0;i<6;i++){const t=i/5;dashFX.push({x:player.x+ux*BLINK_DIST*t,y:player.y+uy*BLINK_DIST*t,life:DASH_TRAIL});}
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
  generateDungeon();
  cur={x:START,y:START};
  player={x:MIDX,y:MIDY,r:13,speed:0.935*PLAYER_MOVE,vx:0,vy:0,kvx:0,kvy:0,lagX:MIDX,lagY:MIDY,hp:8,maxHp:8,armor:0,weaponIdx:0,cooldown:0,cooldownMax:WEAPONS[0].cooldown/TEMPO.rate,altCooldown:0,altCooldownMax:ALT_WEAPON.cooldown/TEMPO.rate,altMode:'blast',iframes:0,hasSilver:false,hasGold:false,
    blinkCharges:2,blinkRegen:0,anim:0,muzzleTimer:0,shootSlow:0,slowMult:1,dirX:0,dirY:0,swerve:0,boost:0,boostX:0,boostY:0,trendVx:0,trendVy:0};
  projectiles=[]; dashFX=[]; burstFX=[]; hookFields=[]; bossUnlocked=false; itemUnlocked=false; trans=null; readyT=0;
  unlockDoor=null; unlockT=0; bossWarnT=0; bossWarned=false; secretFound=false;
  entryDir='N';
  roomFade=1; fadeTicks=sec(0.4); fadeT=fadeTicks;
  run={ticks:0,kills:0,dmgTaken:0,shots:0,hits:0,secret:false,hook:false}; lastRun=null;
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
  records={rooms:get('depths_best'),fastest,wins:get('depths_wins')};
}
function saveRecords(){
  try{
    localStorage.setItem('depths_best',String(records.rooms));
    localStorage.setItem('depths_fastest',String(records.fastest));
    localStorage.setItem('depths_wins',String(records.wins));
    localStorage.setItem(TICK_KEY,String(TICK_HZ));
  }catch(e){}
}
// freeze the run's numbers for the summary screen and fold them into the records (death and win alike)
function endRun(won){
  state=won?'win':'gameover';
  const all=Object.values(rooms), explored=all.filter(x=>x.visited).length;
  const s={won,ticks:run.ticks,explored,total:all.length,kills:run.kills,dmgTaken:run.dmgTaken,
    shots:run.shots,hits:run.hits,weapon:WEAPONS[player.weaponIdx].name,newRooms:false,newFastest:false};
  if(explored>records.rooms){records.rooms=explored;s.newRooms=true;}
  if(won){
    records.wins++;
    if(!records.fastest||s.ticks<records.fastest){records.fastest=s.ticks;s.newFastest=true;}
  }
  saveRecords();
  lastRun=s;
}

// pausing only exists during a run; acc is dropped so resuming never replays the paused time
function setPaused(on){
  paused=!!on&&state==='playing';
  acc=0;
}
function autoPause(){
  keys={}; releaseButtons();   // the matching keyup/mouseup may never arrive
  setPaused(true);
}

function tickBlink(){
  if(player.blinkCharges>=2) return;
  // BLINK_FILL_CLEAR per tick is 7, so a charge comes back in 1.1s in a quiet room and
  // BLINK_RECHARGE (8s) in a fight
  // TEMPO quickens the fill rate rather than shortening the bar, so the HUD fraction stays honest
  player.blinkRegen+=(currentRoom().enemies.length===0?BLINK_FILL_CLEAR:1)*TEMPO.rate;
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

