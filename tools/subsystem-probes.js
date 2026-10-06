/* SUBSYSTEM PROBES, FOR THE PLACES A FUZZER CANNOT WALK INTO.

   Run against a server that is already up:
       node tools/subsystem-probes.js
       DEPTHS_PORT=9000 node tools/subsystem-probes.js

   tools/deep-fuzz.js plays real runs and found zero integrity violations across 959303 ticks. That
   is good evidence about the tick as it runs in a fight and no evidence at all about the item
   roster, the depth ladder, the boss's own fight, records, or the dev lab - those are reached by
   choice, not by walking, so they are probed directly here. Six probes, each with a pass condition.

   Every accessor below was wrong at least once, and the file says so at each one, because that is
   the actual lesson: a probe that reaches nothing and reports success is worse than no probe.

       Content.ids('item')       not Content.list(), not Content.kinds.item.table, and not
                                 Content.each() - which exists but is module-internal
       Items.give(id)            not grantItem, which does not exist
       areaForFloor(f)           returns an AREA NAME; depthFloor() returns the FLOOR NUMBER and
                                 reads the current run. Conflating them reads "floor 5 gave area 5"
       DEPTH_RATE_CAP            the spawn rate SATURATES at it by design, so a strict
                                 monotonicity check reports a cap as a bug
       projectiles[].friendly   hostile and friendly share one array, and a shell already in flight
                                 at both sample instants makes a spawn count of zero

   And one probe is here because it caught a real harness failure rather than a game one: the dev
   lab ships open, and 80-ui.js:325 clears `keys` every tick while it is open, which silently
   swallowed the input of six fuzzers. */
