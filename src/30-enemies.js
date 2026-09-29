function spawnEnemy(boss,room,x,y,type){
  const rx=()=>ROOM_LEFT+SPAWN_MARGIN+Math.random()*(ROOM_RIGHT-ROOM_LEFT-SPAWN_MARGIN*2);
  const ry=()=>ROOM_TOP+SPAWN_MARGIN+Math.random()*(ROOM_BOTTOM-ROOM_TOP-SPAWN_MARGIN*2);
  const base={x:x===undefined?rx():x,y:y===undefined?ry():y,hitFlash:0,idleDir:[0,0],idleTimer:(Math.random()*WANDER_TICKS)|0,
    noticeTimer:(Math.random()*16*SPEEDUP)|0,anim:0,kvx:0,kvy:0,stun:0,slowT:0,alerted:false,dodgeCd:0,pursuit:0};
  const c=ENEMY[boss?'boss':type==='shooter'||type==='gunner'?type:type==='brunch'?'brunch':'chaser'];
  const e=Object.assign(base,{type:boss?'boss':type||'chaser',mass:c.mass,r:c.r,art:c.art,bar:c.bar,hp:c.hp,maxHp:c.hp,armour:c.armour||1});
  if(c.base!==undefined) e.speed=c.base*PRESSURE.rate;
  if(boss){e.aggroTimer=9999;return e;}
  // chasers and Brunch are the same shape of body: they walk at you and hit you on contact. only
  // the two gunners carry a ranged kit
  if(c.walk!==undefined){e.curSpeed=c.walk;e.walkSpeed=c.walk;e.runSpeed=c.run;e.aggroTimer=0;
    // chaser-only lunge state. Brunch do not get it: they are the pressure, and a pack that also
    // telegraphs is a room with nothing left to read
    e.lungeState='approach'; e.lungeT=0; e.lungeCd=0; e.lungeDx=0; e.lungeDy=0;
  e.hookStacks=0; e.hookCalm=0; e.hookMark=0;
  /* Flank slot. Walked round the circle by the golden angle, so no two bodies ever share a slot
     however many have spawned - and NOT randomised, because two chasers drawing the same angle out
     of a bag is a front half the time. The angle is fixed for the body's life: a chaser that
     changed its mind about which side of the player it wanted would weave through its own pack on
     the way, which reads as indecision rather than as pressure. */
  e.flank=FLANK_CURSOR;
  FLANK_CURSOR+=2.399963229728653;
  if(FLANK_CURSOR>6.283185307179586) FLANK_CURSOR-=6.283185307179586;
    e.lungeFromX=x===undefined?0:x; e.lungeFromY=y===undefined?0:y;
    e.lungeChargeFx=0; e.lungeTrail=0;
    return e;}
  e.range=c.range; e.sense=c.sense; e.close=c.close; e.far=c.far; e.cdMin=c.cdMin/PRESSURE.rate; e.cdVar=c.cdVar/PRESSURE.rate;
  e.castT=0; e.castReady=false; e.castAim=0;
  e.dmg=c.dmg; e.pspd=c.pspd; e.pr=c.pr; e.pcol=c.pcol; e.aggroTimer=0;
  e.shootCd=e.cdMin+Math.random()*e.cdVar;
  return e;
}

/* The chaser's whole attack. Four states, and the transitions between them are the fight.
   This is deliberately a function of the body and not of a timer, so a stunned, slowed or knocked
   body stops mid-lunge and resumes from where it was rather than snapping back to "walking at you".
   A committed attack the player can interrupt by hitting the chaser once is a different weapon from
   one that always plays out on schedule, and the interruptible one is the one worth having. */
