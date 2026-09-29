/* ==============================================================================================
   20-world  -  global state and the dungeon generator

   The map graph, room growth, doors, keys, and the spawn plan. Pure simulation: no drawing, no
   input, no platform calls. Contains the global state block, which in C# becomes a GameState class
   rather than nine module-level variables.
   ============================================================================================== */
let rooms, cur, player, projectiles, mouse, mouseDown, altMouseDown, keys, state, records;
let allRooms=[], bossFront={};
let showPerf=false, showSpawn=false, lastT=0, perfSamples=[], trans=null, readyT=0, fadeT=0, fadeTicks=1, entryDir='N';
let roomFade=0, frameCount=0, dashFX=[], burstFX=[], hookFields=[], bossUnlocked=false, itemUnlocked=false;
let unlockDoor=null, unlockT=0, bossWarnT=0, secretFound=false, bossWarned=false;
// Which slice of the circle the next lunger to spawn takes. Walked by the golden angle so that no two
// bodies ever land on the same slot however many have spawned, and not reset per room - a room's
// pack is spaced by its order of arrival, which is what stops a wave arriving as one front.
let FLANK_CURSOR=0;
let paused=false, acc=0, run=null, lastRun=null;
// A locked door no longer opens the instant you hold the key, and it no longer pins you in place
// while it works. Touching the door starts the lock; it finishes on its own whether you stay or
// walk off, which is what turns it into a decision you make rather than a button you hold.
const UNLOCK_TIME=sec(0.5), UNLOCK_RANGE=64, BOSS_WARN_TIME=sec(3.2);

/* ---------- dungeon shape ----------
   Not a rosette of corridors. The map is a few *runs*: each one is a line of fight rooms you walk
   from end to end, winding but never doubling back on itself, with a reward waiting at the end.
   Runs are allowed to fork off the side of each other, so the whole thing reads as three or four
   directions out of the start and a handful of decisions along the way. That is the loop the game
   wants: fight, fight, fight, payout, and a map small enough to hold in your head. */
