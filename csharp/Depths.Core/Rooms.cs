using System.Collections.Generic;

namespace Depths
{
    /// <summary>
    /// Moving through the floor: doors (open only on a cleared room, sealed boss/item doors that a key
    /// unlocks by standing at them), the door-aware wall, the transition, and entering a room - which
    /// spawns its wave the first time and hands the fight its opening state. src/20-world.js,
    /// 40-combat.js clampPlayer/checkDoorTransition, 30-enemies.js enterRoom.
    ///
    /// A room keeps its own bodies and pickups (Room.Enemies/Pickups); the run's lists are the current
    /// room's, swapped on entry, so every phase reads run.enemies exactly as before.
    /// </summary>
    public static class Rooms
    {
        public static (double X, double Y) DoorPoint(Dir d) => d switch
        {
            Dir.N => (Balance.MidX, Balance.RoomTop),
            Dir.S => (Balance.MidX, Balance.RoomBottom),
            Dir.E => (Balance.RoomRight, Balance.MidY),
            _ => (Balance.RoomLeft, Balance.MidY),
        };

        static Dir Opp(Dir d) => d == Dir.N ? Dir.S : d == Dir.S ? Dir.N : d == Dir.E ? Dir.W : Dir.E;
        static double Dist(double x, double y) => System.Math.Sqrt(x * x + y * y);

        static RoomKind? Leads(RunState run, Room r, Dir d) => run.dungeon.Neighbour(r, d)?.Type;

        /// <summary>Sealed: not paid for yet, whatever is in your pocket.</summary>
        public static bool DoorSealed(RunState run, Room r, Dir d)
        {
            var t = Leads(run, r, d);
            if (t == RoomKind.Boss) return !run.bossUnlocked;
            if (t == RoomKind.Item) return !run.itemUnlocked;
            return false;
        }

        public static bool HasKeyFor(RunState run, Room r, Dir d)
        {
            var t = Leads(run, r, d);
            return t == RoomKind.Boss ? run.player.hasGold : t == RoomKind.Item && run.player.hasSilver;
        }

        public static bool AtDoor(RunState run, Dir d)
        {
            var (x, y) = DoorPoint(d);
            return Dist(run.player.x - x, run.player.y - y) < Balance.UnlockRange;
        }

        static bool Unlocking(RunState run, Room r, Dir d) => run.unlockRoom == r && run.unlockDir == d;

        /// <summary>doorPassable: a door that exists, in a cleared room, paid for, and not mid-unlock.</summary>
        public static bool DoorPassable(RunState run, Room r, Dir d) =>
            r.Doors.Contains(d) && run.enemies.Count == 0 && !DoorSealed(run, r, d) && !Unlocking(run, r, d);

        /// <summary>tickUnlock: standing at a sealed door with its key works the lock; once started it finishes.</summary>
        public static void TickUnlock(RunState run)
        {
            var r = run.CurrentRoom;
            if (r == null) return;
            if (run.unlockDir.HasValue)
            {
                var d0 = run.unlockDir.Value;
                if (!(run.unlockRoom == r && r.Doors.Contains(d0) && DoorSealed(run, r, d0))) { run.unlockDir = null; run.unlockRoom = null; }
            }
            if (!run.unlockDir.HasValue)
            {
                foreach (var d in new[] { Dir.N, Dir.S, Dir.E, Dir.W })
                {
                    if (!r.Doors.Contains(d) || !DoorSealed(run, r, d) || !HasKeyFor(run, r, d) || !AtDoor(run, d)) continue;
                    run.unlockDir = d; run.unlockRoom = r; run.unlockT = 0;
                    break;
                }
                if (!run.unlockDir.HasValue) return;
            }
            if (++run.unlockT >= Balance.UnlockTime)
            {
                if (Leads(run, r, run.unlockDir!.Value) == RoomKind.Boss) { run.bossUnlocked = true; run.player.hasGold = false; }
                else { run.itemUnlocked = true; run.player.hasSilver = false; }
                run.unlockDir = null; run.unlockRoom = null; run.unlockT = 0;
            }
        }

