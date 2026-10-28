using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Firebase;
using PlatformServices.Core;
using PlatformServices.Firebase;

namespace PlatformServices.AppCheck
{
    public sealed class FirebaseAppCheckService : IAppCheckService
    {
        private readonly IFirebaseService firebase;
        private readonly IAppCheckProviderConfigurator providerConfigurator;
        private readonly bool requireProviderOnDevice;
        private readonly bool allowUnconfiguredInEditor;

        public FirebaseAppCheckService(
            IFirebaseService firebase,
            IAppCheckProviderConfigurator providerConfigurator,
            AppCheckConfiguration configuration)
        {
            this.firebase = firebase ?? throw new ArgumentNullException(nameof(firebase));
            this.providerConfigurator = providerConfigurator ?? throw new ArgumentNullException(nameof(providerConfigurator));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            requireProviderOnDevice = configuration.RequireProviderOnDevice;
            allowUnconfiguredInEditor = configuration.AllowUnconfiguredInEditor;
        }

        public bool IsInitialized { get; private set; }
        public bool IsProviderConfigured { get; private set; }
        public string FailureReason { get; private set; }

        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (IsInitialized)
                return true;
            if (!firebase.IsAvailable)
                return Fail("Firebase is unavailable.");

            cancellationToken.ThrowIfCancellationRequested();
            IsProviderConfigured = providerConfigurator.Configure(firebase.App, out string failureReason);
            FailureReason = failureReason;

#if UNITY_EDITOR
            if (!IsProviderConfigured && allowUnconfiguredInEditor)
                IsProviderConfigured = true;
#else
            if (!IsProviderConfigured && requireProviderOnDevice)
                return false;
#endif

            cancellationToken.ThrowIfCancellationRequested();
            IsInitialized = true;
            return true;
        }

        public async Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken = default)
        {
            if (!IsInitialized || !IsProviderConfigured)
                return null;

            cancellationToken.ThrowIfCancellationRequested();
            Type appCheckType = Type.GetType("Firebase.AppCheck.FirebaseAppCheck, Firebase.AppCheck");
            if (appCheckType == null)
                return null;

            MethodInfo getInstance = appCheckType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static);
            MethodInfo getToken = appCheckType.GetMethod("GetAppCheckTokenAsync", BindingFlags.Public | BindingFlags.Instance);
            if (getInstance == null || getToken == null)
                return null;

            object appCheck = getInstance.Invoke(null, new object[] { firebase.App });
            if (appCheck == null)
                return null;

            object taskObject = getToken.Invoke(appCheck, new object[] { forceRefresh });
            if (!(taskObject is Task tokenTask))
                return null;

            await tokenTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (tokenTask.IsFaulted || tokenTask.IsCanceled)
                return null;

            PropertyInfo resultProperty = tokenTask.GetType().GetProperty("Result");
            object result = resultProperty?.GetValue(tokenTask, null);
            return result?.GetType().GetProperty("Token")?.GetValue(result, null) as string;
        }

        public void Dispose()
        {
            IsInitialized = false;
            IsProviderConfigured = false;
            FailureReason = null;
        }

        private bool Fail(string reason)
        {
            FailureReason = reason;
            return false;
        }
    }
}
