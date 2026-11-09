using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PhysicalAssembly.Editor
{
    // Capture before and after visual authoring. Deliberately includes inactive levels.
    public static class CreekCampaignValidation
    {
        public static string PhysicsSnapshot(AuthoredSandboxLayout layout)
        {
            if (!layout || !layout.Level1 || !layout.Level2)
                throw new InvalidOperationException("Both authored campaign levels are required.");
            return string.Join("\n", new Component[] { layout.Level1, layout.Level2 }
                .SelectMany(root => root.GetComponentsInChildren<Collider>(true))
                .OrderBy(c => GlobalObjectId.GetGlobalObjectIdSlow(c).ToString())
                .Select(c => GlobalObjectId.GetGlobalObjectIdSlow(c) + "|" +
                    EditorJsonUtility.ToJson(c) + "|" +
                    string.Join(";", c.GetComponentsInParent<Transform>(true)
                        .Select(t => t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")))));
        }

        public static void AssertUnchanged(AuthoredSandboxLayout layout, string before)
        {
            if (PhysicsSnapshot(layout) != before)
                throw new InvalidOperationException("Environment authoring changed a collider or its transform hierarchy.");
        }
    }
}