        /// <summary>clampPlayer: the walls hold, except through an open door's gap, and never more than 40px out.</summary>
        public static void ClampPlayer(RunState run)
        {
            var p = run.player;
            var r = run.CurrentRoom;
            bool inGapX = System.Math.Abs(p.x - Balance.MidX) < Balance.DoorWidth / 2.0;
            bool inGapY = System.Math.Abs(p.y - Balance.MidY) < Balance.DoorWidth / 2.0;
            bool open(Dir d) => r != null && DoorPassable(run, r, d);
            if (p.y < Balance.RoomTop + p.r && !(open(Dir.N) && inGapX)) p.y = Balance.RoomTop + p.r;
            if (p.y > Balance.RoomBottom - p.r && !(open(Dir.S) && inGapX)) p.y = Balance.RoomBottom - p.r;
            if (p.x < Balance.RoomLeft + p.r && !(open(Dir.W) && inGapY)) p.x = Balance.RoomLeft + p.r;
            if (p.x > Balance.RoomRight - p.r && !(open(Dir.E) && inGapY)) p.x = Balance.RoomRight - p.r;
            p.x = System.Math.Max(Balance.RoomLeft - 40, System.Math.Min(Balance.RoomRight + 40, p.x));
            p.y = System.Math.Max(Balance.RoomTop - 40, System.Math.Min(Balance.RoomBottom + 40, p.y));
        }

        /// <summary>checkDoorTransition: 18px past an open door's line, inside its gap, starts the transition.</summary>
        public static bool CheckDoorTransition(RunState run)
        {
            var r = run.CurrentRoom;
            if (r == null) return false;
            var p = run.player;
            bool gx = System.Math.Abs(p.x - Balance.MidX) < Balance.DoorWidth / 2.0, gy = System.Math.Abs(p.y - Balance.MidY) < Balance.DoorWidth / 2.0;
            if (p.y < Balance.RoomTop - 18 && DoorPassable(run, r, Dir.N) && gx) PassDoor(run, r, Dir.N);
            else if (p.y > Balance.RoomBottom + 18 && DoorPassable(run, r, Dir.S) && gx) PassDoor(run, r, Dir.S);
            else if (p.x > Balance.RoomRight + 18 && DoorPassable(run, r, Dir.E) && gy) PassDoor(run, r, Dir.E);
            else if (p.x < Balance.RoomLeft - 18 && DoorPassable(run, r, Dir.W) && gy) PassDoor(run, r, Dir.W);
            return run.trans != null;
        }

        public static void PassDoor(RunState run, Room r, Dir d)
        {
            if (run.trans != null) return;
            var n = run.dungeon.Neighbour(r, d);
            if (n == null) return;
            run.trans = new Trans { t = 0, nx = n.X, ny = n.Y, from = Opp(d).ToString() };
        }

        static void Stash(RunState run, Room r)
        {
            r.Enemies.Clear(); r.Enemies.AddRange(run.enemies); run.enemies.Clear();
            r.Pickups.Clear(); r.Pickups.AddRange(run.pickups); run.pickups.Clear();
        }

        static void Load(RunState run, Room r)
        {
            run.enemies.AddRange(r.Enemies); r.Enemies.Clear();
            run.pickups.AddRange(r.Pickups); r.Pickups.Clear();
        }

