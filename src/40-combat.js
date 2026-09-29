/* ==============================================================================================
   40-combat  -  the player, the guns, and the hook

   The gunner intercept, the cast tell, the hold-still-while-charging rule, the hook's resistance
   curve, the blast pool. Pure simulation.

   Note the target of the gunner intercept: it aims at the player's HITBOX, ten pixels below the
   sprite origin, because playerHit tests that circle. A chaser must NOT use that target - it steers
   a body and would visibly drift low - but a projectile has no excuse for missing it.
   ============================================================================================== */
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
  if(player.iframes>0) return false;
  run.dmgTaken+=amount;
  let rem=amount;
  if(player.armor>0){const used=Math.min(player.armor,rem);player.armor-=used;rem-=used;}
  if(rem>0) player.hp-=rem;
  // Snap a float epsilon to exactly zero. Damage here is deliberately fractional - a shell does
  // 1.8, which is what makes a chaser take a satisfying number of hits - and repeated fractional
  // subtraction can land health on 6.66e-16 instead of 0. That value is greater than zero, so the
  // death check never fired, and the player was left standing on a health bar that read as empty.
  // Only the band just BELOW zero is snapped; anything at or above zero is left exactly as it is,
  // which is the whole point - a positive remainder is real remaining health and must survive.
  if(player.hp<0&&player.hp>-1e-6) player.hp=0;
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
// sniping a far chaser viable now that it aggroes from anywhere
function alertEnemy(e){
  e.noticeTimer=0; e.alerted=true;
  if(e.aggroTimer!==undefined) e.aggroTimer=Math.max(e.aggroTimer,AGGRO_TIME);
}
function slowEnemy(e){e.slowT=HIT_SLOW_TICKS; e.pursuit=0;}
// full damage inside fNear, easing linearly to fMin at fFar. the blast has no fNear at all
/* How big a friendly projectile should be DRAWN, which for the shrinking guns means: exactly as
   damaging as it is right now. Sizing the picture off falloffMult rather than off a second curve is
   the entire point - two curves would drift apart the first time either was retuned, and the player
   would be reading a lie. The collision radius stays fixed either way: the shot's accuracy is not a
   function of how much damage it happens to be carrying. */
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
/* How bright the halo is, 1 at the muzzle down to 0.55 at the far end. Size on its own is a weak
   tell - a small bright disc is still perfectly readable on a lit floor - so the tell is carried by
   two channels at once, which is what makes it something you notice while aiming rather than
   something you notice while dying. It is a separate function so the size and the fade cannot be
   tuned independently by accident and drift into saying different things. */
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
// Does this angle reach the player without crossing a body of ours on the way? Tests a few degrees
// either side of the aim and hands back the first clear one, so a gunner beside a Brunch pack shoots
// round it rather than through it. It only counts bodies actually between the muzzle and the target,
// which is why a pack the gunner is standing inside does not lock it up.
function clearShot(room,e,want){
  const tx=player.x, ty=player.y;
  const clearAt=a=>{
    const dx=Math.cos(a), dy=Math.sin(a);
    const reach=Math.hypot(tx-e.x,ty-e.y);
    for(const o of room.enemies){
      if(o===e||o===e.owner) continue;
      const ox=o.x-e.x, oy=o.y-e.y;
      const along=ox*dx+oy*dy;
      if(along<=0||along>=reach) continue;
      const off=Math.abs(ox*dy-oy*dx);
      if(off<o.r+e.pr) return false;
    }
    return true;
  };
  /* The sweep picks BETWEEN the line it was given and its neighbours. It does not nudge the line it
     was given, and it used to: the zero-offset case added its own random twenty milliradians on top
     of whatever spread the caller had already applied to that angle, so every unblocked shot was
     fired at twice the spread it was aimed with.

     That is not a rounding argument. The gunner's floor is two hundredths of a radian, and an
     intercept against a running player at close range is a three hundred and seventy pixel long
     shot - so the intended spread is about seven pixels and the doubled one about fifteen, which is
     the width of the player's hitbox. Straight lines were being missed by exactly the margin of the
     mistake, which is the least legible way for a mechanic to be broken: the shot goes past the
     player, the player never learns why, and no amount of walking in a line looks punished. */
  for(const off of [0,0.14,-0.14,0.3,-0.3,0.5,-0.5,0.75,-0.75,1.05,-1.05]){
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
function bounceEnemies(a,b){
  const dx=b.x-a.x,dy=b.y-a.y,d=Math.hypot(dx,dy),min=a.r+b.r;
  if(d>=min) return;
  // overlapping bodies are always pushed apart (the heavier one moves less), otherwise chasers
  // converging on the player merge into one blob; knockback is only traded when one of them is flying
  let nx,ny;
  if(d<0.001){const ang=Rnd.jitter()*6.283;nx=Math.cos(ang);ny=Math.sin(ang);}
  else{nx=dx/d;ny=dy/d;}
  const tot=a.mass+b.mass,push=min-d;
  a.x-=nx*push*b.mass/tot; a.y-=ny*push*b.mass/tot;
  b.x+=nx*push*a.mass/tot; b.y+=ny*push*a.mass/tot;
  if(Math.hypot(a.kvx,a.kvy)<KNOCK_TRADE&&Math.hypot(b.kvx,b.kvy)<KNOCK_TRADE) return;
  const rel=(a.kvx-b.kvx)*nx+(a.kvy-b.kvy)*ny;
  if(rel>0){
    const j=(1+KNOCK_BOUNCE)*rel/(1/a.mass+1/b.mass);
    a.kvx-=j*nx/a.mass; a.kvy-=j*ny/a.mass;
    b.kvx+=j*nx/b.mass; b.kvy+=j*ny/b.mass;
    a.stun=Math.max(a.stun,KNOCK_STUN/3); b.stun=Math.max(b.stun,KNOCK_STUN/3);
  }
}
/* `mode.pool` is not per-enemy damage: it is one budget shared out between everyone caught, so a
   lone chaser eats the whole thing and dies, while a clump of four each takes a quarter and the
   blast works as a repositioning tool instead. The shove is scaled by how close you put it, and the
   hook inverts it into a pull and carries no budget at all, so it only ever moves bodies.

   The share is pool / crowd^DISPERSE, not pool / crowd. A flat split means the blast deals its full
   budget no matter how many bodies it catches, which is exactly wrong for a swarm: the budget was
   sized to kill one armoured chaser, and a four-strong Brunch pack does not have enough total health
   to survive being handed a whole chaser's worth of damage. Raising the exponent makes each extra
   body cost more than a proportional slice, so the *total* the blast actually lands falls away as
   the crowd grows - which is what turns it from a pack-clearing button into a panic nudge you still
   have to follow up on.

   The exponent is set by the SMALLEST pack, because that is the case that has to fail. A Brunch
   group rolls 4-8, and at 1.85 a group of 4 took 2.89 against 2.7 health - it deleted the most
   common pack in the game outright, which is precisely the "one click clears it" outcome the whole
   mechanism exists to prevent, and it did it at the one size players meet most often. 2.30 puts a
   four-strong pack at 43% health, which reads as a wound rather than a wound and a corpse.

   The other end of the curve is a killshot and it has to clear the third Brunch with MARGIN, not
   just barely. At 2.40 a three-strong group took 2.689 against 2.7 health: the intent was "it dies"
   and the float said "it survives on 0.011", which is the same failure as a chaser sitting alive at
   3e-15 hp in an earlier build. Any exponent where a whole-body kill lands inside a percent of the
   body is a knife edge waiting for a different TOUGH value. 2.30 leaves an 11% margin at three and
   still halves a four-pack.

   Against a 2.7hp Brunch: 1 caught dies, 2 die, 3 die, 4 are left at 43%, 6 at 76%, 8 at 88%. So the
   blast kills what it catches when it catches a handful and only bruises it once there is an actual
   group, which is the line between "a weapon" and "a panic button". Against a lone 24.3hp chaser it
   is completely unchanged - a crowd of one is a crowd of one whatever the exponent says. */
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
  // the shockwave wears the weapon's own colour and travels the way that weapon actually acts: the
  // blast throws bodies outward so the ring expands, the hook drags them in so the ring collapses
  // inward. Before this it was a hardcoded orange expanding ring for both, so the one number the
  // player reads mid-fight - which way did that thing move things - was wrong for the hook.
  burstFX.push({x,y,r:mode.aoeRadius,life:BURST_TICKS,color:mode.color,dir:mode.pull?-1:1});
  // the hook leaves its spell on the ground. the yank above is the opening move; this is the
  // position you actually fight around for the next couple of seconds
  if(mode.pull) hookFields.push({x,y,r:mode.aoeRadius,life:mode.field,max:mode.field,id:++hookFieldId});
  tryBreakSecret(room,x,y);
}
/* The ground spell. Three things happen to anything inside it, every tick:
     - it is held, by refreshing its stun. A stun stops the walk entirely, which is the point: the
       bodies that fell in stay in, and that is the window you shoot through.
     - it is walked in, by moving it toward the centre directly rather than by shoving it. A stunned
       body does not integrate knockback at all, so a velocity-based suck would do nothing to the
       very bodies it is meant to drag. Position is the only lever that survives the stun.
     - it is ground down, slowly. Far too slowly to be a weapon, fast enough that a field left on a
       big body finishes what the wand started.
   Separation still runs between bodies while they are held, so they pack around the middle of the
   circle rather than all standing in the same pixel, which is also what stops the suck stacking. */
function tickFields(room){
  for(let i=hookFields.length-1;i>=0;i--){
    const f=hookFields[i];
    if(--f.life<=0){ hookFields.splice(i,1); continue; }
    for(let j=room.enemies.length-1;j>=0;j--){
      const e=room.enemies[j];
      const dx=f.x-e.x, dy=f.y-e.y, d=Math.hypot(dx,dy);
      if(d>f.r+e.r) continue;
      /* A body is charged ONCE per field, not once per tick - a field lives nearly two seconds and
         runs every tick, so counting ticks would rack up a hundred resistances on the first cast
         and the second hook would do nothing at all. The field carries an id and the body remembers
         the last one it was charged for. */
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
      e.hp-=HOOK_DPS*power/TICK_HZ*(e.armour||1);   // HOOK_DPS is per second; this runs every tick
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
  const ang0=Math.atan2(mouse.y-player.y,mouse.x-player.x);
  // A weapon whose cone is a STAT reads it here, once, at the moment of firing - not at spawn, and
  // not cached on the weapon, or picking up a Lucky item would not narrow the beam until the wand
  // was swapped.
  const spread=w.spreadFromLuck?luckSpread(w.spread):w.spread;
  for(let i=0;i<w.count;i++){
    const off=w.count>1?(i-(w.count-1)/2)*spread:(Rnd.jitter()-0.5)*2*spread;
    const a=ang0+off;
    projectiles.push({x:player.x,y:player.y,vx:Math.cos(a)*w.speed,vy:Math.sin(a)*w.speed,r:w.r||5,dmg:w.dmg,friendly:true,color:w.color,
      ox:player.x,oy:player.y,fNear:w.fNear,fFar:w.fFar,fMin:w.fMin,shrink:!!w.shrink,
      // `pierce` is the number of EXTRA bodies this shot may pass through, so 3 means four bodies in
      // a line. `scale` starts at 1 and is multiplied by PIERCE_FALLOFF on every body after the
      // first. `hit` remembers which bodies it has already counted, or a bolt that is sitting
      // inside a Brunch takes the same two pixels of HP off it over and over and never advances.
      pierce:w.pierce||0, scale:1, hit:null,
      // the unit direction of travel, captured at spawn. it is what "how far along the line is this
      // body" is measured against when a pierced bolt has to decide which of two overlapping bodies
      // it hit first, and it never changes, so a knot cannot reshuffle it as bodies push each other
      dx:Math.cos(a), dy:Math.sin(a)});
  }
  run.shots+=w.count;
  player.cooldown=w.cooldown/TEMPO.rate; player.cooldownMax=player.cooldown;
  player.muzzleTimer=MUZZLE_TICKS;
  player.shootSlow=Math.min(SHOOT_SLOW_MAX,player.shootSlow+SHOOT_SLOW_MAIN);
}
function fireAlt(){
  const m=activeAlt();
  // A second right click while the HOOK is still in the air detonates it where it is, so you can
  // pull early and off your own aim point - drop it on a pack that is closing while you are still
  // being repositioned. Only the hook: the blast is left exactly as it was.
  //
  // While one is in flight the input is consumed either way and never becomes a second bolt, which
  // is what stops a held button from stacking casts. Past the age floor it detonates; before it the
  // click is simply eaten, which is also what stops the cast and the cancel being the same input
  // and blowing the hook up at your own feet a tick after you threw it. Nothing here touches the
  // cooldown, so it cannot be farmed - it is already running from the shot that got it airborne.
  //
  // The cooldown gates CASTING only, and that distinction is the whole reason the cancel works.
  // It used to gate the right click as well, and the hook's own cooldown (2.0s) is longer than a
  // long cast takes to arrive (up to ~1.9s) - so for most of the flight the button was on cooldown,
  // the handler was never reached, and the cancel silently did nothing. The feature was there and
  // unreachable, which is worse than not having it because there is nothing to notice.
  const live=projectiles.findIndex(p=>p.alt&&p.mode.early);
  if(live>=0){
    const p=projectiles[live];
    if(p.age>=HOOK_EARLY_MIN){
      explode(currentRoom(),p.x,p.y,p.mode);
      projectiles.splice(live,1);
    }
    return;
  }
  if(player.altCooldown>0) return;   // nothing in flight, and the cooldown is still running
  const tx=Math.max(ROOM_LEFT,Math.min(ROOM_RIGHT,mouse.x)), ty=Math.max(ROOM_TOP,Math.min(ROOM_BOTTOM,mouse.y));
  const ang0=Math.atan2(ty-player.y,tx-player.x);
  // `speed` has to ride along with the bolt: the travel test compares the distance still to cover
  // against the per-tick step, and without it that comparison is against undefined and never true,
  // so the bolt sails straight through the point you aimed at and only ever goes off on a wall
  projectiles.push({x:player.x,y:player.y,vx:Math.cos(ang0)*m.speed,vy:Math.sin(ang0)*m.speed,speed:m.speed,r:m.r,friendly:true,color:m.color,
    alt:true,phase:!!m.phase,mode:m,age:0,tx,ty});
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

