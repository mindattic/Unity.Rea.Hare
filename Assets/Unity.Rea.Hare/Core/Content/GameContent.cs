namespace Unity.Rea.Hare.Content
{
    /// <summary>
    /// The live <see cref="ContentRegistry"/> every Core rule reads its tuning and content from, instead of a
    /// `new WeaponTable()` or a hardcoded literal. Starts at <see cref="ContentRegistry.Default"/> (the compiled-in
    /// numbers, already validated), so Core keeps working standalone under `dotnet test`. Runtime's content loader
    /// overwrites this once, at startup, after parsing and validating the game's data file; nothing else should
    /// assign it outside tests, which may swap it in and (in a `[TearDown]`) restore <see cref="ContentRegistry.Default"/>.
    /// </summary>
    public static class GameContent
    {
        public static ContentRegistry Current { get; set; } = ContentRegistry.Default;
    }
}