/* Where a body leaving a chaser's position, at lunge speed and after a windup it spends PLANTED,
   can first reach a player who is already moving at their current velocity - solved, not guessed.

   The old lunge aimed at the player's position plus a lead worth about thirty pixels, and stopped
   at a hundred and sixty six. A player running in a straight line away from a chaser settles about
   a hundred and thirty six pixels out, and from there the shot that actually catches them is two
   hundred and ninety four long. So the old lunge fell one hundred and twenty eight pixels short
   every time it was used: not a hard attack that can be read and beaten, but one that could not
   land at all. That is why running in a straight line was free, and it was arithmetic rather than
   feel - the numbers are in the lunge's own comment history.

   It has to be iterated because the time to contact and the distance to be covered each depend on
   the other. Three passes converge: the second is already within a pixel and the third moves
   nothing measurable. */
function solveIntercept(e,gx,gy){
  /* The velocity the chaser BELIEVES, which is not the velocity the player has.

     Instantaneous velocity is a bad thing to aim from, and not only because it is noisy. A player
     one tick into pressing a key is still nearly stationary, so a chaser that reads the raw number
     commits to a point that stops being true immediately - and a player mid-reversal is read at
     whatever the easing happens to be sitting on that tick. So it reads a smoothed heading, and it
     reads it in proportion to how SETTLED the player looks, using the same swerve the gunners use to
     widen their aim. One signal, two consumers, and it means the two gunners agree about the player.

     The consequence is the point. Hold a line and you are read in full, so the intercept is right
     and the lunge lands. Start reversing and the chaser loses confidence in you, the solution
     shortens, and the lunge it commits to is a smaller one - which the player can then slip. Baiting
     is not free, it is a downgrade: you get a weaker attack instead of no attack. */
  const conf=LUNGE_CONF_MIN+(1-LUNGE_CONF_MIN)*(1-player.swerve);
  const bvx=player.trendVx*conf, bvy=player.trendVy*conf;
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
      /* THE STANDOFF, which is what makes this mechanic a mechanic. A chaser that simply walks at
         the player ends up touching them - its approach speed is faster than theirs at range - and
         then "lunges" from zero distance, where no read is worth anything because there is nothing
         left to dodge. Every measurement of this attack came out a hundred percent for that reason,
         and not because the prediction was any good.

         So it holds at LUNGE_HOLD, the way the gunner holds inside its close/far band, and the lunge
         is what crosses the gap. That makes it a real committed attack: the line is drawn across open
         floor, the player has room to answer it, and the answer is a change of heading rather than a
         hope. It gives the kite back too - a body that never closes the last seventy pixels cannot
         be walked into.

         Inside the hold it brakes rather than reversing. Reversing was the first version and it
         backed five chasers into each other while they ringed a stationary player, which the
         separation pass then had to fight. A brake stops the body at the line and leaves spacing to
         the pass that already owns it.
      /* SPREAD, on the same two lines of code. Every chaser used to steer at the player's exact
         position, so a pack arrived as a single front: one line, one angle, one threat to read. Each
         body now carries a flank angle, assigned at spawn and spread around the circle by the golden
         angle, and it walks that angle around the player.

         The walk is PURELY ANGULAR - a step along the tangent, in the direction that reduces the gap
         between the body's current bearing and its slot, at a fixed size. Three earlier versions
         failed here and all three were the same mistake in different clothes:

           steering at a fixed point on a ring, only while closing, so the instant a body crossed the
             hold line and began braking, the steering went to zero with it - the pack still arrived
             with its tightest pair FOUR DEGREES apart, which is a front;
           scaling the slide by the closing speed, which is likewise zero inside the hold - ELEVEN
             degrees, still a front;
           steering at "the point at MY distance, at my slot's angle", which is tangential only
             while the bearing means something. At eight pixels the tangent is indistinguishable
             from straight at the player, so the slide became a closing force and the pack collapsed
             onto the target - measured, eight pixels.

         A step along the tangent cannot become radial at any distance. Distance is the standoff's
         job; the angle is this one's.

         And it only runs when there is more than one of them. A lone chaser circling to its slot
         before it commits makes the FIRST lunge of a fight late for no reason - there is nothing to
         be spread out from - and it measured worse on its own, badly enough to fail two tests: the
         lunge it set up after circling was much harder to answer than one set up coming straight in.
         Spacing is a pack problem, so it is solved as one. */
    if(room.enemies.length>1){
      const bearing=Math.atan2(e.y-player.y, e.x-player.x);
      let err=e.flank-bearing;
      while(err>Math.PI) err-=2*Math.PI;
      while(err<-Math.PI) err+=2*Math.PI;
      const side=err>=0?1:-1;
      e.x+=-Math.sin(bearing)*side*CHASER_SPREAD; e.y+=Math.cos(bearing)*side*CHASER_SPREAD;
    }
      if(dist<LUNGE_HOLD){
        e.curSpeed+=(0-e.curSpeed)*CHASER_ACCEL*8;    // close enough to lunge; must not walk in
      } else {
        e.curSpeed+=((approachSpeed(dist)*sm)-e.curSpeed)*CHASER_ACCEL*6;
        e.x+=ux*e.curSpeed; e.y+=uy*e.curSpeed;
      }
      if(e.lungeCd<=0){
        /* The commitment is made HERE, at the start of the windup, and never revised. What it aims
           at is the INTERCEPT - not the player, and not a short lead in their direction. The answer
           to "where will you be" is where you are going, and the whole mechanic is in that:

             running in a straight line   you are already standing on the solution, so the lunge
                                         arrives. Indifference is what kills you.
             changing your heading        you step off a line that was drawn for the old one. The
                                         chaser is committed to a line you are no longer on, and
                                         it goes past you like a train.

           The length is the solution as well, so it arrives exactly rather than approximately, and
           it is capped: a chaser whose solution is out of reach does not commit, it keeps closing.
           That is what lets it wait. A player who is far away and running gets closed on at the
           approach speed until the solution fits inside the reach, and only then is the lunge set
           up - so distance is never the reason the attack does not happen, only the reason it has
           not started.

           And the drawn line shows the SOLUTION, not the player, which is the tell that matters. A
           line that points past you is a line that will hit you if you keep going. */
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
      // planted. it does not creep forward, because a chaser that keeps walking through its own
      // windup closes the gap it just told the player it was going to stop at
      e.curSpeed=0;
      e.lungeT--;
      // No stretching, and no re-aiming. There used to be both: the length used to ease outward
      // toward the player while it charged, as a stand-in for aiming properly. Solving the
      // intercept makes both unnecessary - the length is already the distance that lands, so there
      // is nothing left to correct for, and a tell that grows after it has been drawn is a lie
      // about the one number the player is being asked to react to.
      if(--e.lungeChargeFx<=0){ e.lungeChargeFx=sec(0.06); dashFX.push({x:e.x,y:e.y,life:LUNGE_CHARGE_TRAIL,charge:1}); }
      if(e.lungeT<=0){ e.lungeState='lunge'; e.lungeT=Math.ceil(e.lungeLen/LUNGE_SPEED); e.lungeTrail=0; }
      break;
    }
    case 'lunge':{
      e.x+=e.lungeDx*LUNGE_SPEED*sm; e.y+=e.lungeDy*LUNGE_SPEED*sm;
      e.lungeT--;
      /* Two chasers on the same line meet mid-charge and both come off worse. This is the player's
         reward for it: a lunge is aimed at a position, so two bodies reading the same player at the
         same instant are aimed at the same point, and a player who puts themselves between them gets
         a gap and a stunned attacker for free.

         It is checked here rather than left to the separation pass because separation only pushes
         overlapping bodies apart gently, and a lunge moves at nearly four times the player's speed -
         gentle is not enough, and a lunge that phase-straight-throughs a body it is aiming past is
         the one case where two bodies being in the same place should cost something. Both go into
         recover, because neither has the ground under its charge any more. */
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
      e.curSpeed+=((e.walkSpeed*0.5*sm)-e.curSpeed)*CHASER_ACCEL*4;
      e.x+=e.lungeDx*e.curSpeed; e.y+=e.lungeDy*e.curSpeed;
      if(--e.lungeT<=0){ e.lungeState='approach'; e.lungeCd=LUNGE_CD; }
      break;
    }
  }
  // a lunge that runs into a wall is over, not a chaser that grinds along it. without this a chaser
  // cornered against the left wall teleports through it on the far side
  if(e.lungeState==='lunge'){
    const cx=Math.max(ROOM_LEFT,Math.min(ROOM_RIGHT,e.x)), cy=Math.max(ROOM_TOP,Math.min(ROOM_BOTTOM,e.y));
    if(cx!==e.x||cy!==e.y){ e.x=cx; e.y=cy; e.lungeState='recover'; e.lungeT=LUNGE_RECOVER; }
  }
}
function idleWander(e){  e.idleTimer--;
  if(e.idleTimer<=0){e.idleDir=[Math.random()-0.5,Math.random()-0.5];e.idleTimer=WANDER_TICKS/2+((Math.random()*WANDER_TICKS/2)|0);}
  const len=Math.hypot(e.idleDir[0],e.idleDir[1])||1;
  e.x+=e.idleDir[0]/len*WANDER_SPEED; e.y+=e.idleDir[1]/len*WANDER_SPEED;
}

