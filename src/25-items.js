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
    /* There is exactly ONE active slot, so an active does not declare one. It used to: four actives
       carried slot 0, 1, 1 and 2, which made the number a second name for "a key", made two of the
       four collide on key 1, and left the roster's own blurbs disagreeing with it - Lantern Friend
       was slot 2 and said "Hold 2". A number that three places have to agree about, and did not, is
       the thing to delete rather than the thing to fix. */
    if(d.use==='active'&&d.slot!==undefined)
      out.push('there is one active slot and it is not chosen per item, so drop the slot field');
    if(d.use==='passive'&&d.slot!=='sigil')
      out.push('a passive is a sigil; slot "'+d.slot+'" means it wants to be pressed and it cannot be');
    /* A PASSIVE with hooks is a definition that validates and does nothing, which is the one failure
       mode this file exists to refuse. Hooks run when an item is USED, and a passive is never used,
       so its hooks would never fire - the item would appear on the sheet, take a space in the build,
       and be inert. Lantern Friend was exactly this: a companion written down before the trigger
       system that would summon it existed. The honest answer is to refuse it until there is a
       trigger to attach to, so an unimplemented companion has to be ACTIVE and cost a charge. */
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
  /* `give(id, charges)` takes the charges to place at, when the caller knows them.

     It is how a DISPLACED item comes back to the floor and then into the hand again without being
     restored to full. `give` builds every entry from the definition's `d.charges`, which is correct for
     a fresh pickup off the floor and wrong for one that was just taken off the floor carrying a
     half-spent Tin Cup - so the charges had to be able to travel with the item, and the only place they
     can live is on the pickup.

     Anything that does not say otherwise gets the definition's own number, which is what every other
     caller means. */
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
    // A passive is not taken twice, and this is the ONE refusal left in the whole system. It is not a
    // capacity limit and it is not an oversight: two Heavy Hands is just Weighted Rod with extra steps,
    // and the roster already has an item that IS two Heavy Hands. Stacking passives would blur the
    // difference between the cheap stat and the deliberate one, and the floor never offers a duplicate
    // anyway because the pool excludes what the player holds.
    /* A passive stacks, as a second entry in the build. `rebuild` applies each entry's stats on its own, so
     two Heavy Hands is +2 Strength without anything here needing to know that "stacking" is a thing.

     This used to refuse, on the argument that two Heavy Hands is just Weighted Rod with extra steps
     and the roster already has an item that IS two Heavy Hands. That argument was about the ROSTER
     being tidy and it was the wrong priority: it left the one case where a pickup on the floor does
     absolutely nothing, silently, which is the worst failure a loot room can have. The blur is real but
     it costs a player nothing - taking a second Heavy Hands gets them the same +2 they would have got
     from Weighted Rod, and it took a slot and a floor to do it.

     The loot pool still excludes what the player holds, so this is only reachable deliberately. That
     stays: an accidental duplicate in the pool would be a wasted offer, not a build choice. */
    if(already&&d.slot!=='active'&&already.charges==null){
      loadout.items.push({id,name:d.name,charges:null,slot:-1});
      rebuild();
      return {taken:true,dropped:null};
    }
    if(already) return {taken:false,dropped:null};
    /* ONE active, and it is the one the player presses. So an active does not displace a SPECIFIC key
       - there is only the one - it replaces whatever is in the active slot, which is to say it
       replaces the active you were carrying. Passives are sigils at -1 and never collide with it, so
       this cannot cost you a sigil.

       Nine sigils is where build diversity comes from and there is no cap on those. One active is a
       different constraint and it is a good one: it makes the thing you press a DECISION rather than a
       fourth number to stack, and it means picking up a second active is a real swap rather than a
       silent overwrite. The floor drops the displaced one where the new one was standing. */
    const slot=d.use==='active'?ACTIVE_SLOT:-1;
    let dropped=null, droppedCharges=null;
    if(slot>=0){
      const prev=loadout.items.find(s=>s.slot===slot);
      if(prev){ dropped=prev.id; droppedCharges=prev.charges; }
    }
    if(dropped) loadout.items.splice(loadout.items.indexOf(equipped(dropped)),1);
    loadout.items.push({id,name:d.name,charges:start!=null?start:null,slot});
    rebuild();
    /* The displaced item's charges are captured HERE, while the entry still exists.

       The caller cannot get them afterwards - `give` is what removes it, so a read from outside finds
       nothing and the pickup is written with no charges, and the next pickup restores the definition's
       full set. That is how a Tin Cup drained to one charge came back at three. */
    return {taken:true,dropped:dropped,droppedCharges:droppedCharges};
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

  /* Use an active item. The hook is looked up by name - a mod's item works without this file knowing
     it - and A HOOK REPORTS WHETHER IT DID ANYTHING. Its return value is what decides the charge.

     It used to count the hooks it FOUND and spend a charge on that, discarding what they returned. So a
     Tin Cup pressed at full health healed nobody, reported success, and cost one of three charges -
     which reads as a broken item rather than as a game declining, and two presses cost half a tin. That
     was the rule, not an oversight in one hook: nothing in the system could express "nothing happened",
     so nothing in the system could avoid charging for it. Every hook now returns a boolean and the
     charge follows the answer. */
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

  /* THE ACTIVE SLOT, and the only two questions anyone asks about it.

   It took a `slot` field per item for most of this project's life, and three places had to agree
   about it: the definitions, the build, and the blurb a player reads. They did not agree - two actives
   collided on key 1, and Lantern Friend was written as slot 2 and described as "Hold 2". With one slot
   there is nothing to agree about, so the field is gone and this is the single answer.

   Zero rather than one because it is a key in `loadout.items` and the sigils are -1. */
