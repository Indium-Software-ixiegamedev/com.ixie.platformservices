namespace PlatformServices.Core
{
    /// <summary>Registers optional features from assemblies the core does not reference directly.</summary>
    public interface IPlatformFeatureInstaller
    {
        /// <summary>Gets the install order; lower values register (and therefore initialize) first.</summary>
        int Order { get; }

        void Install(IPlatformService platform, PlatformServiceOptions options);
    }
}
