/* ==============================================================================================
   10-art  -  every sprite, generated rather than loaded

   No asset files exist in this project. Each body is a pixel array baked to a canvas at load, which
   is why the whole game is 450KB and why there is no art pipeline to manage.

   In Unity the same arrays become a Texture2D built at runtime and handed to a SpriteRenderer, so
   this module ports nearly mechanically. That is the strongest argument for keeping the art
   procedural, and the reason a 15GB asset pipeline never has to exist.
   ============================================================================================== */
/* ---------- colour, as arithmetic rather than as a string each time ----------
   Three helpers and one reason: the area palette below is a table of hues, and the places that
   consume it need a WASH (the same hue at a fraction of its strength) and a BLEND (this hue mixed
   into that one). Both were being done by writing the resultant hex out by hand, which is how a
   palette stops being a palette and becomes sixteen unrelated literals that drift apart the first
   time one of them is nudged.

   Hex only, because everything in the existing vocabulary is written as hex. A three-digit form is
   expanded rather than rejected, so a hand-typed '#abc' is a legal argument and not a silent
   black. */
function hexRgb(h){
  let s=String(h).replace('#','').trim();
  if(s.length===3) s=s[0]+s[0]+s[1]+s[1]+s[2]+s[2];
  const n=parseInt(s,16);
  if(!Number.isFinite(n)) throw new Error('not a colour: "'+h+'"');
  return [(n>>16)&255,(n>>8)&255,n&255];
}
const rgbHex=(r,g,b)=>'#'+[r,g,b].map(v=>Math.max(0,Math.min(255,Math.round(v))).toString(16).padStart(2,'0')).join('');
/* MIX. `t` is how much of B lands in the result, so mixHex(a,b,0) is a and mixHex(a,b,1) is b. Both
   ends are exact: the test pins them, because a helper whose endpoints are approximate cannot be
   used to keep Area1 identical to what it was. */
function mixHex(a,b,t){
  const A=hexRgb(a),B=hexRgb(b);
  return rgbHex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t);
}
/* ALPHA, as rgba. Used where the code wants a translucent version of a palette hue rather than a
   new colour, so the two cannot stop agreeing - the descent banner's rule under the floor numeral
   used to be its own literal and Area1 still has to produce the same one. */
function withAlpha(h,a){ const c=hexRgb(h); return 'rgba('+c[0]+','+c[1]+','+c[2]+','+a+')'; }

/* ---------- AREA PALETTE, and the one function that reads it ----------
   `areaForFloor()` is core (00-balance) and decides WHICH area; this decides what that area looks
   like. The split is the point: identity is carried by the enemy mix and by the palette, and
   neither of them touches the depth ladder. A themed block is not a harder version of the climb,
   it is the same climb somewhere else.

   THE FIELDS, and each one earns its place by being read somewhere a player will look:

     stone      the wall. The single largest colour on screen after the floor, and the one the eye
                reads as "where am I" before it reads anything else.
     stoneLit   the lit edge of each course, so the wall is masonry and not a rectangle.
     mortar     the joint between courses. Without it a wall has no scale and reads as a border.
     floor      the base the cave speckle sits on. Close-up texture, not the room's colour.
     floorLight the pale speckle. One per area, because a fleck of the wrong hue is what makes a
                recoloured floor look like a filter over the old one.
     floorTint  the hue the room is WASHED in, over the room-type tint. See wash.
     wash       how strongly. 0 for Area1, deliberately: see THE ONE MEASURED CLAIM below.
     accent     UI ink struck into wood - the depth numeral, the descent banner, the area line on
                the character sheet.
     accentDim  the same ink unlit, for the numeral on floor 1 and the "+N" tally.
     mapWash    the near-black ink laid over the minimap's paper. Deliberately NOT `floorTint`: that
                is the hue of the stone under the player, and putting it on the board made the map
                three shades lighter in every channel, which eats the contrast an unvisited room
                needs to stay a light shell rather than a hole in the board.

   THE ONE MEASURED CLAIM: **Area1 is the build that existed before areas did.** Every number
   measured in this project's history - every TTK, every reaction window, every screenshot anyone
   looked at - was measured on floors 1-4 with this palette. So Area1's stone is the `#2b2f3a` the
   wall was already, its floor is the `#25211c` the cave was already, its accent is the `#e8b06a`
   the depth numeral was already, and its wash is ZERO, which is what makes the floor composite
   byte-identical rather than approximately right. A theming pass that quietly re-tinted the first
   four floors would invalidate all of it while looking like a feature. */
const AREA_PAL={
  Area1:{ id:'Area1',
    stone:'#2b2f3a', stoneLit:'#454c5c', mortar:'#1d2028',
    floor:'#25211c', floorLight:'255,240,210', floorTint:'#25211c', wash:0,
    accent:'#e8b06a', accentDim:'#c8a878', mapWash:'#090b11' },
  /* THE FOUR AREAS AS HUES, and the temperatures are the design rather than a by-product.

     TWO COOL AND TWO WARM, deliberately and 2:2 rather than a gradient from cold to hot. A palette
     that ramps monotonically - slate, then olive, then amber, then red - reads as a slider the
     player is being dragged along, and makes each floor look like a slightly worse version of the
     next. Alternating means every descent is a CHANGE, and the two warm areas then have to be told
     apart from each other rather than from the cold ones:

       Area1  blue-grey  b(58) > g(47) > r(43)     cold slate. The shipped look, untouched.
       Area3  green      g(56) > b(51) > r(31)     cold, and unmistakably not Area1: green, and
                                                        a channel Area1 never leads on.
       Area2  amber      r(61) > g(42) > b(32)     warm, and orange - r/g is 1.45, which is brick.
       Final  oxblood    r(51) > g(22) > b(29)     warm, and red - r/g is 2.32. It is the same
                                                        temperature as Area2 and half the green, so
                                                        the two warm areas are distinguishable on
                                                        a channel rather than on brightness.

     Every pair is 42 apart in summed RGB on the wall and 49 on the floor wash (measured, visual pass), and those
     are the measured minimums; the test floors sit below them. The wall is the largest
     thing on screen and the wall floor sits exactly at its minimum, so any nudge breaks the bar. */
  Area2:{ id:'Area2',
    stone:'#3d2a20', stoneLit:'#63432e', mortar:'#261811',
    floor:'#30231a', floorLight:'255,224,168', floorTint:'#5a3a12', wash:0.22,
    accent:'#e08a4a', accentDim:'#b87545', mapWash:'#100a07' },
  Area3:{ id:'Area3',
    stone:'#16382e', stoneLit:'#3c5b52', mortar:'#14241f',
    floor:'#1b2a25', floorLight:'190,240,214', floorTint:'#1e3c34', wash:0.20,
    accent:'#7fd4b0', accentDim:'#63a68c', mapWash:'#070f10' },
  Final:{ id:'Final',
    stone:'#2e0f1c', stoneLit:'#5e2c34', mortar:'#210e13',
    floor:'#26161a', floorLight:'255,196,208', floorTint:'#3e0d20', wash:0.26,
    accent:'#ff9a8a', accentDim:'#c97668', mapWash:'#140609' },
};
/* LOUD on a missing area, for the same reason Content.get is loud: an unknown id here would
   otherwise return undefined and the first thing anybody would see is a room painted in the colour
   of null. The message names the id and lists what exists, because the mistake is always a typo in
   a new area or a rename somebody did in one place. */
