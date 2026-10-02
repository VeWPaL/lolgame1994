/* ==============================================================================================
   70-view  -  everything drawn

   The part Unity replaces wholesale. Read it for BEHAVIOUR, not for structure: what has to be
   legible, what must read in a glance, what the tells are. None of it ports; all of it is
   specification for the replacement.
   ============================================================================================== */
// where a padlock hangs for each side: just outside the frame
function c0(d){ return {N:[MIDX,ROOM_TOP-8],S:[MIDX,ROOM_BOTTOM+8],W:[ROOM_LEFT-8,MIDY],E:[ROOM_RIGHT+8,MIDY]}[d]; }

/* THE GAP IN THE WALL FOR EACH SIDE, as [x,y,w,h].

   One table, three readers: the frame fill, the boss gate, and the unlock sweep. It used to be three
   separate tables written out side by side, and that is the whole reason the boss door was broken in
   two different ways at once.

   The gate is the one that showed. Its copy of the vertical case had the BOTTOM wall's y baked into
   it, so the E and W portcullises were drawn 254px below the doors they were sealing - sitting on the
   floor in the corner of the room - while those doors showed nothing but a bare padlock. The unlock
   sweep had the mirror fault: it always drew a horizontal bar, so on a vertical door it swept across
   the thickness of the wall rather than along the gap it was supposed to be opening.

   Neither was findable by reading the code, and that is the part worth keeping. Two of the three
   tables agreed with each other and the collision code in 40-combat agrees with those two as well, so
   the odd one out was invisible from any single file and only appeared the moment all four sides were
   drawn at once - which a level almost never does, since the boss gate is one door in a layout. Any
   geometry that more than one piece of code needs belongs in exactly one place, and this is what it
   costs to leave it in three. */
function doorRect(d,wt){
  return d==='N'?[MIDX-DOORW/2,ROOM_TOP-wt,DOORW,wt]
       : d==='S'?[MIDX-DOORW/2,ROOM_BOTTOM,DOORW,wt]
       : d==='W'?[ROOM_LEFT-wt,MIDY-DOORW/2,wt,DOORW]
       :         [ROOM_RIGHT,MIDY-DOORW/2,wt,DOORW];
}
/* THE FLOOR, and the cache is keyed by SIZE as well as by type - which it has to be now that a
   room is not one shape.

   It used to be keyed by `type` alone, on the reasoning that there was one room size so the size
   was constant. That reasoning stopped being true the moment bounds went on the room, and the
   failure is quiet rather than loud: a big room asks for its floor, the cache hands back the
   700x450 canvas built for a small one, and it gets stretched to cover 1600x1000. Everything looks
   present. The tile pattern doubles in size, the cave speckles smear, and nobody can say why the
   floor of the biggest room in the game looks like it is wearing a hat two sizes too small.

   So the key is type + width + height, and the radial vignette is sized from the room rather than
   from a literal 420. The vignette is the other half of it: at 420px it was most of the way across
   a 450px-tall room, and on a 1000px-tall room it would be a small bright disc in the middle of a
   large dark floor - the room would read as unlit beyond the disc rather than as a room. Scaling it
   off the diagonal keeps the proportion, which is what was actually being chosen by hand. */
function drawFloor(type){
  const w=roomW(), h=roomH();
  const key=type+':'+w+'x'+h;
  let c=floorCache[key];
  if(!c){
    c=floorCache[key]=document.createElement('canvas');
    c.width=w; c.height=h;
    const g=c.getContext('2d'), cx=c.width/2, cy=c.height/2;
    g.fillStyle=g.createPattern(caveCanvas,'repeat');
    g.fillRect(0,0,c.width,c.height);
    g.globalAlpha=0.34;
    g.fillStyle=ROOM_BG[type]||'#191b22';
    g.fillRect(0,0,c.width,c.height);
    g.globalAlpha=1;
    // 420 was the radius on a 450-tall room, so this recovers the same shape at any size
    const rad=Math.max(160,Math.sqrt(w*w+h*h)*0.47);
    const grad=g.createRadialGradient(cx,cy,Math.min(40,rad*0.1),cx,cy,rad);
    grad.addColorStop(0,'rgba(255,255,255,0.05)');
    grad.addColorStop(1,'rgba(0,0,0,0.32)');
    g.fillStyle=grad;
    g.fillRect(0,0,c.width,c.height);
  }
  ctx.drawImage(c,roomL(),roomT());
}
function drawWand(px,py,a,color,extra){
  const side=Math.cos(a)>=0?1:-1, hx=px+side*11, hy=py+5;
  ctx.save();ctx.translate(hx,hy);ctx.rotate(a);
  ctx.fillStyle='#1a1008';ctx.fillRect(-7,-3,36,6);
  ctx.fillStyle='#8a5a2b';ctx.fillRect(-5,-2,27,4);
  ctx.fillStyle='#b98040';ctx.fillRect(-5,-2,27,1);
  ctx.fillStyle='#5a3618';ctx.fillRect(-5,1,27,1);
  ctx.fillStyle='#3b2a55';ctx.fillRect(-5,-2,9,4);
  ctx.fillStyle='#6a4aa0';ctx.fillRect(-3,-2,1,4);ctx.fillRect(1,-2,1,4);
  ctx.fillStyle='#e8c04a';ctx.fillRect(21,-3,3,6);
  ctx.fillStyle='#1a1008';ctx.fillRect(24,-4,8,8);
  ctx.fillStyle=color;ctx.fillRect(25,-3,6,6);
  ctx.fillStyle='#ffffff';ctx.fillRect(26,-2,2,2);
  ctx.restore();
  const tx=hx+Math.cos(a)*29, ty=hy+Math.sin(a)*29;
  ctx.globalAlpha=Math.min(1,0.35+0.12*Math.sin(frameCount*0.15/SPEEDUP)+extra);
  ctx.drawImage(spellGlow(color,14),tx-14,ty-14);
  ctx.globalAlpha=1;
  const t=frameCount*0.08/SPEEDUP;
  ctx.fillStyle='#ffffff';
  ctx.fillRect(Math.round(tx+Math.cos(t)*9)-1,Math.round(ty+Math.sin(t*1.3)*9)-1,2,2);
  ctx.fillRect(Math.round(tx+Math.cos(t+3)*7)-1,Math.round(ty+Math.sin(t*0.9+2)*7)-1,2,2);
  return [tx,ty];
}
function drawMuzzleFlash(x,y,t){
  if(t<=0)return;
  ctx.globalAlpha=0.85*t/MUZZLE_TICKS;
  ctx.drawImage(MUZZLE_GLOW,x-11,y-11);
  ctx.globalAlpha=1;
}
// the first time the boss shows up on the map you get told what it is, so walking the gold run
// into the boss room is a decision you made and not a door you tripped over
function drawBossWarning(){
  if(bossWarnT<=0) return;
  const t=1-bossWarnT/BOSS_WARN_TIME;
  const a=Math.min(1,t*7)*Math.min(1,(1-t)*5);
  const y=ROOM_TOP+34;
  ctx.globalAlpha=a;
  ctx.fillStyle='rgba(12,7,7,0.9)';
  ctx.fillRect(W/2-150,y,300,30);
  ctx.strokeStyle='rgba(255,210,61,0.8)';ctx.lineWidth=1;
  ctx.strokeRect(W/2-149.5,y+0.5,299,29);
  ctx.fillStyle='#ffd23d';ctx.textAlign='center';ctx.font='bold 15px monospace';
  ctx.fillText('SOMETHING HEAVY IS DOWN THERE',W/2,y+20);
  ctx.textAlign='left';
  ctx.globalAlpha=1;
}

