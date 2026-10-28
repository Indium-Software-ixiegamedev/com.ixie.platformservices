using PlatformServices.AppCheck;
using PlatformServices.Core;
using PlatformServices.Iap;
using PlatformServices.Integrity;
using UnityEngine;

namespace PlatformServices.Production
{
    /// <summary>Composes production platform features and their initialization dependencies.</summary>
    public static class ProductionPlatformServiceFactory
    {
        /// <summary>
        /// Creates the production platform composition. Integrity is registered first, then every installer
        /// contributed by an enabled optional assembly (Firebase, Unity IAP) in installer order.
        /// IAP is registered only when the IAP service is enabled in Project Settings, a catalog is supplied, and a receipt
        /// validator is available (<paramref name="receiptValidator"/>, or the Firebase default when Firebase is enabled).
        /// </summary>
        public static PlatformServiceCore CreateProduction(string googleWebClientId = null, bool enableIntegrityCheck = true,
            IntegrityConfiguration integrityConfiguration = null,
            AppCheckConfiguration appCheckConfiguration = null,
            IapProductCatalog iapProductCatalog = null,
            IReceiptValidator receiptValidator = null)
        {
            PlatformServiceCore platform = new PlatformServiceCore(new UnityLogService());

            PlatformServiceOptions options = new PlatformServiceOptions
            {
                GoogleWebClientId = googleWebClientId,
                EnableIntegrityCheck = enableIntegrityCheck,
                IntegrityConfiguration = integrityConfiguration ?? new IntegrityConfiguration
                {
                    ExpectedApplicationIdentifier = Application.identifier
                },
                AppCheckConfiguration = appCheckConfiguration ?? new AppCheckConfiguration(),
                IapProductCatalog = iapProductCatalog,
                ReceiptValidator = receiptValidator
            };

            if (enableIntegrityCheck)
                platform.RegisterFeature(new IntegrityService(options.IntegrityConfiguration));

            foreach (IPlatformFeatureInstaller installer in PlatformFeatureInstallerRegistry.GetInstallers())
                installer.Install(platform, options);

            return platform;
        }
    }
}
