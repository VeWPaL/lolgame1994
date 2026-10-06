/* 40-combat - the player, the guns, and the hook The gunner intercept, the cast tell, the hold-still-while-charging rule, the hook's resistance... [h:40-combat-1] */
function clampPlayer(){
  const r=currentRoom();
  const inGapX=Math.abs(player.x-MIDX)<DOORW/2, inGapY=Math.abs(player.y-MIDY)<DOORW/2;
  if(player.y<ROOM_TOP+player.r && !(doorPassable(r,'N')&&inGapX)) player.y=ROOM_TOP+player.r;
  if(player.y>ROOM_BOTTOM-player.r && !(doorPassable(r,'S')&&inGapX)) player.y=ROOM_BOTTOM-player.r;
  if(player.x<ROOM_LEFT+player.r && !(doorPassable(r,'W')&&inGapY)) player.x=ROOM_LEFT+player.r;
  if(player.x>ROOM_RIGHT-player.r && !(doorPassable(r,'E')&&inGapY)) player.x=ROOM_RIGHT-player.r;
  player.x=Math.max(ROOM_LEFT-40,Math.min(ROOM_RIGHT+40,player.x));
  player.y=Math.max(ROOM_TOP-40,Math.min(ROOM_BOTTOM+40,player.y));
}

function checkDoorTransition(){
  const r=currentRoom();
  if(player.y<ROOM_TOP-18 && doorPassable(r,'N') && Math.abs(player.x-MIDX)<DOORW/2) return passDoor(r,'N');
  if(player.y>ROOM_BOTTOM+18 && doorPassable(r,'S') && Math.abs(player.x-MIDX)<DOORW/2) return passDoor(r,'S');
  if(player.x>ROOM_RIGHT+18 && doorPassable(r,'E') && Math.abs(player.y-MIDY)<DOORW/2) return passDoor(r,'E');
  if(player.x<ROOM_LEFT-18 && doorPassable(r,'W') && Math.abs(player.y-MIDY)<DOORW/2) return passDoor(r,'W');
}

function damagePlayer(amount,kx,ky,force){
  /* BLINK GRACE, and it is a SEPARATE window rather than more i-frames. [h:40-combat-2] */
  if(player.iframes>0) return false;
  if(player.blinkGrace>0&&!player.graceSpent){
    player.blinkGrace=0;
    player.graceSpent=true;
    // the hit is forgiven but the player still FEELS it: knockback stays, because being shoved by
    // a shell you did not take damage from is correct, and the momentum meter still takes it,
    // because the meter measures whether you are winning the trade and you did not.
    if(force){player.kvx+=kx*force;player.kvy+=ky*force;}
    Momentum.hit();
    /* No i-frames are granted here, and that is the load-bearing decision rather than an omission. [h:40-combat-3] */
    return false;
  }
  run.dmgTaken+=amount;
  /* SOUND, AT THE POINT THE HIT IS CONFIRMED. [h:40-combat-4] */
  Sfx.hurt();
  let rem=amount;
  if(player.armor>0){const used=Math.min(player.armor,rem);player.armor-=used;rem-=used;}
  if(rem>0) player.hp-=rem;
  /* Snap a float epsilon to exactly zero. [h:40-combat-5] */
  if(player.hp<0&&player.hp>-1e-6) player.hp=0;
  /* THE LAB CANNOT DIE, and it is enforced HERE rather than at the death checks. [h:40-combat-6] */
  if(state==='dev'&&player.hp<1) player.hp=1;
  // A hit costs most of the Momentum meter, not all of it. Unconditional here, including a hit that
  // armour absorbed entirely: the player lost the trade, and the meter measures whether you are
  // winning, not whether the health bar went down.
  Momentum.hit();
  if(force){player.kvx+=kx*force;player.kvy+=ky*force;}
  player.iframes=IFRAMES;
  return true;
}

// a landed hit drags the body down for a moment and wakes it up no matter where the player is:
// at this point in the game a shot is what tells a room where you are, and it is what makes
// sniping a far lunger viable now that it aggroes from anywhere
function alertEnemy(e){
  e.noticeTimer=0; e.alerted=true;
  if(e.aggroTimer!==undefined) e.aggroTimer=Math.max(e.aggroTimer,AGGRO_TIME);
}
function slowEnemy(e){e.slowT=HIT_SLOW_TICKS; e.pursuit=0;}
// full damage inside fNear, easing linearly to fMin at fFar. the blast has no fNear at all
/* How big a friendly projectile should be DRAWN, which for the shrinking guns means: [h:40-combat-7] */
function drawR(p){
  if(!p.shrink) return p.r;
  return BOLT_DRAW_R*(BOLT_SIZE_MAX+(BOLT_SIZE_MIN-BOLT_SIZE_MAX)*shrinkT(p));
}
/* How far through its own falloff a bolt is, 0 at the muzzle and 1 past fFar. falloffMult is
   1-(1-fMin)*min(1,t) over exactly this t, so mapping t onto the size band makes the picture a
   fixed multiple of the damage curve rather than merely a similar-looking one. */
