#if PLATFORMSERVICES_FIREBASE && PLATFORMSERVICES_FIREBASE_ENABLED
using System.Threading;
using System.Threading.Tasks;
using PlatformServices.Core;
using PlatformServices.Firebase;
using UnityEngine;

namespace PlatformServices.Samples
{
    /// <summary>A project-specific feature that depends on the Firebase service.</summary>
    public sealed class ExampleFirebaseFeature : IPlatformFeature
    {
        private readonly IFirebaseService firebase;

        public ExampleFirebaseFeature(IFirebaseService firebase)
        {
            this.firebase = firebase;
        }

        public bool IsInitialized { get; private set; }

        public Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            Debug.Log("ExampleFirebaseFeature initialized. Firebase available: " + firebase.IsAvailable);
            IsInitialized = true;
            return Task.FromResult(true);
        }

        public void Dispose()
        {
            IsInitialized = false;
        }
    }

    /// <summary>
    /// Adds a custom feature to the production composition. Order 150 runs after the built-in Firebase
    /// installer (100) and before IAP (200), so Firebase is already registered when this installs.
    /// </summary>
    public sealed class ExampleFirebaseFeatureInstaller : IPlatformFeatureInstaller
    {
        public int Order => 150;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            PlatformFeatureInstallerRegistry.Register(new ExampleFirebaseFeatureInstaller());
        }

        public void Install(IPlatformService platform, PlatformServiceOptions options)
        {
            IFirebaseService firebase = platform.GetFeature<IFirebaseService>();
            if (firebase != null)
                platform.RegisterFeature(new ExampleFirebaseFeature(firebase));
        }
    }
}
#endif
