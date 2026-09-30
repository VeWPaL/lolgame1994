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
        /// HALF THE ROOM'S WIDTH, LESS A MARGIN. It is not half the width.
        /// <para>
        /// This port said 350 and the JavaScript said 300, and there was a test here called
        /// <c>TheDeadzoneIsHalfTheRoomWidth</c> that asserted 350 - so the drift was not merely
        /// unnoticed, it was protected. The number had been written from the RULE rather than from
        /// the value, and the test then re-derived the same rule and passed. A test that agrees with
        /// the thing it audits cannot find the disagreement; this is the third time in this project
        /// that shape has cost something.
        /// </para>
        /// <para>
        /// The margin is 50 and the ramp is 130 past the deadzone. Both are functions of the room
        /// because "how far away is far" is a property of the room, and a constant computed from a
        /// room once is a number that quietly stops describing what it names the moment a second room
        /// exists - which is exactly what happened on the JavaScript side, where it froze at 707.
        /// </para>
        /// </remarks>
        public const int SwerveMargin = 50, SwerveFullBase = 130;

        // Methods rather than a property plus an overload, because C# will not let a property and a
        // method share a name - CS0102 - and a property here would also have been the wrong shape.
        // The JavaScript has one function taking an optional room, so this is the closest honest
        // mirror of that available in the language: two methods, one of which forwards.
        public static int SwerveDeadzone(Room room) => (int)System.Math.Round(room.W / 2.0) - SwerveMargin;
        public static int SwerveFull(Room room) => SwerveDeadzone(room) + SwerveFullBase;
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
        /// The hitbox the enemies aim at trails the real position and converges over roughly 0.4s -
        /// the reaction window a blink is supposed to buy. Without it a gunner that was already
        /// tracking you gets a free intercept shot the instant you vanish.
        /// </summary>
        public const double HitboxLagEase = 0.05;

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
        /// The Brunch ramp. It used to reach full commitment in 0.23s, which meant a pack was on you
        /// before you had finished looking at where it had come from - there was no interval in
        /// which to pick your ground, which is the one thing the pack is supposed to be asking of
        /// you. 2.2s with a lower peak gain is a pack that announces itself and then arrives, instead
        /// of one that is simply on top of you from the moment it was noticed. A Brunch is still
        /// faster than you once it commits - that is the whole reason it is frightening - it just
        /// commits in front of you now.
        /// </summary>
        public static int BrunchRamp => Sec(2.2);
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
        public const double SwerveGain = 0.22, SwerveDecay = 0.011, SwerveAim = 0.30;

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
        /// The shape is 1 + growth*steps/(steps+tau): exactly 1 on the first floor, monotone
        /// forever, approaching 1+growth without ever reaching it. No cap check is needed because the
        /// cap IS the asymptote.
        /// </summary>
        public const double DepthHpGrowth = 2.60, DepthHpTau = 8;
        public const double DepthRateGrowth = 0.95, DepthRateTau = 10;
        public const double DepthBodyGrowth = 5.6;

        /// <summary>
        /// The Brunch pack chance. The one dial on this ladder that is still linear, because it
        /// already has a ceiling and the ceiling was the point: a room that is always a pack is one
        /// shape, and a deep floor has to stay a set of rooms.
        /// </summary>
        public const double DepthPackStep = 0.035, DepthPackCap = 0.85;

        /// <summary>The number of floors below the first one, floored at zero.</summary>
        public static int DepthSteps(int floor) => System.Math.Max(0, floor - 1);

        /// <summary>
        /// The saturating shape, shared by the health and rate dials. Three dials written three ways
        /// would drift apart the moment one of them was retuned, which is the geometry-in-two-places
        /// failure wearing a balance patch instead of a hitbox.
        /// </summary>
        public static double Sat(int steps, double growth, double tau) => 1 + growth * steps / (steps + (double)tau);

        public static double DepthTough(int floor) => Sat(DepthSteps(floor), DepthHpGrowth, DepthHpTau);
        public static double DepthRate(int floor) => Sat(DepthSteps(floor), DepthRateGrowth, DepthRateTau);

        /// <summary>
        /// Density is logarithmic rather than saturating, deliberately. Density is the one dial the
        /// brief actually names, and a saturating curve would flatten a real difficulty lever just
        /// to buy a tidier table. log(1+n) rather than log(n) so floor one adds nothing at all.
        /// </summary>
        public static double DepthBodies(int floor, int rolled) =>
            rolled + DepthBodyGrowth * System.Math.Log(1 + DepthSteps(floor));

        public static double DepthPack(int floor, double brunchChance) =>
            System.Math.Min(DepthPackCap, brunchChance + DepthPackStep * DepthSteps(floor));
    }
}
