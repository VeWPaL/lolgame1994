# 20-world.js — moved comments

Long comments moved out of `src/20-world.js`. The code keeps a one-line gist tagged `[h:20-world-N]`; search this file for that tag.

## [h:20-world-1]
near: `let rooms, cur, player, mouse, mouseDown, altMouseDown, keys, state, records;`

20-world  -  global state and the dungeon generator

  The map graph, room growth, doors, keys, and the spawn plan. Pure simulation: no drawing, no
  input, no platform calls. Contains the global state block, which in C# becomes a GameState class
  rather than nine module-level variables.

## [h:20-world-2]
near: `let projectiles=[];`

`projectiles` is initialised HERE, with the FX arrays below, rather than being left undefined until
  the first run starts it. It used to be declared in the list above and assigned by the first line of
  startGame - which meant `clearTransient` could be asked to empty a variable that did not exist yet,
  because the line it replaced was the one doing the creating. A transient that is created by its own
  reset is a transient that a second reset cannot safely touch.

## [h:20-world-3]
near: `function clearTransient(){`

EVERYTHING THAT BELONGS TO THE ROOM YOU ARE LEAVING, in one list.

  These four arrays are the room's transient state: what is in flight, what is glowing, what is lying
  on the ground. They are emptied together and always together, and the reason they are emptied at
  all is not obvious from the name.

  WORLD COORDINATES ARE PER-ROOM. Every room's bounds start at the same origin, so (400,355) is the
  middle of the room you are in AND the middle of the room you just left. A hook field that outlives
  its room does not drift off harmlessly into the void beside the next one - it lands in the middle of
  that one, on top of whatever is standing there.

  Which was not only ugly. `tickFields` runs against the CURRENT room's bodies, so a leaked field
  charged and stunned a body the player had never met, and being charged is what grants hook
  RESISTANCE - this file's own note on the resistance table says that getting the count wrong there
  quietly nerfed the weapon. Measured: a leaked field charged a fresh body at tick 126, so every room
  entered within about two seconds of laying a hook began with its first enemy already carrying one.

  One list rather than three call sites each remembering four names. `enterRoom` used to clear
  projectiles and nothing else, which is precisely how three of the four outlived a room change while
  the two run-level resets remembered all four.

  The wand's muzzle flash is here too, and it is the one player-owned thing on this list. It is a
  weapon VFX drawn at the wand tip, so a shot fired in the last doorway flashed in the next room.

## [h:20-world-4]
near: `function resetRunCursors(){ FLANK_CURSOR=0; PACK_CURSOR=1; }`

THE TWO CURSORS ABOVE ARE THE ONLY MODULE STATE A RUN OWNS, AND BOTH ARE NOW RESET BY IT.

  Neither was. They are module-level `let`s outside every object, so they survived `startGame()` and
  simply carried on counting - which is correct WITHIN a run and quietly wrong across one.

  The consequence was the second fight from a seed not matching the first: the dungeon was identical,
  the bodies were identical, and every lunger circled the player from a slightly different angle
  because the golden-angle walk had not gone back to zero. Measured, same seed and same commands twice:

      run 1   flank 0.00 2.40 4.80 0.92 3.32 5.72 1.83 4.23    lunger at 401, 414, 430
      run 2   flank 0.35 2.75 5.15 1.27 3.67 6.07 2.18 4.58    lunger at 398, 411, 427

  A seed exists so two people can play the same run, and the first run of a session played differently
  from the second - so a friend comparing runs got a difference with no seed to explain it, which is
  exactly the situation the whole feature exists to prevent.

  "Never reset" was the right instinct for the wrong scope. It was defending against a body outliving a
  room transition and colliding with a pack from another room, and that is a WITHIN-run concern; both
  counters are reset at the start of a run and never touched again until the next one. A pack id is
  unique within a run, which is all the collision argument ever needed.

## [h:20-world-5]
near: `const ARM_DIRS=['N','E','S','W'];`

dungeon shape ----------
  Not a rosette of corridors. The map is a few *runs*: each one is a line of fight rooms you walk
  from end to end, winding but never doubling back on itself, with a reward waiting at the end.
  Runs are allowed to fork off the side of each other, so the whole thing reads as three or four
  directions out of the start and a handful of decisions along the way. That is the loop the game
  wants: fight, fight, fight, payout, and a map small enough to hold in your head.

## [h:20-world-6]
near: `function roomBounds(w,h){`

