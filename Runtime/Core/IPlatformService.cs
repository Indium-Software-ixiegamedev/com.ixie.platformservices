using System;
using System.Threading;
using System.Threading.Tasks;

namespace PlatformServices.Core
{
    public interface IPlatformService : IPlatformFeature
    {
        void RegisterFeature(IPlatformFeature feature);
        bool HasFeature<T>() where T : class, IPlatformFeature;

        /// <summary>Gets the first registered feature assignable to <typeparamref name="T"/>, or null when absent.</summary>
        T GetFeature<T>() where T : class, IPlatformFeature;

        event Action<PlatformServiceState> StateChanged;
        PlatformServiceState State { get; }

        Task<bool> ShutdownAsync(CancellationToken cancellationToken = default);
    }

    public enum PlatformServiceState
    {
        NotStarted,
        Initializing,
        Ready,
        Failed,
        ShuttingDown,
        Shutdown
    }
}