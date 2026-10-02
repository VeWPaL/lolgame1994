/* ==============================================================================================
   20-world  -  global state and the dungeon generator

   The map graph, room growth, doors, keys, and the spawn plan. Pure simulation: no drawing, no
   input, no platform calls. Contains the global state block, which in C# becomes a GameState class
   rather than nine module-level variables.
   ============================================================================================== */
let rooms, cur, player, mouse, mouseDown, altMouseDown, keys, state, records;
/* `projectiles` is initialised HERE, with the FX arrays below, rather than being left undefined until
   the first run starts it. It used to be declared in the list above and assigned by the first line of
   startGame - which meant `clearTransient` could be asked to empty a variable that did not exist yet,
   because the line it replaced was the one doing the creating. A transient that is created by its own
   reset is a transient that a second reset cannot safely touch. */
let projectiles=[];
let allRooms=[], bossFront={};
let showPerf=false, showSpawn=false, lastT=0, perfSamples=[], trans=null, readyT=0, fadeT=0, fadeTicks=1, entryDir='N';
let roomFade=0, frameCount=0, dashFX=[], burstFX=[], hookFields=[], bossUnlocked=false, itemUnlocked=false;

/* EVERYTHING THAT BELONGS TO THE ROOM YOU ARE LEAVING, in one list.

   These four arrays are the room's transient state: what is in flight, what is glowing, what is lying
   on the ground. They are emptied together and always together, and the reason they are emptied at
   all is not obvious from the name.

   WORLD COORDINATES ARE PER-ROOM. Every room's bounds start at the same origin, so (400,355) is the
   middle of the room you are in AND the middle of the room you just left. A hook field that outlives
   its room does not drift off harmlessly into the void beside the next one - it lands in the middle of
   that one, on top of whatever is standing there.

   Which was not only ugly. `tickFields` runs against the CURRENT room's bodies, so a leaked field
   charged and stunned a body the player had never met, and being charged is what grants hook
   RESISTANCE - this file's own note on the resistance table says that getting the count wrong there
   quietly nerfed the weapon. Measured: a leaked field charged a fresh body at tick 126, so every room
   entered within about two seconds of laying a hook began with its first enemy already carrying one.

   One list rather than three call sites each remembering four names. `enterRoom` used to clear
   projectiles and nothing else, which is precisely how three of the four outlived a room change while
   the two run-level resets remembered all four.

   The wand's muzzle flash is here too, and it is the one player-owned thing on this list. It is a
   weapon VFX drawn at the wand tip, so a shot fired in the last doorway flashed in the next room. */
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
/* THE TWO CURSORS ABOVE ARE THE ONLY MODULE STATE A RUN OWNS, AND BOTH ARE NOW RESET BY IT.

   Neither was. They are module-level `let`s outside every object, so they survived `startGame()` and
   simply carried on counting - which is correct WITHIN a run and quietly wrong across one.

   The consequence was the second fight from a seed not matching the first: the dungeon was identical,
   the bodies were identical, and every lunger circled the player from a slightly different angle
   because the golden-angle walk had not gone back to zero. Measured, same seed and same commands twice:

       run 1   flank 0.00 2.40 4.80 0.92 3.32 5.72 1.83 4.23    lunger at 401, 414, 430
       run 2   flank 0.35 2.75 5.15 1.27 3.67 6.07 2.18 4.58    lunger at 398, 411, 427

   A seed exists so two people can play the same run, and the first run of a session played differently
   from the second - so a friend comparing runs got a difference with no seed to explain it, which is
   exactly the situation the whole feature exists to prevent.

   "Never reset" was the right instinct for the wrong scope. It was defending against a body outliving a
   room transition and colliding with a pack from another room, and that is a WITHIN-run concern; both
   counters are reset at the start of a run and never touched again until the next one. A pack id is
   unique within a run, which is all the collision argument ever needed. */
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

/* ---------- dungeon shape ----------
   Not a rosette of corridors. The map is a few *runs*: each one is a line of fight rooms you walk
   from end to end, winding but never doubling back on itself, with a reward waiting at the end.
   Runs are allowed to fork off the side of each other, so the whole thing reads as three or four
   directions out of the start and a handful of decisions along the way. That is the loop the game
   wants: fight, fight, fight, payout, and a map small enough to hold in your head. */
