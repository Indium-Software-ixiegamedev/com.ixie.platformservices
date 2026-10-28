using System;
using System.Threading;
using System.Threading.Tasks;
using PlatformServices.Core;

namespace PlatformServices.Auth
{
    public enum AuthProviderType
    {
        Guest,
        GooglePlayGames,
        AppleId,
        GoogleAccount
    }

    public sealed class UserProfile
    {
        public string UserId { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public AuthProviderType Provider { get; set; }
        public bool IsAnonymous { get; set; }
    }

    public interface IAuthService : IPlatformFeature
    {
        UserProfile CurrentUser { get; }
        bool IsAuthenticated { get; }
        event Action<UserProfile> AuthStateChanged;
        Task<bool> SignInAsync(AuthProviderType provider, CancellationToken cancellationToken = default);
        Task<bool> LinkProviderAsync(AuthProviderType provider, CancellationToken cancellationToken = default);
        Task SignOutAsync(CancellationToken cancellationToken = default);
        Task<bool> DeleteAccountAsync(CancellationToken cancellationToken = default);
    }
}