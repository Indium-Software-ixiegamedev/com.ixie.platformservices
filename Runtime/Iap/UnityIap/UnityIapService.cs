using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PlatformServices.Auth;
using PlatformServices.AppCheck;
using UnityEngine;
using UnityEngine.Purchasing;

namespace PlatformServices.Iap
{
    /// <summary>
    /// Unity IAP 5 adapter for permanent products. It sends pending and restored orders to the trusted backend,
    /// updates ownership only from server responses, and acknowledges pending orders only after verified fulfillment.
    /// </summary>
    public sealed class UnityIapService : IIapService
    {
        private readonly IapProductCatalog catalog;
        private readonly IAuthService auth;
        private readonly IAppCheckService appCheck;
        private readonly IReceiptValidator receiptValidator;
        private readonly Dictionary<string, Product> storeProducts = new Dictionary<string, Product>(StringComparer.Ordinal);
        private readonly HashSet<string> ownedEntitlementIds = new HashSet<string>(StringComparer.Ordinal);
        private StoreController storeController;
        private TaskCompletionSource<PurchaseResult> activePurchase;
        private TaskCompletionSource<bool> purchasesFetchCompletion;
        private IapAccountBinding storeAccountBinding;
        private string storeAccountBindingUid;
        private string activeProductId;
        private bool isDisposed;

        /// <summary>Creates the store adapter with its catalog, Firebase authentication state, App Check, and backend verifier.</summary>
        public UnityIapService(IapProductCatalog catalog, IAuthService auth, IAppCheckService appCheck, IReceiptValidator receiptValidator)
        {
            this.catalog = catalog;
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
            this.appCheck = appCheck;
            this.receiptValidator = receiptValidator ?? throw new ArgumentNullException(nameof(receiptValidator));
        }

        /// <summary>Gets whether the service lifecycle completed, including when the store is temporarily unavailable.</summary>
        public bool IsInitialized { get; private set; }
        /// <summary>Gets whether Unity IAP is currently connected to the store.</summary>
        public bool IsStoreReady { get; private set; }
        /// <summary>Gets the latest non-sensitive operational error.</summary>
        public string LastError { get; private set; }
        /// <summary>Raised when a purchase begins validation or reaches a final/deferred state.</summary>
        public event Action<PurchaseResult> PurchaseUpdated;
        /// <summary>Raised when the authoritative server entitlement snapshot changes.</summary>
        public event Action<IReadOnlyCollection<string>> EntitlementsChanged;
        /// <summary>Gets the currently cached server-verified entitlement IDs.</summary>
        public IReadOnlyCollection<string> OwnedEntitlementIds => Array.AsReadOnly(ownedEntitlementIds.ToArray());

        /// <summary>Gets catalog products combined with store metadata and current verified ownership.</summary>
        public IReadOnlyList<IapProductInfo> Products
        {
            get
            {
                List<IapProductInfo> products = new List<IapProductInfo>();
                if (catalog == null)
                    return products;

                for (int index = 0; index < catalog.Products.Count; index++)
                {
                    IapProductDefinition definition = catalog.Products[index];
                    storeProducts.TryGetValue(definition.ProductId, out Product product);
                    ProductMetadata metadata = product?.metadata;
                    products.Add(new IapProductInfo
                    {
                        ProductId = definition.ProductId,
                        Title = metadata?.localizedTitle,
                        Description = metadata?.localizedDescription,
                        LocalizedPrice = metadata?.localizedPriceString,
                        Price = metadata?.localizedPrice ?? 0m,
                        IsoCurrencyCode = metadata?.isoCurrencyCode,
                        IsAvailable = product != null && product.availableToPurchase,
                        IsOwned = IsProductPurchased(definition.ProductId)
                    });
                }

                return products;
            }
        }

