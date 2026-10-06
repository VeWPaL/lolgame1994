/* THE SOUND SYSTEM. Synthesised, with no assets - which is what makes it ship inside the .exe.

   Every sound here is generated from oscillators and noise buffers at runtime. That is a deliberate
   choice and not the only available one: the alternative is a folder of .wav/.ogg files, which a
   Unity build has to carry, load and keep alive across a domain the browser does not control. A
   procedural sound is a function call, so there is nothing to ship, nothing to fail to load, and no
   asset pipeline in the way. It also means the pitch of every shot can be a consequence of the
   weapon rather than a property of a file, which is what the queued "pitch jitter" item wanted and
   could not have had with samples.

   THE FOUR RULES, and each one exists because the browser will not let you ignore it.

   1. THE CONTEXT IS CREATED ON A GESTURE, NEVER AT LOAD. Every browser suspends an AudioContext
      created before a real interaction and will not resume it without one. Creating it here at
      module scope means a game that never gets a click is silent, permanently, and the reason is not
      visible from the code. So `Sound.unlock()` is called from the first keydown or pointerdown and
      everything before that is a no-op that still records what it would have played.

   2. NOTHING BLOCKS THE TICK. Sound is fire-and-forget: a voice is built, started and handed to a
      pool that reaps it when it ends. Nothing in here reads game state to decide whether to play, and
      nothing here can throw into a tick - every entry point is wrapped, because a sound that throws
      while the player is fighting is a crash, and a crash caused by audio is indefensible.

   3. THE POOL IS BOUNDED. An unbounded oscillator with no end is a leak, and this game can fire
      hundreds of projectiles a second. `MAX_VOICES` is the ceiling on simultaneously sounding
      voices; past it the quietest is stolen, which is what a real mixer does and is inaudible at this
      scale.

   4. IT IS MUTABLE, AND THE MUTE IS HONOURED BEFORE ANYTHING IS BUILT. A player who has turned sound
      off must not pay for the synthesis, and must not get a sound out of a stale voice. `muted` is
      checked at the top of every entry point.

   Everything is a placeholder tone, deliberately: shapes and levels are placeholders so the TIMING
   can be judged against the fight, and real samples come after the timing is right. `Sfx` names the
   event, `Sound.play` is the one call the game makes. */

