# 30-enemies.js — moved comments

Long comments moved out of `src/30-enemies.js`. The code keeps a one-line gist tagged `[h:30-enemies-N]`; search this file for that tag.

## [h:30-enemies-1]
near: `const TRAIT_HOLD=1, TRAIT_CLOSE=2;`

30-enemies  -  spawning a body, and everything it thinks

  Includes the lunge, the only committed attack in the game and the only mechanic whose entire
  quality IS the quality of its prediction. solveIntercept is the reference implementation for the
  two other things that aim at a moving player; the gunner solves the same problem with three extra
  terms, and both now iterate fourteen times, because a shell is only 1.8x the player's speed and
  three passes leave thirty pixels of miss.

## [h:30-enemies-2]
near: `const TRAIT_HOLD=1, TRAIT_CLOSE=2;`

BUILD-READS: a body that answers the gun the player is holding. VARIATION, NEVER DIFFICULTY.

  The rule that governs this file is one sentence, and it is the depth-only rule pointed the other
  way: a trait may change WHERE a fight happens and may never change HOW HARD it is. The ladder is
  the only thing in the game permitted to raise a number, and nothing in this file is allowed to
  read it. If a trait made a body tougher, the floor would get harder for the player who happened to
  pick the wrong gun, which is the exact failure the depth rule exists to prevent.

  So there is no trait here that touches health, damage, speed, cadence, radius, accuracy or aim
  error. Every one of them is a change to a body GEOMETRY, and the test that guards this file
  asserts it: spawn forty of the same body under each of the four guns and check that every
  combat number is byte-identical across the four columns. The only columns allowed to differ are
  the ones describing where the body stands.

  THE MECHANISM, and why it is the only one. A ranged body holds a standoff and derives it fresh
  every tick:

      standoff = far - (far-close)*roomPress*PRESSURE_CLOSURE
      inside close  -> walk TOWARD the player
      outside far   -> walk AWAY from the player

  Both `far` and `close` are per-body and set once at spawn, so shifting them moves the whole band.
  Shifting BOTH by the same factor is the only version of this that is safe: the band keeps its
  width, a body does not start dithering, and the change is a place rather than a personality.

  A WEAKNESS ANSWERED, per gun, and the pairing is the whole design:

    Scatter    a knife-range gun: 8 small pellets, fNear 80, fFar 300. What it wants is to be
               standing next to something. TRAIT_HOLD answers it by standing further out than the
               gun is worth, so the room it works in stops being the room the player is in. This is
               the strongest answer in the game and it is given the highest weight, because the
               falloff is the most punishing in the roster and there is nothing subtle about it.

    Beam       a mid-range gun: fastest cadence, widest cone, and falloff that bites past its own
               fFar. What it wants is bodies at mid range, held on a line. TRAIT_HOLD answers it
               the same way for a different reason - not the knife, but the middle, where its own
               falloff starts costing it more than the Scatter's.

    Bolt       one committed shot on a 38-tick cooldown, aimed at where a body was. What it wants
    Voidball   is a target that holds still enough to be worth that wait. TRAIT_CLOSE answers both
               by walking IN, which is the worst thing that can happen to a gun with a long reload:
               the player has to hold a moving body at knife range on a cadence that was chosen for
               a target across the room. Two of the four guns are single-target, so they get the
               same answer, and that is the honest answer rather than a coincidence - both are
               aimed at a single body, so both are answered by a body that is not standing where
               it was aimed.

  The player is never told which gun a room dislikes. A visible counter is a puzzle with the answer
  printed on it; an invisible one is a fight the player solves by noticing which gun keeps working.
  What IS visible is the consequence, because the player has to be able to act on it: a body that
  holds further out is standing further out, and that is on screen every second it happens.

  A trait is rolled ONCE, at spawn, from the weapon the player is actually holding, and written
  onto the body. Reading it from a global every tick would mean a body changed its mind the
  instant the player swapped guns mid-room, and the player would be fighting two different fights
  in one corridor with nothing to tell them apart.

