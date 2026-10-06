# 05-rng.js — moved comments

Long comments moved out of `src/05-rng.js`. The code keeps a one-line gist tagged `[h:05-rng-N]`; search this file for that tag.

## [h:05-rng-1]
near: `function mulberry32(seed){`

05-rng  -  the random source, and the three streams it is split into

  THE GAME HAD NO SEED. Every decision it made - the shape of the dungeon, which bodies turned
  up and where, what a pack was made of - came out of Math.random, and a player could not see
  the number, could not type it in, and could not give it to anyone. Two people who played the
  same build played different games, and a run that went well could not be repeated to find out
  why.

  THE PART THAT ACTUALLY MATTERS IS NOT THE SEED. It is the split into three streams.

  A single stream is a trap, and it is a slow one. Everything draws from it in call order, so
  adding one idle-wander roll - or changing the art, which rolls a lot - shifts every subsequent
  draw, and the dungeon you published last month is no longer the dungeon that seed produces.
  Seeds silently rot, and nobody notices until a friend pastes one back and gets a different run
  and concludes the feature is broken.

  So randomness is sorted by WHAT IT DECIDES, and each decision gets its own stream:

    run     what the run IS. Dungeon shape, room growth, spawn placement, pack composition,
            which bodies, item rolls. A seed fixes this and nothing else.
    jitter  cosmetic timing and behaviour noise. Cooldown rolls, idle wander, a gunner's dodge,
            a shell's spread. It has to be reproducible too, or the same seed plays differently
            twice, but it must never be able to reach into the run.
    art     texture noise - the grain in the wood, the fleck in the paper. Purely cosmetic,
            consumed a great deal, and therefore the one most likely to change without anyone
    changing the game.

  The player is shown ONE number. The other two are derived from it, so a seed is a single thing
  to copy, and the streams stay independent of each other.

  mulberry32 lives here now rather than in the test harness, because the game needs it too and
  two copies of a generator is two chances for them to disagree.

## [h:05-rng-2]
near: `const OFF_JITTER=0x9E3779B9, OFF_ART=0x85EBCA6B;`

Each stream is seeded from the one number the player can see, through a different constant.
    A fixed irrational-looking offset means the streams do not overlap for nearby seeds: seed 1 and
    seed 2 must not produce the same dungeon, which they would if both streams were just seeded
    with the seed.

## [h:05-rng-3]
near: `function decode(text){`

THE SEED SPACE IS 32 BITS, AND A SEED THAT DOES NOT FIT IS REJECTED RATHER THAN WRAPPED.

    This returned `n>>>0`, which silently truncated. A seed is 7 base-36 characters and the screen
    accepts any 7 of them, but 36^7 is 78.4 BILLION and the generator is mulberry32 - 32 bits, 4.29
    billion. So 94.5% of everything a player can type was quietly folded onto something else:

        DUNGEON  ->  01FO0UV
        ZZZZZZZ  ->  0HFZ0FZ
        1Z141Z4  ->  0000000        (wraps past the top of the range)

    The player types a code, shares it with a friend, and the friend gets a different dungeon with
    no message. A seed exists so two people can play the same run; a seed that silently becomes a
    different seed is worse than no seed at all, because it looks like it worked.

    Seven digits is still the right width - it covers the whole 32-bit space with room to spare - so
    the fix is not to shorten the code but to refuse the codes that cannot be stored. `null` is what
    the seed sheet already treats as "That is not a seed", so this reuses that path rather than
    inventing one.

    It also means the set of usable seeds is the whole 0 .. 2^32-1 range and nothing wraps into it,
    so `encode(decode(x)) === x` holds for every accepted code, which is what makes the round trip
    assertable.

## [h:05-rng-4]
near: `function floorSeed(root,n){`

THE FLOOR SEED. Floor N is generated from the root seed the player typed, mixed with N, and NOT
    from the previous floor's seed. That distinction is the whole reason it is written this way.

    Chaining - reseeding from the current seed each time - would be simpler and is wrong: the streams
    advance as the floor is generated, so floor 3's seed would depend on how many draws floor 2
    happened to make. Two players typing the same seed would get different floor 3. Deriving from a
    FIXED root instead means every floor is a pure function of (root, floor), so the whole run
    replays from the one seed the player can read out loud, and floor 12 is the same dungeon for
    everyone who starts from the same seven characters.

    The mix is mulberry32's own output function rather than an arbitrary constant, because any fixed
    multiplier risks mapping nearby floors onto nearby seeds, and adjacent floors having visibly
    similar layouts is exactly the failure this is meant to prevent.

## [h:05-rng-5]
near: `function fresh(){`

A random seed that a player can read out loud. Time-based, because the alternative - a
    counter - produces 1, 2, 3, which people cannot tell apart across two machines.

    crypto.getRandomValues rather than Math.random, and that is not fastidiousness: it is what
    lets the suite assert that game code contains NO raw random() call sites at all. One is
    achievable, two is not, and a rule you cannot enforce is a rule that decays.
