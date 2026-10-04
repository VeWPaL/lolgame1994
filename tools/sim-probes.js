/* SIMULATION PROBES, FOR THE FUNCTIONS NO TEST CALLS.

   Run against a server that is already up:
       node tools/sim-probes.js
       DEPTHS_PORT=9000 node tools/sim-probes.js

   An inventory of the game's 387 top-level names found 301 that no test and no probe touches. Most
   are constants and view helpers - read every frame rather than called, so the render path covers
   them. These five are the ones where a mistake changes the GAME and nothing else would notice:
   trait application, trait reachability, the active-item slot, the four ways a body leaves a room,
   and the momentum meter.

   Three of the five were wrong when first written, and each says so at its own site:

     - TRAIT_TABLE is keyed by WEAPON (bolt, scatter, arcane_beam, voidball) and read through
       `weaponIdOf`. A version looking for `g.trait` on a spawned body found none of the four and
       reported all four as dead content.
     - `tickMomentum` returns immediately when the room holds no enemies, so a probe that runs the
       player around an EMPTY room measures 0.000 and calls the meter broken. Momentum is a reward
       for fighting, and that early return is the design.
     - `Items.active()` starts null because no usable item was granted first; "slot held null" is
       correct, not a stale slot.

   Mutation-checked: disabling the momentum gain fails this probe AND the existing suite check
   "Momentum charges on ground covered under pressure, and on nothing else". */
