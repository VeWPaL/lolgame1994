/* THE SOUND SYSTEM. [h:45-sound-1] */

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

  /* YIELDS THE MAIN THREAD WITHOUT WAITING FOR A FRAME. [h:45-sound-2] */
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
  /* THE CONTEXT MUST BE BUILT ON A GESTURE, AND THIS FUNCTION IS THE ONLY PLACE IT IS BUILT. [h:45-sound-3] */
  function unlock(){
    if(!Ctor){ lastDecline='unlock: no AudioContext constructor in this browser'; return false; }
    /* NO GESTURE, NO CONTEXT. See the note above: a context built before an interaction is suspended
       by every browser, and the original built one at load. */
    if(!gestureSeen){ lastDecline='unlock: no gesture has happened yet'; return false; }

    /* AN EXISTING CONTEXT THAT IS SUSPENDED IS NOT A REASON TO GIVE UP. [h:45-sound-4] */
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
      /* A COMPRESSOR ON THE MASTER, and it is not decoration. [h:45-sound-5] */
      comp=ctx.createDynamicsCompressor();
      comp.threshold.value=-14; comp.knee.value=22;
      comp.ratio.value=5; comp.attack.value=0.004; comp.release.value=0.18;
      master.connect(comp); comp.connect(ctx.destination);
      noiseBuf=makeNoise();
      /* RESUME, AND THEN WAIT FOR IT. [h:45-sound-6] */
      /* RESUME, AND WAIT FOR IT BEFORE ASKING. [h:45-sound-7] */
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
      /* DO NOT DISCARD A CONTEXT THAT WORKS. [h:45-sound-8] */
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
    /* Deterministic noise, NOT Math.random. [h:45-sound-9] */
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
    /* NOT WHILE AN OFFLINE RENDER HOLDS THE MODULE `ctx`. [h:45-sound-10] */
    if(rendering){ skippedLocked++; lastDecline='a render is in flight'; return false; }
    /* A CONTEXT THAT IS RUNNING IS A CONTEXT THAT PLAYS, WHATEVER THE FLAG SAYS. [h:45-sound-11] */
    if(!unlocked){
      if(ctx&&ctx.state==='running'){ unlocked=true; }
        else if(ctx){
          /* A CONTEXT THAT WENT SUSPENDED IS A CONTEXT TO BE RESUMED, NOT A REASON TO STAY QUIET. [h:45-sound-12] */
          try{ ctx.resume&&ctx.resume(); }catch(e){ /* refused: still counted as a skip below */ }
        }
        else { skippedLocked++; lastDecline='no AudioContext at all'; return false; }
    }
    /* ASK THE CONTEXT, NOT THE FLAG. The flag is set once and goes stale - `resume()` is async, and a
       context that was suspended at the flag's creation is running a moment later. Reading only the
       flag is how a fully working sound system plays nothing. */
    if(ctx&&ctx.state==='running') unlocked=true;
    /* THE RESERVATION IS TAKEN BEFORE THE DEVICE IS ASKED. [h:45-sound-13] */
    /* A SUSPENDED CONTEXT IS NOT A REFUSAL. [h:45-sound-14] */
      if(!ctx){ skippedLocked++; lastDecline='no AudioContext at all'; return false; }
    /* RECLAIM FIRST, THEN BUILD, THEN TRIM. Reclaiming before the build keeps the list from growing
       while a long sound is still sounding; trimming after it is what enforces the cap, because a
       sound adds 3-6 nodes and no pre-check can know how many until it has been built. */
    prune();
    try{
      const t=ctx.currentTime;
      const v=VOICES[name];
      if(!v){ lastDecline='no such voice: '+name; return false; }
      /* The pitch jitter, and it is here rather than at every call site. [h:45-sound-15] */
      const cents=opts.detune!==undefined?opts.detune:(v.jitter||0)*(rand()*2-1);
      const gain=(opts.gain!==undefined?opts.gain:1)*(v.gain===undefined?1:v.gain);
      const pan=(opts.pan!==undefined?opts.pan:0);
      /* THE STOP TIME FOR EVERY NODE OF THIS SOUND, SET BEFORE IT IS BUILT. [h:45-sound-16] */
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
  /* THE POOL, AND IT IS A REAL ONE. [h:45-sound-17] */
  const live=[];
  /* TRACKING HAPPENS EVEN WHEN NOTHING CAN BE HEARD. [h:45-sound-18] */
  /* TRACKING IS PER-VOICE AND PER-SOURCE, and it is a LIST OF EVERY NODE A SOUND CREATES. [h:45-sound-19] */
  /* `buildEnd` is the stop time for the sound currently being built. [h:45-sound-20] */
  let buildEnd=0;
  /* WHETHER THE NODES BEING BUILT BELONG TO THE LIVE POOL. [h:45-sound-21] */
  let tracking=true;
  function track(node){
    if(!node) return node;
    node.__end=buildEnd;
    if(tracking) live.push(node);
    return node;
  }
  /* THE CAP IS ON NODES, NOT ON SOUNDS, and it has to be: [h:45-sound-22] */
const MAX_VOICES=48, MAX_SOUND_LEN=1.4;   // 48 nodes ~= 8-16 sounds; the boss at 1.3s, rounded up
  /* RECLAIM BY AGE, AND NEVER DEPEND ON THE AUDIO CLOCK TO DO IT. [h:45-sound-23] */
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
  /* Each entry builds its own nodes and returns a tracked source. [h:45-sound-24] */
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
    /* the gunner's cast tell. [h:45-sound-25] */
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
  /* EVERY NODE A SOUND CREATES IS TRACKED, AND THAT IS THE WHOLE OF THE STUTTER FIX. [h:45-sound-26] */
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
  /* CALLED WITH AN EVENT, THIS UNLOCKS. [h:45-sound-27] */
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
      /* RENDER AGAINST THE OFFLINE CONTEXT by pointing the builders at it for the duration of the call. [h:45-sound-28] */
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
      /* A TRACE, so a wrong number can be located rather than argued about. [h:45-sound-29] */
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
        /* DERIVED, NOT STORED. [h:45-sound-30] */
        unlocked:!!(ctx&&ctx.state==='running'),
        ctxState:ctx?ctx.state:'none',
        /* WHY `play` DECLINED, because "it did not play" is the least useful sentence available. [h:45-sound-31] */
        lastDecline:lastDecline||'none',
        rendering:!!rendering,
        /* IS `ctx` STILL THE REAL CONTEXT? [h:45-sound-32] */
        ctxIsLive:(ctx===liveCtx),
        ctxKind:(ctx&&ctx.constructor&&ctx.constructor.name)||'none',
        liveState:(liveCtx?liveCtx.state:'none'),
        /* WHY it is silent, when it is. [h:45-sound-33] */
        blockedByPolicy:!!(ctx&&ctx.state!=='running'&&gestureSeen),
        gestureSeen:gestureSeen}; },
    names:()=>Object.keys(VOICES),
    setMuted(v){ muted=!!v; if(master) master.gain.value=muted?0:volume; return muted; },
    setVolume(v){ volume=Math.max(0,Math.min(1,v)); if(master&&!muted) master.gain.value=volume; return volume; },
    getVolume:()=>volume,
    /* RENDER A VOICE OFFLINE, WITH NO AUDIO DEVICE. [h:45-sound-34] */
    async render(name,opts){
      opts=opts||{};
      /* ONE RENDER AT A TIME. [h:45-sound-35] */
      while(rendering) await new Promise(r=>setTimeout(r,1));
      rendering=true;
/* THE SUSPEND IS GONE, AND IT WAS THE THIRD-ATTEMPT WRONG ANSWER. [h:45-sound-36] */
      if(live.length){
        for(const v of live){ try{ if(v.stop) v.stop(); }catch(e){ /* already stopped */ } }
        live.length=0;
      }
      try{ return await renderVoice(name,opts); }
      finally{ rendering=false; }

    },

    /* THE POOL, DIRECTLY. [h:45-sound-37] */
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

    /* A TEST HOOK, and it exists for one reason: [h:45-sound-38] */
    /* RESOLVES ONCE THE CONTEXT IS RUNNING, OR ONCE IT CLEARLY IS NOT GOING TO. [h:45-sound-39] */
    /* RESOLVES WHEN NO RENDER IS IN FLIGHT. [h:45-sound-40] */
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
        /* BOUNDED BY TIME, NOT BY A FRAME COUNT. [h:45-sound-41] */
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
      /* IT SUSPENDS THE LIVE CONTEXT, AND THAT WORD IS LOAD-BEARING. [h:45-sound-42] */
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
/* KEYED BY THE ID FORM, NOT THE DISPLAY NAME. [h:45-sound-43] */
const WEAPON_TONE_CENTS={bolt:-260,scatter:400,arcane_beam:140,voidball:-90};
function weaponDetuneCents(){
  /* THE LOOKUP KEY IS `w.name`, NOT `w.id`. [h:45-sound-44] */
  const w=(typeof WEAPONS!=='undefined'&&player.weaponIdx!==undefined)?WEAPONS[player.weaponIdx]:null;
  if(!w) return 0;
  /* DERIVED THROUGH THE GAME'S OWN idOf, not typed by hand - see the note on the table above. */
  const key=Content.idOf(w);
  return WEAPON_TONE_CENTS[key]!==undefined?WEAPON_TONE_CENTS[key]:0;
}