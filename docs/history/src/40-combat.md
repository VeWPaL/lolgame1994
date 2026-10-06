# 40-combat.js — moved comments

Long comments moved out of `src/40-combat.js`. The code keeps a one-line gist tagged `[h:40-combat-N]`; search this file for that tag.

## [h:40-combat-1]
near: `function clampPlayer(){`

40-combat  -  the player, the guns, and the hook

  The gunner intercept, the cast tell, the hold-still-while-charging rule, the hook's resistance
  curve, the blast pool. Pure simulation.

  Note the target of the gunner intercept: it aims at the player's HITBOX, ten pixels below the
  sprite origin, because playerHit tests that circle. A lunger must NOT use that target - it steers
  a body and would visibly drift low - but a projectile has no excuse for missing it.

## [h:40-combat-2]
near: `if(player.iframes>0) return false;`

BLINK GRACE, and it is a SEPARATE window rather than more i-frames.

    The two are different things and merging them is the trap. I-frames are invulnerability: nothing
    connects at all. The grace is forgiveness: the hit is allowed to LAND, and is then handed back.
    That distinction is the whole safety argument - a longer i-frame window would make a blink a
    0.6s teleport through anything, whereas a grace that forgives one hit leaves the player exposed
    to the second one, which is exactly the situation it was built for.

    Only ONE hit is forgiven per blink. Without that, a grace window in a room of four gunners is
    four free hits and the escape stops being an escape.

    It also has to be checked and cleared here rather than in the tick, because the moment that
    matters is the moment something connects - a grace that expires on a timer would silently run out
    during a lull and then fail to save the hit the player actually needed it for.

## [h:40-combat-3]
near: `return false;`

No i-frames are granted here, and that is the load-bearing decision rather than an omission.
The forgone hit is treated as a dodge, not as a hit, so it earns none of the post-hit
immunity a real hit grants. Setting IFRAMES here would have turned one blink into 0.6s of
grace PLUS a full second of invulnerability the moment the grace was spent - a 1.6s escape
on a 3.5s recharge, which is not a dodge but a second health bar, and it is precisely how the
grace would have made a room of gunners toothless. Without it the second shell of a pair
lands, so the player has to answer the volley rather than press the button once.

## [h:40-combat-4]
near: `Sfx.hurt();`

SOUND, AT THE POINT THE HIT IS CONFIRMED. Not at the call sites: there are several ways to hurt
    the player - contact, a shell, a boss body - and a sound wired to each of them is a sound that
    is missing from the way somebody found to hurt them. This is the first line after the i-frame
    and grace checks, so it fires for every hit that actually lands and for none that is forgiven.

## [h:40-combat-5]
near: `if(player.hp<0&&player.hp>-1e-6) player.hp=0;`

Snap a float epsilon to exactly zero. Damage here is deliberately fractional - a shell does
1.8, which is what makes a lunger take a satisfying number of hits - and repeated fractional
subtraction can land health on 6.66e-16 instead of 0. That value is greater than zero, so the
death check never fired, and the player was left standing on a health bar that read as empty.
Only the band just BELOW zero is snapped; anything at or above zero is left exactly as it is,
which is the whole point - a positive remainder is real remaining health and must survive.

## [h:40-combat-6]
near: `if(state==='dev'&&player.hp<1) player.hp=1;`

THE LAB CANNOT DIE, and it is enforced HERE rather than at the death checks.

    This was first done at the two death checks in update() that a first reading suggested were the
    only ones. There are four. The other two were left unpatched, so a drove killed the lab player
    through one of them, endRun fired, and the run recorded a death that never happened - which is
    the one thing a debug view must not be able to do to a real record.

    So the rule lives at the single point every point of player damage passes through, and it floors
    health at one rather than zero. Flooring rather than restoring matters: restoring at the top of
    the tick would still let a hit take the player under within that same tick, and a check further
    down would see it. One line here, and the four checks need no knowledge of the lab at all.

    It goes here rather than inside endRun because endRun is also the run's legitimate way to end,
    and teaching it about the lab would mean the lab's immunity was a property of the scoreboard.
    The knockback and the momentum cost are deliberately NOT skipped: a body that cannot be hurt
    still gets shoved, and the meter still records the hit, because a lab that protects you from
    consequences is a lab that will lie to you about momentum.

## [h:40-combat-7]
near: `function drawR(p){`

