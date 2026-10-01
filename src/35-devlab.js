/* ==============================================================================================
   35-devlab  -  a game state that is not a game

   THE LAB, and what it is for. Every other way of answering "what does 7 damage against a Brunch
   actually do" costs a run: you play to the room, you pick the gun, you fire, and you read the
   health bar afterwards if you were paying attention. That is a twenty-minute loop for a question
   that takes a tenth of a second, and it is why balance work in this project has leaned on the
   instrumented fixture in 99-tests.js - which works, but measures a SIMULATION and not the thing a
   player looks at.

   So this is a real game state with a real room and real bodies in it, and the only things missing
   are the reasons a real game is hard: there is nothing to die of, nothing to record, nowhere to
   go, and no run to spoil. F2 gets you in, F2 gets you out, and what you were playing before is
   untouched.

   THE ROOM IS BIGGER THAN THE SCREEN, deliberately and not incidentally. 1680x1040 against a 960x600
   viewport, so the camera has to move. That is the second reason the lab exists: a scrolling room
   is the only place the camera is ever exercised, and a camera that has never been exercised in a
   room you can see all of is a camera nobody has tested.

   THE TWO MODES THE LAB PROMISES, and they are different tools rather than one tool with a switch:

   the SPECIMEN ROW is still. Five bodies, one of every kind, on plinths, each wearing its own name
   and its own health bar. Still is the point: a target that walks changes where your shot lands, and
   a number read off a moving target is a number about your aim. Standing still turns the question
   "how much does this do" back into a question with one variable in it.

   the DROVE is the opposite: bodies in numbers, awake, coming at you, for frame cost, for
   separation, for whether the pack formation survives at forty.

   And the DAMAGE NUMBERS, which the game does not otherwise have. There is no floating number in
   Depths - damage is a health bar and a hit flash, which is right for a game and useless for
   tuning one. The lab derives them by COMPARING each body's health to what it was on the previous
   tick, so it reads damage wherever damage is actually dealt rather than duplicating the damage
   path and hoping the two do not drift. The duplication would be the bug: a second place that knows
   how much a weapon hits, disagreeing with the first the moment a trait or a falloff changes.
   ============================================================================================== */

/* The five bodies on the row. `boss` is not a kind in the spawn table - spawnEnemy takes it as a
   separate flag - so it is spelled out here rather than hidden behind a `type` that would lie. */
const LAB_SPECIMENS=[
  {type:'lunger', boss:false, blurb:'the baseline body'},
  {type:'brunch', boss:false, blurb:'cover, in numbers'},
  {type:'shooter', boss:false, blurb:'stands off and shoots'},
  {type:'gunner', boss:false, blurb:'solves your line'},
  {type:'boss',   boss:true,  blurb:'the whole fight'}
];

/* 1680x760 against a 960x600 viewport. Bigger than the screen in BOTH axes, so the camera has to
   move on each, but only 160px of vertical travel - and that ratio is the whole of the layout.

   The first version was 1680x1040 and it could not work. The camera frames the player, the HUD eats
   the top ~225px of the frame, and a room 440px taller than the view leaves 375px of clear screen
   for a row of specimens, a player and a two-row shelf - and the player has to sit at the room's
   centre for the camera to frame any of it, which puts the player exactly where the row wants to
   be. Every arrangement of the three collided with something.

   160px of vertical travel is enough that the camera genuinely moves and clamps, and small enough
   that the room's whole height fits in the frame with the HUD's share taken out of it. The camera is
   exercised by the WIDTH, which is where the interesting scrolling is anyway. */
const LAB_W=1680, LAB_H=760;
const LAB_DROVE=12;