const ARM_DIRS=['N','E','S','W'];
const TURNS={N:['W','E'],S:['E','W'],E:['N','S'],W:['S','N']};
/* A ROOM CARRIES ITS OWN BOUNDS, and this is the field the whole big-room system hangs off.

   Every room is a rectangle in world space, and a standard room is the default rather than the rule.
   `bounds` is stored rather than computed from a global because a room is the thing that knows its
   own size - and because the alternative, a global every room reads, is a global that can only ever
   describe one shape.

   The four values are written out rather than spread from ROOM_W/ROOM_H so that a future room with
   an asymmetric inset, a ledge, or a corridor stub has somewhere to put it without changing the
   shape of the data.

   It reads STD_ROOM for its origin rather than ROOM_LEFT, and that is not a style choice. ROOM_LEFT
   is now the CURRENT room's left edge, so a room built while standing in a big room would be
   positioned relative to the big room - and a room built while standing in itself would be placed at
   its own coordinates, which is a room that is somehow at its own left edge. A generator that
   positions a room from the room you are in is the same geometry-in-two-places bug wearing a
   different hat, so this anchors on the constant that means "where a room starts" and nothing else.

   `cx`/`cy` are on the room because a bigger room's centre is not the screen's centre, and anything
   that needs "the middle of this room" should ask the room rather than average two shorthands that
   happen to be describing whatever room the player is in at the time. */
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
/* THE MAP. A general branching tree, with the rooms that matter assigned to its ENDS afterwards.

   The old generator laid out three named runs by hand - a silver spine, an arm off its first room
   for the upgrade, a gold run off its second, and the boss hanging off the end of that. It was
   legible as code and it produced the same map every time in shape: three corridors, a fixed
   junction at each fork, and a route a player could learn in one run and never have to think about
   again. A dungeon you can learn in one run is a corridor with monsters in it.

   What replaces it grows a TREE and then decides what the ends are FOR. Two trunks leave the start
   in different directions; each forks a side run partway out; the tips of the trunks become the two
   rooms worth fighting towards, and the tips of the side runs are the places a key can be. The
   branches are not decoration - a key is IN one, so a branch is a detour you have to choose to make
   rather than a shortcut you pass through, and which branch holds which key is decided at
   generation, so the route is different every floor even at the same seed.

   WHY THE KEYS GO IN BRANCH ENDS RATHER THAN ON THE TRUNK. A key on the trunk is a toll you pay by
   walking forward, and a toll that is unavoidable is not a decision. Put it at the end of a side run
   and the player is choosing between two ways to spend a floor: go deep on a trunk and find out what
   is at the end of it, or turn off early and come back with a key. Both are correct play and they
   cost different amounts of the thing the player cares about, which is time and health.

   ENDPOINTS, not coordinates. A room is an endpoint if it has exactly one door. Nothing in this
   function knows where anything is on the grid; it asks the graph what its dead ends are and picks
   from those. That is what makes the layout free to change shape without this code changing with it,
   and it is the same reason the key rooms were moved off fixed positions in the first place. */
function isEndpoint(r){ return Object.keys(r.doors).length===1; }