function paletteForArea(area){
  const p=AREA_PAL[area];
  if(!p) throw new Error('paletteForArea: no palette called "'+area+'" (have: '+Object.keys(AREA_PAL).join(', ')+')');
  return p;
}
/* The palette of the floor being played. Every drawing site reads this rather than reaching for the
   table, so there is exactly one place that turns "which floor am I on" into "what does it look
   like". Pure: it reads the floor and a table, spends no RNG, and changes nothing - which is what
   makes it safe to call from a draw path without disturbing a seeded run. */
function areaPalette(){ return paletteForArea(areaForFloor()); }

/* THE COMPOSITED FLOOR TINT, and the single place the two tints are mixed.

   It is a function rather than a pre-mixed colour because the floor and the DOORWAY both need it and
   they are drawn in different places: one inside the baked floor sprite, one as an opaque rect over
   the wall gap. Two copies of this expression is the doorRect bug in a different costume - two
   rectangles that agreed perfectly right up until the first room that was not 700x450, except the
   thing that disagreed here would be a room that is not on floor 1.

   ROOM TYPE FIRST, WASH SECOND, because that is the order the two are composited in on the floor, and
   a mix that reversed its arguments would be a subtly different colour in the doorway than in the
   room it opens into - a difference no screenshot of a single room would show.

   The endpoints are exact and the suite pins both: mixHex(a,b,0)===a and mixHex(a,b,1)===b. A helper
   whose endpoints drift is not usable for the job it has here, which is keeping Area1 identical to
   the build every earlier measurement was taken against - "0.52 of the way" has to be 0.52 of the
   way, not near enough. */
function areaFloorTint(type,area){
  const p=paletteForArea(area);
  return mixHex(ROOM_BG[type]||ROOM_BG.normal,p.floorTint,p.wash);
}

/* ---------- cave floor texture, ONE PER AREA ----------
   It was a single module-level canvas baked at load, so there was exactly one floor in the game and
   re-baking it per area is what gives an area a floor at all.

   Baked LAZILY rather than all four at load, for a reason that is about not disturbing things: the
   old bake ran during module load, immediately BEFORE the wood grain baked itself further down this
   file. Moving four tiles to load time would push four tiles' worth of art draws in front of the
   grain and change the wood in every HUD plate in the game. Lazy keeps the art stream in exactly
   the order the shipped build drew it - the grain is baked from the same first values it always
   was - and a floor is baked once per area per session either way.

   The counts (220 flecks, 36 soft blotches) are unchanged, so texture DENSITY is identical across
   areas and only the hue differs. A theming pass that also changed how busy the floor is would be
   two changes, and the second one would hide the first. */
const caveCache=new Map();
function caveTile(area){
  let c=caveCache.get(area);
  if(c) return c;
  const p=paletteForArea(area);
  c=document.createElement('canvas');
  c.width=128;c.height=128;
  const g=c.getContext('2d');
  g.fillStyle=p.floor;g.fillRect(0,0,128,128);
  for(let i=0;i<220;i++){
    const x=Rnd.art()*128,y=Rnd.art()*128,r=1+Rnd.art()*3;
    g.fillStyle='rgba('+(Rnd.art()<0.5?'0,0,0,':p.floorLight+',')+(0.04+Rnd.art()*0.08)+')';
    g.beginPath();g.arc(x,y,r,0,7);g.fill();
  }
  for(let i=0;i<36;i++){
    const x=Rnd.art()*128,y=Rnd.art()*128,r=3+Rnd.art()*7;
    g.fillStyle='rgba(0,0,0,0.10)';
    g.beginPath();g.arc(x,y,r,0,7);g.fill();
  }
  caveCache.set(area,c);
  return c;
}

/* ---------- the wall, as coursed masonry ----------
   The wall used to be four `fillRect`s of one flat colour, which is the largest single-colour region
   on screen and reads as a frame around the game rather than as stone. Four area palettes over a
   flat rectangle would have made that four times as obvious, so the courses are here for the same
   reason the floor speckle is: a wall needs texture to have scale.

   TWO 16px COURSES PER TILE, and 16 is `wt`, the wall thickness drawRoom uses - so a tile is
   exactly as tall as one band of wall and the joints land on whole bands rather than being cut in
   half by the fill. The second course's joints are offset by half a block, which is the one piece
   of masonry knowledge that matters: a wall whose joints line up into continuous verticals reads
   as tile, and this project has already spent a day deleting a 120px floor lattice for exactly
   that reason.

   The tone variation is per-block and low, because the wall is behind the fight. It has to be
   legible as masonry at a glance and invisible as texture while you are aiming at something. */
const WALL_TILE_W=64, WALL_TILE_H=32, WALL_COURSE=16;
const wallCache=new Map();
function wallTile(area){
  let c=wallCache.get(area);
  if(c) return c;
  const p=paletteForArea(area);
  c=mk(WALL_TILE_W,WALL_TILE_H);
  const g=c.getContext('2d');
  g.fillStyle=p.stone;g.fillRect(0,0,WALL_TILE_W,WALL_TILE_H);
  for(let row=0;row<WALL_TILE_H/WALL_COURSE;row++){
    const y=row*WALL_COURSE, off=row%2?WALL_TILE_W/4:0;
    // the mortar course, then each block's own tone, then the lit top edge of the course above it
    g.fillStyle=p.mortar;g.fillRect(0,y+WALL_COURSE-1,WALL_TILE_W,1);
    for(let bx=-1;bx<2;bx++){
      const x=bx*WALL_TILE_W/2+off;
      g.fillStyle='rgba('+(Rnd.art()<0.5?'0,0,0,':'255,246,224,')+(0.03+Rnd.art()*0.05)+')';
      g.fillRect(x+1,y,WALL_TILE_W/2-2,WALL_COURSE-1);
    }
    g.fillStyle=withAlpha(p.stoneLit,0.5);g.fillRect(0,y,WALL_TILE_W,1);
  }
  wallCache.set(area,c);
  return c;
}

