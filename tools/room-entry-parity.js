/* room-entry-parity.js - a fresh run walking into its first normal room, in the running game, for
 * csharp/Depths.Tests/RoomEntryParityTests.cs.  node tools/room-entry-parity.js  (server on 8791)
 *
 * REPORT-ONLY. startGame(seed); the first start-room door (N, E, S, W order) that leads to a normal
 * room; passDoor; update() until the transition has entered it. Recorded: the door, both streams'
 * draws since just before startGame, the player's position, readyT, and every spawned body (kind,
 * x, y, hp, idleTimer, noticeTimer, packId, packSlot, flank, shootCd).
 */
const path=require('path');
const pw=require(path.join(process.env.LOCALAPPDATA,'hermes','hermes-agent','node_modules','playwright'));
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:720}});
  await p.goto('http://127.0.0.1:'+(process.env.DEPTHS_PORT||8791)+'/depths.html',{waitUntil:'domcontentloaded',timeout:60000});
  await p.waitForFunction('typeof startGame==="function"',null,{timeout:60000});
  const out=await p.evaluate(()=>{
    const res=[];
    for(const seed of [1,42,4242,777,31337,99999]){
      const j0=Rnd.calls.jitter, r0=Rnd.calls.run;
      startGame(seed); if(typeof devOpen!=='undefined') devOpen=false;
      const r=currentRoom();
      const d=['N','E','S','W'].find(k=>{ if(!r.doors[k]) return false; const [nx,ny]=neighbor(r.x,r.y,k); const n=rooms[key(nx,ny)]; return n&&n.type==='normal'; });
      if(!d){ res.push({seed,door:null}); continue; }
      passDoor(r,d);
      let t=0; while(trans&&t<200){ update(); t++; }
      const rm=currentRoom();
      res.push({seed,door:d,jit:Rnd.calls.jitter,run:Rnd.calls.run,px:player.x,py:player.y,readyT,
        bodies:rm.enemies.map(e=>[e.type,e.x,e.y,e.hp,e.idleTimer,e.noticeTimer,e.packId===undefined?0:e.packId,e.packSlot===undefined?-1:e.packSlot,e.flank===undefined?0:e.flank,e.shootCd===undefined?0:e.shootCd])});
    }
    return res;
  });
  await b.close();
  console.log(JSON.stringify(out));
})();
