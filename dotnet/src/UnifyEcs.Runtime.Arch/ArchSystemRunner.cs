using System;
using System.Collections.Generic;
using Arch.Core;

namespace UnifyECS
{
    /// <summary>
    /// Arch-specific implementation of ISystemRunner.
    ///
    /// This runner is bound to a single ArchWorld instance and executes
    /// registered IArchSystem systems against its underlying Arch.Core.World.
    ///
    /// Ordering, grouping, and feature handling are delegated to the
    /// generated systems and higher-level orchestration.
    /// </summary>
    public sealed class ArchSystemRunner : ISystemRunner
    {
        private readonly ArchWorld _archWorld;
        private readonly List<IArchSystem> _systems = new List<IArchSystem>();
        private readonly Dictionary<Type, IUnifySystem> _systemsByType = new Dictionary<Type, IUnifySystem>();
        private bool _initialized;
        private bool _disposed;

        public ArchSystemRunner(ArchWorld world)
        {
            _archWorld = world ?? throw new ArgumentNullException(nameof(world));
            World = world;
        }

        /// <inheritdoc />
        public IWorld World { get; }

        /// <summary>
        /// The underlying Arch.Core.World used by this runner.
        /// </summary>
        public World InnerWorld => _archWorld.InnerWorld;

        public void Register(IUnifySystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            ThrowIfDisposed();

            if (system is IArchSystem archSystem)
            {
                _systems.Add(archSystem);
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
                throw new InvalidOperationException("ArchSystemRunner is already initialized.");
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

            var world = _archWorld.InnerWorld;

            // Execute all registered Arch systems for this frame.
            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].Execute(world, deltaTime);
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
                throw new ObjectDisposedException(nameof(ArchSystemRunner));
            }
        }
    }
}
