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

/* THE WARDEN: the boss, as a composition of the vocabulary the rest of the game already speaks.

   It used to be a large lunger with no ranged kit - `e.type!=='boss'` is explicitly excluded from
   firing - so it walked at the player slowly and did nothing else. 50 HP of walking is not a fight,
   it is a long walk.

   The design rule for this game is that difficulty comes from RATE and DENSITY and never from
   something the player cannot read, so the boss is built out of attacks that already have tells and
   already have answers, rather than out of new mechanics. Every move below is one the player has
   already learned to read from a smaller body:

     VOLLEY   three committed shells, each with the gunner's muzzle tell. The player has been dodging
               these from gunners for twenty minutes; the boss does not teach anything new, it just
               does more of it.
     SWEEP    a lunger lunge, from a body four times the mass. Same wind-up, same commit, same dodge.
     WALL     the Brunch formation, called as COVER for the boss. This is the one genuinely new
               thing here, and it is new because the player has just learned that Brunch eat shells -
               so the fight where that matters most is the boss, and the boss gets its own wall to
               stand behind. The inversion is the point: the cover the player has been building is
               now something the fight takes away.

   PHASES, because a fight that repeats one loop is a job rather than a fight. Two thresholds, and
   each one opens a move rather than just raising a number. The tell is the PHASE BANNER, which is
   drawn and is not skippable, because a phase change the player cannot see is a difficulty spike
   wearing a disguise.

   NOTHING HERE IS UNREADABLE. Every attack resolves at most one commitment at a time, every one is
   preceded by a tell the game has already drawn, and the boss never acts while recovering from its
   own previous move. A player who reads correctly takes very little damage; a player who reads
   nothing takes all of it. That gap is the fight. */

function bossInit(e){
  e.phase=1;
  e.move='idle';
  e.moveT=0;
  e.bossCd=sec(1.2);
  e.volleyLeft=0;
  e.volleyT=0;
  e.sweepAim=0;
  e.sweepFrom=null;
  /* `e.wallIds` used to live here and was written and never read: `bossCallWall` tracked the bodies
     themselves in `e.wallBodies`, and the expiry sweep identifies a wall by `packId === BOSS_WALL_ID`.
     A dead field named for a mechanism is worse than no field - the next reader assumes the wall is
     tracked by id and cannot work out why there is nothing to search. */
  e.wallT=0;
  // the ranged kit it was missing. These are the gunner's numbers with a boss behind them, and the
  // aim is solved at the moment the tell starts rather than when the shell leaves, exactly as it is
  // for a gunner - so a player who commits to a dodge is dodging the shot that will arrive.
  e.range=620; e.sense=900; e.close=140; e.far=260;
  e.cdMin=BOSS_CD_MIN; e.cdVar=BOSS_CD_VAR;
  e.dmg=BOSS_SHELL_DMG; e.pspd=1.9; e.pr=8; e.pcol='#ff9a5a';
}

/* The wall the boss calls. Deliberately built through spawnEnemy and the same pack fields the room
   generator uses, so the formation code, the separation pass and the shell-absorption rule all apply
   to it untouched. A boss that had its own private cover would be a second implementation of the one
   thing that is easiest to get subtly wrong. */
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
    /* PUT IT IN THE ROOM. spawnEnemy RETURNS a body and does not add it - the room generator is the
       thing that pushes, and so is every other caller. The first version of this line was missing
       entirely, so the boss spent the whole fight calling a wall made of nothing: the formation code
       found no Brunch to arrange, the shell-absorption rule had nothing to absorb, and an earlier
       measurement reported "brunch alive: 0" without anyone reading it as the bug it was. It is the
       same trap the walkaround harness fell into six times in a row, and the same trap is in the
       room generator's own comment. */
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


/* The boss's turn. One move at a time, always the same shape: a TELL, a COMMIT, a RECOVER.

   The structure is the whole fairness argument. There is no move that can start while the boss is
   recovering, there is no move that resolves in less time than its own tell, and the phase changes
   open a move rather than accelerating the existing one - so a player who is keeping up with the
   tells is never asked to react to something they have not already seen. */