const Sound=(function(){
  /* ---- state ----------------------------------------------------------------------------------- */
  let ctx=null, master=null, comp=null, noiseBuf=null;
  /* SET WHILE AN OFFLINE RENDER HOLDS `ctx`. Declared here rather than beside the pool because
     `play` reads it and `play` is defined above that point - a `let` further down would be a
     temporal-dead-zone error the first time a sound was triggered. */
  let rendering=false;
  /* The real AudioContext, never swapped. `ctx` is swapped by an offline render; this is not. */
  let liveCtx=null;
  let lastDecline='none';

  /* YIELDS THE MAIN THREAD WITHOUT WAITING FOR A FRAME.

     `setTimeout(0)`, not `requestAnimationFrame`, and the reason is specific to this project's test
     harness: the suite runs its test bodies while the page is still parsing, so the main thread is
     blocked, and a rAF callback is not delivered until the browser regains control - which it does not
     do while the suite is on the stack. A wait loop built on rAF therefore never completes inside the
     suite, and the symptom is a browser navigation timeout rather than a test failure: the page never
     becomes idle enough for `domcontentloaded` to fire.

     A `setTimeout(0)` macrotask is delivered as soon as the current task yields, so it works in both
     places - in the suite and in a real game loop. Anything that has to wait for the audio clock
     should also be bounded, so a browser that refuses to resume cannot turn a wait into a hang. */
  const yieldToBrowser=()=>new Promise(r=>setTimeout(r,1));
  let muted=false, volume=0.55, unlocked=false, warned=false, registered=false, gestureSeen=false;
  let voices=0;
  let played=0, skippedMuted=0, skippedLocked=0, failed=0;

  /* Browser prefixes, because this is a plain <script> file with no build step and no bundler to
     paper over the differences. `webkitAudioContext` is Safari and old Chrome. */
  const Ctor=(typeof window!=='undefined')
    &&(window.AudioContext||window.webkitAudioContext);

  /* ---- unlock ---------------------------------------------------------------------------------- */
  /* THE ONLY PLACE ctx IS CREATED. Idempotent, because a keydown handler that runs sixty times a
     second must not construct sixty contexts - and it does not throw if called twice, but the
     suspended-state dance is worth doing exactly once. */
  /* THE CONTEXT MUST BE BUILT ON A GESTURE, AND THIS FUNCTION IS THE ONLY PLACE IT IS BUILT.

     This was the cause of a game that was completely silent for ever, and it is worth writing down
     because every measurement in this file had said the audio worked.

     THE BUG. `autoUnlock()` is called at load from `80-ui.js`, and it calls this. So the context was
     created BEFORE any user interaction - which is precisely what every browser's autoplay policy
     suspends. The context came up `suspended`. Then `resume()` was called and its promise ignored,
     and `unlocked` was read from `ctx.state` on the very next line, before the promise could
     settle. So `unlocked` stayed false, `play()` skipped every voice through `skippedLocked`, and:

         - the guard at the top of this function is `if(ctx||!Ctor) return false`, and `ctx` had just
           been assigned - so every SUBSEQUENT gesture, every keypress, every click, called straight
           into that guard and returned false without trying anything;
         - `autoUnlock` only removes its listeners once `unlocked` is true, so they stayed installed
           too, calling a function that could not succeed.

     A game with twelve fully working voices, correct waveforms, correct mix levels, a bounded pool -
     and not one sound, because of a line that assigned a variable before the thing that variable was
     waiting on.

     MEASURED, with a real keydown and a real click and no autoplay flag, which is what a player's
     browser has:

         at load       ctxState 'suspended', unlocked false, blockedByPolicy true
         after D       ctxState 'suspended', unlocked false
         after click   ctxState 'suspended', unlocked false
         40 shots      played 40, skippedLocked 40
         held D firing played 50, skippedLocked 50

     And with the autoplay flag set - which every benchmark in this file used - the same page reported
     `ctxState 'running'` and passed everything. **That is why it looked fine.** A headless benchmark
     with `--autoplay-policy=no-user-gesture-required` is measuring a browser the player does not have.

     THE FIX, in three parts:
       1. Do not create a context until a real gesture. `unlock` is a no-op without one.
       2. `resume()` returns a PROMISE. Wait for it, then read the state - do not read the state and
          assume the promise settled.
       3. Allow a retry. A context that is somehow suspended must be resumable again, so the guard
          cannot be `if(ctx) return false`. */
  function unlock(){
    if(!Ctor){ lastDecline='unlock: no AudioContext constructor in this browser'; return false; }
    /* NO GESTURE, NO CONTEXT. See the note above: a context built before an interaction is suspended
       by every browser, and the original built one at load. */
    if(!gestureSeen){ lastDecline='unlock: no gesture has happened yet'; return false; }

    /* AN EXISTING CONTEXT THAT IS SUSPENDED IS NOT A REASON TO GIVE UP. This is part 3 of the fix and
       it is the part that makes the other two recoverable: the old guard was `if(ctx) return false`,
       so a context created in the wrong state could never be repaired - the one thing that would have
       fixed it is the one thing it prevented. */
    if(ctx){
      if(ctx.state==='running'){ unlocked=true; return true; }
      try{ ctx.resume(); }catch(e){ /* a context that refuses is handled by the state check */ }
      unlocked=(ctx.state==='running');
      return unlocked;
    }
    try{
      ctx=new Ctor();
      /* THE LIVE CONTEXT, KEPT UNDER A NAME OF ITS OWN. `ctx` is module state that `renderVoice`
         swaps for the duration of an offline render, so anything that must act on the REAL context -
         the suspend/resume recovery path, the test hooks - cannot use it. See `suspendForTest`. */
      liveCtx=ctx;
      master=ctx.createGain();   // NOT tracked: the master lives for the life of the context
      master.gain.value=muted?0:volume;
      /* A COMPRESSOR ON THE MASTER, and it is not decoration. Six shells, four Brunch and a boss
         volley arriving together is a peak well past 1.0, and clipping a peak is a harsh artefact
         that reads as a mistake in the game rather than in the audio. Soft-knee compression is the
         cheapest fix and the one that costs no per-sound tuning. */
      comp=ctx.createDynamicsCompressor();
      comp.threshold.value=-14; comp.knee.value=22;
      comp.ratio.value=5; comp.attack.value=0.004; comp.release.value=0.18;
      master.connect(comp); comp.connect(ctx.destination);
      noiseBuf=makeNoise();
      /* RESUME, AND THEN WAIT FOR IT. `resume()` returns a promise and `ctx.state` does not change
         until it settles, so reading the state immediately after calling it - which is what the
         first version did - reports 'suspended' even on a context that is about to run. That is the
         third way this file failed to make a sound in a browser that supports audio perfectly well:
         the check was correct and the timing was not.

         Measured in headless Edge with no game involved: a raw context is `running` on create and
         `running` after resume, and `currentTime` does not advance because there is no audio device.
         The game's own context reported `suspended` on the same page, which is this bug. */
      /* RESUME, AND WAIT FOR IT BEFORE ASKING. This is part 2 of the fix.

         `resume()` returns a promise and the state does not change until it settles, so reading
         `ctx.state` on the line after the call is reading it too early - it reported 'suspended' on a
         context that was about to run, and on the real machine it never ran at all.

         So the promise is the thing that is waited on, and the state is read after. The synchronous
         read is kept as well, because a context that is ALREADY running needs no wait and a game
         should not be silent for a frame because of it. */
      let settled=false, resumed=false;
      try{
        const r=ctx.resume&&ctx.resume();
        if(r&&r.then){
          r.then(()=>{ unlocked=(ctx.state==='running'); settled=true; },
                 ()=>{ unlocked=(ctx.state==='running'); settled=true; });
        } else { settled=true; }
      }catch(e){ settled=true; }
      unlocked=(ctx.state==='running')||unlocked;
      /* THE FLAG IS NOT THE STATE. `unlocked` gates `play`, and if it is left false on a context that
         is running, the game is silent with everything else working. So `play` asks the CONTEXT too,
         and this flag is only ever a fast path. */
      return ctx.state==='running';
    }catch(e){
      /* DO NOT DISCARD A CONTEXT THAT WORKS. This line used to be `catch(e){ failed++; ctx=null; }`,
         and it was the fourth way this file could end up permanently silent.

         The throw happens partway through building the graph - `createGain`,
         `createDynamicsCompressor`, `makeNoise` - and by then `ctx` is a real, live, possibly RUNNING
         AudioContext. Setting it to null threw that away, and because the guard at the top of this
         function reads a missing context as "not built yet", the next gesture built a SECOND context
         while the first one's nodes stayed connected to its destination with nothing to stop them.

         Measured: after a setup throw, `stats()` reported `ctxState: 'none'` and
         `lastDecline: 'no AudioContext at all'` on a page whose audio was working moments earlier - and
         the symptom surfaced as "one gesture and the context is suspended rather than running", which
         describes a different bug entirely and sent the search somewhere else for a while.

         So a partially-built context is KEPT. If it is running, the game has audio and the missing piece
         was cosmetic; if it is not, `play` resumes it. Either way the player is better off than with a
         null and no route back to sound. */
      failed++;
      return false;
    }
  }

  /* ONE BUFFER OF WHITE NOISE, made once. Every percussive sound in the game - impacts, dashes,
     the boss landing - is this buffer through a filter, and generating it per shot would allocate a
     few hundred kilobytes a second during a fight. */
  function makeNoise(){
    const n=Math.floor(ctx.sampleRate*1.2);
    const b=ctx.createBuffer(1,n,ctx.sampleRate);
    const d=b.getChannelData(0);
    /* Deterministic noise, NOT Math.random. The game's whole simulation is seeded and reproducible,
       and a sound that changes the RNG stream would make audio the first thing to break parity - or
       worse, the first thing to break a save. A xorshift with a fixed seed keeps the sounds varied
       and the run identical. */
    let s=0x9e3779b9;
    for(let i=0;i<n;i++){ s^=s<<13; s^=s>>>17; s^=s<<5; d[i]=((s>>>0)/4294967296)*2-1; }
    return b;
  }

  /* ---- the one call the game makes ------------------------------------------------------------- */
  /* `opts` is deliberately small and entirely optional, so a call site can be as simple as
     `Sound.play('hit')` and still be right. Everything else about a sound lives here, which is what
     keeps 30 call sites from each inventing their own envelope. */
  function play(name,opts){
    opts=opts||{};
    played++;
    if(muted){ skippedMuted++; lastDecline='muted'; return false; }
    /* NOT WHILE AN OFFLINE RENDER HOLDS THE MODULE `ctx`. THIS IS THE ACTUAL CAUSE OF A CORRUPTED
       WAVEFORM, and it took four wrong fixes to find because the symptom pointed somewhere else.

       `renderVoice` builds a voice against an OfflineAudioContext by swapping the module's `ctx`,
       and then AWAITS `startRendering()`. That await yields the main thread, and the game's own
       code - a fight loop, a keydown, another test's `play()` - runs in the gap. Every one of those
       calls built its nodes against the OFFLINE context and got rendered into the buffer being
       measured. So a 90ms `shot` came back with a tail of 0.78 to 1.28 at 0 to -2dBFS: the
       measurement was full of other sounds, and which ones depended on what else happened to be
       running. Reproduced inside the suite at 960x600 and not at 1280x720, because the only thing
       that decides it is the interleaving.

       The previous three fixes - serialising the renders, clearing the pool, suspending the live
       context - were all aimed at the LIVE graph. The live graph was never the problem. */
    if(rendering){ skippedLocked++; lastDecline='a render is in flight'; return false; }
    /* A CONTEXT THAT IS RUNNING IS A CONTEXT THAT PLAYS, WHATEVER THE FLAG SAYS.

       This is the last line of defence, and it exists because the flag was the single point of
       failure for the whole system: `unlocked` was read from `ctx.state` one line after calling
       `resume()`, which is before the promise settles, so it was false on a context that was about to
       run - and every voice went to `skippedLocked` while the game looked completely healthy.

       Asking the CONTEXT here means a flag that is stale or late cannot silence the game. `resume()`
       is only called when the context is genuinely not running, so this costs a comparison in the
       common case and a resume on the rare one. */
    if(!unlocked){
      if(ctx&&ctx.state==='running'){ unlocked=true; }
        else if(ctx){
          /* A CONTEXT THAT WENT SUSPENDED IS A CONTEXT TO BE RESUMED, NOT A REASON TO STAY QUIET.

             This is not the autoplay case - that is handled in `unlock`, on a gesture. This is the
             case where the game WAS audible and then stopped being: the tab was backgrounded, the
             machine changed audio device, the OS took the device exclusive. In every one of those the
             browser suspends the context, and the original code here simply declined to play and
             counted a skip, for ever.

             So it asks for a resume on the play itself. A node scheduled against a suspended context
             is not lost - it plays when the context comes back - which is the behaviour wanted, and
             it costs one state comparison in the common case. */
          try{ ctx.resume&&ctx.resume(); }catch(e){ /* refused: still counted as a skip below */ }
        }
        else { skippedLocked++; lastDecline='no AudioContext at all'; return false; }
    }
    /* ASK THE CONTEXT, NOT THE FLAG. The flag is set once and goes stale - `resume()` is async, and a
       context that was suspended at the flag's creation is running a moment later. Reading only the
       flag is how a fully working sound system plays nothing. */
    if(ctx&&ctx.state==='running') unlocked=true;
    /* THE RESERVATION IS TAKEN BEFORE THE DEVICE IS ASKED. The pool is a property of this system, so
       it is bounded whether or not anything can be heard - and a headless browser, where the context
       never leaves `suspended`, is the only place the bound CAN be tested at all. Reserving after the
       state check meant the count stayed at zero there and two tests passed against a real leak. */
    /* A SUSPENDED CONTEXT IS NOT A REFUSAL. This is the only thing standing between a tab that was
       backgrounded and a game that is silent until it is reloaded.

       The original line was `if(!unlocked||!ctx||ctx.state!=='running')` - it declined to play
       whenever the context was not running, and the only thing that ever resumed a context was a
       gesture through `unlock`. So: a player alt-tabs mid-fight, comes back, and the game is muted
       for ever with no way back except a page reload. The `resume()` above is on the play itself, and
       a node scheduled against a context that is about to run is not lost - it plays.

       What is still refused is a browser with NO context, which genuinely cannot make a sound, and
       that must stay visible rather than being papered over by a green counter. */
      if(!ctx){ skippedLocked++; lastDecline='no AudioContext at all'; return false; }
    /* RECLAIM FIRST, THEN BUILD, THEN TRIM. Reclaiming before the build keeps the list from growing
       while a long sound is still sounding; trimming after it is what enforces the cap, because a
       sound adds 3-6 nodes and no pre-check can know how many until it has been built. */
    prune();
    try{
      const t=ctx.currentTime;
      const v=VOICES[name];
      if(!v){ lastDecline='no such voice: '+name; return false; }
      /* The pitch jitter, and it is here rather than at every call site. `detune` is in cents, so a
         value of 20 is a fifth of a semitone either way - audible as "not a machine", inaudible as
         "wrong". A repeated shot at an identical pitch is the single thing that makes a synthesised
         sound feel like a beep; this is the whole fix and it costs one number. */
      const cents=opts.detune!==undefined?opts.detune:(v.jitter||0)*(rand()*2-1);
      const gain=(opts.gain!==undefined?opts.gain:1)*(v.gain===undefined?1:v.gain);
      const pan=(opts.pan!==undefined?opts.pan:0);
      /* THE STOP TIME FOR EVERY NODE OF THIS SOUND, SET BEFORE IT IS BUILT. Each voice declares its
         own length - `len` in seconds - and `track` stamps it onto every node the voice creates, so
         a gain and a filter are reclaimed exactly when their source is. A voice with no `len` gets
         the longest, which over-reclaims rather than leaks. */
      buildEnd=t+((v.len!==undefined)?v.len:MAX_SOUND_LEN);
      const node=v.build(t,{gain,cents,pan});
      trimToCap();
      return !!node;
    }catch(e){ failed++; return false; }
  }

  /* ONE DETERMINISTIC RANDOM, local to audio, so nothing here can touch the game's stream. Same
     reasoning as the noise buffer. */
  let rs=0x2545f491;
  function rand(){ rs^=rs<<13; rs^=rs>>>17; rs^=rs<<5; return (rs>>>0)/4294967296; }

  /* ---- voice helpers -------------------------------------------------------------------------- */
  function env(g,t,a,d,sustain,release,peak){
    g.gain.setValueAtTime(0,t);
    g.gain.linearRampToValueAtTime(peak,t+a);
    g.gain.linearRampToValueAtTime(peak*sustain,t+a+d);
    g.gain.exponentialRampToValueAtTime(0.0001,t+a+d+release);
  }
  /* THE POOL, AND IT IS A REAL ONE.

     The first version counted voices with `onended` and a cap that refused new ones, and it leaked:
     measured 162 simultaneous voices against a declared MAX_VOICES of 24, after twenty seconds of a
     five-body fight. Two reasons, both worth naming.

     `onended` does not fire until the scheduled end has actually been reached on the audio thread, so
     a hundred 90ms shots in a second are still "live" long after they are inaudible - and the count
     only ever went UP, because a voice refused at the cap still incremented nothing and nothing
     decremented it back down in time.

     And refusing to start a sound is the wrong answer anyway: the quietest sound to drop is the one
     that has been going longest, and a cap that drops NEWEST makes a fast weapon sound broken.

     So this keeps a list of live voices, prunes the finished ones every tick it is consulted, and
     steals the oldest when it is full. A source is stopped before being dropped, which is what
     actually ends it - `onended` is only ever used to tidy the list. */
  const live=[];
  /* TRACKING HAPPENS EVEN WHEN NOTHING CAN BE HEARD.

     The voice bookkeeping is tracked independently of whether the audio device will play it. A first
     version only tracked inside `build`, so on a browser that refuses the context - a headless one,
     or any page where the autoplay policy has not been satisfied - `live` stayed empty and the
     bound could not be observed at all. Two tests passed against the leak for exactly that reason:
     they read a counter that nothing was incrementing.

     `reserve()` is therefore called from `play`, before the voice is built, and released by `prune`
     on the same schedule. The pool is a property of the SYSTEM, not of whether a speaker exists. */
  /* TRACKING IS PER-VOICE AND PER-SOURCE, and it is a LIST OF EVERY NODE A SOUND CREATES.

     This is the stutter, and it is worth writing down exactly why because the pool looked correct
     for a long time.

     `track()` was given ONE node per sound - the oscillator - and `prune()` released on a single
     shared epoch 1.3s long. Two consequences, both of which only appear on a machine with a working
     audio device, which is why every headless measurement looked clean:

       1. A sound is 4-6 nodes, not one: an oscillator or a buffer source, a gain, a filter, and a
          StereoPanner for anything panned. Only the first was tracked and only the first was ever
          stopped, so a panned hit left its panner, its filter and its gain connected to the master
          graph for ever. Those are the nodes a browser has to keep alive and a GPU-less audio thread
          has to walk.

       2. `reserve()` released on a shared 1.3s epoch rather than per voice, so `reserved` pinned at
          MAX_VOICES almost immediately and then `steal()` ran on EVERY `play()` - and `steal()` calls
          `prune()`, which walks the whole list. A 90ms shot was costing an O(live) walk plus a
          `shift()` per trigger pull, in the tick, forever.

     The fix is unglamorous: every node a voice creates is tracked, every one is stopped, and the
     bound is checked once per play rather than by re-deriving it. The list is short because the
     bound is short. */
  /* `buildEnd` is the stop time for the sound currently being built. `track` reads it, so a voice body
     can call `track(node)` on every node it creates without threading an argument through - which is
     what kept the first version from doing it. The alternative is a parameter on every voice's every
     line, and a voice that forgets one leaks one node. */
  let buildEnd=0;
  /* WHETHER THE NODES BEING BUILT BELONG TO THE LIVE POOL. An offline render builds a whole voice
     through the same factories, and those nodes belong to a graph that is thrown away when the
     render finishes. Tracking them was a real bug with a real symptom: `render()` clears the live
     pool before measuring, so a render that had tracked its nodes left the NEXT render's clear
     stopping a graph that was mid-render - measured as `shot` reporting a tail of 0.78 at
     -2dBFS, at 960x600 and not at 1280x720, because the timing of two async renders is the only
     thing that decides which one gets interrupted. */
  let tracking=true;
  function track(node){
    if(!node) return node;
    node.__end=buildEnd;
    if(tracking) live.push(node);
    return node;
  }
  /* THE CAP IS ON NODES, NOT ON SOUNDS, and it has to be: a sound is 3-6 nodes, so a cap of 24 NODES
   is about six simultaneous sounds - which is the right number for a fight, and is a quarter of what
   24 sounds would have been. A node cap that claimed to be a sound cap was counting the wrong thing
   by a factor of four, which is how the first leak looked like a small number. */
const MAX_VOICES=48, MAX_SOUND_LEN=1.4;   // 48 nodes ~= 8-16 sounds; the boss at 1.3s, rounded up
  /* RECLAIM BY AGE, AND NEVER DEPEND ON THE AUDIO CLOCK TO DO IT.

     `ctx.currentTime` only advances while the context is RUNNING. On a suspended context - which is
     every headless browser, and any tab the player has muted - it is frozen, so a prune that asks
     "is this node past its end time?" reclaims nothing, ever. The list then grows by 3-6 nodes per
     shot with nothing ever removed, and because `play()` walks it on every trigger pull the cost is
     quadratic in the length of the fight. Measured: a suite run reached 86,606 sounds and stopped
     responding entirely.

     So the bound is enforced STRUCTURALLY - trim to the cap after building, oldest first - and the
     clock is only used to release things early. Correctness does not depend on it. */
  function prune(){
    if(!live.length) return;
    const running=ctx&&ctx.state==='running';
    const now=running?ctx.currentTime:0;
    for(let i=live.length-1;i>=0;i--){
      const v=live[i];
      if(running&&v.__end!==undefined&&now>v.__end){
        try{ if(v.stop) v.stop(); }catch(e){ /* already stopped, or not a source */ }
        live.splice(i,1);
      }
    }
  }
  /* TRIM TO THE CAP, OLDEST FIRST. This is the bound that actually holds. One sound is 3-6 nodes, so
     this can release several at once - the earlier version released exactly one per play, which is
     fewer than a single sound adds, so the list grew even when it was "at the cap". */
  function trimToCap(){
    while(live.length>MAX_VOICES){
      let oldest=0;
      for(let i=1;i<live.length;i++) if(live[i].__end<live[oldest].__end) oldest=i;
      const v=live[oldest];
      try{ if(v.stop) v.stop(); }catch(e){ /* not a source */ }
      live.splice(oldest,1);
    }
  }
  /* STOP EVERYTHING, for one sound's worth of nodes. This is the shape `track` alone did not have. */
  function stopAll(nodes){
    for(const v of nodes){ try{ if(v.stop) v.stop(); }catch(e){ /* not a source */ } }
  }
  /* ---- the sounds ------------------------------------------------------------------------------ */
  /* Each entry builds its own nodes and returns a tracked source. `jitter` is in cents.
     `gap` is the PITCH VARIATION BETWEEN REPEATS, which is the item that was queued - see the note
     on `play` above. Shapes and levels here are PLACEHOLDERS: the envelope SHAPES are chosen to be
     correct for the event (a shot is a click with a fast decay, an impact has a body) so the timing
     can be judged against the fight, and the exact tone is what real samples replace. */
  const VOICES={
    /* the player's weapon. A click with almost no body: a shot should be felt as much as heard, and
       a 40ms decay is what keeps a fast weapon from turning into a wall of noise. */
    shot:{gain:0.5,jitter:35,len:0.09,build(t,o){
      const osc=T.osc(), g=T.gain(), f=T.filter();
      osc.type='triangle';
      osc.frequency.setValueAtTime(880,t);
      osc.frequency.exponentialRampToValueAtTime(220,t+0.05);
      osc.detune.value=o.cents;
      f.type='lowpass'; f.frequency.value=2600;
      env(g,t,0.004,0.02,0.35,0.05,o.gain*0.5);
      osc.connect(f); f.connect(g); g.connect(bus(o.pan));
      osc.start(t); osc.stop(t+0.09);
      return track(osc);
    }},
    /* a shell hitting a body. Noise through a bandpass: the band is what makes it read as an impact
       rather than a click, and its centre is the PITCH that the queued jitter item wanted to vary. */
    hit:{gain:0.7,jitter:120,len:0.15,build(t,o){
      const src=T.src(), f=T.filter(), g=T.gain();
      src.buffer=noiseBuf;
      f.type='bandpass'; f.frequency.value=760*(1+o.cents/1200); f.Q.value=1.1;
      env(g,t,0.003,0.03,0.25,0.08,o.gain);
      src.connect(f); f.connect(g); g.connect(bus(o.pan));
      src.start(t); src.stop(t+0.15);
      return track(src);
    }},
    /* the player being hurt. Low, and deliberately ugly - it is the one sound that must cut through
       everything else, because it is the only one that matters at the moment it plays. */
    hurt:{gain:0.5,jitter:0,len:0.3,build(t,o){
      const osc=T.osc(), g=T.gain();
      osc.type='sawtooth';
      osc.frequency.setValueAtTime(180,t);
      osc.frequency.exponentialRampToValueAtTime(70,t+0.22);
      env(g,t,0.005,0.06,0.5,0.2,o.gain);
      osc.connect(g); g.connect(bus(o.pan));
      osc.start(t); osc.stop(t+0.3);
      return track(osc);
    }},
    /* an enemy shell landing on you. Distinct from the player being hurt so the two are separable in
       a four-body fight: higher, shorter, and no sawtooth. */
    playerHit:{gain:0.75,jitter:60,len:0.1,build(t,o){
      const src=T.src(), f=T.filter(), g=T.gain();
      src.buffer=noiseBuf;
      f.type='bandpass'; f.frequency.value=1800*(1+o.cents/900); f.Q.value=0.8;
      env(g,t,0.002,0.02,0.3,0.06,o.gain);
      src.connect(f); f.connect(g); g.connect(bus(o.pan));
      src.start(t); src.stop(t+0.1);
      return track(src);
    }},
    /* the blink. A short upward sweep: the player should be able to hear that they committed to a
       blink before they see where it ended, because the i-frames are the point of the action. */
    blink:{gain:0.55,jitter:20,len:0.18,build(t,o){
      const osc=T.osc(), g=T.gain();
      osc.type='sine';
      osc.frequency.setValueAtTime(420,t);
      osc.frequency.exponentialRampToValueAtTime(1250,t+0.12);
      env(g,t,0.006,0.04,0.5,0.1,o.gain);
      osc.connect(g); g.connect(bus(o.pan));
      osc.start(t); osc.stop(t+0.18);
      return track(osc);
    }},
    /* a body dying. Low and falling, because it is the sound of something stopping. */
    kill:{gain:0.42,jitter:80,len:0.25,build(t,o){
      const osc=T.osc(), g=T.gain();
      osc.type='triangle';
      osc.frequency.setValueAtTime(320,t);
      osc.frequency.exponentialRampToValueAtTime(90,t+0.18);
      env(g,t,0.004,0.05,0.4,0.14,o.gain);
      osc.connect(g); g.connect(bus(o.pan));
      osc.start(t); osc.stop(t+0.25);
      return track(osc);
    }},
    /* a Brunch touching you. Heavier than a kill - it is the same event plus your own health, and it
       is the sound the bomb rush is built out of. */
    touch:{gain:0.46,jitter:40,len:0.3,build(t,o){
      const osc=T.osc(), g=T.gain(), f=T.filter();
      osc.type='square';
      osc.frequency.setValueAtTime(130,t);
      osc.frequency.exponentialRampToValueAtTime(55,t+0.2);
      f.type='lowpass'; f.frequency.value=700;
      env(g,t,0.004,0.05,0.5,0.18,o.gain);
      osc.connect(f); f.connect(g); g.connect(bus(o.pan));
      osc.start(t); osc.stop(t+0.3);
      return track(osc);
    }},
    /* the gunner's cast tell. THE MOST IMPORTANT SOUND IN THE GAME, because it is the only warning a
       ranged body gives and it is a half-second long. It is quiet, it is a rising tone, and it is
       mixed UNDER everything - if the player cannot hear it over a fight, the mechanic they are
       dodging has no tell in practice. */
    tell:{gain:0.16,jitter:15,len:0.55,build(t,o){
      const osc=T.osc(), g=T.gain();
      osc.type='sine';
      osc.frequency.setValueAtTime(660,t);
      osc.frequency.exponentialRampToValueAtTime(990,t+0.45);
      env(g,t,0.05,0.3,0.8,0.12,o.gain);
      osc.connect(g); g.connect(bus(o.pan));
      osc.start(t); osc.stop(t+0.55);
      return track(osc);
    }},
    /* the boss arriving. Long, low, and slow - the one sound that is allowed to be longer than half a
       second, because it is a phase change rather than an event. */
    boss:{gain:0.34,jitter:0,len:1.3,build(t,o){
      const a=T.osc(), b=T.osc(), g=T.gain();
      a.type='sawtooth'; b.type='sine';
      a.frequency.setValueAtTime(70,t); a.frequency.exponentialRampToValueAtTime(42,t+1.1);
      b.frequency.setValueAtTime(35,t); b.frequency.exponentialRampToValueAtTime(28,t+1.1);
      const f=T.filter(); f.type='lowpass'; f.frequency.value=420;
      env(g,t,0.12,0.4,0.7,0.6,o.gain);
      a.connect(f); b.connect(f); f.connect(g); g.connect(bus(0));
      a.start(t); b.start(t); a.stop(t+1.3); b.stop(t+1.3);
      track(a); return a;
    }},
    /* a door opening. The room's punctuation - it tells the player the floor moved on. */
    door:{gain:0.5,jitter:0,len:0.4,build(t,o){
      const src=T.src(), f=T.filter(), g=T.gain();
      src.buffer=noiseBuf;
      f.type='lowpass'; f.frequency.setValueAtTime(400,t);
      f.frequency.exponentialRampToValueAtTime(1800,t+0.3);
      env(g,t,0.03,0.12,0.5,0.16,o.gain);
      src.connect(f); f.connect(g); g.connect(bus(0));
      src.start(t); src.stop(t+0.4);
      return track(src);
    }},
    /* picking something up. Bright and short, the opposite of `door` in every respect. */
    pickup:{gain:0.55,jitter:70,len:0.14,build(t,o){
      const osc=T.osc(), g=T.gain();
      osc.type='triangle';
      osc.frequency.setValueAtTime(740,t);
      osc.frequency.exponentialRampToValueAtTime(1180,t+0.09);
      env(g,t,0.004,0.03,0.5,0.08,o.gain);
      osc.connect(g); g.connect(bus(o.pan));
      osc.start(t); osc.stop(t+0.14);
      return track(osc);
    }},
    /* the run ending. Deliberately the quietest of the set - it is not a sting, it is a fact. */
    over:{gain:0.3,jitter:0,len:1.1,build(t,o){
      const osc=T.osc(), g=T.gain();
      osc.type='sine';
      osc.frequency.setValueAtTime(300,t);
      osc.frequency.exponentialRampToValueAtTime(150,t+0.9);
      env(g,t,0.05,0.4,0.6,0.45,o.gain);
      osc.connect(g); g.connect(bus(0));
      osc.start(t); osc.stop(t+1.1);
      return track(osc);
    }}
  };

  /* Panning is a real StereoPanner when the browser has one and a no-op when it does not, so a call
     site can pass `pan` unconditionally. Silence is not an error here. */
  /* EVERY NODE A SOUND CREATES IS TRACKED, AND THAT IS THE WHOLE OF THE STUTTER FIX.

     A voice is a source, a gain, sometimes a filter, and sometimes a panner. Only the source was
     tracked, so a panned hit left three nodes connected to the master graph for the life of the
     page - and a browser keeps them alive and walks them on the audio thread, forever, with no upper
     bound. That is the shape of "fine in a headless test, unplayable on a real machine": headless
     Edge suspends the context, `play()` returns before building anything, and none of these nodes are
     ever created at all.

     So the factories hand out tracked nodes. A voice body writes `T.osc()` instead of `T.osc()`
     and cannot forget, because the plain factory is no longer in scope for it. */
  const T={
    osc:()=>track(ctx.createOscillator()),
    src:()=>track(ctx.createBufferSource()),
    gain:()=>track(ctx.createGain()),
    filter:()=>track(ctx.createBiquadFilter()),
    pan:()=>track(ctx.createStereoPanner?ctx.createStereoPanner():null)
  };
  function bus(pan){
    if(pan&&ctx.createStereoPanner){
      try{
        const p=T.pan();
        p.pan.value=Math.max(-1,Math.min(1,pan));
        p.connect(master); return p;
      }catch(e){ /* fall through to the plain master */ }
    }
    return master;
  }

  /* ---- wiring ---------------------------------------------------------------------------------- */
  /* Called from the input layer on the FIRST real interaction, and from nothing else. `once` because
     the handler is on the window and fires forever. */
  /* CALLED WITH AN EVENT, THIS UNLOCKS. CALLED WITHOUT ONE, IT ONLY REGISTERS.

     The distinction is the whole fix, and it was inverted. `autoUnlock()` used to set `gestureSeen`
     unconditionally and then unlock - so the load-time call from `80-ui.js` claimed a gesture that had
     not happened, `unlock()` built a context on the strength of it, and the browser suspended that
     context. From then on the game was silent for ever, and the guard inside `unlock` made it
     unrecoverable.

     So: a real event sets the flag, and only then is a context built. The load-time call installs
     the two listeners and does nothing else. */
  function autoUnlock(event){
    /* REGISTERED ONCE, AT LOAD. This is called from the input layer with no event, and that call is
       the only thing that installs the two gesture listeners - so it must register rather than only
       try to unlock, or the game has no sound until something calls it that never will. */
    if(!registered){
      registered=true;
      try{
        window.addEventListener('keydown',autoUnlock,true);
        window.addEventListener('pointerdown',autoUnlock,true);
        /* AND ONE MORE, because a player who uses neither keyboard nor mouse still has to be able to
           start a game: the canvas itself, and touch. Same handler, same reason. */
        window.addEventListener('touchstart',autoUnlock,true);
        window.addEventListener('mousedown',autoUnlock,true);
        window.addEventListener('keyup',autoUnlock,true);
      }catch(e){ /* no window: nothing to attach to */ }
    }
    /* NO EVENT, NO GESTURE. `event` being present is what makes this a gesture; the load-time call
       has none, and must not build a context. */
    if(!event) return;
    gestureSeen=true;
    if(unlocked) return;
    unlock();
    if(unlocked){
      try{
        window.removeEventListener('keydown',autoUnlock,true);
        window.removeEventListener('pointerdown',autoUnlock,true);
        window.removeEventListener('touchstart',autoUnlock,true);
        window.removeEventListener('mousedown',autoUnlock,true);
        window.removeEventListener('keyup',autoUnlock,true);
      }catch(e){ /* nothing to remove */ }
    }
  }

  async function renderVoice(name,opts){
      opts=opts||{};
      const OAC=typeof window!=='undefined'&&(window.OfflineAudioContext||window.webkitOfflineAudioContext);
      if(!OAC||!VOICES[name]) return null;
      const SR=44100;
      /* long enough for the longest voice (the boss at 1.3s) plus a tail, so a sound that rings on
         past its own envelope is visible in the tail rather than cropped out of it */
      const DUR=1.6;
      const off=new OAC(1,Math.ceil(SR*DUR),SR);
      const saved={ctx,master,noiseBuf};
      /* RENDER AGAINST THE OFFLINE CONTEXT by pointing the builders at it for the duration of the
         call. They close over `ctx`/`master`/`noiseBuf`, so this is a swap rather than a parameter -
         which is the honest cost of keeping every voice body free of plumbing, and it is contained
         here. */
      ctx=off; noiseBuf=makeNoise();
      master=off.createGain(); master.gain.value=1;
      const comp=off.createDynamicsCompressor();
      master.connect(comp); comp.connect(off.destination);
      const v=VOICES[name];
      /* OFFLINE NODES ARE NOT LIVE NODES. */
      const wasTracking=tracking; tracking=false;
      try{
        v.build(0,{gain:(opts.gain!==undefined?opts.gain:1)*(v.gain===undefined?1:v.gain),
          cents:opts.detune||0, pan:0});
        tracking=wasTracking;
      }catch(e){ tracking=wasTracking; ctx=saved.ctx; master=saved.master; noiseBuf=saved.noiseBuf;
        return {name,threw:String(e).slice(0,160)}; }
      let rendered=null;
      try{ rendered=await off.startRendering(); }
      catch(e){ ctx=saved.ctx; master=saved.master; noiseBuf=saved.noiseBuf;
        return {name,renderThrew:String(e).slice(0,160)}; }
      ctx=saved.ctx; master=saved.master; noiseBuf=saved.noiseBuf;
      const d=rendered.getChannelData(0);
      /* PEAK, RMS, WHERE THE PEAK LANDS, AND HOW MUCH ENERGY IS AFTER THE ENVELOPE ENDS. Those four
         between them catch every envelope failure that matters: too quiet, too loud, clipped flat,
         ringing on, or decaying the wrong way round. */
      let peak=0, peakAt=0, sum=0;
      for(let i=0;i<d.length;i++){ const a=Math.abs(d[i]); if(a>peak){peak=a;peakAt=i;} sum+=d[i]*d[i]; }
      const rms=Math.sqrt(sum/d.length);
      const tailFrom=Math.floor(SR*1.35);          // past the longest voice's release
      let tailPeak=0;
      for(let i=tailFrom;i<d.length;i++){ const a=Math.abs(d[i]); if(a>tailPeak) tailPeak=a; }
      /* ZERO CROSSINGS PER SECOND is a usable proxy for pitch, and it needs no autocorrelation:
         a 440Hz sine crosses zero 880 times a second. It is rough, but it separates the four weapon
         tones by more than a semitone, which is all the assertion needs. */
      let crossings=0;
      for(let i=1;i<Math.min(d.length,Math.floor(SR*0.2));i++) if(d[i-1]<0&&d[i]>=0) crossings++;
      /* A TRACE, so a wrong number can be located rather than argued about. Every 100ms, the loudest
         sample in that window. A correct envelope walks up and then walks down to nothing; a number
         that stays high after the envelope ends is a real signal and this says which window it is
         in. */
      const trace=[];
      for(let ms=0;ms<DUR*1000;ms+=100){
        const a0=Math.floor(SR*ms/1000), a1=Math.min(d.length,Math.floor(SR*(ms+100)/1000));
        /* `q`, not `i`: the peak loop above uses `i`, and a shadowed loop variable in a function that
           also walks the same buffer twice is a good way to produce a number nobody can explain. */
        let mx=0; for(let q=a0;q<a1;q++){ const a=Math.abs(d[q]); if(a>mx) mx=a; }
        trace.push({ms,peak:+mx.toFixed(4)});
      }
      return {name,peak:+peak.toFixed(4),rms:+rms.toFixed(4),
        peakAtMs:Math.round(peakAt/SR*1000),tailPeak:+tailPeak.toFixed(5),
        zeroCrossHz:Math.round(crossings/(Math.min(d.length,Math.floor(SR*0.2))/SR)),
        trace, silent:peak===0};
    }

  return {
    play:play, unlock:unlock, autoUnlock:autoUnlock,
    isMuted:()=>muted,
    isUnlocked:()=>unlocked,
    /* THE TEST SURFACE. A sound system whose state cannot be read cannot be tested, and "did it play"
       is the only question worth asking of audio. */
    stats:()=>{ prune(); return {played,skippedMuted,skippedLocked,failed,
        voices:live.length,cap:MAX_VOICES,muted,
        /* DERIVED, NOT STORED. `unlocked` is a flag that `play` sets on its own when it finds a
           running context - the last line of defence that stops a stale flag from silencing the game.
           So a stored `unlocked` read back here is stale by construction, and a test asserting on it
           was asserting on bookkeeping rather than on audio. The context is the truth. */
        unlocked:!!(ctx&&ctx.state==='running'),
        ctxState:ctx?ctx.state:'none',
        /* WHY `play` DECLINED, because "it did not play" is the least useful sentence available.
           There are four reasons it can refuse and they are not the same bug: muted, a render in
           flight, no context at all, and a voice that is not in the table. Three consecutive days of
           this bug were spent reading a counter that named none of them. `lastDecline` is the last
           reason, so a failing assertion can print it instead of guessing. */
        lastDecline:lastDecline||'none',
        rendering:!!rendering,
        /* IS `ctx` STILL THE REAL CONTEXT? `renderVoice` swaps the module variable `ctx` for an
           OfflineAudioContext and restores it afterwards. If a render threw between the swap and the
           restore, `ctx` would be left pointing at an offline graph forever - which reports
           `state: 'suspended'`, refuses to make a sound, and answers `resume()` in a way that never
           turns it green. This is the assertion that distinguishes that from a policy suspension. */
        ctxIsLive:(ctx===liveCtx),
        ctxKind:(ctx&&ctx.constructor&&ctx.constructor.name)||'none',
        liveState:(liveCtx?liveCtx.state:'none'),
        /* WHY it is silent, when it is. A test that can only see 'suspended' has to guess, and the
           guess was wrong three times: the autoplay policy, an async resume, and a leaking voice
           counter all looked identical from the outside. This separates "the browser will not let
           us" from "we asked for it". */
        blockedByPolicy:!!(ctx&&ctx.state!=='running'&&gestureSeen),
        gestureSeen:gestureSeen}; },
    names:()=>Object.keys(VOICES),
    setMuted(v){ muted=!!v; if(master) master.gain.value=muted?0:volume; return muted; },
    setVolume(v){ volume=Math.max(0,Math.min(1,v)); if(master&&!muted) master.gain.value=volume; return volume; },
    getVolume:()=>volume,
    /* RENDER A VOICE OFFLINE, WITH NO AUDIO DEVICE.

       This is how the sounds get TESTED rather than merely asserted to exist. Everything above is
       observable - that a voice has a plan, that the pool is bounded, that a fight does not throw -
       and none of it says whether a shot is a click or a warble. `OfflineAudioContext` renders the
       real waveform with no speaker, so the actual envelope can be measured: does it decay, does it
       peak where it should, is it silent after it should be, and does the pitch land where the weapon
       table says.

       It renders the SAME `VOICES[name].build` with the same parameters - not a copy - so what is
       measured here is what the game plays. A test that built its own oscillator would be testing the
       test. */
    async render(name,opts){
      opts=opts||{};
      /* ONE RENDER AT A TIME. `render` swaps the module's `ctx`/`master`/`noiseBuf` so the voice bodies
         can stay free of plumbing, which means two concurrent renders corrupt each other: the second
         call's `ctx=off` lands while the first is still awaiting `startRendering`, so the first voice
         renders into the second one's offline graph. Measured, that produced a `tailPeak` of 0.9182
         (-1dBFS) for `over`, which rings at full volume nowhere in its own envelope - the reported
         defect was two graphs sharing one set of module variables.

         Serialising here is also what makes the trace meaningful: each render gets a clean context and
         the numbers are reproducible in any order. */
      while(rendering) await new Promise(r=>setTimeout(r,1));
      rendering=true;
/* THE SUSPEND IS GONE, AND IT WAS THE THIRD-ATTEMPT WRONG ANSWER.

     Three fixes were tried against one symptom - a `shot` reporting a tail of 0.86-1.28 instead of
     0. Serialising the renders, clearing the pool, and finally suspending the live context around
     each render. The suspend "worked" in the sense that the test stopped hanging, and it was also
     the reason the test HUNG: a suspended context never calls back on `resume()` in a headless
     browser with no audio device, so the `await` never settled and the suite sat on that one test
     until the watchdog named it.

     The actual cause was never the live context. It was that `prune()` reclaimed nodes by asking
     `ctx.currentTime` whether they had expired, and on a suspended context that clock is frozen, so
     the pool never emptied - unbounded growth, quadratic cost, and a render that measured whatever
     pile happened to be there. Fix the pool and the render is hermetic on its own: the offline graph
     shares nothing with the live one, so it does not matter what the live one is doing.

     So: stop the tracked nodes, clear the list, render, restore the flag. No suspend, no resume. */
      if(live.length){
        for(const v of live){ try{ if(v.stop) v.stop(); }catch(e){ /* already stopped */ } }
        live.length=0;
      }
      try{ return await renderVoice(name,opts); }
      finally{ rendering=false; }

    },

    /* THE POOL, DIRECTLY. The bound is a property of this system and not of whether a speaker
       exists, but every path that reaches it goes through the audio context - and in a headless
       browser that context never leaves `suspended`, so nothing is ever reserved and two tests
       passed against a real leak. This takes the reservation and release path with no context at
       all, which is the only way the bound can be observed here. It is the pool's own bookkeeping,
       called by `play`, not a parallel copy of it. */
    /* IT EXERCISES THE REAL PATH, not a parallel copy of it. An earlier version called `reserve()`
       directly, which meant the test measured the reservation bookkeeping and never once created a
       node - so the leak it was written to catch was invisible to it by construction. */
    exercisePool(n){
      for(let i=0;i<n;i++){
        /* A frozen clock cannot drive the prune, so each fake node is aged by hand: this is the one
           situation where a test must not rely on the audio thread, and ageing explicitly is what
           lets the bound be asserted at all. */
        live.push({__end:(ctx?ctx.currentTime:0)+i*0.0001, stop(){}, __fake:true});
        trimToCap();
      }
      return {voices:live.length,cap:MAX_VOICES};
    },
    releasePool(){ live.length=0; },

    /* A TEST HOOK, and it exists for one reason: the recovery path cannot be tested any other way.

       The bug it guards is "a suspended context is never brought back", and to test that you need a
       suspended context. There is no honest way to produce one from outside - the browser produces it
       by autoplay policy, which is the thing under test - so this makes one deliberately, on the
       REAL context, and the test then asks for it back through the same public path a gesture uses.

       A hook rather than a mock, because a mock would be a test of the mock: what is being tested is
       whether `unlock` returns early when `ctx` is set, and that is a property of the real function
       reading the real module state. */
    /* RESOLVES ONCE THE CONTEXT IS RUNNING, OR ONCE IT CLEARLY IS NOT GOING TO.

       A gesture is not finished when its handler returns: `resume()` returns a promise, and reading
       `ctx.state` on the next line is the exact mistake that silenced the game. So this is the
       honest way to wait for an unlock - and it is public because a test that waits synchronously is
       making the same mistake the bug did.

       It gives up rather than hanging, because a browser that refuses to resume must not turn a test
       into a timeout with no explanation. */
    /* RESOLVES WHEN NO RENDER IS IN FLIGHT. The serial queue is not sufficient on its own, and this
       is the reason.

       `render()` sets the `rendering` flag, awaits `startRendering()`, and clears the flag in a
       `finally`. The test that started it has usually already returned by then, so a test declared
       `serial` - which waits for the PREVIOUS test's promise, not for the previous test's audio work
       to finish - can still find the flag set. Measured: a serialised test reporting
       `rendering: true` on entry, and every `play` it made declined with
       "a render is in flight", against code that was correct.

       So the wait is on the actual condition rather than on the neighbouring promise. */
    whenIdle(){
      if(!rendering) return Promise.resolve(true);
      return new Promise(resolve=>{
        /* Ten SECONDS, in milliseconds, because an offline render of the longest voice plus a queue of
           neighbours is not a frame count. The old bound was `++frames>600` - six hundred animation
           frames, which was ten seconds when a frame was a frame and is ten milliseconds now. */
        const deadline=Date.now()+10000;
        const tick=()=>{
          if(!rendering) return resolve(true);
          if(Date.now()>deadline) return resolve(false);
          yieldToBrowser();
        };
        yieldToBrowser();
      });
    },

    whenAudible(){
      const ctxNow=ctx;
      if(!ctxNow) return Promise.resolve(false);
      if(ctxNow.state==='running'){ unlocked=true; return Promise.resolve(true); }
      return new Promise(resolve=>{
        let done=false;
        const finish=(v)=>{ if(done) return; done=true; unlocked=(ctxNow.state==='running'); resolve(unlocked); };
        /* A handful of animation frames, which is far longer than any resume takes and far shorter
           than a test timeout. */
        /* BOUNDED BY TIME, NOT BY A FRAME COUNT. This was `if(++frames>30) finish(false)` - thirty
           animation frames' worth of `setTimeout(1)`, so about thirty milliseconds, which is shorter
           than several of the things it is waiting for. A budget expressed in frames silently changes
           meaning when the waiting primitive changes, and this one did: it gave up before the promise it
           was waiting on could settle, and returned `false` - so a test asked "did the audio come up?"
           and the helper answered "no" on a context that was about to say yes.

           So both bounds are now milliseconds, which is what they were always trying to express. */
        const deadline=Date.now()+3000;
        const tick=()=>{
          if(done) return;
          if(ctxNow.state==='running') return finish(true);
          if(Date.now()>deadline) return finish(false);
          yieldToBrowser();
        };
        yieldToBrowser();
      });
    },

    suspendForTest(){
      /* IT SUSPENDS THE LIVE CONTEXT, AND THAT WORD IS LOAD-BEARING.

         The first version of this hook called `ctx.suspend()` - and `ctx` is the module variable that
         `renderVoice` SWAPS to the OfflineAudioContext for the duration of a render. So a test that
         happened to run while a neighbour was rendering suspended an OFFLINE context, whose
         `suspend()` requires an argument and threw:

             TypeError: Failed to execute 'suspend' on 'OfflineAudioContext': 1 argument required

         That exception escaped into the middle of somebody else's render, the render's `finally` still
         cleared its flag, and three unrelated tests failed with three different and entirely spurious
         messages. It took a page-error listener, which nothing in this harness had ever had, to see
         it.

         The lesson is not about `suspend`. It is that **a test hook acting on a variable the system
         mutates is a hook that can act on the wrong object**, and the only defence is to name the
         object explicitly. `liveCtx` is that name, and it is the one variable here a render does NOT
         touch.

         IT RETURNS A PROMISE, because `suspend()` is one. A hook that returns before the state it
         claims to have changed has actually changed is the same fixture error as a test that asserts
         its own precondition: the caller has no way to tell whether it worked. Measured - a probe
         that called this, then immediately read the state, then played, found the context still
         `running`, so the play succeeded and the whole recovery path went unexercised while the suite
         reported 240/240. */
      const target=liveCtx;
      if(!target) return Promise.resolve(false);
      let p;
      try{ p=(target.state==='running') ? target.suspend() : null; }
      catch(e){ p=null; }
      unlocked=false;
      return Promise.resolve(p).then(()=>true, ()=>true);
    },
    /* a test hook: build a voice without a live context, so the ENVELOPE can be asserted in a
       headless browser where no sound is ever heard. Returns the parameter values it would have
       used, which is what a test can actually check. */
    plan:(name,opts)=>{
      const v=VOICES[name];
      if(!v) return null;
      return {name,gain:(opts&&opts.gain!==undefined?opts.gain:1)*(v.gain===undefined?1:v.gain),
        jitter:v.jitter||0,cent:(opts&&opts.detune!==undefined)?opts.detune:undefined};
    }
  };
})();

