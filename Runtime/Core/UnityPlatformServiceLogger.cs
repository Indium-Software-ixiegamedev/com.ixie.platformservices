using System;
using UnityEngine;

namespace PlatformServices.Core
{
    public sealed class UnityLogService : IPlatformServiceLogger
    {
        private const string Prefix = "[PlatformServices] ";

        public void LogInfo(string message)
        {
            Debug.Log(Prefix + message);
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        public void LogError(string message, Exception exception = null)
        {
            if (exception == null)
                Debug.LogError(Prefix + message);
            else
                Debug.LogError($"{Prefix}{message}\n{exception}");
        }
    }
}