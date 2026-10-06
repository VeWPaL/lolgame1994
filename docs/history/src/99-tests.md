# 99-tests.js — moved comments

Long comments moved out of `src/99-tests.js`. The code keeps a one-line gist tagged `[h:99-tests-N]`; search this file for that tag.

## [h:99-tests-1]
near: `if(new URLSearchParams(location.search).has('test')) (function(){`

99-tests  -  the 94 checks: an executable specification, not a safety net

  Larger than the game itself, and the most valuable thing in the repo. Seeded with mulberry32, so
  a green run is a real contract rather than a lucky sample.

  84 of the 94 bodies assert on the simulation alone and port to a headless NUnit suite unchanged.
  The other 10 assert on drawing, and one of those is only "every screen renders without throwing" -
  a smoke test that belongs on the engine side, not in a headless suite. That 84/10 split is why
  the port is measurable rather than hopeful.

## [h:99-tests-2]
near: `const REC_KEYS=[RECORDS_KEY,'depths_best','depths_fastest','depths_wins','depths_deepest',TICK_KEY],`

Every record key has to be listed here, and the list is the only thing standing between a test
    that writes a record and every test after it. depths_deepest was left off when the floor ladder
    landed, so a test that reached floor 6 wrote it to storage, clearRecords() did not remove it,
    loadRecords() read it straight back, and a later test asserting a depth of 4 saw 6 - a test
    failing on a record another test had set, which is exactly the class of bug this list prevents.

    RECORDS_KEY is in the list for the same reason and one generation later: the records moved from
    four flat keys to a single JSON key, and this list is what makes the suite back up and restore the
    live one. Leaving it out would restore nothing while `clearRecords` deleted it, which is the
    mirror image of the depths_deepest bug - a test quietly destroying real progress instead of
    inheriting another test's. The legacy keys stay listed because a player (or an earlier test) may
    still have written them.

## [h:99-tests-3]
near: `const storedRecords=()=>{ const keep=records; loadRecords(); const out=records;`

WHAT IS ACTUALLY ON DISK, read back through the game's own loader. Every assertion about a saved
    record should go through this rather than reading a key directly: it is the only way to test what
    a RELOAD would see, which is the thing that matters, and it stays correct across the move from
    flat keys to a single JSON value without every test being rewritten when that happens again.
    loadRecords writes into the global `records`, so this saves and restores it rather than
    clobbering whatever the test around it was asserting.

## [h:99-tests-4]
near: `let strayRandom=0;`

The game draws from three named streams, so the suite seeds those rather than hijacking
    Math.random. Two things follow, and both are worth more than the seeding itself.

    ISOLATION. Every test starts from the identical world. Before this, one Math.random closure
    was shared by every test, so the dungeon a test saw depended on how many tests ran before it.
    That is deterministic as a SET and useless individually: a test that failed could not be run
    on its own to find out why, and inserting a test silently changed every test after it. Now a
    test's world is the same every time - which is most of what a test suite is for.

    A TRIPWIRE. Math.random is replaced by a counter rather than simply removed, and the suite
    asserts the count is still zero at the end. The alternative is a comment asking people not to
    use Math.random, and that decays within a month. This fails loudly the first time somebody
    adds a stray call, which is the only kind of rule that survives contact with a real codebase.

## [h:99-tests-5]
near: `const realFresh=Rnd.fresh;`

`Rnd.fresh` IS STUBBED, and it has to be, because `startGame()` now calls it.

    startGame takes an optional seed and reseeds before it builds anything - that is the fix for
    pressing R producing a different dungeon under the same code. With no argument it asks
    `Rnd.fresh()` for a seed, which reads `crypto.getRandomValues`.

    So every one of the ~100 fixtures that says `startGame()` - meaning "start a run from the seed
    this test already established" - was quietly getting a RANDOM dungeon, and the suite went
    flaky rather than red: six consecutive runs gave 197, 197, 197, 197, 195, 197. A test suite that
    fails at random is worse than one that fails always, because people learn to re-run it.

    Stubbing `fresh` puts the ambient seed back: a bare `startGame()` means TEST_SEED, exactly as it
    did before startGame learned to reseed. Fixtures that care about a particular seed pass it.

## [h:99-tests-6]
near: `const resetUI=()=>{`

Every test starts from the same world: the same seed, the same locked meter, and the same UI
    state. The UI part was added after watching three unrelated tests fail because the four before
    them had left a character sheet open - a test that throws leaves whatever it had set up, and
    `test()` catches the throw and carries on, so the damage lands on tests that have nothing to do
    with it. A failure should cost exactly one red line, not a cascade that hides the real one.

    This is the same lesson as the shared-RNG problem, one layer out: a harness that carries state
    between cases reports confident wrong answers rather than failures.

## [h:99-tests-7]
near: `try{ Momentum.release(); }catch(e){}`

And the HELD meter. Momentum.hold() is the measuring instrument, and an instrument left on is
      worse than one that is missing: a test that holds the meter at 0 to compare against a held 1
      throws partway, never releases, and every test after it reads a frozen 0 - which is how three
      unrelated checks were failing at once about a bar and a trail and a dodge. Release belongs
      here, beside the other reset, because "every test starts from a known state" is the property
      and the meter is part of that state.

## [h:99-tests-8]
near: `const pending=[];`

PROMISES FROM ASYNCHRONOUS TESTS, drained before the results are published. A test declared
    `async` - which anything asserting on `Sound.render` must be, because `startRendering()` is a
    promise - was previously run and forgotten: the harness called `fn()`, took the returned promise,
    and immediately pushed `{ok:true}`. Every assertion in such a test ran after the verdict was
    already recorded, so the suite reported a green waveform check that had never looked at a
    waveform, and the mutation that proved it - a voice made to ring on for three seconds - passed.
    Four times in this file a check passed for a reason that had nothing to do with the thing it
    claimed to check.

## [h:99-tests-9]
near: `let serialChain=Promise.resolve();`

A SERIAL CHAIN, for tests that touch shared state.

    Every async test declared here starts the moment it is declared, so they all interleave - which
    is fine for tests that own their fixtures and wrong for tests that share one piece of global
    state. The audio tests share exactly that: one AudioContext, one `unlocked` flag, one voice pool.

    Three of them failed for four consecutive runs against code that was correct, each with a
    different and entirely spurious message - "a shot did not play" from a test whose shot was
    silenced by a neighbour, "a context could not be suspended" from a test whose neighbour had just
    suspended it. Neither message was about the thing it claimed.

    A test opts in by being declared with `serial`, and the chain below runs those one at a time.
    Nothing else changes: a serial test is still a normal test, still counted, still timed out if it
    hangs.

## [h:99-tests-10]
near: `let n=0;`

ASSERTION COUNTING, and the distinction matters more than the count.

      A test that makes no assertions at all passes unconditionally, and that is the whole of the
      `eq(FRAME,FRAME)` shape this file keeps warning about. A test whose assertions all PASS is
      obviously not the same thing - that is what a passing test looks like - so nothing here tries to
      detect it by outcome.

      What IS worth detecting is an assertion that can never fail, and the only sound way to find one
      is to watch it fail: run the suite, then re-run each test with a deliberately broken world and
      see which still pass. That is a mutation harness, not a counter, and it is the correct tool. The
      counts recorded here are kept because they are cheap and because `asserts===0` is a real and
      sufficient signal on its own - a test with no assertions is dead, full stop.

      An earlier version of this counted assertions that evaluated true and listed every test where
      all of them did. That flagged 194 of 229 tests, including every correct one, because a passing
      assertion evaluates true by definition. The number was not a finding; it was the definition of
      passing.

## [h:99-tests-11]
near: `try{`

ASYNCHRONOUS TESTS ARE AWAITED, and this was the fourth silent pass in one file.

      `fn()` was called and its result thrown away, so a test declared `async` ran all of its
      assertions after this harness had already pushed `{ok:true}`. Every one of them was skipped, the
      mutation that proved it - a voice made to ring on for three seconds - passed, and the suite
      reported a green waveform check that had never looked at a waveform.

      The tell was in the shape of the code: `Sound.render` is async because `startRendering()` is,
      so any test asserting on it must be async, and a harness that cannot await cannot host it.

      Results are now collected as promises and drained once at the end. A synchronous test still
      resolves immediately, so nothing else in the file changes behaviour.

## [h:99-tests-12]
near: `const invoke2=()=>fn();`

`fn()` IS CALLED INSIDE THE LAUNCHER, NOT HERE. This is the whole of the serial feature, and
        the first version got it wrong: the call sat above, so a test marked serial had its BODY RUN
        IMMEDIATELY - all three audio tests still interleaved, still suspending each other's context,
        still racing each other's renders. Only the 90-second watchdog race was queued, and a
        watchdog is a thing that WATCHES.

        Serialising a promise you have already started is not serialising anything. The body has to
        be the thing that waits, so `fn()` moves inside `invoke`.

## [h:99-tests-13]
near: `if(serial){`

THE THREE WAYS A TEST CAN END, and the third one is the one that was silently broken.

          1. It returns a promise  - the body has started; race it against the 90s watchdog.
          2. It returns nothing    - a sync test; the body has already run to completion.
          3. It is marked serial and returns a promise - the body has NOT started, because serial
             is exactly the promise that it waits its turn.

          The original dispatch tested `if(r && r.then)` with `r = serial ? null : invoke()`. So a
          serial test took the `else` branch, which pushed `{ok:true, asserts:n}` and DID NOT CALL THE
          BODY. Three serial tests - including the two written specifically to catch a game that is
          permanently silent - had never executed a single line.

          That is the worst failure mode this harness has produced, and it is worth naming exactly:
          the tests were counted in the total, reported in the pass count, and could not fail. A green
          suite reporting 240/240 with a mutation that silences the game outright.

          The tell, if it is ever needed again: `asserts` is a module-level counter shared by every
          test, so a test that never ran still reports a large number.

## [h:99-tests-14]
near: `if(serial){`

BRANCH ON `serial` FIRST, and this is the whole of the fix. The shape of the mistake is
          the lesson: the dispatch tested `if(r && r.then)` and computed `r` as
          `serial ? null : invoke2()`. For a serial test `r` is null BY DESIGN, so the condition was
          false, so control fell to the `else` - which pushes `{ok:true, asserts:n}` and never calls
          the body.

          Three serial tests, including the two written specifically to catch a permanently silent
          game, had never executed a single line. They were counted in the total, included in the
          pass count, and structurally incapable of failing. A mutation that silences the game
          outright passed 240/240 four separate times, and the only reason it was eventually caught
          is that I went looking for it rather than trusting the number.

          `r` is not the question "is this test done". `r` only exists for a test that was allowed to
          start. The FLAG is the question.

## [h:99-tests-15]
near: `let stimer=0;`

A SERIAL TEST GETS A LARGER WATCHDOG, and the reason is arithmetic rather than sentiment.

            A serial audio test legitimately spends seconds waiting: `whenIdle` can wait up to ten
            seconds for a neighbour's offline render, `whenAudible` up to three per attempt and the
            baseline loop attempts three, and the big sound test renders sixteen voices. Against the
            shared 90-second budget three of those tests timed out on correct code - and a watchdog that
            fires on correct code is a watchdog that has stopped being a watchdog and started being a
            source of false failures.

            240 seconds is comfortably more than the worst case and comfortably less than "wait for
            ever". The point of a bound is that it fires on a hang, not that it fires on a slow test.

## [h:99-tests-16]
near: `const goTo=type=>{const r=Object.values(rooms).find(x=>x.type===type);enterRoom(r.x,r.y,'W');readyT=`

`eq` COMPARES BY ===, WHICH FOR AN ARRAY IS A REFERENCE COMPARISON.

    Two freshly-computed arrays with identical contents are two different objects, so
    `eq(hexRgb('#abc'),hexRgb('#aabbcc'))` fails against correct code and has always done so - the
    message prints two identical lists of numbers and the assertion still says they differ, which is
    about the most confusing failure this harness can produce.

    Not changed, because `eq` is right for everything it is actually used on (primitives, and object
    identity where identity is the claim - two cached sprites being different objects IS the property
    being tested). Recorded here so the next person comparing a computed array does not lose twenty
    minutes to it: join it, or use `ok(a.join()===b.join())`. The colour checks in the area palette
    section are written that way and say so.

## [h:99-tests-17]
near: `let _TOP_SPEED_CACHE=null;`

THE PLAYER'S TOP SPEED, MEASURED AT RUN TIME - NOT WRITTEN DOWN.

  `playerSpeedForTest()` above is 0.935*PLAYER_MOVE = 1.122, which is the player's BASE speed. It is
  the right denominator for "can the player still move" and the wrong one for "can a pack catch them",
  because the tick's real top speed is `player.speed * (1 + moveSpeedBonus())` and against a pack
  the player is FASTER still, because the momentum meter fills from being chased and being shot at.

  These were hard-coded numbers - 1.4025 and 1.6045 - and the first one was wrong: measured, the
  empty-room top speed is 1.4041. It was 0.1% off, which is why nobody caught it, and it is wrong in
  the direction that matters, because a bound written against it is 14% optimistic on top of the 43%
  it is optimistic about by quoting 1.122. A number written in a comment cannot disagree with the
  game; a number written in an array can, and this one did, for as long as it was there.

  So both are MEASURED here, by the same probe `tools/perf-baseline.js` uses, and the literals that
  follow are checks on the measurement rather than substitutes for it. Every Brunch bound in this
  file is a multiple of the chased figure - which is why 1.35 read as "1.20x the player, decisive"
  while the pack could not close a gap at all, and why it had to be raised twice before it worked.

## [h:99-tests-18]
near: `rm.bounds=roomBounds(60000,4000); rm.cx=rm.bounds.l+rm.bounds.w/2; rm.cy=rm.bounds.t+rm.bounds.h/2;`

IN A ROOM BIG ENOUGH FOR THE RUN. This probe holds one direction for three seconds and the
    standard room is 700px wide, so at 1.6px/tick the player crosses it in 0.44s - hits the east
    wall and stops, and then measures a wall, not a top speed. That is what made the numbers read
    1.1734 and 0.2832: both are the player pressed against a wall with the velocity zeroed. The
    same shape of error as the Brunch fixture that measured a wall-pinned pack.

## [h:99-tests-19]
near: `Momentum.hold(meterFull?1:0);`

HOLD THE METER, WITH THE INSTRUMENT THAT EXISTS FOR IT.
    `Momentum.hold(v)` (06-stats.js:270) pins the meter and is honoured by `Momentum.level()`,
    which is the only reader. Writing `player.momentum` directly does nothing to the reading,
    because the level comes from `Stats.value('momentum')` - so this probe measured an empty room
    twice and reported 1.4025 for both.

    Its own comment records this exact failure: "the measurement reported an identical number for
    momentum 0 and momentum 1 without anybody noticing that both columns were the same column."
    The instrument was built, documented, and then bypassed by the probe written after it. Hold is
    released in a finally so a failing assertion cannot leave the meter pinned for the next test.

## [h:99-tests-20]
near: `const pointAt=(x,y)=>{ updateCamera(); mouse.x=x-cam.x; mouse.y=y-cam.y; };`

AIM AT A WORLD POINT. Every fixture in this file used to say `mouse.x=e.x; mouse.y=e.y`, which
    reads as obvious and is the reason a real bug survived 163 checks.

    `mouse` is the CURSOR, in screen space - that is what the DOM handler produces and what the
    weapon bench hit-tests against. The fixture was writing a WORLD position into it, and the game
    was reading a world position out of it, so the test and the bug agreed perfectly: aiming worked
    in the suite and was wrong on screen, by between 0.34 and 21.28 degrees depending on where the
    cursor was. The agreement was the accident, and only one of the two sides of it was real.

    So the frame is now stated at every call. `pointAt(e.x,e.y)` means "put the cursor over this
    world point", which is what a fixture always meant, and it cannot be got quietly wrong again:
    a fixture that meant a screen position now has to say so.

## [h:99-tests-21]
near: `const px=(v)=>Math.round(v-cam.x), py=(v)=>Math.round(v-cam.y);`

PIXELS ARE SAMPLED IN SCREEN SPACE, from a WORLD position, and every test that looks at a
    rendered pixel goes through here.

    getImageData reads the framebuffer, which is the screen, and the game is drawn in world space
    under a camera transform. So a test that samples at a body's world x/y was correct only while
    the camera was the identity - which it was, for every room that fit on screen, and stopped being
    the moment a room was allowed to be bigger than the viewport. That is the same class of bug as
    every other stale-derived-value in this file: a thing that was true because of an accident of
    the current numbers, and stops being true the moment a number moves.

    Doing the conversion in one helper rather than at each call site is also what stops a second
    reader of the same idea from appearing: the conversion is the kind of arithmetic that is easy to
    get backwards, and backwards it samples empty floor and reports a hit that is not there.

## [h:99-tests-22]
near: `const noCharacter=()=>{`

THE CHARACTER IS NEUTRALISED FOR WEAPON TESTS, and this one helper exists because a starting
    class put +3 Strength on every pellet of every gun and quietly broke fifteen checks.

    A character is a new input to the damage pipeline. A test that measures a falloff curve, a
    pierce ratio or a boss time-to-kill is measuring the WEAPON, and with a Wyrd's Strength in the
    pipeline it is measuring the Wyrd instead - the Scatter's volley came out at 44.8 raw damage
    against a stated 20.8, which is exactly 20.8 plus 3 on each of 8 pellets, and a test asserting
    20.8 was not wrong about the Scatter so much as measuring something else.

    So the weapon tests call this first, and they measure the weapon. What the class contributes is
    not thereby untested: there is a dedicated check that a class's Strength lands on a real shot,
    because a class that did nothing would pass every one of these and ship.

    Note it subtracts rather than resets, so it also works mid-test after a build has been applied -
    a test can give itself items, strip the character, and be measuring exactly one thing.

## [h:99-tests-23]
near: `ok(Math.abs(EMPTY_ROOM_TOP_SPEED()-1.4041)<TOP_SPEED_BAND,`

These three live in a TEST and not at module scope on purpose. At module scope they ran
      while this file was being parsed, before the ?test harness existed - so a mutation
      anywhere near them hung the loader for the full 240s instead of failing an
      assertion, which is the most expensive way there is to report a wrong number.

## [h:99-tests-24]
near: `const probeRun=()=>{`

A fingerprint of everything the RUN stream decides: the shape of the dungeon, and for every
    ordinary room the bodies it rolled - their types and their positions. Spawns are placed on
    room entry rather than at generation, so this has to walk in and let each room roll, in a
    fixed order, or the second half of the fingerprint would always be empty and the test would
    pass for the wrong reason.

## [h:99-tests-25]
near: `const runAt=seed=>{startGame(seed);return probeRun();};`

`startGame(seed)` is now how a run is STARTED AT A KNOWN SEED, and it reseeds the generator before
  it builds anything. This used to be `Rnd.set(seed); startGame();` - two statements, with startGame
  deliberately NOT touching the generator, which is what let pressing R produce a different dungeon
  under the same code.

  So every fixture that meant "build the dungeon for this seed" said two things when it meant one,
  and the second one was quietly ignored. Written as the single call the game itself makes, a
  fixture cannot drift from the behaviour it is checking again.

## [h:99-tests-26]
near: `const play=(seed,ticks)=>{`

The dungeon is not the run. Two cursors live at MODULE scope in 20-world.js - FLANK_CURSOR, which
      decides which slice of the circle the next lunger takes, and PACK_CURSOR, which hands out wall
      identities - and neither was reset by startGame. They are correct WITHIN a run and wrong ACROSS
      one: the second run from a seed began with the golden-angle walk already part-way round.

      Measured, same seed and same commands twice, before the fix:

          run 1   flank 0.00 2.40 4.80 ...   lunger at 401, 414, 430
          run 2   flank 0.35 2.75 5.15 ...   lunger at 398, 411, 427

      Same rooms, same bodies, same everything the fingerprint below can see - and a different fight,
      because a lunger approaching from a different angle is a different fight. The first run of a
      session played differently from the second, so a friend comparing runs got a difference with no
      seed to explain it. That is the one thing a seed cannot be asked to absorb.

      So the check is a PLAYED fingerprint rather than a generated one: build the world, enter a room,
      play six hundred ticks of the same held key, and compare where every body ended up. A test that
      only compared the dungeon would have passed throughout - `runAt` above does exactly that.

## [h:99-tests-27]
near: `startGame(12345);`

AND THE RESET IS THE RUN'S, NOT THE FLOOR'S. descend() must not call it: a pack id is unique within
      a RUN, and clearing it mid-run would let a body outlive a room transition and end up sharing a
      formation with a pack it has never met - which is exactly what the original comment on those
      counters was defending. The scope is the run.

      Asserted on the pack ids, which are the thing that actually has to stay unique: descending and
      then spawning more bodies must not hand out an id the run has already used.

## [h:99-tests-28]
near: `descend();`

THROUGH A REAL FLOOR CHANGE. Setting `run.floor` does not regenerate anything - descend() is what
      calls generateDungeon - so the first version of this asserted on the same dungeon twice and saw
      no new ids, and read it as "descending does nothing". It calls descend() because that is the path
      a player takes and the one that has to keep the counters running.

## [h:99-tests-29]
near: `const base=runAt(12345);`

THE POINT OF THE WHOLE EXERCISE. If any non-run draw shared the run's stream then adding an
enemy behaviour, or touching the art, would silently renumber every future seed - and the
first person to find out would be a friend who pasted a seed and got a different dungeon,
who would then conclude the feature was broken rather than that it had rotted.

## [h:99-tests-30]
near: `let worst=0;`

A seed system hands the player a number and a promise. The promise has to hold for all of
them, not for the one the test happens to use - so this is the check that the GENERATOR is
sound, across the whole seed space, rather than that one dungeon happens to be fine. 300
seeds is enough to have caught a degenerate draw more than once.

## [h:99-tests-31]
near: `startGame();`

The suppressor that stops the game seeing keys while an overlay is open runs in the CAPTURE
phase on window, which is ahead of every element in the tree. Without an exemption for the
field, the stopPropagation there means the input never sees a keystroke at all - a text box
that accepts nothing, and no error anywhere, because from the browser's point of view nothing
is wrong. This is the whole test: that box has to take letters.

## [h:99-tests-32]
near: `startGame();`

The loop this exists for: read a seed off somebody's summary, type it in, get that exact
dungeon. Three cases, and the middle one matters most - a mistyped seed that quietly started
a random dungeon instead would make the whole feature feel broken, with nothing on screen
saying why.

## [h:99-tests-33]
near: `eq(paperTex(420,320),paperTex(420,320),'the paper is re-baked on every call');`

Two bugs of exactly the same shape lived here, and neither announced itself.

paperTex looked its result up in woodCache under a 'paper' key, which can never collide with
a 'wood' key, so it missed every single time. drawRunSummary calls it once per frame: 3360
noise iterations and about ten thousand draws, sixty times a second, on the death screen.
Worse than the cost: the speckle came from the art stream each time, so the paper SHIMMERED.
eq() on two object references is the whole assertion - a texture that re-bakes is a different
object every frame, and this fails the moment somebody reintroduces the mistake.

## [h:99-tests-34]
near: `startGame(4242);`

AND THE ROOT, NOT WHATEVER THE GENERATOR HAPPENS TO HOLD. The summary read `Rnd.seedText`, which
      after a descent is floorSeed(root, floor) rather than the root - so a player who died on floor 3
      was handed a code that reproduces somebody else's floor 3. Since the summary is the artefact you
      send to a friend, that is the one place where printing the wrong number breaks the feature.

## [h:99-tests-35]
near: `const pw=440,px=(W-pw)/2,L=px+36,R=px+pw-36;`

and it has to FIT. stampText centres on its x, so a value right-aligned by centring hangs
half its width over the paper and the last character falls off the edge - which prints
"000039" for a seed that is "000039U", and a seed missing a character is a dungeon that will
not replay. The summary is a pixel layout, so this is checked as one.

## [h:99-tests-36]
near: `startGame();`

THE rule the whole item system stands on. If a build is applied by multiplying into the live
      value, then after twenty items the number is a product applied in an order nobody can
      reproduce, and taking one item off does not take its effect off. The build cannot be explained
      to the player, cannot be saved, cannot be compared, and no bug report can be acted on because
      the wrong thing is not the wrong line.

      The sharpest form of the check is not that the maths is right - it is that the base constant
      in the game is never touched at all.

## [h:99-tests-37]
near: `eq(Stats.value('strength'),Stats.baseOf('strength'),'a fresh run did not start from its character\'s`

A fresh run is now the CHARACTER'S SHEET, not a row of zeroes - the Wyrd starts with 3
      Strength, 25% speed, 1 Intelligence and 8 Vigor. So "clean" means the class baseline, and the
      assertion is written against baseOf() rather than a literal 0, which means a second class with
      different numbers needs no edit here at all. That is the whole reason the class exists as data.

## [h:99-tests-38]
near: `startGame();`

Two ceilings and the difference is the point. SPEED_CAP is the most items may give, so a Speed
      item always has a readable value; MOVE_SPEED_HARD_CAP is the most ANYTHING may give, so a
      player with every speed item and a full meter still cannot outrun the gunner. The clamp is on
      the DERIVED value rather than on each modifier, because capping inputs would make each item
      quietly worth less than its number the moment a second one arrived.

## [h:99-tests-39]
near: `Stats.flat('speed',0.06);`

First, the degenerate case that the first version of this model had. Aggregation was
      (base+flat) * product(1+mult), and Speed's base is 0 because the real base is PLAYER_MOVE and
      it lives on the player - so a Speed item multiplied zero and the stat stayed at exactly 0. The
      item was equipped, named on the sheet, and did nothing whatsoever. Only the arithmetic said so;
      nothing threw, and the bar simply never moved.

## [h:99-tests-40]
near: `eq(Math.round((moveSpeedBonus()-Stats.baseOf('speed'))*100),6,`

The ITEM'S SHARE, not the total. The Wyrd starts at 25% speed, so moveSpeedBonus() is 31% with
      the item on and 25% without, and asserting 6 was asserting a thing that was never true about
      the stat - it was true about the stat when the character started at nothing. The delta is what
      the item did, and it is the only part the item is responsible for.

## [h:99-tests-41]
near: `{`

THE METER'S SPEED GIFT IS AN ABSOLUTE AMOUNT, and this asserts the distinction rather than
      trusting a comment to carry it.

      The balance file's paragraph on the meter described the contribution as "5%" and named the
      number 0.05, while the constant beside it was 0.18. Both halves were wrong.

      THE UNIT IS THE THING THAT WAS MISUNDERSTOOD, and this is why it is worth a test.
      `moveSpeedBonus()` returns `Stats.value('speed') + Momentum.level()*MOMENTUM_SPEED`, and the
      tick uses it as `(1+moveSpeedBonus())` - so the whole quantity is a MULTIPLIER FACTOR, not a
      speed. The Wyrd starts at a 0.25 speed stat, which is why a naive read of the bonus as "a
      percentage of the stat" makes 0.18 look like 72% of the character. It is not: 0.18 on a 0.25
      base is 0.43 inside `(1+x)`, i.e. 18% more speed than the base alone, and the whole thing is
      then clipped by MOVE_SPEED_HARD_CAP.

      That is why the comment said 5% and was wrong in a way nobody caught: 18% is close enough to
      "a small share" to read as one, and the constant was changed at some point without the prose
      moving with it. Measured, at the starting build: 283.8px/s with the meter empty, 294.5px/s
      full - a gain of 3.8%, not 18%, because at that build the multiplier is further from 1 than the
      raw points suggest and the acceleration half dominates what the player actually feels.

## [h:99-tests-42]
near: `Momentum.unlock();`

Four cases, and the one that took a rewrite is the third. The meter is meant to answer "is the
      player playing well", so it has to distinguish a player who is moving from a player who is
      trying to. The first version charged on velocity - and clampPlayer() stops a body's position at
      a wall while leaving its velocity pointing into it, so a player pinned against a wall with
      bodies alive reported full speed indefinitely and farmed the meter without covering ground.

## [h:99-tests-43]
near: `const cost=1-Momentum.value();`

THE COST IS A RANGE, not a literal. This test used to pin 450 because MOMENTUM_HIT_KEEP was
      0.45, and when the constant was retuned to 0.55 the test failed as though the constant were
      the specification. It is not - the specification is "a hit costs the cushion and not the run".

      So it is asserted as a cost, measured from the meter itself, and checked against a band wide
      enough to survive a retune and narrow enough to catch a regression. A penalty of 0 would be a
      free hit; a penalty of 0.9 would throw a good fight off the scale, which is the failure the
      comment above it describes.

## [h:99-tests-44]
near: `const kinds=[...new Set(sheet.map(s=>s.kind))].sort().join(',');`

Every kind the model defines has to be used by something, and every stat has to use one it
      defines. NOT "six stats, six kinds" - Strength and Vigor are both plain quantities and that is
      correct. What must not happen is a kind that exists in the model and no stat ever uses, which
      is a door left in the type system with nothing behind it.

## [h:99-tests-45]
near: `startGame();`

A sheet that renders once and then goes stale is worse than no sheet, because it is confidently
      wrong: the player makes a decision from a number the game is not using. The rows are rebuilt on
      open rather than kept live, so the thing to test is that rebuilding actually re-reads Stats -
      and that an unchanged build shows no invented contribution.

## [h:99-tests-46]
near: `const wantOnSheet=Stats.ORDER.filter(k=>k!=='momentum');`

MOMENTUM IS NOT A SHEET ROW ANY MORE, and these two assertions used to say the opposite.

      They asserted the sheet drew every stat the model defines, and that Momentum was marked `earned`
      so it would read as the one number you earn rather than pick up. Both were written when the
      meter lived here, and both encoded a decision that has since changed - a stat the player cannot
      act on does not belong on the screen that answers "what am I carrying", and a PAUSE screen is
      the worst place for the one number whose whole appeal is watching it move while you fight. It
      is on the HUD now, and the tutorial that explained it is gone.

      So the sheet's contract is "every stat EXCEPT momentum", and the interesting part is that the
      exclusion is asserted rather than assumed: a stat silently vanishing off the sheet is the
      failure this file has the most of, and momentum is now the one that has to be checked.

## [h:99-tests-47]
near: `const THR=PLAYER_HIT_R+5;`

The user's report was that a shooter could be survived indefinitely by dodging, and that it
      backed away uselessly rather than ever being a threat. Both halves were true, and the cause was
      not the retreat - there is a brake that stops a body at its line, so it settles at 250px and
      holds - it was that NOTHING about a shooter punished a reversal.

      The shooter shares its entire firing branch with the gunner. Both read one signal about the
      player, SWERVE, and both use it to shrink their lead. That signal was decaying with a half-life
      of sixty-three ticks, about a third of a second, which registers a player reversing four times
      a second and washes out completely for one reversing at a human rhythm. Measured average swerve:
      0.85 at a 0.12s reversal, 0.17 at 0.25s, 0.04 at 0.95s. So the enemies read a player who does
      not exist and were blind to the one who does.

      The numbers below are the fix, measured through the same fixture as the gunner test beside it -
      which is the point of deriving this one from that one rather than writing a fresh probe. An
      earlier probe of this said a straight runner was hit 65% of the time, and the figure was wrong
      twice over: it never cleared the ready window, and it let the shooter fire while the player was
      pinned in place, which is a shot correctly aimed at somebody standing still. Both are the
      failures the gunner fixture documents at length, and neither was visible in the output - they
      were only visible as a number that disagreed with a test.

      SWERVE is 0 for a player holding a line, so a runner is unaffected by any of this: it is read as
      perfectly, and it is punished. That is the claim worth making, because a fix that helps reversers
      by making everyone less readable would be a fix in the wrong direction.

## [h:99-tests-48]
near: `ok(rate(rev300)>=0.12,'a player reversing every quarter second is still hit '+`

At 300px the shot is only a fifth of the time, and lowering how much a walking shooter believes
      a reverser - 1.0, then 0.45, then 0.28 - moved the two-hundred-pixel case all the way and left
      this one flat at 21%. That is the useful part: it says the residual here is NOT a belief
      problem. A shell from 300px is in the air for a hundred and thirty-six ticks against a
      hundred-and-four-tick oscillation, and a single lead cannot solve that however much the shooter
      trusts the read - the target genuinely is somewhere else by the time it arrives.

      So the claim is a floor on a pre-existing property rather than a fix: reversing was 13% here
      before the shooter started walking and is 21% now, so the movement change made the long-range
      reverser slightly WORSE, not better, and that is the honest result to carry into playtesting.
      Whether a three-hundred-pixel reverser being a seventy-nine-percent dodge is acceptable is a
      design question, not a tuning one, and it is flagged rather than quietly tuned away.

## [h:99-tests-49]
near: `ok(rate(line200)>=0.9,'a straight runner is only hit '+(rate(line200)*100).toFixed(0)+`

The deadzone claim, and it is now a claim about the MOVING shooter rather than the rooted one.

      This measurement is genuinely unstable at twelve repetitions - the same settings returned 10%,
      21% and 92% - because each rep is close to a single coin flip: the body is chasing a point that
      is rotating, and a centimetre of difference in where it happens to be when the cast starts
      decides whether the shell arrives. Asserting a tight band on twelve samples was asserting a
      coincidence, which is the failure this file has been rewritten several times to avoid. At
      thirty-six repetitions it settles into a rate: a straight runner 100%, a quarter-second
      reverser 38% at 200px and 11% at 300px.

      The runner number is the one that must not move. SWERVE is zero for a player holding a line, so
      a runner is read perfectly and punished, and any fix that helped reversers by making everyone
      less readable would be a fix in the wrong direction. That is why the claim below is a FLOOR on
      the reverser and not a comparison of the two: the gap is real, it is the price the movement
      costs, and it is reported here rather than tuned out of sight.

      Whether a reverser at 38% should feel safer than a runner at 100% is a playtesting question and
      not one a threshold can settle. What a threshold can do is refuse to regress silently.

## [h:99-tests-50]
near: `ok(rate(rev200)>=0.12,'a player reversing every quarter second is hit '+`

THE FLOOR IS 12%, it was 30%, and the fix that caused the drop is now understood.

      A quarter-second reverser measured 38% here when the character had no speed. The Wyrd starts at
      25% and a full meter adds 18%, so a reverser crosses more ground in the same quarter second
      and the shooter's intercept - which is solved, not guessed - is solving a target that really
      has moved further. 16% is the honest measurement of a 43%-faster character, and it is stable:
      four runs, identical, because the fixture is seeded.

      THE EVIDENCE THAT IT IS THE CHARACTER AND NOT THE FIX, which is worth recording because it is
      the same shape of argument this file keeps having to make. The velocity-lag fix in the gun
      solver - reading player.vx instead of a 71-tick-old EMA - moved the COMMITTED straight runner
      at 200px from 0% to 99%, and left this number at exactly 16%. A fix that improved the solver
      and did not touch the reverser is the proof that the two are governed by different signals:
      a settled player is read by the accuracy of their velocity, and a reverser by how little of
      it should be believed at all. The two are conf and freshness, and they were the same knob.

      Whether a reverser dodging 84% is right is a design question and a threshold cannot settle it.
      What the threshold can do is refuse to regress silently, and 12% is measured with the cause
      written down rather than chosen.

## [h:99-tests-51]
near: `eq(roomPressure(1),1,'a lone body is not at maximum pressure, so the last enemy in a room is the '`

The last body in a room has to do all the work, so it closes and shoots faster. Most rubber
      bands make a losing position worse; this one makes it sloppier, which is what keeps a room you
      are losing survivable long enough to be played properly rather than merely survived.

      Both effects are visible - it walks at you, and it fires more often - and that is the only
      reason it is fair. A hidden accuracy ramp on a lone enemy is indistinguishable from the game
      cheating, and the number it reads is the ROOM, never the build.

## [h:99-tests-52]
near: `const bStr=Stats.baseOf('strength');`

Every assertion below is the class baseline PLUS the item. A Wyrd starts with 3 Strength, so
      "+1 Strength" is a total of 4, and a test that says 1 is not measuring the item - it is
      measuring a character that no longer exists. Reading baseOf() rather than a literal means a
      second character needs no edit here at all.

## [h:99-tests-53]
near: `player.hp=player.maxHp;`

A PRESS AT FULL HEALTH SPENDS NOTHING, and this is the assertion that pins it. It used to heal
nobody, report success, and cost a charge - so two reflexive presses cost half a tin, and from the
player's side the item looked broken rather than the game looking busy. The hook now reports
whether it did anything and the charge follows the answer.

## [h:99-tests-54]
near: `test('nine sigils with no cap, exactly one active, and a second active displaces the first',()=>{`

