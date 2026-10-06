/* playtest-report.js - turns playtest/<label>.json files into one HTML page a person can read.

   node tools/playtest-report.js current                 one build
   node tools/playtest-report.js baseline current        A/B: B is compared against A

   Writes playtest/report-<labels>.html (self-contained: thumbnails are inlined). The page leads with the
   numbers that answer "is it more fun / fairer / harder", then the per-run detail and filmstrips. */
const fs=require('fs'), path=require('path');
const DIR=path.join(__dirname,'..','playtest');
const labels=process.argv.slice(2); if(!labels.length) labels.push('current');
const sets=labels.map(l=>JSON.parse(fs.readFileSync(path.join(DIR,l+'.json'),'utf8')));
const TICK=210;

const esc=s=>String(s).replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
const mean=a=>a.length?a.reduce((x,y)=>x+y,0)/a.length:0;
const median=a=>{ if(!a.length) return 0; const b=[...a].sort((x,y)=>x-y), m=b.length>>1; return b.length%2?b[m]:(b[m-1]+b[m])/2; };
const f1=v=>(Math.round(v*10)/10).toFixed(1);

/* one row of numbers per (label, profile) */
function stats(runs){
  const floorsDone=runs.flatMap(r=>r.floors.filter(f=>f.bossKilled));
  const all=runs.flatMap(r=>r.floors);
  const dmgSrc={}; for(const r of runs) for(const [k,v] of Object.entries(r.dmgBySource)) dmgSrc[k]=(dmgSrc[k]||0)+v;
  const healSrc={}; for(const r of runs) for(const [k,v] of Object.entries(r.healBy||{})) healSrc[k]=(healSrc[k]||0)+v;
  const mins=runs.reduce((s,r)=>s+r.ticks,0)/TICK/60;
  return {
    n:runs.length,
    floor:median(runs.map(r=>r.floor)),
    deaths:runs.filter(r=>r.end==='death').length,
    stuck:runs.filter(r=>r.end==='stuck').length,
    errors:runs.filter(r=>r.errors.length).length,
    minPerFloor:mean(floorsDone.map(f=>f.ticks/TICK/60)),
    dmgPerFloor:mean(all.map(f=>f.dmg)),
    healPerFloor:mean(all.map(f=>f.healed||0)),
    bossS:mean(floorsDone.map(f=>f.bossTicks/TICK)),
    acc:runs.reduce((s,r)=>s+r.hits,0)/Math.max(1,runs.reduce((s,r)=>s+r.shots,0)),
    killsPerMin:runs.reduce((s,r)=>s+r.kills,0)/Math.max(1e-9,mins),
    lowHp:mean(all.map(f=>Math.min(f.hpIn,f.hpOut===null?f.hpIn:f.hpOut))),
    dmgSrc, healSrc,
  };
}
const profiles=[...new Set(sets.flatMap(s=>s.profiles))];
const table=sets.map(s=>({label:s.label,all:stats(s.runs),
  by:Object.fromEntries(profiles.map(p=>[p,stats(s.runs.filter(r=>r.profile===p))]))}));

/* which way is "better" is not always obvious, so the delta column only says what moved */
const METRICS=[
  ['floor','median floor reached',v=>f1(v)],
  ['deaths','deaths',v=>String(v)],
  ['stuck','stuck runs (bot or softlock)',v=>String(v)],
  ['minPerFloor','minutes per cleared floor',v=>f1(v)],
  ['dmgPerFloor','damage taken per floor (hp, 2 = 1 heart)',v=>f1(v)],
  ['healPerFloor','healing per floor (hp)',v=>f1(v)],
  ['bossS','boss fight, seconds',v=>f1(v)],
  ['acc','accuracy',v=>Math.round(v*100)+'%'],
  ['killsPerMin','kills per minute',v=>f1(v)],
];
function cell(a,b,k,fmt){
  if(b===undefined) return `<td>${fmt(a[k])}</td>`;
  const d=b[k]-a[k], same=Math.abs(d)<(k==='acc'?0.005:0.05);
  return `<td>${fmt(a[k])}</td><td>${fmt(b[k])}</td><td class="d${same?'':' moved'}">${same?'=':(d>0?'+':'')+(k==='acc'?Math.round(d*100)+'%':f1(d))}</td>`;
}
const AB=table.length===2;
function summary(pick){
  const head=AB?`<tr><th></th><th>${esc(table[0].label)}</th><th>${esc(table[1].label)}</th><th>change</th></tr>`
               :`<tr><th></th><th>${esc(table[0].label)}</th></tr>`;
  return `<table>${head}${METRICS.map(([k,name,fmt])=>`<tr><th>${name}</th>${cell(pick(table[0]),AB?pick(table[1]):undefined,k,fmt)}</tr>`).join('')}</table>`;
}
function bars(src,total){
  const items=Object.entries(src).sort((a,b)=>b[1]-a[1]);
  const max=Math.max(1,...items.map(x=>x[1]));
  return `<div class="bars">${items.map(([k,v])=>`<div class="bar"><span class="bl">${esc(k)}</span><span class="bt"><span class="bf" style="width:${(v/max*100).toFixed(1)}%"></span></span><span class="bv">${Math.round(v/total*100)}%</span></div>`).join('')}</div>`;
}
function runsDetail(s){
  return s.runs.map(r=>`<details><summary><b>${esc(r.profile)}</b> seed ${r.seed} &middot; floor ${r.floor} &middot; ${esc(r.end)}${r.end==='death'&&r.cause?' ('+esc(r.cause[0])+')':''} &middot; ${f1(r.ticks/TICK/60)} min${r.errors.length?' &middot; <span class="bad">ERROR</span>':''}</summary>
    <table class="small"><tr><th>floor</th><th>min</th><th>dmg</th><th>healed</th><th>boss s</th><th>hp in &rarr; out</th></tr>
    ${r.floors.map(f=>`<tr><td>${f.floor}</td><td>${f1(f.ticks/TICK/60)}</td><td>${f1(f.dmg)}</td><td>${f1(f.healed||0)}</td><td>${f1(f.bossTicks/TICK)}</td><td>${f1(f.hpIn)} &rarr; ${f1(f.hpOut===null?f.hpIn:f.hpOut)}</td></tr>`).join('')}</table>
    ${r.items.length?`<p>items: ${r.items.map(esc).join(', ')}</p>`:''}${r.errors.length?`<p class="bad">${esc(r.errors[0])}</p>`:''}
    ${r.end==='stuck'?`<p class="bad">stuck: ${esc(JSON.stringify(r.cause))}</p>`:''}
    <div class="film">${(r.thumbs||[]).map(t=>`<figure><img src="${t.img}" alt="${esc(t.why)}" loading="lazy"><figcaption>${esc(t.why)} &middot; ${t.t}s</figcaption></figure>`).join('')}</div>
  </details>`).join('');
}