function drawFloorField(){
  // The hook's ground spell. Built from the same parts as the wand tip - a radial glow and a few
  // small square motes - so it reads as the same magic, but the motes spiral INWARD instead of
  // orbiting, which is what makes it look like something is draining the room rather than
  // decorating it. It sits on the floor, under everything, and fades out over its last third so its
  // end is legible before it stops holding.
  for(const f of hookFields){
    const left=f.life/f.max;
    const fade=left<0.34?left/0.34:1;
    ctx.save();
    ctx.globalAlpha=0.13*fade;
    ctx.fillStyle=HOOK_WEAPON.color;
    ctx.beginPath();ctx.arc(f.x,f.y,f.r,0,7);ctx.fill();
    ctx.globalAlpha=0.5*fade;
    ctx.strokeStyle=HOOK_WEAPON.color;ctx.lineWidth=2;
    ctx.beginPath();ctx.arc(f.x,f.y,f.r*(0.55+0.45*(1-left)),0,7);ctx.stroke();
    ctx.globalAlpha=0.3*fade;
    ctx.lineWidth=1;
    ctx.beginPath();ctx.arc(f.x,f.y,f.r*0.55,0,7);ctx.stroke();
    // the inward spiral. each mote rides its own radius and phase, drifting down toward the middle
    const t=frameCount*0.05/SPEEDUP;
    ctx.fillStyle='#e8fffb';
    for(let i=0;i<7;i++){
      const spin=t*(1.5+(i%3)*0.35)+i*0.9;
      const rad=f.r*(0.5+0.48*((1-(t*0.22+i*0.14)%1)));
      const mx=f.x+Math.cos(spin)*rad, my=f.y+Math.sin(spin)*rad*0.86;
      ctx.globalAlpha=(0.75-0.35*((i*7)%5)/5)*fade;
      ctx.fillRect(Math.round(mx)-1,Math.round(my)-1,2,2);
    }
    ctx.restore();
  }
}
function drawRoom(){
  const r=currentRoom();
  drawFloor(r.type);
  drawFloorField();
  /* The lab's furniture goes over the floor and under the bodies: the grid and the braziers are
     marks ON the floor, the shelf is a rail on the wall, and a plinth is the thing a body stands
     ON - drawn over its own specimen it would hide the thing you came to look at. The damage
     readout is the other half and goes the other way, over everything; see Lab.drawNumbers. */
  Lab.draw();

  const wt=16;
  ctx.fillStyle='#2b2f3a';
  ctx.fillRect(ROOM_LEFT-wt,ROOM_TOP-wt,ROOM_RIGHT-ROOM_LEFT+wt*2,wt);
  ctx.fillRect(ROOM_LEFT-wt,ROOM_BOTTOM,ROOM_RIGHT-ROOM_LEFT+wt*2,wt);
  ctx.fillRect(ROOM_LEFT-wt,ROOM_TOP-wt,wt,ROOM_BOTTOM-ROOM_TOP+wt*2);
  ctx.fillRect(ROOM_RIGHT,ROOM_TOP-wt,wt,ROOM_BOTTOM-ROOM_TOP+wt*2);
  const bg=ROOM_BG[r.type];
  const dcolFor=d=>!doorOpen(r)?'#c93b3b':leadsToBoss(r,d)&&!bossUnlocked?'#ffd23d':leadsToItem(r,d)&&!itemUnlocked?'#d8dee9':'#5ee27a';
  for(const d of ['N','S','W','E']){
    if(!r.doors[d]) continue;
    const g=doorRect(d,wt);
    ctx.fillStyle=bg; ctx.fillRect(g[0],g[1],g[2],g[3]);
    // the coloured lip sits on the OUTER edge of the wall - the side away from the room - so the door
    // reads as a seam in the masonry rather than a panel stuck on the inside of it
    ctx.fillStyle=dcolFor(d);
    if(d==='N'||d==='S') ctx.fillRect(g[0],d==='N'?g[1]:g[1]+g[3]-4,g[2],4);
    else                   ctx.fillRect(d==='W'?g[0]:g[0]+g[2]-4,g[1],4,g[3]);
  }

  /* the boss gate. rather than a glow pasted over the frame, it is a portcullis of slats across
     the gap: the shutter breathes while it is shut, and it visibly lifts as the lock works, so you
     can read what state the door is in from across the room */
  for(const d of ['N','S','E','W']){
    if(!r.doors[d]||!leadsToBoss(r,d)||bossUnlocked) continue;
    const horiz=d==='N'||d==='S', p=doorPoint(d);
    // ONE source of truth for where the gap is. This used to rebuild the rect here with the bottom
    // wall's y baked into the vertical case, which put the E and W portcullises 254px below the doors
    // they seal. doorRect is the same table the door frame itself is drawn from, so the gate cannot
    // disagree with the doorway even if the geometry changes.
    const gate=doorRect(d,wt);
    const prog=unlocking(r,d)?unlockT/UNLOCK_TIME:0;
    const breathe=0.5+0.5*Math.sin(frameCount*0.07/SPEEDUP);
    const lift=prog*0.5;                                    // the slats pull back as it throws
    ctx.fillStyle='rgba(20,16,8,'+(0.85-0.25*breathe)+')';
    ctx.fillRect(gate[0],gate[1],gate[2],gate[3]);
    ctx.fillStyle='rgba(255,210,61,'+(0.55+0.35*breathe)+')';
    const slats=5;
    for(let i=0;i<slats;i++){
      const f=i/(slats-1), gap=lift*DOORW*0.5;
      if(horiz){
        const sx=gate[0]+4+f*(gate[2]-8)+gap;
        ctx.fillRect(sx,gate[1]+3,3,gate[3]-6);
      }else{
        const sy=gate[1]+4+f*(gate[3]-8)+gap;
        ctx.fillRect(gate[0]+3,sy,gate[2]-6,3);
      }
    }
    // a soft line of light in the gap, dimming as the lock lets go
    ctx.globalAlpha=0.20+0.14*breathe-prog*0.3;
    ctx.fillStyle='#ffd23d';
    ctx.fillRect(gate[0],gate[1],gate[2],gate[3]);
    ctx.globalAlpha=1;
  }

  // a fake wall is solid wall, but not quite: a few hairline cracks are the only tell, and the
  // right click is the only thing that opens it
  if(r.secret){
    const horiz=r.secret==='N'||r.secret==='S', p=doorPoint(r.secret);
    ctx.fillStyle='rgba(164,172,188,0.24)';
    for(let i=0;i<3;i++){
      if(horiz) ctx.fillRect(p[0]-8+i*6,p[1]-(r.secret==='N'?8:-4),1,6);
      else ctx.fillRect(p[0]-(r.secret==='W'?8:-4),p[1]-8+i*6,6,1);
    }
  }
  // a padlock sits on any door that is still shut behind a key, tinted to the key that opens it.
  // holding the key does not take the padlock off: it starts the unlock, and the shackle lifts as
  // the lock works, so the door is something you watch open rather than a door that quietly works
  for(const d of ['N','S','E','W']){
    if(!r.doors[d]||!doorSealed(r,d)) continue;
    const col=leadsToBoss(r,d)?'#ffd23d':'#d8dee9';
    const prog=unlocking(r,d)?unlockT/UNLOCK_TIME:0;
    if(prog>0){
      ctx.globalAlpha=0.3+0.4*prog;
      /* The sweep runs ALONG the gap, not across the wall.

         It used to be one unconditional horizontal bar, which is correct on a north or south door
         and wrong on an east or west one: there it drew a 90px-wide band through the thickness of
         the masonry while the gap it was supposed to be opening ran vertically beside it. The tell
         for "this door is opening" therefore pointed the wrong way on half the doors in the game,
         and the fix is the same one as the gate's - ask doorRect where the gap is rather than
         assuming which way up it happens to be. */
      const g=doorRect(d,wt);
      ctx.fillStyle=col;
      if(g[2]>g[3]) ctx.fillRect(g[0],g[1],g[2],8);        // a horizontal gap: a band across its width
      else          ctx.fillRect(g[0],g[1],8,g[3]);        // a vertical gap: a band down its length
      ctx.globalAlpha=1;
    }
    const lift=prog*14;   // the shackle lifts off as the lock throws
    ctx.globalAlpha=hasKeyFor(r,d)?0.55+0.45*prog:1;
    ctx.fillStyle=col;
    ctx.fillRect(c0(d)[0]-6,c0(d)[1]-1-lift,12,8);
    ctx.strokeStyle=col;ctx.lineWidth=2;ctx.strokeRect(c0(d)[0]-3,c0(d)[1]-6-lift,6,6);
    ctx.fillStyle='#14151a';ctx.fillRect(c0(d)[0]-1,c0(d)[1]+2-lift,2,3);
    ctx.globalAlpha=1;
  }
  for(const f of dashFX){
    if(f.charge){
      // the charge puff: it swells as the windup runs out, so the tell carries a CLOCK and not just
      // a state. drawn under the body so it never hides the thing you have to be able to read
      const grow=1-f.life/LUNGE_CHARGE_TRAIL;
      const rr=6+7*grow;
      ctx.globalAlpha=0.16+0.34*grow;
      ctx.drawImage(DASH_GLOW,f.x-rr,f.y-rr,rr*2,rr*2);
      ctx.globalAlpha=1;
      continue;
    }
    /* The player's own blink, tinted by the meter it was spent at. `f.mine` is the tag from
       doBlink; everything else in this array belongs to a body and keeps the white glow it always
       had. A green puff on a lunger's windup would be telling the player about THEIR meter in the
       middle of reading an ENEMY's, which is the one place a colour cue must not lie. */
    const glow=(f.mine&&f.mom!==undefined)?momentumGlow(f.mom):DASH_GLOW;
    ctx.globalAlpha=0.35*f.life/DASH_TRAIL;
    ctx.drawImage(glow,f.x-14,f.y-14);
    ctx.globalAlpha=1;
  }
  // The committed line. A lunge is a straight shot at a point, so the point gets drawn: from where
  // the lunger was when it planted, to where it is going. Without it the player has to infer the
  // direction from a glow, and with three lungers in a room, inferring which one aimed where is
  // exactly the bookkeeping that makes a fight feel like bookkeeping instead of a fight.
  for(const e of r.enemies){
    if(e.lungeState!=='wind'&&e.lungeState!=='lunge') continue;
    const grow=e.lungeState==='wind'?1-e.lungeT/LUNGE_WINDUP:1;
    const len=e.lungeLen||LUNGE_FLOOR;
    const ex=e.lungeState==='wind'?e.x:e.lungeFromX, ey=e.lungeState==='wind'?e.y:e.lungeFromY;
    const tx=e.lungeState==='wind'?e.x+e.lungeDx*len:e.x, ty=e.lungeState==='wind'?e.y+e.lungeDy*len:e.y;
    ctx.save();
    ctx.globalAlpha=0.20+0.55*grow;
    ctx.strokeStyle='#ff9a6b'; ctx.lineWidth=1+2*grow;
    ctx.setLineDash([5,5]);
    ctx.beginPath();ctx.moveTo(ex,ey);ctx.lineTo(tx,ty);ctx.stroke();
    ctx.setLineDash([]);
    // the landing mark, so "how far" is as legible as "which way"
    ctx.globalAlpha=0.30+0.50*grow;
    ctx.beginPath();ctx.arc(tx,ty,5+3*grow,0,7);ctx.stroke();
    ctx.restore();
  }

  for(const pk of r.pickups){
    if(pk.kind==='exit') drawExitPortal(pk.x,pk.y,frameCount);
    else if(pk.kind==='weapon') drawIcon(pk.w,pk.x,pk.y+Math.sin(frameCount*0.08/SPEEDUP+pk.x)*2);
    else if(pk.kind==='hook'||pk.kind==='blast') drawIcon(pk.kind,pk.x,pk.y+Math.sin(frameCount*0.08/SPEEDUP+pk.x)*2);
    else if(pk.kind==='item') drawItemIcon(pk.id,pk.x,pk.y+Math.sin(frameCount*0.09/SPEEDUP+pk.x)*2,26);
    else if(KEY_SKIN[pk.kind]) drawSprite(KEY_ROWS,KEY_SKIN[pk.kind],pk.x,pk.y+Math.sin(frameCount*0.1/SPEEDUP)*2,3,null,pk.kind+'3');
    else drawHeart(pk.x,pk.y,1,pk.kind==='heart'?'red':'gray',2);
  }

  const readyProg=readyT>0?1-readyT/READY:1;
  for(const e of r.enemies){
    const fr=e.anim>0?Math.floor(e.anim)%4:-1, frame=fr<0?1:fr;
    const bob=(fr===1||fr===3)?-2:0;
    const hop=readyT>0?-Math.abs(Math.sin(readyProg*Math.PI*2))*6:0;
    const boss=e.type==='boss', shooter=e.type==='shooter'||e.type==='gunner';
    ctx.fillStyle='rgba(0,0,0,0.3)';ctx.beginPath();ctx.ellipse(e.x,e.y+e.r*0.8,e.r*0.9,e.r*0.3,0,0,7);ctx.fill();
    const frames=shooter?SHOOTER_FRAMES:LUNGER_FRAMES;
    const pal=e.type==='gunner'?GUNNER_PAL:e.type==='brunch'?BRUNCH_PAL:shooter?SHOOTER_PAL:boss?BOSS_PAL:LUNGER_PAL;
    const ex=e.x, ey=e.y-(boss?6:2)+bob*(boss?2:1)+hop;
    drawSprite(frames[frame],pal,ex,ey,e.art,null,e.type+e.art+frame);
    // a hit lays a translucent white silhouette over the body instead of replacing it, so a body
    // under sustained fire (the Beam is a hit every fifth of a second) still reads as a creature
    if(e.hitFlash>0){
      const k=HIT_FLASH_MIN+(HIT_FLASH_MAX-HIT_FLASH_MIN)*Math.min(1,e.hitFlash/HIT_FLASH);
      ctx.globalAlpha=k;
      drawSprite(frames[frame],pal,ex,ey,e.art,'#ffffff',e.type+e.art+frame+'f');
      ctx.globalAlpha=1;
    }
    /* The floating bar over a body - every body EXCEPT the boss.

       The boss used to get one of these too, on top of the fixed bar at the bottom of the screen, and
       two health bars for the same health is one too many: they disagree the instant they are both
       on screen (the floating one is 4px tall and rounded to whole pixels, the fixed one is 10px and
       notched), and the player has to decide which one to believe. It is redundant with the player
       having their own bar as well - the fight has two health bars and one of them is the boss.

       So the boss's health is drawn once, in one place, by drawBossBar. Every other body keeps its
       floating sliver: at 4px over a 14px body it is a glance, not a reading, and that is the right
       amount of attention for a Brunch. */
    if(e.type!=='boss'){
      const barY=e.y-e.bar;
      ctx.fillStyle='#000';ctx.fillRect(e.x-e.r,barY,e.r*2,4);
      ctx.fillStyle='#5ee27a';ctx.fillRect(e.x-e.r,barY,e.r*2*(e.hp/e.maxHp),4);
    }
    // Stars over a held body. The hook's entire value is that it holds a knot on the floor for a
    // second and a half, and until now nothing said "this one is stuck" - the walk simply stopped,
    // which is also what a body at the edge of its aggro does. Three little stars orbiting the head
    // read as stunned from across the room, and they are the cue that a lunger caught mid-charge
    // has lost that charge, so the glow going out is a reward rather than a glitch.
    if(e.stun>0) drawStunStars(e.x,ey-e.r-6,e.stun,frameCount);
    // The cast tell, drawn with the BODY and not with the projectiles. It was originally drawn in
    // the projectile loop, which meant it was never drawn at all - a gunner is not a projectile, so
    // the condition could never be true, and the tell existed only in the timing. A mechanic nobody
    // can see is not a mechanic.
    // anchored to where the gunner IS, not to where the charge began: a gunner covers about thirty
    // pixels in half a second, so a flash pinned to the muzzle at the start of the cast spends the
    // whole of it trailing thirty pixels behind the body making it
    if(e.castT>0) drawCastFlash(e.x,e.y,e.castT,CAST_TIME,e.pcol);
  }
  /* The lab's damage readout, over the bodies and over the projectiles. It is here rather than with
     the plinths because a number is the one thing in the lab that must never be behind something:
     it is the thing you are reading, and in a room this size the body that just got hit is often
     behind another body that did not. */
  Lab.drawNumbers();
  for(const p of projectiles){
    // the ring marks where the bolt will actually go off, so it has to be the radius of the weapon
    // in flight - otherwise the hook's circle would understate the ground it covers
    if(p.alt){const m=p.phase?HOOK_WEAPON:ALT_WEAPON;
      ctx.strokeStyle=p.color+'48';ctx.lineWidth=2;ctx.beginPath();ctx.arc(p.tx,p.ty,m.aoeRadius,0,7);ctx.stroke();ctx.globalAlpha=0.6;}
    if(p.friendly){
      // A shrinking projectile is the gun telling you what it is worth before it lands. The size is
      // read off the SAME falloff number the damage will be, so the picture and the number can never
      // disagree - and because fMin is a floor, a bolt at the far end of the room is small and nearly
      // harmless rather than small and still lethal, which is the honest way round.
      const dr=drawR(p);
      // the halo shrinks with the bolt AND dims, because size alone is a weak tell on a light floor
      // where a small bright disc is still perfectly readable. Two channels at once is what makes it
      // something you notice while aiming rather than something you notice while dying.
      const fade=drawFade(p);
      const R=Math.max(2,Math.round(dr*3.2));
      ctx.globalAlpha=fade;
      ctx.drawImage(spellGlow(p.color,R),p.x-R,p.y-R);
      ctx.fillStyle=p.color;ctx.beginPath();ctx.arc(p.x,p.y,dr,0,7);ctx.fill();
      if(p.shrink){
        // a thin rim that thins as the bolt does, so a spent one reads as a spent one even against
        // a bright floor
        ctx.globalAlpha=0.5*fade;
        ctx.strokeStyle=p.color;ctx.lineWidth=1;
        ctx.beginPath();ctx.arc(p.x,p.y,dr+2.5,0,7);ctx.stroke();
      }
    } else {
      // a heavy shell gets a bright core and a ring, so the gunner's shot is identifiable in the
      // middle of a fight without reading its colour. Purely visual - see the shot spawn.
      if(p.heavy){
        const R=Math.round(p.r*4);
        ctx.globalAlpha=0.8;
        ctx.drawImage(spellGlow(p.color,R),p.x-R,p.y-R);
        ctx.globalAlpha=1;
        ctx.fillStyle='#fff4e0';ctx.beginPath();ctx.arc(p.x,p.y,p.r*0.55,0,7);ctx.fill();
      }
      ctx.fillStyle=p.color;ctx.beginPath();ctx.arc(p.x,p.y,p.r,0,7);ctx.fill();
    }
    ctx.globalAlpha=1;
  }
  for(const f of burstFX){
    const t=1-f.life/BURST_TICKS;
    // dir -1 collapses from the rim to the point, +1 expands from the point to the rim
    const rad=f.r*(f.dir<0?1-t:t);
    ctx.strokeStyle=f.color||'#ff9640';
    ctx.globalAlpha=0.7*(1-t*0.55);
    ctx.lineWidth=3;
    ctx.beginPath();ctx.arc(f.x,f.y,Math.max(1,rad),0,7);ctx.stroke();
    ctx.globalAlpha=1;
  }

  // the wand points at the cursor, in world space like everything else it is drawn among - see
  // mouseWorld. Drawn in the wrong frame it leaned a few degrees off the shot it was announcing,
  // which is worse than a drawing error: the tell and the thing it was telling you about disagreed
  const aimPt=mouseWorld();
  const a=Math.atan2(aimPt.y-player.y,aimPt.x-player.x);
  const fr=player.anim>0?Math.floor(player.anim)%4:-1, frame=fr<0?1:fr;
  const bob=fr<0?Math.round(Math.sin(frameCount*0.07/SPEEDUP)):((fr&1)?-2:0);
  const hop=readyT>0?-Math.sin(readyProg*Math.PI)*7:0;
  const flick=player.iframes>0&&((player.iframes/IFRAME_FLICKER)|0)%2===0;
  const oy=player.y+bob+hop, wandColor=WEAPONS[player.weaponIdx].color, behind=Math.sin(a)<-0.35;
  // the trailing hitbox, drawn faintly so the post-blink lag is visible while it is tuned
  const lagD=Math.hypot(player.lagX-player.x,player.lagY-player.y);
  if(lagD>3){
    const fade=Math.min(1,lagD/BLINK_DIST);
    ctx.globalAlpha=0.5*fade;
    // the ghost ring is the real hitbox, drawn where the real hitbox is. it used to be a hardcoded 13
    // at the origin, which is a third thing again once the hitbox moved
    ctx.strokeStyle='#66d9ff';ctx.lineWidth=1;ctx.beginPath();ctx.arc(player.lagX,player.lagY+PLAYER_HIT_DY,PLAYER_HIT_R,0,7);ctx.stroke();
    ctx.globalAlpha=0.18*fade;
    drawSprite(PLAYER_FRAMES[frame],PLAYER_PAL,player.lagX,player.lagY+bob+hop-4,2,null,'player'+frame);
    ctx.globalAlpha=1;
  }
  ctx.fillStyle='rgba(0,0,0,0.3)';ctx.beginPath();ctx.ellipse(player.x,player.y+13,11,4,0,0,7);ctx.fill();
  ctx.globalAlpha=flick?0.35:1;
  const glowExtra=readyT>0?0.5*readyProg:0;
  let tip;
  if(behind) tip=drawWand(player.x,oy,a,wandColor,glowExtra);
  drawSprite(PLAYER_FRAMES[frame],PLAYER_PAL,player.x,oy-4,2,null,'player'+frame);
  if(!behind) tip=drawWand(player.x,oy,a,wandColor,glowExtra);
  ctx.globalAlpha=1;
  drawMuzzleFlash(tip[0],tip[1],player.muzzleTimer);
  if(showSpawn) drawSpawnPlan();
}
/* spawn debug: where the plan dropped everyone and which way the gunners were pointing when the
   player walked in. this is the readout for tuning the spacing before there is any cover */
