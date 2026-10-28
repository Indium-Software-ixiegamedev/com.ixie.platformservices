using PlatformServices.AppCheck;
using PlatformServices.Iap;
using PlatformServices.Integrity;

namespace PlatformServices.Core
{
    /// <summary>Inputs shared with feature installers while the production platform is composed.</summary>
    public sealed class PlatformServiceOptions
    {
        public string GoogleWebClientId { get; set; }
        public bool EnableIntegrityCheck { get; set; } = true;
        public IntegrityConfiguration IntegrityConfiguration { get; set; }
        public AppCheckConfiguration AppCheckConfiguration { get; set; }
        public IapProductCatalog IapProductCatalog { get; set; }

        /// <summary>Developer-supplied validator consumed by the IAP installer; backend installers may only fill it in when null.</summary>
        public IReceiptValidator ReceiptValidator { get; set; }
    }
}
