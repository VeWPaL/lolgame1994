# Bosses: the King's standard and the region rosters (design, 2026-10-22)

For the owner to edit and pick from. The owner played the Sunken King with the starting kit (F3 to floor 14, no
items), found it "somewhat difficult", and asked that every region boss be built to that length and difficulty, with
the final boss scaled up from it later. This file writes the King down as the standard, measures it, shows what the
standard means on floors 1-12, and proposes six bosses per region (four rolled a run, no repeats) and three for the
floor 13 miniboss. A rule marked **(new)** does not exist in the game yet; everything else a boss leans on does.

## 1. The standard, measured

Every number comes from the code or from the bot (section 9). "Average" is the average bot profile.

| | Sunken King, a run's kit | Sunken King, starting kit (the owner's test) | Gatekeeper today, floors 1-12 |
|---|---:|---:|---:|
| Health | 2163 (640 x 1.35 x `DepthTough(14)` 2.50) | 2163 | 466 (floor 1) to 977 (floor 12) |
| Fight length, average bot | 163 s (b13, mean) / 176 s (median) | 242 s (median of wins) | 47 s Shallows, 51 s Kiln, 67 s Galleries |
| HP taken a fight, average bot | 21-22 | 22 | 2 Shallows and Kiln, 4 Galleries |
| Average bot wins the fight | 69% | 34% | not measured |
| Novice / skilled win | 44% / 84% | 11% / 50% | |
| Largest single attack's share of the HP taken | 20-24% | | |

**How 22 HP is taken with an 8 HP bar**: "HP taken" counts every hit. The regenerating heart (the last 2 of the 8)
refills inside a fight: after 4 s without a hit, 1 HP a second (`Balance.RegenDelay`, `RegenStep`). A 3-4 minute
fight gives it many chances, so a long fight is survivable only by stretches without a hit. That is part of the
King's difficulty: it is a test of not being hit for 4 s at a time.

So the King, with no items, is as hard for the bot as it was for the owner: about 4 minutes, and it beats the average
bot two times in three. With a run's kit it is 2.7 minutes and beats it one time in three.

### The rubric (frozen: a region boss is built to this, and a critic judges it against this)

1. **Length**: 150-180 s for the average bot with a run's kit (the King: 163 s).
2. **Three phases** at 66% and 33% health. **Each phase changes the room**, not only the move list (the King: the
   throne breaks and the centre falls; the centre closes and the edge falls). A phase change takes about 2.4 s, is
   told, and calls off the move in hand.
3. **One arena mechanic that shapes every phase.** When the arena itself attacks (the King's floor, a tide, a belt,
   a toll), the boss starts nothing while it does (the King waits while its floor cracks or burns), so the player
   reads one thing at a time. An arena that only shapes the fight (cover, light) must still change each phase.
4. **6-8 attacks across the fight**, a weighted set per phase (the King's seated phase has only Fan and Slam, plus its
   pattern and the recall; its risen phase six), the one just used at 0.3x (the King's `RepeatWeight`). Each boss's
   per-phase sets are written when it is built; the lists below say which attacks a phase adds.
5. **No attack over 25% of the HP taken** (the King: 24% at most), measured by booking every hit to its attack, the
   way `RunState.finalBy` does for the King.
6. **The game's tell language only**, every tell at least 0.35 s: the orange dashed line (a charge or a lunge), the
   cast flash (shells), a mark filling on the floor (a lob, a leap), violet (something arriving), gold (an elite).
   A boss's own tell (a foam line, a tether) is allowed when it is one of these families in a new skin and is marked
   **(new)** below. Shells and tells are always fully visible.
7. **At most 2 HP a hit**; its body hurts only while it attacks with it (the King: only while it charges).
8. **Gaps between attacks 0.16-0.46 s at floor 1.** A region boss's walk and gaps are divided by `DepthRate` (up to
   1.34x), as the Gatekeeper's are; the King's are not scaled (it lives on one floor). Tells never shorten.
9. **A death beat** (the King: 1.8 s, its shells cleared) and an item drop of rare or better (`docs/items.md`).
10. **One signature interaction** with a mechanic the game already has (the momentum, the blink and its trailing
    hitbox, the regenerating heart, the blast, the hook, a gun, an enemy rule, an item), so the fight teaches
    something no other fight does.