const ARM_DIRS=['N','E','S','W'];
const TURNS={N:['W','E'],S:['E','W'],E:['N','S'],W:['S','N']};
function newRoom(x,y,type){return {x,y,doors:{},type,visited:false,spawned:false,enemies:[],pickups:[],spawnPlan:null,keyReward:false,goldReward:false,cleared:false,secret:null,armed:false};}
// a direction that leaves `room` through an unused side into empty grid
function freeDir(rs,room,prefer){
  const open=d=>{const [nx,ny]=neighbor(room.x,room.y,d);return !room.doors[d]&&nx>=0&&ny>=0&&nx<GRID&&ny<GRID&&!rs[key(nx,ny)];};
  const free=ARM_DIRS.filter(open);
  if(!free.length) return null;
  // prefer leaving sideways off the host so a fork reads as a fork, not as a stub
  const side=free.filter(d=>d!==OPP[prefer]&&d!==prefer);
  return (side.length?side:free)[(Rnd.run()*(side.length||free.length))|0];
}
// walks one corridor out of (x,y), returning the rooms it placed plus its tip. the run travels in
// straight segments of two or three rooms and bends once at the end of each, so a run reads as a
// path you walk rather than a spiral. strict forbids rooms that touch anything but their parent,
// which is how the loose pass finds room for the long runs.
function grow(rs,x,y,dir,len,strict,bias){
  const path=[];
  let cx=x,cy=y,lastTurn=-1,seg=1+((Rnd.run()*2.4)|0);
  for(let step=0;step<len;step++){
    let order;
    if(seg<=0){
      // bend: take a side, usually keeping the same handedness, so the run makes Ls and Us
      const first=lastTurn<0?(Rnd.run()<0.5?0:1):(Rnd.run()<bias?lastTurn:1-lastTurn);
      order=[TURNS[dir][first],TURNS[dir][1-first],dir];
      seg=1+((Rnd.run()*2.4)|0);
    } else {
      order=[dir,dir,...TURNS[dir]];
      seg--;
    }
    let placed=false;
    for(const d of order){
      const [nx,ny]=neighbor(cx,cy,d);
      if(nx<0||ny<0||nx>=GRID||ny>=GRID||rs[key(nx,ny)]) continue;
      if(strict && ['N','S','E','W'].some(k=>{const [ax,ay]=neighbor(nx,ny,k);return (ax!==cx||ay!==cy)&&rs[key(ax,ay)];})) continue;
      const room=newRoom(nx,ny,'normal');
      rs[key(cx,cy)].doors[d]=true; room.doors[OPP[d]]=true;
      rs[key(nx,ny)]=room;
      if(d!==dir) lastTurn=TURNS[dir].indexOf(d);
      cx=nx; cy=ny; dir=d; path.push(room); placed=true; break;
    }
    if(!placed) return null;
  }
  return {path,tip:rs[key(cx,cy)]};
}
function tryBuild(strict){
  const rs={};
  const startRoom=newRoom(START,START,'start');
  startRoom.visited=true; startRoom.spawned=true;
  rs[key(START,START)]=startRoom;
  // one run: `fights` fight rooms walked out of `host`, with the reward room as its last
  const run=(host,fights,bias)=>{
    // leave through a side, not back the way we came
    const back=host.doors.E?'W':host.doors.W?'E':host.doors.S?'N':'S';
    const d=freeDir(rs,host,back);
    if(!d) return null;
    const built=grow(rs,host.x,host.y,d,fights+1,strict,bias||0.55);
    if(!built) return null;
    return {tip:built.tip,path:built.path,dir:d};
  };
  // the silver run: the spine of the map, three fights and a payout
  const silver=run(startRoom,3);
  if(!silver) return null;
  silver.tip.keyReward=true;
  // an upgrade run forks off the first room of it
  const arm=run(silver.path[0],2);
  if(!arm) return null;
  arm.tip.type='item';
  // the gold run forks off the second, and the boss run hangs off the end of that one, so the
  // intended line is: fight down the spine, detour for the key, fight on into the boss door
  const gold=run(silver.path[1],3);
  if(!gold) return null;
  gold.tip.goldReward=true;
  const boss=run(gold.tip,1);
  if(!boss) return null;
  boss.tip.type='boss';
  // one secret per dungeon, walled off behind a fake wall on a random solid wall. never on the boss
  // or the upgrade room, so those two stay legible, and it stays off the map until the wall is gone
  const walls=[];
  for(const r of Object.values(rs)){
    if(r.type==='boss'||r.type==='item'||r.type==='start') continue;
    for(const d of ARM_DIRS){
      const [nx,ny]=neighbor(r.x,r.y,d);
      if(nx<0||ny<0||nx>=GRID||ny>=GRID||rs[key(nx,ny)]) continue;
      // the pocket has to be shut on every other side, so the fake wall is genuinely the only way in
      // and the secret is not left touching a corridor it has no door to
      let touching=0;
      for(const dd of ARM_DIRS){const [ax,ay]=neighbor(nx,ny,dd); if(rs[key(ax,ay)]) touching++;}
      if(touching!==1) continue;
      walls.push({r,d,x:nx,y:ny});
    }
  }
  if(!walls.length) return null;
  const spot=walls[(Rnd.run()*walls.length)|0];
  rs[key(spot.x,spot.y)]=newRoom(spot.x,spot.y,'secret');
  spot.r.secret=spot.d;
  return rs;
}
function generateDungeon(){
  for(let i=0;i<800;i++){
    const res=tryBuild(i<500);
    if(res){
      rooms=res;
      // Two things get derived here, once, instead of being rediscovered every tick. `allRooms` is
      // the room list as an array: the minimap walks it every frame and `Object.values` on the room
      // map allocated a fresh array 60 times a second for no reason. `bossFront` is the set of rooms
      // with a door onto the boss, which used to be found by an O(n^2) scan of the whole dungeon on
      // every single tick for the entire run until the warning fired.
      allRooms=Object.values(rooms);
      bossFront={};
      for(const r of allRooms){
        if(r.type==='boss') continue;
        for(const d of ARM_DIRS){
          const [nx,ny]=neighbor(r.x,r.y,d);
          const n=rooms[key(nx,ny)];
          if(n&&n.type==='boss') bossFront[key(r.x,r.y)]=1;
        }
      }
      return;
    }
  }
}

