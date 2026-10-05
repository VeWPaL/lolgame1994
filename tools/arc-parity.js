/* arc-parity.js - measures brunchArcSlot in the running game: the shield arc geometry.
 *
 * The expectations for csharp/Depths.Tests/BrunchArcParityTests.cs are generated from this.
 * REPORT-ONLY.
 *
 * AT FULL PRECISION. The first version of this probe rounded with toFixed(2) and reported 49.64
 * where the game produces 49.642207661 - and a CORRECT port then looked 0.002 wrong against a 1e-6
 * tolerance, which cost six failures and a false conclusion about the port. The tolerance belongs to
 * the PRECISION OF THE MEASUREMENT, not to the size of the disagreement you expected.
 *
 * WHAT IT MEASURED, and the arrangement is not what the code looks like:
 *
 *     target (200,300), player (400,300), target radius 10
 *
 *     n=2    50              67
 *     n=3    49.642207661    66.32762017     49.642207661
 *     n=6    49.642207661    65.603039328    50    67    49.642207661    65.603039328
 *     n=12   49.642207661    65.603039328    ... (all twelve on those two radii)
 *
 * Two things fall out of that table and both were bugs once:
 *
 *   - A 12-pack is the SAME WIDTH as a 6-pack. The surplus stacks behind the front rank at the same
 *     angle, so a large shield is two deep rather than six stragglers standing in empty floor beside
 *     their own wall. Every slot of a 12-pack lands on one of two radii.
 *
 *   - The arc forms a QUARTER of the way from the target to the player (0.25 of the gap), because
 *     the thing it has to fit between is a distance that is not fixed and gets small. A constant 118px
 *     radius put the wall BEHIND the player on a 164px gap, blocking line of sight 27-39% of the way
 *     rather than closing it.
 *
 * And one that is a PORT bug rather than a game bug, found here: the original writes
 * `((slot/2)|0)-((cols-1)/2)`, where the `|0` truncates the FIRST term only. Written as C# integer
 * division it truncates BOTH, and for n=3 that mirrors the whole arrangement - `cols-1` is 2, which
 * divides evenly at n=6 and oddly at n=3, so an even-pack-only suite would never have seen it.
 *
 *   node tools/arc-parity.js             (needs the local server on 127.0.0.1:8791)
 */

const pw=require('C:/Users/neefloW/AppData/Local/hermes/hermes-agent/node_modules/playwright');
(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:800,height:600}});
  await p.goto('http://127.0.0.1:8791/depths.html',{waitUntil:'domcontentloaded',timeout:30000});
  await p.waitForTimeout(1500);
  const r=await p.evaluate(()=>{
    const out={};
    // FULL precision this time. The first probe rounded with toFixed(2), which made a correct port
    // look 0.002 wrong - and the honest tolerance is the rounding, not 1e-6.
    out.n6=[]; for(let s=0;s<6;s++){const q=brunchArcSlot(200,300,400,300,6,s,10);
      out.n6.push([s, +(q.x-200).toFixed(9), +Math.hypot(q.x-200,q.y-300).toFixed(9)]);}
    out.n3=[]; for(let s=0;s<3;s++){const q=brunchArcSlot(200,300,400,300,3,s,10);
      out.n3.push([s, +(q.x-200).toFixed(9)]);}
    const c=brunchArcSlot(200,300,300,300,6,0,10), f=brunchArcSlot(200,300,700,300,6,0,10);
    out.close=[+c.x.toFixed(9), +c.y.toFixed(9)];
    out.far=[+f.x.toFixed(9), +f.y.toFixed(9)];
    out.closeOff=+Math.abs(c.y-300).toFixed(9);
    out.farOff=+Math.abs(f.y-300).toFixed(9);
    return out;
  });
  await b.close();
  console.log(JSON.stringify(r,null,1));
})();
