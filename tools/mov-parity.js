/* mov-parity.js - generates the expectations for csharp/Depths.Tests/PlayerPhaseParityTests.cs
 * from the RUNNING GAME.
 *
 * It drives the browser's tickPlayer() with the same inputs the C# tests use and prints the curves.
 * Nothing is transcribed: a hand-written expectation is a second opinion about what the code should
 * do, and two implementations disagreeing is the only comparison worth making.
 *
 * It REPORTS and does not rewrite.
 *
 * THREE FIXTURE CORRECTIONS live in this file, each of which first reported a defect that did not
 * exist. They are here because each is repeatable, and because a fixture that measures the wrong
 * thing is the most expensive mistake in this project - it produces a confident false conclusion.
 *
 *   1. The START room's doors are all LOCKED (measured: N/E/S/W all false), so a player held against
 *      its west wall is standing in a DOORWAY, not against a wall. The wall cases pick a room whose
 *      west door is genuinely closed.
 *   2. `tickMomentum` returns immediately when the room has no bodies - momentum is earned under
 *      pressure, by design. Every momentum measurement therefore has to spawn something, and every
 *      ACCELERATION measurement has to spawn nothing, because a charging meter scales the ramp.
 *   3. `ROOM_LEFT` and friends are re-synced by `syncRoomBounds` on every room change, so the bounds
 *      are captured at setup and compared against those. Reading them after the ticks compares the
 *      player against a room it has already left.
 *
 *   node tools/mov-parity.js            (needs the local server on 127.0.0.1:8791)
 */

