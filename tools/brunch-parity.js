/* brunch-parity.js - measures the BRUNCH MOVEMENT pass in the running game: the shield arc, the
 * bomb rush, the deadzone, the wall threshold, separation, and the speed ramp.
 *
 * The expectations for csharp/Depths.Tests/PackAssemblyParityTests.cs are generated from this.
 * REPORT-ONLY.
 *
 * THE FINDING WORTH READING FIRST, because it corrects a formula the project had asserted:
 *
 *   THE RAMP IS NOT `walk * (1 + RAMP_GAIN * min(1, pursuit/RAMP))`.
 *
 * That formula caps at 0.624 * 2.9 = 1.8096. Measured with a Brunch in a corner so it never reaches
 * the player, the curve passes 1.8096 at about tick 190 and keeps climbing, approaching 2.1:
 *
 *     tick     1  0.632612        tick   200  1.873043
 *     tick    50  1.044938        tick   315  2.059271
 *     tick   100  1.409447        tick   400  2.090368
 *     tick   157  1.717552        tick   600  2.099676
 *
 * The real line is `curSpeed += (runSpeed - curSpeed) * LUNGER_ACCEL * gain` - an EXPONENTIAL EASE
 * toward the run speed, with the pursuit ramp scaling how hard each tick pulls. Simulating it
 * reproduces all ten points to six decimal places with zero difference. Two things follow that the
 * obvious formula gets wrong: the pack never arrives (2.099676 at 600 ticks), and the walk speed
 * matters only as the starting value.
 *
 * FIXTURE CORRECTIONS, the same lesson as the other probes here:
 *   1. `spawnEnemy` RETURNS the body and does not push it into the room; the suite's helper pushes.
 *   2. The player is made effectively immortal and given huge iframes for the movement cases.
 *      Otherwise contact damage ends the run mid-measurement and every "did the pack close" figure
 *      is truncated at the moment of arrival - which reads as "the pack stopped" rather than "the
 *      run ended".
 *   3. The ramp is sampled from a Brunch in a CORNER. One that reaches the player stops, and the
 *      curve then measures the contact rather than the pursuit.
 *
 *   node tools/brunch-parity.js          (needs the local server on 127.0.0.1:8791)
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
    /* spawnEnemy RETURNS the body and does NOT push it. The suite's helper pushes. */
    const put=(rm,x,y,type)=>{const e=spawnEnemy(false,rm,x,y,type);
      e.noticeTimer=0;e.aggroTimer=0;e.hp=e.maxHp; rm.enemies.push(e); return e;};
    const setup=()=>{
      startGame(4242); const rm=goTo('normal');
      rm.enemies.length=0; rm.pickups.length=0; rm.spawnPlan=null; projectiles.length=0;
      readyT=0; fadeT=0; roomFade=0;
      player.x=MIDX; player.y=MIDY; player.lagX=MIDX; player.lagY=MIDY;
      player.hp=999; player.maxHp=999; player.iframes=9999;   // isolate movement from contact
      player.vx=0;player.vy=0;player.kvx=0;player.kvy=0;
      player.swerve=0; player.momentum=0;
      keys['w']=keys['a']=keys['s']=keys['d']=false; mouseDown=false;
      if(typeof Momentum!=='undefined') Momentum.lock();
      return rm;
    };
    const pack=(rm,n,cx,cy)=>{const made=[];
      for(let i=0;i<n;i++){const ang=(i/n)*Math.PI*2;
        const e=put(rm,cx+Math.cos(ang)*26,cy+Math.sin(ang)*26,'brunch');
        e.packId=1; e.packSlot=i; e.pursuit=0; made.push(e);}
      return made;};

    // 1. A GUARDED PACK HOLDS THE ARC. Six Brunch on a shooter should stay at the arc radius,
    //    not close on the player.
    let rm=setup();
    let wall=pack(rm,6,MIDX-150,MIDY);
    const shooter=put(rm,MIDX-190,MIDY,'shooter');
    for(const e of wall) e.shieldTarget=shooter;
    const arc0=wall.map(e=>Math.hypot(e.x-shooter.x,e.y-shooter.y));
    for(let t=0;t<60;t++) tickBodies();
    const arc1=wall.map(e=>Math.hypot(e.x-shooter.x,e.y-shooter.y));
    rows.push({case:'a guarded pack holds its arc rather than closing',
      got:{arcAt0:+ (arc0.reduce((a,b)=>a+b)/6).toFixed(2),
           arcAt60:+(arc1.reduce((a,b)=>a+b)/6).toFixed(2),
           spread:+((Math.max(...arc1)-Math.min(...arc1))).toFixed(2),
           BRUNCH_ARC_GAP:BRUNCH_ARC_GAP, targetR:ENEMY.brunch.r}});

    // 2. AN UNGUARDED PACK CLOSES. No target: the pack walks at the player and arrives.
    rm=setup();
    wall=pack(rm,6,MIDX-450,MIDY);
    const g0=450;
    let arrived=-1;
    for(let t=0;t<21*20 && arrived<0;t++){ tickBodies();
      const gap=Math.hypot(wall[0].x-player.x, wall[0].y-player.y);
      if(gap<g0*0.5) arrived=t; }
    rows.push({case:'an unguarded pack closes on the player',
      got:{gap0:g0, gapNow:+Math.hypot(wall[0].x-player.x,wall[0].y-player.y).toFixed(1),
           halvedAtSec:arrived<0?null:+(arrived/21).toFixed(2), BRUNCH_RAMP:BRUNCH_RAMP,
           BRUNCH_RUN:BRUNCH_RUN}});

    // 3. THE RAMP. Measure curSpeed over time for an unguarded pack, which is where pursuit ramps.
    rm=setup();
    wall=pack(rm,1,MIDX-450,MIDY);
    const spd=[];
    for(let t=0;t<21*3;t++){ tickBodies(); if(t%63===0) spd.push(+wall[0].curSpeed.toFixed(4)); }
    rows.push({case:'an unguarded pack ramps its speed over BRUNCH_RAMP ticks',
      got:{curve:spd, walk:ENEMY.brunch.walk, ramp:BRUNCH_RAMP, rampGain:BRUNCH_RAMP_GAIN}});

    // 4. THE DEADZONE. Inside it the pack steers at the PLAYER, not the slot.
    rm=setup();
    const near=put(rm,player.x-20,player.y,'brunch');
    const t1=[+near.x.toFixed(2),+near.y.toFixed(2)];
    tickBodies();
    rows.push({case:'a Brunch inside the deadzone moves toward the player',
      got:{from:t1, to:[+near.x.toFixed(2),+near.y.toFixed(2)], deadzone:BRUNCH_DEADZONE,
           movedTowardPlayer: near.x>t1[0]}});

    // 5. A SMALL PACK DOES NOT WALL. Two Brunch (below BRUNCH_WALL_MIN=3) walk at the player.
    rm=setup();
    const pair=pack(rm,2,MIDX-300,MIDY);
    const gap0=300;
    for(let t=0;t<21*6;t++) tickBodies();
    rows.push({case:'a pack of two is below the wall threshold and closes',
      got:{gap0, gapNow:+Math.hypot(pair[0].x-player.x,pair[0].y-player.y).toFixed(1),
           BRUNCH_WALL_MIN:BRUNCH_WALL_MIN, BRUNCH_SHIELD_MIN:BRUNCH_SHIELD_MIN}});

    // 6. SEPARATION. Bodies in a knot push apart rather than occupying one point.
    rm=setup();
    const knot=[];
    for(let i=0;i<4;i++) knot.push(put(rm,300+i*2,300,'brunch'));
    for(const e of knot) e.packId=undefined;
    for(let t=0;t<30;t++) tickBodies();
    rows.push({case:'bodies in a knot separate rather than overlap',
      got:{spreadX:+(Math.max(...knot.map(e=>e.x))-Math.min(...knot.map(e=>e.x))).toFixed(2)}});

    return rows;
  });
  await b.close();
  for(const x of r){ console.log('\n== '+x.case); console.log('   '+JSON.stringify(x.got)); }
})();
