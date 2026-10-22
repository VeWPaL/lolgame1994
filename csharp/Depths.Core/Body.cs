namespace Depths
{
    /// <summary>Which kind of body this is. The kind is not cosmetic: the tick branches on it.</summary>
    public enum BodyKind
    {
        Lunger,
        Brunch,
        Shooter,
        Gunner,
        Boss,
        // the middle enemies (Middle.cs; C# only). Appended: Bodies.Rows is indexed by kind
        Splitter,
        Mortar,
        Burrower,
        Sprayer,
        Egg,
        // the floor boss of floors 1-13 (FloorBoss.cs; C# only); the Warden (Boss) stays the final floor's
        Gatekeeper,
        // the final boss of floor 14 (FinalBoss.cs; C# only)
        SunkenKing,
    }
}