/* THE COLOUR OF THE AIR IN A ROOM, in one place.

   Two things are layered on a floor: the room TYPE's tint (start / normal / item / boss, which
   says what the room is FOR) and the area's wash (which says where you are). Keeping them as two
   operations rather than one pre-mixed colour is what stops a themed area from erasing the room
   type - an item room has to go gold in every area, or the glow that tells you a room is an upgrade
   stops meaning anything.

   And the doorway has to show the same answer as the floor behind it, which is why this is one
   function rather than the flat type tint written out at the call site: the gap in the wall is
   filled opaquely, so it has to be the composited colour or every doorway reads as a differently
   coloured rectangle punched through a room.

   A one-line alias rather than a second name for the same thing at the call site, because the test
   reads `roomTone` and the drawing should not be able to answer a different function than the one
   that was asserted. Two names for one expression is fine; two EXPRESSIONS is what has bitten this
   project four times. */
function roomTone(type,area){ return areaFloorTint(type,area); }

/* ---------- pixel-art ---------- */
function mirror(h){return h+h.split('').reverse().join('');}
function mk(w,h){const c=document.createElement('canvas');c.width=w;c.height=h;return c;}
const LEG_FRAMES=[
  ["..bb....bb..","..bbb...bb.."],
  ["...bb..bb...","...bb..bb..."],
  ["..bb....bb..","..bb...bbb.."],
  ["...bb..bb...","...bb..bb..."],
];
const PLAYER_UPPER=[".....h","....hh","...hhh","..hhhh",".hhhhh",".yyyyy","hhhhhh","..ffff","..ffef","..fwww","...www",".mmtww","mmtttw","smyyyy",".mtttt","mmmmmm"].map(mirror);
const PLAYER_PAL={h:'#4b2f86',y:'#e8c04a',f:'#f3cfa8',e:'#1a1220',w:'#ece8f5',m:'#7a55c4',t:'#5b3a9e',s:'#f3cfa8',b:'#2a1d38'};
const LUNGER_UPPER=["h.....","hh....",".hdddd","..dddd","..dyyd","..dkkk",".mmttt","mmtttt","dmtttt","dmtttt",".bbbbb",".bpppp","..pppp"].map(mirror);
const LUNGER_PAL={h:'#e6d9b8',d:'#d1495b',y:'#ffe066',k:'#3a141c',m:'#b03a4a',t:'#8f2b3a',b:'#3a141c',p:'#5c1f28'};
const BOSS_PAL={h:'#f2e2b0',d:'#ff8a3d',y:'#fff1a0',k:'#3d1c08',m:'#d9662a',t:'#b5501f',b:'#3d1c08',p:'#5c2c10'};
const SHOOTER_UPPER=["...kkk","..kkkk",".kkkkk",".kkeed",".kkddd",".kkmtt","mmtttt","mstttt",".ttttt",".ttttt","tttttt","tttttt"].map(mirror);
const SHOOTER_PAL={k:'#4a2f7a',d:'#160d24',e:'#7ff5ff',m:'#6a3fae',t:'#5b3a9e',s:'#c9a6ff',b:'#241a3d'};
// the gunner is the same body scaled up and recoloured crimson: identical frame, bigger pixel size
const GUNNER_PAL={k:'#5a2436',d:'#1a0c12',e:'#ffe08a',m:'#8a2f4a',t:'#6d2238',s:'#ff9db0',b:'#2a1018'};
// Brunch is the lunger shrunk to a single pixel of sprite and recoloured acid, so a pack of them
// reads as one bright smear coming at you rather than as several bodies
const BRUNCH_PAL={h:'#eef7a4',d:'#5f6d1c',y:'#f6ff3d',k:'#171c08',m:'#bfe04a',t:'#9cc033',b:'#3b4712',p:'#5d6d1a'};
const PLAYER_FRAMES=LEG_FRAMES.map(l=>PLAYER_UPPER.concat(l)), LUNGER_FRAMES=LEG_FRAMES.map(l=>LUNGER_UPPER.concat(l)), SHOOTER_FRAMES=LEG_FRAMES.map(l=>SHOOTER_UPPER.concat(l));
// declared up here because the enemy table below reads PLAYER_MOVE; the note on what these three
// dials are for, and the measurement behind them, is under BRUNCH
const TEMPO={rate:1.5}, PRESSURE={rate:1.5}, PLAYER_MOVE=1.2;
/* one row per enemy type. HP carries the +12% that pays for the hit-slowdown every body now gets,
   LUNGER_PAY takes 4% back out of the movement, and the gunner is the shooter cloned: half the
   rate of fire, double the damage per shell, a bigger frame, slower and heavier shells. It also
   carries enough HP to survive a miss or two and sidesteps incoming shots, but only so often.
   Brunch is the anti-solo enemy: quicker than you, so a pack slowly closes on a kiting player, and
   every body that reaches you spends half of itself doing it. That is what stops a swarm from
   spiralling into an unwinnable death loop, and it is why they are worth shooting rather than
   simply outrunning.
   TOUGH is the whole of the difficulty budget. TEMPO makes the player deal damage half again as
   fast, and if HP did not rise to match, that alone would have quietly turned the game into an
   easier one. At 1.35 a body outlasts the extra rate, so the fights are the same length and cost
   more health, which is what the tempo was actually spent on. */
// A walker's closing speed is the one enemy number a player reads off the screen and reacts to, so
// it is set directly rather than scaled by PRESSURE - PRESSURE is about how many bodies turn up and
// how often the guns open up, and folding it in here would quietly turn every lunger into a threat
// nobody asked for. The lunger keeps its 77% of the player (the player got quicker, so the lunger
// does too, and the matchup is unchanged). Brunch is deliberately over that line at 111%: a pack
// slowly eats the gap on a kiting player without ever running one down outright, which is what
// makes it a pack problem rather than a chase problem.
const BRUNCH_WALK=0.62*PLAYER_MOVE, BRUNCH_RUN=1.35;
/* The two gunners. The change in here is about *where they stand*, not about how hard they hit.

   sense is how far off they notice you at all, and ange is where they will actually open up.
   Those used to be the same number, which quietly conflated "I can see you" with "I am willing to
   shoot from here" - and it is why a gunner would ignore a player standing at the edge of the room
   until you walked into its comfort band. Split them: they notice you well before they will fire,
   and while closing they hold a much tighter band than they used to, so the fight happens at close
   quarters instead of at whatever standoff the old numbers happened to allow.

   On rate: the shells are quick and the guns are busy, but not past the point a person can answer.
   At 2.9px a tick a round covers 300px in about half a second, which is inside the ~250ms it takes
   to see something and start moving, so a shot that is plainly on its way is still dodgeable if you
   commit. What makes it hard is the *rate* - a second gunner covering the gap - not any single shell
   being impossible. Neither ever fires through its own side: see clearShot.

   The Brunch deliberately have no armour, so a pack still dies to a hose and the gun you happen to
   be holding is never simply the wrong one. */
