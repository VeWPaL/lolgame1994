/* playtest.js - plays whole runs of the game with a bot and records what a playtester would notice.

   node tools/playtest.js [--root DIR] [--label NAME] [--seeds 1,7,42] [--profiles novice,average,skilled]
                          [--minutes 20] [--shots 8] [--query ab=leash]

   It serves DIR itself (default: the repo root), so a baseline snapshot and the working tree can be
   played side by side with no server set up. Output: playtest/<label>.json; render it, or compare
   two labels, with tools/playtest-report.js.

   The bot drives the real input surface - `keys`, `mouse`, `mouseDown`, and keydown events for
   blink and Q - and calls update() directly, so a 20-minute run takes seconds. Three profiles stand
   in for three players: they differ in reaction time, aim error, how often they dodge and how
   well they keep their distance. The bot does not look for secrets.

   A run that makes no progress for STUCK_S simulated seconds is ended and reported as stuck. That
   means either the bot is out of its depth or the game has softlocked, and both need looking at. */
const http=require('http'), fs=require('fs'), path=require('path');
const pw=require(path.join(process.env.LOCALAPPDATA,'hermes','hermes-agent','node_modules','playwright'));

const arg=(k,d)=>{ const i=process.argv.indexOf('--'+k); return i>0?process.argv[i+1]:d; };
const ROOT=path.resolve(arg('root',path.join(__dirname,'..')));
const LABEL=arg('label','current');
const SEEDS=arg('seeds','1,7,42,777,2024,31337').split(',').map(Number);
const PROFILES=arg('profiles','novice,average,skilled').split(',');
const MINUTES=+arg('minutes','20');
const SHOTS=+arg('shots','8');
const QUERY=arg('query','');
const OUT=path.join(__dirname,'..','playtest');

const TYPES={'.html':'text/html','.js':'text/javascript','.css':'text/css','.json':'application/json'};
const server=http.createServer((q,s)=>{
  const f=path.join(ROOT,decodeURIComponent(q.url.split('?')[0]));
  if(!f.startsWith(ROOT)){ s.writeHead(403); return s.end(); }
  fs.readFile(f,(e,d)=>{ if(e){ s.writeHead(404); return s.end(); }
    s.writeHead(200,{'Content-Type':TYPES[path.extname(f)]||'application/octet-stream','Cache-Control':'no-store'}); s.end(d); });
});

