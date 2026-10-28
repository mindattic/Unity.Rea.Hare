using System;
using UnityEngine;

namespace Unity.Hare.Demo
{
    /// <summary>
    /// U.HARE-T70: the <c>-demo</c> command-line flag Unity.Chain, Unity.SpaceGod and Unity.GridGame share for a
    /// scripted, capturable run. A normal launch is unaffected: with no <c>-demo</c> this does nothing and the game
    /// boots as a player gets it. With it, a <see cref="ScriptedRun"/> plays every room and mode once and (under
    /// <c>-capture screen</c>, parsed by MindAttic.Capture) screenshots each. tools/capture.ps1 launches it.
    /// </summary>
    public static class DemoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (!HasCommandLineFlag("-demo")) return;
            if (UnityEngine.Object.FindAnyObjectByType<ScriptedRun>() != null) return;
            var go = new GameObject("ScriptedRun");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<ScriptedRun>();
        }

        public static bool HasCommandLineFlag(string flag)
        {
            foreach (var arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
