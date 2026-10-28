#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlatformServices.Analytics;
using PlatformServices.AppCheck;
using PlatformServices.Auth;
using PlatformServices.CloudSave;
using PlatformServices.Consent;
using PlatformServices.Core;
using PlatformServices.Iap;
using PlatformServices.Notifications;
using PlatformServices.RemoteConfig;
using PlatformServices.Integrity;

namespace PlatformServices.Testing
{
    public abstract class TestingPlatformFeature : IPlatformFeature
    {
        public bool IsInitialized { get; private set; }

        public virtual Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsInitialized = true;
            return Task.FromResult(true);
        }

        public virtual void Dispose()
        {
            IsInitialized = false;
        }
    }

    public sealed class TestingConsentService : TestingPlatformFeature, IConsentService
    {
        public ConsentStatus CurrentStatus { get; private set; } = ConsentStatus.NotRequired;

        public Task<ConsentStatus> RequestConsentAsync(bool underAgeOfConsent = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CurrentStatus = underAgeOfConsent ? ConsentStatus.RequiredAndDenied : ConsentStatus.NotRequired;
            return Task.FromResult(CurrentStatus);
        }

        public Task ShowPrivacyOptionsFormAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    public sealed class TestingAppCheckService : TestingPlatformFeature, IAppCheckService
    {
        public Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("editor-debug-token");
        }
    }

    public sealed class TestingIntegrityService : TestingPlatformFeature, IIntegrityService
    {
        public string FailureReason { get; private set; }
        public bool IsTrusted => IsInitialized;
    }

    public sealed class TestingAuthService : TestingPlatformFeature, IAuthService
    {
        public UserProfile CurrentUser { get; private set; }
        public bool IsAuthenticated => CurrentUser != null;
        public event Action<UserProfile> AuthStateChanged;

        public Task<bool> SignInAsync(AuthProviderType provider, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CurrentUser = new UserProfile
            {
                UserId = "editor-user",
                DisplayName = "Editor User",
                Provider = provider,
                IsAnonymous = provider == AuthProviderType.Guest
            };
            AuthStateChanged?.Invoke(CurrentUser);
            return Task.FromResult(true);
        }

        public Task<bool> LinkProviderAsync(AuthProviderType provider, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CurrentUser == null || !CurrentUser.IsAnonymous)
                return Task.FromResult(false);

            CurrentUser.Provider = provider;
            CurrentUser.IsAnonymous = false;
            AuthStateChanged?.Invoke(CurrentUser);
            return Task.FromResult(true);
        }

        public Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CurrentUser = null;
            AuthStateChanged?.Invoke(null);
            return Task.CompletedTask;
        }

        public async Task<bool> DeleteAccountAsync(CancellationToken cancellationToken = default)
        {
            await SignOutAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    public sealed class TestingIapService : TestingPlatformFeature, IIapService
    {
        private readonly HashSet<string> purchasedProducts = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> ownedEntitlementIds = new HashSet<string>(StringComparer.Ordinal);
        public event Action<PurchaseResult> PurchaseUpdated;
        public event Action<IReadOnlyCollection<string>> EntitlementsChanged;
        public bool IsStoreReady => IsInitialized;
        public string LastError => null;
        public IReadOnlyList<IapProductInfo> Products { get; } = new List<IapProductInfo>();
        public IReadOnlyCollection<string> OwnedEntitlementIds => ownedEntitlementIds;

        public bool IsProductPurchased(string productId) => purchasedProducts.Contains(productId);
        public bool HasEntitlement(string entitlementId) => ownedEntitlementIds.Contains(entitlementId);
        public decimal GetLocalizedPrice(string productId) => 0m;
        public string GetIsoCurrencyCode(string productId) => "USD";

        public Task RefreshEntitlementsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EntitlementsChanged?.Invoke(OwnedEntitlementIds);
            return Task.CompletedTask;
        }

        public Task<PurchaseResult> InitiatePurchaseAsync(string productId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PurchaseResult result = new PurchaseResult
            {
                Status = PurchaseStatus.Success,
                ProductId = productId,
                TransactionId = "editor-transaction"
            };
            purchasedProducts.Add(productId);
            ownedEntitlementIds.Add(productId);
            PurchaseUpdated?.Invoke(result);
            EntitlementsChanged?.Invoke(OwnedEntitlementIds);
            return Task.FromResult(result);
        }

        public Task RestorePurchasesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    public sealed class TestingRemoteConfigService : TestingPlatformFeature, IRemoteConfigService
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);

        public Task<bool> FetchAndActivateAsync(TimeSpan minimumFetchInterval, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }

        public string GetString(string key, string defaultValue = "") => values.TryGetValue(key, out string value) ? value : defaultValue;
        public int GetInt(string key, int defaultValue = 0) => int.TryParse(GetString(key), out int value) ? value : defaultValue;
        public bool GetBool(string key, bool defaultValue = false) => bool.TryParse(GetString(key), out bool value) ? value : defaultValue;
        public float GetFloat(string key, float defaultValue = 0.0f) => float.TryParse(GetString(key), out float value) ? value : defaultValue;
    }

    public sealed class TestingCloudSaveService : TestingPlatformFeature, ICloudSaveService
    {
        private readonly Dictionary<string, object> documents = new Dictionary<string, object>(StringComparer.Ordinal);

        public Task<bool> SaveDataAsync<T>(string collection, string documentId, T data, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            documents[GetKey(collection, documentId)] = data;
            return Task.FromResult(true);
        }

        public Task<T> LoadDataAsync<T>(string collection, string documentId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            object data;
            return Task.FromResult(documents.TryGetValue(GetKey(collection, documentId), out data) && data is T typedData ? typedData : default);
        }

        public Task<bool> DeleteDataAsync(string collection, string documentId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(documents.Remove(GetKey(collection, documentId)));
        }

        private static string GetKey(string collection, string documentId) => collection + "/" + documentId;
    }

    public sealed class TestingAnalyticsService : TestingPlatformFeature, IAnalyticsService
    {
        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null) { }
        public void SetUserId(string userId) { }
        public void SetUserProperty(string propertyName, string propertyValue) { }
    }

    public sealed class TestingPushNotificationService : TestingPlatformFeature, IPushNotificationService
    {
        public string RegistrationToken { get; private set; }

        public Task<bool> RegisterAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RegistrationToken = "editor-push-token";
            return Task.FromResult(true);
        }

        public Task UnregisterAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RegistrationToken = null;
            return Task.CompletedTask;
        }
    }

    public static class TestingPlatformServiceFactory
    {
        public static PlatformServiceCore CreateDefault()
        {
            PlatformServiceCore platform = new PlatformServiceCore();
            platform.RegisterFeature(new TestingIntegrityService());
            platform.RegisterFeature(new TestingConsentService());
            platform.RegisterFeature(new TestingAppCheckService());
            platform.RegisterFeature(new TestingAuthService());
            platform.RegisterFeature(new TestingIapService());
            platform.RegisterFeature(new TestingRemoteConfigService());
            platform.RegisterFeature(new TestingCloudSaveService());
            platform.RegisterFeature(new TestingAnalyticsService());
            return platform;
        }
    }
}
#endif