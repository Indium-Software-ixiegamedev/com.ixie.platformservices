using System;
using System.Threading;
using System.Threading.Tasks;
using Firebase;
using PlatformServices.Core;

namespace PlatformServices.Firebase
{
    public sealed class FirebaseService : IFirebaseService
    {
        public FirebaseApp App { get; private set; }
        public DependencyStatus DependencyStatus { get; private set; } = DependencyStatus.UnavailableOther;
        public bool IsAvailable => IsInitialized && DependencyStatus == DependencyStatus.Available && App != null;
        public bool IsInitialized { get; private set; }

        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (IsInitialized)
                return IsAvailable;

            cancellationToken.ThrowIfCancellationRequested();
            DependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            cancellationToken.ThrowIfCancellationRequested();

            if (DependencyStatus != DependencyStatus.Available)
                return false;

            App = FirebaseApp.DefaultInstance;
            IsInitialized = App != null;
            return IsAvailable;
        }

        public void Dispose()
        {
            App = null;
            IsInitialized = false;
        }
    }
}