using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Auth;
using PlatformServices.Auth;
using PlatformServices.Core;
using PlatformUserProfile = PlatformServices.Auth.UserProfile;

namespace PlatformServices.Firebase
{
    public sealed class FirebaseAuthService : IAuthService
    {
        private readonly PlatformServices.Firebase.IFirebaseService firebase;
        private readonly Dictionary<AuthProviderType, IFirebaseAuthProviderAdapter> providerAdapters;
        private readonly SynchronizationContext mainThreadContext;
        private FirebaseAuth auth;

        public FirebaseAuthService(
            PlatformServices.Firebase.IFirebaseService firebase,
            params IFirebaseAuthProviderAdapter[] providerAdapters)
        {
            this.firebase = firebase ?? throw new ArgumentNullException(nameof(firebase));
            this.providerAdapters = new Dictionary<AuthProviderType, IFirebaseAuthProviderAdapter>();
            if (providerAdapters != null)
            {
                for (int index = 0; index < providerAdapters.Length; index++)
                {
                    IFirebaseAuthProviderAdapter adapter = providerAdapters[index];
                    if (adapter != null)
                        this.providerAdapters[adapter.Provider] = adapter;
                }
            }
            mainThreadContext = SynchronizationContext.Current;
        }

        public PlatformUserProfile CurrentUser { get; private set; }
        public bool IsAuthenticated => CurrentUser != null;
        public bool IsInitialized { get; private set; }
        public event Action<PlatformUserProfile> AuthStateChanged;

        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {

            if (!firebase.IsAvailable)
                return false;

            cancellationToken.ThrowIfCancellationRequested();
            auth = FirebaseAuth.GetAuth(firebase.App);
            if (auth == null)
                return false;
            if (auth.CurrentUser != null)
                SetCurrentUser(auth.CurrentUser, GetProvider(auth.CurrentUser));

            IsInitialized = true;
            return true;
        }

        public async Task<bool> SignInAsync(AuthProviderType provider, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();

            if (provider == AuthProviderType.Guest)
            {
                if (auth.CurrentUser != null)
                {
                    SetCurrentUser(auth.CurrentUser, GetProvider(auth.CurrentUser));
                    return true;
                }

                AuthResult authResult = await auth.SignInAnonymouslyAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                SetCurrentUser(authResult.User, AuthProviderType.Guest);
                return CurrentUser != null;
            }

            if (!providerAdapters.TryGetValue(provider, out IFirebaseAuthProviderAdapter adapter) || !adapter.IsSupported)
                return false;

            FirebaseUser user = await adapter
                .SignInAsync(auth, auth.CurrentUser, cancellationToken)
                .ConfigureAwait(false);
            SetCurrentUser(user, provider);
            return CurrentUser != null;
        }

        public async Task<bool> LinkProviderAsync(AuthProviderType provider, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();

            if (auth.CurrentUser == null || !auth.CurrentUser.IsAnonymous)
                return false;
            if (!providerAdapters.TryGetValue(provider, out IFirebaseAuthProviderAdapter adapter) || !adapter.IsSupported)
                return false;

            FirebaseUser user = await adapter
                .SignInAsync(auth, auth.CurrentUser, cancellationToken)
                .ConfigureAwait(false);
            SetCurrentUser(user, provider);
            return CurrentUser != null && !CurrentUser.IsAnonymous;
        }

        public async Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            auth.SignOut();
            CurrentUser = null;
            PublishAuthState(null);
            await Task.CompletedTask;
        }

        public async Task<bool> DeleteAccountAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            if (auth.CurrentUser == null)
                return true;

            await auth.CurrentUser.DeleteAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            CurrentUser = null;
            PublishAuthState(null);
            return true;
        }

        public void Dispose()
        {
            auth = null;
            CurrentUser = null;
            IsInitialized = false;
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized && auth == null)
                throw new InvalidOperationException("Firebase Auth has not been initialized.");
        }

        private void SetCurrentUser(FirebaseUser user, AuthProviderType provider)
        {
            CurrentUser = user == null ? null : new PlatformUserProfile
            {
                UserId = user.UserId,
                DisplayName = user.DisplayName,
                Email = user.Email,
                Provider = user.IsAnonymous ? AuthProviderType.Guest : provider,
                IsAnonymous = user.IsAnonymous
            };
            PublishAuthState(CurrentUser);
        }

        private static AuthProviderType GetProvider(FirebaseUser user)
        {
            if (user == null || user.IsAnonymous)
                return AuthProviderType.Guest;

            foreach (IUserInfo provider in user.ProviderData)
            {
                if (provider.ProviderId == "apple.com")
                    return AuthProviderType.AppleId;
                if (provider.ProviderId == GoogleAuthProvider.ProviderId)
                    return AuthProviderType.GoogleAccount;
                if (provider.ProviderId == PlayGamesAuthProvider.ProviderId)
                    return AuthProviderType.GooglePlayGames;
            }

            return AuthProviderType.Guest;
        }

        private void PublishAuthState(PlatformUserProfile profile)
        {
            if (mainThreadContext == null || SynchronizationContext.Current == mainThreadContext)
                AuthStateChanged?.Invoke(profile);
            else
                mainThreadContext.Post(_ => AuthStateChanged?.Invoke(profile), null);
        }
    }
}