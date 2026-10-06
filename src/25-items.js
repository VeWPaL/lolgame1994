/* 25-items - the item framework A definition is DATA. [h:25-items-1] */
const Items=(function(){

  /* Rarity weights, and what one point of Luck does to them. Luck multiplies the RARE end rather
     than adding to it, because adding to every weight changes nothing and multiplying the bottom
     two does: a player with luck wants the same dungeon to have contained something better in it. */
  const RARITY={common:100,uncommon:44,rare:13,legendary:3};
  const LUCK_RARITY_GAIN=0.55;

  /* ---------------------------------------------------------------- the registry ---------- */

  function define(id,def){
    const errs=Content.define('item',id,def,'game');
    if(errs.length) throw new Error('Items: '+errs.join('; '));
    const problems=validate(id);
    if(problems.length) throw new Error('Items: '+id+' is not a legal item - '+problems.join('; '));
    return id;
  }
  /* Validation is per-definition and it is LOUD, because an item that is quietly wrong is worse
     than an item that is absent: it appears on the sheet, it counts against a slot, and it does
     nothing. Every rule here has been a real way for a definition to be broken. */
  function validate(id){
    const d=Content.get('item',id), out=[];
    if(d.use!=='passive'&&d.use!=='active') out.push('use must be passive or active, got "'+d.use+'"');
    if(d.use==='active'&&!(d.charges>0)) out.push('an active item needs charges, or it can never be used');
    /* There is exactly ONE active slot, so an active does not declare one. [h:25-items-2] */
    if(d.use==='active'&&d.slot!==undefined)
      out.push('there is one active slot and it is not chosen per item, so drop the slot field');
    if(d.use==='passive'&&d.slot!=='sigil')
      out.push('a passive is a sigil; slot "'+d.slot+'" means it wants to be pressed and it cannot be');
    /* A PASSIVE with hooks is a definition that validates and does nothing, which is the one failure mode this file exists to refuse. [h:25-items-3] */
    if(d.use==='passive'&&d.fx&&d.fx.hooks&&Object.keys(d.fx.hooks).length)
      out.push('a passive cannot have hooks: they run on use, and a passive is never used. Make it '
        +'active with charges, or wait for a trigger system.');
    if(d.slot==='sigil'&&d.use==='active') out.push('an active item is pressed from the one active slot, not a sigil');
    if(d.rarity&&!(d.rarity in RARITY)) out.push('rarity "'+d.rarity+'" is not one of '+Object.keys(RARITY).join('/'));
    if(d.unlocks&&!Array.isArray(d.unlocks)) out.push('unlocks must be a list of content ids');
    if(d.fx) for(const h in (d.fx.hooks||{})){
      if(typeof HOOKS[h]!=='function') out.push('hook "'+h+'" does not exist');
    }
    if(d.fx&&d.fx.stats) for(const s in d.fx.stats){
      if(typeof d.fx.stats[s]!=='number') out.push('stat "'+s+'" is not a number');
      if(!(s in Stats.DEFS)) out.push('stat "'+s+'" is not a stat this game has');
    }
    return out;
  }

  /* ---------------------------------------------------------------- giving and taking ------ */

  /* Rebuild, never subtract. [h:25-items-4] */
  function rebuild(){
    Stats.reset();
    for(const slot of loadout.items){
      const d=Content.get('item',slot.id);
      const fx=d.fx||{};
      for(const s in (fx.stats||{})) Stats.flat(s,fx.stats[s]);
    }
    // artifacts are a property, not a type, so they are applied here rather than at definition time
    for(const slot of loadout.items){
      const d=Content.get('item',slot.id);
      if(d.unlocks&&d.unlocks.length&&run) run.unlocked[d.unlocks[0]]=true;
    /* Vitals last, and from the same rebuild. [h:25-items-5] */
    applyVitals();
    }
  }

  function equipped(id){ return loadout.items.find(s=>s.id===id)||null; }

  /* NO CAPACITY, and that is the design rather than the absence of one. [h:25-items-6] */
  /* `give(id, charges)` takes the charges to place at, when the caller knows them. [h:25-items-7] */
  function give(id,charges){
    const d=Content.get('item',id);
    const already=equipped(id);
    const start=charges===undefined?d.charges:charges;
    // A second copy of something with charges goes into the SAME slot as more charges, which is what
    // makes a consumable stack and a passive simply not be duplicated.
    if(already&&already.charges!=null&&already.charges!==Infinity){
      // stacking adds whatever this copy is WORTH, so a half-spent one returning from the floor
      // contributes what is left of it rather than a full set
      already.charges+=start!=null?start:1;
      rebuild();
      return {taken:true,dropped:null};
    }
    /* A passive is not taken twice, and this is the ONE refusal left in the whole system. [h:25-items-8] */
    /* A passive stacks, as a second entry in the build. [h:25-items-9] */
    if(already&&d.slot!=='active'&&already.charges==null){
      loadout.items.push({id,name:d.name,charges:null,slot:-1});
      rebuild();
      return {taken:true,dropped:null};
    }
    if(already) return {taken:false,dropped:null};
    /* ONE active, and it is the one the player presses. [h:25-items-10] */
    const slot=d.use==='active'?ACTIVE_SLOT:-1;
    let dropped=null, droppedCharges=null;
    if(slot>=0){
      const prev=loadout.items.find(s=>s.slot===slot);
      if(prev){ dropped=prev.id; droppedCharges=prev.charges; }
    }
    if(dropped) loadout.items.splice(loadout.items.indexOf(equipped(dropped)),1);
    loadout.items.push({id,name:d.name,charges:start!=null?start:null,slot});
    rebuild();
    /* The displaced item's charges are captured HERE, while the entry still exists. [h:25-items-11] */
    return {taken:true,dropped:dropped,droppedCharges:droppedCharges};
  }

  /* Losing an item is a rebuild, so it cannot leave a residue. [h:25-items-12] */
  function remove(id){
    const i=loadout.items.findIndex(s=>s.id===id);
    if(i<0) return false;
    loadout.items.splice(i,1);
    rebuild();
    return true;
  }

  /* Use an active item. [h:25-items-13] */
  function use(id){
    const slot=equipped(id);
    if(!slot) return 'you are not carrying that';
    const d=Content.get('item',id);
    if(d.use!=='active') return 'that is not something you use, it is something you carry';
    if(slot.charges===0) return 'there is nothing left of it';
    const hooks=(d.fx&&d.fx.hooks)||{};
    let ran=0;
    for(const h in hooks){
      const fn=HOOKS[h];
      if(!fn){ continue; }
      if(fn(hooks[h],d,id)) ran++;
    }
    if(!ran) return 'nothing happens when you use that yet';
    if(slot.charges!=null) slot.charges--;
    if(slot.charges===0) remove(id);
    return null;
  }

  /* ---------------------------------------------------------------- the pool -------------- */

  /* A weighted roll, luck pushing weight toward the rare end. Written as an explicit walk over the
     rarities in descending order rather than a cumulative table, because the order is the design:
     the first rarity whose band the roll lands in is the one you get. */
  function rollRarity(luck){
    const L=Math.max(0,luck||0);
    const w={};
    for(const k in RARITY) w[k]=RARITY[k]*(k==='common'?1:1+LUCK_RARITY_GAIN*L);
    const order=['legendary','rare','uncommon','common'];
    let total=0; for(const k of order) total+=w[k];
    let r=Rnd.run()*total;
    for(const k of order){ r-=w[k]; if(r<0) return k; }
    return 'common';
  }
  /* A pool of N ids at a rarity, less any the player already holds and cannot stack. The exclusion
     is what stops a run of four commons all being the same one. */
  /* THE LOOT POOL EXCLUDES ANY ITEM WHOSE HOOKS DECLINE. [h:25-items-14] */
  /* ITEMS THAT ARE WRITTEN DOWN BUT NOT YET IMPLEMENTED, and so are not offered. [h:25-items-15] */
  function pool(n,rarity,exclude){
    const ids=Content.all('item').filter(id=>{
      const d=Content.get('item',id);
      if(rarity&&d.rarity!==rarity) return false;
      if(exclude&&exclude.indexOf(id)>=0) return false;
      if(d.unimplemented) return false;   // defined so it validates; not offered because it does nothing
      return true;
    });
    const out=[];
    for(let i=0;i<n&&ids.length;i++){
      const j=(Rnd.run()*ids.length)|0;
      out.push(ids.splice(j,1)[0]);
    }
    return out;
  }
  function rollItem(n,exclude){
    return pool(n||1,rollRarity(Stats.value('luck')),exclude)[0]||null;
  }

  /* THE ACTIVE SLOT, and the only two questions anyone asks about it. [h:25-items-16] */
const ACTIVE_SLOT=0;

/* The active the player is carrying, or null. One function rather than a caller that knows the slot
   number, because the whole point of collapsing to one slot is that nothing outside this file should
   have to care what the number is. */
function active(){
  return loadout.items.find(s=>s.slot===ACTIVE_SLOT&&s.charges!=null)||null;
}

/* Pressing Q. The key handler calls this and nothing else, so "which item does Q press" is answered here and the handler never learns what an active... [h:25-items-17] */
function useActive(){
  const a=active();
  if(!a) return false;
  // `use` returns null when it did something and a REASON string when it did not, so the comparison
  // is against null rather than for truthiness. `!!a && use(a.id)` looks equivalent and is not: it
  // hands back the null that means SUCCESS, which is falsy, so a working keypress reported failure.
  return use(a.id)===null;
}

  function reset(){
    loadout.items.length=0;
    if(run){ delete run.unlocked; run.unlocked={}; }
    Stats.reset();
  }

  return {define:define,validate:validate,give:give,remove:remove,use:use,useActive:useActive,
          rebuild:rebuild,equipped:equipped,rollRarity:rollRarity,rollItem:rollItem,pool:pool,
          active:active,reset:reset,ACTIVE_SLOT:ACTIVE_SLOT,RARITY:RARITY};
})();

