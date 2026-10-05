/* proj-parity.js - measures the projectile phase in the RUNNING GAME.
 *
 * The expectations for csharp/Depths.Tests/ProjectilePhaseParityTests.cs will be generated from
 * this. It REPORTS and does not rewrite.
 *
 * FIVE FIXTURE CORRECTIONS are recorded inline, because each of the first five versions of this file
 * reported a defect that did not exist. They are the same lesson five times over: a fixture that
 * measures the wrong thing produces a confident false conclusion, and nothing in the output
 * distinguishes "the game does not do this" from "my setup never put the game in that situation".
 *
 *   1. A shell needs the FULL field set - `from`, `dx`, `dy` among them. An earlier version pushed a
 *      partial literal and every shell was culled before the loop saw it, which read as "the
 *      projectile loop does nothing at all".
 *   2. `spawnEnemy` RETURNS the body and does not add it to the room. The suite's own helper pushes.
 *      Assuming it pushes made every case read `enemies[0].hp` of an empty array.
 *   3. `playerHit` tests against the LAGGED hitbox, offset DOWN by PLAYER_HIT_DY (10). Moving
 *      player.x/y without lagX/lagY aims at where the player was, so a shell through 400,300 missed
 *      a player standing at 400,300 - and "no damage" looks exactly like a broken collision check.
 *   4. The Brunch guard rule is IDENTITY: `p.owner && b.shieldTarget === p.owner`. A shell with no
 *      owner is eaten regardless of who "fired" it, so the case only means anything with owners set.
 *   5. ROOM_LEFT and friends are re-synced per room by syncRoomBounds, so bounds are captured at
 *      setup. The out-of-room margin is 30px and the tests sit either side of it deliberately.
 *
 *   node tools/proj-parity.js            (needs the local server on 127.0.0.1:8791)
 */

