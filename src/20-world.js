/* 20-world - global state and the dungeon generator The map graph, room growth, doors, keys, and the spawn plan. [h:20-world-1] */
let rooms, cur, player, mouse, mouseDown, altMouseDown, keys, state, records;
/* `projectiles` is initialised HERE, with the FX arrays below, rather than being left undefined until the first run starts it. [h:20-world-2] */
let projectiles=[];
let allRooms=[], bossFront={};
let showPerf=false, showSpawn=false, lastT=0, perfSamples=[], trans=null, readyT=0, fadeT=0, fadeTicks=1, entryDir='N';
let roomFade=0, frameCount=0, dashFX=[], burstFX=[], hookFields=[], bossUnlocked=false, itemUnlocked=false;

/* EVERYTHING THAT BELONGS TO THE ROOM YOU ARE LEAVING, in one list. [h:20-world-3] */
function clearTransient(){
  projectiles.length=0;
  dashFX.length=0;
  burstFX.length=0;
  hookFields.length=0;
  // the same reasoning as applyVitals: player is a module-scope let, so it is undefined before the
  // first startGame and touching it here would throw rather than read undefined
  if(player) player.muzzleTimer=0;
}
let unlockDoor=null, unlockT=0, bossWarnT=0, secretFound=false, bossWarned=false;
// Which slice of the circle the next lunger to spawn takes. Walked by the golden angle so that no two
// bodies ever land on the same slot however many have spawned, and not reset per room - a room's
// pack is spaced by its order of arrival, which is what stops a wave arriving as one front.
let FLANK_CURSOR=0;
// Pack identity, handed out one ID per wall. Never reused and never reset, so a body that outlives a
// room transition cannot end up sharing a formation with a pack it has never met.
let PACK_CURSOR=1;
/* THE TWO CURSORS ABOVE ARE THE ONLY MODULE STATE A RUN OWNS, AND BOTH ARE NOW RESET BY IT. [h:20-world-4] */
function resetRunCursors(){ FLANK_CURSOR=0; PACK_CURSOR=1; }
let paused=false, acc=0, run=null, lastRun=null;
// The between-floors banner: the floor you came from and how long the fade has left to run. Lives
// here with the other module-level run state because it is set by descend() and read by the view,
// and threading a third parameter through render() to carry two numbers would be worse.
let descendFrom=0, descendT=0;
// A locked door no longer opens the instant you hold the key, and it no longer pins you in place
// while it works. Touching the door starts the lock; it finishes on its own whether you stay or
// walk off, which is what turns it into a decision you make rather than a button you hold.
const UNLOCK_TIME=sec(0.5), UNLOCK_RANGE=64, BOSS_WARN_TIME=sec(3.2);

/* dungeon shape ---------- Not a rosette of corridors. [h:20-world-5] */
const ARM_DIRS=['N','E','S','W'];
const TURNS={N:['W','E'],S:['E','W'],E:['N','S'],W:['S','N']};
/* A ROOM CARRIES ITS OWN BOUNDS, and this is the field the whole big-room system hangs off. [h:20-world-6] */
function roomBounds(w,h){
  w=w||ROOM_W; h=h||ROOM_H;
  return {l:STD_ROOM.l,t:STD_ROOM.t,r:STD_ROOM.l+w,b:STD_ROOM.t+h,w:w,h:h};
}
function newRoom(x,y,type,bounds){
  const b=bounds||roomBounds();
  return {x,y,doors:{},type,visited:false,spawned:false,enemies:[],pickups:[],spawnPlan:null,
    keyReward:false,goldReward:false,cleared:false,secret:null,armed:false,
    bounds:b, cx:b.l+b.w/2, cy:b.t+b.h/2};
}
// a direction that leaves `room` through an unused side into empty grid
function freeDir(rs,room,prefer){
  const open=d=>{const [nx,ny]=neighbor(room.x,room.y,d);return !room.doors[d]&&nx>=0&&ny>=0&&nx<GRID&&ny<GRID&&!rs[key(nx,ny)];};
  const free=ARM_DIRS.filter(open);
  if(!free.length) return null;
  // prefer leaving sideways off the host so a fork reads as a fork, not as a stub
  const side=free.filter(d=>d!==OPP[prefer]&&d!==prefer);
  return (side.length?side:free)[(Rnd.run()*(side.length||free.length))|0];
}
/* walks one corridor out of (x,y), returning the rooms it placed plus its tip. [h:20-world-7] */
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
/* THE MAP. A general branching tree, with the rooms that matter assigned to its ENDS afterwards. [h:20-world-8] */
function isEndpoint(r){ return Object.keys(r.doors).length===1; }


