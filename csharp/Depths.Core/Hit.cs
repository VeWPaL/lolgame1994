namespace Depths
{
    /// <summary>The one place that asks "did that hit me". [h:Hit-1]</summary>
    public static class Hit
    {
        /// <summary>Tests a point against the player's hitbox. [h:Hit-2]</summary>
        public static bool PlayerHit(double x, double y, double r, double lagX, double lagY)
        {
            double dy = y - (lagY + Balance.PlayerHitDy);
            double dx = x - lagX;
            return System.Math.Sqrt(dx * dx + dy * dy) < r + Balance.PlayerHitR;
        }
    }
}
