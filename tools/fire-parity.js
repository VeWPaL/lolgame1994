/* fire-parity.js - measures fireWeapon in the running game, for csharp/Depths.Tests/FireParityTests.cs.
 *
 *   node tools/fire-parity.js            (needs the local server on 127.0.0.1:8791)
 *
 * REPORT-ONLY. Each case reseeds the RNG immediately before firing (Rnd.set), so a fresh C#
 * `new Rng(seed)` is in the same state: the jitter draws spent on the spread and the buckshot are
 * then comparable draw for draw. The aim is set in WORLD space through the camera, as the game
 * reads it (mouseWorld). Numbers are printed at full precision; the C# compares to 1e-6.
 */
const path=require('path');
const pw=require(path.join(process.env.LOCALAPPDATA,'hermes','hermes-agent','node_modules','playwright'));
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:720}});
  await p.goto('http://127.0.0.1:'+(process.env.DEPTHS_PORT||8791)+'/depths.html',{waitUntil:'domcontentloaded',timeout:60000});
  await p.waitForFunction('typeof startGame==="function"',null,{timeout:60000});
  const rows=await p.evaluate(()=>{
    const out=[];
    const h=v=>Math.round(v*1e6)/1e6;
    for(const seed of [1,42,4242,99999]) for(let w=0;w<WEAPONS.length;w++) for(const ang of [0,2.1,-1.3]){
      startGame(seed); if(typeof devOpen!=='undefined') devOpen=false;
      player.x=400; player.y=330; player.weaponIdx=w; player.shootSlow=0.5; player.cooldown=0;
      projectiles.length=0;
      const tx=player.x+Math.cos(ang)*200, ty=player.y+Math.sin(ang)*200;
      updateCamera(); mouse.x=tx-cam.x; mouse.y=ty-cam.y;
      Rnd.set(seed);
      const j0=Rnd.calls.jitter, r0=Rnd.calls.run;
      fireWeapon();
      // a signature of every shell: position, velocity, damage, band, pierce - rounded to 1e-6
      const sig=projectiles.map(q=>[q.x,q.y,q.vx,q.vy,q.dmg,q.r,q.ox,q.oy,q.dx,q.dy].map(h).join(',')+':'+q.pierce+':'+q.fNear).join('|');
      out.push({seed,w,ang,tx:h(tx),ty:h(ty),n:projectiles.length,jit:Rnd.calls.jitter-j0,run:Rnd.calls.run-r0,
        cd:h(player.cooldown),slow:h(player.shootSlow),muzzle:player.muzzleTimer,sig});
    }
    return out;
  });
  await b.close();
  for(const r of rows)
    console.log(`        [TestCase(${r.seed}u, ${r.w}, ${r.tx}, ${r.ty}, ${r.n}, ${r.jit}, ${r.cd}, ${r.slow}, "${r.sig}")]`);
})();
