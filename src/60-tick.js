/* 60-tick - update(), 552 lines, the keystone of the port The one function I would not translate mechanically. [h:60-tick-1] */
/* Momentum is charged on MOVEMENT UNDER PRESSURE, and the distinction is the whole mechanic: [h:60-tick-2] */
/* A shot whose cast has finished LEAVES, stunned or not. [h:60-tick-3] */
function fireCommittedShot(e,roomPress){
  if(e.castT>0){ if(--e.castT<=0) e.castReady=true; return; }
  if(!e.castReady) return;
  e.castReady=false;
  e.shootCd=(e.cdMin+Rnd.jitter()*e.cdVar)*(1-PRESSURE_CADENCE*roomPress);
  projectiles.push({x:e.x,y:e.y,vx:Math.cos(e.castAim)*e.pspd,vy:Math.sin(e.castAim)*e.pspd,r:e.pr,
    dmg:e.dmg,friendly:false,color:e.pcol,owner:e,heavy:e.type==='gunner',from:'enemy'});
}

function tickMomentum(moved){
  if(momentumLocked) return;
  if(currentRoom().enemies.length===0) return;
  player.momentum=moved>MOMENTUM_MOVE_FLOOR
    ? Math.min(1,player.momentum+MOMENTUM_GAIN*moved)
    : Math.max(0,player.momentum-MOMENTUM_STALL_DECAY);
  Stats.earn('momentum',player.momentum);
}

/* THE WARDEN: the boss, as a composition of the vocabulary the rest of the game already speaks. [h:60-tick-4] */

function bossInit(e){
  Sfx.boss();   // the phase change, not an event: the one sound allowed to run long
  e.phase=1;
  e.move='idle';
  e.moveT=0;
  e.bossCd=sec(1.2);
  e.volleyLeft=0;
  e.volleyT=0;
  e.sweepAim=0;
  e.sweepFrom=null;
  /* `e.wallIds` used to live here and was written and never read: [h:60-tick-5] */
  e.wallT=0;
  // the ranged kit it was missing. These are the gunner's numbers with a boss behind them, and the
  // aim is solved at the moment the tell starts rather than when the shell leaves, exactly as it is
  // for a gunner - so a player who commits to a dodge is dodging the shot that will arrive.
  e.range=620; e.sense=900; e.close=140; e.far=260;
  e.cdMin=BOSS_CD_MIN; e.cdVar=BOSS_CD_VAR;
  e.dmg=BOSS_SHELL_DMG; e.pspd=1.9; e.pr=8; e.pcol='#ff9a5a';
}

/* The wall the boss calls. [h:60-tick-6] */
function bossCallWall(e,room){
  const n=BOSS_WALL_HP;
  const base={x:clampX(e.x+(e.x<player.x?70:-70)),y:e.y};
  const made=[];
  for(let i=0;i<n;i++){
    const a=(i/n)*6.283, rad=i%2?30:16;
    const b=spawnEnemy(false,room,base.x+Math.cos(a)*rad,base.y+Math.sin(a)*rad,'brunch');
    b.hp=b.maxHp=BOSS_WALL_HP*2;
    b.packId=BOSS_WALL_ID; b.packSlot=i;
    b.noticeTimer=1e9; b.aggroTimer=0; b.pursuit=0;
    /* PUT IT IN THE ROOM. [h:60-tick-7] */
    room.enemies.push(b);
    made.push(b);
  }
  // the BODIES, not the slot numbers - the slot is a formation index and reading it back as an
  // identity gives five copies of a number and no way to find the wall again
  e.wallBodies=made;
  e.wallT=0;
}

function clampX(x){
  return Math.max(ROOM_LEFT+60,Math.min(ROOM_RIGHT-60,x));
}


/* The boss's turn. [h:60-tick-8] */
function stepBoss(e,edx,edy,dist,sm,room){
  const hx=player.x, hy=player.y;
  if(e.moveT>0) e.moveT--;

  /* the wall it called expires on its own, and it is the only boss resource that runs without the player doing anything - a wall that never went away... [h:60-tick-9] */
  /* SWEEP BY AGE, ALWAYS - not only when `wallBodies` is null. [h:60-tick-10] */
  for(let i=room.enemies.length-1;i>=0;i--){
    const b=room.enemies[i];
    if(b.packId!==BOSS_WALL_ID) continue;
    if(b.wallAge===undefined) b.wallAge=0; else b.wallAge++;
    if(b.wallAge>sec(14)) room.enemies.splice(i,1);
  }
  if(e.wallBodies){
    e.wallT++;
    if(e.wallT>sec(14)||!e.wallBodies.some(b=>b.hp>0)){
      for(const b of e.wallBodies){
        const i=room.enemies.indexOf(b);
        if(i>=0) room.enemies.splice(i,1);
      }
      e.wallBodies=null;
    }
  }

  // PHASE. Both thresholds fire on a transition only, so a boss that sits at 66.4% for ten seconds
  // does not announce itself ten times.
  const frac=e.hp/e.maxHp;
  if(e.phase===1&&frac<=BOSS_PHASE_1) bossPhase(e,2);
  else if(e.phase===2&&frac<=BOSS_PHASE_2) bossPhase(e,3);

  if(e.move!=='idle'){ resolveBoss(e,edx,edy,dist,sm,room); return; }
  if(e.moveT>0) return;

  // choose. Weighted by phase rather than random, so a phase is a different FIGHT and not the same
  // one with the dice rolled differently - and so the mix can be read off the phase number.
  e.bossCd-=1;
  if(e.bossCd>0) return;
  const bag=[];
  bag.push('volley'); bag.push('volley');
  bag.push('sweep');
  if(e.phase>=2) bag.push('wall');
  /* THREE MORE MOVES, AND ALL THREE ARE PHASE 3's. [h:60-tick-11] */
  if(e.phase>=3){ bag.push('volley'); bag.push('sweep'); bag.push('sweep'); }
  const pick=bag[(Rnd.run()*bag.length)|0];
  e.bossCd=(e.cdMin+Rnd.jitter()*e.cdVar)*(1-PRESSURE_CADENCE*roomPressure(room.enemies.length));
  beginBoss(e,pick,room);
}

