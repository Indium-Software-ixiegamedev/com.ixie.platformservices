using PlatformServices.AppCheck;
using PlatformServices.Auth;
using PlatformServices.Core;
using UnityEngine;
using UnityEngine.Purchasing;

namespace PlatformServices.Iap
{
    /// <summary>Registers the Unity IAP store service; discovered by the core through its installer registry.</summary>
    internal sealed class UnityIapPlatformFeatureInstaller : IPlatformFeatureInstaller
    {
        public int Order => 200;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            PlatformFeatureInstallerRegistry.Register(new UnityIapPlatformFeatureInstaller());
        }

        public void Install(IPlatformService platform, PlatformServiceOptions options)
        {
            if (options.IapProductCatalog == null)
            {
                Debug.LogWarning("[PlatformServices] IAP is enabled but no IapProductCatalog was supplied; IAP was not registered.");
                return;
            }

            if (options.ReceiptValidator == null)
            {
                Debug.LogError("[PlatformServices] IAP needs a receipt validator, which is provided by the Firebase integration; IAP was not registered.");
                return;
            }

            platform.RegisterFeature(new UnityIapService(
                options.IapProductCatalog,
                platform.GetFeature<IAuthService>(),
                platform.GetFeature<IAppCheckService>(),
                options.ReceiptValidator));
        }
    }

    internal static class IapProductDefinitionExtensions
    {
        public static ProductDefinition ToUnityDefinition(this IapProductDefinition definition)
        {
            return new ProductDefinition(definition.ProductId, definition.GetStoreProductId(), ProductType.NonConsumable);
        }
    }
}