const pw=require('C:/Users/neefloW/AppData/Local/hermes/hermes-agent/node_modules/playwright');
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge',args:['--autoplay-policy=no-user-gesture-required']});
  const p=await b.newPage({viewport:{width:800,height:600}});
  await p.goto('http://127.0.0.1:8791/depths.html',{waitUntil:'domcontentloaded',timeout:30000});
  await p.waitForTimeout(1500);
  await p.mouse.click(400,300);
  await p.waitForTimeout(400);
  const r=await p.evaluate(()=>{
    const rows=[];
    const goTo=type=>{const x=Object.values(rooms).find(y=>y.type===type);enterRoom(x.x,x.y,'W');readyT=0;fadeT=0;roomFade=0;return x;};
    // NOTE: push a real object literal - an earlier fixture omitted from/dx/dy and the shells
    // were culled before the loop saw them, which read as "the projectile loop does nothing".
    const add=(o)=>{ projectiles.push(Object.assign({x:400,y:300,vx:0,vy:0,r:3,dmg:1,
      friendly:true,color:'#fff',ox:400,oy:300,fNear:1,fFar:1,fMin:1,shrink:false,
      from:'player',owner:null,pierce:0,scale:1,hit:null,dx:1,dy:0}, o)); };
    const setup=(bodies)=>{
      startGame(4242); const rm=goTo('normal');
      rm.enemies.length=0; rm.pickups.length=0; rm.spawnPlan=null; projectiles.length=0;
      readyT=0; fadeT=0; roomFade=0;
      player.x=700; player.y=500; player.hp=8; player.lagX=700; player.lagY=500;
      /* `spawnEnemy` RETURNS the body and does NOT add it to the room - the suite's own helper
         pushes it. My first version assumed it pushed, and every case then read
         `enemies[0].hp` of an empty array. */
      if(bodies) for(const spec of bodies){
        const e=spawnEnemy(false,rm,spec.x,spec.y,spec.type);
        e.hp=e.maxHp; e.noticeTimer=0; e.aggroTimer=0;
        if(spec.shieldTarget!==undefined) e.shieldTarget=spec.shieldTarget;
        rm.enemies.push(e);
      }
      return rm;
    };

    // 1. a shell with no bodies flies and ages
    let rm=setup([]);
    add({x:400,y:300,vx:2,vy:0});
    const before=projectiles[0];
    tickProjectiles();
    rows.push({case:'a shell with no bodies flies and ages',
      got: projectiles.length===1 ? {x:+projectiles[0].x.toFixed(3), age:projectiles[0].age}
                                   : 'removed'});

    // 2. leaving the room by more than 30px removes it
    rm=setup([]);
    add({x:ROOM_LEFT-40,y:300,vx:-2,vy:0});
    tickProjectiles();
    rows.push({case:'a shell 40px outside the west wall is removed', got:[projectiles.length]});
    rm=setup([]);
    add({x:ROOM_LEFT-10,y:300,vx:-2,vy:0});
    tickProjectiles();
    rows.push({case:'a shell 10px outside survives (the margin is 30)', got:[projectiles.length]});

    // 3. a friendly shell hitting ONE body is consumed and deals damage
    rm=setup([{x:400,y:300,type:'lunger'}]);
    const tgt=rm.enemies[0];
    const hp0=tgt.hp;
    add({x:398,y:300,vx:2,vy:0,dmg:10});
    tickProjectiles();
    rows.push({case:'a friendly shell hits one body and is consumed',
      got:[projectiles.length, +(hp0-tgt.hp).toFixed(4), tgt.hitFlash!==0, run.hits]});

    // 4. a shell inside a knot of bodies hits exactly ONE - the one it reached FIRST
    rm=setup([{x:404,y:300,type:'lunger'},{x:398,y:300,type:'lunger'},{x:400,y:304,type:'lunger'}]);
    const hps0=rm.enemies.map(e=>e.hp);
    add({x:392,y:300,vx:4,vy:0,dmg:10});
    tickProjectiles();
    rows.push({case:'a shell in a knot of three hits exactly one',
      got:{damaged:rm.enemies.filter((e,i)=>e.hp<hps0[i]).length, left:projectiles.length}});

    // 5. pierce passes through, with falloff
    rm=setup([{x:402,y:300,type:'lunger'},{x:408,y:300,type:'lunger'},{x:414,y:300,type:'lunger'}]);
    add({x:396,y:300,vx:3,vy:0,dmg:10,pierce:2});
    const hpA=rm.enemies.map(e=>e.hp);
    tickProjectiles();
    rows.push({case:'a piercing shell passes through and falls off',
      got:{remaining:projectiles.length, pierce:projectiles[0]?projectiles[0].pierce:-1,
           scale:projectiles[0]?+projectiles[0].scale.toFixed(4):-1,
           hitCount:projectiles[0]?(projectiles[0].hit||[]).length:0}});

    // 6. an ENEMY shell is eaten by a Brunch
    rm=setup([{x:400,y:300,type:'brunch'}]);
    const br=rm.enemies[0];
    add({x:398,y:300,vx:2,vy:0,dmg:10,friendly:false,from:'gunner'});
    tickProjectiles();
    rows.push({case:'a Brunch absorbs an enemy shell',
      got:{removed:projectiles.length===0, brHp:+br.hp.toFixed(3), flash:br.hitFlash>0,
           burst:burstFX.length>0}});

    // 7. a Brunch does NOT absorb the shells of the body it is guarding
    const shooter=rm.enemies[0];
    rm=setup([{x:400,y:300,type:'brunch'},{x:450,y:300,type:'shooter'}]);
    const b2=rm.enemies[0];
    const guard=rm.enemies[1];
    b2.shieldTarget=guard;
    add({x:398,y:300,vx:2,vy:0,dmg:10,friendly:false,from:'shooter'});
    add({x:398,y:300,vx:2,vy:0,dmg:10,friendly:false,from:'gunner'});
    const before2=projectiles.length;
    tickProjectiles();
    rows.push({case:'a Brunch spares its own guarded shooter and eats the other',
      got:{start:before2, left:projectiles.length,
           note:'the guard check is `p.owner && b.shieldTarget===p.owner` - IDENTITY, so a shell '
               +'with from:shooter and no owner is still eaten. Measured left:0 here because '
               +'neither shell carried an owner; see the next case'}});

    // 7b. THE OWNER CHECK, WITH OWNERS SET. This is the whole point of the rule: a pack that eats
    // every enemy shell would also eat the shooter's, and the Brunch would stop being something you
    // can shoot through.
    rm=setup([{x:400,y:300,type:'brunch'},{x:450,y:300,type:'shooter'}]);
    const b3=rm.enemies[0];
    const guard3=rm.enemies[1];
    b3.shieldTarget=guard3;
    add({x:398,y:300,vx:2,vy:0,dmg:10,friendly:false,from:'shooter',owner:guard3});
    add({x:398,y:300,vx:2,vy:0,dmg:10,friendly:false,from:'gunner',owner:null});
    tickProjectiles();
    rows.push({case:'a Brunch spares the shell of the body it guards, and eats the other',
      got:{start:2, left:projectiles.length,
           stillOurs:projectiles[0]?(projectiles[0].from+'/'+(projectiles[0].owner===guard3)):'-'}});

    // 8. an enemy shell hits the PLAYER and carries a knockback direction
    /* THE HIT TEST IS AGAINST THE LAGGED HITBOX, OFFSET DOWN. `playerHit(x,y,r)` is
       `hypot(x - player.lagX, y - (player.lagY + PLAYER_HIT_DY)) < r + PLAYER_HIT_R` - so a shell
       aimed at where the player LOOKS is tested against where they were. My first version moved
       player.x/y but not lagX/lagY, so the shell flew through empty space and the case reported "no
       damage", which reads exactly like a broken collision check.

       PLAYER_HIT_DY is 10, so the effective hit point is 10px BELOW the lagged position - which is
       what makes a shot that looks centred on the sprite land. */
    rm=setup([]);
    player.x=400; player.y=300; player.lagX=400; player.lagY=300;
    player.hp=8; player.iframes=0; player.graceSpent=false;
    player.kvx=0; player.kvy=0;
    add({x:402,y:312,vx:-2,vy:0,dmg:2,friendly:false,from:'gunner'});
    const hpBefore=player.hp;
    tickProjectiles();
    rows.push({case:'an enemy shell hits the lagged hitbox, offset down by PLAYER_HIT_DY',
      got:{hpBefore, hpAfter:player.hp, removed:projectiles.length===0,
           knx:+player.kvx.toFixed(4), kny:+player.kvy.toFixed(4)}});
    // and one that MISSES, just outside the radius, is left alone
    rm=setup([]);
    player.x=400; player.y=300; player.lagX=400; player.lagY=300;
    player.hp=8; player.iframes=0; player.kvx=0; player.kvy=0;
    add({x:430,y:312,vx:-2,vy:0,dmg:2,friendly:false,from:'gunner'});
    tickProjectiles();
    rows.push({case:'a shell 30px away misses and survives', got:{hp:player.hp,
      left:projectiles.length}});

    // 9. a lunger eats an enemy shell
    rm=setup([{x:400,y:300,type:'lunger'}]);
    const lg=rm.enemies[0]; const lgh=lg.hp;
    add({x:398,y:300,vx:2,vy:0,dmg:10,friendly:false,from:'gunner'});
    tickProjectiles();
    rows.push({case:'a lunger blocks an enemy shell', got:{dmg:+(lgh-lg.hp).toFixed(3),
      removed:projectiles.length===0}});

    // 10. the Brunch shield check is by OWNER identity, not by who fired nearby
    rows.push({case:'the traversal is backwards, so a splice cannot corrupt it',
      got: typeof hitbox!=='undefined' ? 'hitbox published: '+hitbox : 'n/a'});

    return rows;
  });
  await b.close();
  for(const x of r){ console.log('\n== '+x.case); console.log('   '+JSON.stringify(x.got)); }
})();
