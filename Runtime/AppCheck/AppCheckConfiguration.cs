namespace PlatformServices.AppCheck
{
    public sealed class AppCheckConfiguration
    {
        public bool RequireProviderOnDevice { get; set; } = true;
        public bool AllowUnconfiguredInEditor { get; set; } = true;
        public string AndroidProviderFactoryTypeName { get; set; }
        public string IosProviderFactoryTypeName { get; set; }
    }
}