/* One trunk, with side runs cut off it.

   This is `grow` with forks added, and it is written as a separate function rather than as a flag on
   `grow` because the fork has to happen from INSIDE the walk - a branch can only be cut from a room
   that actually exists - and threading that through the existing loop meant giving `grow` two return
   shapes and one more parameter, for a caller that wants a different answer.

   `forkAt` is the list of step indices to cut from, and `forkLen` how long each cut is. Returns the
   path, the tip, and every endpoint the side runs produced. */
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
    /* NEVER FORK FROM THE TIP. The last room of a trunk is the one that becomes the upgrade room or
       the boss, and a branch hanging off it puts the key in that branch BEHIND the locked door the
       key is supposed to open. Measured before this was caught: 113 of 300 dungeons had an
       unreachable gold key, and the grid showed a branch running east out of the boss room.

       The tip has to stay an endpoint - one door - or the room that is supposed to be the end of
       the map is a corridor junction with a locked door on one side of it, which is not a room worth
       fighting towards. */
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

  // Two trunks out of the start, in different directions. The second one is grown AFTER the first,
  // so freeDir already knows the first trunk is there and the two cannot collide - which is what
  // stops the whole build being thrown away for a run that walks into its own corridor.
  //
  // trunkLen 4-5 and forkLen 3-4 rather than something shorter. The first version of the tree used
  // 3-4 and 2-3, which put a floor at 8 fight rooms in the worst case - and eight is not a floor, it
  // is a corridor with two decisions in it, and a player who reaches the boss in ninety seconds has
  // not been given the thing the map is for. The lengths are the cost side of the same trade: they
  // are what the retry loop in generateDungeon is paying for, and at 17 rooms against a 7x7 grid
  // there is still room for strict mode to refuse a cramped build and try again.
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

  /* ROLES, assigned to the ENDS of what grew rather than to positions chosen in advance.

     The two trunks get the two rooms worth walking towards, and they are different on purpose: one
     end is the upgrade room and one is the boss, so a player who commits early to a direction has
     committed to what that direction was FOR. Which trunk gets which is a coin toss, so the map
     cannot be learned as "left is the boss".

     A trunk tip is a dead end by construction - it is where the walk stopped - so it is an endpoint
     and the same test covers it. The upgrade and boss rooms are marked so the door code can seal
     them, which is the only part of this that needs to know a room's job. */
  const aIsBoss=Rnd.run()<0.5;
  A.tip.type=aIsBoss?'boss':'item';
  B.tip.type=aIsBoss?'item':'boss';

  /* THE KEYS, one per trunk, placed at a BRANCH END and chosen at generation.

     A branch end is an ordinary fight room with a key in it, so getting the key costs a detour and
     the detour can be blocked, which is the whole point of putting it there rather than on the
     spine. If a trunk produced no branch the build is thrown away rather than quietly putting the
     key on the spine, because a map that sometimes has a detour and sometimes does not is a map
     where the detour is not a decision. */
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
  //
  // depthBodies() adds a fraction of a body per floor on top of the roll rather than moving the roll
  // itself, so the depth ladder cannot change WHICH bodies a room draws - only how many. A deeper
  // floor should be a fuller room of the same fight, not a different fight: the player learns the
  // shapes on floor 1 and the shapes are still the shapes on floor 12.
  const rolled=2+((Rnd.run()*(2+PRESSURE.rate))|0);
  const n=Math.max(2,Math.floor(rolled+depthBodies(0)));
  const pts=spawnPlan(n,fromDir);
  // at most one gunner, and only where there are enough bodies to space it from the rest
  const heavy=n>=3&&Rnd.run()<0.55;
  // depthPack() rather than BRUNCH.chance: a pack is the most interesting thing a room can contain
  // and the most reliable cover, so if deep floors were only tougher they would be the same rooms
  // with longer fights. Capped, because a room that is always a pack is a single shape.
  const pack=Rnd.run()<depthPack()&&n>=2?rollPack():0;
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
  // A pack gets an ID and a slot index per body, and those two numbers are the entire formation
  // interface: the movement code never needs to know how many packs exist or where they started, only
  // which wall a body belongs to and where in it the body stands. PACK_CURSOR is a module counter
  // rather than something derived from the room, so two packs in one room are genuinely two walls
  // and cannot be mistaken for one formation of sixteen.
  for(const s of slots){
    if(!s.pack){ room.enemies.push(spawnEnemy(false,room,s.x,s.y,s.type)); continue; }
    /* THE ID IS PER PACK, TAKEN ONCE, BEFORE THE BODIES. This incremented PACK_CURSOR inside the
       body loop as well as after it, so every body in a pack got its own id and no two of them ever
       agreed on which pack they were in. The wall rule asks for `packC[packId].n >= BRUNCH_WALL_MIN`
       - a count of bodies sharing one id - so with a unique id per body every pack counted 1 and the
       wall could never form.

       Measured over 40 seeds and 221 rooms containing Brunch: 1227 bodies, 1227 distinct ids, and
       ZERO packs reaching the threshold. An entire documented mechanic - "a Brunch pack is a WALL,
       not a crowd" - was dead in the game and live only in the boss's hand-placed wall, which uses a
       fixed id and so was unaffected. That is why it was never noticed: the one wall anyone had seen
       was the one that worked. */
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
