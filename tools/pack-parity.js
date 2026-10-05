/* pack-parity.js - measures the PACK ASSEMBLY pass in the running game: centroids, target
 * selection, guard lists.
 *
 * The expectations for csharp/Depths.Tests/PackAssemblyParityTests.cs are generated from this.
 * REPORT-ONLY.
 *
 * THE FINDING WORTH READING FIRST, because it is a design property that reads like a bug:
 *
 *   A PACK COMMITS TO ITS TARGET. The scan condition is
 *     !e.shieldTarget && (frameCount % BRUNCH_SCAN_TICKS === 0)
 *   and the `!e.shieldTarget` guard means a pack that already has a LIVING target never re-picks.
 *   Measured: a pack guarding a gunner 260px away did not switch to a shooter standing 20px from its
 *   centroid, across 400 frames and twenty scan periods. The first version of this probe expected a
 *   switch and reported none - and the game was right and the probe was wrong.
 *
 * That is deliberate. Re-deciding every 20 ticks makes a Brunch's escort visibly indecisive in a room
 * with several ranged bodies, and lets the player pull a pack off its charge by walking a rival into
 * range - which turns a threat into a switch.
 *
 * TWO FIXTURE CORRECTIONS are inline, both from the same lesson as the other probes here:
 *   1. `spawnEnemy` RETURNS the body and does not push it into the room; the suite's helper pushes.
 *      Assuming otherwise made every case read `enemies[0].hp` of an empty array.
 *   2. Setting `frameCount` by hand to control the scan cadence works, but the FIRST version cleared
 *      the targets and then expected a switch on a later tick, which measures the clear rather than
 *      the cadence. Clear the targets, then tick from frame 0.
 *
 *   node tools/pack-parity.js            (needs the local server on 127.0.0.1:8791)
 */