/* ---- everything below runs INSIDE the page, once per run ---- */
function playOne(cfg){
  const P={
    novice: {react:55,aimErr:0.14,dodge:0.2,band:[110,220],blast:0.25,heal:1},
    average:{react:38,aimErr:0.07,dodge:0.55,band:[150,260],blast:0.55,heal:2},
    skilled:{react:24,aimErr:0.03,dodge:0.9,band:[170,280],blast:0.85,heal:2},
  }[cfg.profile];
  const TICK=210, CAP=Math.round(cfg.minutes*60*TICK), STUCK=60*TICK;
  let bs=(cfg.seed*2654435761+cfg.profile.length*977)>>>0;
  const rnd=()=>{ bs^=bs<<13; bs^=bs>>>17; bs^=bs<<5; return (bs>>>0)/4294967296; };
  const gauss=()=>{ let u=0; for(let i=0;i<4;i++) u+=rnd(); return (u-2)*0.87; };
  const press=k=>{ window.dispatchEvent(new KeyboardEvent('keydown',{key:k}));
                   window.dispatchEvent(new KeyboardEvent('keyup',{key:k})); };

  try{ Sound.setMuted(true); }catch(e){}
  startGame(cfg.seed);
  if(typeof devOpen!=='undefined') devOpen=false;
  paused=false;

  const out={seed:cfg.seed,profile:cfg.profile,floor:1,ticks:0,end:'timeout',cause:null,
    floors:[],dmgBySource:{},healed:0,healBy:{},hits:0,shots:0,kills:0,blinks:0,dodgeBlinks:0,actives:0,
    items:[],weapons:[],secrets:0,shotsTaken:[],errors:[]};
  let F=null;
  const newFloor=()=>{ F={floor:run.floor,ticks:0,rooms:0,cleared:0,dmg:0,bossTicks:0,
    bossKilled:false,hpIn:player.hp,maxHpIn:player.maxHp,hpOut:null,healed:0}; out.floors.push(F); };
  newFloor();

  /* attribution: wrap the one function every hit goes through */
  const realDamage=damagePlayer;
  damagePlayer=function(amount,kx,ky,force){
    const before=player.hp+player.armor;
    const r=realDamage.apply(this,arguments);
    const lost=before-(player.hp+player.armor);
    if(lost>0){
      const rm=currentRoom();
      let src='contact', best=1e9, near=null;
      for(const p of projectiles) if(!p.friendly){
        const d=Math.hypot(p.x-player.x,p.y-player.y); if(d<best){best=d; near=p;} }
      if(near&&best<40) src='shot:'+(near.owner&&near.owner.type||'?');
      else { let bd=1e9, ne=null; for(const e of rm.enemies){ const d=Math.hypot(e.x-player.x,e.y-player.y);
        if(d<bd){bd=d; ne=e;} } src='contact:'+(ne?ne.type:'?'); }
      out.dmgBySource[src]=(out.dmgBySource[src]||0)+lost;
      F.dmg+=lost;
    }
    return r;
  };
  const realGive=Items.give;
  Items.give=function(id){ const g=realGive.apply(this,arguments); if(g&&g.taken) out.items.push(id+'@'+run.floor); return g; };

  const thumbs=[]; const thumb=(why)=>{
    if(thumbs.length>=cfg.shots) return;
    try{ render(); const c=document.createElement('canvas'); c.width=480; c.height=300;
      c.getContext('2d').drawImage(canvas,0,0,480,300);
      thumbs.push({why,floor:run.floor,t:+(out.ticks/TICK).toFixed(1),img:c.toDataURL('image/jpeg',0.6)}); }catch(e){}
  };

  const born=new WeakMap();       // when the bot first SAW each hostile shot - its reaction clock
  const ignored=new WeakSet();    // pickups it chose not to take, or has already dealt with
  const tookItemIn=new Set();
  let decideT=0, strafe=rnd()<0.5?1:-1, strafeT=0, aimJ=0, moveV=[0,0], fire=false;
  let lastHpSum=0, lastProgress=0, lastKey='', lastCleared=0, lowHpShot=false, bossShot=false, prevKills=0;
  thumb('floor 1');

  const key2=(x,y)=>x+','+y;
  function route(){
    /* BFS over the floor. A door is walkable for planning if it exists and is either open or one
       the bot holds the key for. Goal order is how a careful first-time player clears a floor:
       every reachable unvisited room, the item room once there is a silver key, then the boss. */
    const start=currentRoom(), q=[start], prev=new Map([[start,null]]);
    while(q.length){ const r=q.shift();
      for(const d of ['N','S','E','W']){ if(!r.doors[d]) continue;
        const [nx,ny]=neighbor(r.x,r.y,d); const n=rooms[key2(nx,ny)]; if(!n||prev.has(n)) continue;
        if(doorSealed(r,d)&&!hasKeyFor(r,d)) continue;
        prev.set(n,[r,d]); q.push(n); } }
    const reach=[...prev.keys()];
    const pick=reach.find(r=>!r.visited&&r.type!=='boss')
      ||reach.find(r=>r.type==='item'&&!tookItemIn.has(r))
      ||reach.find(r=>r.type==='boss');
    if(!pick||pick===start) return null;
    let n=pick; while(prev.get(n)&&prev.get(n)[0]!==start) n=prev.get(n)[0];
    return prev.get(n)[1];
  }
  function steer(tx,ty,stop){
    const dx=tx-player.x, dy=ty-player.y;
    keys.w=dy<-stop; keys.s=dy>stop; keys.a=dx<-stop; keys.d=dx>stop;
  }

  for(let t=0;t<CAP;t++){
    if(state!=='playing'){ out.end=state==='gameover'?'death':state; break; }
    const r=currentRoom();
    out.ticks++; F.ticks++;
    if(r.type==='boss'&&r.enemies.length){ F.bossTicks++; if(!bossShot&&F.bossTicks>TICK*3){ bossShot=true; thumb('boss fight'); } }
    if(!lowHpShot&&player.hp<=1&&player.hp>0){ lowHpShot=true; thumb('last heart'); }
    const rk=key2(r.x,r.y)+'@'+run.floor;
    if(rk!==lastKey){ lastKey=rk; F.rooms++; lastProgress=t; }
    if(run.kills!==prevKills){ prevKills=run.kills; lastProgress=t; }
    // damage dealt is progress too: a deep boss fight runs past a minute without a kill
    { let hpSum=0; for(const e of r.enemies) hpSum+=Math.max(0,e.hp); if(hpSum<lastHpSum-1e-9) lastProgress=t; lastHpSum=hpSum; }
    if(t-lastProgress>STUCK){ out.end='stuck'; out.cause={room:r.type,enemies:r.enemies.length,
      pickups:r.pickups.map(p=>p.kind),gold:player.hasGold,silver:player.hasSilver,
      bossUnlocked,itemUnlocked}; thumb('stuck'); break; }

    for(const k of ['w','a','s','d']) keys[k]=false;
    mouseDown=false; altMouseDown=false;
    if(trans){ try{ update(); }catch(e){ out.errors.push(String(e).slice(0,200)); break; } continue; }

    const live=r.enemies.filter(e=>e.hp>0);
    if(live.length){
      /* the fight. Re-decide every `react` ticks, as a person does; hold the decision between. */
      for(const p of projectiles) if(!p.friendly&&!born.has(p)) born.set(p,t);
      const target=live.find(e=>e.type==='boss')||live.reduce((a,e)=>Math.hypot(e.x-player.x,e.y-player.y)<Math.hypot(a.x-player.x,a.y-player.y)?e:a);
      if(t>=decideT){
        decideT=t+P.react;
        aimJ=gauss()*P.aimErr;
        if(t>=strafeT){ strafe=-strafe; strafeT=t+TICK*(1+rnd()*2); }
        const dx=target.x-player.x, dy=target.y-player.y, dist=Math.hypot(dx,dy)||1;
        const ux=dx/dist, uy=dy/dist;
        let mx=-uy*strafe, my=ux*strafe;                    // circle the target
        if(dist<P.band[0]){ mx-=ux*1.5; my-=uy*1.5; }        // too close: back off
        else if(dist>P.band[1]){ mx+=ux*1.2; my+=uy*1.2; }   // too far: close in
        const b=r.bounds, m=70;                              // and stay off the walls
        if(player.x<b.l+m) mx+=1; if(player.x>b.r-m) mx-=1;
        if(player.y<b.t+m) my+=1; if(player.y>b.b-m) my-=1;
        moveV=[mx,my];
        fire=dist<420;
        const crowd=live.filter(e=>Math.hypot(e.x-player.x,e.y-player.y)<150).length;
        if(crowd>=3&&rnd()<P.blast) altMouseDown=true;
        if(player.hp<=P.heal&&Items.useActive&&rnd()<0.5){ const h=player.hp; press('q'); if(player.hp>h) out.actives++; }
      }
      /* dodging: only a shot the bot has had time to react to, heading for it */
      for(const p of projectiles){ if(p.friendly||t-born.get(p)<P.react) continue;
        const rx=p.x-player.x, ry=p.y-player.y, vv=p.vx*p.vx+p.vy*p.vy; if(!vv) continue;
        const tc=-(rx*p.vx+ry*p.vy)/vv; if(tc<0||tc>30) continue;
        const cx=rx+p.vx*tc, cy=ry+p.vy*tc;
        if(Math.hypot(cx,cy)<p.r+player.r+8){
          const s=(cx*p.vy-cy*p.vx)>0?1:-1, vn=Math.sqrt(vv);
          moveV=[-p.vy/vn*s*2,p.vx/vn*s*2];
          if(tc<12&&player.blinkCharges>0&&rnd()<P.dodge*0.15){
            keys.w=moveV[1]<-0.3; keys.s=moveV[1]>0.3; keys.a=moveV[0]<-0.3; keys.d=moveV[0]>0.3;
            const c=player.blinkCharges; press('Shift'); if(player.blinkCharges<c){ out.blinks++; out.dodgeBlinks++; }
          }
          break;
        }
      }
      keys.w=moveV[1]<-0.3; keys.s=moveV[1]>0.3; keys.a=moveV[0]<-0.3; keys.d=moveV[0]>0.3;
      updateCamera();
      const a=Math.atan2(target.y-player.y,target.x-player.x)+aimJ;
      const d=Math.hypot(target.x-player.x,target.y-player.y);
      mouse.x=player.x+Math.cos(a)*d-cam.x; mouse.y=player.y+Math.sin(a)*d-cam.y;
      mouseDown=fire;
    } else {
      if(r.cleared&&r.enemies.length===0&&lastCleared!==r){ lastCleared=r; F.cleared++; }
      /* the quiet room: pickups worth having, then the way on */
      const want=r.pickups.filter(p=>{
        if(p.hold||ignored.has(p)) return false;
        if(p.kind==='heart') return player.hp<player.maxHp;
        if(p.kind==='armor') return player.armor<MAX_ARMOR;
        if(p.kind==='item') return !tookItemIn.has(r);
        if(p.kind==='weapon'||p.kind==='hook'||p.kind==='blast') return false;
        return p.kind!=='exit';
      });
      const exit=r.pickups.find(p=>p.kind==='exit');
      const goal=want[0]||(exit&&!route()?exit:null)||exit;
      if(goal&&(goal!==exit||!want.length)){
        steer(goal.x,goal.y,3);
        if(goal.kind==='item'&&Math.hypot(goal.x-player.x,goal.y-player.y)<goal.r+player.r+2) tookItemIn.add(r);
        if(goal===exit){ F.hpOut=player.hp; F.bossKilled=true; }
      } else {
        const d=route();
        if(d){ const pt=doorPoint(d);
          steer(pt[0]+Math.sign(pt[0]-MIDX)*40,pt[1]+Math.sign(pt[1]-MIDY)*40,4); }
      }
    }
    const floorBefore=run.floor, hpBefore=player.hp+player.armor, pk0=r.pickups.map(p=>p.kind);
    try{ update(); }catch(e){ out.errors.push(String(e).slice(0,200)); break; }
    { const gain=player.hp+player.armor-hpBefore; if(gain>0&&run.floor===floorBefore){
        const gone=pk0.filter(k=>!currentRoom().pickups.some(p=>p.kind===k)); const by=gone[0]||('active');
        out.healed+=gain; F.healed+=gain; out.healBy[by]=(out.healBy[by]||0)+gain; } }
    if(run.floor!==floorBefore){ newFloor(); bossShot=false; lowHpShot=false; thumb('floor '+run.floor); lastProgress=t; }
  }
  if(state==='gameover') thumb('death');
  out.floor=run.floor; out.kills=run.kills; out.shots=run.shots; out.hits=run.hits;
  out.secrets=run.secret?1:0; out.weapon=WEAPONS[player.weaponIdx].name;
  out.cause=out.cause||(out.end==='death'?Object.entries(out.dmgBySource).sort((a,b)=>b[1]-a[1])[0]:null);
  if(F.hpOut===null) F.hpOut=player.hp;
  damagePlayer=realDamage; Items.give=realGive;
  paused=true;
  out.thumbs=thumbs;
  return out;
}