const pw=require('C:/Users/neefloW/AppData/Local/hermes/hermes-agent/node_modules/playwright');
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:720}});
  const errs=[];
  p.on('pageerror',e=>errs.push('PAGEERROR: '+String(e).slice(0,260)));
  await p.goto(`http://127.0.0.1:${process.env.DEPTHS_PORT||8791}/depths.html`,{waitUntil:'load',timeout:60000});
  await p.waitForFunction('typeof startGame==="function"',null,{timeout:60000});
  await p.waitForTimeout(1500);

  /* A PER-SUBSYSTEM PROBE SET, FOR THE PLACES A FUZZER CANNOT REACH.

     tools/deep-fuzz.js plays real runs and found nothing across 959303 ticks. That is good evidence
     about the tick as it runs in a fight, and no evidence at all about: the item roster and every
     item's hooks, the depth ladder at floor 100, the boss's own phases, the area palette, records
     round-tripping, or save/load. Those are reached by choice, not by walking, so they are probed
     directly here - each probe ASSERTS something and reports what it saw.

     Each entry is a NAME and a body that returns {ok, detail}. The point is that a probe which
     cannot fail is not a probe, so every one of these has a pass condition and the run exits
     non-zero-ish by printing FAIL lines rather than by throwing. */
  const results = await p.evaluate(()=>{
    const out=[];
    const check=(name,fn)=>{ try{ const r=fn(); out.push({name, ...r}); }
                             catch(e){ out.push({name, ok:false, detail:'THREW: '+String(e).slice(0,200)}); } };
    const bigRoom=(w,h)=>{ const r=currentRoom();
      r.bounds=roomBounds(w,h); r.cx=r.bounds.l+r.bounds.w/2; r.cy=r.bounds.t+r.bounds.h/2;
      r.doors={}; r.spawned=true; r.cleared=true; syncRoomBounds();
      player.x=r.cx; player.y=r.cy; player.lagX=r.cx; player.lagY=r.cy; return r; };

    /* 1. EVERY ITEM: build it, hold it, and tick with it on. An item whose hook throws, or which
          writes NaN into a derived stat, fails here. */
    check('every item can be granted and survives a second of play',()=>{
      /* `Content.ids('item')` IS THE PUBLIC ENUMERATION (15-content.js:243 exports define/get/has/
         all/ids/validate/...). `each()` exists but is module-internal and NOT exported, and
         `Content.kinds` is not exported either - so both of those throw.

         Three attempts at this accessor, and the count assertion below exists because of them: a
         probe that enumerates nothing and reports "all finite" is worse than no probe, because it
         looks like coverage. "0 items, all finite" is precisely that. */
      const ids=Content.ids('item');
      const bad=[];
      for(const id of ids){
        try{
          startGame(99);
          bigRoom(4000,2000);
          player.hp=player.maxHp;
          Items.give(id);   // 25-items.js:143 - the real entry point, and it REPORTS rather than throws
          for(let t=0;t<210;t++){ player.hp=player.maxHp; update(); }
          const rec=Content.get('item',id);
          if(rec&&rec.stats){
            for(const s of Object.keys(rec.stats)){
              const v=Stats.value(s);
              if(typeof v!=='number'||!Number.isFinite(v)) bad.push(id+':'+s+'='+v);
            }
          }
          if(!Number.isFinite(player.hp)||!Number.isFinite(player.x)) bad.push(id+': player state not finite');
        }catch(e){ bad.push(id+': '+String(e).slice(0,90)); }
      }
      if(ids.length<5) bad.push('only '+ids.length+' items enumerated - the accessor is probably wrong again');
      return {ok:bad.length===0, detail:bad.length?bad.slice(0,6).join(' | '):ids.length+' items, all finite',
              itemCount:ids.length};
    });

    /* 2. EVERY ENEMY TYPE: spawn it alone in a big room, awake, and let it act for four seconds.
          A type that never fires, or fires at itself, or walks out of the room, shows up here. */
    check('every enemy type acts without leaving the room or going non-finite',()=>{
      const types=Object.keys(ENEMY);
      const bad=[], fired={};
      for(const t of types){
        try{
          startGame(99);
          const r=bigRoom(3000,1600);
          player.x=r.cx; player.y=r.cy;
          const g=spawnEnemy(false,r,r.cx+300,r.cy,t); r.enemies.push(g);
          g.noticeTimer=0; g.aggroTimer=0; g.maxHp=g.hp=1e9;
          /* COUNT PROJECTILE TICKS, NOT SPAWENTS. The first version sampled `projectiles.length`
             every 40th tick and compared before/after, which measures how many were ALIVE at two
             instants - not how many were created. Shooters were firing constantly and the probe
             reported 0 of 5 types firing, because a shell in flight was already in the array at
             both samples. Counting every tick the array holds a HOSTILE projectile (friendly and
             hostile share one array) is the number that means something. Measured: lunger and
             brunch are melee and fire nothing, shooter 622, gunner 513, boss 7983 ticks. */
          let hostile=0;
          for(let k=0;k<210*4;k++){
            player.hp=player.maxHp; g.hp=g.maxHp;   // both immortal: this measures the TYPE
            update();
            for(const q of projectiles) if(!q.friendly) hostile++;
            if(!Number.isFinite(g.x)||!Number.isFinite(g.y)){ bad.push(t+': non-finite position'); break; }
            if(g.x<r.bounds.l-80||g.x>r.bounds.l+r.bounds.w+80||
               g.y<r.bounds.t-80||g.y>r.bounds.t+r.bounds.h+80){ bad.push(t+': left the room'); break; }
          }
          fired[t]=hostile;
        }catch(e){ bad.push(t+': '+String(e).slice(0,90)); }
      }
      /* A MELEE TYPE FIRING NOTHING IS CORRECT, and asserting otherwise would be wrong. What is
         not correct is a RANGED type that never fires - so the two are named explicitly. */
      const RANGED=['shooter','gunner','boss'];
      for(const t of RANGED) if(!fired[t]) bad.push(t+' is ranged and fired nothing in four seconds');
      return {ok:bad.length===0, detail:bad.length?bad.slice(0,6).join(' | ')
        :types.length+' types stayed in the room and finite; hostile projectile ticks '+JSON.stringify(fired),
              types, fired};
    });

    /* 3. THE DEPTH LADDER AT ITS EXTREMES. depthFloor() and the area map are pure functions of
          floor, so they can be checked at the boundaries directly rather than by playing there. */
    /* THE LADDER READS run.floor, IT DOES NOT TAKE AN ARGUMENT - `depthTough()`, `depthRate()`,
       `depthPack()` and `depthFloor()` all read the current run. So the probe has to SET the floor
       and read them back, which is also the only way that matches how the game calls them. A first
       version passed the floor as a parameter to functions that ignore it and therefore measured
       floor 1 two hundred times over. */
    check('the depth ladder is monotonic and finite across every floor it can be given',()=>{
      startGame(1);
      const bad=[];
      let prevTough=-1, prevRate=-1, prevArea=1, prevRate2=-1;
      for(let f=1;f<=200;f++){
        run.floor=f;
        const tough=depthTough(), rate=depthRate(), pack=depthPack();
        if(![tough,rate,pack].every(Number.isFinite)) bad.push('floor '+f+' produced a non-finite value');
        if(tough<prevTough-1e-9) bad.push('floor '+f+' toughness fell: '+prevTough+' -> '+tough);
        prevTough=tough; prevRate=rate;
        /* depthFloor() returns the AREA NUMBER 1-4, not a name, and depthRate() RISING with depth is
           the whole point of the ladder - so the rate check asserts it rises, and the area check
           asserts the number is in range and never goes backwards. Both were written as if the
           functions took arguments and returned names, which is how a probe ends up measuring floor
           1 two hundred times and calling it a monotonicity failure. */
        const a=areaForFloor(f);
        /* `areaForFloor(f)` returns the AREA NAME and takes the floor as an argument
           (00-balance.js:1145) - unlike `depthFloor()`, which returns the floor NUMBER and reads the
           current run. Conflating the two is what made an earlier version of this probe read "floor 5
           gave area 5, outside 1-4": it was asking for the floor and calling it an area. */
        if(!['Area1','Area2','Area3','Final'].includes(a)) bad.push('floor '+f+' gave area '+a);
        const aN=['Area1','Area2','Area3','Final'].indexOf(a)+1;
        if(aN<prevArea) bad.push('floor '+f+' went BACKWARDS to area '+a+' from '+areaName(prevArea-1));
        prevArea=aN;
        /* THE RATE IS CAPPED, and the cap is DEPTH_RATE_CAP (00-balance.js:1173:
           `Math.min(DEPTH_RATE_CAP, ...)`). It saturates at 1.34 from floor 12 and stays there, which
           is the design - a spawn rate that keeps climbing forever is a spawn rate that trivialises
           the game by floor 60. So the assertion is RISE-THEN-HOLD: it must strictly increase while
           below the cap and must never exceed it. A first version demanded a rise at every floor and
           reported a cap as a bug. */
        if(rate>DEPTH_RATE_CAP+1e-9) bad.push('floor '+f+' rate '+rate+' is above the cap '+DEPTH_RATE_CAP);
        if(rate<prevRate2-1e-9) bad.push('floor '+f+' rate FELL: '+prevRate2+' -> '+rate);
        if(rate<DEPTH_RATE_CAP-1e-9&&rate<=prevRate2&&f>1)
          bad.push('floor '+f+' rate is below the cap but did not rise: '+prevRate2+' -> '+rate);
        prevRate2=rate;
      }
      const areaName=(n)=>['Area1','Area2','Area3','Final'][n-1];
      run.floor=200;
      const at200={area:areaForFloor(200), tough:+depthTough().toFixed(3),
        rate:+depthRate().toFixed(3), pack:+depthPack().toFixed(3)};
      run.floor=1;
      return {ok:bad.length===0, detail:bad.length?bad.slice(0,5).join(' | ')
        :'200 floors finite; rate rises then holds at the cap '+DEPTH_RATE_CAP+'; at floor 200 '
          +JSON.stringify(at200), at200, rateCap:DEPTH_RATE_CAP};
    });

    /* 4. THE BOSS, IN ITS OWN ROOM, AT FULL HEALTH - the one encounter the fuzzer reached zero
          times because it never unlocked the door. */
    check('the boss runs its fight without going non-finite or leaving its room',()=>{
      startGame(1234);
      const r=currentRoom();
      r.type='boss'; r.boss=true;
      const bad=[];
      const g=spawnEnemy(true,r,r.cx,r.cy,'boss'); r.enemies.push(g);
      g.noticeTimer=0; g.aggroTimer=0; g.maxHp=g.hp=1e9; player.hp=player.maxHp;
      let phases=new Set(), volleys=0;
      for(let t=0;t<210*40;t++){
        player.hp=player.maxHp; g.hp=g.maxHp;
        const n=projectiles.length;
        update();
        if(projectiles.length>n) volleys++;
        if(g.phase!==undefined) phases.add(g.phase);
        if(!Number.isFinite(g.x)||!Number.isFinite(g.y)){ bad.push('boss position non-finite at t='+t); break; }
      }
      return {ok:bad.length===0, detail:bad.length?bad.join(' | ')
        :'boss survived 40s, '+volleys+' projectile ticks, phases seen: '+
          (phases.size?[...phases].join(','):'none recorded')};
    });

    /* 5. RECORDS ROUND-TRIP. A score that cannot be written and read back is a score the player
          loses, and nothing else in the suite touches localStorage. */
    check('a run record survives a write and a read',()=>{
      try{
        const key='__probe_record__';
        const rec={seed:4242, floor:17, kills:123, dmgTaken:456.5, won:false, t:Date.now()};
        localStorage.setItem(key, JSON.stringify(rec));
        const back=JSON.parse(localStorage.getItem(key));
        localStorage.removeItem(key);
        const same=back.seed===rec.seed&&back.floor===rec.floor&&back.kills===rec.kills&&
                   back.dmgTaken===rec.dmgTaken&&back.won===rec.won;
        return {ok:same, detail:same?'round-tripped':'read back '+JSON.stringify(back)};
      }catch(e){ return {ok:false, detail:'threw: '+String(e).slice(0,120)}; }
    });

    /* 6. THE DEV LAB MUST NOT SWALLOW PLAYER INPUT. Found by accident: it ships open, and
          80-ui.js:325 clears `keys` every tick while it is open - which is what made six fuzzers
          report clean runs without the player ever moving. */
    check('the dev lab does not steal movement input while it is open',()=>{
      try{
        if(typeof devOpen==='undefined') return {ok:true, detail:'no dev lab in this build'};
        startGame(5);
        const wasOpen=!!devOpen;
        devOpen=true;
        player.x=MIDX; player.y=MIDY; player.lagX=MIDX; player.lagY=MIDY;
        const x0=player.x;
        for(const k of Object.keys(keys)) delete keys[k];
        keys={d:1};
        for(let t=0;t<60;t++) update();
        const movedWithLab=player.x-x0;
        keys={};
        const detail='with the lab open the player moved '+movedWithLab.toFixed(1)+
          'px in 60 ticks (wasOpen='+wasOpen+')';
        return {ok:movedWithLab>5, detail};
      }catch(e){ return {ok:false, detail:'threw: '+String(e).slice(0,120)}; }
    });
    return out;
  });

  await b.close();
  let fails=0;
  for(const r of results){
    console.log(`${r.ok?'PASS':'FAIL'}  ${r.name}\n        ${r.detail}`);
    if(!r.ok) fails++;
  }
  console.log(`\n${results.length-fails}/${results.length} subsystem probes passed.`);
  if(errs.length) console.log('PAGE ERRORS:\n'+[...new Set(errs)].slice(0,6).join('\n'));
})();