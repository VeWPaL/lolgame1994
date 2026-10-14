namespace Depths
{
    /// <summary>The themed blocks of the climb. [h:Area-1]</summary>
    public enum Area { Area1, Area2, Area3, Final }

    /// <summary>The theme lookup. PURE: it reads the floor number and nothing else, spends no RNG, and is therefore safe to... [h:Area-2]</summary>
    public static class AreaRules
    {
        public static Area AreaForFloor(int floor)
        {
            if (floor <= 4) return Area.Area1;
            if (floor <= 8) return Area.Area2;
            if (floor <= 12) return Area.Area3;
            return Area.Final;
        }
    }
}
