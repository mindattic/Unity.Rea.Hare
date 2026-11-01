using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.Rea.Hare.EditorTools.Rendering
{
    /// <summary>
    /// The only code in Unity.Rea.Hare that references <c>UnityEngine.Rendering.Universal</c> (U.HARE-T44), kept in
    /// its own assembly (<c>Unity.Rea.Hare.Editor.Rendering.asmdef</c>) so the restriction is enforced by the
    /// compiler, not just by convention: <c>Unity.Rea.Hare.Editor</c> and <c>Unity.Rea.Hare.Runtime</c> no longer
    /// reference the URP packages at all, so a stray <c>using UnityEngine.Rendering.Universal;</c> anywhere
    /// else in the project fails to compile. Everything outside this file reaches rendering through the
    /// pipeline-agnostic pieces in <c>Unity.Rea.Hare.Rendering</c> (<see cref="Unity.Rea.Hare.Rendering.CameraRig"/>,
    /// <see cref="Unity.Rea.Hare.Rendering.LetterboxCamera"/>, <see cref="Unity.Rea.Hare.Rendering.HareSortingLayers"/>,
    /// <see cref="Unity.Rea.Hare.Rendering.ScreenOverlayLayer"/>) or through this seam's plain-typed methods, so a
    /// future pipeline change touches only this one file and its asmdef.
    /// </summary>
    public static class UrpSeam
    {
        /// <summary>
        /// A URP asset whose only renderer is the 2D renderer, saved at <paramref name="pipelinePath"/> (its
        /// <see cref="Renderer2DData"/> at <paramref name="rendererPath"/>, created only if missing, same as
        /// before this ticket) and made the default render pipeline for every quality level.
        /// </summary>
        public static void EnsureUrp2DPipeline(string rendererPath, string pipelinePath)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = null; // every level falls back to the default above
            }
        }

        /// <summary>
        /// Adds URP's per-camera data to <paramref name="cameraGo"/>: a camera added purely by script (unlike
        /// one placed by hand in the Editor) gets none of its own, and without it URP may not render it
        /// correctly.
        /// </summary>
        public static void AddCameraData(GameObject cameraGo)
        {
            if (cameraGo.GetComponent<UniversalAdditionalCameraData>() == null)
                cameraGo.AddComponent<UniversalAdditionalCameraData>();
        }

        /// <summary>
        /// Adds a global 2D light to <paramref name="host"/> at <paramref name="intensity"/>: the original has
        /// no equivalent (GameMaker draws unlit), but URP's 2D renderer draws every sprite black without at
        /// least one light reaching it.
        /// </summary>
        public static void AddGlobalLight2D(GameObject host, float intensity)
        {
            var light = host.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = intensity;
        }
    }
}
