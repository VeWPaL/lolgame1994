namespace Depths
{
    /// <summary>
    /// spawnEnemy for a body placed at a known point (src/30-enemies.js), draw for draw: the idle
    /// timer then the notice timer from the jitter stream; a walker takes the run's golden-angle flank;
    /// a ranged body rolls its first cooldown (jitter) and its band trait (run, by the held gun); the
    /// Warden gets its kit. Depth scaling uses the run's floor.
    /// </summary>
    public static class Spawn
    {
        const double FlankStep = 2.399963229728653, Tau = 6.283185307179586;
        const double TraitHoldScale = 1.45, TraitCloseScale = 0.72, TraitChance = 0.5;
        const double TraitFarCeil = 0.78, TraitFarFloor = 90, TraitBandMin = 40;

        // trait: one the wave planner already rolled (no draw here); null rolls it, as a lone spawn does
        public static Enemy Body(RunState run, BodyKind kind, double x, double y, int? trait = null)
        {
            var e = Enemy.Of(kind, x, y);
            double tough = Balance.DepthTough(run.floor), rate = Balance.DepthRate(run.floor);
            e.hp = e.maxHp = e.maxHp * tough;
            e.idleTimer = (int)(run.rng.Jitter() * Balance.WanderTicks);
            e.noticeTimer = (int)(run.rng.Jitter() * 16 * Balance.Speedup);
            e.speed *= rate;
            if (kind == BodyKind.Boss) { BossInit(e); e.aggroTimer = 9999; return e; }
            if (kind == BodyKind.Lunger || kind == BodyKind.Brunch)
            {
                e.flank = run.flankCursor;
                run.flankCursor += FlankStep;
                if (run.flankCursor > Tau) run.flankCursor -= Tau;
                return e;
            }
            e.cdMin /= rate; e.cdVar /= rate;
            e.shootCd = e.cdMin + run.rng.Jitter() * e.cdVar;
            ApplyTrait(e, trait ?? RollTrait(run.rng, run.player.weaponIdx));
            return e;
        }

        /// <summary>spawnEnemy with no position: a random point inside the spawn margin (x then y, run stream).</summary>
        public static Enemy BodyAnywhere(RunState run, BodyKind kind)
        {
            double x = Balance.RoomLeft + Balance.SpawnMargin + run.rng.Run() * (Balance.RoomRight - Balance.RoomLeft - Balance.SpawnMargin * 2);
            double y = Balance.RoomTop + Balance.SpawnMargin + run.rng.Run() * (Balance.RoomBottom - Balance.RoomTop - Balance.SpawnMargin * 2);
            return Body(run, kind, x, y);
        }

        /// <summary>bossInit: phase 1, idle, a first cooldown, and the gunner-like ranged kit.</summary>
        public static void BossInit(Enemy e)
        {
            e.phase = 1; e.move = "idle"; e.moveT = 0; e.bossCd = Balance.Sec(1.2);
            e.volleyLeft = 0; e.volleyT = 0; e.wallT = 0;
            e.sense = 900; e.close = 140; e.far = 260;
            e.cdMin = Balance.BossCdMin; e.cdVar = Balance.BossCdVar;
            e.dmg = Balance.BossShellDmg; e.pspd = 1.9; e.pr = 8;
        }

        // TRAIT_TABLE by the gun the player holds: (trait, weight)
        static readonly (int trait, double weight)[] Traits = { (2, 0.50), (1, 0.70), (1, 0.62), (2, 0.50) };

        /// <summary>rollTrait: one run draw against the held gun's weight; 0 (none), 1 (hold) or 2 (close).</summary>
        public static int RollTrait(Rng rng, int weaponIdx)
        {
            if (weaponIdx < 0 || weaponIdx >= Traits.Length) return 0;
            var row = Traits[weaponIdx];
            return rng.Run() < TraitChance * row.weight ? row.trait : 0;
        }

        static void ApplyTrait(Enemy e, int trait)
        {
            if (trait == 0) return;
            e.trait = trait;
            double k = trait == 1 ? TraitHoldScale : TraitCloseScale, cap = e.sense * TraitFarCeil;
            double far = e.far * k, close = e.close * k;
            if (far > cap) far = cap;
            if (far < TraitFarFloor) far = TraitFarFloor;
            if (far - close < TraitBandMin) close = far - TraitBandMin;
            e.far = far; e.close = close;
        }
    }
}