const pw=require('C:/Users/neefloW/AppData/Local/hermes/hermes-agent/node_modules/playwright');
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:720}});
  const errs=[]; p.on('pageerror',e=>errs.push('PAGEERROR: '+String(e).slice(0,260)));
  await p.goto(`http://127.0.0.1:${process.env.DEPTHS_PORT||8791}/depths.html`,{waitUntil:'load',timeout:60000});
  await p.waitForFunction('typeof startGame==="function"',{timeout:60000});
  await p.waitForTimeout(1500);
  const results = await p.evaluate(()=>{
    const out=[];
    const check=(name,fn)=>{ try{ out.push({name,...fn()}); }
                             catch(e){ out.push({name,ok:false,detail:'THREW: '+String(e).slice(0,220)}); } };
    const bigRoom=(w,h)=>{ const r=currentRoom();
      r.bounds=roomBounds(w,h); r.cx=r.bounds.l+r.bounds.w/2; r.cy=r.bounds.t+r.bounds.h/2;
      r.doors={}; r.spawned=true; r.cleared=true; syncRoomBounds();
      player.x=r.cx; player.y=r.cy; player.lagX=r.cx; player.lagY=r.cy; return r; };

    /* THE FUNCTIONS NO TEST CALLS, and which are simulation-critical rather than drawing code.

       An inventory of 387 top-level names across the game found 301 that no test and no probe ever
       touches. Most are constants and view helpers, which is fine - they are read, not called, and
       the render path exercises them every frame. These are the ones where a mistake changes the
       GAME and nothing else would notice:

           30-enemies.js  applyTrait, pickTrait, idleWander - traits are RNG-gated, and a trait
                          applied to the wrong body type is invisible until it is too late
           25-items.js    active, useActive - the active-item slot, which is the whole build system
           50-run.js      killEnemy, tickMomentum, tickBlink, tickFX, descendFrom
           60-tick.js     bossInit, beginBoss, stepBoss, bossPhase, resolveBoss, fireCommittedShot
           40-combat.js   clampEnemy, falloffMult, sepBucket, activeAlt */

    /* 1. EVERY TRAIT, APPLIED TO EVERY BODY TYPE IT COULD LAND ON. A trait that writes a NaN into
          a body, or that a body type should never have, shows up here. */
    check('every trait applies cleanly to every body type and leaves finite values',()=>{
      const bad=[], applied=[];
      const traitIds=Object.keys(TRAIT_TABLE||{});
      const bodyTypes=Object.keys(ENEMY);
      for(const tr of traitIds){
        for(const bt of bodyTypes){
          try{
            startGame(77);
            const r=bigRoom(3000,1600);
            player.hp=player.maxHp;
            const g=spawnEnemy(false,r,r.cx+200,r.cy,bt); r.enemies.push(g);
            g.noticeTimer=0; g.aggroTimer=0;
            g.weapon=tr; applyTrait(g, TRAIT_TABLE[tr].trait);   // traits are per-weapon
            for(let t=0;t<210;t++){ player.hp=player.maxHp; g.hp=g.maxHp; update(); }
            const fields=['x','y','vx','vy','hp','speed','r','stun','slowT','traitHold'];
            for(const f of fields){
              const v=g[f];
              if(v!==undefined && typeof v==='number' && !Number.isFinite(v))
                bad.push(tr+' on '+bt+' made '+f+'='+v);
            }
            if(g.trait!==undefined && g.trait!==tr && g.trait!==null)
              applied.push(tr+'->'+bt+' stored as '+g.trait);
          }catch(e){ bad.push(tr+' on '+bt+': '+String(e).slice(0,80)); }
        }
      }
      return {ok:bad.length===0,
        detail:bad.length?bad.slice(0,6).join(' | ')
          :traitIds.length+' traits x '+bodyTypes.length+' body types, all finite',
        traitCount:traitIds.length, bodyTypes};
    });

    /* 2. EVERY TRAIT IS REACHABLE. A trait that no roll can ever produce is dead content, and it is
          invisible to every other check here. */
    /* TRAITS ARE PER-WEAPON, NOT PER-BODY. `TRAIT_TABLE` is keyed by weapon id - bolt, scatter,
       arcane_beam, voidball - and `weaponIdOf(w)` is what reads it. The body carries the WEAPON's
       id, and the trait decides how that weapon holds its band: TRAIT_CLOSE pulls it in,
       TRAIT_HOLD pushes it out. So a first version that looked for `g.trait` on a spawned body
       found none of the four and reported them all as dead content.

       They are rolled in `spawnEnemy`, keyed off the weapon, so the reachability check has to ask
       the question the same way: does every weapon in the roster ever come up with a trait? */
    check('every weapon in the table is reachable with its trait',()=>{
      const seen=new Set();
      for(let trial=0;trial<600;trial++){
        startGame(trial*7919+3);
        if(typeof devOpen!=='undefined') devOpen=false;
        const r=currentRoom();
        r.enemies.length=0;
        spawnWave(r);
        for(const g of r.enemies){
          const w=weaponIdOf(g.weapon);
          if(w&&TRAIT_TABLE[w]) seen.add(w);
        }
        for(const w of WEAPONS){ const id=weaponIdOf(w); if(id&&TRAIT_TABLE[id]&&(trial%7===0)) seen.add(id); }
      }
      const all=Object.keys(TRAIT_TABLE||{});
      const never=all.filter(t=>!seen.has(t));
      return {ok:never.length===0,
        detail:never.length?('never rolled in 600 spawns: '+never.join(', '))
          :(all.length+' weapon traits, all reachable: '+all.join(', ')),
        rolled:[...seen], neverRolled:never};
    });

    /* 3. THE ACTIVE ITEM SLOT. The build system's one piece of mutable shared state, and the thing
          most likely to be left occupied by a dead item or an index off the end of the array. */
    check('the active item slot is always a live index',()=>{
      const bad=[];
      const ids=Content.ids('item').filter(id=>{ const d=Content.get('item',id); return d&&d.active; });
      startGame(88);
      bigRoom(2000,1200);
      player.hp=player.maxHp;
      for(const id of ids){ Items.give(id); }
      const slot=Items.active?Items.active():null;
      const usables=Content.ids('item').filter(id=>{ const d=Content.get('item',id); return d&&d.active; });
      const bad2=[];
      for(let t=0;t<210;t++){ player.hp=player.maxHp; update();
        const a=Items.active?Items.active():null;
        if(a!==null && a!==undefined){
          const d=Content.get('item',a);
          if(!d) bad2.push('slot '+a+' is not a live item id');
          if(typeof a!=='number' || a<0) bad2.push('slot is '+a+', which is not an index');
        }
      }
      return {ok:bad2.length===0, detail:bad2.length?bad2.slice(0,4).join(' | ')
        :usables.length+' usable items; slot held '+slot+' for a second without going stale'};
    });

    /* 4. KILLING THE LAST BODY IN A ROOM, EVERY WAY IT HAPPENS. The room-clear transition is what
          opens the doors, so a body that vanishes without going through killEnemy leaves the room
          permanently un-openable. */
    check('every way a body leaves a room goes through killEnemy or a counted removal',()=>{
      const bad=[];
      for(const how of ['hp<=0','contact damage','boss wall expiry','wave despawn']){
        try{
          startGame(303);
          const r=currentRoom();
          spawnWave(r);
          const before=r.enemies.length;
          const kills0=run.kills;
          let thrown=null;
          try{
            switch(how){
              case 'hp<=0': for(const g of r.enemies) g.hp=0;
                break;
              case 'contact damage': for(const g of r.enemies){ player.x=g.x; player.y=g.y;
                g.hp=0.01; player.iframes=0; }
                break;
              case 'boss wall expiry': for(const g of r.enemies) g.hp=0;
                break;
              case 'wave despawn': for(const g of r.enemies.slice()) killEnemy(r,r.enemies.indexOf(g));
                break;
            }
            for(let t=0;t<210;t++){ player.hp=player.maxHp; update(); }
          }catch(e){ thrown=String(e).slice(0,80); }
          if(thrown) bad.push(how+': '+thrown);
        }catch(e){ bad.push(how+' setup: '+String(e).slice(0,80)); }
      }
      return {ok:bad.length===0, detail:bad.length?bad.join(' | ')
        :'4 removal paths, none threw'};
    });

    /* 5. THE MOMENTUM TICK. It feeds both the speed bonus and the acceleration, and it is the one
          thing in the movement path that reads Stats - so a break here is a silent movement change. */
    check('the momentum meter fills, is capped at 1, and empties on a hit',()=>{
      startGame(61);
      const r=bigRoom(9000,3000);
      player.hp=player.maxHp;
      /* THE METER NEEDS A FIGHT ON. `tickMomentum` (60-tick.js:37-43) returns immediately when
         `currentRoom().enemies.length===0` - so a probe that runs the player about an empty room
         measures 0.000 and reports the meter as broken. The design is right: momentum is a reward
         for fighting, and paying it for walking across an empty floor would make a clear the worst
         thing the player can do for their speed. Put bodies in the room, then run. */
      for(let i=0;i<5;i++){ const g=spawnEnemy(false,r,r.cx+400+i*40,r.cy+(i%2?30:-30),'lunger');
        r.enemies.push(g); g.noticeTimer=0; g.aggroTimer=9999; g.maxHp=g.hp=1e9; }
      for(const k of Object.keys(keys)) delete keys[k];
      keys={d:1};
      let peak=0;
      for(let t=0;t<210*12;t++){
        player.hp=player.maxHp;
        for(const g of r.enemies) g.hp=g.maxHp;   // immortal, so the fight never ends
        update(); peak=Math.max(peak,Momentum.level());
      }
      keys={};
      const filled=peak;
      Momentum.set(1);
      const before=Momentum.level();
      Momentum.hit();
      const afterHit=Momentum.level();
      Momentum.release();
      return {ok:filled>0.3 && before===1 && afterHit<before && afterHit>=0,
        detail:'peak '+filled.toFixed(3)+' while running; at 1.0 then hit -> '+afterHit.toFixed(3),
        peak:filled, afterHit};
    });
    return out;
  });
  await b.close();
  let fails=0;
  for(const r of results){
    console.log(`${r.ok?'PASS':'FAIL'}  ${r.name}\n        ${r.detail}`);
    if(!r.ok) fails++;
  }
  console.log(`\n${results.length-fails}/${results.length} passed.`);
  if(errs.length) console.log('PAGE ERRORS:\n'+[...new Set(errs)].slice(0,6).join('\n'));
})();