function currentRoom(){return rooms[key(cur.x,cur.y)];}
function doorOpen(r){return r.enemies.length===0;}
function leadsTo(r,d,type){const [nx,ny]=neighbor(r.x,r.y,d);const n=rooms[key(nx,ny)];return !!n&&n.type===type;}
function leadsToBoss(r,d){return leadsTo(r,d,'boss');}
function leadsToItem(r,d){return leadsTo(r,d,'item');}
/* two locks, two colours: the gold key is the run objective and only opens the boss door, the
   silver key is the one the ordinary fight rooms hand out and it opens the upgrade room. A locked
   door does not open the instant you hold the key: the lock has to work while you stand at it, and
   you are free to walk away instead. That pause is the beat where you decide whether the run behind
   that door is worth taking. */
function hasKeyFor(r,d){ return leadsToBoss(r,d)?player.hasGold:leadsToItem(r,d)?player.hasSilver:false; }
// sealed: this door has not been paid for yet, whatever is in your pocket
function doorSealed(r,d){
  if(leadsToBoss(r,d)) return !bossUnlocked;
  if(leadsToItem(r,d)) return !itemUnlocked;
  return false;
}
// locked: sealed and you cannot open it right now, which is what puts a padlock on the frame
function doorLocked(r,d){ return doorSealed(r,d)&&!hasKeyFor(r,d); }
function doorPoint(d){ return {N:[MIDX,ROOM_TOP],S:[MIDX,ROOM_BOTTOM],E:[ROOM_RIGHT,MIDY],W:[ROOM_LEFT,MIDY]}[d]; }
function atDoor(r,d){
  const p=doorPoint(d);
  return Math.hypot(player.x-p[0],player.y-p[1])<UNLOCK_RANGE;
}
function unlocking(r,d){ return !!unlockDoor&&unlockDoor.r===r&&unlockDoor.d===d; }
// the door stays shut until the lock has finished working, so walking into it mid-unlock does
// nothing and the choice to go through is made after the bar fills, not before
function doorPassable(r,d){return !!r.doors[d]&&doorOpen(r)&&!doorSealed(r,d)&&!unlocking(r,d);}
function passDoor(r,d){
  if(trans) return;
  if(leadsToBoss(r,d)&&!bossUnlocked){bossUnlocked=true;player.hasGold=false;}
  if(leadsToItem(r,d)&&!itemUnlocked){itemUnlocked=true;player.hasSilver=false;}
  const [nx,ny]=neighbor(cur.x,cur.y,d);
  trans={t:0,nx,ny,from:OPP[d]};
}
// the lock works on whichever sealed door you touched. once started it runs to completion on its
// own, so stepping away is a choice rather than a cancel
function tickUnlock(){
  const r=currentRoom();
  if(unlockDoor){
    const still=unlockDoor.r===r&&r.doors[unlockDoor.d]&&doorSealed(r,unlockDoor.d);
    if(!still) unlockDoor=null;
  }
  if(!unlockDoor){
    for(const d of ARM_DIRS){
      if(!r.doors[d]||!doorSealed(r,d)||!hasKeyFor(r,d)||!atDoor(r,d)) continue;
      unlockDoor={r,d}; unlockT=0; break;
    }
    if(!unlockDoor) return;
  }
  if(++unlockT>=UNLOCK_TIME){
    if(leadsToBoss(r,unlockDoor.d)){bossUnlocked=true;player.hasGold=false;}
    else{itemUnlocked=true;player.hasSilver=false;}
    unlockDoor=null; unlockT=0;
  }
}

/* ---------- spawning ----------
   The rooms are still wide open rectangles, so the only thing standing between a gunner and a
   free hit on the player as the door opens is where the wave lands. The plan therefore keeps
   three promises: bodies never start overlapped, gunners start on the far side of the room from
   the door the player walks in through, and nothing starts inside a clear firing line to that
   door. With the rooms wide open this is the only protection a player gets on the turn - and the
   gunners deliberately sense far wider than they will shoot from, so what matters most now is that
   they have to walk you down rather than snapping to a firing line the moment you appear. */
