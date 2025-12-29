using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace UnifyECS
{
    /// <summary>
    /// Friflo-specific implementation of ISystemRunner.
    ///
    /// This runner is bound to a single FrifloWorld instance and executes
    /// registered IFrifloSystem systems against its underlying Friflo.Engine.ECS.EntityStore.
    ///
    /// Ordering, grouping, and feature handling are delegated to the
    /// generated systems and higher-level orchestration.
    /// </summary>
    public sealed class FrifloSystemRunner : ISystemRunner
    {
        private readonly FrifloWorld _frifloWorld;
        private readonly List<IFrifloSystem> _systems = new();
        private readonly Dictionary<Type, IUnifySystem> _systemsByType = new();
        private bool _initialized;
        private bool _disposed;

        public FrifloSystemRunner(FrifloWorld world)
        {
            _frifloWorld = world ?? throw new ArgumentNullException(nameof(world));
            World = world;
        }

        /// <inheritdoc />
        public IWorld World { get; }

        /// <summary>
        /// The underlying Friflo.Engine.ECS.EntityStore used by this runner.
        /// </summary>
        public EntityStore EntityStore => _frifloWorld.EntityStore;

        public void Register(IUnifySystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            ThrowIfDisposed();

            if (system is IFrifloSystem frifloSystem)
            {
                _systems.Add(frifloSystem);
                _systemsByType[system.GetType()] = system;
            }
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
                throw new InvalidOperationException("FrifloSystemRunner is already initialized.");
            }

            _initialized = true;

            // Dependency injection and more advanced lifecycle hooks are
            // handled by generated code or higher-level orchestration.
        }

        public void Update(float deltaTime)
        {
            ThrowIfDisposed();

            if (!_initialized)
            {
                throw new InvalidOperationException("Call Initialize() before Update().");
            }

            var entityStore = _frifloWorld.EntityStore;

            // Execute all registered Friflo systems for this frame.
            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].Execute(entityStore, deltaTime);
            }
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
                throw new ObjectDisposedException(nameof(FrifloSystemRunner));
            }
        }
    }
}