const html=`<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Depths Playtest</title><style>
:root{--bg:#f6f4ef;--fg:#1d1b18;--mute:#6b665d;--line:#d9d4c9;--card:#fff;--acc:#8a5a2b;--up:#2f7d4f;--down:#b3412f;--bar:#c08a52}
@media (prefers-color-scheme:dark){:root:not([data-theme="light"]){--bg:#14151a;--fg:#e8e6e1;--mute:#9a968c;--line:#2c2e36;--card:#1c1e25;--acc:#e0b07a;--up:#6fcf97;--down:#ff8a75;--bar:#b8834d}}
:root[data-theme="dark"]{--bg:#14151a;--fg:#e8e6e1;--mute:#9a968c;--line:#2c2e36;--card:#1c1e25;--acc:#e0b07a;--up:#6fcf97;--down:#ff8a75;--bar:#b8834d}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:15px/1.5 system-ui,sans-serif}
main{max-width:980px;margin:0 auto;padding:24px 16px 64px}h1{margin:0 0 4px;font-size:24px}h2{margin:32px 0 8px;font-size:18px;color:var(--acc)}
.meta{color:var(--mute);font-size:13px}table{border-collapse:collapse;width:100%;background:var(--card);margin:8px 0}
th,td{padding:6px 10px;border-bottom:1px solid var(--line);text-align:right;font-variant-numeric:tabular-nums}th:first-child{text-align:left;font-weight:500}
table.small{font-size:13px}.d.moved{color:var(--acc);font-weight:600}.bad{color:var(--down);font-weight:600}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(330px,1fr));gap:16px}.card{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:12px}
.bars{display:flex;flex-direction:column;gap:4px;font-size:13px}.bar{display:grid;grid-template-columns:120px 1fr 40px;gap:8px;align-items:center}
.bt{background:var(--line);height:10px;border-radius:5px;overflow:hidden}.bf{display:block;height:100%;background:var(--bar)}.bv{text-align:right;color:var(--mute)}
details{background:var(--card);border:1px solid var(--line);border-radius:8px;margin:6px 0;padding:6px 10px}summary{cursor:pointer}
.film{display:flex;gap:8px;overflow-x:auto;padding:6px 0}.film figure{margin:0;flex:0 0 240px}.film img{width:240px;border-radius:4px;display:block}
.film figcaption{font-size:12px;color:var(--mute)}
</style></head><body><main>
<h1>Depths playtest${AB?': '+esc(table[0].label)+' vs '+esc(table[1].label):''}</h1>
<p class="meta">${sets.map(s=>`${esc(s.label)}: ${s.runs.length} runs, seeds ${s.seeds.join(', ')}, up to ${s.minutes} sim-min each${s.query?', ?'+esc(s.query):''}, ${esc(s.date.slice(0,16).replace('T',' '))}`).join('<br>')}<br>
A bot plays every run through the game's own input. Profiles differ in reaction time, aim error, dodging and spacing. It does not hunt secrets.</p>
${sets.some(s=>s.pageErrors.length)?`<p class="bad">page errors: ${esc(sets.flatMap(s=>s.pageErrors).slice(0,3).join(' | '))}</p>`:''}
<h2>All profiles</h2>${summary(t=>t.all)}
<h2>By profile</h2><div class="grid">${profiles.map(p=>`<div class="card"><b>${esc(p)}</b>${summary(t=>t.by[p])}</div>`).join('')}</div>
<h2>Where the damage comes from</h2><div class="grid">${table.map(t=>{const tot=Object.values(t.all.dmgSrc).reduce((a,b)=>a+b,0)||1;return `<div class="card"><b>${esc(t.label)}</b>${bars(t.all.dmgSrc,tot)}</div>`;}).join('')}</div>
<h2>Where the healing comes from</h2><div class="grid">${table.map(t=>{const tot=Object.values(t.all.healSrc).reduce((a,b)=>a+b,0)||1;return `<div class="card"><b>${esc(t.label)}</b>${bars(t.all.healSrc,tot)}</div>`;}).join('')}</div>
${sets.map(s=>`<h2>Runs: ${esc(s.label)}</h2>${runsDetail(s)}`).join('')}
</main></body></html>`;
const out=path.join(DIR,'report-'+labels.join('-vs-')+'.html');
fs.writeFileSync(out,html);
console.log('wrote '+path.relative(process.cwd(),out)+' ('+Math.round(html.length/1024)+' KB)');