function bossPhase(e,n){
  e.phase=n;
  e.move='idle';
  e.moveT=BOSS_RECOVER;          // the phase change is itself a beat of recovery, not a free attack
  e.volleyLeft=0;
  e.bossCd=sec(0.4);
  e.phaseFlash=BOSS_PHASE_FLASH;
}

function beginBoss(e,move,room){
  e.move=move;
  /* Read the player position HERE rather than taking it as an argument. [h:60-tick-12] */
  const hx=player.x, hy=player.y;
  if(move==='volley'){
    /* THE TELL HAS TO EXIST BEFORE THE FIRST SHELL, and it did not. [h:60-tick-13] */
    e.castT=CAST_TIME; e.castReady=false; e.castAim=Math.atan2(hy-e.y,hx-e.x);
    e.volleyLeft=BOSS_VOLLEY_N;
    /* CAST_TIME+1, NOT 1. [h:60-tick-14] */
    e.volleyT=CAST_TIME+1;
    e.moveT=CAST_TIME+1;
  } else if(move==='sweep'){
    e.castAim=Math.atan2(hy-e.y,hx-e.x);
    e.sweepFrom={x:e.x,y:e.y};
    e.moveT=CAST_TIME;           // wind-up: the boss stops, and a stopped body is a readable one
  } else if(move==='wall'){
    bossCallWall(e,room);
    e.moveT=BOSS_RECOVER;
  }
}

function resolveBoss(e,edx,edy,dist,sm,room){
  const hx=player.x, hy=player.y;
  if(e.move==='volley'){
    if(e.volleyLeft>0){
      e.volleyT--;
      if(e.volleyT<=0){
        // one shell, re-aimed each time. Re-aiming between shells rather than firing all three along
        // one line is the difference between a volley and a wall the player steps around once.
        e.castAim=Math.atan2(hy-e.y,hx-e.x);
        projectiles.push({x:e.x,y:e.y,vx:Math.cos(e.castAim)*e.pspd,vy:Math.sin(e.castAim)*e.pspd,r:e.pr,
          dmg:e.dmg,friendly:false,color:e.pcol,owner:e,heavy:true,from:'enemy'});
        e.volleyLeft--;
        e.volleyT=BOSS_VOLLEY_GAP;
      }
      // The tell counts down to the next shell, so each one is answerable on its own and the flash
      // ends as it leaves. It used to be set to CAST_TIME and never drained: on for the whole fight.
      e.castT=e.volleyLeft>0?Math.min(CAST_TIME,e.volleyT):0;
    } else {
      e.move='idle'; e.moveT=BOSS_RECOVER; e.castT=0;
    }
  } else if(e.move==='sweep'){
    if(e.moveT>0) return;        // still winding up
    // the lunge, from a body that cannot be pushed out of its line by anything in the room
    if(!e.sweepDone){
      e.sweepDone=true;
      const a=e.castAim;
      for(let i=0;i<6;i++){
        dashFX.push({x:e.x-Math.cos(a)*i*9,y:e.y-Math.sin(a)*i*9,life:LUNGE_TRAIL});
      }
      e.x+=Math.cos(a)*BOSS_SWEEP_DIST; e.y+=Math.sin(a)*BOSS_SWEEP_DIST;
      clampEnemy(e);
      // it only hurts on the way THROUGH, not on arrival: a body that lands on you and then sits
      // there is a body you cannot get away from
      if(Math.hypot(e.x-hx,e.y-hy)<e.r+player.r)
        damagePlayer(e.dmg*1.4,Math.cos(a),Math.sin(a),3*KNOCK_P_GAIN);
    }
    e.move='idle'; e.moveT=BOSS_RECOVER; e.sweepDone=false;
  } else {
    e.move='idle';
  }
}


/* THE PLAYER'S LAGGED HITBOX, published once per tick by `tickPlayer` and read by `tickBodies`.
   See the note at the assignment for why it is module state and not a local. */
const hitbox={x:0,y:0};

function update(){
  frameCount++;
  tickFX();
  /* THE BLINK BAR'S ARRIVAL FILL, advanced here rather than down in tickBlink. [h:60-tick-15] */
  if(player&&player.blinkRestore&&player.blinkRestore.t>0){
    const br=player.blinkRestore;
    br.t--;
    const held=br.from+(2-br.from)*(1-br.t/br.span);   // charges, from what it was up to both
    const whole=Math.min(2,Math.floor(held));
    player.blinkCharges=whole;
    player.blinkRegen=(held-whole)*BLINK_RECHARGE;
    if(br.t<=0){ player.blinkCharges=2; player.blinkRegen=0; }
  }
  // roomFade is progress-driven, not "the complement of a smoothstep": smooth() is flat at BOTH
  // ends, so 1-smooth(fadeT/ticks) jumped to 0 on the first tick and the whole fade was a cut.
  // p runs 0 -> 1 across the fade, so 1-smooth(p) starts at full black and eases out of it.
  if(fadeT>0&&!trans){fadeT--; roomFade=1-smooth(1-fadeT/fadeTicks);}
  else if(!trans) roomFade=0;
  if(state!=='playing'&&state!=='dev') return;
  /* THE DEATH BACKSTOP. [h:60-tick-16] */
  if(player.hp<=0){ endRun(false); return; }
  /* The backstop. [h:60-tick-17] */
  if(player.hp<=0){ endRun(false); return; }
  run.ticks++;
  /* The per-floor clock, alongside the run clock and for the same reason: [h:60-tick-18] */
  run.floorTicks++;
  // The descent banner ages on the same clock as the fade it is drawn on, for the same reason the
  // grace ages on its own: two numbers for one duration drift apart, and the first symptom is a
  // banner still fading in over a room the player is already being shot at.
  if(descendT>0) descendT--;
  /* THE LAB'S OWN TICK, and it is here - after the run clocks, before the transition early-returns - for two reasons. [h:60-tick-19] */
  if(state==='dev'){ Lab.remember(); Lab.freeze(); }
  if(trans){
    trans.t++; roomFade=smooth(Math.min(1,trans.t/FADE_OUT));
    if(trans.t>=FADE_OUT){const t=trans;trans=null;enterRoom(t.nx,t.ny,t.from);}
    return;
  }
  if(readyT>0){readyT--;return;}
  if(bossWarnT>0)bossWarnT--;
  // a key in hand is not a key spent: stand at the door and the lock works, and walking off it
  // leaves the door exactly as shut as it was

  /* THE FOUR PHASES ============================ Order is measured, not chosen: [h:60-tick-20] */
  if(tickPlayer()===false) return;
  tickProjectiles();
  tickBodies();
  tickRoom();
}