A ROOM CARRIES ITS OWN BOUNDS, and this is the field the whole big-room system hangs off.

  Every room is a rectangle in world space, and a standard room is the default rather than the rule.
  `bounds` is stored rather than computed from a global because a room is the thing that knows its
  own size - and because the alternative, a global every room reads, is a global that can only ever
  describe one shape.

  The four values are written out rather than spread from ROOM_W/ROOM_H so that a future room with
  an asymmetric inset, a ledge, or a corridor stub has somewhere to put it without changing the
  shape of the data.

  It reads STD_ROOM for its origin rather than ROOM_LEFT, and that is not a style choice. ROOM_LEFT
  is now the CURRENT room's left edge, so a room built while standing in a big room would be
  positioned relative to the big room - and a room built while standing in itself would be placed at
  its own coordinates, which is a room that is somehow at its own left edge. A generator that
  positions a room from the room you are in is the same geometry-in-two-places bug wearing a
  different hat, so this anchors on the constant that means "where a room starts" and nothing else.

  `cx`/`cy` are on the room because a bigger room's centre is not the screen's centre, and anything
  that needs "the middle of this room" should ask the room rather than average two shorthands that
  happen to be describing whatever room the player is in at the time.

## [h:20-world-7]
near: `function grow(rs,x,y,dir,len,strict,bias){`

walks one corridor out of (x,y), returning the rooms it placed plus its tip. the run travels in
straight segments of two or three rooms and bends once at the end of each, so a run reads as a
path you walk rather than a spiral. strict forbids rooms that touch anything but their parent,
which is how the loose pass finds room for the long runs.

## [h:20-world-8]
near: `function isEndpoint(r){ return Object.keys(r.doors).length===1; }`

THE MAP. A general branching tree, with the rooms that matter assigned to its ENDS afterwards.

  The old generator laid out three named runs by hand - a silver spine, an arm off its first room
  for the upgrade, a gold run off its second, and the boss hanging off the end of that. It was
  legible as code and it produced the same map every time in shape: three corridors, a fixed
  junction at each fork, and a route a player could learn in one run and never have to think about
  again. A dungeon you can learn in one run is a corridor with monsters in it.

  What replaces it grows a TREE and then decides what the ends are FOR. Two trunks leave the start
  in different directions; each forks a side run partway out; the tips of the trunks become the two
  rooms worth fighting towards, and the tips of the side runs are the places a key can be. The
  branches are not decoration - a key is IN one, so a branch is a detour you have to choose to make
  rather than a shortcut you pass through, and which branch holds which key is decided at
  generation, so the route is different every floor even at the same seed.

  WHY THE KEYS GO IN BRANCH ENDS RATHER THAN ON THE TRUNK. A key on the trunk is a toll you pay by
  walking forward, and a toll that is unavoidable is not a decision. Put it at the end of a side run
  and the player is choosing between two ways to spend a floor: go deep on a trunk and find out what
  is at the end of it, or turn off early and come back with a key. Both are correct play and they
  cost different amounts of the thing the player cares about, which is time and health.

  ENDPOINTS, not coordinates. A room is an endpoint if it has exactly one door. Nothing in this
  function knows where anything is on the grid; it asks the graph what its dead ends are and picks
  from those. That is what makes the layout free to change shape without this code changing with it,
  and it is the same reason the key rooms were moved off fixed positions in the first place.

## [h:20-world-9]
near: `function growForked(rs,x,y,dir,len,strict,bias,forkAt,forkLen){`

One trunk, with side runs cut off it.

  This is `grow` with forks added, and it is written as a separate function rather than as a flag on
  `grow` because the fork has to happen from INSIDE the walk - a branch can only be cut from a room
  that actually exists - and threading that through the existing loop meant giving `grow` two return
  shapes and one more parameter, for a caller that wants a different answer.

  `forkAt` is the list of step indices to cut from, and `forkLen` how long each cut is. Returns the
  path, the tip, and every endpoint the side runs produced.

## [h:20-world-10]
near: `if(step>=len-1) continue;`

NEVER FORK FROM THE TIP. The last room of a trunk is the one that becomes the upgrade room or
      the boss, and a branch hanging off it puts the key in that branch BEHIND the locked door the
      key is supposed to open. Measured before this was caught: 113 of 300 dungeons had an
      unreachable gold key, and the grid showed a branch running east out of the boss room.

      The tip has to stay an endpoint - one door - or the room that is supposed to be the end of
      the map is a corridor junction with a locked door on one side of it, which is not a room worth
      fighting towards.

## [h:20-world-11]
near: `const trunkLen=4+((Rnd.run()*2)|0);`

Two trunks out of the start, in different directions. The second one is grown AFTER the first,
so freeDir already knows the first trunk is there and the two cannot collide - which is what
stops the whole build being thrown away for a run that walks into its own corridor.

trunkLen 4-5 and forkLen 3-4 rather than something shorter. The first version of the tree used
3-4 and 2-3, which put a floor at 8 fight rooms in the worst case - and eight is not a floor, it
is a corridor with two decisions in it, and a player who reaches the boss in ninety seconds has
not been given the thing the map is for. The lengths are the cost side of the same trade: they
are what the retry loop in generateDungeon is paying for, and at 17 rooms against a 7x7 grid
there is still room for strict mode to refuse a cramped build and try again.

