/* brunch-move-parity.js - Brunch packs moving in the running game's tickBodies(), for
 * csharp/Depths.Tests/BrunchMoveParityTests.cs.  node tools/brunch-move-parity.js  (server on 8791)
 *
 * REPORT-ONLY. Seed 4242, a cleared standard room, bodies spawned by the game then placed (a pack
 * shares a packId and takes slots 0..n-1), the player held still, Rnd.set(seed) before the first
 * tick, tickBodies() N times. VARIANT.brunch is set per scenario and restored. Every 10 ticks, per
 * body in spawn order: x, y, vx, vy, curSpeed, hp (or "dead"); then the player's hp.
 */
const path=require('path');
const pw=require(path.join(process.env.LOCALAPPDATA,'hermes','hermes-agent','node_modules','playwright'));
const pack=(n,x,y,id)=>Array.from({length:n},(_,i)=>['brunch',x+(i%2)*18,y+((i/2)|0)*18,id,i]);
const SCEN=[
  {name:'WallBeforeShooter', variant:'A', player:[650,450], bodies:[['shooter',550,250],...pack(4,300,300,7)], ticks:300},
  {name:'Chase',             variant:'A', player:[500,400], bodies:pack(3,200,250,8), ticks:300},
  {name:'LeashB',            variant:'B', player:[700,560], bodies:[['shooter',150,180],...pack(4,260,260,9)], ticks:300},
  {name:'LeashEdgeB',        variant:'B', player:[700,300], bodies:[['shooter',300,300],...pack(4,330,380,11)], ticks:120},
  {name:'AdvanceAPlus',      variant:'A+',player:[720,560], bodies:[['shooter',120,160],...pack(4,220,240,10)], ticks:300},
];
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:720}});
  await p.goto('http://127.0.0.1:'+(process.env.DEPTHS_PORT||8791)+'/depths.html',{waitUntil:'domcontentloaded',timeout:60000});
  await p.waitForFunction('typeof startGame==="function"',null,{timeout:60000});
  const out=await p.evaluate((SCEN)=>{
    const res={};
    const saved=VARIANT.brunch;
    for(const s of SCEN){
      VARIANT.brunch=s.variant;
      startGame(4242); if(typeof devOpen!=='undefined') devOpen=false;
      const r=currentRoom(); r.enemies.length=0; r.pickups.length=0; projectiles.length=0;
      readyT=0; fadeT=0;
      player.x=player.lagX=s.player[0]; player.y=player.lagY=s.player[1]; player.vx=player.vy=0; player.swerve=0;
      player.hp=player.maxHp=1e6; player.iframes=0; player.armor=0;
      hitbox.x=player.lagX; hitbox.y=player.lagY;
      const bodies=s.bodies.map(([type,x,y,id,slot],i)=>{
        const e=spawnEnemy(false,r,x,y,type);
        e.noticeTimer=0; e.aggroTimer=0;
        if(id!==undefined){ e.packId=id; e.packSlot=slot; }
        r.enemies.push(e); return e;
      });
      const init=bodies.map(e=>({curSpeed:e.curSpeed,idleTimer:e.idleTimer,shootCd:e.shootCd===undefined?0:e.shootCd}));
      Rnd.set(4242);
      const j0=Rnd.calls.jitter, r0=Rnd.calls.run;
      const rows=[];
      for(let t=1;t<=s.ticks;t++){
        player.iframes=0;
        tickBodies();
        if(t%10===0) rows.push([t, ...bodies.flatMap(e=>r.enemies.includes(e)?[e.x,e.y,e.vx||0,e.vy||0,e.curSpeed||0,e.hp]:['dead',0,0,0,0,0]), player.hp]);
      }
      res[s.name]={init,rows,jit:Rnd.calls.jitter-j0,run:Rnd.calls.run-r0};
    }
    VARIANT.brunch=saved;
    return res;
  },SCEN);
  await b.close();
  console.log(JSON.stringify(out));
})();
