using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlatformServices.Iap
{
    /// <summary>
    /// Defines one non-consumable product and the permanent entitlement IDs granted after server verification.
    /// <see cref="ProductId"/> is the canonical ID used by <see cref="IIapService"/>; store IDs may differ by platform.
    /// </summary>
    [Serializable]
    public sealed class IapProductDefinition
    {
        /// <summary>Canonical product ID used by game code, backend allowlists, and the service API.</summary>
        [Tooltip("Your stable cross-platform ID, for example shloka.morning. Use this exact value in IAPProductDataSO and the backend product allowlist. This is not necessarily the store listing SKU.")]
        [SerializeField] private string productId;
        /// <summary>Exact App Store Connect listing SKU. Leave empty to reuse the canonical product ID.</summary>
        [Tooltip("The exact product ID created in App Store Connect under Monetization > In-App Purchases. Copy it exactly. Leave empty only if this is identical to the canonical Product Id above.")]
        [SerializeField] private string appleProductId;
        /// <summary>Exact Google Play listing SKU. Leave empty to reuse the canonical product ID.</summary>
        [Tooltip("The exact product ID created in Google Play Console under Monetize with Play > Products. Copy it exactly. Leave empty only if this is identical to the canonical Product Id above.")]
        [SerializeField] private string googlePlayProductId;
        /// <summary>Permanent server-owned content entitlements granted by this product, such as a Shloka ID.</summary>
        [Tooltip("Stable content entitlement keys granted by the trusted backend, for example shloka.gayatri. Add matching grant rules to the backend and check ownership with IIapService.HasEntitlement. Do not put store SKUs here.")]
        [SerializeField] private string[] entitlementIds = Array.Empty<string>();

        /// <summary>Gets the canonical ID used by PlatformServices APIs.</summary>
        public string ProductId => productId;
        /// <summary>Gets the permanent entitlement IDs associated with this product.</summary>
        public IReadOnlyList<string> EntitlementIds => entitlementIds;

        /// <summary>Gets the store-specific SKU for the active player build.</summary>
        public string GetStoreProductId()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return string.IsNullOrWhiteSpace(appleProductId) ? productId : appleProductId;
#elif UNITY_ANDROID && !UNITY_EDITOR
            return string.IsNullOrWhiteSpace(googlePlayProductId) ? productId : googlePlayProductId;
#else
            return productId;
#endif
        }
    }

    /// <summary>
    /// Shared runtime catalog for store products and their canonical, permanent entitlement mappings.
    /// Entries must agree with the backend product allowlist and game-facing purchase references.
    /// </summary>
    [CreateAssetMenu(fileName = "IapProductCatalog", menuName = "Platform Services/IAP Product Catalog")]
    public sealed class IapProductCatalog : ScriptableObject
    {
        [Tooltip("One entry per purchasable product. Every canonical ID must match the game-facing IAPProductDataSO and the backend allowlist; platform SKUs must match the store consoles.")]
        [SerializeField] private List<IapProductDefinition> products = new List<IapProductDefinition>();

        /// <summary>Gets the configured store products.</summary>
        public IReadOnlyList<IapProductDefinition> Products => products;

        /// <summary>Checks required IDs, duplicates, entitlement mappings, and that the catalog is not empty.</summary>
        public bool TryValidate(out string error)
        {
            if (products == null)
            {
                error = "The IAP product catalog is missing its product list.";
                return false;
            }

            HashSet<string> productIds = new HashSet<string>(StringComparer.Ordinal);

            for (int productIndex = 0; productIndex < products.Count; productIndex++)
            {
                IapProductDefinition product = products[productIndex];
                if (product == null || string.IsNullOrWhiteSpace(product.ProductId))
                {
                    error = "Every IAP catalog entry requires a product ID.";
                    return false;
                }

                if (!productIds.Add(product.ProductId))
                {
                    error = "Duplicate IAP product ID: " + product.ProductId;
                    return false;
                }

                if (product.EntitlementIds == null || product.EntitlementIds.Count == 0)
                {
                    error = "Product '" + product.ProductId + "' must grant at least one entitlement ID.";
                    return false;
                }

                for (int entitlementIndex = 0; entitlementIndex < product.EntitlementIds.Count; entitlementIndex++)
                {
                    string entitlementId = product.EntitlementIds[entitlementIndex];
                    if (string.IsNullOrWhiteSpace(entitlementId))
                    {
                        error = "Product '" + product.ProductId + "' contains an empty entitlement ID.";
                        return false;
                    }

                }
            }

            if (products.Count == 0)
            {
                error = "The IAP product catalog is empty.";
                return false;
            }

            error = null;
            return true;
        }
    }
}