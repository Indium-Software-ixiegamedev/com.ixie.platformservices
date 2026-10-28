using System;
using System.Threading;
using System.Threading.Tasks;
using PlatformServices.Core;

namespace PlatformServices.RemoteConfig
{
    public interface IRemoteConfigService : IPlatformFeature
    {
        Task<bool> FetchAndActivateAsync(TimeSpan minimumFetchInterval, CancellationToken cancellationToken = default);
        string GetString(string key, string defaultValue = "");
        int GetInt(string key, int defaultValue = 0);
        bool GetBool(string key, bool defaultValue = false);
        float GetFloat(string key, float defaultValue = 0.0f);
    }
}