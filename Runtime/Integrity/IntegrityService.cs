using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using PlatformServices.Core;

namespace PlatformServices.Integrity
{
    /// <summary>
    /// Integrity service that validates the application's integrity based on the provided configuration.
    /// This is used to ensure that the application is running in a trusted environment.
    /// This is the first line of defense against tampering and ensures the integrity of the application.
    /// </summary>
    public sealed class IntegrityService : IIntegrityService
    {
        private readonly IntegrityConfiguration configuration;

        public IntegrityService(IntegrityConfiguration configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public bool IsInitialized { get; private set; }
        public bool IsTrusted { get; private set; }
        public string FailureReason { get; private set; }

        public Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsInitialized = false;
            IsTrusted = false;
            FailureReason = null;

            try
            {
                configuration.Validate();
            }
            catch (ArgumentException exception)
            {
                return Fail(exception.Message);
            }

#if UNITY_EDITOR
            if (!configuration.AllowEditor)
                return Fail("Editor execution is not allowed.");
#else
            if (configuration.RequireGenuineApplication && !Application.genuine)
                return Fail("Application.genuine reported an untrusted application.");
#endif

            if (!string.Equals(Application.identifier, configuration.ExpectedApplicationIdentifier, StringComparison.Ordinal))
                return Fail("Application identifier does not match the configured production identifier.");

#if DEVELOPMENT_BUILD
            if (!configuration.AllowDevelopmentBuild)
                return Fail("Development builds are not allowed.");
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
            string certificateFailure = ValidateAndroidCertificate();
            if (certificateFailure != null)
                return Fail(certificateFailure);

            string installerFailure = ValidateAndroidInstaller();
            if (installerFailure != null)
                return Fail(installerFailure);
#endif

            IsInitialized = true;
            IsTrusted = true;
            return Task.FromResult(true);
        }

        public void Dispose()
        {
            IsInitialized = false;
            IsTrusted = false;
        }

        private Task<bool> Fail(string reason)
        {
            FailureReason = reason;
            return Task.FromResult(false);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private string ValidateAndroidCertificate()
        {
            if (!configuration.RequireAndroidSigningCertificate)
                return null;

            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
                using (AndroidJavaObject packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", Application.identifier, 64))
                {
                    AndroidJavaObject[] signatures = packageInfo.Get<AndroidJavaObject[]>("signatures");
                    if (signatures == null || signatures.Length == 0)
                        return "Android signing certificate was not available.";

                    using (AndroidJavaObject signature = signatures[0])
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        byte[] digest = sha256.ComputeHash(signature.Call<byte[]>("toByteArray"));
                        string actual = string.Concat(digest.Select(value => value.ToString("X2")));
                        string expected = NormalizeCertificate(configuration.ExpectedAndroidSha256Certificate);
                        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                            return "Android signing certificate does not match the configured certificate.";
                    }
                }
            }
            catch (Exception exception)
            {
                return "Android signing certificate validation failed: " + exception.GetType().Name;
            }

            return null;
        }

        private string ValidateAndroidInstaller()
        {
            if (!configuration.RequireAndroidInstallerPackage)
                return null;

            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    string installer = packageManager.Call<string>("getInstallerPackageName", Application.identifier);
                    if (!string.Equals(installer, configuration.ExpectedAndroidInstallerPackage, StringComparison.Ordinal))
                        return "Android installer package does not match the configured installer.";
                }
            }
            catch (Exception exception)
            {
                return "Android installer validation failed: " + exception.GetType().Name;
            }

            return null;
        }

        private static string NormalizeCertificate(string certificate)
        {
            return new string(certificate.Where(char.IsLetterOrDigit).ToArray());
        }
#endif
    }
}
