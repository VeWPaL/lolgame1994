# 45-sound.js — moved comments

Long comments moved out of `src/45-sound.js`. The code keeps a one-line gist tagged `[h:45-sound-N]`; search this file for that tag.

## [h:45-sound-1]
near: `const Sound=(function(){`

THE SOUND SYSTEM. Synthesised, with no assets - which is what makes it ship inside the .exe.

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
  event, `Sound.play` is the one call the game makes.

## [h:45-sound-2]
near: `const yieldToBrowser=()=>new Promise(r=>setTimeout(r,1));`

YIELDS THE MAIN THREAD WITHOUT WAITING FOR A FRAME.

    `setTimeout(0)`, not `requestAnimationFrame`, and the reason is specific to this project's test
    harness: the suite runs its test bodies while the page is still parsing, so the main thread is
    blocked, and a rAF callback is not delivered until the browser regains control - which it does not
    do while the suite is on the stack. A wait loop built on rAF therefore never completes inside the
    suite, and the symptom is a browser navigation timeout rather than a test failure: the page never
    becomes idle enough for `domcontentloaded` to fire.

    A `setTimeout(0)` macrotask is delivered as soon as the current task yields, so it works in both
    places - in the suite and in a real game loop. Anything that has to wait for the audio clock
    should also be bounded, so a browser that refuses to resume cannot turn a wait into a hang.

## [h:45-sound-3]
near: `function unlock(){`

THE CONTEXT MUST BE BUILT ON A GESTURE, AND THIS FUNCTION IS THE ONLY PLACE IT IS BUILT.

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
         cannot be `if(ctx) return false`.

## [h:45-sound-4]
near: `if(ctx){`

AN EXISTING CONTEXT THAT IS SUSPENDED IS NOT A REASON TO GIVE UP. This is part 3 of the fix and
      it is the part that makes the other two recoverable: the old guard was `if(ctx) return false`,
      so a context created in the wrong state could never be repaired - the one thing that would have
      fixed it is the one thing it prevented.

## [h:45-sound-5]
near: `comp=ctx.createDynamicsCompressor();`

A COMPRESSOR ON THE MASTER, and it is not decoration. Six shells, four Brunch and a boss
        volley arriving together is a peak well past 1.0, and clipping a peak is a harsh artefact
        that reads as a mistake in the game rather than in the audio. Soft-knee compression is the
        cheapest fix and the one that costs no per-sound tuning.

## [h:45-sound-6]
near: `let settled=false, resumed=false;`

RESUME, AND THEN WAIT FOR IT. `resume()` returns a promise and `ctx.state` does not change
        until it settles, so reading the state immediately after calling it - which is what the
        first version did - reports 'suspended' even on a context that is about to run. That is the
        third way this file failed to make a sound in a browser that supports audio perfectly well:
        the check was correct and the timing was not.

        Measured in headless Edge with no game involved: a raw context is `running` on create and
        `running` after resume, and `currentTime` does not advance because there is no audio device.
        The game's own context reported `suspended` on the same page, which is this bug.

## [h:45-sound-7]
near: `let settled=false, resumed=false;`

RESUME, AND WAIT FOR IT BEFORE ASKING. This is part 2 of the fix.

        `resume()` returns a promise and the state does not change until it settles, so reading
        `ctx.state` on the line after the call is reading it too early - it reported 'suspended' on a
        context that was about to run, and on the real machine it never ran at all.

        So the promise is the thing that is waited on, and the state is read after. The synchronous
        read is kept as well, because a context that is ALREADY running needs no wait and a game
        should not be silent for a frame because of it.

## [h:45-sound-8]
near: `failed++;`

DO NOT DISCARD A CONTEXT THAT WORKS. This line used to be `catch(e){ failed++; ctx=null; }`,
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
        null and no route back to sound.

## [h:45-sound-9]
near: `let s=0x9e3779b9;`