        /// <summary>Connects to Unity IAP, loads catalog products, and begins purchase reconciliation.</summary>
        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (IsInitialized)
                return true;
            string catalogError = null;
            if (catalog == null || !catalog.TryValidate(out catalogError))
            {
                LastError = catalog == null ? "An IAP product catalog is required." : catalogError;
                return false;
            }
            if (appCheck == null || !appCheck.IsInitialized)
            {
                LastError = "Firebase App Check must be initialized before enabling IAP.";
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            storeController = UnityIAPServices.StoreController();
            SubscribeToStoreEvents();
            auth.AuthStateChanged += OnAuthStateChanged;
            storeController.ProcessPendingOrdersOnPurchasesFetched(true);

            try
            {
                await storeController.Connect();
                IsStoreReady = true;
                List<ProductDefinition> definitions = new List<ProductDefinition>();
                for (int index = 0; index < catalog.Products.Count; index++)
                    definitions.Add(catalog.Products[index].ToUnityDefinition());

                TaskCompletionSource<bool> productsFetched = new TaskCompletionSource<bool>();
                storeController.OnProductsFetched += OnInitialProductsFetched;
                storeController.OnProductsFetchFailed += OnInitialProductsFetchFailed;
                storeController.FetchProductsWithNoRetries(definitions);
                await productsFetched.Task;
                storeController.OnProductsFetched -= OnInitialProductsFetched;
                storeController.OnProductsFetchFailed -= OnInitialProductsFetchFailed;

                void OnInitialProductsFetched(List<Product> products)
                {
                    CacheProducts(products);
                    productsFetched.TrySetResult(true);
                }

                void OnInitialProductsFetchFailed(ProductFetchFailed failure)
                {
                    LastError = "Store product fetch failed: " + failure;
                    productsFetched.TrySetResult(false);
                }

                storeController.FetchPurchases();
                IsInitialized = true;
                if (auth.IsAuthenticated)
                    await RefreshEntitlementsAsync(cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                IsStoreReady = false;
                LastError = "Store connection failed: " + exception.GetType().Name;
                IsInitialized = true;
                return true;
            }
        }

        /// <summary>Checks whether all catalog entitlements for a canonical product ID are owned.</summary>
        public bool IsProductPurchased(string productId)
        {
            IapProductDefinition definition = FindProductDefinition(productId);
            if (definition == null || definition.EntitlementIds.Count == 0)
                return false;

            for (int index = 0; index < definition.EntitlementIds.Count; index++)
            {
                if (!ownedEntitlementIds.Contains(definition.EntitlementIds[index]))
                    return false;
            }

            return true;
        }

        /// <summary>Checks the cached server-verified entitlement snapshot.</summary>
        public bool HasEntitlement(string entitlementId)
        {
            return !string.IsNullOrWhiteSpace(entitlementId) && ownedEntitlementIds.Contains(entitlementId);
        }

        /// <summary>Gets the store-localized numeric price for a canonical product ID.</summary>
        public decimal GetLocalizedPrice(string productId)
        {
            return storeProducts.TryGetValue(productId, out Product product) && product.metadata != null
                ? product.metadata.localizedPrice
                : 0m;
        }

        /// <summary>Gets the store-localized currency code for a canonical product ID.</summary>
        public string GetIsoCurrencyCode(string productId)
        {
            return storeProducts.TryGetValue(productId, out Product product) ? product.metadata?.isoCurrencyCode : null;
        }

        /// <summary>Starts a one-at-a-time store purchase; ownership is granted only after backend verification.</summary>
        public async Task<PurchaseResult> InitiatePurchaseAsync(string productId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsInitialized || !IsStoreReady || storeController == null)
                return PublishResult(PurchaseStatus.Failed, productId, null, "The store is not available.");
            if (!auth.IsAuthenticated)
                return PublishResult(PurchaseStatus.Failed, productId, null, "Sign in before purchasing.");
            if (activePurchase != null)
                return PublishResult(PurchaseStatus.Failed, productId, null, "Another purchase is already in progress.");

            try
            {
                await EnsureStoreAccountBindingAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                LastError = "Could not prepare store account binding: " + exception.GetType().Name;
                return PublishResult(PurchaseStatus.Failed, productId, null, "Could not securely prepare this purchase. Try again.");
            }

            if (IsProductPurchased(productId))
                return PublishResult(PurchaseStatus.Success, productId, null, null);
            if (!storeProducts.TryGetValue(productId, out Product product) || !product.availableToPurchase)
                return PublishResult(PurchaseStatus.Failed, productId, null, "The product is unavailable in the store.");

            activeProductId = productId;
            activePurchase = new TaskCompletionSource<PurchaseResult>();
            Task<PurchaseResult> purchaseTask = activePurchase.Task;
            try
            {
                storeController.PurchaseProduct(product);
            }
            catch (Exception exception)
            {
                CompleteActivePurchase(PublishResult(PurchaseStatus.Failed, productId, null, exception.GetType().Name));
            }

            return await purchaseTask;
        }

        /// <summary>Replaces the local entitlement cache with the authenticated player's backend snapshot.</summary>
        public async Task RefreshEntitlementsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!auth.IsAuthenticated)
            {
                ReplaceOwnedEntitlements(Array.Empty<string>());
                return;
            }

