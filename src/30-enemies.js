/* 30-enemies - spawning a body, and everything it thinks Includes the lunge, the only committed attack in the game and the only mechanic whose... [h:30-enemies-1] */
/* BUILD-READS: a body that answers the gun the player is holding. [h:30-enemies-2] */
const TRAIT_HOLD=1, TRAIT_CLOSE=2;

/* Which answer a gun gets, and how reliably. [h:30-enemies-3] */
const TRAIT_TABLE={
  bolt:       {trait:TRAIT_CLOSE, weight:0.50},
  scatter:    {trait:TRAIT_HOLD,  weight:0.70},
  arcane_beam:{trait:TRAIT_HOLD,  weight:0.62},
  voidball:   {trait:TRAIT_CLOSE, weight:0.50},
};
const TRAIT_HOLD_SCALE=1.45;   // shift the band outward by this much
const TRAIT_CLOSE_SCALE=0.72;  // ...or inward by this much
const TRAIT_CHANCE=0.5;         // per body, so a room is a mix and not a themed set

/* Where the shifted band is allowed to land. [h:30-enemies-4] */
const TRAIT_FAR_CEIL=0.78, TRAIT_FAR_FLOOR=90, TRAIT_BAND_MIN=40;

function weaponIdOf(w){ return Content.idOf(w||WEAPONS[player.weaponIdx]); }

function rollTrait(w){
  const row=TRAIT_TABLE[weaponIdOf(w)];
  if(!row) return 0;
  return Rnd.run()<TRAIT_CHANCE*row.weight ? row.trait : 0;
}

/* Applied to RANGED bodies only, and the restriction is not a preference - it is the bug this shipped once. [h:30-enemies-5] */
function applyTrait(e){
  if(e.walkSpeed!==undefined) return 0;   // a walker has no band to move
  if(e.far===undefined) return 0;         // the boss before it has been given its kit
  const t=rollTrait();
  if(!t) return 0;
  e.trait=t;
  const k=t===TRAIT_HOLD?TRAIT_HOLD_SCALE:TRAIT_CLOSE_SCALE;
  const cap=e.sense*TRAIT_FAR_CEIL;
  let far=e.far*k, close=e.close*k;
  if(far>cap) far=cap;
  if(far<TRAIT_FAR_FLOOR) far=TRAIT_FAR_FLOOR;
  if(far-close<TRAIT_BAND_MIN) close=far-TRAIT_BAND_MIN;
  e.far=far; e.close=close;
  return t;
}


function spawnEnemy(boss,room,x,y,type){
  const rx=()=>ROOM_LEFT+SPAWN_MARGIN+Rnd.run()*(ROOM_RIGHT-ROOM_LEFT-SPAWN_MARGIN*2);
  const ry=()=>ROOM_TOP+SPAWN_MARGIN+Rnd.run()*(ROOM_BOTTOM-ROOM_TOP-SPAWN_MARGIN*2);
  const base={x:x===undefined?rx():x,y:y===undefined?ry():y,hitFlash:0,idleDir:[0,0],idleTimer:(Rnd.jitter()*WANDER_TICKS)|0,
    noticeTimer:(Rnd.jitter()*16*SPEEDUP)|0,anim:0,kvx:0,kvy:0,stun:0,slowT:0,alerted:false,dodgeCd:0,pursuit:0};
  const c=ENEMY[boss?'boss':type==='shooter'||type==='gunner'?type:type==='brunch'?'brunch':'lunger'];
  const e=Object.assign(base,{type:boss?'boss':type||'lunger',mass:c.mass,r:c.r,art:c.art,bar:c.bar,hp:c.hp*depthTough(),maxHp:c.hp*depthTough(),armour:c.armour===undefined?ARMOUR:c.armour});
  if(c.base!==undefined) e.speed=c.base*PRESSURE.rate*depthRate();
  if(boss){e.aggroTimer=9999;bossInit(e);return e;}
  // lungers and Brunch are the same shape of body: they walk at you and hit you on contact. only
  // the two gunners carry a ranged kit
  if(c.walk!==undefined){e.curSpeed=c.walk;e.walkSpeed=c.walk;e.runSpeed=c.run;e.aggroTimer=0;
    // lunger-only lunge state. Brunch do not get it: they are the pressure, and a pack that also
    // telegraphs is a room with nothing left to read
    e.lungeState='approach'; e.lungeT=0; e.lungeCd=0; e.lungeDx=0; e.lungeDy=0;
  e.hookStacks=0; e.hookCalm=0; e.hookMark=0;
  /* Flank slot. Walked round the circle by the golden angle, so no two bodies ever share a slot however many have spawned - and NOT randomised,... [h:30-enemies-6] */
  e.flank=FLANK_CURSOR;
  FLANK_CURSOR+=2.399963229728653;
  if(FLANK_CURSOR>6.283185307179586) FLANK_CURSOR-=6.283185307179586;
    e.lungeFromX=x===undefined?0:x; e.lungeFromY=y===undefined?0:y;
    e.lungeChargeFx=0; e.lungeTrail=0;
    return e;}
  e.range=c.range; e.sense=c.sense; e.close=c.close; e.far=c.far;
  /* depthRate() divides as well as multiplies. [h:30-enemies-7] */
  e.cdMin=c.cdMin/PRESSURE.rate/depthRate(); e.cdVar=c.cdVar/PRESSURE.rate/depthRate();
  e.castT=0; e.castReady=false; e.castAim=0;
  e.dmg=c.dmg; e.pspd=c.pspd; e.pr=c.pr; e.pcol=c.pcol; e.aggroTimer=0;
  e.shootCd=e.cdMin+Rnd.jitter()*e.cdVar;
  /* The trait is rolled ONCE, here, and written onto the body. [h:30-enemies-8] */
  applyTrait(e);
  return e;
}

