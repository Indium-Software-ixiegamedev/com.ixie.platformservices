using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PlatformServices.Core
{
    public sealed class PlatformServiceCore : IPlatformService
    {
        private readonly SemaphoreSlim lifecycleLock = new SemaphoreSlim(1, 1);
        private readonly IPlatformServiceLogger logger;
        private bool isDisposed;

        // The list defines initialization order; the dictionary caches resolved lookups by requested type.
        private readonly List<IPlatformFeature> features = new List<IPlatformFeature>();
        private readonly Dictionary<Type, IPlatformFeature> lookupCache = new Dictionary<Type, IPlatformFeature>();

        public bool IsInitialized => State == PlatformServiceState.Ready;
        public PlatformServiceState State { get; private set; } = PlatformServiceState.NotStarted;
        public event Action<PlatformServiceState> StateChanged;

        public PlatformServiceCore(IPlatformServiceLogger logger = null)
        {
            this.logger = logger;
        }

        public void RegisterFeature(IPlatformFeature feature)
        {
            if (feature == null)
                throw new ArgumentNullException(nameof(feature));
            if (State != PlatformServiceState.NotStarted && State != PlatformServiceState.Failed)
                throw new InvalidOperationException("Features must be registered before platform initialization.");
            if (features.Contains(feature))
                return;

            features.Add(feature);
            lookupCache.Clear();
        }

        public bool HasFeature<T>() where T : class, IPlatformFeature
        {
            return GetFeature<T>() != null;
        }

        public T GetFeature<T>() where T : class, IPlatformFeature
        {
            if (lookupCache.TryGetValue(typeof(T), out IPlatformFeature cached))
                return (T)cached;

            for (int index = 0; index < features.Count; index++)
            {
                if (features[index] is T feature)
                {
                    lookupCache[typeof(T)] = feature;
                    return feature;
                }
            }

            return null;
        }

        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            await lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (State == PlatformServiceState.Ready)
                    return true;
                if (isDisposed || State == PlatformServiceState.ShuttingDown || State == PlatformServiceState.Shutdown)
                    return false;

                SetState(PlatformServiceState.Initializing);
                logger?.LogInfo($"Initializing {features.Count} registered feature(s).");

                for (int index = 0; index < features.Count; index++)
                {
                    IPlatformFeature feature = features[index];
                    string featureName = feature.GetType().Name;

                    try
                    {
                        logger?.LogInfo($"Initializing feature '{featureName}' ({index + 1}/{features.Count}).");
                        if (!await InitializeRequiredAsync(feature, cancellationToken).ConfigureAwait(false))
                        {
                            logger?.LogError($"Feature '{featureName}' returned false during initialization.");
                            SetState(PlatformServiceState.Failed);
                            return false;
                        }

                        logger?.LogInfo($"Feature '{featureName}' initialized successfully.");
                    }
                    catch (OperationCanceledException)
                    {
                        logger?.LogWarning($"Feature '{featureName}' initialization was cancelled.");
                        throw;
                    }
                    catch (Exception exception)
                    {
                        logger?.LogError($"Feature '{featureName}' threw during initialization.", exception);
                        SetState(PlatformServiceState.Failed);
                        return false;
                    }
                }

                SetState(PlatformServiceState.Ready);
                logger?.LogInfo("All registered features initialized successfully.");
                return true;
            }
            catch (OperationCanceledException)
            {
                if (State == PlatformServiceState.Initializing)
                    SetState(PlatformServiceState.Failed);
                throw;
            }
            catch (Exception exception)
            {
                logger?.LogError("Platform initialization failed outside feature initialization.", exception);
                SetState(PlatformServiceState.Failed);
                return false;
            }
            finally
            {
                lifecycleLock.Release();
            }
        }

        public async Task<bool> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            await lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (State == PlatformServiceState.Shutdown)
                    return true;

                SetState(PlatformServiceState.ShuttingDown);
                for (int index = features.Count - 1; index >= 0; index--)
                    DisposeFeature(features[index], logger);
                SetState(PlatformServiceState.Shutdown);
                return true;
            }
            finally
            {
                lifecycleLock.Release();
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            ShutdownAsync().GetAwaiter().GetResult();
            lifecycleLock.Dispose();
        }

        private static async Task<bool> InitializeRequiredAsync(IPlatformFeature feature, CancellationToken cancellationToken)
        {
            return feature.IsInitialized || await feature.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void DisposeFeature(IPlatformFeature feature, IPlatformServiceLogger logger)
        {
            try
            {
                feature.Dispose();
            }
            catch (Exception exception)
            {
                logger?.LogError($"Feature '{feature.GetType().Name}' threw during disposal.", exception);
            }
        }

        private void SetState(PlatformServiceState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}