/* One trunk, with side runs cut off it. [h:20-world-9] */
function growForked(rs,x,y,dir,len,strict,bias,forkAt,forkLen){
  const path=[],ends=[];
  let cx=x,cy=y,lastTurn=-1,seg=1+((Rnd.run()*2.4)|0);
  for(let step=0;step<len;step++){
    let order;
    if(seg<=0){
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
    /* NEVER FORK FROM THE TIP. [h:20-world-10] */
    if(step>=len-1) continue;
    if(forkAt.indexOf(step)<0) continue;
    // Cut a side run off the room just placed. It leaves through a direction that is not the way we
    // came and not the way we are going, so a branch is never a stub pointing back down the trunk.
    const room=path[path.length-1];
    const incoming=OPP[dir];
    const sides=ARM_DIRS.filter(k=>k!==incoming&&k!==dir);
    for(const sd of sides){
      const [bx,by]=neighbor(room.x,room.y,sd);
      if(bx<0||by<0||bx>=GRID||by>=GRID||rs[key(bx,by)]) continue;
      const b=grow(rs,room.x,room.y,sd,forkLen,strict,bias);
      if(!b) continue;
      ends.push(b.tip);
      break;
    }
  }
  return {path,tip:rs[key(cx,cy)],ends};
}

function tryBuild(strict){
  const rs={};
  const startRoom=newRoom(START,START,'start');
  startRoom.visited=true; startRoom.spawned=true;
  rs[key(START,START)]=startRoom;

  /* Two trunks out of the start, in different directions. [h:20-world-11] */
  const trunkLen=4+((Rnd.run()*2)|0);
  const forkLen=3+((Rnd.run()*2)|0);
  // The fork is cut from an INTERIOR room. The index is drawn from the steps that are not the last
  // one, because the last one is the tip and the tip is a terminal room - see growForked, where
  // forking from it is refused and why.
  const forkAt=[(Rnd.run()*(trunkLen-1))|0];
  const dirA=freeDir(rs,startRoom,null);
  if(!dirA) return null;
  const A=growForked(rs,START,START,dirA,trunkLen,strict,0.55,forkAt,forkLen);
  if(!A) return null;
  const dirB=freeDir(rs,startRoom,dirA);
  if(!dirB) return null;
  const B=growForked(rs,START,START,dirB,trunkLen,strict,0.55,forkAt,forkLen);
  if(!B) return null;

  /* ROLES, assigned to the ENDS of what grew rather than to positions chosen in advance. [h:20-world-12] */
  const aIsBoss=Rnd.run()<0.5;
  A.tip.type=aIsBoss?'boss':'item';
  B.tip.type=aIsBoss?'item':'boss';

  /* THE KEYS, one per trunk, placed at a BRANCH END and chosen at generation. [h:20-world-13] */
  const aEnds=A.ends.filter(r=>isEndpoint(r)&&rs[key(r.x,r.y)]);
  const bEnds=B.ends.filter(r=>isEndpoint(r)&&rs[key(r.x,r.y)]);
  if(!aEnds.length||!bEnds.length) return null;
  const silverAt=aEnds[(Rnd.run()*aEnds.length)|0];
  const goldAt=bEnds[(Rnd.run()*bEnds.length)|0];
  if(silverAt===goldAt) return null;
  silverAt.keyReward=true;
  goldAt.goldReward=true;

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
      /* Two things get derived here, once, instead of being rediscovered every tick. [h:20-world-14] */
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
/* two locks, two colours: [h:20-world-15] */
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
  Sfx.door();   // the room's punctuation: it tells the player the floor moved on
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

/* spawning ---------- The rooms are still wide open rectangles, so the only thing standing between a gunner and a free hit on the player as the door... [h:20-world-16] */
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
  /* more bodies at higher pressure. [h:20-world-17] */
  const rolled=2+((Rnd.run()*(2+PRESSURE.rate))|0);
  const n=Math.max(2,Math.floor(rolled+depthBodies(0)));
  const pts=spawnPlan(n,fromDir);
  // at most one gunner, and only where there are enough bodies to space it from the rest
  const heavy=n>=3&&Rnd.run()<areaMix().heavy;
  // depthPack() rather than BRUNCH.chance: a pack is the most interesting thing a room can contain
  // and the most reliable cover, so if deep floors were only tougher they would be the same rooms
  // with longer fights. Capped, because a room that is always a pack is a single shape.
  const pack=Rnd.run()<depthPack()&&n>=2?rollPack():0;
  const slots=pts.slice().sort((a,b)=>b.d-a.d).map((p,k)=>{
    let type=Rnd.run()<areaMix().lunger?'lunger':'shooter';
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
  /* A pack gets an ID and a slot index per body, and those two numbers are the entire formation interface: [h:20-world-18] */
  for(const s of slots){
    if(!s.pack){ room.enemies.push(spawnEnemy(false,room,s.x,s.y,s.type)); continue; }
    /* THE ID IS PER PACK, TAKEN ONCE, BEFORE THE BODIES. [h:20-world-19] */
    const packId=PACK_CURSOR;
    PACK_CURSOR++;
    for(let i=0;i<s.pack;i++){
      const a=(i/s.pack)*6.283, rad=i%2?25:13;   // two rings, so the knot is not a straight line
      const b=spawnEnemy(false,room,s.x+Math.cos(a)*rad,s.y+Math.sin(a)*rad,'brunch');
      b.packId=packId; b.packSlot=i;
      room.enemies.push(b);
    }
  }
}
