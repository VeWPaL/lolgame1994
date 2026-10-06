/* ranged-parity.js - measures shooters and gunners in the running game's tickBodies(), for
 * csharp/Depths.Tests/RangedParityTests.cs.  node tools/ranged-parity.js  (server on 127.0.0.1:8791)
 *
 * REPORT-ONLY. Same fixture as lunger-parity.js: seed 4242, a cleared standard room, bodies spawned
 * by the game then placed, the player held still with a fixed velocity and swerve (its hitbox
 * published by hand), Rnd.set(seed) before the first tick. Every 10 ticks: each body's x, y, castT,
 * castReady, shootCd, castAim, dodgeCd, kvx, kvy, then the projectile count and the player's hp.
 */
const path=require('path');
const pw=require(path.join(process.env.LOCALAPPDATA,'hermes','hermes-agent','node_modules','playwright'));
const SCEN=[
  {name:'ShooterStill', player:[550,355,0,0,0], bodies:[['shooter',250,300]], ticks:400},
  {name:'GunnerStill',  player:[550,400,0,0,0], bodies:[['gunner',200,250]], ticks:400},
  {name:'ShooterLead',  player:[500,450,1.1,-0.4,0.3], bodies:[['shooter',250,300]], ticks:400},
  {name:'Blocked',      player:[600,355,0,0,0], bodies:[['shooter',200,355],['lunger',380,355]], ticks:300},
  {name:'GunnerDodge',  player:[650,300,0,0,0], bodies:[['gunner',400,300]], ticks:300, shell:[300,300,2,0]},
];
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:720}});
  await p.goto('http://127.0.0.1:'+(process.env.DEPTHS_PORT||8791)+'/depths.html',{waitUntil:'domcontentloaded',timeout:60000});
  await p.waitForFunction('typeof startGame==="function"',null,{timeout:60000});
  const out=await p.evaluate((SCEN)=>{
    startGame(4242);
    const res={consts:{dz:swerveDeadzone(),full:swerveFull()}};
    for(const s of SCEN){
      startGame(4242); if(typeof devOpen!=='undefined') devOpen=false;
      const r=currentRoom(); r.enemies.length=0; r.pickups.length=0; projectiles.length=0;
      readyT=0; fadeT=0;
      const [px,py,vx,vy,sw]=s.player;
      player.x=player.lagX=px; player.y=player.lagY=py; player.vx=vx; player.vy=vy; player.swerve=sw;
      player.beliefVx=0; player.beliefVy=0; player.trendVx=0; player.trendVy=0;
      player.hp=player.maxHp=1e6; player.iframes=0; player.armor=0;
      hitbox.x=player.lagX; hitbox.y=player.lagY;
      s.bodies.forEach(([type,x,y],i)=>{
        const e=spawnEnemy(false,r,x,y,type);
        e.noticeTimer=0; e.aggroTimer=0; if(e.flank!==undefined) e.flank=i*2.399963229728653;
        r.enemies.push(e);
      });
      if(s.shell) projectiles.push({x:s.shell[0],y:s.shell[1],vx:s.shell[2],vy:s.shell[3],r:5,dmg:1,friendly:true,
        color:'#fff',ox:s.shell[0],oy:s.shell[1],pierce:0,scale:1,hit:null,dx:1,dy:0,from:'player',owner:null});
      const init=r.enemies.map(e=>({shootCd:e.shootCd,idleTimer:e.idleTimer,curSpeed:e.curSpeed}));
      Rnd.set(4242);
      const j0=Rnd.calls.jitter, r0=Rnd.calls.run;
      const rows=[];
      for(let t=1;t<=s.ticks;t++){
        player.iframes=0;
        tickBodies();
        if(t%10===0) rows.push([t, ...r.enemies.flatMap(e=>e.type==='lunger'
          ? [e.x,e.y,0,false,0,0,0,e.kvx,e.kvy]
          : [e.x,e.y,e.castT,e.castReady,e.shootCd,e.castAim,e.dodgeCd,e.kvx,e.kvy]), projectiles.length, player.hp]);
      }
      res[s.name]={init,rows,jit:Rnd.calls.jitter-j0,run:Rnd.calls.run-r0};
    }
    return res;
  },SCEN);
  await b.close();
  console.log(JSON.stringify(out));
})();
