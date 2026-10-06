/* THE PARITY AUDIT. Reads the live `depths.html?parity` output and the C# test tables and reports
   every row that disagrees. It REPORTS - it never writes the C# side, because a generator that
   regenerates its own expectations asserts nothing (which is why 97-parity.js is careful to say its
   own output asserts nothing on purpose).

   WHY IT EXISTS. The workflow PORTED.md describes is "load the page, paste the rows over the C# file".
   That workflow was assumed correct for the life of the project and was wrong three times in one
   session: the page emitted the wrong AREA for four floors, the wrong DRAW COUNT for every spawn row,
   and eleven floors where the table had eighteen. Two of those would have been pasted, and pasting the
   area table would have broken a correct port. A generator that emits less than the table pins makes
   a partial paste look like a complete one.

   USAGE - it needs a static server on 8791 and Edge via Playwright, the same two dependencies
   verify.ps1 already has, neither of which is added to this repo:

       node tools/parity-audit.js

   PATHS are resolved relative to this file rather than hardcoded, so a checkout anywhere works.
   PLAYWRIGHT is required from the Hermes install for the same reason verify.ps1 requires Edge from
   there: this repo takes no new dependency to run a maintenance script. */
const path=require('path'), fs=require('fs');
const ROOT=path.resolve(__dirname,'..');
const DIR=path.join(ROOT,'csharp','Depths.Tests')+path.sep;
const HERMES=process.env.HERMES_HOME ||
  path.join(process.env.LOCALAPPDATA||'', 'hermes');
let pw;
try{ pw=require(path.join(HERMES,'hermes-agent','node_modules','playwright')); }
catch(e){
  console.error('playwright not found under '+path.join(HERMES,'hermes-agent','node_modules')+
    '\nSet HERMES_HOME, or run this from the Hermes install as verify.ps1 does.');
  process.exit(2);
}

/* THE PARITY AUDIT, AS A SCRIPT RATHER THAN A THING I DID ONCE.

   `depths.html?parity` emits five tables and PORTED.md tells a human to paste them over the C# test
   file. That workflow had been assumed correct for the life of the project and was wrong three times
   in one session - the generator emitted the wrong AREA for four floors, the wrong DRAW COUNT for
   every spawn row, and eleven floors where the table had eighteen. Two of those would have been
   pasted, and a paste of the area table would have broken a correct port.

   So the diff is automated. It reads the live page and the C# file and reports, per table, which
   rows disagree. It is NOT wired into verify.ps1, and the reason matters: the C# side cannot be
   regenerated from the page, only checked against it, and a gate that regenerates its own
   expectations asserts nothing. This reports; a human decides. */