Deterministic noise, NOT Math.random. The game's whole simulation is seeded and reproducible,
      and a sound that changes the RNG stream would make audio the first thing to break parity - or
      worse, the first thing to break a save. A xorshift with a fixed seed keeps the sounds varied
      and the run identical.

## [h:45-sound-10]
near: `if(rendering){ skippedLocked++; lastDecline='a render is in flight'; return false; }`

NOT WHILE AN OFFLINE RENDER HOLDS THE MODULE `ctx`. THIS IS THE ACTUAL CAUSE OF A CORRUPTED
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
      context - were all aimed at the LIVE graph. The live graph was never the problem.

## [h:45-sound-11]
near: `if(!unlocked){`

A CONTEXT THAT IS RUNNING IS A CONTEXT THAT PLAYS, WHATEVER THE FLAG SAYS.

      This is the last line of defence, and it exists because the flag was the single point of
      failure for the whole system: `unlocked` was read from `ctx.state` one line after calling
      `resume()`, which is before the promise settles, so it was false on a context that was about to
      run - and every voice went to `skippedLocked` while the game looked completely healthy.

      Asking the CONTEXT here means a flag that is stale or late cannot silence the game. `resume()`
      is only called when the context is genuinely not running, so this costs a comparison in the
      common case and a resume on the rare one.

## [h:45-sound-12]
near: `try{ ctx.resume&&ctx.resume(); }catch(e){ /* refused: still counted as a skip below */ }`

A CONTEXT THAT WENT SUSPENDED IS A CONTEXT TO BE RESUMED, NOT A REASON TO STAY QUIET.

            This is not the autoplay case - that is handled in `unlock`, on a gesture. This is the
            case where the game WAS audible and then stopped being: the tab was backgrounded, the
            machine changed audio device, the OS took the device exclusive. In every one of those the
            browser suspends the context, and the original code here simply declined to play and
            counted a skip, for ever.

            So it asks for a resume on the play itself. A node scheduled against a suspended context
            is not lost - it plays when the context comes back - which is the behaviour wanted, and
            it costs one state comparison in the common case.

## [h:45-sound-13]
near: `if(!ctx){ skippedLocked++; lastDecline='no AudioContext at all'; return false; }`

THE RESERVATION IS TAKEN BEFORE THE DEVICE IS ASKED. The pool is a property of this system, so
      it is bounded whether or not anything can be heard - and a headless browser, where the context
      never leaves `suspended`, is the only place the bound CAN be tested at all. Reserving after the
      state check meant the count stayed at zero there and two tests passed against a real leak.

## [h:45-sound-14]
near: `if(!ctx){ skippedLocked++; lastDecline='no AudioContext at all'; return false; }`

A SUSPENDED CONTEXT IS NOT A REFUSAL. This is the only thing standing between a tab that was
      backgrounded and a game that is silent until it is reloaded.

      The original line was `if(!unlocked||!ctx||ctx.state!=='running')` - it declined to play
      whenever the context was not running, and the only thing that ever resumed a context was a
      gesture through `unlock`. So: a player alt-tabs mid-fight, comes back, and the game is muted
      for ever with no way back except a page reload. The `resume()` above is on the play itself, and
      a node scheduled against a context that is about to run is not lost - it plays.

      What is still refused is a browser with NO context, which genuinely cannot make a sound, and
      that must stay visible rather than being papered over by a green counter.

## [h:45-sound-15]
near: `const cents=opts.detune!==undefined?opts.detune:(v.jitter||0)*(rand()*2-1);`

The pitch jitter, and it is here rather than at every call site. `detune` is in cents, so a
        value of 20 is a fifth of a semitone either way - audible as "not a machine", inaudible as
        "wrong". A repeated shot at an identical pitch is the single thing that makes a synthesised
        sound feel like a beep; this is the whole fix and it costs one number.

