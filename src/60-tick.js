/* ==============================================================================================
   60-tick  -  update(), 552 lines, the keystone of the port

   The one function I would not translate mechanically. Everything else here is a list of rules;
   this is where the rules interleave, and the only place where a mistake stays invisible until it
   is expensive. Port it last, and port it against the tests rather than by reading it.

   The order inside matters and is load-bearing: projectiles resolve, then bodies, then the player,
   then the room. Several bugs in this function were ORDER bugs - a hit landing after the body it
   hit had already moved, a projectile resolving before the cast that spawned it.
   ============================================================================================== */
/* Momentum is charged on MOVEMENT UNDER PRESSURE, and the distinction is the whole mechanic: it
   reads actual velocity rather than whether a key is down, so a body pinned against a wall by two
   lungers is not quietly farming the meter, and it only runs while something is alive to pressure
   it, so backtracking and empty rooms neither charge nor drain it. A cleared room is a breath
   rather than a reset. */
/* A shot whose cast has finished LEAVES, stunned or not.

   This is its own function because it has to run from two places, and the second one is the whole
   point. The stun check used to `continue` past the fire branch, so a body that was touched on the
   very tick its cast completed stood there with castReady set and did not fire: the player watched
   half a second of swelling light at the muzzle resolve into nothing at all, and the shell it had
   already been told about simply never arrived. A tell that has been shown is a promise, and the
   stun is not a reason to break it - the body is held, not silenced.

   It was found by a test failing for a reason that had nothing to do with what the test was about,
   which is the usual way the good ones turn up. */
function fireCommittedShot(e,roomPress){
  if(e.castT>0){ if(--e.castT<=0) e.castReady=true; return; }
  if(!e.castReady) return;
  e.castReady=false;
  e.shootCd=(e.cdMin+Rnd.jitter()*e.cdVar)*(1-PRESSURE_CADENCE*roomPress);
  projectiles.push({x:e.x,y:e.y,vx:Math.cos(e.castAim)*e.pspd,vy:Math.sin(e.castAim)*e.pspd,r:e.pr,
    dmg:e.dmg,friendly:false,color:e.pcol,owner:e,heavy:e.type==='gunner'});
}

function tickMomentum(moved){
  if(momentumLocked) return;
  if(currentRoom().enemies.length===0) return;
  player.momentum=moved>MOMENTUM_MOVE_FLOOR
    ? Math.min(1,player.momentum+MOMENTUM_GAIN*moved)
    : Math.max(0,player.momentum-MOMENTUM_STALL_DECAY);
  Stats.earn('momentum',player.momentum);
}