/* THE ENEMY TABLE. Every row states its own `armour`, explicitly, and that is a rule rather than a
   detail. It used to be `c.armour||1` at spawn, which meant a row that forgot the field silently
   became a body that takes FULL damage while every other body is multiplied by 0.66 - a 1.52x
   difference nobody had chosen and nobody could see. The Warden was one of the forgetters, and the
   comment beside it said the omission was deliberate, which is the worst state a tuning table can be
   in: a rule that is easy to get wrong, plus a note saying that getting it wrong is intended.

   THE RULE, which is about SIZE and not about any one body:

     every body at least as big as a lunger is armoured - lunger, shooter, gunner, and the boss.

   A shooter is exactly as wide as a lunger, so treating it as chip because it happens to be ranged
   would have been arbitrary. The boss is the largest body in the game and was taking 1.52x more
   damage per hit than anything else, which on an unbounded health ladder is a gap that grows with
   depth rather than staying a property of the body.

   The Brunch is the one exemption, and it is the ORIGINAL design rather than a carve-out made to
   keep a test green. It is half a lunger's radius and an eighth of its health, ARMOUR's own comment
   says it is a stat about "a big body", and the alt blast's pool is sized on the promise that one
   budget deletes a lone heavy OR wipes a small Brunch group outright. Armouring the chip body broke
   that promise, measurably: a blast into three Brunch left all three standing.

   So: stated everywhere, uniform everywhere except the one body the rule is about. `99-tests.js`
   asserts both halves - that no row is silent, and that the ones above the line agree. */
const ENEMY={
 lunger:{mass:1,r:14,art:2,bar:26,hp:15*TOUGH,walk:LUNGER_WALK*PLAYER_MOVE,run:LUNGER_RUN*PLAYER_MOVE,armour:ARMOUR},
 brunch:{mass:0.5,r:8,art:1,bar:11,hp:2*TOUGH,walk:BRUNCH_WALK,run:BRUNCH_RUN,armour:1},
  shooter:{mass:0.8,r:14,art:2,bar:26,hp:5.6*TOUGH,base:0.45*LUNGER_PAY,sense:600,range:520,close:150,far:250,cdMin:sec(0.5),cdVar:sec(0.4),dmg:SHOT_DMG,pspd:2.2,pr:5,pcol:'#ff4d4d',armour:ARMOUR},
  gunner:{mass:2.4,r:22,art:3,bar:32,hp:8*TOUGH,base:0.3*LUNGER_PAY,sense:700,range:600,close:120,far:200,cdMin:sec(0.8),cdVar:sec(0.6),dmg:SHOT_DMG*2,pspd:2.05,pr:7,pcol:'#ffb03a',armour:ARMOUR},
  /* THE BOSS HP IS SIZED FROM MEASURED WEAPON DPS, which is the only way to size a health bar.

     It was 50*TOUGH = 67.5, and measured against the four guns that killed it in 2.20s, 2.66s, 3.81s
     and 5.24s. That is not a fight: it is the same walk with a health bar on it, and a player would
     learn in one attempt that the boss is a formality between the gold key and the next floor.

     Sized so the fight is a fight with the gun the player is actually holding, and re-measured every
     time a weapon changed. On the starting build, 400px, mean of 25 trials:

        Scatter 22.2s    Voidball 29.1s    Bolt 29.9s    Arcane Beam 12.5s

     a spread of x2.39. The spread is left alone deliberately - the slow gun SHOULD take longer, and
     flattening it would mean tuning the boss to the median weapon and telling the player their choice
     does not matter. The depth ladder multiplies all of it, so a floor 10 boss is a long fight by
     exactly the same rule that made everything else on that floor harder.

     The floor is the Arcane Beam with nothing invested in it, which is deliberate - it is a weapon
     whose base damage is low so that Strength sigils are worth several times more to it than to any
     other gun. At zero Strength that gun takes 87.2s here, which is a minute and a half, and the
     4x spread bound in the suite is what stops that from becoming the norm.

     NO ARMOUR, deliberately. Armour would flatten the read: a player watching a health bar fall in
     irregular chunks cannot tell how much of a volley landed, and the whole design is that a player
     who reads the tells takes very little damage.

     THAT ARGUMENT WAS RIGHT AND IT WAS STILL NOT WORTH THE PRICE, which is the part this row did not
     say when it was written. Armour does flatten the read - a bar that falls in regular chunks is
     easier to follow than one that falls in irregular ones. But it was being paid for with a body
     that ignored the single damage rule every other body obeys, and the bill came due three ways:

       - every hit on the boss landed 1/0.66 = 1.52x harder than the same hit anywhere else, so its
         effective health was 19x a lunger's for the same shot count rather than the 29x the numbers
         said;
       - the ladder multiplies health without limit, so the gap was a difference that GREW with depth,
         which is the opposite of what a per-body stat is for - it walked the Warden out of scale with
         the floor it was standing on;
       - and the C# port had already taken the OTHER answer, giving the Warden 0.66 while JavaScript
         gave it 1.0. Two ports, two rules, neither one marking the other wrong.

     Every row in this table now declares its armour, and the boss is one of them.

     Which raises the obvious question: if the boss now takes 0.66, does the fight get 1.52x longer?
     It does, and the first draft of this comment claimed otherwise. "Make the rule consistent" and
     "leave the balance alone" are separate asks, and pretending the first implies the second is how
     a fix becomes a surprise. Worse, the ladder multiplies boss HP without limit, so it compounds: at
     floor 13, where depthTough is 2.50, the Bolt fight would have gone from 111s to 168s.

     So HP absorbs the factor. 520*TOUGH becomes 343*TOUGH = 463.05, which at 0.66 is 701.6 damage
     units - the effective pool the boss had a moment ago (702.0), arrived at by obeying the same rule
     as everything else. That is not a fudge to keep a number still; it is exactly how lunger, gunner
     and shooter already differ from one another. All four are armoured. They have different HP.

     Measured, starting build, one body, firing for real:

       range      Bolt    Scatter   Arcane Beam   Voidball
       150px     30.2s      12.5s         10.4s      29.4s
       200px     31.4s      16.2s         10.2s      29.0s
       300px     41.5s      16.5s         13.3s      32.4s
       400px     40.4s      22.2s         19.7s      35.8s

     and one consequence worth stating plainly, because it was the actual defect: the boss's effective
     health is now 19.0x a lunger's, which is the ratio its HP has always implied on paper. Before
     this change it was 28.9x in the fight, because the number in the table and the number the game
     used were different numbers.

     The paragraph above this one still quotes 22.9 / 27.6 / 39.6 / 54.4s. Those are stale against
     BOTH states - they do not describe the fight as it ran before this change either - and they are
     left only because the sentence introducing them explains what they were for. The measured table
     above is the one to believe. A health bar sized by a figure nobody can reproduce is not a
     measurement, and the honest version of this comment is the one admitting that. */
 boss:{mass:4,r:28,art:4,bar:40,hp:343*TOUGH,base:0.6*LUNGER_PAY,walk:0.42*PLAYER_MOVE,run:0.72*PLAYER_MOVE,armour:ARMOUR},
};
// A Brunch pack arrives as one knot. big packs are much rarer than small ones, so a room that rolls
// an eight has genuinely gone wrong, and a room that rolls a four is a nuisance rather than a wall
const BRUNCH={pack:[4,5,6,7,8], weight:[0.30,0.25,0.20,0.15,0.10], chance:0.45};
/* The general pace of the game, in three dials that are only meaningful together.

   TEMPO is how fast the game READS. It quickens the player: their two attack cooldowns, the alt and
   the hook, and the rate the blink refills. PLAYER_MOVE is the same idea applied to their feet.
   PRESSURE is how much is coming at you: more bodies per room, walkers that close faster, guns that
   open up sooner and reposition quicker.

   The reason this is three dials and not one number is a measurement. A room's clear time is
   roughly (total enemy HP) / (your DPS), and TEMPO is a straight multiplier on your DPS, so tempo
   alone is not a difficulty control at all - it just trades time for safety. On a kiting bot:

       TEMPO alone, 1.0 -> 1.75   clear 5.1s -> 3.4s,  damage 1.0h -> 0.4h

   i.e. speeding the player up makes the game strictly easier, because incoming pressure does not
   scale with it. Enemy fire cadence alone is nearly inert (they already miss a lot at range).
   Enemy HP is the only lever that genuinely raises difficulty. Which means tempo and toughness
   trade off almost exactly one for one, and you cannot buy "faster AND harder" out of the same
   budget - you can only decide which one the wall-clock speed is spent on. This build spends it on
   feel: the game reads half again as fast and you move a fifth quicker, and the ground that gives
   back is paid for in tougher, denser, faster-closing bodies, so a room costs about the same
   wall-clock time as before while costing ~15% more health.

   Nothing here shortens a reaction window. i-frames, blink distance and its i-frames, the ready
   window, the unlock, projectile speeds, enemy engage ranges and the aim assist on a body are all
   untouched, so a faster game means less dead time, never less warning. */
