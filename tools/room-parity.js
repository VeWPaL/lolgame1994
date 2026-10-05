/* room-parity.js - generates the expectations for csharp/Depths.Tests/RoomPhaseParityTests.cs
 * from the RUNNING GAME.
 *
 * It drives the browser's tickRoom() through the same fixtures the C# tests use and prints what the
 * game did. Nothing here is transcribed from source: a hand-written expectation is a second opinion
 * about what the code should do, and two implementations disagreeing is the only comparison worth
 * making.
 *
 * It REPORTS and does not rewrite. A generator that writes its own expectations cannot catch a
 * regression in the thing it is generating from - it would simply agree with itself.
 *
 *   node tools/room-parity.js            (needs the local server on 127.0.0.1:8791)
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
    const put=(kind,x,y,r,extra)=>Object.assign({x,y,r,kind},extra||{});
    const setup=()=>{ startGame(4242); const rm=goTo('normal');
      rm.enemies.length=0; rm.pickups.length=0; readyT=0; fadeT=0; roomFade=0;
      player.x=400; player.y=355; player.hp=8; player.armor=0; player.hasSilver=false; player.hasGold=false;
      player.weaponIdx=0; player.cooldown=0; player.altMode='blast'; player.r=13;
      player.blinkRegen=0; run.hook=false;
      return rm; };

    // 1. the half-blink refund, and that it happens ONCE
    let rm=setup();
    const blink0=player.blinkRegen;
    tickRoom();
    const after1=player.blinkRegen;
    tickRoom(); tickRoom();
    rows.push({case:'cleared room refunds half a blink, once',
      got:[blink0, +after1.toFixed(2), +player.blinkRegen.toFixed(2)],
      want:[0, +((735*0.5)).toFixed(2), +((735*0.5)).toFixed(2)]});

    // 2. heart: at full health it is LEFT, below full it heals 2 and is removed
    rm=setup();
    rm.pickups.push(put('heart',400,355,16));
    player.hp=8; player.maxHp=8;
    tickRoom();
    rows.push({case:'a heart at full health is not collected',
      got:[player.hp, rm.pickups.length], want:[8, 1]});
    player.hp=5;
    tickRoom();
    rows.push({case:'a heart below full heals 2 and is taken',
      got:[player.hp, rm.pickups.length], want:[7, 0]});

    // 3. armour, both directions
    rm=setup(); player.armor=4;
    rm.pickups.push(put('armor',400,355,16));
    tickRoom();
    rows.push({case:'armour at the cap is not collected', got:[player.armor, rm.pickups.length], want:[4,1]});
    player.armor=1;
    tickRoom();
    rows.push({case:'armour below the cap adds 2', got:[player.armor, rm.pickups.length], want:[3,0]});

    // 4. keys
    rm=setup();
    rm.pickups.push(put('key',400,355,14));
    rm.pickups.push(put('goldkey',400,355,16));
    tickRoom();
    rows.push({case:'both keys are taken', got:[player.hasSilver, player.hasGold, rm.pickups.length],
               want:[true,true,0]});

    // 5. the weapon swap parks the old gun, held, so it cannot be re-taken
    rm=setup(); player.weaponIdx=0;
    rm.pickups.push(put('weapon',400,355,16,{w:2}));
    tickRoom();
    const parked=rm.pickups[0];
    rows.push({case:'a weapon swap parks the old one, held',
      got:[player.weaponIdx, rm.pickups.length, parked?parked.kind:'-', parked?parked.w:-1,
           parked?parked.hold:'-', +player.cooldown.toFixed(2)],
      want:[2, 1, 'weapon', 0, true, +Math.min(0, 18.2).toFixed(2)]});
    // and stepping off then back on re-collects it
    player.x=600;
    tickRoom();
    const afterOff = parked? parked.hold : null;
    player.x=400;
    tickRoom();
    rows.push({case:'stepping off clears hold, so the swap settles',
      got:[afterOff, player.weaponIdx, rm.pickups.length], want:[false, 0, 1]});

    // 6. the exit radius is TIGHTER, and touching it DESCENDS rather than removing the pickup
    //
    // Two things were wrong with the first version of this fixture, and both made it report the
    // opposite of what happened. It asserted on the pickup list, but the original calls descend()
    // and RETURNS - the portal stays in the list by design, so a distance of zero read as "not
    // collected". And it used `tickRoom` on a run whose floor could not change, so even a correct
    // descent was invisible. The observable is the FLOOR.
    for(const d of [12, 10, 9, 0]){
      const rr=setup();
      rr.pickups.push(put('exit',400+d,355,30));
      const dist=Math.hypot(rr.pickups[0].x-player.x, rr.pickups[0].y-player.y);
      const floor0=run.floor;
      tickRoom();
      rows.push({case:'exit at '+d+'px (dist '+dist.toFixed(2)+') '+(d<10?'descends':'does not descend'),
        got:[run.floor-floor0, rr.pickups.length], want:[d<10?1:0, 1]});
    }
// and compare with a normal pickup at the same 12px
    rm=setup();
    rm.pickups.push(put('heart',400+12,355,16));
    player.hp=5;
    tickRoom();
    rows.push({case:'a heart 12px away IS collected (r + player.r = 29)',
      got:[player.hp, rm.pickups.length], want:[7, 0]});

    // 7. the reward drops, once each
    rm=setup(); rm.keyReward=true; rm.goldReward=true;
    rm.enemies.length=0;
    // AWAY from the room centre. The reward drops AT MIDX,MIDY, and a player standing there collects
    // it on the same tick it is created - so with the player on the centre this fixture observed an
    // empty list and looked like the drop was broken. It is not; it is one tick of collection.
    player.x=120; player.y=200;
    tickRoom();
    const gotKinds=rm.pickups.map(x=>x.kind+':'+x.r+':'+x.x+','+x.y).sort();
    tickRoom();
    rows.push({case:'rewards drop once, at the room centre, with their own radii',
      got:gotKinds, want:['goldkey:16:400,355','key:14:400,355'],
      note:'second tick must add nothing: '+rm.pickups.length+' pickups after two ticks'});

    // 8. the boss exit opens once
    rm=setup(); rm.type='boss';
    rm.enemies.length=1;
    tickRoom();
    const beforeBoss=rm.pickups.filter(x=>x.kind==='exit').length;
    rm.enemies.length=0;
    tickRoom();
    const afterBoss=rm.pickups.filter(x=>x.kind==='exit').length;
    tickRoom();
    rows.push({case:'a cleared boss room opens one exit, once',
      got:[beforeBoss, afterBoss, rm.pickups.filter(x=>x.kind==='exit').length],
      want:[0,1,1]});

    // 9. death wins ties over the boss exit
    rm=setup(); rm.type='boss'; rm.enemies.length=0; player.hp=0;
    tickRoom();
    rows.push({case:'a dead player ends the run and does not open an exit',
      got:[state, rm.pickups.filter(x=>x.kind==='exit').length], want:['gameover', 0]});

    return rows;
  });
  await b.close();
  let bad=0;
  for(const x of r){
    const ok = JSON.stringify(x.got)===JSON.stringify(x.want);
    if(!ok) bad++;
    console.log((ok?'ok   ':'DIFF ')+x.case);
    if(!ok){ console.log('      got  '+JSON.stringify(x.got)); console.log('      want '+JSON.stringify(x.want)); }
    if(x.note) console.log('      ('+x.note+')');
  }
  console.log('\n'+r.length+' cases, '+bad+' differ');
})();