11. **The bot reads it**, so its numbers can be measured before the owner plays it.

### What the region bosses must not take from the King

The King keeps its signatures so floor 14 still feels new: **the floor grid that cracks, burns and falls** (the
ember crack outline and the violet fall are its language alone), **the recall** (adds drawn from this run's areas)
and **the throne**. A region boss may reuse the `Ground` code underneath for a different state (water, mud, belts)
but never cells that burn or fall, and never the ember outline.

## 2. What the standard means on floors 1-12 (two decisions for the owner)

### Health: fixed per area, not scaled by depth

The player's damage barely grows over a run (the measured damage a second against a boss: 10.6 in the Shallows,
12.7 in the Kiln Works, 13.0 in the Galleries, 12.3 against the King), but `DepthTough` grows 2.5x from floor 1 to
14. That is why today's Galleries fights already last 40% longer than the Shallows'. A 165 s boss scaled by depth
would last over 4 minutes on floor 12. So health is set per area from the measured damage, and depth adds pace
(`DepthRate`, up to 1.34x) instead:

| Area | Health for a 165 s fight | vs the area's average Gatekeeper | vs the King |
|---|---:|---:|---:|
| Shallows (1-4) | about 1750 | 3.5x (504) | 0.81x |
| Kiln Works (5-8) | about 2100 | 3.3x (643) | 0.97x |
| Flooded Galleries (9-12) | about 2150 | 2.5x (867) | 0.99x |
| Floor 13 miniboss (90 s) | about 1150 | 1.1x (1066) | 0.53x |

These are starting points: each boss's uptime differs (one that hides or burrows needs less), so each is tuned into
the 150-180 s band with the bot once built.

**Owner's answers (2026-10-22)**: Decision 1 is **B**. The bot is weaker than the owner (they beat the King with the
starting kit in under 2 minutes, faster than the average bot does with a run's kit), so a stronger, human-like bot
comes first, and the targets are set against it. Decision 2: the longer run is fine for now, to revisit. Flat boss
health across depth is fine: stronger items, stats and guns come later. The plan:
`docs/plan-2026-10-22b-autonomous.md`.

### Decision 1: how lethal a region boss is (the one that matters)

Length is safe to copy; lethality is not. A run meets 13 bosses before the King (12 region bosses and the floor 13
miniboss), and the chances multiply. Today the average bot wins 61% of runs: 88% of the time it gets through
everything but the King, then beats the King 69% of the time. With the same rest of the run and the King unchanged:

| The average bot survives each boss | It survives all 13 | Its run win (today 61%; target 40-70%) |
|---:|---:|---:|
| 69% (the King with a run's kit) | 0.8% | 0.5% |
| 90% | 25% | 16% |
| 95% | 51% | 31% |
| 97% | 67% | 41% |
| 98% | 77% | 47% |

Three ways to read "that difficult":

- **A. The King's lethality everywhere.** Honest to the words, but almost no run would reach floor 14.
- **B. The King's length, structure and density; lethality budgeted** (recommended): every rubric item, but each
  boss tuned so the average bot survives about 97-98% of boss fights (the run win stays near the bottom of the 40-70%
  target). It is the King's fight in every way a player feels (long, three rooms in one, always something to read),
  but a mistake costs a heart, not the run. The King keeps its full lethality as the real test, which matches
  "difficulty will scale for the final boss later". The HP budget each boss gets is set by measuring, not guessed here.
- **C. Full King lethality on each region's last floor (4, 8, 12), B elsewhere.** A clear climax per region; it
  needs each region's rolled four to name a climax, and it costs more run wins (three 69% fights: about 15% run win
  with B elsewhere). Only if the owner wants a much harder game.

### Decision 2: run length