/* THE SIMULATION PHASES ====================== Split out of a single 1,212-line `update()`. [h:60-tick-21] */

/* ---- the player: input, momentum, blink, firing, and the transition abort ---- */
function tickPlayer(){
  tickUnlock();
  // warn once, the first time the boss becomes visible on the map. bossFront is the set of rooms
  // with a door onto the boss, built once by generateDungeon, so this is a lookup rather than a
  // sweep of the whole dungeon - and it is only consulted while the warning is still unspent
  if(!bossWarned){
    for(const k in bossFront){
      const n=rooms[k];
      if(n&&n.visited){ bossWarned=true; bossWarnT=BOSS_WARN_TIME; break; }
    }
  }

  let dx=0,dy=0;
  if(keys['w']||keys['arrowup'])dy-=1;
  if(keys['s']||keys['arrowdown'])dy+=1;
  if(keys['a']||keys['arrowleft'])dx-=1;
  if(keys['d']||keys['arrowright'])dx+=1;
  const len=Math.hypot(dx,dy)||0;
  player.slowMult+=((1-player.shootSlow)-player.slowMult)*SLOW_EASE;
  /* The landing burst, decaying. [h:60-tick-22] */
  if(player.boost>0)player.boost--;
  const align=len?(dx/len)*player.boostX+(dy/len)*player.boostY:0;
  // top speed comes from the DERIVED stat, never from an item having multiplied player.speed in
  // place. The base constant is untouched, so removing an item removes exactly its contribution.
  const spd=player.speed*player.slowMult*(1+moveSpeedBonus())*(player.boost>0?1+(BLINK_BOOST_GAIN-1)*Math.max(0,align):1);
  const targetVx=len?(dx/len)*spd:0, targetVy=len?(dy/len)*spd:0;
  /* acceleration, not speed, is where the Momentum reward lives. [h:60-tick-23] */
  const accel=MOVE_ACCEL*(1+Momentum.level()*MOMENTUM_ACCEL);
  player.vx+=(targetVx-player.vx)*accel;
  player.vy+=(targetVy-player.vy)*accel;
  /* The heading the player has been HOLDING, as opposed to the heading they are on this tick. [h:60-tick-24] */
  player.trendVx+=(player.vx-player.trendVx)*LUNGE_TRACK;
  player.trendVy+=(player.vy-player.trendVy)*LUNGE_TRACK;

  /* WHICH WAY THE LUNGERS THINK YOU ARE GOING, which is a different question from how fast you are moving and is maintained HERE rather than inside... [h:60-tick-25] */
  const bsp=Math.hypot(player.vx,player.vy);
  if(bsp>PLAYER_SPEED_EPS){
    player.beliefVx+=(player.vx/bsp-player.beliefVx)*LUNGE_BELIEF_TRACK;
    player.beliefVy+=(player.vy/bsp-player.beliefVy)*LUNGE_BELIEF_TRACK;
    const bl=Math.hypot(player.beliefVx,player.beliefVy);
    if(bl>1e-4){ player.beliefVx/=bl; player.beliefVy/=bl; }
  }
  /* SWERVE: how unsettled your movement is right now, 0 while you hold a heading and 1 while you are reversing every few ticks. [h:60-tick-26] */
  const msp=Math.hypot(targetVx,targetVy);
  if(msp>0.05){
    if(player.dirX||player.dirY){
      const align=(targetVx*player.dirX+targetVy*player.dirY)/msp;
      if(align<0.35) player.swerve=Math.min(1,player.swerve+SWERVE_GAIN*(1-align));
    }
    player.dirX=targetVx/msp; player.dirY=targetVy/msp;
  }
  player.swerve=Math.max(0,player.swerve-SWERVE_DECAY);
  // where the body was before the move, and how much of what moves it is the enemy's doing rather
  // than the player's. See tickMomentum - charging on velocity instead of on displacement is the bug
  // this exists to prevent.
  const wasX=player.x, wasY=player.y, knk=Math.hypot(player.kvx,player.kvy);
  player.x+=player.vx+player.kvx; player.y+=player.vy+player.kvy;
  player.kvx*=KNOCK_P_FRICTION; player.kvy*=KNOCK_P_FRICTION;
  if(Math.abs(player.kvx)<KNOCK_CUT)player.kvx=0;
  if(Math.abs(player.kvy)<KNOCK_CUT)player.kvy=0;
  clampPlayer();
  checkDoorTransition();
  if(trans) return false;   // a transition aborts the REST OF THE TICK - see the note below

  const sp=Math.hypot(player.vx,player.vy);
  /* VELOCITY THAT POINTS INTO A WALL IS NOT VELOCITY, and the enemies were reading it as if it were. [h:60-tick-27] */
  {
    const r=currentRoom();
    const inGapX=Math.abs(player.x-MIDX)<DOORW/2, inGapY=Math.abs(player.y-MIDY)<DOORW/2;
    if(player.y<=ROOM_TOP+player.r && !(doorPassable(r,'N')&&inGapX)) player.vy=0;
    if(player.y>=ROOM_BOTTOM-player.r && !(doorPassable(r,'S')&&inGapX)) player.vy=0;
    if(player.x<=ROOM_LEFT+player.r && !(doorPassable(r,'W')&&inGapY)) player.vx=0;
    if(player.x>=ROOM_RIGHT-player.r && !(doorPassable(r,'E')&&inGapY)) player.vx=0;
  }
  /* Displacement under the player's OWN power: [h:60-tick-28] */
  tickMomentum(Math.max(0,Math.hypot(player.x-wasX,player.y-wasY)-knk));
  player.anim=sp>0.12?player.anim+sp/STRIDE:0;
  tickBlink();

  if(player.cooldown>0)player.cooldown--;
  if(player.altCooldown>0)player.altCooldown--;
  tickFields(currentRoom());
  tickHookResist(currentRoom());
  if(player.iframes>0)player.iframes--;
  /* The blink grace ages on its own clock, deliberately NOT folded into iframes. [h:60-tick-29] */
  if(player.blinkGrace>0)player.blinkGrace--;
  if(player.muzzleTimer>0)player.muzzleTimer--;
  if(player.shootSlow>0)player.shootSlow=Math.max(0,player.shootSlow-SHOOT_SLOW_RECOVER);
  if(mouseDown && player.cooldown<=0) fireWeapon();
  // deliberately NOT gated on the alt cooldown: the handler decides whether a click is a cancel or
  // a cast, and it can only do that if it is actually reached. See fireAlt.
  if(altMouseDown) fireAlt();

  // the hitbox the enemies use trails the real position after a blink, then catches up
  player.lagX+=(player.x-player.lagX)*HITBOX_LAG_EASE;
  player.lagY+=(player.y-player.lagY)*HITBOX_LAG_EASE;
  /* PUBLISH THE LAGGED HITBOX. [h:60-tick-30] */
  hitbox.x=player.lagX; hitbox.y=player.lagY;
  /* THE LAGGED HITBOX THE GUNNERS AIM AT IS HANDED ON THROUGH MODULE STATE, not through a local. [h:60-tick-31] */
  hx=player.lagX; hy=player.lagY;
}

