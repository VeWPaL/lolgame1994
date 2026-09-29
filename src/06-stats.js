/* =========================================================================================
   06-stats  -  the character sheet, and the rule that keeps it honest

   Six numbers, shown on the pause sheet, that items change and the game reads.

   THE ONE RULE: a stat is DERIVED, never assigned. There is no code anywhere that does
   player.speed *= 1.1. An item calls Stats.flat('strength', 2), and the value is recomputed from the
   base and the whole modifier set. This is not tidiness - it is the difference between an item
   system and a spreadsheet that happens to be in the game. After twenty additive items, an
   in-place model has multiplied in an order nobody can reproduce: removing one does not remove its
   effect, a build cannot be explained to the player or saved to disk, and "what does this item do"
   becomes unanswerable. Rebuilding from base every time costs nothing and makes the whole system
   explainable for free.

       value = min(cap, base + sum(flat) + earned)

   ADDITIVE, AND THAT IS THE WHOLE RULE. The first version had the usual two-part form,
   (base + flat) * product(1 + mult), and it had a silent degenerate case: a stat whose base is 0
   multiplied by anything is still 0. Speed's base IS 0 - the real base is PLAYER_MOVE and it lives
   on the player - so Stats.mult('speed', 0.06) produced exactly 0, a Speed item did nothing at all,
   and the only symptom was a bar that stayed empty. Twenty percent faster, invisibly slower.

   A rule with a silent failure mode is worse than a rule that is merely limited, and the limit here
   costs nothing real: a genuinely multiplicative effect - twenty percent faster casting, say - is a
   RATE, not a character attribute, and rates belong in named hooks. The stat model stays additive so
   that every number on the sheet means what it looks like it means.

   The clamp is applied to the DERIVED value, never to the inputs. That distinction matters: capping
   each modifier as it arrives means five Speed items quietly become worth less each and the player
   cannot reason about any of them. Capping the total means the budget is one number, SPEED_CAP,
   visible, tunable, and impossible to exceed by stacking.

   Each stat declares what KIND of number it is, because that is what stops it being used wrongly:

     add   a quantity you add up. Strength, health, arcana keys, and the speed BONUS - which is a
           fraction of a number that lives on the player, and so is itself additive.
     meter 0..1, earned by play rather than given by an item. Momentum.
     roll  a weight on a distribution. Luck.
     key   a threshold that opens content rather than a quantity. Intelligence.

   Momentum and Speed are ONE system, deliberately. Speed is what items give you; Momentum is what
   playing well gives you; they share one capped budget and one payoff. See 00-balance for why
   Momentum's weight sits on acceleration rather than top speed.
   ========================================================================================= */

