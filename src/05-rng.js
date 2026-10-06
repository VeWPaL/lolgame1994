/* 05-rng - the random source, and the three streams it is split into THE GAME HAD NO SEED. [h:05-rng-1] */

function mulberry32(seed){
  let s=seed>>>0;
  return function(){
    s=(s+0x6D2B79F5)>>>0;
    let t=s;
    t=Math.imul(t^(t>>>15),1|t);
    t=(t+Math.imul(t^(t>>>7),61|t))^t;
    return ((t^(t>>>14))>>>0)/4294967296;
  };
}

const Rnd=(function(){
  /* Each stream is seeded from the one number the player can see, through a different constant. [h:05-rng-2] */
  const OFF_JITTER=0x9E3779B9, OFF_ART=0x85EBCA6B;

  /* A short readable seed. base36 keeps it typable and copyable, and seven characters is 78
     billion possibilities, which is far more than a player will exhaust and far fewer than they
     will mistype. Fixed width, so a seed is always the same shape on screen. */
  const WIDTH=7;
  const encode=n=>{let s=(n>>>0).toString(36).toUpperCase();while(s.length<WIDTH)s='0'+s;return s;};
  /* THE SEED SPACE IS 32 BITS, AND A SEED THAT DOES NOT FIT IS REJECTED RATHER THAN WRAPPED. [h:05-rng-3] */
  function decode(text){
    if(text==null) return null;
    const t=String(text).trim().toUpperCase().replace(/[^0-9A-Z]/g,'');
    if(!t||t.length>WIDTH) return null;
    const n=parseInt(t,36);
    if(!Number.isFinite(n)||n<0) return null;
    // the whole 7-character space is 78.4 billion, so this rejects the top 94.5% rather than wrapping
    if(n>0xFFFFFFFF) return null;
    return n;
  }

  let seed=0, runFn=null, jitFn=null, artFn=null;
  /* A call count per stream, so the tests can prove the split is being respected rather than
     merely intended. An unused diagnostic is a comment; a counted one is a test. */
  const calls={run:0,jitter:0,art:0};

  function set(n){
    seed=n>>>0;
    runFn=mulberry32(seed);
    jitFn=mulberry32((seed^OFF_JITTER)>>>0);
    artFn=mulberry32((seed^OFF_ART)>>>0);
    calls.run=0; calls.jitter=0; calls.art=0;
    return seed;
  }

  /* THE FLOOR SEED. [h:05-rng-4] */
  function floorSeed(root,n){
    const t=(root^Math.imul(n+1,0x9E3779B1))>>>0;
    let a=t;
    a^=a>>>16; a=Math.imul(a,0x21F0AAAD);
    a^=a>>>15; a=Math.imul(a,0x735A2D97);
    a^=a>>>15;
    return a>>>0;
  }

  /* A random seed that a player can read out loud. [h:05-rng-5] */
  function fresh(){
    const buf=new Uint32Array(1);
    if(typeof crypto!=='undefined'&&crypto.getRandomValues){ crypto.getRandomValues(buf); return buf[0]>>>0; }
    /* Fallback for an environment with no Web Crypto. Date.now alone is coarse, but a run lasts
       seconds, so consecutive runs still differ - and this is a single-player game played by a
       person, not a lottery. */
    return (Date.now()^(Math.floor(performance.now())*2654435761))>>>0;
  }

  return {
    set:set, encode:encode, decode:decode, fresh:fresh, floorSeed:floorSeed,
    /* WIDTH and MAX are exported so a caller can explain WHY a code was refused without repeating the
       numbers. The seed sheet has to tell a player that 1Z141Z4 is too large rather than calling it a
       typo, and it should not have to hard-code a 7 and a 1Z141Z3 to do it. */
    WIDTH:WIDTH, MAX:0xFFFFFFFF,
    get seed(){return seed;},
    get seedText(){return encode(seed);},
    calls:calls,
    run:()=>{calls.run++; return runFn();},
    jitter:()=>{calls.jitter++; return jitFn();},
    art:()=>{calls.art++; return artFn();},
    /* convenience, so a call site reads as a decision rather than as plumbing */
    range:(lo,hi,f)=>lo+f()*(hi-lo),
    int:(n,f)=>Math.floor(f()*n),
    pick:(arr,f)=>arr[Math.floor(f()*arr.length)],
  };
})();

/* Seeded once at load, so nothing in the game can reach an uninitialised stream. A game with an
   unseeded RNG is a game whose first run is unreproducible, and that is the run everybody
   remembers. */
Rnd.set(Rnd.fresh());
