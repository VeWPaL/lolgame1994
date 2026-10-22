# Items: the overhaul (design, 2026-10-22)

For the owner to edit. Today's roster (13, from the JS) is almost all stat sticks (+1 strength, +2 vigor), and the
two working actives run on fixed charges that never come back, so the bot's actives heal 0.2 times a run. The
overhaul keeps the JS roster for the parity tests and plays a new roster when tuned (`Balance.TuneItems`).

## The rules the items live by

- **Every item changes how a fight is played**, not only a number: what a shot does, what a blink does, what a kill
  or a cleared room gives, what Q does. Stats stay, as a side effect, never as the whole item.
- **Actives recharge by rooms** (Isaac): each has a recharge of 1-4 fought rooms; a cleared fight room adds one; Q
  spends a full charge. The Q cap shows pips. A new active swaps with the held one, which drops on the floor.
- **Tiers** stay common / uncommon / rare / legendary, with the luck-weighted roll. A few **cursed** items trade a
  strength for a weakness, marked in red on their plate.
- **Where they come from**: the item room (two items and two guns, as now); a boss drops one item of rare or better
  (new); a spike room's sure drop is an item one time in four (new).
- **Readable**: each item has a 26 px icon in the item style and a one-line plate ("Shots bounce once off walls").
- **The bot** uses every active with a plain rule (below), so A/B numbers can say whether an item matters.

## The roster (24)

| # | Item | Tier | Kind | What it does | Bot rule |
|---|---|---|---|---|---|
| 1 | **Hush Bell** | uncommon | active, 2 rooms | Rings out: every hostile shell within 220 px is gone and bodies there are knocked back (Gungeon's blank) | 3+ hostile shells within 150 px, or one within 40 px |
| 2 | **Time Glass** | rare | active, 3 rooms | 2.5 s of slow time: bodies and their shells at 35% speed, you at full | 4+ bodies within 250 px, or under half health in a fight |
| 3 | **Tin Cup** | common | active, 3 rooms | Heal 1 heart (2 HP) | missing 2+ HP |
| 4 | **Ricochet Shells** | common | passive | Your shots bounce once off a wall | - |
| 5 | **Ember Rounds** | common | passive | A hit sets the body burning: 1 damage a second for 3 s (refreshed, not stacked) | - |
| 6 | **Chain Spark** | rare | passive | A hit jumps to the nearest other body within 140 px for half its damage | - |
| 7 | **Glass Heart** | uncommon, cursed | passive | x1.25 damage; your maximum health drops by half a heart | - |
| 8 | **Phoenix Feather** | legendary | passive, once | The first time you would die, you rise with 2 hearts and a blink of invulnerability; it burns away | - |
| 9 | Long Barrel | common | passive | Shots fly 25% faster and the Bolt's fall-off starts 40% later | - |
| 10 | Quick Feet | common | passive | Blinks recharge 25% sooner | - |
| 11 | Thick Hide | common | passive | The first hit in each room costs 1 HP less | - |
| 12 | Static Coat | uncommon | passive | A shot within 1 s after a blink deals double | - |
| 13 | Splinter Bolts | uncommon | passive | A kill bursts into 3 friendly shards | - |
| 14 | Second Wind | uncommon | passive | Clearing a fight at 2 HP or less heals 1 more | - |
| 15 | Magnet Coil | uncommon | passive | Pickups drift to you from 200 px (Bone Whistle's pull, always on) | - |
| 16 | Momentum Engine | rare | passive | At full momentum your shots pierce one body | - |
| 17 | Vampire Fang | rare | passive | Every 12 damage you deal heals 1 HP | - |
| 18 | Orbiting Lantern | rare | passive | A lantern circles you and eats hostile shells (the egg's orbit, friendly) | - |
| 19 | Blood Pact | rare, cursed | passive | Kills have a 1 in 8 chance to heal 1 HP; heart pickups heal half | - |
| 20 | Hunter's Mark | rare | active, 2 rooms | Marks every body in the room: +50% damage taken for 6 s (today's, recharging) | 3+ bodies alive |
| 21 | Smoke Pot | uncommon | active, 2 rooms | Bodies within 200 px lose you: no shots or charges for 1.5 s | 3+ bodies within 200 px |
| 22 | Twin Barrel | legendary | passive | Every shot fires a second at 15 degrees for half damage | - |
| 23 | Brass Compass | legendary | passive | Opens the floor's secret wall when carried past it (today's) | - |
| 24 | Overcharge | legendary | active, 4 rooms | Refills both blinks and the blast at once | no blink charge left and a body within 120 px |

## Built first (this session): 1-8

The eight that change play most, across all four tiers, three of them actives: Hush Bell, Time Glass, Tin Cup
(recharging), Ricochet Shells, Ember Rounds, Chain Spark, Glass Heart, Phoenix Feather. 9-24 stay designed for the
owner to edit before they are built.

## The hooks Core needs (costed)

| Hook | Where | Used by | Cost |
|---|---|---|---|
| active recharge by rooms | `Items` (a slot's charge), `Rooms`/`TickRoom` on a won fight | every active | small |
| shell clear in a radius | `Items.RunHook` | Hush Bell | small |
| slow time for bodies and hostile shells | a `run.slowT` read where bodies and shells step (`sm`, shell step) | Time Glass | medium |
| on wall (a friendly shell) | the shell step's wall test | Ricochet Shells | small |
| on hit (a friendly shell) | `Combat` where a shell damages a body | Ember Rounds, Chain Spark | small |
| burning, a damage-over-time | a body's burn clock in the body step | Ember Rounds | small |
| damage multiplier and max health | `Stats` (strength, vigor) | Glass Heart | none (stats) |
| on death (the player's) | `Combat.DamagePlayer` / the death check | Phoenix Feather | small |
| on blink, on kill, on clear | `TryBlink`, `Kills.KillEnemy`, the clear refill | 10-14, 17, 19 | small each |
| friendly orbiter | the egg's orbit (`Eggs`), friendly | Orbiting Lantern | medium |