const Stats=(function(){
  const ORDER=['strength','speed','momentum','intelligence','luck','vigor','precision'];

  const DEFS={
    /* flat damage, added BEFORE falloff. Flat and not multiplicative on purpose: falloff is what
       gives each of the four guns its shape, and a multiplier applied to it collapses all four
       into one gun with a different colour. Bolt's whole identity is one committed hit that hurts
       more than anything else; that only survives while damage is a thing you add. */
    strength:{label:'STRENGTH',kind:'add',base:0,cap:Infinity,
      blurb:'Damage added to every hit, before range falloff.'},

    /* the top-speed half of the movement system, and a FRACTION rather than a rate: it is a bonus
       added to a base that lives on the player, which is exactly why it is `add` and not a
       multiplier. Capped at SPEED_CAP, the most items may give, so a Speed item always has a
       readable value; the SUM of items and Momentum is separately clamped at MOVE_SPEED_HARD_CAP. */
    speed:{label:'SPEED',kind:'add',base:0,cap:SPEED_CAP,asPct:true,
      blurb:'Top speed from items. A full Momentum meter adds more, up to a separate hard ceiling.'},

    /* earned, not given. There is no item that hands you Momentum, and that is the point: it is the
       only stat on the sheet that measures what you did rather than what you picked up. */
    momentum:{label:'MOMENTUM',kind:'meter',base:0,cap:1,
      blurb:'Charges while you move under pressure. Bleeds if you stand still. A hit costs most of it.'},

    /* arcana keys, not a damage stat. Once INT reaches a threshold the door that wants it opens, and
       STAYS open - which is a content gate rather than a multiplier, and means the stat can open a
       region of the game instead of nudging a number. The latching matters: a door that relocks
       because you later drank something that lowered your arcana would strand a run with no way
       back, and a player cannot plan around a stat that can go down. */
    intelligence:{label:'INTELLIGENCE',kind:'key',base:0,cap:Infinity,
      blurb:'Arcana keys. At INT n, every door wanting n opens - and stays open, whatever happens to INT after.'},

    /* a weight on the rarity distribution, never a multiplier on damage. This is the stat that
       makes a seed worth sharing without making it worth replaying identically: same seed, two
       players, different luck, two different runs from one dungeon. */
    luck:{label:'LUCK',kind:'roll',base:0,cap:Infinity,
      blurb:'Shifts rarity rolls toward the unusual. Same dungeon, different good stuff.'},

    /* health. Without it, health items have nowhere to attach and half of a roster's worth of
       consumables would have to be smuggled in as flat healing. */
    vigor:{label:'VIGOR',kind:'add',base:0,cap:Infinity,
      blurb:'Maximum health, and how much a heart returns.'},

    /* PRECISION: how narrowly a shot leaves the wand. It was on LUCK, which was wrong twice over.

       Luck is a weight on a distribution - it decides what the dungeon contains and nothing else.
       Bolting "and your aim" onto it made one number mean two unrelated things, so a player could
       not reason about either: a Lucky Coin quietly became a damage item, and the stat that was
       meant to be about the run started being about the wand. Split, each can be tuned on its own
       curve, because they no longer have to move together.

       The lever lives in its own constants in 00-balance rather than inline in the firing code,
       because it is a LEVER and there will be more than one thing on it: the beam uses it now, and
       anything else wanting a stat-driven cone should read preciseSpread() rather than open-code
       its own, so there is exactly one place where "what does precision buy" is decided. */
    precision:{label:'PRECISION',kind:'add',base:0,cap:Infinity,
      blurb:'Narrows the Arcane Beam a fifth each. Luck finds better loot; this finds the gap.'}
  };

  const base={}, flat={}, earned={}, value={};
  for(const k of ORDER){ base[k]=DEFS[k].base; flat[k]=0; earned[k]=0; value[k]=DEFS[k].base; }

  /* The rebuild. Everything else in this file exists to make sure that when a build changes, this
     is the only path that produces a stat value. */
  function derive(){
    for(const k of ORDER){
      const d=DEFS[k];
      value[k]=Math.min(d.cap,base[k]+flat[k]+earned[k]);
    }
    return value;
  }

  function reset(){
    for(const k of ORDER){ flat[k]=0; earned[k]=0; }
    derive();
  }

  /* every entry point validates, and breakdown() is no exception. It was the one that did not, and
     it returned a bag of NaNs for a misspelled stat instead of saying so - which is the exact failure
     the loud throws exist to prevent, hiding in the one function that had been written to be read
     rather than called. */
  function check(k){
    if(!(k in DEFS)) throw new Error('Stats: no stat called "'+k+'"');
    return k;
  }

  /* what the sheet shows underneath a number: how much of it came from the build. Momentum reports
     fromItems false because its whole point is that no item contributed to it.

     A named function rather than a method on the object below, because sheet() needs to call it and
     a sibling method is not in scope inside the object literal that declares it. */
  function breakdown(k){
    check(k);
    return {base:base[k],flat:flat[k],earned:earned[k],total:value[k],fromItems:flat[k]!==0};
  }

  return {
    ORDER:ORDER, DEFS:DEFS,
    reset:reset, derive:derive,
    /* An item's contribution. One way in, because there is only one way. */
    flat:function(k,v){ check(k); flat[k]+=v; derive(); return value[k]; },
    /* Momentum, and only Momentum, arrives this way - it is measured, not chosen. */
    earn:function(k,v){ check(k); earned[k]=v; derive(); return value[k]; },
    value:function(k){ check(k); return value[k]; },
    breakdown:breakdown,
    /* for the pause sheet. Returns a copy so a caller cannot scribble on the derived values, which
       is the same mistake the whole file exists to prevent. */
    sheet:function(){
      const out=[];
      for(const k of ORDER) out.push({key:k,label:DEFS[k].label,kind:DEFS[k].kind,asPct:!!DEFS[k].asPct,
        blurb:DEFS[k].blurb,cap:DEFS[k].cap,value:value[k],...breakdown(k)});
      return out;
    },
  };
})();

/* Momentum, as a controller rather than a bare field, because the suite needs to be able to say
   which motion model it is measuring.

   107 of the checks walk a player around with live enemies, and Momentum charges on exactly that.
   Momentum raises ACCELERATION, and the gunner solves a CONSTANT-VELOCITY intercept - so a player
   who commits faster really is harder to hit, which is the mechanic working. It also means those
   107 numbers, taken before Momentum existed, are no longer what those tests say they are. The
   first honest move is not to loosen the numbers; it is to make the baseline explicit.

   So the suite LOCKS the meter the same way it seeds the RNG, and Momentum has its own tests that
   unlock it. A lock a test must opt out of beats a flag every test must remember to set, because
   the default is the thing that should happen. */
