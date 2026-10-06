/* blast-parity.js - measures the right-click blast (fireAlt + explode) in the running game, for
 * csharp/Depths.Tests/BlastParityTests.cs.  node tools/blast-parity.js  (server on 127.0.0.1:8791)
 *
 * REPORT-ONLY. Seed 4242, a cleared standard room, bodies placed by hand, the player at (300, 400).
 * Rnd.set(seed) immediately before fireAlt(), then tickProjectiles() alone until the blast is gone
 * (bodies do not move, so the blast's own effect is isolated). Recorded: the tick it went off, each
 * body's hp/kvx/kvy/stun/slowT/hitFlash (or "dead"), kills, pickups, and both streams' draw counts.
 */
const path=require('path');
const pw=require(path.join(process.env.LOCALAPPDATA,'hermes','hermes-agent','node_modules','playwright'));
const SCEN=[
  {name:'Cluster', aim:[450,300], bodies:[['lunger',450,300],['lunger',480,320],['lunger',430,350]]},
  {name:'Contact', aim:[600,300], bodies:[['lunger',420,360]]},
  {name:'Wall',    aim:[900,200], bodies:[['lunger',700,250]]},
  {name:'Kill',    aim:[500,250], bodies:[['shooter',500,250]]},
];
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:720}});
  await p.goto('http://127.0.0.1:'+(process.env.DEPTHS_PORT||8791)+'/depths.html',{waitUntil:'domcontentloaded',timeout:60000});
  await p.waitForFunction('typeof startGame==="function"',null,{timeout:60000});
  const out=await p.evaluate((SCEN)=>{
    const res={alt:{cooldown:ALT_WEAPON.cooldown,speed:ALT_WEAPON.speed,r:ALT_WEAPON.r,aoe:ALT_WEAPON.aoeRadius,
      pool:ALT_WEAPON.pool,knock:ALT_WEAPON.aoeKnock,near:ALT_KNOCK_NEAR,far:ALT_KNOCK_FAR,disperse:DISPERSE}};
    for(const s of SCEN){
      startGame(4242); if(typeof devOpen!=='undefined') devOpen=false;
      const r=currentRoom(); r.enemies.length=0; r.pickups.length=0; projectiles.length=0;
      r.secret=null;
      player.x=player.lagX=300; player.y=player.lagY=400; player.altMode='blast'; player.altCooldown=0; player.shootSlow=0.5;
      for(const [type,x,y] of s.bodies){ const e=spawnEnemy(false,r,x,y,type); e.noticeTimer=0; e.aggroTimer=0; r.enemies.push(e); }
      const bodies=r.enemies.slice();
      updateCamera(); mouse.x=s.aim[0]-cam.x; mouse.y=s.aim[1]-cam.y;
      Rnd.set(4242);
      const j0=Rnd.calls.jitter, r0=Rnd.calls.run, k0=run.kills;
      fireAlt();
      const after={altCooldown:player.altCooldown, shootSlow:player.shootSlow, tx:projectiles[0].tx, ty:projectiles[0].ty};
      let t=0; while(projectiles.length&&t<400){ tickProjectiles(); t++; }
      res[s.name]={after, t, bodies:bodies.map(e=>r.enemies.includes(e)?[e.hp,e.kvx,e.kvy,e.stun,e.slowT,e.hitFlash]:'dead'),
        kills:run.kills-k0, pickups:r.pickups.map(q=>q.kind), jit:Rnd.calls.jitter-j0, run:Rnd.calls.run-r0};
    }
    return res;
  },SCEN);
  await b.close();
  console.log(JSON.stringify(out));
})();