## [h:45-sound-16]
near: `buildEnd=t+((v.len!==undefined)?v.len:MAX_SOUND_LEN);`

THE STOP TIME FOR EVERY NODE OF THIS SOUND, SET BEFORE IT IS BUILT. Each voice declares its
        own length - `len` in seconds - and `track` stamps it onto every node the voice creates, so
        a gain and a filter are reclaimed exactly when their source is. A voice with no `len` gets
        the longest, which over-reclaims rather than leaks.

## [h:45-sound-17]
near: `const live=[];`

THE POOL, AND IT IS A REAL ONE.

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
    actually ends it - `onended` is only ever used to tidy the list.

## [h:45-sound-18]
near: `let buildEnd=0;`

TRACKING HAPPENS EVEN WHEN NOTHING CAN BE HEARD.

    The voice bookkeeping is tracked independently of whether the audio device will play it. A first
    version only tracked inside `build`, so on a browser that refuses the context - a headless one,
    or any page where the autoplay policy has not been satisfied - `live` stayed empty and the
    bound could not be observed at all. Two tests passed against the leak for exactly that reason:
    they read a counter that nothing was incrementing.

    `reserve()` is therefore called from `play`, before the voice is built, and released by `prune`
    on the same schedule. The pool is a property of the SYSTEM, not of whether a speaker exists.

## [h:45-sound-19]
near: `let buildEnd=0;`

TRACKING IS PER-VOICE AND PER-SOURCE, and it is a LIST OF EVERY NODE A SOUND CREATES.

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
    bound is short.

## [h:45-sound-20]
near: `let buildEnd=0;`

`buildEnd` is the stop time for the sound currently being built. `track` reads it, so a voice body
    can call `track(node)` on every node it creates without threading an argument through - which is
    what kept the first version from doing it. The alternative is a parameter on every voice's every
    line, and a voice that forgets one leaks one node.

## [h:45-sound-21]
near: `let tracking=true;`

WHETHER THE NODES BEING BUILT BELONG TO THE LIVE POOL. An offline render builds a whole voice
    through the same factories, and those nodes belong to a graph that is thrown away when the
    render finishes. Tracking them was a real bug with a real symptom: `render()` clears the live
    pool before measuring, so a render that had tracked its nodes left the NEXT render's clear
    stopping a graph that was mid-render - measured as `shot` reporting a tail of 0.78 at
    -2dBFS, at 960x600 and not at 1280x720, because the timing of two async renders is the only
    thing that decides which one gets interrupted.

## [h:45-sound-22]
near: `const MAX_VOICES=48, MAX_SOUND_LEN=1.4;   // 48 nodes ~= 8-16 sounds; the boss at 1.3s, rounded up`

THE CAP IS ON NODES, NOT ON SOUNDS, and it has to be: a sound is 3-6 nodes, so a cap of 24 NODES
  is about six simultaneous sounds - which is the right number for a fight, and is a quarter of what
  24 sounds would have been. A node cap that claimed to be a sound cap was counting the wrong thing
  by a factor of four, which is how the first leak looked like a small number.

## [h:45-sound-23]
near: `function prune(){`

RECLAIM BY AGE, AND NEVER DEPEND ON THE AUDIO CLOCK TO DO IT.

    `ctx.currentTime` only advances while the context is RUNNING. On a suspended context - which is
    every headless browser, and any tab the player has muted - it is frozen, so a prune that asks
    "is this node past its end time?" reclaims nothing, ever. The list then grows by 3-6 nodes per
    shot with nothing ever removed, and because `play()` walks it on every trigger pull the cost is
    quadratic in the length of the fight. Measured: a suite run reached 86,606 sounds and stopped
    responding entirely.

    So the bound is enforced STRUCTURALLY - trim to the cap after building, oldest first - and the
    clock is only used to release things early. Correctness does not depend on it.

## [h:45-sound-24]
near: `const VOICES={`