            IReadOnlyCollection<string> entitlements = await receiptValidator.GetOwnedEntitlementIdsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ReplaceOwnedEntitlements(entitlements);
        }

        /// <summary>Restores store transactions, verifies catalog orders, then refreshes backend ownership.</summary>
        public async Task RestorePurchasesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsStoreReady || storeController == null)
                throw new InvalidOperationException("The store is not available.");
            if (!auth.IsAuthenticated)
                throw new InvalidOperationException("Sign in before restoring purchases.");

            TaskCompletionSource<bool> restoreCompletion = new TaskCompletionSource<bool>();
            storeController.RestoreTransactions((success, error) =>
            {
                if (success)
                    restoreCompletion.TrySetResult(true);
                else
                    restoreCompletion.TrySetException(new InvalidOperationException(error ?? "Store restore failed."));
            });
            await restoreCompletion.Task;
            cancellationToken.ThrowIfCancellationRequested();

            await FetchPurchasesAndWaitAsync(cancellationToken);
            await RefreshEntitlementsAsync(cancellationToken);
        }

        /// <summary>Unsubscribes from store/auth events and clears transient product and entitlement state.</summary>
        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            auth.AuthStateChanged -= OnAuthStateChanged;
            if (storeController != null)
                UnsubscribeFromStoreEvents();

            IsStoreReady = false;
            IsInitialized = false;
            activePurchase?.TrySetResult(PublishResult(PurchaseStatus.Failed, activeProductId, null, "IAP service was shut down."));
            activePurchase = null;
            ownedEntitlementIds.Clear();
            storeProducts.Clear();
        }

        private void SubscribeToStoreEvents()
        {
            storeController.OnStoreDisconnected += OnStoreDisconnected;
            storeController.OnStoreConnected += OnStoreConnected;
            storeController.OnPurchasePending += OnPurchasePending;
            storeController.OnPurchaseFailed += OnPurchaseFailed;
            storeController.OnPurchaseDeferred += OnPurchaseDeferred;
            storeController.OnProductsFetched += CacheProducts;
            storeController.OnPurchasesFetched += OnPurchasesFetched;
            storeController.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
        }

        private void UnsubscribeFromStoreEvents()
        {
            storeController.OnStoreDisconnected -= OnStoreDisconnected;
            storeController.OnStoreConnected -= OnStoreConnected;
            storeController.OnPurchasePending -= OnPurchasePending;
            storeController.OnPurchaseFailed -= OnPurchaseFailed;
            storeController.OnPurchaseDeferred -= OnPurchaseDeferred;
            storeController.OnProductsFetched -= CacheProducts;
            storeController.OnPurchasesFetched -= OnPurchasesFetched;
            storeController.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
        }

        private void OnStoreConnected()
        {
            IsStoreReady = true;
            LastError = null;
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
        {
            IsStoreReady = false;
            LastError = "Store disconnected: " + failure;
        }

        private void CacheProducts(List<Product> products)
        {
            storeProducts.Clear();
            for (int index = 0; index < products.Count; index++)
                storeProducts[products[index].definition.id] = products[index];
        }

        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            LastError = "Store purchase restore failed: " + failure;
            purchasesFetchCompletion?.TrySetException(new InvalidOperationException(LastError));
        }

        private async void OnPurchasePending(PendingOrder order)
        {
            if (isDisposed)
                return;

            await ProcessOrderAsync(order, true);
        }

        private async void OnPurchasesFetched(Orders orders)
        {
            bool allOrdersVerified = true;
            if (!isDisposed && auth.IsAuthenticated && orders != null)
            {
                for (int index = 0; index < orders.ConfirmedOrders.Count; index++)
                    allOrdersVerified &= await ProcessOrderAsync(orders.ConfirmedOrders[index], false);
            }

            purchasesFetchCompletion?.TrySetResult(allOrdersVerified);
        }

        private async Task<bool> ProcessOrderAsync(Order order, bool confirmAfterValidation)
        {
            string productId = GetSingleProductId(order);
            string transactionId = order?.Info?.TransactionID;
            string transactionKey = CreateIdempotencyKey(order, productId, transactionId);

            try
            {
                if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(transactionKey))
                    throw new InvalidOperationException("The store order did not contain verifiable product and transaction data.");

                if (confirmAfterValidation)
                    PublishResult(PurchaseStatus.Validating, productId, transactionId, null);

                IapProductDefinition definition = FindProductDefinition(productId);
                if (definition == null)
                    throw new InvalidOperationException("The store order product is not in the configured IAP catalog.");

                ReceiptValidationResult validation = await receiptValidator.ValidateReceiptAsync(
                    CreateReceiptPayload(order, productId, transactionId, transactionKey));
                if (validation == null || !validation.IsValid || !string.Equals(validation.ProductId, productId, StringComparison.Ordinal))
                    throw new InvalidOperationException(validation?.ErrorCode ?? "The backend rejected this purchase.");
                if (!string.IsNullOrEmpty(transactionId) && !string.IsNullOrEmpty(validation.TransactionId)
                    && !string.Equals(validation.TransactionId, transactionId, StringComparison.Ordinal))
                    throw new InvalidOperationException("The backend verified a different transaction.");
                if (validation.OwnedEntitlementIds == null || !ContainsAll(validation.OwnedEntitlementIds, definition.EntitlementIds))
                    throw new InvalidOperationException("The backend did not return the product's granted entitlements.");

                ReplaceOwnedEntitlements(validation.OwnedEntitlementIds);
                if (confirmAfterValidation)
                {
                    try
                    {
                        storeController.ConfirmPurchase((PendingOrder)order);
                    }
                    catch (Exception exception)
                    {
                        LastError = "Purchase was granted, but store confirmation will retry: " + exception.GetType().Name;
                    }

                    CompleteActivePurchaseForProduct(productId, PublishResult(PurchaseStatus.Success, productId, transactionId, null));
                }
                return true;
            }
            catch (Exception exception)
            {
                LastError = "IAP verification failed: " + exception.GetType().Name;
                if (confirmAfterValidation)
                    CompleteActivePurchaseForProduct(productId, PublishResult(PurchaseStatus.Failed, productId, transactionId, LastError));
                return false;
            }
        }

        private async Task FetchPurchasesAndWaitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            purchasesFetchCompletion = completion;
            try
            {
                storeController.FetchPurchases();
                bool allOrdersVerified = await completion.Task;
                cancellationToken.ThrowIfCancellationRequested();
                if (!allOrdersVerified)
                    throw new InvalidOperationException(LastError ?? "One or more restored purchases could not be verified.");
            }
            finally
            {
                if (purchasesFetchCompletion == completion)
                    purchasesFetchCompletion = null;
            }
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            string productId = GetSingleProductId(order);
            string transactionId = order?.Info?.TransactionID;
            PurchaseStatus status = order != null && (order.FailureReason == PurchaseFailureReason.UserCancelled || order.FailureReason == PurchaseFailureReason.OrderCancelled)
                ? PurchaseStatus.Cancelled
                : PurchaseStatus.Failed;
            CompleteActivePurchaseForProduct(productId, PublishResult(status, productId, transactionId, order?.FailureReason.ToString()));
        }

        private void OnPurchaseDeferred(DeferredOrder order)
        {
            string productId = GetSingleProductId(order);
            PurchaseResult result = new PurchaseResult
            {
                Status = PurchaseStatus.Pending,
                ProductId = productId,
                TransactionId = order?.Info?.TransactionID,
                IsDeferred = true
            };
            PurchaseUpdated?.Invoke(result);
            CompleteActivePurchaseForProduct(productId, result);
        }

        private async void OnAuthStateChanged(UserProfile user)
        {
            ReplaceOwnedEntitlements(Array.Empty<string>());
            storeAccountBinding = null;
            storeAccountBindingUid = null;
            if (user == null)
                return;

            try
            {
                await EnsureStoreAccountBindingAsync();
                await RefreshEntitlementsAsync();
                storeController?.FetchPurchases();
            }
            catch (Exception exception)
            {
                LastError = "IAP entitlement refresh failed: " + exception.GetType().Name;
            }
        }

        private async Task EnsureStoreAccountBindingAsync(CancellationToken cancellationToken = default)
        {
            if (!auth.IsAuthenticated || string.IsNullOrWhiteSpace(auth.CurrentUser?.UserId))
                throw new InvalidOperationException("A signed-in Firebase user is required to bind store purchases.");

            string uid = auth.CurrentUser.UserId;
            if (storeAccountBinding != null && string.Equals(storeAccountBindingUid, uid, StringComparison.Ordinal))
                return;

            IapAccountBinding binding = await receiptValidator.GetAccountBindingAsync(cancellationToken);
            if (binding == null || !Guid.TryParse(binding.AppleAppAccountToken, out Guid appleToken)
                || string.IsNullOrWhiteSpace(binding.GoogleObfuscatedAccountId))
                throw new InvalidOperationException("The IAP backend returned an invalid store account binding.");

#if UNITY_IOS && !UNITY_EDITOR
            if (storeController?.AppleStoreExtendedService == null)
                throw new InvalidOperationException("Unity IAP Apple account-binding support is unavailable.");
            storeController.AppleStoreExtendedService.SetAppAccountToken(appleToken);
#elif UNITY_ANDROID && !UNITY_EDITOR
            if (storeController?.GooglePlayStoreExtendedService == null)
                throw new InvalidOperationException("Unity IAP Google account-binding support is unavailable.");
            storeController.GooglePlayStoreExtendedService.SetObfuscatedAccountId(binding.GoogleObfuscatedAccountId);
#endif

            storeAccountBinding = binding;
            storeAccountBindingUid = uid;
        }

        private PurchaseReceiptPayload CreateReceiptPayload(Order order, string productId, string transactionId, string idempotencyKey)
        {
            string store = GetStoreName();
            string storeToken = order.Info.Google?.PurchaseToken
                ?? order.Info.Apple?.jwsRepresentation
                ?? order.Info.Apple?.AppReceipt;
            return new PurchaseReceiptPayload
            {
                ProductId = productId,
                Store = store,
                TransactionId = transactionId,
                StorePurchaseToken = storeToken,
                RawReceipt = order.Info.Receipt,
                IdempotencyKey = idempotencyKey
            };
        }

        private static string GetStoreName()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return "google_play";
