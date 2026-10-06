/* 10-art - every sprite, generated rather than loaded No asset files exist in this project. [h:10-art-1] */
/* colour, as arithmetic rather than as a string each time ---------- Three helpers and one reason: [h:10-art-2] */
function hexRgb(h){
  let s=String(h).replace('#','').trim();
  if(!/^([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/.test(s))
    throw new Error('not a colour: "'+h+'" - expected 3 or 6 hex digits, e.g. #abc or #aabbcc');
  if(s.length===3) s=s[0]+s[0]+s[1]+s[1]+s[2]+s[2];
  return [parseInt(s.slice(0,2),16),parseInt(s.slice(2,4),16),parseInt(s.slice(4,6),16)];
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

/* AREA PALETTE, and the one function that reads it ---------- `areaForFloor()` is core (00-balance) and decides WHICH area; this decides what that... [h:10-art-3] */
const AREA_PAL={
  Area1:{ id:'Area1',
    stone:'#2b2f3a', stoneLit:'#454c5c', mortar:'#1d2028',
    floor:'#25211c', floorLight:'255,240,210', floorTint:'#25211c', wash:0,
    accent:'#e8b06a', accentDim:'#c8a878', mapWash:'#090b11' },
  /* THE FOUR AREAS AS HUES, and the temperatures are the design rather than a by-product. [h:10-art-4] */
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
/* LOUD on a missing area, for the same reason Content.get is loud: [h:10-art-5] */
function paletteForArea(area){
  const p=AREA_PAL[area];
  if(!p) throw new Error('paletteForArea: no palette called "'+area+'" (have: '+Object.keys(AREA_PAL).join(', ')+')');
  return p;
}
/* The palette of the floor being played. [h:10-art-6] */
function areaPalette(){ return paletteForArea(areaForFloor()); }

/* THE COMPOSITED FLOOR TINT, and the single place the two tints are mixed. [h:10-art-7] */
function areaFloorTint(type,area){
  const p=paletteForArea(area);
  return mixHex(ROOM_BG[type]||ROOM_BG.normal,p.floorTint,p.wash);
}

/* cave floor texture, ONE PER AREA ---------- It was a single module-level canvas baked at load, so there was exactly one floor in the game and... [h:10-art-8] */
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

/* the wall, as coursed masonry ---------- The wall used to be four `fillRect`s of one flat colour, which is the largest single-colour region on... [h:10-art-9] */
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
/* CLEARING THE TILES, because they are SEEDED PER RUN ---------- These two caches, and the floor cache beside them in 70-view, hold canvases that... [h:10-art-10] */
function clearTileCaches(){
  caveCache.clear();
  wallCache.clear();
}

/* THE COLOUR OF THE AIR IN A ROOM, in one place. [h:10-art-11] */
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
/* one row per enemy type. [h:10-art-12] */
/* A walker's closing speed is the one enemy number a player reads off the screen and reacts to, so it is set directly rather than scaled by PRESSURE... [h:10-art-13] */
/* BRUNCH_CHASE_SPEED IS 2.1 and the ramp is 1.5s, and BOTH numbers were wrong for the same reason: [h:10-art-14] */
const BRUNCH_CHASE_SPEED=2.1,      // no target to protect: 1.31x a chased player, which is a chase
      BRUNCH_SHIELD_SPEED=0.72,   // walking onto a slot: slow, because the slot is a fixed point
      BRUNCH_WALK=0.52*PLAYER_MOVE;
const BRUNCH_RUN=BRUNCH_CHASE_SPEED;

/* The two gunners. [h:10-art-15] */
/* THE ENEMY TABLE. [h:10-art-16] */
const ENEMY={
 lunger:{mass:1,r:14,art:2,bar:26,hp:15*TOUGH,walk:LUNGER_WALK*PLAYER_MOVE,run:LUNGER_RUN*PLAYER_MOVE,armour:ARMOUR},
 brunch:{mass:0.5,r:8,art:1,bar:11,hp:2*TOUGH,walk:BRUNCH_WALK,run:BRUNCH_RUN,armour:1},
  shooter:{mass:0.8,r:14,art:2,bar:26,hp:5.6*TOUGH,base:0.45*LUNGER_PAY,sense:600,range:520,close:150,far:250,cdMin:sec(0.5),cdVar:sec(0.4),dmg:SHOT_DMG,pspd:2.2,pr:5,pcol:'#ff4d4d',armour:ARMOUR},
  gunner:{mass:2.4,r:22,art:3,bar:32,hp:8*TOUGH,base:0.3*LUNGER_PAY,sense:700,range:600,close:120,far:200,cdMin:sec(0.8),cdVar:sec(0.6),dmg:SHOT_DMG*2,pspd:2.05,pr:7,pcol:'#ffb03a',armour:ARMOUR},
  /* THE BOSS HP IS SIZED FROM MEASURED WEAPON DPS, which is the only way to size a health bar. [h:10-art-17] */
 boss:{mass:4,r:28,art:4,bar:40,hp:343*TOUGH,base:0.6*LUNGER_PAY,walk:0.42*PLAYER_MOVE,run:0.72*PLAYER_MOVE,armour:ARMOUR},
};
// A Brunch pack arrives as one knot. big packs are much rarer than small ones, so a room that rolls
// an eight has genuinely gone wrong, and a room that rolls a four is a nuisance rather than a wall
const BRUNCH={pack:[4,5,6,7,8], weight:[0.30,0.25,0.20,0.15,0.10], chance:0.45};
/* The general pace of the game, in three dials that are only meaningful together. [h:10-art-18] */
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

/* paperCache was missing: [h:10-art-19] */
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

/* THE MOMENTUM RAMP - the blink trail, tinted by what the meter was when you blinked. [h:10-art-20] */
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

/* Pixel hearts, filled by FRACTION rather than by one of three states. [h:10-art-21] */
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
  /* The colour of an icon is the colour of the THING, never a default. [h:10-art-22] */
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
/* The gunner's cast tell. [h:10-art-23] */
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
/* Three stars orbiting a held body. [h:10-art-24] */
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
  /* Icons are baked at 34px into a slot whose inner area is 35px wide, so a full-size draw sat half a pixel off both inner edges and the blast's rays... [h:10-art-25] */
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

/* An item's face, for the character sheet. [h:10-art-26] */
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
/* THE SAME TILE ON THE FLOOR, which is not the same thing as the same function. [h:10-art-27] */
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