// The gunner's sidestep. Three dials, because "how much it dodges" is really three questions: how
// far it moves, how often it is allowed to, and whether it takes the chance at all. Tuned to cost
// you a shot or two rather than to become a wall of hit points.
const GUNNER_DODGE={kick:0.6, cd:sec(0.6), chance:0.5, sight:250};
const KEY_ROWS=[".ggg.....","g...g....","g...ggggg","g...g.g.g",".ggg....."], KEY_PAL={g:'#ffd23d'};
const KEY_SILVER_PAL={g:'#d8dee9'};
// an unheld key still shows, as the same shape in a darker alloy, so the slot reads as waiting
// for it rather than missing
const KEY_PAL_DIM={g:'#6e5f22'}, KEY_SILVER_DIM={g:'#5b6068'};
const KEY_SKIN={key:KEY_SILVER_PAL,goldkey:KEY_PAL};   // the two keys are the same shape, two alloys
const MOUSE_ROWS=["..mmm..",".mmmmm.","mmmdmmm","mLmdmRm","mLmdmRm","mLmdmRm","mmmdmmm",".mmmmm.","..mmm.."];

/* paperCache was missing: paperTex was reading and writing woodCache under a 'paper' key, which can
   never collide with a 'wood' key, so it re-baked every call. drawRunSummary calls it once per
   frame - 3360 noise iterations and about ten thousand draws, sixty times a second, on the death
   screen. Worse than the cost: the speckle came from the art stream each time, so the paper
   visibly SHIMMERED rather than sitting still. */
const spriteCache={}, glowCache={}, floorCache={}, woodCache={}, paperCache={}, iconCache={}, glyphCache={};
function bakeSprite(rows,palette,pxSize,flash){
  const w=rows[0].length,h=rows.length,pad=2;
  const c=mk(Math.ceil(w*pxSize)+pad*2,Math.ceil(h*pxSize)+pad*2);
  const g=c.getContext('2d');
  g.fillStyle=flash||'#14151a';
  for(let ry=0;ry<h;ry++)for(let rx=0;rx<w;rx++){
    if(rows[ry][rx]==='.')continue;
    g.fillRect(pad+rx*pxSize-1,pad+ry*pxSize-1,pxSize+2,pxSize+2);
  }
  if(!flash)for(let ry=0;ry<h;ry++)for(let rx=0;rx<w;rx++){
    const ch=rows[ry][rx];
    if(ch==='.')continue;
    g.fillStyle=palette[ch];
    g.fillRect(pad+rx*pxSize,pad+ry*pxSize,pxSize,pxSize);
  }
  return c;
}
function drawSprite(rows,palette,cx,cy,pxSize,flash,ck){
  const c=spriteCache[ck]||(spriteCache[ck]=bakeSprite(typeof rows==='function'?rows():rows,palette,pxSize,flash));
  ctx.drawImage(c,Math.round(cx-c.width/2),Math.round(cy-c.height/2));
}
function makeGlow(r,inner,outer){
  const c=mk(r*2,r*2);
  const g=c.getContext('2d'), gr=g.createRadialGradient(r,r,0,r,r,r);
  gr.addColorStop(0,inner);gr.addColorStop(1,outer);
  g.fillStyle=gr;g.fillRect(0,0,r*2,r*2);
  return c;
}
function spellGlow(color,r){return glowCache[color+r]||(glowCache[color+r]=makeGlow(r,color,'rgba(0,0,0,0)'));}
const MUZZLE_GLOW=makeGlow(11,'rgba(230,200,255,1)','rgba(180,110,255,0)');
const DASH_GLOW=makeGlow(14,'rgba(255,255,255,1)','rgba(255,255,255,0)');

/* THE MOMENTUM RAMP - the blink trail, tinted by what the meter was when you blinked.

   Momentum is the one stat that measures what you did rather than what you picked up, and a stat
   like that cannot be taught with a sentence without becoming the thing the sentence is about. So it
   is taught by the art instead: the blink you spend at a full meter leaves a green streak, and one
   you spend at nothing leaves the white streak it always did. A player sees the colour two or three
   times before they see the bar, and connects them on their own, which is the only way anything
   sticks.

   It is read off the blink's OWN momentum rather than live, baked into the trail when it is made. A
   trail that re-reads the meter while it is still on screen would flicker through the whole ramp as
   the meter moved, and a colour that changes under you is not a readout of anything.

   FIVE steps, not a continuous blend, and the reason is that it has to be noticed. A smooth
   interpolation between white and a pale green is a colour the eye cannot name, so it reads as
   "something is different" and never becomes "I am moving well". Five discrete steps is a set of
   states the player can learn, and the jump between them is a thing they can watch happen.

   The green is a spring green rather than the Voidball's deep one, because this is a glow, and a
   glow wants to be brighter than the thing it is lighting. */