const Lab=(function(){

  let on=false, frozen=true, dropper=0;
  let numbers=[];          // the floating damage readouts, which the game does not have
  let lastHp=new Map();    // body -> the health it had last tick, which is how damage is derived
  const hpSeen=e=>{ lastHp.set(e,e.hp); return e.hp; };

  /* ------------------------------------------------------------------ the room -------------- */

  /* The lab room is built by REPLACING the bounds on the room the run already made, rather than by
     generating a new dungeon. That is deliberate: the lab has to be the ordinary room with a
     different size, not a special room, because the whole claim being tested is that an ordinary
     room works at another size. Anything special about the lab would hide the thing that is being
     checked.

     The plinths then go on the floor, and the grid and the braziers go in with them - a large flat
     floor with no features on it is not merely dull, it is UNREADABLE as a moving image, because
     with nothing to pass under the camera there is no way to tell the view has scrolled at all.
     The survey grid is not decoration: it is the instrument that makes the camera legible. */
  function build(){
    const r=currentRoom();
    r.bounds=roomBounds(LAB_W,LAB_H);
    r.cx=r.bounds.l+LAB_W/2; r.cy=r.bounds.t+LAB_H/2;
    r.type='normal';
    r.doors={};            // no way out, and that is the point: there is nothing here to progress to
    r.spawned=true; r.armed=false;
    r.enemies.length=0; r.pickups.length=0; r.spawnPlan=null; r.cleared=true;
    syncRoomBounds();
    // the player goes where the CAMERA will frame the furniture, not at the room's centre. These two
    // are not the same place in a room this size, and picking the centre is what hid the first
    // attempt's furniture behind the HUD - see layRow.
    player.x=r.cx; player.y=standY(r.bounds);
    player.lagX=player.x; player.lagY=player.y;
    player.vx=0; player.vy=0; player.kvx=0; player.kvy=0;
    return r;
  }

  /* THE ROW, and where it goes is the whole argument.

     The first attempt placed the row and the shelf against the room's TOP edge, which is the obvious
     thing to do and the wrong thing: the camera frames the PLAYER, not the room. A player standing
     near the middle of a 1040-tall room puts the visible band at world y 420..1020, while the row was
     at 360 and the shelf at 216 - one hidden behind the HUD, one entirely off-screen. Four hundred
     pixels of carefully drawn furniture that nobody could see, and every test still passed, because
     "the shelf has thirteen entries" says nothing about whether the shelf is VISIBLE.

     So these are fractions of the room's height, chosen against the frame rather than against the
     room, and written down so the next person does not tidy them back to the top edge:
       - the camera's top is pinned near the room's top because the player starts low in the band;
       - the HUD eats roughly the first 120px of the band, so the row sits below that;
       - the shelf sits below the row, the player between them.
     They are fractions rather than pixels so a differently sized lab still works, and a test re-derives
     them from W and H so a change to either cannot quietly push the furniture off-screen again. */
  /* THE THREE BANDS, and they are placed in SCREEN terms and then converted, because that is the
     only coordinate system in which "visible" means anything.

     The camera frames the player, so if the player stands at the room's centre the visible band is
     the middle 600px of the room and the HUD's ~225px comes off the top of it. What is left is
     375px of clear screen, and the three things that have to fit in it are the specimen row, the
     player and a two-row shelf. Placing them as fractions of the room - which is what the first
     version did - cannot work, because the mapping from "fraction of the room" to "pixels of clear
     screen" depends on where the camera ended up.

     So: the player is AT the room's centre, which puts the camera exactly there, and the furniture
     is placed by how far below the centre it should sit ON SCREEN. The row goes just under the HUD,
     the shelf near the bottom, and the player is left on the centre line where the camera wants it.
     A test re-derives all of this from W, H and the HUD's depth, so a change to any of the three
     fails loudly rather than quietly putting a plinth behind the health bar. */
  const rowY=b=>b.t+b.h/2-30, shelfY=b=>b.t+b.h/2+130, standY=b=>b.t+b.h/2;
  function layRow(){
    const r=currentRoom(), b=r.bounds;
    // spaced so the widest plinth cannot touch its neighbour's; read from the room, not hardcoded
    const gap=Math.min(215,(b.w-460)/(LAB_SPECIMENS.length-1));
    let x=b.l+b.w/2-gap*(LAB_SPECIMENS.length-1)/2;
    for(const s of LAB_SPECIMENS){
      const e=spawnEnemy(s.boss,r,x,rowY(b),s.type);
      // spawned by hand, so it is pushed by hand: spawnEnemy RETURNS a body and does not add it.
      r.enemies.push(e);
      e.labSpecimen=true;
      e.labName=s.type==='boss'?'warden':s.type;
      e.labBlurb=s.blurb;
      x+=gap;
    }
  }

  /* Every item in the roster, laid out as a shelf of alcoves along the top wall, and equipped on
     entry as well. Equipping them AND showing them is not redundant: the build is what a damage
     number is measured AGAINST, so a lab that shows you the item but does not give it to you would
     have you reading a number that describes a different character than the one you are holding.

     The shelf is lab-owned data and NOT pushed into r.pickups. A pickup is something the game
     consumes, and the game's pickup kinds are a closed list it switches on; an entry of a kind it
     does not know is at best inert and at worst falls into a default arm that does something to the
     player. The shelf wants to be looked at, not walked into - everything on it is already equipped
     - so it does not pretend to be a pickup. */
  let shelf=[];
  /* TWO ROWS, because thirteen alcoves do not fit in the frame and this is not a close call.

     The first shelf was one row of thirteen at 106px each - 1378px of rail in a 960px viewport, so
     the middle seven were on screen and the rest ran off both sides. Nothing about that was
     detectable from the data: thirteen entries, thirteen names, thirteen glyphs, all correct, and
     five of them in a place you could not look at. The test now measures screen position, and it
     caught this immediately, which is the argument for measuring screen position.

     Seven per row at 120px is 840px, so a full row fits inside the frame with room for the name
     plates under it. The row count is derived from the room width rather than fixed, so a narrower
     lab wraps to three rows instead of overflowing again. */
  const SHELF_ROWS=2;
  /* THE SHELF IS A PICKUP, not a picture of one.

     It was lab-owned data and deliberately not pushed into `r.pickups`, on the reasoning that the
     shelf is something to be LOOKED at - everything on it is already equipped, so walking into one
     should do nothing. That reasoning was about the shelf and missed the obvious: in a room with a
     roster on a rail, the first thing anyone does is walk up and take one, and the shelf was the one
     place in the game where items could not be picked up at all. A dev view that refuses to do the
     thing the game does is testing something other than the game.

     So each alcove is also a real pickup at the same position. Taking it equips it through exactly
     the same code path as a chest in a real room - `Items.give`, the same displacement of the item
     that was already held, the same drop-on-the-floor. Which means the lab now exercises the swap
     path, which is the single most intricate piece of the item system, on every walk past the rail.

     The alcoves RESPAWN. Without that the shelf empties itself the first time it is used and the
     thing you came to test is gone, so each alcove carries a flag and refills a short time after it
     is taken. The wait is in ticks rather than milliseconds because the game thinks in ticks. */
  const SHELF_RESPAWN_TICKS=sec(2.5);
  function layShelf(){
    const r=currentRoom(), b=r.bounds;
    const ids=Content.ids('item');
    const perRow=Math.ceil(ids.length/SHELF_ROWS);
    const gap=Math.min(120,(b.w-420)/Math.max(1,perRow-1));
    shelf=ids.map((id,i)=>{
      const d=Content.get('item',id);
      const row=Math.floor(i/perRow), col=i%perRow;
      // the last row is centred on its own, not left-aligned under the first: a row of six hanging
      // off the left of a row of seven reads as a mistake rather than as a shelf
      const inRow=Math.min(perRow,ids.length-row*perRow);
      const x=b.l+b.w/2+(col-(inRow-1)/2)*gap;
      return {id:id,name:d.name,glyph:d.glyph,color:d.color,blurb:d.blurb,
              slot:d.slot,rarity:d.rarity,x:x,y:shelfY(b)+(row?1:-1)*58,row:row,
              gone:0};
    });
    syncShelfPickups();
  }
  /* One pickup per alcove, carrying the alcove's index so the alcove can be emptied when it is taken.
     `hold` is deliberately NOT set: that flag exists to stop a just-dropped item being handed straight
     back, and these are meant to be picked up the moment the player touches them. */
  function syncShelfPickups(){
    const r=currentRoom();
    if(!r) return;
    /* IN PLACE, never by reassignment. `r.pickups` has two owners: this and the game's pickup loop
       in 60-tick, which splices as it goes. Reassigning the field here replaced the array the loop
       was holding, so a shelf pickup could be consumed out of one array while the alcoves were
       rebuilt in another - and leaving the lab then filtered the new array, leaving the old one
       intact. One leftover shelf item survived the exit and the next run started with a free
       item on the floor. The symptom was three pickups left after F2 and no plausible cause; the
       cause is that two things thought they owned the field.

       So the shelf removes its own entries with splice, exactly as the game does, and the field
       keeps one owner for its identity. */
    for(let i=r.pickups.length-1;i>=0;i--){
      if(r.pickups[i].labShelf!==undefined) r.pickups.splice(i,1);
    }
    for(let i=0;i<shelf.length;i++){
      const s=shelf[i];
      if(s.gone>0) continue;
      r.pickups.push({x:s.x,y:s.y,r:18,kind:'item',id:s.id,labShelf:i});
    }
  }
  /* An alcove that has been emptied comes back, so the rail can be used again. Driven from the lab's
     own tick rather than from the pickup loop, because the pickup loop owns `r.pickups` and must not
     be made to know that a shelf exists.

     An alcove is emptied by the pickup loop removing its pickup, which happens the moment the player
     walks into it. So "has this alcove still got its pickup" is the question, and asking it is more
     robust than being told: a pickup consumed by any path at all - including one added later - refills
     the alcove, and nothing has to remember to report it. */
  function tickShelf(){
    const r=currentRoom();
    if(!r) return;
    const live=new Set();
    for(const pk of r.pickups) if(pk.labShelf!==undefined) live.add(pk.labShelf);
    let changed=false;
    for(let i=0;i<shelf.length;i++){
      const s=shelf[i];
      if(s.gone>0){
        /* Counting down. When it reaches zero the alcove is put back on the rail on THIS tick, and
           `gone` is left at zero rather than immediately restarted. The first version did the refill
           and then fell through to the next tick, which found the alcove marked empty, found no pickup
           where one should be - because the refill had not happened yet - and started the timer again.
           The alcove therefore oscillated between one tick available and 522 ticks gone, and a player
           standing on the rail could never pick the same item up twice.

           The two states are kept distinct on purpose: `gone > 0` means "waiting to come back", and
           `gone === 0` with a pickup present means "on the rail". Refilling sets the pickup and stops
           there, so the next tick sees both and leaves it alone. */
        if(--s.gone===0) changed=true;   // back on the rail this tick; `gone` stays 0
      } else if(!live.has(i)){
        // the alcove is marked available but its pickup is gone: it was just taken
        s.gone=SHELF_RESPAWN_TICKS;
        changed=true;
      }
    }
    if(changed) syncShelfPickups();
  }

  /* ------------------------------------------------------------------ the drove -------------- */

  /* Spawned in a RING around the player rather than at a point, because a drove spawned on top of
     you is a frame-cost spike and nothing else - the bodies are inside each other, the separation
     solver spends its first second unstacking them, and the first thing you measure is the solver
     rather than the fight. A ring puts them already spread, where the thing under test is the thing
     you came for.

     THE RING IS BELOW THE PLAYER, not on them, and that is not decoration. The first version centred
     it on the player, which put half the pack on top of the specimen row - and the row is FROZEN, so
     those five bodies are immovable. Twelve bodies pressing against a wall of statues do not go round
     it. So the ring opens downward, into the clear floor between the player and the shelf, which is
     also where you want a pack to arrive from: you see it coming.

     A DROVE THAT APPEARED NOT TO WORK, and never did. It was recorded as an open bug for a while,
     on the evidence that a dropped body travelled 1.0px in 40 ticks with curSpeed 0. Both numbers
     were true and the reading was wrong: that window is the LUNGE WINDUP, during which a lunger is
     planted and motionless on purpose, because stopping to telegraph is what makes the attack
     readable. The diagnostic that reported it printed lungeState='wind' and read past it. Over 120
     ticks a dropped body closes 279px to 83px and hits. The lesson is the third of its kind in this
     project: a fixture that measures a transient and reports it as a steady state, and the cure is
     always the same - run it longer, and read the field that says which phase you are in. */
  function drove(){
    const r=currentRoom(), t=droverType();
    const rad=Math.min(300,Math.min(roomW(),roomH())*0.3);
    const cy=Math.max(r.bounds.t+rad+40,Math.min(r.bounds.b-rad-40,player.y+230));
    for(let i=0;i<LAB_DROVE;i++){
      const a=(i/LAB_DROVE)*Math.PI*2+0.4;
      const b=spawnEnemy(false,r,player.x+Math.cos(a)*rad,cy+Math.sin(a)*rad*0.8,t.type);
      r.enemies.push(b);
      b.labDrove=true;
      /* AWAKE ON ARRIVAL. spawnEnemy hands a body up to 56 ticks of noticeTimer before it does
         anything at all, which in a real room is a grace period - you get a moment to read the room
         before it comes at you. In a lab it is two seconds of nothing, and a check that looks for
         movement is measuring the grace period rather than the fight.

         The specimen row silences its bodies for the opposite reason, and the difference is
         deliberate: a row you are reading damage off must not also be shooting you, and a drove you
         are stress-testing must not spend two seconds waking up. */
      b.noticeTimer=0; b.alerted=true;
    }
  }
  const droverType=()=>LAB_SPECIMENS[dropper];

  /* ------------------------------------------------------------------ enter and leave ------- */

  function enter(){
    /* startGame() first, always. The lab is not a stripped-down world: it is a REAL run's state
       with the run's contents replaced, and building the player by hand is how a lab ends up
       testing a character nobody can actually play. Starting from a real start also means the
       character sheet, the bench and the pause overlay all work in here, which they otherwise
       would not, and a debug view where the menus are broken is a debug view that lies.

       And then it stops being a run. `state` is set to 'dev' AFTER startGame, not before, because
       startGame is what sets it to 'playing' - set it first and the call overwrites it, which would
       leave the lab running as a live run with a full health bar and a path to the records. Every
       `state==='playing'` test in the game is now false, so nothing counts a shot, nothing descends
       and nothing can be recorded, and the simulation still runs because the tick was taught to let
       'dev' through. */
    startGame();
    on=true; frozen=true; dropper=0; numbers.length=0; lastHp.clear();
    const r=build();
    layRow();
    layShelf();
    for(const id of Content.ids('item')) Items.give(id);
    player.hp=player.maxHp;
    readyT=0; fadeT=0; roomFade=0; trans=null; bossWarnT=0; bossWarned=true;
    devOpen=false; keys={};
    state='dev';
    return r;
  }

  /* Leaving goes back to the TITLE, not to a run. There is no run to go back to: the lab called
     startGame() and threw away whatever the player was in the moment they walked in, so the honest
     thing is to leave them somewhere they can start again rather than somewhere that looks like
     their floor 7 continues. Returning to a half-remembered run state is how a debug tool eats
     somebody's afternoon. */
  function leave(){
    on=false; numbers.length=0; lastHp.clear();
    /* The shelf's pickups go with it. They live in `r.pickups`, and leaving the lab does not destroy
       the room - it only changes the state - so without this, thirteen un-takeable items are still on
       the rail the next time a run starts, and the run would hand the player a free full roster. */
    const r=currentRoom();
    if(r&&r.pickups) for(let i=r.pickups.length-1;i>=0;i--)
      if(r.pickups[i].labShelf!==undefined) r.pickups.splice(i,1);
    for(const s of shelf) s.gone=0;
    state='start';
    keys={};
  }
  const toggle=()=>{ if(on) leave(); else enter(); };

  /* ------------------------------------------------------------------ the tick --------------- */

  /* THE DAMAGE NUMBERS, derived rather than reported. Each body is compared with what it was last
     tick and the difference is what floats. The alternative - having the damage path call the lab -
     would mean a second place that knows how much a weapon hits, and the two would drift the first
     time a trait, a falloff or a resistance curve changed and only one of them was updated. Here
     there is nothing to keep in step, because the lab is not told anything: it watches.

     A rise is printed too, and red rather than white, because a body that HEALS between two ticks is
     a thing worth seeing rather than a thing to quietly average away - a Brunch wall, a regen item,
     a hook that gives health back all show up here first. */
  function tickNumbers(){
    const r=currentRoom();
    for(const e of r.enemies){
      const was=lastHp.get(e);
      if(was===undefined){ lastHp.set(e,e.hp); continue; }
      if(e.hp!==was){
        const d=e.hp-was;
        numbers.push({x:e.x,y:e.y-e.r-6,vy:-0.55,t:0,max:sec(1.1),txt:(d>0?'+':'')+d,heal:d>0});
        lastHp.set(e,e.hp);
      }
    }
    for(let i=numbers.length-1;i>=0;i--){
      const n=numbers[i]; n.t++; n.y+=n.vy; n.vy*=0.94;
      if(n.t>=n.max) numbers.splice(i,1);
    }
  }

  /* FROZEN means the bodies stop being simulated, not that they are drawn still. Each tick their
     position and heading are written back, which is the difference between a target you can measure
     against and a target that is merely slow: a slowed body still drifts on knockback, still turns
     to face you, and still takes a frame of the fight into your reading of the number. Writing the
     position back is what makes the specimen row honest.

     It ALSO stops them acting, and that part was missing. Pinning a shooter's position while leaving
     its firing branch live gives you a stationary gunner shooting you from thirty pixels away, forever,
     which is not a still copy of anything - it is a fight you are trying to measure a number inside.
     noticeTimer is the game's own "has not noticed you yet" clock, and a body with it pinned high
     skips its whole update, so this is the existing silence rather than a new one.

     Which is why the row and the drove are opposites rather than two settings of one thing: a row
     you read damage off must be still AND harmless, and a drove you stress-test must be neither. */
  function freeze(){
    if(!frozen) return;
    const r=currentRoom();
    for(const e of r.enemies){
      if(!e.labSpecimen) continue;   // the row is the still copy; a drove is never stilled
      e.x=e.labX; e.y=e.labY; e.vx=0; e.vy=0; e.kvx=0; e.kvy=0;
      e.noticeTimer=1e9;
    }
  }
  /* Remember where a row body was, the first tick it is seen, so the freeze has something to write
     back to. Done as a lazy write rather than at spawn because a body can be dropped into the row
     later by the drove, and then it is not a specimen and must not be pinned. */
  function remember(){
    const r=currentRoom();
    for(const e of r.enemies) if(e.labSpecimen&&e.labX===undefined){ e.labX=e.x; e.labY=e.y; }
  }

  /* ------------------------------------------------------------------ the keys --------------- */

  function key(k,first){
    /* F2 is answered whether or not the lab is open. That is the whole reason this is the first
       test in here: a lab that can only be entered is a key that works once, and a lab that can
       only be left is a browser refresh - which throws away the run you opened it from. */
    if(k==='f2'){ toggle(); return true; }
    if(!on) return false;
    if(!first) return false;
    if(k==='f3'){ frozen=!frozen; return true; }
    if(k==='f4'){ dropper=(dropper+1)%LAB_SPECIMENS.length; return true; }
    if(k==='f5'){ drove(); return true; }
    /* F6 REFILLS THE SHELF. The alcoves respawn on their own after two and a half seconds, which is
       right when you are walking past the rail collecting things, and useless when you have taken
       everything and want it back without waiting - which is what you want after changing a build and
       coming back to see the effect. */
    if(k==='f6'){ for(const s of shelf) s.gone=0; syncShelfPickups(); return true; }
    return false;
  }

  /* ------------------------------------------------------------------ the picture ------------ */

  /* Everything below is drawn in WORLD space, inside the camera transform, so it scrolls with the
     room - which is the point. A label pinned to the screen would be readable but would not tell you
     where anything is, and in a lab whose whole subject is "where is this body relative to that
     one" a label that does not move is worse than no label. */

  const engraved=(text,x,y,color,align,font)=>{
    ctx.font=font||'12px monospace'; ctx.textAlign=align||'center';
    // a dark offset under a light face: the game's engraving trick, and the reason the nameplates
    // read as cut into stone rather than printed on top of it
    ctx.fillStyle='rgba(0,0,0,0.75)'; ctx.fillText(text,x+1,y+1);
    ctx.fillStyle=color||'#d8cfae'; ctx.fillText(text,x,y);
    ctx.textAlign='left';
  };

  /* THE SURVEY GRID, and this is the single most functional thing in the lab.

     A camera scrolling over a featureless floor is very hard to read: the eye has nothing to track,
     so a scroll of a hundred pixels looks like a scroll of ten. The grid gives the view something
     to move against, and because the lines are WORLD-fixed they are also a ruler - you can see that
     a body is 200px from the wall rather than inferring it.

     The major/minor split matters. Uniformly spaced lines read as wallpaper and stop being a
     ruler; a line every 120 with a brighter one every 480 gives both a fine reference and an obvious
     "one big square" to count, which is how you estimate a distance in a fight.

     BAKED, because it is WORLD-FIXED and therefore frame-invariant. It was two stroke passes and
     about twenty line segments every frame, to redraw a lattice that cannot change until the room
     does; a room 1680 wide has 14 verticals and 7 horizontals in it. One drawImage now, cached per
     room size and origin. The same argument as the floor, which was already cached for exactly this
     reason before the lab existed. */
  const MINOR=120, MAJOR=480, gridCache=new Map();
  /* The grid is a MEASURING AID, so it is faint, and it used not to be. It is drawn at
     rgba(150,160,180,0.055) for minor lines over a floor that is already a seamless speckle - and
     over a 1680x760 room that is 14 verticals and 7 horizontals, which reads as a tiled floor rather
     than as a floor with a ruler on it. A dungeon room is 700x450 and has NO grid at all, which is
     exactly why the dungeon floor looks like ground and the lab floor looked like squares: the lab
     was carrying a visual element the game does not have, over the element the game does have.

     So the minor lines are gone entirely and only the majors remain, at a quarter of the old
     strength. What is left is 3 verticals and 2 horizontals in a room this size: enough to judge
     distance and camera travel by, not enough to tile the floor. The centre cross stays because it
     is one mark, not a lattice. */
  const GRID_MINOR_ALPHA=0;      // was 0.055 - a 120px lattice over a 1680px room is a tiled floor
  const GRID_MAJOR_ALPHA=0.055;  // was 0.13
  function gridAlphaFor(step){ return step===MINOR?GRID_MINOR_ALPHA:GRID_MAJOR_ALPHA; }
  /* The two brazier offsets and the flame's half-size, published so the suite can check the flame
     against the bowl without re-deriving numbers that only mean something together.

     It is a FUNCTION, not a constant, and that is not a style preference. The first version built the
     object here, at the top of the file, out of BRAZIER_BOWL_Y and FLAME_BASE_Y - which are declared
     70 lines further down. `const` does not hoist, so the Lab module threw a ReferenceError the
     moment it loaded, `Lab` never existed, and 32 tests failed for a reason that had nothing to do
     with any of them: an object literal reads its values at the moment it is built, so building it
     before the values are declared cannot work however far apart they are.

     Reading them inside a function defers that to the moment of the call, which is the only thing
     that makes the order irrelevant. The failure was silent in the worst way - the console showed a
     ReferenceError from a line that looked entirely reasonable. */
  const BRAZIER_GEOM=()=>({bowlY:BRAZIER_BOWL_Y, flameBaseY:FLAME_BASE_Y, flameR:FLAME_R, r:BRAZIER_R});
  function gridSprite(b){
    const key=b.w+'x'+b.h+'@'+b.l+','+b.t;
    let c=gridCache.get(key);
    if(c) return c;
    c=mk(b.w,b.h);
    const g=c.getContext('2d');
    for(let pass=0;pass<2;pass++){
      const step=pass?MAJOR:MINOR;
      // a pass whose alpha is zero is skipped rather than stroked invisibly: stroking 21 invisible
      // lines costs a path build and a rasterise per frame for a lattice nobody can see
      if(gridAlphaFor(step)===0) continue;
      g.strokeStyle=pass?'rgba(214,178,110,'+gridAlphaFor(step)+')'
                        :'rgba(150,160,180,'+gridAlphaFor(step)+')';
      g.lineWidth=1;
      g.beginPath();
      for(let x=0;x<=b.w;x+=step){ g.moveTo(x+0.5,0); g.lineTo(x+0.5,b.h); }
      for(let y=0;y<=b.h;y+=step){ g.moveTo(0,y+0.5); g.lineTo(b.w,y+0.5); }
      g.stroke();
    }
    const cx=b.w/2, cy=b.h/2;
    g.strokeStyle='rgba(214,178,170,0.22)'; g.lineWidth=1;
    g.strokeRect(cx-0.5,cy-0.5,1,1);
    g.beginPath();
    g.moveTo(cx-14,cy); g.lineTo(cx+14,cy); g.moveTo(cx,cy-14); g.lineTo(cx,cy+14);
    g.stroke();
    g.beginPath(); g.arc(cx,cy,7,0,7); g.stroke();
    gridCache.set(key,c);
    return c;
  }
  function drawGrid(){
    const b=currentRoom().bounds;
    ctx.drawImage(gridSprite(b),b.l,b.t);
  }

  /* BRAZIERS on the walls. Warm pools of light on a cold floor, and - like the grid - fixed points
     the camera passes. They also do the job the room vignette cannot: the vignette darkens toward
     the edges, which on a room this size means the far corners go almost black, and a body fighting
     out there would be invisible. The braziers put light back where the play is. */
  /* BRAZIERS, AND WHY THEY ARE A SPRITE.

     Measured, not assumed: a normal frame in this game allocates ZERO canvas gradients and renders
     30 bodies in 0.44ms. The lab allocated 29 gradients per frame - 15 radial, 14 linear - and
     rendered in 0.94ms. Every one of those was light that looks identical every frame being rebuilt
     from scratch every frame. createRadialGradient is not free, and twenty-nine of them is most of a
     millisecond.

     So they are baked, the way the floor and the sprites already are in this project. The pool and
     the column go into one canvas per brazier, drawn with a single drawImage; the flame goes into
     five baked frames picked by the same counter that drives everything else that flickers.

     The pool's brightness rides in globalAlpha rather than being baked in, because alpha is free and
     a second sprite per flicker step is not. And the sprites are built ONCE and cached in a Map,
     which is the same shape as spriteCache and glowCache in 10-art.js - the lab was the only place
     in the game still building its art from scratch on every frame.

     FREE-STANDING COLUMNS IN THE ROOM, not on its walls. The first version put all eight braziers on
     the walls, and in a room larger than the viewport the walls are never on screen, so all eight
     sat outside the frame at every camera position and the lab rendered with no light in it at all.
     A count of "eight braziers placed" says nothing about how many are visible. */
  const BRAZIER_R=168, BRAZIER_FRAMES=5, brazierCache=new Map();
  /* WHERE THE FLAME GOES, derived rather than guessed. BRAZIER_BOWL_Y is the bowl's centre inside the
     brazier sprite (baked at o-21 in brazierSprite); FLAME_BASE_Y is the flame's base inside the
     flame sprite (baked at o+25 in flameFrame); FLAME_R is the half-size of the flame sprite. The
     flame is drawn so its base lands exactly on the bowl's centre - which is the only relationship
     between the two numbers that means anything visually, and which the previous pair of constants
     did not have. */
  const BRAZIER_BOWL_Y=21, FLAME_BASE_Y=25, FLAME_R=36;
  function brazierSprite(){
    let c=brazierCache.get('body');
    if(c) return c;
    const S=BRAZIER_R*2;
    c=mk(S,S);
    const g=c.getContext('2d'), o=BRAZIER_R;
    const pool=g.createRadialGradient(o,o,2,o,o,BRAZIER_R);
    pool.addColorStop(0,'rgba(255,178,90,0.2)');
    pool.addColorStop(1,'rgba(255,140,60,0)');
    g.fillStyle=pool; g.beginPath(); g.arc(o,o,BRAZIER_R,0,7); g.fill();
    g.fillStyle='#1a1c22'; g.beginPath(); g.ellipse(o,o+9,16,7,0,0,7); g.fill();
    g.fillStyle='#23262e'; g.fillRect(o-5,o-20,10,28);
    g.fillStyle='#343a45'; g.fillRect(o+1,o-20,4,28);
    g.fillStyle='#2c3038'; g.beginPath(); g.ellipse(o,o-21,14,6,0,0,7); g.fill();
    g.fillStyle='#464e5c'; g.beginPath(); g.ellipse(o,o-23,11,5,0,0,7); g.fill();
    brazierCache.set('body',c);
    return c;
  }
  /* FIVE BAKED FLAMES, not a blend. The same reasoning as the momentum ramp: a blend is a colour
     the eye cannot name, and a flame that is always exactly one of five heights reads as a flame
     cycling rather than as a rendering artefact. */
  function flameFrame(i){
    const key='f'+i;
    let c=brazierCache.get(key);
    if(c) return c;
    const S=72, o=S/2, fh=19+5*(i/(BRAZIER_FRAMES-1));
    c=mk(S,S);
    const g=c.getContext('2d');
    g.globalCompositeOperation='lighter';
    g.fillStyle='rgba(255,120,40,0.6)';
    g.beginPath(); g.moveTo(o,o+25);
    g.quadraticCurveTo(o+10,o+25-fh*0.6,o,o+25-fh); g.quadraticCurveTo(o-10,o+25-fh*0.6,o,o+25); g.fill();
    g.fillStyle='rgba(255,220,150,0.85)';
    g.beginPath(); g.moveTo(o,o+25);
    g.quadraticCurveTo(o+5,o+25-fh*0.4,o,o+25-fh*0.6); g.quadraticCurveTo(o-5,o+25-fh*0.4,o,o+25); g.fill();
    brazierCache.set(key,c);
    return c;
  }
  function braziers(){
    const b=currentRoom().bounds, spots=[];
    /* FREE-STANDING COLUMNS IN THE ROOM, not on its walls. The first version put all eight braziers
       on the walls - corners and midpoints - and in a room larger than the viewport the walls are
       NEVER on screen, so all eight sat outside the frame at every camera position and the lab
       rendered with no light in it at all. Nothing was broken; there was nothing there to see, and a
       count of "eight braziers placed" says nothing about how many are visible. They are set in from
       the walls so the ones in the middle are always in frame, and spaced off the room so the
       density per screen stays constant as it grows. */
    const stepX=300;
    for(let x=b.l+175;x<=b.r-175;x+=stepX)
      for(let y=b.t+175;y<=b.b-175;y+=250) spots.push([x,y]);
    const body=brazierSprite();
    const flame=flameFrame(Math.floor(frameCount/3)%BRAZIER_FRAMES);
    for(const s of spots){
      const x=s[0],y=s[1];
      ctx.save();
      ctx.globalAlpha=0.86+0.14*Math.sin(frameCount*0.21/SPEEDUP);
      ctx.drawImage(body,x-BRAZIER_R,y-BRAZIER_R);
      ctx.globalAlpha=1;
      /* THE FLAME SITS IN THE BOWL, and it used to sit on the floor beside the stand.

         Both sprites are baked around their own centre, so their features live at an offset inside
         them, and the two offsets were never reconciled: the bowl is drawn at `o-21` within the
         brazier sprite (so `y - BRAZIER_R - 21` on screen) while the flame's BASE is baked at `o+25`
         within the flame sprite (so `y - 48 + 25 = y - 23`). That put the flame's base 166px below the
         bowl it belongs to - which is a fifth of a brazier's height - and read as a lit line on the
         floor beside a stand, rather than as fire in a dish.

         The offsets are now named and derived from the sprites themselves rather than from whatever
         two numbers were typed next to each other, so moving the bowl in the brazier sprite moves the
         flame with it. BRAZIER_BOWL_Y is where the bowl sits inside the brazier sprite, and
         FLAME_BASE_Y where the flame's base sits inside the flame sprite. */
      ctx.drawImage(flame, x-FLAME_R, y-BRAZIER_R-BRAZIER_BOWL_Y-FLAME_BASE_Y);
      ctx.restore();
    }
  }

  /* A SPECIMEN PLINTH: a stone drum with a lit top face, and a brass nameplate on the front.

     The plinth earns its place twice over. It tells you which body is which without a floating label
     that would fight the sprites, and it is why the row reads as a curated display rather than as
     five things that happen to be in a room - which matters, because the whole claim of this view is
     that it is a considered arrangement rather than a level. The top face catches light and the
     front face is in shadow, which is the same two-value read the game's own stone uses. */
  /* The shadow under a plinth, baked per radius. Same measurement as the braziers: this was one
     createRadialGradient per specimen per frame, for a shape that is a circle with a soft edge and
     never changes. The cache is keyed by radius because the boss's plinth is a different size, and a
     sprite drawn at the wrong scale would be a soft circle of the wrong softness. */
  const shadowCache=new Map();
  function plinthShadow(rad){
    const k=Math.round(rad);
    let c=shadowCache.get(k);
    if(c) return c;
    const S=Math.ceil(rad*2)+4, o=S/2;
    c=mk(S,S);
    const g=c.getContext('2d');
    const sh=g.createRadialGradient(o,o,2,o,o,rad);
    sh.addColorStop(0,'rgba(0,0,0,0.5)'); sh.addColorStop(1,'rgba(0,0,0,0)');
    g.fillStyle=sh; g.beginPath(); g.ellipse(o,o,rad,rad*0.38,0,0,7); g.fill();
    shadowCache.set(k,c);
    return c;
  }
  function drawPlinth(e){
    const rad=e.r+18, top=e.y-e.r-4;
    ctx.save();
    // shadow pooling at the base
    const S=plinthShadow(rad*1.3);
    ctx.drawImage(S,e.x-S.width/2,e.y+e.r+4-S.height/2);
    // the drum
    ctx.fillStyle='#20232a';
    ctx.beginPath(); ctx.ellipse(e.x,e.y+e.r+2,rad,rad*0.34,0,0,7); ctx.fill();
    ctx.fillStyle='#282c35';
    ctx.fillRect(e.x-rad,top+6,rad*2,(e.y+e.r+2)-(top+6)-3);
    // bevelled top: a bright rim and a lit face, so the drum has a lid rather than being a cut-off tube
    ctx.fillStyle='#39404d';
    ctx.beginPath(); ctx.ellipse(e.x,top+6,rad,rad*0.34,0,0,7); ctx.fill();
    ctx.fillStyle='#4b5464';
    ctx.beginPath(); ctx.ellipse(e.x,top+4,rad*0.86,rad*0.28,0,0,7); ctx.fill();
    // the brass plate
    const py=e.y+e.r+26;
    ctx.fillStyle='#0d0e12';
    ctx.fillRect(e.x-rad+6,py-11,rad*2-12,22);
    ctx.strokeStyle='rgba(198,152,74,0.5)'; ctx.lineWidth=1;
    ctx.strokeRect(e.x-rad+6.5,py-10.5,rad*2-13,21);
    ctx.restore();
    const dead=e.hp<=0;
    engraved(dead?e.labName+' - dead':e.labName, e.x, py, dead?'#6b6f78':'#e8cf94');
    engraved(e.labBlurb, e.x, py+12, 'rgba(150,158,172,0.75)', 'center', '10px monospace');
    // the numbers that are the actual point of standing here
    engraved(Math.ceil(e.hp)+' / '+e.maxHp, e.x, py-26, dead?'#6b6f78':'#cfd6e2','center','11px monospace');
  }

  /* THE SHELF: an alcove per item, cut into a stone rail along the top wall, with the item's OWN
     glyph in the item's OWN colour. That is why it does not read as a row of placeholders - there
     are thirteen different marks in thirteen different colours because each one is the item's, and
     you recognise the build you are holding by looking at the rail. Rarity tints the alcove's inner
     shadow, so a legendary is visibly seated in a warmer, deeper niche. */
  const RARITY_TINT={common:'#6f7889',uncommon:'#6fbf9a',rare:'#7fb2f0',legendary:'#f0c86a'};
  /* The alcove's inner glow, baked per rarity. Thirteen alcoves is thirteen createLinearGradient
     calls a frame, for thirteen gradients that differ only by which of four colours they are - so the
     cache has four entries and the draw is a drawImage. Same measurement, same fix. */
  const alcoveCache=new Map();
  function alcoveGlow(tint,w,h){
    const k=tint+':'+Math.round(w)+'x'+Math.round(h);
    let c=alcoveCache.get(k);
    if(c) return c;
    const S=Math.ceil(w), T=Math.ceil(h);
    c=mk(S,T);
    const g=c.getContext('2d');
    const gr=g.createLinearGradient(0,T,0,0);
    gr.addColorStop(0,tint+'44'); gr.addColorStop(1,'rgba(0,0,0,0)');
    g.fillStyle=gr; g.fillRect(0,0,S,T);
    alcoveCache.set(k,c);
    return c;
  }
  function drawShelf(){
    if(!shelf.length) return;
    const b=currentRoom().bounds;
    // the rail spans the widest row and no more. It used to span the room, which put a 1600px beam
    // across the screen behind the HUD and read as a wall rather than as furniture.
    const g=gapOf(), xs=shelf.map(s=>s.x);
    const x0=Math.min.apply(null,xs)-g/2-18, x1=Math.max.apply(null,xs)+g/2+18;
    const y=shelfY(b), h=34;
    ctx.save();
    const rail=ctx.createLinearGradient(0,y-102,0,y+122);
    rail.addColorStop(0,'#2a2e37'); rail.addColorStop(0.5,'#1b1e24'); rail.addColorStop(1,'#2a2e37');
    ctx.fillStyle=rail; ctx.fillRect(x0,y-102,x1-x0,224);
    // a shelf board between the two rows, so it reads as two shelves in a case rather than as
    // thirteen boxes floating in a panel
    ctx.fillStyle='#343a45'; ctx.fillRect(x0+6,y-4,x1-x0-12,8);
    ctx.fillStyle='rgba(198,152,74,0.22)'; ctx.fillRect(x0+6,y-4,x1-x0-12,1);
    ctx.strokeStyle='rgba(198,152,74,0.3)'; ctx.lineWidth=1;
    ctx.strokeRect(x0+0.5,y-101.5,x1-x0-1,223);
    ctx.beginPath(); ctx.moveTo(x0,y-101.5); ctx.lineTo(x1,y-101.5); ctx.stroke();
    for(const s of shelf){
      // each alcove is drawn about ITS OWN y, not the rail's: with two rows they are 116px apart and
      // drawing both at the rail's centre would stack the lower row's arches inside the upper row's
      const ay=s.y, w=Math.min(104,gapOf());
      // the recess: dark inside, with the rarity tint bleeding up from the floor of it
      ctx.fillStyle='#0b0c10';
      ctx.beginPath();
      ctx.moveTo(s.x-w/2,ay+h); ctx.lineTo(s.x-w/2,ay-h*0.35);
      ctx.quadraticCurveTo(s.x,ay-h*1.15,s.x+w/2,ay-h*0.35);
      ctx.lineTo(s.x+w/2,ay+h); ctx.closePath(); ctx.fill();
      const tint=RARITY_TINT[s.rarity]||RARITY_TINT.common;
      ctx.drawImage(alcoveGlow(tint,w,h*1.4),s.x-w/2,ay-h*0.4);
      // the glyph, in the item's own colour, with a bloom so it reads as lit from inside the niche
      ctx.save();
      ctx.textAlign='center'; ctx.font='bold 17px monospace';
      ctx.shadowColor=s.color; ctx.shadowBlur=9;
      ctx.fillStyle=s.color; ctx.fillText(s.glyph,s.x,ay+7);
      ctx.shadowBlur=0;
      ctx.restore();
      // the sill, catching a little of the glyph's colour
      ctx.fillStyle='rgba(0,0,0,0.55)'; ctx.fillRect(s.x-w/2,ay+h-2,w,2);
    }
    ctx.restore();
    for(const s of shelf) engraved(s.name,s.x,s.y+h+15,'#cfc4a4');
  }
  /* The gap is measured from the shelf's own data rather than remembered, and it is measured within
     a ROW - shelf[0] and shelf[1] are neighbours only when there are more than one per row, and
     reading the difference across a row boundary would report a 120px gap for a 104px alcove. */
  const gapOf=()=>{
    const row=shelf.filter(s=>s.row===0);
    if(row.length<2) return 104;
    return Math.abs(row[1].x-row[0].x)-16;
  };

  /* THE DAMAGE NUMBERS. Outlined rather than shadowed, and rising, because a number over a dark
     floor and a number over a lit body need different treatment and one of them has to lose. The
     outline wins: it survives both, and it costs one extra fill of the same string. */
  function drawNumbers_(){
    for(const n of numbers){
      const a=1-n.t/n.max;
      ctx.save();
      ctx.globalAlpha=Math.min(1,a*1.6);
      ctx.font='bold 15px monospace'; ctx.textAlign='center';
      ctx.lineWidth=3; ctx.strokeStyle='rgba(0,0,0,0.85)';
      ctx.strokeText(n.txt,n.x,n.y);
      ctx.fillStyle=n.heal?'#7fe0a8':'#fff2d0';
      ctx.fillText(n.txt,n.x,n.y);
      ctx.restore();
    }
  }

  /* Everything, in the order it should stack. TWO calls rather than one, because one number-free
     draw is not enough and neither is one number-bearing draw:

     draw() is the furniture and goes UNDER the bodies - the survey grid and the braziers are marks
     on the floor, the shelf is a rail on the wall, and a plinth is the thing a body stands ON. A
     plinth drawn over its own specimen would hide the thing you came to look at, and a brazier's
     glow drawn under the floor would be a brazier that does not light anything.

     drawNumbers() is the readout and goes OVER the bodies, because a damage number half-hidden
     behind a lunger is a number you have to move the camera to read, and the camera is the thing
     this lab is also testing. */
  function draw(){
    if(!on) return;
    drawGrid();
    braziers();
    drawShelf();
    const r=currentRoom();
    for(const e of r.enemies) if(e.labSpecimen) drawPlinth(e);
  }
  function drawNumbers(){
    if(!on) return;
    drawNumbers_();
  }
  /* THE LEGEND, and it is drawn in SCREEN space, unlike everything above.

     Every other thing in here scrolls, and that is right - a nameplate pinned to the world belongs
     to the world. But the key list is a property of the VIEW, not of the room, and worse: a legend
     that scrolls away is a legend you have to walk back to, which in a room four screens wide means
     walking back. So it sits in the corner, always, and it is also the only place the lab's state is
     written down - frozen or not, and which body the dropper is armed with - because those are the
     two things you would otherwise have to remember or guess, and a debug view that makes you guess
     is a debug view that produces a wrong number and blames the game. */
  const LEGEND=[
    ['F2','leave the lab'],
    ['F3','freeze / release the row'],
    ['F4','arm the dropper'],
    ['F5','drop a dozen'],
    ['F6','refill the shelf']
  ];
  function drawLegend(){
    if(!on) return;
    /* A HORIZONTAL STRIP ALONG THE BOTTOM, and it was a panel in the bottom corner - which put it on
       top of the item shelf both times it was tried, once in the bottom-left and once in the
       bottom-right, because the shelf is a wide rail across the lower band of the room and the
       lower band is where the bottom of the screen is.

       A strip is also the shape this game's own furniture already uses: the binds bar under the
       canvas is a row of key chips. So the lab legend reads as part of the same language rather
       than as a debug overlay parked on the picture, and it cannot collide with anything, because
       the only thing it shares its row with is the shelf ABOVE it. */
    const h=26, y=H-h, x=10;
    ctx.save();
    ctx.fillStyle='rgba(8,9,13,0.86)';
    ctx.fillRect(x,y,W-2*x,h);
    ctx.fillStyle='rgba(198,152,74,0.3)';
    ctx.fillRect(x,y,W-2*x,1);
    let cx=x+12;
    ctx.font='12px monospace';
    for(const k of LEGEND){
      const kw=ctx.measureText(k[0]).width+12;
      ctx.fillStyle='rgba(198,152,74,0.18)';
      ctx.fillRect(cx,y+6,kw,15);
      engraved(k[0],cx+kw/2,y+18,'#e8cf94','center','11px monospace');
      engraved(k[1],cx+kw+7,y+18,'rgba(186,194,208,0.92)','left','11px monospace');
      cx+=kw+7+ctx.measureText(k[1]).width+20;
    }
    // the lab's own state, on the right of the same row: frozen or not, and what the dropper is
    // armed with. These are the two things you would otherwise have to remember or guess, and a
    // debug view that makes you guess produces a wrong number and blames the game.
    const t=LAB_SPECIMENS[dropper];
    engraved((frozen?'FROZEN':'LIVE')+'   dropper: '+(t.type==='boss'?'warden':t.type),
             W-x-12,y+18,frozen?'#9fd0ff':'#7fe0a8','right','11px monospace');
    ctx.restore();
  }

  const shelfData=()=>shelf;

  return {on:()=>on, frozen:()=>frozen, dropper:()=>dropper, toggle, enter, leave, key,
          build, layRow, layShelf, drove, tickNumbers, freeze, remember, draw, drawNumbers, drawLegend,
          shelfData, numbers:()=>numbers, LAB_W, LAB_H, tickShelf, syncShelfPickups, BRAZIER_GEOM,
          gridAlphas:()=>({minor:GRID_MINOR_ALPHA, major:GRID_MAJOR_ALPHA, minorStep:MINOR, majorStep:MAJOR}),
          refill:()=>{ for(const s of shelf) s.gone=0; syncShelfPickups(); }};
})();