(async()=>{
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:720}});
  const errs=[];
  p.on('pageerror',e=>errs.push(e.message));
  await p.goto((process.env.DEPTHS_URL||'http://127.0.0.1:8791/depths.html')+'?parity',{waitUntil:'load',timeout:60000});
  await p.waitForFunction('window.__parityTable!==undefined',null,{timeout:120000});
  const live=await p.evaluate(()=>window.__parityTable);
  await b.close();
  if(errs.length){ console.log('THE PAGE THREW:\n'+errs.join('\n')); process.exit(1); }

  const sections={}; let cur=null;
  for(const ln of live.split(/\r?\n/)){
    const m=ln.match(/^\/\/ ---- (\w+) ----/);
    if(m){ cur=m[1]; sections[cur]=[]; continue; }
    if(cur) sections[cur].push(ln);
  }

  const report=[];

  /* 1. the depth ladder: floor -> tough, rate, pack, bodies0, area. Six columns on both sides. */
  {
    const js={};
    for(const ln of sections.DepthLadderParityTests||[]){
      const m=ln.match(/floor\s+(\d+)\s+tough\s+([\d.]+)\s+rate\s+([\d.]+)\s+pack\s+([\d.]+)\s+bodies0\s+([\d.]+)\s+area\s+(Area\d|Final)/);
      if(m) js[m[1]]={tough:m[2],rate:m[3],pack:m[4],bodies0:m[5],area:m[6]};
    }
    const cs=fs.readFileSync(DIR+'DepthLadderParityTests.cs','utf8');
    const csr={};
    for(const m of cs.matchAll(/TestCase\((\d+),\s*([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*Area\.(\w+)\)/g))
      csr[m[1]]={tough:m[2],rate:m[3],pack:m[4],bodies0:m[5],area:m[6]};
    const bad=[];
    for(const f of new Set([...Object.keys(js),...Object.keys(csr)])){
      if(!js[f]){ bad.push(`floor ${f}: on the page's terms only`); continue; }
      if(!csr[f]){ bad.push(`floor ${f}: in C# only`); continue; }
      for(const k of ['tough','rate','pack','bodies0','area'])
        if(js[f][k]!==csr[f][k]) bad.push(`floor ${f} ${k}: page ${js[f][k]} vs C# ${csr[f][k]}`);
    }
    report.push(['DepthLadderParityTests', Object.keys(js).length, Object.keys(csr).length, bad]);
  }

  /* 2. the area table: floor -> area. Compared as PAIRS, because comparing number sets ignores
        pairing and both files carry overlapping incidental digits - the first version of this said
        all five tables differed, which was the script being wrong rather than the tables. */
  {
    const js={}; let on=false;
    for(const ln of live.split(/\r?\n/)){
      if(/\/\/ ---- \w+ ----/.test(ln)){ on=/AreaTests/.test(ln); continue; }
      if(!on) continue;
      const m=ln.match(/floor\s+(\d+)\s+(Area\d|Final)/);
      if(m) js[m[1]]=m[2];
    }
    const cs=fs.readFileSync(DIR+'AreaTests.cs','utf8');
    const csr={};
    for(const m of cs.matchAll(/TestCase\((\d+),\s*Area\.(\w+)\)/g)) csr[m[1]]=m[2];
    const bad=[];
    for(const f of new Set([...Object.keys(js),...Object.keys(csr)]))
      if(js[f]!==csr[f]) bad.push(`floor ${f}: page ${js[f]||'(absent)'} vs C# ${csr[f]||'(absent)'}`);
    report.push(['AreaTests', Object.keys(js).length, Object.keys(csr).length, bad]);
  }

  /* 3. the spawn plan: seed/floor/dir -> draws. The C# rows are multi-line attributes, so this is
        parsed by paren depth rather than by regex - four regexes failed on this file, all because of
        where the closing bracket sits. */
  {
    const js={}; let on=false;
    for(const ln of live.split(/\r?\n/)){
      if(/\/\/ ---- \w+ ----/.test(ln)){ on=/SpawnPlanParityTests/.test(ln); continue; }
      if(!on) continue;
      const m=ln.match(/seed (\d+) floor (\d+) from (\w)\s+draws (\d+)/);
      if(m) js[`${m[1]}/${m[2]}/${m[3]}`]=+m[4];
    }
    const lines=fs.readFileSync(DIR+'SpawnPlanParityTests.cs','utf8').split(/\r?\n/);
    const csr={}; let acc=null, depth=0;
    for(const raw of lines){
      const ln=raw.trim();
      if(!acc){ const m=ln.match(/^\[TestCase\((\d+)u,\s*(\d+),\s*Dir\.(\w+),\s*(\d+),?$/);
        if(m){ acc={k:`${m[1]}/${m[2]}/${m[3]}`,d:+m[4]}; depth=1; } continue; }
      depth += (ln.match(/\(/g)||[]).length - (ln.match(/\)/g)||[]).length;
      if(depth<=0){ csr[acc.k]=acc.d; acc=null; depth=0; }
    }
    const bad=[];
    for(const k of new Set([...Object.keys(js),...Object.keys(csr)])){
      if(js[k]===undefined) bad.push(`${k}: on the page's terms only`);
      else if(csr[k]===undefined) bad.push(`${k}: in C# only`);
      else if(js[k]!==csr[k]) bad.push(`${k}: page ${js[k]} vs C# ${csr[k]}`);
    }
    report.push(['SpawnPlanParityTests', Object.keys(js).length, Object.keys(csr).length, bad]);
  }

  /* 4. the room-scaled numbers, at three room sizes. */
  {
    const js={}; let on=false;
    for(const ln of live.split(/\r?\n/)){
      if(/\/\/ ---- \w+ ----/.test(ln)){ on=/RoomScaledParityTests/.test(ln); continue; }
      if(!on) continue;
      const m=ln.match(/(\d+)x(\d+)\s+aggro (\d+)\s+deadzone (\d+)\s+rampBase (\d+)\s+full (\d+)/);
      if(m) js[`${m[1]}/${m[2]}`]={aggro:+m[3],dz:+m[4],fb:+m[5],full:+m[6]};
    }
    const cs=fs.readFileSync(DIR+'RoomScaledParityTests.cs','utf8');
    const csr={};
    for(const m of cs.matchAll(/TestCase\((\d+),\s*(\d+),\s*(\d+),\s*(\d+),\s*(\d+),\s*(\d+)\)/g))
      csr[`${m[1]}/${m[2]}`]={aggro:+m[3],dz:+m[4],fb:+m[5],full:+m[6]};
    const bad=[];
    for(const k of new Set([...Object.keys(js),...Object.keys(csr)])){
      if(!js[k]){ bad.push(`${k}: on the page's terms only`); continue; }
      if(!csr[k]){ bad.push(`${k}: in C# only`); continue; }
      for(const f of ['aggro','dz','fb','full'])
        if(js[k][f]!==csr[k][f]) bad.push(`${k} ${f}: page ${js[k][f]} vs C# ${csr[k][f]}`);
    }
    report.push(['RoomScaledParityTests', Object.keys(js).length, Object.keys(csr).length, bad]);
  }

  /* 5. the generator: seed -> rooms, draws, signature. The signature is the load-bearing column and
        the longest string in the project, so it is compared whole rather than sampled. */
  {
    const js={}; let on=false;
    for(const ln of live.split(/\r?\n/)){
      if(/\/\/ ---- \w+ ----/.test(ln)){ on=/GeneratorParityTests/.test(ln); continue; }
      if(!on) continue;
      const h=ln.match(/seed (\d+)\s+rooms (\d+)\s+draws (\d+)/);
      if(h) js[h[1]]={rooms:+h[2],draws:+h[3],sig:null};
      const s=ln.match(/^\/\/\s+"([^"]+)"/);
      if(s && cur){ /* handled below */ }
    }
    // signatures sit on the line AFTER their header, so pair them in a second pass
    const raw=(sections.GeneratorParityTests||[]);
    for(let i=0;i<raw.length;i++){
      const h=raw[i].match(/seed (\d+)\s+rooms (\d+)\s+draws (\d+)/);
      if(!h) continue;
      const nx=raw[i+1]||'';
      const sm=nx.match(/"([^"]+)"/);
      if(js[h[1]] && sm) js[h[1]].sig=sm[1];
    }
    const csLines=fs.readFileSync(DIR+'GeneratorParityTests.cs','utf8').split(/\r?\n/);
    const csr={}; let acc=null, depth=0;
    for(const raw2 of csLines){
      const ln=raw2.trim();
      if(!acc){ const m=ln.match(/^\[TestCase\((\d+)u,\s*(\d+),\s*(\d+),?$/);
        if(m){ acc={k:m[1],rooms:+m[2],draws:+m[3],sig:null}; depth=1; } continue; }
      const sm=ln.match(/"([^"]+)"/);
      if(sm && acc && !acc.sig) acc.sig=sm[1];
      depth += (ln.match(/\(/g)||[]).length - (ln.match(/\)/g)||[]).length;
      if(depth<=0){ csr[acc.k]=acc; acc=null; depth=0; }
    }
    const bad=[];
    for(const k of new Set([...Object.keys(js),...Object.keys(csr)])){
      const j=js[k], c=csr[k];
      if(!j){ bad.push(`seed ${k}: in C# only`); continue; }
      if(!c){ bad.push(`seed ${k}: on the page's terms only`); continue; }
      if(j.rooms!==c.rooms) bad.push(`seed ${k} rooms: page ${j.rooms} vs C# ${c.rooms}`);
      if(j.draws!==c.draws) bad.push(`seed ${k} draws: page ${j.draws} vs C# ${c.draws}`);
      if(j.sig!==c.sig) bad.push(`seed ${k} signature: ${j.sig===c.sig?'same':'DIFFERENT'}`+
        (j.sig&&c.sig?` (page ${j.sig.length} chars, C# ${c.sig.length})`:''));
    }
    report.push(['GeneratorParityTests', Object.keys(js).length, Object.keys(csr).length, bad]);
  }

  let total=0;
  console.log('PARITY AUDIT - the live page against the C# test tables\n');
  for(const [name,jn,cn,bad] of report){
    total+=bad.length;
    console.log(`${bad.length?'DIFFERS':'matches '}  ${name.padEnd(24)} ${String(jn).padStart(3)} page rows, ${String(cn).padStart(3)} C# rows`);
    for(const b of bad) console.log('      '+b);
  }
  console.log(`\n${total} difference(s) across ${report.length} tables.`);
  console.log('This script REPORTS and does not regenerate: the C# side is checked against the page,');
  console.log('never written from it. A generator that regenerates its own expectations asserts nothing.');
})();