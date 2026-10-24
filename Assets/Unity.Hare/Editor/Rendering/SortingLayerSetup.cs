using System.Collections.Generic;
using UnityEditor;

namespace Unity.Hare.EditorTools.Rendering
{
    /// <summary>
    /// Writes the project's sorting layers in code (U.HARE-T44), the Tags and Layers window's equivalent of
    /// <c>ProjectSetup</c>'s other project-settings edits: puts them in the back-to-front order
    /// <see cref="Unity.Hare.Rendering.HareSortingLayers.Order"/> calls for, creating any layer that is missing
    /// and leaving an existing one's id alone (a sorting layer is referenced by id, not name, so changing a
    /// kept layer's id would silently move every <c>SpriteRenderer</c> already assigned to it). Idempotent: run
    /// again on a project already in the right order and nothing changes. Not URP-specific, but kept beside
    /// <see cref="UrpSeam"/> as the other half of the presentation seam's project-settings setup.
    /// </summary>
    public static class SortingLayerSetup
    {
        public static void EnsureSortingLayers(params string[] orderedNames)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");

            bool alreadyInOrder = layers.arraySize == orderedNames.Length;
            for (int i = 0; alreadyInOrder && i < orderedNames.Length; i++)
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue != orderedNames[i])
                    alreadyInOrder = false;
            if (alreadyInOrder) return;

            var existingIds = new Dictionary<string, int>();
            int maxId = -1;
            for (int i = 0; i < layers.arraySize; i++)
            {
                var entry = layers.GetArrayElementAtIndex(i);
                string name = entry.FindPropertyRelative("name").stringValue;
                int id = entry.FindPropertyRelative("uniqueID").intValue;
                existingIds[name] = id;
                if (id > maxId) maxId = id;
            }

            int nextId = maxId + 1;
            layers.arraySize = orderedNames.Length;
            for (int i = 0; i < orderedNames.Length; i++)
            {
                var entry = layers.GetArrayElementAtIndex(i);
                string name = orderedNames[i];
                int id = existingIds.TryGetValue(name, out int found) ? found : nextId++;
                entry.FindPropertyRelative("name").stringValue = name;
                entry.FindPropertyRelative("uniqueID").intValue = id;
                entry.FindPropertyRelative("locked").boolValue = false;
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
