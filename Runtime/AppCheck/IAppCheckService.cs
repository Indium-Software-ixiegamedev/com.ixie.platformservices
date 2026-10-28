using System.Threading;
using System.Threading.Tasks;
using PlatformServices.Core;

namespace PlatformServices.AppCheck
{
    public interface IAppCheckService : IPlatformFeature
    {
        Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken = default);
    }
}