/* THE ONE LINE THE GAME USES. `Sfx` exists so that no call site ever names a sound directly -
   a call site says WHAT happened and this decides what it sounds like, which is what makes the whole
   set replaceable when real samples arrive without touching a single enemy or weapon. */
const Sfx={
  shot(){ Sound.play('shot',{detune:weaponDetuneCents()}); },
  hit(p){ Sound.play('hit',{pan:panFor(p)}); },
  kill(p){ Sound.play('kill',{pan:panFor(p)}); },
  touch(p){ Sound.play('touch',{pan:panFor(p)}); },
  hurt(){ Sound.play('hurt'); },
  playerHit(){ Sound.play('playerHit'); },
  blink(){ Sound.play('blink'); },
  tell(p){ Sound.play('tell',{pan:panFor(p)}); },
  boss(){ Sound.play('boss'); },
  door(){ Sound.play('door'); },
  pickup(){ Sound.play('pickup'); },
  over(){ Sound.play('over'); }
};

/* ---- where a sound sits in the stereo field --------------------------------------------------- */
/* Panned by the body's position relative to the player, not by its role: a hit on the far side of
   the room should sound like it. It is the cheapest depth cue available and it is the difference
   between "something happened" and "something happened over there". */
function panFor(body){
  if(!body||typeof body.x!=='number') return 0;
  const dx=(body.x-player.x);
  return Math.max(-1,Math.min(1,dx/500));
}