How big a friendly projectile should be DRAWN, which for the shrinking guns means: exactly as
  damaging as it is right now. Sizing the picture off falloffMult rather than off a second curve is
  the entire point - two curves would drift apart the first time either was retuned, and the player
  would be reading a lie. The collision radius stays fixed either way: the shot's accuracy is not a
  function of how much damage it happens to be carrying.

## [h:40-combat-8]
near: `const BOLT_FADE_MIN=0.55;`

How bright the halo is, 1 at the muzzle down to 0.55 at the far end. Size on its own is a weak
  tell - a small bright disc is still perfectly readable on a lit floor - so the tell is carried by
  two channels at once, which is what makes it something you notice while aiming rather than
  something you notice while dying. It is a separate function so the size and the fade cannot be
  tuned independently by accident and drift into saying different things.

## [h:40-combat-9]
near: `function clearShot(room,e,want){`

Does this angle reach the player without crossing a body of ours on the way? Tests a few degrees
either side of the aim and hands back the first clear one, so a gunner beside a Brunch pack shoots
round it rather than through it. It only counts bodies actually between the muzzle and the target,
which is why a pack the gunner is standing inside does not lock it up.

## [h:40-combat-10]
near: `const ownGuards=e.shieldGuardFor;`

ITS OWN BODYGUARD IS NOT AN OBSTACLE. This is the same rule the shell-absorption test applies -
    a shell passes through a Brunch that is guarding the shooter who fired it - and `clearShot` was
    not applying it, so the two halves of one idea contradicted each other:

      absorption says  "this shell goes through your escort"
      clearShot said   "that escort is in the way, sweep 60 degrees to miss it"

    Measured consequence, and this is the whole of the escorted-shooter problem at BRUNCH_SHIELD_FRAC
    0.25: the pack formed 74px from the muzzle, `clearShot` could only find a genuinely clear angle
    about 21 degrees off, and 21 degrees at that range is a 91px miss. The escorted shooter fired 15
    shells and landed zero, against 18 shells and 9 hits with no pack in the room. It was not being
    silenced by the pack EATING its shells - that was already fixed - it was being made to spray by
    the sweep dodging an ally that shells pass through anyway.

    So a body the shooter is guarding is skipped here exactly as it is skipped in the absorption test.
    `shieldGuardFor` is the reverse of `shieldTarget` and is rebuilt every tick; it can be a tick
    stale relative to a shell fired this frame, which is why the absorption test reads the forward link
    directly, but a standoff decision does not need to be frame-exact and this one self-corrects
    within a tick.

    The result is that a shooter with an escort fires straight down its true aim, through its own
    wall, and the pack's job reverts to what it should be: making the shooter harder to hit, not
    making the shooter worse at shooting.

## [h:40-combat-11]
near: `for(const off of [0,0.1,-0.1,0.2,-0.2,0.3,-0.3,0.42,-0.42]){`

The sweep picks BETWEEN the line it was given and its neighbours. It does not nudge the line it
    was given, and it used to: the zero-offset case added its own random twenty milliradians on top
    of whatever spread the caller had already applied to that angle, so every unblocked shot was
    fired at twice the spread it was aimed with.

    That is not a rounding argument. The gunner's floor is two hundredths of a radian, and an
    intercept against a running player at close range is a three hundred and seventy pixel long
    shot - so the intended spread is about seven pixels and the doubled one about fifteen, which is
    the width of the player's hitbox. Straight lines were being missed by exactly the margin of the
    mistake, which is the least legible way for a mechanic to be broken: the shot goes past the
    player, the player never learns why, and no amount of walking in a line looks punished.

## [h:40-combat-12]
near: `for(const off of [0,0.1,-0.1,0.2,-0.2,0.3,-0.3,0.42,-0.42]){`

