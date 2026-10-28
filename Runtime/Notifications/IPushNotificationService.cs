using System.Threading;
using System.Threading.Tasks;
using PlatformServices.Core;

namespace PlatformServices.Notifications
{
    public interface IPushNotificationService : IPlatformFeature
    {
        string RegistrationToken { get; }
        Task<bool> RegisterAsync(CancellationToken cancellationToken = default);
        Task UnregisterAsync(CancellationToken cancellationToken = default);
    }
}