/* ---- projectiles: movement, collisions, the hook ---- */
function tickProjectiles(){
  /* `r` WAS A LOCAL OF update() AND EVERY PHASE READ IT. [h:60-tick-32] */
  const r=currentRoom();
  for(let i=projectiles.length-1;i>=0;i--){
    const p=projectiles[i];
    if(p.alt){
      /* Stop on the point, not past it. [h:60-tick-33] */
      if(Math.hypot(p.tx-p.x,p.ty-p.y)<=p.speed){
        p.x=p.tx; p.y=p.ty;
        // p.mode, NOT activeAlt(). The bolt is resolved from the weapon you THREW, not the one you
        // are holding when it lands - swap to the other right click while a hook is in the air and
        // the bolt used to detonate as a blast: a shove and a damage budget where a pull was owed.
        explode(r,p.tx,p.ty,p.mode);
        projectiles.splice(i,1);continue;
      }
    }
    p.age++;
    p.x+=p.vx;p.y+=p.vy;
    if(p.x<ROOM_LEFT-30||p.x>ROOM_RIGHT+30||p.y<ROOM_TOP-30||p.y>ROOM_BOTTOM+30){
      if(p.alt) explode(r,p.x,p.y,p.mode);   // it goes off on the mode it was cast with, not the one you hold now
      projectiles.splice(i,1);continue;
    }
    if(p.alt&&p.phase) continue;   // the hook flies through bodies and only acts at its point
    if(p.friendly){
      /* exactly one body per tick. [h:60-tick-34] */
      let best=-1, bestA=Infinity;
      for(let j=r.enemies.length-1;j>=0;j--){
        const e=r.enemies[j];
        if(p.hit&&p.hit.indexOf(e)>=0) continue;   // already counted this one; move on to the next
        if(Math.hypot(p.x-e.x,p.y-e.y)>=p.r+e.r) continue;
        /* Only a bolt that CARRIES ON needs to know which of two touching bodies it reached first. [h:60-tick-35] */
        if(!p.pierce){ best=j; break; }
        const a=(e.x-p.ox)*p.dx+(e.y-p.oy)*p.dy;
        if(a<bestA){ bestA=a; best=j; }
      }
      if(best>=0){
        const e=r.enemies[best];
        const j=best;
        if(p.alt){ explode(r,p.x,p.y,p.mode); projectiles.splice(i,1); continue; }   // the blast detonates on contact
        e.hp-=p.dmg*falloffMult(p)*e.armour*p.scale; e.hitFlash=HIT_FLASH; run.hits++;
        Sfx.hit(e);   // panned by where the body is, so a hit on the far side of the room says so
        alertEnemy(e); slowEnemy(e);
        if(e.hp<=0) killEnemy(r,j);
        if(p.pierce>0){
          // drill through and keep going. `p.hit` is what stops the bolt re-hitting the body it is
          // currently sitting inside, and the falloff is what makes lining the pack up the decision
          p.pierce--; p.scale*=PIERCE_FALLOFF; (p.hit||(p.hit=[])).push(e);
        } else {
          projectiles.splice(i,1);
          continue;   // not break: older projectiles still have to move this tick
        }
      }
    } else {
      let hitSomething=false;
      /* BRUNCH ARE COVER, and the cover has to be VISIBLE or it is a rule the player has to infer from damage numbers. [h:60-tick-36] */
      for(let j=r.enemies.length-1;j>=0;j--){
        const b=r.enemies[j];
        if(b.type!=='brunch'||b.hp<=0) continue;
        /* A BRUNCH DOES NOT SWALLOW ITS OWN GUNNER'S FIRE. [h:60-tick-37] */
        if(p.owner&&b.shieldTarget===p.owner) continue;
        if(Math.hypot(p.x-b.x,p.y-b.y)<p.r+b.r){
          // the ring collapses inward rather than expanding, which is the read: something arrived
          // and was swallowed. An expanding burst would say the opposite.
          burstFX.push({x:p.x,y:p.y,r:BRUNCH_ABSORB_R,life:BURST_TICKS,color:p.color,dir:-1});
          dashFX.push({x:p.x,y:p.y,life:BRUNCH_ABSORB_PUFF});
          b.hitFlash=Math.max(b.hitFlash,BRUNCH_ABSORB_FLASH);
          hitSomething=true;
          break;
        }
      }
      if(!hitSomething){
        for(let j=r.enemies.length-1;j>=0;j--){
          const e2=r.enemies[j];
          if(e2===p.owner || e2.type!=='lunger') continue;
          if(Math.hypot(p.x-e2.x,p.y-e2.y)<p.r+e2.r){
            e2.hp-=1; e2.hitFlash=HIT_FLASH; alertEnemy(e2); slowEnemy(e2); hitSomething=true;
            if(e2.hp<=0) killEnemy(r,j);
            break;
          }
        }
      }
      if(!hitSomething && playerHit(p.x,p.y,p.r)){
        const pn=Math.hypot(p.vx,p.vy)||1;
        damagePlayer(p.dmg,p.vx/pn,p.vy/pn,1.5*KNOCK_P_GAIN);
        hitSomething=true;
      }
      if(hitSomething) projectiles.splice(i,1);
    }
  }

  /* Iterate a SNAPSHOT, not the live array. [h:60-tick-38] */
}

