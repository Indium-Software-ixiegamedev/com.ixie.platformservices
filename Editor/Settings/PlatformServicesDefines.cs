using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

namespace PlatformServices.EditorSupport
{
    internal static class PlatformServicesDefines
    {
        /// <summary>Set automatically when the Firebase SDK assemblies are present in the project.</summary>
        public const string FirebaseSdk = "PLATFORMSERVICES_FIREBASE";
        /// <summary>Set manually from Project Settings to compile the Firebase platform services.</summary>
        public const string FirebaseEnabled = "PLATFORMSERVICES_FIREBASE_ENABLED";
        /// <summary>Set manually from Project Settings to compile the Unity IAP platform service.</summary>
        public const string IapEnabled = "PLATFORMSERVICES_IAP_ENABLED";

        private static readonly NamedBuildTarget[] Targets =
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Server,
            NamedBuildTarget.iOS,
            NamedBuildTarget.Android,
            NamedBuildTarget.WebGL
        };

        public static bool IsDefined(string define)
        {
            foreach (NamedBuildTarget target in Targets)
            {
                if (Read(target).Contains(define))
                    return true;
            }

            return false;
        }

        public static void SetDefined(string define, bool enabled)
        {
            foreach (NamedBuildTarget target in Targets)
            {
                List<string> defines = Read(target);
                if (defines.Contains(define) == enabled)
                    continue;

                if (enabled)
                    defines.Add(define);
                else
                    defines.RemoveAll(existing => existing == define);

                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            }
        }

        private static List<string> Read(NamedBuildTarget target)
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbols(target);
            return new List<string>(symbols.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
