using System;
using System.Collections.Generic;
using UnityEditor;

namespace PlatformServices.EditorSupport
{
    /// <summary>Keeps PLATFORMSERVICES_FIREBASE in sync with whether the Firebase .unitypackage (or UPM) DLLs are in the project.</summary>
    internal sealed class FirebaseSdkDetector : AssetPostprocessor
    {
        private static readonly string[] RequiredAssemblies =
        {
            "Firebase.App",
            "Firebase.Auth",
            "Firebase.Firestore",
            "Firebase.Functions"
        };

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            EditorApplication.delayCall += Sync;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (TouchesFirebaseDll(imported) || TouchesFirebaseDll(deleted) || TouchesFirebaseDll(moved) || TouchesFirebaseDll(movedFrom))
                EditorApplication.delayCall += Sync;
        }

        /// <summary>Returns the required Firebase assemblies that are not in the project.</summary>
        public static IReadOnlyList<string> FindMissingAssemblies()
        {
            List<string> missing = new List<string>();
            foreach (string assemblyName in RequiredAssemblies)
            {
                if (!ContainsDll(assemblyName))
                    missing.Add(assemblyName);
            }

            return missing;
        }

        public static void Sync()
        {
            IReadOnlyList<string> missing = FindMissingAssemblies();
            PlatformServicesDefines.SetDefined(PlatformServicesDefines.FirebaseSdk, missing.Count == 0);

            if (missing.Count > 0)
                PlatformServicesSettingsStore.DisableOption(PlatformServicesDefines.FirebaseEnabled, "the Firebase SDK is missing (" + string.Join(", ", missing) + ")");
        }

        private static bool ContainsDll(string assemblyName)
        {
            string suffix = "/" + assemblyName + ".dll";
            foreach (string guid in AssetDatabase.FindAssets(assemblyName))
            {
                if (AssetDatabase.GUIDToAssetPath(guid).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool TouchesFirebaseDll(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && path.IndexOf("Firebase", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