/* ---- bodies: packs, Brunch, gunners, separation ---- */
function tickBodies(){
  /* `r` WAS A LOCAL OF update() AND EVERY PHASE READ IT. [h:60-tick-39] */
  const r=currentRoom();
  const roomPress=roomPressure(r.enemies.reduce((n,x)=>n+(x.hp>0?1:0),0));
  /* PACK CENTROIDS, computed ONCE per tick and before the body loop. [h:60-tick-40] */
  const packC={};
  for(const b of r.enemies){
    if(b.type!=='brunch'||b.packId===undefined) continue;
    const c=packC[b.packId]||(packC[b.packId]={x:0,y:0,n:0});
    c.x+=b.x; c.y+=b.y; c.n++;
  }
  for(const k in packC){ const c=packC[k]; c.x/=c.n; c.y/=c.n; }

  /* THE PACK'S SHIELD TARGET, chosen once and committed to. [h:60-tick-41] */
  const RANGED={shooter:1,gunner:1,boss:1};
  /* THE COMMITMENT IS TO THE BODY, AND IT ENDS ONLY WHEN THE BODY DIES. [h:60-tick-42] */
  const pickShield=(c)=>{
    let best=null,bestD=Infinity;
    for(const o of r.enemies){
      if(o.hp<=0||!RANGED[o.type]) continue;
      const d=(o.x-c.x)*(o.x-c.x)+(o.y-c.y)*(o.y-c.y);
      if(d<bestD){ bestD=d; best=o; }
    }
    return best;
  };
  for(const e of r.enemies){
    if(e.type!=='brunch'||e.packId===undefined) continue;
    const c=packC[e.packId];
    if(!c) continue;
    /* THE SCAN RUNS EVERY TICK, NOT ONLY WHEN THE CURRENT TARGET DIES. [h:60-tick-43] */
    const tgt=e.shieldTarget;
    /* THE ONLY REASONS TO RE-PICK ARE THE TARGET'S DEATH AND THE PACK HAVING NO TARGET. [h:60-tick-44] */
    const stale=!tgt||tgt.hp<=0||!r.enemies.includes(tgt);
    if(stale||(!e.shieldTarget&&(frameCount%BRUNCH_SCAN_TICKS===0)))
      e.shieldTarget=pickShield(c);
  }
  /* THE REVERSE LINK, REBUILT EVERY TICK: [h:60-tick-45] */
  for(const o of r.enemies) if(RANGED[o.type]) o.shieldGuardFor=null;
  for(const e of r.enemies){
    if(e.type!=='brunch'||e.packId===undefined||!e.shieldTarget) continue;
    const g=e.shieldTarget.shieldGuardFor||(e.shieldTarget.shieldGuardFor=[]);
    g.push(e);
  }




  for(const e of r.enemies.slice()){
    if(e.hp<=0) continue;   // a body killed earlier in this same tick has already been removed
    if(e.hitFlash>0)e.hitFlash--;
    if(e.kvx||e.kvy){
      e.x+=e.kvx; e.y+=e.kvy; e.kvx*=KNOCK_FRICTION; e.kvy*=KNOCK_FRICTION;
      if(Math.abs(e.kvx)<KNOCK_CUT)e.kvx=0;
      if(Math.abs(e.kvy)<KNOCK_CUT)e.kvy=0;
    }
    /* `stun` AND `slowT` ARE TICK COUNTS, SO THEY ARE CLAMPED BESIDE THEIR DECREMENT - not inside the branch that decrements them. [h:60-tick-46] */
    if(e.slowT>0)e.slowT--;
    if(e.slowT<0)e.slowT=0;
    if(e.stun<0)e.stun=0;
    const sm=e.slowT>0?HIT_SLOW_MULT:1;
    if(e.stun>0){
      /* A stunned lunger is not a lunger mid-lunge, it is a lunger with its feet stuck. [h:60-tick-47] */
      if(e.lungeState==='wind'||e.lungeState==='lunge'){
        e.lungeState='approach'; e.lungeT=0; e.lungeLen=0;
        // and it pays for it: the cooldown starts now, so a lunger you caught mid-charge has to
        // come all the way back around before it tries again
        e.lungeCd=LUNGE_CD;
      }
      // A cast that has already finished still fires, or the tell the player was reading would be a
      // lie told by the game to the player. See fireCommittedShot.
      if(e.walkSpeed===undefined&&e.type!=='boss') fireCommittedShot(e,roomPress);
      e.stun--;
      e.anim=0;clampEnemy(e);continue;
    }
    if(e.noticeTimer>0){e.noticeTimer--;e.anim=0;continue;}
    const ox=e.x,oy=e.y;
    // the gunner sidesteps a shot that is actually on its way, but on a cooldown, so a fast enough
    // stream still lands and he is a nuisance rather than a wall
    if(e.type==='gunner'){
      if(e.dodgeCd>0)e.dodgeCd--;
      else{
        let threat=null;
        for(const p of projectiles){
          if(!p.friendly||p.owner===e) continue;
          const dx=e.x-p.x, dy=e.y-p.y;
          if(Math.hypot(dx,dy)<GUNNER_DODGE.sight && dx*p.vx+dy*p.vy>0){threat=p;break;}
        }
        if(threat&&Rnd.jitter()<GUNNER_DODGE.chance){
          const sp=Math.hypot(threat.vx,threat.vy)||1, side=Rnd.jitter()<0.5?1:-1;
          e.kvx+=-threat.vy/sp*side*GUNNER_DODGE.kick; e.kvy+=threat.vx/sp*side*GUNNER_DODGE.kick;
          e.dodgeCd=GUNNER_DODGE.cd;
        }
      }
    }
    // The offset hitbox is only used for the TOUCH test. Steering still aims at hx,hy: a body walks
    // at the player's position, not at the middle of their chest, and moving the seek target down ten
    // pixels would make every lunger in the room drift visibly low as it closed.
    const edx=hitbox.x-e.x,edy=hitbox.y-e.y,dist=Math.hypot(edx,edy)||1;
    /* THE BOSS IS CHECKED FIRST, and that ordering is load-bearing rather than stylistic. [h:60-tick-48] */
    if(e.type==='boss'){
      stepBoss(e,edx,edy,dist,sm,r);
    } else if(e.walkSpeed!==undefined){
      if(dist<aggroRange()) e.aggroTimer=AGGRO_TIME;
      else if(e.aggroTimer>0) e.aggroTimer--;
      if(e.aggroTimer>0){
        if(e.type==='lunger') stepLunge(e,edx/dist,edy/dist,dist,sm,r);
        else{
          // a Brunch pack announces itself and then arrives: the ramp is long enough that there is
          // a real interval in which to choose your ground before it is on you
          e.pursuit++;
          const gain=1+BRUNCH_RAMP_GAIN*Math.min(1,e.pursuit/BRUNCH_RAMP);
          e.curSpeed+=(e.runSpeed-e.curSpeed)*LUNGER_ACCEL*gain;
          /* THE WALL. A Brunch with a pack steers at a SLOT rather than at the player: [h:60-tick-49] */
          const pc=e.packId!==undefined?packC[e.packId]:null;
          let mdx=edx,mdy=edy;
          /* A PACK WITH A SHIELD TARGET FORMS THE ARC IN FRONT OF THAT TARGET, and this branch comes FIRST - it is the whole point of the change. [h:60-tick-50] */
          const tgt=e.shieldTarget;
          if(tgt&&tgt.hp>0&&pc&&pc.n>=BRUNCH_SHIELD_MIN){
            const slotPt=brunchArcSlot(tgt.x,tgt.y,player.x,player.y,pc.n,e.packSlot||0,tgt.r);
            if(slotPt){ mdx=slotPt.x-e.x; mdy=slotPt.y-e.y; }
          } else if(pc&&pc.n>=BRUNCH_WALL_MIN){
            /* NOTHING TO SHIELD, SO NOTHING TO ASSEMBLE. [h:60-tick-51] */
          }
          const md=Math.hypot(mdx,mdy);
          /* THE DEAD ZONE GOVERNS THE SLOT, NOT THE PLAYER. [h:60-tick-52] */
          /* A SLOT THAT IS PHYSICALLY OCCUPIED IS NOT A SLOT, and steering into one is how a body ends up wedged forever. [h:60-tick-53] */
          const reachable=md<=BRUNCH_DEADZONE+ENEMY.brunch.r;
          const tx=reachable?edx:mdx, ty=reachable?edy:mdy;
          const td=Math.hypot(tx,ty)||1;
          /* TWO SPEEDS, PICKED BY ROLE. [h:60-tick-54] */
          const shielding=!!(tgt&&tgt.hp>0&&pc&&pc.n>=BRUNCH_SHIELD_MIN);
          const desired=shielding?BRUNCH_SHIELD_SPEED:e.curSpeed;
          const dvx=tx/td*desired, dvy=ty/td*desired;
          /* Close a fixed FRACTION of the remaining gap per tick, which is the shape that eases a body into its slot instead of snapping at the last pixel. [h:60-tick-55] */
          const share=desired>e.curSpeed?BRUNCH_ACCEL:BRUNCH_DECEL;
          e.vx=(e.vx||0)+(dvx-(e.vx||0))*share;
          e.vy=(e.vy||0)+(dvy-(e.vy||0))*share;
          /* below a floor the velocity is snapped, so a body does not spend forever asymptotically
             approaching a slot it will never quite reach - which shimmers, and shimmers read as a bug */
          if(Math.abs(e.vx)<0.02)e.vx=0;
          if(Math.abs(e.vy)<0.02)e.vy=0;
          /* `sm` is the hit-stop multiplier and it scales the whole integration rather than the
             target, so a slowed body is a slower body rather than a body aiming to be slower */
          e.x+=e.vx*sm; e.y+=e.vy*sm;
        }
      } else { e.curSpeed=e.walkSpeed; e.pursuit=0; idleWander(e); }
    } else {
      // an alerted gunner keeps firing from anywhere, so a hit from across the room is answered.
      // `sense` is wider than `range` on purpose: it notices you early and walks you down, rather
      // than standing at the edge of its own reach pretending it cannot see you
      if(e.alerted||dist<e.sense){
        /* A GUNNER HOLDS ITS GROUND WHILE IT CHARGES, and that is what makes the tell a promise. [h:60-tick-56] */
        /* The standoff is a function of how empty the room is, and it is the whole of the fix for a shooter that could be stared at indefinitely: [h:60-tick-57] */
        /* A BODY WITH GUARDS HOLDS A LONGER STANDOFF, because the wall it is carrying forms at a fixed fraction of the gap and would otherwise stand inside... [h:60-tick-58] */
        const guarded=e.shieldGuardFor&&e.shieldGuardFor.length>0;
        const standoff=(guarded?e.far*GUARD_STANDOFF_MULT:e.far)-(e.far-e.close)*roomPress*PRESSURE_CLOSURE;

        /* A SHOOTER KEEPS WALKING WHILE IT CHARGES, and that is a change of identity rather than a tweak. [h:60-tick-59] */
        const rootsWhileCasting=e.type==='gunner';
        if(e.castT<=0||!rootsWhileCasting){
          /* A GUARDED BODY DOES NOT CLOSE. [h:60-tick-60] */
          if(dist<e.close){e.x-=edx/dist*e.speed*sm;e.y-=edy/dist*e.speed*sm;}
          else if(dist>standoff){e.x+=edx/dist*e.speed*sm;e.y+=edy/dist*e.speed*sm;}
        }
        e.shootCd--;
        /* CAST, then fire. [h:60-tick-61] */
        if(e.castT>0||e.castReady) fireCommittedShot(e,roomPress);
        else if(e.shootCd<=0){
          /* Lead the shot at where the player is GOING, using their current velocity, and widen the aim by how unsettled that movement is. [h:60-tick-62] */
          /* THE LEAD is the thing that was actually making gunners miss, not the spread. [h:60-tick-63] */
          /* THE GUNNER SOLVES AN INTERCEPT. [h:60-tick-64] */
          const dz=swerveDeadzone(), reach=Math.max(0,Math.min(1,(dist-dz)/(swerveFull()-dz)));
          const conf=Math.max(0,1-player.swerve*(rootsWhileCasting?SWERVE_TRUST_ROOTED:SWERVE_TRUST_WALKING));
          /* THE BELIEVED VELOCITY IS THE CURRENT ONE, and the bug this fixes was NOT acceleration. [h:60-tick-65] */
          const bvx=player.vx*conf, bvy=player.vy*conf;
          /* The target is the player's HITBOX, not the point their sprite is drawn from. [h:60-tick-66] */
          const ty=hitbox.y+PLAYER_HIT_DY;
          /* The ORIGIN is where the muzzle will be when the shell leaves, not where it is now. [h:60-tick-67] */
          /* WHERE THE MUZZLE WILL BE, simulated rather than approximated. [h:60-tick-68] */
          let mx=e.x, my=e.y;
          if(!rootsWhileCasting){
            const step=CAST_TIME/24, v=e.speed*sm*step;
            for(let t=step;t<=CAST_TIME+0.5;t+=step){
              const px=hitbox.x+bvx*t, py=ty+bvy*t;
              const ax=px-mx, ay=py-my, ad=Math.hypot(ax,ay)||1;
              if(ad<e.close){ mx-=ax/ad*v; my-=ay/ad*v; }
              else if(ad>standoff){ mx+=ax/ad*v; my+=ay/ad*v; }
            }
          }
          let sx=hitbox.x-mx, sy=ty-my, need=0;
          /* Twelve passes, and the number is not arbitrary. [h:60-tick-69] */
          for(let k=0;k<14;k++){
            need=(CAST_TIME+Math.hypot(sx,sy)/e.pspd);
            const px=hitbox.x+bvx*need, py=ty+bvy*need;
            sx=px-mx; sy=py-my;
          }
          const want=Math.atan2(sy,sx)+(Rnd.jitter()-0.5)*2*(0.02+SWERVE_AIM*player.swerve*reach);
          /* never put a shell through one of your own. [h:60-tick-70] */
          const shot=clearShot(r,e,want);
          if(shot!==null){
            e.castAim=shot; e.castT=CAST_TIME;
        /* THE TELL IS THE SOUND. [h:60-tick-71] */
        Sfx.tell(e);
          } else e.shootCd=e.cdMin*0.25;
        }

      } else idleWander(e);
    }
    const mv=Math.hypot(e.x-ox,e.y-oy);
    e.anim=mv>0.05?e.anim+mv/STRIDE:0;
    clampEnemy(e);
    if(Math.hypot(edx,edy-(PLAYER_HIT_DY))<e.r+PLAYER_HIT_R){
      const boss=e.type==='boss';
      if(damagePlayer(boss?2:1,edx/dist,edy/dist,(boss?8:5.5)*KNOCK_P_GAIN)){
        /* A BRUNCH THAT REACHES YOU IS NOT PUSHED BACK. [h:60-tick-72] */
        /* ONLY FOR A BRUNCH WITH NOTHING TO SHIELD. [h:60-tick-73] */
        /* A LIVE `shieldTarget` is the whole test. [h:60-tick-74] */
        const holdingSlot=!!(e.shieldTarget&&e.shieldTarget.hp>0);
        if(e.type!=='brunch'||holdingSlot) knockEnemy(e,-edx,-edy,4*KNOCK_GAIN);
        /* The Brunch's own sound, and it is HERE rather than with the knockback it is not doing: [h:60-tick-75] */
        if(e.type==='brunch'&&!holdingSlot) Sfx.touch(e);
      }
      /* A Brunch spends its own body on every touch, and dies on the second one, so a pack can punish you twice and then it eats itself. [h:60-tick-76] */
      if(e.type==='brunch'){
        e.hp-=e.maxHp/2; e.hitFlash=HIT_FLASH;
        if(e.hp<=0){ const j=r.enemies.indexOf(e); if(j>=0) killEnemy(r,j); }
      }
    }
  }
  separateBodies(r.enemies);
  for(const e of r.enemies) clampEnemy(e);   // separation can push a body into a wall

  // clearing a room is what pays out its key: the branch tip gives the gold one that opens the
  // boss door, an arm tip gives the silver one that opens the upgrade room
}

