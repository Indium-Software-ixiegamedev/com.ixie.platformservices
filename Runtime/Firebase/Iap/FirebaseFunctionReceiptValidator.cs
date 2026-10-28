using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Functions;
using PlatformServices.Firebase;

namespace PlatformServices.Iap
{
    /// <summary>
    /// Calls the trusted Firebase callable endpoints for transaction verification and entitlement lookup.
    /// The server must enforce Firebase Auth and App Check and verify transactions with Apple or Google.
    /// </summary>
    public sealed class FirebaseFunctionReceiptValidator : IReceiptValidator
    {
        private readonly IFirebaseService firebase;
        private readonly string accountBindingFunctionName;
        private readonly string validateFunctionName;
        private readonly string entitlementsFunctionName;

        /// <summary>Creates a callable client using the configured backend function names.</summary>
        public FirebaseFunctionReceiptValidator(
            IFirebaseService firebase,
            string accountBindingFunctionName = "getIapAccountBinding",
            string validateFunctionName = "validateIapPurchase",
            string entitlementsFunctionName = "getIapEntitlements")
        {
            this.firebase = firebase ?? throw new ArgumentNullException(nameof(firebase));
            this.accountBindingFunctionName = accountBindingFunctionName;
            this.validateFunctionName = validateFunctionName;
            this.entitlementsFunctionName = entitlementsFunctionName;
        }

        /// <summary>Gets stable Apple and Google account bindings for the signed-in Firebase caller.</summary>
        public async Task<IapAccountBinding> GetAccountBindingAsync(CancellationToken cancellationToken = default)
        {
            if (!firebase.IsAvailable)
                throw new InvalidOperationException("Firebase is unavailable for IAP account binding.");

            cancellationToken.ThrowIfCancellationRequested();
            HttpsCallableResult callableResult = await GetFunctions()
                .GetHttpsCallable(accountBindingFunctionName)
                .CallAsync(new Dictionary<string, object>());
            cancellationToken.ThrowIfCancellationRequested();

            IDictionary<string, object> data = AsDictionary(callableResult.Data);
            string appleToken = GetString(data, "appleAppAccountToken");
            string googleAccountId = GetString(data, "googleObfuscatedAccountId");
            if (!Guid.TryParse(appleToken, out _) || string.IsNullOrWhiteSpace(googleAccountId))
                throw new InvalidOperationException("IAP backend returned an invalid account binding.");

            return new IapAccountBinding
            {
                AppleAppAccountToken = appleToken,
                GoogleObfuscatedAccountId = googleAccountId
            };
        }

        /// <summary>Submits transient store proof to the backend and parses its verified fulfillment response.</summary>
        public async Task<ReceiptValidationResult> ValidateReceiptAsync(
            PurchaseReceiptPayload receipt,
            CancellationToken cancellationToken = default)
        {
            if (receipt == null)
                throw new ArgumentNullException(nameof(receipt));
            if (!firebase.IsAvailable)
                throw new InvalidOperationException("Firebase is unavailable for IAP verification.");

            cancellationToken.ThrowIfCancellationRequested();
            Dictionary<string, object> request = new Dictionary<string, object>
            {
                ["productId"] = receipt.ProductId,
                ["store"] = receipt.Store,
                ["transactionId"] = receipt.TransactionId,
                ["storePurchaseToken"] = receipt.StorePurchaseToken,
                ["rawReceipt"] = receipt.RawReceipt,
                ["idempotencyKey"] = receipt.IdempotencyKey
            };

            HttpsCallableResult callableResult = await GetFunctions()
                .GetHttpsCallable(validateFunctionName)
                .CallAsync(request);
            cancellationToken.ThrowIfCancellationRequested();

            IDictionary<string, object> data = AsDictionary(callableResult.Data);
            return new ReceiptValidationResult
            {
                IsValid = GetBoolean(data, "valid"),
                ProductId = GetString(data, "productId"),
                TransactionId = GetString(data, "transactionId"),
                ErrorCode = GetString(data, "errorCode"),
                OwnedEntitlementIds = GetStringList(data, "ownedEntitlementIds")
            };
        }

        /// <summary>Fetches the entitlement snapshot for the Firebase-authenticated caller.</summary>
        public async Task<IReadOnlyCollection<string>> GetOwnedEntitlementIdsAsync(
            CancellationToken cancellationToken = default)
        {
            if (!firebase.IsAvailable)
                throw new InvalidOperationException("Firebase is unavailable for IAP entitlement lookup.");

            cancellationToken.ThrowIfCancellationRequested();
            HttpsCallableResult callableResult = await GetFunctions()
                .GetHttpsCallable(entitlementsFunctionName)
                .CallAsync(new Dictionary<string, object>());
            cancellationToken.ThrowIfCancellationRequested();

            return GetStringList(AsDictionary(callableResult.Data), "ownedEntitlementIds");
        }

        private FirebaseFunctions GetFunctions()
        {
            return FirebaseFunctions.GetInstance(firebase.App);
        }

        private static IDictionary<string, object> AsDictionary(object value)
        {
            if (value is IDictionary<string, object> dictionary)
                return dictionary;

            throw new InvalidOperationException("IAP backend returned an invalid response.");
        }

        private static string GetString(IDictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out object value) ? value as string : null;
        }

        private static bool GetBoolean(IDictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out object value) && value is bool result && result;
        }

        private static IReadOnlyCollection<string> GetStringList(IDictionary<string, object> data, string key)
        {
            List<string> values = new List<string>();
            if (!data.TryGetValue(key, out object value) || !(value is IEnumerable items))
                return values;

            foreach (object item in items)
            {
                if (item is string text && !string.IsNullOrWhiteSpace(text))
                    values.Add(text);
            }

            return values;
        }
    }
}