const MOMENTUM_STEPS=5;
const MOMENTUM_COLD=[255,255,255], MOMENTUM_HOT=[120,255,160];
const MOMENTUM_GLOW=[];
for(let i=0;i<MOMENTUM_STEPS;i++){
  const t=i/(MOMENTUM_STEPS-1);
  const rgb=MOMENTUM_COLD.map((c,j)=>Math.round(c+(MOMENTUM_HOT[j]-c)*t));
  MOMENTUM_GLOW.push(makeGlow(14,'rgba('+rgb[0]+','+rgb[1]+','+rgb[2]+',1)','rgba('+rgb[0]+','+rgb[1]+','+rgb[2]+',0)'));
}
/* The step for a meter value, clamped at both ends. Momentum.level() rather than value(), because a
   held meter is what the movement code reads, and the trail should agree with the speed it bought. */
function momentumGlow(m){
  const i=Math.round(Math.max(0,Math.min(1,m))*(MOMENTUM_STEPS-1));
  return MOMENTUM_GLOW[i];
}

/* carved-wood + paper interface assets */
const WOOD_TEX=(function(){
  const c=mk(96,96),g=c.getContext('2d');
  g.fillStyle='#6b4423';g.fillRect(0,0,96,96);
  const shades=['#5b3a1c','#7a5230','#684222','#73492a'];
  for(let x=0;x<96;x+=2){
    let y=0;
    while(y<96){const len=(8+((Rnd.art()*30)|0))&~1;g.fillStyle=shades[(Rnd.art()*4)|0];g.fillRect(x,y,2,len);y+=len;}
  }
  for(let i=0;i<26;i++){g.fillStyle='rgba(28,14,5,0.4)';g.fillRect(((Rnd.art()*94)|0)&~1,((Rnd.art()*90)|0)&~1,2,2+2*((Rnd.art()*4)|0));}
  for(let i=0;i<3;i++){const x=10+((Rnd.art()*76)|0),y=10+((Rnd.art()*76)|0);g.fillStyle='#4a2c14';g.fillRect(x,y,6,4);g.fillStyle='#3a200e';g.fillRect(x+2,y+1,2,2);}
  return c;
})();
function woodPlate(w,h){
  const k='wood'+w+'x'+h;if(woodCache[k])return woodCache[k];
  const c=mk(w,h),g=c.getContext('2d');
  g.fillStyle=g.createPattern(WOOD_TEX,'repeat');g.fillRect(0,0,w,h);
  g.fillStyle='#24140a';g.fillRect(0,0,w,2);g.fillRect(0,h-2,w,2);g.fillRect(0,0,2,h);g.fillRect(w-2,0,2,h);
  g.fillStyle='#b98a55';g.fillRect(2,2,w-4,2);g.fillRect(2,2,2,h-4);
  g.fillStyle='#3a2210';g.fillRect(2,h-4,w-4,2);g.fillRect(w-4,2,2,h-4);
  for(const [x,y] of [[5,5],[w-7,5],[5,h-7],[w-7,h-7]]){g.fillStyle='#24140a';g.fillRect(x-1,y-1,4,4);g.fillStyle='#e0c07a';g.fillRect(x,y,2,2);}
  return woodCache[k]=c;
}
function drawInset(x,y,w,h,fill){
  ctx.fillStyle='#120a04';ctx.fillRect(x,y,w,2);ctx.fillRect(x,y,2,h);
  ctx.fillStyle='#8a6238';ctx.fillRect(x,y+h-2,w,2);ctx.fillRect(x+w-2,y,2,h);
  if(fill){ctx.fillStyle=fill;ctx.fillRect(x+2,y+2,w-4,h-4);}
}
function paperTex(w,h){
  const k='paper'+w+'x'+h;if(paperCache[k])return paperCache[k];
  const c=mk(w,h),g=c.getContext('2d');
  g.fillStyle='#efe7d1';g.fillRect(0,0,w,h);
  for(let i=0;i<w*h/40;i++){g.fillStyle=Rnd.art()<0.5?'rgba(150,125,80,0.10)':'rgba(255,255,255,0.35)';g.fillRect((Rnd.art()*w)|0,(Rnd.art()*h)|0,1+((Rnd.art()*2)|0),1);}
  g.fillStyle='rgba(120,95,55,0.10)';g.fillRect(0,0,w,2);g.fillRect(0,h-2,w,2);g.fillRect(0,0,2,h);g.fillRect(w-2,0,2,h);
  return paperCache[k]=c;
}

/* Pixel hearts, filled by FRACTION rather than by one of three states.

   This used to be 'full' / 'half' / 'empty', and that was a real bug waiting to happen. Health is
   not a multiple of two - a shell does 1.8, so a hit can leave you on 0.4 - and the three-state
   heart had a special case for *exactly* 1. Anything below that rendered as a completely empty
   heart, so a player on 0.4 health was staring at a plate with no hearts in it while very much
   alive. Worse, repeated 1.8 damage can leave hp at a float epsilon just above zero, where
   `hp<=0` is false and the run does not end. A continuous fill removes the whole class: a living
   player always has a visible amount in the plate, and the thing on screen is the number that the
   death check is testing. */
const HEART_ROWS=[".##.##.","#######","#######",".#####.","..###..","...#..."];
const HEART_PAL={red:{R:'#e8395a',H:'#ff9db0',E:'#3a2530',G:'#55384a'},gray:{R:'#9aa0ab',H:'#dfe3ea',E:'#3a2530',G:'#55384a'},dimgray:{R:'#4b4f58',H:'#686e78',E:'#241a20',G:'#2b2432'}};
const HEART_STEPS=16;   // fill is quantised to this many steps, purely to bound the sprite cache
function heartRows(fill){
  const f=Math.max(0,Math.min(1,fill)), on=f*7;
  return HEART_ROWS.map((row,ry)=>row.split('').map((ch,rx)=>{
    if(ch!=='#')return '.';
    const lit=rx<on;
    return (ry===0&&rx===1)?(lit?'H':'G'):(lit?'R':'E');
  }).join(''));
}
function drawHeart(cx,cy,fill,scheme,px){
  const q=Math.round(Math.max(0,Math.min(1,fill))*HEART_STEPS);
  drawSprite(()=>heartRows(q/HEART_STEPS),HEART_PAL[scheme],cx,cy,px,null,'heart'+q+scheme+px);
}

