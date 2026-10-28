using System.Threading;
using System.Threading.Tasks;
using PlatformServices.Core;

namespace PlatformServices.Consent
{
    public enum ConsentStatus
    {
        Unknown,
        NotRequired,
        RequiredAndObtained,
        RequiredAndDenied
    }

    public interface IConsentService : IPlatformFeature
    {
        ConsentStatus CurrentStatus { get; }
        Task<ConsentStatus> RequestConsentAsync(bool underAgeOfConsent = false, CancellationToken cancellationToken = default);
        Task ShowPrivacyOptionsFormAsync(CancellationToken cancellationToken = default);
    }
}