#elif UNITY_IOS && !UNITY_EDITOR
            return "app_store";
#else
            return "editor";
#endif
        }

        private static string GetSingleProductId(Order order)
        {
            if (order?.CartOrdered == null)
                return null;
            IReadOnlyList<CartItem> items = order.CartOrdered.Items();
            return items.Count == 1 ? items[0].Product.definition.id : null;
        }

        private static string CreateIdempotencyKey(Order order, string productId, string transactionId)
        {
            string token = order?.Info?.Google?.PurchaseToken
                ?? order?.Info?.Apple?.jwsRepresentation
                ?? transactionId;
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(productId))
                return null;

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(GetStoreName() + ":" + productId + ":" + token));
                return string.Concat(hash.Select(value => value.ToString("x2")));
            }
        }

        private IapProductDefinition FindProductDefinition(string productId)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(productId))
                return null;
            for (int index = 0; index < catalog.Products.Count; index++)
            {
                if (string.Equals(catalog.Products[index].ProductId, productId, StringComparison.Ordinal))
                    return catalog.Products[index];
            }

            return null;
        }

        private void ReplaceOwnedEntitlements(IEnumerable<string> entitlementIds)
        {
            HashSet<string> allowedEntitlementIds = new HashSet<string>(StringComparer.Ordinal);
            if (catalog != null)
            {
                for (int productIndex = 0; productIndex < catalog.Products.Count; productIndex++)
                {
                    for (int entitlementIndex = 0; entitlementIndex < catalog.Products[productIndex].EntitlementIds.Count; entitlementIndex++)
                        allowedEntitlementIds.Add(catalog.Products[productIndex].EntitlementIds[entitlementIndex]);
                }
            }

            ownedEntitlementIds.Clear();
            if (entitlementIds != null)
            {
                foreach (string entitlementId in entitlementIds)
                {
                    if (allowedEntitlementIds.Contains(entitlementId))
                        ownedEntitlementIds.Add(entitlementId);
                }
            }

            EntitlementsChanged?.Invoke(OwnedEntitlementIds);
        }

        private static bool ContainsAll(IReadOnlyCollection<string> values, IReadOnlyList<string> requiredValues)
        {
            HashSet<string> valueSet = new HashSet<string>(values, StringComparer.Ordinal);
            for (int index = 0; index < requiredValues.Count; index++)
            {
                if (!valueSet.Contains(requiredValues[index]))
                    return false;
            }

            return true;
        }

        private PurchaseResult PublishResult(PurchaseStatus status, string productId, string transactionId, string error)
        {
            PurchaseResult result = new PurchaseResult
            {
                Status = status,
                ProductId = productId,
                TransactionId = transactionId,
                ErrorMessage = error
            };
            PurchaseUpdated?.Invoke(result);
            return result;
        }

        private void CompleteActivePurchase(PurchaseResult result)
        {
            activePurchase?.TrySetResult(result);
            activePurchase = null;
            activeProductId = null;
        }

        private void CompleteActivePurchaseForProduct(string productId, PurchaseResult result)
        {
            if (activePurchase == null || (!string.IsNullOrEmpty(productId) && !string.Equals(activeProductId, productId, StringComparison.Ordinal)))
                return;

            CompleteActivePurchase(result);
        }
    }
}