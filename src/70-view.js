/* ==============================================================================================
   70-view  -  everything drawn

   The part Unity replaces wholesale. Read it for BEHAVIOUR, not for structure: what has to be
   legible, what must read in a glance, what the tells are. None of it ports; all of it is
   specification for the replacement.
   ============================================================================================== */
// where a padlock hangs for each side: just outside the frame
function c0(d){ return {N:[MIDX,ROOM_TOP-8],S:[MIDX,ROOM_BOTTOM+8],W:[ROOM_LEFT-8,MIDY],E:[ROOM_RIGHT+8,MIDY]}[d]; }
function drawFloor(type){
  let c=floorCache[type];
  if(!c){
    c=floorCache[type]=document.createElement('canvas');
    c.width=ROOM_RIGHT-ROOM_LEFT; c.height=ROOM_BOTTOM-ROOM_TOP;
    const g=c.getContext('2d'), cx=c.width/2, cy=c.height/2;
    g.fillStyle=g.createPattern(caveCanvas,'repeat');
    g.fillRect(0,0,c.width,c.height);
    g.globalAlpha=0.34;
    g.fillStyle=ROOM_BG[type]||'#191b22';
    g.fillRect(0,0,c.width,c.height);
    g.globalAlpha=1;
    const grad=g.createRadialGradient(cx,cy,40,cx,cy,420);
    grad.addColorStop(0,'rgba(255,255,255,0.05)');
    grad.addColorStop(1,'rgba(0,0,0,0.32)');
    g.fillStyle=grad;
    g.fillRect(0,0,c.width,c.height);
  }
  ctx.drawImage(c,ROOM_LEFT,ROOM_TOP);
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

  const wt=16;
  ctx.fillStyle='#2b2f3a';
  ctx.fillRect(ROOM_LEFT-wt,ROOM_TOP-wt,ROOM_RIGHT-ROOM_LEFT+wt*2,wt);
  ctx.fillRect(ROOM_LEFT-wt,ROOM_BOTTOM,ROOM_RIGHT-ROOM_LEFT+wt*2,wt);
  ctx.fillRect(ROOM_LEFT-wt,ROOM_TOP-wt,wt,ROOM_BOTTOM-ROOM_TOP+wt*2);
  ctx.fillRect(ROOM_RIGHT,ROOM_TOP-wt,wt,ROOM_BOTTOM-ROOM_TOP+wt*2);
  const bg=ROOM_BG[r.type];
  const dcolFor=d=>!doorOpen(r)?'#c93b3b':leadsToBoss(r,d)&&!bossUnlocked?'#ffd23d':leadsToItem(r,d)&&!itemUnlocked?'#d8dee9':'#5ee27a';
  if(r.doors.N){ctx.fillStyle=bg;ctx.fillRect(MIDX-DOORW/2,ROOM_TOP-wt,DOORW,wt);ctx.fillStyle=dcolFor('N');ctx.fillRect(MIDX-DOORW/2,ROOM_TOP-wt,DOORW,4);}
  if(r.doors.S){ctx.fillStyle=bg;ctx.fillRect(MIDX-DOORW/2,ROOM_BOTTOM,DOORW,wt);ctx.fillStyle=dcolFor('S');ctx.fillRect(MIDX-DOORW/2,ROOM_BOTTOM+wt-4,DOORW,4);}
  if(r.doors.W){ctx.fillStyle=bg;ctx.fillRect(ROOM_LEFT-wt,MIDY-DOORW/2,wt,DOORW);ctx.fillStyle=dcolFor('W');ctx.fillRect(ROOM_LEFT-wt,MIDY-DOORW/2,4,DOORW);}
  if(r.doors.E){ctx.fillStyle=bg;ctx.fillRect(ROOM_RIGHT,MIDY-DOORW/2,wt,DOORW);ctx.fillStyle=dcolFor('E');ctx.fillRect(ROOM_RIGHT+wt-4,MIDY-DOORW/2,4,DOORW);}

  /* the boss gate. rather than a glow pasted over the frame, it is a portcullis of slats across
     the gap: the shutter breathes while it is shut, and it visibly lifts as the lock works, so you
     can read what state the door is in from across the room */
  for(const d of ['N','S','E','W']){
    if(!r.doors[d]||!leadsToBoss(r,d)||bossUnlocked) continue;
    const horiz=d==='N'||d==='S', p=doorPoint(d);
    const x0=horiz?MIDX-DOORW/2:ROOM_LEFT-wt, y0=horiz?ROOM_TOP-wt:ROOM_BOTTOM-wt;
    const gate={N:[x0,ROOM_TOP-wt,DOORW,wt],S:[x0,ROOM_BOTTOM,DOORW,wt],W:[ROOM_LEFT-wt,y0,wt,DOORW],E:[ROOM_RIGHT,y0,wt,DOORW]}[d];
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
      const p=doorPoint(d);
      ctx.globalAlpha=0.3+0.4*prog;
      ctx.fillStyle=col; ctx.fillRect(p[0]-DOORW/2,p[1]-4,DOORW,8);
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
    ctx.globalAlpha=0.35*f.life/DASH_TRAIL;
    ctx.drawImage(DASH_GLOW,f.x-14,f.y-14);
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
    const barY=e.y-e.bar;
    ctx.fillStyle='#000';ctx.fillRect(e.x-e.r,barY,e.r*2,4);
    ctx.fillStyle='#5ee27a';ctx.fillRect(e.x-e.r,barY,e.r*2*(e.hp/e.maxHp),4);
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

  const a=Math.atan2(mouse.y-player.y,mouse.x-player.x);
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
  r.spawnPlan.forEach((s,i)=>{
    const e=r.enemies[i];
    ctx.strokeStyle=e&&e.alerted?'#ff6b6b':'rgba(94,226,122,0.8)';
    ctx.beginPath();ctx.arc(s.x,s.y,e?e.r:12,0,7);ctx.stroke();
    if(e&&e.type!=='lunger'&&e.type!=='boss'){
      ctx.strokeStyle='rgba(255,106,106,0.28)';
      ctx.beginPath();ctx.moveTo(e.x,e.y);ctx.lineTo(s.x,s.y);ctx.stroke();
      ctx.fillStyle='rgba(255,106,106,0.9)';
      ctx.fillText(e.type+' '+Math.round(s.d)+'px',s.x+8,s.y-8);
    }
  });
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
  const MARGIN_X=15, MARGIN_Y=14, FRAME=6, GAP=1;
  const HP_H=40, ROW_H=30, KEY_W=62, KEY_H=34, BLINK_W=150;
  // the heart plate grows with maxHp, so everything that has to line up with it is measured after it
  const hearts=Math.ceil(player.maxHp/2), armorSlots=Math.ceil(MAX_ARMOR/2);
  const healthW=30+(hearts+armorSlots)*26;
  const hx=MARGIN_X, hy=MARGIN_Y;

  // row 1: health, with the keys on its right shoulder, top-aligned and touching
  ctx.drawImage(woodPlate(healthW,HP_H),hx,hy);
  drawInset(hx+FRAME,hy+FRAME,healthW-FRAME*2,HP_H-FRAME*2,'#241610');
  // The hearts are CENTRED in the inset, not started from a hardcoded 36. The old version measured
  // the left margin and the right margin with two different expressions, and the armour slots added
  // four more pixels on the right on top of that, so the last heart always sat closer to the frame
  // than the first did. One expression for the first slot, and the whole row follows from it.
  const insetX=hx+FRAME, insetW=healthW-FRAME*2, heartSlotW=26;
  const rowW=(hearts+armorSlots)*heartSlotW;
  const slotX=insetX+Math.floor((insetW-rowW)/2)+13;   // +13: the sprite is 26 wide, origin is its left edge
  const heartY=hy+HP_H/2;
  for(let i=0;i<hearts;i++){
    // each heart is worth 2hp, so the fill is this heart's share of one, clamped: a slot further
    // along the plate can have a large `val` and must still read as simply full
    const fill=Math.max(0,Math.min(1,(player.hp-i*2)/2));
    drawHeart(slotX+i*heartSlotW,heartY,fill,'red',3);
  }
  // The last point of life gets a halo so "one heart left" can never be misread as "none left".
  // It matters more than it sounds: at 1hp the plate shows one half heart and five empties, and
  // beside two dimmed armour slots that is easy to read as an empty plate. The pulse is the same one
  // the boss warning uses, and it only exists on the heart that still has blood in it - not on the
  // empties, which would make the whole plate look live.
  if(player.hp===1){
    const pulse=0.5+0.5*Math.sin(frameCount*0.16/SPEEDUP);
    ctx.save();
    ctx.globalAlpha=0.35+0.55*pulse;
    ctx.fillStyle='#e8395a';
    ctx.beginPath();
    ctx.arc(slotX,heartY,15+3*pulse,0,7);
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



  // the minimap hangs off the same right-hand margin as the left-hand one, so the two edges of the
  // HUD are the same distance from the screen and the whole block reads as one inset
  const cell=17,mapW=GRID*cell,pw=mapW+28,mx0=W-MARGIN_X-pw,my0=MARGIN_Y,mx=mx0+14,my=my0+14,ic=3;
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

  // The weapon row is three plates on one line: left hand, middle, right hand, touching, and glued
  // to the bottom of the map. The middle one is a placeholder and is meant to stay that way - it is
  // where a consumable goes, and a consumable wants to be a different shape from a weapon, so the
  // gap is reserved rather than filled. It is drawn as a real empty frame with the same wood and the
  // same border as its neighbours, because an undrawn gap between two plates reads as a mistake and
  // a drawn one reads as a slot. The map is exactly three slots wide to within a pixel, so the row
  // cannot be off-centre.
  const slotY=my0+pw+GAP, slotH=50, slotGap=2;
  const slotW=Math.floor((pw-slotGap*2)/3);
  const midX=mx0+slotW+slotGap, midW=pw-slotW*2-slotGap*2;
  const wp=WEAPONS[player.weaponIdx], alt=activeAlt();
  drawSlot(mx0,slotY,slotW,player.weaponIdx,'left',wp.color,1-player.cooldown/(player.cooldownMax||wp.cooldown));
  drawSlot(midX+Math.round((midW-slotW)/2),slotY,slotW,'empty','none',0);
  drawSlot(mx0+pw-slotW,slotY,slotW,player.altMode==='hook'?'hook':'alt','right',alt.color,1-player.altCooldown/(player.altCooldownMax||alt.cooldown));
  // labels centred under their own plate, on one baseline
  const labelY=slotY+slotH+14;
  ctx.font='12px monospace';
  ctx.textAlign='center';
  ctx.fillStyle='#e8dcc0';ctx.fillText(wp.name,mx0+slotW/2,labelY);
  ctx.fillStyle=alt.color;ctx.fillText(player.altMode==='hook'?'Hook':'Blast',mx0+pw-slotW/2,labelY);
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
function drawOverlay(title,sub,foot){
  ctx.fillStyle='rgba(0,0,0,0.72)';ctx.fillRect(0,0,W,H);
  ctx.fillStyle='#fff';ctx.textAlign='center';
  ctx.font='bold 34px monospace';ctx.fillText(title,W/2,270);
  ctx.font='16px monospace';ctx.fillText(sub,W/2,310);
  if(foot){ctx.font='13px monospace';ctx.fillStyle='#aaa';ctx.fillText(foot,W/2,340);}
  ctx.textAlign='left';
}
function recordsLine(){
  let s='best rooms explored: '+records.rooms;
  if(records.wins) s+=' · fastest clear: '+fmtTime(records.fastest)+' · dungeons cleared: '+records.wins;
  return s;
}
// TICK_HZ ticks = 1s; counted in integer tenths so rounding can never print 0:60.0
function fmtTime(ticks){
  const tenths=Math.floor(ticks*10/TICK_HZ), m=Math.floor(tenths/600), s=Math.floor(tenths%600/10);
  return m+':'+(s<10?'0':'')+s+'.'+tenths%10;
}
function fmtHearts(half){const h=half/2;return h+(h===1?' heart':' hearts');}

/* end-of-run summary: paper sheet on a wood plate, same materials as the HUD */
const INK='#3a2616', INK_SOFT='#7a6040', INK_NEW='#b3261e';
function drawRunSummary(){
  const s=lastRun; if(!s) return;
  ctx.fillStyle='rgba(0,0,0,0.72)';ctx.fillRect(0,0,W,H);
  ctx.textAlign='center';ctx.font='bold 34px monospace';ctx.fillStyle=s.won?'#5ee27a':'#ff6b6b';
  ctx.fillText(s.won?'DUNGEON CLEARED':'YOU DIED',W/2,108);
  const pw=440,ph=380,px=(W-pw)/2,py=132,L=px+36,R=px+pw-36;
  ctx.drawImage(woodPlate(pw,ph),px,py);
  drawInset(px+8,py+8,pw-16,ph-16,null);
  ctx.drawImage(paperTex(pw-20,ph-20),px+10,py+10);
  const row=(y,label,value,isNew)=>{
    ctx.textAlign='left';ctx.font='15px monospace';ctx.fillStyle=INK;ctx.fillText(label,L,y);
    ctx.textAlign='right';ctx.font='bold 15px monospace';ctx.fillText(value,R,y);
    if(isNew){const vw=ctx.measureText(value).width;ctx.font='bold 11px monospace';ctx.fillStyle=INK_NEW;ctx.fillText('NEW',R-vw-10,y-1);}
  };
  let y=py+46;
  row(y,'Time',fmtTime(s.ticks),s.newFastest); y+=26;
  row(y,'Rooms explored',s.explored+' / '+s.total,s.newRooms); y+=26;
  row(y,'Enemies defeated',String(s.kills)); y+=26;
  row(y,'Damage taken',fmtHearts(s.dmgTaken)); y+=26;
  row(y,'Accuracy',s.shots?Math.round(100*s.hits/s.shots)+'% ('+s.hits+'/'+s.shots+')':'no shots fired'); y+=26;
  row(y,'Weapon',s.weapon); y+=26;
  // the seed is tracked out like a serial number rather than set like the rest of the sheet,
  // because it is the one line here the reader is expected to copy out character by character -
  // and because it is what turns "that went well" into an argument somebody else can check
  ctx.textAlign='left';ctx.font='15px monospace';ctx.fillStyle=INK;ctx.fillText('Seed',L,y);
  stampRight(ctx,s.seed||Rnd.seedText,R,y,15,INK,2.6); y+=20;
  ctx.fillStyle='rgba(90,60,30,0.35)';ctx.fillRect(L,y,R-L,2); y+=26;
  ctx.textAlign='left';ctx.font='bold 12px monospace';ctx.fillStyle=INK_SOFT;ctx.fillText('RECORDS',L,y); y+=24;
  row(y,'Best rooms explored',String(records.rooms)); y+=26;
  row(y,'Fastest clear',records.fastest?fmtTime(records.fastest):'not yet'); y+=26;
  row(y,'Dungeons cleared',String(records.wins));
  ctx.textAlign='center';ctx.font='16px monospace';ctx.fillStyle='#fff';
  ctx.fillText(s.won?'press R for a new dungeon':'press R to try again',W/2,py+ph+36);
  ctx.textAlign='left';
}

function render(){
  ctx.clearRect(0,0,W,H);
  if(state==='start'){
    ctx.fillStyle='#12141a';ctx.fillRect(0,0,W,H);
    drawOverlay('DEPTHS','click or press any key to descend',recordsLine());
    drawStartSeed();
  } else {
    drawRoom(); drawHUD();
    if(roomFade>0){ctx.fillStyle='rgba(0,0,0,'+roomFade+')';ctx.fillRect(0,0,W,H);}
    drawBossWarning();   // over the fade, so a room transition cannot swallow the warning
    if(state==='gameover'||state==='win') drawRunSummary();
    // no canvas PAUSED overlay while the character sheet is up: the sheet's own backdrop already dims
    // the whole screen, and two dim layers stacked reads as a rendering fault rather than a pause
    else if(paused&&!uiSheetOpen) drawOverlay('PAUSED','Esc / P or click to resume','R restarts this run · time '+fmtTime(run.ticks));
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
  if(paused){acc=0;return 0;}
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

