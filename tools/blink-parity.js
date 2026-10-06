/* blink-parity.js - measures the blink (doBlink + tickBlink) in the running game, for
 * csharp/Depths.Tests/BlinkParityTests.cs.  node tools/blink-parity.js  (server on 127.0.0.1:8791)
 *
 * REPORT-ONLY. Seed 4242, a cleared standard room with one body in it (so the charge refills at the
 * in-fight rate), the player at the room centre holding a direction. 20 ticks of tickPlayer, then
 * doBlink(), then 800 more ticks. Rows: tick, x, y, vx, vy, lagX, lagY, iframes, blinkGrace, boost,
 * blinkCharges, blinkRegen - the blink tick itself is row 0.
 */
const path=require('path');
const pw=require(path.join(process.env.LOCALAPPDATA,'hermes','hermes-agent','node_modules','playwright'));
const SCEN=[
  {name:'East', keys:{d:true}},
  {name:'NorthWest', keys:{w:true,a:true}},
  {name:'StandingAimsAtCursor', keys:{}, aim:[200,200]},
];
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:720}});
  await p.goto('http://127.0.0.1:'+(process.env.DEPTHS_PORT||8791)+'/depths.html',{waitUntil:'domcontentloaded',timeout:60000});
  await p.waitForFunction('typeof startGame==="function"',null,{timeout:60000});
  const out=await p.evaluate((SCEN)=>{
    const res={c:{BLINK_DIST,BLINK_RECHARGE,BLINK_FILL_CLEAR,BLINK_IFRAMES,BLINK_GRACE,DASH_TRAIL,BLINK_BOOST}};
    for(const s of SCEN){
      startGame(4242); if(typeof devOpen!=='undefined') devOpen=false;
      const r=currentRoom(); r.enemies.length=0; r.pickups.length=0; projectiles.length=0;
      readyT=0; fadeT=0; trans=null; paused=false;
      const e=spawnEnemy(false,r,ROOM_LEFT+40,ROOM_TOP+40,'lunger'); e.noticeTimer=1e9; r.enemies.push(e);
      player.x=player.lagX=MIDX; player.y=player.lagY=MIDY; player.vx=player.vy=0;
      if(s.aim){ updateCamera(); mouse.x=s.aim[0]-cam.x; mouse.y=s.aim[1]-cam.y; }
      for(const k of Object.keys(keys)) delete keys[k];
      Object.assign(keys,s.keys);
      const row=t=>[t,player.x,player.y,player.vx,player.vy,player.lagX,player.lagY,player.iframes,player.blinkGrace,
        player.boost,player.blinkCharges,player.blinkRegen];
      for(let t=0;t<20;t++) tickPlayer();
      doBlink();
      const rows=[row(0)];
      for(let t=1;t<=800;t++){ tickPlayer(); if(t<=40||t%20===0) rows.push(row(t)); }
      for(const k of Object.keys(keys)) delete keys[k];
      res[s.name]=rows;
    }
    return res;
  },SCEN);
  await b.close();
  console.log(JSON.stringify(out));
})();
