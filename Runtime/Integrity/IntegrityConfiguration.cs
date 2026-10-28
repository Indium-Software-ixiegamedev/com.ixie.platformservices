using System;

namespace PlatformServices.Integrity
{
    public sealed class IntegrityConfiguration
    {
        public string ExpectedApplicationIdentifier { get; set; }
        public string ExpectedAndroidSha256Certificate { get; set; }
        public string ExpectedAndroidInstallerPackage { get; set; }
        public bool RequireAndroidInstallerPackage { get; set; }
        public bool AllowEditor { get; set; } = true;
        public bool AllowDevelopmentBuild { get; set; }
        public bool RequireGenuineApplication { get; set; } = true;
        public bool RequireAndroidSigningCertificate { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ExpectedApplicationIdentifier))
                throw new ArgumentException("An expected application identifier is required.", nameof(ExpectedApplicationIdentifier));
            if (RequireAndroidSigningCertificate && string.IsNullOrWhiteSpace(ExpectedAndroidSha256Certificate))
                throw new ArgumentException("An Android SHA-256 certificate is required when certificate validation is enabled.", nameof(ExpectedAndroidSha256Certificate));
            if (RequireAndroidInstallerPackage && string.IsNullOrWhiteSpace(ExpectedAndroidInstallerPackage))
                throw new ArgumentException("An Android installer package is required when installer validation is enabled.", nameof(ExpectedAndroidInstallerPackage));
        }
    }
}
