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
    }
}