function drawSpawnPlan(){
  const r=currentRoom();
  if(!r.spawnPlan) return;
  const [ex,ey]=entryPoint(entryDir);
  ctx.strokeStyle='rgba(255,210,61,0.9)';ctx.lineWidth=1;
  ctx.beginPath();ctx.arc(ex,ey,11,0,7);ctx.stroke();
  ctx.beginPath();ctx.moveTo(ex-16,ey);ctx.lineTo(ex+16,ey);ctx.moveTo(ex,ey-16);ctx.lineTo(ex,ey+16);ctx.stroke();
  ctx.font='11px monospace';ctx.fillStyle='rgba(255,210,61,0.9)';
  ctx.fillText('entry '+entryDir,ex+18,ey+4);
  /* THE MARK IS DRAWN AT THE PLAN'S OWN POSITION, and the line goes to whatever body is standing there -
       NOT to `enemies[i]`.

       This paired `spawnPlan[i]` with `enemies[i]`, which is wrong in two independent ways:

       - a Brunch PACK is one entry in the plan and SEVERAL bodies in the room, so every body after the
         first is annotated with the plan line of some other spawn entirely;
       - dead bodies leave `enemies`, so the indices slide and every mark after the first kill points at
         the wrong body.

       Both produce the same result: a tuning overlay that confidently annotates the wrong enemy. It is
       debug-only, which is why it survived - but an overlay you cannot trust is worse than none, because
       it gets read as evidence about spacing. So the mark is where the plan says it is, the line goes to
       whatever actually occupies that spot, and a planned spawn with nobody on it says so rather than
       borrowing its neighbour's annotation. */
  r.spawnPlan.forEach((s,i)=>{
    const here=r.enemies.find(e=>Math.abs(e.x-s.x)<14&&Math.abs(e.y-s.y)<14);
    ctx.strokeStyle=here&&here.alerted?'#ff6b6b':'rgba(94,226,122,0.8)';
    ctx.beginPath();ctx.arc(s.x,s.y,here?here.r:12,0,7);ctx.stroke();
    if(here&&here.type!=='lunger'&&here.type!=='boss'){
      ctx.strokeStyle='rgba(255,106,106,0.28)';
      ctx.beginPath();ctx.moveTo(here.x,here.y);ctx.lineTo(s.x,s.y);ctx.stroke();
      ctx.fillStyle='rgba(255,106,106,0.9)';
      ctx.fillText(here.type+' '+Math.round(s.d)+'px',s.x+8,s.y-8);
    } else if(!here){
      ctx.fillStyle='rgba(150,150,150,0.7)';
      ctx.fillText('empty',s.x+8,s.y-8);
    }
  });
}

/* THE ACTIVE PLATE. Not a weapon plate with different contents, because it is answering a different
   question: the weapon plates show a cooldown filling, and this one shows how many presses are LEFT,
   which for a stackable consumable is the number that decides whether to spend it now. So the count
   is drawn large in the corner rather than as a filling bar, and an item that never runs out shows a
   mark instead of a number, because "x0" would be a lie about an item that is always available.

   The empty state keeps the dotted outline `drawSlot` uses for a reserved plate, for the reason in
   that function: a frame you can see is a slot, and a gap is a mistake. The Q is drawn on the plate
   rather than only in the controls sheet, because this is the one key that acts on the thing in this
   frame and a player should not have to remember which frame it was. */
/* The width of the label row, written by drawHUD each frame and read by the suite. Zero until the
   HUD has drawn once, which is why the test draws before it measures rather than trusting this. */
let BENCH_ROW_W=0;

/* ONE LABEL, SHRUNK TO FIT THE WIDTH IT IS GIVEN, and only truncated as a last resort.

   This replaced a three-label row fitter, and the reason is worth keeping because both earlier
   versions of that fitter looked correct and were not.

   The first fitted each name into the 62px plate under it. Eleven of the thirteen item names are
   wider than that, and "Arcane Beam" was 73px, so the labels overlapped.

   The second fixed the overlap by shrinking and truncating each label against a budget derived from
   its neighbours, and a screenshot of it read:

       ARCAN...   UNTER'S ...   BLAST

   which is worse than the overlap, because a stub in the active-item slot removes the one piece of
   information the player cannot get anywhere else. Shrinking to 9-10px is the same failure wearing
   different clothes: a row of 10px type is unreadable in exactly the situation it matters.

   The third fitted all three as one line of text, which is correct arithmetic and still wrong: the
   row is 147px, and the three names at 12px come to 210px in the worst case. It also carried a test
   that asserted against a hardcoded row width of 190, so it passed while checking a geometry the game
   does not have - which is the failure mode that let all three versions look right.

   The row now belongs to the item name alone, so this fits ONE string, and the honest claim is simply
   that every name in the roster fits whole at full size. It does: the longest, "Lantern Friend", is
   92px in a 139px budget.

   Truncation remains for a future name that does not fit, and it is built from a prefix and
   re-measured each step. The first version dropped the last character and re-appended '...', which is
   an INFINITE LOOP - the string never gets shorter - and it was found by a mutation that stopped the
   suite responding rather than reporting a failure. A freeze is the worst defect this function could
   have, and no current name reaches that branch, so it would have shipped.

   Returns the text, the size it settled on, its width, and whether it had to cut, so a test can
   assert the last one rather than trusting that it never happens. Sets ctx.font and ctx.textAlign and
   leaves them set, as the drawing code already assumes. */
function fitLabel(text,maxW){
  const base=12, floor=9;
  ctx.textAlign='center';
  const measure=(n,s)=>{ ctx.font=s+'px monospace'; return ctx.measureText(n).width; };
  let size=base, w=measure(text,size);
  while(size>floor && w>maxW){ size--; w=measure(text,size); }  // re-measure every step
  let shown=text;
  if(w>maxW){
    let cut=shown.length;
    while(cut>0 && measure(shown.slice(0,cut)+'...',size)>maxW) cut--;
    shown=cut>0?shown.slice(0,cut)+'...':'...';
    w=measure(shown,size);
  }
  ctx.font=size+'px monospace';
  return {text:shown, size:size, w:Math.round(w), truncated:shown!==text};
}

function drawActivePlate(x,y,w,held){
  const h=50;
  ctx.drawImage(woodPlate(w,h),x,y);
  drawInset(x+6,y+6,w-12,h-12,'#1a1210');
  if(!held){
    ctx.save();
    ctx.globalAlpha=0.34; ctx.strokeStyle='#6b5a44'; ctx.lineWidth=1;
    ctx.setLineDash([3,4]);
    const q=w/2-9;
    ctx.strokeRect(x+w/2-q,y+h/2-q,q*2,q*2);
    ctx.restore();
    return;
  }
  const def=Content.get('item',held.id);
  /* A CAPTION STRIP, because the first version drew Q and the count straight onto the tile and they
     landed on its bottom corners - a 34px tile in a 50px plate leaves no room for a second row, and
     "Q" over the tile's lower-left bevel reads as part of the item's own artwork. The neighbours get
     to own their whole plate too: theirs fill a background, this gets a strip at the foot and the tile
     sits above it. The tile is 26 rather than 34 because it is a FILLED square and the weapon icons are
     sparse sprites - at equal nominal size the square carries more mass, not less, so this reads level
     with its neighbours rather than smaller. */
  ctx.fillStyle='rgba(8,4,2,0.74)';
  ctx.fillRect(x+7,y+h-20,w-14,14);
  drawItemIcon(held.id,x+w/2,y+17,26);
  // the count, bottom-right, in the item's own colour so it reads as belonging to it
  ctx.textBaseline='alphabetic';
  ctx.font='bold 12px monospace';
  if(held.charges===Infinity){
    ctx.textAlign='right';
    ctx.fillStyle='#5fa8a0';
    ctx.fillText('x',x+w-9,y+h-9);
  }else{
    ctx.textAlign='right';
    ctx.fillStyle=held.charges>0?(def.color||'#e8dcc0'):'#c85a5a';
    ctx.fillText('x'+held.charges,x+w-9,y+h-9);
  }
  // the key, bottom-left, dim once there is nothing to press
  ctx.textAlign='left';
  ctx.fillStyle=held.charges>0?'#e8dcc0':'#6b5a44';
  ctx.fillText('Q',x+9,y+h-9);
  ctx.textAlign='center';
}

function drawSlot(x,y,w,idx,side,color,ready){
  const h=50;
  ctx.drawImage(woodPlate(w,h),x,y);
  drawInset(x+6,y+6,w-12,h-12,'#1a1210');
  if(idx==='empty'){
    // the reserved consumable slot. A dotted outline and nothing in it: it has to read as "there
    // is a thing here and you do not have it" rather than as a weapon you failed to pick up, which
    // is what a dark plate with no glyph reads as today
    ctx.save();
    ctx.globalAlpha=0.34; ctx.strokeStyle='#6b5a44'; ctx.lineWidth=1;
    ctx.setLineDash([3,4]);
    const q=w/2-9;
    ctx.strokeRect(x+w/2-q,y+h/2-q,q*2,q*2);
    ctx.restore();
    return;
  }
  drawIcon(idx,x+w/2,y+h/2,ICON_IN_SLOT);
  if(ready<1){ctx.fillStyle='rgba(8,4,2,0.62)';ctx.fillRect(x+8,y+8,w-16,(h-16)*(1-Math.max(0,ready)));}
  const pal={m:'#7d838f',d:'#20222b',L:side==='left'?color:'#4a4f5a',R:side==='right'?color:'#4a4f5a'};
  drawSprite(MOUSE_ROWS,pal,x+w-5,y+h-5,2,null,'mouse'+side+color);
}


/* THE HUD LAYOUT TABLE, hoisted out of drawHUD so the tests read these numbers instead of copying
   them.

   They were function-locals, and the suite re-declared the same nine integers to assert against -
   on the stated ground that it wanted to test "the HUD matches a stated layout" rather than "the HUD
   matches itself". The reasoning is not wrong and the consequence is worse: `ok(MARGIN_X!==MARGIN_Y)`
   compared the test's own two copies, so it could not fail whatever the game did, and a real change to
   a margin would fail nine assertions that all needed editing by hand.

   This is the boss-door bug and the pulse bug and the drift bug, in its purest form: the same geometry
   living in two places, and the copy is the one under test. One table, read by both, and the
   comparison becomes a real one - if the margins are ever collapsed back into a single number, the
   assertion now fires because it is reading the number the drawing is reading. */
const HUD_MARGIN_X=15, HUD_MARGIN_Y=14, HUD_FRAME=6, HUD_GAP=1;
const HUD_HP_H=40, HUD_ROW_H=30, HUD_KEY_W=62, HUD_KEY_H=34, HUD_BLINK_W=150, HUD_DEPTH_W=104;
const HUD_MOMENTUM_W=208;

/* WHERE THE BAR GOES, worked out from the room being drawn right now.

   A function rather than a constant, because there is more than one room: the Lab is 1680x760 and the
   floors are 700x450. The first version baked the rectangle out of ROOM_W and ROOM_TOP at load time,
   so a bar sized 660px wide for a 700px room was being drawn 660px wide inside a 1680px one, at a y
   captured before the Lab changed the room. A constant that describes a room is wrong the moment
   there is more than one room, and the Lab exists precisely to prove those are not the same thing. */
function bossBarRect(){
  const bottom=canvas.height-BOSS_BAR_MARGIN;
  return {x:ROOM_LEFT+BOSS_BAR_INSET_X,
          y:bottom-BOSS_BAR_H,
          w:ROOM_RIGHT-ROOM_LEFT-BOSS_BAR_INSET_X*2,
          h:BOSS_BAR_H};
}

/* THE WARDEN'S HEALTH, as the one piece of the fight that is fixed to the screen.

   The boss already had a health bar - a 56px sliver, twice a regular body's, floating above a body
   that walks around. It is not nothing, and this is not a claim that the boss had no bar at all; it
   is that a bar the player has to find, that moves with its owner, that carries no phase markers and
   no name, does not do the four jobs this one has to do:

     1. SHOW WHAT IS LEFT. A floating sliver answers "did that hit land". This answers "how much of
        this is there", which is the question a player is actually asking at 66% health.
     2. MARK THE PHASES. BOSS_PHASE_1 and BOSS_PHASE_2 are fractions of max HP at 0.66 and 0.33, and
        crossing them changes the move mix from 3 moves to 4 to 7 - the wall only appears in phase 2,
        and phase 3 is two thirds sweep. The fight visibly changes and NOTHING said so. A bar with the
        two thresholds drawn on it turns that from a thing that happens to you into a thing you can
        see coming, which is the whole fairness argument this boss was built on.
     3. NOT MOVE. Everything else in a fight is moving. A fixed thing is the thing you glance at
        between glances at the boss.
     4. NAME IT. The Warden had no name field - it was called THE WARDEN in a dozen comments and
        never on screen.

   Deliberately NOT a second visual language: the same green as every body bar, because a boss bar in
   a different colour reads as a different kind of object and this is the same quantity at a size you
   can read. What changes is the size, the fixed position, and the two notches.

   The fill is drawn from the LEFT edge so the bar empties toward the door the player came in by,
   which is where they are heading and where the fight will resume. It is not drawn as a shrinking
   width from both ends, which would keep the centre of mass fixed and make the change hardest to see.

   The phase notches are 2px of bone-white standing PROUD of the bar rather than cut into it, because
   a notch inside the bar disappears as the fill passes over it, and these need to stay visible after
   they have been crossed - at that point they are the record of how far the fight has already come. */
function drawBossBar(e){
  const rect=bossBarRect(), x=rect.x, w=rect.w, y=rect.y, h=rect.h;
  const frac=Math.max(0,Math.min(1,e.hp/e.maxHp));
  // the frame: a dark recess with a bevelled top edge, the same language as the HUD plates
  ctx.fillStyle='#14110c';
  ctx.fillRect(x-3,y-3,w+6,h+6);
  ctx.fillStyle='#0a0806';
  ctx.fillRect(x,y,w,h);
  // the fill, from the left
  const fw=Math.round(w*frac);
  ctx.fillStyle='#5ee27a';
  ctx.fillRect(x,y,fw,h);
  // a lit top edge on the fill only, so the filled part reads as a surface with a depth
  if(fw>0){
    ctx.fillStyle='rgba(190,255,200,0.30)';
    ctx.fillRect(x,y,fw,1);
  }
  /* The phase notches, drawn proud of the bar so crossing one does not erase it. These read
     BOSS_PHASE_1 and BOSS_PHASE_2 rather than retyping the numbers, so the mark on the screen cannot
     drift from the threshold in the fight, and they are placed with the SAME expression that places
     the fill's edge: both are Math.round(w*f) on the same w.

     That is not a cosmetic detail. Comparing the two as fractions - round(w*th)/w against th - comes
     out 0.0006 apart, which reads as a mismatch and is not one, because both were rounded from the
     same width. Dividing a rounded pixel back out and comparing it to a raw fraction manufactures a
     disagreement that does not exist on screen. The claim that is actually true, and that the suite
     asserts, is that at exactly the threshold health the fill's edge IS the notch's pixel. */
  for(const th of [BOSS_PHASE_1,BOSS_PHASE_2]){
    const nx=x+Math.round(w*th);
    ctx.fillStyle='#e8dcc0';
    ctx.fillRect(nx-1,y-4,2,h+8);
  }
  /* The name and the phase, ABOVE the bar. Below is the canvas edge - the bar hangs in the 20px strip
     below the room, so there is no room under it for a caption, and putting the labels there would
     push them off the bottom of the screen. Above, they sit over the floor in the bottom of the room,
     which is dead space during a boss fight, because the player is looking at the boss.

     The name is flush left and the phase flush right so a long name and a number cannot collide
     however wide the bar is, and the phase is shown as a number because the mix it selects is not
     legible from colour alone - three moves, then four, then seven. */
  ctx.font='bold 11px monospace';
  ctx.textAlign='left';
  ctx.fillStyle=BOSS_NAME_COLOR[e.phase]||BOSS_NAME_COLOR[1];
  ctx.fillText(BOSS_NAME, x, y-6);
  ctx.textAlign='right';
  ctx.fillStyle='#8a7a63';
  ctx.fillText('PHASE '+e.phase, x+w, y-6);
  ctx.textAlign='left';
}
/* One colour per phase, and the colour is the fight's own language rather than a new one: phase 1 is
   the safe green, and each step hotter. A player who never reads the number still sees the fight
   change colour, which is a tell rather than a readout. */
