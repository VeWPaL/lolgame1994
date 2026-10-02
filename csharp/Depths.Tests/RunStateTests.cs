using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The run-scoped holder, and the one-seed-one-run property it exists to protect.
    ///
    /// <para>
    /// Finding #8, in the holder's own terms: the JavaScript kept <c>PACK_CURSOR</c> and the RNG
    /// cursors outside any function, so module state outlived <c>startGame</c> and a second run
    /// from the same seed began with cursors the first run had moved. These tests pin the C# shape
    /// of the fix - every run's cursors live on its own object - by constructing two runs from the
    /// same seed and proving they are independent copies rather than two handles on one cursor.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class RunStateTests
    {
        [Test]
        public void OneSeedOneRunReplaysTheSameDungeonButAdvancesIndependently()
        {
            uint seed = 1234567u;
            var runA = new RunState(new Rng(seed));
            var runB = new RunState(new Rng(seed));

            // same stream, same floor: the seed means something again
            runA.dungeon.Generate();
            runB.dungeon.Generate();
            Assert.That(runA.dungeon.Signature(), Is.EqualTo(runB.dungeon.Signature()),
                "two runs from one seed produced different dungeons, so the seed is decorative");
            Assert.That(runA.rng.RunCalls, Is.EqualTo(runB.rng.RunCalls),
                "the two runs consumed the stream differently, so the same work cost two seeds");

            // and the independence half: advancing A must not move B at all
            int aCalls = runA.rng.RunCalls;
            double aNext = runA.rng.Run();
            Assert.That(runB.rng.RunCalls, Is.EqualTo(aCalls),
                "drawing from one run's stream advanced the other run's cursor");
            Assert.That(runB.rng.Run(), Is.EqualTo(aNext),
                "the second run's next draw is not where the first run started - the streams are shared");
        }

        [Test]
        public void ThePlannerCursorBelongsToTheRunNotToTheStatic()
        {
            // seed 12345 floor 1 plans exactly one Brunch pack (the parity table pins this), so
            // PacksIssued moves - and a cursor kept statically would move with it and leak into
            // the next run, which is finding #8's exact symptom.
            var first = new RunState(new Rng(12345u));
            first.dungeon.Generate();
            first.planner.PlanWave(1, Dir.N);
            Assert.That(first.planner.PacksIssued, Is.EqualTo(1),
                "the parity table says this wave plans one pack");

            var second = new RunState(new Rng(12345u));
            Assert.That(second.planner.PacksIssued, Is.EqualTo(0),
                "a shared pack cursor would leak the first run's count into the second run");
        }

        [Test]
        public void AFreshRunStartsEveryCursorAtItsOrigin()
        {
            // The planner's cursor is per-run rather than per-floor: pack ids are unique across
            // floors, so a fresh RunState must start from 1 no matter what the previous run left
            // behind. This is the structural version of the JS resetRunCursors() call that used
            // to be missing entirely.
            var first = new RunState(new Rng(7u));
            first.dungeon.Generate();
            Assert.That(first.planner.PacksIssued, Is.EqualTo(0));

            var second = new RunState(new Rng(7u));
            Assert.That(second.planner.PacksIssued, Is.EqualTo(0),
                "a new RunState inherited a pack cursor - it must build its own");
            Assert.That(second.ticks, Is.EqualTo(0));
            Assert.That(second.floorTicks, Is.EqualTo(0));
            Assert.That(second.hits, Is.EqualTo(0));
            Assert.That(second.floor, Is.EqualTo(1));
        }

        [Test]
        public void RunCountersStartAtZeroAndTheStateIsNotPlayingYet()
        {
            // state 'start' matters: Update() gates on 'playing', and a run constructed but not
            // started must tick nothing, exactly like startGame() before the room is entered.
            var run = new RunState(new Rng(1u));
            Assert.That(run.state, Is.EqualTo("start"));
            Assert.That(run.ticks, Is.EqualTo(0));
            Assert.That(run.floorTicks, Is.EqualTo(0));
            Assert.That(run.hits, Is.EqualTo(0));
            Assert.That(run.readyT, Is.EqualTo(0));
            Assert.That(run.trans, Is.Null);
            Assert.That(run.projectiles, Is.Empty);
            Assert.That(run.transients, Is.Empty);
        }
    }
}
