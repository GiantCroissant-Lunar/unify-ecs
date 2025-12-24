using System;
using System.Collections.Generic;

namespace UnifyECS
{
    public sealed class SystemRunner : ISystemRunner
    {
        private readonly List<IUnifySystem> _systems = new List<IUnifySystem>();
        private readonly Dictionary<Type, IUnifySystem> _systemsByType = new Dictionary<Type, IUnifySystem>();
        private bool _initialized;
        private bool _disposed;

        public SystemRunner(IWorld world)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
        }

        public IWorld World { get; }

        public void Register(IUnifySystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            ThrowIfDisposed();

            _systems.Add(system);
            _systemsByType[system.GetType()] = system;
        }

        public void Register(params IUnifySystem[] systems)
        {
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            foreach (var system in systems)
            {
                if (system != null)
                {
                    Register(system);
                }
            }
        }

        public T? GetSystem<T>() where T : class, IUnifySystem
        {
            ThrowIfDisposed();

            if (_systemsByType.TryGetValue(typeof(T), out var system))
            {
                return (T)system;
            }

            return null;
        }

        public void Initialize()
        {
            ThrowIfDisposed();

            if (_initialized)
            {
                throw new InvalidOperationException("SystemRunner is already initialized.");
            }

            _initialized = true;

            // Dependency injection and more advanced lifecycle hooks are
            // handled by generated code or backend-specific adapters.
        }

        public void Update(float deltaTime)
        {
            ThrowIfDisposed();

            if (!_initialized)
            {
                throw new InvalidOperationException("Call Initialize() before Update().");
            }

            // Backend-specific execution is handled by generated code or adapters.
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _systems.Clear();
            _systemsByType.Clear();
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SystemRunner));
            }
        }
    }
}
