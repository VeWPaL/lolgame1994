/* hook-parity.js - the hook (fireAlt in hook mode, the pull explode, the ground field, resistance) in
 * the running game, for csharp/Depths.Tests/HookParityTests.cs.  node tools/hook-parity.js  (8791)
 *
 * REPORT-ONLY. Seed 4242, a cleared standard room, three lungers placed (inert: tickBodies is not
 * run), the player at (300, 400) holding the hook, Rnd.set(seed), fireAlt() at the aim, then each tick
 * tickPlayer() (which runs the fields) and tickProjectiles(). 'Early' right-clicks again at tick 60.
 * Every 10 ticks, per body: x, y, hp, stun, kvx, kvy, hookStacks, hitFlash; then fields, shells.
 */
const path=require('path');
const pw=require(path.join(process.env.LOCALAPPDATA,'hermes','hermes-agent','node_modules','playwright'));
const SCEN=[
  {name:'PullAndField', aim:[530,310], early:0, ticks:500},
  {name:'Early',        aim:[600,250], early:60, ticks:500},
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
      const r=currentRoom(); r.enemies.length=0; r.pickups.length=0; projectiles.length=0; hookFields.length=0;
      r.secret=null; readyT=0; fadeT=0;
      for(const k of Object.keys(keys)) delete keys[k];
      mouseDown=false; altMouseDown=false;
      player.x=player.lagX=300; player.y=player.lagY=400; player.altMode='hook'; player.altCooldown=0;
      const bodies=[[520,280],[560,330],[500,350]].map(([x,y])=>{ const e=spawnEnemy(false,r,x,y,'lunger'); e.noticeTimer=1e9; r.enemies.push(e); return e; });
      updateCamera(); mouse.x=s.aim[0]-cam.x; mouse.y=s.aim[1]-cam.y;
      Rnd.set(4242);
      const j0=Rnd.calls.jitter, r0=Rnd.calls.run;
      fireAlt();
      const rows=[];
      for(let t=1;t<=s.ticks;t++){
        if(s.early&&t===s.early) fireAlt();
        tickPlayer(); tickProjectiles();
        if(t%10===0) rows.push([t,...bodies.flatMap(e=>r.enemies.includes(e)?[e.x,e.y,e.hp,e.stun,e.kvx,e.kvy,e.hookStacks,e.hitFlash]:['dead',0,0,0,0,0,0,0]),hookFields.length,projectiles.length]);
      }
      res[s.name]={rows,jit:Rnd.calls.jitter-j0,run:Rnd.calls.run-r0};
    }
    return res;
  },SCEN);
  await b.close();
  console.log(JSON.stringify(out));
})();
