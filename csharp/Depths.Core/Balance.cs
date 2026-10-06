namespace Depths
{
    /// <summary>
    /// Every dial in the game, ported from <c>src/00-balance.js</c>.
    ///
    /// <para>
    /// Nothing in here draws, reads input or touches the DOM, so all of it moves across unchanged.
    /// This is the highest-value file in the project: these numbers ARE the game's feel, and most of
    /// them carry the measurement that produced them. When a number is wrong this is the file; when
    /// a number is right, the comment above it is why.
    /// </para>
    /// </summary>
    public static class Balance
    {
        // ------------------------------------------------------------- pacing

        /// <summary>
        /// The simulation runs faster than wall-clock. Every per-tick distance is unchanged by this
        /// and every counter is multiplied by it, so the game reads at its designed speed on a 210Hz
        /// tick rather than being tuned twice.
        /// </summary>
        public const double Speedup = 3.5;

        /// <summary>
        /// Not a const: <c>Math.Round</c> is not a compile-time constant, and a dials file full of
        /// mystery static initialisers is worse than one that says why.
        /// </summary>
        public static readonly int TickHz = (int)System.Math.Round(60 * Speedup);

        /// <summary>Seconds to ticks. The rounding is in the original and is load-bearing.</summary>
        public static int Sec(double seconds) => (int)System.Math.Round(seconds * TickHz);

        // ------------------------------------------------------------- the room

        public const int RoomLeft = 50, RoomRight = 750, RoomTop = 130, RoomBottom = 580;
        public const int MidX = (RoomLeft + RoomRight) / 2, MidY = (RoomTop + RoomBottom) / 2;



        // ------------------------------------------------------------- spawning

        /// <summary>
        /// Where a body may NOT be, and how far apart the ones that may.
        ///
        /// <para>
        /// Four separate rules rather than one "spawn away from things" number, because they answer
        /// different questions. The margin keeps a body off the wall; the middle keeps the middle of the
        /// room walkable so the player is never somewhere they cannot fight from; the doorway keeps a
        /// body from appearing on top of the player; and the separation is the only one that is about
        /// the bodies themselves rather than the room.
        /// </para>
        /// <para>
        /// The separation is 158 against a room 700 wide, which is what lets five bodies have a real
        /// choice of where to stand instead of all taking the roomiest corner.
        /// </para>
        /// </summary>
        public const double SpawnMargin = 76, SpawnMid = 104, SpawnSep = 158, SpawnDoor = 130, SpawnFar = 210;

        /// <summary>How far in from a wall the player enters, and therefore where a body must not start.</summary>
        public const double EntryInset = 36;

        /// <summary>A room's bounds in world space. A room is DATA, not a shape.</summary>
        /// <remarks>
        /// The four constants above used to mean "the room", and this whole port was built on that:
        /// every piece of geometry here derived from one rectangle, which was correct for exactly as
        /// long as there was exactly one rectangle. The reference implementation has since moved the
        /// size onto the room record, so a room here is four numbers you pass in rather than a global
        /// you read.
        /// <para>
        /// The fields are l/t/r/b rather than Left/Top/Right/Bottom to match the JavaScript
        /// <c>bounds</c> object field for field, because this is a parity port and a renamed field is
        /// a renamed field when you are reading the two side by side.
        /// </para>
        /// </remarks>
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

        /// <summary>How far away a gunner has to be before counterstrafing buys it anything.</summary>
        /// <remarks>
        /// A QUARTER OF THE ROOM'S WIDTH. It was half the width less a margin, which was wrong.
        /// <para>
        /// This port said 350 and the JavaScript said 300, and there was a test here called
        /// <c>TheDeadzoneIsHalfTheRoomWidth</c> that asserted 350 - so the drift was not merely
        /// unnoticed, it was protected. The number had been written from the RULE rather than from
        /// the value, and the test then re-derived the same rule and passed. A test that agrees with
        /// the thing it audits cannot find the disagreement; this is the third time in this project
        /// that shape has cost something.
        /// </para>
        /// <para>
        /// BOTH BOUNDS ARE FRACTIONS OF THE ROOM, and the port was 130px of ramp behind the game until
        /// 2026-10-03 - a parity break committed by me, in the same commit that changed the
        /// JavaScript, which is the one rule this manifest states twice.
        /// </para>
        /// <para>
        /// The JavaScript measured at 300px of deadzone and 130px of ramp, in a 700px room. That is a
        /// 130px NOTCH: `reach` saturates at 1.0 above it, so beyond 430px the spread stopped growing
        /// and "further is worse" could not be expressed at all. It is now a quarter of the room to
        /// four fifths - 175px to 560px, a 385px ramp that never saturates inside a room. The port read
        /// 300/430 and its tests asserted 300/430, so both sides were self-consistent and wrong.
        /// </para>
        /// <para>
        /// The margin constant is gone rather than renamed. There is no "margin off half the width"
        /// any more, because half the width is no longer where the deadzone starts; leaving a constant
        /// named SwerveMargin at 50 next to a formula that does not use it would be a number that
        /// looks load-bearing and is not.
        /// </para>
        /// </remarks>
        public static int SwerveDeadzone(Room room) => (int)System.Math.Round(room.W * 0.25);
        public static int SwerveFullBase(Room room) => (int)System.Math.Round(room.W * 0.55);

        // Methods rather than a property plus an overload, because C# will not let a property and a
        // method share a name - CS0102 - and a property here would also have been the wrong shape.
        // The JavaScript has one function taking an optional room, so this is the closest honest
        // mirror of that available in the language: two methods, one of which forwards.
        public static int SwerveFull(Room room) => SwerveDeadzone(room) + SwerveFullBase(room);
        public static int SwerveDeadzone() => SwerveDeadzone(Room.Standard);
        public static int SwerveFull() => SwerveFull(Room.Standard);

        /// <summary>How far a body can be and still notice the player: 0.85 of the room's diagonal.</summary>
        /// <remarks>
        /// The other half of the same class. The JavaScript had this frozen at module load, reading
        /// 707 - correct for a 700x450 room and wrong for every other one, so a body 900px away in a
        /// 1680-wide room was outside its aggro and stood still. A function of the room here, with the
        /// 707 preserved for the standard room so nothing about the current balance moves.
        /// </remarks>
        public static int AggroRange(Room room) =>
            (int)System.Math.Round(0.85 * System.Math.Sqrt((double)room.W * room.W + (double)room.H * room.H));

        // ------------------------------------------------------------- the player

        /// <summary>
        /// Declared in the art module in the original because the enemy table reads it. Moved here
        /// because in C# there is no load-order reason for a constant to live in the wrong file, and
        /// leaving it there would be a load-order bug wearing a costume.
        /// </summary>
        public const double PlayerMove = 1.2;

        public const double TempoRate = 1.5;
        public const double PressureRate = 1.5;

        /// <summary>
        /// THE MOMENTUM DIALS, straight from src/00-balance.js:1020-1024. Momentum is charged on
        /// movement under pressure: below the floor it decays rather than holds, which is the whole
        /// difference between a meter that reads a fight and a meter that reads a held key.
        /// </summary>
        public const double MomentumGain = 0.0056;
        public const double MomentumStallDecay = 0.006;
        public const double MomentumMoveFloor = 0.3;

        /// <summary>
        /// How much a crowded room shortens a body's cadence, src/00-balance.js:1160
        /// (PRESSURE_CLOSURE's twin). A body at full pressure answers 0.4 of a cooldown sooner.
        /// </summary>
        public const double PressureCadence = 0.4;

        /// <summary>
        /// The Warden's pacing dials, from src/00-balance.js:430-476. BOSS_CD_MIN/VAR are the gap
        /// between moves - sec(3.2) and sec(1.0) in the original - and the recover is the dead time
        /// after every move, which is the gap the player spends reading the next tell.
        /// </summary>
        public static int BossCdMin => Sec(3.2);
        public static int BossCdVar => Sec(1.0);
        public static int BossRecover => Sec(1.1);
        public const double BossPhase1 = 0.66, BossPhase2 = 0.33;   // fractions of max HP

        /// <summary>The fade out of a room transition, src/00-balance.js:251 (FADE_OUT).</summary>
        public static int FadeOut => Sec(0.35);

        /// <summary>The ready window after entering a room, src/00-balance.js:251 (READY).</summary>
        public static int Ready => Sec(0.75);

        /// <summary>
        /// DY slides the hit circle down onto the actual mass of the character - the chest and waist,
        /// which is rows 11-15 of the sprite, and the band a shot is aimed at.
        ///
        /// <para>
        /// R is deliberately SMALLER than the model: 10 gives a 20px hitbox against a 24px
        /// character, so it is 83% of the width and misses the edges of the hood and the hem. This is
        /// on purpose in both directions. An exact hitbox feels like the game is grading an exam, and
        /// it makes the two damage paths - the shell that grazes a shoulder and the body that walks
        /// into you - disagree at the boundary in a way players read as unfair. A forgiving one is
        /// the same forgiveness the whole game already runs on: readable, reactive, never a
        /// measurement. If the game gets faster, this is the dial that gets retuned first, and it
        /// should get smaller before anything telegraph gets shorter.
        /// </para>
        /// </summary>
        public const int PlayerHitDy = 10, PlayerHitR = 10;

    /// <summary>
    /// Ticks a struck body stays flashed. Read out of the running game: 27.
    /// <para>
    /// Read by the body phase every tick, and it DECREMENTS rather than clearing - so a body hit
    /// again while already flashing has its flash extended rather than restarted, and a rapid weapon
    /// reads as one sustained hit instead of a stutter of separate ones.
    /// </para>
    /// </summary>
    public const int HitFlash = 27;

    // The projectile phase. Values read from the running game, 2026-10-06.
    public static readonly int Iframes = Sec(1.0);          // IFRAMES 210
    public const double MomentumHitKeep = 0.55;             // a hit keeps 55% of the meter
    public const double PierceFalloff = 0.72;               // each body a piercing bolt drills through
    public const int BrunchAbsorbFlash = 21;                // a Brunch flashes this long when it eats a shell
    public const int BossWallId = -1;                       // the pack id of the statues the Warden calls
    public const double KnockMax = 5, KnockTrade = 0.09;    // a hit shoves, never launches; below this speed no trade
    public const int KnockStun = 88;                        // KNOCK_STUN
    public const int WanderTicks = 210;                     // WANDER_TICKS
    public const int PressureSpan = 4;                      // PRESSURE_SPAN
    public const double PressureFloor = 0.25;               // PRESSURE_FLOOR
    // GUNNER_DODGE: a gunner sees a bolt coming within `sight`, sidesteps half the time, on a cooldown
    public const double GunnerDodgeSight = 250, GunnerDodgeChance = 0.5, GunnerDodgeKick = 0.6;
    public const int GunnerDodgeCd = 126;

    // The right-click blast, ALT_WEAPON, read from the game 2026-10-06.
    public const int AltCooldown = 756;                     // sec(3.6); /TEMPO at the cast
    public const double AltSpeed = 1.87, AltR = 12, AltAoe = 100, AltKnock = 2.7;
    public const double AltPool = 18 * 1.35 / 0.66 * 1.02;  // 18*TOUGH/ARMOUR*1.02 = 37.5545...
    /// <summary>
    /// The Brunch guard rule, as the JS playtest flag ?brunch= (A: guard the shooter wherever the
    /// player is; B: leash at BrunchGuardLeash; A+: the wall advances once the player leaves the
    /// target's reach). A is the reference until the owner chooses.
    /// </summary>
    public static string BrunchVariant = "A";
    public const double BrunchGuardLeash = 420;

    // The Warden, read from the game 2026-10-06.
    public const int BossVolleyN = 3, BossVolleyGap = 116, BossWallHp = 5, BossWallLife = 2940;   // sec(14)
    public const double BossSweepDist = 150, BossShellDmg = 1.44;

    // The blink, read from the game 2026-10-06.
    public const int BlinkFillClear = 9;                    // a charge refills 9x faster in a cleared room
    public static readonly int BlinkIframes = Sec(0.17), BlinkGrace = Sec(0.6), DashTrail = Sec(0.23);   // 36, 126, 48
    public const double AltKnockNear = 2, AltKnockFar = 0.35, Disperse = 2.3, ShootSlowAlt = 0.45;

    /// <summary>roomPressure: 1 with one body or none, easing to PRESSURE_FLOOR at PRESSURE_SPAN+1 bodies.</summary>
    public static double RoomPressure(int live)
    {
        if (live <= 1) return 1;
        if (live >= PressureSpan + 1) return PressureFloor;
        return PressureFloor + (1 - PressureFloor) * (PressureSpan + 1 - live) / (double)PressureSpan;
    }

    /// <summary>
    /// How often, in ticks, an UNGUARDED pack looks for a body to shield. Read out of the
    /// running game: 20.
    /// </summary>
    public const int BrunchScanTicks = 20;

    /// <summary>
    /// The smallest pack that will form a shield arc. Below this a pack walks at the player
    /// instead. Read out: 2.
    /// </summary>
    public const int BrunchShieldMin = 2;

    /// <summary>
    /// The smallest pack that will form a wall around an unguarded position. Read out: 3.
    /// Above <see cref="BrunchShieldMin"/>, so a pack of two guards and a pack of three walls.
    /// </summary>
    public const int BrunchWallMin = 3;

    /// <summary>
    /// How fast a pack moves while it is holding a shield slot, rather than closing. Read
    /// out: 0.72 - slower than the guard's own approach, so a guarded body is genuinely easier to
    /// reach than an unguarded one.
    /// </summary>
    public const double BrunchShieldSpeed = 0.72;

    /// <summary>
    /// How fast a pack gains speed when closing on its slot. Read out: 0.09.
    /// </summary>
    public const double BrunchAccel = 0.09;

    /// <summary>
    /// How fast a pack sheds speed when leaving a slot. Deliberately FASTER than
    /// <see cref="BrunchAccel"/> (0.16 against 0.09), so a pack does not drift on after its target
    /// moves - a pack that eases out slowly feels like it is following you.
    /// </summary>
    public const double BrunchDecel = 0.16;

    /// <summary>
    /// How close a Brunch must be before it stops steering and simply reaches. Read out: 6.
    /// Inside this the pack uses the direction to the PLAYER rather than to its slot, which is what
    /// makes a pack that has arrived commit to contact instead of circling.
    /// </summary>
    public const double BrunchDeadzone = 6;


    /// <summary>
    /// The peak chase speed of a pack. Read out: 2.1, which is faster than a momentum-full
    /// player at 1.6045 - that is the whole point of the bomb-rush tuning.
    /// </summary>
    /// <summary>
    /// The distance beyond which the arc stops tracking the real gap. Read out of the running game:
    /// 118. See the note on <see cref="BrunchShieldFrac"/> for why this is not the stand-off.
    /// </summary>
    public const double BrunchShieldR = 118;

    /// <summary>
    /// Where the shield forms, as a FRACTION OF THE GAP between the guarded body and the player.
    /// Read out of the running game: 0.25.
    /// <para>
    /// A constant radius is badly wrong here, because the thing the arc has to fit between is the
    /// distance from the player to the body being shielded - and that is not fixed. It is whatever the
    /// fight has produced, and it gets SMALL: a player who closes on a shooter is inside 118px within
    /// a second.
    /// </para>
    /// <para>
    /// Measured, before this became a fraction: a player at 170,330 and a shooter at 334,330 is a
    /// 164px gap, and a 118px stand-off put the wall BEHIND the player - correctly built, correctly
    /// centred, correctly angled, and on the wrong side of the person it was protecting. Line of sight
    /// was blocked 27-39% of the way rather than closed. As a quarter of the gap the same wall forms
    /// 50px from the shooter, which is 114px from the player and well inside the room between them.
    /// </para>
    /// </summary>
    public const double BrunchShieldFrac = 0.25;

    /// <summary>
    /// How far BEHIND the front rank a second-rank body stands. Read out of the running game: 17.
    /// <para>
    /// The surplus of a large pack stacks here rather than sliding outward along the arc, so a shield
    /// is two deep in the middle and thin at the ends. A shield that is a single file with six
    /// stragglers beside it is a queue.
    /// </para>
    /// </summary>
    public const double BrunchWallRank = 17;

    /// <summary>The narrowest the arc cone may be, in radians. 0.15707963 = 9 degrees.</summary>
    public const double BrunchArcFloor = 0.15707963267948966;

    /// <summary>
    /// The widest the arc cone may be, in radians. 1.04719755 = 60 degrees. This is the only limit
    /// left on the cone, and it exists so a large boss at close range cannot wrap into a mob.
    /// </summary>
    public const double BrunchArcCeil = 1.0471975511965976;

    public const double BrunchRun = 2.1;

    /// <summary>
    /// A Brunch's walk speed - its starting point, and its speed while it has not committed. Read out
    /// of the running game as <c>ENEMY.brunch.walk</c>: 0.624.
    /// <para>
    /// It is a CONSTANT rather than a read from a body archetype, because the C# port has no spawn
    /// table yet - and this is the number a body archetype would carry. Named here so the ramp can be
    /// stated as a curve from a known starting point, which is how the game states it.
    /// </para>
    /// </summary>
    public const double BrunchWalkSpeed = 0.624;

    /// <summary>
    /// The angular gap between two Brunch holding a shield arc, in pixels at the target's
    /// radius. Read out: 19.
    /// </summary>
    public const double BrunchArcGap = 19;

    /// <summary>
    /// A guarded ranged body stands off at <c>far * this</c> rather than <c>far</c>, so a
    /// Brunch escort pulls its charge back and gives the player room to reach it. Read out: 1.45.
    /// </summary>
    public const double GuardStandoffMult = 1.45;

    /// <summary>
    /// How much a crowded room closes the standoff distance. Read out: 0.85 - at a
    /// full room a guarded gunner stands at <c>far * 1.45 - (far-close) * 0.85</c>, which is nearer
    /// than its own far value, so pressure pushes a guarded body INTO range rather than away.
    /// </summary>
    public const double PressureClosure = 0.85;

    /// <summary>
    /// How much of the swerve meter widens a gunner's aim cone, in radians at the extremes.
    /// Read out: 0.30.
    /// </summary>
    public const double SwerveAim = 0.3;

    /// <summary>
    /// How much a SWERVE costs a gunner's confidence in its intercept while it is casting,
    /// when it is planted and reading the player's velocity directly. Read out: 1 - so a counterstrafe
    /// takes a rooted gunner's confidence to zero.
    /// </summary>
    public const double SwerveTrustRooted = 1.0;

    /// <summary>
    /// The same cost while the gunner is moving. Read out: 2.2 against 1 for rooted, so
    /// a walking gunner is roughly twice as wrong - which is why the counterstrafe result was so much
    /// stronger against a moving shooter than a planted one.
    /// </summary>
    public const double SwerveTrustWalking = 2.2;



    /// <summary>
    /// Ticks the boss warning stays on screen once the boss room comes into view. Read
    /// out of the running game: 672, which at 210 ticks a second is 3.2 seconds.
    /// </summary>
    public const int BossWarnTime = 672;

    /// <summary>
    /// The width of a doorway in the wall, and the half-width the player must be inside for a
    /// door to accept them. Read out of the running game: 90.
    /// </summary>
    public const int DoorWidth = 90;

    /// <summary>
    /// Below this magnitude a knockback component is set to exactly zero, so a body that has
    /// all but stopped does not keep feeding the audio and the hit-flash for ever.
    /// </summary>
    public const double KnockCut = 0.006;

    /// <summary>
    /// Per-tick decay on the PLAYER's knockback, distinct from <see cref="KnockFriction"/>
    /// which is the body's. Read out of the running game: 0.958, against the body's 0.976 - the
    /// player's knockback dies faster, which is what makes a trade feel like an escape.
    /// </summary>
    public const double KnockPFriction = 0.958;

    /// <summary>
    /// How fast the player's direction-of-travel estimate chases the real velocity. Lower
    /// than <see cref="LungeTrack"/> because this one feeds the lunger's belief about where the
    /// player is going, and a belief that updates too eagerly is a lunger that never commits.
    /// </summary>
    public const double LungeBeliefTrack = 0.14;

    /// <summary>
    /// How much a full Momentum meter improves acceleration. Read out of the running
    /// game: 0.55, so a charged player accelerates 55% harder rather than merely faster.
    /// </summary>
    public const double MomentumAccel = 0.55;

    /// <summary>
    /// The fraction of the remaining velocity gap closed each tick. Read out of the running
    /// game: 0.116, which is why the player takes about 40 ticks to reach top speed rather than
    /// snapping to it - the ramp is the feel.
    /// </summary>
    public const double MoveAccel = 0.116;

    /// <summary>
    /// The hard ceiling on the movement bonus. Read out of the running game: 0.44.
    /// <para>
    /// The bonus is <c>min(cap, stats.speed + momentum * MomentumSpeed)</c>, so an empty run at
    /// 0.25 plus a full meter at 0.18 reaches 0.43 - just under the cap, which means the cap is not
    /// currently the binding constraint and the ceiling exists for a build that has not been designed
    /// yet rather than for the one that has. Pinned anyway, because "not currently binding" is a
    /// property of two other numbers and changes the moment either does.
    /// </para>
    /// </summary>
    public const double MoveSpeedHardCap = 0.44;

    /// <summary>What a full Momentum meter adds to the movement bonus. Read out: 0.18.</summary>
    public const double MomentumSpeed = 0.18;

    /// <summary>
    /// The movement bonus a run starts with, before any item and before any momentum. Read out of
    /// the running game: 0.25, and it is the whole of <c>Stats.value('speed')</c> on an empty sheet.
    /// <para>
    /// A constant rather than a call into a stats system, because the C# port has no
    /// <c>Stats</c> yet. It is therefore correct for an empty run and WRONG for any run carrying a
    /// speed item - which is stated here rather than left to be discovered, and it is the reason
    /// <c>TickPlayer</c> takes the bonus as a parameter instead of reading a constant internally.
    /// </para>
    /// </summary>
    public const double MoveSpeedBonusBase = 0.25;

    /// <summary>
    /// Below this speed the player's direction-of-travel estimate stops updating, so a
    /// player standing still is not read as jittering by the enemies that aim at them.
    /// </summary>
    public const double PlayerSpeedEps = 0.05;

    /// <summary>
    /// Per-tick recovery of the firing slow-motion. Fractional on purpose: the meter
    /// eases and rounding it would make the recovery visibly steppy.
    /// </summary>
    public const double ShootSlowRecover = 0.105;

    /// <summary>
    /// Per-tick easing of the firing slow-motion toward its target of 1. The lower this is,
    /// the longer the player spends slowed after a shot, which is the cost of firing on the move.
    /// </summary>
    public const double SlowEase = 0.079;

    /// <summary>
    /// Pixels of animation phase per pixel travelled. Dividing by it is what makes the walk
    /// cycle scale with actual distance rather than with time, so a slow walk animates slowly.
    /// </summary>
    public const double Stride = 38.5;



    /// <summary>
    /// Ticks for one blink charge, and the divisor behind the half-charge a cleared room refunds.
    /// <para>
    /// Read out of the running game: <c>BLINK_RECHARGE</c> is 735 there. The refund is
    /// <c>BlinkRecharge * 0.5</c> = 367.5, which is a fractional tick count on purpose - the
    /// original stores it in a float field and the counter applies the ceiling, so rounding it here
    /// to 368 would make the refund half a tick larger on every cleared room.
    /// </para>
    /// </summary>
    public const int BlinkRecharge = 735;

    /// <summary>
    /// Per-weapon cooldowns in ticks, in roster order: Bolt, Scatter, Arcane Beam, Voidball.
    /// <para>
    /// Read out of the running game rather than transcribed. The third value is 18.2, not 18 - the
    /// Arcane Beam is a continuous weapon and its cadence is genuinely fractional, so this table is
    /// <c>double</c> rather than <c>int</c>. Rounding it to 18 would be a silent balance change that
    /// no test comparing a whole number would catch.
    /// </para>
    /// </summary>
    public static readonly double[] WeaponCooldowns = { 133, 231, 18.2, 84 };

    /// <summary>
    /// The cooldown of weapon <paramref name="index"/>. Out-of-range indices clamp to the last
    /// entry rather than throwing: the room phase calls this with whatever a pickup carried, and a
    /// malformed pickup should not be able to end a run with an exception from the tick.
    /// </summary>
    public static double WeaponCooldown(int index)
    {
        if (index < 0) index = 0;
        if (index >= WeaponCooldowns.Length) index = WeaponCooldowns.Length - 1;
        return WeaponCooldowns[index];
    }

        /// <summary>
        /// The hitbox the enemies aim at trails the real position and converges over roughly 0.4s -
        /// the reaction window a blink is supposed to buy. Without it a gunner that was already
        /// tracking you gets a free intercept shot the instant you vanish.
        /// </summary>
        public const double HitboxLagEase = 0.05;

        /// <summary>
        /// How long a body stays noticed after something hits it. A DURATION, so it is a constant and
        /// not a function of the room - the same reasoning <c>RoomW()</c> carries in the other
        /// direction, and the reason these two are not in the same family of numbers.
        ///
        /// <para>
        /// src/00-balance.js:214, <c>AGGRO_TIME=sec(2.5)</c>. Read by <c>alertEnemy</c>, which only
        /// ever RAISES a body to this - it never lowers one, because a body that has seen you should
        /// not lose interest because the next shell took longer to arrive.
        /// </para>
        /// </summary>
        public static readonly int AggroTime = Sec(2.5);

        /// <summary>
        /// The slow a landed shell applies, in ticks, and what fraction of speed it leaves behind.
        /// src/00-balance.js:657, <c>HIT_SLOW_MULT=0.62, HIT_SLOW_TICKS=sec(0.55)</c>.
        ///
        /// <para>
        /// Both are here because <c>slowEnemy</c> is one line and needs both, and a one-line helper
        /// with two constants is where a port ends up hard-coding 0.62 inline "just for now".
        /// <c>HitSlowMult</c> has no reader yet: nothing that reduces speed is ported, so it is
        /// declared rather than used, and PORTED.md's rule is that a constant in the port must be one
        /// the game has - not that it must already have a caller here.
        /// </para>
        /// </summary>
        public static readonly int HitSlowTicks = Sec(0.55);
        public const double HitSlowMult = 0.62;

        /// <summary>
        /// The blink was 140px on an 8s recharge, and together those two made it a teleport with a
        /// long wait rather than an escape with a cost: nothing about a 140px jump reads as
        /// movement, so the move is only ever "get me out of here", and an 8s wait means the correct
        /// play is to bank both charges for an emergency and never use one for ground. Shorter and
        /// quicker, plus the landing burst below, changes it into a thing you use to keep distance -
        /// which is the thing this game is actually about. 116px is still most of a body width past a
        /// Brunch, and 6.5s is roughly a third of a full room, so a panic blink and a positioning
        /// blink are different decisions.
        /// </summary>
        public const int BlinkDist = 116;

        /// <summary>
        /// The blink's landing burst. Gain is the speed it gives you when you hold the direction you
        /// blinked in, Burst is how long it lasts, and both are deliberately small: this is a nudge
        /// that makes the blink worth using for ground as well as for survival, not a dash that
        /// replaces walking. At 0.11s and 1.55x it is about two seconds of useful extra ground if you
        /// commit to it and nothing at all if you do not.
        /// </summary>
        public const double BlinkBoostGain = 1.55;
        public static int BlinkBoost => Sec(0.11);

        // ------------------------------------------------------------- knockback

        public const double KnockFriction = 0.976, KnockGain = 0.294, KnockPGain = 0.3, KnockBounce = 0.6;

        /// <summary>
        /// A knock of speed v coasts v/(1-friction) pixels, so to cover a distance d you want
        /// v = d*(1-friction). The hook's pull is the overshoot on top, a little over 1 so bodies
        /// cross the centre and knot up instead of merely touching it.
        /// </summary>
        public const double HookPullGain = 1 - KnockFriction;

        // ------------------------------------------------------------- lungers

        public const double LungerPay = 0.96;
        public const double LungerWalk = 0.3 * LungerPay;
        public const double LungerRun = 0.82 * LungerPay;
        public const int MaxArmor = 4;

        /// <summary>
        /// The ramp. A walker used to ease toward its top speed at one fixed rate, so its approach
        /// was a straight line you could measure in the first second and then forget. Instead the
        /// rate itself grows with the chase: the first moments are slow and readable, and a body you
        /// keep ignoring keeps picking up speed behind you. The gain is how much faster the
        /// acceleration gets once the body has committed. This replaced a hard "lunge" step that
        /// jumped the speed up all at once, which read as a bug rather than as pressure. Any body
        /// that does not shoot back does not ramp, so a Brunch pack still closes at the speed it is
        /// supposed to.
        /// </summary>
        public const double LungerAccel = 0.0058;
        public const double WanderSpeed = 0.25;

        /// <summary>
        /// The Brunch ramp: ticks over which a pack's chase speed climbs from its walk to its run.
        ///
        /// <para>
        /// It used to reach full commitment in 0.23s, which meant a pack was on you before you had
        /// finished looking at where it had come from - there was no interval in which to pick your
        /// ground, which is the one thing the pack is supposed to be asking of you. A pack that
        /// announces itself and then arrives is the design; one that is simply on top of you from the
        /// moment it was noticed is not.
        /// </para>
        ///
        /// <para>
        /// <b>1.5s, and this was STALE.</b> The value here was <c>Sec(2.2)</c> = 462 ticks - the
        /// pre-tuning number. The game ships 1.5s, chosen deliberately when the slow approach was
        /// replaced by the bomb-rush: the ramp had to stay long enough that a pack announces itself,
        /// but 2.2s was dead time in which a pack was neither guarding nor threatening.
        /// </para>
        ///
        /// <para>
        /// Nothing caught it because nothing in the port read the constant - the same failure mode as
        /// <see cref="SwerveDecay"/>, and the second one in this file. An unpinned constant in the
        /// place the game's tuning lives is indistinguishable from a correct one until something
        /// reads it. Measured: <c>BRUNCH_RAMP</c> is 315 in the running game, which is 1.5s at 210Hz.
        /// </para>
        /// </summary>
        public static int BrunchRamp => Sec(1.5);
        public const double BrunchRampGain = 1.9;

        // ------------------------------------------------------------- the lunge

        /// <summary>
        /// THE STANDOFF is what makes this mechanic a mechanic. A lunger that simply walks at the
        /// player ends up touching them - its approach speed is faster than theirs at range - and then
        /// "lunges" from zero distance, where no read is worth anything because there is nothing left
        /// to dodge. Every measurement of this attack came out a hundred percent for that reason, and
        /// not because the prediction was any good.
        ///
        /// <para>
        /// The lunger holds at this distance, the way the gunner holds inside its close/far band, and
        /// the lunge is what crosses the gap. That makes it a real committed attack: the line is
        /// drawn across open ground, there is time to see it, and there is something to answer with.
        /// </para>
        /// </summary>
        public const int LungeHold = 78;
        public const int LungeMin = 34;

        /// <summary>
        /// How far the lunge can be aimed at all. A lunger whose solution is outside this does not
        /// commit - it keeps closing at its approach speed until the solution fits, which is what
        /// "too far" means: not that the attack fails, but that it has not started yet.
        ///
        /// <para>
        /// The old lunge led thirty-two pixels a shot that needed two hundred and ninety-four and
        /// stopped at a hundred and sixty-six, so it fell a hundred and twenty-eight pixels short
        /// every time it was used: not a hard attack that can be read and beaten, but one that could
        /// not land at all. That is why running in a straight line was free, and it was arithmetic
        /// rather than feel.
        /// </para>
        /// </summary>
        public const int LungeReach = 280;
        public const int LungeFloor = 56;

        /// <summary>
        /// How the lunger believes the player is moving. It reads a SMOOTHED heading rather than the
        /// instantaneous velocity, and scales that reading by how settled the player looks: a player
        /// holding one line is read in full and gets the whole intercept, a player who has been
        /// reversing is read as unreliable and gets a much shorter, weaker lunge. So reversing to
        /// bait buys a weaker attack instead of a free one, which is the trade the mechanic was
        /// missing.
        /// </summary>
        public const double LungeTrack = 0.014;
        public const double LungeConfMin = 0.30;

        /// <summary>
        /// Iterations for the intercept's fixed point. Three is enough for a lunge because a lunge
        /// crosses at roughly six times the player's speed, which makes the fixed point contract
        /// hard. A gunner shell is only 1.8x player speed, so the same three passes leave a
        /// sixteenth of the error - about thirty pixels of miss - and the gunner uses
        /// <see cref="GunIter"/> instead.
        /// </summary>
        public const int LungeIter = 3;

        public static int LungeCd => Sec(1.15);
        public static int LungeWindup => Sec(0.34);
        public const double LungeSpeed = 4.2;
        public static int LungeRecover => Sec(0.55);
        public const int LungeRange = 178;
        public const double LungerNear = 0.55, LungerFar = 1.3;

        /// <summary>Two lungers meeting mid-charge come off both worse.</summary>
        public const double LungeClash = 5.5;

        /// <summary>
        /// How fast a lunger walks its own angle around the player, in px/tick, aimed tangentially so
        /// it can never change how far away it is. This is the whole of the anti-front mechanic and it
        /// is a single number on purpose: at a third of a pixel a tick the bodies take a second or
        /// two to fan out, which reads as encircling rather than as teleporting into position.
        /// Scaling it by the approach speed instead does nothing at all, because the approach speed
        /// is zero inside the standoff - which is exactly where a pack spends most of its time.
        /// </summary>
        public const double LungerSpread = 0.9;

        // ------------------------------------------------------------- counterstrafing

        /// <summary>How fast a reversal is forgotten, and how much a reversal widens a gunner's aim.</summary>
        public const double SwerveGain = 0.22;

        /// <summary>
        /// Per-tick decay of the swerve meter - how fast the game forgets that the player reversed.
        /// <para>
        /// THIS WAS WRONG AND NOTHING CHECKED IT. The value here was 0.011, roughly three times the
        /// original's 0.0035, and no test pinned it: the suite asserted <c>SwerveDecay &gt; 0</c> and
        /// that a reversal is remembered for a second, and 0.011 satisfies both. Nothing in the port
        /// read the constant, so a wrong value sat in a file whose whole purpose is to hold the
        /// game's numbers.
        /// </para>
        /// <para>
        /// The original's own test says what the constant is FOR: <c>SWERVE_DECAY * sec(1) &gt; 0.5</c>,
        /// so a reversal is still fully remembered after a second. At 210 ticks that is a per-tick
        /// decay below about 0.0033 - which rules 0.011 out on the game's own terms, before any
        /// measurement. Measured directly as well: a meter filled to 1 and left alone falls to 0.5765
        /// after one second, which is 0.0035 a tick.
        /// </para>
        /// </summary>
        public const double SwerveDecay = 0.0035;

        // SwerveDeadzone and SwerveFull used to live here, reading (RoomRight - RoomLeft) / 2 and
        // +220 - which is 350 and 570, where the JavaScript reads 300 and 430. They now sit beside
        // the Room struct near the top of this file as functions of the room, and the reasoning is
        // written there: this pair was the single worst piece of drift in the port, and a test was
        // defending it rather than catching it.

        // ------------------------------------------------------------- the gunner

        /// <summary>
        /// The gunner's cast tell. This is the whole of it: the shell used to leave the instant the
        /// gunner's cooldown ran out, so the player had nothing to read and the only counter was not
        /// being there. Half a second of swelling light at the muzzle turns that into a reaction. It
        /// is paid for by the cooldowns going up, not by the damage going down - a slower, heavier,
        /// more telegraphed shell is a better gun, and a faster, lighter, untelegraphed one is just
        /// a tax.
        /// </summary>
        public static int CastTime => Sec(0.5);

        /// <summary>
        /// Iterations for the gunner's intercept. Fourteen, and the number is not arbitrary.
        ///
        /// <para>
        /// This is fixed-point iteration, and how fast it converges is set by how much slower the
        /// target is than the shell: each pass pulls the remaining error down by the ratio of the
        /// player's speed to the shell's, which here is a little over a half. Three passes - what the
        /// lunge uses, and plenty there - leaves a sixteenth of the error, and a sixteenth of a
        /// two-hundred-and-sixty-tick horizon is twenty-six ticks of lead, which is thirty pixels of
        /// miss on a player doing nothing clever. Fourteen passes leaves a thousandth. It is a few
        /// extra square roots per shell and shells are the rarest thing in the game.
        /// </para>
        /// </summary>
        public const int GunIter = 14;

        /// <summary>
        /// The floor on a gunner's spread, in radians. It exists because a gunner is never perfectly
        /// deterministic, and a shot that is the same shot every time is a shot that can be walked
        /// into.
        /// </summary>
        public const double GunSpreadFloor = 0.02;

        // ------------------------------------------------------- the depth ladder

        /// <summary>
        /// The growth and time constant of the health dial, and the rate dial, and the growth of the
        /// density dial. The ladder is BOUNDED, and it was linear until it was measured - which is
        /// worth recording because the brief asked for linear "for now" and the right instinct, and
        /// three linear ladders multiplied together is not a difficulty curve:
        ///
        ///   floor  tough  rate  bodies  shells/s per gunner
        ///     1    1.00   1.00      6          1.9
        ///    10    3.70   2.44     79          3.8
        ///    20    6.70   4.04    159          7.4
        ///    50   15.70   8.84    400         24.8
        ///
        /// A room on floor 50 was asking for four hundred bodies. That is not a hard game, it is a
        /// game that cannot be finished.
        ///
        /// The rate dial is the one that really mattered, because it is the only one of the three
        /// that was breaking a stated rule. It scales approach speed as well as cadence, so at
        /// 8.84x a gunner crosses the room at 3.98 px/tick against a player who moves at 1.2, and
        /// fires every 60ms. Seventeen shells a second is not a fight, it is a video of a fight.
        /// Scaling a body's SPEED with depth is a reaction-time tax wearing the costume of a
        /// difficulty curve, and the brief rules it out.
        ///
        /// The shape is EXPONENTIAL and the health dial has NO CEILING.
        ///
        /// It was 1 + growth*steps/(steps+tau) - monotonic, but SATURATING, so the steps shrank and
        /// the curve flattened into an asymptote it never reached. The asymptote was 3.6x, floor 14
        /// already reached 2.5x, and everything past that was spent approaching a number it had
        /// nearly hit: the deep floors were shallower than the table made them look. The brief now
        /// asks for a climb that is unbroken, and an asymptote is a promise the content eventually
        /// outgrows.
        ///
        /// Exponential rather than logarithmic, and the distinction is the design rather than a word:
        /// a logarithm's increments DECREASE, so a log curve is steep early and flat late - the
        /// opposite of wanting later floors to ramp significantly more. An exponential's increments
        /// increase. Over floors 1-14 the per-floor step grows from 0.05 to 0.22, a 4x increase, and
        /// is still growing at the last floor.
        /// </summary>
        public const double DepthGrowth = 0.40;
        public const double DepthPow = 0.12;

        /// <summary>
        /// The reaction-time ceiling on the RATE dial, and the only one in the file. DERIVED, not
        /// chosen: a ranged body starts at 0.648 px/tick against a player who moves at 1.20, and the
        /// standing guarantee is that it never closes faster than 0.87. 0.87/0.648 is 1.343.
        ///
        /// The old ceiling was 1.95, which would allow 1.264 - FASTER THAN THE PLAYER. The old
        /// ladder only stayed honest because its own saturation never reached that ceiling before the
        /// content ran out, which is the real lesson: a safety limit that safety never needed is
        /// indistinguishable from no limit until the day it is needed.
        /// </summary>
        public const double DepthRateCap = 1.34;

        /// <summary>
        /// Density is capped too, and for a DIFFERENT reason: not fairness but playability. Uncapped
        /// it is 246 bodies by floor 40 and 1869 by floor 50 - not a hard fight, a hang. Past the
        /// point where the room stops being playable the ladder leans on health, which costs the
        /// player attention rather than the machine its frame budget.
        /// </summary>
        public const double DepthBodyPow = 0.085;
        public const double DepthBodyCap = 28;

        /// <summary>
        /// The Brunch pack chance. The one dial on this ladder that is still linear, because it
        /// already has a ceiling and the ceiling was the point: a room that is always a pack is one
        /// shape, and a deep floor has to stay a set of rooms.
        /// </summary>
        public const double DepthPackStep = 0.035, DepthPackCap = 0.85;

        /// <summary>The number of floors below the first one, floored at zero.</summary>
        public static int DepthSteps(int floor) => System.Math.Max(0, floor - 1);

        /// <summary>
        /// The curve: 1 + growth*(e^(rate*steps) - 1). Exactly 1 on floor one, strictly increasing
        /// for every floor after it, and unbounded - so the ladder is a DIRECTION rather than a number
        /// the curve approaches. The -1 rather than a bare e^ is what makes floor one free.
        /// </summary>
        public static double Ramp(int steps, double growth, double rate) =>
            1 + growth * (System.Math.Exp(rate * steps) - 1);

        public static double DepthTough(int floor) => Ramp(DepthSteps(floor), DepthGrowth, DepthPow);
        public static double DepthRate(int floor) =>
            System.Math.Min(DepthRateCap, Ramp(DepthSteps(floor), DepthGrowth, DepthPow * 0.55));

        /// <summary>
        /// Density is its own exponent rather than the health one, because it is a different kind of
        /// lever: health makes a fight LONGER and density makes it WIDER, and they should not move in
        /// lockstep or every deep floor would be the same fight with more health. Capped, for the
        /// playability reason on DepthBodyCap.
        /// The -1 makes floor one add NOTHING, for the same reason the health curve has one: a first
        /// floor has to be exactly the base experience, and a dial already 0.085 from zero on floor
        /// one is a dial that was retuned without anybody deciding to retune floor one.
        /// </summary>
        public static double DepthBodies(int floor, int rolled) =>
            System.Math.Min(DepthBodyCap, rolled + DepthBodyPow * (System.Math.Exp(DepthPow * 1.7 * DepthSteps(floor)) - 1));

        public static double DepthPack(int floor, double brunchChance) =>
            System.Math.Min(DepthPackCap, brunchChance + DepthPackStep * DepthSteps(floor));
    }
}