/* spell icons */
function pixDisc(g,cx,cy,r,px,color){
  g.fillStyle=color;
  for(let dy=-r;dy<=r;dy++)for(let dx=-r;dx<=r;dx++)if(dx*dx+dy*dy<=r*r+r*0.6)g.fillRect(cx+dx*px,cy+dy*px,px,px);
}
function bakeIcon(idx){
  // The colour of an icon is the colour of the THING, never a default. It used to reach for the alt
  // weapon's colour for every icon at all, which is how the Bolt - a violet spell cast from a violet
  // wand - ended up drawn in blast-orange. Anything the player reads as "this is what I am holding"
  // has to be the same colour as the projectile, the wand tip and the pickup glow, or the icon is
  // actively lying about which gun they picked up.
  // 'alt' is the HUD's name for "whichever right-click you are holding", and it is the same icon as
  // the blast, so it has to resolve to a real weapon rather than indexing WEAPONS with a string
  const col=idx==='hook'?HOOK_WEAPON.color
        :idx==='alt'||idx==='blast'?ALT_WEAPON.color
        :WEAPONS[idx].color;
  const c=mk(34,34),g=c.getContext('2d');
  g.globalAlpha=0.55;g.drawImage(spellGlow(col,16),1,1);g.globalAlpha=1;
  const P=2;
  if(idx===0){
    g.fillStyle=col;g.fillRect(2,15,4,2);g.fillRect(5,19,3,2);g.fillRect(4,11,3,2);
    pixDisc(g,19,17,5,P,col);pixDisc(g,19,17,3,P,'#e9dcff');pixDisc(g,19,17,1,P,'#ffffff');
  }else if(idx===1){
    for(const [x,y] of [[9,21],[25,21],[17,11],[17,27]]){pixDisc(g,x,y,2,P,col);pixDisc(g,x,y,1,P,'#ffd9c4');}
  }else if(idx===2){
    for(let i=0;i<6;i++){const t=i/5,x=Math.round(6+22*t),y=Math.round(28-22*t);pixDisc(g,x,y,i===5?2:1,P,col);g.fillStyle='#dff0ff';g.fillRect(x,y,2,2);}
  }else if(idx===3){
    // the voidball is green now, and it has to read as a HOLE and not a dark ball: a near-black core
    // ringed in green, with the swallowed light of the thing it is named for
    pixDisc(g,17,17,6,P,'#6fbf7a');pixDisc(g,17,17,5,P,'#2a6b34');pixDisc(g,17,17,3,P,'#0d1a10');
    g.fillStyle='#a8e6b0';g.fillRect(9,7,2,2);g.fillRect(24,10,2,2);g.fillRect(24,25,2,2);g.fillRect(8,23,2,2);
  }else if(idx==='hook'){
    // a ring of three bodies being dragged into a single point
    g.fillStyle=col;
    for(const [x,y] of [[7,7],[27,9],[9,26]]) pixDisc(g,x,y,2,P,col);
    for(let i=0;i<5;i++){ const t=i/4, a=2.2-t*1.5;
      g.fillStyle=col; g.fillRect(Math.round(17+Math.cos(a)*13)-1,Math.round(17+Math.sin(a)*13)-1,2,2); }
    pixDisc(g,17,17,4,P,col);pixDisc(g,17,17,2,P,'#cfe6ff');
  }else{
    g.fillStyle=col;
    // the blast is the only icon whose rays reach the edge of the 34px cell, and they are what read
    // as touching the frame. Pulled in by 2px on every side, which is the difference between an
    // icon sitting inside a plate and a plate that looks like it is full.
    for(const [x,y,w,h] of [[16,4,2,4],[16,28,2,4],[4,16,4,2],[26,16,4,2],[8,8,2,2],[24,8,2,2],[8,24,2,2],[24,24,2,2]])g.fillRect(x,y,w,h);
    pixDisc(g,17,17,4,P,col);pixDisc(g,17,17,2,P,'#ffd9a0');pixDisc(g,17,17,1,P,'#ffffff');
  }
  return c;
}
/* The gunner's cast tell. A ring at the muzzle that swells as the charge runs out, in the shell's
   own colour, so it is obviously the same weapon it is about to use. Deliberately small and
   deliberately at the GUNNER rather than at the player: it has to be visible in peripheral vision
   while you are looking at something else, which means close to the thing making it. */
function drawCastFlash(x,y,t,total,col){
  const k=1-t/total;            // 0 at the start of the charge, 1 as it fires
  ctx.save();
  ctx.globalAlpha=0.35+0.6*k;
  ctx.strokeStyle=col;ctx.lineWidth=1+1.5*k;
  ctx.beginPath();ctx.arc(x,y,5+7*k,0,7);ctx.stroke();
  ctx.globalAlpha=0.5*k;
  ctx.drawImage(spellGlow(col,Math.round(6+10*k)),x-8-Math.round(5*k),y-8-Math.round(5*k),
    16+Math.round(10*k),16+Math.round(10*k));
  ctx.restore();
}
/* Three stars orbiting a held body. Deliberately not a ring and not a countdown: a ring reads as a
   timer and a countdown reads as damage, and both are things this is not. They orbit because a
   still shape is easy to miss against a busy floor, and they slow down as the hold runs out so the
   player can see a knot about to come loose - which is exactly when they want to start shooting. */
function drawStunStars(x,y,stun,fc){
  if(stun<=0) return;   // a body that is not held draws nothing, rather than nothing at alpha zero
  const t=fc*0.09/SPEEDUP;
  const left=Math.max(0,stun)/HOOK_WEAPON.hold;
  ctx.save();
  for(let i=0;i<3;i++){
    const a=t+i*2.094;
    const r=8+2*Math.sin(t*1.7+i);
    const sx=x+Math.cos(a)*r, sy=y+Math.sin(a)*r*0.6;
    ctx.globalAlpha=Math.min(1,left*3)*0.95;
    ctx.fillStyle='#ffe9a8';
    // a four-point star: two crossed bars, which is cheap and reads at this size
    const s=2.6;
    ctx.fillRect(Math.round(sx)-s,Math.round(sy)-1,2*s,2);
    ctx.fillRect(Math.round(sx)-1,Math.round(sy)-s,2,2*s);
  }
  ctx.restore();
}
/* The way out of a cleared boss room. A placeholder on purpose: it reads as a way out rather than as
   another pickup, and it is the only thing in the game drawn in the frame's own warm white, so the
   eye goes to it the moment the room goes quiet without a single line of tutorial text. */