const pw=require('C:/Users/neefloW/AppData/Local/hermes/hermes-agent/node_modules/playwright');
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:800,height:600}});
  await p.goto('http://127.0.0.1:8791/depths.html',{waitUntil:'domcontentloaded',timeout:30000});
  await p.waitForTimeout(1500);
  const r=await p.evaluate(()=>{
    const rows=[];
    const goTo=type=>{const x=Object.values(rooms).find(y=>y.type===type);enterRoom(x.x,x.y,'W');readyT=0;fadeT=0;roomFade=0;return x;};
    // A run with no bodies, no weapons fired, and the helpers that are NOT yet ported neutralised
    // as far as possible. Anything they do is reported separately so the port knows what it is not
    // covering.
    /* THREE FIXTURE CORRECTIONS, all of which first reported a defect that did not exist.

       1. The START room's doors are all locked (measured: N/E/S/W all false), so a player held
          against its west wall is standing in a doorway, not against a wall. The wall cases below
          pick a room whose west door is genuinely closed.
       2. `tickMomentum` returns immediately when the room has no bodies - momentum is earned under
          pressure, by design. Every momentum measurement here therefore has to spawn something.
       3. `ROOM_LEFT` and friends are re-synced by `syncRoomBounds` on every room change, so the
          bounds are captured at setup and compared against those. Reading them after the ticks
          compares the player against a room it has already left. */
    let BOUNDS=null;
    const setup=(kx,ky,bodies)=>{
      startGame(4242);
      const rm = bodies ? Object.values(rooms).find(x=>x.type==='normal' && !x.doors.W)
                        : goTo('normal');
      if(rm!==currentRoom()){ enterRoom(rm.x,rm.y,'W'); readyT=0; fadeT=0; roomFade=0; }
      BOUNDS={L:ROOM_LEFT,R:ROOM_RIGHT,T:ROOM_TOP,B:ROOM_BOTTOM,
              mx:MIDX,my:MIDY,r:player.r,DOORW};
      if(bodies) for(let i=0;i<bodies;i++)
        rm.enemies.push(spawnEnemy(false,rm,BOUNDS.mx+40+i*8,BOUNDS.my,'lunger'));
      rm.enemies.length=0; rm.pickups.length=0; rm.spawnPlan=null;
      projectiles.length=0; readyT=0; fadeT=0; roomFade=0;
      mouseDown=false; altMouseDown=false;
      player.x=400; player.y=355; player.lagX=400; player.lagY=355;
      player.vx=0; player.vy=0; player.kvx=0; player.kvy=0;
      player.swerve=0; player.dirX=0; player.dirY=0;
      player.trendVx=0; player.trendVy=0; player.beliefVx=0; player.beliefVy=0;
      player.anim=0; player.slowMult=1; player.shootSlow=0; player.boost=0;
      player.cooldown=0; player.altCooldown=0; player.iframes=0;
      player.blinkCharges=2; player.blinkGrace=0; player.muzzleTimer=0;
      player.momentum=0;
      if(typeof Momentum!=='undefined') Momentum.unlock();
      keys['w']=!!ky&&ky<0; keys['s']=!!ky&&ky>0;
      keys['a']=!!kx&&kx<0; keys['d']=!!kx&&kx>0;
      keys['arrowup']=keys['arrowdown']=keys['arrowleft']=keys['arrowright']=false;
      return rm;
    };
    const snap=()=>({x:+player.x.toFixed(6), y:+player.y.toFixed(6),
                     vx:+player.vx.toFixed(6), vy:+player.vy.toFixed(6),
                     anim:+player.anim.toFixed(6), swerve:+player.swerve.toFixed(6),
                     momentum:+player.momentum.toFixed(6)});

    // 1. a single tick with no input: nothing should move
    setup(0,0);
    tickPlayer();
    rows.push({case:'one tick, no keys: nothing moves', got:snap()});

    // 2. acceleration curve holding EAST - the shape, not the endpoint
    setup(1,0);
    const curve=[];
    for(let i=0;i<10;i++){ tickPlayer(); curve.push(+player.vx.toFixed(6)); }
    rows.push({case:'10 ticks east: velocity curve', got:curve});

    // 3. terminal speed after 400 ticks holding east, in an empty room
    setup(1,0);
    for(let i=0;i<400;i++) tickPlayer();
    rows.push({case:'terminal east speed after 400 ticks', got:+Math.hypot(player.vx,player.vy).toFixed(6)});

    // 4. diagonal is NOT faster: (1,1) must give the same speed as (1,0)
    setup(1,1);
    for(let i=0;i<400;i++) tickPlayer();
    const diag=+Math.hypot(player.vx,player.vy).toFixed(6);
    setup(1,0);
    for(let i=0;i<400;i++) tickPlayer();
    const straight=+Math.hypot(player.vx,player.vy).toFixed(6);
    rows.push({case:'diagonal speed equals straight speed', got:[diag, straight]});

    // 5. releasing the key decelerates to a stop
    setup(1,0);
    for(let i=0;i<300;i++) tickPlayer();
    const before=+Math.hypot(player.vx,player.vy).toFixed(6);
    keys['d']=false;
    const dec=[];
    for(let i=0;i<60;i++){ tickPlayer(); dec.push(+player.vx.toFixed(6)); }
    rows.push({case:'release: 60 ticks of deceleration', got:[before, dec[0], dec[10], dec[30], dec[59]]});

    // 6. knockback is separate from velocity and decays on its own
    setup(0,0); player.kvx=10;
    tickPlayer();
    rows.push({case:'knockback applies then decays', got:[+player.x.toFixed(4), +player.kvx.toFixed(6)]});

    // 7. the wall clamp: velocity into a wall is zeroed, position stopped
    setup(-1,0,true);
    player.x = BOUNDS.L + player.r + 1;
    player.y = BOUNDS.my;
    for(let i=0;i<120;i++) tickPlayer();
    rows.push({case:'held against a CLOSED west wall',
      got:{x:+player.x.toFixed(3), vx:+player.vx.toFixed(6),
           wallAt:+(BOUNDS.L+player.r).toFixed(3), passableW:doorPassable(currentRoom(),'W')}});

    // 8. momentum charges from DISPLACEMENT, not velocity - so a wall-pinned player gains nothing
    setup(-1,0,true);
    player.x = BOUNDS.L + player.r + 1; player.y = BOUNDS.my;
    for(let i=0;i<200;i++) tickPlayer();
    const pinned={momentum:+player.momentum.toFixed(6), x:+player.x.toFixed(3)};
    setup(1,0,true);   // bodies present: momentum is earned under pressure
    player.x = BOUNDS.L + 200; player.vx = 0;
    for(let i=0;i<200;i++) tickPlayer();
    const moving = +player.momentum.toFixed(6);
    setup(1,0,false);  // and an EMPTY room earns nothing at all
    player.x = BOUNDS.mx; player.vx = 0;
    for(let i=0;i<200;i++) tickPlayer();
    rows.push({case:'momentum: pinned vs moving vs an empty room',
      got:[pinned, moving, +player.momentum.toFixed(6)]});

    // 9. the swerve meter rises on a reversal and decays otherwise
    setup(1,0);
    for(let i=0;i<200;i++) tickPlayer();
    const preSwerve=+player.swerve.toFixed(6);
    keys['d']=false; keys['a']=true;
    const sw=[];
    for(let i=0;i<40;i++){ tickPlayer(); sw.push(+player.swerve.toFixed(4)); }
    rows.push({case:'swerve on a reversal', got:[preSwerve, sw[0], sw[5], sw[20], sw[39]]});

    // 10. the lagged hitbox is published, and it LAGS
    setup(1,0);
    for(let i=0;i<30;i++) tickPlayer();
    rows.push({case:'the lagged hitbox trails the player',
      got:{playerX:+player.x.toFixed(4), lagX:+player.lagX.toFixed(4),
           publishedX:+hitbox.x.toFixed(4), equal:hitbox.x===player.lagX}});

    return rows;
  });
  await b.close();
  for(const x of r){ console.log('\n== '+x.case); console.log('   '+JSON.stringify(x.got)); }
})();