function entryPoint(dir){
  return {N:[MIDX,ROOM_TOP+36],S:[MIDX,ROOM_BOTTOM-36],E:[ROOM_RIGHT-36,MIDY],W:[ROOM_LEFT+36,MIDY]}[dir]||[MIDX,MIDY];
}
function spawnPlan(count,fromDir){
  const [ex,ey]=entryPoint(fromDir);
  const cand=[], spare=[];
  for(let i=0;i<320;i++){
    const x=ROOM_LEFT+SPAWN_MARGIN+Rnd.run()*(ROOM_RIGHT-ROOM_LEFT-SPAWN_MARGIN*2);
    const y=ROOM_TOP+SPAWN_MARGIN+Rnd.run()*(ROOM_BOTTOM-ROOM_TOP-SPAWN_MARGIN*2);
    if(Math.hypot(x-MIDX,y-MIDY)<SPAWN_MID) continue;   // the middle of the room stays walkable
    (Math.hypot(x-ex,y-ey)<SPAWN_DOOR?spare:cand).push({x,y});   // nothing starts in the doorway
  }
  for(const s of spare) if(cand.length<count+4) cand.push(s);
  while(cand.length<count) cand.push({x:MIDX+Math.cos(Rnd.run()*6.283)*200,y:MIDY+Math.sin(Rnd.run()*6.283)*140});
  const pts=[];
  for(let i=0;i<count;i++){
    // first pick optimises against the entry door, later picks balance it against the bodies
    // already placed; nearOf has to be a big finite number, not Infinity, or every candidate
    // scores the same and the "farthest from the door" pick degenerates into the first candidate
    const nearOf=c=>pts.length?pts.reduce((m,p)=>Math.min(m,Math.hypot(p.x-c.x,p.y-c.y)),Infinity):1e4;
    let best=null,bestScore=-Infinity,roomy=null,roomyScore=-Infinity;
    for(const c of cand){
      const n=nearOf(c);
      if(n>roomyScore){roomyScore=n;roomy=c;}
      if(n<SPAWN_SEP) continue;
      const score=Math.hypot(c.x-ex,c.y-ey)+n*1.4;
      if(score>bestScore){bestScore=score;best=c;}
    }
    const pick=best||roomy;   // room too tight for the spacing: take the roomiest slot left
    pts.push({x:pick.x,y:pick.y,d:Math.hypot(pick.x-ex,pick.y-ey)});
  }
  return pts;
}
function rollPack(){
  let r=Rnd.run();
  for(let i=0;i<BRUNCH.pack.length;i++){ if(r<BRUNCH.weight[i]) return BRUNCH.pack[i]; r-=BRUNCH.weight[i]; }
  return BRUNCH.pack[0];
}
function spawnWave(room,fromDir){
  // more bodies at higher pressure. this is the only lever that makes a competent kiter work for
  // something, and it is the one that costs clear time, so it is what pays back the ground that
  // TEMPO gives away
  const n=Math.max(2,2+((Rnd.run()*(2+PRESSURE.rate))|0));
  const pts=spawnPlan(n,fromDir);
  // at most one gunner, and only where there are enough bodies to space it from the rest
  const heavy=n>=3&&Rnd.run()<0.55;
  const pack=Rnd.run()<BRUNCH.chance&&n>=2?rollPack():0;
  const slots=pts.slice().sort((a,b)=>b.d-a.d).map((p,k)=>{
    let type=Rnd.run()<0.5?'lunger':'shooter';
    if(heavy&&k===0) type='gunner';
    else if(type==='shooter'&&p.d<SPAWN_FAR) type='lunger';   // no gunner starts on the doorstep
    return {x:p.x,y:p.y,d:p.d,type};
  });
  // a pack takes over one standard slot and crowds into a knot around it, so the room does not get
  // bigger, it just gets a different problem. never the slot the gunner is holding
  if(pack&&slots.length>1){
    const victim=slots[1+((Rnd.run()*(slots.length-1))|0)];
    victim.type='brunch'; victim.pack=pack;
  }
  room.spawnPlan=slots;
  for(const s of slots){
    if(!s.pack){ room.enemies.push(spawnEnemy(false,room,s.x,s.y,s.type)); continue; }
    for(let i=0;i<s.pack;i++){
      const a=(i/s.pack)*6.283, rad=i%2?25:13;   // two rings, so the knot is not a straight line
      room.enemies.push(spawnEnemy(false,room,s.x+Math.cos(a)*rad,s.y+Math.sin(a)*rad,'brunch'));
    }
  }
}