/* The lunger's whole attack. [h:30-enemies-9] */
/* Where a body leaving a lunger's position, at lunge speed and after a windup it spends PLANTED, can first reach a player who is already moving at... [h:30-enemies-10] */
function solveIntercept(e,gx,gy){
  /* THE BELIEF IS A DISPLACEMENT, NOT A HEADING, and that is the whole correction. [h:30-enemies-11] */
  /* HOW MUCH OF THE BELIEVED DISPLACEMENT IS REAL. [h:30-enemies-12] */
  const conf=1-player.swerve;
  // READS the belief the tick maintains. It does not update it: a function called "solve" that
  // rewrites the player's state is doing two jobs, and the tick is the only thing that owns
  // per-frame state. The belief is maintained beside trendV in update().
  let dirVx=player.beliefVx, dirVy=player.beliefVy;
  if(Math.hypot(dirVx,dirVy)<1e-4){
    dirVx=player.trendVx; dirVy=player.trendVy;   // never aim a lunge from a zero-length belief
  }
  const bvx=dirVx*PLAYER_MOVE*conf, bvy=dirVy*PLAYER_MOVE*conf;
  let ax=gx, ay=gy, n=0;
  for(let k=0;k<LUNGE_ITER;k++){
    n=Math.hypot(ax,ay)/LUNGE_SPEED;
    ax=gx+bvx*(LUNGE_WINDUP+n);
    ay=gy+bvy*(LUNGE_WINDUP+n);
  }
  const d=Math.hypot(ax,ay)||1;
  return {dx:ax/d, dy:ay/d, dist:d, n:n, conf:conf};
}
function stepLunge(e,ux,uy,dist,sm,room){
  if(!e.lungeState) e.lungeState='approach';
  if(e.lungeCd>0) e.lungeCd--;
  switch(e.lungeState){
    case 'approach':{
      /* THE STANDOFF, which is what makes this mechanic a mechanic. [h:30-enemies-13] */
    if(room.enemies.length>1){
      const bearing=Math.atan2(e.y-player.y, e.x-player.x);
      let err=e.flank-bearing;
      while(err>Math.PI) err-=2*Math.PI;
      while(err<-Math.PI) err+=2*Math.PI;
      const side=err>=0?1:-1;
      e.x+=-Math.sin(bearing)*side*LUNGER_SPREAD; e.y+=Math.cos(bearing)*side*LUNGER_SPREAD;
    }
      if(dist<LUNGE_HOLD){
        e.curSpeed+=(0-e.curSpeed)*LUNGER_ACCEL*8;    // close enough to lunge; must not walk in
      } else {
        e.curSpeed+=((approachSpeed(dist)*sm)-e.curSpeed)*LUNGER_ACCEL*6;
        e.x+=ux*e.curSpeed; e.y+=uy*e.curSpeed;
      }
      if(e.lungeCd<=0){
        /* The commitment is made HERE, at the start of the windup, and never revised. [h:30-enemies-14] */
        const sol=solveIntercept(e,player.x-e.x,player.y-e.y);
        // and never from inside contact. A lunge is a gap-closing attack; fired from arm's length it
        // is a contact attack wearing a lunge's clothes, and the line it draws is a decoration.
        if(dist>=LUNGE_MIN&&sol.dist<=LUNGE_REACH){
          e.lungeState='wind'; e.lungeT=LUNGE_WINDUP;
          e.lungeDx=sol.dx; e.lungeDy=sol.dy;
          e.lungeFromX=e.x; e.lungeFromY=e.y;
          e.lungeLen=Math.max(LUNGE_FLOOR,sol.dist);
        }
      }
      break;
    }
    case 'wind':{
      // planted. it does not creep forward, because a lunger that keeps walking through its own
      // windup closes the gap it just told the player it was going to stop at
      e.curSpeed=0;
      e.lungeT--;
      /* No stretching, and no re-aiming. [h:30-enemies-15] */
      if(--e.lungeChargeFx<=0){ e.lungeChargeFx=sec(0.06); dashFX.push({x:e.x,y:e.y,life:LUNGE_CHARGE_TRAIL,charge:1}); }
      if(e.lungeT<=0){ e.lungeState='lunge'; e.lungeT=Math.ceil(e.lungeLen/LUNGE_SPEED); e.lungeTrail=0; }
      break;
    }
    case 'lunge':{
      e.x+=e.lungeDx*LUNGE_SPEED*sm; e.y+=e.lungeDy*LUNGE_SPEED*sm;
      e.lungeT--;
      /* Two lungers on the same line meet mid-charge and both come off worse. [h:30-enemies-16] */
      for(const o of room.enemies){
        if(o===e) continue;
        const ox=o.x-e.x, oy=o.y-e.y, d=Math.hypot(ox,oy);
        if(d>e.r+o.r) continue;
        const nx=d>0.01?ox/d:Math.cos(e.flank), ny=d>0.01?oy/d:Math.sin(e.flank);
        knockEnemy(e,-nx,-ny,LUNGE_CLASH*KNOCK_GAIN);
        knockEnemy(o, nx, ny, LUNGE_CLASH*KNOCK_GAIN*0.85);
        e.lungeState='recover'; e.lungeT=LUNGE_RECOVER;
        o.lungeCd=Math.max(o.lungeCd||0,LUNGE_CD);
        burstFX.push({x:(e.x+o.x)/2,y:(e.y+o.y)/2,life:sec(0.22),r:26,color:'#ffb37a'});
        break;
      }
      // the same puff trail a blink leaves, shorter and dimmer, so the lunge reads as a small blink
      if(++e.lungeTrail>=2){ e.lungeTrail=0; dashFX.push({x:e.x,y:e.y,life:LUNGE_TRAIL}); }
      if(e.lungeT<=0){ e.lungeState='recover'; e.lungeT=LUNGE_RECOVER; }
      break;
    }
    case 'recover':{
      // committed to having missed, and facing where it went. this is the window the player earns
      e.curSpeed+=((e.walkSpeed*0.5*sm)-e.curSpeed)*LUNGER_ACCEL*4;
      e.x+=e.lungeDx*e.curSpeed; e.y+=e.lungeDy*e.curSpeed;
      if(--e.lungeT<=0){ e.lungeState='approach'; e.lungeCd=LUNGE_CD; }
      break;
    }
  }
  // a lunge that runs into a wall is over, not a lunger that grinds along it. without this a lunger
  // cornered against the left wall teleports through it on the far side
  if(e.lungeState==='lunge'){
    const cx=Math.max(ROOM_LEFT,Math.min(ROOM_RIGHT,e.x)), cy=Math.max(ROOM_TOP,Math.min(ROOM_BOTTOM,e.y));
    if(cx!==e.x||cy!==e.y){ e.x=cx; e.y=cy; e.lungeState='recover'; e.lungeT=LUNGE_RECOVER; }
  }
}
function idleWander(e){  e.idleTimer--;
  if(e.idleTimer<=0){e.idleDir=[Rnd.jitter()-0.5,Rnd.jitter()-0.5];e.idleTimer=WANDER_TICKS/2+((Rnd.jitter()*WANDER_TICKS/2)|0);}
  const len=Math.hypot(e.idleDir[0],e.idleDir[1])||1;
  e.x+=e.idleDir[0]/len*WANDER_SPEED; e.y+=e.idleDir[1]/len*WANDER_SPEED;
}