## [h:20-world-12]
near: `const aIsBoss=Rnd.run()<0.5;`

ROLES, assigned to the ENDS of what grew rather than to positions chosen in advance.

    The two trunks get the two rooms worth walking towards, and they are different on purpose: one
    end is the upgrade room and one is the boss, so a player who commits early to a direction has
    committed to what that direction was FOR. Which trunk gets which is a coin toss, so the map
    cannot be learned as "left is the boss".

    A trunk tip is a dead end by construction - it is where the walk stopped - so it is an endpoint
    and the same test covers it. The upgrade and boss rooms are marked so the door code can seal
    them, which is the only part of this that needs to know a room's job.

## [h:20-world-13]
near: `const aEnds=A.ends.filter(r=>isEndpoint(r)&&rs[key(r.x,r.y)]);`

THE KEYS, one per trunk, placed at a BRANCH END and chosen at generation.

    A branch end is an ordinary fight room with a key in it, so getting the key costs a detour and
    the detour can be blocked, which is the whole point of putting it there rather than on the
    spine. If a trunk produced no branch the build is thrown away rather than quietly putting the
    key on the spine, because a map that sometimes has a detour and sometimes does not is a map
    where the detour is not a decision.

## [h:20-world-14]
near: `allRooms=Object.values(rooms);`

Two things get derived here, once, instead of being rediscovered every tick. `allRooms` is
the room list as an array: the minimap walks it every frame and `Object.values` on the room
map allocated a fresh array 60 times a second for no reason. `bossFront` is the set of rooms
with a door onto the boss, which used to be found by an O(n^2) scan of the whole dungeon on
every single tick for the entire run until the warning fired.

## [h:20-world-15]
near: `function hasKeyFor(r,d){ return leadsToBoss(r,d)?player.hasGold:leadsToItem(r,d)?player.hasSilver:fa`

two locks, two colours: the gold key is the run objective and only opens the boss door, the
  silver key is the one the ordinary fight rooms hand out and it opens the upgrade room. A locked
  door does not open the instant you hold the key: the lock has to work while you stand at it, and
  you are free to walk away instead. That pause is the beat where you decide whether the run behind
  that door is worth taking.

## [h:20-world-16]
near: `function entryPoint(dir){`

spawning ----------
  The rooms are still wide open rectangles, so the only thing standing between a gunner and a
  free hit on the player as the door opens is where the wave lands. The plan therefore keeps
  three promises: bodies never start overlapped, gunners start on the far side of the room from
  the door the player walks in through, and nothing starts inside a clear firing line to that
  door. With the rooms wide open this is the only protection a player gets on the turn - and the
  gunners deliberately sense far wider than they will shoot from, so what matters most now is that
  they have to walk you down rather than snapping to a firing line the moment you appear.

## [h:20-world-17]
near: `const rolled=2+((Rnd.run()*(2+PRESSURE.rate))|0);`

more bodies at higher pressure. this is the only lever that makes a competent kiter work for
something, and it is the one that costs clear time, so it is what pays back the ground that
TEMPO gives away

depthBodies() adds a fraction of a body per floor on top of the roll rather than moving the roll
itself, so the depth ladder cannot change WHICH bodies a room draws - only how many. A deeper
floor should be a fuller room of the same fight, not a different fight: the player learns the
shapes on floor 1 and the shapes are still the shapes on floor 12.

## [h:20-world-18]
near: `for(const s of slots){`

A pack gets an ID and a slot index per body, and those two numbers are the entire formation
interface: the movement code never needs to know how many packs exist or where they started, only
which wall a body belongs to and where in it the body stands. PACK_CURSOR is a module counter
rather than something derived from the room, so two packs in one room are genuinely two walls
and cannot be mistaken for one formation of sixteen.

## [h:20-world-19]
near: `const packId=PACK_CURSOR;`

THE ID IS PER PACK, TAKEN ONCE, BEFORE THE BODIES. This incremented PACK_CURSOR inside the
      body loop as well as after it, so every body in a pack got its own id and no two of them ever
      agreed on which pack they were in. The wall rule asks for `packC[packId].n >= BRUNCH_WALL_MIN`
      - a count of bodies sharing one id - so with a unique id per body every pack counted 1 and the
      wall could never form.

      Measured over 40 seeds and 221 rooms containing Brunch: 1227 bodies, 1227 distinct ids, and
      ZERO packs reaching the threshold. An entire documented mechanic - "a Brunch pack is a WALL,
      not a crowd" - was dead in the game and live only in the boss's hand-placed wall, which uses a
      fixed id and so was unaffected. That is why it was never noticed: the one wall anyone had seen
      was the one that worked.
