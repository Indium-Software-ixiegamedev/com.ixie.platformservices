using System.Threading.Tasks;
using PlatformServices.Auth;
using PlatformServices.Core;
using PlatformServices.Production;
using UnityEngine;
#if PLATFORMSERVICES_FIREBASE && PLATFORMSERVICES_FIREBASE_ENABLED
using PlatformServices.Firebase;
#endif

namespace PlatformServices.Samples
{
    /// <summary>
    /// Shows how to compose the platform now that Firebase features are contributed by an installer
    /// instead of being hard-wired into the factory.
    /// </summary>
    public sealed class PlatformServicesBootstrap : MonoBehaviour
    {
        [SerializeField] private string googleWebClientId;
        [SerializeField] private bool composeManually;

        private PlatformServiceCore platform;

        private async void Start()
        {
            // Firebase (when enabled in Project Settings > Ixie Platform Services) is registered by its installer here.
            platform = composeManually ? CreateManually() : ProductionPlatformServiceFactory.CreateProduction(googleWebClientId);

            if (!await platform.InitializeAsync())
            {
                Debug.LogError("Platform services failed to initialize.");
                return;
            }

            await UseServicesAsync();
        }

        private async Task UseServicesAsync()
        {
            // Features are looked up by interface; GetFeature returns null when their assembly is not enabled.
            IAuthService auth = platform.GetFeature<IAuthService>();
            if (auth != null && !auth.IsAuthenticated)
                await auth.SignInAsync(AuthProviderType.Guest);

#if PLATFORMSERVICES_FIREBASE && PLATFORMSERVICES_FIREBASE_ENABLED
            IFirebaseService firebase = platform.GetFeature<IFirebaseService>();
            Debug.Log("Firebase available: " + (firebase != null && firebase.IsAvailable));
#endif
        }

        private PlatformServiceCore CreateManually()
        {
            PlatformServiceCore manual = new PlatformServiceCore(new UnityLogService());

#if PLATFORMSERVICES_FIREBASE && PLATFORMSERVICES_FIREBASE_ENABLED
            // Registration order is initialization order: Firebase first, then features that depend on it.
            IFirebaseService firebase = new FirebaseService();
            manual.RegisterFeature(firebase);
            manual.RegisterFeature(new FirebaseAuthService(
                firebase,
                new GoogleAccountAuthProviderAdapter(googleWebClientId),
                new AppleAuthProviderAdapter()));
            manual.RegisterFeature(new FirestoreCloudSaveService(firebase));
#endif

            return manual;
        }

        private async void OnDestroy()
        {
            if (platform != null)
                await platform.ShutdownAsync();
        }
    }
}