const BOSS_NAME_COLOR={1:'#8fd89a', 2:'#e8c46a', 3:'#e8804a'};

function drawHUD(){
  /* The HUD is one block, laid out from a single table of numbers so that nothing can drift.

     Everything here used to be placed by hand against the thing next to it - the key plate at 208
     because the heart plate ended near 200, the blink plate at y=56 because the heart plate was
     40 tall and started at 10. It looked fine on the numbers it was tuned on and fell apart the
     moment anything moved: raising maxHp widened the heart plate and left the key plate floating
     in the middle of nowhere, and the blink plate kept a gap under a plate it was supposed to be
     part of. Six plates and a map is a lot of hand-placed edges, and every one of them is a seam
     that does not line up.

     So: one MARGIN off the screen edge, one FRAME for the wood, one GAP between adjacent plates, and
     everything else measured off those. The plates are still separate frames - each keeps its own
     border and its own studs - because "one block" should read as a set of labelled things in one
     place, not as one long smeared bar.

     They touch, but they are NOT all the same width or the same height, and they should not be. The
     health plate is as wide as the health needs; the key plate is two small icons; the blink plate
     is two short bars. Forcing all three to one width - which is what an over-literal reading of
     "one block" produces - fills the narrow ones with empty wood, and empty wood inside a frame
     reads as a missing slot rather than as breathing room. */
  /* MARGIN is split by axis, because the two are not the same decision. The left edge of the HUD is
     measured against the screen border and the room and wants a pixel more air than the top edge
     does - the plates sit in a corner, and a margin that is right on one axis looks wrong on the
     other whenever the two share a number.

     GAP is the space between the health plate and the key plate on its shoulder, and it is one
     pixel. Three read as two separate objects with a corridor between them; one reads as two plates
     in the same frame of reference, which is what they are. */
  const MARGIN_X=HUD_MARGIN_X, MARGIN_Y=HUD_MARGIN_Y, FRAME=HUD_FRAME, GAP=HUD_GAP;
  const HP_H=HUD_HP_H, ROW_H=HUD_ROW_H, KEY_W=HUD_KEY_W, KEY_H=HUD_KEY_H, BLINK_W=HUD_BLINK_W;
  /* THE HEART PLATE GROWS WITH maxHp, AND maxHp HAS NO CEILING - so it needs a ceiling of its OWN.

     Vigor is unbounded by design, so a build with enough Iron Ribs puts 28 hearts in the plate, and
     28 hearts at 26px is 810px from a margin of 15. The minimap plate begins at 798. From maxHp 56 the
     two overlap: the heart plate is drawn over the map, and the map - the thing that tells you where
     the boss is and which rooms you have seen - is unreadable for the rest of a run.

     This is theoretical in the way a lot of balance bugs are theoretical: it takes twenty-one Iron
     Ribs, no other sigils, and a floor deep enough to have found them. Nothing catches it because
     nothing plays that build.

     Two fixes, and both are needed. The plate CAPS its width at what the screen can hold, so it can
     never reach the minimap however much health there is - and the number of hearts actually DRAWN is
     capped with it, because 28 half-hearts at 26px is unreadable whatever space they have. The full
     health is still on the plate, because the HUD already prints it as a number next to the hearts and
     a player with 98 health who cannot see 28 of them still knows they have 98. */
  const hearts=Math.ceil(player.maxHp/2), armorSlots=Math.ceil(MAX_ARMOR/2);
  const heartSlot=26;
  /* the minimap plate starts here: W - MARGIN_X - (GRID*17 + 28), with a gutter of its own */
  const miniLeft=W-MARGIN_X-(GRID*17+28);
  /* HOW MANY HEARTS FIT, AND HOW MUCH ROOM IS LEFT OVER.

     Solved in two steps rather than one, because the label and the heart budget both need the other's
     answer: the label only exists if hearts are hidden, and the hearts only fit if the label has been
     paid for. So the budget is computed with the label reserved, and if it turns out nothing is hidden
     the reserved width is handed back.

     The reservation is one 26px slot, which is what "+23" at 11px monospace needs with room to spare,
     and it is paid out of the heart budget rather than added to the total - so a wide plate still cannot
     reach the minimap however much health there is. */
  const LABEL_W=26;
  const fitWithLabel=Math.max(4,Math.floor((miniLeft-HUD_GAP-MARGIN_X-30)/heartSlot)-armorSlots-1);
  const heartsWouldHide=hearts>Math.max(4,Math.floor((miniLeft-HUD_GAP-MARGIN_X-30)/heartSlot)-armorSlots);
  const heartsDrawn=Math.min(hearts,heartsWouldHide?fitWithLabel:Math.max(4,fitWithLabel+1));
  const heartsHidden=hearts-heartsDrawn;
  const labelW=heartsHidden>0?LABEL_W:0;
  const healthW=30+(heartsDrawn+armorSlots)*heartSlot+labelW;
  const hx=MARGIN_X, hy=MARGIN_Y;

  // row 1: health, with the keys on its right shoulder, top-aligned and touching
  ctx.drawImage(woodPlate(healthW,HP_H),hx,hy);
  drawInset(hx+FRAME,hy+FRAME,healthW-FRAME*2,HP_H-FRAME*2,'#241610');
  // The hearts are CENTRED in the inset, not started from a hardcoded 36. The old version measured
  // the left margin and the right margin with two different expressions, and the armour slots added
  // four more pixels on the right on top of that, so the last heart always sat closer to the frame
  // than the first did. One expression for the first slot, and the whole row follows from it.
  const insetX=hx+FRAME, insetW=healthW-FRAME*2, heartSlotW=heartSlot;
  const rowW=(heartsDrawn+armorSlots)*heartSlotW;
  const slotX=insetX+Math.floor((insetW-rowW)/2)+13;   // +13: the sprite is 26 wide, origin is its left edge
  const heartY=hy+HP_H/2;
  /* EACH SLOT IS ITS OWN INDEX, AND HEALTH DRAINS LEFT TO RIGHT.

     `i` is the slot's own heart number, so the fill is `(hp - i*2)/2`: full hearts first, empties at
     the right. That is the direction the bar has always drained and the direction a player reads.

     This measured `heartsDrawn-1-i` instead - the slot's distance from the END of the row - which
     reverses the whole plate. At 2 health the row read `[empty x7, full]`: the one heart with blood in
     it sat in the far-right slot, and the bar counted backwards. It read as a deliberate choice
     because it was written to fix a real problem, so it is worth being explicit about what that problem
     was and why this is the right answer instead.

     The problem: when the plate is CAPPED and hearts are hidden, drawing only the first `heartsDrawn`
     slots means a player at 98 health sees 26 full hearts and no empties - which is correct - but a
     player at LOW health has their remaining health in the FIRST hearts, which are the ones being
     drawn, so the row correctly showed the damage. The original worry was that a capped row would be
     "full then empty", which is only wrong if it is drawn from the wrong end.

     Measured, at maxHp 98 with 23 hearts hidden, from the left:
         hp 98  26 full hearts              hp 46  23 full then 3 empty
         hp 2   1 full then 25 empty       hp 1   1 half then 25 empty
     Every one reads as the health it is, because a prefix of the row IS the player's remaining health
     and the "+23" says how much of the total is not on screen. */
  for(let i=0;i<heartsDrawn;i++){
    // each heart is worth 2hp, so the fill is this heart's share of one, clamped: a slot further
    // along the plate can have a large `val` and must still read as simply full
    const fill=Math.max(0,Math.min(1,(player.hp-i*2)/2));
    drawHeart(slotX+i*heartSlotW,heartY,fill,'red',3);
  }
  /* "+N" GOES AFTER THE LAST HEART, not before the first one.

     The row now runs left to right with the health on it, so a label at the left end sits exactly where
     a player's remaining health is drawn - on top of the first heart, which is the one they are reading.
     The count of hidden hearts belongs on the far side, past the empties, where it reads as "and there
     are more of these". */
  if(heartsHidden>0){
    ctx.font='bold 11px monospace';ctx.textAlign='left';ctx.fillStyle='#e8c9a0';
    ctx.fillText('+'+heartsHidden,slotX+heartsDrawn*heartSlotW+4,heartY+4);
    ctx.textAlign='left';
  }
  // The last point of life gets a halo so "one heart left" can never be misread as "none left".
  // It matters more than it sounds: at 1hp the plate shows one half heart and five empties, and
  // beside two dimmed armour slots that is easy to read as an empty plate. The pulse is the same one
  // the boss warning uses, and it only exists on the heart that still has blood in it - not on the
  // empties, which would make the whole plate look live.
  /* "ONE HEART LEFT" MEANS ONE HEART ON THE PLATE, which is not the same as hp equal to one.

     This asked `player.hp===1`, an exact comparison, and almost nothing in the game ever produces an
     exact 1. Damage is fractional on purpose - SHOT_DMG 1.8, LUNGER_PAY 0.96, BOSS_SHELL_DMG 1.44 -
     so the health walks past 1 without landing on it: from 8, four shooter shots reach 0.8 and the
     fifth kills, four boss shells reach 1.24, and the pulse never fires at all. A warning that exists
     to catch you at the moment you are about to die, and does not, is worse than no warning: it is a
     promise the screen made and did not keep.

     The rule is now the one the heart plate itself draws by. `fmtHearts` rounds to the nearest half
     heart, because that is what a half-slot heart IS, and a player reading the plate sees 1 heart
     while their health is anywhere near it. So the halo asks the same question the plate answers
     rather than a different and stricter one. */
  if(Math.round((player.hp/2)*2)/2<=1&&player.hp>0){
    const pulse=0.5+0.5*Math.sin(frameCount*0.16/SPEEDUP);
    ctx.save();
    ctx.globalAlpha=0.35+0.55*pulse;
    ctx.fillStyle='#e8395a';
    ctx.beginPath();
    /* on the LAST HEART DRAWN, which is not the first slot. With a capped plate the row is drawn from the
     right, so the heart that says "this is the last of it" is the last one on screen - and the halo on
     the first slot would pulse a heart that is nowhere near the player's actual health. */
  if(Math.round((player.hp/2)*2)/2<=1&&player.hp>0){
    const pulse=0.5+0.5*Math.sin(frameCount*0.16/SPEEDUP);
    ctx.save();
    ctx.globalAlpha=0.35+0.55*pulse;
    ctx.fillStyle='#e8395a';
    ctx.beginPath();
    ctx.arc(slotX+Math.max(0,heartsDrawn-1)*heartSlotW,heartY,15+3*pulse,0,7);
    ctx.fill();
    ctx.restore();
  }
    ctx.fill();
    ctx.restore();
  }
  // an armour slot you have not filled and a key you are not carrying both draw as a darkened
  // version of the real thing rather than as nothing, so the frame reads as a full set of slots
  // instead of a ragged one. an empty slot still costs you nothing, it just stops looking like a hole
  for(let i=0;i<armorSlots;i++){
    // on the SAME grid as the hearts, with no offset. There was a +4 here "to leave a gap", and it
    // was the only thing breaking this plate's symmetry: the row was computed as centred and then
    // the last slot was shoved four pixels right, so the hearts sat 11.5px from the left of the
    // inset and the armour sat 7.5px from the right. The gap it wanted is already 26-21=5px.
    const val=player.armor-i*2, cx=slotX+(hearts+i)*heartSlotW;
    if(val>0) drawHeart(cx,heartY,Math.min(1,val/2),'gray',3);
    else drawHeart(cx,heartY,1,'dimgray',3);
  }

  // the keys, on the same row, touching, top-aligned, and back at the height they always were.
  // Stretching this frame to match the health plate was an over-correction: it holds two small
  // icons, it is a different kind of thing, and the empty space around them read as missing slots.
  const kx=hx+healthW+GAP;
  ctx.drawImage(woodPlate(KEY_W,KEY_H),kx,hy);
  drawInset(kx+FRAME,hy+FRAME,KEY_W-FRAME*2,KEY_H-FRAME*2,'#241610');
  /* The keys were the worst-aligned thing on the screen and both halves of it came from one
     mistake: drawing a sprite at a point that was not its centre. drawSprite anchors at the CENTRE.

     This placed the pair by its left edge and then added a vertical offset tuned by eye, which put
     the silver key 6.5px OUTSIDE the frame's inner border - sitting on the wood, which is exactly
     what the screenshot shows - and left the pair 8px low, with 11px of air above it and 3px below.
     It read as a health-bar problem because it is drawn on the same row as one, but nothing was
     inherited from the health plate: the two are measured independently and both were wrong.

     So the inset is measured, and the pair is centred on it in BOTH axes, with the gap between the
     keys being whatever the padding leaves. One rule, and the frame is what it should be. */
  const kInsetX=kx+FRAME, kInsetY=hy+FRAME, kInsetW=KEY_W-FRAME*2, kInsetH=KEY_H-FRAME*2;
  const keyW=15, KEY_PAD=5;
  const keyGap=kInsetW-KEY_PAD*2-keyW*2;
  const keyX=kInsetX+KEY_PAD+keyW/2, keyY=kInsetY+kInsetH/2;
  drawSprite(KEY_ROWS,player.hasSilver?KEY_SILVER_PAL:KEY_SILVER_DIM,keyX,keyY,1.5,null,player.hasSilver?'hudS':'hudSd');
  drawSprite(KEY_ROWS,player.hasGold?KEY_PAL:KEY_PAL_DIM,keyX+keyW+keyGap,keyY,1.5,null,player.hasGold?'hudG':'hudGd');

  // row 2: the blink charges, glued to the bottom of the health plate and back to the width they
  // were. Two short bars stretched across 186px of wood read as progress on something enormous. The
  // row is deliberately NARROWER than the plate above it, and that asymmetry is what makes the two
  // read as two different instruments stacked rather than as one long bar with a second row of
  // decoration on it.
  const by=hy+HP_H;
  ctx.drawImage(woodPlate(BLINK_W,ROW_H),hx,by);
  drawInset(hx+FRAME,by+FRAME,BLINK_W-FRAME*2,ROW_H-FRAME*2,'#241610');
  // The bars fill what the padding leaves, on the SAME padding in both axes. Horizontal and vertical
  // used to be derived separately and came out 5px and 6px, which reads as the bars leaning; one PAD
  // for both means they cannot.
  const PAD=5, BAR_GAP=2;
  const bInsetX=hx+FRAME, bInsetY=by+FRAME, bInsetW=BLINK_W-FRAME*2, bInsetH=ROW_H-FRAME*2;
  const BAR=Math.floor((bInsetW-PAD*2-BAR_GAP)/2), barH=bInsetH-PAD*2;
  const barX=bInsetX+PAD, barY=bInsetY+PAD;
  for(let i=0;i<2;i++){
    let fill;
    if(i<player.blinkCharges) fill=1;
    else if(i===player.blinkCharges) fill=player.blinkRegen/BLINK_RECHARGE;
    else fill=0;
    const sx=barX+i*(BAR+BAR_GAP);
    ctx.fillStyle='#0e0a08';ctx.fillRect(sx,barY,BAR,barH);
    ctx.fillStyle='#ffffff';ctx.fillRect(sx,barY,BAR*fill,barH);
    ctx.strokeStyle='#5a4630';ctx.lineWidth=1;ctx.strokeRect(sx+0.5,barY+0.5,BAR-1,barH-1);
  }

  /* THE MOMENTUM PLATE. On the blink's row, beside it, because these are the same kind of thing:
     both are the two numbers that describe how the player is doing THIS SECOND rather than what they
     have built. Blink is what you have left, Momentum is what you are worth right now, and pairing
     them says so without a word.

     It was on the character sheet before, and that was wrong for a reason worth writing down. The
     sheet is a pause screen: the player opens it to find out what they are CARRYING, and Momentum is
     the one number on it that they did not pick up. A row that answers a different question than the
     other seven reads as a mistake, and worse, a stat you cannot change is a stat you cannot act on -
     so the only time the player saw the number was when they were not playing.

     In the HUD it is something to watch, which is the entire point of it. It charges while you move
     with bodies in the room, bleeds when you stop, and a hit costs most of it, and every one of
     those is legible from the bar alone if you can see it change. The sheet said that in a sentence.
     The bar says it by being a bar.

     NO TUTORIAL. Deliberately. The plate carries its own name, engraved the same way the depth
     numeral is, and the bar carries the ceiling. A stat that has to be explained is a stat the
     explanation has to keep up with, and this one is legible: it goes up when you are doing well and
     down when you are not, which is the entire rule, visible in real time.

     The bar is NOTCHED for the same reason the character sheet's was. Momentum is capped at 1, and for
     a stat whose whole design is a ceiling that cannot be passed, the ceiling is the interesting
     part - a smooth fill cannot answer "how close am I", and a nearly-full notched bar can.

     The label and the bar split the plate the same way the depth plate splits it, so the row reads
     as one grammar: an engraved mark on the left, a measured indicator on the right. */
  const mX=hx+BLINK_W+GAP;
  ctx.drawImage(woodPlate(HUD_MOMENTUM_W,ROW_H),mX,by);
  drawInset(mX+FRAME,by+FRAME,HUD_MOMENTUM_W-FRAME*2,ROW_H-FRAME*2,'#1a1410');
  const momVal=Momentum.level();
  ctx.save();
  ctx.textAlign='center'; ctx.textBaseline='middle';
  const mInsetW=HUD_MOMENTUM_W-FRAME*2;
  const mCx=mX+FRAME+Math.floor(mInsetW*0.24), mCy=by+ROW_H/2;
  ctx.font='700 10px "Courier New",monospace';
  // struck into the plate, the same way the depth numeral is: a dark copy one pixel down and right,
  // then the lit copy on top, so the word reads as cut into the wood rather than printed on it
  ctx.fillStyle='#0d0a08';
  ctx.fillText('MOMENTUM',mCx+1,mCy+1);
  // the label warms with the meter too, so the plate is legible even in a glance at the corner of the
  // eye. It is the same ramp as the blink trail, which is the point: one colour means one number.
  const mr=Math.round(200+(120-200)*momVal), mg=Math.round(168+87*momVal);
  ctx.fillStyle=momVal>0.04?'rgb('+mr+','+mg+','+Math.round(120+40*momVal)+')':'#c8a878';
  ctx.fillText('MOMENTUM',mCx,mCy);
  ctx.restore();
  ctx.textBaseline='alphabetic';

  // The bar takes everything the label does not, and it takes a LOT of it. This is the number the
  // player is meant to watch move, so it gets the width: at 150px of plate the label ate 40% of the
  // space and left a 40px bar, which is a decoration. A meter you cannot see travel is not a meter
  // you can learn from.
  const mTrackX=mX+FRAME+Math.floor(mInsetW*0.44), mTrackY=mCy-6;
  const mTrackW=mInsetW-Math.floor(mInsetW*0.44)-9, mTrackH=12;
  ctx.fillStyle='#0d0a08'; ctx.fillRect(mTrackX,mTrackY,mTrackW,mTrackH);
  const mFill=Math.max(0,Math.min(1,momVal))*mTrackW;
  if(mFill>0.5){
    // the fill is the green from the blink ramp, so the bar and the trail agree about what "a lot"
    // looks like. It is a meter the player is meant to want to fill, so it is drawn in the colour
    // the game already uses for "you are doing well".
    const fg=Math.round(120+90*momVal);
    ctx.fillStyle='rgb('+Math.round(60+40*momVal)+','+fg+','+Math.round(90+40*momVal)+')';
    ctx.fillRect(mTrackX,mTrackY,mFill,mTrackH);
  }
  // four notches over a 0..1 meter: one every quarter, which is coarse on purpose. A dense ruler on a
  // bar this short reads as a progress bar for something enormous, and the ceiling is the only
  // graduation that carries information - "am I nearly there" is the question, not "am I at 62%".
  ctx.fillStyle='#6b563c';
  for(let i=1;i<4;i++) ctx.fillRect(mTrackX+Math.round(mTrackW*i/4),mTrackY,1,mTrackH);
  ctx.strokeStyle='#5a4630'; ctx.lineWidth=1; ctx.strokeRect(mTrackX+0.5,mTrackY+0.5,mTrackW-1,mTrackH-1);



  /* THE DEPTH PLATE. Which floor you are on, in the same wood-and-inset vocabulary as the health,
     key and blink plates rather than as text painted on the floor.

     It has to be a plate and not a number drawn in the corner, for the same reason those three are
     plates: the HUD is a row of instruments, and a bare number sitting beside them reads as a
     leftover debug readout rather than as the fourth thing the player is watching. The floor is now
     the number the whole game is about - it is what the difficulty ladder reads and what the record
     is - so it belongs in the row, and it belongs there at a glance.

     The depth is drawn as a struck numeral with a tally of ticks beside it rather than as "FLOOR 7".
     The word costs a third of the plate and says nothing the numeral does not, and the tally is
     there because a depth that is only ever a number gives no sense of accumulating distance: three
     marks at floor 7 and one at floor 4 read as different places, which is what they are. */
  const dW=HUD_DEPTH_W, dy=by+ROW_H+HUD_GAP;
  ctx.drawImage(woodPlate(dW,ROW_H),hx,dy);
  drawInset(hx+FRAME,dy+FRAME,dW-FRAME*2,ROW_H-FRAME*2,'#1a1410');
  const dFloor=run?run.floor:1;
  ctx.save();
  ctx.textAlign='center'; ctx.textBaseline='middle';
  const dCx=hx+FRAME+Math.floor((dW-FRAME*2)*0.34), dCy=dy+ROW_H/2;
  // a struck numeral: the depth is a mark cut into the plate, not a label printed on it
  ctx.font='700 17px "Courier New",monospace';
  ctx.fillStyle='#0d0a08';
  ctx.fillText(String(dFloor),dCx+1,dCy+1);
  ctx.fillStyle=dFloor>1?'#e8b06a':'#c8a878';
  ctx.fillText(String(dFloor),dCx,dCy);
  // the tally: one notch per floor, up to eight, then a count of the rest. A depth of forty should
  // look further than a depth of three without needing forty-one pixels of plate.
  const tX=hx+FRAME+Math.floor((dW-FRAME*2)*0.56), tY=dCy-6, tW=dW-FRAME*2-Math.floor((dW-FRAME*2)*0.56)-7;
  ctx.fillStyle='#0d0a08';
  for(let i=0;i<Math.min(8,dFloor);i++) ctx.fillRect(tX+i*3,tY,2,12);
  if(dFloor>8){
    ctx.font='9px "Courier New",monospace';
    ctx.fillStyle='#c8a878';
    ctx.fillText('+'+(dFloor-8),tX+8*3+3,tY+7);
  }
  ctx.restore();
  ctx.textBaseline='alphabetic';

  /* THE MAP IS NOT DRAWN IN THE LAB, and the reason is that in the lab it would be a lie.

     The lab is built on top of a real startGame(), so a real 18-room dungeon exists behind it -
     which is what gives the lab a valid player, a valid build and working menus. Its map is
     therefore a map of a dungeon that is not there. On screen that reads as a large dark grey
     rectangle with one white cell in it: the unvisited-room colour on the knocked-back paper, with
     nothing in it to look at. A player - or a designer - sees a grey box and reasonably concludes
     something failed to draw.

     A debug view that shows furniture from a world it is not in is worse than one that shows
     nothing, so it shows nothing. The lab's own survey grid and braziers are its map, and they are
     true.

     The guard wraps the map's DRAWING and not its layout arithmetic, which is deliberate and was
     wrong the first time: the weapon row underneath reuses pw and mx0 to sit flush against the
     bottom of the map, so closing the guard any earlier left those names undeclared and the HUD
     threw on the very first frame. The weapon row is also deliberately left OUTSIDE the guard - the
     bench works in the lab, and the lab is exactly where you want to be able to swap guns. */
  // the minimap hangs off the same right-hand margin as the left-hand one, so the two edges of the
  // HUD are the same distance from the screen and the whole block reads as one inset
  const cell=17,mapW=GRID*cell,pw=mapW+28,mx0=W-MARGIN_X-pw,my0=MARGIN_Y,mx=mx0+14,my=my0+14,ic=3;
  if(state!=='dev'){
  ctx.drawImage(woodPlate(pw,pw),mx0,my0);
  drawInset(mx0+8,my0+8,pw-16,pw-16,null);
  ctx.drawImage(paperTex(pw-20,pw-20),mx0+10,my0+10);
  // knock the paper back so the map reads as a lit board in a dark room: the white door frames and
  // the room glyphs end up the brightest things on it instead of white-on-cream
  ctx.fillStyle='rgba(9,11,17,0.52)';ctx.fillRect(mx0+10,my0+10,pw-20,pw-20);
  // rooms next to somewhere you have been are shown as unlit shells: enough shape to plan a route
  // with, not enough to skip the room. only rooms an actual door leads to count, otherwise the map
  // shows a room that is merely touching you on the grid and there is no way in. built from the
  // cached room array and a fixed four-direction walk, because this runs every frame
  const known={};
  for(const r of allRooms){
    if(!r.visited) continue;
    known[key(r.x,r.y)]=1;
    for(const d of ARM_DIRS){
      if(!r.doors[d]) continue;
      const [nx,ny]=neighbor(r.x,r.y,d);
      if(rooms[key(nx,ny)]) known[key(nx,ny)]=1;
    }
  }
  for(let gy=0;gy<GRID;gy++)for(let gx=0;gx<GRID;gx++){
    const rr=rooms[key(gx,gy)];
    if(!rr||!known[key(gx,gy)])continue;
    const seen=rr.visited, cx=mx+gx*cell, cy=my+gy*cell, here=gx===cur.x&&gy===cur.y;
    if(!seen){
      ctx.fillStyle='#33383f';
      ctx.fillRect(cx+2,cy+2,cell-5,cell-5);
      ctx.strokeStyle='#454b58';ctx.lineWidth=1;
      ctx.strokeRect(cx+2.5,cy+2.5,cell-6,cell-6);
      // an uncovered upgrade or boss room wears a padlock, tinted to the key that opens it, so a
      // locked objective is findable on the map before you walk into it
      if(rr.type==='item'||rr.type==='boss'){
        const locked=rr.type==='boss'?!bossUnlocked&&!player.hasGold:!itemUnlocked&&!player.hasSilver;
        if(locked) drawGlyph('lock',cx+cell/2-0.5,cy+cell/2,2,rr.type==='boss'?'#ffd23d':'#d8dee9');
      }
      continue;
    }
    ctx.fillStyle=here?'#8a909c':rr.type==='boss'?'#ef8434':rr.type==='item'?'#ffd23d':rr.type==='start'?'#a0a6b2':'#b9c0cc';
    ctx.fillRect(cx,cy,cell-1,cell-1);
    ctx.strokeStyle=here?'#15171c':'#4a4f5a';ctx.lineWidth=here?2:1;
    ctx.strokeRect(cx+(here?1:0.5),cy+(here?1:0.5),cell-(here?3:2),cell-(here?3:2));
    ctx.fillStyle='#ffffff';
    const t=3;
    if(rr.doors.N) ctx.fillRect(cx+cell/2-t,cy,t*2,2);
    if(rr.doors.S) ctx.fillRect(cx+cell/2-t,cy+cell-3,t*2,2);
    if(rr.doors.W) ctx.fillRect(cx,cy+cell/2-t,2,t*2);
    if(rr.doors.E) ctx.fillRect(cx+cell-3,cy+cell/2-t,2,t*2);
    if(rr.type==='item'||rr.type==='boss'){
      // the padlock stays on until the door is actually open, so it keeps pointing you at the
      // objective even while you are carrying the key that opens it
      const sealed=rr.type==='boss'?!bossUnlocked:!itemUnlocked;
      drawGlyph(sealed?'lock':rr.type,cx+cell/2-0.5,cy+cell/2,2,sealed?(rr.type==='boss'?'#ffd23d':'#d8dee9'):'#2a1a10');
    }
    // A boss you have walked into and not finished gets its own mark. The padlock only tells you the
    // door is shut, which stops being useful the moment you are standing in the doorway with the key
    // in your hand - and backing out of a half-fought boss with no marker is the one place on the map
    // you have no idea where to come back to. The ring breathes so it reads as live rather than as a
    // permanent fixture, and it is the loudest thing in that cell while the fight is on.
    if(rr.type==='boss'&&!rr.cleared){
      const pulse=0.5+0.5*Math.sin(frameCount*0.09/SPEEDUP);
      ctx.strokeStyle='rgba(255,70,70,'+(0.55+0.45*pulse).toFixed(3)+')';
      ctx.lineWidth=2;
      ctx.strokeRect(cx-1.5,cy-1.5,cell+1,cell+1);
      ctx.fillStyle='rgba(255,70,70,'+(0.16+0.20*pulse).toFixed(3)+')';
      ctx.fillRect(cx,cy,cell-1,cell-1);
      // four corner ticks, so it still reads as a threat on a map seen at a glance
      ctx.fillStyle='rgba(255,120,90,'+(0.6+0.4*pulse).toFixed(3)+')';
      const t2=3;
      for(const [ox,oy] of [[0,0],[cell-1-t2,0],[0,cell-1-t2],[cell-1-t2,cell-1-t2]]) ctx.fillRect(cx+ox-2,cy+oy-2,t2+4,t2);
    }
    const pk=rr.pickups&&rr.pickups.find(p=>p.kind==='heart'||p.kind==='armor'||p.kind==='key'||p.kind==='goldkey');
    if(pk){
      ctx.fillStyle='#1d1f26';ctx.fillRect(cx+(cell-ic)/2-1,cy+(cell-ic)/2-1,ic+2,ic+2);
      ctx.fillStyle=pk.kind==='heart'?'#ff4d6d':pk.kind==='goldkey'?'#ffd23d':pk.kind==='key'?'#d8dee9':'#dfe3ea';
      ctx.fillRect(cx+(cell-ic)/2,cy+(cell-ic)/2,ic,ic);
    }
  }
  }   // end of the map, which the lab skips

  // The weapon row is three plates on one line: left hand, middle, right hand, touching, and glued
  // to the bottom of the map. The middle one is the ACTIVE SLOT - the item Q presses - and it was a
  // drawn empty frame for most of this file's life on the grounds that a consumable wants to be a
  // different shape from a weapon. The shape argument was right and the conclusion was wrong: an
  // empty plate between two weapons reads as a hole in the HUD, and the plate was already reserved,
  // so leaving it empty only made the reservation visible.
  //
  // It is drawn with the same wood, border and inset as its neighbours because an undrawn gap between
  // two plates reads as a mistake and a drawn one reads as a slot. The map is exactly three slots wide
  // to within a pixel, so the row cannot be off-centre.
  const slotY=my0+pw+GAP, slotH=50, slotGap=2;
  const slotW=Math.floor((pw-slotGap*2)/3);
  /* The row width the labels are fitted into, published so the test can assert against the value the
     drawing actually uses. It was a hardcoded 190 in the suite, and the real value is 147 - so the
     test was checking a geometry the game does not have, and passed while doing it. A test that
     cannot read the number it is about has to guess it, and a guess that is comfortably larger than
     the truth is the worst kind: it fails nothing. */
  BENCH_ROW_W=pw;
  const midX=mx0+slotW+slotGap, midW=pw-slotW*2-slotGap*2;
  const wp=WEAPONS[player.weaponIdx], alt=activeAlt();
  const held=Items.active();
  drawSlot(mx0,slotY,slotW,player.weaponIdx,'left',wp.color,1-player.cooldown/(player.cooldownMax||wp.cooldown));
  drawActivePlate(midX+Math.round((midW-slotW)/2),slotY,slotW,held);
  drawSlot(mx0+pw-slotW,slotY,slotW,player.altMode==='hook'?'hook':'alt','right',alt.color,1-player.altCooldown/(player.altCooldownMax||alt.cooldown));
  /* Only the ITEM is named on this row, and it is named across the whole row.

     Three names on one line was tried and it does not work, and the measurements are the reason
     rather than taste: the row is 147px, and the three of them at 12px come to 210px for the worst
     case ("Arcane Beam" + "Lantern Friend" + "Blast"), 197px for the next, 184px for the next. Only
     "Bolt" + "Tin Cup" + "Blast" fits unshrunk. Everything else had to shrink to 9-10px or lose
     letters, and a row of 10px type is the same legibility problem as a row of stubs - it just
     fails more quietly.

     So the middle plate gets the row to itself, and the two weapons keep only what the player cannot
     already read off them. That is not a loss of information: the weapon plate carries its icon and
     its cooldown sweep, the alt plate carries its icon and the footer already says "LMB cast /
     RMB blast" and "SHIFT blink" every frame, and the character sheet (F1) lists both weapon names in
     full. The item name is the one that is new, unbounded in length, and absent from the sheet
     until you go looking for it - so it is the one that gets the space.

     The row is drawn as a single centred line rather than under the middle plate, because the middle
     plate is 47px wide and "Lantern Friend" is 92px. Anchoring it to the plate is what produced the
     original overlap; letting it use the row is what fits it at 12px with 8px to spare either side. */
  const labelY=slotY+slotH+14;
  const itemColor=held?(Content.has('item',held.id)?(Content.get('item',held.id).color||'#e8dcc0'):'#e8dcc0'):'#6b5a44';
  const label=fitLabel(held?held.name:'nothing', pw-8);
  ctx.fillStyle=itemColor;
  ctx.fillText(label.text, mx0+pw/2, labelY);

  /* The Warden's bar, drawn here rather than in drawRoom so it sits with the other HUD plates and
     inherits the same frame. It is drawn LAST so it is over the plates if the layout ever puts them
     in the same place, and it is drawn only while a boss is actually alive in THIS room - a bar for a
     boss that is not here would be a lie about the current fight, and the depths after the boss room
     would carry it forever if nothing took it away. */
  const warden=currentRoom().enemies.find(b=>b.type==='boss'&&b.hp>0);
  if(warden) drawBossBar(warden);

  ctx.textAlign='left';
}