/* ---- the room: pickups, the exit, boss resolution, run end ---- */
function tickRoom(){
  /* `r` WAS A LOCAL OF update() AND EVERY PHASE READ IT. [h:60-tick-77] */
  const r=currentRoom();
  if(r.enemies.length===0){
    // the fight is over, so the dodge bar sprints: walking to the next room should never cost you a
    // blink. half a bar on the spot plus the quiet-room rate means both charges are back well before
    // the next door
    if(!r.cleared){r.cleared=true;player.blinkRegen=Math.max(player.blinkRegen,BLINK_RECHARGE*0.5);}
  }
  if(r.enemies.length===0&&(r.keyReward||r.goldReward)){
    if(r.keyReward&&!r.keySpawned){r.keySpawned=true;r.pickups.push({x:MIDX,y:MIDY,r:14,kind:'key'});}
    if(r.goldReward&&!r.goldSpawned){r.goldSpawned=true;r.pickups.push({x:MIDX,y:MIDY,r:16,kind:'goldkey'});}
  }
  /* A dead player collects nothing. [h:60-tick-78] */
  if(player.hp<=0){ endRun(false); return; }
  for(let i=r.pickups.length-1;i>=0;i--){
    const pk=r.pickups[i];
    // The way out is walked into, not collected. It gets its own touch radius - a tighter one than
    // a pickup, because a portal you brush past on the way to the heart behind it would be the
    // single most annoying thing in the game, and the player should have to mean it.
    const touching=pk.kind==='exit'
      ? Math.hypot(pk.x-player.x,pk.y-player.y)<PLAYER_HIT_R
      : Math.hypot(pk.x-player.x,pk.y-player.y)<pk.r+player.r;
    if(pk.hold){ if(!touching) pk.hold=false; continue; }   // just-dropped weapon: step off before it can swap again
    if(!touching) continue;
    // The way out is now the way DOWN. It used to call endRun(true), which ended the game and told the
    // player to press R for a new dungeon - correct when one floor was the whole game, and the single
    // most obvious thing to change once there are several. endRun is now only reachable by dying.
    if(pk.kind==='exit'){ descend(); return; }
    if(pk.kind==='weapon'){
      // swap: the held weapon is left where the new one was, so no weapon is ever lost
      const old=player.weaponIdx;
      player.weaponIdx=pk.w;
      player.cooldown=Math.min(player.cooldown,WEAPONS[pk.w].cooldown);
      pk.w=old; pk.hold=true;
      continue;
    }
    if(pk.kind==='heart'){ if(player.hp>=player.maxHp) continue; player.hp=Math.min(player.maxHp,player.hp+2); }
    else if(pk.kind==='armor'){ if(player.armor>=MAX_ARMOR) continue; player.armor=Math.min(MAX_ARMOR,player.armor+2); }
    else if(pk.kind==='key') player.hasSilver=true;
    else if(pk.kind==='goldkey') player.hasGold=true;
    /* THE RIGHT CLICK IS A WEAPON, SO IT SWAPS LIKE ONE. [h:60-tick-79] */
    else if(pk.kind==='hook'){
      if(player.altMode!=='hook'){
        player.altMode='hook'; run.hook=true;
        r.pickups.push({x:pk.x,y:pk.y,r:16,kind:'blast',hold:true});
      } else run.hook=true;
    }
    else if(pk.kind==='blast'){ player.altMode='blast'; run.hook=false; }
    /* An item goes in the slot it declares, and whatever was on that key is left where the new one was standing - the same swap a weapon gets, and for... [h:60-tick-80] */
    else if(pk.kind==='item'){
      /* A DISPLACED ITEM IS DROPPED WITH THE CHARGES IT HAD, not the ones its definition says. [h:60-tick-81] */
      const got=Items.give(pk.id,pk.charges);
      if(!got.taken) continue;              // only a duplicate passive, and the pool never offers one
      if(got.dropped)
        r.pickups.push({x:pk.x,y:pk.y,r:16,kind:'item',id:got.dropped,hold:true,charges:got.droppedCharges});
    }
    Sfx.pickup();   // only after `got.taken`, so a refused duplicate is silent
    r.pickups.splice(i,1);
  }

  // death wins ties: a bullet that lands on the frame the boss dies still ends the run as a death
  if(player.hp<=0) endRun(false);
  else if(r.type==='boss' && r.enemies.length===0){
    /* The boss no longer ends the run the instant it dies. [h:60-tick-82] */
    if(!r.exitOpen){
      r.exitOpen=true;
      r.pickups.push({x:MIDX,y:MIDY,r:30,kind:'exit'});
    }
  }
  /* THE LAB'S READOUT, at the very end, and the position is the whole argument. [h:60-tick-83] */
  if(state==='dev'){ Lab.tickNumbers(); Lab.tickShelf(); }
}


/* The way out. It is a pickup slot so that the existing touch-to-collect code carries it, but it is not an item: [h:60-tick-84] */
