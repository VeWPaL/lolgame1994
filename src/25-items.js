/* =========================================================================================
   25-items  -  the item framework

   A definition is DATA. Six orthogonal fields, and the five names the design started from are
   PRESETS over them rather than categories of their own:

     use      'passive' | 'active'          does it need a keypress
     charges  integer, or Infinity          THIS IS consumable-vs-reusable. A potion is
                                            {active, charges:1}; a repeatable spell is
                                            {active, charges:Infinity}. They are not two
                                            things, and treating them as two guarantees every
                                            future item gets forced into whichever bin is
                                            nearer - which is how item systems grow hybrids.
     slot     integer, or 'sigil'           equipment slots, or free-floating
     fx       {stats,hooks,spawn}           the effect vocabulary
     unlocks  [contentIds]                  THIS IS artifact-ness. Not a type: an artifact is
                                            any item that also carries unlocks, so a consumable
                                            you burn to open a magic door for the rest of the
                                            run is a consumable artifact, and a passive that
                                            lets you SEE those doors is a passive one.
     rarity, tags                          pools, luck, and weapon mods

   THE PAYOFF OF DERIVED STATS. Removing an item does not subtract anything. Stats.reset() wipes
   the sheet and every remaining item is re-applied from base, so an item's contribution is exactly
   what the build says it is and there is no order for it to have got out of step in. That is why
   give/remove here are a rebuild rather than an inverse, and it is why the beam can read a stat at
   the moment of firing and be right.

   HOOKS are named, never inlined. A definition can say {hooks:{heal_self:2}} and a mod can do the
   same without any code existing in this file. A hook that is named but not implemented is a
   definition that validates and does nothing, loudly - which is the seam that lets the roster grow
   before the behaviour does. */
const Items=(function(){

  const SLOTS=3;
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
    if(d.slot!=='sigil'&&!(d.slot>=0&&d.slot<SLOTS)) out.push('slot must be a number 0..'+(SLOTS-1)+' or "sigil", got "'+d.slot+'"');
    /* A PASSIVE with hooks is a definition that validates and does nothing, which is the one failure
       mode this file exists to refuse. Hooks run when an item is USED, and a passive is never used,
       so its hooks would never fire - the item would appear on the sheet, take a space in the build,
       and be inert. Lantern Friend was exactly this: a companion written down before the trigger
       system that would summon it existed. The honest answer is to refuse it until there is a
       trigger to attach to, so an unimplemented companion has to be ACTIVE and cost a charge. */
    if(d.use==='passive'&&d.fx&&d.fx.hooks&&Object.keys(d.fx.hooks).length)
      out.push('a passive cannot have hooks: they run on use, and a passive is never used. Make it '
        +'active with charges, or wait for a trigger system.');
    if(d.slot==='sigil'&&d.use==='active') out.push('an active item needs a slot to be pressed from');
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

  /* Rebuild, never subtract. One pass over the build, from base, every time anything changes. It is
     a handful of additions over a list of about ten entries, it cannot drift, and it means the
     in-flight value the player is currently playing with is always exactly the value the sheet
     shows - which is the property the whole system was designed to have. */
  function rebuild(){
    Stats.reset();
    for(const slot of loadout.items){
      const d=Content.get('item',slot.id);
      const fx=d.fx||{};
      for(const s in (fx.stats||{})) Stats.flat(s,fx.stats[s]);
      if(fx.hooks&&fx.hooks.luck_spread) Stats.mult('luck',0);
    }
    // artifacts are a property, not a type, so they are applied here rather than at definition time
    for(const slot of loadout.items){
      const d=Content.get('item',slot.id);
      if(d.unlocks&&d.unlocks.length) run.unlocked[d.unlocks[0]]=true;
    }
  }

  function equipped(id){ return loadout.items.find(s=>s.id===id)||null; }

  /* Returns false when the build cannot take it, so the pickup stays on the floor rather than
     vanishing into a full inventory. A heart does the same thing when you are already full. */
  function give(id){
    const d=Content.get('item',id);
    const already=equipped(id);
    // a second copy of an item you already hold goes into the SAME slot as more charges, which is
    // what makes a consumable stack and a passive simply not be duplicated
    if(already&&already.charges!=null&&already.charges!==Infinity){
      already.charges+=d.charges!=null?d.charges:1;
      rebuild();
      return true;
    }
    if(already) return false;                       // a passive is not taken twice
    const slot=d.slot==='sigil'?-1:freeSlot();
    if(d.slot!=='sigil'&&slot<0) return false;      // every slot is full
    loadout.items.push({id,name:d.name,charges:d.charges!=null?d.charges:null,slot});
    rebuild();
    return true;
  }

  function freeSlot(){
    const used=loadout.items.map(s=>s.slot).filter(x=>x>=0);
    for(let i=0;i<SLOTS;i++) if(used.indexOf(i)<0) return i;
    return -1;
  }

  /* Losing an item is a rebuild, so it cannot leave a residue. The charges go with it. */
  function remove(id){
    const i=loadout.items.findIndex(s=>s.id===id);
    if(i<0) return false;
    loadout.items.splice(i,1);
    loadout.items.forEach((s,k)=>{ if(s.slot>=0) s.slot=k; });
    rebuild();
    return true;
  }

  /* Use an active item. The charge is spent FIRST, so a hook that throws cannot leave a free use
     behind, and the hook is looked up by name - a mod's item works without this file knowing it. */
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
      fn(hooks[h],d,id);
      ran++;
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
  function pool(n,rarity,exclude){
    const ids=Content.all('item').filter(id=>{
      const d=Content.get('item',id);
      if(rarity&&d.rarity!==rarity) return false;
      if(exclude&&exclude.indexOf(id)>=0) return false;
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

  /* Which of the player's active items answers a given key. Separate from `use` so a keypress can
     find its target without the caller knowing anything about slots. */
  function activeInSlot(slot){
    return loadout.items.find(s=>s.slot===slot&&s.charges!==null)||null;
  }

  function reset(){
    loadout.items.length=0;
    delete run.unlocked;
    run.unlocked={};
    Stats.reset();
  }

  return {define:define,validate:validate,give:give,remove:remove,use:use,rebuild:rebuild,
          equipped:equipped,rollRarity:rollRarity,rollItem:rollItem,pool:pool,
          activeInSlot:activeInSlot,reset:reset,SLOTS:SLOTS,RARITY:RARITY};
})();

/* ------------------------------------------------------------------ the hooks -------------- */

HOOKS.heal_self=function(n){ player.hp=Math.min(player.maxHp,player.hp+(n||2)); };
HOOKS.reveal_room=function(){
  for(const e of currentRoom().enemies) e.alerted=true;
  for(const pk of currentRoom().pickups) pk.shown=true;
};
HOOKS.pull_pickups=function(){
  for(const pk of currentRoom().pickups){
    if(pk.kind==='exit') continue;                 // the way out is not loot and does not come to you
    const d=Math.hypot(pk.x-player.x,pk.y-player.y)||1;
    if(d>200) continue;
    pk.x=player.x+(pk.x-player.x)/d*90;
    pk.y=player.y+(pk.y-player.y)/d*90;
  }
};
/* A named hook that is deliberately not implemented. It exists so that a definition using it
   VALIDATES - which is the seam that lets companions and the rest of the roster be written down
   before the behaviour behind them exists, and so a mod can reference it and get an honest "nothing
   happens yet" rather than a crash. */
HOOKS.spawn_companion=function(){ run.companionPending=(run.companionPending||0)+1; };