// n distinct weapons, none of them the one being held
function pickWeapons(n,exclude){
  return shuffle(WEAPONS.map((_,i)=>i).filter(i=>i!==exclude)).slice(0,n);
}

function enterRoom(nx,ny,fromDir){
  cur={x:nx,y:ny};
  entryDir=fromDir;
  const r=rooms[key(nx,ny)];
  r.visited=true;
  projectiles.length=0;
  // by the time it is first walked into, and a reward room must not turn out to have furniture in it
  if(!r.spawned){
    r.spawned=true;
    if(r.type==='normal') spawnWave(r,fromDir);
    else if(r.type==='boss') r.enemies.push(spawnEnemy(true,r));
    else if(r.type==='secret') r.pickups.push({x:MIDX,y:MIDY,r:16,kind:'hook'});
    else if(r.type==='item'){
      const [w1,w2]=pickWeapons(2,player.weaponIdx);
      r.pickups.push({x:MIDX-110,y:MIDY,r:16,kind:'weapon',w:w1});
      r.pickups.push({x:MIDX+110,y:MIDY,r:16,kind:'weapon',w:w2});
    }
  }
  if(fromDir==='N'){player.x=MIDX;player.y=ROOM_TOP+34;}
  else if(fromDir==='S'){player.x=MIDX;player.y=ROOM_BOTTOM-34;}
  else if(fromDir==='E'){player.x=ROOM_RIGHT-34;player.y=MIDY;}
  else {player.x=ROOM_LEFT+34;player.y=MIDY;}
  player.vx=player.vy=player.kvx=player.kvy=0;
  player.lagX=player.x; player.lagY=player.y;
  const uncleared=r.enemies.length>0;
  // Walking into a live room hands back everything you spent getting there, so the corridor between
  // fights costs you nothing and a fight always opens with the whole kit. r.armed is the whole
  // anti-exploit: the refill is a one-shot per room, so backing out of a room you have not cleared
  // and stepping back in cannot farm it, and a room that is already quiet has nothing to arm you for.
  if(uncleared&&!r.armed){
    r.armed=true;
    const wp=WEAPONS[player.weaponIdx];
    player.cooldown=0; player.cooldownMax=wp.cooldown/TEMPO.rate;
    player.altCooldown=0; player.altCooldownMax=activeAlt().cooldown/TEMPO.rate;
    player.blinkCharges=2; player.blinkRegen=0;
  }
  roomFade=1; fadeTicks=uncleared?READY:FADE_CLEAR; fadeT=fadeTicks; readyT=uncleared?READY:0;
}