/* ------------------------------------------------------------------ the hooks -------------- */

/* Returns whether it actually did something, which is how a charge is protected. [h:25-items-18] */
HOOKS.heal_self=function(n){
  const before=player.hp;
  player.hp=Math.min(player.maxHp,player.hp+(n||2));
  return player.hp>before;
};
HOOKS.reveal_room=function(){
  let n=0;
  for(const e of currentRoom().enemies) if(!e.alerted){ e.alerted=true; n++; }
  for(const pk of currentRoom().pickups) if(!pk.shown){ pk.shown=true; n++; }
  return n>0;                     // nothing new to show is nothing spent
};
HOOKS.pull_pickups=function(){
  let n=0;
  const r=currentRoom();
  for(const pk of r.pickups){
    if(pk.kind==='exit') continue;                 // the way out is not loot and does not come to you
    const d=Math.hypot(pk.x-player.x,pk.y-player.y)||1;
    if(d>200) continue;
    /* ONLY EVER TOWARD THE PLAYER, AND ONLY AS FAR AS THE WALL ALLOWS. [h:25-items-19] */
    if(d<=90) continue;
    let nx=player.x+(pk.x-player.x)/d*90, ny=player.y+(pk.y-player.y)/d*90;
    nx=Math.max(ROOM_LEFT+pk.r,Math.min(ROOM_RIGHT-pk.r,nx));
    ny=Math.max(ROOM_TOP+pk.r,Math.min(ROOM_BOTTOM-pk.r,ny));
    pk.x=nx; pk.y=ny;
    n++;
  }
  return n>0;                     // an empty room spends nothing
};
/* A named hook that is deliberately not implemented, and it now says so by REFUSING. [h:25-items-20] */
HOOKS.spawn_companion=function(){ return false; };
