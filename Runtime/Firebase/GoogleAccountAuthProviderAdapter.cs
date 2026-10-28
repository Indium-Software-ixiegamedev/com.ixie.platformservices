using System;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Auth;
using PlatformServices.Auth;
#if GOOGLE_SIGN_IN
using Google;
#endif

namespace PlatformServices.Firebase
{
    public sealed class GoogleAccountAuthProviderAdapter : IFirebaseAuthProviderAdapter
    {
        private readonly string webClientId;

        public GoogleAccountAuthProviderAdapter(string webClientId)
        {
            this.webClientId = webClientId;
        }

        public AuthProviderType Provider => AuthProviderType.GoogleAccount;

#if (UNITY_ANDROID || UNITY_IOS) && GOOGLE_SIGN_IN && !UNITY_EDITOR
        public bool IsSupported => !string.IsNullOrWhiteSpace(webClientId);
#else
        public bool IsSupported => false;
#endif

        public async Task<FirebaseUser> SignInAsync(
            FirebaseAuth auth,
            FirebaseUser currentUser,
            CancellationToken cancellationToken = default)
        {
#if (UNITY_ANDROID || UNITY_IOS) && GOOGLE_SIGN_IN && !UNITY_EDITOR
            if (!IsSupported)
                return null;

            cancellationToken.ThrowIfCancellationRequested();
            GoogleSignIn.Configuration = new GoogleSignInConfiguration
            {
                WebClientId = webClientId,
                RequestEmail = true,
                RequestIdToken = true
            };

            GoogleSignIn.DefaultInstance.SignOut();
            GoogleSignInUser googleUser;
            try
            {
                googleUser = await GoogleSignIn.DefaultInstance.SignIn();
            }
            catch (GoogleSignIn.SignInException exception)
            {
                throw new InvalidOperationException(GetSignInErrorMessage(exception.Status), exception);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (googleUser == null || string.IsNullOrEmpty(googleUser.IdToken))
                return null;

            Credential credential = GoogleAuthProvider.GetCredential(googleUser.IdToken, null);
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

#if (UNITY_ANDROID || UNITY_IOS) && GOOGLE_SIGN_IN && !UNITY_EDITOR
        private static string GetSignInErrorMessage(GoogleSignInStatusCode status)
        {
            if (status == GoogleSignInStatusCode.DeveloperError)
            {
                return "Google account sign-in failed with DeveloperError (6). Verify the Web OAuth client ID, Android package name, and SHA-1/SHA-256 fingerprints for the APK signing key.";
            }

            return $"Google account sign-in failed with status {status} ({(int)status}).";
        }
#endif
    }
}