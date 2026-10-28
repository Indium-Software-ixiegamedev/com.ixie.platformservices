using System;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Auth;
using PlatformServices.Auth;
#if UNITY_ANDROID && GOOGLE_PLAY_GAMES
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace PlatformServices.Firebase
{
    public sealed class GooglePlayGamesAuthProviderAdapter : IFirebaseAuthProviderAdapter
    {
        public AuthProviderType Provider => AuthProviderType.GooglePlayGames;

#if UNITY_ANDROID && GOOGLE_PLAY_GAMES && !UNITY_EDITOR
        public bool IsSupported => true;
#else
        public bool IsSupported => false;
#endif

        public async Task<FirebaseUser> SignInAsync(
            FirebaseAuth auth,
            FirebaseUser currentUser,
            CancellationToken cancellationToken = default)
        {
#if UNITY_ANDROID && GOOGLE_PLAY_GAMES
            string serverAuthCode = await GetServerAuthCodeAsync(cancellationToken);
            if (string.IsNullOrEmpty(serverAuthCode))
                return null;

            Credential credential = PlayGamesAuthProvider.GetCredential(serverAuthCode);
            if (currentUser != null && currentUser.IsAnonymous)
            {
                AuthResult linkedResult = await currentUser
                    .LinkWithCredentialAsync(credential)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return linkedResult.User;
            }

            FirebaseUser user = await auth
                .SignInWithCredentialAsync(credential)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return user;
#else
            await Task.CompletedTask;
            return null;
#endif
        }

#if UNITY_ANDROID && GOOGLE_PLAY_GAMES
        private static async Task<string> GetServerAuthCodeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TaskCompletionSource<bool> authentication = new TaskCompletionSource<bool>();
            PlayGamesPlatform.Instance.Authenticate(status =>
                authentication.TrySetResult(status == SignInStatus.Success));

            if (!await authentication.Task.ConfigureAwait(false))
                return null;

            cancellationToken.ThrowIfCancellationRequested();
            TaskCompletionSource<string> serverAuthCode = new TaskCompletionSource<string>();
            PlayGamesPlatform.Instance.RequestServerSideAccess(
                false,
                code => serverAuthCode.TrySetResult(code));
            return await serverAuthCode.Task.ConfigureAwait(false);
        }
#endif
    }
}