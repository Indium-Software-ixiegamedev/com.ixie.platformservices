using System;

namespace PlatformServices.Core
{
    public interface IPlatformMainThreadDispatcher
    {
        bool IsMainThread { get; }
        void Post(Action action);
    }
}