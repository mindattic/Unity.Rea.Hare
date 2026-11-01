using NUnit.Framework;
using UnityEngine;
using Unity.Rea.Hare.Rendering;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>U.HARE-T44: the 2D camera rig's hierarchy and per-camera setup, independent of any render pipeline.</summary>
    public class CameraRigTests
    {
        GameObject rig;

        [TearDown]
        public void TearDown()
        {
            if (rig != null) Object.DestroyImmediate(rig);
        }

        CameraRig.Cameras Build()
        {
            var cameras = CameraRig.Build(new Vector3(0f, 0f, -10f), 5.4f, Color.magenta);
            rig = cameras.Main.transform.parent.gameObject;
            return cameras;
        }

        [Test]
        public void MainCameraIsTaggedAndPositioned()
        {
            var cameras = Build();
            Assert.AreEqual("MainCamera", cameras.Main.gameObject.tag);
            Assert.AreEqual(new Vector3(0f, 0f, -10f), cameras.Main.transform.position);
            Assert.AreEqual(5.4f, cameras.Main.orthographicSize);
            Assert.IsTrue(cameras.Main.orthographic);
            Assert.AreEqual(Color.magenta, cameras.Main.backgroundColor);
        }

        [Test]
        public void MainCameraCarriesTheLetterboxAndAudioListenerAndOverlay()
        {
            var cameras = Build();
            Assert.IsNotNull(cameras.Main.GetComponent<LetterboxCamera>());
            Assert.IsNotNull(cameras.Main.GetComponent<AudioListener>());
            Assert.IsNotNull(cameras.Main.GetComponentInChildren<ScreenOverlayLayer>());
        }

        [Test]
        public void BarsCameraClearsBlackBehindEverythingAndRendersNothing()
        {
            var cameras = Build();
            Assert.AreEqual(0, cameras.Bars.cullingMask);
            Assert.AreEqual(CameraClearFlags.SolidColor, cameras.Bars.clearFlags);
            Assert.AreEqual(Color.black, cameras.Bars.backgroundColor);
            Assert.Less(cameras.Bars.depth, cameras.Main.depth);
            Assert.IsNull(cameras.Bars.GetComponent<AudioListener>());
        }

        [Test]
        public void BothCamerasShareOneRigParent()
        {
            var cameras = Build();
            Assert.AreSame(cameras.Main.transform.parent, cameras.Bars.transform.parent);
            Assert.AreEqual("CameraRig", cameras.Main.transform.parent.name);
        }
    }
}
