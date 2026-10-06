# Bodies.cs - moved comments

## [h:Bodies-1] the armour rule

Every row in <see cref="Rows"/> sets <c>Armour</c> explicitly, and that is a rule rather
than a coincidence. <c>BodyRow.Armour</c> is a plain <c>double</c>, so a row that simply
forgot it reads 0.0 - and this port used to disagree with the JavaScript on exactly that
point, in OPPOSITE directions: C# forgot to a body that absorbs everything, JavaScript
forgot to a body that takes full damage while every other body is multiplied by 0.66. The
Warden was the casualty in JS, where the omission was deliberate and commented as such, so
both ports were wrong and neither one marked the other wrong.

<para>
The fix is not a cleverer default - a default would only move the failure to whichever
value someone happened to pick. It is that every row names its armour, so a row that
forgets is a test failure. <c>BodyParityTests</c> asserts it, and asserts the rule it has
to obey: every body at least as wide as a lunger is armoured, and the Brunch - half a
lunger's radius - is the single body that is not.
</para>

## [h:Bodies-2] the old BrunchRun 1.75 (stale)

Removed 2026-10-06. Bodies.cs kept its own BrunchWalk (0.62*PlayerMove = 0.744) and BrunchRun (1.75)
after the game moved to BRUNCH_WALK 0.52*PLAYER_MOVE (0.624) and BRUNCH_CHASE_SPEED 2.1, and the lunger
at 18*TOUGH after the game moved to 15*TOUGH. BodyParityTests pinned the stale numbers by hand, so it
passed. The rows now read Balance.BrunchWalkSpeed / Balance.BrunchRun, which tools/constant-audit.js
checks against the running game. The old rationale:

The Brunch's last-resort chase speed: the one it uses when it has nothing left to shield.
Measured against the player's 1.2 in a room large enough that a 14s chase never reaches a
wall, 1.35 grew the gap ~107px per 2s - a pack that could never run the player down, which
is the only job this sprint exists for. 1.62 merely held the gap and never closed it.
1.75 is the first value on the sweep that closes (~62px per 2s from 450px). The BRUNCH_RAMP
announcement is unchanged, so the pack is still slower than the player for ~1.1s.
</summary>
