using UnityEditor;
using UnityEngine;

namespace PlatformServices.EditorSupport
{
    internal static class PlatformServicesSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/Ixie Platform Services", SettingsScope.Project)
            {
                label = "Ixie Platform Services",
                keywords = new[] { "ixie", "platform", "services", "firebase", "iap", "purchasing" },
                guiHandler = _ => Draw()
            };
        }

        private static void Draw()
        {
            EditorGUILayout.Space();
            DrawFirebase();
            EditorGUILayout.Space();
            DrawIap();
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("Changing these options updates the scripting define symbols, saves them to ProjectSettings/IxiePlatformServices.json (commit this file to share them with the team), and triggers a script recompile.", MessageType.None);
        }

        private static void DrawFirebase()
        {
            EditorGUILayout.LabelField("Firebase", EditorStyles.boldLabel);
            DrawToggle("Enable Firebase platform services", PlatformServicesDefines.FirebaseEnabled);
        }

        private static void DrawIap()
        {
            EditorGUILayout.LabelField("In-App Purchasing", EditorStyles.boldLabel);

            DrawToggle("Enable IAP platform service", PlatformServicesDefines.IapEnabled);

            if (PlatformServicesDefines.IsDefined(PlatformServicesDefines.IapEnabled) && !PlatformServicesDefines.IsDefined(PlatformServicesDefines.FirebaseEnabled))
                EditorGUILayout.HelpBox("Firebase is not enabled, so IAP has no default receipt validator. Override PlatformService.CreateReceiptValidator() to supply your own IReceiptValidator.", MessageType.Info);
        }

        private static void DrawToggle(string label, string define)
        {
            bool current = PlatformServicesDefines.IsDefined(define);
            EditorGUI.BeginChangeCheck();
            bool next = EditorGUILayout.ToggleLeft(new GUIContent(label, "Defines " + define), current);
            if (EditorGUI.EndChangeCheck())
            {
                PlatformServicesDefines.SetDefined(define, next);
                PlatformServicesSettingsStore.SaveCurrent();
            }
        }
    }
}