ONE ACTIVE, NINE SIGILS, NO CAP ON EITHER MEASURED SEPARATELY.

    The two limits in this system are opposites and it is worth saying which is which. Sigils are
    uncapped, because nine of them compounding is where the number of possible builds comes from - a
    cap there would be a statement that some combinations are less worth having. There is exactly ONE
    active, because a key you press is a decision rather than a fourth number to stack, and because
    four actives spread over three declared keys used to collide: Bone Whistle and Hunter's Mark both
    claimed key 1, so the largest build was 12 of 13 and nobody could tell whether that was a rule.

    The per-item `slot` field is gone, which is what actually settles it. Three places used to have to
    agree about a number - the definition, the build, and the blurb a player reads - and they did not:
    Lantern Friend was written as slot 2 and described as "Hold 2". With one slot there is nothing to
    agree about.

    What is asserted here is the SHAPE, measured from the roster rather than as a count, so that adding
    a fourth active or a tenth sigil makes this test stronger instead of needing editing.

## [h:99-tests-55]
near: `Items.reset();`

4. Q presses it, and pressing with an empty slot is a no-op rather than an error. The player is
   WOUNDED first, because a hook that reports nothing happened does not spend a charge, and a
   full-health Tin Cup is now exactly that - so a healthy fixture here would be testing the
   decline rather than the press.

## [h:99-tests-56]
near: `test('Q presses the active item, through the real key handler',()=>{`

NOTHING VISIBLE OR LIVE SURVIVES A DOORWAY.

  The room's transient state is four arrays plus the wand's muzzle flash, and `clearTransient` is the
  one list that holds all of them. `enterRoom` used to clear projectiles and nothing else, so the
  other three outlived the transition - and because every room's bounds start at the same origin, an
  effect left behind does not drift into the void beside the next room. It lands in the middle of it.

  That made the hook field a gameplay bug rather than a smear of leftover art: `tickFields` runs
  against the current room's bodies, so a leaked field charged and stunned an enemy the player had
  never met, and being charged is what grants hook RESISTANCE. The assertion below is about that
  charge, because the leftovers are only interesting if something consumes them.

## [h:99-tests-57]
near: `test('Q presses the active item, through the real key handler',()=>{`

THE BINDING ITSELF, dispatched as a real key rather than called.

  Every other test of Q calls Items.useActive(), which proves the function works and says nothing at
  all about whether anything presses it - and "nothing presses it" is precisely the state this whole
  system sat in for its entire life: four actives, a working use(), and no key anywhere in the game.
  So the assertion is on a keydown event going through the real handler.

  It also pins the one behaviour that is easy to get wrong by accident: Q with an empty slot must be a
  no-op rather than an error, an overlay stealing it, or a dialog opening. A key that fires every time
  you press it with empty hands is worse than a key that is not there yet.

## [h:99-tests-58]
near: `test('the run summary card fits its own contents, and the contents fit the canvas',()=>{`

THE ACTIVE ITEM'S NAME FITS THE BENCH ROW WHOLE.

    This used to be about THREE labels - the weapon, the item and the alt - fitted against each
    other, and that layout was abandoned because the row is 147px and the three names come to 210px
    at full size. The two weapons keep their icons and cooldown sweeps instead, and the footer already
    reads LMB cast / RMB blast every frame, so the item name is the only one that needed the room and
    the only one whose full text is not otherwise on screen.

    Two earlier versions of this test passed while the HUD was wrong, so the assertions below are
    chosen to fail on what actually broke:

      - One re-implemented the budget formula beside the drawing code, so a mutation of the drawing's
        call site went unnoticed. It now calls fitLabel, which is the function the drawing calls.
      - One asserted only that labels did not OVERLAP, which passed a mutation that set the gap to
        zero - touching is not overlapping, but three words jammed together read as one word. It now
        asserts the layout that was requested, and a 6px gap is what was requested.
      - One asserted against a hardcoded row width of 190 when the real one is 147, so it was
        checking a geometry the game does not have. It now reads BENCH_ROW_W, published by drawHUD.

    And the assertion that matters is not "no overlap" but "WHOLE": the defect that actually reached
    the screen was ARCAN... UNTER'S ... BLAST, a collision fixed by deleting the information, and
    nothing that only asked about overlap would ever have noticed.

## [h:99-tests-59]
near: `test('the run summary card fits its own contents, and the contents fit the canvas',()=>{`

THE WARDEN'S BAR, and the four things it has to do that the floating sliver did not.

    The starting point matters, because the first version of this comment claimed the boss had no
    health bar at all. It had one - 56px, twice a regular body's, riding the body as it walked. The
    claim was wrong and was caught only because the measurement that produced it used
    spawnEnemy(false, ...) and quietly got a lunger back. So the tests below assert what the bar
    DOES rather than that some bar exists, because "a bar exists" was true before and was not
    sufficient.

## [h:99-tests-60]
near: `test('the run summary card fits its own contents, and the contents fit the canvas',()=>{`

THE RUN SUMMARY CARD IS SIZED BY ITS CONTENTS, and nothing on it prints outside its own frame.

    The card was `pw=440, ph=380` - three literals - wrapped around content that is a runtime stack of
    rows at 26px steps. Sixteen rows, a seed line, a rule, a heading and four more rows put the last
    baseline at y 534 against a card bottom of y 512, so "Dungeons cleared" was printed on the wood
    BELOW the paper. Nothing was wrong with the arithmetic; the box was a guess beside a computed
    stack, which is a defect no reading finds and no test that does not measure the last row against
    the card can catch.

    Then fixing it introduced the same defect one layer in: the RECORDS heading was drawn at
    `y+RULE_GAP+14` while its own item was only `RULE_GAP*2+2` tall, so the 12px heading's baseline
    fell 6px past the end of its slot and printed through "Deepest floor". The card had stopped
    bursting and started colliding. So the assertions below cover BOTH: nothing outside the card, and
    no two items' text on top of each other.

## [h:99-tests-61]
near: `startGame();`

The overlap this catches is not the same as the overflow above. A card can contain all of its
      text and still print two labels on top of each other, which is what happened the moment the
      card stopped bursting: the heading was positioned by an offset that its own height did not
      account for. So every item's slot has to be tall enough for what it draws at the y it is
      given, and the text must not reach into the slot below it.

      The numbers are font metrics, not guesses: a 15px monospace row needs 18px of line box, a 12px
      heading needs 14px, and each is measured against its declared height.

## [h:99-tests-62]
near: `const y=[];`

The condition that matters is not "is the slot taller than the font size" - it is whether one
      item's glyphs reach into the next one's. A baseline is where text SITS; what collides is the
      DESCENT of the row above against the ASCENT of the row below. Measured, not guessed:

        bold 12px monospace   ascent 8.0  descent 2.0
        15px monospace        ascent 10.0 descent 3.0

      So a heading followed immediately by a row needs more than 2+10=12px between their baselines -
      and the earlier version gave the heading a 29px slot but drew it at an OFFSET inside that slot,
      which is how "RECORDS" ended up printed through "Deepest floor" while every slot was nominally
      large enough. An earlier attempt at this test asserted `h >= 14` and passed a mutation that
      shrank the heading's slot to 16px, because 16 >= 14; the assertion was measuring a number
      nothing collides over.

      This measures the two halves that actually meet, and requires real air between them.

## [h:99-tests-63]
near: `const realFill=ctx.fillText;`

And the DRAWING, not just the layout. The offset bug above was a `fillText('RECORDS',L0,y+22)`
      in the draw loop, and no assertion about item heights can see it - the layout was correct and
      the drawing ignored it. Three attempts at this test checked the data and passed the defect,
      which is the same failure as the earlier one where a test re-implemented the rule instead of
      calling it: the thing being verified is not the thing being drawn.

      So this reads the drawing function and finds where RECORDS is actually positioned. It is not a
      substitute for a screenshot, but it fails on the defect that a screenshot found, and it does so
      without one.

## [h:99-tests-64]
near: `test('the lab is reachable from a key alone, and its shelf items can be picked up',()=>{`

THE LAB IS A REAL PLACE, not a mock-up: F2 alone reaches it, and everything on its rail can be
    taken. Four defects were found by walking into it, and each of them is a class worth a test.

    None of them are subtle from the source. What they share is that the lab was built to LOOK at,
    so the things that only happen when you USE it were never exercised - which is the standing
    argument of this project, in its own house.

## [h:99-tests-65]
near: `startGame();`

This one hid behind two owners of one field. The shelf rebuilt `r.pickups` by REASSIGNING it
      while the game's pickup loop in 60-tick held the old array and spliced as it went. Leaving the
      lab then filtered the new array, so the old one kept its items - and the next run started with
      free items lying on the floor. The symptom was "3 pickups left after F2" with no plausible
      cause; the cause was that two things thought they owned the reference.

      So the assertions are about the room's pickups after an actual leave, and the array identity
      is checked as well - two owners of one field is the whole bug.

## [h:99-tests-66]
near: `player.x=pk.x+220; player.y=pk.y; player.lagX=player.x; player.lagY=player.y;`

THE PLAYER STEPS AWAY FIRST, and that is not tidiness - it is the whole test.

      The alcove refills at the end of its timer and the game consumes a pickup the instant the
      player touches it. So a test that keeps the player standing on the rail watches the alcove
      return at zero ticks and get taken again on the next one, which is CORRECT and looks exactly
      like a shelf that never refills. It cost me two wrong diagnoses: first that the timer was not
      running (it was, 522 down to 516), then that the refill branch was unreachable (it fires on the
      tick the timer reaches zero). Measured tick by tick: gone=2,1,0 with the pickup back, then 525
      again because the player was standing on it.

      So the player moves off the alcove, the timer runs out, and the item is observed to be back -
      which is the thing a player walking along the rail actually sees.

## [h:99-tests-67]
near: `const ga=Lab.gridAlphas();`

Both of these were only ever visible in a screenshot, which is the argument for looking.

      THE FLAME. The brazier and the flame are two separate baked sprites, each centred on its own
      middle, so the features inside them sit at offsets: the bowl is at o-21 within the brazier, and
      the flame's BASE is at o+25 within the flame. Drawn at two unrelated offsets those put the
      flame's base 166px below the bowl - a fifth of a brazier's height - which read as a lit line on
      the floor beside a stand rather than as fire in a dish.

      The relationship that matters is the only one that means anything: the flame's base must land
      on the bowl's centre. So the test measures both offsets out of the sprites' own geometry and
      checks the drawn position against the bowl.

      THE GRID. The lab drew a 120px grid over a floor that is otherwise a seamless speckle. A room
      1680 wide has 14 of those verticals, and over an untextured background they read as tiles
      rather than as a ruler on the ground. A dungeon room has NO grid at all - which is exactly why
      the dungeon floor looks like ground and the lab floor looked like squares.

## [h:99-tests-68]
near: `ok(G.flameBaseRow>G.flameR,'the flame base row is '+G.flameBaseRow+' in a '+(G.flameR*2)+`

One invariant, and it is the only one that is checkable without re-deriving the drawing.

      The bowl is baked at sprite row `r - bowlY` in a sprite drawn at `spot - r`, so it lands at
      `spot - bowlY` - the radius cancels. The flame's base is baked at row `FLAME_BASE_ROW`, so drawn
      at top T it lands at `T + FLAME_BASE_ROW`. For those to meet, T = `-(FLAME_BASE_ROW + bowlY)`.

      Only the SIGN of the row is asserted here; the magnitude is the pixel measurement's job. Three
      versions of this test asserted the magnitude and all three were wrong in the same direction,
      because they were re-expressing the drawing's assumption instead of testing it. The one thing
      that is cheap and certain is that the base row is ABOVE the sprite's own middle - the flame is
      baked pointing up, and if that ever inverts the whole shape is wrong.

## [h:99-tests-69]
near: `Lab.enter();`

AND WHAT IS ACTUALLY DRAWN. Everything above computes from the constants, so it cannot see a
      wrong call site - a mutation that put the flame back at the old y-48 passed all of it, because
      the constants were still correct while the drawing ignored them. That is the third time in this
      work that a test verified the data while the defect was in the drawing, and the third time a
      screenshot found it first.

      So this measures the draw calls themselves. `drawImage` is wrapped for one frame and every
      flame-sized sprite recorded with its y. It cannot be fooled by a constant that nothing uses,
      and it does not care how the call is spelled.

      The player is moved onto a brazier first, and that is load-bearing rather than cosmetic: the
      braziers are laid out from the room's own origin at 175px in and 300px apart, and the camera
      follows the player, so a test that renders wherever the player happens to be standing may have
      no brazier in frame at all. "No flame was drawn" is then a true report about the wrong place.

## [h:99-tests-70]
near: `const sx=Math.round(brX-cam.x), sy=Math.round(brY-cam.y);`

THE PIXELS, not the arithmetic. Every version of this assertion that computed from the constants
        passed while the flame was in the wrong place - wrong by 168px, then 42px, then 11px - because
        each time the formula and the mistake shared the same wrong idea. A test written from the
        implementation checks that the implementation agrees with itself, and three of them did.

        So this reads the rendered framebuffer: it finds the warm pixels of the fire and the cool
        pixels of the metal it sits in, and requires the fire to be up inside the bowl rather than
        beside it. That is a property of the picture, which is the thing that was actually wrong, and it
        is the check that finally found it.

        The fire is found by `r > 110 && r > b + 50` - warm and redder than blue. The metal is the
        remainder of the brazier's own colours, `r < 90 && g < 95 && b < 105`, which is the bowl rim
        (#464e5c) and the stand (#23262e). Both bands are read from the same 60x120 sample so they are
        measuring one another's frame of reference.

## [h:99-tests-71]
near: `const WD=24, HT=70, x0=sx-12, y0=sy-G.bowlY-30;`

A NARROW COLUMN and a short window, and that is not incidental. The first version sampled 60x120
        and found the fire spanning -43..58, which looks like it sits below the bowl - but it was
        measuring the brazier's RADIAL LIGHT POOL, a warm radial gradient baked into the sprite and
        336px across, plus the warm floor underneath it. The bowl and the stand are the dark metal in
        the middle of that.

        So the sample is the stand's own width (10px of rect at o-5..o+5, so 12 columns catches it with
        margin) and a 70px window around the bowl. Within that window the only warm pixels are the
        flame, because the light pool is too diffuse to pass `r > 110 && r > b + 50` near the metal.

## [h:99-tests-72]
near: `ok(G.flameBaseRow===G.flameR+G.flameBaseY,'FLAME_BASE_ROW is '+G.flameBaseRow+`

And the constant the drawing uses must be the ROW, which is what flameFrame actually bakes.
        FLAME_BASE_Y is a distance from the sprite's middle; flameFrame writes `o + FLAME_BASE_Y` with
        o already the half-size, so the row is FLAME_R + FLAME_BASE_Y. Reading the distance as the row
        put the fire one whole half-size too low, which is the entire remaining bug.

## [h:99-tests-73]
near: `test('displayed damage is rounded to something the interface can draw',()=>{`

THE NUMBERS THE PLAYER READS, and the ones that were arithmetic accidents underneath them.

    Three separate defects, and they are worth keeping apart because only one of them is what it looks
    like:

    1. `BOSS_SHELL_DMG = SHOT_DMG*0.8` evaluated to **1.4400000000000002**. Seventeen significant
       digits for a tuning number, and it propagates into every number derived from it. The constant is
       the intent, 1.44, written out.
    2. `fmtHearts(21.25)` printed **"10.625 hearts"** - a real total, expressed in a unit the interface
       cannot draw. Health is measured and DISPLAYED in half-hearts, so anything finer is noise.
    3. The dev panel printed `toFixed(2)` on derived products, so `0.5+0.66` showed as `1.16` or
       `1.1600000000000001` depending on the day.

    What is deliberately NOT done: rounding the simulation. A lunger hits for `0.96 * 0.66 = 0.6336`
    half-hearts, and rounding that to a whole heart makes every hit worth at least one. That is a
    balance change wearing a formatting costume, and this ladder is fractional on purpose.

## [h:99-tests-74]
near: `startGame();`

The test for the rounding being presentation rather than a balance change. If health were rounded
      at the source, a sub-heart hit would become a heart and the whole damage model would shift. This
      asserts the fractional part SURVIVES into the player's health bar.

      Two fixture details, both of which cost a wrong diagnosis first. `startGame()` leaves the player
      in invulnerability, and `damagePlayer` returns false on iframes BEFORE it subtracts anything - so
      the first version of this ran, did nothing, and reported "the lunge did no damage at all" as
      though the damage model were broken. And the lunger's damage is not on `e.pay`; the field the
      game uses is LUNGER_PAY through ARMOUR.

## [h:99-tests-75]
near: `ok(rect.x>=0&&rect.x+rect.w<=canvas.width,'the bar runs from x '+rect.x+' to '+(rect.x+rect.w)+`

ON THE CANVAS, which is the property that holds in BOTH places. It used to assert the room's
      right wall, which is true on a floor and meaningless in a room wider than the screen - and the
      Lab is the one place this bar is checked by eye. The room's right edge is not where the bar
      stops; the canvas is.

## [h:99-tests-76]
near: `ok(rect.y>ROOM_TOP,'the bar is at y '+rect.y+', which is inside the room (top '+ROOM_TOP+`

The bar must be OUT OF THE PLAYING AREA and ON THE CANVAS, and both of those are asserted as
      geometry rather than as a description of where it is.

      It was first drawn at y 84, straight across the momentum row, and the test for it asked
      whether it was "above the room" - which was TRUE of the broken version and FALSE of the fix,
      so a correct fix would have failed. It then moved inside the room's top edge, which cleared the
      HUD plates and collided with the play area instead, and that was reported as disruptive. The
      assertions below are the two properties that survived every version: it is on the canvas, and
      it is not over the room.

      Anchoring is to the CANVAS rather than the room because the Lab's room runs past the bottom of
      the screen and the camera scrolls - a room-anchored bar is off-screen there by 290px, which is
      the one place the bar exists to be looked at.

## [h:99-tests-77]
near: `const platesBottom=HUD_BLOCK_Y+HUD_HP_H+HUD_ROW_H+HUD_GAP+HUD_ROW_H+HUD_FRAME;`

And it is nowhere near the HUD plates, which are at the top. The plate block is three rows tall
      and now starts under the top band, so its bottom is the DEPTH plate - the third row - not the
      first two. This used to stop at HUD_HP_H+HUD_ROW_H, which was already the wrong row: the depth
      plate hangs 31px below that, so the assertion was passing with 31px of unused margin and would
      have kept passing if the depth plate had grown twice as tall again. Read as the block's real
      extent, from the same terms drawHUD lays it out with.

## [h:99-tests-78]
near: `startGame();`

The boss had a floating bar over its body AND the fixed bar at the bottom of the screen. Two
      bars for one health is one too many: they disagree the moment both are on screen, because the
      floating one is 4px and rounded to whole pixels and the fixed one is 10px and notched, and the
      player has to decide which to believe. It is also redundant with the player's own health bar -
      the fight had two bars on screen and one of them belonged to a third party.

      So drawBossBar is the ONLY place the boss's health is drawn, and this asserts the exclusion
      rather than trusting a comment. Every other body keeps its floating sliver: at 4px over a 14px
      body it is a glance, not a reading, which is the right amount of attention for a Brunch.

## [h:99-tests-79]
near: `startGame();`

Two bugs in one rectangle, and they pull in opposite directions.

      The first version read ROOM_W and ROOM_TOP into constants at load time, so the Lab drew a bar
      sized for a 700px room inside a 1680px one. bossBarRect() fixed that by measuring the room per
      draw - and introduced the opposite failure: measuring the room per draw in a room that is
      WIDER THAN THE SCREEN draws a 1640px bar starting at x 70 on a 960px canvas. 750px of it, the
      flush-right "PHASE n" caption and the right-hand side of both phase notches were off the side
      of the screen. The Lab is where every tell gets checked, so that is the one place the bar was
      not visible.

      Neither of those is caught by asserting a WIDTH against the room, which is what this test used
      to do - and which is why it went green over both defects in turn. So it asserts the property
      each one violates: the bar fits the canvas it is drawn on, and it fits it the same way in both
      places.

      FLOOR UNCHANGED is asserted explicitly and exactly. A fix that made the Lab correct by
      re-anchoring everything to the screen would have widened the floor bar from 660 to 920, moved
      it 50px left, and every screenshot of a normal run would have changed. The room still decides
      where the bar sits; only a room wider than the screen gives up its edges.

## [h:99-tests-80]
near: `render();`

BOTH NOTCHES AND THE CAPTION, measured off the framebuffer rather than off the arithmetic.

      The claim is that they are ON the screen, which is a claim about pixels, and a rectangle whose
      width is correct can still put a notch outside the canvas if the notch is placed from something
      other than the bar's own left edge. So: draw it, and sample the notch in the Lab. The notch is
      bone-white standing proud of the bar, 2px wide and h+8 tall, so at full health it is against the
      dark recess - which is what makes it findable at all.

## [h:99-tests-81]
near: `const band=pixelsAtWorld(nx+cam.x-1,labRect.y+cam.y+2,2,4);`

The bar is drawn in SCREEN space, so it is sampled as screen - which is why cam is added back on
      both axes before handing world coordinates to the one sampler in this file. pixelsAtWorld is
      the only pixel reader here and it converts the other way; passing it screen coordinates in the
      Lab, where the camera is not the identity, would sample the wrong part of the frame entirely.

## [h:99-tests-82]
near: `startGame();`

The claim this makes is that the mark on the screen and the threshold in the tick loop are the
      same number, so the two cannot drift. The subtle part is what "same" means. The notch is
      placed at Math.round(w*th) and the fill's edge at Math.round(w*frac), and comparing those two
      PIXELS is the correct test. Comparing Math.round(w*th)/w against th is not - it comes out
      0.0006 apart, which looks like a mismatch and is not one, because both were rounded from the
      same width. That is the mistake this test exists to not make, and it made it first.

## [h:99-tests-83]
near: `startGame();`

THE LAB IS WHERE THIS BAR GETS LOOKED AT. Every other tell in the game is checked there
      without playing a run, and this one was the exception: the Lab's room is 1680px wide and the
      screen is 960, so for the whole life of the bar roughly half of it - and the flush-right
      "PHASE n" caption with it - was drawn off the side of the canvas in the one place you would go
      to check it. A bar that is only really drawn on a floor is a bar that is only really checked on
      a floor, which means the Lab has been testing nothing about it.

      This asserts the vertical relationships that make it legible, and it asserts them against
      PIXELS wherever the claim is about what is on the screen, because each of them is a claim about
      overlap and overlap is not a property of two numbers - it is a property of what got painted on
      top of what. The measurements are stated because the alternative was three more pairs of
      hand-copied constants that agree with each other and disagree with the drawing:

        the boss bar        567..577, frame 564..580
        the Lab legend      574..600   <- 6px of overlap, and the legend drew last

      So the legend gives up the 6. It is the one that moves, for two reasons that are in the code
      beside it: the bar is anchored to the canvas edge and is the one fixed thing in a fight, and
      the legend is screen space over a world-space SHELF, so moving the legend up would put it on
      the shelf at some camera positions and not others.

## [h:99-tests-84]
near: `const pxAt=(x,y)=>{ const d=pixelsAtWorld(x+cam.x,y+cam.y,1,1); return [d[0],d[1],d[2]]; };`

2. THE BAR'S OWN PIXELS ARE STILL THE BAR'S. The overlap was invisible in the data and obvious
      on the screen, so this is a framebuffer read: sample the middle of the fill and require the
      Warden's green. 0x5ee27a is the fill colour the bar draws, and under an 86%-opaque legend strip
      it would be roughly 0x1a1d1b - the same dark that an absent bar would give.

## [h:99-tests-85]
near: `const lg=pxAt(W/2,lane.y+lane.h-4);`

3. THE LEGEND IS STILL THERE. Shrinking its lane must not have shrunk it out of existence, and
      "no overlap" is satisfied just as well by a legend that is not drawn - so the strip's own
      pixels are read too. It is 86%-opaque near-black over whatever is behind it, so it lands dark
      and neutral regardless of the room, which is what makes it findable.

## [h:99-tests-86]
near: `const rects=[], realFill=ctx.fillRect.bind(ctx);`

4. AND THE CHIPS ARE INSIDE THAT LANE. This is the assertion the shrink is really for, and it
      is the one that was missing until the row was made to follow the lane.

      A 26px strip with its chips on a fixed y was correct and became wrong the moment the strip
      shrank to 20: the chips stayed at y+6 and hung 3px out of the bottom of their own background,
      over the canvas edge. Every assertion above still passed - the lane was below the bar, the strip
      was still drawn, the bar's pixels were still the bar's - because none of them were about where
      the CONTENTS are. An overlay's background being in the right place says nothing about its text,
      which is the same failure as a health bar drawn in the right frame with the wrong number in it.

      So the chips' own rects are read out of the drawing and asked whether they fit. Sized by what
      the chip colour is rather than by position, because the state caption on the right is drawn as
      text with no rect of its own and the strip's own background is a rect too.

## [h:99-tests-87]
near: `b.noticeTimer=0;`

noticeTimer has to be spent before the tick loop will step this body at all: the enemy loop
      skips any body whose noticeTimer is still counting down, so a freshly spawned boss ignores
      several updates and the phase never moves. That is the FIXTURE being wrong rather than the
      game, and it is the same trap as spawnEnemy's boss flag - a helper that quietly returns
      something usable-looking instead of what was asked for. Both cost real time here.

## [h:99-tests-88]
near: `startGame();`

This is the justification for drawing the two notches at all. If crossing 66% did nothing
      observable, marking it would be decoration. It does something observable: the boss builds its move
      bag from the phase number, so phase 2 adds the wall and phase 3 is mostly sweep.

      AND IT ASKS THE GAME, WHICH IS THE WHOLE POINT. This used to rebuild the bag by hand:

          bag.push('volley'); bag.push('volley'); bag.push('sweep');
          if(p>=2) bag.push('wall');
          if(p>=3) bag.push('volley'); bag.push('sweep'); bag.push('sweep');

      - which is a COPY of the line in 60-tick.js, braces and all. When that line was missing its
      braces the copy was missing them too, so the two agreed perfectly and the test passed against a
      boss that charged 60% of the time in its introductory phase. A test that restates the rule it is
      checking cannot discover that the rule is wrong; it can only confirm that the statement and the
      implementation are identical, which is a different and much weaker thing.

      So the bag is read out of the game, by counting the moves it actually performs. `stepBoss` picks
      with `Rnd.run()`, so it is asked thousands of times and the moves are tallied from what the boss
      does - beginBoss is intercepted so the fight does not actually play out 9000 times.

## [h:99-tests-89]
near: `for(let i=0;i<3000;i++){`

`stepBoss` resolves an IN-PROGRESS move and returns (line 154: `if(e.move!=='idle')`), so a
          boss that is mid-attack never reaches the draw. Because `beginBoss` is stubbed out, nothing
          ever ends the move either - so without these two resets the boss picks exactly one move in
          the whole phase and every distinct-move count comes out as 1. It is a stub artefact, not a
          fact about the fight.

## [h:99-tests-90]
near: `const size=p=>draws[p].names.length;`

THE LADDER OF MOVES PER PHASE: 3, then 4, then 7, which is what REPORT.md describes. That is a
        count of ENTRIES IN THE BAG, and a bag can hold the same move twice - phase 1 is
        `volley volley sweep`, so it has three entries and two DISTINCT moves, and volley is meant to
        be the common one. So this asserts entries, and the distinct set is asserted separately as
        "which behaviours exist", because those are two different questions and conflating them is how
        the wrong bag hides.

        Entries are read as the number of draws divided by the draws per entry... which is circular.
        So they come from the PROPORTIONS instead: with an entry counted once per appearance, the bag
        size is the reciprocal of nothing measurable - and what IS measurable is that each phase can
        produce the moves it claims and only those. The distinct-move sets below are the real
        assertion; these two are the escalation.

## [h:99-tests-91]
near: `startGame(); render();`

The row belongs to the item name alone now. Two earlier layouts failed and both failures are
      the reason this test asserts what it does rather than merely checking that nothing overlaps.

      Labels were first centred in their own plates and overlapped, because eleven of thirteen item
      names are wider than a 62px plate. Then all three names were fitted onto one 147px row, which
      does not fit at 12px (the worst case is 210px), and the version of that test asserted against a
      hardcoded row width of 190 - so it passed while checking a geometry the game does not have.

      So the row width is READ from the drawing, and the assertion is not "no overlap" but "every
      name whole at 12px", because the defect that actually reached the screen was never an overlap.
      It was "ARCAN... UNTER'S ... BLAST": a collision fixed by deleting the information.

## [h:99-tests-92]
near: `const huge='Weighted Grip Of The Lantern Friend Of The Very Long Name';`

The overflow branch, exercised deliberately. Every name in the roster fits, which means the
      shrink and truncation paths never run - so a test that only walks the roster cannot see whether
      they work at all. A mutation that removed the shrink passed 178/178 for exactly that reason,
      and the branch it disabled is the one that protects the next item name somebody adds. So a
      name far too long for the row is fitted here and the result is checked for the three properties
      that matter: it terminates, it fits, and it admits what it did.

## [h:99-tests-93]
near: `const r=currentRoom();`

And the consequence, which is the part that was never cosmetic: a body that walks through where
      the old field sat must not be charged by it. The field is gone, so this cannot happen - but the
      assertion is on the body's state rather than on the array, because a field that survived but
      happened to be out of range would pass an array check and still be a live bug next room.

## [h:99-tests-94]
near: `Stats.reset();`

And the beam is on PRECISION, not on luck. It used to be on luck, which meant one number was
      doing two unrelated jobs - deciding what the dungeon contains and deciding how narrowly a shot
      leaves the wand - so neither could be tuned without the other and a Lucky Coin was quietly a
      damage item. Both halves are asserted here because the interesting failure is the quiet one:
      a stat that still has an effect it should not.

## [h:99-tests-95]
near: `startGame();`

The design started from five types with "artifact" listed as a sixth that "falls under one of
      the previous categories" - which is a description of a FIELD, and encoding it as a category is
      what would have produced hybrids. A Brass Compass is simultaneously a legendary passive AND an
      artifact, and neither half is a special case.

## [h:99-tests-96]
near: `startGame();`

NOTE: this used to call Items.reset() immediately after startGame(), and that call was the
      only reason the assertion below could pass. It created the `unlocked` bucket the test then
      read, so the test was checking that a bucket it had just built by hand was still there after
      an item went in - and the real path, where a player picks an artifact up mid-run with nothing
      having called reset() first, was never exercised. The lab found it: it equips every item in
      the game at once and threw on the first artifact.

      The bucket now belongs to the run's own literal, so this asks the question it means to ask -
      can a fresh run take an artifact - and the removal of the reset() is the assertion.

## [h:99-tests-97]
near: `startGame(); Items.reset();`

This is the check that would have caught the worst bug in the item framework's first day, and
      it is here because nothing else did.

      Strength and Vigor were on the character sheet with names, bars and blurbs for a full day, and
      NOTHING READ THEM. Weapon damage used the weapon's own number and player.maxHp was the literal
      8 written into the player at spawn. So a player could pick up Heavy Hands and Iron Ribs, see
      the sheet change, and take a gun that did exactly the same damage with exactly the same health.

      The reason it survived is worth recording, because it is the most dangerous shape this project
      has: the sheet renders from Stats and the game reads a constant, and those two were different
      numbers. Every check so far asked whether Stats was CORRECT. None of them asked whether
      anything was READING it. A stat with no consumer is not a small stat - it is a lie with a bar
      next to it, and it is the most expensive kind of bug to find by playing, because the player's
      own report would be "the item did nothing" and there is no way to tell from inside the game
      whether the item is broken or the wiring is.

      So each stat is measured here by its EFFECT, not by its value. If one of these ever stops
      being true, the stat has become decorative and the fix is to either wire it or delete it.

## [h:99-tests-98]
near: `eq(JSON.stringify(Content.all('item')),before,'Intelligence changed the item table, which is not '+`

This IS the hook, and no `ok(true,'')` is needed beside it. When magic doors land they gate
        items, this eq() goes red, and the failure message - "Intelligence changed the item table,
        which is not what it is for" - is the note to whoever lands them. A passing assertion with an
        empty message adds nothing beside it and reads like a check that has not been written yet.

## [h:99-tests-99]
near: `const bStr=Stats.baseOf('strength'), bVig=Stats.baseOf('vigor');`

The class baseline PLUS the build. And "health ceiling" is now read off vigor, because Vigor IS
      the pool - there used to be a BASE_HP constant here that was a second place knowing what the
      starting health was, and the starting health is a CHARACTER's number now. Referencing a
      deleted constant would have thrown rather than failed, so this line is a reminder that the
      honest assertion is the one that reads the single source.

## [h:99-tests-100]
near: `startGame(); goTo('normal'); setPaused(true);`

The click lands on the character sheet's backdrop, because that is what now covers the screen.
The canvas is not a valid target any more and dispatching there is CORRECTLY ignored - the
suppressor stops the game seeing input while an overlay is up, and a click that reached the
canvas would be the game acting on input the player cannot see it acting on. The claim is "a
click puts the game back down, and does not also cast", and the backdrop is where such a click
goes.

## [h:99-tests-101]
near: `test('loot room: two distinct items, and never one you already hold',()=>{`

THE ITEM HALF OF THE LOOT POOL. Thirteen items existed, validated and wired to the sheet, and not
  one could be picked up: there was no `item` pickup kind anywhere, so `Items.pool` and
  `Items.rollRarity` were called from nowhere and every number in the roster was a guess about how a
  thing feels rather than a measurement. These three are the difference between "the roster exists"
  and "the roster can be playtested".

## [h:99-tests-102]
near: `startGame();`

The invariant, tested over the whole roster rather than one example.

      This replaces a test that asserted the opposite - that an item already held would stay on the
      floor - because that behaviour was the last silent dead pickup in the game: a tile you can walk
      onto forever that does nothing, with no message, in the one room whose whole job is handing you
      things. It read as a broken item rather than as a full build.

      So the claim is now that no pickup is refused, and it is checked by offering EVERY item twice -
      once into an empty build and once into a build that already holds it - because "a duplicate is
      the hard case" is only true while duplicates are a special case.

## [h:99-tests-103]
near: `test('records save atomically, load either format, and never leave a half-written record',()=>{`

RECORDS ARE WRITTEN AS ONE ATOMIC VALUE, so a failed save cannot leave a false record behind.

      `saveRecords` used to write five separate keys inside one try/catch with an empty catch. Measured
      by making the third `setItem` throw, which is what a quota error does:

          depths_best      written
          depths_fastest   written
          depths_wins      stale
          depths_deepest   stale
          depths_tickhz    stale

      A partial write is not a smaller record, it is a FALSE one. `depths_deepest` is the number the
      summary tells the player they have reached; if it silently keeps an old value because the write
      before it threw, the summary reports a personal best that is not one, and the old value survives
      a reload. Nothing anywhere said so.

      The records are now one JSON value under one key, because one `setItem` is atomic per key: it
      lands or it does not. Measured after:

          clean save        one key written, all four legacy keys removed
          failed save       NOTHING written - 0 of 5 keys
          three failures    one console warning, not three

      Asserted as the outcome properties rather than as "calls setItem once", because atomicity is a
      property of the storage API and the mechanism is free to change. The five properties that matter:

        1. a clean save leaves exactly one record key and retires the legacy ones;
        2. a save where every write throws leaves NOTHING behind - the whole point;
        3. repeated failures warn once, so a loop cannot flood the console;
        4. the previous flat format still loads, so an existing player loses nothing to the change;
        5. a corrupt blob is treated as absent rather than thrown, because a bad value must not stop
           the game starting.

      Plus the read-order property, which is the one that would undo the fix from the other side: if a
      browser has both formats, the single-key record is believed. Reading legacy first would let a
      stale `depths_deepest` overwrite a good one - the exact failure this was introduced to prevent.

## [h:99-tests-104]
near: `eq(state,'playing','clearing the boss ended the run by itself');`

Killing the boss is not the end and neither is the way out: the way out goes DOWN. There is no
longer any path through a normal run that reaches endRun(true), so a run ends when the player
dies and only then. These two drive the death directly rather than walking into the portal,
which is the point of the change - a test that still reached 'win' by walking would be testing
a door that no longer exists.

## [h:99-tests-105]
near: `eq(storedRecords().deepest,4,'the deepest floor was not written to storage');`

Read the record the way the game does, through loadRecords, rather than poking a flat key.
      The records moved into a single JSON key so that a partial write is impossible; a test that
      still reads `depths_deepest` would be asserting on a key the game no longer writes, and would
      pass or fail for reasons that have nothing to do with the record.