## [h:30-enemies-3]
near: `const TRAIT_TABLE={`

Which answer a gun gets, and how reliably. The weight is NOT difficulty - it is how reliably the
  answer works, which decides how many bodies in a room take it. The ceiling is what stops a stack:
  a room of TRAIT_HOLD bodies must still all stand in roughly the same place rather than drifting
  further out every time the trait is applied.

## [h:30-enemies-4]
near: `const TRAIT_FAR_CEIL=0.78, TRAIT_FAR_FLOOR=90, TRAIT_BAND_MIN=40;`

Where the shifted band is allowed to land. `far` is capped under `sense` because a body that
  notices you at 600 and then tries to hold at 700 is a body that stands in a corner and never
  fires, which reads as broken rather than as an answer. The lower bound keeps the band from
  collapsing into a point, which would make the body twitch on the spot instead of standing.

## [h:30-enemies-5]
near: `function applyTrait(e){`

Applied to RANGED bodies only, and the restriction is not a preference - it is the bug this
  shipped once. The tick decides what kind of body it is holding with `e.walkSpeed!==undefined`
  (see 60-tick.js, and the note there about that test having cost a boss phase). A walker is the
  branch that has `walkSpeed`. So writing `walkSpeed` onto a body that lacked it - which the first
  version of the shield trait did, to "make it close faster" - does not make a shooter close
  faster. It silently converts it into a lunger, its gun and its whole ranged kit never run again,
  and nothing anywhere says why. Never add or remove `walkSpeed` here. Only `far` and `close`.

  Returns the trait it applied, so the suite can assert a trait is not silently a no-op.

## [h:30-enemies-6]
near: `e.flank=FLANK_CURSOR;`

Flank slot. Walked round the circle by the golden angle, so no two bodies ever share a slot
    however many have spawned - and NOT randomised, because two lungers drawing the same angle out
    of a bag is a front half the time. The angle is fixed for the body's life: a lunger that
    changed its mind about which side of the player it wanted would weave through its own pack on
    the way, which reads as indecision rather than as pressure.

## [h:30-enemies-7]
near: `e.cdMin=c.cdMin/PRESSURE.rate/depthRate(); e.cdVar=c.cdVar/PRESSURE.rate/depthRate();`

depthRate() divides as well as multiplies. A tougher floor is not only a tougher body, it is a
body that answers sooner, and the cadence is the part a player actually feels - a deep floor
where the shells arrive at the same rate is a deep floor that plays like a shallow one with
more health on the bar.

## [h:30-enemies-8]
near: `applyTrait(e);`

The trait is rolled ONCE, here, and written onto the body. On the ranged path specifically:
the walker path returned before this point, and it must keep returning before it, because a
trait is a thing a body WITH A STANDOFF BAND can do. See applyTrait for why that is not a
preference.

## [h:30-enemies-9]
near: `function solveIntercept(e,gx,gy){`

The lunger's whole attack. Four states, and the transitions between them are the fight.
  This is deliberately a function of the body and not of a timer, so a stunned, slowed or knocked
  body stops mid-lunge and resumes from where it was rather than snapping back to "walking at you".
  A committed attack the player can interrupt by hitting the lunger once is a different weapon from
  one that always plays out on schedule, and the interruptible one is the one worth having.

## [h:30-enemies-10]
near: `function solveIntercept(e,gx,gy){`

Where a body leaving a lunger's position, at lunge speed and after a windup it spends PLANTED,
  can first reach a player who is already moving at their current velocity - solved, not guessed.

  The old lunge aimed at the player's position plus a lead worth about thirty pixels, and stopped
  at a hundred and sixty six. A player running in a straight line away from a lunger settles about
  a hundred and thirty six pixels out, and from there the shot that actually catches them is two
  hundred and ninety four long. So the old lunge fell one hundred and twenty eight pixels short
  every time it was used: not a hard attack that can be read and beaten, but one that could not
  land at all. That is why running in a straight line was free, and it was arithmetic rather than
  feel - the numbers are in the lunge's own comment history.

  It has to be iterated because the time to contact and the distance to be covered each depend on
  the other. Three passes converge: the second is already within a pixel and the third moves
  nothing measurable.