Each entry builds its own nodes and returns a tracked source. `jitter` is in cents.
    `gap` is the PITCH VARIATION BETWEEN REPEATS, which is the item that was queued - see the note
    on `play` above. Shapes and levels here are PLACEHOLDERS: the envelope SHAPES are chosen to be
    correct for the event (a shot is a click with a fast decay, an impact has a body) so the timing
    can be judged against the fight, and the exact tone is what real samples replace.

## [h:45-sound-25]
near: `tell:{gain:0.16,jitter:15,len:0.55,build(t,o){`

the gunner's cast tell. THE MOST IMPORTANT SOUND IN THE GAME, because it is the only warning a
      ranged body gives and it is a half-second long. It is quiet, it is a rising tone, and it is
      mixed UNDER everything - if the player cannot hear it over a fight, the mechanic they are
      dodging has no tell in practice.

## [h:45-sound-26]
near: `const T={`

EVERY NODE A SOUND CREATES IS TRACKED, AND THAT IS THE WHOLE OF THE STUTTER FIX.

    A voice is a source, a gain, sometimes a filter, and sometimes a panner. Only the source was
    tracked, so a panned hit left three nodes connected to the master graph for the life of the
    page - and a browser keeps them alive and walks them on the audio thread, forever, with no upper
    bound. That is the shape of "fine in a headless test, unplayable on a real machine": headless
    Edge suspends the context, `play()` returns before building anything, and none of these nodes are
    ever created at all.

    So the factories hand out tracked nodes. A voice body writes `T.osc()` instead of `T.osc()`
    and cannot forget, because the plain factory is no longer in scope for it.

## [h:45-sound-27]
near: `function autoUnlock(event){`

CALLED WITH AN EVENT, THIS UNLOCKS. CALLED WITHOUT ONE, IT ONLY REGISTERS.

    The distinction is the whole fix, and it was inverted. `autoUnlock()` used to set `gestureSeen`
    unconditionally and then unlock - so the load-time call from `80-ui.js` claimed a gesture that had
    not happened, `unlock()` built a context on the strength of it, and the browser suspended that
    context. From then on the game was silent for ever, and the guard inside `unlock` made it
    unrecoverable.

    So: a real event sets the flag, and only then is a context built. The load-time call installs
    the two listeners and does nothing else.

## [h:45-sound-28]
near: `ctx=off; noiseBuf=makeNoise();`

RENDER AGAINST THE OFFLINE CONTEXT by pointing the builders at it for the duration of the
        call. They close over `ctx`/`master`/`noiseBuf`, so this is a swap rather than a parameter -
        which is the honest cost of keeping every voice body free of plumbing, and it is contained
        here.

## [h:45-sound-29]
near: `const trace=[];`

A TRACE, so a wrong number can be located rather than argued about. Every 100ms, the loudest
        sample in that window. A correct envelope walks up and then walks down to nothing; a number
        that stays high after the envelope ends is a real signal and this says which window it is
        in.

## [h:45-sound-30]
near: `unlocked:!!(ctx&&ctx.state==='running'),`

DERIVED, NOT STORED. `unlocked` is a flag that `play` sets on its own when it finds a
          running context - the last line of defence that stops a stale flag from silencing the game.
          So a stored `unlocked` read back here is stale by construction, and a test asserting on it
          was asserting on bookkeeping rather than on audio. The context is the truth.

## [h:45-sound-31]
near: `lastDecline:lastDecline||'none',`

WHY `play` DECLINED, because "it did not play" is the least useful sentence available.
          There are four reasons it can refuse and they are not the same bug: muted, a render in
          flight, no context at all, and a voice that is not in the table. Three consecutive days of
          this bug were spent reading a counter that named none of them. `lastDecline` is the last
          reason, so a failing assertion can print it instead of guessing.

## [h:45-sound-32]
near: `ctxIsLive:(ctx===liveCtx),`