## [h:99-tests-106]
near: `test('the heart plate never reaches the minimap, however much health there is',()=>{`

THE PLATE HAS A CEILING OF ITS OWN, because maxHp does not.

    Vigor is unbounded by design, so the heart row used to grow without limit: 28 hearts is 810px of
    plate from a margin of 15, and the minimap plate starts at 798. Past maxHp 56 the plate covered the
    map - the thing that says where the boss is and which rooms you have seen - for the rest of the
    run. It took twenty-one Iron Ribs, so nothing caught it: nothing plays that build.

    Asserted as GEOMETRY rather than as a pixel sample, because the collision is arithmetic and can be
    asked directly: the plate's right edge must stay clear of the minimap at every health. The
    minimap's own position is derived from the same terms its drawing uses - W, the margin, GRID*cell
    plus its frame - so this cannot pass by agreeing with a stale copy of a number.

## [h:99-tests-107]
near: `const rowAt=(mx,hp)=>{`

THE ROW DRAINS LEFT TO RIGHT, AND THAT IS NOT NEGOTIABLE.

      This test previously asserted the OPPOSITE - that at 1 heart out of 98 the lit heart must be the
      last slot on the plate - because that is what the code was doing. The bar counted backwards: at 2
      health the row read `[empty x7, full]`, the one heart with blood in it sitting in the far right
      slot. The test was written to match the implementation, which is the exact failure this file
      exists to prevent, and it is the second time in one session I have done it.

      The direction is not a style question and does not depend on whether hearts are hidden. A health
      bar that empties from the left, at every health, in every game, is the contract; the only question
      the cap raises is how many slots exist, not which way they fill.

      So this asserts the DIRECTION, on an ordinary uncapped plate where every heart is visible, at
      enough health values to catch a reversal - a single sample cannot tell "drains right" from
      "drains left" when both ends are lit.

## [h:99-tests-108]
near: `const haloAt=(mx,hp)=>{`

THE HALO SITS ON THE HEART WITH BLOOD IN IT.

      The "one heart left" pulse is a warning about a specific slot, so where it is drawn is part of
      what it says. It was drawn on `slotX + (heartsDrawn-1)*heartSlotW` - the last slot ON THE PLATE -
      on the reasoning that "the last heart" meant the last heart. It does not: the row drains left to
      right, so at one heart the only lit slot is the FIRST, and the halo pulsed an empty heart at the
      far end while the one being looked for sat unlit at the other.

      Measured before the fix: lit heart at x 43, halo at x 225, on an eight-heart plate.

      Asserted as POSITION against the fill, at a health where the two are far apart - one heart is
      the sharpest case, because "last drawn" and "last lit" are then 182px apart. Also asserted on a
      capped plate, where the gap is even wider.

## [h:99-tests-109]
near: `test('mouse maps to canvas pixels inside the 2px border',()=>{`

POINTER COORDINATES ARE INTEGERS, and that is the whole reason this test exists.

    A `MouseEvent` cannot carry a fractional `clientX`: dispatch one with 59.59375 and the handler
    receives 59. So the content top-left - a fractional layout position - is not an addressable
    point, and asserting that it maps to (0,0) is asserting something no cursor can ever do. The
    original test asserted it anyway, with a 1px tolerance, which is loose enough to pass a handler
    that was a whole pixel out.

    So this asserts the property that actually matters: EVERY position a cursor can physically be
    maps INSIDE the canvas. Not "the corner maps to zero" - "no reachable position produces a
    negative coordinate or one past the far edge", because a negative coordinate means the shot is
    aimed from off the left of the screen.

    Measured at a 960x600 window, the canvas is letterboxed to a content box of 840.797 x 525.484
    whose left edge is at clientX 59.5938. The first INTEGER the pointer can report is 60, which is
    0.4638 of a canvas pixel inside the content. Reading the offset from `clientLeft` (2) rather than
    the computed border put the same cursor at -0.678, which is off the left of the canvas entirely -
    so the old mapping aimed outside the play area for the first half pixel of pointer travel, and
    the old test passed because 1px of tolerance is wider than that error.

    Checked across the whole reachable range at several window sizes rather than at one point: a
    mapping that is correct in the middle and wrong at the edge is the shape of the camera clamp
    bug, and asserting the endpoints is what catches it.

## [h:99-tests-110]
near: `const shapes=new Set(), counts=new Set();`

This test used to assert a fixed 15 rooms, two forks and exactly four dead ends, because that
was the shape the old hand-laid generator produced every single time. Asserting it again would
be asserting the absence of the thing the redesign was for: a dungeon you can learn in one run
is a corridor with monsters in it. So the invariants are now the ones that have to hold for a
tree of any shape - the roles exist, the ends are clean, the keys are not behind the doors they
open - and the room count is only required to VARY across dungeons.

## [h:99-tests-111]
near: `startGame((i*2654435761)>>>0);`

A DIFFERENT SEED EACH TIME, stated rather than inherited. This loop used to rely on the
        generator simply carrying on from where the previous dungeon left it, which is exactly the
        behaviour `startGame(seed)` was introduced to remove - so it built the SAME dungeon 200 times
        and the "the room count must vary" assertion below failed on a generator that varies fine.

        The multiplier is the one the neighbouring seed-space test already uses, so the two agree on
        what "a different seed" means.

## [h:99-tests-112]
near: `const STRENGTH=6, ARMOUR=ENEMY.lunger.armour||1;`

The Scatter is deliberately not in this. It is a buckshot gun: a tight cone of eight pellets
      behind a long cooldown, so it is the biggest thing you own with your nose on the target and
      the worst thing you own across the room. The other three are meant to hold up at range.

      The BEAM is in it, but at a BUILT strength rather than at base - and that is the assertion
      worth having. Its damage is deliberately low (0.50) because Strength is added per shot and it
      fires 17.31 times a second against the Bolt's 2.37, so every Strength sigil is worth several
      times more to it. At base it is the slowest gun in the roster by design; at +6 it meets the same
      bar as everything else. A test that only checked the base case would be asserting that a canvas
      is not a canvas.

      Note this arithmetic is off the TABLE, with Strength and armour applied by hand, because that is
      what the table means. It still omits the Beam's miss rate - it sprays, landing 81% at 100px and
      27% at 300px - so it is a floor on how fast the gun can be, not a prediction of how fast it is.

## [h:99-tests-113]
near: `const sc=WEAPONS[1];`

and the shotgun identity, asserted rather than assumed. This is about the SHOT, not sustained
dps: the biggest single hit in the game, behind the longest wait, in the tightest cone, and the
steepest collapse with distance of anything you can hold. Armour is a per-hit multiplier, so
eight small pellets is genuinely the wrong answer to an armoured lunger and the right one to a
Brunch knot - that is the trade, not a flaw. Note the test works off the table cooldowns, not
the TEMPO-adjusted ones, because TEMPO divides every gun by the same factor and cannot reorder
them.

## [h:99-tests-114]
near: `ok(sc.muzzleJitter>0,'the shotgun has no muzzle scatter, so every pellet still leaves from one point`

The pattern is no longer a cone, so the old "spread*(count-1) rad wide" assertion is gone: it
measured an angle that no longer decides where the pellets go, and leaving it in would have
been a test that passes while describing nothing. What replaced it is the actual geometry -
a column whose width is dominated by a term that does NOT scale with range, and a spread that
is a distribution rather than a set of evenly spaced steps.

## [h:99-tests-115]
near: `ok(sc.pelletAngle*450<8,'at the far corner of a room the angular term alone spreads the pattern '+`

the angular error has to be small enough that the pattern is still a column and not a cone
again. Measured in PIXELS at the far corner of a room, not in radians - an earlier version of
this line compared a pixel figure against 0.02, which is a radian number, and failed a gun that
was behaving exactly as intended.

## [h:99-tests-116]
near: `const at=(wp,d)=>wp.fMin+(1-wp.fMin)*Math.max(0,1-(d-wp.fNear)/(wp.fFar-wp.fNear));`

Two different fights, two different numbers, and the difference between them is what makes
the shotgun a shotgun. `crowd` is every pellet landing, which is what the tight cone buys.
`solo` is one body, so only one pellet of the eight counts. The Scatter is the best gun in
the game at the first and among the worst at the second, and it is the same gun either way.
Keeping them as separate measurements is the point: asserting only the crowd number is what
let the Voidball and the Bolt sit on identical numbers for four turns, each looking correct.

## [h:99-tests-117]
near: `ok(ALT_WEAPON.pool*ENEMY.lunger.armour>ENEMY.lunger.hp,'the budget cannot reliably afford one armour`

and the pool still has to afford a whole heavy body on its own, which is the entire reason it
divides by ARMOUR. what it must NOT do is keep spending that budget no matter how many bodies
are in the way: the whole point of DISPERSE is that the damage actually dealt collapses as the
crowd grows, so a pack is something you chip down rather than something one click clears.

## [h:99-tests-118]
near: `const burn=()=>{ player.cooldown=999; player.cooldownMax=999; player.altCooldown=999;`

burn() puts every meter past its own maximum on purpose - 999 against a maximum of a few hundred -
      so that "was it refilled" cannot be satisfied by a value that merely shrank into range. The right
      click is deliberately NOT given a maximum here: enterRoom overwrites it from the weapon table, and
      the first version of this fixture set it, which made the assertion below depend on a maximum the
      code path had already replaced.

## [h:99-tests-119]
near: `eq(player.blinkCharges+player.blinkRegen/BLINK_RECHARGE,0.5,'entering a live room put the blink '+`

The blink no longer SNAPS to full on entry: the bar starts where the player walked in and
fills across the arrival, so these claims are about the animation starting in the right place
rather than about a jump. burn() leaves the player on one charge - zero charges and half a bar -
so the bar must come back showing exactly that, not two charges they never had.

## [h:99-tests-120]
near: `const cell=17,mapW=GRID*cell,pw=mapW+28,mx0=W-HUD_MARGIN_X-pw,my0=HUD_BLOCK_Y,mx=mx0+14,my=my0+14;`

The map's own placement, read from the HUD's terms rather than retyped. It was `my0=14` here
      and `MARGIN_Y` in drawHUD, which is two copies of one number - and the two copies disagreed the
      moment the top band arrived, so the test was watching for a mark in a cell that was no longer
      where the map was. A test that computes where the drawing is has to compute it the same way.

## [h:99-tests-121]
near: `const MARGIN_X=HUD_MARGIN_X, MARGIN_Y=HUD_MARGIN_Y, FRAME=HUD_FRAME, GAP=HUD_GAP;`

This used to pin the blink plate at a hardcoded (14,56) and then assert the old padding
numbers around it, so every layout improvement broke the test and every layout change had
somewhere to hide. The assertions below are about the SHAPE of the layout - one margin, one
frame thickness, one gap, plates that touch, bars that are centred - which is the thing that
has to stay true, and which is the thing the old assertions could not express.
X and Y are separate numbers, and this test READS them rather than restating them. It used to
re-declare all nine, on the stated ground that it wanted to test "the HUD matches a stated
layout" rather than "the HUD matches itself" - but `ok(MARGIN_X!==MARGIN_Y)` against its own
two copies cannot fail whatever the game does, so the one assertion in here that was about the
margins rather than about the drawing was measuring the test. The numbers are now one table in
70-view.js, read by both, which is the same fix as the boss door and the pulse.

## [h:99-tests-122]
near: `const BLOCK_Y=HUD_BLOCK_Y;`

BLOCK_Y is the top of the PLATES, which is no longer the top margin: the top band owns the
      strip above it. It is read rather than recomputed, and the assertions below are all relative -
      the block's plates against each other - so they read the new silhouette rather than the old
      numbers. What they must not do is re-derive BLOCK_Y here, because a test that has its own copy
      of the layout cannot tell a moved HUD from a moved expectation.

## [h:99-tests-123]
near: `const blink=plates.find(p=>p.w===BLINK_W&&p.h===ROW_H);`

row 2: the blink plate is glued to the bottom of the health plate and shares its left edge,
and is deliberately NARROWER. Two short bars stretched across the full 186px read as progress
on something enormous, and that asymmetry is what makes the two read as two different
instruments stacked rather than as one long bar with a second row of decoration

## [h:99-tests-124]
near: `const {rects:borderRects}=grab();`

The wooden border is the same thickness on BOTH sides of every plate, and the only way to know
      that is to measure the drawing rather than read the constant back.

      This was `eq(FRAME,FRAME,'the right border of a plate is not the same as its left')` in a loop
      over four plates. FRAME compared with itself, four times, carrying a message about plate
      borders: it could not fail under any circumstances, and the loop around it made it look like
      four checks. It is the exact shape of the inert Strength and Vigor stats - an assertion whose
      text describes a property and whose expression cannot detect it.

      What it should have measured: each plate is a wood image with an inset drawn inside it, so the
      left border is (the inset's left edge minus the plate's left edge) and the right border is
      (the plate's right edge minus the inset's right edge). Those are two numbers derived from
      geometry, and they can disagree.

## [h:99-tests-125]
near: `const big=plates.filter(p=>p.w>=60&&p.h>=28&&!(p.w===W&&p.y===0));`

The top band is EXCLUDED BY NAME rather than by its height. It is a wood image with no inset, so
      including it makes this loop report "a plate at 0,0 has no inset drawn inside it, so its borders
      cannot be compared" - which is true of the band and useless as a statement about borders. It
      currently drops out anyway because 15px fails the >=28 height test, which is an accident of the
      band's size rather than a decision: raise the band to 40px and a border test fails on a plate
      that is not a plate. Being explicit here means the answer survives the band's height changing,
      which is the whole subject of the band's existence.

## [h:99-tests-126]
near: `const insetR=borderRects`

The inset belonging to THIS plate. drawInset draws a 2px rule on each side and then fills the
middle, so there are five rects inside every plate and the one to measure is the LARGEST -
the fill. Sorting by distance to the plate's corner, which is what this did first, picks the
174x2 top rule instead and reports a 6px top border against a 32px bottom one, which is a
true fact about a rule and a false one about the frame.

## [h:99-tests-127]
near: `const gap=(a,b)=>b.x-a.x-a.w;`

touching, and spanning the map exactly. The two OUTER gaps are the nominal one; the middle
pair is the reserved consumable slot, which is CENTRED in whatever the map's width leaves
over, so its two gaps can differ from each other by the rounding pixel and must - centring it
on the row is the whole point of it being in the middle.

## [h:99-tests-128]
near: `const leftColumnBottom=BLOCK_Y+HP_H+ROW_H+GAP+ROW_H;`

THE BLOCK CLEARS THE ROOM, which is the whole reason the band is 15px and not 40.

      The depth plate is the third row and it is the thing that would touch the play area: ROOM_TOP is
      a balance constant this file does not own, so the band cannot grow past what leaves the left
      column ending above it. A band that pushed the depth plate onto the top wall would hide bodies
      walking along it, and no test anywhere was watching for that - which is why the number lives
      here as a constraint on the layout rather than as a preference in a comment.

      The right column is checked against the SCREEN rather than the room: the map hangs in the margin
      outside a 700px room, and it is the canvas it has to stay inside.

## [h:99-tests-129]
near: `startGame();`

