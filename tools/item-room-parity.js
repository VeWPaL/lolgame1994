/* item-room-parity.js - an item room's pedestals in the running game, for
 * csharp/Depths.Tests/ItemRoomParityTests.cs.  node tools/item-room-parity.js  (server on 8791)
 *
 * REPORT-ONLY. startGame(seed); optionally give items first (they change luck and what is excluded);
 * enterRoom() straight into the floor's item room from the west. Recorded: both streams' totals since
 * the reseed, and every pickup placed (kind, x, y, weapon index, item id), plus the stats it read.
 */
const path=require('path');
const pw=require(path.join(process.env.LOCALAPPDATA,'hermes','hermes-agent','node_modules','playwright'));
const SCEN=[
  {seed:1, give:[]}, {seed:42, give:[]}, {seed:4242, give:[]}, {seed:777, give:[]},
  {seed:31337, give:['lucky_coin','glass_wands']},
  {seed:99999, give:['brass_compass','glass_wands']},
];
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:720}});
  await p.goto('http://127.0.0.1:'+(process.env.DEPTHS_PORT||8791)+'/depths.html',{waitUntil:'domcontentloaded',timeout:60000});
  await p.waitForFunction('typeof startGame==="function"',null,{timeout:60000});
  const out=await p.evaluate((SCEN)=>{
    const res=[];
    for(const s of SCEN){
      startGame(s.seed); if(typeof devOpen!=='undefined') devOpen=false;
      for(const id of s.give) Items.give(id);
      const ir=Object.values(rooms).find(x=>x.type==='item');
      enterRoom(ir.x,ir.y,'W');
      res.push({seed:s.seed,give:s.give,jit:Rnd.calls.jitter,run:Rnd.calls.run,luck:Stats.value('luck'),strength:Stats.value('strength'),
        pickups:currentRoom().pickups.map(q=>[q.kind,q.x,q.y,q.w===undefined?-1:q.w,q.id||''])});
    }
    return res;
  },SCEN);
  await b.close();
  console.log(JSON.stringify(out));
})();
