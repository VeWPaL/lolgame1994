/* lunger-parity.js - measures the lunger in the running game's tickBodies(), for
 * csharp/Depths.Tests/LungerParityTests.cs.  node tools/lunger-parity.js  (server on 127.0.0.1:8791)
 *
 * REPORT-ONLY. Each scenario: seed 4242, a cleared standard room, lungers placed by hand with the
 * spawn's own field values, the player held still (its hitbox published by hand, since tickPlayer
 * is not called), Rnd.set(seed) immediately before the first tick so a fresh C# Rng matches, then
 * tickBodies() N times. Every 10 ticks each body's x, y, state, lungeT, curSpeed, kvx, kvy and the
 * player's hp are recorded, at full precision.
 */
const path=require('path');
const pw=require(path.join(process.env.LOCALAPPDATA,'hermes','hermes-agent','node_modules','playwright'));
const SCEN=[
  {name:'Alone',   player:[550,355], bodies:[[250,300,0,0]], ticks:420},
  {name:'Pair',    player:[550,355], bodies:[[250,300,0,0],[262,420,0,0]], ticks:420},
  {name:'Wander',  player:[740,570], bodies:[[64,144,0,0]], ticks:300},
  {name:'Knocked', player:[400,500], bodies:[[90,300,-4,1]], ticks:200},
];
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:720}});
  await p.goto('http://127.0.0.1:'+(process.env.DEPTHS_PORT||8791)+'/depths.html',{waitUntil:'domcontentloaded',timeout:60000});
  await p.waitForFunction('typeof startGame==="function"',null,{timeout:60000});
  const out=await p.evaluate((SCEN)=>{
    const res={};
    for(const s of SCEN){
      startGame(4242); if(typeof devOpen!=='undefined') devOpen=false;
      const r=currentRoom(); r.enemies.length=0; r.pickups.length=0; projectiles.length=0;
      readyT=0; fadeT=0;
      player.x=player.lagX=s.player[0]; player.y=player.lagY=s.player[1];
      player.hp=player.maxHp=1e6; player.iframes=0; player.armor=0; player.kvx=player.kvy=0;
      hitbox.x=player.lagX; hitbox.y=player.lagY;
      s.bodies.forEach(([x,y,kvx,kvy],i)=>{
        const e=spawnEnemy(false,r,x,y,'lunger');
        e.noticeTimer=0; e.aggroTimer=0; e.kvx=kvx; e.kvy=kvy; e.flank=i*2.399963229728653;
        r.enemies.push(e);
      });
      const init=r.enemies.map(e=>({idleTimer:e.idleTimer,curSpeed:e.curSpeed,walkSpeed:e.walkSpeed,runSpeed:e.runSpeed,lungeCd:e.lungeCd,idleDir:e.idleDir}));
      Rnd.set(4242);
      const j0=Rnd.calls.jitter, r0=Rnd.calls.run;
      const rows=[];
      for(let t=1;t<=s.ticks;t++){
        player.iframes=0;                       // every touch lands, so contact is measured each time
        tickBodies();
        if(t%10===0) rows.push([t, player.hp, ...r.enemies.flatMap(e=>[e.x,e.y,e.lungeState,e.lungeT,e.curSpeed,e.kvx,e.kvy])]);
      }
      res[s.name]={init,rows,jit:Rnd.calls.jitter-j0,run:Rnd.calls.run-r0};
    }
    return res;
  },SCEN);
  await b.close();
  console.log(JSON.stringify(out));
})();