IS `ctx` STILL THE REAL CONTEXT? `renderVoice` swaps the module variable `ctx` for an
          OfflineAudioContext and restores it afterwards. If a render threw between the swap and the
          restore, `ctx` would be left pointing at an offline graph forever - which reports
          `state: 'suspended'`, refuses to make a sound, and answers `resume()` in a way that never
          turns it green. This is the assertion that distinguishes that from a policy suspension.

## [h:45-sound-33]
near: `blockedByPolicy:!!(ctx&&ctx.state!=='running'&&gestureSeen),`

WHY it is silent, when it is. A test that can only see 'suspended' has to guess, and the
          guess was wrong three times: the autoplay policy, an async resume, and a leaking voice
          counter all looked identical from the outside. This separates "the browser will not let
          us" from "we asked for it".

## [h:45-sound-34]
near: `async render(name,opts){`

RENDER A VOICE OFFLINE, WITH NO AUDIO DEVICE.

      This is how the sounds get TESTED rather than merely asserted to exist. Everything above is
      observable - that a voice has a plan, that the pool is bounded, that a fight does not throw -
      and none of it says whether a shot is a click or a warble. `OfflineAudioContext` renders the
      real waveform with no speaker, so the actual envelope can be measured: does it decay, does it
      peak where it should, is it silent after it should be, and does the pitch land where the weapon
      table says.

      It renders the SAME `VOICES[name].build` with the same parameters - not a copy - so what is
      measured here is what the game plays. A test that built its own oscillator would be testing the
      test.

## [h:45-sound-35]
near: `while(rendering) await new Promise(r=>setTimeout(r,1));`

ONE RENDER AT A TIME. `render` swaps the module's `ctx`/`master`/`noiseBuf` so the voice bodies
        can stay free of plumbing, which means two concurrent renders corrupt each other: the second
        call's `ctx=off` lands while the first is still awaiting `startRendering`, so the first voice
        renders into the second one's offline graph. Measured, that produced a `tailPeak` of 0.9182
        (-1dBFS) for `over`, which rings at full volume nowhere in its own envelope - the reported
        defect was two graphs sharing one set of module variables.

        Serialising here is also what makes the trace meaningful: each render gets a clean context and
        the numbers are reproducible in any order.

## [h:45-sound-36]
near: `if(live.length){`

THE SUSPEND IS GONE, AND IT WAS THE THIRD-ATTEMPT WRONG ANSWER.

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

    So: stop the tracked nodes, clear the list, render, restore the flag. No suspend, no resume.

## [h:45-sound-37]
near: `exercisePool(n){`

THE POOL, DIRECTLY. The bound is a property of this system and not of whether a speaker
      exists, but every path that reaches it goes through the audio context - and in a headless
      browser that context never leaves `suspended`, so nothing is ever reserved and two tests
      passed against a real leak. This takes the reservation and release path with no context at
      all, which is the only way the bound can be observed here. It is the pool's own bookkeeping,
      called by `play`, not a parallel copy of it.

## [h:45-sound-38]
near: `whenIdle(){`

A TEST HOOK, and it exists for one reason: the recovery path cannot be tested any other way.

      The bug it guards is "a suspended context is never brought back", and to test that you need a
      suspended context. There is no honest way to produce one from outside - the browser produces it
      by autoplay policy, which is the thing under test - so this makes one deliberately, on the
      REAL context, and the test then asks for it back through the same public path a gesture uses.

      A hook rather than a mock, because a mock would be a test of the mock: what is being tested is
      whether `unlock` returns early when `ctx` is set, and that is a property of the real function
      reading the real module state.

## [h:45-sound-39]
near: `whenIdle(){`

RESOLVES ONCE THE CONTEXT IS RUNNING, OR ONCE IT CLEARLY IS NOT GOING TO.

      A gesture is not finished when its handler returns: `resume()` returns a promise, and reading
      `ctx.state` on the next line is the exact mistake that silenced the game. So this is the
      honest way to wait for an unlock - and it is public because a test that waits synchronously is
      making the same mistake the bug did.

      It gives up rather than hanging, because a browser that refuses to resume must not turn a test
      into a timeout with no explanation.