// n distinct weapons, none of them the one being held
function pickWeapons(n,exclude){
  return shuffle(WEAPONS.map((_,i)=>i).filter(i=>i!==exclude)).slice(0,n);
}

function enterRoom(nx,ny,fromDir){
  cur={x:nx,y:ny};
  /* THE CURRENT ROOM CHANGED, so the wall shorthand has to be re-synced before anything in this function or the tick reads a size belonging to the... [h:30-enemies-17] */
  syncRoomBounds();
  entryDir=fromDir;
  const r=rooms[key(nx,ny)];
  r.visited=true;
  clearTransient();
  // by the time it is first walked into, and a reward room must not turn out to have furniture in it
  if(!r.spawned){
    r.spawned=true;
    if(r.type==='normal') spawnWave(r,fromDir);
    else if(r.type==='boss') r.enemies.push(spawnEnemy(true,r));
    else if(r.type==='secret') r.pickups.push({x:MIDX,y:MIDY,r:16,kind:'hook'});
    else if(r.type==='item'){
      const [w1,w2]=pickWeapons(2,player.weaponIdx);
      r.pickups.push({x:MIDX-255,y:MIDY,r:16,kind:'weapon',w:w1});
      r.pickups.push({x:MIDX+255,y:MIDY,r:16,kind:'weapon',w:w2});
      /* THE ITEM HALF OF THE LOOT POOL, which did not exist until now. [h:30-enemies-18] */
      const held=Content.all('item').filter(id=>Items.equipped(id));
      let ids=Items.pool(2,Items.rollRarity(Stats.value('luck')),held);
      if(ids.length<2) ids=ids.concat(Items.pool(2-ids.length,null,held));
      ids.forEach((id,i)=>r.pickups.push({x:MIDX+(i?-85:85),y:MIDY,r:16,kind:'item',id}));
    }
  }
  if(fromDir==='N'){player.x=MIDX;player.y=ROOM_TOP+34;}
  else if(fromDir==='S'){player.x=MIDX;player.y=ROOM_BOTTOM-34;}
  else if(fromDir==='E'){player.x=ROOM_RIGHT-34;player.y=MIDY;}
  else {player.x=ROOM_LEFT+34;player.y=MIDY;}
  player.vx=player.vy=player.kvx=player.kvy=0;
  player.lagX=player.x; player.lagY=player.y;
  const uncleared=r.enemies.length>0;
  /* Walking into a live room hands back everything you spent getting there, so the corridor between fights costs you nothing and a fight always opens... [h:30-enemies-19] */
  if(uncleared&&!r.armed){
    r.armed=true;
    const wp=WEAPONS[player.weaponIdx], altNow=activeAlt();

    /* THE BLINK BAR FILLS ACROSS THE ARRIVAL, and nothing else about the refill is animated. [h:30-enemies-20] */
    const heldBlink=player.blinkCharges+player.blinkRegen/BLINK_RECHARGE;
    player.cooldown=0; player.cooldownMax=wp.cooldown/TEMPO.rate;
    player.altCooldown=0; player.altCooldownMax=altNow.cooldown/TEMPO.rate;
    /* and the drawn value starts HERE rather than on the next tick, so the bar is already showing what the player walked in with during the frame the... [h:30-enemies-21] */
    player.blinkCharges=Math.min(2,Math.floor(heldBlink));
    player.blinkRegen=(heldBlink-player.blinkCharges)*BLINK_RECHARGE;
    player.blinkRestore={from:Math.max(0,Math.min(2,heldBlink)),t:RESTORE_FX_SPAN,span:RESTORE_FX_SPAN};
  }
  roomFade=1; fadeTicks=uncleared?READY:FADE_CLEAR; fadeT=fadeTicks; readyT=uncleared?READY:0;
}

