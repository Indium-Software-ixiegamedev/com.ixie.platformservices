#if UNITY_EDITOR
using System.Threading;
using System.Threading.Tasks;
using Firebase;
using PlatformServices.Firebase;

namespace PlatformServices.Testing
{
    public sealed class TestingFirebaseService : IFirebaseService
    {
        public bool IsInitialized { get; private set; }
        public FirebaseApp App => null;
        public DependencyStatus DependencyStatus => DependencyStatus.Available;
        public bool IsAvailable => IsInitialized;

        public Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsInitialized = true;
            return Task.FromResult(true);
        }

        public void Dispose()
        {
            IsInitialized = false;
        }
    }
}
#endif
