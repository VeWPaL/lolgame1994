/* A DEEP FUZZER FOR THE GAME, AND A RECORD OF SEVEN WAYS TO WRITE ONE THAT PROVES NOTHING.

   Run it against a server that is already up:
       node tools/deep-fuzz.js            (expects http://127.0.0.1:8791/depths.html)
       DEPTHS_PORT=9000 node tools/deep-fuzz.js

   It plays real runs - walks through real doors, fights with the real weapon - and sweeps every
   live body and projectile each tick for values no game state should ever hold: NaN, Infinity, a
   negative timer, a body outside the room it is in, a projectile with negative life, or a thrown
   exception. Two passes, because they answer different questions: one where the player can die, and
   one where they cannot, since death ends a trial and a fuzz that always dies never sees floor 2.

   THE POINT OF THE FILE IS THE COMMENT BLOCK IN THE MIDDLE. Six earlier versions reported 0 bugs
   while never leaving the start room. Each one was a clean run and none of them were evidence:

     - `room.doors[d]` is a BOOLEAN, not a position. The position is `doorPoint(d)`. Steering with
       `doors[k].x` aims at NaN.
     - `devOpen` is true on a headless load, and `80-ui.js:325` does `if(devOpen){ keys={}; }` - so
       synthetic input is wiped every tick before the player can move.
     - `mouse.x`/`mouse.y` are CANVAS pixels, not world units. `mouse.x = worldX` fires roughly 80px
       wide of the target, which reads exactly like a broken weapon: shots counted, zero hits, a
       projectile curving across the room.
     - `keys = {}` each tick resets the player's acceleration. Holding a key gives 2.9px/tick
       against 1.0 for a fresh assignment, so a door 200 ticks away becomes 600.
     - A door opens when the player is more than 18px BEYOND the wall line
       (`40-combat.js:24-27`), so a fuzz steering to the door's own point stops short forever.
     - `startGame()` resets `run.kills`, `run.shots` and `run.hits`, so sampling them per trial and
       adding them up reports the LAST trial's count. 116000 ticks of shooting read as "0 kills".
     - A room with no enemies is not a CLEARED room - the start room always has none, and counting
       that as a clear is how a run reported 18 rooms cleared with zero kills.

   So this file reports COVERAGE FIRST and prints a verdict that is about the FUZZ, not the game:
   "PARTIAL - fought on floor 1 only" is the honest reading of a clean run that never went
   downstairs, and it is the sentence six of those runs needed and did not have. */