THE SWEEP IS BOUNDED TIGHTLY, and it used to run to 60 degrees either side of the true aim.

    The purpose is right: a gunner must not waste a shell on its own Brunch pack, and finding a few
    degrees off is genuinely shooting THROUGH the pack at the player. But the offsets reached 1.05
    radians, and by the time the sweep gets that far it is no longer aiming at the player at all.

    MEASURED, and this is what the closer barricade exposed. With BRUNCH_SHIELD_FRAC at 0.25 the pack
    stands so close to the shooter that `clearShot` used to find its first "clear" angle 20.6 degrees
    one side and 21.8 the other, and the shooter committed to it:

        aim error vs the true bearing to the player
          no pack in the room      median  2.8 deg   p90  3.9
          pack escorting it       median 26.4 deg   p90 27.1

    So the escorted shooter was not being silenced by the pack eating its shells - that is fixed, and
    a shell aimed straight at the player now passes through the wall untouched. It was firing
    twenty-six degrees off, because a wide sweep plus a close wall means the first "clear" angle it
    finds is a bad one. The pack did not block the shot; it broke the aim.

    Both causes are now gone: the sweep no longer dodges the shooter's own escort (see the guard skip
    above), and the sweep is bounded so it cannot wander somewhere pointless even when a genuine
    obstacle is in the way. The bound is 0.42 radians, about 24 degrees - just past the 20.6 that was
    measured while the escort was still being treated as an obstacle, so a real gap is still found.

    Two intermediate values were measured and both were wrong, which is why the number is here rather
    than left at the old 1.05:
      - 1.05 rad (60 deg) overshot the answer by nearly 40 degrees and the shooter sprayed.
      - 0.22 rad (12.6 deg) was too TIGHT and the shooter held every shot: 0 emitted in 14 seconds
        against 18 unguarded. A gunner that never fires is a worse bug than one that fires badly, and
        it is invisible - nothing looks wrong, the room is just quiet.

## [h:40-combat-13]
near: `const SEP_CELL=96;   // comfortably wider than the largest sum of two body radii`

THE SEPARATION PASS, and the grid under it.

  It used to be a plain double loop: every pair of bodies, every tick, with bounceEnemies deciding
  in one instruction that almost all of them are too far apart to matter. Measured, the cost per
  body rises with the room - 1.30us at five bodies, 2.69us at forty, 8.35us at a hundred and sixty,
  and the ratio on doubling goes 2.38, 2.32, 2.99, 3.14, 3.96 where linear would be a flat 2.00.
  That is a quadratic wearing a linear costume, and it is invisible today only because a room tops
  out around twenty-three bodies. The game is planned to grow in both directions - more floors,
  bigger rooms, and a lab that drops a dozen at a time - so this is the one place where the cost
  of being wrong is paid twice: once now, and again at the point where the content outgrows it.

  A uniform grid fixes it, and the only thing that has to be got right is that it must find EXACTLY
  the pairs the double loop found. Two bodies can only interact when they overlap, and they overlap
  only when they are within a.r+b.r of each other, so if a cell is wider than the largest possible
  sum of two radii then any overlapping pair is in the same cell or an adjacent one - and a 3x3
  neighbourhood sweep is exact, not an approximation.

  THE ORDER IS THE PART THAT IS EASY TO BREAK. The old loop went a in array order and b above a,
  and bounceEnemies MUTATES both bodies, so the order decides the result: a pack pushed in a
  different order ends up in a different shape. So `a` still walks the array in index order and only
  `b > a` is considered - the visited pairs and their order are identical, and the grid only ever
  removes pairs that bounceEnemies would have rejected on its first line anyway.

  The one thing that is a genuine approximation, and it is measured rather than assumed: a body pushed
  ACROSS a cell boundary mid-pass is still in the bucket it was inserted into, so a pair that comes to
  overlap because of an earlier push this tick is resolved on the NEXT tick instead. Measured at
  1.01px on the worst body of a forty-body room - which is a knockback slide, not a pack flying
  apart - and the suite pins that bound. Re-bucketing after every push would make it exact and cost
  more than the quadratic did.

## [h:40-combat-14]
near: `let cnt=0;`

THE CANDIDATES ARE SORTED BACK INTO INDEX ORDER, and this is the whole correctness of the
      change - the first version did not, and it was caught by the equivalence check disagreeing by
      76 pixels on a sixty-four body room.

      The reason is that the two passes visit pairs in DIFFERENT ORDERS even when they visit the
      same pairs. The double loop takes b in ascending array order. A grid naturally takes them
      grouped by cell, because that is the order the buckets come out in - and bounceEnemies MUTATES
      both bodies, so the order decides the arrangement the pack ends up in. One body pushed early
      instead of late moves everything downstream of it, which is why a one-pair ordering difference
      became 76 pixels rather than 76 thousandths: it compounds.

      So the neighbour indices are gathered and sorted before use. The sort is over the handful of
      bodies in a 3x3 neighbourhood, not the whole room, so it is nearly free - and "nearly free" is
      checked by the scaling test, which fails if the grid turns out to have cost more than it
      saved.