const ACTIVE_SLOT=0;

/* The active the player is carrying, or null. One function rather than a caller that knows the slot
   number, because the whole point of collapsing to one slot is that nothing outside this file should
   have to care what the number is. */
function active(){
  return loadout.items.find(s=>s.slot===ACTIVE_SLOT&&s.charges!=null)||null;
}

/* Pressing Q. The key handler calls this and nothing else, so "which item does Q press" is answered
   here and the handler never learns what an active slot is.

   Returns false when there is nothing to press, which is different from returning a reason - the
   handler does not open anything on a miss, because a key that opens a dialog every time you press it
   with empty hands is worse than a key that does nothing. */
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

/* Returns whether it actually did something, which is how a charge is protected.

   `use` counts truthy hook returns and only spends a charge if at least one fired, so a hook that
   returns a bare truthy has just told the player their charge was well spent on nothing. At full health
   the Tin Cup healed 0, reported success, and decremented - which reads as the item being broken rather
   than as the game declining politely, and it cost two of three charges if you pressed it twice
   reflexively.

   Declining is also the better answer than healing anyway: a heart at full health is not a wasted
   moment to the player, it is a moment where they learned they are fine. */
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
    /* ONLY EVER TOWARD THE PLAYER, AND ONLY AS FAR AS THE WALL ALLOWS.

       This set the new position to exactly 90px from the player along the line to the pickup, for any
       distance at all - including a distance of 9px. So a heart lying almost under the player was
       pushed AWAY from them to 90px, and if they were near a wall that push put it outside the room,
       where it can never be picked up again. Reproduced with the player against the left wall and a
       heart 9px away: the heart ended up 77px outside the map.

       Two changes, and the first is the one that matters:

       1. NOTHING MOVES TOWARD A POINT IT IS ALREADY PAST. A pickup closer than the target distance is
          simply left where it is. "Come here" cannot mean "go over there", and a spell that moves
          something AWAY from you is not a pull.
       2. Whatever happens, the result is CLAMPED INTO THE ROOM. The player is inside the room and the
          pickup's new position is computed from them, so clamping is a formality - but it is the
          formality that makes the rule true rather than merely intended. */
    if(d<=90) continue;
    let nx=player.x+(pk.x-player.x)/d*90, ny=player.y+(pk.y-player.y)/d*90;
    nx=Math.max(ROOM_LEFT+pk.r,Math.min(ROOM_RIGHT-pk.r,nx));
    ny=Math.max(ROOM_TOP+pk.r,Math.min(ROOM_BOTTOM-pk.r,ny));
    pk.x=nx; pk.y=ny;
    n++;
  }
  return n>0;                     // an empty room spends nothing
};
/* A named hook that is deliberately not implemented, and it now says so by REFUSING.

   It exists so that a definition using it VALIDATES - which is the seam that lets companions and the
   rest of the roster be written down before the behaviour behind them exists, and so a mod can
   reference it and get an honest "nothing happens yet" rather than a crash. That is still what it does.

   What it also did was REPORT SUCCESS while writing a flag nothing reads, so Lantern Friend spent
   its single charge on no companion and the press looked broken. Since `use` now spends a charge
   only when a hook reports that it did something, returning false here is the whole fix: the press
   declines, keeps the charge, and says why. The moment a companion exists this returns true, and
   nothing else has to change.

   `companionPending` is deleted rather than left accumulating, because a flag with no reader is the
   kind of thing that gets read three months later by someone who assumes it means what it says. */
HOOKS.spawn_companion=function(){ return false; };