function shrinkT(p){
  return Math.max(0,Math.min(1,(Math.hypot(p.x-p.ox,p.y-p.oy)-p.fNear)/Math.max(1,p.fFar-p.fNear)));
}
/* How bright the halo is, 1 at the muzzle down to 0.55 at the far end. [h:40-combat-8] */
const BOLT_FADE_MIN=0.55;
function drawFade(p){ return p.shrink?1-(1-BOLT_FADE_MIN)*shrinkT(p):1; }
/* the same falloff the damage uses */
function falloffMult(p){
  if(p.fNear===undefined) return 1;
  const t=Math.max(0,Math.hypot(p.x-p.ox,p.y-p.oy)-p.fNear)/Math.max(1,p.fFar-p.fNear);
  return 1-(1-p.fMin)*Math.min(1,t);
}

function knockEnemy(e,dx,dy,force){
  const len=Math.hypot(dx,dy);
  if(len<1){const a=Rnd.jitter()*6.283;dx=Math.cos(a);dy=Math.sin(a);}
  else{dx/=len;dy/=len;}
  e.kvx+=dx*force/e.mass; e.kvy+=dy*force/e.mass;
  const sp=Math.hypot(e.kvx,e.kvy);
  if(sp>KNOCK_MAX){e.kvx*=KNOCK_MAX/sp;e.kvy*=KNOCK_MAX/sp;}   // a hit shoves, it never launches
  e.stun=Math.max(e.stun,KNOCK_STUN); e.noticeTimer=0;
}
/* Does this angle reach the player without crossing a body of ours on the way? [h:40-combat-9] */
function clearShot(room,e,want){
  const tx=player.x, ty=player.y;
  /* ITS OWN BODYGUARD IS NOT AN OBSTACLE. [h:40-combat-10] */
  const ownGuards=e.shieldGuardFor;
  const clearAt=a=>{
    const dx=Math.cos(a), dy=Math.sin(a);
    const reach=Math.hypot(tx-e.x,ty-e.y);
    for(const o of room.enemies){
      if(o===e||o===e.owner) continue;
      if(ownGuards&&ownGuards.includes(o)) continue;
      const ox=o.x-e.x, oy=o.y-e.y;
      const along=ox*dx+oy*dy;
      if(along<=0||along>=reach) continue;
      const off=Math.abs(ox*dy-oy*dx);
      if(off<o.r+e.pr) return false;
    }
    return true;
  };
  /* The sweep picks BETWEEN the line it was given and its neighbours. [h:40-combat-11] */
  /* THE SWEEP IS BOUNDED TIGHTLY, and it used to run to 60 degrees either side of the true aim. [h:40-combat-12] */
  for(const off of [0,0.1,-0.1,0.2,-0.2,0.3,-0.3,0.42,-0.42]){
    const a=want+off;
    if(clearAt(a)) return a;
  }
  return null;
}
function clampEnemy(e){
  const minX=ROOM_LEFT+e.r,maxX=ROOM_RIGHT-e.r,minY=ROOM_TOP+e.r,maxY=ROOM_BOTTOM-e.r;
  if(e.x<minX){e.x=minX;if(e.kvx<0)e.kvx=-e.kvx*KNOCK_BOUNCE;}
  else if(e.x>maxX){e.x=maxX;if(e.kvx>0)e.kvx=-e.kvx*KNOCK_BOUNCE;}
  if(e.y<minY){e.y=minY;if(e.kvy<0)e.kvy=-e.kvy*KNOCK_BOUNCE;}
  else if(e.y>maxY){e.y=maxY;if(e.kvy>0)e.kvy=-e.kvy*KNOCK_BOUNCE;}
}
/* THE SEPARATION PASS, and the grid under it. [h:40-combat-13] */
const SEP_CELL=96;   // comfortably wider than the largest sum of two body radii
const sepBuckets=new Map(), sepScratch=[];
function separateBodies(list){
  const n=list.length;
  // below this the grid costs more to build than the pairs it saves, and the double loop is clearer
  if(n<8){
    for(let a=0;a<n;a++)for(let b=a+1;b<n;b++) bounceEnemies(list[a],list[b]);
    return;
  }
  sepBuckets.clear();
  for(let i=0;i<n;i++){
    const e=list[i], k=Math.floor(e.x/SEP_CELL)+','+Math.floor(e.y/SEP_CELL);
    let arr=sepBuckets.get(k);
    if(!arr){arr=[];sepBuckets.set(k,arr);}
    arr.push(i);
  }
  for(let a=0;a<n;a++){
    const e=list[a], cx=Math.floor(e.x/SEP_CELL), cy=Math.floor(e.y/SEP_CELL);
    /* THE CANDIDATES ARE SORTED BACK INTO INDEX ORDER, and this is the whole correctness of the change - the first version did not, and it was caught by... [h:40-combat-14] */
    let cnt=0;
    for(let ox=-1;ox<=1;ox++){
      for(let oy=-1;oy<=1;oy++){
        const arr=sepBuckets.get((cx+ox)+','+(cy+oy));
        if(!arr) continue;
        for(let k=0;k<arr.length;k++){ const b=arr[k]; if(b>a) sepScratch[cnt++]=b; }
      }
    }
    if(cnt===0) continue;
    const slice=sepScratch.slice(0,cnt).sort((p,q)=>p-q);
    for(let k=0;k<slice.length;k++) bounceEnemies(e,list[slice[k]]);
  }
}
function bounceEnemies(a,b){
  const dx=b.x-a.x,dy=b.y-a.y,d=Math.hypot(dx,dy),min=a.r+b.r;
  if(d>=min) return;
  // overlapping bodies are always pushed apart (the heavier one moves less), otherwise lungers
  // converging on the player merge into one blob; knockback is only traded when one of them is flying
  let nx,ny;
  if(d<0.001){const ang=Rnd.jitter()*6.283;nx=Math.cos(ang);ny=Math.sin(ang);}
  else{nx=dx/d;ny=dy/d;}
  const tot=a.mass+b.mass,push=min-d;
  a.x-=nx*push*b.mass/tot; a.y-=ny*push*b.mass/tot;
  b.x+=nx*push*a.mass/tot; b.y+=ny*push*a.mass/tot;
  /* STUN IS A TICK COUNT, and a FRACTIONAL one corrupts it permanently. [h:40-combat-15] */
  if(Math.hypot(a.kvx,a.kvy)<KNOCK_TRADE&&Math.hypot(b.kvx,b.kvy)<KNOCK_TRADE) return;
  const rel=(a.kvx-b.kvx)*nx+(a.kvy-b.kvy)*ny;
  if(rel>0){
    const j=(1+KNOCK_BOUNCE)*rel/(1/a.mass+1/b.mass);
    a.kvx-=j*nx/a.mass; a.kvy-=j*ny/a.mass;
    b.kvx+=j*nx/b.mass; b.kvy+=j*ny/b.mass;
    const shove=Math.round(KNOCK_STUN/3);
    a.stun=Math.max(a.stun,shove); b.stun=Math.max(b.stun,shove);
  }
}
/* `mode.pool` is not per-enemy damage: [h:40-combat-16] */
const DISPERSE=2.3;
function explode(room,x,y,mode){
  const hit=[];
  for(const e of room.enemies) if(Math.hypot(x-e.x,y-e.y)<mode.aoeRadius+e.r) hit.push(e);
  const share=hit.length?mode.pool/Math.pow(hit.length,DISPERSE):0;
  for(let j=room.enemies.length-1;j>=0;j--){
    const e=room.enemies[j];
    if(hit.indexOf(e)<0) continue;
    const dist=Math.hypot(x-e.x,y-e.y), t=Math.min(1,dist/(mode.aoeRadius+e.r));
    // a body that took no damage must not flinch like it did: the hook drags, and that is all it does
    if(share) e.hp-=share*e.armour;
    if(share) e.hitFlash=HIT_FLASH;
    alertEnemy(e); slowEnemy(e);
    if(mode.pull){
      // the yank, aimed at the point, scaled by how far out the body was caught so it coasts
      // exactly that far, and by its own mass so everything caught arrives together
      knockEnemy(e,x-e.x,y-e.y,mode.pull*dist*HOOK_PULL_GAIN*e.mass);
      // ...and then they are held there. A drag that scatters the crowd and immediately lets it
      // walk back to you has bought nothing; the whole point is the seconds where the swarm is a
      // knot somewhere that is not on top of you.
      e.stun=Math.max(e.stun,mode.hold);
    }
    else{
      // the shove is strongest on a body it goes off in the middle of and fades hard to the rim
      const near=1-smooth(t);
      knockEnemy(e,e.x-x,e.y-y,mode.aoeKnock*(ALT_KNOCK_FAR+(ALT_KNOCK_NEAR-ALT_KNOCK_FAR)*near));
    }
    if(e.hp<=0) killEnemy(room,j);
  }
  /* the shockwave wears the weapon's own colour and travels the way that weapon actually acts: [h:40-combat-17] */
  burstFX.push({x,y,r:mode.aoeRadius,life:BURST_TICKS,color:mode.color,dir:mode.pull?-1:1});
  // the hook leaves its spell on the ground. the yank above is the opening move; this is the
  // position you actually fight around for the next couple of seconds
  if(mode.pull) hookFields.push({x,y,r:mode.aoeRadius,life:mode.field,max:mode.field,id:++hookFieldId});
  tryBreakSecret(room,x,y);
}
/* The ground spell. [h:40-combat-18] */
function tickFields(room){
  for(let i=hookFields.length-1;i>=0;i--){
    const f=hookFields[i];
    if(--f.life<=0){ hookFields.splice(i,1); continue; }
    for(let j=room.enemies.length-1;j>=0;j--){
      const e=room.enemies[j];
      const dx=f.x-e.x, dy=f.y-e.y, d=Math.hypot(dx,dy);
      if(d>f.r+e.r) continue;
      /* A body is charged ONCE per field, not once per tick - a field lives nearly two seconds and runs every tick, so counting ticks would rack up a... [h:40-combat-19] */
      if(e.hookMark!==f.id){
        e.hookMark=f.id;
        // the resistance is read from the number of hooks this body has ALREADY taken, so the first
        // one is full. Reading it from the count after incrementing made the very first cast pay the
        // second-cast price, which quietly nerfed the weapon rather than fixing the loop.
        const n=e.hookStacks||0;
        e.hookPower=HOOK_RESIST[n]!==undefined?HOOK_RESIST[n]:HOOK_RESIST[HOOK_RESIST.length-1];
        e.hookStacks=Math.min(HOOK_RESIST.length-1,n+1);
        e.hookCalm=0;
      }
      const power=e.hookPower;
      e.stun=Math.max(e.stun,3*power);
      if(d>1){ e.x+=dx/d*HOOK_SUCK*power; e.y+=dy/d*HOOK_SUCK*power; }
      e.hp-=HOOK_DPS*power/TICK_HZ*e.armour;   // HOOK_DPS is per second; this runs every tick
      // the flash is scaled with the rest of it, so a body that is fighting the hook visibly is
      // fighting it. Otherwise the only sign is that it stopped working, which reads as a bug.
      e.hitFlash=Math.max(e.hitFlash,power);
      alertEnemy(e);
      if(e.hp<=0) killEnemy(room,j);
    }
  }
}
/* Forgetting. A body counts up to HOOK_FORGET outside a field and drops one resistance when it gets
   there, so a hook you used once on a body you then walked away from is worth full value when you
   come back - and a body you are standing on top of, re-hooking, is not. */