## [h:30-enemies-11]
near: `const conf=1-player.swerve;`

THE BELIEF IS A DISPLACEMENT, NOT A HEADING, and that is the whole correction.

    Both earlier versions of this asked "which way is the player going" and answered it with a
    direction: a long velocity EMA with 0.34s of memory, which lags a human reversal; and then a
    quick smoothed heading, which fixed the lag and kept the deeper mistake. Measured at the real
    lunge cadence, both aim BACKWARDS at a counter-strafing player 33-50% of the time, which is the
    thing that was reported as "it lunges the opposite way".

    The mistake is asking the question at all. A player who reverses every quarter second NET travels
    almost nothing over a lunge's horizon - windup plus flight is about half a second, two full
    reversal cycles. There is no direction to lead along, because their displacement over the time
    the lunge spends in the air is near zero and points wherever their phase happened to be. So the
    honest prediction is

        settled, holding a line   their real displacement, because they really are going there
        reversing continuously     no lead at all, because they are not going anywhere

    and the transition between those two is exactly what SWERVE already measures. The old code had
    both the wrong model and a floor that let a third of a wrong-direction lead survive. Here the
    belief is a DISPLACEMENT in pixels scaled by how settled the player looks, so an unsettled
    player's belief collapses toward zero and the solve aims at where they are - which is the
    correct answer for somebody who is not going anywhere, and which no heading can express.

    A heading is still read, because a line-holder needs one and because a player who has only just
    started moving has no displacement at all yet. It is only ever used to give that displacement a
    DIRECTION.

## [h:30-enemies-12]
near: `const conf=1-player.swerve;`

HOW MUCH OF THE BELIEVED DISPLACEMENT IS REAL. It runs all the way to zero.

    It used to stop at LUNGE_CONF_MIN (0.30), which was the other half of the bug: even a player who
    had convinced the lunger they were going nowhere still got a third of a full lead thrown along
    the last direction the lunger believed, and when that direction was stale the lunge went the
    wrong way. A floor on confidence is only safe when the thing being scaled is a velocity, because
    then a wrong direction still points roughly at the player. Scaled as a displacement it is not
    safe at all, so the floor had to go when the model did.

    At zero the solve aims at the player's current position, which is the correct answer for a
    continuous reverser and costs them nothing they had not already given up by never settling.

## [h:30-enemies-13]
near: `if(room.enemies.length>1){`

