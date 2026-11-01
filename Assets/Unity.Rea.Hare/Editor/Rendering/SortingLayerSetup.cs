using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Unity.Rea.Hare.EditorTools.Rendering
{
    /// <summary>
    /// Writes the project's sorting layers in code (U.HARE-T44), the Tags and Layers window's equivalent of
    /// <c>ProjectSetup</c>'s other project-settings edits: puts <see cref="Unity.Rea.Hare.Rendering.HareSortingLayers.Order"/>'s
    /// layers first, in that back-to-front order, creating any of them that is missing and leaving an existing
    /// one's id alone (a sorting layer is referenced by id, not name, so changing a kept layer's id would
    /// silently move every <c>SpriteRenderer</c> already assigned to it). Any other sorting layer the project
    /// already has — one this file doesn't know about, added by hand or by another tool — is kept, appended
    /// after the ordered ones in its original relative order with its id and lock state untouched; this never
    /// deletes a layer. Idempotent: run again on a project already in the right order and nothing changes. Not
    /// URP-specific, but kept beside <see cref="UrpSeam"/> as the other half of the presentation seam's
    /// project-settings setup.
    /// </summary>
    public static class SortingLayerSetup
    {
        public static void EnsureSortingLayers(params string[] orderedNames)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");

            bool alreadyInOrder = layers.arraySize >= orderedNames.Length;
            for (int i = 0; alreadyInOrder && i < orderedNames.Length; i++)
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue != orderedNames[i])
                    alreadyInOrder = false;
            if (alreadyInOrder) return;

            var existing = new List<(string name, int id, bool locked)>();
            int maxId = -1;
            for (int i = 0; i < layers.arraySize; i++)
            {
                var entry = layers.GetArrayElementAtIndex(i);
                string name = entry.FindPropertyRelative("name").stringValue;
                int id = entry.FindPropertyRelative("uniqueID").intValue;
                bool locked = entry.FindPropertyRelative("locked").boolValue;
                existing.Add((name, id, locked));
                if (id > maxId) maxId = id;
            }

            var orderedSet = new HashSet<string>(orderedNames);
            var unknown = existing.Where(e => !orderedSet.Contains(e.name)).ToList();
            var existingIds = existing.ToDictionary(e => e.name, e => e.id);

            int nextId = maxId + 1;
            layers.arraySize = orderedNames.Length + unknown.Count;

            for (int i = 0; i < orderedNames.Length; i++)
            {
                var entry = layers.GetArrayElementAtIndex(i);
                string name = orderedNames[i];
                int id = existingIds.TryGetValue(name, out int found) ? found : nextId++;
                entry.FindPropertyRelative("name").stringValue = name;
                entry.FindPropertyRelative("uniqueID").intValue = id;
                entry.FindPropertyRelative("locked").boolValue = false;
            }

            for (int i = 0; i < unknown.Count; i++)
            {
                var entry = layers.GetArrayElementAtIndex(orderedNames.Length + i);
                entry.FindPropertyRelative("name").stringValue = unknown[i].name;
                entry.FindPropertyRelative("uniqueID").intValue = unknown[i].id;
                entry.FindPropertyRelative("locked").boolValue = unknown[i].locked;
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
