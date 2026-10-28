using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlatformServices.EditorSupport
{
    /// <summary>
    /// Persists the Firebase/IAP enable toggles in a version-controlled file and re-applies them to the
    /// scripting defines whenever the file changes (for example after a git pull).
    /// </summary>
    [InitializeOnLoad]
    internal static class PlatformServicesSettingsStore
    {
        private const string FileName = "IxiePlatformServices.json";
        private const string AppliedStampKey = "PlatformServices.SettingsStore.AppliedStamp";

        [Serializable]
        private sealed class SettingsData
        {
            public bool firebaseEnabled;
            public bool iapEnabled;
        }

        static PlatformServicesSettingsStore()
        {
            EditorApplication.delayCall += () => ApplyFromFile(force: true);
            EditorApplication.focusChanged += focused =>
            {
                if (focused)
                    ApplyFromFile(force: false);
            };
        }

        private static string FilePath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "ProjectSettings", FileName);

        /// <summary>Writes the current toggle state to the shared settings file when it differs from what is saved.</summary>
        public static void SaveCurrent()
        {
            SettingsData current = new SettingsData
            {
                firebaseEnabled = PlatformServicesDefines.IsDefined(PlatformServicesDefines.FirebaseEnabled),
                iapEnabled = PlatformServicesDefines.IsDefined(PlatformServicesDefines.IapEnabled)
            };

            string json = JsonUtility.ToJson(current, true);
            if (File.Exists(FilePath) && File.ReadAllText(FilePath) == json)
                return;

            File.WriteAllText(FilePath, json);
            RememberApplied();
        }

        /// <summary>Turns an option off and saves it, for when its SDK or package is no longer in the project.</summary>
        public static void DisableOption(string define, string reason)
        {
            if (!PlatformServicesDefines.IsDefined(define))
                return;

            UnityEngine.Debug.LogWarning("[PlatformServices] Disabled " + define + " because " + reason + ".");
            PlatformServicesDefines.SetDefined(define, false);
            SaveCurrent();
        }

        private static void ApplyFromFile(bool force)
        {
            if (!File.Exists(FilePath))
            {
                SaveCurrent();
                return;
            }

            if (!force && SessionState.GetString(AppliedStampKey, string.Empty) == CurrentStamp())
                return;

            SettingsData saved;
            try
            {
                saved = JsonUtility.FromJson<SettingsData>(File.ReadAllText(FilePath));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PlatformServices] Could not read " + FileName + ": " + exception.Message);
                return;
            }

            if (saved == null)
                return;

            PlatformServicesDefines.SetDefined(PlatformServicesDefines.FirebaseEnabled, saved.firebaseEnabled);
            PlatformServicesDefines.SetDefined(PlatformServicesDefines.IapEnabled, saved.iapEnabled);
            RememberApplied();
        }

        private static void RememberApplied()
        {
            SessionState.SetString(AppliedStampKey, CurrentStamp());
        }

        private static string CurrentStamp()
        {
            return File.Exists(FilePath) ? File.GetLastWriteTimeUtc(FilePath).Ticks.ToString() : string.Empty;
        }
    }
}