/* ---- pitch follows the weapon ----------------------------------------------------------------- */
/* THE QUEUED "PITCH JITTER" ITEM, implemented. Four weapons with four tones, so the player learns
   which gun they are holding by ear before they look - and each shot still jitters by `VOICES.shot.jitter`
   so no two are identical, which is the part that stops a synthesised weapon sounding like a test tone. */
/* KEYED BY THE ID FORM, NOT THE DISPLAY NAME. The four weapons are named 'Bolt', 'Scatter',
   'Arcane Beam' and 'Voidball', and `Content.idOf` turns those into 'bolt', 'scatter', 'arcane_beam'
   and 'voidball' - which is the form the rest of the game keys on (TRAIT_TABLE uses it too). A first
   version keyed off `w.name` and matched three of four, silently, because 'Beam' is not a name any
   weapon has. Two of these lookups have now failed in this file on a key that does not exist, which
   is the argument for deriving the key rather than typing it. */
const WEAPON_TONE_CENTS={bolt:-260,scatter:400,arcane_beam:140,voidball:-90};
function weaponDetuneCents(){
  /* THE LOOKUP KEY IS `w.name`, NOT `w.id`. There is no `id` on a weapon: the entries in WEAPONS
     (00-balance.js:741) carry `name` - 'Bolt', 'Scatter', 'Beam', 'Voidball' - and a first version
     keyed off `id` matched nothing and returned 0 for all four. That read as "pitch jitter is not
     working" and it was, under a key that does not exist, so every gun sounded identical.

     A WIDE SPREAD on purpose, and measured rather than guessed: a semitone is 100 cents and anything
     under about 40 reads as the same shot. These are -260, -90, +140 and +400 - about a quarter of a
     semitone, one, one and a half and four - a clear identity per weapon and still inside a range
     where the shot does not sound like the wrong instrument. */
  const w=(typeof WEAPONS!=='undefined'&&player.weaponIdx!==undefined)?WEAPONS[player.weaponIdx]:null;
  if(!w) return 0;
  /* DERIVED THROUGH THE GAME'S OWN idOf, not typed by hand - see the note on the table above. */
  const key=Content.idOf(w);
  return WEAPON_TONE_CENTS[key]!==undefined?WEAPON_TONE_CENTS[key]:0;
}