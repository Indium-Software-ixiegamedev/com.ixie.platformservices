using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace PlatformServices.EditorSupport
{
    /// <summary>Disables the IAP option in Project Settings when the Unity IAP package is removed or too old.</summary>
    internal static class IapPackageDetector
    {
        public const string PackageName = "com.unity.purchasing";
        public const int MinimumMajorVersion = 5;

        private static ListRequest listRequest;

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            Events.registeredPackages += _ => Sync();
            EditorApplication.delayCall += Sync;
        }

        /// <summary>Returns the installed Unity IAP version when it meets the minimum major version; otherwise null.</summary>
        public static string FindVersion()
        {
            return FindVersion(PackageInfo.GetAllRegisteredPackages());
        }

        public static void Sync()
        {
            if (listRequest != null || !PlatformServicesDefines.IsDefined(PlatformServicesDefines.IapEnabled))
                return;

            listRequest = Client.List(true);
            EditorApplication.update += PollList;
        }

        private static void PollList()
        {
            if (!listRequest.IsCompleted)
                return;

            EditorApplication.update -= PollList;
            ListRequest completed = listRequest;
            listRequest = null;

            if (completed.Status == StatusCode.Success && FindVersion(completed.Result) == null)
                PlatformServicesSettingsStore.DisableOption(PlatformServicesDefines.IapEnabled, "the Unity IAP package is not installed");
        }

        private static string FindVersion(IEnumerable<PackageInfo> packages)
        {
            foreach (PackageInfo package in packages)
            {
                if (package.name != PackageName)
                    continue;

                string numeric = package.version.Split('-')[0];
                return Version.TryParse(numeric, out Version version) && version.Major >= MinimumMajorVersion ? package.version : null;
            }

            return null;
        }
    }
}
