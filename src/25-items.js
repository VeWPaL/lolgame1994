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
    if(d.slot!=='sigil'&&!(Number.isInteger(d.slot)&&d.slot>=0))
      out.push('slot must be a non-negative whole number or "sigil", got "'+d.slot+'"');
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
    }
    // artifacts are a property, not a type, so they are applied here rather than at definition time
    for(const slot of loadout.items){
      const d=Content.get('item',slot.id);
      if(d.unlocks&&d.unlocks.length&&run) run.unlocked[d.unlocks[0]]=true;
    /* Vitals last, and from the same rebuild. Vigor is maximum health, and the character's health is
       a derived value like any other - which it was not, until this: maxHp was the literal 8 written
       into the player at spawn, so an item that raised Vigor appeared on the sheet and did nothing.
       Because rebuild() is the ONLY path by which a build changes, putting it here means there is
       no way to pick something up and forget to recompute the body it was picked up for. */
    applyVitals();
    }
  }

  function equipped(id){ return loadout.items.find(s=>s.id===id)||null; }

  /* NO CAPACITY, and that is the design rather than the absence of one.

     A build used to hold three active items and refuse a fourth, on the reasoning that a limit keeps
     a run's identity narrow. That is backwards for this game: the number of possible builds IS the
     content, and a cap is a statement that some combinations are less worth having. Nine sigils plus
     four actives is thirteen items and every one of them is reachable, so the space is as wide as the
     roster allows rather than as narrow as an inventory screen does.

     So there is no limit, and there is no `freeSlot`. An active does not go in "a free slot" - it goes
     in ITS slot, which is the key it answers to, and if something is already on that key then that
     thing is what gets displaced. Same deal a weapon gets: you take the new one and the old one is
     left where you found the new one.

     Which is why `give` reports what it displaced rather than just whether it succeeded. The floor
     needs the displaced id to drop it, and a `give` that returned a bare boolean would force the
     caller to go and look up what is on that key - a second reader of the same fact, in the one place
     where the two would drift. */
  function give(id){
    const d=Content.get('item',id);
    const already=equipped(id);
    // A second copy of something with charges goes into the SAME slot as more charges, which is what
    // makes a consumable stack and a passive simply not be duplicated.
    if(already&&already.charges!=null&&already.charges!==Infinity){
      already.charges+=d.charges!=null?d.charges:1;
      rebuild();
      return {taken:true,dropped:null};
    }
    // A passive is not taken twice, and this is the ONE refusal left in the whole system. It is not a
    // capacity limit and it is not an oversight: two Heavy Hands is just Weighted Rod with extra steps,
    // and the roster already has an item that IS two Heavy Hands. Stacking passives would blur the
    // difference between the cheap stat and the deliberate one, and the floor never offers a duplicate
    // anyway because the pool excludes what the player holds.
    if(already) return {taken:false,dropped:null};
    const slot=d.slot==='sigil'?-1:d.slot;
    let dropped=null;
    if(slot>=0){
      const prev=loadout.items.find(s=>s.slot===slot);
      if(prev) dropped=prev.id;
    }
    if(dropped) loadout.items.splice(loadout.items.indexOf(equipped(dropped)),1);
    loadout.items.push({id,name:d.name,charges:d.charges!=null?d.charges:null,slot});
    rebuild();
    return {taken:true,dropped:dropped};
  }

  /* Losing an item is a rebuild, so it cannot leave a residue. The charges go with it.

     The slot renumbering that used to live here is gone, and it had to go. It existed to compact a
     capped set - slots were handed out as "lowest free index", so closing a gap meant renumbering the
     rest. Now a slot is a DECLARED key rather than an allocation, so renumbering on removal does not
     compact anything: drop a Bone Whistle on key 1 and a Hunter's Mark on key 2 would slide down to
     key 1 and answer to a key it was never written for. That bug was live before this change too -
     it just could not fire, because removal had no caller outside the tests. */
  function remove(id){
    const i=loadout.items.findIndex(s=>s.id===id);
    if(i<0) return false;
    loadout.items.splice(i,1);
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
    if(run){ delete run.unlocked; run.unlocked={}; }
    Stats.reset();
  }

  return {define:define,validate:validate,give:give,remove:remove,use:use,rebuild:rebuild,
          equipped:equipped,rollRarity:rollRarity,rollItem:rollItem,pool:pool,
          activeInSlot:activeInSlot,reset:reset,RARITY:RARITY};
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
