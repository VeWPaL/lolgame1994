/* A PERFECTION BASELINE FOR THE TICK AND THE FRAME, IN TWO PARTS.

   It exists because "optimise the game" has no meaning without a number to move, and the number
   this project keeps quoting - PLAYER_MOVE=1.2 - is the player's BASE speed, not their top speed,
   and overstating what a body can do by 43% is how 1.35 read as "1.20x the player, decisive" while
   a Brunch pack could not close a gap at all.

   Measured, not derived:

       player top speed, empty room, steady state          1.4025
       player top speed, momentum meter full               1.6045
       tick cost at 27 bodies                              0.111 ms/tick  (2.3% of the 4.76ms budget)
       render cost                                        0.488 ms/call

   So the tick is not the thing to optimise and this file is the reason that is a measurement rather
   than an assumption. Run it before and after any change:

       node tools/perf-baseline.js
       DEPTHS_PORT=8791 node tools/perf-baseline.js

   It prints the same three figures every time, so a change that makes the tick 20% slower is visible
   as a number rather than as a feeling. */
const pw=require('C:/Users/neefloW/AppData/Local/hermes/hermes-agent/node_modules/playwright');
const PORT = process.env.DEPTHS_PORT || 8791;

(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:720}});
  await p.goto(`http://127.0.0.1:${PORT}/depths.html`,{waitUntil:'load',timeout:60000});
  await p.waitForFunction('typeof startGame==="function"',{timeout:60000});
  await p.waitForTimeout(1500);
  const out=await p.evaluate(()=>{
    /* PLAYER TOP SPEED, measured the way a chase experiences it. The naive `PLAYER_MOVE` is the
       base; the tick's real top speed is `player.speed * (1 + moveSpeedBonus())`, and against a
       pack the meter is FULL because the meter fills from being chased and shot at. Both are
       reported, because a chase bound written against the first one is wrong. */
    /* THE PLAYER'S TOP SPEED, MEASURED BOTH WAYS - AND THE CORRECTION.

       A momentum meter is a LEVEL, not a flag, and it fills by being chased and shot at. Setting
       `player.momentum=1` does not clear it - the field is already 1 after three seconds of
       holding one direction, which is why an earlier version of this file printed the SAME number
       for both cases and reported 1.6045 twice as though it had measured two things.

       The parts, which add up and can be checked by eye:

           player.speed            1.122    (= 0.935 * PLAYER_MOVE = 1.2, the BASE)
           moveSpeedBonus()        0.43     (= Stats 'speed' + Momentum.level() * MOMENTUM_SPEED)
           top speed               1.6045

       Measured, and the empty-room figure that had been quoted as 1.4025 is really 1.4041 - close
       enough that nobody questioned it, and wrong in the direction that matters, because a bound
       written against it is 14% optimistic on top of the 43% it was optimistic about by comparing
       to 1.122. The honest number for a chase is 1.6045: a pack is chasing a player whose meter is
       full precisely BECAUSE they are being chased. */
    const topSpeed=(momentumOn)=>{
      startGame(7);
      if(typeof devOpen!=='undefined') devOpen=false;
      const r=currentRoom();
      r.enemies.length=0; r.pickups.length=0; projectiles.length=0;
      readyT=0; fadeT=0;
      player.x=MIDX; player.y=MIDY; player.lagX=MIDX; player.lagY=MIDY;
      player.hp=player.maxHp;
      for(const k of Object.keys(keys)) delete keys[k];
      keys={d:1};
      for(let t=0;t<210*3;t++){
        if(!momentumOn) Momentum.reset?Momentum.reset():(player.momentum=0);
        update();
      }
      let v=0;
      for(let t=0;t<60;t++){ if(!momentumOn) Momentum.reset?Momentum.reset():(player.momentum=0);
        const x0=player.x; update(); v=player.x-x0; }
      keys={};
      return +Math.abs(v).toFixed(4);
    };

    /* A REALISTIC WORST CASE for the tick: twelve Brunch in a pack, eight shooters, six lungers and
       a boss, all awake. Not the game's actual ceiling - the encounter caps that - but a hard load
       that any real frame would have to survive. */
    const tickCost=(bodies)=>{
      startGame(7);
      if(typeof devOpen!=='undefined') devOpen=false;
      const r=currentRoom();
      r.enemies.length=0; r.pickups.length=0; projectiles.length=0;
      readyT=0; fadeT=0;
      for(let i=0;i<12;i++){ const g=spawnEnemy(false,r,ROOM_LEFT+120+i*18,MIDY+(i%4-2)*26,'brunch');
        g.noticeTimer=0; g.aggroTimer=9999; r.enemies.push(g); }
      for(let i=0;i<8;i++){ const g=spawnEnemy(false,r,ROOM_LEFT+150+i*44,ROOM_TOP+80+(i%3)*90,
        i%2?'gunner':'shooter'); g.noticeTimer=0; g.aggroTimer=9999; r.enemies.push(g); }
      for(let i=0;i<6;i++){ const g=spawnEnemy(false,r,ROOM_LEFT+200+i*60,ROOM_BOTTOM-90,'lunger');
        g.noticeTimer=0; g.aggroTimer=9999; r.enemies.push(g); }
      const boss=spawnEnemy(true,r,ROOM_RIGHT-140,MIDY,'boss'); r.enemies.push(boss);
      boss.noticeTimer=0; boss.aggroTimer=9999;
      player.x=MIDX; player.y=MIDY; player.lagX=MIDX; player.lagY=MIDY;
      player.hp=player.maxHp;
      const n=r.enemies.length;
      const keep=()=>{ player.hp=player.maxHp; player.iframes=0; for(const g of r.enemies) g.hp=g.maxHp; };
      for(let t=0;t<210;t++) keep(), update();                 // warm the JIT
      const N=210*10;
      const t0=performance.now();
      for(let t=0;t<N;t++) keep(), update();
      const ms=performance.now()-t0;
      const budget=1000/TICK_HZ;
      return {bodies:n, ticks:N, msPerTick:+(ms/N).toFixed(4),
        budgetMsPerTick:+budget.toFixed(4), pctOfBudget:+(100*(ms/N)/budget).toFixed(1),
        usPerBodyPerTick:+(1000*ms/N/n).toFixed(3)};
    };

    const renderCost=()=>{
      startGame(7);
      if(typeof devOpen!=='undefined') devOpen=false;
      const r=currentRoom();
      r.enemies.length=0; readyT=0; fadeT=0;
      for(let i=0;i<27;i++){ const g=spawnEnemy(false,r,ROOM_LEFT+120+i*20,MIDY+(i%5-2)*30,'brunch');
        g.noticeTimer=0; g.aggroTimer=9999; r.enemies.push(g); }
      const boss=spawnEnemy(true,r,ROOM_RIGHT-140,MIDY,'boss'); r.enemies.push(boss);
      player.x=MIDX; player.y=MIDY; player.lagX=MIDX; player.lagY=MIDY;
      for(let i=0;i<30;i++) render();
      const N=200, t0=performance.now();
      for(let i=0;i<N;i++) render();
      const ms=performance.now()-t0;
      return {calls:N, msPerRender:+(ms/N).toFixed(4), pctOf60HzBudget:+(100*(ms/N)/(1000/60)).toFixed(1)};
    };

    return {
      /* read the parts INSIDE the game context, after a startGame has built the player */
      PLAYER_MOVE_BASE: PLAYER_MOVE,
      /* order matters: a JS object literal evaluates in source order, and `player.speed` is only
         valid after a startGame has built the player. Read it AFTER the top-speed calls, which do
         start that game. */
      playerTopSpeed_meterEmpty: topSpeed(false),
      playerTopSpeed_meterFull: topSpeed(true),
      playerBaseSpeed: player.speed,
      moveSpeedBonus: moveSpeedBonus(),
      tick: tickCost(),
      render: renderCost(),
      TICK_HZ
    };
  });
  await b.close();
  console.log(JSON.stringify(out,null,1));
  const t=out.tick;
  console.log(`\nVERDICT: the tick uses ${t.pctOfBudget}% of its budget at ${t.bodies} bodies. `
    +`${t.pctOfBudget>60?'THAT IS THE PROBLEM - it will drop frames.'
      :'Frame time is not the constraint here, so effort belongs in correctness and redundancy, '
       +'not in micro-optimising a loop that is not slow.'}`);
  console.log(`        render uses ${out.render.pctOf60HzBudget}% of a 60fps frame.`);
})();