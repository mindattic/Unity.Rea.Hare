using System.Collections;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Unity.Hare.UI;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T48: the original's fonts actually reach the screen. On a live UI Toolkit panel every named text style's
    /// Theme.uss rule resolves to its face's bitmap FontAsset at the face's native size, and (with a graphics device)
    /// each glyph is drawn exactly as GameMaker baked it, 1:1, with nothing bleeding in from its atlas neighbours, and a
    /// whole line's glyphs land on draw_text's own pen positions (each <c>shift</c> after the last, drawn <c>offset</c>
    /// right of the pen). The rendering test also writes Screenshots/fonts.png, a sample line in every style.
    /// </summary>
    public class HareTextPlayTests
    {
        const int Width = 1024, Height = 640, ProbeY = 16, Ring = 12, LineX = 8;
        static int ProbeX(int i) => 16 + i * 96;
        static string SampleLine(HareTextStyles.Style style) => style.Name + ": Level 0123456789";

        GameObject host;
        PanelSettings settings;
        RenderTexture target;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.Destroy(host);
            if (settings != null) Object.Destroy(settings);
            if (target != null) { target.Release(); Object.Destroy(target); }
        }

        [UnityTest]
        public IEnumerator EveryTextStyleResolvesToItsFaceAtItsNativeSize()
        {
            host = new GameObject("Hare UI");
            var document = HarePanel.Attach(host);
            settings = document.panelSettings;
            var stage = HarePanel.BuildRoot(document);

            var labels = new Label[HareTextStyles.All.Length];
            for (int i = 0; i < labels.Length; i++)
                stage.Add(labels[i] = HareTextStyles.Apply(new Label("Level Complete"), HareTextStyles.All[i]));
            var unstyled = new Label("Level Complete");
            stage.Add(unstyled);

            yield return null;
            yield return null;

            for (int i = 0; i < labels.Length; i++)
            {
                var style = HareTextStyles.All[i];
                var asset = HareFonts.Get(style.Face);
                Assert.That(asset, Is.Not.Null, style.Name);
                Assert.That(labels[i].resolvedStyle.unityFontDefinition.fontAsset, Is.SameAs(asset), style.Name + "'s Theme.uss rule resolves to its face");
                Assert.That(labels[i].resolvedStyle.fontSize, Is.EqualTo(asset.faceInfo.pointSize), style.Name + " is at the face's native size");
                Assert.That(labels[i].layout.width, Is.GreaterThan(0f), style.Name + " laid out");
            }
            // Text with no style of its own inherits .hare-root's face: fnt_point, the one the original draws most with.
            Assert.That(unstyled.resolvedStyle.unityFontDefinition.fontAsset, Is.SameAs(HareFonts.Get(HareFonts.Point)));
            Assert.That(unstyled.resolvedStyle.fontSize, Is.EqualTo(12f));
        }

        [UnityTest]
        public IEnumerator GlyphsDrawExactlyAsGameMakerBakedThem()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("needs a graphics device: run the PlayMode tests without -nographics (tools/unity.ps1 -Graphics)");

            target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = "HareTextPlayTests" };
            target.Create();
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.hideFlags = HideFlags.DontSave;
            settings.targetTexture = target;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.clearColor = true;
            settings.colorClearValue = Color.black;
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(HarePanel.ThemeStyleSheetResourcePath);

            host = new GameObject("Hare text render");
            var document = HarePanel.Attach(host, settings);
            var stage = HarePanel.BuildRoot(document);
            stage.style.backgroundColor = Color.black;

            // One "M" in every face, each pinned to a known pixel with room around it...
            var faces = new[] { HareFonts.Basic, HareFonts.Medium, HareFonts.Point, HareFonts.Score, HareFonts.Small };
            for (int i = 0; i < faces.Length; i++)
            {
                var probe = Place(new Label("M"), ProbeX(i), ProbeY);
                HareFonts.Apply(probe, faces[i]);
                stage.Add(probe);
            }
            // ...and a sample line in every named style, each in its own band (its tallest cell) so no line's
            // descenders reach the next.
            var lineTops = new int[HareTextStyles.All.Length];
            int y = 120;
            for (int i = 0; i < lineTops.Length; i++)
            {
                var style = HareTextStyles.All[i];
                lineTops[i] = y;
                stage.Add(Place(HareTextStyles.Apply(new Label(SampleLine(style)), style), LineX, y));
                var face = HareFonts.Get(style.Face).faceInfo;
                y += (int)(face.lineHeight - face.descentLine) + 4;
            }
            Assert.That(y, Is.LessThan(Height), "every sample line fits the target");

            for (int i = 0; i < 4; i++) yield return null;

            var rendered = Read(target);
            SaveScreenshot(rendered);
            float Drawn(int x, int yDown) => rendered[(Height - 1 - yDown) * Width + x].r / 255f;

            for (int i = 0; i < faces.Length; i++)
            {
                var asset = HareFonts.Get(faces[i]);
                var alpha = AtlasAlpha(asset);
                int atlasWidth = asset.atlasTextures[0].width;
                var glyph = asset.characterLookupTable['M'].glyph;
                var r = glyph.glyphRect;
                int left = ProbeX(i) + (int)glyph.metrics.horizontalBearingX, top = ProbeY; // the cell's top sits on the line's top

                int ink = 0, matched = 0;
                for (int gy = 0; gy < r.height; gy++)
                for (int gx = 0; gx < r.width; gx++)
                {
                    // Atlas rows are bottom-up, so the cell's top row is r.y + r.height - 1.
                    float coverage = alpha[(r.y + r.height - 1 - gy) * atlasWidth + r.x + gx] / 255f;
                    float drawn = Drawn(left + gx, top + gy);
                    if (coverage > 0.5f) { ink++; if (drawn > 0.35f) matched++; }
                    if (coverage == 0f) Assert.That(drawn, Is.LessThan(0.1f), $"{faces[i]} 'M' pixel ({gx},{gy}) is blank in the bake but drawn");
                }
                Assert.That(ink, Is.GreaterThan(20), faces[i] + ": the bake's M has ink");
                Assert.That(matched, Is.EqualTo(ink), faces[i] + ": every inked pixel of the bake's M is drawn in place");

                // Nothing is drawn in a ring around the cell: the glyph is neither offset nor scaled, and no
                // neighbouring glyph's ink bleeds in from the atlas.
                for (int yy = top - Ring; yy < top + r.height + Ring; yy++)
                for (int xx = left - Ring; xx < left + r.width + Ring; xx++)
                {
                    bool inCell = xx >= left && xx < left + r.width && yy >= top && yy < top + r.height;
                    if (!inCell) Assert.That(Drawn(xx, yy), Is.LessThan(0.1f), $"{faces[i]}: ink outside the M's cell at ({xx - left},{yy - top})");
                }
            }

            // A whole line: its ink starts where draw_text puts its first glyph's first inked column and ends where the
            // pen, stepped by every shift before it, puts the last glyph's last inked column.
            for (int i = 0; i < lineTops.Length; i++)
            {
                var style = HareTextStyles.All[i];
                var asset = HareFonts.Get(style.Face);
                var alpha = AtlasAlpha(asset);
                string text = SampleLine(style);
                int pen = LineX;
                int expectedLeft = int.MaxValue, expectedRight = int.MinValue;
                foreach (char c in text)
                {
                    var glyph = asset.characterLookupTable[c].glyph;
                    var (first, last) = InkColumns(alpha, asset.atlasTextures[0].width, glyph.glyphRect);
                    if (first >= 0)
                    {
                        int x = pen + (int)glyph.metrics.horizontalBearingX;
                        expectedLeft = Mathf.Min(expectedLeft, x + first);
                        expectedRight = Mathf.Max(expectedRight, x + last);
                    }
                    pen += (int)glyph.metrics.horizontalAdvance;
                }

                var face = asset.faceInfo;
                int bandBottom = lineTops[i] + (int)(face.lineHeight - face.descentLine);
                int drawnLeft = int.MaxValue, drawnRight = int.MinValue;
                for (int yy = lineTops[i]; yy < bandBottom; yy++)
                for (int xx = 0; xx < Width; xx++)
                    if (Drawn(xx, yy) > 0.5f) { drawnLeft = Mathf.Min(drawnLeft, xx); drawnRight = Mathf.Max(drawnRight, xx); }
                Assert.That((drawnLeft, drawnRight), Is.EqualTo((expectedLeft, expectedRight)), style.Name + "'s line lands on draw_text's pen positions");
            }
        }

        /// <summary>The first and last atlas columns of a cell with any pixel over half coverage, or (-1, -1).</summary>
        static (int, int) InkColumns(NativeArray<byte> alpha, int atlasWidth, UnityEngine.TextCore.GlyphRect r)
        {
            int first = -1, last = -1;
            for (int gx = 0; gx < r.width; gx++)
            for (int gy = 0; gy < r.height; gy++)
                if (alpha[(r.y + gy) * atlasWidth + r.x + gx] > 127) { if (first < 0) first = gx; last = gx; break; }
            return (first, last);
        }

        static NativeArray<byte> AtlasAlpha(FontAsset asset) => AsyncGPUReadback.Request(asset.atlasTextures[0], 0).WaitForCompletionData();

        static T Place<T>(T element, float x, float y) where T : VisualElement
        {
            element.style.position = Position.Absolute;
            element.style.left = x;
            element.style.top = y;
            element.style.paddingLeft = element.style.paddingRight = element.style.paddingTop = element.style.paddingBottom = 0;
            element.style.marginLeft = element.style.marginRight = element.style.marginTop = element.style.marginBottom = 0;
            element.style.color = Color.white;
            return element;
        }

        static Color32[] Read(RenderTexture rt)
        {
            var data = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32).WaitForCompletionData();
            var pixels = new Color32[Width * Height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(data[i * 4], data[i * 4 + 1], data[i * 4 + 2], data[i * 4 + 3]);
            return pixels;
        }

        static void SaveScreenshot(Color32[] pixels)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "fonts.png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }

    static class ReadbackExtensions
    {
        public static NativeArray<byte> WaitForCompletionData(this AsyncGPUReadbackRequest request)
        {
            request.WaitForCompletion();
            Assert.That(request.hasError, Is.False, "GPU readback failed");
            return request.GetData<byte>();
        }
    }
}
