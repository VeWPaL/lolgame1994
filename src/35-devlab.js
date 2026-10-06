/* 35-devlab - a game state that is not a game THE LAB, and what it is for. [h:35-devlab-1] */

/* The five bodies on the row. `boss` is not a kind in the spawn table - spawnEnemy takes it as a
   separate flag - so it is spelled out here rather than hidden behind a `type` that would lie. */
const LAB_SPECIMENS=[
  {type:'lunger', boss:false, blurb:'the baseline body'},
  {type:'brunch', boss:false, blurb:'cover, in numbers'},
  {type:'shooter', boss:false, blurb:'stands off and shoots'},
  {type:'gunner', boss:false, blurb:'solves your line'},
  {type:'boss',   boss:true,  blurb:'the whole fight'}
];

/* 1680x760 against a 960x600 viewport. [h:35-devlab-2] */
const LAB_W=1680, LAB_H=760;
const LAB_DROVE=12;
/* Every drove of Brunch the lab builds needs its OWN pack id, for the reason documented at the spawn site: [h:35-devlab-3] */
let LAB_PACK_CURSOR=1e6;

const Lab=(function(){

  let on=false, frozen=true, dropper=0;
  let numbers=[];          // the floating damage readouts, which the game does not have
  let lastHp=new Map();    // body -> the health it had last tick, which is how damage is derived
  const hpSeen=e=>{ lastHp.set(e,e.hp); return e.hp; };

  /* ------------------------------------------------------------------ the room -------------- */

  /* The lab room is built by REPLACING the bounds on the room the run already made, rather than by generating a new dungeon. [h:35-devlab-4] */
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

  /* THE ROW, and where it goes is the whole argument. [h:35-devlab-5] */
  /* THE THREE BANDS, and they are placed in SCREEN terms and then converted, because that is the only coordinate system in which "visible" means anything. [h:35-devlab-6] */
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

  /* Every item in the roster, laid out as a shelf of alcoves along the top wall, and equipped on entry as well. [h:35-devlab-7] */
  let shelf=[];
  /* TWO ROWS, because thirteen alcoves do not fit in the frame and this is not a close call. [h:35-devlab-8] */
  const SHELF_ROWS=2;
  /* THE SHELF IS A PICKUP, not a picture of one. [h:35-devlab-9] */
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
    /* IN PLACE, never by reassignment. [h:35-devlab-10] */
    for(let i=r.pickups.length-1;i>=0;i--){
      if(r.pickups[i].labShelf!==undefined) r.pickups.splice(i,1);
    }
    for(let i=0;i<shelf.length;i++){
      const s=shelf[i];
      if(s.gone>0) continue;
      r.pickups.push({x:s.x,y:s.y,r:18,kind:'item',id:s.id,labShelf:i});
    }
  }
  /* An alcove that has been emptied comes back, so the rail can be used again. [h:35-devlab-11] */
  function tickShelf(){
    const r=currentRoom();
    if(!r) return;
    const live=new Set();
    for(const pk of r.pickups) if(pk.labShelf!==undefined) live.add(pk.labShelf);
    let changed=false;
    for(let i=0;i<shelf.length;i++){
      const s=shelf[i];
      if(s.gone>0){
        /* Counting down. [h:35-devlab-12] */
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

  /* Spawned in a RING around the player rather than at a point, because a drove spawned on top of you is a frame-cost spike and nothing else - the... [h:35-devlab-13] */
  function drove(){
    const r=currentRoom(), t=droverType();
    const rad=Math.min(300,Math.min(roomW(),roomH())*0.3);
    const cy=Math.max(r.bounds.t+rad+40,Math.min(r.bounds.b-rad-40,player.y+230));
    /* A DROVE OF BRUNCH MUST BE ONE PACK, or it does not behave like the bodies the generator builds. [h:35-devlab-14] */
    const packId=t.type==='brunch'?LAB_PACK_CURSOR++:undefined;
    for(let i=0;i<LAB_DROVE;i++){
      const a=(i/LAB_DROVE)*Math.PI*2+0.4;
      const b=spawnEnemy(false,r,player.x+Math.cos(a)*rad,cy+Math.sin(a)*0.8*rad,t.type);
      r.enemies.push(b);
      if(packId!==undefined){ b.packId=packId; b.packSlot=i; }
      b.labDrove=true;
      /* AWAKE ON ARRIVAL. [h:35-devlab-15] */
      b.noticeTimer=0; b.alerted=true;
    }
  }
  const droverType=()=>LAB_SPECIMENS[dropper];

  /* ------------------------------------------------------------------ enter and leave ------- */

  function enter(){
    /* startGame() first, always. [h:35-devlab-16] */
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

  /* Leaving goes back to the TITLE, not to a run. [h:35-devlab-17] */
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

  /* THE DAMAGE NUMBERS, derived rather than reported. [h:35-devlab-18] */
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

  /* FROZEN means the bodies stop being simulated, not that they are drawn still. [h:35-devlab-19] */
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
    /* F7 DROPS A DOZEN. [h:35-devlab-20] */
    if(k==='f7'){ drove(); return true; }
    /* F6 REFILLS THE SHELF. [h:35-devlab-21] */
    if(k==='f6'){ for(const s of shelf) s.gone=0; syncShelfPickups(); return true; }
    /* THE BENCH'S THREE SHORTCUTS LIVE HERE, because the lab is where they are safe. [h:35-devlab-22] */
    if(k==='g'){ player.hasGold=true; return true; }
    if(k==='s'){ player.hasSilver=true; return true; }
    if(k==='h'){ player.hp=player.maxHp; player.cooldown=0; player.altCooldown=0;
      player.blinkCharges=2; player.blinkRegen=0; return true; }
    /* F5 is NOT a lab key: it reloads the page. It is listed here so the legend does not invite
       anyone to press it, and so the omission reads as a decision rather than a gap. */
    if(k==='f5') return false;
    return false;
  }

  /* ------------------------------------------------------------------ the picture ------------ */

  /* Everything below is drawn in WORLD space, inside the camera transform, so it scrolls with the room - which is the point. [h:35-devlab-23] */

  const engraved=(text,x,y,color,align,font)=>{
    ctx.font=font||'12px monospace'; ctx.textAlign=align||'center';
    // a dark offset under a light face: the game's engraving trick, and the reason the nameplates
    // read as cut into stone rather than printed on top of it
    ctx.fillStyle='rgba(0,0,0,0.75)'; ctx.fillText(text,x+1,y+1);
    ctx.fillStyle=color||'#d8cfae'; ctx.fillText(text,x,y);
    ctx.textAlign='left';
  };

  /* THE SURVEY GRID, and this is the single most functional thing in the lab. [h:35-devlab-24] */
  const MINOR=120, MAJOR=480, gridCache=new Map();
  /* The grid is gone entirely, and the reason it existed is worth keeping. [h:35-devlab-25] */
  const GRID_MINOR_ALPHA=0;      // was 0.055 - a 120px lattice over a 1680px room is a tiled floor
  const GRID_MAJOR_ALPHA=0;      // was 0.13, then 0.055 - see the note; a dungeon room has none
  function gridAlphaFor(step){ return step===MINOR?GRID_MINOR_ALPHA:GRID_MAJOR_ALPHA; }
  /* The two brazier offsets and the flame's half-size, published so the suite can check the flame against the bowl without re-deriving numbers that... [h:35-devlab-26] */
  const BRAZIER_GEOM=()=>({bowlY:BRAZIER_BOWL_Y, flameBaseY:FLAME_BASE_Y, flameBaseRow:FLAME_BASE_ROW,
          flameR:FLAME_R, r:BRAZIER_R, flameFrames:BRAZIER_FRAMES});
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

  /* BRAZIERS on the walls. [h:35-devlab-27] */
  /* BRAZIERS, AND WHY THEY ARE A SPRITE. [h:35-devlab-28] */
  const BRAZIER_R=168, BRAZIER_FRAMES=5, brazierCache=new Map();
  /* HOW FAST THE FIRE BURNS, and it was far too fast to read as fire. [h:35-devlab-29] */
  const BRAZIER_FRAME_TICKS=18;
  /* WHERE THE FLAME GOES, and the sign convention that makes it mean something. [h:35-devlab-30] */
  const BRAZIER_BOWL_Y=21,   // the bowl is this many rows ABOVE the brazier sprite's middle
        FLAME_R=36,          // half-size of the flame sprite
        /* THE FLAME'S BASE ROW, in absolute sprite rows - which is what the drawing needs, and is NOT the same number as the constant flameFrame bakes with. [h:35-devlab-31] */
        FLAME_BASE_ROW=61,
        FLAME_BASE_Y=25;      // the distance from the sprite's middle, for baking the shape
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
  /* FIVE BAKED FLAMES, not a blend. [h:35-devlab-32] */
  const FLAME_LEAN=[0,-1.6,0.9,1.7,-1.1];       // tip offset, px
  const FLAME_WAIST=[1,1.14,0.88,1.05,0.94];    // body width multiplier
  function flameFrame(i){
    const key='f'+i;
    let c=brazierCache.get(key);
    if(c) return c;
    const S=72, o=S/2, fh=19+5*(i/(BRAZIER_FRAMES-1));
    const lean=FLAME_LEAN[i%FLAME_LEAN.length], waist=FLAME_WAIST[i%FLAME_WAIST.length];
    const tip=o+lean, wid=10*waist, wid2=5*waist;
    c=mk(S,S);
    const g=c.getContext('2d');
    g.globalCompositeOperation='lighter';
    g.fillStyle='rgba(255,120,40,0.6)';
    g.beginPath(); g.moveTo(o,o+FLAME_BASE_Y);
    g.quadraticCurveTo(o+wid,o+FLAME_BASE_Y-fh*0.6,tip,o+FLAME_BASE_Y-fh);
    g.quadraticCurveTo(o-wid,o+FLAME_BASE_Y-fh*0.6,o,o+FLAME_BASE_Y); g.fill();
    g.fillStyle='rgba(255,220,150,0.85)';
    g.beginPath(); g.moveTo(o,o+FLAME_BASE_Y);
    g.quadraticCurveTo(o+wid2,o+FLAME_BASE_Y-fh*0.4,tip*0.92+o*0.08,o+FLAME_BASE_Y-fh*0.6);
    g.quadraticCurveTo(o-wid2,o+FLAME_BASE_Y-fh*0.4,o,o+FLAME_BASE_Y); g.fill();
    brazierCache.set(key,c);
    return c;
  }
  function braziers(){
    const b=currentRoom().bounds, spots=[];
    /* FREE-STANDING COLUMNS IN THE ROOM, not on its walls. [h:35-devlab-33] */
    const stepX=300;
    for(let x=b.l+175;x<=b.r-175;x+=stepX)
      for(let y=b.t+175;y<=b.b-175;y+=250) spots.push([x,y]);
    const body=brazierSprite();
    const flame=flameFrame(Math.floor(frameCount/BRAZIER_FRAME_TICKS)%BRAZIER_FRAMES);
    for(const s of spots){
      const x=s[0],y=s[1];
      ctx.save();
      ctx.globalAlpha=0.86+0.14*Math.sin(frameCount*0.21/SPEEDUP);
      ctx.drawImage(body,x-BRAZIER_R,y-BRAZIER_R);
      ctx.globalAlpha=1;
      /* THE FLAME SITS IN THE BOWL, and it used to sit on the floor beside the stand. [h:35-devlab-34] */
      /* The flame's BASE must land on the bowl's centre, which is `spot - BRAZIER_BOWL_Y` (the brazier radius cancels: [h:35-devlab-35] */
    ctx.drawImage(flame, x-FLAME_R, y-FLAME_BASE_ROW-BRAZIER_BOWL_Y);
      ctx.restore();
    }
  }

  /* A SPECIMEN PLINTH: [h:35-devlab-36] */
  /* The shadow under a plinth, baked per radius. [h:35-devlab-37] */
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

  /* THE SHELF: an alcove per item, cut into a stone rail along the top wall, with the item's OWN glyph in the item's OWN colour. [h:35-devlab-38] */
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

  /* Everything, in the order it should stack. [h:35-devlab-39] */
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
  /* THE LEGEND, and it is drawn in SCREEN space, unlike everything above. [h:35-devlab-40] */
  const LEGEND=[
    ['F2','leave the lab'],
    ['F3','freeze / release the row'],
    ['F4','arm the dropper'],
    ['F7','drop a dozen'],
    ['F6','refill the shelf']
  ];
  function drawLegend(){
    if(!on) return;
    /* A HORIZONTAL STRIP ALONG THE BOTTOM, and it was a panel in the bottom corner - which put it on top of the item shelf both times it was tried, once... [h:35-devlab-41] */
    const lane=legendLane();
    const h=lane.h, y=lane.y, x=10;
    /* THE CONTENTS ARE CENTRED IN WHATEVER LANE THERE IS, not pinned to a y that was measured against a 26px strip. [h:35-devlab-42] */
    const chipTop=y+Math.max(2,Math.min(6,Math.round((h-15)/2))), baseline=chipTop+12;
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
      ctx.fillRect(cx,chipTop,kw,15);
      engraved(k[0],cx+kw/2,baseline,'#e8cf94','center','11px monospace');
      engraved(k[1],cx+kw+7,baseline,'rgba(186,194,208,0.92)','left','11px monospace');
      cx+=kw+7+ctx.measureText(k[1]).width+20;
    }
    // the lab's own state, on the right of the same row: frozen or not, and what the dropper is
    // armed with. These are the two things you would otherwise have to remember or guess, and a
    // debug view that makes you guess produces a wrong number and blames the game.
    const t=LAB_SPECIMENS[dropper];
    engraved((frozen?'FROZEN':'LIVE')+'   dropper: '+(t.type==='boss'?'warden':t.type),
             W-x-12,baseline,frozen?'#9fd0ff':'#7fe0a8','right','11px monospace');
    ctx.restore();
  }

  /* THE LEGEND'S LANE, and it is a function of whether a Warden is alive in this room. [h:35-devlab-43] */
  const LEGEND_H=26, LEGEND_MIN_H=20;
  function legendLane(){
    const full=H-LEGEND_H;
    let top=full;
    if(liveBossInRoom()){
      const b=bossBarRect();
      /* MAX, not min. [h:35-devlab-44] */
      /* b.y-3 .. b.y+b.h+3 is the FRAME, because drawBossBar fills the recess at (x-3, y-3, w+6, h+6) - so the frame's bottom edge is b.y+b.h+3 and not... [h:35-devlab-45] */
      top=Math.min(H-LEGEND_MIN_H,Math.max(full,Math.min(H,b.y+b.h+3)));
    }
    /* and the floor is applied to the TOP, not the height: [h:35-devlab-46] */
    const h=Math.max(LEGEND_MIN_H,H-top);
    return {y:Math.min(top,H-h),h};
  }

  const shelfData=()=>shelf;

  return {on:()=>on, frozen:()=>frozen, dropper:()=>dropper, toggle, enter, leave, key,
          build, layRow, layShelf, drove, tickNumbers, freeze, remember, draw, drawNumbers, drawLegend, legendLane,
          shelfData, numbers:()=>numbers, LAB_W, LAB_H, tickShelf, syncShelfPickups, BRAZIER_GEOM,
          gridAlphas:()=>({minor:GRID_MINOR_ALPHA, major:GRID_MAJOR_ALPHA, minorStep:MINOR, majorStep:MAJOR}),
          refill:()=>{ for(const s of shelf) s.gone=0; syncShelfPickups(); }};
})();
