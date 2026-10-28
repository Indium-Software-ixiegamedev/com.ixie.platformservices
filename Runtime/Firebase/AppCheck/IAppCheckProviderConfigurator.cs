using Firebase;

namespace PlatformServices.AppCheck
{
    public interface IAppCheckProviderConfigurator
    {
        bool Configure(FirebaseApp app, out string failureReason);
    }
}
