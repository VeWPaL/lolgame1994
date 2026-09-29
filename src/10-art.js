/* ==============================================================================================
   10-art  -  every sprite, generated rather than loaded

   No asset files exist in this project. Each body is a pixel array baked to a canvas at load, which
   is why the whole game is 450KB and why there is no art pipeline to manage.

   In Unity the same arrays become a Texture2D built at runtime and handed to a SpriteRenderer, so
   this module ports nearly mechanically. That is the strongest argument for keeping the art
   procedural, and the reason a 15GB asset pipeline never has to exist.
   ============================================================================================== */
/* ---------- cave floor texture ---------- */
const caveCanvas=document.createElement('canvas');
caveCanvas.width=128;caveCanvas.height=128;
(function(){
  const c=caveCanvas.getContext('2d');
  c.fillStyle='#25211c';c.fillRect(0,0,128,128);
  for(let i=0;i<220;i++){
    const x=Rnd.art()*128,y=Rnd.art()*128,r=1+Rnd.art()*3;
    c.fillStyle='rgba('+(Rnd.art()<0.5?'0,0,0,':'255,240,210,')+(0.04+Rnd.art()*0.08)+')';
    c.beginPath();c.arc(x,y,r,0,7);c.fill();
  }
  for(let i=0;i<36;i++){
    const x=Rnd.art()*128,y=Rnd.art()*128,r=3+Rnd.art()*7;
    c.fillStyle='rgba(0,0,0,0.10)';
    c.beginPath();c.arc(x,y,r,0,7);c.fill();
  }
})();

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
const CHASER_UPPER=["h.....","hh....",".hdddd","..dddd","..dyyd","..dkkk",".mmttt","mmtttt","dmtttt","dmtttt",".bbbbb",".bpppp","..pppp"].map(mirror);
const CHASER_PAL={h:'#e6d9b8',d:'#d1495b',y:'#ffe066',k:'#3a141c',m:'#b03a4a',t:'#8f2b3a',b:'#3a141c',p:'#5c1f28'};
const BOSS_PAL={h:'#f2e2b0',d:'#ff8a3d',y:'#fff1a0',k:'#3d1c08',m:'#d9662a',t:'#b5501f',b:'#3d1c08',p:'#5c2c10'};
const SHOOTER_UPPER=["...kkk","..kkkk",".kkkkk",".kkeed",".kkddd",".kkmtt","mmtttt","mstttt",".ttttt",".ttttt","tttttt","tttttt"].map(mirror);
const SHOOTER_PAL={k:'#4a2f7a',d:'#160d24',e:'#7ff5ff',m:'#6a3fae',t:'#5b3a9e',s:'#c9a6ff',b:'#241a3d'};
// the gunner is the same body scaled up and recoloured crimson: identical frame, bigger pixel size
const GUNNER_PAL={k:'#5a2436',d:'#1a0c12',e:'#ffe08a',m:'#8a2f4a',t:'#6d2238',s:'#ff9db0',b:'#2a1018'};
// Brunch is the chaser shrunk to a single pixel of sprite and recoloured acid, so a pack of them
// reads as one bright smear coming at you rather than as several bodies
const BRUNCH_PAL={h:'#eef7a4',d:'#5f6d1c',y:'#f6ff3d',k:'#171c08',m:'#bfe04a',t:'#9cc033',b:'#3b4712',p:'#5d6d1a'};
const PLAYER_FRAMES=LEG_FRAMES.map(l=>PLAYER_UPPER.concat(l)), CHASER_FRAMES=LEG_FRAMES.map(l=>CHASER_UPPER.concat(l)), SHOOTER_FRAMES=LEG_FRAMES.map(l=>SHOOTER_UPPER.concat(l));
// declared up here because the enemy table below reads PLAYER_MOVE; the note on what these three
// dials are for, and the measurement behind them, is under BRUNCH
const TEMPO={rate:1.5}, PRESSURE={rate:1.5}, PLAYER_MOVE=1.2;
/* one row per enemy type. HP carries the +12% that pays for the hit-slowdown every body now gets,
   CHASER_PAY takes 4% back out of the movement, and the gunner is the shooter cloned: half the
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
// how often the guns open up, and folding it in here would quietly turn every chaser into a threat
// nobody asked for. The chaser keeps its 77% of the player (the player got quicker, so the chaser
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
const ENEMY={
 chaser:{mass:1,r:14,art:2,bar:26,hp:18*TOUGH,walk:CHASER_WALK*PLAYER_MOVE,run:CHASER_RUN*PLAYER_MOVE,armour:ARMOUR},
 brunch:{mass:0.5,r:8,art:1,bar:11,hp:2*TOUGH,walk:BRUNCH_WALK,run:BRUNCH_RUN},
  shooter:{mass:0.8,r:14,art:2,bar:26,hp:5.6*TOUGH,base:0.45*CHASER_PAY,sense:600,range:520,close:150,far:250,cdMin:sec(0.5),cdVar:sec(0.4),dmg:SHOT_DMG,pspd:2.2,pr:5,pcol:'#ff4d4d'},
  gunner:{mass:2.4,r:22,art:3,bar:32,hp:8*TOUGH,base:0.3*CHASER_PAY,sense:700,range:600,close:120,far:200,cdMin:sec(0.8),cdVar:sec(0.6),dmg:SHOT_DMG*2,pspd:2.05,pr:7,pcol:'#ffb03a',armour:ARMOUR},
 boss:{mass:4,r:28,art:4,bar:40,hp:50*TOUGH,base:0.6*CHASER_PAY},
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
function dropLoot(x,y){
  const roll=Rnd.run();
  if(roll<0.18) return {x,y,r:10,kind:'heart'};
  if(roll<0.26) return {x,y,r:10,kind:'armor'};
  return null;
}