function stepBoss(e,edx,edy,dist,sm,room){
  const hx=player.x, hy=player.y;
  if(e.moveT>0) e.moveT--;

  // the wall it called expires on its own, and it is the only boss resource that runs without the
  // player doing anything - a wall that never went away would end the fight for them
  //
  // EXPIRY REMOVES THE BODIES. It used to only drop the handle (`e.wallBodies=null`), which stopped the
  // bookkeeping and left all five Brunch standing in the room forever - and `bossCallWall` OVERWRITES
  // that handle each call, so the 14s timer could only ever police the most recent wall while every
  // earlier one stayed on the floor for the rest of the fight.
  //
  // Measured, player kept alive through a phase-3 Warden: 7 wall calls over 120 seconds left 35 Brunch
  // in one room and climbing, against `DEPTH_BODY_CAP=28`. That cap is applied by `depthBodies()` per
  // normal spawn wave and is never consulted here, so the documented "the cap means the game never
  // reaches counts where this matters" does not hold for the boss - and the cost is not linear in
  // bodies but superlinear in everything that walks them:
  //
  //     28 bodies    0.094 ms/tick    2.0% of the 4.76ms budget
  //     60 bodies    0.307            6.4%
  //    120 bodies    1.111           23.3%
  //    240 bodies    3.512           73.7%
  //
  // So a long phase-3 fight degrades into a slideshow, and it happens in normal play rather than in a
  // stress fixture. Expiry is by AGE, so a wall the player has not dealt with yet still goes, and
  // `BOSS_WALL_ID` is the identity that makes it findable - which is what the dead `e.wallIds` field
  // was for.
  /* SWEEP BY AGE, ALWAYS - not only when `wallBodies` is null. The handle-based expiry below can only
     police the most recent wall, because `bossCallWall` overwrites `wallBodies` on every call, so a
     wall from three moves ago is not covered by it at all. Sweeping the room each tick is what makes
     the claim true regardless of how many walls the boss has called.

     BOTH HALVES WERE NECESSARY TO TEST, and the mutations are what established it rather than an
     assumption about which one mattered:

       - delete the age sweep alone  -> suite GREEN. The handle path still cleaned up, because
         `!wallBodies.some(b=>b.hp>0)` retires a wall as soon as its bodies die.
       - delete the handle loop alone -> suite GREEN. The age sweep catches them at 14s regardless.
       - delete BOTH (the original bug) -> suite RED, 50 Brunch surviving the expiry.

     So either half is sufficient alone and the test cannot distinguish them, which is worth knowing:
     it means the assertion is pinned to the OUTCOME ("the room empties") and not to a mechanism, and
     that is deliberate - the outcome is the property, and a test that demanded one particular
     implementation would be the `eq(FRAME,FRAME)` shape this file keeps warning about. Both are kept
     because they are cheap and they retire a wall at different moments: the handle clears it the
     instant its bodies die, the sweep clears it on age whether or not anything killed it. */
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
  /* THREE MORE MOVES, AND ALL THREE ARE PHASE 3's. The braces were missing, so only the first push
     was conditional: every phase drew `volley sweep sweep` in addition to its own bag, and phase 1 -
     the one that is supposed to be the boss introducing itself - came out 60% sweep, the body-to-body
     charge, instead of the 33% the design describes. Measured over 3000 draws per phase:

       phase 1   bag 5   volley 40.0%  sweep 60.0%
       phase 2   bag 6   volley 33.3%  sweep 50.0%  wall 16.7%
       phase 3   bag 7   volley 42.9%  sweep 42.9%  wall 14.3%

     REPORT.md says 3, 4 and 7 moves. With the braces it is 3, 4 and 7 again, and the sweep is 33% in
     phase 1, 25% in phase 2 and about 43% in phase 3 - a fight that escalates by adding moves rather
     than by reweighting the ones the player has already learned to read. */
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
  // Read the player position HERE rather than taking it as an argument. The first version used `hy`
  // and `hx`, which are locals of stepBoss and resolveBoss - two other functions' variables, in a
  // third. It threw on the first volley of the first fight, which is the only place a boss is ever
  // spawned from a real room, so no amount of reading the code would have surfaced it.
  const hx=player.x, hy=player.y;
  if(move==='volley'){
    // the tell is the aim being solved and held, which is exactly what a gunner does for half a
    // second. A player who has read a gunner is already reading this.
    e.castT=CAST_TIME; e.castReady=false; e.castAim=Math.atan2(hy-e.y,hx-e.x);
    e.volleyLeft=BOSS_VOLLEY_N; e.volleyT=0;
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
        e.castT=CAST_TIME;       // re-tell between shells, so each one is answerable on its own
      }
    } else {
      e.move='idle'; e.moveT=BOSS_RECOVER;
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


function update(){
  frameCount++;
  tickFX();
  /* THE BLINK BAR'S ARRIVAL FILL, advanced here rather than down in tickBlink.

     tickBlink is inside the playing branch and update() returns early on a room transition, on the
     READY window and on death - all three of which are exactly when this needs to run. It is the
     same placement problem the ring had, and the same answer: presentation that must keep running
     through states where the simulation has stopped lives above the returns.

     It sets the real charges to full immediately and then walks the DRAWN value forward, so the
     animation cannot be interrupted or lost however the player arrives. The guard on player is not
     defensive noise: on the title screen there is no player at all, and a line placed above the
     state check has to survive the states above it. Every test in this file calls startGame()
     before it touches anything, so the title screen is the one place this could break and the one
     place no test stands - which is why there is a test that runs it with no game in it. */
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
  /* THE DEATH BACKSTOP. The lab passes through here like anything else and is protected one level
     down, at damagePlayer - which is the only place the rule could safely live, because there are
     four death checks in this file and not one of them should know the lab exists. Guarding this one
     and the one below it left the other two live, and a drove killed the lab player through one of
     those and wrote a record. See the note in damagePlayer. */
  if(player.hp<=0){ endRun(false); return; }
  // The backstop. Everything below here can return early - a room transition, the ready window, a
  // death check further down - and every one of those returns is a tick that never looks at your
  // health. So the health is checked here, before any of them can fire, as well as at the point the
  // damage lands. One check on its own is not enough: the only way to guarantee a dead player cannot
  // still be playing is to check on the way in AND on the way out, so there is no route through
  // this function that leaves a zero-health player standing. The lab passes through here like
  // anything else and is protected one level down, in damagePlayer.
  if(player.hp<=0){ endRun(false); return; }
  run.ticks++;
  // The per-floor clock, alongside the run clock and for the same reason: the summary wants to say
  // both "you died on floor 9" and "you spent 4:20 on that floor", and those are different questions.
  // It is here rather than in endRun because endRun is called from two places in this file and a
  // clock that only advances on one of them is a clock that lies.
  run.floorTicks++;
  // The descent banner ages on the same clock as the fade it is drawn on, for the same reason the
  // grace ages on its own: two numbers for one duration drift apart, and the first symptom is a
  // banner still fading in over a room the player is already being shot at.
  if(descendT>0) descendT--;
  /* THE LAB'S OWN TICK, and it is here - after the run clocks, before the transition early-returns -
     for two reasons. It has to be after the state gate so it does not tick on the title screen, and
     it has to be BEFORE the transition returns because the lab has no transitions and must therefore
     never be the reason a tick is skipped; if it were placed below them it would silently stop
     running the moment anything above it returned, which is the failure this project keeps meeting
     in a new costume.

     remember() before freeze(): a body needs somewhere to be pinned TO, and on the very first tick
     a row body has no remembered position yet. The other order works until the first tick and then
     pins everything to undefined. */
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
  // The blink grace ages on its own clock, deliberately NOT folded into iframes. They are two
  // different windows with different rules - i-frames block everything, the grace forgives exactly
  // one hit and only when something actually connects - and a tick that treated them as one number
  // would quietly restore the merged window the whole design exists to avoid.
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
      /* BRUNCH ARE COVER, and the cover has to be VISIBLE or it is a rule the player has to infer
         from damage numbers. A shell that reaches a Brunch stops there and dies, and the Brunch is
         unharmed - it is impervious, not armoured, so there is no number to grind down and no
         counterplay to work out. What the player sees is the shot they were about to eat collapsing
         into a wall, in its own colour, which is the clearest possible statement that the pack is
         doing something.

         Checked BEFORE the lungers and before the player, and that ordering is the design rather than
         an implementation detail: a shell that overlaps a Brunch and the player on the same tick
         dies on the Brunch. Cover that only works when nothing else happens to be nearby is not
         cover, it is a coincidence, and a player who learns that coincidences apply will stop
         trusting the pack the moment a gunner is also in the room.

         Player projectiles are NOT absorbed. The player has to be able to shoot through a Brunch pack
         to clear it, and the Voidball's pierce and the Bolts drilling a line both depend on it. Only
         incoming fire is eaten. */
      for(let j=r.enemies.length-1;j>=0;j--){
        const b=r.enemies[j];
        if(b.type!=='brunch'||b.hp<=0) continue;
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

  // Iterate a SNAPSHOT, not the live array. A Brunch that lands its second touch calls killEnemy,
  // which splices r.enemies out from under this loop - and a for...of over an array being spliced
  // silently skips the element that slid into the hole. With a knot of Brunch touching at once that
  // is a body which does not get its turn, does not spend itself, and never dies. Iterating a copy
  // costs one allocation and makes the kill order irrelevant.
  // Room pressure is counted ONCE here, from the snapshot, rather than per enemy: it is a property
  // of the room and recomputing it inside the loop would make a pack of eight cost eight scans.
  const roomPress=roomPressure(r.enemies.reduce((n,x)=>n+(x.hp>0?1:0),0));
  /* PACK CENTROIDS, computed ONCE per tick and before the body loop.

     A slot is a position in the pack's own frame, so the pack has to know where its own middle is
     before any of its bodies can be told where to stand. Computing that inside the per-Brunch branch
     would be O(packs x bodies) every tick to produce a number that cannot change within the tick, and
     the more expensive mistake is the subtle one: if each body recomputed the centroid AFTER the
     earlier bodies had already moved, the wall would rotate a little on every pass, because each body
     would be aiming at a middle that had drifted since it was last read. The formation would creep.

     A snapshot, not the live array, for the same reason the enemy loop below uses one: a Brunch that
     spends itself on the player is removed from the room mid-tick, and a centroid that included a
     body which is no longer there would put the wall off-centre for a frame. */
  const packC={};
  for(const b of r.enemies){
    if(b.type!=='brunch'||b.packId===undefined) continue;
    const c=packC[b.packId]||(packC[b.packId]={x:0,y:0,n:0});
    c.x+=b.x; c.y+=b.y; c.n++;
  }
  for(const k in packC){ const c=packC[k]; c.x/=c.n; c.y/=c.n; }

  /* THE PACK'S SHIELD TARGET, chosen once and committed to.

     A pack picks the nearest RANGED enemy in the room - shooter, gunner or boss - and holds that
     choice until either the pack or its target dies. The commitment is the point: a pack that
     re-picked whenever a nearer shooter walked past would swing between two enemies, and a wall that
     moves is not a wall, it is noise the player has to learn to ignore.

     ONLY RANGED. A lunger is not cover for anything, because it is already on top of the player;
     shielding one would put a wall between the player and a threat that does not threaten from range.

     The target is stored as a live body reference, not as an id or a position, so the expiry test is
     `hp <= 0` and a target removed from the room - which is how bodies die - retires it correctly. A
     stored id would dangle and the pack would walk to a point where nothing is.

     `alive` is the guard against that: a body can be spliced out of the room by a caller that never
     touches its hp, and a stale reference would point at a corpse the pack keeps shielding. */
  const RANGED={shooter:1,gunner:1,boss:1};
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
    if(!e.shieldTarget||e.shieldTarget.hp<=0||!r.enemies.includes(e.shieldTarget))
      e.shieldTarget=pickShield(c);
  }




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
    /* THE BOSS IS CHECKED FIRST, and that ordering is load-bearing rather than stylistic.

       The branch used to be `if(e.walkSpeed!==undefined){...} else if(e.type==='boss'){...}`, which
       reads perfectly and is wrong. Giving the boss walk/run in the ENEMY table - which it needs, so
       it can close distance instead of standing at the far wall - makes walkSpeed defined, so the
       shared pursue branch took it and the boss turn never ran at all. Measured: 600 ticks against a
       stationary player, zero hits, and the boss sitting at `move=idle` with bossCd still 252,
       having never been decremented once.

       Nothing about that failure looks like a boss bug. It looks exactly like a boss whose attacks
       all miss, and the first instinct is to blame the shells - which were measured hitting a
       stationary player on tick 202, perfectly. So the shells were innocent and the branch order was
       the whole of it. A type check that depends on an unrelated field in another table is a landmine
       with a plausible story attached, and the only defence is to test the type before the shape. */
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
          /* THE WALL. A Brunch with a pack steers at a SLOT rather than at the player: its position
             in the pack's own frame, across the approach vector and back along it. The result is a
             rigid body that turns to face the player as one thing, instead of eight bodies
             converging on a point - which is a crowd by geometry whatever the code intends.

             A pack too small to have a shape walks straight in. Three bodies in a row is not a wall,
             it is a queue, and turning a queue into a formation would only slow three Brunch down for
             the sake of a picture.

             THE DEAD ZONE is what stops a body already standing on its slot from jittering on the
             spot forever. It is also what lets a wall pass THROUGH a player who walks into it: the
             slot does not retreat, so a body inside the line is a body that has arrived, and arriving
             spends itself on contact the way a Brunch always has. */
          const pc=e.packId!==undefined?packC[e.packId]:null;
          let mdx=edx,mdy=edy;
          /* A PACK WITH A SHIELD TARGET FORMS THE ARC IN FRONT OF THAT TARGET, and this branch comes
             FIRST - it is the whole point of the change. Previously every pack steered at the player,
             so a wall formed beautifully in front of the wrong body: measured over 8 seconds, the
             pack sat 8-60px from the player while the shooter it should have been covering stood
             132-229px away. The shell-absorption rule already worked (verified: a shell through a
             five-body wall never reached the player and no Brunch lost HP), so all that was missing
             was something standing where the cover could be used.

             With no ranged enemy in the room the pack falls through to the two-rank wall below, which
             is the advance-on-the-player behaviour it always had. A pack with nothing to cover milling
             around a corner would be strictly worse than the bum-rush it replaces. */
          const tgt=e.shieldTarget;
          if(tgt&&tgt.hp>0&&pc&&pc.n>=BRUNCH_SHIELD_MIN){
            const slotPt=brunchArcSlot(tgt.x,tgt.y,player.x,player.y,pc.n,e.packSlot||0,tgt.r);
            if(slotPt){ mdx=slotPt.x-e.x; mdy=slotPt.y-e.y; }
          } else if(pc&&pc.n>=BRUNCH_WALL_MIN){
            // the approach vector, pack centre to player, IS the wall's normal
            const ndx=hx-pc.x, ndy=hy-pc.y, nd=Math.hypot(ndx,ndy)||1;
            const ux=ndx/nd, uy=ndy/nd;
            // two ranks, offset by half a column, so a shot that finds the gap in the front rank does
            // not find a body sitting behind it
            const cols=Math.ceil(pc.n/2);
            const rank=e.packSlot%2, col=((e.packSlot/2)|0)-(cols-1)/2;
            const sx=pc.x+(-uy)*col*BRUNCH_WALL_GAP+ux*(rank-0.5)*BRUNCH_WALL_RANK;
            const sy=pc.y+(ux)*col*BRUNCH_WALL_GAP+uy*(rank-0.5)*BRUNCH_WALL_RANK;
            mdx=sx-e.x; mdy=sy-e.y;
          }
          const md=Math.hypot(mdx,mdy);
          if(md>6){
            e.x+=mdx/md*e.curSpeed*sm; e.y+=mdy/md*e.curSpeed*sm;
          } else {
            // already on its slot: close the last few pixels toward the player, so a wall that has
            // been reached keeps advancing instead of standing there waiting to be shot
            e.x+=edx/dist*e.curSpeed*sm; e.y+=edy/dist*e.curSpeed*sm;
          }
        }
      } else { e.curSpeed=e.walkSpeed; e.pursuit=0; idleWander(e); }
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
          const dz=swerveDeadzone(), reach=Math.max(0,Math.min(1,(dist-dz)/(swerveFull()-dz)));
          const conf=Math.max(0,1-player.swerve*(rootsWhileCasting?SWERVE_TRUST_ROOTED:SWERVE_TRUST_WALKING));
          /* THE BELIEVED VELOCITY IS THE CURRENT ONE, and the bug this fixes was NOT acceleration.
             It was the LAG.

             trendV is an EMA with LUNGE_TRACK 0.014, so it lags by about 71 ticks, and the solver
             was treating that lagged value as the player's present velocity. A 400px shell is in the
             air for roughly 195 ticks and the player covers about 1.4px a tick, so seventy-one ticks
             of stale velocity is a hundred pixels of position error by the time the shell arrives.
             Measured: a straight runner at 400px was missed by a consistent 86-108px, which is that
             number and not a coincidence.

             So the prediction reads player.vx - where the player is actually going this tick - and
             the smoothing is not removed, it is DEMOTED, which is the accurate word for it. trendV's
             job was never accuracy; it was not reacting to a single tick of knockback noise, and
             `conf` already does that job and does it better, because a player who has just been
             knocked about has a high swerve read and a high swerve read scales the whole belief down.
             The noise rejection lives in the confidence and the freshness lives in the velocity; they
             were the same knob doing two jobs.

             And the effect lands where it should, which is worth checking rather than assuming. A
             COMMITTED player is helped: their velocity is steady, so reading it fresh removes a
             hundred pixels of error and they are hit again. A REVERSING player is not: a quarter-second
             reversal flips the true velocity every 26 ticks while trendV barely moves, so reading
             fresh makes the belief noisier rather than truer - and that noise is then scaled by a conf
             already near zero for a body that unsettled. The two cases separate because they are
             different signals, which is the whole reason to keep them separate. */
          const bvx=player.vx*conf, bvy=player.vy*conf;
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
  separateBodies(r.enemies);
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
    /* THE RIGHT CLICK IS A WEAPON, SO IT SWAPS LIKE ONE.

     The Hook replaces the Blast permanently: `player.altMode` is overwritten and nothing puts the Blast
     back. Weapons do not behave like that - picking up a wand drops the one you were holding, so no
     wand is ever lost - and the code already had a branch for `kind==='blast'`, which nothing in the
     game ever creates. That is a swap-back that was designed and never wired to anything, which is why
     this reads as an oversight rather than a decision.

     So the displaced Blast is dropped on the tile the Hook was standing on, exactly as a displaced item
     or a displaced wand is, and `hold` stops the next tick handing it straight back. The secret room
     still offers the Hook; it just no longer costs the Blast. */
    else if(pk.kind==='hook'){
      if(player.altMode!=='hook'){
        player.altMode='hook'; run.hook=true;
        r.pickups.push({x:pk.x,y:pk.y,r:16,kind:'blast',hold:true});
      } else run.hook=true;
    }
    else if(pk.kind==='blast'){ player.altMode='blast'; run.hook=false; }
    /* An item goes in the slot it declares, and whatever was on that key is left where the new one was
       standing - the same swap a weapon gets, and for the same reason: a limit on what a build may
       carry is a statement about which combinations are worth having, and this game wants all of them.
       `hold` is what stops the swap running away: the displaced item is dropped on the tile the player
       is standing on, so without it the next tick would hand it straight back. */
    else if(pk.kind==='item'){
      /* A DISPLACED ITEM IS DROPPED WITH THE CHARGES IT HAD, not the ones its definition says.

         `give` is what removes the outgoing item, so the caller cannot read its charges afterwards -
         there is nothing left to read. The floor pickup was therefore built from the id alone, and the
         next pickup rebuilt it from `d.charges`: a Tin Cup drained to one charge, swapped for a Bone
         Whistle and picked back up came back at three. With two actives in a room that is unlimited
         healing, and it is found by accident rather than by cheating.

         So the charges travel with the drop, and travel with the pickup back in, and `give` reads a
         missing value as "a full one" so that a plain spawn is unaffected. */
      const got=Items.give(pk.id,pk.charges);
      if(!got.taken) continue;              // only a duplicate passive, and the pool never offers one
      if(got.dropped)
        r.pickups.push({x:pk.x,y:pk.y,r:16,kind:'item',id:got.dropped,hold:true,charges:got.droppedCharges});
    }
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
  /* THE LAB'S READOUT, at the very end, and the position is the whole argument.

     It compares each body's health with the tick before, so it has to run after everything that can
     have damaged something - the enemy pass, the projectile pass, the weapon pass, the trap pass.
     Put it anywhere earlier and it reports the damage dealt on the PREVIOUS tick, which is a number
     that is one tick stale and therefore wrong exactly when it matters: a burst that lands on one
     tick would be attributed to the next, and a reader comparing the number against the flash would
     find them a tick apart and conclude the readout is broken. */
  if(state==='dev'){ Lab.tickNumbers(); Lab.tickShelf(); }
}

/* The way out. It is a pickup slot so that the existing touch-to-collect code carries it, but it is
   not an item: it cannot be picked up, moved, or missed by accident, and walking into it is the
   only way to trigger it. It sits dead centre because a door in a corner is a thing you can miss on
   the way past, and this is a thing you should be able to see from the far side of the room. */