THE STANDOFF, which is what makes this mechanic a mechanic. A lunger that simply walks at
        the player ends up touching them - its approach speed is faster than theirs at range - and
        then "lunges" from zero distance, where no read is worth anything because there is nothing
        left to dodge. Every measurement of this attack came out a hundred percent for that reason,
        and not because the prediction was any good.

        So it holds at LUNGE_HOLD, the way the gunner holds inside its close/far band, and the lunge
        is what crosses the gap. That makes it a real committed attack: the line is drawn across open
        floor, the player has room to answer it, and the answer is a change of heading rather than a
        hope. It gives the kite back too - a body that never closes the last seventy pixels cannot
        be walked into.

        Inside the hold it brakes rather than reversing. Reversing was the first version and it
        backed five lungers into each other while they ringed a stationary player, which the
        separation pass then had to fight. A brake stops the body at the line and leaves spacing to
        the pass that already owns it.
     /* SPREAD, on the same two lines of code. Every lunger used to steer at the player's exact
        position, so a pack arrived as a single front: one line, one angle, one threat to read. Each
        body now carries a flank angle, assigned at spawn and spread around the circle by the golden
        angle, and it walks that angle around the player.

        The walk is PURELY ANGULAR - a step along the tangent, in the direction that reduces the gap
        between the body's current bearing and its slot, at a fixed size. Three earlier versions
        failed here and all three were the same mistake in different clothes:

          steering at a fixed point on a ring, only while closing, so the instant a body crossed the
            hold line and began braking, the steering went to zero with it - the pack still arrived
            with its tightest pair FOUR DEGREES apart, which is a front;
          scaling the slide by the closing speed, which is likewise zero inside the hold - ELEVEN
            degrees, still a front;
          steering at "the point at MY distance, at my slot's angle", which is tangential only
            while the bearing means something. At eight pixels the tangent is indistinguishable
            from straight at the player, so the slide became a closing force and the pack collapsed
            onto the target - measured, eight pixels.

        A step along the tangent cannot become radial at any distance. Distance is the standoff's
        job; the angle is this one's.

        And it only runs when there is more than one of them. A lone lunger circling to its slot
        before it commits makes the FIRST lunge of a fight late for no reason - there is nothing to
        be spread out from - and it measured worse on its own, badly enough to fail two tests: the
        lunge it set up after circling was much harder to answer than one set up coming straight in.
        Spacing is a pack problem, so it is solved as one.

## [h:30-enemies-14]
near: `const sol=solveIntercept(e,player.x-e.x,player.y-e.y);`

The commitment is made HERE, at the start of the windup, and never revised. What it aims
          at is the INTERCEPT - not the player, and not a short lead in their direction. The answer
          to "where will you be" is where you are going, and the whole mechanic is in that:

            running in a straight line   you are already standing on the solution, so the lunge
                                        arrives. Indifference is what kills you.
            changing your heading        you step off a line that was drawn for the old one. The
                                        lunger is committed to a line you are no longer on, and
                                        it goes past you like a train.

          The length is the solution as well, so it arrives exactly rather than approximately, and
          it is capped: a lunger whose solution is out of reach does not commit, it keeps closing.
          That is what lets it wait. A player who is far away and running gets closed on at the
          approach speed until the solution fits inside the reach, and only then is the lunge set
          up - so distance is never the reason the attack does not happen, only the reason it has
          not started.

          And the drawn line shows the SOLUTION, not the player, which is the tell that matters. A
          line that points past you is a line that will hit you if you keep going.

## [h:30-enemies-15]
near: `if(--e.lungeChargeFx<=0){ e.lungeChargeFx=sec(0.06); dashFX.push({x:e.x,y:e.y,life:LUNGE_CHARGE_TRAI`

No stretching, and no re-aiming. There used to be both: the length used to ease outward
toward the player while it charged, as a stand-in for aiming properly. Solving the
intercept makes both unnecessary - the length is already the distance that lands, so there
is nothing left to correct for, and a tell that grows after it has been drawn is a lie
about the one number the player is being asked to react to.

## [h:30-enemies-16]
near: `for(const o of room.enemies){`

Two lungers on the same line meet mid-charge and both come off worse. This is the player's
        reward for it: a lunge is aimed at a position, so two bodies reading the same player at the
        same instant are aimed at the same point, and a player who puts themselves between them gets
        a gap and a stunned attacker for free.

        It is checked here rather than left to the separation pass because separation only pushes
        overlapping bodies apart gently, and a lunge moves at nearly four times the player's speed -
        gentle is not enough, and a lunge that phase-straight-throughs a body it is aiming past is
        the one case where two bodies being in the same place should cost something. Both go into
        recover, because neither has the ground under its charge any more.

## [h:30-enemies-17]
near: `syncRoomBounds();`

THE CURRENT ROOM CHANGED, so the wall shorthand has to be re-synced before anything in this
    function or the tick reads a size belonging to the room the player just left. It goes first,
    immediately after `cur`, because everything below - the spawn margins, the secret pickup at
    MIDX, the wave - reads those numbers, and a spawn placed by the previous room's dimensions is
    the kind of bug that only shows up in the first oversized room and then never again.

## [h:30-enemies-18]
near: `const held=Content.all('item').filter(id=>Items.equipped(id));`

THE ITEM HALF OF THE LOOT POOL, which did not exist until now.

        Thirteen items have been defined, validated and wired to the character sheet since the
        framework was built, and NOT ONE of them could be picked up, because there was no `item`
        pickup kind anywhere in the game. The roster was the only content in the project that had
        no way to reach a player - which means "playtest the items" was not possible at all, and
        every number in the roster was a guess about how a thing feels rather than a measurement.

        The machinery was already here and unused: `Items.pool` picks N ids at a rarity and
        `Items.rollRarity` is what Luck feeds. They were called from nowhere.

        What the player already holds is excluded, and that is load-bearing rather than tidy. A
        passive cannot be taken twice - `Items.give` returns false for one - so a room offering two
        copies of Heavy Hands would show two pickups that one of them silently cannot take. Worse,
        for a playtest it would look like a broken item rather than a full one. The pool is asked
        for what is not held first, and only falls back to the whole table if that runs dry, so the
        room always offers something takeable.

        PROVISIONAL. Two items beside the existing two weapons, laid out as a row of four, is a
        layout chosen for legibility and for getting the roster into a player's hands quickly -
        not a balance decision. One item per floor, or two on alternate floors, is the question
        this raises and it should be answered from play rather than from here.

## [h:30-enemies-19]
near: `if(uncleared&&!r.armed){`

Walking into a live room hands back everything you spent getting there, so the corridor between
fights costs you nothing and a fight always opens with the whole kit. r.armed is the whole
anti-exploit: the refill is a one-shot per room, so backing out of a room you have not cleared
and stepping back in cannot farm it, and a room that is already quiet has nothing to arm you for.

## [h:30-enemies-20]
near: `const heldBlink=player.blinkCharges+player.blinkRegen/BLINK_RECHARGE;`

THE BLINK BAR FILLS ACROSS THE ARRIVAL, and nothing else about the refill is animated.

      The weapons are restored instantly and silently. They were getting rings, and a ring on a bar
      that has already snapped to full is decoration at best: the player saw the bar jump and the
      ring arrived afterwards to announce something that had already happened. The cooldown is
      simply there when they next look at it.

      The blink is different because its value is a CONTINUUM the player is used to watching. A
      half-spent blink snapping to two full charges reads as the game taking something away and
      giving it back in the same frame, which is why it looked wrong even after the fade bug was
      fixed. So the blink bar animates: it starts at exactly what it was before the door and fills
      to full across the whole arrival.

      Timed to end on RESTORE_FX_SPAN, which is the READY window - the same number of ticks the fade
      takes. So the bar reaches full on the exact tick the player regains control, which is the only
      moment the full value is of any use to them. They never get a fraction of a second in which
      the bar says one thing and the game does another.

      The charges are NOT held back and filled over time, and that direction matters: if the real
      value were behind the drawing, a player could reach the end of a corridor with no blink
      because they walked through the door four ticks early, and the generosity would quietly
      depend on frame timing - interruptible, losable, impossible to reason about.

      So the real value is always the true one and the animation can never make it a lie. For the
      length of the arrival the bar shows what was spent while the player is actually holding both,
      so the one thing it ever under-reports is a gift already in their hands - and the instant they
      can act, it is exactly true. Nothing about the fill can be interrupted, spammed or lost.

## [h:30-enemies-21]
near: `player.blinkCharges=Math.min(2,Math.floor(heldBlink));`

and the drawn value starts HERE rather than on the next tick, so the bar is already showing
what the player walked in with during the frame the door finishes opening. Setting it in the
animation instead showed a full bar for one frame first, which is exactly the snap the whole
change was made to remove - just one frame later.