/* ---- the seed tag ------------------------------------------------------------------------------
   A luggage tag: chamfered corner, punched hole, the word stamped above the number.

   It is the one thing on screen whose entire job is to be READ OFF and sent to somebody else, so
   it is drawn as a physical object you could pick up rather than as a line of text floating on a
   background. Baked per text and cached, for the reason paperTex had to be: an object re-baked from
   a random stream every frame shimmers rather than sitting still, and a tag is read - not glanced
   at - so it has to hold perfectly still while it is being read. */
const tagCache={};
/* letter-spacing by hand rather than ctx.letterSpacing, which is not everywhere yet, and by hand
   it can be measured properly so the run of characters is CENTRED - canvas has no tracking-aware
   measureText, so a centred letterspaced run otherwise sits off-centre by half the tracking. */
function stampText(g,text,cx,y,size,color,gap){
  let x=cx-stampWidth(g,text,size,gap)/2;
  const chs=String(text).split('');
  g.textAlign='left'; g.fillStyle=color;
  for(const ch of chs){ const w=g.measureText(ch).width; g.fillText(ch,x,y); x+=w+gap; }
}
function stampWidth(g,text,size,gap){
  g.font='bold '+size+'px monospace';
  return String(text).split('').reduce((a,ch)=>a+g.measureText(ch).width,0)+gap*(String(text).length-1);
}
/* Right-aligned, for the sheet. stampText CENTRES on its x, and canvas has no tracking-aware
   measureText, so centring a value where every other row is flush to the right margin hangs it
   half its width over the paper. The first version of this row did exactly that and the seed read
   "000039" instead of "000039U" - a truncated seed is worse than no seed, because the missing
   character is the difference between a dungeon that replays and one that does not. */