function tickHookResist(room){
  for(const e of room.enemies){
    if(e.hookMark===undefined) continue;
    if(e.hookStacks<=0) continue;
    if(++e.hookCalm>=HOOK_FORGET){ e.hookStacks--; e.hookCalm=0; }
  }
}
function activeAlt(){ return player.altMode==='hook'?HOOK_WEAPON:ALT_WEAPON; }

function fireWeapon(){
  const w=WEAPONS[player.weaponIdx];
  // the cursor, in the frame the player is in - see mouseWorld. This used to subtract the player's
  // world position straight out of the screen position, which is a vector between two frames
  const aim=mouseWorld();
  const ang0=Math.atan2(aim.y-player.y,aim.x-player.x);
  // A weapon whose cone is a STAT reads it here, once, at the moment of firing - not at spawn, and
  // not cached on the weapon, or picking up a Steady Hand would not narrow the beam until the wand
  // was swapped.
  const spread=w.spreadFromPrecision?preciseSpread(w.spread):w.spread;
  /* STRENGTH, added to the weapon's own number rather than multiplied into it, and read HERE at the moment of firing for the same reason the cone is: [h:40-combat-20] */
  /* STRENGTH IS ADDED ONCE PER SHOT, NOT ONCE PER PELLET, and that distinction is the whole reason the Scatter was not merely strong but WRONG. [h:40-combat-21] */
  const shotDmg=w.dmg*w.count+Stats.value('strength'), dmg=shotDmg/w.count;
  for(let i=0;i<w.count;i++){
    /* BUCKSHOT, for a weapon with more than one pellet. [h:40-combat-22] */
    let sx=player.x, sy=player.y, a=ang0, spd=w.speed;
    if(w.muzzleJitter!==undefined){
      sx+=(Rnd.jitter()-0.5)*2*w.muzzleJitter;
      sy+=(Rnd.jitter()-0.5)*2*w.muzzleJitter;
      a=ang0+(Rnd.jitter()-0.5)*2*w.pelletAngle;
      spd=w.speed*(1+(Rnd.jitter()-0.5)*2*w.pelletSpeedVar);
    } else {
      a=ang0+(w.count>1?(i-(w.count-1)/2)*spread:(Rnd.jitter()-0.5)*2*spread);
    }
    projectiles.push({x:sx,y:sy,vx:Math.cos(a)*spd,vy:Math.sin(a)*spd,r:w.r||5,dmg:dmg,friendly:true,color:w.color,
      ox:sx,oy:sy,fNear:w.fNear,fFar:w.fFar,fMin:w.fMin,shrink:!!w.shrink,
      /* `from` is the OWNER of the shot, as a value both directions can carry. [h:40-combat-23] */
      from:'player',owner:null,

      pierce:w.pierce||0, scale:1, hit:null,
      // the unit direction of travel, captured at spawn. it is what "how far along the line is this
      // body" is measured against when a pierced bolt has to decide which of two overlapping bodies
      // it hit first, and it never changes, so a knot cannot reshuffle it as bodies push each other
      dx:Math.cos(a), dy:Math.sin(a)});
  }
  run.shots+=w.count;
  /* SOUND, AT THE ONE PLACE A SHOT IS COUNTED. [h:40-combat-24] */
  Sfx.shot();
  player.cooldown=w.cooldown/TEMPO.rate; player.cooldownMax=player.cooldown;
  player.muzzleTimer=MUZZLE_TICKS;
  player.shootSlow=Math.min(SHOOT_SLOW_MAX,player.shootSlow+SHOOT_SLOW_MAIN);
}
function fireAlt(){
  const m=activeAlt();
  /* A second right click while the HOOK is still in the air detonates it where it is, so you can pull early and off your own aim point - drop it on a... [h:40-combat-25] */
  const live=projectiles.findIndex(p=>p.alt&&p.mode.early);
  if(live>=0){
    const p=projectiles[live];
    if(p.age>=HOOK_EARLY_MIN){
      explode(currentRoom(),p.x,p.y,p.mode);
      projectiles.splice(live,1);
    }
    return;
  }
  if(player.altCooldown>0) return;   /* nothing in flight, and the cooldown is still running the ground spell lands where the cursor is, clamped to the room. [h:40-combat-26] */
  const aim=mouseWorld();
  const tx=Math.max(roomL(),Math.min(roomR(),aim.x)), ty=Math.max(roomT(),Math.min(roomB(),aim.y));
  const ang0=Math.atan2(ty-player.y,tx-player.x);
  // `speed` has to ride along with the bolt: the travel test compares the distance still to cover
  // against the per-tick step, and without it that comparison is against undefined and never true,
  // so the bolt sails straight through the point you aimed at and only ever goes off on a wall
  projectiles.push({x:player.x,y:player.y,vx:Math.cos(ang0)*m.speed,vy:Math.sin(ang0)*m.speed,speed:m.speed,r:m.r,friendly:true,color:m.color,
    // `from:'player'` for the same reason the wand's pellets carry it: the Brunch rule asks who fired
    // a shot, and a right-click bolt is the player's too. Without it the hook would be eaten by the
    // pack the player is trying to drag a line through.
    alt:true,phase:!!m.phase,mode:m,age:0,tx,ty,from:'player',owner:null});
  player.altCooldown=m.cooldown/TEMPO.rate; player.altCooldownMax=player.altCooldown;
  player.muzzleTimer=MUZZLE_TICKS;
  player.shootSlow=Math.min(SHOOT_SLOW_MAX,player.shootSlow+SHOOT_SLOW_ALT);
}
/* the blast only breaks a fake wall if it actually lands on it, and the hook breaks it the same
   way: both are right-click impacts, so either form of the right click opens the way in */
function tryBreakSecret(room,x,y){
  if(!room.secret) return false;
  const p=doorPoint(room.secret);
  if(Math.hypot(x-p[0],y-p[1])>ALT_WEAPON.aoeRadius*0.8) return false;
  const d=room.secret, [nx,ny]=neighbor(room.x,room.y,d);
  const sec=rooms[key(nx,ny)];
  room.secret=null; room.doors[d]=true;
  if(sec) sec.doors[OPP[d]]=true;
  secretFound=true; if(run) run.secret=true;
  burstFX.push({x:p[0],y:p[1],r:46,life:BURST_TICKS});
  return true;
}