## [h:45-sound-40]
near: `whenIdle(){`

RESOLVES WHEN NO RENDER IS IN FLIGHT. The serial queue is not sufficient on its own, and this
      is the reason.

      `render()` sets the `rendering` flag, awaits `startRendering()`, and clears the flag in a
      `finally`. The test that started it has usually already returned by then, so a test declared
      `serial` - which waits for the PREVIOUS test's promise, not for the previous test's audio work
      to finish - can still find the flag set. Measured: a serialised test reporting
      `rendering: true` on entry, and every `play` it made declined with
      "a render is in flight", against code that was correct.

      So the wait is on the actual condition rather than on the neighbouring promise.

## [h:45-sound-41]
near: `const deadline=Date.now()+3000;`

BOUNDED BY TIME, NOT BY A FRAME COUNT. This was `if(++frames>30) finish(false)` - thirty
          animation frames' worth of `setTimeout(1)`, so about thirty milliseconds, which is shorter
          than several of the things it is waiting for. A budget expressed in frames silently changes
          meaning when the waiting primitive changes, and this one did: it gave up before the promise it
          was waiting on could settle, and returned `false` - so a test asked "did the audio come up?"
          and the helper answered "no" on a context that was about to say yes.

          So both bounds are now milliseconds, which is what they were always trying to express.

          Correction, 2026-10-06: the bound never ran. When `requestAnimationFrame(tick)` became
          `yieldToBrowser()` (2f516b2), the `.then(tick)` was dropped, so `tick` was never called and
          both `whenAudible` and `whenIdle` waited for ever on any context not already running. The
          "suspended context can be brought back" test hung to its 240s watchdog on every run. Fixed
          by `yieldToBrowser().then(tick)`; suite 238/239 -> 239/239; whole gate (3 viewports) ~15 min -> 172s.

## [h:45-sound-42]
near: `const target=liveCtx;`

IT SUSPENDS THE LIVE CONTEXT, AND THAT WORD IS LOAD-BEARING.

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
        reported 240/240.

## [h:45-sound-43]
near: `const WEAPON_TONE_CENTS={bolt:-260,scatter:400,arcane_beam:140,voidball:-90};`

KEYED BY THE ID FORM, NOT THE DISPLAY NAME. The four weapons are named 'Bolt', 'Scatter',
  'Arcane Beam' and 'Voidball', and `Content.idOf` turns those into 'bolt', 'scatter', 'arcane_beam'
  and 'voidball' - which is the form the rest of the game keys on (TRAIT_TABLE uses it too). A first
  version keyed off `w.name` and matched three of four, silently, because 'Beam' is not a name any
  weapon has. Two of these lookups have now failed in this file on a key that does not exist, which
  is the argument for deriving the key rather than typing it.

## [h:45-sound-44]
near: `const w=(typeof WEAPONS!=='undefined'&&player.weaponIdx!==undefined)?WEAPONS[player.weaponIdx]:null;`

THE LOOKUP KEY IS `w.name`, NOT `w.id`. There is no `id` on a weapon: the entries in WEAPONS
    (00-balance.js:741) carry `name` - 'Bolt', 'Scatter', 'Beam', 'Voidball' - and a first version
    keyed off `id` matched nothing and returned 0 for all four. That read as "pitch jitter is not
    working" and it was, under a key that does not exist, so every gun sounded identical.

    A WIDE SPREAD on purpose, and measured rather than guessed: a semitone is 100 cents and anything
    under about 40 reads as the same shot. These are -260, -90, +140 and +400 - about a quarter of a
    semitone, one, one and a half and four - a clear identity per weapon and still inside a range
    where the shot does not sound like the wrong instrument.