function stampRight(g,text,rx,y,size,color,gap){
  stampText(g,text,rx-stampWidth(g,text,size,gap)/2,y,size,color,gap);
}
function seedTagArt(w,h,text){
  const k=w+'x'+h+text;
  if(tagCache[k])return tagCache[k];
  const c=mk(w,h),g=c.getContext('2d'),CH=18;
  const outline=()=>{g.beginPath();g.moveTo(CH,1);g.lineTo(w-1,1);g.lineTo(w-1,h-1);g.lineTo(1,h-1);g.lineTo(1,CH);g.closePath();};
  outline(); g.fillStyle=g.createPattern(WOOD_TEX,'repeat'); g.fill();
  // light falls from the top-left, the same direction woodPlate already implies, so the two read as
  // cut from the same plank
  g.save(); g.clip();
  g.strokeStyle='rgba(255,228,186,0.26)'; g.lineWidth=2; g.stroke();
  g.restore();
  g.strokeStyle='rgba(26,14,6,0.85)'; g.lineWidth=1; outline(); g.stroke();
  g.strokeStyle='rgba(255,214,160,0.32)'; g.lineWidth=1;   // the cut edge, catching light on its diagonal
  g.beginPath(); g.moveTo(1,CH); g.lineTo(CH,1); g.stroke();
  // a punched hole: dark bore, far wall lit on the lower right, so it reads as a hole THROUGH the
  // card rather than a dot painted on it
  const hx=CH*0.72,hy=h/2,hr=5.5;
  g.fillStyle='#150c05'; g.beginPath(); g.arc(hx,hy,hr,0,6.283); g.fill();
  g.strokeStyle='rgba(240,205,150,0.38)'; g.lineWidth=1.5;
  g.beginPath(); g.arc(hx,hy,hr,Math.PI*0.15,Math.PI*0.95); g.stroke();
  // sized off h rather than written in, so the tag cannot be resized without the type coming with
  // it - which is how the number and the caption ended up sitting on top of each other.
  // The number is filled twice, a pixel apart: a pale one down-right, then the ink over it. That one
  // extra fill is the whole difference between PRINTED on a tag and PRESSED into one, and a tag is
  // only worth drawing at all if it looks like it has been through something.
  stampText(g,text,w/2+12,h*0.62+1,h*0.40,'rgba(255,214,160,0.32)',3.6);
  stampText(g,text,w/2+12,h*0.62,h*0.40,'#241206',3.6);
  stampText(g,'SEED',w/2+12,h*0.88,h*0.17,'rgba(36,18,6,0.72)',2.8);
  return tagCache[k]=c;
}
function drawSeedTag(cx,cy,text,w,h){
  w=w||260; h=h||58;
  ctx.drawImage(seedTagArt(w,h,text),Math.round(cx-w/2),Math.round(cy-h/2));
  return w;
}
function drawStartSeed(){
  // shown BEFORE you commit to it, not only afterwards: the number that is about to decide this
  // dungeon belongs on screen while the player decides whether to accept it
  drawSeedTag(W/2,418,Rnd.seedText);
  ctx.textAlign='center'; ctx.font='12px monospace'; ctx.fillStyle='#6f7a8c';
  ctx.fillText('the seed decides this dungeon  ·  press S to play a different one',W/2,472);
  ctx.textAlign='left';
}
/* THE DESCENT BEAT. Drawn over the room fade, during the one moment in a run where nothing is
   trying to kill the player.

   It earns the 0.9s fade that descend() asks for, and that fade is the reason it is here rather than
   somewhere in the HUD: the transition between floors is the only place in the game where the player
   is guaranteed a clear look at the screen, and a beat that exists only to show a number has to use
   it or the beat is wasted.

   The line under it says what is actually about to change, because "FLOOR 7" alone does not tell a
   player that the thing they are carrying is about to matter more. The three numbers are the three
   levers of the depth ladder, stated as ratios against floor 1 so they are readable without knowing
   what any of them mean internally. */
function drawDescent(){
  if(descendT<=0) return;
  const t=descendT/FADE_DESCEND;
  // ease in and out so it arrives rather than appears, and is gone before control returns
  const a=t>0.5?(1-t)*2:t*2;
  const f=run?run.floor:1;
  ctx.save();
  ctx.textAlign='center';
  ctx.globalAlpha=Math.max(0,Math.min(1,a));
  ctx.fillStyle='#e8b06a';
  ctx.font='bold 44px monospace';
  ctx.fillText('FLOOR '+f,W/2,H/2-34);
  ctx.fillStyle='#8a7a62';
  ctx.font='14px monospace';
  // "deeper than you have been" was here first and it is a lie on the second descent - by floor 12
  // a returning player has plainly been deeper. The transition itself is always true, always says
  // something the player did not already know, and needs no assumption about their history.
  ctx.fillText('floor '+(descendFrom||f-1)+'  →  '+f,W/2,H/2-6);
  // the three levers, as ratios rather than percentages. "4.3x tougher" reads at a glance where
  // "330% tougher" needs the mental arithmetic, and a banner is not the place to ask for that.
  const t1=depthTough(), r1=depthRate();
  ctx.fillStyle='#6b6152';
  ctx.font='13px monospace';
  ctx.fillText('bodies '+t1.toFixed(1)+'x tougher  ·  they answer '+r1.toFixed(1)+
    'x faster  ·  rooms fuller',W/2,H/2+22);
  // a ruled line under it, struck like the depth plate, so the beat belongs to the same object
  ctx.strokeStyle='rgba(232,176,106,0.45)';
  ctx.lineWidth=1;
  ctx.beginPath();
  ctx.moveTo(W/2-150,H/2+36); ctx.lineTo(W/2+150,H/2+36);
  ctx.stroke();
  ctx.restore();
  ctx.textAlign='left';
}

function drawOverlay(title,sub,foot){
  ctx.fillStyle='rgba(0,0,0,0.72)';ctx.fillRect(0,0,W,H);
  ctx.fillStyle='#fff';ctx.textAlign='center';
  ctx.font='bold 34px monospace';ctx.fillText(title,W/2,270);
  ctx.font='16px monospace';ctx.fillText(sub,W/2,310);
  if(foot){ctx.font='13px monospace';ctx.fillStyle='#aaa';ctx.fillText(foot,W/2,340);}
  ctx.textAlign='left';
}
function recordsLine(){
  // Depth first, because it is the number the game is about now. Rooms explored and dungeons
  // cleared are both historical - they describe a version of the game where one dungeon WAS the
  // game - and they stay because they are still true and a player who has them set should not lose
  // them, but they are no longer the headline.
  let s='deepest floor: '+records.deepest+' · best rooms explored: '+records.rooms;
  if(records.wins) s+=' · fastest clear: '+fmtTime(records.fastest)+' · dungeons cleared: '+records.wins;
  return s;
}
// TICK_HZ ticks = 1s; counted in integer tenths so rounding can never print 0:60.0
function fmtTime(ticks){
  const tenths=Math.floor(ticks*10/TICK_HZ), m=Math.floor(tenths/600), s=Math.floor(tenths%600/10);
  return m+':'+(s<10?'0':'')+s+'.'+tenths%10;
}
/* A DISPLAYED NUMBER, at the precision the reader can act on.

     `toFixed(2)` on a derived value prints the binary rounding error along with the number: a per-pellet
     damage of 2.6 * 1.0 comes out as "2.60" by luck, but 0.5 + 0.66 and 1.8 * 0.8 produce "1.1600000000000001"
     and "1.44" from the same expression. Those digits are not information - they are the mantissa, and
     a player reading a damage panel is reading a number they intend to compare against a fight.

     So every player-facing number goes through here, with the precision chosen per quantity rather than
     applied as a blanket default: a tenth where the number is a rate or a time, none where it is a
     multiplier the player reads as "roughly double". The value is NOT rounded in the simulation - this
     is presentation, and rounding a hit to 1 damage would be a balance change. */
function showNum(v,dp){
  if(!isFinite(v)) return '—';
  const r=Math.round(v*Math.pow(10,dp===undefined?1:dp))/Math.pow(10,dp===undefined?1:dp);
  return String(r);
}

