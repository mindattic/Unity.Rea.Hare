namespace Unity.Rea.Hare.Rendering
{
    /// <summary>
    /// The project's sorting layers (U.HARE-T44), back to front: <see cref="Backdrop"/> (the rooms' background
    /// slots) draws first, <see cref="Default"/> (every object the original's
    /// per-depth draw order already covers, see <c>HareArt.SortingOrderForDepth</c>) next, then
    /// <see cref="Overlay"/> (the screen-wide fade and flash layer, <see cref="ScreenOverlayLayer"/>) on top of
    /// everything regardless of any object's own sorting order. The layers themselves are written into
    /// <c>ProjectSettings/TagManager.asset</c> by <c>Unity.Rea.Hare.EditorTools.Rendering.SortingLayerSetup</c>
    /// (the only other place this plan is expressed); nothing here needs Unity's render pipeline to know about
    /// it, so these names are plain strings usable from Core-adjacent Runtime code without a URP reference.
    /// </summary>
    public static class HareSortingLayers
    {
        public const string Backdrop = "Backdrop";
        public const string Default = "Default";
        public const string Overlay = "Overlay";

        /// <summary>The back-to-front order <see cref="SortingLayerSetup"/> in Editor/Rendering writes.</summary>
        public static readonly string[] Order = { Backdrop, Default, Overlay };
    }
}
