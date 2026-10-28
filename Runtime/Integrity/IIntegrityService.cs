using PlatformServices.Core;

namespace PlatformServices.Integrity
{
    public interface IIntegrityService : IPlatformFeature
    {
        string FailureReason { get; }
        bool IsTrusted { get; }
    }
}