/* HEARTS, for the player. Rounded to the nearest HALF heart, and that is the whole argument.

     Health in this game is measured in half-hearts and displayed as hearts, so a half-heart is the
     smallest thing the player has ever been shown - quoting anything finer than that is quoting a
     number the interface has no way to represent. The damage that goes in is genuinely fractional:
     LUNGER_PAY 0.96 through ARMOUR 0.66 is 0.6336 half-hearts a hit, accumulated over a floor, so an
     unrounded total reaches things like "10.625 hearts" and "17.375 hearts" - which read as a bug
     rather than as a measurement, and invite the player to check the arithmetic instead of reading
     the result.

     NOT rounded at the simulation. Damage stays exact; only the display is rounded. Rounding health
     itself would make every hit worth at least one heart, because the smallest hit in the game is
     under half a heart - that is a balance change wearing a formatting costume, and this game's whole
     difficulty ladder is fractional on purpose (ARMOUR 0.66, LUNGER_PAY 0.96, TEMPO.rate).

     Rounded to the half rather than the whole, because a heart with a half in it is what the game
     already draws: the heart plate has an armour row of half-slots. */
function fmtHearts(half){
  let h=Math.round((half/2)*2)/2;         // nearest half heart, from half-hearts
  h=Math.round(h*10)/10;                   // kill float noise like 3.5000000000000004
  const s=String(h);
  return s+(h===1?' heart':' hearts');
}

/* end-of-run summary: paper sheet on a wood plate, same materials as the HUD */
const INK='#3a2616', INK_SOFT='#7a6040', INK_NEW='#b3261e';
/* ---- the weapon bench (F1) ------------------------------------------------------------------------
   A panel for swapping guns mid-playtest without going back to the title screen for each attempt.

   WHY IT EXISTS, in one line: weapon balance could not be judged because every attempt at a new
   gun cost a run, and a run costs twenty minutes. The numbers below are the ones the balance
   argument is actually about, so the panel shows them rather than making the player remember them.

   THE NUMBERS ARE LIVE. Every figure reads the current build - the weapon's own damage PLUS
   Stats.value('strength'), which is the term that decides how a gun behaves under a buff. A bench
   that printed each weapon's own damage would have said the Arcane Beam was weak when the real
   complaint is that a flat +4 Strength nearly triples it while it barely touches the Bolt. Showing
   the buffed figure next to the base one is the whole point of the panel.

   THE GEOMETRY IS COMPUTED ONCE, here, and both the drawing and the click handler read it. The
   boss gate was broken for a week because the gate and the doorway each had their own copy of the
   same rectangle and they drifted apart; a hit box that is calculated a second time at the click
   site is that exact bug waiting to happen, and it fails silently - the button draws, the click
   lands somewhere else, and it looks like the game is not listening. */

function devLayout(){
  /* Every vertical position in the panel is a band measured from the top of the card, and the card is
     tall enough to hold them all with air between. The first version of this panel overlapped itself
     three ways - the subtitle ran into the held-weapon block, the column headers sat on the first row,
     and the footer fought the ESC hint - because the numbers were written into each draw call as it
     was needed instead of being budgeted once. So they are budgeted once, here, and every band is
     named. Nothing below is allowed to invent a y coordinate.

       head    the title, and what is held, on the same baseline
       sub     two lines of explanation, clear of the head's right-hand column
       colHdr  the column titles, above the rule and above the first row
       rows    four of them, 60px each
       foot    the keys and the build readout, below a rule
   */
  const pw=608, ph=464, px=Math.round((W-pw)/2), py=Math.round((H-ph)/2);
  const rowH=58, rowX=px+26, rowW=pw-52;
  const head=py+34, sub=py+72, colHdr=py+112, listY=py+128;
  // The footer band is measured from its TALLEST element, not its rule. It used to be budgeted from
  // the rule, which put the ESC key cap three pixels past the bottom edge of the card - and the gap
  // check that should have caught it was measuring the rule too, so it agreed with itself. The two
  // tallest things down there are the caps (17px) and the baseline 13px under the last one.
  const footRule=py+370, foot=py+386;
  return {pw:pw,ph:ph,px:px,py:py,rowH:rowH,rowX:rowX,rowW:rowW,listY:listY,
          head:head,sub:sub,colHdr:colHdr,foot:foot,footRule:footRule,
          rowY:i=>listY+i*rowH, rowH2:rowH-6};
}

/* A key cap, drawn to match the .cap caps in the binds bar under the window: a dark plate with a
   hairline top and a heavier bottom edge, so it reads as a physical key rather than a label in a
   box. It is baked per label because the width is measured from the text. */
const devCapCache={};
function devCap(label,on){
  const k=label+'|'+(on?1:0);
  let c=devCapCache[k];
  if(c) return c;
  const g=document.createElement('canvas').getContext('2d');
  g.font='11px monospace';
  const tw=Math.ceil(g.measureText(label).width);
  const w=tw+12, h=17;
  c=document.createElement('canvas'); c.width=w; c.height=h;
  const x=c.getContext('2d');
  // the well the cap sits in, one shade darker than the card behind it
  x.fillStyle=on?'#241a10':'#15181f';
  x.fillRect(0,0,w,h);
  // hairline on three sides, heavy along the bottom: the shading that makes it a key
  x.fillStyle=on?'#6b5636':'#3c4453';
  x.fillRect(0,0,w,1); x.fillRect(0,0,1,h); x.fillRect(w-1,0,1,h);
  x.fillStyle=on?'#8a7048':'#525b6c';
  x.fillRect(0,h-2,w,2);
  x.font='11px monospace'; x.textAlign='center'; x.textBaseline='middle';
  x.fillStyle=on?'#ffe9b0':'#c3ccdb';
  x.fillText(label,w/2,h/2+0.5);
  devCapCache[k]=c;
  return c;
}
/* how far this weapon's damage falls off, as the game computes it. One copy of the rule, because a
   panel that reimplemented the curve would eventually disagree with the gun it is describing. */
function devFalloff(w,d){
  if(d>=w.fFar) return w.fMin;
  if(d<=w.fNear) return 1;
  return 1-(1-w.fMin)*((d-w.fNear)/(w.fFar-w.fNear));
}
function drawDevMenu(){
  if(!devOpen) return;
  const G=devLayout();
  ctx.fillStyle='rgba(6,8,12,0.80)';ctx.fillRect(0,0,W,H);
  ctx.drawImage(woodPlate(G.pw,G.ph),G.px,G.py);
  drawInset(G.px+8,G.py+8,G.pw-16,G.ph-16,null);
  ctx.drawImage(paperTex(G.pw-20,G.ph-20),G.px+10,G.py+10);


  // Read once, before anything is drawn. All three are the panel's spine: which gun is held, what
  // the current build adds to every hit, and which gun is strongest at the range that matters - the
  // last one because RELATIVE is a bar, and a bar needs something to be measured against.
  const held=WEAPONS[player.weaponIdx];
  const str=Stats.value('strength');
  let bestDps=-1;
  for(const w of WEAPONS){
    const d=devDps(w,str,250);
    if(d>bestDps) bestDps=d;
  }
  const L=G.px+26, R=G.px+G.pw-26;

  // the head: title on the left, what is held on the right, both on the SAME baseline so they read
  // as two ends of one line rather than as a title and a caption that drifted together
  ctx.textAlign='left';
  ctx.font='bold 15px monospace';ctx.fillStyle=INK;
  ctx.fillText('WEAPON BENCH',L,G.head);
  ctx.textAlign='right';ctx.font='11px monospace';ctx.fillStyle=INK_SOFT;
  ctx.fillText('holding',R,G.head);
  ctx.font='bold 15px monospace';ctx.fillStyle=held.color;
  ctx.fillText(held.name,R,G.head+18);
  ctx.textAlign='left';

  // the subtitle gets its own band, BELOW the head rather than beside it. The two lines are the
  // whole reason this panel exists - that it is not saved, and that the figures are buffed - and
  // putting them beside a right-aligned column meant the longer line ran underneath it.
  ctx.font='11px monospace';ctx.fillStyle=INK_SOFT;
  ctx.fillText('Swap a gun without losing the run. Nothing here is saved.',L,G.sub);
  ctx.fillText('Every figure includes your current Strength - that is what decides how a gun behaves buffed.',L,G.sub+15);

  // the column titles, in a band of their own with a rule under them, so they cannot land on a row
  ctx.textAlign='center';ctx.font='bold 10px monospace';ctx.fillStyle=INK_SOFT;
  ctx.fillText('DMG/PULL',G.px+152,G.colHdr);
  ctx.fillText('PULLS/S',G.px+238,G.colHdr);
  ctx.fillText('DPS @100',G.px+312,G.colHdr);
  ctx.fillText('@250',G.px+374,G.colHdr);
  ctx.fillText('TTK @250',G.px+448,G.colHdr);
  ctx.textAlign='right';ctx.fillText('RELATIVE',R,G.colHdr);
  ctx.textAlign='left';
  ctx.fillStyle='rgba(90,60,30,0.35)';ctx.fillRect(L,G.colHdr+8,G.pw-52,1);

  for(let i=0;i<WEAPONS.length;i++){
    const w=WEAPONS[i];
    const y=G.rowY(i), equipped=player.weaponIdx===i;
    const hovered=(mouse.x>=G.rowX&&mouse.x<G.rowX+G.rowW&&mouse.y>=y&&mouse.y<y+G.rowH);
    // the row is a plate of its own, not a gap between rules: a list you can aim at has to look
    // like a list of things rather than four lines of text with a border round the lot
    ctx.fillStyle=equipped?'rgba(120,88,40,0.30)':hovered?'rgba(90,110,140,0.20)':'rgba(40,44,54,0.16)';
    ctx.fillRect(G.rowX,y,G.rowW,G.rowH2);
    ctx.strokeStyle=equipped?'#8a7048':'rgba(80,90,108,0.55)';ctx.lineWidth=1;
    ctx.strokeRect(G.rowX+0.5,y+0.5,G.rowW-1,G.rowH2-1);
    if(equipped){
      // a gold spine down the equipped edge, so the current gun is findable without reading it
      ctx.fillStyle=w.color;ctx.fillRect(G.rowX+1,y+1,3,G.rowH2-2);
    }

    drawIcon(i,G.rowX+34,y+G.rowH2/2,1.05);
    const cap=devCap(String(i+1),equipped);
    ctx.drawImage(cap,G.rowX+G.rowW-14-cap.width,y+G.rowH2/2-cap.height/2);

    const tx=G.rowX+62;
    ctx.font='bold 13px monospace';ctx.fillStyle=w.color;
    ctx.fillText(w.name,tx,y+20);
    ctx.font='10px monospace';ctx.fillStyle=INK_SOFT;
    /* THE CONE, IN DEGREES, ONCE.

       This read `spread*spread` - it multiplied the spread by itself and then squared the result, by
       the look of it - so the Arcane Beam's 0.16 rad printed as 0.1 degrees instead of 18.3, and
       three of the four guns printed 0.0. The column existed to answer "can I aim this", and it
       answered "they are all the same, and all zero". It is now the full opening angle in degrees,
       which is what the label says and what a player can feel the difference between. */
    const spreadRad=w.spreadFromPrecision?preciseSpread(w.spread):w.spread;
    const deg=(spreadRad*2*180/Math.PI).toFixed(1);
    ctx.fillText('cone '+deg+'deg  ·  floor '+(w.fMin*100).toFixed(0)+'%  ·  '+
      (w.count>1?(w.count+' pellets'):'single hit')+(w.pierce?'  ·  pierces '+w.pierce:''),tx,y+35);
    /* STRENGTH IS ADDED ONCE PER SHOT, so this is base + str and not base + str*count.

       The game does `count*dmg + strength` and shares it across the pellets (40-combat.js, with a long
       note on why: a flat +4 is +57% on the Bolt and +476% on the Beam, so a per-pellet bonus would
       have made the shotgun eight times stronger per sigil than everything else). The panel added the
       Strength once per PELLET, so the Scatter showed 55.2 where the game deals 34.2 - the bench told
       the player the gun they already own is 60% stronger than it is, on the screen whose entire job
       is helping them choose. */
    const base=w.dmg*w.count;
    ctx.fillStyle=str>0?'#d8a23c':INK_SOFT;
    ctx.fillText(str>0?('base '+showNum(base)+'  →  with +'+str+' Strength  '+showNum(base+str))
                 :('base '+showNum(base)+' a pull, single target'),tx,y+48);

    const dps100=devDps(w,str,100), dps250=devDps(w,str,250);
    const pulls=TICK_HZ/(w.cooldown/TEMPO.rate);
    const per=w.dmg*w.count+str;
    ctx.textAlign='center';ctx.font='11px monospace';
    ctx.fillStyle=INK;ctx.fillText(showNum(per),G.px+150,y+28);
    ctx.fillText(pulls.toFixed(1),G.px+238,y+28);
    ctx.fillStyle=dps250>=bestDps-0.01?'#5ee27a':INK;ctx.fillText(dps100.toFixed(1),G.px+310,y+28);
    ctx.fillStyle=dps250>=bestDps-0.01?'#5ee27a':INK;ctx.fillText(dps250.toFixed(1),G.px+372,y+28);
    /* TIME TO KILL A LUNGER, AGAINST THE LUNGER THE GAME ACTUALLY HAS.

       This divided by `18*TOUGH` - the lunger's health before it was reduced - and by nothing else, so
       it ignored ARMOUR entirely. Both halves were wrong in the same direction, and the bench said a
       gun kills a tanky armoured body faster than it does.

       Both are now read from the table rather than written down, so a body buff or a health change
       cannot leave the panel quoting a number the game is not using. ARMOUR is applied because every
       shot pays it; it is also the reason the figure is a FLOOR rather than a promise, since a shotgun
       at range is not landing every pellet - which the panel says nowhere, and should. */
    const lunger=ENEMY.lunger;
    const ttkSecs=dps250>0?lunger.hp/(dps250*lunger.armour):0;
    ctx.fillText(ttkSecs>0?showNum(ttkSecs)+'s':'-',G.px+446,y+28);
    // REL is a bar, not a number: "is this one stronger" is a comparison and a bar answers it at a
    // glance, where four numbers in a column have to be read against each other one at a time
    const bw=54, bx=R-bw, by=y+22;
    ctx.fillStyle='rgba(20,24,32,0.9)';ctx.fillRect(bx,by,bw,8);
    ctx.fillStyle=w.color;ctx.fillRect(bx,by,bw*(dps250/bestDps),8);
    ctx.strokeStyle='#5a4630';ctx.lineWidth=1;ctx.strokeRect(bx+0.5,by+0.5,bw-1,7);
    ctx.textAlign='left';
  }

  // footer: the two keys and a heal - IN THE LAB ONLY, because a playtest that has to be restarted
  // every time you spend your charges is a playtest you stop doing. Outside the lab there is nothing to
  // advertise: granting a key or a heart during a real run is not a shortcut, it is a cheat with a
  // label on it, and this panel is reachable from any game at any depth with F1.
  const fy=G.foot;   // the band devLayout budgeted, not a second guess at where the footer is
  ctx.fillStyle='rgba(90,60,30,0.35)';ctx.fillRect(L,fy-12,G.pw-52,1);
  ctx.font='11px monospace';
  const cap=(label,x,on)=>{const c=devCap(label,on);ctx.drawImage(c,x,fy+4);return x+c.width+5;};
  ctx.fillStyle=INK_SOFT;
  let x=L;
  if(state==='dev'){
    x=cap('G',x,player.hasGold);ctx.fillStyle=player.hasGold?'#ffd23d':INK_SOFT;
    ctx.fillText(player.hasGold?'gold key held':'give gold key',x,fy+17);x+=130;
    x=cap('S',x,player.hasSilver);ctx.fillStyle=player.hasSilver?'#d8dee9':INK_SOFT;
    ctx.fillText(player.hasSilver?'silver key held':'give silver key',x,fy+17);x+=140;
    x=cap('H',x,false);ctx.fillStyle=INK_SOFT;
    ctx.fillText('refill heart and cooldowns',x,fy+17);
  } else {
    ctx.fillStyle=INK_SOFT;
    ctx.fillText('1-4 swap wand  ·  ESC or F1 close  ·  F2 for the lab, where keys and hearts are free',x,fy+17);
  }

  ctx.textAlign='right';ctx.fillStyle=INK_SOFT;ctx.font='10px monospace';
  ctx.fillText('strength '+str+'   ·   tempo '+showNum(TEMPO.rate,2)+'x   ·   tick '+TICK_HZ+'Hz',R,fy+30);
  ctx.font='bold 11px monospace';ctx.fillStyle=INK;
  const c=devCap('ESC',false);ctx.drawImage(c,R-c.width,fy+44);
  ctx.fillStyle=INK_SOFT;ctx.font='10px monospace';
  ctx.fillText('or F1 to close',R-c.width-6,fy+57);
  ctx.textAlign='left';
}
/* hearts per second for a weapon at a range, under the current build. This is the number the balance
   argument is about and the panel exists to show it, so it is derived from the same terms the game
   fires with: base damage plus Strength, times pellets, times rate, times the same falloff the
   projectile itself uses. */
