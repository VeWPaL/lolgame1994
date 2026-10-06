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

        public static Enemy Body(RunState run, BodyKind kind, double x, double y)
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
            ApplyTrait(run, e);
            return e;
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

        static void ApplyTrait(RunState run, Enemy e)
        {
            int w = run.player.weaponIdx;
            if (w < 0 || w >= Traits.Length) return;
            var row = Traits[w];
            if (!(run.rng.Run() < TraitChance * row.weight)) return;
            e.trait = row.trait;
            double k = row.trait == 1 ? TraitHoldScale : TraitCloseScale, cap = e.sense * TraitFarCeil;
            double far = e.far * k, close = e.close * k;
            if (far > cap) far = cap;
            if (far < TraitFarFloor) far = TraitFarFloor;
            if (far - close < TraitBandMin) close = far - TraitBandMin;
            e.far = far; e.close = close;
        }
    }
}
