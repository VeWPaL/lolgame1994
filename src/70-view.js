/* 70-view - everything drawn The part Unity replaces wholesale. [h:70-view-1] */
// where a padlock hangs for each side: just outside the frame
function c0(d){ return {N:[MIDX,ROOM_TOP-8],S:[MIDX,ROOM_BOTTOM+8],W:[ROOM_LEFT-8,MIDY],E:[ROOM_RIGHT+8,MIDY]}[d]; }

/* THE GAP IN THE WALL FOR EACH SIDE, as [x,y,w,h]. [h:70-view-2] */
function doorRect(d,wt){
  return d==='N'?[MIDX-DOORW/2,ROOM_TOP-wt,DOORW,wt]
       : d==='S'?[MIDX-DOORW/2,ROOM_BOTTOM,DOORW,wt]
       : d==='W'?[ROOM_LEFT-wt,MIDY-DOORW/2,wt,DOORW]
       :         [ROOM_RIGHT,MIDY-DOORW/2,wt,DOORW];
}
/* THE FLOOR, and the cache is keyed by SIZE as well as by type - which it has to be now that a room is not one shape. [h:70-view-3] */
function drawFloor(type){
  const w=roomW(), h=roomH(), pal=areaPalette();
  const key=type+':'+w+'x'+h+':'+pal.id;
  let c=floorCache[key];
  if(!c){
    c=floorCache[key]=document.createElement('canvas');
    c.width=w; c.height=h;
    const g=c.getContext('2d'), cx=c.width/2, cy=c.height/2;
    /* TWO TINTS, IN THIS ORDER, and the order is the design. [h:70-view-4] */
    g.fillStyle=g.createPattern(caveTile(pal.id),'repeat');
    g.fillRect(0,0,c.width,c.height);
    g.globalAlpha=0.34;
    g.fillStyle=ROOM_BG[type]||'#191b22';
    g.fillRect(0,0,c.width,c.height);
    if(pal.wash>0){
      g.globalAlpha=pal.wash;
      g.fillStyle=pal.floorTint;
      g.fillRect(0,0,c.width,c.height);
    }
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
/* THE FLOOR CACHE IS EMPTIED HERE, BECAUSE ITS PIXELS BELONG TO A RUN AND NOT TO A SESSION. [h:70-view-5] */
function clearFloorCache(){
  for(const k of Object.keys(floorCache)) delete floorCache[k];
}
/* ALL THREE RUN-DEPENDENT ART CACHES, in the one call the run lifecycle should have to make. [h:70-view-6] */
function clearArtCaches(){
  clearFloorCache();
  clearTileCaches();
}
/* THE WALL, as four bands of one masonry pattern anchored to the room's own corner. [h:70-view-7] */
function drawWall(pal,wt){
  ctx.save();
  ctx.translate(ROOM_LEFT-wt,ROOM_TOP-wt);
  ctx.fillStyle=ctx.createPattern(wallTile(pal.id),'repeat');
  const iw=ROOM_RIGHT-ROOM_LEFT+wt*2, ih=ROOM_BOTTOM-ROOM_TOP+wt*2;
  ctx.fillRect(0,0,iw,wt);                    // north
  ctx.fillRect(0,ih-wt,iw,wt);               // south
  ctx.fillRect(0,wt,wt,ih-wt*2);             // west
  ctx.fillRect(iw-wt,wt,wt,ih-wt*2);         // east
  ctx.restore();
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
  /* The hook's ground spell. [h:70-view-8] */
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
  /* The lab's furniture goes over the floor and under the bodies: [h:70-view-9] */
  Lab.draw();

  const wt=16;
  drawWall(areaPalette(),wt);
  /* THE DOORWAY IS FILLED WITH THE COMPOSITED ROOM TINT, not the bare type tint, because it is a hole through the wall and what shows through it has... [h:70-view-10] */
  const bg=roomTone(r.type,areaPalette().id);
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
    /* ONE source of truth for where the gap is. [h:70-view-11] */
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
      /* The sweep runs ALONG the gap, not across the wall. [h:70-view-12] */
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
    /* The player's own blink, tinted by the meter it was spent at. [h:70-view-13] */
    const glow=(f.mine&&f.mom!==undefined)?momentumGlow(f.mom):DASH_GLOW;
    ctx.globalAlpha=0.35*f.life/DASH_TRAIL;
    ctx.drawImage(glow,f.x-14,f.y-14);
    ctx.globalAlpha=1;
  }
  /* The committed line. [h:70-view-14] */
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
    /* The floating bar over a body - every body EXCEPT the boss. [h:70-view-15] */
    if(e.type!=='boss'){
      const barY=e.y-e.bar;
      ctx.fillStyle='#000';ctx.fillRect(e.x-e.r,barY,e.r*2,4);
      ctx.fillStyle='#5ee27a';ctx.fillRect(e.x-e.r,barY,e.r*2*(e.hp/e.maxHp),4);
    }
    /* Stars over a held body. [h:70-view-16] */
    if(e.stun>0) drawStunStars(e.x,ey-e.r-6,e.stun,frameCount);
    /* The cast tell, drawn with the BODY and not with the projectiles. [h:70-view-17] */
    if(e.castT>0) drawCastFlash(e.x,e.y,e.castT,CAST_TIME,e.pcol);
  }
  /* The lab's damage readout, over the bodies and over the projectiles. [h:70-view-18] */
  Lab.drawNumbers();
  for(const p of projectiles){
    // the ring marks where the bolt will actually go off, so it has to be the radius of the weapon
    // in flight - otherwise the hook's circle would understate the ground it covers
    if(p.alt){const m=p.phase?HOOK_WEAPON:ALT_WEAPON;
      ctx.strokeStyle=p.color+'48';ctx.lineWidth=2;ctx.beginPath();ctx.arc(p.tx,p.ty,m.aoeRadius,0,7);ctx.stroke();ctx.globalAlpha=0.6;}
    if(p.friendly){
      /* A shrinking projectile is the gun telling you what it is worth before it lands. [h:70-view-19] */
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
  /* THE MARK IS DRAWN AT THE PLAN'S OWN POSITION, and the line goes to whatever body is standing there - NOT to `enemies[i]`. [h:70-view-20] */
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

/* THE ACTIVE PLATE. [h:70-view-21] */
/* The width of the label row, written by drawHUD each frame and read by the suite. Zero until the
   HUD has drawn once, which is why the test draws before it measures rather than trusting this. */
let BENCH_ROW_W=0;

/* ONE LABEL, SHRUNK TO FIT THE WIDTH IT IS GIVEN, and only truncated as a last resort. [h:70-view-22] */
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
  /* GUARDED, like every sibling. [h:70-view-23] */
  const def=Content.has('item',held.id)?Content.get('item',held.id):null;
  /* A CAPTION STRIP, because the first version drew Q and the count straight onto the tile and they landed on its bottom corners - a 34px tile in a... [h:70-view-24] */
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
    ctx.fillStyle=held.charges>0?((def&&def.color)||'#e8dcc0'):'#c85a5a';
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


/* THE HUD LAYOUT TABLE, hoisted out of drawHUD so the tests read these numbers instead of copying them. [h:70-view-25] */
const HUD_MARGIN_X=15, HUD_MARGIN_Y=14, HUD_FRAME=6, HUD_GAP=1;
const HUD_HP_H=40, HUD_ROW_H=30, HUD_KEY_W=62, HUD_KEY_H=34, HUD_BLINK_W=150, HUD_DEPTH_W=104;
const HUD_MOMENTUM_W=208;
/* THE TOP BAND, AND THE TOP OF THE PLATE BLOCK UNDER IT. [h:70-view-26] */
const HUD_TOP_BAND_H=15;
const HUD_BLOCK_Y=HUD_MARGIN_Y+HUD_TOP_BAND_H;

/* THE RAIL. Drawn first, so everything else in the HUD is on top of it, and drawn in screen space
   like the rest of the HUD - it is part of the interface, not of the room, and a band that scrolled
   with the camera would be a band you had to walk back to. */
function drawTopBand(){
  /* No drop shadow under the rail, and there was one for a while. [h:70-view-27] */
  ctx.drawImage(woodPlate(W,HUD_TOP_BAND_H),0,0);
}

/* WHERE THE BAR GOES, worked out from the room being drawn right now AND from the screen it is being drawn on. [h:70-view-28] */
function bossBarRect(){
  const bottom=canvas.height-BOSS_BAR_MARGIN;
  const span=Math.min(ROOM_RIGHT-ROOM_LEFT,canvas.width);
  const left=Math.max(0,Math.min(ROOM_LEFT,canvas.width-span));
  return {x:left+BOSS_BAR_INSET_X,
          y:bottom-BOSS_BAR_H,
          w:span-BOSS_BAR_INSET_X*2,
          h:BOSS_BAR_H};
}
/* THE BOSS THE BAR IS ABOUT, or null. [h:70-view-29] */
function liveBossInRoom(){
  const r=currentRoom();
  if(!r) return null;
  return r.enemies.find(b=>b.type==='boss'&&b.hp>0)||null;
}

/* THE WARDEN'S HEALTH, as the one piece of the fight that is fixed to the screen. [h:70-view-30] */
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
  /* The phase notches, drawn proud of the bar so crossing one does not erase it. [h:70-view-31] */
  for(const th of [BOSS_PHASE_1,BOSS_PHASE_2]){
    const nx=x+Math.round(w*th);
    ctx.fillStyle='#e8dcc0';
    ctx.fillRect(nx-1,y-4,2,h+8);
  }
  /* The name and the phase, ABOVE the bar. [h:70-view-32] */
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
  /* The HUD is one block, laid out from a single table of numbers so that nothing can drift. [h:70-view-33] */
  /* MARGIN is split by axis, because the two are not the same decision. [h:70-view-34] */
  const MARGIN_X=HUD_MARGIN_X, MARGIN_Y=HUD_MARGIN_Y, FRAME=HUD_FRAME, GAP=HUD_GAP;
  const HP_H=HUD_HP_H, ROW_H=HUD_ROW_H, KEY_W=HUD_KEY_W, KEY_H=HUD_KEY_H, BLINK_W=HUD_BLINK_W;
  /* The band owns the top of the screen, so the block starts under it rather than on the top margin. [h:70-view-35] */
  drawTopBand();
  const hy=HUD_BLOCK_Y;
  /* THE HEART PLATE GROWS WITH maxHp, AND maxHp HAS NO CEILING - so it needs a ceiling of its OWN. [h:70-view-36] */
  const hearts=Math.ceil(player.maxHp/2), armorSlots=Math.ceil(MAX_ARMOR/2);
  const heartSlot=26;
  /* the minimap plate starts here: W - MARGIN_X - (GRID*17 + 28), with a gutter of its own */
  const miniLeft=W-MARGIN_X-(GRID*17+28);
  /* HOW MANY HEARTS FIT, AND HOW MUCH ROOM IS LEFT OVER. [h:70-view-37] */
  const LABEL_W=26;
  const fitWithLabel=Math.max(4,Math.floor((miniLeft-HUD_GAP-MARGIN_X-30)/heartSlot)-armorSlots-1);
  const heartsWouldHide=hearts>Math.max(4,Math.floor((miniLeft-HUD_GAP-MARGIN_X-30)/heartSlot)-armorSlots);
  const heartsDrawn=Math.min(hearts,heartsWouldHide?fitWithLabel:Math.max(4,fitWithLabel+1));
  const heartsHidden=hearts-heartsDrawn;
  const labelW=heartsHidden>0?LABEL_W:0;
  const healthW=30+(heartsDrawn+armorSlots)*heartSlot+labelW;
  const hx=MARGIN_X;   // hy came from HUD_BLOCK_Y, above the heart budget

  // row 1: health, with the keys on its right shoulder, top-aligned and touching
  ctx.drawImage(woodPlate(healthW,HP_H),hx,hy);
  drawInset(hx+FRAME,hy+FRAME,healthW-FRAME*2,HP_H-FRAME*2,'#241610');
  /* The hearts are CENTRED in the inset, not started from a hardcoded 36. [h:70-view-38] */
  const insetX=hx+FRAME, insetW=healthW-FRAME*2, heartSlotW=heartSlot;
  const rowW=(heartsDrawn+armorSlots)*heartSlotW;
  const slotX=insetX+Math.floor((insetW-rowW)/2)+13;   // +13: the sprite is 26 wide, origin is its left edge
  const heartY=hy+HP_H/2;
  /* EACH SLOT IS ITS OWN INDEX, AND HEALTH DRAINS LEFT TO RIGHT. [h:70-view-39] */
  for(let i=0;i<heartsDrawn;i++){
    // each heart is worth 2hp, so the fill is this heart's share of one, clamped: a slot further
    // along the plate can have a large `val` and must still read as simply full
    const fill=Math.max(0,Math.min(1,(player.hp-i*2)/2));
    drawHeart(slotX+i*heartSlotW,heartY,fill,'red',3);
  }
  /* "+N" GOES AFTER THE LAST HEART, not before the first one. [h:70-view-40] */
  if(heartsHidden>0){
    ctx.font='bold 11px monospace';ctx.textAlign='left';ctx.fillStyle='#e8c9a0';
    ctx.fillText('+'+heartsHidden,slotX+heartsDrawn*heartSlotW+4,heartY+4);
    ctx.textAlign='left';
  }
  /* The last point of life gets a halo so "one heart left" can never be misread as "none left". [h:70-view-41] */
  /* "ONE HEART LEFT" MEANS ONE HEART ON THE PLATE, which is not the same as hp equal to one. [h:70-view-42] */
  if(Math.round((player.hp/2)*2)/2<=1&&player.hp>0){
    /* ON THE LAST HEART THAT STILL HAS BLOOD IN IT, which is not the last slot on the plate. [h:70-view-43] */
    let litLast=-1;
    for(let i=0;i<heartsDrawn;i++){ if(Math.max(0,Math.min(1,(player.hp-i*2)/2))>0) litLast=i; }
    const pulse=0.5+0.5*Math.sin(frameCount*0.16/SPEEDUP);
    ctx.save();
    ctx.globalAlpha=0.35+0.55*pulse;
    ctx.fillStyle='#e8395a';
    ctx.beginPath();
    // nothing is lit only if hp is 0, which the guard above already excludes, so litLast is >= 0 here
    ctx.arc(slotX+Math.max(0,litLast)*heartSlotW,heartY,15+3*pulse,0,7);
    ctx.fill();
    ctx.restore();
  }
  // an armour slot you have not filled and a key you are not carrying both draw as a darkened
  // version of the real thing rather than as nothing, so the frame reads as a full set of slots
  // instead of a ragged one. an empty slot still costs you nothing, it just stops looking like a hole
  for(let i=0;i<armorSlots;i++){
    /* on the SAME grid as the hearts, with no offset. [h:70-view-44] */
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
  /* The keys were the worst-aligned thing on the screen and both halves of it came from one mistake: [h:70-view-45] */
  const kInsetX=kx+FRAME, kInsetY=hy+FRAME, kInsetW=KEY_W-FRAME*2, kInsetH=KEY_H-FRAME*2;
  const keyW=15, KEY_PAD=5;
  const keyGap=kInsetW-KEY_PAD*2-keyW*2;
  const keyX=kInsetX+KEY_PAD+keyW/2, keyY=kInsetY+kInsetH/2;
  drawSprite(KEY_ROWS,player.hasSilver?KEY_SILVER_PAL:KEY_SILVER_DIM,keyX,keyY,1.5,null,player.hasSilver?'hudS':'hudSd');
  drawSprite(KEY_ROWS,player.hasGold?KEY_PAL:KEY_PAL_DIM,keyX+keyW+keyGap,keyY,1.5,null,player.hasGold?'hudG':'hudGd');

  /* row 2: the blink charges, glued to the bottom of the health plate and back to the width they were. [h:70-view-46] */
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

  /* THE MOMENTUM PLATE. [h:70-view-47] */
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

  /* The bar takes everything the label does not, and it takes a LOT of it. [h:70-view-48] */
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



  /* THE DEPTH PLATE. [h:70-view-49] */
  const dW=HUD_DEPTH_W, dy=by+ROW_H+HUD_GAP;
  ctx.drawImage(woodPlate(dW,ROW_H),hx,dy);
  drawInset(hx+FRAME,dy+FRAME,dW-FRAME*2,ROW_H-FRAME*2,'#1a1410');
  const dFloor=run?run.floor:1;
  ctx.save();
  ctx.textAlign='center'; ctx.textBaseline='middle';
  const dCx=hx+FRAME+Math.floor((dW-FRAME*2)*0.34), dCy=dy+ROW_H/2;
  // a struck numeral: the depth is a mark cut into the plate, not a label printed on it
  ctx.font='700 17px "Courier New",monospace';
  /* THE NUMERAL IS STRUCK IN THE AREA'S INK, and this is the whole of the HUD's palette. [h:70-view-50] */
  ctx.fillStyle='#0d0a08';
  ctx.fillText(String(dFloor),dCx+1,dCy+1);
  ctx.fillStyle=dFloor>1?areaPalette().accent:areaPalette().accentDim;
  ctx.fillText(String(dFloor),dCx,dCy);
  // the tally: one notch per floor, up to eight, then a count of the rest. A depth of forty should
  // look further than a depth of three without needing forty-one pixels of plate.
  const tX=hx+FRAME+Math.floor((dW-FRAME*2)*0.56), tY=dCy-6, tW=dW-FRAME*2-Math.floor((dW-FRAME*2)*0.56)-7;
  ctx.fillStyle='#0d0a08';
  for(let i=0;i<Math.min(8,dFloor);i++) ctx.fillRect(tX+i*3,tY,2,12);
  if(dFloor>8){
    ctx.font='9px "Courier New",monospace';
    ctx.fillStyle=areaPalette().accentDim;
    ctx.fillText('+'+(dFloor-8),tX+8*3+3,tY+7);
  }
  ctx.restore();
  ctx.textBaseline='alphabetic';

  /* THE MAP IS NOT DRAWN IN THE LAB, and the reason is that in the lab it would be a lie. [h:70-view-51] */
  // the minimap hangs off the same right-hand margin as the left-hand one, so the two edges of the
  // HUD are the same distance from the screen and the whole block reads as one inset
  const cell=17,mapW=GRID*cell,pw=mapW+28,mx0=W-MARGIN_X-pw,my0=HUD_BLOCK_Y,mx=mx0+14,my=my0+14,ic=3;
  if(state!=='dev'){
  ctx.drawImage(woodPlate(pw,pw),mx0,my0);
  drawInset(mx0+8,my0+8,pw-16,pw-16,null);
  ctx.drawImage(paperTex(pw-20,pw-20),mx0+10,my0+10);
  /* Knock the paper back so the map reads as a lit board in a dark room: [h:70-view-52] */
  ctx.fillStyle=withAlpha(areaPalette().mapWash,0.52);ctx.fillRect(mx0+10,my0+10,pw-20,pw-20);
  /* rooms next to somewhere you have been are shown as unlit shells: [h:70-view-53] */
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
    /* A boss you have walked into and not finished gets its own mark. [h:70-view-54] */
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

  /* The weapon row is three plates on one line: [h:70-view-55] */
  const slotY=my0+pw+GAP, slotH=50, slotGap=2;
  const slotW=Math.floor((pw-slotGap*2)/3);
  /* The row width the labels are fitted into, published so the test can assert against the value the drawing actually uses. [h:70-view-56] */
  BENCH_ROW_W=pw;
  const midX=mx0+slotW+slotGap, midW=pw-slotW*2-slotGap*2;
  const wp=WEAPONS[player.weaponIdx], alt=activeAlt();
  const held=Items.active();
  drawSlot(mx0,slotY,slotW,player.weaponIdx,'left',wp.color,1-player.cooldown/(player.cooldownMax||wp.cooldown));
  drawActivePlate(midX+Math.round((midW-slotW)/2),slotY,slotW,held);
  drawSlot(mx0+pw-slotW,slotY,slotW,player.altMode==='hook'?'hook':'alt','right',alt.color,1-player.altCooldown/(player.altCooldownMax||alt.cooldown));
  /* Only the ITEM is named on this row, and it is named across the whole row. [h:70-view-57] */
  const labelY=slotY+slotH+14;
  const itemColor=held?(Content.has('item',held.id)?(Content.get('item',held.id).color||'#e8dcc0'):'#e8dcc0'):'#6b5a44';
  const label=fitLabel(held?held.name:'nothing', pw-8);
  ctx.fillStyle=itemColor;
  ctx.fillText(label.text, mx0+pw/2, labelY);

  /* The Warden's bar, drawn here rather than in drawRoom so it sits with the other HUD plates and inherits the same frame. [h:70-view-58] */
  const warden=liveBossInRoom();
  if(warden) drawBossBar(warden);

  ctx.textAlign='left';
}

/* the seed tag ------------------------------------------------------------------------------ A luggage tag: [h:70-view-59] */
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
/* Right-aligned, for the sheet. [h:70-view-60] */
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
  /* sized off h rather than written in, so the tag cannot be resized without the type coming with it - which is how the number and the caption ended... [h:70-view-61] */
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
/* THE DESCENT BEAT. [h:70-view-62] */
function drawDescent(){
  if(descendT<=0) return;
  const t=descendT/FADE_DESCEND;
  // ease in and out so it arrives rather than appears, and is gone before control returns
  const a=t>0.5?(1-t)*2:t*2;
  const f=run?run.floor:1;
  ctx.save();
  ctx.textAlign='center';
  ctx.globalAlpha=Math.max(0,Math.min(1,a));
  /* THE SCRIM, and it is here because of the one thing the fade above cannot do. [h:70-view-63] */
  /* The band grew when the area name arrived at a boundary: [h:70-view-64] */
const scrimTop=H/2-92, scrimBot=H/2+68;
  const scrim=ctx.createLinearGradient(0,scrimTop,0,scrimBot);
  scrim.addColorStop(0,'rgba(5,4,3,0)');
  scrim.addColorStop(0.22,'rgba(5,4,3,0.90)');
  scrim.addColorStop(0.74,'rgba(5,4,3,0.90)');
  scrim.addColorStop(1,'rgba(5,4,3,0)');
  ctx.fillStyle=scrim;
  ctx.fillRect(0,scrimTop,W,scrimBot-scrimTop);
  /* THE PLAYER IS REDRAWN OVER THE SCRIM, and this is the fix for the thing the scrim itself caused. [h:70-view-65] */
  ctx.save();
  ctx.translate(-cam.x,-cam.y);   // the room pass draws the player in world space, under the camera
  {
    const fr2=player.anim>0?Math.floor(player.anim)%4:-1, frame2=fr2<0?1:fr2;
    const bob2=fr2<0?Math.round(Math.sin(frameCount*0.07/SPEEDUP)):((fr2&1)?-2:0);
    /* `readyProg` IS RECOMPUTED HERE, and it has to be. [h:70-view-66] */
    const readyProg2=readyT>0?1-readyT/READY:1;
    const hop2=readyT>0?-Math.sin(readyProg2*Math.PI)*7:0;
    const flick2=player.iframes>0&&((player.iframes/IFRAME_FLICKER)|0)%2===0;
    const oy2=player.y+bob2+hop2;
    /* The aim angle is recomputed rather than read from the room pass: [h:70-view-67] */
    const aim2=mouseWorld();
    const a2=Math.atan2(aim2.y-player.y,aim2.x-player.x);
    const behind2=Math.sin(a2)<-0.35;
    const glow2=readyT>0?0.5*readyProg2:0;
    ctx.globalAlpha=a*(flick2?0.35:1);
    let tip2;
    if(behind2) tip2=drawWand(player.x,oy2,a2,WEAPONS[player.weaponIdx].color,glow2);
    drawSprite(PLAYER_FRAMES[frame2],PLAYER_PAL,player.x,oy2-4,2,null,'player'+frame2);
    if(!behind2) tip2=drawWand(player.x,oy2,a2,WEAPONS[player.weaponIdx].color,glow2);
    ctx.globalAlpha=a;
    drawMuzzleFlash(tip2[0],tip2[1],player.muzzleTimer);
  }
  ctx.restore();
  /* THE FLOOR NUMBER IS STRUCK IN THE AREA'S ACCENT, like the depth numeral, so the beat and the plate agree about where you are. [h:70-view-68] */
  const fromArea=areaForFloor(descendFrom||f-1), toArea=areaForFloor(f);
  if(fromArea!==toArea){
    const an=Content.get('area',toArea);
    ctx.fillStyle=areaPalette().accent;
    ctx.font='13px monospace';
    /* INSIDE THE SCRIM, which is the whole difficulty of placing a fifth line. [h:70-view-69] */
    ctx.fillText(an.name,W/2,H/2-42);
  }
  ctx.fillStyle=areaPalette().accent;
  ctx.font='bold 44px monospace';
  ctx.fillText('FLOOR '+f,W/2,H/2-14);
  ctx.fillStyle='#8a7a62';
  ctx.font='14px monospace';
  // "deeper than you have been" was here first and it is a lie on the second descent - by floor 12
  // a returning player has plainly been deeper. The transition itself is always true, always says
  // something the player did not already know, and needs no assumption about their history.
  ctx.fillText('floor '+(descendFrom||f-1)+'  →  '+f,W/2,H/2+14);
  // the three levers, as ratios rather than percentages. "4.3x tougher" reads at a glance where
  // "330% tougher" needs the mental arithmetic, and a banner is not the place to ask for that.
  const t1=depthTough(), r1=depthRate();
  ctx.fillStyle='#6b6152';
  ctx.font='13px monospace';
  ctx.fillText('bodies '+t1.toFixed(1)+'x tougher  ·  they answer '+r1.toFixed(1)+
    'x faster  ·  rooms fuller',W/2,H/2+36);
  // a ruled line under it, struck like the depth plate, so the beat belongs to the same object.
  // withAlpha rather than a literal, so the rule under the number cannot be a different hue from
  // the number once there are four areas - and so Area1 still composites to the exact same string.
  ctx.strokeStyle=withAlpha(areaPalette().accent,0.45);
  ctx.lineWidth=1;
  ctx.beginPath();
  ctx.moveTo(W/2-150,H/2+50); ctx.lineTo(W/2+150,H/2+50);
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
  /* Depth first, because it is the number the game is about now. [h:70-view-70] */
  let s='deepest floor: '+records.deepest+' · best rooms explored: '+records.rooms;
  if(records.wins) s+=' · fastest clear: '+fmtTime(records.fastest)+' · dungeons cleared: '+records.wins;
  return s;
}
// TICK_HZ ticks = 1s; counted in integer tenths so rounding can never print 0:60.0
function fmtTime(ticks){
  const tenths=Math.floor(ticks*10/TICK_HZ), m=Math.floor(tenths/600), s=Math.floor(tenths%600/10);
  return m+':'+(s<10?'0':'')+s+'.'+tenths%10;
}
/* A DISPLAYED NUMBER, at the precision the reader can act on. [h:70-view-71] */
function showNum(v,dp){
  if(!isFinite(v)) return '—';
  const r=Math.round(v*Math.pow(10,dp===undefined?1:dp))/Math.pow(10,dp===undefined?1:dp);
  return String(r);
}

/* HEARTS, for the player. [h:70-view-72] */
function fmtHearts(half){
  let h=Math.round((half/2)*2)/2;         // nearest half heart, from half-hearts
  h=Math.round(h*10)/10;                   // kill float noise like 3.5000000000000004
  const s=String(h);
  return s+(h===1?' heart':' hearts');
}

/* end-of-run summary: paper sheet on a wood plate, same materials as the HUD */
const INK='#3a2616', INK_SOFT='#7a6040', INK_NEW='#b3261e';
/* the weapon bench (F1) ------------------------------------------------------------------------ A panel for swapping guns mid-playtest without... [h:70-view-73] */

function devLayout(){
  /* Every vertical position in the panel is a band measured from the top of the card, and the card is tall enough to hold them all with air between. [h:70-view-74] */
  const pw=608, ph=464, px=Math.round((W-pw)/2), py=Math.round((H-ph)/2);
  const rowH=58, rowX=px+26, rowW=pw-52;
  const head=py+34, sub=py+72, colHdr=py+112, listY=py+128;
  /* The footer band is measured from its TALLEST element, not its rule. [h:70-view-75] */
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
    /* THE CONE, IN DEGREES, ONCE. [h:70-view-76] */
    const spreadRad=w.spreadFromPrecision?preciseSpread(w.spread):w.spread;
    const deg=(spreadRad*2*180/Math.PI).toFixed(1);
    ctx.fillText('cone '+deg+'deg  ·  floor '+(w.fMin*100).toFixed(0)+'%  ·  '+
      (w.count>1?(w.count+' pellets'):'single hit')+(w.pierce?'  ·  pierces '+w.pierce:''),tx,y+35);
    /* STRENGTH IS ADDED ONCE PER SHOT, so this is base + str and not base + str*count. [h:70-view-77] */
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
    /* TIME TO KILL A LUNGER, AGAINST THE LUNGER THE GAME ACTUALLY HAS. [h:70-view-78] */
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

  /* footer: the two keys and a heal - IN THE LAB ONLY, because a playtest that has to be restarted every time you spend your charges is a playtest you... [h:70-view-79] */
  const fy=G.foot;   // the band devLayout budgeted, not a second guess at where the footer is
  ctx.fillStyle='rgba(90,60,30,0.35)';ctx.fillRect(L,fy-12,G.pw-52,1);
  ctx.font='11px monospace';
  const cap=(label,x,on)=>{const c=devCap(label,on);ctx.drawImage(c,x,fy+4);return x+c.width+5;};
  ctx.fillStyle=INK_SOFT;
  let x=L;
  if(state==='dev'){
    x=cap('G',x,player.hasGold);ctx.fillStyle=player.hasGold?'#ffd23d':INK_SOFT;
    ctx.fillText(player.hasGold?'gold key held':'give gold key',x,fy+17);x+=130;
    /* Y, NOT S. The silver key was on S, and S is DOWN - so in the lab the shortcut won and the
       player could not walk south, which is the bug this footer used to advertise as a feature. G
       and H are safe because neither is a movement key; S never was. */
    x=cap('Y',x,player.hasSilver);ctx.fillStyle=player.hasSilver?'#d8dee9':INK_SOFT;
    ctx.fillText(player.hasSilver?'silver key held':'give silver key',x,fy+17);x+=140;
    x=cap('H',x,false);ctx.fillStyle=INK_SOFT;
    ctx.fillText('refill heart and cooldowns',x,fy+17);
  } else {
    ctx.fillStyle=INK_SOFT;
    ctx.fillText('1-4 swap wand  ·  ESC or F1 close  ·  WASD still moves, the run is live  ·  F2 for the lab',x,fy+17);
  }

  ctx.textAlign='right';ctx.fillStyle=INK_SOFT;ctx.font='10px monospace';
  ctx.fillText('strength '+str+'   ·   tempo '+showNum(TEMPO.rate,2)+'x   ·   tick '+TICK_HZ+'Hz',R,fy+30);
  ctx.font='bold 11px monospace';ctx.fillStyle=INK;
  const c=devCap('ESC',false);ctx.drawImage(c,R-c.width,fy+44);
  ctx.fillStyle=INK_SOFT;ctx.font='10px monospace';
  ctx.fillText('or F1 to close',R-c.width-6,fy+57);
  ctx.textAlign='left';
}
/* hearts per second for a weapon at a range, under the current build. [h:70-view-80] */
function devDps(w,str,dist){
  const pulls=TICK_HZ/(w.cooldown/TEMPO.rate);
  /* `count*dmg + strength`, matching fireWeapon. This was `(dmg+strength)*count`, which is the
     per-pellet reading the game explicitly does not use. */
  return pulls*(w.dmg*w.count+str)*devFalloff(w,dist);
}
/* THE CARD'S SHAPE, decided by its contents rather than by a number someone wrote down. [h:70-view-81] */
const SUMMARY_MARGIN_X=36,   // inner text margin from each edge of the card
      SUMMARY_ROW_H=26,      // one statistic's baseline to the next
      SUMMARY_HEAD_GAP=34,   // air above the first row, inside the top of the card
      SUMMARY_FOOT_GAP=18,   // air below the last row, inside the bottom of the card
      SUMMARY_PAD=10,        // paper inset inside the wood
      /* Air AROUND a structural break rather than inside it. [h:70-view-82] */
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
  /* TWO ROWS THAT COULD NOT EVER CHANGE, REPLACED BY TWO THAT DO. [h:70-view-83] */

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
  /* The card is CENTRED VERTICALLY on whatever space is left under the title, rather than placed at a fixed y. [h:70-view-84] */
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
  /* "press R for a new dungeon" was correct when a dungeon was the whole game. [h:70-view-85] */
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
    /* THE CAMERA, applied to the ROOM AND NOTHING ELSE. [h:70-view-86] */
    updateCamera();
    ctx.save();
    ctx.translate(-cam.x,-cam.y);
    drawRoom();
    ctx.restore();
    drawHUD();
    /* The lab's legend is screen space, so it is drawn out here with the HUD rather than in the room pass - see Lab.drawLegend for why it does not... [h:70-view-87] */
    Lab.drawLegend();
    if(roomFade>0){ctx.fillStyle='rgba(0,0,0,'+roomFade+')';ctx.fillRect(0,0,W,H);}
    drawBossWarning();   // over the fade, so a room transition cannot swallow the warning
    drawDescent();       /* also over the fade, and for the same reason: [h:70-view-88] */
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
  /* panel the player READS, and a fight that keeps running underneath numbers they are trying to read is a fight they lose for having tried to... [h:70-view-89] */
  // An overlay that takes the player's input stops the fight too (Tab, H, the bug list, the seed
  // sheet): it used to block input while the room kept running, and a reader was hit for free.
  if(paused||devOpen||uiHoldsInput()){acc=0;return 0;}
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

