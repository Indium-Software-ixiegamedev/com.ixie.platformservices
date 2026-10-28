using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PlatformServices.Iap
{
    /// <summary>Store-specific opaque identifiers that bind store transactions to one player.</summary>
    public sealed class IapAccountBinding
    {
        /// <summary>Gets the stable UUID set as Apple's appAccountToken before purchases.</summary>
        public string AppleAppAccountToken { get; set; }
        /// <summary>Gets the stable opaque account ID set in Google Play Billing before purchases.</summary>
        public string GoogleObfuscatedAccountId { get; set; }
    }

    /// <summary>Trusted backend response for validating one store transaction.</summary>
    public sealed class ReceiptValidationResult
    {
        /// <summary>Gets or sets whether the backend accepted the verified transaction.</summary>
        public bool IsValid { get; set; }
        /// <summary>Gets or sets the canonical product ID validated by the backend.</summary>
        public string ProductId { get; set; }
        /// <summary>Gets or sets the verified transaction ID.</summary>
        public string TransactionId { get; set; }
        /// <summary>Gets or sets a non-sensitive backend error code.</summary>
        public string ErrorCode { get; set; }
        /// <summary>Gets or sets the complete owned entitlement snapshot for the authenticated player.</summary>
        public IReadOnlyCollection<string> OwnedEntitlementIds { get; set; }
    }

    /// <summary>
    /// Client transport contract for trusted server verification and entitlement lookup.
    /// Implementations must not validate receipts locally or grant ownership themselves.
    /// </summary>
    public interface IReceiptValidator
    {
        /// <summary>Gets or creates store account identifiers for the authenticated user.</summary>
        Task<IapAccountBinding> GetAccountBindingAsync(CancellationToken cancellationToken = default);

        /// <summary>Sends transient store proof to the trusted backend for verification and idempotent fulfillment.</summary>
        Task<ReceiptValidationResult> ValidateReceiptAsync(
            PurchaseReceiptPayload receipt,
            CancellationToken cancellationToken = default);

        /// <summary>Fetches the signed-in player's server-owned entitlement snapshot.</summary>
        Task<IReadOnlyCollection<string>> GetOwnedEntitlementIdsAsync(
            CancellationToken cancellationToken = default);
    }
}