function devDps(w,str,dist){
  const pulls=TICK_HZ/(w.cooldown/TEMPO.rate);
  /* `count*dmg + strength`, matching fireWeapon. This was `(dmg+strength)*count`, which is the
     per-pellet reading the game explicitly does not use. */
  return pulls*(w.dmg*w.count+str)*devFalloff(w,dist);
}
/* THE CARD'S SHAPE, decided by its contents rather than by a number someone wrote down.

   The card used to be `pw=440, ph=380` - three literals - while everything inside it was a runtime
   stack of rows at 26px steps. Sixteen rows, a seed line, a rule, a RECORDS heading and four more rows
   come to a last baseline of y 534 against a card bottom of y 512, so "Dungeons cleared" was printed
   on the wood BELOW the paper. Twenty-two pixels of overflow, and nothing anywhere was wrong: the
   arithmetic was correct and the box was a guess.

   That is the whole failure mode - a height that is a constant beside content that is computed. It
   cannot be caught by reading, it survives every test that does not measure the last row against the
   card, and it comes straight back the moment somebody adds a statistic. So the rows are collected
   first, their extent is measured, and the card is sized to fit them.

   The constants are now named rather than scattered through the function, because five different
   increments (26, 20, 26, 24, and the 2px rule) inside one draw call is a layout nobody can change
   without re-deriving it, and this card will be changed again. */
const SUMMARY_MARGIN_X=36,   // inner text margin from each edge of the card
      SUMMARY_ROW_H=26,      // one statistic's baseline to the next
      SUMMARY_HEAD_GAP=34,   // air above the first row, inside the top of the card
      SUMMARY_FOOT_GAP=18,   // air below the last row, inside the bottom of the card
      SUMMARY_PAD=10,        // paper inset inside the wood
      /* Air AROUND a structural break rather than inside it. The seed gets a little because it is a
         different kind of line and the gap is what says so; the rule and the RECORDS heading get a
         lot because they separate two groups and the card would read as one long list without it.

         The first version of this drew the heading at y+RULE_GAP+14 while giving its item only
         RULE_GAP*2+2 of height, so the heading's baseline fell 6px past the end of its own slot and
         printed through the first record row. The fix is structural rather than a bigger number:
         every item now draws against the y it is given, and its height is the air plus the text. */
      SUMMARY_SEED_AIR=10,
      SUMMARY_BREAK_AIR=16;

/* One statistic, or a labelled break in the list. `row` returns the height it wants; the caller sums
   them and the card is built to that total, so a row can be added, removed or re-worded without
   anybody touching a pixel measurement. */
function summaryLayout(s){
  const items=[];
  const row=(label,value,isNew)=>{ items.push({kind:'row',label:label,value:value,isNew:!!isNew,h:SUMMARY_ROW_H}); };
  // every item's height is the air it needs PLUS the text it draws, and it draws on the y it is
  // given. Nothing is positioned by an offset that its own height does not account for.
  const head=()=>{ items.push({kind:'head',h:SUMMARY_BREAK_AIR+13}); };
  const rule=()=>{ items.push({kind:'rule',h:SUMMARY_BREAK_AIR+2}); };

  /* The floor comes FIRST on the sheet. It is the number the run is about and the one the record is
     measured in; everything below it is detail about how the run went rather than how far it got. */
  row('Floor reached',String(s.floor),s.newDepth);
  row('Time on this floor',fmtTime(s.floorTicks||0));
  row('Time',fmtTime(s.ticks),s.newFastest);
  row('Rooms explored',s.explored+' / '+s.total,s.newRooms);
  row('Enemies defeated',String(s.kills));
  row('Damage taken',fmtHearts(s.dmgTaken));
  row('Accuracy',s.shots?Math.round(100*s.hits/s.shots)+'% ('+s.hits+'/'+s.shots+')':'no shots fired');
  row('Weapon',s.weapon);
  // the seed is tracked out like a serial number rather than set like the rest of the sheet, because
  // it is the one line here the reader is expected to copy out character by character - and because
  // it is what turns "that went well" into an argument somebody else can check
  items.push({kind:'seed',label:'Seed',value:s.seed||Rnd.seedText,h:SUMMARY_SEED_AIR+18});
  rule();
  head();
  row('Deepest floor',String(records.deepest));
  row('Best rooms explored',String(records.rooms));
  /* TWO ROWS THAT COULD NOT EVER CHANGE, REPLACED BY TWO THAT DO.

     "Fastest clear" and "Dungeons cleared" were written when killing the boss ended the run, and they
     printed `records.fastest` and `records.wins`. Nothing increments those any more - a cleared boss
     room opens a way out and the run continues - so `endRun(true)` has no callers left and both rows
     printed 'not yet' and '0' on every summary in the game, forever. A record the player cannot move
     is not a record, and a permanent zero reads as "you have done nothing" rather than "this line is
     from a version that no longer exists".

     What replaces them is what THIS run did, which is the question the rest of the card is answering:
     how far you got, how much of the floor you saw, how many bodies you killed, and how well you shot.
     Accuracy is shown as a percentage rather than as a ratio of two numbers the reader has to divide. */
  row('Bodies killed',String(s.kills));
  row('Accuracy',s.shots>0?Math.round(s.hits/s.shots*100)+'%':'-');

  // the card is as tall as the list, plus the air at the top and the foot. Nothing about this number
  // is a design decision, which is the point.
  const contentH=items.reduce((a,it)=>a+it.h,0);
  const h=contentH+SUMMARY_HEAD_GAP+SUMMARY_FOOT_GAP;
  return {items:items,h:h};
}

function drawRunSummary(){
  const s=lastRun; if(!s) return;
  ctx.fillStyle='rgba(0,0,0,0.72)';ctx.fillRect(0,0,W,H);
  ctx.textAlign='center';ctx.font='bold 34px monospace';ctx.fillStyle=s.won?'#5ee27a':'#ff6b6b';

  const lay=summaryLayout(s);
  const pw=440, ph=lay.h;
  /* The card is CENTRED VERTICALLY on whatever space is left under the title, rather than placed at a
     fixed y. A fixed y worked while ph was a constant and stops working the moment the content is a
     variable - and a card that is 440px wide on a 960px canvas but 500px tall on a 700px canvas will
     run off the bottom if its top is a literal. Both edges are now computed from the measured height,
     so a longer card grows in both directions and stays inside the canvas. */
  const titleH=108;
  const available=H-titleH-52;           // below the title, above the prompt
  const px=(W-pw)/2, py=titleH+Math.max(0,(available-ph)/2);
  const L0=px+SUMMARY_MARGIN_X, R0=px+pw-SUMMARY_MARGIN_X;
  ctx.fillText(s.won?'DUNGEON CLEARED':'YOU DIED',W/2,titleH-24);

  ctx.drawImage(woodPlate(pw,ph),px,py);
  drawInset(px+8,py+8,pw-16,ph-16,null);
  ctx.drawImage(paperTex(pw-SUMMARY_PAD*2,ph-SUMMARY_PAD*2),px+SUMMARY_PAD,py+SUMMARY_PAD);

  let y=py+SUMMARY_HEAD_GAP;
  for(const it of lay.items){
    if(it.kind==='row'){
      ctx.textAlign='left';ctx.font='15px monospace';ctx.fillStyle=INK;ctx.fillText(it.label,L0,y);
      ctx.textAlign='right';ctx.font='bold 15px monospace';ctx.fillText(it.value,R0,y);
      if(it.isNew){
        const vw=ctx.measureText(it.value).width;
        ctx.textAlign='right';ctx.font='bold 11px monospace';ctx.fillStyle=INK_NEW;
        ctx.fillText('NEW',R0-vw-10,y-1);
      }
    } else if(it.kind==='seed'){
      ctx.textAlign='left';ctx.font='15px monospace';ctx.fillStyle=INK;ctx.fillText(it.label,L0,y);
      stampRight(ctx,it.value,R0,y,15,INK,2.6);
    } else if(it.kind==='rule'){
      ctx.fillStyle='rgba(90,60,30,0.35)';
      ctx.fillRect(L0,y+SUMMARY_BREAK_AIR,R0-L0,2);
    } else if(it.kind==='head'){
      ctx.textAlign='left';ctx.font='bold 12px monospace';ctx.fillStyle=INK_SOFT;
      ctx.fillText('RECORDS',L0,y);
    }
    y+=it.h;
  }

  ctx.textAlign='center';ctx.font='16px monospace';ctx.fillStyle='#fff';
  // "press R for a new dungeon" was correct when a dungeon was the whole game. The run now ends only
  // by dying, and what R starts is a fresh run at floor 1 - which is a different thing to ask for and
  // worth saying plainly, because the player has just spent an hour going down and the prompt used
  // to imply that was the shape of a completed run.
  ctx.fillText(s.won?'press R to descend again':'press R to try again',W/2,Math.min(H-14,py+ph+34));
  ctx.textAlign='left';
}

function render(){
  ctx.clearRect(0,0,W,H);
  if(state==='start'){
    ctx.fillStyle='#12141a';ctx.fillRect(0,0,W,H);
    drawOverlay('DEPTHS','click or press any key to descend',recordsLine());
    drawStartSeed();
  } else {
    /* THE CAMERA, applied to the ROOM AND NOTHING ELSE.

       The transform is inside a save/restore pair that closes before the HUD, so the HUD, the
       minimap and every overlay draw in screen space exactly as they always did. A HUD that scrolled
       with the room would be a HUD that walks off the corner of a big room, and a minimap that
       scrolled would be a minimap of a window rather than of the floor.

       For every room that fits on screen this translate is the identity, because the camera clamps
       to the room's own origin. That is deliberate: it means the camera can be added to a game that
       is already green without any of its existing numbers moving, and the only thing being tested
       on day one is a transform that is currently a no-op.

       It is computed HERE, immediately before it is used, rather than in the tick. That is the
       lesson of every stale-derived-value bug in this file: a value updated somewhere else is a
       value that can be wrong at the moment it matters, and the first symptom is a one-frame
       offset nobody can reproduce. Computing it at the point of use makes it impossible to be stale,
       and it is one subtraction and two clamps. */
    updateCamera();
    ctx.save();
    ctx.translate(-cam.x,-cam.y);
    drawRoom();
    ctx.restore();
    drawHUD();
    // the lab's legend is screen space, so it is drawn out here with the HUD rather than in the
    // room pass - see Lab.drawLegend for why it does not scroll with the world
    Lab.drawLegend();
    if(roomFade>0){ctx.fillStyle='rgba(0,0,0,'+roomFade+')';ctx.fillRect(0,0,W,H);}
    drawBossWarning();   // over the fade, so a room transition cannot swallow the warning
    drawDescent();       // also over the fade, and for the same reason: the fade is what it is drawn on
    // the bench is drawn over everything, after the fade and the boss warning, because it is the one
    // thing that has to be readable at any moment - and it carries its own dim, so the PAUSED
    // overlay beneath it would be a second dim layer and two of those read as a rendering fault
    if(devOpen) drawDevMenu();
    if(state==='gameover'||state==='win') drawRunSummary();
    // no canvas PAUSED overlay while the character sheet is up: the sheet's own backdrop already dims
    // the whole screen, and two dim layers stacked reads as a rendering fault rather than a pause
    else if(paused&&!uiSheetOpen&&!devOpen) drawOverlay('PAUSED','Esc / P or click to resume','R restarts this run · time '+fmtTime(run.ticks));
  }
  if(showPerf&&perfSamples.length){
    let sum=0,ups=0,worst=0;
    for(const p of perfSamples){sum+=p.dt;ups+=p.n;worst=Math.max(worst,p.dt);}
    ctx.fillStyle='#0d0e13';ctx.fillRect(W/2-120,4,240,20);
    ctx.fillStyle=worst>25?'#ff6b6b':'#5ee27a';ctx.font='12px monospace';ctx.textAlign='center';
    ctx.fillText('fps '+Math.round(1000*perfSamples.length/sum)+' · ups '+Math.round(1000*ups/sum)+' · worst '+Math.round(worst)+'ms',W/2,18);
    ctx.textAlign='left';
  }
}

// run as many fixed TICK_HZ ticks as the elapsed time covers; returns how many ran
function advance(dt){
  // panel the player READS, and a fight that keeps running underneath numbers they are trying to
  // read is a fight they lose for having tried to understand the weapon. It is deliberately not
  // routed through setPaused, because that opens the character sheet and two cards stacked is worse
  // than either alone.
  if(paused||devOpen){acc=0;return 0;}
  acc+=Math.min(dt,MAX_CATCHUP_MS);
  let n=0;
  while(acc>=STEP_MS-STEP_TOL){update();acc-=STEP_MS;n++;}
  return n;
}
function loop(now){
  const dt=lastT?now-lastT:STEP_MS;
  lastT=now;
  const n=advance(dt);
  perfSamples.push({dt,n}); if(perfSamples.length>120) perfSamples.shift();
  render();
  requestAnimationFrame(loop);
}