        /// <summary>
        /// enterRoom: the room becomes current and visited; its wave spawns the first time (a normal
        /// room's plan, the Warden at a random point, the secret's hook, the item room's weapons); the
        /// player is placed inside the door they came through; a live room hands back the gun and alt
        /// cooldowns and tops up the blink; the fade and the ready freeze start.
        /// </summary>
        public static void EnterRoom(RunState run, int nx, int ny, Dir from)
        {
            var old = run.CurrentRoom;
            if (old != null) Stash(run, old);
            run.curX = nx; run.curY = ny;
            var r = run.CurrentRoom!;
            r.Visited = true;
            run.projectiles.Clear();
            run.hookFields.Clear();   // clearTransient
            run.player.muzzleTimer = 0;
            Load(run, r);
            Items.CompassOpens(run);
            if (!r.Spawned)
            {
                r.Spawned = true;
                if (r.Type == RoomKind.Normal)
                {
                    var plan = run.planner.PlanWave(run.floor, from, AreaRules.AreaForFloor(run.floor), run.player.weaponIdx);
                    foreach (var b in plan.Bodies)
                    {
                        var e = Spawn.Body(run, b.Kind, b.X, b.Y, b.Trait);
                        if (b.Kind == BodyKind.Brunch && b.PackId > 0) { e.packId = b.PackId; e.packSlot = b.PackSlot; }
                        run.enemies.Add(e);
                    }
                }
                else if (r.Type == RoomKind.Boss) run.enemies.Add(Spawn.BodyAnywhere(run, BodyKind.Boss));
                else if (r.Type == RoomKind.Secret) run.pickups.Add(Pickup.Of("hook", Balance.MidX, Balance.MidY, 16));
                else if (r.Type == RoomKind.Item)
                {
                    // pickWeapons(2, held): a shuffle of the other three guns, two taken
                    var ws = new List<int>();
                    for (int i = 0; i < Weapons.All.Length; i++) if (i != run.player.weaponIdx) ws.Add(i);
                    for (int i = ws.Count - 1; i > 0; i--) { int j = (int)(run.rng.Run() * (i + 1)); (ws[i], ws[j]) = (ws[j], ws[i]); }
                    run.pickups.Add(Pickup.Weapon(Balance.MidX - 255, Balance.MidY, ws[0]));
                    run.pickups.Add(Pickup.Weapon(Balance.MidX + 255, Balance.MidY, ws[1]));
                    // the item half: two of the rolled rarity (luck-weighted), topped up from any rarity
                    // without the one just chosen; nothing already carried
                    var held = new List<string>();
                    foreach (var d in Items.All) if (Items.Equipped(run, d.Id) != null) held.Add(d.Id);
                    var ids = Items.Pool(run.rng, 2, Items.RollRarity(run.rng, run.stats.Value("luck")), held);
                    if (ids.Count < 2) { var ex = new List<string>(held); ex.AddRange(ids); ids.AddRange(Items.Pool(run.rng, 2 - ids.Count, null, ex)); }
                    for (int i = 0; i < ids.Count; i++)
                        run.pickups.Add(new Pickup { x = Balance.MidX + (i == 1 ? -85 : 85), y = Balance.MidY, r = 16, kind = "item", id = ids[i] });
                }
                r.Fought = run.enemies.Count > 0;   // the wave decides; PlanWave always plans 2+ bodies today
            }
            var p = run.player;
            if (from == Dir.N) { p.x = Balance.MidX; p.y = Balance.RoomTop + 34; }
            else if (from == Dir.S) { p.x = Balance.MidX; p.y = Balance.RoomBottom - 34; }
            else if (from == Dir.E) { p.x = Balance.RoomRight - 34; p.y = Balance.MidY; }
            else { p.x = Balance.RoomLeft + 34; p.y = Balance.MidY; }
            p.vx = p.vy = p.kvx = p.kvy = 0;
            p.lagX = p.x; p.lagY = p.y;
            bool uncleared = run.enemies.Count > 0;
            if (uncleared && !r.Armed)
            {
                r.Armed = true;
                p.cooldown = 0; p.altCooldown = 0;
                double held = run.blinkCharges + p.blinkRegen / Balance.BlinkRecharge;
                run.blinkCharges = (int)System.Math.Min(2, System.Math.Floor(held));
                p.blinkRegen = (held - run.blinkCharges) * Balance.BlinkRecharge;
                // and the bar then fills to both charges across the ready freeze (blinkRestore)
                run.blinkRestoreFrom = System.Math.Max(0, System.Math.Min(2, held));
                run.blinkRestoreT = run.blinkRestoreSpan = Balance.Ready;
            }
            run.roomFade = 1;
            run.fadeTicks = uncleared ? Balance.Ready : Balance.FadeClear;
            run.fadeT = run.fadeTicks;
            run.readyT = uncleared ? Balance.Ready : 0;
        }
    }
}
