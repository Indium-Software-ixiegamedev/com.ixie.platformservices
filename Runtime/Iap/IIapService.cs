using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlatformServices.Core;

namespace PlatformServices.Iap
{
    /// <summary>Current client-facing stage of a purchase request.</summary>
    public enum PurchaseStatus
    {
        Pending,
        Validating,
        Success,
        Cancelled,
        Failed
    }

    /// <summary>Outcome or progress update for one purchase attempt.</summary>
    public sealed class PurchaseResult
    {
        /// <summary>Gets or sets the purchase lifecycle status.</summary>
        public PurchaseStatus Status { get; set; }
        /// <summary>Gets or sets the canonical catalog product ID.</summary>
        public string ProductId { get; set; }
        /// <summary>Gets or sets the store transaction ID when available.</summary>
        public string TransactionId { get; set; }
        /// <summary>Gets or sets a player-safe error description; never include raw receipts or tokens.</summary>
        public string ErrorMessage { get; set; }
        /// <summary>Gets or sets whether store approval is deferred, for example pending parental approval.</summary>
        public bool IsDeferred { get; set; }
    }

    /// <summary>Store display information combined with authoritative entitlement state.</summary>
    public sealed class IapProductInfo
    {
        /// <summary>Gets or sets the canonical catalog product ID.</summary>
        public string ProductId { get; set; }
        /// <summary>Gets or sets the localized store title.</summary>
        public string Title { get; set; }
        /// <summary>Gets or sets the localized store description.</summary>
        public string Description { get; set; }
        /// <summary>Gets or sets the localized price string for display.</summary>
        public string LocalizedPrice { get; set; }
        /// <summary>Gets or sets the numeric localized price.</summary>
        public decimal Price { get; set; }
        /// <summary>Gets or sets the ISO 4217 currency code.</summary>
        public string IsoCurrencyCode { get; set; }
        /// <summary>Gets or sets whether the store currently offers this product.</summary>
        public bool IsAvailable { get; set; }
        /// <summary>Gets or sets whether all entitlements mapped to this product are owned.</summary>
        public bool IsOwned { get; set; }
    }

    /// <summary>
    /// Transient store proof sent to the trusted backend. Never persist or log this payload or its receipt/token fields.
    /// </summary>
    public sealed class PurchaseReceiptPayload
    {
        /// <summary>Gets or sets the canonical product ID expected by the backend allowlist.</summary>
        public string ProductId { get; set; }
        /// <summary>Gets or sets the normalized store name, such as google_play or app_store.</summary>
        public string Store { get; set; }
        /// <summary>Gets or sets the store transaction ID.</summary>
        public string TransactionId { get; set; }
        /// <summary>Gets or sets the Google Play purchase token or Apple's signed transaction proof.</summary>
        public string StorePurchaseToken { get; set; }
        /// <summary>Gets or sets the raw Unity IAP receipt when required by the backend verifier.</summary>
        public string RawReceipt { get; set; }
        /// <summary>Gets or sets the stable key used by the backend to prevent duplicate transaction grants.</summary>
        public string IdempotencyKey { get; set; }
    }

    /// <summary>
    /// Public game-facing IAP API. Ownership is derived from server-verified entitlement snapshots, not local receipt data.
    /// </summary>
    public interface IIapService : IPlatformFeature
    {
        /// <summary>Gets whether Unity IAP currently has a connected store.</summary>
        bool IsStoreReady { get; }
        /// <summary>Gets the latest non-sensitive initialization or store error.</summary>
        string LastError { get; }
        /// <summary>Raised for purchase progress and final outcomes.</summary>
        event Action<PurchaseResult> PurchaseUpdated;
        /// <summary>Raised when the server-owned entitlement snapshot changes.</summary>
        event Action<IReadOnlyCollection<string>> EntitlementsChanged;
        /// <summary>Gets loaded store products and their current display/ownership state.</summary>
        IReadOnlyList<IapProductInfo> Products { get; }
        /// <summary>Gets the current server-verified entitlement ID snapshot.</summary>
        IReadOnlyCollection<string> OwnedEntitlementIds { get; }
        /// <summary>Checks whether all entitlements mapped to a canonical product ID are owned.</summary>
        bool IsProductPurchased(string productId);
        /// <summary>Checks whether a server-verified permanent entitlement is currently owned.</summary>
        bool HasEntitlement(string entitlementId);
        /// <summary>Gets the localized numeric price, or zero when product metadata is unavailable.</summary>
        decimal GetLocalizedPrice(string productId);
        /// <summary>Gets the product's ISO currency code, or null when metadata is unavailable.</summary>
        string GetIsoCurrencyCode(string productId);
        /// <summary>Starts purchase flow and completes after backend verification and store acknowledgment handling.</summary>
        Task<PurchaseResult> InitiatePurchaseAsync(string productId, CancellationToken cancellationToken = default);
        /// <summary>Refreshes the signed-in player's authoritative entitlements from the backend.</summary>
        Task RefreshEntitlementsAsync(CancellationToken cancellationToken = default);
        /// <summary>Restores store transactions and reconciles them with the server-owned entitlement snapshot.</summary>
        Task RestorePurchasesAsync(CancellationToken cancellationToken = default);
    }
}