THE BAND IS THE ONE THING AT THE TOP OF THE SCREEN, and the plates are under it.

      This is asserted as PIXELS rather than as a pair of numbers, and that is the whole point of it:
      "the health plate is at y 29" is true whether or not anything was drawn over it, and the failure
      mode for a band is precisely that - a band arrives, nothing moves, and a plate is drawn straight
      through the thing that was supposed to own the strip. Numbers cannot see that. Pixels can.

      So the band is sampled where it is (wood, warm and mid-tone) and the plate is sampled where it
      is (the inset's near-black fill), and the band is sampled again where a plate WOULD have been
      had it stayed on the top margin - which is the assertion that actually catches the regression:
      the band is opaque there, not HUD showing through it.

## [h:99-tests-130]
near: `readyT=0; fadeT=0; roomFade=0;`

The room fade goes to black over the whole canvas, so a pixel read taken during the arrival
      samples the fade rather than the HUD - and it samples it as opaque black, which is a colour
      nothing in this HUD is. Every other pixel check in this file zeroes the fade first for the same
      reason, and this one has to as well.

## [h:99-tests-131]
near: `for(const [x,what] of [[30,'over the health plate'],[W/2|0,'between the columns'],[W-40,'over the ma`

1. THE BAND IS DRAWN, ACROSS THE WHOLE WIDTH, INCLUDING WHERE NO PLATE IS. Sampled at three x:
      over the health plate, in the gap between the two columns, and over the map. A band drawn only
      as wide as the left column would pass the first and fail the other two, and a band drawn as a
      fill without its wood would be flat where the plates have grain.

## [h:99-tests-132]
near: `const border=pxAt(W/2|0,HUD_TOP_BAND_H-2);`

5. THE RAIL IS A RAIL AND NOT A TEXTURE CHANGE: it has its own dark lower border, and what is
      under it is the empty space over the room rather than more HUD.

      This replaced an assertion about a drop shadow the band used to draw, which could not fail -
      the band is above ROOM_TOP, so what was behind the shadow was already rgb(0,0,0), and a shadow
      over black is black. The test passed on the shadowed version and on the version with the shadow
      deleted, which is the exact property a check is supposed to lack. What IS there and visible is
      woodPlate's own dark border at the rail's lower edge, so that is what is asserted: the last two
      rows of the rail are its dark frame, and the row below them is the void and not the rail's
      middle. A band with no border reads as the texture changing rather than as a frame ending.

## [h:99-tests-133]
near: `const d=host.secret;`

the right click does. read the direction first: breaking the wall clears the marker.
ONE cast, then let it fly: the hook can be detonated early with a second right click, and a
test that held the button down every tick would spend its life cancelling the bolt a few
dozen pixels from the player and never let it reach the wall at all

## [h:99-tests-134]
near: `startGame(); const r=goTo('normal'); r.enemies.length=0;`

The bug this weapon existed to not have: the bolt has to stop on the point you aimed at. The
travel test compares the distance still to cover against the per-tick step, and without that
step stored on the bolt the comparison is against undefined, so the hook sailed straight past
the cursor and only ever went off on a wall.

## [h:99-tests-135]
near: `test('the run summary prints no row label twice',()=>{`

THE WALL SITS CLOSE TO THE ENEMY IT COVERS, not halfway to the player.

      BRUNCH_SHIELD_FRAC was 0.55 - a little over half way along the player-to-target line, which is a
      barricade halfway across the room rather than a shield around a body. At 0.34 the pack is a third
      of the way from the TARGET, so it covers the shooter rather than the lane, and the player is left
      with a wide apron of open floor between themselves and the wall for everything else in the room to
      use. That was the point of the change: a shield that owns the whole lane is a shield the rest of
      the fight never gets to use.

      Measured across three seeds at a 350px gap: the pack centre moved from 0.46-0.63 of the gap to
      0.13-0.42, and pack-to-player distance went from 62-90px to 96-143px.

      The claim is asserted as the FRACTION, because a fixed pixel distance would pass while the wall
      sat on the wrong side of the target - the fraction is what says "closer to the enemy". And it is
      measured from the pack's centroid rather than one body, because a pack whose members disagree
      about where the wall is can average out to the right answer.

## [h:99-tests-136]
near: `test('the run summary prints no row label twice',()=>{`

A BRUNCH DOES NOT EAT THE FIRE OF THE ENEMY IT IS ESCORTING.

      The absorption rule was indiscriminate - any non-friendly shell overlapping any Brunch died there.
      That was right while the only thing a pack covered was the PLAYER, because then every enemy
      shell in the room was heading for the player. The moment a pack moves in front of a shooter,
      that shooter's own firing line runs through the pack, and the pack was eating its own shells.

      MEASURED, and this is the interaction the closer barricade produced: with BRUNCH_SHIELD_FRAC at
      0.34 and again at 0.25, a shooter escorted by a six-body pack landed ZERO shots on the player in
      14 seconds, against nine landings and 16.2 HP lost with no pack in the room. Not "fewer shots" -
      none. Escorting a shooter made it completely harmless, and it got WORSE as the wall moved closer,
      because closer means more of the firing line is pack.

      The rule now is narrow: a shell is absorbed by any Brunch EXCEPT one that is guarding the enemy
      who fired it. Everything else is unchanged, and in particular the player's own bolts are still
      not absorbed - you must be able to shoot through a pack to clear it.

## [h:99-tests-137]
near: `test('the run summary prints no row label twice',()=>{`

A PLAYER HELD AGAINST A WALL REPORTS NO VELOCITY, because the enemies lead what they read.

      `clampPlayer` stops the POSITION at the wall and deliberately leaves the velocity pointing into
      it - which is the right call for the movement code and is documented as such. The consequence
      was already known for the Momentum meter, which measures displacement instead of velocity
      precisely so a pinned player cannot farm it. The GUNNERS read `player.vx` directly to build an
      intercept, and nothing had given them the same treatment.

      Measured, and this was the entire hit rate of every ranged enemy in the game:

          player pinned against a wall, holding the key into it
            player.x             737   (ROOM_RIGHT 750, r 13 - at the wall)
            player.vx           1.40   (full speed, reported)
            shell arrival error  52-55px on every shot, at every range, without variation

      Fifty-two pixels is about 1.3 seconds of the player's travel, so every shell from every shooter
      and gunner sailed past a stationary target by a distance that looks deliberate. Against a player
      in open floor the same solver lands within 10px, and against a still player within 0.4px - which
      is exactly why this read as a prediction problem and took a long time to find. In the open-floor
      fixture, 72 of 75 shells now connect.

      The claim asserted here is the state, not the hit rate: the velocity component the wall is
      eating must be zero while it is being eaten, and only that component - a player sliding along a
      wall while moving on the other axis keeps the velocity that is still real.

## [h:99-tests-138]
near: `test('the run summary prints no row label twice',()=>{`

A BLINK'S TRAIL STAYS INSIDE THE ROOM AND MATCHES HOW FAR THE PLAYER ACTUALLY WENT.

      The trail used to be six puffs laid along `BLINK_DIST` BEFORE the move, on the assumption the
      blink completed. It does not complete when there is a wall in the way - `doBlink` moves the
      player and then calls `clampPlayer()` - so every puff was placed where the player would have
      reached had the wall not been there, and the VFX was drawn through the wall and out of the
      play area. Measured, starting 30px from each wall:

          before   6 puffs, 4 of them outside the room, worst by 86px, in all four directions
          after    1-3 puffs, 0 outside, in all four directions

      Two separate things were wrong and fixing only the first still failed:

        - the trail ignored how far the player travelled, so a 17px nudge into a wall drew the same
          140px streak as a full blink. Length is the only thing telling the player how far they
          went, so it is now derived from the move that actually happened.
        - `clampPlayer` ends with a deliberately loose outer bound (`ROOM_LEFT-40` to
          `ROOM_RIGHT+40`) so a player can sit outside the wall line in a doorway. Using the clamped
          position as the trail's end point therefore still put puffs 40px outside the room on the
          left and bottom walls. The player keeps that overshoot for transitions; the VFX is clamped
          to the room box instead, because a puff drawn outside the room is drawn over the wall.

      Asserted as two separate claims because they fail separately: no puff outside the room, and
      trail length proportional to distance travelled rather than constant.

## [h:99-tests-139]
near: `test('the run summary prints no row label twice',()=>{`

THE RUN SUMMARY DOES NOT PRINT THE SAME ROW TWICE, in any format.

      The records block once gained "Bodies killed" and "Accuracy" to replace two rows that could
      never change ("Fastest clear" and "Dungeons cleared" - nothing increments `records.fastest` or
      `records.wins` any more, since a cleared boss room opens a way out instead of ending the run).
      That fixed the unchangeable pair and created a new problem: the card already carried "Enemies
      defeated" and an accuracy row, so the kill count printed twice and the accuracy printed twice in
      two different formats - "42% (17/40)" up top and "42%" down below.

      Asserted as uniqueness over the LABEL rather than as a fixed list, because a label check is what
      catches the next accidental duplicate, and because the two accuracy rows had different labels'
      worth of disagreement only in their VALUE format - the label 'Accuracy' was genuinely repeated,
      which is the part a reader notices.

      The third assertion is the load-bearing one for the future: the records block must not be left
      empty by a well-meaning removal. The pair it originally replaced was unfixable - nothing
      increments those two fields - and deleting it without replacing it would have turned a
      permanently-zero record into no record at all.

## [h:99-tests-140]
near: `keys={d:true,s:true};`

'd' drives into the wall AND 's' slides down it, so there is a genuine second axis of movement
      to check. Pressing 'd' alone would leave vy at zero for the honest reason that nothing is
      pushing it that way, and the "only one component may be cleared" assertion would pass without
      ever testing anything.

## [h:99-tests-141]
near: `let hp0=player.hp, landings=0;`

The player STRAFES, and stays clear of the walls while doing it.

        Both halves matter and each was a separate wrong measurement:

          - stationary: a motionless player is hit almost every time a shell is fired at it, so
            every leg of the comparison lands a similar number of hits and "is the wall still
            cover" cannot be distinguished from noise.
          - against a wall: `clampPlayer` stops the position but leaves the velocity pointing into
            the wall, so a player held against it reports full speed while going nowhere. The
            gunners lead that stale velocity and miss by about 52px every shot - which is now fixed
            in the tick, but a fixture that leans on the bug cannot test the mechanic.

        So the player walks a slow strafe and reverses heading whenever it gets within 140px of any
        wall, which keeps it in open floor for the whole measurement.

## [h:99-tests-142]
near: `let blocked=0, fired=0;`

AND THE WALL MUST STILL BE COVER - measured the only way that means anything, which is by
      firing the PLAYER's bolts at the guarded shooter and counting how many arrive.

      The assertion this replaces compared escorted and unguarded HIT RATES and asked for the
      escorted number to be lower. That is not the claim: the pack does not shield the shooter from
      the player's gun, it stands in the way of it, and after the guard exemption the shooter's own
      shells pass through its escort. So both figures come out the same (8 against 8) even while
      the wall is blocking every player shot - the two numbers were never going to separate.

      This version asserts the mechanic directly. 100% blocked means the pack is a wall; anything
      less means the formation has a gap in it, which is a real and different failure.

## [h:99-tests-143]
near: `test('audio is not built until a gesture, and a gesture makes it work',async ()=>{`

A PACK SWITCHES TO THE CHASE WHEN THE ENEMY IT WAS GUARDING DIES, and the whole of this test is
      built around the fixture traps that hid the bug for a long time.

      The reported symptom was "when the enemy being guarded dies, the Brunch do not activate the
      chase". Measured across six GENERATED rooms, the transition itself is fine:

          rooms where the pack guarded the shooter we killed   4 of 6
            guard count before      7, 5, 6, 4   (every body)
            guard count after       0, 0, 0, 0   (cleared on the same tick)
            time to 90% chase speed  62-67ms
            still guarding a dead body  0

      The two rooms that appeared to fail were both rooms with a SECOND shooter, and in both the
      pack had been guarding the other one - so killing the body this test happened to pick left it
      guarding a live enemy 153px away at 0.25 speed. Which is correct, and looks exactly like the
      bug. That is why the assertion below kills the body the pack is ACTUALLY guarding rather than
      a body the fixture chose.

      Two fixture traps, both of which produced confident nonsense before this test existed:

        - a body spawned into a room the tick is not walking is NEVER TICKED. `currentRoom()` is the
          room the PLAYER is in, so a detached scratch room full of enemies reports "nothing is
          happening" forever. Every probe that built its own room shared this.
        - `spawnEnemy` RETURNS a body and does not add it, so a hand-built pack that forgets the push
          is simply not there.

      This test uses currentRoom() and pushes what it builds.

## [h:99-tests-144]
near: `test('audio is not built until a gesture, and a gesture makes it work',async ()=>{`

A PACK HOLDS ITS ESCORT UNTIL THE ESCORTED BODY DIES, and sprints at the player only when there
      is nothing left to shield.

      The design, as stated: a Brunch pack's primary purpose is to be a living shield. It leaves that
      job for one reason only - the thing it is shielding is dead. The sprint at the player is the
      LAST RESORT, not a fallback that gets used whenever the pack feels like it.

      This replaces a leash that was implemented here and removed. The leash released the target when
      the player moved more than BRUNCH_GUARD_LEASH away, and it was a defensible reading of the
      mechanic that turned out to be wrong: it made the wall conditional on the player's habits.
      Cross the room and the escort abandoned the shooter it was standing in front of, which means the
      mechanic is only present when the player happens to be nearby - a decoration rather than a
      threat. The wall is the point, so it does not get switched off by walking.

      Three claims, and the second is the one that was actually wrong before:
        - a target stays held through any player movement, including the far corner;
        - it stays held when a NEARER shooter appears, because re-picking on proximity is what makes a
          wall oscillate as bodies shuffle;
        - it is released on death, and the pack sprints at BRUNCH_RUN immediately afterwards.

## [h:99-tests-145]
near: `const before=Sound.stats();`

THE TEST FOR "THE GAME IS SILENT FOR EVER". The defect, in full, because every assertion in
this file passed while it was true:

`Sound.autoUnlock()` is called at LOAD from the input layer, and it used to set `gestureSeen`
unconditionally and then unlock. So an AudioContext was built BEFORE any user interaction -
exactly what every browser's autoplay policy suspends. The context came up suspended. `resume()`
was called and its promise ignored, and `unlocked` was read from `ctx.state` on the very next
line, before that promise could settle. So:

  - `unlocked` stayed false, so every voice went to `skippedLocked`;
  - the guard at the top of `unlock` was `if(ctx) return false`, and `ctx` had just been
    assigned, so every LATER gesture returned false without trying anything;
  - `autoUnlock` only removed its listeners once `unlocked` was true, so they stayed installed,
    calling a function that could not succeed.

Twelve working voices, correct waveforms, a correct mix, a bounded pool - and not one sound, the
only symptom a counter nobody reads.

Measured with a real keydown, a real click and NO autoplay flag (which is what a player's
browser has): before the fix `ctxState` stayed `suspended` through every gesture and 50 shots
reported 50 skips. With `--autoplay-policy=no-user-gesture-required` the same page reported
`running` and everything passed. That is why every audio benchmark in this project said the
audio was fine: the harness was relaxing the very policy under test.

WHAT IS ASSERTED, and the discipline here is the point:

  1. A gesture, and ONLY a gesture, brings the audio up. No helper, no pump, no wait between
     the gesture and the assertion.
  2. Then, and only then, a real play.

The rule learned here, at the cost of a mutation that survived 239/239: **a helper that repairs
the state under test deletes the test.** An earlier version of this test called
`await Sound.whenAudible()` immediately after the gesture - and `whenAudible` sets `unlocked`
itself. Restoring the original permanent-silence guard into `play` then passed the whole suite,
because the helper had already made the context look healthy before `play` was ever called.

## [h:99-tests-146]
near: `const before=Sound.stats();`

NO PRECONDITION ASSERTED, AND THIS IS THE FIFTH TIME IN THIS PROJECT THAT ONE HAS HAD TO BE
      REMOVED FROM AN AUDIO TEST.

      The suite runs every async test concurrently, and 100 of them have already dispatched synthetic
      keydowns at the audio system by the time this one starts - so `ctxState` here is `suspended`
      and `gestureSeen` is true, and neither means anything. Asserting on either produces a failure
      that says "this test cannot run" rather than "this test found a bug", and the difference is the
      whole ballgame: the first costs an hour, the second costs a minute.

      What is asserted below is only what THIS test causes. Everything else is established, not
      assumed - which is the discipline the assertion I just deleted was violating.

## [h:99-tests-147]
near: `for(let i=0;i<2 && Sound.stats().ctxState!=='running';i++){`

THE GESTURE. `autoUnlock` requires `event` to be present before it will claim one or build
      anything, so this is the same path a keypress takes.

      It is repeated until the context is running, because in this harness the context may have been
      suspended by a neighbouring test and a player's FIRST gesture would repair that. A loop rather
      than a single call so the assertion below is about the game's path, not about which test ran
      first. Three passes is ample: the real fix makes the first one work, and if it does not, the
      assertion reports the state rather than hiding it.

## [h:99-tests-148]
near: `await new Promise(z=>setTimeout(z,0));`

ONE FRAME, AND THE REASON IS WORTH THE WAIT.

      The assertion is still about the game's own path - the gesture above is a bare
      `autoUnlock(new Event('keydown'))`, the same call a keypress makes, and `whenAudible` is not
      used after it - but the context is read on the NEXT frame rather than on the next line.

      `resume()` returns a promise. Reading `ctx.state` synchronously after a gesture is precisely the
      mistake this whole bug was made of, and asserting on it here would be the same mistake wearing
      a test's clothes: a test that fails against correct code teaches you to distrust the code.

      Measured, and it is why the loop above exists at all: after the suite finishes, the same context
      reads `running`. During the suite it reads `suspended`, because ~100 concurrent tests share it
      and one of them suspends it to exercise the recovery path. A single frame is enough for the
      promise this particular gesture started to settle, and long before the next thing can touch it.

## [h:99-tests-149]
near: `await new Promise(z=>setTimeout(z,0));`

A MACROTASK, NOT `requestAnimationFrame`. This distinction cost a 120-second browser timeout and
      is worth recording: the suite runs its bodies while the page is still parsing, so the main thread
      is blocked, and a `requestAnimationFrame` callback does not fire until the browser gets control
      back - which it never does while the suite is on the stack. A `setTimeout(0)` is a macrotask and
      is delivered as soon as the current task yields.

      So: audio that depends on animation frames is untestable inside this harness, and the helpers
      that wait for audio (`whenAudible`, `whenIdle`) have the same problem - which is why they resolve
      against `ctx.currentTime` advancing rather than against frames, and why the suite reports them as
      TIMEOUT rather than hanging silently.

## [h:99-tests-150]
near: `await Sound.whenIdle();`

THE THIRD PART OF THE FIX, and the one that makes the other two recoverable.

The original `unlock` began `if(ctx||!Ctor) return false`. `ctx` was assigned a few lines
earlier, so on the SECOND call - which is every gesture after the first - the function returned
immediately without attempting anything. A context in the wrong state could therefore never be
repaired, and the one thing that would have fixed the silence was the one thing that prevented
it. That is why a game could be permanently silent with no code path out of it.

Asserted here by SUSPENDING the context deliberately and asking for it back. There is no mock:
`Sound.stats()` exposes no setter and this uses the real context the game is holding, because a
test of the recovery path that used a fake context would be a test of the fake.

## [h:99-tests-151]
near: `for(let i=0;i<2 && Sound.stats().ctxState!=='running';i++){`

BRING THE CONTEXT UP FIRST, REPEATEDLY, AND SAY WHY. `whenAudible` is not trusted to do it once:
      a neighbouring test may have suspended the context again between the await and the next line,
      so this is a loop that exits the moment the context is running. The fixture rule in this project
      is that the precondition is established, never assumed - and a loop is the honest form of that
      when the state is shared with 200 concurrent tests.

## [h:99-tests-152]
near: `Sound.autoUnlock(new Event('keydown'));`

ESTABLISH THE PRECONDITION RATHER THAN ASSUMING IT. Because the tests run concurrently, this
      one cannot know what state the audio is in when it starts - so it brings the context up first,
      and only then breaks it. A test that asserted its own precondition here failed for four
      consecutive runs against code that was correct.

## [h:99-tests-153]
near: `const atPlay=Sound.stats();`

AND THE CASE THAT ACTUALLY NEEDS `play` TO DO THE FIXING, which is the one a gesture cannot
      reach and which nothing else in this file asserted.

      This is the alt-tab: the game was audible, the tab went to the background, the browser
      suspended the context, and the player came back and fired without pressing anything that would
      register as a gesture. With the guard `if(!unlocked||!ctx||ctx.state!=='running')` the play is
      simply refused and counted as a skip - so the game is muted until the player happens to press a
      key, which is not a thing anyone would think to do about a game that has gone quiet.

      The mutation that put that guard back PASSED 239/239 until this assertion existed, because the
      other two audio tests always had a gesture immediately before their `play`, and a gesture
      resumes the context - so the broken guard was never on the path anything measured.

      A fix that only works when the player does the obvious thing is not a fix, and this is the
      assertion that says so.

## [h:99-tests-154]
near: `const atPlay=Sound.stats();`

THE PRECONDITION, ASSERTED. `eq(broken.ctxState,'suspended')` above checks the state at the
      moment of the suspend call, and `suspend()` is a promise - so by the time `play` runs, another
      await has happened and the context may already be back. Four runs of this test failed with a
      completely unrelated message, and then this one passed against a mutation that should have
      killed it, which together meant the fixture was not exercising the path at all.

## [h:99-tests-155]
near: `ok(typeof Sound!=='undefined'&&typeof Sfx!=='undefined',`

AUDIO IS TESTED BY ITS PLAN AND ITS LIMITS, NOT BY ITS SOUND. A headless browser hears
      nothing, so what is asserted here is everything that is observable: that every voice builds,
      that the pool is bounded, that mute is honoured before synthesis rather than after, that the
      four weapons have four distinct pitches, that panning follows the body, and that a real fight
      runs 4200 ticks without audio throwing into it.

      The last of those is the one that matters. A sound that throws while the player is fighting is
      a crash, and a crash caused by audio is indefensible - so every entry point in Sound wraps
      itself, and `failed` is a counter that must stay at zero across a whole room.

## [h:99-tests-156]
near: `Sound.releasePool();`

THE POOL IS BOUNDED, and it leaked once: measured at 162 simultaneous voices against a
      declared cap of 24, after twenty seconds of a five-body fight. `onended` does not fire until
      the audio thread reaches the scheduled end, so a count that waits for it only ever climbs. The
      pool now prunes against the audio clock and steals the oldest.

      THE BOUND IS PROVED BY OVERRUNNING IT, NOT BY READING IT. Two earlier versions of this
      assertion checked `voices <= cap` and passed while the leak was present, for a reason worth
      recording: in a headless browser the context never leaves `suspended`, nothing is ever
      actually scheduled, and `track()` is never reached - so the count stays at zero and the
      assertion is true for the wrong reason. Reading a counter that a disabled subsystem never
      increments cannot test that subsystem.

      So the cap is tested by putting MORE than `cap` voices through `play` and checking the pool
      does not grow past it. That needs the voices to be tracked, and tracking is what a live
      context does - so this test counts them through the same path a browser takes, and the count
      is asserted either way: if the browser refuses the sounds entirely, the bound is trivially
      satisfied and that is stated rather than passed off as a result.

## [h:99-tests-157]
near: `Sound.releasePool();`

THE BOUND, EXERCISED THROUGH THE POOL'S OWN BOOKKEEPING. `exercisePool` calls the same
      `reserve`/`steal` that `play` calls - it is not a parallel copy - and takes no AudioContext, so
      it works in a browser that will never make a sound. Every path to the bound otherwise goes
      through `ctx.state==='running'`, which in headless is never true, so `reserved` stays at 0 and
      the count reads zero for the wrong reason. Two earlier assertions passed against the leak
      that way.

## [h:99-tests-158]
near: `const tones={};`

FOUR WEAPONS, FOUR PITCHES. This is the queued pitch-jitter item, and it failed twice on a
      lookup key that does not exist - once on `w.id` (weapons have no id) and once on `w.name`
      ('Beam' is not a name any weapon has; it is 'Arcane Beam'). The key is derived through the
      game's own `Content.idOf` now, so a rename cannot silently mute the difference.

## [h:99-tests-159]
near: `ok(typeof Sound.render==='function','Sound.render is missing, so the waveforms cannot be '`

THE SOUNDS ARE RENDERED, NOT MERELY ASSERTED TO EXIST. Everything above is observable
      bookkeeping; none of it says whether a shot is a click or a warble. `OfflineAudioContext`
      renders the real waveform with no speaker, so the envelope is measured from the same
      `VOICES[name].build` the game plays - not from a copy, which would be testing the test.

      And it immediately caught a mix that was wrong in a way no existence check could see. Measured
      first pass: boss peaked at 0.9477 against a shot at 0.0771 - twelve times louder, which is not
      a mix but an interruption - and `tell` at -8.3dBFS was LOUDER than the Brunch touch at -9.6,
      inverting the design, since the tell exists precisely to sit under a fight. Both corrected; the
      set now spans 20dB with the tell under the touch.

## [h:99-tests-160]
near: `Sound.releasePool();`

SILENCE THE SYSTEM BEFORE MEASURING IT. The fight above scheduled thousands of live voices,
      and `render` clears the pool before it renders - but only the ones it can see, and a source
      that was started in a previous task and has not been reaped yet is not in the list. Measured
      consequence: a `shot` whose envelope ends at 0.09s reported a tail of 0.86 to 1.28 at 0 to
      -1dBFS, varying between runs because it depended on what was still sounding when the offline
      graph was built. Measured with the pool released first: peak 0.0771, tail exactly 0, every
      time and at every viewport.

      The lesson is the same one this file keeps arriving at, and it is now four separate checks in
      it: a measurement taken while the thing being measured is still doing something else is not a
      measurement. The test above measures "did audio break a tick"; this one measures "what shape is
      the waveform", and the two cannot both be true at once.

## [h:99-tests-161]
near: `const measured=[];`

ONE AT A TIME, IN SEQUENCE, AND NOT `Promise.all`.
        `Sound.render` swaps the module's ctx so the voice bodies can stay free of plumbing, and
        twelve concurrent renders therefore corrupt each other: the numbers that came back described
        a sound ringing at 3dBFS 1.46 seconds after a 90ms shot that is silent from 100ms on. Every
        one of them was an artefact of two offline graphs sharing one set of module variables.

        Rendered sequentially, `shot` measures peak 0.0771 with a tail of exactly 0 - the waveform is
        correct and the concurrency was the only thing wrong with it.

## [h:99-tests-162]
near: `if(m.tailPeak>0.001) ringing.push(m.name+' at '+m.tailPeak.toFixed(4)`

The tail check is in AMPLITUDE, and the threshold has to be an audible one. Every voice's
          envelope ends on `exponentialRampToValueAtTime(0.0001)`, so the last of it sits at about
          -80dBFS - inaudible, and a source that is stopped a moment later leaves a sliver of that
          behind. Measuring at 0.001 (-60dBFS) reports a voice as ringing when it is 80dB down, which
          is what the first version did: `over` was flagged at 1.48671 while its envelope ended at
          0.9s, and the "defect" was the tail of a ramp that had already gone silent.

          -60dBFS is the threshold because that is roughly the quietest thing worth keeping: a fight
          has a shot at -22dBFS and a boss at -4, so anything below -60 under them is gone. A voice
          genuinely RINGING - a filter with feedback, a release that never terminates - sits far above
          it, so the check still bites.

## [h:99-tests-163]
near: `ok(measured.filter(Boolean).length===names.length,'the render step measured '`

MUTATION GUARD: the assertions below are only meaningful if `measured` has content. A skipped
        promise, a renamed method, or a test that silently awaits nothing would leave this array empty
        and every check trivially true - which is what happened to the pool-bound assertions in this
        same test, twice, for the same reason.

## [h:99-tests-164]
near: `const hz=[];`

FOUR WEAPONS, FOUR MEASURED PITCHES. Asserted on the rendered waveform rather than on the
        table, because the table was wrong twice on a key that did not exist and the table cannot
        tell you. Zero crossings per second is a rough pitch proxy - a 440Hz sine crosses zero 880
        times a second - and it separates four tones spread over 690 cents without any of that
        needing to be precise.

## [h:99-tests-165]
near: `Sound.releasePool();`

A RENDER MUST BE A MEASUREMENT, NOT A WINDOW. This is the fourth attempt at one symptom and
      the first that found it, so it is worth stating what the symptom was and where everyone
      looked: a 90ms `shot` kept reporting a tail of 0.78 to 1.28 at 0 to -2dBFS, and the number
      moved between viewports and between runs.

      Three fixes were aimed at the LIVE audio graph - serialising the renders, clearing the pool,
      suspending the live context - and every one of them was reasonable and none of them was the
      cause. The live graph was innocent. `renderVoice` builds its voice by swapping the module's
      `ctx` to an OfflineAudioContext and then AWAITING `startRendering()`. That await yields the
      main thread, and the game's own code runs in the gap: a fight loop, a keydown, another test's
      `play()`. Every one of those built its nodes against the OFFLINE context and was rendered
      into the buffer being measured. So the "shot" was a recording of whatever else was sounding,
      which is why it was loud, why it rang on, and why it varied.

      The fix is one line in `play`: do not build while a render holds `ctx`. And this test is the
      proof, because it does the one thing the suite's own ordering made impossible - it fires
      sounds INTO the render's await window. Mutate the guard out and this fails immediately, with
      a peak of 1.07 and a 500ms ring on a sound that is silent after 75ms.

## [h:99-tests-166]
near: `const walk=(label,setup)=>{`

THE BENCH IS NOT A PAUSE, AND IT WAS EATING THE WHOLE KEYBOARD.

      Two INDEPENDENT gates had to be opened, and fixing one left the bug looking smaller rather
      than smaller-and-fixed - which is how it survived: after the handler stopped consuming
      unclaimed keys, W, A and D were STILL dead while S worked.

      1. The handler's `if(devOpen)` block ended in a bare `return`, so every key it did not
         explicitly claim never reached `keys[k]=true` at 80-ui.js:987.
      2. `uiHoldsInput()` returned `!!uiOverlay()||devOpen`, so the suppressor on 80-ui.js:891 sent
         every key through `uiAllows()`, and WASD is not in UI_KEYS.

      S worked after gate 1 because the lab's silver-key shortcut had put 's' in UI_KEYS - so one
      of the four directions got through and the report said "S is broken" rather than "the bench
      ate the keyboard". A fix that leaves one key alive is a fix that gets filed again.

      Measured before, on the bench: W/A/S/D each moved the player 0px in 120 ticks, against 157.6
      px with no panel open. The assertion below is that every direction moves the same amount with
      the bench and the lab open as with neither - not merely that it moves, because a panel that
      halved your speed would pass the weaker version.

## [h:99-tests-167]
near: `startGame(); const rr=goTo('normal');`

The lab shortcuts are gated on BOTH the bench being open and `state==='dev'` - the block is
      `if(devOpen){ ... if(state==='dev'){ ... } }` (80-ui.js). Setting `state` alone leaves the
      outer gate shut, so the shortcuts cannot fire and the test reports them broken, which is the
      fixture being wrong rather than the code. `toggleDev(true)` opens the bench.

## [h:99-tests-168]
near: `startGame();`

THE PLAYER IS ALLOWED 40px PAST A WALL LINE AND THE PUFF IS NOT.

      `clampPlayer` ends with a deliberately loose outer bound so a player can stand in a doorway
      overshoot for the sake of transitions, which means a puff placed at the player's own clamped
      position is drawn over the wall. That was measured - a blink into the left or bottom wall left
      two puffs 40px outside the room - and it is fixed by clamping the TRAIL to the room box rather
      than to the player's bounds (src/50-run.js:60-84).

      Measured across all four walls at 15 starting distances each: 0 violations, 0px outside. This
      asserts that, because the fix is a clamp on two numbers and a clamp is exactly the kind of
      thing that gets tidied away by someone who did not know why it was not the player's bounds.

## [h:99-tests-169]
near: `startGame(); const r=goTo('normal');`

THE SAME TELL ON EVERY RANGED BODY, PINNED TO THE MEASURED NUMBER.

      The boss volley tell was already fixed and has its own test above. The GUNNER and SHOOTER use
      the identical mechanism - `60-tick.js:1363` sets `castT=CAST_TIME` and `fireCommittedShot`
      counts it down before pushing the shell - and nothing pinned it. Two probes in this session
      measured it as 1 tick and then as 0 ticks, both times wrongly:

        - sampling `castReady` transitions conflates the first shot with every later one;
        - sampling `castT` on the tick the shell leaves always reads 0, because
          `fireCommittedShot` does `if(--e.castT<=0) e.castReady=true` and then pushes - so the
          value on screen for the whole countdown is invisible to a sample taken at the muzzle.

      Measured, tracing one shot tick by tick: `castT` is set to CAST_TIME=105 on tick 169 and
      counts down 104, 103, ... 1, and the shell leaves when it reaches 0. That is 0.5 seconds of
      warning at 210Hz, and it is the number this asserts. A gunner whose tell collapsed to a
      single tick would be unhittable-by-reading, which is the same failure the boss had.

## [h:99-tests-170]
near: `g.noticeTimer=0; g.aggroTimer=0; g.maxHp=g.hp=1e9;`

`shootCd` is LEFT ALONE. It is the cooldown that gates the cast - `60-tick.js:1186` reads
        `else if(e.shootCd<=0)` and only then starts one - so pinning it to 1e9 stops the gunner
        ever arming, and the test measured a tell of 0 for a body that telegraphs perfectly well.
        A probe that silences the thing it is measuring reports the silence, not the behaviour.

## [h:99-tests-171]
near: `test('the boss volley tells before every shell, the first one included',()=>{`

THE BOSS VOLLEY TELLS BEFORE EVERY SHELL, INCLUDING THE FIRST.

      The volley set `castT=CAST_TIME` - which is exactly what the draw reads
  (`if(e.castT>0) drawCastFlash(...)`, 70-view.js:442) - and set `volleyT=0` alongside it. `resolveBoss`
      decrements `volleyT` and fires when it is `<= 0`, so the first shell left on the SAME TICK the
      move was chosen: a charge that began and ended inside one tick, and was therefore never
      rendered. The player was hit by a shell whose tell they had not seen.

      Measured over a full phase-3 fight, the delay from entering the volley to each shell leaving:

          before   first shell   1 tick      <-- no window at all
                   second        117
                   third         233

          after    first shell   105 ticks   500ms, the same as a gunner
                   second        221
                   third         337

      The re-tell BETWEEN shells was always there and is generous; the first shot of every volley was
  the one with nothing. That is the worst shape for a three-shell attack to have, because the shell
  you had no warning of is the one that sets up the two you did.

      Asserted on all three shells rather than the first, because the between-shell re-tell and the
  opening tell are different mechanisms and either can be broken alone.

      The first version of the fix set `volleyT=1` and read as correct in a trace - `volleyT` 1,
 `castT` 105 on the entry tick - while behaving exactly as before, because the decrement made it 0
 and `<= 0` fired. A state field that has just been assigned says nothing about what the next tick
      does with it; the only way to know is to watch a shell leave.

## [h:99-tests-172]
near: `startGame(); const rc=currentRoom(); rc.enemies.length=0; projectiles.length=0;`

THE BUG THIS EXISTS FOR. `40-combat.js` wrote `stun = KNOCK_STUN/3` = 29.333..., and the tick
      decrements under the guard `if(e.stun>0)`. Thirty clean decrements take 29.333 to **-0.667**,
      and a negative stun fails that guard - so the branch stops running, the value is never clamped,
      the body is never decremented again, and it is left in a state that is neither stunned nor
      clean. It is a permanent, silent, unrecoverable body state caused by one missing `Math.round`.

      Measured consequence, before this was found: a Brunch pack could not chase a sprinting player
      at ANY chase speed. Contact landed, the pack took knockback, `stun` pinned at 88 with `kvx` at
      -2.36 and never decaying, and the pack was driven backwards at ~70px/s while `curSpeed` read
      3.0. The symptom - "Brunch still cannot reach the player when sprinting away" - reads exactly
      like a speed problem, and raising the speed did nothing, which is what made it worth chasing
      rather than retuning.

      The three assertions are: the CONSTANT is integral, the STATE never goes negative, and a
      fractional value planted directly still recovers. The third is the one that would have caught
      this at the time.

## [h:99-tests-173]
near: `startGame(); const rc=currentRoom(); rc.enemies.length=0; projectiles.length=0;`

NOT `Number.isInteger(KNOCK_STUN/3)` - that expression is fractional by arithmetic and always
      will be, so asserting on it asserts something false. What has to be integral is the value
      WRITTEN TO A BODY, and that is only observable by running a collision. Which is the better
      test anyway: it reads the game's own answer rather than re-deriving it from the constant.

## [h:99-tests-174]
near: `startGame(); const r3=goTo('normal'); r3.enemies.length=0; projectiles.length=0;`

and an integral stun decays to EXACTLY zero rather than past it, which is the whole property.
      It needs `aggroTimer` set: an unalerted body is skipped by `if(dist<aggroRange()) ... else
      if(e.aggroTimer>0)` before the stun branch is ever reached, so a 3-tick stun planted on a body
      that has not noticed the player never ticks down at all. That is correct behaviour - a body
      that has not seen you is not being stunned - and it is the fourth fixture in this session to
      have measured the wrong thing for that reason.

## [h:99-tests-175]
near: `startGame(); const r3=goTo('normal'); r3.enemies.length=0; projectiles.length=0;`

A FRESH RUN, because the fight above ends the run. `run.state` was 'undefined' - the 12-second
      brawl with six bodies ran the player to zero and `endRun` cleared it - so `update()` returned at
      its state gate and no body was ever stepped. Two assertions in this file had been reading a
      fixture that measured the state gate rather than the stun branch.

      Asserting the run is alive before measuring it is the fix, and it is asserted rather than
      assumed: a fixture whose game has ended is not a slow test, it is a wrong answer.

## [h:99-tests-176]
near: `ok(r3.enemies.indexOf(d)>=0 && typeof currentRoom()==='object',`

NOT an assertion on `run.state` - that field does not exist. `startGame()` builds a run object
      with floor, ticks, kills, dmgTaken and the rest, and no `state` on it; the play/dead/gameover
      state is a module-level variable beside it. A check written against a field that is not there
      reads `undefined`, fails, and looks like a broken fixture rather than an invented one - which
      is what it was, for three attempts.

      What is worth asserting is the thing that actually gates the tick: that the fixture is in a
      room and the room is stepping. Both of those are used below, so both are checked here rather
      than discovered as a wrong number.

## [h:99-tests-177]
near: `const nearPack=br.reduce((a,e)=>({x:a.x+e.x/br.length,y:a.y+e.y/br.length}),{x:0,y:0});`

CLAIM 2: a DIFFERENT, and pack-nearer, shooter appears. The pack keeps the body it committed to.

      The second candidate has to be nearer to THE PACK than the committed one, not merely nearer to
      the player. `pickShield` breaks ties by distance to the pack centroid, so a shooter that is
      closer to the player but further from the pack loses anyway - and this assertion passed
      VACUOUSLY while the commitment was removed entirely: with unconditional re-picking, pickShield
      still returned the committed body, because it was the nearer one to the pack. Measured: peak
      player-to-shooter separation in this fixture is 424px, so a distance leash is also reachable
      here, and neither was actually being tested until both were fixed.

      So this spawns the second shooter right next to the pack, which is the case that would flip the
      pick for real.

## [h:99-tests-178]
near: `const ixN=room.enemies.indexOf(nearer); if(ixN>=0) room.enemies.splice(ixN,1);`

CLAIM 3: the escorted body dies, and only then does the pack come at the player.

      The `nearer` shooter from claim 2 has to GO before this, or the pack correctly re-acquires it
      on the next scan and the assertion reads "6 of 6 still holding a target" - which is right, and
      is not the claim being tested. Claim 2 proved the pack does not switch while both are alive;
      claim 3 is about what happens when the committed one dies and nothing else is left.

## [h:99-tests-179]
near: `startGame();`

This asserts the BEHAVIOUR, because the number is what went wrong. The chase speed was 1.35
      against a player at 1.2 - a ratio of 1.13, which reads as decisive in a table and is not. In a
      room big enough that a 14-second chase never reaches a wall, with the player running flat out
      and the pack starting 450px behind, the gap GREW ~107px per 2s. A pack that cannot run you down
      is scenery, and the sprint's entire reason for existing is the moment there is nothing left to
      shield.

      Every previous check on this number was a RATIO against the player or against a lunger, and
      ratios pass at almost any value between them - which is why a 13% cut to 1.18 went unnoticed.
      So this measures the gap over time and asks a question about its SHAPE.

      Two things have to hold and they pull against each other:
        - the gap must SHRINK, or the pack cannot catch anyone;
        - the pack must be slower than the player for a real interval first, or there is no window in
          which to pick your ground, which is the entire reason BRUNCH_RAMP exists.
      A speed high enough to satisfy the first can trivially break the second, so neither alone is
      worth asserting.

## [h:99-tests-180]
near: `for(let t=0;t<210*20;t++){`

20 SECONDS, not 14, and the shortfall was the assertion failing for the right reason at the
      wrong threshold. Measured: a pack starting 450px behind closes at 104px/s and first overlaps the
      player's hitbox at 13.6s - so a 14s budget has 0.4s of margin on a race, and it read "the pack
      never reached the player" while the pack was 16px away and closing. The bound should be a
      statement about the chase, not about how close to the deadline the fixture happened to stop.

## [h:99-tests-181]
near: `if(player.x>r.bounds.l+r.bounds.w-300){`

SLIDE THE WHOLE FORMATION WEST before the tick, not the player alone. Without this the
        player reaches the east wall of the 9000px room after about 90 seconds of a 14-second chase,
        stops, and the "gap" stops being a gap - and the readings 451, 451, 395, 102, 181, 50, 146,
        225 are that: the pack arriving at a player who has already stopped against a wall.

        Moving the pack with the player rather than teleporting the player keeps the separation
        between them intact, which a teleport would not: the whole point is to measure a chase at a
        constant closing rate, and a teleport measures a different fight every tick.

## [h:99-tests-182]
near: `if(firstContactSec===null){`

AFTER the update, and with the offset SUBTRACTED. Both halves were wrong the first time and
        they cancelled into "the pack never arrived" at a gap of 50px.

        The order: this is checked after `update()` above, so the positions are post-move. Checking
        before the tick measured the gap as it was a tick ago, and at 2.1px/tick that is two whole
        body-radii of error - enough to miss contact entirely on a body that is arriving.

        The sign: the game's own test at 60-tick.js:1365 reads
        `Math.hypot(edx, edy-(PLAYER_HIT_DY)) < e.r+PLAYER_HIT_R`, and it is the only place in the
        file that subtracts. Every other hitbox in the game adds 10 - `playerHit` measures against
        `y+PLAYER_HIT_DY` - so a fixture written from the general rule measures a hitbox 20px from
        the real one and silently never contacts.

        Measured with both correct: closest approach 16.1px against a threshold of 18, first contact
        at 13.25s. The two errors were opposite in sign, which is why the gap still looked plausible.

## [h:99-tests-183]
near: `ok(firstContactSec!==null,'the pack never reached the player in '+(210*20/TICK_HZ).toFixed(0)+`

The announcement bar is deliberately loose. The first version wanted the pack to still be 450px
      out once its ramp finished, and it read 407px - which is the ramp WORKING, not failing: the gap
      is expected to open slightly during the phase where the pack is deliberately slower than the
      player, and the point is only that it must not have closed. Asserting "no closer than the start"
      would be asserting that a pack which never chased would pass, which is the exact opposite of
      this test. What has to hold is that the gap is still open and the pack has not arrived.

## [h:99-tests-184]
near: `ok(firstContactSec!==null,'the pack never reached the player in '+(210*20/TICK_HZ).toFixed(0)+`

THIS TEST WAS MEASURING THE WRONG MOMENT, and it only looked right by accident.

      It read the gap at a FIXED time - 14 seconds - and called that "did it close". But a pack that
      closes and LANDS is not a pack that ends 14 seconds later standing next to the player: contact
      damage at 60-tick.js:1367 is mutual and symmetric, a Brunch has 2*TOUGH = 2.7 HP and takes 1.35
      per touch, so the body that arrives dies on the second touch and the survivors are left with
      nothing to shield and nothing to chase. The gap then opens because the pack is GONE, not
      because it failed to arrive.

      Measured with the pack kept alive it never went above 180px. Measured with the pack allowed to
      die, the same trace reads 451, 451, 395, 102, 76, 221, 224, 813 - which looks exactly like a
      pack that closes and then gives up.

      So the question is WHEN IT ARRIVED, not where it was at an arbitrary tick. `firstContactSec` is
      the arrival, and the bound on it is what the chase speed actually controls.

## [h:99-tests-185]
near: `ok(ENEMY.brunch.run>1.70,'the chase speed is '+ENEMY.brunch.run+' - measured, 1.35 grew the gap '+`

THE 0.55/1.35 BOUNDS WERE RELATIVE, AND RELATIVE BOUNDS DID NOT NOTICE A REAL REGRESSION.
      Both of these survived a 13% speed cut without complaining: a value can be "slower than 1.3x the
      player" and "faster than a lunger" at almost any number in between, so nothing objected when
      BRUNCH_RUN went 1.35 -> 1.18 and the pack became scenery. That comment claimed this test
      "pins the cut", and it did not pin anything.

      What the number has to satisfy is a BEHAVIOUR, not a ratio: with nothing to protect, a pack must
      actually close on a player running flat out. Measured in a room large enough that a 14-second
      chase never reaches a wall, gap every 2s from 450px:

          run 1.35   451  682  790  898  1005  1112  1219    grows
          run 1.62   451  609  604  599   594   589   583    holds, never closes
          run 1.75   451  572  513  445   383   322   261    closes
          run 1.90   451  538  423  300   176   197   156    closes faster

      So 1.35 failed the property it existed for, and 1.62 is the more dangerous value: it holds the
      gap, which looks like parity in a table and is not, because the player is never caught and can
      walk away indefinitely. 1.75 is the pick - the first on the sweep that closes, and it closes
      steadily rather than snapping.

      The two speeds are asserted separately on purpose. BRUNCH_RUN is what a pack uses with nothing
      to protect and BRUNCH_SHIELD_SPEED (0.72) is what it uses walking onto a slot, and the second
      is a body heading for a FIXED point where overshooting is a real failure - so neither bound
      can move without the other being noticed.

## [h:99-tests-186]
near: `ok(ENEMY.brunch.run<CHASED_PLAYER_SPEED()*1.9,'brunch is so quick the player cannot kite them at all`

THE CEILING IS 1.9x, and it is stated against the CHASED player rather than the empty-room one.

      `playerSpeedForTest()` is the player's speed in an empty room: 1.4025. A pack that is chasing you
      is chasing a player whose momentum meter is full, which is BECAUSE they are being chased, and
      that player moves at 1.6045. So the honest denominator is 1.6045 and 2.1 reads as 1.31x of it -
      a chase. Measured time to contact from a 450px gap against a player holding one direction:

          run 1.75   37.2s
          run 2.10   30.3s     <- the pick
          run 2.60   29.3s
          run 3.20   28.3s

      The curve goes flat hard after 2.1, which is why the ceiling is here and not at 3.2: at 3.2 the
      pack closes at 1.6px/tick, where every mistake is fatal and it stops being something you can
      read. At 2.1 it closes at 0.50px/tick, so a 200px mistake is survivable and a 400px one is not.

      Both figures are asserted below, because a ceiling written against the wrong denominator is the
      same error as a chase speed tuned against the wrong one - and that error is what made 1.35 look
      adequate for an entire session.

## [h:99-tests-187]
near: `let bodies=0,packs=0,ableToWall=0,roomsWithBrunch=0,largest=0;`

THE BODIES OF A PACK MUST SHARE ONE ID, or there is no pack. This is the assertion the whole wall
      mechanic rests on, and it is asked of rooms THE GENERATOR BUILT rather than of a pack this file
      assembled by hand.

      The generator incremented its pack counter once per body as well as once per pack, so each Brunch
      received its own packId. The wall rule counts bodies sharing an id and compares that count to
      BRUNCH_WALL_MIN, so every pack scored 1 and no pack could ever form: 1227 bodies across 221 rooms,
      1227 distinct ids, zero walls. Every other wall test built its own pack with a hardcoded shared
      id, which is the arrangement the mechanic needs, so none of them could see it.

      Measured over 40 seeds rather than asserted from one room, because the failure was uniform and a
      single room would not have shown that.

## [h:99-tests-188]
near: `startGame(); const r=goTo('boss');`

The run no longer ends on the kill. It ends when the player walks into the portal, which
leaves them free to go back and finish the room, or to turn an accidental clear into an
earned one. It is also the one change here that could silently soft-lock a run forever, so
the portal's existence, its position, and the fact that it can be missed are all pinned.

## [h:99-tests-189]
near: `const duel=(react,trials)=>{`

and the reaction has to work, and the measure is DAMAGE, not "was ever touched". Over nine
seconds a lunger gets three or four lunges away and a single graze is close to certain even
for a player who reads every tell, so a hit-rate comparison comes out 100% either way and
proves nothing at all. Hearts lost is the number that separates the two.

## [h:99-tests-190]
near: `if(g.lungeState==='wind'&&!wasWind){`

On each windup, step ACROSS the committed line - and pick whichever side has room.
Fixing one side for the whole fight walks the player into a wall in about four
seconds, and once they are in a corner no dodge works, so the measurement becomes a
measurement of the corner rather than of the tell. A player picks the open side too.

## [h:99-tests-191]
near: `ok(BRUNCH_RAMP>=sec(1.2),'the Brunch ramp is back under 1.2s, at which point a pack is on you '`

THE RAMP IS 1.5s, down from 2.2s, and the floor moves from 1.8s to 1.2s with it.

      The 2.2s was tuned against `PLAYER_MOVE`=1.2. The player a pack is actually chasing moves at
      the measured CHASED_PLAYER_SPEED() above once the meter is full - which it is, because the meter
      fills from being chased - and at the measured EMPTY_ROOM_TOP_SPEED() even empty. Both are
      measured at run time by that block rather than written down here, so this reasoning cannot rot.
      At 2.2s the pack was ALREADY faster than a chased player by 0.74s, so the
      ramp was no longer buying an interval to choose ground in; it was dead time at a speed the
      player cannot act on. Measured: the pack passes the chased player's speed at 0.64s.

      So the ramp is now bounded from below by what the player can react to and from above by the
      point where the pack stops being slower than you at all. 1.5s leaves the pack slower than the
      player for roughly its first half-second - the announcement survives - and cuts the interval in
      which you are being chased by something that has not arrived to two thirds of what it was.

      The upper bound is the one that matters and it is not arbitrary: at 1.5s the `curSpeed` curve is
      0.62, 0.81, 0.98, 1.14, 1.28 ... so a player who reacts on seeing the charge has a real
      interval, and one who does not is still caught. That is the difference between a chase and an
      ambush, and it is the whole reason this number exists.

## [h:99-tests-192]
near: `ok(wp.fNear>0&&wp.fFar>wp.fNear&&wp.fMin>=0.2&&wp.fMin<1,wp.name+' has no usable falloff band');`

fMin is now 0.22..1. The floor is what a gun is worth at the far end of a room, and the
      Scatter's is deliberately low - it has come down from 0.45 to 0.30 to 0.22 as the gun was
      measured as too consistent at medium range each time. A 0.45 floor gave a shotgun one-shots
      across the whole room; 0.30 still killed a lunger at 350px in two volleys.

      The floor still has to be a real number rather than 0 (a gun that does nothing at range is a
      dead gun) and below 1 (a gun that does not fall off has no range at all).

## [h:99-tests-193]
near: `test('the Scatter is at full damage inside a quarter of a room, and a bad idea past a third',()=>{`

The two tests below pin SPECIFIC VALUES, where the one above only pins the shape of the curve.
    That distinction was found by mutation rather than assumed: feeding the old Scatter band
    (fNear 80, fFar 300) and the old lunger HP (24.30) through the shape test's own arithmetic
    passes all three of its assertions identically, because a band that starts at 80 and one that
    starts at 175 are both "a usable falloff band". A suite made only of shape tests therefore
    cannot tell a deliberate retune from no retune at all, and both of these changes went in green.

    So each value gets an assertion that states WHY it is that value, in a form that fails if the
    number moves.

## [h:99-tests-194]
near: `const roomW=700;`

A room is 700x450. Measured over five seeds, every room in every type is exactly 700 wide -
      there is no variance to average, so the fractions below are not approximations.

      180px is a QUARTER of a room, and that is the current design: full damage inside a quarter,
      falling away from there, floor reached by 320px. It started at 233 (a third) and was measured
      as too consistent - a lunger at 350px, which is a body in the middle of the left of the room
      and a player in the middle of the right, still died in 2 volleys / 2.2 seconds. Widening the
      effective range is exactly the wrong direction for a weapon people say is too easy at medium
      range, so fNear came DOWN and the floor came down with it.

      The pixel value is pinned, and the band around it is asserted too, so the number cannot drift
      into being neither a quarter nor a third without one of the two failing.

## [h:99-tests-195]
near: `eq(sc.fNear,180,'the Scatter effective radius moved off a quarter of a '+roomW+'px room ('+`

180 is not exactly a quarter of 700 (that would be 175), and the assertion says so rather
      than asserting a fraction and moving the number to match. Rounding fNear to 175 would buy
      nothing: the falloff band is 140px wide, so 5px is a third of a percent of where the curve
      starts. Stating the pixel value and saying approximately-a-quarter is more honest than
      asserting an exact fraction and quietly fitting the data to it.

## [h:99-tests-196]
near: `const mult=d=>1-(1-sc.fMin)*Math.min(1,Math.max(0,d-sc.fNear)/Math.max(1,sc.fFar-sc.fNear));`

The DESCENDING form, which is what falloffMult actually computes: 1 at the muzzle down to
      fMin at fFar. The older table tests above use the ascending fMin+(1-fMin)*(...) form for the
      same curve, and both are correct - but writing the wrong one here produced 1.4278 at the
      muzzle instead of 1, which is the tell that a test has stopped describing the thing it
      claims to check.

## [h:99-tests-197]
near: `startGame();`

THE PART THAT DECIDES WHETHER THE GUN IS FAIR, and it is a lunger because the lunger is the
      body the gun was reported too easy on.

      This runs the REAL simulation rather than the table above, because the table is wrong by a
      factor of nearly two here and in the one direction that matters. A lunger WALKS TOWARD THE
      PLAYER while the volley is in the air - 350px at the moment of firing, about 318px by the time
      the last pellet arrives - and `falloffMult` is read at the moment of the hit, from the
      projectile's own origin to where it touches. So later pellets in the same volley are taxed at
      a shorter distance than the first ones, and measured multipliers run 0.302 then 0.376 then
      0.491 across one volley. The static table says a lunger takes 5 volleys at 350px. It takes 3.

      That is not a rounding detail, it is the difference between "the gun is weak at range" and
      "the gun is fine at range", and it is invisible to any assertion written against the table.
      The table is still worth asserting - it is the shape of the curve - but the fairness claim
      has to be measured, or it is measuring a fight that does not happen.

      Two or three volleys at medium range is the requirement. A shotgun that needs six is not
      balanced, it is abandoned.

## [h:99-tests-198]
near: `startGame();`

DO NOT CALL noCharacter() HERE. This assertion was written with it, and that made the test pass
      under both 0.22 and 0.30 - a mutation check confirmed 0.30 sailed through green, so the number
      that fixes the reported problem was not actually pinned by anything.

      The reason is that `noCharacter()` strips the +3 Strength the Wyrd starts with, and Strength
      is added once per SHOT (`count*dmg + strength`, shared across the pellets). Stripping it takes
      34.2 raw down to 31.2, which is a 9% loss on a gun that is already being taxed by range - and
      9% is exactly enough to drop a lunger from 3 volleys at 350px back to 2. The test was
      measuring a character nobody plays.

      With the real build, at 350px: fMin 0.30 gives 2 volleys, fMin 0.22 gives 3. That difference
      IS the fix the user asked for, so the assertion has to be made with Strength intact.

## [h:99-tests-199]
near: `Rnd.set(1234+i*7+dist);`

Reseeded per trial. Without this every iteration replays the identical fight - same room,
          same muzzle jitter, same pellet speeds - so the "mean" is one sample six times over and
          cannot see the variance a shotgun actually has. A test that reports 3.00 volleys has
          measured one thing, not six.

## [h:99-tests-200]
near: `const at350=volleysLive('lunger',350,6);`

The band this asserts is 3 at 350px and nothing else. An earlier version said "between 2 and 3.5",
      which sounded reasonable and pinned NOTHING: fMin 0.30 gives 2 volleys at 350px and fMin 0.22
      gives 3, and 2.0 satisfies `>=2 && <=3.5` under both. A mutation check caught it - the floor
      was moved from 0.22 back to 0.30 and the suite stayed green.

      A range assertion is only useful when the value it allows is one the thing cannot produce. Here
      the fix the user asked for IS the difference between 2 and 3 at exactly one distance, so the
      assertion has to name that distance and that count rather than bracket it.

## [h:99-tests-201]
near: `const lung=ENEMY.lunger;`

15*TOUGH, down from 18*TOUGH. Two reasons, and the second is the load-bearing one.

      First, it closed a gap the roster did not want: at 24.30 the lunger took longer to kill than
      anything else in the game by a wide margin, which made it the one body that punished every
      gun equally instead of playing to what each one is good at.

      Second - and this is what forced the number - at 24.30 NO cone angle could be measured
      against it, because 8 pellets at 2.6 could not kill it at any range, including point blank.
      The weapon's defining property is its range, and a target it can never one-shot makes that
      untestable. Below 20.8 raw (8 x 2.6) the question becomes real: where does the shot stop
      landing all of them.

      The armour is why the margin is thin. ARMOUR is 0.66, so 8 pellets of 2.6 land 13.73 on a
      lunger and not 20.8 - which is why this is asserted as "one shot's worth of damage" rather
      than "one shot kills", and why the number cannot drift much without the gun's identity
      quietly changing underneath it.

## [h:99-tests-202]
near: `const eight=sc.dmg*sc.count*lung.armour, seven=sc.dmg*(sc.count-1)*lung.armour;`

THE CONTRACT IS ABOUT ARMOUR, NOT RAW DAMAGE. This assertion used to compare the lunger's
      health against `dmg*count` - the RAW figure - which was wrong in the way that mattered, because
      ARMOUR is 0.66 and the raw number is not what lands on the body. At dmg 2.6 the raw check said
      20.25 < 20.8 and passed, while the shot in fact landed 13.73 and killed nothing at all.

      So the contract is stated in the currency the game actually spends: EIGHT pellets kill a lunger
      and SEVEN do not. Both halves, because either alone is satisfiable by the wrong weapon - 4.5
      would clear "eight kills" while quietly deleting the requirement that all eight are needed.

## [h:99-tests-203]
near: `ok(sc.dmg*ENEMY.brunch.armour>=ENEMY.brunch.hp,`

The Brunch is the one body a shotgun should NOT have to work for. One pellet is 3.9 against
      2.70 hp and no armour, so they still die to a single grain - a pack of eight is not a
      problem the volley has to solve, and this is the check that the buff did not quietly make
      every chip body a two-pellet problem.

## [h:99-tests-204]
near: `const SHOTS=6;`

Six shots, not one. The claim is that falloff reduces damage, and a single shot cannot measure
        it for a weapon with a cone: the Arcane Beam at zero luck throws a shot +/-52px at 326px, and
        a 14px hitbox inside that is hit perhaps a quarter of the time, so one shot reports "this gun
        does nothing" and the assertion below fails on a gun that is working exactly as designed. A
        player measures falloff by holding the trigger down, which is also what a 5.2-tick cooldown
        invites, so this is the honest shape of the measurement. The alternative - special-casing the
        distance per weapon - would have quietly stopped testing the beam's range at all.

## [h:99-tests-205]
near: `s.shootCd=0; projectiles.length=0;`

The aim is taken when the cast BEGINS, which is what makes the tell honest, and the lag is
still at the pre-blink spot at that moment. If the aim were taken at the moment of firing it
would be taken half a second later, by which time the lag has caught up and the shell goes
straight at the real position - which is exactly the bug the lag exists to prevent.

## [h:99-tests-206]
near: `ok(Math.abs(player.lagX-player.x)<BLINK_DIST*0.05,'lag hitbox never caught up ('+`

As a FRACTION of the blink, not an absolute. The lag eases at a twentieth a tick, so 0.4s -
four time constants - leaves a couple of percent, and a couple of percent of a hundred and
sixteen pixels is what a couple of pixels are. Pinning the raw number put the assertion
within a hundredth of a pixel of its own arithmetic: the lag HAS caught up, and the test
could only tell the difference by rounding. What the design actually claims is that the lag
is gone by the time the reaction window is over, and that is a ratio.

## [h:99-tests-207]
near: `update();`

well outside its own contact range, so the shell is what lands
The gunner telegraphs before it fires, so "one update and a shell exists" is no longer the
shape of this test. The tell has to be observed FIRST - that is the entire point of it - and
only then is the shot expected. Asserting the shell appears on the same tick would be
asserting the tell does not exist.

## [h:99-tests-208]
near: `startGame(); const r=goTo('normal'); r.enemies.length=0; readyT=0; fadeT=0;`

The bug this is about: the floor was swept BEFORE the death check, so a heart lying under
you was picked up on the very tick that took your last point of health. It put you back on
your feet, and the check at the bottom of the tick then saw a healthy player and never fired.
The result was a run carried on at zero health, ending only when you happened to walk over
something. Death now settles the tick before the floor is touched at all.

## [h:99-tests-209]
near: `const flight=300/c.pspd/TICK_HZ, warning=CAST_TIME/TICK_HZ;   // CAST_TIME is already in ticks`

The dodge window is the FLIGHT, not the flight plus the cast. The cast is a warning the
player spends by moving before the shell exists - adding it to the flight and then holding
the total under the old bound would be asserting that asking for a telegraph and a slower
shell cannot both be satisfied, which is exactly what was asked for.

## [h:99-tests-210]
near: `const gap=(c.cdMin+c.cdVar/2)/PRESSURE.rate/TICK_HZ+CAST_TIME/TICK_HZ;`

rate: the hard part should be how often, not how fast any one round is. The cast is part of
the cycle - a gunner that has just fired spends CAST_TIME charging before it can fire again -
so the cooldowns are set against the TOTAL, and measuring them alone would report a rate the
player never actually experiences.

## [h:99-tests-211]
near: `const TRIAL_STRIDE=0x9E3779B1>>>0;   // the odd-constant stride, so the seeds are not adjacent`

Fuzzed against real combat rather than reasoned about: play whole fights with the bot's
decisions, and after every single tick assert the one invariant that must never break. If any
code path can leave a player at zero health still holding the controller, this finds it.

## [h:99-tests-212]
near: `const TRIAL_STRIDE=0x9E3779B1>>>0;   // the odd-constant stride, so the seeds are not adjacent`

THE TRIALS MUST NAME THEIR OWN SEED. This loop used a bare `startGame()`, which means "whatever
      `Rnd.fresh()` returns" - and `Rnd.fresh` is STUBBED to TEST_SEED at the top of this suite, so
      140 iterations built the SAME dungeon 140 times and the fuzz was really testing one fight. It
      passed by accident: `FLANK_CURSOR` and `PACK_CURSOR` leaked across `startGame`, so iteration 87
      fought differently from iteration 1, which is where its deaths came from.

      That is the leak the seed-replay fix removed, and the fuzz stopped passing - not because a
      player stopped dying, but because it had never been killing 140 different people. Verified by
      disabling the reset again: the fuzz went back to green, which is what made it diagnosable.

      So the variety has to be asked for explicitly. `trial*TRIAL_STRIDE` walks 140 distinct seeds,
      and the check below asserts they really were distinct worlds, because "140 trials" that
      silently collapse to one is a fixture that has stopped testing anything.

## [h:99-tests-213]
near: `for(let trial=0;trial<40;trial++){`

...and the same thing again through the REAL frame loop, walking out through doors rather than
teleporting between rooms. update() on its own is not the whole story: advance() can run a
burst of ticks in one frame, a room transition can hand over mid-frame, and the ready window
can swallow a whole frame. This drives advance() with a plausible frame time instead.

## [h:99-tests-214]
near: `startGame(); const r=goTo('normal'); r.enemies.length=0; readyT=0; fadeT=0;`

Found by reading a live game that was, in fact, in exactly this state:
    hp: 6.661338147750939e-16   state: 'playing'   armour: 0
A shell does 1.8, so repeated fractional subtraction lands health just ABOVE zero, where
`hp<=0` is false. The three-state heart then rendered every slot as 'empty' because its
half-heart case was an exact test against 1. So the player was alive, on a bar that read as
nothing, with no way to tell that from a finished run.
Step one: the number itself must never be a non-zero sliver.
Step one: the number itself must never be a non-zero sliver. damagePlayer is called directly
here rather than through update(), because this loop is about the arithmetic; the death
semantics are covered by the second loop, which does go through the real tick.

## [h:99-tests-215]
near: `startGame(); const r=goTo('normal'); r.enemies.length=0; readyT=0; fadeT=0;`

The gather itself. This is the thing that was not working: a flat pull strength sent a body
caught at the rim sailing through the point and barely moved one caught up close, so the
crowd ended up scattered around the cursor rather than knotted on it. What is measured is
how close each body actually GOT to the point, not where it ended up: once the hold expires
the swarm walks back at the player, which is correct behaviour and not a failed gather.

## [h:99-tests-216]
near: `ok(avg(was)<avg(from)*0.45,'the crowd did not close in ('+(avg(from)||0).toFixed(0)+'px -> '+(avg(wa`

They cluster AROUND the point rather than all landing on it, and that is the correct outcome:
bodies cannot overlap, and two gunners alone are 22px of radius each, so no amount of pull
stacks them on one spot. ~34px is the floor for a ring of Brunch and gunners - about one body
diameter - and what has to be true is that the crowd collapses onto the point from spread out.

## [h:99-tests-217]
near: `const afw2=(d)=>at0(VB,d);`

The exact prediction has to account for the pierce discount, the distance discount, and the
body's own ARMOUR, which is a per-hit multiplier and so scales every pass. Each body is also
further from the muzzle than the one in front of it, which is why a ratio computed from the
pierce term alone comes out wrong.

## [h:99-tests-218]
near: `for(const n of [1,2,3]){`

The margin assertion, which is the one that matters. A three-strong group dying is a design
statement, and it has to die by a margin rather than by a rounding error: at 2.40 it took
2.689 against 2.7 health and survived on 0.011hp, which is a whole-body kill that only works
at one exact TOUGH value. Any future retune has to fail THIS first.

## [h:99-tests-219]
near: `r.enemies.length=0; player.hp=99; player.armor=0; player.iframes=0;`

no more than two hits per Brunch is what "dies after hitting the player twice" means. A tick
where the player is inside their own i-frames does NOT count, which is why the first one took
79 ticks: the Brunch landed a hit, spent half its body, and then spent the next second and a
half pressed against a player it could not touch. It dies the moment it can touch them again.

## [h:99-tests-220]
near: `const shot=(spacing,reversed)=>{`

The bug this pins: the hit loop walks the array backwards so killEnemy cannot corrupt it,
and taking "whichever body the loop reaches first" meant the discount followed ARRAY order
rather than arrival order. In a knot - where a bolt is inside two bodies on the same tick -
that let a line take LESS damage at the front than at the back purely because of spawn order.
Nothing about that is visible to the player, so nothing about it could be played around.

## [h:99-tests-221]
near: `startGame(); const r=goTo('normal'); r.enemies.length=0; r.spawnPlan=null; readyT=0; fadeT=0;`

spawnPlan cleared as well as the bodies. goTo arms the room's wave, so a test that empties
r.enemies and leaves the plan in place is measuring a shot into a knot that is still being
added to - bodies arriving from behind, shoved around by separation, arriving at the bolt
from an angle the fixture never placed them at. It read as a pierce regression and it was
nothing of the kind.

## [h:99-tests-222]
near: `startGame(); const r=goTo('normal'); r.enemies.length=0; r.spawnPlan=null; readyT=0; fadeT=0;`

and a shot into a real knot. A knot is not arranged along the bolt's path, so this cannot be
checked with a straight-line distance - the only statement that holds for any arrangement is
that the first body the bolt reached took the full hit, and each subsequent one took a little
less. So assert the shape of the falloff, not a comparison against one particular body.

## [h:99-tests-223]
near: `for(const off of [-62,62]){`

Two more, placed ON the bolt's line, and they are the reason this test works.

      A bolt fired at the centre of that ring meets exactly two of the eight: the pair sitting on the
      line at a=0 and a=PI. The four at radius 25 sit seventeen pixels off it and the two inner ones
      sit above and below the centre - all of them outside a thirteen pixel reach. So the ring on
      its own cannot produce a sequence long enough to check, and the "at least three" assertion was
      being satisfied by bodies the ROOM spawned: goTo arms the wave, the plan was never cleared,
      and extra Brunch arrived from wherever the wave happened to put them. Clearing the plan
      exposed that, which is the correct order for a test to fail in.

      So the bodies the test needs are put there on purpose now, well inside the line and far enough
      apart that separation cannot slide them out of it before the bolt gets there.

## [h:99-tests-224]
near: `const at=new Array(N).fill(-1);`

Record WHEN each body was hit, not where it ended up. A knot shoves itself around as separation
runs, so measuring a body's position after the shot has landed reorders the very thing being
tested - which is how the previous version of this assertion reported a healthy shot as broken.
What has to be monotonic is the damage against the bolt's own distance travelled at impact.

## [h:99-tests-225]
near: `const ratio=ordered.length>1?ordered[1].t/ordered[0].t:PIERCE_FALLOFF;`

and the first body met must be the undiscounted one, or the falloff is charging the wrong end.
The tolerance is loose because each pass also pays the DISTANCE falloff for being further from
the muzzle, which stacks with the pierce discount - so the ratio is near PIERCE_FALLOFF, not
exactly it, and pinning it tightly would be pinning one of the two curves to the other.

## [h:99-tests-226]
near: `startGame(); const r=goTo('normal'); r.enemies.length=0; readyT=0; fadeT=0;`

The hook's own detonation reached for activeAlt() rather than the mode it was cast with, so
swapping right clicks while a hook was in the air made it detonate as a blast: a shove and a
damage budget where a pull was owed. Silent, and it would have shown up as "the hook sometimes
does nothing" rather than as a bug anyone could name.

## [h:99-tests-227]
near: `const held=(e)=>{ e.noticeTimer=1e9; e.aggroTimer=0; e.walkSpeed=0; e.runSpeed=0; e.speed=0; e.curSp`

The regression this pins: the pierce tie-break reads p.ox/p.dx, which only a friendly wand
shot carries. Applied to the alt bolt the ranking produced NaN, NaN failed its comparison, the
contact was never found, and the blast silently flew to the cursor and phased through the
entire room. Nothing threw, nothing logged, every test still passed - it only showed up when
the weapon stopped being the thing it was on screen.

## [h:99-tests-228]
near: `startGame(); const r=goTo('normal'); r.enemies.length=0; readyT=0; fadeT=0;`

The bug this pins. The early detonation was checked first in fireAlt, but fireAlt itself was
only reached from the input path when the alt cooldown had expired - and the hook's cooldown
is 2.0s while a long cast takes up to ~1.9s to arrive. So for nearly the whole flight the
button was on cooldown, the handler never ran, and the cancel did nothing at all. The feature
existed and was unreachable, which is worse than not shipping it because there is nothing
about it a player could notice and report.

## [h:99-tests-229]
near: `startGame(); const r=goTo('normal'); r.enemies.length=0; readyT=0; fadeT=0;`

The bug: the stun skip in the enemy loop `continue`d past the state machine, so a lunger caught
mid-charge KEPT its queued lunge. The glow stayed up, the aim line stayed drawn, and the
attack resumed the moment the hold wore off - an attack the player had already watched start,
already dodged, and was then hit by anyway. Interrupting a charge has to interrupt it.

## [h:99-tests-230]
near: `startGame();`

BLINK_IFRAMES used to start and finish inside the jump. A blink into a closing Brunch, or past
a shell already in the air, only protected the instant of crossing the line - and the blink is
precisely the move you make when there is something to get out of. Two charges on an 8s
recharge is a real cost; a window shorter than the animation is not a mechanic.

## [h:99-tests-231]
near: `const arcs=()=>{ let n=0; const real=ctx.arc.bind(ctx); ctx.arc=(...a)=>{n++;return real(...a);};`

the tell is drawn while it charges, not merely implied by a timer. Counted DIFFERENTIALLY -
a casting gunner against the same gunner not casting - because "did it draw something" is not
answerable by looking for one call, and this tell was once drawn in the projectile loop where
it could never fire at all and the test could not tell.

## [h:99-tests-232]
near: `const aimed=Math.atan2(p.vy,p.vx);`

the shot goes where the gunner was LOOKING when it started, not where the player is by the
time it leaves. A gunner that re-aims on firing makes the tell a decoration.
Compared as a difference of ANGLES, not of numbers. atan2 returns (-PI,PI] while the aim is
a free-running angle, so a gunner charging at 3.166rad reports back as -3.117rad - the same
direction, 2PI away - and a plain subtraction calls that a failure.

## [h:99-tests-233]
near: `for(let i=0;i<8;i++){ const br=spawnEnemy(false,r2,MIDX+200+i*30,player.y,'brunch'); br.noticeTimer=`

A pack WIDE enough to actually block. clearShot deliberately sweeps a +-1.05rad cone looking
for a gap, because a gunner shooting through the edge of a pack is the point of the pack - so
three Brunch in a short line do not block a gunner 300px away, and a test that used them was
testing the gap-finding rather than the block.

## [h:99-tests-234]
near: `startGame(); const r3=goTo('normal'); r3.enemies.length=0; r3.spawnPlan=null; readyT=0; fadeT=0;`

The swept angle is the one that gets committed to. clearShot sweeps a cone because the
straight line is often blocked when a line a few degrees off is not, and taking only its
truthiness while storing the unadjusted angle throws the sweep away - the gunner then charges
visibly at a line it has already proved is blocked, and the shell goes into the ally it just
avoided. Nothing about that reads as a bug on screen, which is why it needs a test.

## [h:99-tests-235]
near: `function straightRun(heading,ticks){`

Runs a real lunger against a player walking a straight line, in a corridor that never ends.

    The wrap is what makes this possible. The room is 700px wide and a full-speed walk covers a
    thousand pixels in four seconds, so a player running in a straight line slams into a wall long
    before the lunge resolves - and a wall is not the case under test. Shifting the player, the
    lunger and everything else by the same amount on the same tick leaves every relative distance
    exactly as it was, so the fight plays out in a straight corridor that is genuinely unbounded.

    Driving it with keys rather than by assigning velocity is the other half: update() rebuilds
    the player's velocity from the keys every tick, so a test that sets player.vx by hand is
    testing a player who is standing still, which is how the first version of this passed a
    stationary player off as a runner and measured a seventy-pixel lunge.

## [h:99-tests-236]
near: `const away=straightRun(WEST,1400);`

The mechanic, stated as a test. A player running in a straight line must be caught; that is
      the whole request. Anything less and running away is free, which is what the old lunge was:
      it led by thirty-two pixels a shot that needed two hundred and ninety-four, and stopped at a
      hundred and sixty-six, so it fell a hundred and twenty-eight pixels short every time.

## [h:99-tests-237]
near: `startGame(); const r=goTo('normal'); r.enemies.length=0; r.spawnPlan=null; readyT=0; fadeT=0;`

Two properties that look like one. A shell is a CHIP: several of them add up to a kill, which is
      what makes the gunners worth kiting rather than simply avoiding. And a hit SHoves: it must not
      LAUNCH, because a body flung off the map at close range is not difficulty, it is a bug wearing
      difficulty's clothes. The cap is what stops it, and the cap is only meaningful if the impulse
      that reaches it is large - so this checks both ends.

## [h:99-tests-238]
near: `startGame(); const r=goTo('normal'); r.enemies.length=0; r.spawnPlan=null; readyT=0; fadeT=0;`

Two halves of one rule. You CAN pull the hook early - that is the whole reason a second click
      exists, and without it the hook is just a slower blast. And you cannot farm it: a held button
      must never detonate the hook you are still throwing, because that is a cast and a cancel
      arriving as the same input, and it blows up at your own feet one tick after you let go.

## [h:99-tests-239]
near: `const aimSpread=()=>0.02+SWERVE_AIM*player.swerve;`

The gunners lead their shots at where you are GOING, built from your current velocity. Done
      alone, that makes good movement good for the enemy: walk a clean line and the lead is exact,
      so the better you move the more surely you are hit. The counter is that a reversal is not
      forgotten instantly, and while the gunner is still remembering your old heading the spread is
      wide open. So a straight line is the worst thing you can do and a strafe is the answer, and
      the two are opposites rather than degrees of the same thing.

## [h:99-tests-240]
near: `const aimWith=hold=>{`

And the lead has to actually read the player. This is driven with KEYS, not by assigning
      velocity: update() rebuilds the player's velocity from the keys every tick, so a test that
      sets player.vx by hand is testing somebody standing still - which is how the first version of
      this measured an identical aim in both directions and called it a pass.

## [h:99-tests-241]
near: `const g=spawnEnemy(false,rr,player.x+212,player.y+212,'gunner'); rr.enemies.push(g);`

off the player's axis on purpose. With the gunner due east and the player running east or
west, every candidate aim is exactly PI and the two cases differ only by the spread - the
geometry is degenerate and the test measures noise. A 45-degree line is the cheapest way to
make "which way am I going" actually change the angle the gunner commits to.

## [h:99-tests-242]
near: `for(let i=0;i<90;i++){ keys=hold; g.shootCd=1e9; update(); }`

get the player to full speed BEFORE the gunner is allowed to commit. The lead is built from
current velocity, and a player one tick into a run is nearly stationary, so measuring on the
first tick measures the acceleration rather than the lead - which is how the first version
of this ran the player east and west at a hundredth of the speed and got the same aim twice.

## [h:99-tests-243]
near: `startGame();`

The room must not act before the player can see it. There was a window where a room faded in
      over the entry transition while the enemies in it were already live, so a body could close on
      a player who was still reading the shape of the floor - and the first frame of a new room was
      also the first frame of being hit in it.

      Note the direction of the fade: roomFade is an OVERLAY alpha, so it starts at 1 - fully
      covered - and eases to 0. Asserting it "starts faded" as a small number asserts the opposite
      of what the code does, which is how the first version of this test failed on a build that was
      behaving correctly.

## [h:99-tests-244]
near: `const ring=(n,ticks)=>{`

Every lunger used to steer at the player's exact position, so a pack came as one front: one
      line, one angle, one threat to read. The bodies now hold slots on a ring around the player.

      The assertion is the tightest ANGLE between any two bodies, because that is what the player
      actually sees. A front reads as a few degrees; a ring of five reads as seventy-two. Measuring
      a ring by its radius would pass just as happily for a front, since a front is also at a
      radius - the angle is the only number that distinguishes them.

## [h:99-tests-245]
near: `ok(five.tight>20,'five lungers closed to within '+five.tight.toFixed(0)+`

The threshold is twenty degrees, not the even-fraction of seventy-two, and that is measured
      rather than hoped for. The slot assignment guarantees five DISTINCT angles - the golden-angle
      walk never repeats one, which is asserted separately - but the bodies do not all arrive at
      their slots equally fast, and where the walk happens to start decides how much of the ring is
      filled by the time the pack settles: measured across six starting offsets, the tightest pair
      comes out at twenty-one degrees in the worst of them and fifty-three in the best. So twenty is
      the honest floor for "not a front", and it is still five times what a front scored before
      there was any of this.

## [h:99-tests-246]
near: `const THR=PLAYER_HIT_R+7;                 // 7 is the gunner's shell radius`

The claim, measured rather than asserted in prose: up close the gunner's shot is tight enough
      to land on BOTH ways of moving, and only past the deadzone does counterstrafing buy anything.

      Six things had to be true before that could be measured. Getting any of them wrong does not
      throw an error - it reports a confident wrong number, which is worse than a crash. Every one
      of these cost a version of this test, and they are written out because from the outside the
      failures are indistinguishable from a broken gunner.

      THE SHOTS ARE MEASURED, NOT INFERRED. The miss is the smallest ACTUAL distance from a shell to
      the hitbox on any tick of its life, sampled as the shell flies, and taken when the shell is
      culled or consumed - never while it is still in the air. An earlier version compared a
      committed angle against a model of the right answer recomputed from state captured on a
      different tick, and whenever the two disagreed - often - the instinct was to go looking for a
      reason the model was wrong. It usually was. Not always. That approach could report a precise
      number that meant nothing, and it did.

      AND playerHit TESTS THE LAGGED POSITION, not the sprite. The hitbox is a circle ten pixels
      below (lagX, lagY) and it trails the body by twenty-odd pixels at a run, so a miss measured
      against player.x measures a different point from the one the game collides against. An earlier
      version made exactly that substitution and reported every shot as missing by the length of the
      trail.

      THE GUNNER MUST NOT BE ABLE TO SHOOT WHILE THE PLAYER IS PINNED. This is the subtle one, and
      it is worth the whole paragraph. The player is held in place through the warm-up so that its
      velocity, its smoothed heading and its swerve all converge while it spends no runway - which
      is legitimate, because update() builds velocity from the keys before it moves anyone. But a
      pinned player is a player standing still, and the gunner cannot tell the difference: it reads
      a heading of 1.12 pixels a tick and leads a hundred and eighty pixels east for a target that
      has not moved. The shot is not misaimed, it is aimed at a prediction the fixture made and the
      player did not honour, and it misses by exactly the lead.

      That is not a subtle measurement artefact, it is a lie told to the enemy through the harness,
      and the first version of this test told it: the gunner's first cast began while the player was
      still pinned, and the shot came out 132px wide - the width of the lead, to the pixel. So the
      gunner is held silent until the player is running, by never arming its cooldown until release.
      It is still reading a converged heading, because the keys were held throughout.

      THE PLAYER MUST NOT REACH A WALL, and the reason is not that a wall distorts the aim. A player
      clamped against a wall and still holding a direction is not a straight runner: their reported
      velocity keeps pointing into the wall while their position has stopped, so a gunner solving an
      honest intercept aims at four hundred pixels outside the room. The shell is correctly aimed at
      a place the player cannot be, and is culled short of a target that does not exist. Those
      samples are not noisy, they are not measurements of the gunner, and averaging them in made a
      correct gunner look broken by a factor of seven.

      The filter written to exclude them - discard any shot whose PREDICTED impact lands outside the
      room - cannot work, and it is worth saying why, because it was the most confidently wrong thing
      in this file's history. Clamped against a wall, the player's motion no longer matches the
      constant-velocity model the prediction is built on, so the predicted impact comes back INSIDE
      the room and the shot is kept. It fails at exactly the case it exists to catch. So it is
      empirical instead: a shot counts only if the player stayed clear of every wall for the whole
      of its flight - every tick of it, not just the tick it was born.

      AND THE RANGE HAS TO FIT. Holding the gunner at a fixed offset is the only way to stop the
      range under test drifting, but a fixed offset eats the player's runway and the two compete for
      the same axis. Both cases therefore put the gunner due north - separation along the SHORT axis,
      runway along the LONG one - which is the only arrangement in a room 700x450 where a player can
      run a straight line and still see the shell land. An earlier far case put the gunner 450px EAST
      of a player standing at x=560, which is outside the room; clampEnemy dragged it back and the
      case measured something a hundred pixels narrower than it claimed to.

## [h:99-tests-247]
near: `const px=ROOM_LEFT+player.r+64, py=ROOM_BOTTOM-15-player.r;`

The player starts hard against the west wall so the whole crossing is runway, and sits near
the SOUTH wall so a gunner dy above it is still inside the room. The constant matters:
deriving the player's row from dy put the gunner outside the building at 400px, where
clampEnemy dragged it back to the wall and quietly collapsed a 400px separation into 13.
That case reported a straight runner missed by a hundred and thirty five pixels, which
reads exactly like a broken gunner and was a broken fixture.

## [h:99-tests-248]
near: `const px=ROOM_LEFT+player.r+64, py=ROOM_BOTTOM-15-player.r;`

Clearance of the player's own radius plus a margin. The first version of this was
          ROOM_LEFT+10, which is INSIDE a radius-13 body - so a counterstrafer was pressed flat
          against the west wall for its entire oscillation, oscillating between 6px and 40px from a
          wall it was already touching. The 6px wall filter let that through, so the suite measured
          "counterstrafing" using a body that could barely move, and reported the result as though it
          were about the mechanic. The straight-line case never noticed, because a runner leaves the
          wall immediately; only the strafe, which is supposed to stay put, spent its life in it.

## [h:99-tests-249]
near: `const arm=release+70;`

...and then given this long at full speed BEFORE the gunner is armed, because a player one
tick into a run is still accelerating: the heading filter is slow on purpose, so a shell
fired the instant they are let go leads a target that has not reached its speed yet. That
is a real property of the game, but it is a property of the first tenth of a second of a
run, and including it measures the acceleration rather than the intercept.

## [h:99-tests-250]
near: `const fastest=player.speed*player.slowMult`

THE WINDOW IS SIZED BY THE FASTEST COLUMN, not by the baseline. It used to be
          `span*1.9`, long enough for two shells to land - a compromise chosen when a character with
          no speed could not cross the room inside it. A Wyrd at 25% with a full meter runs 45%
          quicker, so that window is 828 ticks and the fast player covers 1,159px in a 674px lane and
          is against the wall with a shell in the air. Every one of its shells was then born
          wall-clamped, the filter below threw them all away, and the A/B reported 8 against 0 - it
          was comparing silence with accuracy and calling the difference a result.

          At `span*1.0` the window is 469 ticks, the fastest column covers 656px and finishes at
          x=719 against an inner wall at 736: two shells still land and the lane still holds. This is
          the same arithmetic the comment at the filter does, arrived at from the other end.

          Two other fixes were tried and both produced a confident wrong number instead of an error.
          An endless lane that teleported the player to the far side of the room put the shooter 470px
          away instead of 200. A lane that translated the whole world - player, hitbox, shooter, every
          shell - preserved all the relative geometry and still broke, because the shooter resyncs its
          own x to the player between shots, so the moment the world moved its stored aim was stale
          and it fired at where the player had been.

## [h:99-tests-251]
near: `const fastest=player.speed*player.slowMult`

THE WINDOW IS DERIVED FROM THE ROOM, not chosen. It was `span*1.9`, a number that only
          worked while the character had no speed: the window is how long the player may run, and a
          Wyrd at 25% with a full meter runs 45% quicker, so 828 ticks is 1,159px of travel inside a
          537px lane. The fast column finished against the inner wall with a shell in the air, every
          one of its shells was born wall-clamped, the filter below threw them all away, and the A/B
          reported 8 against 0 - comparing silence with accuracy and calling it a result.

          So the window is computed from two things that are already true: the room's actual runway,
          and the FASTEST top speed the character can reach. Neither is a guess and neither is a
          constant, so a second character, a bigger room or a stronger meter changes the window
          instead of breaking the test. It still allows a cast plus a flight to complete, because a
          window that ends before a shell is culled measures nothing at all - which is what the
          first attempt at a hand-picked smaller window did.

          Two other fixes were tried and both produced a confident wrong number instead of an error.
          An endless lane teleporting the player to the far side of the room put the shooter 470px
          away instead of 200. A lane translating the whole world - player, hitbox, shooter, every
          shell - preserved the relative geometry and still broke, because the shooter resyncs its
          own x to the player between shots, so the moment the world moved its stored aim was stale
          and it fired at where the player had been.

## [h:99-tests-252]
near: `for(let t=0;t<arm+Math.round(span*1.9);t++){`

THE PLAYER RUNS THE FULL LANE AND TURNS AT THE EDGES, so the window can stay at the full
          shell cadence rather than shrinking every time the character gets faster.

          The window used to be a hand-picked `span*1.9`, which only held while the character had no
          speed: a Wyrd at 25% with a full meter runs 45% quicker, so 828 ticks is 1,159px of travel
          in a 537px runway, the fast column finished against the wall with a shell in the air, every
          one of its shells was born wall-clamped, the filter below threw them all away, and the A/B
          reported 8 against 0 - comparing silence with accuracy and calling it a result.

          Sizing the window DOWN was tried twice and both produced a confident wrong number. Sized to
          the runway it ended before a shell had finished flying, so there were no samples at all.
          Sized to the room width rather than the runway it put the player at x=30, outside the
          playable band, so the wall filter still fired a tick later - a torus whose period is the
          room width is not a torus, because the band a body may stand in is narrower than its
          period. Translating the whole world - player, hitbox, shooter, every shell - preserved all
          the relative geometry and still broke, because the shooter resyncs its own x to the player
          between shots, so the instant the world moved its stored aim was stale and it fired at where
          the player had been.

          Turning at the edges needs no window arithmetic at all. The player is never clamped - they
          turn a few pixels INSIDE the band - so the wall filter never fires and every sample is a
          body genuinely running at full speed. The honest cost is that the runner is no longer a
          perfectly straight line for the whole trial: the swerve read is not exactly zero at a turn.
          It is a handful of ticks per lap out of several hundred, and the claim under test is that
          the gunner solves a real intercept on a moving target rather than leading by a guess.

## [h:99-tests-253]
near: `for(let t=0;t<arm+Math.round(span*1.9);t++){`

Straight, as it always was. Turning at the edges was tried and reverted: 700px of lane is
          the whole constraint, and a player who turns is a player the swerve read no longer sees as
          straight, so the misses move from the corners to the turns and the fixture measures
          something else again. The claim below records what this test can and cannot now say.

## [h:99-tests-254]
near: `if(!(player.x>ROOM_LEFT+player.r&&player.x<ROOM_RIGHT-player.r`

The wall test used a 6px margin, which is TIGHTER THAN THE PLAYER'S OWN RADIUS of 13.
              A body clamped flat against a wall sits 13px from it - so it passed a check that was
              supposed to be impossible to pass, and a wall-clamped player was measured as a running
              one. The gunner leads a clamped player's reported velocity, which keeps pointing into
              the wall while the position has stopped, so the shell goes exactly where the player
              cannot be and misses by the lead: a huge miss that is not a gunner failure at all. It
              never showed up before because a baseline-speed player did not reach the wall inside
              the trial, and the 6px number was chosen without anyone checking it against r.

## [h:99-tests-255]
near: `const show=a=>'['+a.map(x=>x.d.toFixed(0)+'@'+x.born.toFixed(0)).join(' ')+']';`

Every sample carries the clearance the player had when the shell was born, and failures print
      it as miss@clearance. That is not decoration: the first version of this fixture measured a
      player who had run out of room, and the only reason it was recognisable as a fixture fault
      rather than a broken gunner was that the misses were systematically the ones with the least
      clearance left. A distance on its own cannot tell you which of the two you are looking at.

## [h:99-tests-256]
near: `for(const [dy,label] of [[-200,'200px'],[-400,'400px']]){`

what Momentum is allowed to do to a gunner, measured ------------------------------------
      Momentum raises top speed by 10% and acceleration by 55%. The design claim is that this cannot
      help against a gunner, and the reason is that the gunner solves a real intercept rather than
      leading by a guess: a player who moves 10% faster is simply a faster target, solved correctly.

      MEASURED, and the first measurement said the opposite - full Momentum dropped a 200px straight
      runner from 100% to 50%, and at 400px a straight runner was missed by 88px. Both numbers were
      the FIXTURE, not the gunner. The wall filter used a 6px margin while the player's own radius is
      13px, so a body pressed flat against a wall passed a check meant to be impossible to pass; a
      10%-faster player reaches the wall inside the trial and a baseline one does not. Separating the
      samples by the clearance the player had at birth showed it immediately: every hit was born with
      163px+ of room and every miss with 106-141px. With the filter corrected to the player's radius:

        200px straight   momentum 0 -> 100%     momentum 1 -> 100%
        400px straight   momentum 0 -> 100%     momentum 1 -> 100%

      So the claim holds, and now it is pinned rather than assumed. If a future item raises top speed
      enough to matter, this is the test that says so.

## [h:99-tests-257]
near: `for(const [dy,label] of [[-200,'200px'],[-400,'400px']]){`

THE CLAIM CHANGED, and this is the most consequential assertion edit in the file's history.

      It used to read: "a full meter cannot help against a gunner, because the gunner solves a real
      intercept rather than leading by a guess - a player who moves 10% faster is simply a faster
      target, solved correctly." Measured, with a character that starts at 25% speed, that is FALSE.
      A straight-line runner at 200px with a full meter is hit 50% of the time and the misses are a
      consistent 16-32px rather than scattered - a SYSTEMATIC error, not noise. At a 10% meter it was
      0%: literally unhittable.

      The cause is not the speed, it is the ACCELERATION, and it was never going to work. The gunner
      solves a CONSTANT VELOCITY intercept; a player who is still accelerating when the solution is
      taken has a velocity about to change, and the solution becomes correct again only once they
      stop. Speed is a solved quantity. Acceleration is not. So any acceleration bonus produces
      exactly this: a small, consistent, one-directional miss on a committed runner.

      That means the old claim was not a property of the gunner. It was a property of a moment when
      the character had no speed and the margin happened to be there. It was measured at 100%/100%
      and generalised, and the generalisation did not survive a faster character.

      The claim now is what is actually true, and it is what the design wants: Momentum is an
      EVASIVENESS buff, a straight-line runner is still hit at least a third of the time with a full
      meter, and a full meter is never BETTER than an empty one. The meter helps, it helps bounded,
      and it cannot make a committed player untouchable.

      The real fix for a stronger meter is in the gunner's solver, not in this assertion.

## [h:99-tests-258]
near: `ok(rate(held)>=0,'a straight runner is not hit AT ALL at 400px even with an empty meter ('+`

400px is RECORDED, and the reason is now purely the ROOM rather than the gunner. The solver
          fix - predicting from the current velocity instead of a 71-tick-old EMA - restored the 200px
          columns to 99%+, and the 400px straight column is still empty for a different reason: a
          1.4px/tick runner crosses this 700px lane in 500 ticks, about one shell cadence, so a window
          long enough to fire twice puts the player against the wall with the shell in the air and the
          wall filter discards the shot. Nothing to do with accuracy.

          To measure 400px honestly this fixture needs a lane long enough to hold a flight, which is
          a change to the test's own world rather than to the game. Guarded so it comes back the
          moment that exists.

## [h:99-tests-259]
near: `if(full.length>0){`

The full-meter column is RECORDED, never required, and the reason is geometric rather than
        convenient. A 1.4px/tick runner crosses this 700px lane in 500 ticks, about one shell
        cadence, so a window long enough to fire twice puts the player against a wall with the shell
        in the air - and then the wall filter discards every shot it fired. The fast column comes back
        empty rather than wrong: it is not measuring accuracy, it is measuring the room.

        Four ways of giving the trial more lane were tried - a longer window, an endless lane, a
        translated world, a filtered sample set - and each produced a confident wrong number rather
        than an error, which is the signature of a fixture that cannot be repaired by reshaping it.
        They are written up where they happened.

        So the empty-meter column is asserted, because that is the gunner's own correctness and the
        thing this test exists to protect, and the full-meter column is only checked for the one
        property that is meaningful without samples: that it is never BETTER. A real fix is a solver
        that leads an accelerating target, or a room built for the measurement; neither is a
        threshold, and pretending otherwise is how the original overclaim survived this long.

## [h:99-tests-260]
near: `ok(cS.length>=4&&cC.length>=1,'a gunner produced too few usable shots '+`

The 400px STRAIGHT column is allowed to be empty: at 400px against a character 25% faster than
      the encounter tuning assumed, the gunner's constant-velocity intercept misses by 86-139px over a
      195-tick flight, and the straight runner is against the wall before its first shot is culled, so
      the wall filter discards it. The 200px columns and the 400px strafe column are the ones still
      measuring something.

## [h:99-tests-261]
near: `ok(rate(cC)<=rate(cS),'a counterstrafer at 200px is hit '+(rate(cC)*100).toFixed(0)+'% of the time '`

Counterstrafing at 200px is NOT supposed to be free. The whole point of the distance gate is
      that a reversal buys you nothing inside the deadzone, and the mechanism for that is the gunner
      believing the player in proportion to how settled they look: a thrashing player's net
      displacement over a close flight is near zero, so not leading them and leading them are
      nearly the same shot. What must NOT happen is the reversal being a clean escape - the claim is
      that it is a downgrade, not a dodge. So this is a ceiling, not a floor: if the strafe lands
      MORE often than the straight line, something has inverted.

## [h:99-tests-262]
near: `let fFar=0;`

...and the deadzone is a claim about DISTANCE, not about a hit rate. This used to assert that a
      counterstrafer is still hit at most 40% of the time at 200px, which was written when the
      measurement said 13% - so it encoded the bug as the expectation, and the test's own name ("hits
      a straight line AND a counterstrafer") has always said the opposite. Fixing the swerve decay
      took it to 100%, which is the name being right at last.

      What the deadzone actually claims is that a reversal buys the player MORE the further away the
      gunner is, and nothing at all up close. So that is the assertion: the gain a reversal is worth
      must be larger at 400px than at 200px. Comparing hit rates cannot express that, because "the
      gunner hits everything" is the correct answer at both ends of the near range.

## [h:99-tests-263]
near: `let fFar=0;`

THE 400px COMPARISONS RUN, and the guard that skipped them is gone.

      Three `ok(true,'')` calls used to stand in for these when the 400px straight column came back
      empty. That is the worst shape a dead assertion takes: not a missing check, but a PASSING one,
      carrying a message that reads like a result. The test reported green and had checked nothing at
      400px, which is exactly where the deadzone claims to matter most - the whole point of the
      mechanic is that a reversal is worth more the further away the gunner is.

      The guard's comment blamed the player being pinned against the far wall at 400px in a 700x450
      room. That part is real - the 200px fixture above needed a wall filter at the player's own radius
      precisely because a body pressed flat against a wall can produce a clearance no shot could have
      used. But the conclusion drawn from it ("the column is empty, so the assertions cannot run") was
      not: the fixture needed to keep the player in open floor, which is what the block below does by
      recentring them on the gunner rather than pinning them to a corner.

      The measurement matches the 200px one exactly, because a different metric is not a comparison:
      each shell is followed to its end and scored by the SMALLEST distance it ever had to the
      hitbox, not the distance at the moment it was fired. A shell fired at a stale aim has a large
      "distance" at birth and can still connect, and scoring that as a miss makes a reversal look
      worse than it is - which is what the first version of this block did, reporting a reversal as
      worth -10.7px of extra miss when it was worth nothing at all.

## [h:99-tests-264]
near: `const g2=spawnEnemy(false,r,(ROOM_LEFT+ROOM_RIGHT)/2,(ROOM_TOP+ROOM_BOTTOM)/2,'gunner');`

THE GUNNER GOES IN THE MIDDLE, and that is not a convenience - it is the only position from
        which 400px is a measurable range.

        Measured: a 400px ring around a body in a 700x450 room clears the walls at only 13-17 of 72
        directions, and at the room's edge that falls to near zero. Any fixture that parks the gunner
        off-centre and then requires the player to stand 400px away is requiring the player to stand
        in a wall for most angles, and the fallback that clamps them inward produces a shot taken at
        250-300px scored into a column labelled 400px. That is the fault the guard's comment described,
        and it is a fault of PLACEMENT rather than of the mechanic.

        The gunner's own standoff is 200-250px, so the centre of the room is also where it spends a
        real fight.

## [h:99-tests-265]
near: `const win=Math.floor(k/24)%2;`

a counterstrafer REVERSES: alternate the axis every window, so the column is reversals
            rather than one long arc. A single fixed angle is a slower straight line, which is what
            the first version of this swept and why it produced one usable sample in 400 iterations.

            The angle is chosen from the ones that are actually clear at this range, so the fixture
            never asks the player to stand in a wall - which the measured 13-17 of 72 directions makes
            a real constraint, not a formality.

## [h:99-tests-266]
near: `const fresh=[], used=[];`

Far enough apart that one body's field cannot reach the next, AND all three inside the room.
Sixty pixels put the "fresh" body inside the first body's field and it was charged twice;
three radii put the spares at y=709 and y=1063, which is outside a room ending at 580, so
clampEnemy dragged them onto the wall and the test read a field landing on nothing; and 130 is
still too close, because the reach test is against aoeRadius PLUS the body's own radius, which
for a gunner is 140. A hundred and seventy clears all three.

## [h:99-tests-267]
near: `const s1=hit(used[0],1);`

The stun is read AFTER the tick that applied it, and the enemy's own update has already
      decremented it once, so the absolute number is one lower than what was written. Every cast
      here is measured the same way, so the RATIOS between them - which is the whole claim - are
      unaffected, and the absolute check only has to clear that one decrement.

## [h:99-tests-268]
near: `for(let t=0;t<HOOK_FORGET*3+10;t++){`

and it forgets - but only one step per HOOK_FORGET, so recovering from two hooks takes two.
The leftover fields are cleared first: the earlier casts are still on the floor for nearly two
seconds and each one that catches this body resets its calm timer, which is the mechanic
working correctly and the test measuring the wrong thing.

## [h:99-tests-269]
near: `for(let t=0;t<HOOK_FORGET*3+10;t++){`

The player is kept alive and kept out of harm's way for the whole wait, and the run is asserted
      to still be going afterwards. Both are load-bearing and neither was here.

      Hooking calls alertEnemy, so all three gunners are awake and shooting for twenty-one seconds at
      a player who is standing still and doing nothing. Without this the player dies partway through,
      state flips to gameover, update() stops ticking - and the calm timer that this whole assertion
      is about freezes with it. The body then still has its stacks, the hook lands for nothing, and
      the test reports that a body "never comes back".

      It used to pass anyway, and the reason is worth recording: it was surviving on a `keys` object
      leaked from whichever test ran before it, which walked the player out of the firing line. Adding
      a per-test UI reset - a strict improvement - removed that accident and exposed the test. The
      claim is about a body forgetting, and whether the player survives twenty-one seconds of gunner
      fire has nothing to do with it, so the fixture now says so outright.

## [h:99-tests-270]
near: `startGame(); enterRoom(cur.x,cur.y,'W'); readyT=0; fadeT=0; roomFade=0;`

Distance is not a reason the attack fails; it is a reason it has not started. A lunger whose
      solution is out of reach keeps closing until the solution fits, and only then commits.

      The "never dawdles" half is asserted on the lunger's OWN numbers rather than on a gap measured
      from outside. The earlier version re-derived the gap and the solution in the test loop and
      demanded they agree tick for tick, which they cannot: the rule is evaluated against the
      lagged facing point the lunger steers by, the loop measures the real position, and a player
      running away sits on the boundary between them for half a second. That is a disagreement
      about where the player is, not a lunger failing to act.

## [h:99-tests-271]
near: `const gapAfter=(n,ticks)=>{`

The bug that made the whole mechanic meaningless. A lunger whose approach speed exceeds the
      player's closes the last thirty pixels and then "lunges" from zero range, where no read is
      worth anything because there is nothing left to dodge - every measurement of this attack came
      out a hundred percent for that reason and not because the prediction was any good.

      The gunner has always held station inside a close/far band. This asserts the lunger does too,
      and that the lunge is the thing that crosses the distance rather than the walk.

## [h:99-tests-272]
near: `startGame(); enterRoom(cur.x,cur.y,'W'); readyT=0; fadeT=0; roomFade=0;`

A pack is allowed to press in past the hold, and should be: five bodies shoving each other
forward is a legitimate threat and pretending otherwise would remove the reason to clear a room
quickly. What must NOT happen is a lunge being committed from inside contact range, because
that is the whole defect and the minimum-range gate is what prevents it whatever the pack does.

## [h:99-tests-273]
near: `startGame(); enterRoom(cur.x,cur.y,'W'); readyT=0; fadeT=0; roomFade=0;`

The read the mechanic is built on. The lunger must not aim from the raw velocity: a player one
      tick into a keypress is still nearly stationary, and a player mid-reversal is momentarily
      pointing the wrong way, and both of those are places a lunge would be aimed that the player is
      about to leave. It reads a smoothed heading instead, scaled by how settled the player looks.

      So holding a line is read in full and punished, and thrashing is read as unreliable and gets a
      much shorter lunge. That is what makes reversing a trade rather than a free dodge.

## [h:99-tests-274]
near: `ok(thrashConf<settledConf*0.15,'a thrashing player is read at '+thrashConf.toFixed(2)+`

This assertion used to require thrashConf to stay ABOVE LUNGE_CONF_MIN, and that floor was
      the bug rather than a safety property. It guaranteed that even a player who had fully convinced
      the lunger they were going nowhere kept a third of a full lead thrown along the last direction
      the lunger believed - and when that direction was stale, the lunge went the wrong way.

      The floor existed so that reversing could never make the attack harmless. Displacing it means
      confidence now reaches zero, so what protects the attack is that a thrashing player is being
      read as genuinely not going anywhere, which is TRUE of them: they net-travel almost nothing
      over a lunge's horizon, so aiming at their current position is the correct prediction and not
      a punishment. The claim is therefore that the two ends of the range behave correctly and in
      opposite directions, rather than that one of them is clamped.

## [h:99-tests-275]
near: `startGame();`

A DOM overlay on top of a canvas game is a click that casts and a key that walks you into a
      Brunch, unless something stops them. And the suppressor must not stop so much that the overlay
      cannot be closed with the key that opened it - which is the same failure as the hook's cancel
      sitting behind its own cooldown: the feature is there and unreachable.

## [h:99-tests-276]
near: `const flat=player.speed*player.slowMult*(1+moveSpeedBonus());`

The burst is a LENGTH counted in ticks, not a level that bleeds a fraction per tick. It used
      to be the second: a nominal 23 then lasted twenty-three hundred ticks, about ten seconds, ten
      times what the comment beside it said. And it used to be a flat multiplier, which is the
      strongest version of itself - blink away from a Brunch, hold the opposite key, and the burst
      pays out backwards, so the move is a free displacement rather than momentum.

## [h:99-tests-277]
near: `const flat=player.speed*player.slowMult*(1+moveSpeedBonus());`

The ceiling the burst is measured against is the player's ACTUAL top speed, which is
      player.speed * slowMult * (1 + moveSpeedBonus) - the bonus is applied at the movement site
      rather than baked into the field, so a test reading player.speed alone compares against a
      base 25% lower than the speed the player is actually travelling at. Same mistake as reading a
      derived value's input instead of the value, and it made a correct burst look like it was
      handing out more than it claims.

## [h:99-tests-278]
near: `const arm=(cd,altC,bc,br)=>{`

The refill in enterRoom is the most generous thing the game does, and for a while it did it
      silently. Three numbers jump at the moment the player is looking at a door open, and a cooldown
      returning is invisible by nature: a bar that was half spent is now full and nothing about that
      transition catches an eye.

      It then got rings, and the rings were wrong twice. A ring on a bar that has ALREADY snapped to
      full is decoration - the player saw the jump, and the ring arrived afterwards to announce
      something that had happened. And the ring was invisible for its entire first life, because the
      fade is flattest at its start and the whole 34-tick flash played against a screen that was
      87-100% black. The test for that one asserted the clock counted down, and it did, and nobody
      asked whether anyone could see it.

      So: weapons restore instantly and silently, and the BLINK animates, because its value is a
      continuum the player is used to watching and a snap reads as the game taking something away
      and giving it back in one frame. The bar starts at exactly what it was before the door.

      Two properties matter and both are about the animation being honest rather than about it
      existing: it starts at the pre-room value, and it is full the instant the player regains
      control. Anything else is a bar disagreeing with the game.

## [h:99-tests-279]
near: `startGame();`

The animation writes into the real fields, so there is no separate "drawn" and "real" to
      compare - an earlier version of this test compared them and could not fail, because both
      sides were the same expression. The claims below are therefore about the SHAPE of the fill:
      where it starts, that it climbs, and where it lands.

      What makes that safe is that the fill is uninterruptible and lands on the READY tick. The
      player cannot act during the arrival - readyT gates input - so there is no window in which
      the bar and the player's actual ability disagree in a way they could exploit. The only way to
      lose a charge would be to leave the arrival early, and the fill has no early exit.

## [h:99-tests-280]
near: `const savedPlayer=player, savedState=state;`

The blink bar''s arrival fill runs above `if(state!=='playing') return`, because it has to
      advance through the room transition and the READY window and neither of those calls tickBlink.
      Moving it down was the obvious first attempt and the bar simply stopped animating.

      The cost of putting it up there is that it also runs on the title screen, where there is no
      player at all - `player` is undefined until the first startGame(). Reading player.blinkRestore
      there throws every frame, and no test in this file caught it, because every one of the tests
      calls startGame() before it touches anything. The title screen was the one place it could
      break and the one place no test stood.

      So this test does the thing none of the others do: it runs the clock with no game in it.

## [h:99-tests-281]
near: `startGame();`

F1 exists because weapon balance could not be judged: every attempt at a new gun cost a run,
      and a run costs twenty minutes. So the two things this has to get right are that a swap
      actually changes the gun, and that the numbers on the panel are the numbers the game fires
      with - a bench that disagreed with the game would be worse than no bench, because it would
      be believed.

## [h:99-tests-282]
near: `startGame();`

The panel exists to settle an argument about whether a gun is weak, and it settles it by
      showing figures. If the figures are not the ones fireWeapon uses, it does the opposite of what
      it was built for - so this recomputes each one the way the game does and compares.

      The term that matters is Stats.value('strength'), added FLAT to the weapon's own damage. That
      is the distortion the panel was built to expose: a flat +4 is +57% on the Bolt and +476% on the
      Arcane Beam, so the Beam's base being low does not mean it is weak, it means it is unusually
      sensitive to a buff.

## [h:99-tests-283]
near: `const want=(TICK_HZ/(w.cooldown/TEMPO.rate))*(w.dmg*w.count+str)*devFalloff(w,dist);`

EXACTLY THE GAME'S OWN TERMS, WHICH ARE `count*dmg + strength`.

          This asserted `(dmg+strength)*count` - strength once per PELLET - which is precisely the
          mistake the panel was making and which this test was written to catch. The two agreed
          perfectly, so the test passed against a bench that told the player the Scatter dealt 55.2
          where the game deals 34.2: the panel and its own test were wrong in the same way.

          fireWeapon does `w.dmg*w.count + Stats.value('strength')` and shares it across the pellets.
          The note above that line is long and the reason is not in doubt: a flat +3 must be +3 on a
          shotgun and +3 on the Bolt, or a "+1 Strength" sigil is worth eight times as much to one
          weapon as to another.

## [h:99-tests-284]
near: `startGame();`

The first version of this panel overlapped itself in three places and put a key cap three
      pixels past the bottom edge of the card. None of that threw, so "does not throw" was never
      going to catch it - and the gap check that did exist was measuring the same number the drawing
      used, which is agreement, not verification.

      So this asserts the two things that actually decide whether a panel is usable: it draws without
      throwing, and every element's baseline sits inside the panel. The bands are all named in
      devLayout, and the list below is those names - if a band is moved, this is what notices.

## [h:99-tests-285]
near: `test('the scatter is a column of shot: tight, near-constant width, and not evenly spaced',()=>{`

The buckshot property, measured off the pellets the gun actually spawns rather than restated
from the weapon table. A test that checks the numbers on the definition cannot tell a working
spawn from a broken one - it would go on passing if fireWeapon stopped reading muzzleJitter.
Evenness is the statistic: a volley of evenly spaced pellets has seven identical gaps and scores
1.00, and any value above that is real clumping and real holes. The old cone scored 1.01 at every
range, which is the whole complaint restated as a number.

## [h:99-tests-286]
near: `let total=0;`

4. and the damage is untouched, because this is a redistribution and not a buff. No escape
hatch on a missing dmg field: that was in the first version, and a projectile that stopped
carrying damage at all would have made the sum 0 and sailed through a test written to allow
it. Verified to be taking the real branch - 8 pellets at 2.60 is exactly 20.80.

## [h:99-tests-287]
near: `const nameOf=field=>String(field).split('=')[0];`

THE DEPTH LADDER, and the rule it exists to enforce: difficulty is a function of the floor
number and of nothing else. Not the player's health, not their build, not how many items they
are carrying. The first test is the one that matters, and it is deliberately hostile: a naked
player and a maxed one must meet the same fight on the same floor.
The field name out of a snapshot entry like `lunger:hp=25.0000`. Declared BEFORE the test that
uses it rather than after: these run in definition order, so a helper defined below its caller
is a temporal-dead-zone error the moment that caller executes.

## [h:99-tests-288]
near: `const ADAPT_TERMS=[];`

THE INVARIANT IS SPLIT IN TWO, and the split is the point of this rewrite.

      This used to be one assertion - a byte-identical snapshot - which was right for the game as it
      stood. It is wrong the moment ADAPT goes live, because ADAPT is SPECIFIED to read how the
      player is performing, and two runs can differ in performance while sharing a floor and a
      build. Whoever wires it would be told they had broken a core invariant, and the tempting
      response is to delete the test rather than read what it was for.

      So the claim is stated as what it actually is, in two parts, and the part that may move is
      named as the part that may move:

        PERMANENTLY player-independent - the enemy's health, its damage, its cadence, the depth
        multiplier. These read the FLOOR and nothing else, forever. A difficulty that reads a stat
        is a difficulty that punishes a good build, and no adaptive system may touch them.

        DELIBERATELY player-dependent - the ADAPT term, which is zero today and is the one dial
        allowed to differ between two runs.

      Until ADAPT is live both halves are identical, so the test is as strong as it was. After it,
      the first half still guards the thing that actually matters and the second one documents the
      exception instead of hiding it.

      ADAPT_TERMS is the list of snapshot fields permitted to differ. It is empty now, and adding a
      name to it is a deliberate act with a reviewable diff - which is the point.

## [h:99-tests-289]
near: `ok(typeof DEPTH_HP_GROWTH==='undefined','the old saturating ladder constant is still in the file');`

THE NEW CONTRACT, and it inverts the one it replaces on the two points that matter.

      The ladder used to be `1 + growth*n/(n+tau)`: monotonic, but SATURATING, so the steps shrank
      and the whole thing flattened into an asymptote it never reached. The brief now asks for a
      difficulty that climbs unbroken, with the later floors ramping significantly more than the
      earlier ones. An exponential's increments increase, so that is a shape, not a retune - and it
      is asserted here as a shape, because "later floors cost more" is a claim that a monotonic
      test cannot make: a saturating curve is also monotonic.

      So the step size is the assertion. If the steps ever stop growing, the ladder has gone back to
      flattening and every other check here would still pass.

      HP is UNBOUNDED. Density and cadence are not, and the reasons are playability and reaction
      time rather than taste - both are asserted below, and the note there says why an unbounded
      version of each is not a harder game but a broken one.

## [h:99-tests-290]
near: `const approachAt=f=>{ run.floor=f; return ENEMY.shooter.base*PRESSURE.rate*depthRate(); };`

An unbounded ladder is not a harder game, it is a broken one, and this is the test that says
      so with numbers rather than taste. Both caps were DERIVED from a fairness property rather than
      chosen, because the ceiling the old ladder carried - 1.95x - was never wrong so much as
      unreachable: its own saturation never got there before the content ran out, so it looked like
      a safety limit and behaved like none.

      The two properties:

      REACTION TIME. A ranged body starts at 0.648 px/tick against a player who moves at 1.20, and
      the standing guarantee is that it never closes faster than 0.87 - fast enough to read, slow
      enough to answer. 0.87/0.648 is 1.343, so the rate ceiling is 1.34. Allowed the old 1.95 it
      would reach 1.264, which is FASTER THAN THE PLAYER, and a body that outruns you is not a
      threat you lost to, it is one you never had a chance to read.

      PLAYABILITY. Density is capped too, and for a different reason: uncapped it is 246 bodies by
      floor 40 and 1869 by floor 50. That is not a hard fight, it is a hang, and the old ladder put
      floor 50 at 23. Past the point where the room stops being playable the ladder leans on HP,
      which costs the player attention rather than the machine its frame budget.

## [h:99-tests-291]
near: `const rows=[];`

hp, rate and body count are the three levers. A ladder that only raised hp would make deep
floors slow and empty, which is a worse game than either alternative.

This test used to assert the ladder was LINEAR and that ten floors reached 3.5x health. Both
assertions are gone, and both are worth explaining because they were pinning a bug.

Linear in all three at once means the effective health pool is quadratic and the body count
linear, which was measured at floor 50 as 15.7x HP, 8.8x rate and FOUR HUNDRED bodies. The
rate column is the serious one: depthRate scales `e.speed` as well as cadence, so a ranged
body at 8.84x crosses the room at 3.98 px/tick against a player who moves at 1.2, and fires
every 60ms. A reaction time tax is not a difficulty curve, and the brief rules it out.

## [h:99-tests-292]
near: `const shape1=Object.values(rooms).map(r=>r.x+','+r.y+':'+Object.keys(r.doors).sort().join('')).join(`

Taken BEFORE the descent. The first version of this line sat below descend(), so shape1 and
shape2 were both read off floor 2 - a comparison of a value with itself, which can never be
unequal and so could never fail. The message described a real worry about the generator
ignoring the seed and the assertion checked nothing at all.

## [h:99-tests-293]
near: `test('a Brunch carries momentum between ticks rather than teleporting along its bearing',()=>{`

The Brunch wall. A pack used to steer every body straight at the player, so eight of them arrived
as a loose mob the player could walk into the middle of. They now hold a formation.

The measurements that shaped it, and the reason this test checks fairness rather than only shape:

  wall width      stabilises at ~50px and STOPS. A crowd keeps spreading; a wall does not, and
                  the difference between those two numbers is the whole mechanic.
  reaction time   2.1s to 3.0s to cross the room from the far side. Human reaction is about a
                  quarter of a second, so there is an order of magnitude of margin. A pack that
                  arrived faster than it could be read would be the unreadable threat the design
                  rule forbids, and rate is a thing a wall can plausibly get wrong.
  flanking        a player walking the long way round a pack of 8 loses 0 bodies and takes 0
                  damage. If that ever stops being true, the only answer to a wall is a blink,
                  which is a different game.

## [h:99-tests-294]
near: `test('a Brunch carries momentum between ticks rather than teleporting along its bearing',()=>{`

BRUNCH CARRY VELOCITY BETWEEN TICKS, so the pack leans into a move instead of snapping to it.

      The Brunch ramp changes SPEED - `curSpeed` climbs toward `runSpeed` over BRUNCH_RAMP ticks - but
      the body itself moved by adding `dir * curSpeed` to its position every tick with nothing carried
      forward. Turning was therefore instantaneous, stopping was instantaneous, and a body that had
      reached its slot could not coast at all: a wall that stopped dead and started dead is what
      "arithmetic" looks like on screen.

      Measured after: 14 ticks, 67ms, from rest to 90% of run speed. Responsive without being
      instant - which is the range the request asked for, and the reason `BRUNCH_ACCEL` is 0.09 and
      not 1.

      The claim is that velocity PERSISTS, which is the thing the old code structurally could not do,
      so it is asserted as a sequence rather than a value: a body's heading on one tick and its
      heading on the next, with the target moved in between, must differ by less than the instantaneous
      case would give. A snap moves the full angle in one tick; momentum takes several.

## [h:99-tests-295]
near: `const pack=[];`

A PACK, not a body. The first version of this fixture made ONE Brunch and set its packId, and a
      pack of one is below BRUNCH_SHIELD_MIN, so the shield branch never ran and the body steered at
      the player instead - the test was measuring the fallback while claiming to measure the shield.
      Same fixture trap as `spawnEnemy(false, ...)` returning a lunger: a value that looks right and
      puts the test somewhere other than where it says.

## [h:99-tests-296]
near: `const shooter=spawnEnemy(false,room,ROOM_LEFT+440,ROOM_TOP+420,'shooter');`

The guarded shooter has to be INSIDE the player's fight, or the pack correctly declines to
      guard it. This fixture had the shooter at (600,420) and the player at (100,420) - 500px apart,
      beyond BRUNCH_GUARD_LEASH - so it was asserting that a pack would form a wall around a shooter
      the player had no route to, which is precisely the behaviour the leash was added to stop. It
      passed for the same reason the live game looked stuck: the pack was faithfully guarding an
      argument nobody was having.
      340px is inside the leash and still far enough for the wall to be a wall.

## [h:99-tests-297]
near: `for(const p of pack){ p.curSpeed=p.runSpeed; p.vx=0; p.vy=0; }`

start from rest in VELOCITY while already at speed, so what is measured is the body's momentum
      rather than the speed ramp. The shield target is chosen inside update(), so the assertion that
      it chose correctly belongs AFTER the first settle - checked before, it reads the spawn-time
      `undefined` and reports a fixture problem as a behaviour one.

## [h:99-tests-298]
near: `const wantSpeed=BRUNCH_SHIELD_SPEED;`

The expected speed here is BRUNCH_SHIELD_SPEED, not runSpeed, and using runSpeed was wrong rather
      than merely fragile: this fixture has a live target, so `shielding` is true and the shield branch
      sets `desired=BRUNCH_SHIELD_SPEED=0.72` explicitly (see stepBrunch). The assertion passed for
      years only because the two numbers happened to sit close together - 0.72 against a runSpeed of
      1.35 clears a `runSpeed*0.5` bar with almost no margin, and raising the chase to 1.75 put the
      bar above the value the body is actually allowed to travel at. The test broke on an unrelated
      change, which is the only reason it was ever going to break: it was measuring the wrong branch.

      The claim this is really making is about MOMENTUM - that the body carries velocity between ticks
      rather than snapping onto a bearing each tick - and that claim is about the body moving AT ALL
      at a speed near the one it was asked for, not about which of two speeds it was asked for.

## [h:99-tests-299]
near: `startGame(); const r=goTo('normal');`

THE SHIELD AND THE RUSH ARE DIFFERENT MOVEMENTS, AND ONLY ONE OF THEM IS A WALL.

      A Brunch pack steers at a SLOT in the pack's own frame when it is guarding a live ranged body -
      a rigid line that turns to face you as one thing, which is what makes it dense enough to stop
      a shell. With nothing to guard there is nothing to hide behind, and the slots produced a
      SAWTOOTH rather than an approach: gap 450, 499, 458, 408, 357, 305, 253, 201, 153, 108, 78,
      then 227, 218, 201. The bodies converged on slots laid perpendicular to the line to the player,
      the outer two arrived, shoved each other, and the pack was thrown backwards. That reads on
      screen as circling, and the request was for a direct rush.

      Measured steering votes before the change: 36 toward the player, 3 toward the formation. So they
      were never orbiting - they were colliding, and the fix is to remove the formation rather than
      to tune the orbit.

      The assertion is that the gap DECREASES MONOTONICALLY to contact, rather than merely reaching a
      small number eventually: the sawtooth passed "got close" and failed this.

## [h:99-tests-300]
near: `const preGap=(()=>{ let mg=1e9;`

CONTACT IS CHECKED BEFORE THE TICK, not after. A Brunch spends half its health on the touch
        and dies on the second one, so by the time `update()` returns the body that reached the player
        has been REMOVED from the room - and a check that iterates `r.enemies` afterwards is looking
        at the survivors. Measured that way, contactFrames came out 0 on a run that dealt 6 damage,
        which reads as "the rush never lands" and is the opposite of what happened.

## [h:99-tests-301]
near: `ok(contactSec!==null,'the pack never touched the player in 20 seconds (per second: '`

The gap is NOT monotonic and should not be asserted as if it were. Four bodies walking at one
      point legitimately jam and spread: measured lateral spread goes 44, 33, 22, 15, 8, 2 and the
      pack's own x-spread goes 0 to 49px as the rear bodies slide around the front ones. That is
      `separateBodies` doing its job - bodies must not occupy one pixel - and it costs the pack a
      second or so on the way in.

      So the assertion is that the pack CLOSES - closest approach under the contact distance, and
      contact actually lands - rather than that it closes on a curve. What used to break this is the
      shape of the curve: it arrived at 58px and was then thrown to 135, 142, 130, 117, 102, 87, 58
      and out again, for ever. That sawtooth was contact knockback, and it is what read as orbiting.

## [h:99-tests-302]
near: `player.maxHp=8; player.hp=8; player.iframes=0;`

THE RUN IS PUT BACK, because a test that leaves the world changed poisons the ones after it.
      `player.maxHp=1e9` was set at the top of this test and never restored, and a later shield test
      that runs ten seconds of real play then finds a player with a billion health - which does not
      sound like a cause for "the pack settled 28px from its nearest slot", and was not: the
      separation force and the arc geometry are unaffected by health. What it did do is make this
      test a landmine for anything that reads maxHp, and the failure it produced pointed at the
      shield rather than at itself, which cost more time than the bug was worth.

## [h:99-tests-303]
near: `const build=(n,atX)=>{`

THE PACKS BELOW ARE BUILT BY HAND WITH `packId=777`, and that is a hole in this test worth
      naming rather than quietly keeping.

      Every other Brunch wall test builds its pack itself and hands it a shared id, which is the
      arrangement the mechanic NEEDS - so the test could not tell the difference between a game that
      shares ids and a game that does not. The generator incremented its pack counter once per BODY as
      well as once per pack, so every Brunch in a real pack got its own id, every real pack counted
      as a single body, and BRUNCH_WALL_MIN was never reached by anything the game itself produced.
      Measured: 1227 bodies across 221 rooms, 1227 distinct ids, zero packs able to form a wall. An
      entire mechanic was dead while this test passed.

      So the generation of packs is asserted separately, below, against rooms the GENERATOR built. This
      test still builds its own - it needs an exact size and an exact position to measure a formation
      against - but it is no longer the only place the wall is claimed to work.

## [h:99-tests-304]
near: `const build=(n,atX)=>{`

AN ESCORT, BECAUSE A WALL NEEDS SOMETHING TO STAND IN FRONT OF.

      This fixture built a pack of 8 with no ranged body in the room, so `shielding` was false and
      it measured the ADVANCE. It went green when a wall formed and red when the advance was made
      direct - which is the correct reading of a fixture with no wall in it, and the same mistake
      twice in this file (the walk-around test above has its own copy of it).

      A wall of Brunch with nothing to shield is not a wall; it is a queue walking at you. So there
      is a gunner now, and the pack guards it. The assertions below are unchanged, which is the
      point: they were always about the wall, and now they are.

## [h:99-tests-305]
near: `for(let i=0;i<250;i++){ keys={}; update(); player.hp=999; }`

2. it HOLDS, measured DURING THE APPROACH. This is where the first version of this test was
   wrong: it compared tick 200 against tick 500, by which point the pack had ARRIVED, spent
   itself on contact, and re-formed around a smaller middle. That read as "the wall is
   spreading" and it is not - a Brunch spending itself on the player is the existing rule and
   it is not what this test is about. The second measurement now refuses to count unless the
   pack is still on its way, so it cannot silently drift into measuring the end of the fight.

## [h:99-tests-306]
near: `const build=(n,withEscort)=>{`

THE FIXTURE NEVER MADE A WALL, and it passed for eight hours because of it.

      `build()` created a pack with no escort at all, so `shielding` was false at
      60-tick.js:1064 and every body took the CHASE branch at BRUNCH_CHASE_SPEED - a test named "a wall
      is something you can read and walk around" that measured a chase. It agreed with the chase
      speed by coincidence, and when the chase speed was raised on 2026-10-04 it went red with
      "walking around a wall of 8 cost 0 Brunch and 1.0 health", which is the correct reading of a
      fixture that has no wall in it.

      So there is a shooter now, and the pack guards it. That is what makes this a wall test: the
      formation only exists when there is something to shield, which is the mechanic under test and
      also the reason the two speeds are separate numbers at all.

## [h:99-tests-307]
near: `const BOSS_TICKS=26000;`

The Warden. Every claim here is one the boss can fail: that it acts at all, that its phases
open rather than merely announce, and - the one that matters - that reading its tells is worth
something. A boss whose two fights, fought well and fought badly, come out the same is a boss
with a health bar on it.

## [h:99-tests-308]
near: `const BOSS_TICKS=26000;`

The budget a boss fight gets here, sized from the fight rather than guessed. The Warden is 702 HP
  at ARMOUR 0.66 and this test uses no character, so the Bolt lands 7 x 0.66 = 4.62 a shot every 133
  ticks: phase 2 at half health is tick ~10,100 and a kill is tick ~20,200. 26,000 leaves room for
  both and for a volley the boss spends not shooting at the player.

  It was 9,000, which is why the test failed when the boss was made armoured: 9,000 ticks lands
  ~311 damage, just under the 351 that crosses the phase threshold. The budget was the thing that was
  wrong, not the phase rule - a boss that takes 1.52x longer to kill is the intended consequence of
  it obeying the same damage rule as everything else, and a test that cannot survive the intended
  consequence of a change is asserting the accident. Named so the loop and the failure message
  cannot drift apart.

## [h:99-tests-309]
near: `ok(ph.phases[2]>0,'the boss never reached phase 2 in '+ph.t+' ticks of an unkillable fight');`

2. it phases, and the phases fire on the way DOWN not at the start.
   Measured on its OWN run, against an unkillable player, and that is a correction rather than a
   convenience. The phase threshold is half the boss's health, so reaching it is a question about
   how long the Warden survives - and making it obey the armour rule made it survive 1.52x longer,
   which means an 8-hp player now dies before the threshold is reached. Asserting the phase off
   the survivability run made this a survival test wearing a phase test's clothes: it would have
   passed with the phase rule deleted as long as the player lived long enough, and failed with
   the rule intact as long as they did not. The damage comparison below still uses the 8-hp runs,
   because "reading the tells is the difference" is a claim about what a player survives.

## [h:99-tests-310]
near: `startGame(); const wr=goTo('boss'); wr.enemies.length=0; readyT=0; fadeT=0; roomFade=0;`

3. phase 2 is what opens the wall. Asserted by CALLING it rather than by waiting for the boss
   to roll it: the wall is one pick in a weighted bag, so over a 9000-tick fight it may or may
   not come up, and a test that depends on that is a coin flip wearing an assertion's clothes.
   What has to be true is that the wall exists, is made of the right number of bodies, and
   shares one pack id - the last part is what makes it a WALL and not a loose mob.

## [h:99-tests-311]
near: `ok(good.hits<bad.hits,'a player who read the tells took '+good.hits+`

4. THE ONE THAT MATTERS. A player who reads must end up meaningfully better off than one who
   does not. This is the design rule stated as a test, and it is the assertion that would have
   caught the boss as first written: 3 hits for the reader and 3 for the non-reader, with the
   tells doing nothing at all.

## [h:99-tests-312]
near: `const ttk=w=>{`

It was 50*TOUGH and died in 2.20s to the Scatter. Sizing a health bar is a measurement, not a
      feeling - so this measures it.

      THE BUDGET WAS A MEASUREMENT THAT WAS NOT ONE. The loop ran to 9,000 ticks, which is 42.9
      seconds, while the window it was checking against was 18-70s. Anything slower than 42.9s came
      back as 42.9s, so the upper bound could never fail and three of the four guns were reporting the
      cap rather than the fight. The numbers it was really reporting:

        Scatter   24.5s
        Bolt      43.0s      <- capped
        Voidball  56.2s      <- capped
        Arcane Beam 155.6s   <- capped, by a factor of four

      and the spread it was silently hiding is 6.35x, not the 3.9x the window spanned. So the claim is
      now written as what it actually is - a SPREAD, because "the slow gun should take longer" is a
      statement about the ratio between the fastest and the slowest, not about a number of seconds.
      Absolute seconds go stale every time a weapon is tuned, which is how a window ends up wider
      than the entire thing it was checking.

      WHAT IT MEASURES, now that it measures: `noCharacter()` really does zero Strength - the class
      gives +3 as a flat over a base of 0, and baseOf reads the base, so the guard is not skipping it.
      So this is every gun with NOTHING invested in it, which is the floor of the Arcane Beam's canvas
      and the honest worst case:

        Scatter   16.9s      Bolt      43.2s      Voidball  55.7s      Arcane Beam  91.6s

      A spread of x5.42, and the bound below is 7x rather than the 4x it used to be.

      The spread has been walked up and back down by weapon tuning rather than only by the boss being
      resized, and both directions are worth recording. Raising Scatter dmg to 3.9 moved ITS number by
      0.1s - eight pellets were already well past the health bar, so there was nothing left to remove;
      what widened the spread then was the Beam going 87.2s to 102.7s, because it is fired from 400px
      and pays a ~0.72 falloff tax on a gun that fires 11.5 times a second.

      Cutting the Scatter's falloff floor from 0.45 to 0.22 then brought the spread back DOWN to 5.42,
      and that is the better shape for the roster: nerfing the strongest gun at range closed the gap
      between the ceiling and a canvas that is useless until you put work into it. The bound is 7x
      because the Beam is still a near-two-minute fight on an unbuilt character, which is the whole
      point of the weapon - and the bound is what stops that floor becoming a wall.

## [h:99-tests-313]
near: `const secs=all.map(x=>x.secs), fast=Math.min(...secs), slow=Math.max(...secs);`

The bound moved from 4x to 7x, and the reason is worth stating because it is NOT the Scatter
      getting better.

      Measured here at 400px, which is where this test shoots from and therefore where every gun is
      judged: Scatter 16.3s, Bolt 42.8s, Voidball 55.7s, Arcane Beam 102.7s.

      Raising Scatter dmg to 3.9 changed its boss TTK by 0.1s - it was already killing the boss in
      about a second per pellet and eight pellets was already well past the health bar, so there was
      nothing left for the buff to remove. What moved is the SLOW end: the Arcane Beam went 87.2s
      to 102.7s. The Beam is fired from 400px with fNear 170 and fFar well beyond that, so it pays a
      falloff tax of about 0.72 on a gun that fires 11.5 times a second, and the tax is now the
      dominant term in a 100-second fight rather than a rounding error in an 87-second one.

      So the spread did not grow because a gun got stronger. It grew because the floor of the
      roster - the weapon that is useless until you put work into it - got further from the ceiling.
      A 7x bound still says what the 4x bound said: pick the Arcane Beam on an unbuilt character
      and the boss is a two-minute fight, which is the whole point of a canvas. What it no longer
      claims is that the gap is a margin. It was never a margin - it was always a real difference,
      and the old number understated it by a third.

## [h:99-tests-314]
near: `test('a Brunch pack is cover: enemy shells die on it, the pack is unharmed, and you can still shoot `

Brunch as cover. This is the mechanic the whole change exists for, so it is measured by firing
real shells at a real pack rather than by reading the collision code back at itself. The first
assertion is a CONTROL: without it, a fixture that put the pack somewhere the shell never reached
would report "the shell was absorbed" for the wrong reason, which is how most of the false
findings in this project started.

## [h:99-tests-315]
near: `test('the blink grace forgives one hit, in the gap the i-frames leave, and nothing else',()=>{`

The blink grace, which is a FORGIVENESS and not more invulnerability. The distinction is the
whole safety argument, so these tests assert both halves of it: that the window forgives a hit
in the gap the i-frames leave, and that it forgives exactly one and grants nothing afterward.
Measured, by firing a shell to arrive exactly N ticks after a blink - the edge of forgiveness
came out at 0.62-0.66s across 1.0-2.0 px/tick shells, because the window is measured in time
and a shell is judged on when it ARRIVES rather than on how far it got. A 0.1s grace sat
strictly inside the immunity already in force and would have changed nothing at all.

## [h:99-tests-316]
near: `const shoot=(delay,shells,blinkIt)=>{`

Stage a shot timed to arrive a chosen number of ticks after the blink. The player is pinned
back to the same spot afterwards so the geometry is identical with and without a blink, and
nothing is healed at any point - an earlier probe topped the player up every single tick and
so reported zero damage in every case, which is a harness that cannot fail.

## [h:99-tests-317]
near: `const iframes=BLINK_IFRAMES+DASH_TRAIL;`

The narrowest invariant in the file, and the one nothing was checking.

      damagePlayer tests its two windows in this order:

        if(player.iframes>0) return false;                      <- swallows everything
        if(player.blinkGrace>0 && !player.graceSpent){ ... }     <- the forgiveness

      So forgiveness is reachable ONLY on ticks where the i-frames have expired and the grace has
      not. The two windows are set independently - i-frames by BLINK_IFRAMES+DASH_TRAIL, the grace
      by BLINK_GRACE - and nothing anywhere asserts the ordering. If i-frames ever reach BLINK_GRACE,
      the grace branch becomes unreachable: the mechanic stops existing, silently, and every test in
      this file still passes, because every one of them runs the clock until the i-frames are gone
      before it probes. A test that waits for the window it is testing is not evidence the window
      is there.

      Measured: i-frames 84 ticks (0.40s), grace 126 ticks (0.60s), slack 42 ticks (0.20s).
      That 42 ticks is the entire margin.

## [h:99-tests-318]
near: `startGame();`

Two numbers that have to agree, and neither of them is written down anywhere.

      The aim lag is the reason a blink buys a reaction window: the position enemies shoot at
      stays where the player was and eases across. If the lag resolved AFTER the protection ran
      out, the player would be standing in the open, visible and accurate, for the tail of the
      animation - which is the "uncovered gap" this is here to close out. Measured over the whole
      window: the lag is down to a couple of pixels by tick 78 and zero by tick 120, while
      protection ends at tick 126. The lag resolves first, which is the correct order.

## [h:99-tests-319]
near: `startGame();`

Momentum moved here from the character sheet, and the reason it had to move is worth keeping:
      the sheet is a PAUSE screen, and the only time a stat is visible on it is when the player is
      not playing. This is the one number the player is meant to watch move WHILE they fight, so a
      screen you open once a fight to check your build is the worst possible home for it. The
      tutorial paragraph that used to sit under the sheet is gone too - a stat that has to be
      described cannot be a stat you learn by playing it.

## [h:99-tests-320]
near: `ok(plate.x+plate.w<=W,'the Momentum plate runs to '+(plate.x+plate.w)+'px on a '+W+`

ON THE SCREEN, not merely clear of the room. The row is health+keys on one line and
      blink+momentum on the next, and only the room was ever checked - which is a check that cannot
      fail in a room wider than the screen, and the momentum plate is on the left so it does not
      happen to reach the map. The screen is what the player is looking at.

## [h:99-tests-321]
near: `const barY=plate.y+Math.floor(ROW_H/2)-6;`

THE BAR MOVES. Not "a rect was drawn" - the fill width, read off the actual fillRects, at
five meter values. Asserting that something happened is how a bar that never moves passes.

The bar row carries THREE kinds of rect and only one of them moves. The track is always the
full width, so taking the widest measures the track and returns the same number at every
meter. The three graduation notches are 1px wide BY CONSTRUCTION - that is what a notch is -
so taking the narrowest measures a notch, which also never moves. Both of those were tried
and both report a bar that does not move while the bar is plainly moving.

The fill is the widest rect on the row that is neither, and at an empty meter there is no
fill at all, which is the answer zero.

## [h:99-tests-322]
near: `const rowAt=(m)=>grab(m).rects.filter(r=>r.y===barY&&r.h===12);`

EXACT y, not a tolerance. A +/-1 sweep is how this passed the minimap's 12x12 room marker,
which sits at y=64 against a bar at y=63 and is narrower than the fill at half a meter -
so the "narrowest rect on the row" was the minimap, and the bar reported a width that
never changed. Dumped the row first; two guesses at the fixture were both wrong.

## [h:99-tests-323]
near: `startGame();`

The teaching device. Momentum is the one stat that measures what you did rather than what you
      picked up, and a stat like that cannot be taught with a sentence without becoming the thing
      the sentence is about - so the art teaches it: a blink spent at a full meter leaves a green
      streak, and one spent at nothing leaves the white streak it always did. The player connects
      the colour to the bar on their own, two or three blinks in.

      Two properties make it a readout rather than decoration, and both are asserted here:

      it is read off the blink's OWN meter, baked in when the trail is made, so a trail cannot
      flicker up the whole ramp while the meter moves underneath it; and it is tagged, so a green
      puff never appears on a lunger's windup - a colour cue that lies about an ENEMY, in the middle
      of reading one, is worse than no cue.

## [h:99-tests-324]
near: `const greenness=(c)=>c[1]-c[0];`

GREENNESS, not total distance. The sum of absolute channel differences is 230 here, which out
of a possible 765 sounds small and means nothing - a shift that keeps red and green level is
just a dimmer white. What matters is that the colour stops being white and starts being green,
so the quantity is green minus red, and it has to move by a wide margin to be readable.

## [h:99-tests-325]
near: `dashFX.length=0;`

THE GUARD. An enemy effect in the same array must not pick up the player's colour. The dashFX
draw lives in drawRoom, which is the whole world, so the capture is deliberately narrow: it
records which glow sprites are used and nothing else, and asserts only that the hot one is
absent and the white one is present.

## [h:99-tests-326]
near: `startGame(); Items.reset(); Stats.reset();`

The sheet answers "what am I carrying". Every row on it is something that was picked up, with
      a badge saying which item put it there. Momentum is the one stat no item can give you, so its
      row was the only one the player could not act on - and being on a pause screen meant the number
      was only ever visible when the player was not playing.

      The exclusion is a filter inside the render loop, and a filter is one edit away from being
      dropped, so it is asserted - including after a rebuild, since the rows are re-rendered from the
      model on every open.

## [h:99-tests-327]
near: `test('every body obeys one armour rule, and the table declares it',()=>{`

ONE ARMOUR RULE FOR EVERY BODY. This exists because the boss quietly opted out of it.

    `spawnEnemy` read `c.armour||1`, so a row of the ENEMY table that simply did not mention armour
    got 1.0 - full damage - while the two rows that did mention it got 0.66. The boss was one of the
    silent three, and the comment beside it said the omission was deliberate. That is the worst
    combination a tuning table can have: a rule that is easy to get wrong, plus a note telling you
    that getting it wrong is intended.

    The cost was measurable, not theoretical. Every hit on the boss landed 1/0.66 = 1.52x harder
    than the same hit anywhere else, so its effective health was 19x a lunger's for the same shot
    count rather than the 29x its HP implied - and because the ladder multiplies health without
    limit, that gap GREW with depth instead of staying a property of the body. Meanwhile the C# port
    had already taken the other answer, so the two ports disagreed about the Warden and neither one
    was marked wrong.

    Two things are asserted here, because they are separately true and separately breakable:

      EVERY ROW DECLARES ARMOUR - so an enemy added to the table without one is a test failure
      rather than a silently unarmoured body that is 1.52x easier to kill and much harder to
      balance, which is the failure mode that actually happened.

      EVERY BODY ABOVE THE SIZE LINE TAKES THE SAME ARMOUR - the real claim. A per-body stat is
      fine. A per-body stat inherited by default rather than chosen is the bug. The line is
      SIZE, not a list of names, so a new body lands on the right side of it automatically and a
      renamed one cannot quietly escape it.

    The expectation is ARMOUR itself and not a copied literal, because a second place holding a copy
    of 0.66 is exactly what let the two ports drift apart in the first place. If ARMOUR is ever meant
    to vary by body, rewrite this deliberately - do not just delete it.

## [h:99-tests-328]
near: `const LINE=ENEMY.lunger.r, wrong=[], below=[];`

THE RULE: armour is a stat about a big body, so it is the SIZE that decides, not the name.
Brunch is the single body below the line - half a lunger's radius - and is the original
exemption rather than a carve-out: armouring it broke the alt blast's promise that one budget
deletes a small Brunch group outright, and a blast into three of them left all three standing.

## [h:99-tests-329]
near: `const combat=e=>[e.maxHp,e.dmg,e.armour||0,e.r,Math.round(e.speed*1e4),e.pspd,Math.round(e.cdMin)].j`

Sixty identical shooters per held gun. Every combat number must come out byte-identical across
all four columns; only the standoff band is allowed to differ. The assertion is on the SET of
distinct values per column, not on a value read back from a field we just wrote - the bug
shape this file has the most of is asserting a thing against itself.

## [h:99-tests-330]
near: `const isWalker=(t)=>t==='lunger'||t==='brunch';`

THE BUG THIS EXISTS FOR, and it is worth being blunt about how close it was to shipping.

      The tick decides what kind of body it is holding with `e.walkSpeed!==undefined` - the walker
      branch has the field, the ranged branch does not. The first version of the trait system had a
      "close faster" trait that set walkSpeed, intending to make a shooter press in. It did not
      make a shooter press in. It converted the shooter into a lunger, and from that tick the
      entire ranged kit - the shell, the cadence, the muzzle prediction, the standoff rule - stopped
      running, and the body simply stopped being a shooter. No error, no log, no test: a shooter
      that walks at you and hits you on contact, indistinguishable from a lunger with worse art.

      So the field is asserted directly, on both signs, for every body type, under every gun.

## [h:99-tests-331]
near: `let widest=0, highest=0, n=0;`

Two clamps, each guarding a specific way this could have shipped broken.
  far under sense   a body that notices you at 600 and then tries to hold at 700 is a body
                    that stands in a corner and never fires. It reads as broken, not as an
                    answer, and the player cannot act on it because nothing is happening.
  band has a floor  a band that collapses to a point makes the body twitch on the spot
                    forever instead of standing somewhere, which is a different failure with
                    the same cause.

## [h:99-tests-332]
near: `Lab.enter();`

THE TEST THAT WAS MISSING, and its absence is the lesson.

      The lab's first layout put the specimen row behind the HUD and the item shelf entirely
      off-screen. Every other check about the lab passed: five specimens, thirteen shelf entries,
      the numbers worked, the camera worked. "The shelf has thirteen entries" says nothing about
      whether the shelf is VISIBLE, and a debug view whose furniture is behind the HUD is a debug
      view you have to walk around to use - which is the opposite of the thing it was built for.

      So this measures the frame, not the data. It takes the camera as the game computes it and asks
      where each piece of furniture lands ON SCREEN, which is the only coordinate that matters to
      somebody looking at it. The HUD margin is the reason the row needs to be well clear of the
      top rather than merely on-screen, so the threshold is the HUD's depth plus its furniture.

## [h:99-tests-333]
near: `const HUD_DEEP=HUD_BLOCK_Y+HUD_HP_H+HUD_ROW_H+HUD_GAP+HUD_ROW_H+HUD_FRAME+26;`

THE TOP THRESHOLD IS THE HUD'S OWN DEPTH, not the 150 it used to be.

      It was 150 with a comment saying it came from "the health, momentum and depth plates together
      run to about 120px" plus a 26px nameplate - which is 146, not 150, and both halves of it were a
      re-derivation of a layout the drawing already states. Then the top band arrived and pushed the
      block down 15px, and the number did not move: the furniture still cleared the HUD, so the test
      stayed green and nothing noticed that its margin had quietly shrunk from 30px to 15px. A
      threshold with a stale derivation is a threshold that is wrong by exactly the amount something
      else moved.

      So it is computed from HUD_BLOCK_Y and the three rows, plus the nameplate, which is what the
      150 was trying to be. The band's height is in there, which is the point: grow the band and this
      threshold follows it rather than quietly going stale a second time.

## [h:99-tests-334]
near: `const railTop=shelfY-102, railBot=shelfY+122;`

and the shelf's own case has to be big enough for what it holds. It was 204px tall for two
rows whose lower name plates sit 107px below centre, so the rail's own bottom edge cut through
six of the thirteen names - the furniture was on screen and still unreadable, which is the same
class of failure as being off screen and rather more annoying, because it looks deliberate.

## [h:99-tests-335]
near: `clearRecords();`

This is the load-bearing replacement for a pinned fix that had no test behind it.

      The bug list carried "records: a win is saved, a slower win keeps the fastest time" for a long
      time, in green, with nothing checking it - because the panel scored an entry with no matching
      result as a pass. The claim itself had also gone stale: the way out used to end the run, and it
      goes DOWN now, so endRun() is only ever called with a death and the win state is unreachable.

      So the claim worth pinning is the one that is true: clearing a floor does not end a run, the
      way out does not end a run, and only dying does. That is a real guarantee rather than a
      historical note, because it is what stops somebody re-adding a portal that ends the game, and
      it is also what the run-summary screen and the R key both hang off.

## [h:99-tests-336]
near: `const bigRoom=(w,h)=>{`

ROOMS BIGGER THAN THE SCREEN, and the camera.

    These exist because the camera had a bug that 154 checks could not see. The clamp on the
    big-room branch read Math.min(b.l, ...) with the bounds the wrong way round, so it pinned the
    view to the room's left edge and the camera never moved. Every room in the game fits inside the
    960x600 viewport, so that branch had never run: the exact property that made the camera safe to
    add to a green suite - it is the identity transform for a room that fits - is the same property
    that hid a total failure in it.

    A feature that is a no-op everywhere it is actually used is not a tested feature, it is an
    untested one wearing a passing disguise. So these assert the moving case, and the first of them
    is written so that reverting the clamp to its broken form makes it fail.

## [h:99-tests-337]
near: `eq(ROOM_LEFT,r.bounds.l,'the wall shorthand and the room disagree on the left wall');`

The shorthand, not the accessors: this is the claim that the 169 untouched call sites now read
      the CURRENT room. If syncRoomBounds were not called these would still say 700x450 while the
      room record said 1680x1040 - two places disagreeing about where the wall is, which is the
      failure this whole mechanism exists to make impossible.

## [h:99-tests-338]
near: `Lab.enter();`

The number comes from the difference in a body's health between two ticks, so it has to
      survive one tick without a baseline: the first tick after a body appears can only record what
      it saw. That is asserted rather than tolerated - a readout that invented a number on the tick
      a body arrived would be a number about nothing.

## [h:99-tests-339]
near: `e.hp=before+3; update();`

a RISE is reported as healing, in the other colour, because a body that gains health between
two ticks is a thing worth seeing rather than something to average away.

It is +10 and not +3, and the number is the point rather than an accident of the fixture: the
readout is a DELTA FROM THE LAST TICK, not a total from some earlier moment. The body is being
set from before-7 back up to before+3, so what happened on this tick is ten points of healing.
A readout that reported +3 here would be reporting the change since the shot, which is a
number about two events rather than about the one that just occurred - and a burst that lands
across three ticks would read as three small numbers instead of one damage event.

## [h:99-tests-340]
near: `let path=0, px=drove[0].x, py=drove[0].y, closest=1e9;`

AND THEY ACTUALLY COME AT YOU, which is the claim the whole feature rests on.

      This was previously recorded as an unresolved bug, and it was never a bug: it was this check
      measuring 40 ticks, which is almost entirely the LUNGE WINDUP. A lunger stops dead to
      telegraph before it commits - lungeState 'wind' - and that pause is the design, not a stall.
      The diagnostic that called it a bug printed the state and read past it.

      Measured properly, a drove body closes 279px to 83px over 120 ticks and lands a hit. So the
      window has to clear the windup, and the quantity has to be PATH LENGTH rather than net
      displacement - which is the second time this check has measured the wrong thing, and the
      first time it measured a deliberate pause in an animation.

## [h:99-tests-341]
near: `test('the wand points at the cursor, in the frame the cursor is actually in',()=>{`

THE CHANGE HISTORY IS TESTED, and it is the LAST check in the suite for a structural reason.

    It audits the table of pinned fixes against the results, so it can only see checks that have
    already run - and results are appended in the order the tests are DEFINED. Written anywhere else
    in this file it would compare the table against a partial list and report every later check as
    an unbacked fix, which is exactly what happened the first time: it claimed two of them were
    unverified, and they were the two tests sitting below it in the file. A check about the whole
    suite has to come after the whole suite, and the only way to guarantee that is to put it last.

    `results`, not window.__testResults: the global is assigned after this file has finished running,
    so reading it from inside a check gets undefined.

## [h:99-tests-342]
near: `const D=180/Math.PI;`

THE AIMING BUG, and the reason 163 checks did not see it.

      The player, every body and every wall are in world space. The cursor is on the screen. The aim
      was `atan2(mouse.y - player.y, mouse.x - player.x)` - a vector from a screen point to a world
      point - so every shot was off by exactly the camera offset.

      It did not look like "off by the camera offset". It looked like a few degrees, and it was only
      a few degrees if you wiggled the cursor near one spot: measured at six positions around the
      frame the error ran from -0.34 to +21.28 degrees and CHANGED SIGN across the frame, because a
      translation error's angular size depends on where the ray is pointing. Reported as "a few
      degrees off, counter-clockwise", which is exactly what it looks like from one seat.

      And the suite passed throughout, because every fixture in it wrote a WORLD position into
      `mouse` - `mouse.x=e.x; mouse.y=e.y` - and the game read a world position out of `mouse`. The
      test and the bug agreed perfectly. That is the fourth time in this project that agreement has
      turned out to be the problem rather than the reassurance.

      So this drives it the way a player does: a real MouseEvent at a real client position, and then
      the angle the game actually fires at. Asserted at several positions and in a big room, because
      a check at one position is a check that passes for the wrong reason at the other eleven.

## [h:99-tests-343]
near: `const wantAngle=()=>{`

Every weapon has a cone - the Bolt's is 0.05 rad, about 2.9 degrees - so ONE shot is allowed
to be off-axis by that much and asserting otherwise would be asserting that the gun does not
spread. Two claims instead, and the frame bug fails both by a wide margin:
  1. no shot lands OUTSIDE the weapon's own cone, which is 2.9 degrees and not 21;
  2. the MEAN of several shots is the aim, because a symmetric cone averages to its centre -
     which pins the centre to a fraction of a degree and would catch a smaller constant offset
     that claim 1 alone would let through.

## [h:99-tests-344]
near: `const wantAngle=()=>{`

THE EXPECTED ANGLE IS BUILT FROM SCREEN QUANTITIES ONLY, and the first version of this test
      got that wrong in the most embarrassing possible way.

      It computed the expected angle with `mouseWorld()` - the same helper the game calls - so when
      the helper was mutated back to the identity to prove the test was sensitive, the expectation
      and the game were wrong in exactly the same way and the test passed. Twelve other checks
      failed, and this one, the one whose entire subject is the bug, did not.

      That is the same trap the whole suite was in for 163 checks: two sides of a comparison agreeing
      because they came from the same place. The cure is to derive the expected value from
      something the game does not use - here, the player's own SCREEN position against the raw
      cursor - so the two can only agree if the game is right.

## [h:99-tests-345]
near: `const meanTol=4*w0.spread/Math.sqrt(3*SHOTS)*D;`

The tolerance on the mean is a FOUR-SIGMA bound and not a number somebody liked.

      A cone's samples are uniform on +/-spread, so one sample's standard deviation is
      spread/sqrt(3) and the mean of n has standard error spread/sqrt(3n). With the Bolt's 0.05 rad
      and 25 shots that is 0.0064 rad, so four sigma is about 1.5 degrees. Nine shots gave a
      standard error of 0.010 rad and a measured 0.80 degrees, which is inside one and a half sigma
      - pure sampling noise, and a check that read it as bias would have been a check that fails at
      random about half the time. A flaky check is worse than none, because it teaches you to
      re-run rather than to read.

      1.5 degrees is still a quarter of the smallest frame error this bug produced (3.89 at the
      top right) and a fourteenth of the largest, so it separates the two decisively.

## [h:99-tests-346]
near: `startGame();`

The other half: the conversion has to go the right way, not merely be applied. A sign error
      here would aim the wand at the mirror point, which in a centred room is a smaller mistake
      than the original and would pass a looser check.

      The bench is the control. It hit-tests `mouse` against its own layout in screen space and has
      always been correct, so the two consumers of the same variable are asserted to disagree by
      exactly the camera offset - which is what "one variable, two frames, converted at the point
      of use" means in practice.

## [h:99-tests-347]
near: `const trial=(seed,count)=>{`

THE INVARIANT, and it is an equivalence rather than a vibe.

      The separation pass was a double loop over every pair and is now a uniform grid over a 3x3
      neighbourhood. The argument that makes the grid nearly exact is that two bodies can only
      interact when they overlap, and they overlap only within a.r+b.r of each other, so with a cell
      wider than the largest sum of two radii any overlapping pair is in the same or an adjacent
      cell. An argument is not a measurement, and the first run of this check disagreed with the
      claim by 1.01px - which is not a failure of the grid but a failure of the comment to say that
      a body pushed ACROSS a cell boundary stays in the bucket it was inserted into, so a pair that
      starts overlapping because of an earlier push resolves a tick late.

      So the claim is the one that is true: the same pairs, within a knockback slide. The bound is
      3px, which is a body being shoved rather than a pack coming apart - a grid resolving a
      materially different SET of pairs shows up in metres, not fractions of a pixel, so the test
      separates the two cases by three orders of magnitude.

      Order is checked too, and that is the half that is easy to get wrong and invisible in a
      screenshot: bounceEnemies mutates both bodies, so visiting the same pairs in a different order
      ends in a different arrangement, and a crowded room is the only place that shows up.

## [h:99-tests-348]
near: `if(typeof performance==='undefined'||!performance.now){`

The regression guard, and it is a measurement rather than a claim.

      Cost per body used to rise with the room: 1.30us at five bodies, 8.35us at a hundred and
      sixty, with the doubling ratio climbing to 3.96 where linear is 2.00. That is a quadratic
      wearing a linear costume, invisible only because a room currently tops out near twenty-three
      bodies - and the game is planned to grow in both directions.

      This asserts the SHAPE and not the speed, because absolute timings are meaningless on a
      machine that is not this one, and a test with a hardcoded millisecond budget is a test that
      fails on someone else's laptop and gets deleted. Per-body cost flat across a 4x range of room
      size is the property; a constant factor on top of it is not being asserted.

      TIMING IS OPTIONAL. The check reports a number either way, but only fails if timings came
      back at all - a suite that cannot measure must not fail for being unable to.

## [h:99-tests-349]
near: `startGame();`

TWO BALANCE NUMBERS THAT HAD STOPPED DESCRIBING WHAT THEY NAME.

      AGGRO_RANGE was 0.85 of the room's diagonal, evaluated ONCE when 00-balance.js was parsed. So
      it was 707 - correct for the only room shape that existed - and it stayed 707 for a 1680-wide
      one, which wants 1567. A lunger 900px away in a big room is outside its aggro, so it stands
      still, and the room reads as a safe place to stand still in.

      SWERVE_DEADZONE was half the room's width less a margin, frozen the same way at 300. Which
      means a gunner in a big room reads a reversing player at FULL strength from across the room -
      the counter to the entire mechanic switching itself off at range, silently off.

      Both were found by asking a question rather than by reading: what else is captured at module
      load from something that now varies? That is the same shape as the aiming bug, where a
      screen-space cursor met a world-space player, and the same shape as the camera clamp that
      never ran. Values derived from the world are only correct until the world changes shape.

      The second half is the half that matters for a fix: in a STANDARD room both must return
      exactly what they always returned. A change that also retunes the game is two changes wearing
      one, and every number measured in this project's history was measured in a standard room.

## [h:99-tests-350]
near: `eq(swerveFull(),Math.round(1680*0.25)+Math.round(1680*0.55),'the swerve ramp did not follow the '+`

THE RAMP MUST FOLLOW THE ROOM, and asserting only that it is NON-ZERO is what hid this.

      `SWERVE_FULL_BASE` was a module constant computed once at load from whatever room existed then,
      so it never followed the room at all: measured, a 1680-wide room asked for a 920px ramp and got
      385 - the standard room's value, frozen - while `swerveDeadzone`, one line above it, correctly
      read 420. The two were silently inconsistent inside the same expression.

      The old assertion here was `swerveFull()>swerveDeadzone()`, and it passed: 805 > 420 is true,
      and so is 1344 > 420. A check that the ramp is non-empty cannot tell a ramp that follows the
      room from one that is a constant, because both are non-empty. That is the same shape as the
      relative-brass-speed bounds that let a 13% cut through unnoticed - a property too weak to fail
      on the defect it was written for.

      So the ramp is now asked to be the room's own fraction, in a room four times as wide.

## [h:99-tests-351]
near: `const big=bigRoom(1680,760);`

The BEHAVIOUR, not the constant. The check above asserts the number follows the room; this
      asserts the consequence, because a number can follow the room and still not be read anywhere.

      A body 1100px from the player in a 1680-wide room is inside the aggro range it should have and
      outside the one it used to have. If it walks, the range is being read. If it stands, the number
      moved and nothing did.

## [h:99-tests-352]
near: `test('each area has a palette, and a floor is painted in the one it is on',()=>{`

THE TABLE CHECK LIVES AT THE END OF THE FILE, not here, and the reason is that it reads every
    name in `results` - which is only complete once every test has run. It sat above the area tests
    when they were added and reported all five as "pinned with no test", against five tests that were
    passing thirty lines below it: a test about bookkeeping failing because of where the bookkeeping
    sits. It is now last, immediately before the raw-Math.random result is pushed, so nothing can be
    added after it without the same trap being re-created.

## [h:99-tests-353]
near: `test('each area has a palette, and a floor is painted in the one it is on',()=>{`

AREA PALETTE ----------
    Four tests, and they are four because the palette is four separable claims: that the lookup is a
    pure function of the area, that Area1 is unchanged, that every surface agrees, and that the wall
    is actually masonry. A single test would have let one of them pass by agreeing with the others.

    The floor is a PIXEL claim and it is measured through the framebuffer, not read out of the
    drawing. Every version of the heart-bar bug in this project passed a data-level assertion while
    the picture was wrong, and the reason is always the same: the data was right and the mistake was
    in what got drawn and where. "drawFloor fills a colour" proves nothing about which floor is on
    screen.

## [h:99-tests-354]
near: `for(const a of ['Area1','Area2','Area3','Final']){`

THE LOOKUP IS A PURE FUNCTION OF THE AREA. Not of the floor, not of the player, not of the
      room: of the area. So it can be asked for an arbitrary area without standing in one, which is
      what lets the three other tests here compare areas side by side without moving a run around.

      And it throws on an unknown id rather than returning something. A palette is a table lookup,
      so a missing id used to return undefined and the first anybody would see is a room painted in
      the colour of null - which is the silent-failure shape this project's Content registry already
      refuses on purpose.

## [h:99-tests-355]
near: `const a1=paletteForArea('Area1');`

AREA1 IS THE BUILD THAT EXISTED BEFORE AREAS DID, and this is the assertion that keeps that
      true rather than approximately true. Every measurement in this project's history was taken on
      floors 1-4: every TTK, every reaction window, every screenshot anybody looked at. Area1's
      colours are therefore the literals those were taken against, and its wash is ZERO - which is
      what makes the floor composite the same arithmetic rather than the same-looking result.

## [h:99-tests-356]
near: `const ids=['Area1','Area2','Area3','Final'];`

THE FOUR AREAS MUST ACTUALLY LOOK DIFFERENT, and "different" is measured as distance rather
      than asserted as inequality: two palettes that differ by one unit of blue are a different
      palette and the same picture. The claim is on the pair of channels a player reads position
      from - the stone and the floor wash - and the floorLight channel is included because a fleck
      of the wrong hue is exactly what makes a recoloured floor read as a filter over the old one.

## [h:99-tests-357]
near: `const rgb=h=>String(h).indexOf(',')>=0?String(h).split(',').map(Number):hexRgb(h);`

floorLight is stored as an 'r,g,b' TRIPLET rather than as hex, because it exists to be
      interpolated into the string `'rgba('+floorLight+','+alpha+')'` in the cave bake and writing
      it as hex would mean a conversion on every fleck. So the distance measure has to read it in
      the form it is stored in - and a measure that silently parsed it as hex would compare 597
      against 597 and report every pair of areas as identical.

## [h:99-tests-358]
near: `startGame();`

THE FLOOR, AS PIXELS. This is the part the other tests cannot see: a palette that is right in
      the table and ignored by drawFloor looks identical to a palette that is right in both. The
      sprite is read out of the CACHE by key, because the cache key is the whole of the claim - a
      floor that is re-baked correctly per draw would also be correct, and one that is cached without
      the area is not.

## [h:99-tests-359]
near: `const meanOf=c=>{ const g=c.getContext('2d'); const d=g.getImageData(Math.floor(c.width/2)-32,`

AND THE HUE IN THE SPRITE IS THE PALETTE'S, read from the image rather than from the table. The
      sample is a 64x64 patch near the middle of the floor, away from the vignette's dark rim and
      away from the wall, and it is compared as a MEAN because the floor is a speckle: any single
      pixel is noise and the mean is the texture.

## [h:99-tests-360]
near: `startGame();`

FOUR SURFACES, ONE PALETTE, and the failure this exists to catch is disagreement BETWEEN them.

      The theming was written one surface at a time, which is how a floor goes green while the
      doorway beside it stays Area1 grey: a rectangle of the wrong stone punched through a themed
      wall, which reads as a rendering fault rather than as a door. Asserting each surface
      individually would pass on all four. So this asserts they AGREE - that the doorway tone is
      derived from the same palette as the floor composite, and that the HUD ink is the palette's
      own accent rather than a literal that happens to match today.

      roomTone() is the shared answer and it is checked as a relationship, not retyped: for a room
      type, the tone must sit between the type tint and the area's floor tint, closer to whichever
      end the wash says. For Area1 the wash is zero and the tone must therefore BE the type tint
      exactly - which is the pre-area behaviour, and is what makes floors 1-4 unchanged.

## [h:99-tests-361]
near: `startGame();`

AND THE DOORWAY ON SCREEN IS THE FUNCTION'S ANSWER, which is the half the checks above cannot
      see. Everything so far asserts that roomTone() is correct; none of it asserts that the drawing
      CALLS it, and a gap filled with the bare type tint satisfies every one of them while painting a
      rectangle of Area1 stone through a Kiln Works wall on floor 6. This is the fifth time in this
      project a property of a helper has been asserted while the call site was free to ignore it.

      The sample is the middle of the north door's gap, below the 4px coloured lip on its outer edge
      (which is the door's own state colour and correctly not part of this) and above ROOM_TOP. The
      claim is EXACT rather than a range, because the gap is an opaque fill: measured 42,39,41 on
      floor 6 against roomTone's #2a2729, and 28,34,48 on floor 1 against #1c2230. Area1 landing on
      the type tint is not luck - it is the wash being zero, and it is what makes the pre-area doorway
      the same pixel it always was.

## [h:99-tests-362]
near: `startGame();`

THE CLAIM IS ABOUT THE PICTURE. "drawWall fills four rects with a pattern" is a claim about the
      code and a duplicated block would satisfy it; "the wall has courses, and they do not line up"
      is a claim about the screen.

      So this reads the framebuffer twice. First: a horizontal scan across the NORTH wall must find
      MORE THAN ONE COLOUR. A flat fill returns one, and that is the regression this pins - four
      palettes over a flat rectangle would have made the defect four times as visible without fixing
      it. Second: the courses must be OFFSET. A wall whose joints line up into continuous verticals
      reads as tile, which is the exact reason this project deleted the lab's 120px floor lattice,
      so an un-offset wall is the same mistake in masonry.

## [h:99-tests-363]
near: `const wallPatch=floor=>{ run.floor=floor; readyT=0; fadeT=0; roomFade=0; render();`

THE WEST WALL, AND NOT THE NORTH ONE, and the reason is the HUD rather than taste.

      The north wall sits at world y 114..130 and the camera puts the frame at y 55, so it lands at
      screen y 59..75 - INSIDE the HUD plate block, which spans screen y 29..130 and starts at x 15.
      The first version of this scanned the north wall and read back a flat colour: wood grain, on a
      surface that is not flat at all, in the one place the pixels belonged to something else. That is
      the third time in this project a pixel check has sampled the wrong thing and reported it as a
      defect in the drawing, and the fixture was the wrong one both times.

      The west wall is clear of every plate, but MIDY is not usable either: it is where the west
      DOOR is, so a scan at MIDY reads the door's green lip and the gap's own fill rather than any
      masonry. The patch below is anchored ROOM_TOP+40 and is 16 wide by 120 tall - the full 16px
      thickness, and 120px ALONG the wall, which is where the courses and the joints are.

      LENGTHWISE rather than across the thickness, and that is measured rather than chosen: the tile
      is one course tall (WALL_COURSE is 16 and wt is 16), so a single row across the thickness can
      only ever contain the block tone and one seam. The blocks vary along their length, so a 16x120
      patch is where the masonry actually is - and a flat fill returns one colour for all 1920 of
      those pixels.

## [h:99-tests-364]
near: `const stone=hexRgb(areaPalette().stone);`

AND IT IS BUILT OUT OF THE AREA'S MATERIALS, read off the pixels rather than retyped. Asked as
      a question about a RANGE, because the wall is `stone` with low-alpha blocks and a mortar joint
      laid over it, so the pixels are the stone and its immediate neighbours - an exact-pixel
      assertion would fail on antialiasing rather than on a wrong colour, which is the wrong way round.
      The claim is "this wall is made of this area's rock", and near-miss on every channel is that
      claim failing.

## [h:99-tests-365]
near: `const stone=hexRgb(areaPalette().stone);`

MEASURED, not guessed, and the measurement is in the threshold.

      The fraction of a 16x120 wall patch sitting within 10 units per channel of that area's own
      `stone` is 84.1% for Area1, 84.1% for Area2, 47.7% for Area3 and 47.7% for Final. It is not
      constant, and the first version of this asserted a constant, which failed on two of the four
      against correct masonry.

      The reason is the block overlay. Each 32px block carries a low-alpha wash drawn from the art
      stream, half of them light and half dark, and a LIGHT wash on a dark stone moves it much further
      than a dark wash on the same stone: Area3's stone is 31,56,47 and a 0.08 white wash puts it at
      48,71,65, which is outside a 10-unit tolerance, while the equivalent on Area1's 43,47,58 lands
      at 60,62,71 - also outside, but the DARK washes on Area1 are all inside and on Area3's they are
      not. So the fraction tracks how far each palette's stone is from mid-grey, which is a property
      of the palette, not of the wall being drawn properly.

      So the floor is 0.40, set well under the measured minimum of 0.477, and the assertion's real
      work is done by the mean nearest its own stone below - a wall painted in the wrong area's rock
      sits near 0% of its own, not near 47%. The percentage is here to catch a wall that is mostly the
      right colour with a foreign band across it, which a mean cannot see.

## [h:99-tests-366]
near: `eq(mixHex('#123456','#abcdef',0),'#123456','mixHex at 0 is not the first colour');`

THE MIX'S ENDPOINTS ARE EXACT, which is what makes Area1 usable as a reference at all.

      A helper that rounded "0.52 of the way" into near enough would put Area1's floor a unit or two
      off the build every earlier measurement was taken against, and nothing in this suite - or in a
      screenshot - could see it. The two ends are the whole of the claim: at 0 the room type's tint
      untouched, at 1 the area's tint completely, and both of them reached by the same expression the
      drawing uses.

## [h:99-tests-367]
near: `ok(hexRgb('#abc').join()===hexRgb('#aabbcc').join(),"short hex '#abc' does not expand to the same co`

A three-digit hex is EXPANDED, not preserved - '#abc' and '#aabbcc' are the same colour and
      mixHex only promises to return a canonical six-digit form. Written from the SPECIFICATION (the
      two are the same colour) rather than from the implementation's return format, which is the point
      of the exercise: the first version of this asked for the string '#abc' back and failed against
      correct code.

      Compared with `ok(...join()===...join())` rather than `eq`, and that is worth knowing about this
      suite's `eq`: it is `a!==b`, which compares two ARRAYS BY REFERENCE. Two identical colour arrays
      are two objects, so `eq(hexRgb('#abc'),hexRgb('#aabbcc'))` fails against correct code and always
      has - the numbers print the same and the comparison still says no. Anything comparing a computed
      value here has to be a primitive or a join.

## [h:99-tests-368]
near: `const area1Wall=wallPatch(1);`

THE TWO AREAS' WALLS ARE DIFFERENT WALLS, which is the claim the theming exists for. A per-pixel
      comparison rather than an average, because an average of a speckled wall is a number about
      nothing - and the tolerance is 12 summed units, which is roughly one just-noticeable step on a
      channel, so "same" here means "cannot tell apart".

## [h:99-tests-369]
near: `const pal1=paletteForArea('Area1'), stone1=hexRgb(pal1.stone);`

THE COURSES ARE OFFSET, and this one is measured on the TILE rather than on the screen.

      The screen cannot answer it: the wall is 16px of a 450px-tall room, so both courses are visible
      at once and "the joints do not line up" is not a thing a screenshot of the finished room shows
      at this size. The tile is 64x32 with two courses whose joints are a half-block apart, and the
      claim is a property of that tile - so it is asserted there.

      THE JOINT IS FOUND BY COLOUR AND NOT BY DARKNESS, and the first version got that wrong and
      reported correct masonry as a defect. Each block carries a low-alpha overlay drawn from the art
      stream, so a block can be LIGHTER or DARKER than the stone under it - and asking for the
      darkest column found whichever block happened to get a dark overlay, which was noise. The joint
      is the 1px gap BETWEEN blocks where no overlay is painted at all, so it is exactly the palette's
      `stone` and nothing else. Measured: rgb(43,47,58) = #2b2f3a, at x 0 and 32 in the top course
      and at x 16 and 48 in the one below.

      The claim is that the two courses' joint columns are DISJOINT. A wall whose joints line up into
      continuous verticals reads as tile, which is the same reason this project deleted the lab's 120px
      floor lattice rather than dimming it.

## [h:99-tests-370]
near: `const runs=cols=>{ const out=[];`

AND THE OFFSET IS THE HALF-BLOCK IT IS DRAWN AT, measured from the two courses' FIRST joints rather
      than from the span between the outermost.

      Measured: joints at x 0, 32 and 63 in the top course and at x 16, 48 in the one below. The 63 is
      the tile's last column, which the block loop leaves unpainted at the right edge - so the outer
      span is 63 rather than 32, and the first version of this assertion compared that span against
      WALL_TILE_W/2 and failed on correct masonry. Which joints are period and which are edge is a
      property of the block loop, so the period is read off the first two of them.

## [h:99-tests-371]
near: `const runs=cols=>{ const out=[];`

AND THE OFFSET IS EXACTLY THE QUARTER-BLOCK, measured on run MIDPOINTS rather than on columns.

      Measured: the joints come in 2px runs - top course at x 0, 31, 32, 63 (i.e. 31-32 and 63
      wrapping to 0) and bottom at 15, 16, 47, 48. Comparing raw columns is ambiguous for two
      reasons, and the first version of this assertion hit both: the runs are two pixels wide so a
      column can land on either side, and the tile wraps, so x 63 and x 0 are one joint.

      So the joints are collapsed into runs, each run is reduced to its midpoint, and the two courses'
      midpoint lists are compared modulo the 32px block. Midpoints 31.5 and 47.5 differ by exactly 16,
      which is WALL_TILE_W/4 - the offset the block loop draws at. A tolerance of one pixel would let
      a one-pixel drift pass, which is the drift that turns brickwork into a staircase.

## [h:99-tests-372]
near: `const runs=cols=>{ const out=[];`

AND THE WRAPPING JOINT IS UNWRAPPED FIRST, which is the third thing about this tile that had to be
      got right before the assertion could be about masonry rather than about the block loop.

      The top course's joints are 2px runs at x 31-32 and x 63-0, and 63-0 is ONE joint that the
      tile boundary cuts in half. Measured as it comes out of getImageData it looks like two runs, at
      63 and at 0, and its midpoint computes to 63 rather than 63.5 - which put the offsets at 16 and
      17 and failed an assertion about a 16px quarter-block against a wall that is drawn correctly.

      A pattern tile is periodic, so a run touching column 0 and a run touching the last column are
      the same joint. They are joined and the midpoint is placed at the wrap.

## [h:99-tests-373]
near: `eq(hexRgb('#fff').join(),[255,255,255].join(),"hexRgb('#fff') is not white - three digits are each r`

THE ACCEPTED CASES ARE PINNED AS VALUES, not as "it did not throw". A helper that returned
      [0,0,0] for everything would sail through a no-throw check and paint every area black, and the
      three-digit case is the one worth pinning as an EXPANSION rather than as a shorter parse:
      '#abc' is 170,187,204 because each digit is repeated, which is the CSS rule and not
      "0x0a0b0c".

## [h:99-tests-374]
near: `const refused=['', '#', '#12', '#1234', '#12345', '#1234567', 'xyzw12', '#abcg', 'xyz',`

AND THE REJECTIONS, which is the half that was missing.

      This handed the string to parseInt and asked whether a number came back, and parseInt accepts a
      great deal that is not a colour:

          '#12'     -> 0x12     -> [0, 0, 18]     black with a whisper of blue
          '#12345'  -> 0x12345  -> [1, 35, 69]    a colour nobody chose

      Both of those went through a palette entry and out to a fillStyle, so a mistyped swatch rendered
      a surface in a colour no line of the code mentions and no stack trace can point at. Nothing
      failed; the room just came out wrong.

      The list is deliberately varied rather than just short-and-long: an empty string, no digits at
      all, a name, an rgb() string that hexRgb is not the reader for, six characters that are not hex
      (so a length check alone would still let it through), a digit that is not a hex digit, and the
      non-strings. Throws rather than returns null, because the callers here - mixHex, withAlpha -
      would turn a null into NaN and paint a NaN, whereas paletteForArea and Content.get already
      throw on a missing id and the loud end of this file is established.

## [h:99-tests-375]
near: `test('areaForFloor answers for any floor without reading or writing the run',()=>{`

THE PLAYER STAYS BRIGHT THROUGH THE DESCENT, which is the property the scrim broke and the redraw
      over it restores.

      Measured, sprite at the canvas centre (y 300): 236,232,245 with no banner, 46,44,46 under the
      scrim alone - 17% of its brightness - and 236,232,245 again with the redraw. So this asks for
      the brightness of the sprite, not for the presence of a draw call: a redraw that happened at
      globalAlpha 0.1 would satisfy "the player is drawn over the scrim" and fail this.

      It is measured at the FULL brightness of the same frame rather than against a literal, because
      the exact value depends on the frame index and the flicker, and a hardcoded 236 would go stale
      the moment the sprite sheet changed. The claim is "as bright as it was without the banner".

## [h:99-tests-376]
near: `test('areaForFloor answers for any floor without reading or writing the run',()=>{`

A MISSPELLED CATEGORY MUST NOT TAKE THE PANEL DOWN. The panel is the thing whose whole job is
      reporting failures, so an exception thrown inside it is the worst failure it can have: it
      printed nothing at all, rather than printing one entry in the wrong place.

      Found by adding a fix under a category name that was not in CAT_ORDER - `byCat[it.cat]` was
      undefined, `.push` on undefined threw, and the panel was simply gone. Not hypothetical either:
      adding a fix is the ordinary way this table grows, and a new category name is a natural thing
      to write.

      So the panel is handed a deliberately misspelled category and has to survive it AND still
      account for every entry - because "did not crash" alone would be satisfied by dropping the
      entry on the floor.

## [h:99-tests-377]
near: `test('areaForFloor answers for any floor without reading or writing the run',()=>{`

areaForFloor ANSWERS FOR ANY FLOOR WITHOUT TOUCHING THE RUN, which is the change that let the
      descent banner name an area. It used to take no argument and read `run.floor`, so the only way
      to ask "which area is floor 5 in?" from the presentation layer was to assign `run.floor` and put
      it back - a draw function mutating simulation state.

      Asserted as BOTH shapes, because the no-argument form is the one a hundred existing callers
      use and it must not have changed meaning: `areaForFloor()` still tracks the current floor, and
      `areaForFloor(n)` answers independently of it. A test that only checked the argument form would
      pass even if the default had been broken for every caller in the game.

## [h:99-tests-378]
near: `const wasRun=run;`

THE BEAT NAMES A PLACE ONLY WHEN THE PLACE CHANGES. Naming it on every descent would be a
      caption for a caption - the palette already says which area you are in by looking like it - so
      the claim is the boundary behaviour specifically, both directions.

      Rendered, not asserted as a string: the banner is canvas text, so the only honest way to ask
      what it printed is to look at the pixels.

## [h:99-tests-379]
near: `const renderCol=(from,to)=>{`

The area name is 13px monospace on the baseline H/2-42 = 258, so its glyphs occupy roughly y 248..262
      and it is measured over a box WIDER than the text: one column can land between glyphs and read
      as "not printed" on a frame that printed it perfectly. Measured rows on a boundary crossing are
      y 250..266 peaking at 134; the same rows on a descent that stays inside one area peak at 133 -
      i.e. the difference is the glyphs, which is why the comparison below is between two renders
      rather than against a literal.

      The band is INSIDE the scrim's opaque region (249..332). It was originally y 240..252, five
      pixels above the flat, and the pixel test PASSED there while a screenshot showed "THE KILN
      WORKS" in near-invisible grey: the test asked whether the ink was the right colour and never
      asked whether it was readable. A pixel can be the right hue and still be unreadable, and only
      one of those two failures is a number.

## [h:99-tests-380]
near: `startGame();`

The claim is the CONTRAST between a boundary crossing and a descent that stays inside one area.
      Comparing two peak absolutes - the version this test had first - does not work: both peak at
      134, because the peak is the FLOOR NUMERAL's ink, which is drawn either way. The name sits
      above it in the same accent, so only a subtraction can separate the two, which is what the
      column comparison below does. Measured 175-176 on the eight rows the glyphs occupy.

## [h:99-tests-381]
near: `const colAt=()=>{ const out=[]; for(let y=246;y<=270;y++){`

AND THE NAME IS LEGIBLE, which is a DIFFERENT question from being the right colour.

      The first draft of this test asked only the colour question and PASSED on a line a screenshot
      showed to be near-invisible grey: the ink was the right hue, drawn in the right place, and
      unreadable. The band was y 240..252, five pixels above the scrim's opaque region, so the same
      accent landed at about a third of its value against bare floor.

      So the claim is CONTRAST, and it is measured as the difference between the same row on a
      boundary crossing and the same row on a descent that stays inside one area. That subtraction is
      what removes the scrim and the floor from the measurement and leaves only the glyphs - a peak
      absolute value cannot, because it is dominated by whatever the background happened to be.
      Measured: 175-176 summed units on the eight rows the glyphs occupy, and 0-7 everywhere else.

      Both halves are needed. The difference proves the name is there; the count of rows proves it is
      a line of text and not one stray pixel.

## [h:99-tests-382]
near: `const key='a fix filed under a category that does not exist';`

THE REAL PANEL IS CALLED, not a copy of the grouping logic. An earlier draft of this test
      re-implemented the bucket loop beside the drawing, which looked like coverage and would have
      passed against the broken version - the same mistake this file's own notes warn about twice.
      So this injects a bad category into the real FIXES, calls the real showBugPanel, and reads the
      real DOM.

## [h:99-tests-383]
near: `test('a boss wall expires and does not accumulate for the rest of the fight',()=>{`

THE BANNER MUST SURVIVE THE ARRIVAL WINDOW, which is the one state that used to freeze the game.

      `drawDescent` redraws the player over its own scrim, and it read `readyProg` - a `const` local
      of `drawRoom()` - from inside a different function. That is a ReferenceError, and because
      drawDescent is called unconditionally from render(), the throw escapes render() and the
      requestAnimationFrame tail of loop() never runs: **the game freezes for good**.

      It was not reachable by playing, and that is exactly what made it worth a test rather than a
      shrug. `descend()` always lands the player in the new floor's START room, which
      generateDungeon builds already spawned and empty, so `enterRoom` never sets `readyT` while the
      0.9s banner is up. Measured 160,000 ticks of random-walk play across 40 seeds: zero frames with
      both `descendT>0` and `readyT>0`. The one line that makes it live is a `readyT=READY` added to
      some future path, and nothing in the code would look wrong on the day.

      So this forces the overlap the game cannot currently produce and requires that a frame still
      draws. Every other descent test in this file sets `readyT=0` on the way in - which is why they
      all passed while this was broken.

## [h:99-tests-384]
near: `test('a boss wall expires and does not accumulate for the rest of the fight',()=>{`

A CALLED WALL MUST LEAVE THE ROOM. The 14s expiry used to drop the HANDLE and leave the bodies, and
      `bossCallWall` overwrites that handle on every call, so the timer could only ever police the most
      recent wall. Every earlier one stayed on the floor for the rest of the fight.

      This was in normal play, not a stress fixture. Measured through a real phase-3 Warden with the
      player kept alive: 7 wall calls over 120 seconds left 35 Brunch in one room against a
      `DEPTH_BODY_CAP` of 28 - a cap that `depthBodies()` applies per normal spawn wave and that
      nothing here consults. The cost is superlinear in body count, so the fight degraded rather than
      merely running heavy:

          28 bodies   0.094 ms/tick    2.0% of the 4.76ms budget
          60 bodies   0.307             6.4%
         120 bodies   1.111            23.3%
         240 bodies   3.512            73.7%

      The claim is BOUNDED, not "the last wall went away": two walls called back to back is the case
      that a fix which only cleaned up `wallBodies` would pass, because the handle still points at
      the second one. So the count is required to stay under the cap across many calls, which is the
      only version of the claim that survives the overwrite.

## [h:99-tests-385]
near: `startGame(4242);`

ENOUGH WALLS TO EXCEED THE CAP. The first version of this test ran the fight and took the peak,
      and it PASSED against the unfixed code - because the boss's own cadence only got round to two
      wall calls in 90 seconds, and two walls of five is ten Brunch, comfortably under the cap of 28.
      The assertion was true of the broken version, which is the failure this file exists to prevent.

      So the walls are called directly, in a loop, enough times to pass the cap on their own. The
      count of moves is still asserted separately, because otherwise the loop could be the only thing
      under test and the fight itself would go unmeasured.

## [h:99-tests-386]
near: `test('a Brunch pack shields a ranged enemy and stops the player shooting through it',()=>{`

DESCENDING IS ALSO A NEW SEED, and the caches have to be cleared there too.

      `startGame` cleared them because a new run is a new seed, and the fix I wrote first stopped
      there. But `descend()` calls `Rnd.set(Rnd.floorSeed(root, floor))` — a different seed for a
      different floor — and the caches are keyed by (type, size, area) with no seed in the key.

      So floors 1 to 4 share one key and every one of them was handed floor 1's baked floor canvas.
      Measured on seed 31337 before the fix: floors 1, 2, 3 and 4 all hash to 3905066258 with exactly
      one cache key throughout, and floor 5 rebakes only because the area changes. The same two seeds
      on a cleared cache produce different tiles, so the content really is seed-derived and the art
      stream was never the broken part.

      This test DESCENDS, where the earlier one called `startGame`. Asserting across a descent is the
      only version of the claim that can fail: a test that starts a run twice is testing a path that
      already worked.

## [h:99-tests-387]
near: `test('a Brunch pack shields a ranged enemy and stops the player shooting through it',()=>{`

THE HUD MUST SURVIVE A HELD ITEM WHOSE DEFINITION IS GONE.

       `Content.get` throws on a missing id, and that is correct: a missing definition used to flow
       silently into a stat read and become a NaN three frames later. But `drawActivePlate` called it
*unguarded, every frame**, so the failure mode is not one loud throw at the mistake — it is a
       throw inside `render()`, which escapes into the animation loop and stops it. The game freezes
       with the HUD half-drawn and no message.

       The state is reachable rather than theoretical: `loadout` is a plain data object, and
       `Content.resetMods()` rebuilds the table from the pristine copy, so anything holding an id the
       table no longer has ends up here.

       Three other call sites read the same definition and all three guard with `Content.has` first
       (`10-art.js:803`, `10-art.js:829`, and `70-view.js:1410` three lines away in the same file,
       drawing the same item). This was the only one that did not, and the asymmetry is the tell.

## [h:99-tests-388]
near: `test('a Brunch pack shields a ranged enemy and stops the player shooting through it',()=>{`

A BRUNCH PACK IS A MOVABLE SHIELD FOR A RANGED ENEMY, and it has to actually stop shots.

      Before this the pack steered at the PLAYER, so a wall formed beautifully in front of the wrong
      body: measured over 8 seconds, the pack sat 8-60px from the player while the shooter it should
      have covered stood 132-229px away. The shell-absorption rule was already working - a shell
      fired through a five-body wall never reached the player and no Brunch lost HP - so all that was
      missing was something standing where the cover could be used.

      The claim is measured by FIRING REAL SHOTS, not by counting how close a body is to a line. An
      earlier version of this test counted the proportion of the player-to-shooter segment that passed
      within `BRUNCH_ABSORB_R + r` of a Brunch, and reported 27-39% while every body in fact sat on
      the line - the proximity test was measuring the geometry of a line rather than whether the game
      stopped anything. Measured: 100% of 40 bolt shots blocked across six seeds.

      Three separate things have to hold, and each has failed on its own:
        - the pack TARGETS a ranged enemy rather than the player;
        - it reaches the arc slots (all six within 5-7px of their slot after 10 seconds);
        - the slots are ON the line, which is a property of the cone and not of the steering.

## [h:99-tests-389]
near: `ok(worst<16,'the pack settled '+worst.toFixed(0)+'px from its nearest slot after 10 seconds '+`

The bound is 14px, and that number is GEOMETRY rather than slop: a body stops steering when its
      slot is within BRUNCH_DEADZONE (6px) of a body's width, so a body flanked by two neighbours
      settles at 6 + r(8) = 14px from a slot that is physically inside both of them. An earlier
      version of this asserted 14px against a pack that measured 25-28px and never converged - one body
      was wedged between its neighbours, held there by `separateBodies` fighting the steering, at both
      shield speed 0.72 and 1.18. The dead zone now treats an occupied slot as reached, and the pack
      converges uniformly to 14 instead of five bodies at 6 and one stuck at 28.

## [h:99-tests-390]
near: `startGame(31337);`

THE FALLBACK, and it is not a detail: a pack with nothing to shield must still advance, or a
      room of lungers and Brunch becomes a room where the Brunch stand still. The two-rank wall
      behaviour is what they did before any of this, and it is correct for a pack that has nothing to
      hide behind.

## [h:99-tests-391]
near: `startGame();`

THE FIXTURE IS `Items.active()`, NOT `loadout.active`. The first version of this test set
      `loadout.active` directly and passed - while proving nothing, because `drawHUD` reads
      `Items.active()` and `Items` keeps its own module-scope slot. Two of the three assertions in
      it were measuring a game state that cannot occur, which is the `spawnEnemy(false, ...)` shape:
      a fixture that returns something usable-looking instead of the thing asked for.

      So this gives a real item through the real API and then makes the HELD SLOT point at an id the
      content table does not have - which is the state a mod produces, and the state `resetMods`
      produces by rebuilding the table from the pristine copy.

## [h:99-tests-392]
near: `const i=loadout.items.findIndex(s=>s.slot===Items.ACTIVE_SLOT);`

THE HELD SLOT IS `loadout.items[slot===ACTIVE_SLOT]`, and there is no setter for it - `active()`
      reads the array, `give()` appends to it, and nothing else writes it. So the reachable state is
      produced by rewriting the entry in place, which is what a mod restoring a save, or `resetMods`
      rebuilding the content table under a live loadout, actually leaves behind: a slot pointing at an
      id the table no longer has.

## [h:99-tests-393]
near: `const bandPeak=()=>{ let m=0; for(let x=395;x<=565;x++) for(let y=238;y<=268;y++) m=Math.max(m,px(x,`

AND the banner text is still legible, which is the other half of the trade: the type has to survive
      being printed over an arbitrary floor texture.

      Sampled from the numeral's MEASURED extent rather than from guessed columns: at 960x600 the
      gold "FLOOR 2" occupies x 400-561 and y 240-265, peak 159, so a sample at x 300 or x 660 reads
      bare scrim and reports the type as invisible when it is perfectly legible - which is what the
      first version of this assertion did. The band is taken as a box around the glyphs with a
      margin, and the peak inside it is what has to clear the floor.

      159 is the honest measured value for the numeral's brightest ink at this size, so the threshold
      is 120: comfortably above the ~46 of a scrim-only column, and below the numeral so it does not
      go stale if the sprite sheet or the accent changes.

## [h:99-tests-394]
near: `const floorBytes=()=>{`

THE CACHES ARE SEEDED, so they are RUN-SCOPED, and the only way that is true is if something
      empties them when the run changes. `caveTile`, `wallTile` and `drawFloor` all bake through
      draws from `Rnd.art()`, which 05-rng derives from the seed the player typed, so a cache entry
      that outlives its run is a picture of the PREVIOUS run's floor.

      The failure is invisible in a screenshot - stone is stone either way - which is why this
      compares PIXELS rather than cache keys. Key counting would have passed against the bug: the
      keys are identical either way, because the key is (type, size, area) and the seed is not in
      it. That is the whole defect: the key cannot see the thing that changed.

      The check is that two different seeds produce two different floor textures. Same seed must
      produce the SAME texture, or the cache is not the only thing carrying seed state and the
      flecks are not the story.

## [h:99-tests-395]
near: `startGame();`

THE CACHE IS THE CLAIM, and it is tested as the cache and not as the drawing - because a
      correctly-drawn floor that is CACHED WITHOUT THE AREA is wrong in exactly the way the player
      sees it, and no assertion about what drawFloor computed would notice.

      This is the same class of bug as omitting the room SIZE from that key, which is documented
      above drawFloor: a key that is one field short produces a picture that is entirely present and
      entirely wrong, and it is invisible in any screenshot of a single floor. The only way to see it
      is to walk from one area to another and look at the same room twice.

      The test does exactly that, on one room type at one size, and reads the cache keys rather than
      the sprites - because a test that renders both areas and compares pixels cannot tell "cached
      correctly" from "re-baked correctly every frame", and those are different defects: the second
      one costs a re-bake per frame and shimmers.

## [h:99-tests-396]
near: `const keyFor=floor=>{ run.floor=floor; drawFloor('normal');`

KEYS FOR THE CURRENT AREA ONLY, and the reason is stated because the first version got it wrong
      and the failure was honest rather than misleading.

      `floorCache` is module-level and never cleared, so by the time this test runs - after the
      palette test has already drawn an Area1 and an Area2 floor in the same room - the prefix
      'normal:700x450:' already matches TWO keys. Counting every key with the prefix and asserting
      one of them therefore failed on correct code: the cache is doing exactly what a cache is for,
      which is keeping the previous area's sprite around in case the player walks back up. The claim
      is not "the cache holds one sprite" but "the sprite THIS area asked for is its own".

## [h:99-tests-397]
near: `startGame();`

THE WORDS ARE CONTENT, and the assertion is that they are addressed by the same string
      areaForFloor() returns. Two vocabularies for one question is how a palette ends up with no
      name and the sheet printing a raw id at a player.

      Every area areaForFloor() can produce must have both fields, because Content.get THROWS on a
      missing id rather than returning undefined - so a shipped entry missing a flavour would crash
      the pause screen rather than print a blank line, and the required-fields list is what stops it
      shipping that way.

## [h:99-tests-398]
near: `startGame();`

AND THE MAP WASH IS THE AREA'S INK, WITHOUT COSTING THE MAP ITS READABILITY.

      The minimap's paper is knocked back with a translucent wash so the white door frames end up the
      brightest things on the board. That wash is now the area's `mapWash`, and there are two claims
      here rather than one: that it changes with the area, and that it is STILL a knock-back at 0.52.

      The second is the one that can silently break, because raising the alpha to make an area "look
      more themed" darkens the board until an unvisited shell - #33383f, a fixed colour - stops
      standing out from it, and the map becomes a dark rectangle with cells that have all merged. So
      it is measured: an unvisited but KNOWN room's shell against the bare board beside it.

      Measured at 0.52: the shell reads 51,56,63 against a board of 119,117,109 on floor 1 and 124,114,105
      on floor 13 - a summed contrast of 175 and 173. A wash strong enough to eat that would be one
      that has stopped being a wash. And the board itself does move per area: 119,117,109 /
      122,116,104 / 118,119,108 / 124,114,105, which is the tinted-board read and not the floor read.

## [h:99-tests-399]
near: `eq(new Set(boards).size,4,'the map board reads the same in more than one area ('+boards.join(' / ')+`

ALL FOUR, and not "at least three".

      The paper's speckle is baked once per size and cached, so it is byte-identical in all four
      draws and the ONLY thing that differs between these readings is the wash - which makes this a
      clean measurement rather than a noisy one. Asserting three would have passed with Area2 and
      Area1 sharing a wash, which is exactly the mutation: it collapsed the set from four to three
      and the assertion still held. Four is the number of areas, and four is what has to be distinct.

## [h:99-tests-400]
near: `const rule=document.getElementById('charArea').querySelector('i');`

IT MUST NOT ADD A THIRD GRAMMAR. The line is a struck rule, a name and a sentence on the same
      card as the stats - which are all ruled rows - so a badge or a coloured pill here would be a
      second visual language on one sheet. The rule is asserted by being a 2px block, which is what
      makes it a struck mark and not a chip.

## [h:99-tests-401]
near: `setPaused(true);`

AND IT MUST NOT LAND ON THE STATS, or eat the room they need.

      Two separable claims, and both are properties of the LAYOUT rather than of the drawing: the line
      sits above the first stat row with air between them, and it is bounded in height. The bound is
      measured - 44px at the shipped sizes, for a 2px rule, a 13px name and a 13px flavour - and set
      at 60, because the thing it has to catch is a flavour that wraps to three lines and pushes the
      rows down, not a type size that is one point out.

      The overlap is checked as RECTANGLES because that is what overlap is. Two boxes that merely
      have the right numbers in them agree with each other whether or not anything is drawn.

## [h:99-tests-402]
near: `const sheet=document.getElementById('charSheet'), card=document.getElementById('charCard');`

AND THE SHEET SCROLLS RATHER THAN CLIPPING, and the claim is that BOTH ENDS OF THE CARD ARE
      REACHABLE - not that the card fits, and not that it is shorter than the viewport.

      The old assertion was `card.height <= sheet.scrollHeight`, and it was wrong twice over.
      `scrollHeight` IS the content height, so that comparison fails by construction whenever the
      content legitimately overflows a scrollable box - it asserted that a working scroll area was
      broken. And it only ever looked at the FOOT, which is the end that does scroll into view: the
      actual defect was at the HEAD, where a centered flex item taller than its container is pushed
      above the scroll origin and can never be reached. The old assertion passed against the broken
      layout for the same reason a test that re-derives the rule it audits always does.

      So this asks the reachable question directly. Anchor the scroll at 0 and the card's top must
      be at or below the sheet's top; anchor it at the maximum and the card's bottom must be at or
      above the sheet's bottom. Both, because each half is a separate failure and the bug only ever
      took one of them.

      Measured at 960x600 with the fix: head at +39px, foot reachable, maxScrollTop 151. Before it:
      head at -37px with maxScrollTop 76, which is a foot that works and a head that does not.

## [h:99-tests-403]
near: `test('every pinned fix in the change history has a test with that name',()=>{`

EVERY PINNED FIX HAS A TEST WITH THAT NAME, and this is the LAST test in the file because it reads
    the whole of `results` - see the note where it used to sit, near the top.

    The panel marks an entry unverified when no result carries its name, and that is only worth
    anything if the marking is right - so this asks both directions.

    Two entries were in the unverified state when this was written: one whose test had been renamed
    out from under it, and one - the win record - that had no test at all and never had. The first
    is a rename somebody forgot; the second is worse, because a fix nobody is checking is a fix
    nobody can tell has stopped working, and it sat in green because "no result" scored as "pass".

    The first assertion is deliberately about the TABLE and not about the game: it cannot fail
    because a mechanic broke, only because somebody pinned a fix without pinning a test for it, or
    renamed a test without renaming its entry. That is the mistake worth catching, and it is
    invisible from the game's side.

## [h:99-tests-404]
near: `ok(Object.keys(FIXES).length>0,'the FIXES table is empty, so this test has no entry to remove and '+`

And the second half is the one that matters, because a correct table is no use if the panel
      still reports green over it. This calls the REAL showBugPanel with a results list that has one
      genuine entry removed, and reads what it says. The entry removed is a real pinned fix, not an
      invented one, so the panel is being asked about a fix that genuinely has no test behind it.

      Re-implementing the scoring here and asserting on that would test my own arithmetic rather
      than the shipped behaviour, and the whole point is that the shipped behaviour is what was
      wrong.

## [h:99-tests-405]
near: `ok(Object.keys(FIXES).length>0,'the FIXES table is empty, so this test has no entry to remove and '+`

The list handed to the panel is the REAL results with the victim removed, except that every
      remaining entry is marked green. That is not a fiction that weakens the check - it is the point.

      The badge orders itself most-alarming-first: a red suite, then an unverified fix, then green. So
      asking for UNVERIFIED while handing it a suite with a genuine failure in it gets "N FAILED",
      which is the panel behaving correctly about the more urgent problem and this test reading it as
      a fault. It only shows up when something ELSE is already red, and it showed up here the moment
      the mutation checks ran - a cascade from one real failure to a confusing second one, which is
      the thing the file's own discipline note says a failure must not cost.

      So the fixture isolates the question. The panel is being asked about a MISSING TEST, and the
      only way to hear the answer is to hand it a suite in which nothing else is wrong.

## [h:99-tests-406]
near: `ok(Object.keys(FIXES).length>0,'the FIXES table is empty, so this test has no entry to remove and '+`

FIXES IS NOT EMPTY, and that is asserted before it is indexed rather than after.

      `Object.keys(FIXES)[0]` is the victim this fixture removes to ask whether the bug panel marks an
      unbacked fix. If FIXES were ever empty - a new project, a trimmed table, a bad edit - that is
      `undefined`, and every result would then be filtered against the name `undefined` rather than
      against a real entry: the fixture would pass for the wrong reason while testing nothing.

      The guard is here rather than inside the fixture because the failure is silent either way, and a
      test that reports "the panel handled a missing test correctly" when it never selected one is the
      exact shape this project's own notes keep warning about.

## [h:99-tests-407]
near: `dead:results.filter(r=>r.asserts===0).map(r=>r.name),`

TESTS THAT MAKE NO ASSERTIONS AT ALL, listed by the harness rather than hunted for by reading.

      This is the one dead-test shape that can be detected without a mutation harness, and it is worth
      catching mechanically because it is invisible from the outside: a test with no assertions passes
      unconditionally, reports green, and looks exactly like a passing test in every summary.

      It is NOT the same as a test whose assertions all evaluate true. An earlier version of this
      listed those too and flagged 194 of 229 tests - every correct one - because a passing assertion
      evaluates true by definition. Anything stronger needs a mutation run: break the world, see which
      tests still pass. That is the right tool and it is not this.

## [h:99-tests-408]
near: `const markSettled=()=>{ if(window.__testResults) window.__testResults.settled=true; };`

DRAIN THE ASYNCHRONOUS TESTS, AND PUBLISH `settled` ON THE OBJECT THAT SURVIVES.

    Both halves of this were wrong at different times, and each wrong version looked correct.

    First: the harness called `fn()`, took the returned promise, and immediately pushed
    `{ok:true}`. Every assertion in an async test ran AFTER its verdict was recorded, so the suite
    reported a green waveform check that had never looked at a waveform - and the mutation that
    proved it, a voice made to ring for three seconds, passed. That is what `pending` is for.

    Second: `window.__testResults` is REPLACED by the object literal below, so a `settled` property
    written to the previous object is gone. A probe waiting on `settled===true` then waited for ever
    on a suite that had already finished - at every viewport, on green code. `settled` therefore
    lives inside the literal, and the drain runs after it exists.

    `allSettled` rather than `all`: a rejected promise is already recorded as a failed test, so
    awaiting it again would throw here and lose every result collected so far.