## [h:40-combat-15]
near: `if(Math.hypot(a.kvx,a.kvy)<KNOCK_TRADE&&Math.hypot(b.kvx,b.kvy)<KNOCK_TRADE) return;`

STUN IS A TICK COUNT, and a FRACTIONAL one corrupts it permanently.

  `KNOCK_STUN/3` is 29.333..., not 29. The tick decrements with `e.stun--` under the guard
  `if(e.stun>0)`, so thirty clean decrements take 29.333 to -0.667 — and a negative stun is not > 0,
  so the branch stops running, the value is never clamped back to zero, and the body is left in a
  state that is neither stunned nor clean.

  MEASURED, and the consequence was not cosmetic. A Brunch pack chasing a player who sprints in a
  straight line:

      sec 4    gap  -43px   stun -0.667   the pack is ON the player
      sec 5    gap  -85px   stun  88      a collision re-set it, then it decays again
      sec 8    gap -1145px  stun  88      knocked west, 2.35px/tick, forever

  `curSpeed` was 3.0 and `vx` was 3.0 the whole time — the pack was at full sprint and still losing
  100px a second, because the stun branch at 60-tick.js:907 does `e.stun--; ...; continue;` and skips
  the entire body pass. A stunned body cannot walk onto its slot and cannot chase. So the pack closed
  to contact, took the hit, and was then held and driven away, which is precisely the reported
  symptom: a last-resort sprint that cannot land a single bomb rush.

  The fix is `Math.round`, which is what "a third of 88 ticks" has to mean for a value that is
  decremented one at a time. Flooring to 29 would be equally integral and one tick shorter; rounding
  is the honest reading of "a third of".

  The defensive half matters more than the arithmetic, though: `slowT` and `stun` are both decremented
  under a `> 0` guard and both are written from non-integral sources elsewhere
  (`40-combat.js:391` uses `mode.hold`, `443` uses `3*power`). So the tick clamps as well, and a stun
  that arrives negative from any future source costs one tick of stun instead of the rest of the
  fight.

## [h:40-combat-16]
near: `const DISPERSE=2.3;`

`mode.pool` is not per-enemy damage: it is one budget shared out between everyone caught, so a
  lone lunger eats the whole thing and dies, while a clump of four each takes a quarter and the
  blast works as a repositioning tool instead. The shove is scaled by how close you put it, and the
  hook inverts it into a pull and carries no budget at all, so it only ever moves bodies.

  The share is pool / crowd^DISPERSE, not pool / crowd. A flat split means the blast deals its full
  budget no matter how many bodies it catches, which is exactly wrong for a swarm: the budget was
  sized to kill one armoured lunger, and a four-strong Brunch pack does not have enough total health
  to survive being handed a whole lunger's worth of damage. Raising the exponent makes each extra
  body cost more than a proportional slice, so the *total* the blast actually lands falls away as
  the crowd grows - which is what turns it from a pack-clearing button into a panic nudge you still
  have to follow up on.

  The exponent is set by the SMALLEST pack, because that is the case that has to fail. A Brunch
  group rolls 4-8, and at 1.85 a group of 4 took 2.89 against 2.7 health - it deleted the most
  common pack in the game outright, which is precisely the "one click clears it" outcome the whole
  mechanism exists to prevent, and it did it at the one size players meet most often. 2.30 puts a
  four-strong pack at 43% health, which reads as a wound rather than a wound and a corpse.

  The other end of the curve is a killshot and it has to clear the third Brunch with MARGIN, not
  just barely. At 2.40 a three-strong group took 2.689 against 2.7 health: the intent was "it dies"
  and the float said "it survives on 0.011", which is the same failure as a lunger sitting alive at
  3e-15 hp in an earlier build. Any exponent where a whole-body kill lands inside a percent of the
  body is a knife edge waiting for a different TOUGH value. 2.30 leaves an 11% margin at three and
  still halves a four-pack.

  Against a 2.7hp Brunch: 1 caught dies, 2 die, 3 die, 4 are left at 43%, 6 at 76%, 8 at 88%. So the
  blast kills what it catches when it catches a handful and only bruises it once there is an actual
  group, which is the line between "a weapon" and "a panic button". Against a lone 24.3hp lunger it
  is completely unchanged - a crowd of one is a crowd of one whatever the exponent says.