function drawExitPortal(cx,cy,fc){
  const t=fc*0.05/SPEEDUP;
  ctx.save();
  // three rings breathing outward on a stagger, so it is obviously alive and obviously not loot
  for(let i=0;i<3;i++){
    const p=((t*0.5+i/3)%1);
    ctx.globalAlpha=0.5*(1-p);
    ctx.strokeStyle='#f4e6c8';ctx.lineWidth=2;
    ctx.beginPath();ctx.arc(cx,cy,8+30*p,0,7);ctx.stroke();
  }
  ctx.globalAlpha=0.20+0.10*Math.sin(t*2);
  ctx.fillStyle='#f4e6c8';
  ctx.beginPath();ctx.arc(cx,cy,15,0,7);ctx.fill();
  // a floor shadow under it, so it sits ON the floor rather than floating over it
  ctx.globalAlpha=0.28;
  ctx.fillStyle='#000';
  ctx.beginPath();ctx.ellipse(cx,cy+16,14,5,0,0,7);ctx.fill();
  // an up-arrow: the one universal "leave" glyph, and the only way anyone will know what it is
  ctx.globalAlpha=0.85;
  ctx.fillStyle='#f4e6c8';
  ctx.fillRect(cx-2,cy-2,4,14);
  ctx.beginPath();
  ctx.moveTo(cx,cy-11);ctx.lineTo(cx+8,cy-1);ctx.lineTo(cx-8,cy-1);
  ctx.closePath();ctx.fill();
  ctx.restore();
}
// Icons are baked 34px into a slot whose inner area is 35px, so drawn full size they fill it edge to
// edge. A little smaller leaves the frame looking like a frame.
const ICON_IN_SLOT=0.86;
function drawIcon(idx,cx,cy,scale){
  const c=iconCache[idx]||(iconCache[idx]=bakeIcon(idx));
  // Icons are baked at 34px into a slot whose inner area is 35px wide, so a full-size draw sat
  // half a pixel off both inner edges and the blast's rays read as touching the wood. Scaled down
  // inside the slot it has real air around it, and the offset is what puts the blast's mass a
  // little left of centre: its rays are the widest part of any icon and they were what filled the
  // frame, so nudging them in is the difference between "an icon in a frame" and "a frame with
  // something crammed into it".
  const s=scale||1, w=Math.round(c.width*s);
  ctx.drawImage(c,Math.round(cx-w/2)-(idx==='blast'?1:0),Math.round(cy-c.height*s/2),w,Math.round(c.height*s));
}
/* minimap glyphs: an upgrade blade for the weapon room and a skull for the boss, drawn as dark ink
   over the coloured cell so they survive being dimmed with everything else */
const MINI_GLYPH={item:["..#..","..#..","#####","..#..","..#.."],
                  boss:[".###.","#####","#.#.#","#####",".#.#."],
                  lock:[".###.","#...#","#####","#####","#.#.#","#####"]};
function bakeGlyph(rows,color,px){
  const c=mk(rows[0].length*px,rows.length*px),g=c.getContext('2d');
  g.fillStyle=color;
  for(let y=0;y<rows.length;y++)for(let x=0;x<rows[y].length;x++) if(rows[y][x]==='#') g.fillRect(x*px,y*px,px,px);
  return c;
}
function drawGlyph(kind,cx,cy,px,color){
  const k=kind+px+color; const c=glyphCache[k]||(glyphCache[k]=bakeGlyph(MINI_GLYPH[kind],color,px));
  ctx.drawImage(c,Math.round(cx-c.width/2),Math.round(cy-c.height/2));
}

function key(x,y){return x+','+y;}
function neighbor(x,y,d){return d==='N'?[x,y-1]:d==='S'?[x,y+1]:d==='E'?[x+1,y]:[x-1,y];}
function shuffle(a){for(let i=a.length-1;i>0;i--){const j=(Rnd.run()*(i+1))|0;[a[i],a[j]]=[a[j],a[i]];}return a;}
// smoothstep, so room fades ease in and out of black instead of snapping
function smooth(t){return t<=0?0:t>=1?1:t*t*(3-2*t);}

/* An item's face, for the character sheet. Drawn rather than labelled, because a list of words is a
   list of words and a player scanning a build is looking for SHAPES - the same reason the HUD uses
   baked icons instead of printing weapon names.

   A beveled tile in the item's own colour with a stamped initial, so even the first dozen items read
   as a set of objects rather than as a column of text. An item definition supplies `glyph` (a short
   mark) and `color`; everything else here is the tile. Memoised per id+size, like every other baker in
   this file - a sheet that re-bakes on every open shimmers while you are trying to read it. */
const itemIconCache={};
function bakeItemIcon(id,px,color,glyph){
  const k=id+px+(color||'')+(glyph||'');
  if(itemIconCache[k])return itemIconCache[k];
  const c=mk(px,px),g=c.getContext('2d'),m=Math.max(1,Math.round(px*0.09));
  g.fillStyle='#0b0e14';g.fillRect(0,0,px,px);
  g.fillStyle=color||'#8a6238';g.fillRect(m,m,px-m*2,px-m*2);
  g.fillStyle='rgba(255,240,210,0.30)';g.fillRect(m,m,px-m*2,m);
  g.fillStyle='rgba(0,0,0,0.42)';g.fillRect(m,px-m*2,px-m*2,m);
  g.fillStyle='rgba(0,0,0,0.55)';g.fillRect(0,0,px,m);g.fillRect(0,0,m,px);
  // a stamped mark, in the darker ink the rest of the sheet uses for anything printed on a surface
  g.fillStyle='rgba(20,12,5,0.72)';
  const t=(glyph||String(id||'?').replace(/[^a-z0-9]/gi,'').slice(0,2)||'?').toUpperCase();
  g.font='bold '+Math.round(px*0.46)+'px monospace';g.textAlign='center';g.textBaseline='middle';
  g.fillText(t.slice(0,2),px/2,px/2+px*0.03);
  return itemIconCache[k]=c;
}
/* returns a live canvas element, because the sheet rebuilds its rows on open and an <img> would need
   a data URL per item per open */
function itemIcon(id,px){
  const def=(typeof Content!=='undefined'&&Content.has('item',id))?Content.get('item',id):null;
  const src=bakeItemIcon(id,px||22,def&&def.color,def&&def.glyph);
  const c=document.createElement('canvas');
  c.width=src.width; c.height=src.height;
  c.getContext('2d').drawImage(src,0,0);
  return c;
}
/* THE SAME TILE ON THE FLOOR, which is not the same thing as the same function.

   `itemIcon` allocates a fresh canvas and copies into it on every call, which is right for the sheet
   (a handful of icons, drawn once per open) and wrong here: an item pickup is drawn EVERY FRAME, and
   a canvas allocation per frame per pickup is the kind of cost that shows up as stutter exactly when
   the player is walking over the thing they came for. So this reads the memoised bake directly.

   The tile is also the right SHAPE to use on the floor. A weapon pickup is a drawn silhouette that
   reads as a tool; thirteen items in their own colours with their own stamped marks read as objects
   laid out on a shelf, and the player can tell two apart from across the room without a label - which
   matters in a playtest, where the first question about any item is "which one was that".

   Kept plain, and this is a decision that was reversed once. A rarity bar went under the tile and the
   tiles were baked at 34 to match the weight of the weapons beside them; both were argued for on the
   grounds that rarity is the axis being judged and that a smaller tile reads as clutter. Having both
   on screen at once settled it the other way - the plain squares are cleaner, and they read as a set
   rather than as thirteen badges competing for attention. Rarity is on the character sheet, which is
   where the player goes when they actually want to know. */
function drawItemIcon(id,cx,cy,px){
  const def=(typeof Content!=='undefined'&&Content.has('item',id))?Content.get('item',id):null;
  const c=bakeItemIcon(id,px||26,def&&def.color,def&&def.glyph);
  ctx.drawImage(c,Math.round(cx-c.width/2),Math.round(cy-c.height/2));
}
function dropLoot(x,y){
  const roll=Rnd.run();
  if(roll<0.18) return {x,y,r:10,kind:'heart'};
  if(roll<0.26) return {x,y,r:10,kind:'armor'};
  return null;
}

