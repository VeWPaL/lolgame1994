using System;
using System.Collections.Generic;
using System.Linq;
using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The <see cref="Projectile"/> record, pinned by a round trip rather than by field-by-field
    /// equality assertions.
    /// </summary>
    /// <remarks>
    /// WHY A ROUND TRIP. The record was widened from seven fields to the twenty-three the projectile
    /// pass in <c>src/60-tick.js</c> (lines 592-741) actually reads, and the widening was MEASURED
    /// from the JavaScript rather than guessed: twenty-three distinct <c>p.&lt;field&gt;</c> reads,
    /// of which sixteen did not exist on the port. A test that asserted each field in turn would be
    /// twenty-three assertions that all fail in the same way if the record is replaced by a struct,
    /// and would silently pass if a field were renamed on one side only.
    ///
    /// <para>
    /// The round trip instead writes a DISTINCT value into every field and reads all of them back.
    /// Two things it catches that per-field assertions do not: a field that exists but is shadowed,
    /// and a field whose type cannot hold the value the JavaScript puts in it - <c>p.age</c> and
    /// <c>p.pierce</c> are integer counts in the original, and a double-typed port field would
    /// accept them without complaint and then compare wrongly against a body count.
    /// </para>
    ///
    /// <para>
    /// It also pins the seven fields that are deliberately NOT here. Shells are constructed with
    /// twenty-nine distinct fields across four JavaScript files, and seven of those belong to the
    /// view (<c>fNear</c>/<c>fMid</c>/<c>fFar</c> are the blast falloff band and belong with
    /// <c>explode</c>; <c>from</c>, <c>it</c>, <c>shrink</c> and <c>heavy</c> are presentation). A field
    /// in the port that no ported code reads is the drift this manifest exists to prevent, so their
    /// absence is asserted rather than left to review.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class ProjectileRecordTests
    {
        [Test]
        public void EveryFieldTheProjectilePassReads_SurvivesAWriteAndReadBack()
        {
            var body = new Body { MaxHp = 7 };
            var p = new Projectile
            {
                // motion and identity - present before this slice
                x = 1.5, y = 2.5, vx = 3.5, vy = 4.5,
                r = 5.5, dmg = 6.5,
                friendly = true, heavy = true,

                // the sixteen this slice added
                age = 11,
                alt = true,
                phase = true,
                tx = 12.5, ty = 13.5,
                dx = 0.6, dy = 0.8,
                ox = 14.5, oy = 15.5,
                speed = 2.4,
                pierce = 2,
                scale = 0.875,
                color = "#e8502a",
                owner = body,
                mode = "hook",
            };
            p.hit = new List<Body> { body };

            // --- doubles
            Assert.Multiple(() =>
            {
                Assert.That(p.x, Is.EqualTo(1.5));
                Assert.That(p.y, Is.EqualTo(2.5));
                Assert.That(p.vx, Is.EqualTo(3.5));
                Assert.That(p.vy, Is.EqualTo(4.5));
                Assert.That(p.r, Is.EqualTo(5.5));
                Assert.That(p.dmg, Is.EqualTo(6.5));
                Assert.That(p.tx, Is.EqualTo(12.5));
                Assert.That(p.ty, Is.EqualTo(13.5));
                Assert.That(p.ox, Is.EqualTo(14.5));
                Assert.That(p.oy, Is.EqualTo(15.5));
                Assert.That(p.speed, Is.EqualTo(2.4));
                Assert.That(p.scale, Is.EqualTo(0.875));
                Assert.That(p.dx, Is.EqualTo(0.6));
                Assert.That(p.dy, Is.EqualTo(0.8));
            });

            // --- counts, and their TYPES. These are INTEGERS in the original: `p.age++` and
            // `p.pierce--` count ticks and bodies, and `p.pierce>0` decides whether a bolt keeps going.
            //
            // MUTATION-CHECKED, and the first version of this assertion did NOT hold. Changing
            // `pierce` from int to double left the suite GREEN, because NUnit's Is.EqualTo compares
            // 2 and 2.0 as equal - so an assertion on the VALUE cannot pin the TYPE, and the comment
            // claiming it did was itself the kind of unverified claim this repo keeps warning about.
            // Asserting on FieldType is what makes the claim real; it is here because the mutation
            // passed, not because the type looked important.
            Assert.Multiple(() =>
            {
                Assert.That(p.age, Is.EqualTo(11));
                Assert.That(p.pierce, Is.EqualTo(2));
                Assert.That(typeof(Projectile).GetField("age")!.FieldType, Is.EqualTo(typeof(int)),
                    "p.age counts ticks; as a double it would hold every value JavaScript can produce "
                    + "and still be the wrong type");
                Assert.That(typeof(Projectile).GetField("pierce")!.FieldType, Is.EqualTo(typeof(int)),
                    "p.pierce counts remaining bodies; a double here compares wrongly against the "
                    + "body count the piercing loop compares it against");
            });

            // --- flags
            Assert.Multiple(() =>
            {
                Assert.That(p.friendly, Is.True);
                Assert.That(p.heavy, Is.True);
                Assert.That(p.alt, Is.True);
                Assert.That(p.phase, Is.True);
            });

            // --- references and strings
            Assert.Multiple(() =>
            {
                Assert.That(p.owner, Is.SameAs(body),
                    "owner is how a body is kept from shooting itself; a copy would not be the body");
                Assert.That(p.hit, Is.Not.Null);
                Assert.That(p.hit, Has.Count.EqualTo(1));
                Assert.That(p.hit![0], Is.SameAs(body),
                    "p.hit holds REFERENCES to the bodies already counted, so a piercing bolt cannot "
                    + "damage the same body twice and cannot reorder on a value copy");
                Assert.That(p.color, Is.EqualTo("#e8502a"));
                Assert.That(p.mode, Is.EqualTo("hook"),
                    "mode is the weapon the shell was CAST with, resolved on arrival rather than "
                    + "from whatever is held then");
            });
        }

        [Test]
        public void AFreshShell_DefaultsToTheOriginalsValues()
        {
            // `default` in C# is 0/false/null, which matches the JavaScript object literal for every
            // numeric and boolean field. What is NOT the default in the original is `scale`: a shell is
            // constructed with an explicit scale of 1 and falls off from there, so a port that left it
            // at 0 would make every shell harmless rather than every shell wrong in an obvious way.
            var p = new Projectile();
            Assert.Multiple(() =>
            {
                Assert.That(p.age, Is.Zero);
                Assert.That(p.pierce, Is.Zero);
                Assert.That(p.scale, Is.Zero,
                    "the original always passes an explicit scale at construction; a zero here would "
                    + "silently make every shell deal no damage, so construction must set it");
                Assert.That(p.friendly, Is.False);
                Assert.That(p.alt, Is.False);
                Assert.That(p.phase, Is.False);
                Assert.That(p.hit, Is.Null);
                Assert.That(p.owner, Is.Null);
                Assert.That(p.color, Is.Empty);
                Assert.That(p.mode, Is.Empty);
            });
        }

        [Test]
        public void TheSevenViewOnlyFields_AreAbsentFromThePort()
        {
            // Asserted by NAME rather than by reflection, because reflection over a record that has
            // gained a field for a legitimate reason would then need this test edited - which is the
            // point. If one of these is ever ported, the reason belongs in PORTED.md next to the
            // change, and this line is where that decision gets recorded.
            var names = typeof(Projectile)
                .GetFields(System.Reflection.BindingFlags.Public
                           | System.Reflection.BindingFlags.Instance)
                .Select(f => f.Name)
                .ToHashSet();

            foreach (var viewOnly in new[] { "fNear", "fMid", "fFar", "from", "it", "shrink" })
                Assert.That(names, Does.Not.Contain(viewOnly),
                    viewOnly + " is a presentation or blast-falloff field with no ported reader; "
                    + "adding it without the code that reads it is the drift PORTED.md exists to stop");

            // `heavy` IS declared - it arrived with the original stub - but the projectile pass never
            // reads it. It is the one exception, and it is recorded rather than quietly tolerated.
            Assert.That(names, Does.Contain("heavy"),
                "heavy was on the record before this slice; if it is being removed, do it in a commit "
                + "that says so rather than as a side effect of widening the record");
        }
    }
}