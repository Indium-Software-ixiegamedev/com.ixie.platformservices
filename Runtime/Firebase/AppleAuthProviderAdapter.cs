using System.Threading;
using System.Threading.Tasks;
using Firebase.Auth;
using PlatformServices.Auth;

namespace PlatformServices.Firebase
{
    public sealed class AppleAuthProviderAdapter : IFirebaseAuthProviderAdapter
    {
        public AuthProviderType Provider => AuthProviderType.AppleId;

#if UNITY_IOS && !UNITY_EDITOR
        public bool IsSupported => true;
#else
        public bool IsSupported => false;
#endif

        public async Task<FirebaseUser> SignInAsync(
            FirebaseAuth auth,
            FirebaseUser currentUser,
            CancellationToken cancellationToken = default)
        {
            if (!IsSupported)
                return null;

            cancellationToken.ThrowIfCancellationRequested();
            FederatedOAuthProviderData providerData = new FederatedOAuthProviderData
            {
                ProviderId = "apple.com"
            };
            FederatedOAuthProvider provider = new FederatedOAuthProvider(providerData);
            AuthResult result = currentUser != null && currentUser.IsAnonymous
                ? await currentUser.LinkWithProviderAsync(provider).ConfigureAwait(false)
                : await auth.SignInWithProviderAsync(provider).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result.User;
        }
    }
}