/* boss-parity.js - the Warden in the running game's tickBodies(), for
 * csharp/Depths.Tests/BossParityTests.cs.  node tools/boss-parity.js  (server on 127.0.0.1:8791)
 *
 * REPORT-ONLY. Seed 4242, a cleared standard room, the boss spawned by the game then placed (notice
 * timer zeroed, health set per scenario), the player held still with i-frames cleared every tick so
 * every sweep that lands is measured, Rnd.set(seed) before the first tick, tickBodies() N times.
 * Every 25 ticks: boss x, y, move, moveT, castT, volleyLeft, volleyT, bossCd, phase; the body count,
 * the shells fired and the player's hp.
 */
const path=require('path');
const pw=require(path.join(process.env.LOCALAPPDATA,'hermes','hermes-agent','node_modules','playwright'));
const SCEN=[
  {name:'PhaseOne',   hpFrac:1.0, boss:[400,250], player:[400,450], ticks:2500},
  {name:'PhaseThree', hpFrac:0.3, boss:[300,300], player:[560,420], ticks:2500},
  {name:'PhaseThreeLong', hpFrac:0.3, boss:[420,300], player:[600,500], ticks:16000, every:100},
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
      readyT=0; fadeT=0; resetRunCursors();
      player.x=player.lagX=s.player[0]; player.y=player.lagY=s.player[1]; player.vx=player.vy=0;
      player.hp=player.maxHp=1e6; player.iframes=0; player.armor=0;
      hitbox.x=player.lagX; hitbox.y=player.lagY;
      const e=spawnEnemy(true,r,s.boss[0],s.boss[1]);
      e.noticeTimer=0; e.hp=e.maxHp*s.hpFrac;
      r.enemies.push(e);
      Rnd.set(4242);
      const j0=Rnd.calls.jitter, r0=Rnd.calls.run;
      const rows=[];
      for(let t=1;t<=s.ticks;t++){
        player.iframes=0;
        tickBodies();
        if(t%(s.every||25)===0) rows.push([t,e.x,e.y,e.move,e.moveT,e.castT||0,e.volleyLeft,e.volleyT,e.bossCd,e.phase,r.enemies.length,projectiles.length,player.hp]);
      }
      res[s.name]={rows,jit:Rnd.calls.jitter-j0,run:Rnd.calls.run-r0,maxHp:e.maxHp};
    }
    return res;
  },SCEN);
  await b.close();
  console.log(JSON.stringify(out));
})();
