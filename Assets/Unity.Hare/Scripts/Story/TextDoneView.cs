using UnityEngine;
using UnityEngine.UIElements;
using Unity.Hare.Art;

namespace Unity.Hare.Story
{
    /// <summary>
    /// obj_textdone drawn (U.HARE-T87): spr_textdone's frame at its 8x scale where Core's <see cref="TextDoneMarker"/>
    /// puts it, in the view's bottom right corner. Hidden while the marker does not exist. The caller offsets it
    /// (<see cref="Present"/>'s <c>originY</c>) when it sits inside a box that moves.
    /// </summary>
    public sealed class TextDoneView : VisualElement
    {
        public const string ElementName = "obj_textdone";

        readonly TextDoneMarker marker;
        readonly HareArtManifest manifest;
        readonly int viewWidth, viewHeight;
        int shownFrame = -1;

        public TextDoneView(TextDoneMarker marker, int viewWidth, int viewHeight)
        {
            this.marker = marker;
            this.viewWidth = viewWidth;
            this.viewHeight = viewHeight;
            manifest = HareArtManifest.Load();
            name = ElementName;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            var d = marker.Definition;
            style.left = (float)marker.X(viewWidth);
            style.width = d.Width * d.Scale;
            style.height = d.Height * d.Scale;
            style.display = DisplayStyle.None;
            if (manifest == null || manifest.Frame(d.Sprite) == null) Debug.LogWarning("[Unity.Hare] " + d.Sprite + " is not imported.");
        }

        /// <summary>Shows the marker's frame, <paramref name="originY"/> being the GUI y of this element's parent.</summary>
        public void Present(double originY = 0d)
        {
            if (!marker.Visible)
            {
                style.display = DisplayStyle.None;
                shownFrame = -1;
                return;
            }
            style.display = DisplayStyle.Flex;
            style.top = (float)(marker.Y(viewHeight) - originY);
            if (marker.Frame == shownFrame) return;
            shownFrame = marker.Frame;
            var frame = manifest != null ? manifest.Frame(marker.Definition.Sprite, shownFrame) : null;
            if (frame != null) style.backgroundImage = new StyleBackground(frame);
        }
    }
}