Today the bosses take 12.4 minutes of a run (13 fights). At 165 s each, plus a 90 s floor 13 miniboss, they take 34.5
minutes, so a winning run grows from about 51 to about 73 minutes. Options: accept it (Isaac runs are 30-60 minutes,
Gungeon's 40-90); shorten the normal floors to pay for it (fewer rooms a floor in the Shallows); or make floor 1's
boss a two-phase "opener" of 120 s for new players. Recommended: accept it for the first playtests and measure.

## 3. Selection

- Each area rolls **4 of its 6** bosses for its 4 floors, shuffled, no boss twice in a run, on its own random stream
  from the seed (as `Spikes.Mark` does), so no other roll moves.
- Floor 13 rolls **1 of the 3** minibosses. Floor 14 is always the Sunken King.
- The ledger map shows the boss's sigil once its door is seen, and the dev line (F10) lists the run's draws.
- The Gatekeeper retires to a fallback (`TuneFloorBoss` off, or a boss not built yet).

## 4. The Shallows (floors 1-4): lines and movement

What the Shallows teach: read a line, step off it, keep moving (today's Gatekeeper leans on the charge here). Tide
pools, reeds, rocks, mud. Leaps belong to two bosses only (S4, S5), so the area is not all marked landings.

### S1. The Old Pike

A huge pike lurking in a reed bed. **Arena**: six reed clumps that block shots both ways; its dorsal fin shows above
the one it is in. **Signature: the Lunger's dodge, as a duel.** It dodges a bolt it sees coming (the Lunger's rule,
twice the reach) but never inside close range (180 px, the existing rule; a Lunger's once-a-life startle is the one
exception, and the Pike has none), so closing in with the Scatter is the answer. **(new)** A dodge that ends in a wall stuns it 0.8 s, so baiting a dodge toward a wall is the second answer.
The hook yanks it out of a dodge.
- Room: phase 1 six reed clumps; phase 2 they flatten one at a time; phase 3 open water.
- Attacks (6): Lunge (orange line, 0.5 s), Double lunge (the second line drawn as the first ends), Fin wake (it
  swims between clumps leaving a strip of slow shells), Bite (a short lunge, 0.35 s line), Splash (a ring of 12 as
  it leaves the water), Dodge-lunge (phase 3: a dodge chained into a lunge, its line drawn at the dodge's end, 0.4 s).
- Build: the Lunger's body, dodge and lunge; reed cover is new. **Medium.**

### S2. Mother Tide

A drowned priestess who calls the water. **Arena: the tide.** **(new)** Bands of water sweep across the room (a **(new)** foam
line, the line family, rises 1 s before each); water slows the player 30%. It starts nothing while a tide crosses. **Signature: momentum.** Momentum drains when you stop, so
the water, which slows you, also starves it: keeping to the dry strip is how you keep your speed.
- Room: phase 1 one tide crossing; phase 2 two tides from opposite walls meeting in the middle; phase 3 a whirlpool
  at the centre that pulls gently (full momentum outruns it).
- Attacks (6): Tide (the arena), Fan (cast flash, 7 shells), Spout (3 lob marks), Wave (a wall of shells with one
  gap, marked in foam), Rain (slow shells falling on marks), Whirlpool spiral (shells spun out of the centre).
- Build: the `Ground` grid with a water state (not burning), a pull force. **Medium-large.**

### S3. The Hermit

A crab in a stolen shell, in a pool of rocks. **Signature: facing.** **(new)** Its front takes 20% damage; the sides
and back take full. Circling at speed (momentum), Ricochet Shells off the room's walls (and **(new)** off its rocks), or a blast to the face
(**(new)** flips it: 1.5 s on its back at double damage) are the ways in.
- Room: phase 1 four rocks (cover); phase 2 it sheds its shell, which stays as a fifth piece of cover it can kick
  around; phase 3 it smashes the rocks one by one (each one breaks in a ring of shards), leaving only its old shell.
- Attacks (7): Pincer sweep (a **(new)** arc marked on the floor, the mark family, 0.45 s), Shell charge (orange line), Bubble cone (the
  Sprayer's 0.85 s wind-up), Rock toss (a lob), Shell kick (phase 2: it kicks its shell along a line, the Brunch
  dash's tell), Claw snap (a close 0.35 s arc), Rock smash (phase 3).
- Build: facing armour and the flip are new; the rest reused. **Medium.**

### S4. The Frog Chorus

Three frogs on lily pads, one health bar. **Arena: the song.** **(new)** The pool pulses on a beat you can see (rings
on the water); the frogs act only on the beat, so the room is the metronome. **Signature: a boss of three bodies.**
Each frog has its own share of the bar; when a share runs out its pad sinks. **The phases are the frogs alive (3, 2,
1)**, and the song speeds up for the survivors. Chain Spark jumps frog to frog; Time Glass slows the song with them.
- Room: phase 1 five pads; each phase a pad sinks with its frog, and the beat quickens.
- Attacks (6): Leap (a marked landing, 2 HP, the Burrower's mark), Landing ring, Croak wave (a ring from every frog
  at once), Bubble lob, Tongue (a 0.5 s line; it pulls you 80 px instead of hurting), Chorus (the last frog's fan on
  every beat).
- Build: a shared-health boss of several bodies (new; K5 reuses it). **Medium.**

### S5. Old Mudmouth

A giant Burrower. **Signature: the blast on the crack.** Before every leap the floor cracks for 0.3 s (the
Burrower's own tell). **(new)** A blast on that crack knocks it out of the ground (as a blast opens the secret
wall's scorched crack) and it lies stunned 2 s on the surface at double damage. Otherwise it is untouchable
underground, like every Burrower.
- Room: phase 1 firm ground; phase 2 the mud it passes through stays soft and slows for 3 s; phase 3 **(new)** raised dirt
  lines (the line family) show its tunnels before it bursts out.
- Attacks (6): Leap (marked, 2 HP), Mud spit (3 lob marks), Surface ring (shells as it lands), Double leap (the
  second marked from the first's landing), Tunnel (phase 3), Brood (two small Burrowers of its own kind; the recall
  stays the King's).
- Build: the Burrower reused, plus the blast rule. **Small-medium.** Cheapest Shallows boss.

### S6. The Grey Wader

A tall heron. **Signature: it learns your running.** Its beak strikes land on marks aimed with the Mortar's trust in
your velocity, but **(new)** the trust rises each time you run straight and resets when you turn or stop. Standing
still beats it and drains your momentum; the fight is about when to turn.
- Room: phase 1 open shallows; phase 2 **(new)** driftwood logs wash in as cover (it steps over them, you cannot);
  phase 3 its wing gusts send the logs rolling across the room (a 0.6 s line before each; 2 HP).
- Attacks (6): Strike (a marked point, 0.5 s), Twin strike (two marks, the second on the trust after the first),
  Wing gust (a push across the room, shells along it), Feather fan, Fish toss (a lob), Log roll (phase 3, the arena's
  attack: it stands still while logs roll).
- Build: the Mortar's aim and lob marks reused. **Small-medium.**

## 5. The Kiln Works (floors 5-8): the floor and the lobs

What the Kiln Works teach: read marks filling on the floor (the Gatekeeper leans on the slam here). Rails, furnaces,
clay, slag, bellows, belts.

### K1. The Foreman

Rides the room's rail in a cart. **Arena and signature: the Kiln's rails.** The cart runs a track (one of the eight
shapes rail eggs use) towing two rail eggs. An egg's ring turns so as to hit the fewest allies, so its gap tends to
face the Foreman: stand on the Foreman's side of an egg. The hook holds the cart still for 0.7 s (it cannot drag the
eggs, but it can stop the wheels).
- Room: phase 1 a loop; phase 2 a **(new)** switch flashes 1 s (the flash family) and the track changes shape (**(new)** drawn faintly first); phase 3 it
  derails and the old track stays drawn as the lines it charges along.
- Attacks (6): Slag drop (lob marks behind the cart), Whistle (a fan from the cart), Brake sparks (a ring at each
  corner), Coal lob, Coupling (phase 2: an egg uncoupled to ride alone), Derail charge (phase 3: orange lines, 0.5 s).
- The cart hurts only in its Derail charge; touching it on the rail is safe.
- Build: the rail egg's tracks and motion reused. **Medium.**

### K2. The Bellows

A great leather bellows on legs. **Arena: the air.** **(new)** Inhale (a cast flash and a drawn-in dust ring, 1 s)
pulls the player and every shell toward it, yours included, so your shots curve into it. Exhale pushes everything
away and throws embers. **Signature: momentum against the pull**, and Hush Bell against the embers.
- Room: phase 1 still air; phase 2 vents in the walls blow in turn (a **(new)** strip marked 0.6 s; a push, no damage)
  and shells drift with them; it starts nothing while a vent blows; phase 3 the vents and the Bellows alternate faster.
- Attacks (6): Inhale, Exhale (embers as slow shells), Coal lob, Vent gust, Ember spiral, Roll (phase 3, an orange
  line).
- Build: a pull and a push on the player and on shells (new). **Medium.**

### K3. The Potter

The kiln's mother shapes Brunch from wet clay. **Signature: the Brunch, unfinished.** It makes clay Brunch (brown,
half health) that guard it as Brunch guard any shooter; **(new)** after 4 s in the kiln's glow they fire and harden
(full health, armour). Kill them wet. The Brunch's dash and its tell stay, and so does their 0.45x Beam damage, so the
Beam is the wrong gun for the clay.
- Room: phase 1 one kiln; phase 2 the kiln doors open along two walls, and clay Brunch standing in a door's glow
  bake in 2 s instead of 4 (so you fight them away from the doors); phase 3 the kilns go dark and the Potter fires itself.
- Attacks (6): Shape (two clay Brunch), Glaze (a fan), Throw pot (a lob that bursts in 6 shards), Ember fan (phase 2),
  Clay spit (slow shells that leave nothing), Fire self (phase 3: 3 s hard at 50% damage, then 2 s cracked at double).
- Build: the Brunch reused with a bake timer. **Medium.**

### K4. The Slag Colossus

A walking heap of slag. **Arena and signature: your damage builds the room.** **(new)** Every 6% of health it loses
drops a slag block where it stands: cover that blocks shots both ways. Blasts break blocks; **(new)** Ricochet bounces off them too.
The room fills up as the fight goes on.
- Room: phase 1 empty, filling; phase 2 it hurls blocks, which land as new cover; phase 3 the blocks crack one by one
  into rings of shrapnel, clearing the room again.
- Attacks (6): Stomp (a ring mark around it, 0.6 s), Slag lob (2 HP marks), Grind (slow shells along the floor),
  Hurl (a big landing mark), Shatter (phase 3), Slag wave (a line of lob marks walking toward you).
- Build: walls that appear mid-fight (pathing, `ClearShot`, the bot) are new. **Large.** The most distinctive Kiln
  boss.

### K5. The Twin Crucibles

Two crucibles on chains, one pouring, one venting, one bar. **Arena: the swap.** Every 6 s they swap roles, and the
room's danger moves with them. **Signature: the open lid, the Gungeon weak-point rule.** **(new)** The one venting has its lid open
and takes double damage, and a shot into the open lid also stops its vent. Where the Frog Chorus asks which body to
kill first, the Crucibles ask when to shoot: the Scatter's 1.1 s cooldown can miss a window the Bolt makes.
- Room: phase 1 they hang still at two ends; phase 2 they swing on their chains (a **(new)** orange arc, the line family bent,
  0.5 s before; they hurt only while swinging); phase 3 they merge into one crucible that pours and vents in turns.
- Attacks (6): Pour (a line of lob marks), Vent (the Sprayer's steam cone), Swing (phase 2), Spill (scattered marks),
  Lid toss (a line out and back), Merged pour (phase 3).
- Build: the shared-health boss of several bodies from S4. **Medium.**

### K6. The Overseer

A Gunner captain on the factory's belts. **Arena: the conveyor belts.** **(new)** Belts on the floor carry the player
and bodies at 90 px/s in their direction. **Signature: the Brunch's dash.** A Brunch plans its dash for a retreat of
up to 75% of its speed (the existing rule), so a belt carrying you away faster than that beats it, and a belt carrying
you toward it is a trap. The Overseer's two Brunch packs guard it as Brunch guard any shooter.
- Room: phase 1 two belts lengthwise; phase 2 belts across the room, and catwalk mortars lob from the room's edges;
  phase 3 the belts reverse in a rhythm (**(new)** arrows flash 0.8 s before; it fires nothing while a belt turns).
- Attacks (6): Volley (the Gunner's cast; it roots), Packs (two Brunch packs, their rush and its tell), Catwalk lobs
  (the Mortar's marks, from the edges), Spray (the Sprayer's wind-up), Crate drop (a big lob, 2 HP), Belt reverse.
- Build: the bodies reused; belts on the `Ground` grid are new. **Medium.**

## 6. The Flooded Galleries (floors 9-12): shells and patterns

What the Galleries teach: shell patterns and gaps (the Gatekeeper leans on the fan and the ring here). Flooded halls,
statues, glass, dark water, echoes.

### G1. The Curator

A statue that keeps the gallery. **Arena and signature: your aim.** **(new)** Its four statues move only while they
are outside your aim (a cone around the mouse); aim at one and it freezes. You cannot aim at the Curator and watch
the statues at once.
- Room: phase 1 four statues on plinths; phase 2 a statue you aim at for 1 s rings out shells, so watching is a risk
  too; phase 3 the lights dim and when you blink, the Curator and a statue swap places.
- Attacks (6): Fan, Statue lunge (a statue that reaches 120 px lunges with the orange line; statues hurt only then),
  Statue ring (phase 2), Plinth lob (lobs from the empty plinths), Gaze (a line of shells down your aim), Swap
  (phase 3).
- Build: an aim-cone test (small), statue bodies (new). **Medium.** Open question: gamepad aim, if the game gets it.

### G2. The Glass Saint

A saint of blown glass in a hall of mirrors. **Arena and signature: Ricochet Shells for everyone.** **(new)** Mirror
panels on the walls bounce every shell once, the Saint's and yours, as Ricochet Shells bounces yours off any wall.
The player who holds Ricochet bounces twice off a mirror.
- Room: phase 1 mirrors on two walls; phase 2 on all four; phase 3 they shatter one by one into bursts of shards.
- Attacks (6): Fan, Ring, Prism (a shell that splits in 3 when it hits a mirror: the Splitter's burst), Glass beam (a
  slow stream told by a line), Shatter (phase 3), Glass rain (lob marks).
- Build: mirror walls on the shell step's wall test (the Ricochet hook exists). **Medium.**

### G3. The Undertow

A long eel in the gallery's channels. **Arena: the channels** it swims in. **Signature: a body of many segments.**
**(new)** Only the head takes damage; the segments block shots both ways. Ricochet curls shots round it; Chain Spark
jumps from a hit segment to the head; the hook pins a segment for 0.7 s.
- Room: phase 1 two channels; phase 2 the water swaps channels (a dry channel floods as the other drains); phase 3 it leaves the water and coils
  round you.
- Attacks (6): Bite (it surfaces; orange line, 0.5 s), Spray (shells along its length as it dives), Wake ring,
  Tail slap (an arc), Geyser (lob marks from the channels), Coil (phase 3: a ring of segments tightening with one gap,
  then a bite through it).
- Its segments hurt only in Coil and Tail slap; brushing a resting segment is safe.
- Build: a segmented body (new); channels on `Ground` as water. **Large.**

### G4. The Lamplighter

**Arena: the dark.** **(new)** The room and its bodies are dark except near three lamps; shells and tells are always
drawn in full (rubric 6). **Signature: the blast as a light.** **(new)** Your blast lights 100 px for 1 s, which is
how you find the Lamplighter to aim at it in the dark.
- Room: phase 1 three lamps; phase 2 it carries one lamp and snuffs the others; phase 3 the only light is itself.
- Attacks (6): Fan, Ring, Nova (two rings, offset), Snuff (a lamp flickers 1 s and goes out), Wick lob, Lamp throw
  (a lob that lights where it lands).
- The dark never attacks; it shapes every phase (rubric 3's second half).
- Build: a light pass in `WorldView` (new). **Medium-large.** Risk: readability; a playtest of the dark alone first.

### G5. The Choir of Echoes

A choir of drowned voices. **Arena: the stalls**, four alcoves in the walls the voices sing from. **Signature: the
tell drawn twice.** **(new)** Every attack repeats 1.2 s later from the alcove nearest where it began, its **(new)** pale
tell (the same tell, faded) drawn at once, so you read two patterns at once. Time Glass slows the echo with the rest; Hush Bell clears both.
- Room: phase 1 four alcoves; phase 2 two alcoves flood and the others sing twice; phase 3 the echo leads:
  the pale pattern plays first, the real one follows.
- Attacks (6): Fan, Ring, Line (a row of shells), Spiral, Hymn (two offset rings), Silence (a cast flash, then every
  shell in the room stops for 1 s and goes on).
- It breaks rubric 3's "one thing at a time" on purpose: two patterns at once is the whole fight, so its
  attacks are lighter (1 HP shells only) to pay for it.
- Build: recording and replaying an attack. **Medium.** The purest Galleries boss.

### G6. The Leech Bishop

**Arena: the pillars.** **Signature: the regenerating heart.** **(new)** A red tether (the line family) grows toward you over 1 s while
it can see you; while joined, the regenerating heart's refill (1 HP a second after 4 s without a hit) goes to the
Bishop instead of you. The pillars break the line; a blink through it cuts it.
- Room: phase 1 six pillars; phase 2 the water takes two of them; phase 3 it tethers to the last pillars and the
  shells it fires orbit them.
- Attacks (6): Tether, Fan, Ring, Blood lob, Orbit (phase 3), Drain (a pull of the room's shells toward it, a cast
  flash 0.6 s).
- Build: a tether with line of sight (new). **Medium.**

## 7. Floor 13: the miniboss (one of three)

Floor 13 is the last ordinary floor before the King. The miniboss standard: about 90 s, two phases (a room change at
50%), 4-5 attacks, the rubric otherwise, none of the King's signatures. It drops a heart as well as an item, so the
King is met healthy.

### D1. The Warden, Returned

The game's first boss, rebuilt to the standard: its volleys and sweeps, and the wall of Brunch statues that guard it,
waking and acting as they do tuned. Phase 2: the statues wake all at once. **Signature: the Brunch guard wall**, the
one the player has fought behind all run. **Small-medium** (the code exists).

### D2. The Twin Gatekeepers

The floor boss the run has met until now, twice, sharing one bar: when one charges, the other slams. Phase 2: the
survivor of a share takes the dead one's moves. **Signature: the Gatekeeper's own five attacks, crossed.** The cheapest
of the three, and a send-off for the placeholder. **Small.**

### D3. The Bell-Ringer

The Deep's bell tower. **Arena: the toll.** **(new)** The great bell swings (1 s, its **(new)** arc drawn, the line family) and tolls; it starts nothing during a swing. Every
shell in the room, the player's and its own, is gone, and every body is thrown back, as the Hush Bell does at 220 px.
**Signature: the Hush Bell, turned against you.** Shots fired before the toll are wasted; fire right after it. The
player's own Hush Bell, rung during the swing, silences that toll.
- Phase 1: the bell tolls every 6 s; Fan, Ring, Rope swing (a **(new)** arc marked on the floor).
- Phase 2: it tolls every 4 s and the rope swing sweeps twice; Bell lob (a big mark, 2 HP).
- **Medium.**

## 8. Recommendation, and what to build first

1. Decide **Decision 1** (B recommended) and **Decision 2** (accept and measure).
2. Build the shared kit once: per-area health, phases with room changes, per-attack damage booking (the King's
   `finalBy`, generalised), a boss of several bodies sharing a bar, the selection stream, and the bot's reading.
3. Then one boss per region, the cheapest distinct one first, so the whole run has new bosses early: **Old
   Mudmouth** (S5), **The Foreman** (K1), **The Choir of Echoes** (G5), and **The Warden, Returned** (D1). Each one
   goes through the rubric, the bot (150-180 s, its lethality budget, no attack over 25%) and your playtest.
4. The large ones (K4 Slag Colossus, G3 Undertow) after the kit has carried four bosses.

### Rubric check (every boss)

| Boss | Arena | Arena attacks, boss waits (stated in its text) | Room changes each phase | Attacks | Signature on an existing mechanic | New rules and tells |
|---|---|---|---|---:|---|---|
| S1 Old Pike | reeds | no (cover) | flatten, open | 6 | Lunger dodge, close range | wall stun |
| S2 Mother Tide | the tide | yes (every tide) | 1, 2 tides, whirlpool | 6 | momentum | water, pull, foam line |
| S3 The Hermit | rocks | no (shapes) | shell sheds, rocks smashed | 7 | Ricochet, blast | facing, flip, rock bounce, arc mark |
| S4 Frog Chorus | the song | no (times the frogs) | a pad sinks a phase | 6 | Chain Spark, Time Glass | shared bar, beat |
| S5 Old Mudmouth | the mud | no (slows) | soft mud, tunnels | 6 | Burrower crack, blast | blast unearths, dirt lines |
| S6 Grey Wader | driftwood | yes (phase 3 logs) | logs wash in, roll | 6 | Mortar trust, momentum | learning trust, logs |
| K1 Foreman | the rail | no (shapes) | track changes, derails | 6 | rail eggs, egg ring, hook | switch flash, track preview |
| K2 Bellows | the air | yes (phase 2-3 vents) | vents, faster | 6 | momentum, Hush Bell | pull and push, vent strip |
| K3 Potter | the kiln | no (bakes faster) | doors bake faster, dark | 6 | Brunch guard, Beam 0.45x | bake timer |
| K4 Slag Colossus | the blocks | no (shapes) | filling, hurled, cracked | 6 | blast, Ricochet | walls mid-fight, block bounce |
| K5 Twin Crucibles | the swap | no (its own bodies) | still, swinging, merged | 6 | the lid's weak point, gun cooldowns | lid damage, orange arc |
| K6 Overseer | the belts | yes (phase 3 turns) | lengthwise, across, reversing | 6 | Brunch dash's retreat plan | belts, arrows |
| G1 Curator | the statues | no (its own bodies) | rings, swaps | 6 | aim, blink | aim cone |
| G2 Glass Saint | the mirrors | no (shapes) | 2 walls, 4, shattered | 6 | Ricochet, Splitter | mirror walls |
| G3 Undertow | the channels | phase 3 coil | water swaps, coil | 6 | Ricochet, Chain Spark, hook | segments |
| G4 Lamplighter | the dark | no (light) | carried lamp, one light | 6 | the blast | dark, blast light |
| G5 Choir of Echoes | the stalls | no: breaks rubric 3 on purpose | flood, echo leads | 6 | Time Glass, Hush Bell | echoes, pale tell |
| G6 Leech Bishop | the pillars | no (shapes) | two lost, orbit | 6 | the regenerating heart | tether line |
| D1 Warden | the statues | no (its guards) | all wake | 5 | the Brunch guard wall | none |
| D2 Twin Gatekeepers | none | no | one takes over | 5 | the Gatekeeper's attacks | shared bar |
| D3 Bell-Ringer | the toll | yes (every swing) | faster, double sweep | 5 | the Hush Bell | the toll, bell arc, rope arc |

## 9. Verification log

- Health per floor: `Balance.DepthTough` from the core (scratch program `bossnums hp`; `Tuned` on, `Curve` off, as
  the game runs).
- The King with the starting kit: 100 seeds x 3 profiles, the bot put on floor 14 by `Descend` (the F3 path), 8 HP,
  no items (`bossnums king`).
- The Gatekeeper per area and the King with a run's kit: 40 seeds x 3 profiles, full runs (`bossnums runs`); the
  King's 163 s and its attack shares from the b13 report (100 seeds x 3 profiles).
- The health table, the run length, the survival and run-win tables: `bossmath.py`, `bossmath2.py` (scratch) from
  those measurements.
- The regenerating heart's refill: `Balance.RegenDelay` (4 s) and `RegenStep` (1 s), `Tick.cs`.
- Checked against the code by two cold critics (the second found no wrong claim about an existing mechanic): the Lunger's 180 px rule, the blast and the secret wall's crack, the
  hook and eggs, the eight rail shapes, the Brunch's 0.45x Beam damage and its retreat plan, the Burrower's 0.3 s
  crack, the King's numbers. Its corrections are in this version (`ClearShot` skips a shooter's own guards; the egg
  ring still fires every shell; the King's repeat weight is 0.3).
- Not verified: the run-win table assumes the rest of a run stays as it is (today's Gatekeepers rarely kill); each
  boss's cost is a judgment; how a person (not the bot) feels about 73-minute runs.