## [h:40-combat-17]
near: `burstFX.push({x,y,r:mode.aoeRadius,life:BURST_TICKS,color:mode.color,dir:mode.pull?-1:1});`

the shockwave wears the weapon's own colour and travels the way that weapon actually acts: the
blast throws bodies outward so the ring expands, the hook drags them in so the ring collapses
inward. Before this it was a hardcoded orange expanding ring for both, so the one number the
player reads mid-fight - which way did that thing move things - was wrong for the hook.

## [h:40-combat-18]
near: `function tickFields(room){`

The ground spell. Three things happen to anything inside it, every tick:
    - it is held, by refreshing its stun. A stun stops the walk entirely, which is the point: the
      bodies that fell in stay in, and that is the window you shoot through.
    - it is walked in, by moving it toward the centre directly rather than by shoving it. A stunned
      body does not integrate knockback at all, so a velocity-based suck would do nothing to the
      very bodies it is meant to drag. Position is the only lever that survives the stun.
    - it is ground down, slowly. Far too slowly to be a weapon, fast enough that a field left on a
      big body finishes what the wand started.
  Separation still runs between bodies while they are held, so they pack around the middle of the
  circle rather than all standing in the same pixel, which is also what stops the suck stacking.

## [h:40-combat-19]
near: `if(e.hookMark!==f.id){`

A body is charged ONCE per field, not once per tick - a field lives nearly two seconds and
        runs every tick, so counting ticks would rack up a hundred resistances on the first cast
        and the second hook would do nothing at all. The field carries an id and the body remembers
        the last one it was charged for.

## [h:40-combat-20]
near: `const shotDmg=w.dmg*w.count+Stats.value('strength'), dmg=shotDmg/w.count;`

STRENGTH, added to the weapon's own number rather than multiplied into it, and read HERE at the
moment of firing for the same reason the cone is: it has to be whatever the build says it is on
this shot. Flat, deliberately - falloff is what gives each gun its shape, and a multiplier on
the curve collapses all four into one gun with a different colour. It was a sheet entry and
nothing else for the whole of the item framework's first day; a stat that is printed and not
read is worse than no stat, because the player is told they have it.

## [h:40-combat-21]
near: `const shotDmg=w.dmg*w.count+Stats.value('strength'), dmg=shotDmg/w.count;`

STRENGTH IS ADDED ONCE PER SHOT, NOT ONCE PER PELLET, and that distinction is the whole reason
    the Scatter was not merely strong but WRONG.

    It used to read `w.dmg + strength` and then fire `w.count` projectiles each carrying that whole
    value. For a weapon with one pellet - Bolt, Beam, Voidball - that is identical to adding it once.
    For the Scatter, with eight pellets, +3 Strength contributed +24 damage a shot instead of +3.
    A "+1 Strength" sigil was therefore worth EIGHT times as much to the shotgun as to the Bolt, and
    the roster's own note on the stat says why that is wrong:

      "Flat and not multiplicative on purpose: falloff is what gives each of the four guns its shape,
       and a multiplier applied to it collapses all four into one gun with a different colour. Bolt's
       whole identity is one committed hit that hurts more than anything else; that only survives while
       damage is a thing you add."

    Per-pellet addition is exactly the multiplication that note rules out, and it is why the Scatter
    measured as the best single-target gun in the game despite having the slowest rate of fire: its
    nominal DPS was 28.3 against the Beam's 26.8, the Bolt's 11.1 and the Voidball's 12.75.

    So the shot's total damage is `count x dmg + strength` and the pellets share it. One pellet is
    unaffected; eight pellets get the flat bonus once, as written. Measured effect on the START build
    against armour 0.66: a lunger took 29.57 a volley before and 15.70 after, so the Scatter no longer
    one-shots the largest common body - while still one-shots a Brunch, a Shooter and a Gunner, which
    is the identity it is supposed to have. At MAX strength (9) it one-shots a lunger again, which is
    what a finished build is for.

## [h:40-combat-22]
near: `let sx=player.x, sy=player.y, a=ang0, spd=w.speed;`

