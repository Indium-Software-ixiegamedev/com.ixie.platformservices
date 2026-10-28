using System;
using System.Threading;
using System.Threading.Tasks;

namespace PlatformServices.Core
{
    public interface IPlatformFeature : IDisposable
    {
        bool IsInitialized { get; }
        Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
    }
}