(async()=>{
  await new Promise(r=>server.listen(0,'127.0.0.1',r));
  const port=server.address().port;
  const b=await pw.chromium.launch({channel:'msedge'});
  const p=await b.newPage({viewport:{width:1280,height:800}});
  const errs=[]; p.on('pageerror',e=>errs.push(String(e).slice(0,240)));
  await p.goto(`http://127.0.0.1:${port}/depths.html${QUERY?'?'+QUERY:''}`,{waitUntil:'load',timeout:60000});
  await p.waitForFunction('typeof startGame==="function"&&typeof update==="function"',null,{timeout:60000});
  await p.evaluate(`window.__playOne=${playOne.toString()}`);
  const runs=[]; const t0=Date.now();
  for(const profile of PROFILES) for(const seed of SEEDS){
    const r=await p.evaluate(cfg=>window.__playOne(cfg),{seed,profile,minutes:MINUTES,shots:SHOTS});
    runs.push(r);
    console.log(`${profile.padEnd(8)} seed ${String(seed).padEnd(6)} floor ${r.floor}  ${r.end.padEnd(7)} ${(r.ticks/210/60).toFixed(1)}min  `+
      `dmg ${JSON.stringify(r.dmgBySource)}${r.errors.length?'  ERRORS '+r.errors[0]:''}`);
  }
  await b.close(); server.close();
  fs.mkdirSync(OUT,{recursive:true});
  const file=path.join(OUT,LABEL+'.json');
  fs.writeFileSync(file,JSON.stringify({label:LABEL,root:ROOT,query:QUERY,date:new Date().toISOString(),
    minutes:MINUTES,seeds:SEEDS,profiles:PROFILES,pageErrors:[...new Set(errs)],runs}));
  console.log(`\n${runs.length} runs in ${((Date.now()-t0)/1000).toFixed(0)}s -> ${path.relative(process.cwd(),file)}`);
  if(errs.length) console.log('PAGE ERRORS:\n'+[...new Set(errs)].slice(0,6).join('\n'));
})();
