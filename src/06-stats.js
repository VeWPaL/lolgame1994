/* 06-stats - the character sheet, and the rule that keeps it honest Six numbers, shown on the pause sheet, that items change and the game reads. [h:06-stats-1] */

const Stats=(function(){
  const ORDER=['strength','speed','momentum','intelligence','luck','vigor','precision'];

  const DEFS={
    /* flat damage, added BEFORE falloff. [h:06-stats-2] */
    strength:{label:'STRENGTH',kind:'add',base:0,cap:Infinity,
      blurb:'Damage added to every hit, before range falloff.'},

    /* the top-speed half of the movement system, and a FRACTION rather than a rate: [h:06-stats-3] */
    speed:{label:'SPEED',kind:'add',base:0,cap:SPEED_CAP,asPct:true,
      blurb:'Top speed from items. A full Momentum meter adds more, up to a separate hard ceiling.'},

    /* earned, not given. There is no item that hands you Momentum, and that is the point: it is the
       only stat on the sheet that measures what you did rather than what you picked up. */
    momentum:{label:'MOMENTUM',kind:'meter',base:0,cap:1,
      blurb:'Charges while you move under pressure. Bleeds if you stand still. A hit costs close to half of it.'},

    /* arcana keys, not a damage stat. [h:06-stats-4] */
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
      blurb:'Maximum health. Two points is one heart.'},

    /* PRECISION: how narrowly a shot leaves the wand. [h:06-stats-5] */
    precision:{label:'PRECISION',kind:'add',base:0,cap:Infinity,
      blurb:'Narrows the Arcane Beam a fifth each. Luck finds better loot; this finds the gap.'}
  };

  const base={}, flat={}, earned={}, value={};
  for(const k of ORDER){ base[k]=DEFS[k].base; flat[k]=0; earned[k]=0; value[k]=DEFS[k].base; }

  /* CHARACTER CLASSES, and the reason the starting numbers are not a DEFS field any more. [h:06-stats-6] */
  const CLASSES={
    /* THE STARTER. It is the plain human: [h:06-stats-7] */
    wyrd:{name:'Wyrd', blurb:'Nobody in particular, which is the point.',
      glyph:'W', color:'#c79bff',
      stats:{strength:3, speed:0.25, momentum:0, intelligence:1, luck:0, vigor:8, precision:0}},
  };
  let classId='wyrd';

  /* A missing stat in a class is a ZERO, not a fallback to DEFS. A class that forgets Momentum is
     starting with whatever DEFS happens to say, and DEFS is the absence of a character - so the two
     would differ by exactly the thing a new class is most likely to get wrong. */
  function setBase(k,v){ check(k); base[k]=v||0; }

  /* It does NOT call applyVitals, and that omission is load-bearing rather than an oversight. [h:06-stats-8] */
  function applyClass(id){
    const c=CLASSES[id];
    if(!c) throw new Error('Stats: no character class called "'+id+'"');
    for(const k of ORDER) setBase(k,c.stats[k]||0);
    classId=id;
    derive();
    return classId;
  }

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
    /* The class RE-APPLIES, because a new run is played by the same character. [h:06-stats-9] */
    const c=CLASSES[classId]||CLASSES.wyrd;
    for(const k of ORDER) setBase(k,c.stats[k]||0);
    derive();
  }

  /* every entry point validates, and breakdown() is no exception. [h:06-stats-10] */
  function check(k){
    if(!(k in DEFS)) throw new Error('Stats: no stat called "'+k+'"');
    return k;
  }

  /* what the sheet shows underneath a number: [h:06-stats-11] */
  function breakdown(k){
    check(k);
    return {base:base[k],flat:flat[k],earned:earned[k],total:value[k],fromItems:flat[k]!==0};
  }

  return {
    ORDER:ORDER, DEFS:DEFS,
    /* the character table, and which one this run is playing. Exposed because the pause sheet and
       the future selection menu both need to read them, and a menu that had to reach into this IIFE
       to list the classes would be a menu that could not be written. */
    CLASSES:CLASSES,
    get classId(){ return classId; },
    classOf:function(id){ return CLASSES[id||classId]; },
    applyClass:applyClass,
    /* the raw base of a stat, for the sheet's "starts at" line. Separate from value() on purpose:
       value() is derived and moves, this one only moves when the character changes. */
    baseOf:function(k){ check(k); return base[k]; },
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

/* Momentum, as a controller rather than a bare field, because the suite needs to be able to say which motion model it is measuring. [h:06-stats-12] */
let momentumLocked=false, momentumHeld=null;
const Momentum={
  lock(){ momentumLocked=true; },
  unlock(){ momentumLocked=false; },
  locked(){ return momentumLocked; },
  set(v){ player.momentum=Math.max(0,Math.min(1,v)); Stats.earn('momentum',player.momentum); return player.momentum; },
  /* hold the meter at a fixed value, immune even to the hit penalty. [h:06-stats-13] */
  hold(v){ momentumHeld=(v==null?null:Math.max(0,Math.min(1,v))); if(momentumHeld!==null) Momentum.set(momentumHeld); return momentumHeld; },
  holding(){ return momentumHeld; },
  release(){ momentumHeld=null; },
  /* THE value the game reads, and the only place a held meter is honoured. [h:06-stats-14] */
  level(){ return momentumHeld!==null? momentumHeld : Stats.value('momentum'); },
  /* a hit costs most of the meter. See MOMENTUM_HIT_KEEP: a meter that resets to zero punishes the
     player during the moment they are already recovering, and turns a good run into a series of
     unrelated fights. 45% means a hit costs the cushion, not the run. */
  hit(){ if(momentumHeld!==null) return player.momentum; return Momentum.set(player.momentum*MOMENTUM_HIT_KEEP); },
  value(){ return player.momentum; },
};

/* The one number the movement code reads, and the only place two different sources are combined. [h:06-stats-15] */
function moveSpeedBonus(){
  return Math.min(MOVE_SPEED_HARD_CAP,Stats.value('speed')+Momentum.level()*MOMENTUM_SPEED);
}

/* The build. Items write here and the sheet reads here, so there is exactly one place that answers "what is this player carrying". [h:06-stats-16] */
const loadout={items:[]};

/* VIGOR IS MAXIMUM HEALTH, and this is where that stops being a lie. [h:06-stats-17] */
function applyVitals(){
  // there may be no player yet: the test harness resets the build before the first startGame, and
  // resetting a build is a thing you can do to nothing. typeof rather than a try, because player is
  // a module-scope let and touching it in its temporal dead zone throws rather than reading undefined.
  if(!player) return;   // undefined before the first startGame, and never null - but not worth betting on
  const max=Math.max(1,Stats.value('vigor'));
  if(max!==player.maxHp){
    player.maxHp=max;
    if(player.hp>max) player.hp=max;
  }
}
Stats.applyClass('wyrd');
