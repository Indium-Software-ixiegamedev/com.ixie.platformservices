using Firebase;

namespace PlatformServices.AppCheck
{
    public sealed class UnavailableAppCheckProviderConfigurator : IAppCheckProviderConfigurator
    {
        public bool Configure(FirebaseApp app, out string failureReason)
        {
            failureReason = "No Firebase App Check provider is installed or configured.";
            return false;
        }
    }
}
