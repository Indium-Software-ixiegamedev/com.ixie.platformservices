using Firebase;
using PlatformServices.Core;

namespace PlatformServices.Firebase
{
    public interface IFirebaseService : IPlatformFeature
    {
        FirebaseApp App { get; }
        DependencyStatus DependencyStatus { get; }
        bool IsAvailable { get; }
    }
}