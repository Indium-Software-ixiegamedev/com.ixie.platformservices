using System;

namespace PlatformServices.Core
{
    public interface IPlatformServiceLogger
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception exception = null);
    }
}