let momentumLocked=false, momentumHeld=null;
const Momentum={
  lock(){ momentumLocked=true; },
  unlock(){ momentumLocked=false; },
  locked(){ return momentumLocked; },
  set(v){ player.momentum=Math.max(0,Math.min(1,v)); Stats.earn('momentum',player.momentum); return player.momentum; },
  /* hold the meter at a fixed value, immune even to the hit penalty. This is the measuring
     instrument, and it exists because the alternative is a comparison that means nothing: the same
     run at "momentum 0" and at "momentum 1" has to differ ONLY in acceleration, or the two numbers
     are not about the same thing. A held meter makes the A/B exact. */
  hold(v){ momentumHeld=(v==null?null:Math.max(0,Math.min(1,v))); if(momentumHeld!==null) Momentum.set(momentumHeld); return momentumHeld; },
  holding(){ return momentumHeld; },
  release(){ momentumHeld=null; },
  /* THE value the game reads, and the only place a held meter is honoured.

     A hold has to be authoritative rather than a one-shot assignment, or it silently reverts the
     first time anything resets the player - which is exactly what happened when this was measured
     the first time: the hold was set, startGame() rebuilt the player, and the measurement reported
     an identical number for momentum 0 and momentum 1 without anybody noticing that both columns
     were the same column. A measurement instrument that can be invalidated by ordinary game flow is
     not an instrument. */
  level(){ return momentumHeld!==null? momentumHeld : Stats.value('momentum'); },
  /* a hit costs most of the meter. See MOMENTUM_HIT_KEEP: a meter that resets to zero punishes the
     player during the moment they are already recovering, and turns a good run into a series of
     unrelated fights. 45% means a hit costs the cushion, not the run. */
  hit(){ if(momentumHeld!==null) return player.momentum; return Momentum.set(player.momentum*MOMENTUM_HIT_KEEP); },
  value(){ return player.momentum; },
};

/* The one number the movement code reads, and the only place two different sources are combined.

   A Speed item and a full Momentum meter are bought in completely different currencies - one by
   picking something up, one by playing well - and they both spend the same budget. Clamping each
   separately would let a player with both exceed the speed the entire fight tuning was measured
   against, so the sum is clamped here instead. Every stat on the sheet is derived; this is the one
   derived VALUE rather than a modifier, because it is two stats added together and the addition
   needs its own ceiling. */
function moveSpeedBonus(){
  return Math.min(MOVE_SPEED_HARD_CAP,Stats.value('speed')+Momentum.level()*MOMENTUM_SPEED);
}

/* The build. Items write here and the sheet reads here, so there is exactly one place that answers
   "what is this player carrying". Empty until the first item exists - and the sheet's empty state is
   designed copy rather than a gap, because "nothing carried" is a perfectly normal thing to be
   looking at on the first tick of a run and it should not read as a feature that has not arrived. */
const loadout={items:[]};

/* VIGOR IS MAXIMUM HEALTH, and this is where that stops being a lie.

   It was a stat with a name, a bar, a row on the character sheet and a blurb - and nothing read it.
   player.maxHp was the literal 8, written into the player object at spawn, so Iron Ribs put +2 Vigor
   on the sheet and changed nothing at all. That is the exact failure this file was built to make
   impossible, and it survived because the sheet renders from Stats while the game reads a constant,
   and nobody checked that those were the same number.

   Derived here rather than in the items module, because it is a stat about the player rather than
   about the build - but it is derived for the same reason and in the same place, so there is one
   answer to "what is this character's health" and it is not written down twice.

   Losing the current health when the ceiling drops is deliberate rather than convenient: an item
   that takes Vigor away should cost you hearts now, because a build that can be made worse by
   picking something up has to be able to do that visibly. */
const BASE_HP=8;
function applyVitals(){
  // there may be no player yet: the test harness resets the build before the first startGame, and
  // resetting a build is a thing you can do to nothing. typeof rather than a try, because player is
  // a module-scope let and touching it in its temporal dead zone throws rather than reading undefined.
  if(!player) return;   // undefined before the first startGame, and never null - but not worth betting on
  const max=BASE_HP+Math.max(0,Stats.value('vigor'));
  if(max!==player.maxHp){
    player.maxHp=max;
    if(player.hp>max) player.hp=max;
  }
}
Stats.reset();