BUCKSHOT, for a weapon with more than one pellet.

      The old model fanned the pellets by ANGLE - pellet i left the muzzle at (i-(count-1)/2)*spread
      - so every pellet started at the same point and diverged from it. Eight evenly spaced rays
      from a common origin is not a shotgun, it is a diffraction pattern. And because the
      divergence was purely angular it opened into a V that kept widening with range, which is the
      same shape the Arcane Beam makes, and the reason the Scatter read as a wave shot rather than
      a shell full of shot.

      Real shot leaves the barrel from across the BORE rather than from a point, and the pellets do
      not share a direction. So the spread moves off the angle and onto the muzzle POSITION: each
      pellet starts somewhere in a small disc and flies near-parallel to the aim. That alone turns
      the cone into a column, which is what the pattern is supposed to be.

      The chaos is per-pellet SPEED, and it is worth being explicit that this is the part that is
      not merely cosmetic. A column of identical pellets travelling at identical speed is a bar of
      light - a tighter cone, but still not shot. Shot is de-synchronised: some out, some sagging,
      and by any distance the pattern has holes in it.

      Per-pellet speed variance is the whole of that mechanism, and it needs nothing on top of it.
      A per-tick drag was written here first, on the reasoning that it would let the fast pellets
      fall behind the slow ones and open the column with range. Measured, it cannot: a drag that is
      the same for every pellet scales all their velocities by one shared factor, so the ratio
      between the fastest and the slowest pellet is exactly what it was at the muzzle, forever. The
      mechanism does not amplify the spread at all - it only slows the whole shot down, and paying
      range for a cosmetic that was already free is a bad trade. The speed spread at the muzzle
      already separates pellets LINEARLY in time - at 0.25 variance on a 2.4 px/tick shot that is
      0.6 px/tick of difference, so 50px of spread by 200px of range and 112px by 450 - which is
      the column opening with range, for nothing.

      Damage is NOT changed by any of this. Same pellet count, same damage per pellet: the 2.60
      crowd rating the Scatter earned is untouched. This is a redistribution of where the damage
      lands, not a buff, and the tests assert the total so it cannot quietly become one.

## [h:40-combat-23]
near: `from:'player',owner:null,`

`from` is the OWNER of the shot, as a value both directions can carry. `friendly` already
said which side it was on, but it is a boolean about the projectile, and everything the
Brunch rule needs is a question about who fired it: a pack that eats enemy shells has to
leave the player's alone, or the Brunch stop being a thing you can shoot through. Deriving
that from `from` is one comparison, and it cannot be got wrong by a flag that drifted out of
step with the other one. `owner` stays as the enemy that fired, which is what stops a
gunner hurting itself.

## [h:40-combat-24]
near: `Sfx.shot();`

SOUND, AT THE ONE PLACE A SHOT IS COUNTED. Not at the call site and not in the tick: `run.shots`
    is incremented exactly once per trigger pull, so a sound here cannot double-fire on a multi-pellet
    weapon or miss a pellet. `Sfx.shot` reads the weapon for its pitch, so the four guns are told
    apart by ear.

## [h:40-combat-25]
near: `const live=projectiles.findIndex(p=>p.alt&&p.mode.early);`

A second right click while the HOOK is still in the air detonates it where it is, so you can
pull early and off your own aim point - drop it on a pack that is closing while you are still
being repositioned. Only the hook: the blast is left exactly as it was.

While one is in flight the input is consumed either way and never becomes a second bolt, which
is what stops a held button from stacking casts. Past the age floor it detonates; before it the
click is simply eaten, which is also what stops the cast and the cancel being the same input
and blowing the hook up at your own feet a tick after you threw it. Nothing here touches the
cooldown, so it cannot be farmed - it is already running from the shot that got it airborne.

The cooldown gates CASTING only, and that distinction is the whole reason the cancel works.
It used to gate the right click as well, and the hook's own cooldown (2.0s) is longer than a
long cast takes to arrive (up to ~1.9s) - so for most of the flight the button was on cooldown,
the handler was never reached, and the cancel silently did nothing. The feature was there and
unreachable, which is worse than not having it because there is nothing to notice.

## [h:40-combat-26]
near: `const aim=mouseWorld();`

nothing in flight, and the cooldown is still running
the ground spell lands where the cursor is, clamped to the room. Both halves of that needed the
conversion: it was clamping a SCREEN position against the room's WORLD walls, so in any room
that was not sitting at the screen origin the target was dragged toward a corner it was never
near - which is a second aiming bug wearing the same root cause as the wand's.
