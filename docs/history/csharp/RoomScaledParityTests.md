# RoomScaledParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/RoomScaledParityTests.cs`. The code keeps a one-line gist tagged `[h:RoomScaledParityTests-N]`.

## [h:RoomScaledParityTests-1]
near: `[TestFixture]`

/// <summary>
/// The room-scaled numbers, at three room sizes, read out of the running JavaScript.
/// </summary>
/// <remarks>
/// WHY THIS TABLE EXISTS AND WHY THE THIRD ROW IS SMALLER THAN THE FIRST.
/// <para>
/// Every other parity table in this project pins a constant or a function of a SEED, so a drift in
/// one is caught by comparing names. These are functions of the ROOM — <c>room.W * 0.25</c> and
/// friends — and no amount of grepping finds a value that is right in one room and wrong in
/// another.
/// </para>
/// <para>
/// That is not hypothetical. The fix for a frozen room number REPRODUCED the bug it was fixing:
/// <c>swerveDeadzone</c> was made a function of the room while the ramp beside it was left as a
/// value captured once at load. The two sat in the same expression, one following the room and one
/// frozen at the standard room's answer, and the assertion in place was
/// <c>SwerveFull() &gt; SwerveDeadzone()</c> — which passes for both, because 805 &gt; 420 is true
/// and so is 1344 &gt; 420. A check that a quantity is non-empty cannot distinguish a quantity that
/// follows its input from one that is constant.
/// </para>
/// <para>
/// So the rule this table enforces is: <b>ask for the value the input implies, in an input where
/// the two answers disagree.</b> 700×450 is the room that existed when the balance file loaded, so
/// it is the one room a load-time capture gets right by construction. 1680×760 is twice as wide.
/// 350×225 is HALF as wide, and it is the row that matters most: a constant cannot narrow.
/// </para>
/// <para>
/// Values are read from <c>depths.html?parity</c>, which now emits this section. Regenerate rather
/// than hand-computing: hand-computing a parity value produced three wrong numbers out of nine once
/// already, and the test was what failed each time.
/// </para>
/// </remarks>

## [h:RoomScaledParityTests-2]
near: `var std = Balance.Room.Standard;`

// Stated separately from the table above because the table pins VALUES and this pins the
// SHAPE. A capture-at-load bug passes every row of the table above except the small one,
// and it passes this test in the standard room too — so the assertions have to be about
// the direction of travel, not about the standard room agreeing with itself.

## [h:RoomScaledParityTests-3]
near: `foreach (var room in new[] { std, big, small })`

// and the ramp can never saturate inside the room it is scaled to. Under the old fixed
// 130px ramp, any room wider than 430px had a stretch past which "further is worse" could
// not be expressed at all — which is the entire defect this area exists to prevent.

## [h:RoomScaledParityTests-4]
near: `var small = new Balance.Room(50, 130, 50 + 350, 130 + 225);`

// The other half of "follows the room": something must NOT move with it. The spawn dials
// are offsets from a wall, not fractions, and a spawn planner that tracked room size would
// quietly stop meaning anything in a big room. Asserted so the direction is explicit.