function update(){
  frameCount++;
  tickFX();
  /* The refill flash is ticked HERE, above every early return in this function, and both of the two
     places it was first written were wrong in the same way. update() bails out on a door transition,
     on the READY window, and on death, and enterRoom fires the flash DURING a transition with a
     ready window already running - 158 ticks against a flash of 34. A clock anywhere below those
     returns therefore never advanced at all, so the ring the player is meant to watch fade out from
     under the fade stayed at full strength for the entire fade and then vanished in a single frame.
     It read as a rendering fault rather than as the gift it was meant to be, and nothing about the
     symptom pointed at the clock.

     It lives next to tickFX() because it is the same kind of thing: presentation that must keep
     running through states where the simulation has stopped. If it is ever moved down into the
     playing branch, it will stop working for exactly the one moment it exists to cover.

     The `player&&` guard is not defensive noise and it has to stay. This line sits deliberately above
     the state!=='playing' return, because that is the point of it, and on the title screen there is
     no player at all - player is undefined until the first startGame(). A tick placed above the state
     check has to survive the states above it. The suite missed this entirely because every test calls
     startGame() before it touches anything, so the title screen was the one place it could break and
     no test ever stood there. */
  if(player&&player.restoreFX&&player.restoreFX.t>0)player.restoreFX.t--;
  // roomFade is progress-driven, not "the complement of a smoothstep": smooth() is flat at BOTH
  // ends, so 1-smooth(fadeT/ticks) jumped to 0 on the first tick and the whole fade was a cut.
  // p runs 0 -> 1 across the fade, so 1-smooth(p) starts at full black and eases out of it.
  if(fadeT>0&&!trans){fadeT--; roomFade=1-smooth(1-fadeT/fadeTicks);}
  else if(!trans) roomFade=0;
  if(state!=='playing') return;
  // The backstop. Everything below here can return early - a room transition, the ready window, a
  // death check further down - and every one of those returns is a tick that never looks at your
  // health. So the health is checked here, before any of them can fire, as well as at the point the
  // damage lands. One check on its own is not enough: the only way to guarantee a dead player cannot
  // still be playing is to check on the way in AND on the way out, so there is no route through
  // this function that leaves a zero-health player standing.
  if(player.hp<=0){ endRun(false); return; }
  run.ticks++;
  if(trans){
    trans.t++; roomFade=smooth(Math.min(1,trans.t/FADE_OUT));
    if(trans.t>=FADE_OUT){const t=trans;trans=null;enterRoom(t.nx,t.ny,t.from);}
    return;
  }
  if(readyT>0){readyT--;return;}
  if(bossWarnT>0)bossWarnT--;
  // a key in hand is not a key spent: stand at the door and the lock works, and walking off it
  // leaves the door exactly as shut as it was
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
  /* The landing burst, decaying. Three things about it are deliberate.

     It is counted in TICKS and decremented by one, so BLINK_BOOST is honestly the length of it. It
     was originally a level that lost 0.011 per tick, which made a value of 23 last twenty-three
     hundred ticks - about ten seconds, ten times what the comment beside it claimed.

     It is DIRECTIONAL. The gain scales with how well the held direction matches the direction you
     blinked, because a flat multiplier was quietly the strongest version of itself: you could
     blink away from a Brunch and then immediately hold the opposite key and cover the ground twice
     over, which is not momentum, it is a free displacement. A burst that only pays out along its
     own heading means continuing is rewarded and turning around costs you the whole of it, which
     is what makes "land and run" a decision rather than a reflex.

     And it decays whether you are moving or not, so it cannot be banked by standing still. */
  if(player.boost>0)player.boost--;
  const align=len?(dx/len)*player.boostX+(dy/len)*player.boostY:0;
  // top speed comes from the DERIVED stat, never from an item having multiplied player.speed in
  // place. The base constant is untouched, so removing an item removes exactly its contribution.
  const spd=player.speed*player.slowMult*(1+moveSpeedBonus())*(player.boost>0?1+(BLINK_BOOST_GAIN-1)*Math.max(0,align):1);
  const targetVx=len?(dx/len)*spd:0, targetVy=len?(dy/len)*spd:0;
  // acceleration, not speed, is where the Momentum reward lives. Reaching a steady heading takes
  // the same number of ticks either way, so a room is still crossed in the same time - which is
  // what keeps every distance-based measurement in this game valid - while the player stops
  // sliding and can commit on the tick they think of it.
  const accel=MOVE_ACCEL*(1+Momentum.level()*MOMENTUM_ACCEL);
  player.vx+=(targetVx-player.vx)*accel;
  player.vy+=(targetVy-player.vy)*accel;
  /* The heading the player has been HOLDING, as opposed to the heading they are on this tick. The
     lungers read this one and not the raw velocity, because a player one tick into a keypress is
     still nearly stationary and a player mid-reversal is momentarily pointing the wrong way - both
     of which a lunge aimed at would be aimed at a place the player is about to leave. Slow on
     purpose: a third of a second of memory is about how long it takes to see a line and answer it. */
  player.trendVx+=(player.vx-player.trendVx)*LUNGE_TRACK;
  player.trendVy+=(player.vy-player.trendVy)*LUNGE_TRACK;

  /* WHICH WAY THE LUNGERS THINK YOU ARE GOING, which is a different question from how fast you are
     moving and is maintained HERE rather than inside the solve, because the tick is the only thing
     that owns per-frame state.

     This is a heading and not a velocity, and it is quick rather than slow. trendV above is a
     velocity averaged over about a third of a second, which is the right memory for "which line is
     this person on" and the wrong one for "which way have they just turned": a person reverses faster
     than that average can follow, so aiming from it put nearly a third of all lunges at the direction
     the player had just left - measured at 289 of 900 commits against a half-second reversal.

     It is still smoothed, because a heading read from one raw tick flips on any jitter. A player too
     slow to have a heading at all leaves the belief alone and the solve falls back to trendV. */
  const bsp=Math.hypot(player.vx,player.vy);
  if(bsp>PLAYER_SPEED_EPS){
    player.beliefVx+=(player.vx/bsp-player.beliefVx)*LUNGE_BELIEF_TRACK;
    player.beliefVy+=(player.vy/bsp-player.beliefVy)*LUNGE_BELIEF_TRACK;
    const bl=Math.hypot(player.beliefVx,player.beliefVy);
    if(bl>1e-4){ player.beliefVx/=bl; player.beliefVy/=bl; }
  }
  // SWERVE: how unsettled your movement is right now, 0 while you hold a heading and 1 while you
  // are reversing every few ticks. The gunners aim wider the higher it is, which is what turns
  // counterstrafing from a lucky trick into a tactic you can rely on - a shot at a target holding a
  // straight line is a tight shot with a good lead, and a shot at a target that keeps reversing is
  // a wide one. Walking in a straight line is therefore the thing that gets you hit.
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
  if(trans) return;

  const sp=Math.hypot(player.vx,player.vy);
  /* Displacement under the player's OWN power: how far the body actually got, less whatever the
     enemy threw at it. Not velocity, and not whether a key is down.

     Velocity was the first attempt and it is wrong in a way that only shows up when you ask what it
     does to a player who is losing. clampPlayer() stops a body's POSITION at the wall and leaves its
     velocity pointing into it, because that is what keeps the movement code simple everywhere else -
     so a player holding a key against a wall with bodies alive reports full speed forever and farms
     the meter without moving a pixel. Momentum is meant to measure whether you are playing well, and
     a player who is pinned is not. Measuring the ground actually covered is the honest version. */
  tickMomentum(Math.max(0,Math.hypot(player.x-wasX,player.y-wasY)-knk));
  player.anim=sp>0.12?player.anim+sp/STRIDE:0;
  tickBlink();

  if(player.cooldown>0)player.cooldown--;
  if(player.altCooldown>0)player.altCooldown--;
  tickFields(currentRoom());
  tickHookResist(currentRoom());
  if(player.iframes>0)player.iframes--;
  if(player.muzzleTimer>0)player.muzzleTimer--;
  if(player.shootSlow>0)player.shootSlow=Math.max(0,player.shootSlow-SHOOT_SLOW_RECOVER);
  if(mouseDown && player.cooldown<=0) fireWeapon();
  // deliberately NOT gated on the alt cooldown: the handler decides whether a click is a cancel or
  // a cast, and it can only do that if it is actually reached. See fireAlt.
  if(altMouseDown) fireAlt();

  // the hitbox the enemies use trails the real position after a blink, then catches up
  player.lagX+=(player.x-player.lagX)*HITBOX_LAG_EASE;
  player.lagY+=(player.y-player.lagY)*HITBOX_LAG_EASE;
  const hx=player.lagX, hy=player.lagY;

  const r=currentRoom();
  for(let i=projectiles.length-1;i>=0;i--){
    const p=projectiles[i];
    if(p.alt){
      // Stop on the point, not past it. The question is asked BEFORE the move: if the target is
      // within one step, snap to it and go off there. Moving first and then asking whether the
      // remaining distance has fallen to the step size puts the test on a knife edge - aim at your
      // own feet and the remaining distance lands a float epsilon over the step, never triggers, and
      // the bolt sails on to hit a wall instead. This is the whole reason the hook could not detonate
      // where you clicked.
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
      // exactly one body per tick. Without this a bolt inside a knot of Brunch counts every one of
      // them at once, and pierce stops meaning "drills through a line" and starts meaning "hits
      // everything it overlaps", which is a different weapon and a much stronger one. A body is
      // counted, the bolt carries on, and the next body in the line is counted a few ticks later.
      // Of the bodies in contact RIGHT NOW, hit the one the bolt reached FIRST. "Reached first" is
      // how far the body sits ALONG the direction of travel, not how far it is from the muzzle in a
      // straight line and not where it happens to sit in the array.
      //
      // All three of those are wrong in a way that matters. The array is walked backwards so that
      // killEnemy's splice cannot corrupt it, and taking "whichever body the loop reached first"
      // therefore handed the discount out in ARRAY order - so a line of Brunch took less damage at
      // the front than at the back purely because of spawn order. Straight-line distance from the
      // muzzle is wrong too, because a bolt flies in a straight line and a knot is not arranged
      // along it: a body 20px off the line is reached after one that is further down it but level
      // with the barrel. The projection is the only measure that is arrival order by definition, and
      // it is fixed at spawn so no amount of jostling inside a knot can reshuffle it.
      let best=-1, bestA=Infinity;
      for(let j=r.enemies.length-1;j>=0;j--){
        const e=r.enemies[j];
        if(p.hit&&p.hit.indexOf(e)>=0) continue;   // already counted this one; move on to the next
        if(Math.hypot(p.x-e.x,p.y-e.y)>=p.r+e.r) continue;
        // Only a bolt that CARRIES ON needs to know which of two touching bodies it reached first.
        // Anything else is detonating on the first thing it touches, and the order of bodies in the
        // array is irrelevant to that - so do not ask. Asking anyway is what broke the blast: the
        // ranking reads p.ox/p.dx, which only a friendly wand shot has, so on an alt bolt it produced
        // NaN, NaN failed the comparison, no contact was ever found, and the blast quietly flew to
        // the cursor and phased straight through the room.
        if(!p.pierce){ best=j; break; }
        const a=(e.x-p.ox)*p.dx+(e.y-p.oy)*p.dy;
        if(a<bestA){ bestA=a; best=j; }
      }
      if(best>=0){
        const e=r.enemies[best];
        const j=best;
        if(p.alt){ explode(r,p.x,p.y,p.mode); projectiles.splice(i,1); break; }   // the blast detonates on contact
        e.hp-=p.dmg*falloffMult(p)*e.armour*p.scale; e.hitFlash=HIT_FLASH; run.hits++;
        alertEnemy(e); slowEnemy(e);
        if(e.hp<=0) killEnemy(r,j);
        if(p.pierce>0){
          // drill through and keep going. `p.hit` is what stops the bolt re-hitting the body it is
          // currently sitting inside, and the falloff is what makes lining the pack up the decision
          p.pierce--; p.scale*=PIERCE_FALLOFF; (p.hit||(p.hit=[])).push(e);
        } else {
          projectiles.splice(i,1);
          break;
        }
      }
    } else {
      let hitSomething=false;
      for(let j=r.enemies.length-1;j>=0;j--){
        const e2=r.enemies[j];
        if(e2===p.owner || e2.type!=='lunger') continue;
        if(Math.hypot(p.x-e2.x,p.y-e2.y)<p.r+e2.r){
          e2.hp-=1; e2.hitFlash=HIT_FLASH; alertEnemy(e2); slowEnemy(e2); hitSomething=true;
          if(e2.hp<=0) killEnemy(r,j);
          break;
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

  // Iterate a SNAPSHOT, not the live array. A Brunch that lands its second touch calls killEnemy,
  // which splices r.enemies out from under this loop - and a for...of over an array being spliced
  // silently skips the element that slid into the hole. With a knot of Brunch touching at once that
  // is a body which does not get its turn, does not spend itself, and never dies. Iterating a copy
  // costs one allocation and makes the kill order irrelevant.
  // Room pressure is counted ONCE here, from the snapshot, rather than per enemy: it is a property
  // of the room and recomputing it inside the loop would make a pack of eight cost eight scans.
  const roomPress=roomPressure(r.enemies.reduce((n,x)=>n+(x.hp>0?1:0),0));
  for(const e of r.enemies.slice()){
    if(e.hp<=0) continue;   // a body killed earlier in this same tick has already been removed
    if(e.hitFlash>0)e.hitFlash--;
    if(e.kvx||e.kvy){
      e.x+=e.kvx; e.y+=e.kvy; e.kvx*=KNOCK_FRICTION; e.kvy*=KNOCK_FRICTION;
      if(Math.abs(e.kvx)<KNOCK_CUT)e.kvx=0;
      if(Math.abs(e.kvy)<KNOCK_CUT)e.kvy=0;
    }
    if(e.slowT>0)e.slowT--;
    const sm=e.slowT>0?HIT_SLOW_MULT:1;
    if(e.stun>0){
      // A stunned lunger is not a lunger mid-lunge, it is a lunger with its feet stuck. Letting the
      // stun skip the state machine without clearing it left the lunge QUEUED: the glow stayed up,
      // the aim line stayed drawn, and the attack resumed the instant the hold wore off, which is
      // both an attack you could not read and one you had already dodged. Interrupting a charge
      // with the hook has to actually interrupt it, and it has to look like it did.
      if(e.lungeState==='wind'||e.lungeState==='lunge'){
        e.lungeState='approach'; e.lungeT=0; e.lungeLen=0;
        // and it pays for it: the cooldown starts now, so a lunger you caught mid-charge has to
        // come all the way back around before it tries again
        e.lungeCd=LUNGE_CD;
      }
      // A cast that has already finished still fires, or the tell the player was reading would be a
      // lie told by the game to the player. See fireCommittedShot.
      if(e.walkSpeed===undefined&&e.type!=='boss') fireCommittedShot(e,roomPress);
      e.stun--;e.anim=0;clampEnemy(e);continue;
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
    const edx=hx-e.x,edy=hy-e.y,dist=Math.hypot(edx,edy)||1;
    if(e.walkSpeed!==undefined){
      if(dist<AGGRO_RANGE) e.aggroTimer=AGGRO_TIME;
      else if(e.aggroTimer>0) e.aggroTimer--;
      if(e.aggroTimer>0){
        if(e.type==='lunger') stepLunge(e,edx/dist,edy/dist,dist,sm,r);
        else{
          // a Brunch pack announces itself and then arrives: the ramp is long enough that there is
          // a real interval in which to choose your ground before it is on you
          e.pursuit++;
          const gain=1+BRUNCH_RAMP_GAIN*Math.min(1,e.pursuit/BRUNCH_RAMP);
          e.curSpeed+=(e.runSpeed-e.curSpeed)*LUNGER_ACCEL*gain;
          e.x+=edx/dist*e.curSpeed*sm; e.y+=edy/dist*e.curSpeed*sm;
        }
      } else { e.curSpeed=e.walkSpeed; e.pursuit=0; idleWander(e); }
    } else if(e.type==='boss'){
      e.x+=edx/dist*e.speed*sm;e.y+=edy/dist*e.speed*sm;
    } else {
      // an alerted gunner keeps firing from anywhere, so a hit from across the room is answered.
      // `sense` is wider than `range` on purpose: it notices you early and walks you down, rather
      // than standing at the edge of its own reach pretending it cannot see you
      if(e.alerted||dist<e.sense){
        /* A GUNNER HOLDS ITS GROUND WHILE IT CHARGES, and that is what makes the tell a promise.

           The aim is solved when the cast starts and the shell leaves half a second later, so the
           two positions have to be the same position or the solution describes a shot nobody is
           going to take. A gunner walking toward a moving player covers a hundred and twenty pixels
           in that half second - it is chasing - so an intercept solved from where it stood when the
           muzzle lit up is aimed at a point the shell, leaving from somewhere else entirely, sails
           straight past. Nothing about that looks wrong on screen: the flash appears, the shell
           appears, the shell misses. The gunner was simply solving the wrong problem from the wrong
           place.

           So it stands still to charge. It costs it half a second of closing, which is nothing at
           the speed it walks, and it buys three things that are all worth more than that: the tell
           now points at exactly where the shell is going to arrive, the clear-line check is done
           from the muzzle that will fire it, and the shot can be dodged by changing your line while
           the flash is up - which is a thing a player can actually do, and is the whole point of
           having half a second of warning. */
        /* The standoff is a function of how empty the room is, and it is the whole of the fix for a
           shooter that could be stared at indefinitely: a shell from 150px has a flight short enough
           that reversing inside it is not an answer, while the same body in a room of five is content
           to hold 250px and let the count do the work. See roomPressure in 00-balance. */
        const standoff=e.far-(e.far-e.close)*roomPress*PRESSURE_CLOSURE;

        /* A SHOOTER KEEPS WALKING WHILE IT CHARGES, and that is a change of identity rather than a
           tweak. Holding the ground was the gunner's whole trick: the muzzle does not move, so the
           line the clear-shot sweep checked and the line the shell walks are the same line, and the
           tell can be trusted to preview the shot. It also made the body a statue for half a second,
           at a fixed spot, every single cast - completely predictable, and a predictable enemy has
           much less effect on a fight than a threatening one.

           So the two are split. The gunner still roots itself: it is the heavy, committed shot and
           its accuracy is measured to be exactly that. A shooter keeps drifting, which means its aim
           has to be solved from where its muzzle WILL BE rather than where it is - see below. */
        const rootsWhileCasting=e.type==='gunner';
        if(e.castT<=0||!rootsWhileCasting){
          if(dist<e.close){e.x-=edx/dist*e.speed*sm;e.y-=edy/dist*e.speed*sm;}
          else if(dist>standoff){e.x+=edx/dist*e.speed*sm;e.y+=edy/dist*e.speed*sm;}
        }
        e.shootCd--;
        /* CAST, then fire. A gunner that fires the instant its cooldown runs out gives the player
           nothing to read - the shell is already in the air before you have decided what to do, and
           the only counter is not being there. This is the smallest version of the lunger's tell: a
           swelling light at the muzzle, no new state machine, and the gunner keeps moving and can
           still touch you while it charges.

           The aim is computed when the cast STARTS and held, and that is the important half. A
           gunner that re-aims at the moment of firing makes the flash a meaningless decoration -
           you watched it point somewhere and it shot somewhere else. Aiming at the start means the
           tell genuinely previews the shot, and it also means the shot goes where the player was
           half a second ago, which is what makes the tell worth reading rather than just worth
           noticing. It is the same bargain the lunger's lunge makes, at a quarter of the size.

           The clear-line check runs before the cast rather than after it, so the flash always means a
           shell is genuinely coming. A gunner that cannot see past its own Brunch pack holds its
           shot instead of building a tell and then wasting it, which teaches the player that the
           pack is worth something. */
        if(e.castT>0||e.castReady) fireCommittedShot(e,roomPress);
        else if(e.shootCd<=0){
          // Lead the shot at where the player is GOING, using their current velocity, and widen the
          // aim by how unsettled that movement is. Together those two make the fight a movement read:
          //
          //   walking a straight line at constant speed is the easiest thing in the game to hit,
          //   because the lead is exactly right and the spread is tight;
          //   counterstrafing - reversing every half second or so - is a reliable dodge, because the
          //   lead is built from a velocity that keeps flipping and the spread is wide open.
          //
          // Without the widening, counterstrafing is only a coin flip: you are right about half the
          // time and it feels random. With it, the tactic is something you can commit to, which is
          // what makes it a tactic rather than a habit.
          /* THE LEAD is the thing that was actually making gunners miss, not the spread.

             A shell is aimed at where the player is GOING, computed from their velocity. At two
             hundred pixels the flight is about ninety ticks - nearly half a second - and the lead is
             worth about fifty pixels. So a player who reverses sends the shell the wrong way by a
             hundred pixels, and no amount of tightening the spread matters: the shell was never
             going to arrive. Gating the SPREAD on distance fixed the wrong half of this and left
             counterstrafing completely free in front of a gunner at point blank range, which is
             exactly what it still felt like afterwards.

             Two changes, on one signal.

             The lead is built from the SMOOTHED heading rather than this tick's velocity, so it is
             not reading a number that is mid-swing.

             And the AMOUNT of lead is scaled by how SETTLED the player looks, on the same swerve
             the lunge reads. That is the part that matters, and the reason is arithmetic rather
             than taste: a player who reverses every half second has travelled very nearly nowhere
             by the time a shell from close range arrives. Leading a thrashing player points the
             shell at where they are going; NOT leading them points it at where they are, and those
             are nearly the same place. So the gunner stops leading the moment the movement stops
             making sense, and the shot lands on the counterstrafer for the same reason it lands on
             the runner - by a different route.

             One signal, and the two gunners now agree about the player: the lunger's intercept and
             the gunner's lead both shrink when the player thrashes, and both are exact when they
             do not. */
          /* THE GUNNER SOLVES AN INTERCEPT. It did not, and no amount of tuning the lead would have
             fixed it, because the thing it was computing was the wrong quantity rather than the
             wrong size of the right one.

             A lead is an ARITHMETIC claim about where the player will be: this far away, moving this
             fast, therefore over there. But the shell is not teleported to that point, it is fired
             at it, and it only covers so much ground. At two hundred pixels the flight is about
             fifty ticks and the shell travels exactly the two hundred it had to - and the claim was
             about a point three hundred away. So the shot went wide of the player by the same
             margin every single time, aimed at something the shell could never reach. Ten shells,
             no hits, against a player walking in a perfectly straight line. The arithmetic was
             correct and the answer was unreachable.

             And the error scales with the player's speed, which is the worst possible way for it to
             behave: standing still, the lead is zero and the aim is trivially right, so the gunner
             looks fine in every test that does not involve movement. It only ever misses against the
             thing it is supposed to punish.

             The same three-pass iteration the lunger's lunge uses, with two differences that are the
             whole of this. The shell does not exist for CAST_TIME more ticks, so the intercept is
             seeded from where the player will be WHEN THE SHELL LEAVES rather than where they are
             now - otherwise the half second of visible charging is a half second of free movement
             that the prediction never saw. And the result is a POINT rather than an angle, so the
             gunner can keep walking while it charges and still fire at it: the tell points at the
             spot the shell is going to arrive at, and the arrival is computed from wherever the
             muzzle has got to by then.

             The believed velocity is the smoothed heading scaled by how settled the player looks, on
             the same swerve the lunge reads - one signal, three consumers now, and all three agree
             about the player. A counterstrafer is believed in only a little, so the intercept
             barely leads them, so the shell arrives at roughly where they are; over a flight of
             fifty ticks a player who reverses every half second has travelled very nearly nowhere,
             and the two are nearly the same place. That is why the counterstrafe stops working at
             close range without anything being special-cased for it.

             And the spread stays a DISTANCE thing. It used to be applied at every range, so a gunner
             two hundred pixels away punished a straight line exactly as hard as one five hundred
             away - there was never a reason to close the distance, and nothing at all could
             threaten a player who kept moving in a line. It is zero inside half the width of the
             room and ramps to the full bonus over the next stretch, so distance is what buys the
             player the tactic. The floor of 0.02 stays: a gunner is never perfectly deterministic,
             and a shot that is the same shot every time is a shot that can be walked into. */
          const reach=Math.max(0,Math.min(1,(dist-SWERVE_DEADZONE)/(SWERVE_FULL-SWERVE_DEADZONE)));
          const conf=Math.max(0,1-player.swerve*(rootsWhileCasting?SWERVE_TRUST_ROOTED:SWERVE_TRUST_WALKING));
          const bvx=player.trendVx*conf, bvy=player.trendVy*conf;
          /* The target is the player's HITBOX, not the point their sprite is drawn from. The hit test
             is a circle ten pixels below the origin - a body reads as its chest and hem, not as its
             coordinate - and an intercept solved against the origin is therefore aimed ten pixels
             ABOVE the thing it is trying to hit, every single time, by an amount that is small
             enough to look like nothing and large enough to matter: the shot passes over the player's
             head with a hitbox's width to spare and registers as a clean miss. A lunger must not be
             given that target, because it steers a body and would visibly drift low, but a gunner is
             aiming a projectile at a point and has no reason at all to miss the one part of the
             player it is actually going to hit. */
          const ty=hy+PLAYER_HIT_DY;
          /* The ORIGIN is where the muzzle will be when the shell leaves, not where it is now.
             The player's position is already predicted this way - the shell does not exist for
             CAST_TIME more ticks - and a walking shooter needs the same treatment for its own side,
             or the angle is solved from a place the body has already left and the shell walks off
             from wherever it ended up. The drift is exact rather than estimated: a body walking at
             e.speed covers e.speed*CAST_TIME, and it walks toward or away depending on the same
             standoff test that moved it a tick ago. */
          /* The drift is what the standoff rule will REALLY do over the cast, not speed*CAST_TIME -
             see standoffDrift, which exists because the difference between those two is forty pixels
             of miss on a player running in a straight line. */
          /* WHERE THE MUZZLE WILL BE, simulated rather than approximated.

             Two approximations were tried and both left every shot about twenty pixels wide. The
             first predicted a radial offset of speed*CAST_TIME and ignored that the body reverses at
             its own threshold, so it travels a third of that. The second used the corrected radial
             number and was still wrong, because "toward the player" is a BEARING: the player covers
             more ground over the cast than the shooter does, so the bearing rotates underneath a walk
             predicted as though it stood still.

             So the muzzle is walked forward against the believed player path in twenty-four steps,
             obeying the same standoff rule that is moving it. It depends on the BELIEF rather than the
             truth, which is exactly right - a body solves the player it thinks is there, and when the
             belief is wrong the shot misses, which is the mechanic and not a flaw in the arithmetic.
             A gunner skips this entirely, so the loop below is unchanged for the heavy shot. */
          let mx=e.x, my=e.y;
          if(!rootsWhileCasting){
            const step=CAST_TIME/24, v=e.speed*sm*step;
            for(let t=step;t<=CAST_TIME+0.5;t+=step){
              const px=hx+bvx*t, py=ty+bvy*t;
              const ax=px-mx, ay=py-my, ad=Math.hypot(ax,ay)||1;
              if(ad<e.close){ mx-=ax/ad*v; my-=ay/ad*v; }
              else if(ad>standoff){ mx+=ax/ad*v; my+=ay/ad*v; }
            }
          }
          let sx=hx-mx, sy=ty-my, need=0;
          /* Twelve passes, and the number is not arbitrary. This is fixed-point iteration, and how
             fast it converges is set by how much slower the target is than the shell: each pass pulls
             the remaining error down by the ratio of the player's speed to the shell's, which here
             is a little over a half. Three passes - which is what the lunger's lunge uses, and is
             plenty there, because a lunge crosses at six times the player's speed - leaves a
             sixteenth of the error, and a sixteenth of a two hundred and sixty tick horizon is
             twenty six ticks of lead, which is thirty pixels of miss on a player doing nothing
             clever. Fourteen passes leaves a thousandth. It is a few extra square roots per shell
             and shells are the rarest thing in the game. */
          for(let k=0;k<14;k++){
            need=(CAST_TIME+Math.hypot(sx,sy)/e.pspd);
            const px=hx+bvx*need, py=ty+bvy*need;
            sx=px-mx; sy=py-my;
          }
          const want=Math.atan2(sy,sx)+(Rnd.jitter()-0.5)*2*(0.02+SWERVE_AIM*player.swerve*reach);
          // never put a shell through one of your own. a gunner that blindly fires into a Brunch pack
          // wastes its shot and teaches the player that shells are not the threat; one that waits
          // for a gap is shooting at you *through* the pack, which is the whole reason to want the
          // pack there. if nothing is clear it holds the shot rather than waste it.
          // The angle clearShot RETURNS is the one committed to, and that is the entire reason it
          // sweeps: the straight line at you is often blocked when a line a few degrees off is not,
          // and finding that is what the sweep is for. Testing its truthiness and then storing the
          // unadjusted `want` throws the sweep away and fires the shell into the very ally it just
          // dodged - so the shell goes where the search actually found a gap.
          const shot=clearShot(r,e,want);
          if(shot!==null){
            e.castAim=shot; e.castT=CAST_TIME;
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
        knockEnemy(e,-edx,-edy,4*KNOCK_GAIN);
      }
      // A Brunch spends its own body on every touch, and dies on the second one, so a pack can
      // punish you twice and then it eats itself. Without this a knot of them is a damage clock
      // that never stops, and it is the one enemy that outnumbers you by design.
      //
      // It is deliberately NOT inside the iframes check above. If it were, a pack would only ever
      // spend a Brunch on the tick that got past the player's i-frames, and the other seven - which
      // are all touching the player on the very same tick - would cost the pack nothing at all. The
      // bodies would keep hitting and keep surviving, which is the exact opposite of the intent.
      // Collision is collision: a Brunch that reaches you spends itself, hit or no hit.
      if(e.type==='brunch'){
        e.hp-=e.maxHp/2; e.hitFlash=HIT_FLASH;
        if(e.hp<=0){ const j=r.enemies.indexOf(e); if(j>=0) killEnemy(r,j); }
      }
    }
  }
  for(let a=0;a<r.enemies.length;a++)for(let b=a+1;b<r.enemies.length;b++) bounceEnemies(r.enemies[a],r.enemies[b]);
  for(const e of r.enemies) clampEnemy(e);   // separation can push a body into a wall

  // clearing a room is what pays out its key: the branch tip gives the gold one that opens the
  // boss door, an arm tip gives the silver one that opens the upgrade room
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
  // A dead player collects nothing. This has to be checked BEFORE the floor is swept, not after:
  // a heart under your feet used to be picked up on the very tick that took your last point of
  // health, put you back on your feet, and the death check at the bottom of the tick then saw a
  // healthy player and never fired. Standing on a heart with nothing left is how you ended a run at
  // zero health and carried on, so death is settled the moment it happens and nothing after it in
  // the tick gets a vote.
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
    if(pk.kind==='exit'){ endRun(true); return; }
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
    else if(pk.kind==='hook'){ player.altMode='hook'; run.hook=true; }
    else if(pk.kind==='blast'){ player.altMode='blast'; run.hook=false; }
    r.pickups.splice(i,1);
  }

  // death wins ties: a bullet that lands on the frame the boss dies still ends the run as a death
  if(player.hp<=0) endRun(false);
  else if(r.type==='boss' && r.enemies.length===0){
    // The boss no longer ends the run the instant it dies. A cleared boss room opens a way out -
    // one portal, in the middle of the floor - and the run ends when the player walks into it.
    // Ending on the kill took the decision away: the moment you wanted to go back and finish the
    // rest of the room, or turn a run you had already won into a run you had earned, the results
    // screen was already up. Walking to the portal is the smallest possible version of that
    // decision, and it costs the player one walk they would have made anyway.
    if(!r.exitOpen){
      r.exitOpen=true;
      r.pickups.push({x:MIDX,y:MIDY,r:30,kind:'exit'});
    }
  }
}

/* The way out. It is a pickup slot so that the existing touch-to-collect code carries it, but it is
   not an item: it cannot be picked up, moved, or missed by accident, and walking into it is the
   only way to trigger it. It sits dead centre because a door in a corner is a thing you can miss on
   the way past, and this is a thing you should be able to see from the far side of the room. */
