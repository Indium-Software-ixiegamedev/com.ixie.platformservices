using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PlatformServices.EditorSupport
{
    internal static class PlatformServicesSettingsProvider
    {
        private const string IapPackageName = "com.unity.purchasing";
        private const int MinimumIapMajorVersion = 5;

        private static string[] missingFirebaseAssemblies = Array.Empty<string>();
        private static string iapPackageVersion;

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/Ixie Platform Services", SettingsScope.Project)
            {
                label = "Ixie Platform Services",
                keywords = new[] { "ixie", "platform", "services", "firebase", "iap", "purchasing" },
                activateHandler = (_, __) => Refresh(),
                guiHandler = _ => Draw()
            };
        }

        private static void Refresh()
        {
            missingFirebaseAssemblies = FirebaseSdkDetector.FindMissingAssemblies().ToArray();
            iapPackageVersion = FindIapPackageVersion();
        }

        private static void Draw()
        {
            EditorGUILayout.Space();
            DrawFirebase();
            EditorGUILayout.Space();
            DrawIap();
            EditorGUILayout.Space();

            if (GUILayout.Button("Refresh detection", GUILayout.Width(150)))
            {
                Refresh();
                FirebaseSdkDetector.Sync();
            }

            EditorGUILayout.HelpBox("Changing these options updates the scripting define symbols, saves them to ProjectSettings/IxiePlatformServices.json (commit this file to share them with the team), and triggers a script recompile.", MessageType.None);
        }

        private static void DrawFirebase()
        {
            EditorGUILayout.LabelField("Firebase", EditorStyles.boldLabel);

            bool sdkDetected = missingFirebaseAssemblies.Length == 0;
            if (sdkDetected)
            {
                EditorGUILayout.HelpBox("Firebase SDK detected (" + PlatformServicesDefines.FirebaseSdk + " is defined automatically).", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Firebase SDK not found. Missing assemblies: " + string.Join(", ", missingFirebaseAssemblies), MessageType.Warning);
            }

            DrawToggle("Enable Firebase platform services", PlatformServicesDefines.FirebaseEnabled, sdkDetected);
        }

        private static void DrawIap()
        {
            EditorGUILayout.LabelField("In-App Purchasing", EditorStyles.boldLabel);

            bool iapInstalled = iapPackageVersion != null;
            if (!iapInstalled)
                EditorGUILayout.HelpBox("Unity IAP " + MinimumIapMajorVersion + " or later (" + IapPackageName + ") is not installed.", MessageType.Warning);
            else
                EditorGUILayout.HelpBox("Unity IAP " + iapPackageVersion + " detected.", MessageType.Info);

            DrawToggle("Enable IAP platform service", PlatformServicesDefines.IapEnabled, iapInstalled);

            if (PlatformServicesDefines.IsDefined(PlatformServicesDefines.IapEnabled) && !PlatformServicesDefines.IsDefined(PlatformServicesDefines.FirebaseEnabled))
                EditorGUILayout.HelpBox("Firebase is not enabled, so IAP has no default receipt validator. Override PlatformService.CreateReceiptValidator() to supply your own IReceiptValidator.", MessageType.Info);
        }

        private static void DrawToggle(string label, string define, bool canEnable)
        {
            bool current = PlatformServicesDefines.IsDefined(define);
            using (new EditorGUI.DisabledScope(!current && !canEnable))
            {
                EditorGUI.BeginChangeCheck();
                bool next = EditorGUILayout.ToggleLeft(new GUIContent(label, "Defines " + define), current);
                if (EditorGUI.EndChangeCheck())
                {
                    PlatformServicesDefines.SetDefined(define, next);
                    PlatformServicesSettingsStore.SaveCurrent();
                }
            }
        }

        private static string FindIapPackageVersion()
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .FirstOrDefault(info => info.name == IapPackageName);
            if (package == null)
                return null;

            string numeric = package.version.Split('-')[0];
            return Version.TryParse(numeric, out Version version) && version.Major >= MinimumIapMajorVersion ? package.version : null;
        }
    }
}
