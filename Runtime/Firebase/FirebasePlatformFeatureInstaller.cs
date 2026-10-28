using PlatformServices.AppCheck;
using PlatformServices.Core;
using PlatformServices.Iap;
using UnityEngine;

namespace PlatformServices.Firebase
{
    /// <summary>Registers the Firebase-backed features; discovered by the core through its installer registry.</summary>
    internal sealed class FirebasePlatformFeatureInstaller : IPlatformFeatureInstaller
    {
        private const string DefaultAndroidAppCheckProvider = "Firebase.AppCheck.PlayIntegrityProviderFactory, Firebase.AppCheck";
        private const string DefaultIosAppCheckProvider = "Firebase.AppCheck.AppAttestProviderFactory, Firebase.AppCheck";

        public int Order => 100;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            PlatformFeatureInstallerRegistry.Register(new FirebasePlatformFeatureInstaller());
        }

        public void Install(IPlatformService platform, PlatformServiceOptions options)
        {
            IFirebaseService firebase = new FirebaseService();
            platform.RegisterFeature(firebase);

            if (options.EnableIntegrityCheck)
            {
                platform.RegisterFeature(new FirebaseAppCheckService(
                    firebase,
                    CreateAppCheckProviderConfigurator(options.AppCheckConfiguration),
                    options.AppCheckConfiguration));
            }

            FirebaseAuthService auth = new FirebaseAuthService(firebase,
                new GoogleAccountAuthProviderAdapter(options.GoogleWebClientId),
                //new GooglePlayGamesAuthProviderAdapter(),
                new AppleAuthProviderAdapter());
            platform.RegisterFeature(auth);

            platform.RegisterFeature(new FirestoreCloudSaveService(firebase));

            if (options.ReceiptValidator == null)
                options.ReceiptValidator = new FirebaseFunctionReceiptValidator(firebase);
        }

        private static IAppCheckProviderConfigurator CreateAppCheckProviderConfigurator(AppCheckConfiguration configuration)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new ReflectionFirebaseAppCheckProviderConfigurator(
                configuration.AndroidProviderFactoryTypeName ?? DefaultAndroidAppCheckProvider);
#elif UNITY_IOS && !UNITY_EDITOR
            return new ReflectionFirebaseAppCheckProviderConfigurator(
                configuration.IosProviderFactoryTypeName ?? DefaultIosAppCheckProvider);
#else
            return new UnavailableAppCheckProviderConfigurator();
#endif
        }
    }
}