const pw=require('C:/Users/neefloW/AppData/Local/hermes/hermes-agent/node_modules/playwright');
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:720}});
  const errs=[];
  p.on('pageerror',e=>errs.push('PAGEERROR: '+String(e).slice(0,260)));
  await p.goto('http://127.0.0.1:'+(process.env.DEPTHS_PORT||8791)+'/depths.html',{waitUntil:'load',timeout:60000});
  await p.waitForFunction('typeof startGame==="function"',null,{timeout:60000});
  await p.waitForTimeout(1500);

  /* A FUZZER THAT REPORTS ITS OWN COVERAGE, OR ITS "0 BUGS" IS WORTH NOTHING.

     This is the seventh attempt at this. The six before it each reported 0 integrity violations
     while never leaving the start room, because:

       - `room.doors[d]` is a BOOLEAN, not a position (the position is `doorPoint(d)`), so the
         fuzz steered toward NaN;
       - `devOpen` is true on a headless load and `80-ui.js:325` clears `keys` every tick when it
         is, so synthetic input was swallowed before the player could move;
       - the target has to be aimed in SCREEN space (`updateCamera(); mouse.x = wx - cam.x`), and
         `mouse.x = wx` fires ~80px wide of the target - which reads exactly like a broken weapon;
       - `keys = {}` every tick resets the player's acceleration: holding a key gives 2.9px/tick
         against 1.0 for a fresh assignment;
       - the door opens 18px BEYOND the wall line, so a fuzz steering to the door's own point
         stops short and stands there;
       - `run.kills` is reset by `startGame`, so sampling it per trial and calling it a total
         reports the LAST trial's count - that read 116000 ticks of shooting as "0 kills".

     Each produced a clean run. That is the whole lesson: a fuzz result is only evidence about what
     it actually reached, so coverage is reported first and the verdict says out loud when a pass
     explored too little to mean anything. */
  const runPass = async (keepAlive) => p.evaluate((keepAlive)=>{
    const num=v=>typeof v==='number'&&Number.isFinite(v);
    const TICK=210, BUDGET=TICK*240;
    const cov={rooms:new Set(),types:new Set(),floors:new Set(),bodies:new Set(),
      pickups:new Set(),
      ticks:0,transitions:0,deaths:0,bodyMax:0,projMax:0,pickMax:0,bossTicks:0,
      contactFrames:0,minGap:1e9,kills:0,shots:0,hits:0,clearedRooms:0,ticksWithEnemies:0,
      sealedSkipped:0,unlocks:{boss:false,item:false}};
    const bad=[];
    for(let trial=0;trial<10;trial++){
      const seed=([1,7,42,31337,99999,12345,777,2024,555,8888][trial])+trial*137;
      startGame(seed);
      if(typeof devOpen!=='undefined') devOpen=false;   // 80-ui.js:325 clears keys when it is open
      const hadFight=new Set(), wasCleared=new Set();
      let prevKills=0, prevShots=0, prevHits=0;
      for(let t=0;t<BUDGET;t++){
        cov.ticks++;
        const r=currentRoom();
        cov.rooms.add(r.x+','+r.y); cov.types.add(r.type); cov.floors.add(run.floor);
        cov.bodyMax=Math.max(cov.bodyMax,r.enemies.length);
        cov.projMax=Math.max(cov.projMax,projectiles.length);
        cov.pickMax=Math.max(cov.pickMax,r.pickups.length);
        if(r.boss) cov.bossTicks++;
        for(const q of r.pickups) cov.pickups.add(q.kind);
        if(typeof bossUnlocked!=='undefined'&&bossUnlocked) cov.unlocks.boss=true;
        if(typeof itemUnlocked!=='undefined'&&itemUnlocked) cov.unlocks.item=true;

        /* A CLEARED FIGHT ROOM - one that had bodies and then had none. The start room is always
           empty, and counting that as a cleared fight is how an earlier run reported 18 rooms
           cleared while reporting zero kills. */
        const rk=r.x+','+r.y;
        if(r.enemies.length){ cov.ticksWithEnemies++; hadFight.add(rk); }
        if(hadFight.has(rk)&&!r.enemies.length&&!wasCleared.has(rk)){
          wasCleared.add(rk); cov.clearedRooms++;
        }

        for(const g of r.enemies){
          cov.bodies.add(g.type);
          if(!num(g.x)||!num(g.y)||!num(g.hp)){ bad.push({seed,t,kind:'NaN-body',type:g.type}); break; }
          if(g.stun<0||g.slowT<0||g.hookStacks<0){ bad.push({seed,t,kind:'negative-timer',
            type:g.type,stun:g.stun,slowT:g.slowT,hs:g.hookStacks}); break; }
          const bl=r.bounds;
          if(g.x<bl.l-60||g.x>bl.l+bl.w+60||g.y<bl.t-60||g.y>bl.t+bl.h+60)
            { bad.push({seed,t,kind:'body-escaped-room',type:g.type,
              x:Math.round(g.x),y:Math.round(g.y)}); break; }
          const d=Math.hypot(g.x-player.x,g.y-(player.y-PLAYER_HIT_DY));
          if(d<cov.minGap) cov.minGap=d;
          if(d<g.r+PLAYER_HIT_R) cov.contactFrames++;
        }
        if(bad.length&&bad[bad.length-1].seed===seed) break;
        for(const q of projectiles){
          if(!num(q.x)||!num(q.y)){ bad.push({seed,t,kind:'NaN-projectile'}); break; }
          if(q.life<0){ bad.push({seed,t,kind:'projectile-negative-life',life:q.life}); break; }
        }
        if(bad.length&&bad[bad.length-1].seed===seed) break;

        for(const k of Object.keys(keys)) delete keys[k];   // patch, never replace
        if(keepAlive){ player.hp=player.maxHp; player.iframes=Math.max(player.iframes,1); }

        const target=r.enemies.find(g=>g.hp>0)||null;
        if(target){
          /* AIM IN SCREEN SPACE AND CLOSE TO WEAPON RANGE BEFORE FIRING. Measured hit rate:
             100% to 300px, 80% at 450, ~55% at 600, ~50% at 1000-1200 - spread widens with range
             by design. A fuzz firing from across the room misses half its shots and clears nothing,
             which is a fact about the fuzz, not the game. */
          updateCamera();
          mouse.x=target.x-cam.x; mouse.y=target.y-cam.y;
          mouseDown=true;
          const dx=target.x-player.x, dy=target.y-player.y;
          if(Math.abs(dx)>260) keys[dx>0?'d':'a']=1;
          if(Math.abs(dy)>260) keys[dy>0?'s':'w']=1;
        } else {
          mouseDown=false;
          let best=null,bd=1e9;
          for(const k of Object.keys(r.doors)){
            if(!r.doors[k]) continue;
            if(!doorPassable(r,k)){ cov.sealedSkipped++; continue; }
            const pt=doorPoint(k);
            const dd=Math.hypot(pt[0]-player.x,pt[1]-player.y);
            if(dd<bd){ bd=dd; best=pt; }
          }
          if(best){
            /* 40px PAST the door: 40-combat.js:24-27 opens a door once the player is more than 18px
               beyond the wall line, so steering to the door's own point stops short of it. */
            const tx=best[0]+Math.sign(best[0]-MIDX)*40, ty=best[1]+Math.sign(best[1]-MIDY)*40;
            if(Math.abs(tx-player.x)>4) keys[tx>player.x?'d':'a']=1;
            if(Math.abs(ty-player.y)>4) keys[ty>player.y?'s':'w']=1;
          }
        }
        try{ update(); }catch(err){ bad.push({seed,t,kind:'throw',msg:String(err).slice(0,200)}); break; }

        /* ACCUMULATE DELTAS: `startGame` resets run.* on the next trial, so sampling `run.kills`
           and reporting it as a total gives the LAST trial's figure rather than the run's. */
        cov.kills+=Math.max(0,run.kills-prevKills);
        cov.shots+=Math.max(0,run.shots-prevShots);
        cov.hits+=Math.max(0,run.hits-prevHits);
        prevKills=run.kills; prevShots=run.shots; prevHits=run.hits;

        if(currentRoom().x!==r.x||currentRoom().y!==r.y) cov.transitions++;
        if(player.hp<=0){ cov.deaths++; break; }
      }
    }
    const byKind={};
    for(const x of bad){ const k=x.kind+(x.type?':'+x.type:'');
      byKind[k]=byKind[k]||{count:0,example:x}; byKind[k].count++; }
    return {ticks:cov.ticks, roomsEntered:cov.rooms.size, transitions:cov.transitions,
      floorsVisited:[...cov.floors].sort((a,b)=>a-b), roomTypes:[...cov.types],
      enemyTypesSeen:[...cov.bodies], pickupKindsSeen:[...cov.pickups],
      unlocksSeen:cov.unlocks, deaths:cov.deaths, ticksWithEnemies:cov.ticksWithEnemies,
      fightRoomsCleared:cov.clearedRooms, sealedDoorsSkipped:cov.sealedSkipped,
      shots:cov.shots, hits:cov.hits, kills:cov.kills,
      maxBodiesInRoom:cov.bodyMax, maxProjectiles:cov.projMax, maxPickups:cov.pickMax,
      bossTicks:cov.bossTicks, contactFrames:cov.contactFrames,
      closestApproach:+(cov.minGap===1e9?0:cov.minGap).toFixed(1),
      badCount:bad.length, byKind, sample:bad.slice(0,6)};
  }, keepAlive);

  const survivable = await runPass(false);
  const deep = await runPass(true);
  const report=(name,r)=>{
    /* THE VERDICT IS ABOUT THE FUZZ, NOT THE GAME. A pass that never left floor 1 cannot say
       anything about floors 2+, however clean its tick count. */
    let verdict='explored';
    if(r.roomsEntered<3) verdict='INCONCLUSIVE - never left the start room';
    else if(r.kills===0) verdict='INCONCLUSIVE - never killed anything, so nothing downstream was reached';
    else if(r.floorsVisited.length<2) verdict='PARTIAL - fought on floor 1 only; floors 2+ untouched';
    console.log(`\n=== ${name} ===\n${JSON.stringify(r,null,1)}\nVERDICT: ${verdict}`);
  };
  report('SURVIVABLE (the player can die)', survivable);
  report('DEEP (the player cannot die)', deep);
  if(errs.length) console.log('\nPAGE ERRORS:\n'+[...new Set(errs)].slice(0,8).join('\n'));
  await b.close();
})();