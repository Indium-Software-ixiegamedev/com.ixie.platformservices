using System.Collections.Generic;
using PlatformServices.Core;

namespace PlatformServices.Analytics
{
    public interface IAnalyticsService : IPlatformFeature
    {
        void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null);
        void SetUserId(string userId);
        void SetUserProperty(string propertyName, string propertyValue);
    }
}