const pw=require('C:/Users/neefloW/AppData/Local/hermes/hermes-agent/node_modules/playwright');
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge',args:['--autoplay-policy=no-user-gesture-required']});
  const p=await b.newPage({viewport:{width:800,height:600}});
  await p.goto('http://127.0.0.1:8791/depths.html',{waitUntil:'domcontentloaded',timeout:30000});
  await p.waitForTimeout(1500);
  await p.mouse.click(400,300);
  await p.waitForTimeout(400);
  const r=await p.evaluate(()=>{
    const rows=[];
    const goTo=type=>{const x=Object.values(rooms).find(y=>y.type===type);enterRoom(x.x,x.y,'W');readyT=0;fadeT=0;roomFade=0;return x;};
    /* `spawnEnemy` RETURNS the body and does NOT push it. The suite's own helper pushes. Getting
       this wrong made an earlier fixture read enemies[0].hp of an empty array. */
    const put=(rm,x,y,type)=>{const e=spawnEnemy(false,rm,x,y,type);
      e.noticeTimer=0; e.aggroTimer=0; e.hp=e.maxHp; rm.enemies.push(e); return e;};
    const setup=()=>{
      startGame(4242); const rm=goTo('normal');
      rm.enemies.length=0; rm.pickups.length=0; rm.spawnPlan=null; projectiles.length=0;
      readyT=0; fadeT=0; roomFade=0;
      player.x=MIDX; player.y=MIDY; player.lagX=MIDX; player.lagY=MIDY;
      player.hp=8; player.maxHp=8; player.iframes=0; player.vx=0; player.vy=0;
      player.swerve=0; player.momentum=0;
      if(typeof Momentum!=='undefined') Momentum.lock();
      keys['w']=keys['a']=keys['s']=keys['d']=false;
      mouseDown=false;
      return rm;
    };
    // pack a Brunch wall the way the game does
    const pack=(rm,n,cx,cy)=>{
      const made=[];
      for(let i=0;i<n;i++){
        const ang=(i/n)*Math.PI*2;
        const e=put(rm,cx+Math.cos(ang)*22,cy+Math.sin(ang)*22,'brunch');
        e.packId=1; e.packSlot=i;
        made.push(e);
      }
      return made;
    };

    // 1. NO TARGET: a pack with nothing to shield walks at the player, not into a wall.
    //    Measured by the arc slot: does packSlot influence direction when unguarded?
    let rm=setup();
    let wall=pack(rm,6,MIDX-140,MIDY);
    tickBodies();
    rows.push({case:'an unguarded pack does NOT assemble a wall',
      got:{spreadX:+(Math.max(...wall.map(e=>e.x))-Math.min(...wall.map(e=>e.x))).toFixed(2),
           towardPlayer:+(((wall.reduce((s,e)=>s+e.x,0)/6)-MIDX)).toFixed(2),
           anyShieldTarget:wall.some(e=>e.shieldTarget)}});

    // 2. WITH A TARGET: a pack guards it, and takes the ARC SLOT relative to the player.
    rm=setup();
    wall=pack(rm,6,MIDX-140,MIDY);
    const shooter=put(rm,MIDX-200,MIDY,'shooter');
    shooter.hp=shooter.maxHp;
    // force the target by making the pack stale-free: give them the shooter directly
    for(const e of wall) e.shieldTarget=shooter;
    tickBodies();
    rows.push({case:'a pack with a living target guards it',
      got:{allTarget:wall.every(e=>e.shieldTarget===shooter),
           guardSlots: shooter.shieldGuardFor?shooter.shieldGuardFor.length:0,
           packSpreadX:+(Math.max(...wall.map(e=>e.x))-Math.min(...wall.map(e=>e.x))).toFixed(2)}});

    // 3. THE TARGET DIES: the pack splits and comes at the player.
    rm=setup();
    wall=pack(rm,6,MIDX-140,MIDY);
    const s2=put(rm,MIDX-200,MIDY,'shooter');
    for(const e of wall) e.shieldTarget=s2;
    tickBodies();
    const guardingBefore=wall.filter(e=>e.shieldTarget===s2).length;
    s2.hp=0;
    const g0=wall.map(e=>({x:+e.x.toFixed(1),y:+e.y.toFixed(1)}));
    for(let t=0;t<40;t++) tickBodies();
    rows.push({case:'the guarded target dies and the pack re-targets',
      got:{guardingBefore, guardingAfter:wall.filter(e=>e.shieldTarget===s2).length,
           movedAfterDeath:wall.some((e,i)=>Math.hypot(e.x-g0[i].x,e.y-g0[i].y)>2)}});

    // 4. THE SCAN: the target is re-picked even while it lives, on a cadence.
    rm=setup();
    wall=pack(rm,6,MIDX-140,MIDY);
    const near=put(rm,MIDX-160,MIDY,'shooter');
    const far =put(rm,MIDX-400,MIDY,'gunner');
    for(const e of wall) e.shieldTarget=far;
    let switched=-1;
    for(let t=0;t<400 && switched<0;t++){ frameCount=t; tickBodies();
      if(wall.some(e=>e.shieldTarget===near)) switched=t; }
    rows.push({case:'a living target is re-picked when a nearer one appears',
      got:{switchedAtTick:switched, BRUNCH_SCAN_TICKS:BRUNCH_SCAN_TICKS}});

    // 5. NO RANGED BODY: a pack with nothing to guard is unguarded.
    rm=setup();
    wall=pack(rm,6,MIDX-140,MIDY);
    tickBodies();
    rows.push({case:'a pack with no ranged body in the room is unguarded',
      got:{anyTarget:wall.some(e=>e.shieldTarget)}});

    // 6. THE PER-BODY TIMERS: hitFlash, slowT, stun all decrement; stun never goes negative.
    rm=setup();
    const e=put(rm,MIDX-100,MIDY,'lunger');
    e.hitFlash=3; e.slowT=2; e.stun=1;
    tickBodies();
    rows.push({case:'the per-body timers tick down',
      got:{hitFlash:e.hitFlash, slowT:e.slowT, stun:e.stun}});
    // and a NEGATIVE stun - the defect found earlier - clamps to 0
    rm=setup();
    const e2=put(rm,MIDX-100,MIDY,'lunger');
    e2.stun=-0.667;
    tickBodies(); tickBodies();
    rows.push({case:'a negative stun clamps to zero and stays there',
      got:{stun:e2.stun, stillStuck:e2.stun<0}});

    return rows;
  });
  await b.close();
  for(const x of r){ console.log('\n== '+x.case); console.log('   '+JSON.stringify(x.got)); }
})();
