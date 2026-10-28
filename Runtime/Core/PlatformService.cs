using System;
using System.Threading;
using System.Threading.Tasks;
using PlatformServices.Analytics;
using PlatformServices.AppCheck;
using PlatformServices.Auth;
using PlatformServices.Production;
using PlatformServices.CloudSave;
using PlatformServices.Consent;
#if PLATFORMSERVICES_FIREBASE && PLATFORMSERVICES_FIREBASE_ENABLED
using PlatformServices.Firebase;
#endif
using PlatformServices.Integrity;
using PlatformServices.Iap;
using PlatformServices.RemoteConfig;
using UnityEngine;

namespace PlatformServices.Core
{
    public class PlatformService : MonoBehaviour
    {
        [Header("Authentication")]
        [SerializeField] private string googleWebClientId;

        [Header("Integrity")]
        [Tooltip("Enables both the local integrity gate and Firebase App Check. Disable only for projects/builds that intentionally do not use these protections.")]
        [SerializeField] private bool enableIntegrityCheck = true;
        [Tooltip("Expected application/package identifier used to reject builds with a different bundle ID. Find it in Project Settings > Player > Identification > Package Name for each target platform.")]
        [SerializeField] private string expectedApplicationIdentifier = "com.letsgamenow.bharatdarshan";
        [Tooltip("Expected Android signing certificate SHA-256 fingerprint. Get it from the release/upload keystore certificate with keytool (keytool -list -v -keystore <keystore> -alias <alias>), then enter the SHA-256 fingerprint; colons and spaces are accepted.")]
        [SerializeField] private string expectedAndroidSha256Certificate;
        [Tooltip("Expected Android installer package name. For Google Play distribution this is usually com.android.vending; confirm the installer used by your target store/distribution channel.")]
        [SerializeField] private string expectedAndroidInstallerPackage;
        [Tooltip("When enabled, startup fails unless the installed Android app's signing certificate matches the SHA-256 fingerprint above. Requires the fingerprint to be configured.")]
        [SerializeField] private bool requireAndroidSigningCertificate;
        [Tooltip("When enabled, startup fails unless Android reports the installer package above. Sideloaded installs commonly have no installer package.")]
        [SerializeField] private bool requireAndroidInstallerPackage;
        [Tooltip("Allows builds made with Unity's Development Build option. Leave disabled for production distribution.")]
        [SerializeField] private bool allowDevelopmentBuild;

        [Header("In-App Purchasing")]
        [Tooltip("Enables the production Unity IAP flow. Requires an IAP product catalog, Firebase App Check, signed-in players, and deployed server-side purchase verification functions.")]
        [SerializeField] private bool enableIap;
        [Tooltip("Catalog of canonical product IDs, platform store SKUs, and backend-owned entitlement IDs. Game-facing IAPProductDataSO IDs must match each catalog canonical ProductId.")]
        [SerializeField] private IapProductCatalog iapProductCatalog;

        protected PlatformServiceCore platformServiceCore;
        protected CancellationTokenSource cancellationTokenSource;
        protected Task<bool> initializationTask;

        public IPlatformService Core => platformServiceCore;
        public IConsentService Consent => platformServiceCore?.GetFeature<IConsentService>();
    #if PLATFORMSERVICES_FIREBASE && PLATFORMSERVICES_FIREBASE_ENABLED
        public IFirebaseService Firebase => platformServiceCore?.GetFeature<IFirebaseService>();
    #endif
        public IAppCheckService AppCheck => platformServiceCore?.GetFeature<IAppCheckService>();
        public IAuthService Auth => platformServiceCore?.GetFeature<IAuthService>();
        public IIapService Iap => platformServiceCore?.GetFeature<IIapService>();
        public IRemoteConfigService RemoteConfig => platformServiceCore?.GetFeature<IRemoteConfigService>();
        public ICloudSaveService CloudSave => platformServiceCore?.GetFeature<ICloudSaveService>();
        public IAnalyticsService Analytics => platformServiceCore?.GetFeature<IAnalyticsService>();
        public IIntegrityService Integrity => platformServiceCore?.GetFeature<IIntegrityService>();
        public Task<bool> InitializationTask => initializationTask ?? Task.FromResult(false);
        public bool IsInitialized => platformServiceCore != null && platformServiceCore.IsInitialized;

        public virtual void Initialize()
        {
            if (initializationTask != null)
                return;

            cancellationTokenSource = new CancellationTokenSource();
            initializationTask = InitializePlatformServicesAsync(cancellationTokenSource.Token);
        }

        protected virtual async Task<bool> InitializePlatformServicesAsync(CancellationToken cancellationToken)
        {
            platformServiceCore = ProductionPlatformServiceFactory.CreateProduction(
                googleWebClientId,
                enableIntegrityCheck,
                new IntegrityConfiguration
                {
                    ExpectedApplicationIdentifier = expectedApplicationIdentifier,
                    ExpectedAndroidSha256Certificate = expectedAndroidSha256Certificate,
                    ExpectedAndroidInstallerPackage = expectedAndroidInstallerPackage,
                    RequireAndroidSigningCertificate = requireAndroidSigningCertificate,
                    RequireAndroidInstallerPackage = requireAndroidInstallerPackage,
                    AllowDevelopmentBuild = allowDevelopmentBuild
                },
                new AppCheckConfiguration(),
                iapProductCatalog);

            bool initialized = await platformServiceCore.InitializeAsync(cancellationToken);
            if (!initialized)
                Debug.LogError("[PlatformService] PlatformServices core initialization failed.");

            return initialized;
        }

        protected virtual async void OnDestroy()
        {
            cancellationTokenSource?.Cancel();
            if (platformServiceCore != null)
                await platformServiceCore.ShutdownAsync();
            cancellationTokenSource?.Dispose();
        }
    }
}
