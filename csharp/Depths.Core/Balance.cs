namespace Depths
{
    /// <summary>Every dial in the game, ported from src/00-balance.js. [h:Balance-1]</summary>
    public static class Balance
    {
        // ------------------------------------------------------------- pacing

        /// <summary>The JS reference rate (60 x its 3.5 speedup). The parity rows were recorded at it.</summary>
        public const int JsHz = 210;

        /// <summary>The game's rate: one tick per frame at 60 fps.</summary>
        public const int GameHz = 60;

        static int _tickHz = GameHz;

        /// <summary>The one tick-rate setting. [h:Balance-2]</summary>
        public static int TickHz
        {
            get => _tickHz;
            set
            {
                if (value < 1 || value > 10000) throw new System.ArgumentOutOfRangeException(nameof(value), "a tick rate in Hz, 1 to 10000");
                _tickHz = value;
                Recompute();
            }
        }

        static Balance() => Recompute();

        /// <summary>Seconds to whole ticks. The rounding is in the original and is load-bearing.</summary>
        public static int Sec(double seconds) => (int)System.Math.Round(seconds * TickHz);

        /// <summary>Seconds to fractional ticks, for counters the game keeps as doubles (cooldowns).</summary>
        public static double SecF(double seconds) => Snap(seconds * TickHz);

        /// <summary>A per-second quantity (px/s, or a meter's units/s) as its per-tick step.</summary>
        public static double PerSec(double perSecond) => Snap(perSecond / TickHz);

        /// <summary>A multiplicative per-tick factor defined at the JS tick: f^(JsHz/hz), so f^JsHz per second holds.</summary>
        public static double Decay(double perJsTick) =>
            TickHz == JsHz ? perJsTick : System.Math.Pow(perJsTick, (double)JsHz / TickHz);

        /// <summary>A lerp share per JS tick (x += (target - x) * a) at this rate: what is left, 1 - a, decays exactly.</summary>
        public static double Ease(double perJsTick) =>
            TickHz == JsHz ? perJsTick : 1 - System.Math.Pow(1 - perJsTick, (double)JsHz / TickHz);

        /// <summary>Ease(a) / a, appended to an expression that multiplies by a, so the JS-rate arithmetic stays bit-exact.</summary>
        public static double EaseK(double perJsTick) => TickHz == JsHz ? 1 : Ease(perJsTick) / perJsTick;

        /// <summary>A per-JS-tick probability at this rate, keeping the chance of at least one success per second.</summary>
        public static double Chance(double perJsTick) => Ease(perJsTick);

        // Fifteen significant digits: a decimal dial divided by the rate lands on the same double as its old literal.
        static double Snap(double x)
        {
            if (x == 0 || double.IsNaN(x) || double.IsInfinity(x)) return x;
            int digits = 15 - (int)System.Math.Ceiling(System.Math.Log10(System.Math.Abs(x)));
            return digits < 0 ? x : System.Math.Round(x, System.Math.Min(15, digits));
        }

        /// <summary>Parts a shell's move and a body's contact test are cut into per tick: none longer than a JS tick's, so neither steps over a hitbox.</summary>
        public static int Substeps => TickHz >= JsHz ? 1 : (JsHz + TickHz - 1) / TickHz;

        static double _knockFriction, _knockPFriction, _knockScale, _knockPScale;

        static void Recompute()
        {
            _knockFriction = Decay(KnockFrictionJs);
            _knockPFriction = Decay(KnockPFrictionJs);
            // a knock is defined by how far it coasts, v / (1 - f); scaling v by this keeps that distance exact
            _knockScale = (1 - _knockFriction) / (1 - KnockFrictionJs);
            _knockPScale = (1 - _knockPFriction) / (1 - KnockPFrictionJs);
        }

        // ------------------------------------------------------------- the room

        public const int RoomLeft = 50, RoomRight = 750, RoomTop = 130, RoomBottom = 580;
        public const int MidX = (RoomLeft + RoomRight) / 2, MidY = (RoomTop + RoomBottom) / 2;



        // ------------------------------------------------------------- spawning

        /// <summary>Where a body may NOT be, and how far apart the ones that may. [h:Balance-3]</summary>
        public const double SpawnMargin = 76, SpawnMid = 104, SpawnSep = 158, SpawnDoor = 130, SpawnFar = 210;

        /// <summary>How far in from a wall the player enters, and therefore where a body must not start.</summary>
        public const double EntryInset = 36;

        /// <summary>A room's bounds in world space. [h:Balance-4]</summary>
        public readonly struct Room
        {
            public readonly int L, T, R, B;
            public Room(int l, int t, int r, int b) { L = l; T = t; R = r; B = b; }
            public int W => R - L;
            public int H => B - T;
            public int Cx => L + W / 2;
            public int Cy => T + H / 2;
            public static readonly Room Standard = new Room(RoomLeft, RoomTop, RoomRight, RoomBottom);
            public override string ToString() => $"{W}x{H} at ({L},{T})";
        }

        /// <summary>How far away a gunner has to be before counterstrafing buys it anything. [h:Balance-5]</summary>
        public static int SwerveDeadzone(Room room) => (int)System.Math.Round(room.W * 0.25);
        public static int SwerveFullBase(Room room) => (int)System.Math.Round(room.W * 0.55);

        // Methods rather than a property plus an overload, because C# will not let a property and a method share a name... [h:Balance-6]
        public static int SwerveFull(Room room) => SwerveDeadzone(room) + SwerveFullBase(room);
        public static int SwerveDeadzone() => SwerveDeadzone(Room.Standard);
        public static int SwerveFull() => SwerveFull(Room.Standard);

        /// <summary>How far a body can be and still notice the player: 0.85 of the room's diagonal. [h:Balance-7]</summary>
        public static int AggroRange(Room room) =>
            (int)System.Math.Round(0.85 * System.Math.Sqrt((double)room.W * room.W + (double)room.H * room.H));

        // ------------------------------------------------------------- the player

        /// <summary>Declared in the art module in the original because the enemy table reads it. [h:Balance-8]</summary>
        public static double PlayerMove => PerSec(252);   // px/s (1.2 px per JS tick)

        public const double TempoRate = 1.5;
        public const double PressureRate = 1.5;

        /// <summary>THE MOMENTUM DIALS, straight from src/00-balance.js:1020-1024. [h:Balance-9]</summary>
        public const double MomentumGain = 0.0056;
        public static double MomentumStallDecay => PerSec(1.26);   // meter per second
        public static double MomentumMoveFloor => PerSec(63);       // px/s: slower than this stalls

        /// <summary>How much a crowded room shortens a body's cadence, src/00-balance.js:1160 (PRESSURE_CLOSURE's twin). [h:Balance-10]</summary>
        public const double PressureCadence = 0.4;

        /// <summary>The Warden's pacing dials, from src/00-balance.js:430-476. [h:Balance-11]</summary>
        public static int BossCdMin => Sec(3.2);
        public static int BossCdVar => Sec(1.0);
        public static int BossRecover => Sec(1.1);
        public static int BossFirstCd => Sec(1.2);   // the Warden's first move comes this soon
        public static int BossPhaseCd => Sec(0.4);   // and the first after a phase change
        public const double BossPhase1 = 0.66, BossPhase2 = 0.33;   // fractions of max HP

        /// <summary>The fade out of a room transition, src/00-balance.js:251 (FADE_OUT).</summary>
        public static int FadeOut => Sec(0.35);

        /// <summary>The ready window after entering a room, src/00-balance.js:251 (READY).</summary>
        public static int Ready => Sec(0.75);

        /// <summary>The fade in at the start of a run, src/50-run.js:109 (fadeTicks=sec(0.4)).</summary>
        public static int FadeStart => Sec(0.4);

        /// <summary>A fresh body notices the player within this many ticks: 16 frames at the JS's old 60 fps.</summary>
        public static double NoticeWindow => SecF(16 / 60.0);

        /// <summary>DY slides the hit circle down onto the actual mass of the character - the chest and waist, which is rows... [h:Balance-12]</summary>
        public const int PlayerHitDy = 10, PlayerHitR = 10;

    /// <summary>Ticks a struck body stays flashed. [h:Balance-13]</summary>
    public static int HitFlash => Sec(0.13);   // 27 at the JS rate

    // The projectile phase. Values read from the running game, 2026-10-06.
    public static int Iframes => Sec(1.0);                  // IFRAMES
    public const double MomentumHitKeep = 0.55;             // a hit keeps 55% of the meter
    public const double PierceFalloff = 0.72;               // each body a piercing bolt drills through
    public static int BrunchAbsorbFlash => Sec(0.1);        // a Brunch flashes this long when it eats a shell
    public const int BossWallId = -1;                       // the pack id of the statues the Warden calls
    // a hit shoves, never launches (5 px per JS tick, as coast distance); below KnockTrade no trade
    public static double KnockMax => 5 * _knockScale;
    public static double KnockTrade => 0.09 * _knockScale;
    public static int KnockStun => Sec(0.42);               // KNOCK_STUN
    public static int WanderTicks => Sec(1);                // WANDER_TICKS
    public const int PressureSpan = 4;                      // PRESSURE_SPAN
    public const double PressureFloor = 0.25;               // PRESSURE_FLOOR
    // GUNNER_DODGE: a gunner sees a bolt coming within `sight`, sidesteps half the time (a chance per tick), on a cooldown
    public const double GunnerDodgeSight = 250;
    public static double GunnerDodgeChance => Chance(0.5);
    public static double GunnerDodgeKick => 0.6 * _knockScale;
    public static int GunnerDodgeCd => Sec(0.6);

    // The right-click blast, ALT_WEAPON, read from the game 2026-10-06.
    public static int AltCooldown => Sec(3.6);              // /TEMPO at the cast
    public static double AltSpeed => PerSec(392.7);         // px/s (2.2 * 0.85 px per JS tick)
    public const double AltR = 12, AltAoe = 100, AltKnock = 2.7;
    public const double AltPool = 18 * 1.35 / 0.66 * 1.02;  // 18*TOUGH/ARMOUR*1.02 = 37.5545...
    /// <summary>The Brunch guard rule, as the JS playtest flag ?brunch= (A: guard the shooter wherever the player is; B... [h:Balance-14]</summary>
    public static string BrunchVariant = "A+";
    public const double BrunchGuardLeash = 420;

    // Hearts, 2026-10-06 (C# only): HP is whole numbers, 1 HP = half a heart, 8 at the start. [h:Balance-15]
    public const double ArmorTake = 0.6;
    public const int HeartHeal = 2, HalfHeal = 1;
    // The regenerating heart: the last of the starting 8 HP. After RegenDelay without taking damage, while
    // the room has live bodies, +1 HP, then +1 every RegenStep; clearing a fought room refills it fully.
    public const int RegenHp = 2;
    public static int RegenDelay => Sec(4);
    public static int RegenStep => Sec(1);
    /// <summary>The JS game's damage rules (fractional hits, armour 1:1, no half pickups), for the parity tests whose... [h:Balance-16]</summary>
    public static bool JsReference = false;
    public const double JsShotDmg = 1.8, JsBossShellDmg = 1.44, JsBossSweepMult = 1.4;

    // ---- Playtest tuning (C# only, owner's notes of 2026-10-13). On in the game and the bot; the test
    // assembly turns it off ([TunedOn(false)]) so the parity rows keep playing the JS, as with Curve.On.
    public static bool Tuned = true;
    /// <summary>Each part of the tuning on its own, for measuring them apart (the bot's --untune). All on in the game.</summary>
    public static bool TuneWall = true, TuneRush = true, TuneDodge = true, TuneLoot = true, TuneBeam = true, TuneWarden = true;
    /// <summary>Eased motion (owner 2026-10-13: the dodge and enemy moves "too snappy and robotic"): a dash speeds up and
    /// slows down instead of jumping to full speed, and a ranged body glides into and out of its walk.</summary>
    public static bool TuneEase = true;
    /// <summary>The player's damage at <see cref="PlayerDmgMult"/> (owner 2026-10-13: "fights seem a little too winnable",
    /// a test of 30% less). Every hit the player lands: shots, the blast, the hook's field.</summary>
    public static bool TuneDamage = true;
    public const double PlayerDmgMult = 0.7;
    /// <summary>What the player's hits are multiplied by now: 1 untuned, so the JS rows stay exact.</summary>
    public static double PlayerDmg => Tuned && TuneDamage ? PlayerDmgMult : 1;
    /// <summary>Momentum you build and feel (owner 2026-10-13: "ramp up slower and more noticeably, as of right now it feels... [h:Balance-17]</summary>
    public static bool TuneMomentum = true;
    public static double MomGain => Tuned && TuneMomentum ? 0.0007 : MomentumGain;   // meter per px moved (rate-free)
    public static double MomStallDecay => Tuned && TuneMomentum ? PerSec(0.8) : MomentumStallDecay;   // meter per second
    public static double MomSpeed => Tuned && TuneMomentum ? 0.40 : MomentumSpeed;   // speed bonus at a full meter
    public static double MomCap => Tuned && TuneMomentum ? 0.65 : MoveSpeedHardCap;   // the ceiling with momentum; the speed stat alone stays at 0.44
    public static double MomAccel => Tuned && TuneMomentum ? 1.0 : MomentumAccel;    // acceleration bonus at a full meter
    /// <summary>Each run rolls which kinds each area brings, the middle five among them (Pools.cs; owner 2026-10-13).</summary>
    public static bool TunePools = true;
    public static bool TuneDepth = true;   // the middle lift: floors 5-12 climb toward the Deep instead of idling (DepthLift)
    public static bool TuneSpike = true;   // spike rooms on floors 5-12 (Spikes.cs); they need TuneLoot, which pays them
    public static bool TuneEnd = true;     // the run ends: floor 14 is an antechamber and the final boss, whose death wins
    public static bool TuneItems = true;   // the item overhaul (docs/items.md): the new roster, actives that recharge by rooms
    public const int FinalFloor = 14;
    /// <summary>Whether this run can be won (tuned): untuned, the JS's endless descent.</summary>
    public static bool RunEnds => Tuned && TuneEnd;
    /// <summary>The middle five's retune of 2026-10-13 (Middle.Sharp): aim, split, lead, chase, cone, ally-safe volleys.</summary>
    public static bool TuneMiddle = true;
    /// <summary>Every ranged body aims with Aim.Intercept (owner 2026-10-14: shots "completely away" from a straight walker).</summary>
    public static bool TuneAim = true;
    public static double AimHorizon => SecF(2.4);   // the longest lead a shot is solved for: a flight across the room
    /// <summary>"Close": the Scatter's full-damage reach. Inside it a Lunger does not dodge and a Brunch
    /// rushes you instead of walking to its wall slot. Read from the gun, so the two cannot drift apart.</summary>
    public static double CloseRange => Weapons.All[1].FNear ?? 180;
    public static double BrunchGuardSpeed => PerSec(320);     // onto a wall slot: was BrunchShieldSpeed (151 px/s)
    public static int BrunchGuardArrive => Sec(0.25);         // eases into the slot: speed = distance / this, capped
    public const double LungerDodgeDist = 72;                 // px, sideways off a bolt's line
    public const double LungerDodgeBlastMax = 130;            // px, the furthest it will go to clear a blast
    public static int LungerDodgeCd => Sec(1.4);
    public static int LungerDodgeReact => Sec(0.12);          // a shot arriving sooner than this is not dodged
    public static int LungerDodgeHorizon => Sec(0.6);         // a shot arriving later than this is not a threat yet
    public static int LungerStartle => Sec(1.5);              // new to the room this long: its first dodge ignores range and reaction
    public const double BrunchRushRelease = 24;               // px of hysteresis, so a Brunch on the line does not flip every tick
    public static int BrunchRushWind => Sec(0.25);            // a Brunch brakes and shivers this long before a dash: the tell
    public const double BrunchChaseCommit = 90;               // px: a chasing Brunch (no wall to hold) winds up this close
    public const double BrunchDashGain = 1.4;                 // the dash is its run speed x this, in a line locked as the tell ends
    public const double BrunchDashOver = 60;                  // px it carries on past where the player will be
    public const double BrunchRetreatCap = 0.75;              // a retreat along the line is planned for up to this share of the dash speed
    public const double BrunchTellHold = 2;                   // the tell holds until the player is this x its reach away
    public const double BrunchDashStagger = 0.07;             // each pack slot dashes this much slower (slot % 4): a pack lands in a ripple
    public static int BrunchDashMax => Sec(0.8);              // the longest a dash runs
    public static int BrunchRushRest => Sec(0.35);            // after a dash it skids to a stop and cannot wind up again
    public const double RoomDropChance = 0.75;                // the last kill of a fought room; nothing before it
    public const double BrunchBeamTaken = 0.45;               // Brunch take this x an Arcane Beam bolt: 2-3 bolts, not 1
    // TuneEase: each dash is a speed curve, up over the first share of it and down over the last (smoothstep ramps)
    public const double LungeRampIn = 0.2, LungeRampOut = 0.25;   // same length and reach as the flat lunge, so the aim holds
    public const double DodgeRampIn = 0.25, DodgeRampOut = 0.4;   // same distance, a little longer...
    public const double DodgePeak = 1.15;                         // ...topping out just above lunge speed, so it stays snappy
    public static double RangedWalkEase => Ease(0.045);           // per JS tick: about 0.1 s to reach or leave walking speed

    // Placeholder item effects (C# only, 2026-10-06; the item overhaul replaces them).
    public static int MarkTicks => Sec(5);           // Hunter's Mark: how long a body stays marked
    public const double MarkVuln = 1.5;              // and the damage it takes meanwhile

    // Doors and rooms, read from the game 2026-10-06.
    public static int UnlockTime => Sec(0.5);
    public static int FadeClear => Sec(0.15);
    public const double UnlockRange = 64;

    // The Warden, read from the game 2026-10-06.
    public const int BossVolleyN = 3, BossWallHp = 5;
    public static int BossVolleyGap => Sec(0.55);
    public static int BossWallLife => Sec(14);
    public static double BossShotSpeed => PerSec(399);   // px/s
    public const double BossSweepDist = 150, BossShellDmg = 2, BossSweepDmg = 3;   // whole HP since 2026-10-06 (JS: 1.44, and the sweep was shell x 1.4)

    // The hook (HOOK_WEAPON and its field), read from the game 2026-10-06.
    public static double HookSpeed => PerSec(428.4);     // px/s
    public static double HookSuck => PerSec(50.4);       // px/s at full power
    public const double HookR = 14, HookAoe = 118, HookPull = 1.2, HookDps = 3.5;   // HookDps is per second
    public static int HookHold => Sec(0.7);
    public static int HookField => Sec(1.8);
    public static int HookForget => Sec(7);
    public static int HookEarlyMin => Sec(0.18);
    // a body in a field stays stunned and lit while it is in it: 3 and 1 JS ticks per unit of power
    public static double HookFieldStun => SecF(3.0 / JsHz);
    public static double HookFieldFlash => SecF(1.0 / JsHz);
    public static readonly double[] HookResist = { 1, 0.7, 0.45, 0.2, 0.08 };

    // The blink, read from the game 2026-10-06.
    public const int BlinkFillClear = 9;                    // a charge refills 9x faster in a cleared room
    public static int BlinkIframes => Sec(0.17);
    public static int BlinkGrace => Sec(0.6);
    public static int DashTrail => Sec(0.23);
    public const double AltKnockNear = 2, AltKnockFar = 0.35, Disperse = 2.3, ShootSlowAlt = 0.45;

    /// <summary>roomPressure: 1 with one body or none, easing to PRESSURE_FLOOR at PRESSURE_SPAN+1 bodies.</summary>
    public static double RoomPressure(int live)
    {
        if (live <= 1) return 1;
        if (live >= PressureSpan + 1) return PressureFloor;
        return PressureFloor + (1 - PressureFloor) * (PressureSpan + 1 - live) / (double)PressureSpan;
    }

    /// <summary>How often, in ticks, an UNGUARDED pack looks for a body to shield. [h:Balance-18]</summary>
    public static int BrunchScanTicks => Sec(0.095);   // 20 at the JS rate

    /// <summary>The smallest pack that will form a shield arc. [h:Balance-19]</summary>
    public const int BrunchShieldMin = 2;

    /// <summary>The smallest pack that will form a wall around an unguarded position. [h:Balance-20]</summary>
    public const int BrunchWallMin = 3;

    /// <summary>How fast a pack moves while it is holding a shield slot, rather than closing. [h:Balance-21]</summary>
    public static double BrunchShieldSpeed => PerSec(151.2);   // px/s

    /// <summary>How fast a pack gains speed when closing on its slot. [h:Balance-22]</summary>
    public static double BrunchAccel => Ease(0.09);   // per JS tick

    /// <summary>How fast a pack sheds speed when leaving a slot. [h:Balance-23]</summary>
    public static double BrunchDecel => Ease(0.16);

    /// <summary>How close a Brunch must be before it stops steering and simply reaches. [h:Balance-24]</summary>
    public const double BrunchDeadzone = 6;


    /// <summary>Where the shield forms, as a FRACTION OF THE GAP between the guarded body and the player. [h:Balance-26]</summary>
    public const double BrunchShieldFrac = 0.25;

    /// <summary>How far BEHIND the front rank a second-rank body stands. [h:Balance-27]</summary>
    public const double BrunchWallRank = 17;

    /// <summary>The narrowest the arc cone may be, in radians. 0.15707963 = 9 degrees.</summary>
    public const double BrunchArcFloor = 0.15707963267948966;

    /// <summary>The widest the arc cone may be, in radians. [h:Balance-28]</summary>
    public const double BrunchArcCeil = 1.0471975511965976;

    public static double BrunchRun => PerSec(441);   // px/s

    /// <summary>A Brunch's walk speed - its starting point, and its speed while it has not committed. [h:Balance-29]</summary>
    public static double BrunchWalkSpeed => PerSec(131.04);   // px/s

    /// <summary>The angular gap between two Brunch holding a shield arc, in pixels at the target's radius. [h:Balance-30]</summary>
    public const double BrunchArcGap = 19;

    /// <summary>A guarded ranged body stands off at far * this rather than far, so a Brunch escort pulls its charge back and... [h:Balance-31]</summary>
    public const double GuardStandoffMult = 1.45;

    /// <summary>How much a crowded room closes the standoff distance. [h:Balance-32]</summary>
    public const double PressureClosure = 0.85;

    /// <summary>How much of the swerve meter widens a gunner's aim cone, in radians at the extremes. [h:Balance-33]</summary>
    public const double SwerveAim = 0.3;

    /// <summary>How much a SWERVE costs a gunner's confidence in its intercept while it is casting, when it is planted and... [h:Balance-34]</summary>
    public const double SwerveTrustRooted = 1.0;

    /// <summary>The same cost while the gunner is moving. [h:Balance-35]</summary>
    public const double SwerveTrustWalking = 2.2;



    /// <summary>Ticks the boss warning stays on screen once the boss room comes into view: 3.2 seconds (672 at the JS rate). [h:Balance-36]</summary>
    public static int BossWarnTime => Sec(3.2);

    /// <summary>The width of a doorway in the wall, and the half-width the player must be inside for a door to accept them. [h:Balance-37]</summary>
    public const int DoorWidth = 90;

    /// <summary>Below this magnitude a knockback component is set to exactly zero, so a body that has all but stopped does... [h:Balance-38]</summary>
    public static double KnockCut => 0.006 * _knockScale;

    /// <summary>The same cut on the player's knockback, which coasts on its own friction.</summary>
    public static double KnockPCut => 0.006 * _knockPScale;

    /// <summary>Per-tick decay on the PLAYER's knockback, distinct from KnockFriction which is the body's. [h:Balance-39]</summary>
    public const double KnockPFrictionJs = 0.958;
    public static double KnockPFriction => _knockPFriction;

    /// <summary>What a knock impulse on the player is multiplied by at this rate (1 at the JS rate).</summary>
    public static double KnockPScale => _knockPScale;

    /// <summary>How fast the player's direction-of-travel estimate chases the real velocity. [h:Balance-40]</summary>
    public static double LungeBeliefTrack => Ease(0.14);   // per JS tick

    /// <summary>How much a full Momentum meter improves acceleration. [h:Balance-41]</summary>
    public const double MomentumAccel = 0.55;

    /// <summary>The fraction of the remaining velocity gap closed each tick. [h:Balance-42]</summary>
    public const double MoveAccel = 0.116;   // per JS tick; TickPlayer converts it with momentum's share (Ease)

    /// <summary>The hard ceiling on the movement bonus. [h:Balance-43]</summary>
    public const double MoveSpeedHardCap = 0.44;

    /// <summary>What a full Momentum meter adds to the movement bonus. Read out: 0.18.</summary>
    public const double MomentumSpeed = 0.18;

    /// <summary>Below this speed the player's direction-of-travel estimate stops updating, so a player standing still is not... [h:Balance-45]</summary>
    public static double PlayerSpeedEps => PerSec(10.5);   // px/s

    // Speeds below which something counts as still: a held direction (and a body's walk cycle), the
    // player's walk cycle, a blink's fallback direction, a Brunch's eased velocity. 0.05, 0.12, 0.1, 0.02 px per JS tick.
    public static double MoveEps => PerSec(10.5);
    public static double PlayerAnimEps => PerSec(25.2);
    public static double BlinkDirEps => PerSec(21);
    public static double BrunchStill => PerSec(4.2);

    /// <summary>Per-tick recovery of the firing slow-motion. [h:Balance-46]</summary>
    public static double ShootSlowRecover => PerSec(22.05);   // per second

    /// <summary>Per-tick easing of the firing slow-motion toward its target of 1. [h:Balance-47]</summary>
    public static double SlowEase => Ease(0.079);   // per JS tick

    /// <summary>Pixels of animation phase per pixel travelled. [h:Balance-48]</summary>
    public const double Stride = 38.5;



    /// <summary>Ticks for one blink charge, and the divisor behind the half-charge a cleared room refunds. [h:Balance-49]</summary>
    public static int BlinkRecharge => Sec(3.5);

    /// <summary>The cooldown of weapon index, in ticks (the weapon table holds it in seconds). [h:Balance-50]</summary>
    public static double WeaponCooldown(int index)
    {
        var all = Weapons.All;
        if (index < 0) index = 0;
        if (index >= all.Length) index = all.Length - 1;
        return all[index].Cooldown;
    }

        /// <summary>The hitbox the enemies aim at trails the real position and converges over roughly 0.4s - the reaction window... [h:Balance-51]</summary>
        public static double HitboxLagEase => Ease(0.05);   // per JS tick

        /// <summary>How long a body stays noticed after something hits it. [h:Balance-52]</summary>
        public static int AggroTime => Sec(2.5);

        /// <summary>The slow a landed shell applies, in ticks, and what fraction of speed it leaves behind. [h:Balance-53]</summary>
        public static int HitSlowTicks => Sec(0.55);
        public const double HitSlowMult = 0.62;

        /// <summary>The blink was 140px on an 8s recharge, and together those two made it a teleport with a long wait rather than... [h:Balance-54]</summary>
        public const int BlinkDist = 116;

        /// <summary>The blink's landing burst. [h:Balance-55]</summary>
        public const double BlinkBoostGain = 1.55;
        public static int BlinkBoost => Sec(0.11);

        // ------------------------------------------------------------- knockback

        // Frictions are per JS tick; Decay converts them. Gains are impulses, scaled by KnockScale where applied.
        public const double KnockFrictionJs = 0.976, KnockGain = 0.294, KnockPGain = 0.3, KnockBounce = 0.6;
        public static double KnockFriction => _knockFriction;

        /// <summary>What a knock impulse on a body is multiplied by at this rate (1 at the JS rate).</summary>
        public static double KnockScale => _knockScale;

        /// <summary>A knock of speed v coasts v/(1-friction) pixels, so to cover a distance d you want v = d*(1-friction). [h:Balance-56]</summary>
        public const double HookPullGain = 1 - KnockFrictionJs;   // Knock scales it to this rate's friction

        // ------------------------------------------------------------- lungers

        public const double LungerPay = 0.96;
        public const double LungerWalk = 0.3 * LungerPay;
        public const double LungerRun = 0.82 * LungerPay;
        public const int MaxArmor = 4;

        /// <summary>The ramp. A walker used to ease toward its top speed at one fixed rate, so its approach was a straight line... [h:Balance-57]</summary>
        public const double LungerAccel = 0.0058;   // per JS tick; the callers scale it, then convert (EaseK)
        public static double WanderSpeed => PerSec(52.5);   // px/s

        /// <summary>The Brunch ramp: ticks over which a pack's chase speed climbs from its walk to its run. [h:Balance-58]</summary>
        public static int BrunchRamp => Sec(1.5);
        public const double BrunchRampGain = 1.9;

        // ------------------------------------------------------------- the lunge

        /// <summary>THE STANDOFF is what makes this mechanic a mechanic. [h:Balance-59]</summary>
        public const int LungeHold = 78;
        public const int LungeMin = 34;

        /// <summary>How far the lunge can be aimed at all. [h:Balance-60]</summary>
        public const int LungeReach = 280;
        public const int LungeFloor = 56;

        /// <summary>How the lunger believes the player is moving. [h:Balance-61]</summary>
        public static double LungeTrack => Ease(0.014);   // per JS tick

        /// <summary>Iterations for the intercept's fixed point. [h:Balance-62]</summary>
        public const int LungeIter = 3;

        public static int LungeCd => Sec(1.15);
        public static int LungeWindup => Sec(0.34);
        public static double LungeSpeed => PerSec(882);   // px/s
        public static int LungeRecover => Sec(0.55);
        public const int LungeRange = 178;
        public static double LungerNear => PerSec(115.5);   // px/s
        public static double LungerFar => PerSec(273);

        /// <summary>Two lungers meeting mid-charge come off both worse.</summary>
        public const double LungeClash = 5.5;

        /// <summary>How fast a lunger walks its own angle around the player, in px/tick, aimed tangentially so it can never... [h:Balance-63]</summary>
        public static double LungerSpread => PerSec(189);   // px/s

        // ------------------------------------------------------------- counterstrafing

        /// <summary>How fast a reversal is forgotten, and how much a reversal widens a gunner's aim.</summary>
        public const double SwerveGain = 0.22;

        /// <summary>Per-tick decay of the swerve meter - how fast the game forgets that the player reversed. [h:Balance-64]</summary>
        public static double SwerveDecay => PerSec(0.735);   // meter per second

        // SwerveDeadzone and SwerveFull used to live here, reading (RoomRight - RoomLeft) / 2 and +220 - which is 350... [h:Balance-65]

        // ------------------------------------------------------------- the gunner

        /// <summary>The gunner's cast tell. [h:Balance-66]</summary>
        public static int CastTime => Sec(0.5);

        /// <summary>A cast's shell leaves the tick after the cast ends: 1/210 s at the JS rate, a whole 1/60 s at 60. [h:Balance-67]</summary>
        public static double CastLag => TickHz == JsHz ? 0 : 1 - (double)TickHz / JsHz;

        /// <summary>Iterations for the gunner's intercept. [h:Balance-68]</summary>
        public const int GunIter = 14;

        /// <summary>The floor on a gunner's spread, in radians. [h:Balance-69]</summary>
        public const double GunSpreadFloor = 0.02;

        // ------------------------------------------------------- the depth ladder

        /// <summary>The growth and time constant of the health dial, and the rate dial, and the growth of the density dial. [h:Balance-70]</summary>
        public const double DepthGrowth = 0.40;
        public const double DepthPow = 0.12;

        /// <summary>The reaction-time ceiling on the RATE dial, and the only one in the file. [h:Balance-71]</summary>
        public const double DepthRateCap = 1.34;

        /// <summary>Density is capped too, and for a DIFFERENT reason: not fairness but playability. [h:Balance-72]</summary>
        public const double DepthBodyPow = 0.085;
        public const double DepthBodyCap = 28;

        /// <summary>The Brunch pack chance. [h:Balance-73]</summary>
        public const double DepthPackStep = 0.035, DepthPackCap = 0.85;

        /// <summary>The number of floors below the first one, floored at zero.</summary>
        public static int DepthSteps(int floor) => System.Math.Max(0, floor - 1);

        /// <summary>The curve: 1 + growth*(e^(rate*steps) - 1). [h:Balance-74]</summary>
        public static double Ramp(int steps, double growth, double rate) =>
            1 + growth * (System.Math.Exp(rate * steps) - 1);

        // the middle lift (TuneDepth): 0 through floor 4, rising evenly to 1 at floor 10, then LiftDeep below 12 (the Deep
        // climbs on health already). Pace, bodies and Brunch packs, never health: the middle gets denser, not longer
        public const double LiftRate = 0.25, LiftBodies = 0.8, LiftPack = 0.15, LiftDeep = 0.5;
        public const int LiftFull = 8;
        public static double DepthLift(int floor) =>
            !(Tuned && TuneDepth) ? 0 : floor > 12 ? LiftDeep : System.Math.Max(0, System.Math.Min(1, (floor - 4) / (double)(LiftFull - 4)));

        // All four dials carry the floor's Curve shape (identity when Curve.On is false); the rate cap binds after it.
        public static double DepthTough(int floor) => Ramp(DepthSteps(floor), DepthGrowth, DepthPow) * Curve.At(floor).Tough;
        public static double DepthRate(int floor) =>
            System.Math.Min(DepthRateCap, Ramp(DepthSteps(floor), DepthGrowth, DepthPow * 0.55) * Curve.At(floor).Rate * (1 + LiftRate * DepthLift(floor)));

        /// <summary>Density is its own exponent rather than the health one, because it is a different kind of lever: health makes... [h:Balance-75]</summary>
        public static double DepthBodies(int floor, int rolled) =>
            System.Math.Min(DepthBodyCap, rolled + DepthBodyPow * (System.Math.Exp(DepthPow * 1.7 * DepthSteps(floor)) - 1) + Curve.At(floor).Bodies + LiftBodies * DepthLift(floor));

        public static double DepthPack(int floor, double brunchChance) =>
            System.Math.Max(0, System.Math.Min(DepthPackCap, brunchChance + DepthPackStep * DepthSteps(floor) + Curve.At(floor).Pack + LiftPack * DepthLift(floor)));
    }
}
