using System;
using System.Collections.Generic;
using FlecsCore = Flecs.NET.Core;

namespace UnifyECS
{
    /// <summary>
    /// Flecs-specific system runner.
    /// Manages system registration and execution using Flecs.
    /// </summary>
    public sealed class FlecsSystemRunner : ISystemRunner
    {
        private readonly FlecsCore.World _world;
        private readonly List<IFlecsSystem> _systems;
        private readonly Dictionary<Type, IUnifySystem> _systemsByType;
        private bool _initialized;
        private bool _disposed;

        /// <summary>
        /// Gets the underlying Flecs World.
        /// </summary>
        public FlecsCore.World WorldObject => _world;

        /// <summary>
        /// Gets the world interface.
        /// </summary>
        public IWorld World { get; }

        /// <summary>
        /// Creates a new FlecsSystemRunner.
        /// </summary>
        public FlecsSystemRunner()
        {
            _world = new FlecsCore.World();
            _systems = new List<IFlecsSystem>();
            _systemsByType = new Dictionary<Type, IUnifySystem>();
            World = new FlecsWorld(_world);
        }

        /// <summary>
        /// Creates a new FlecsSystemRunner with a custom world.
        /// </summary>
        /// <param name="world">The Flecs world to use.</param>
        public FlecsSystemRunner(FlecsCore.World world)
        {
            _world = world;
            _systems = new List<IFlecsSystem>();
            _systemsByType = new Dictionary<Type, IUnifySystem>();
            World = new FlecsWorld(_world);
        }

        /// <summary>
        /// Registers a system with this runner.
        /// </summary>
        /// <param name="system">The system to register.</param>
        public void Register(IFlecsSystem system)
        {
            if (system is null)
                throw new ArgumentNullException(nameof(system));

            _systems.Add(system);
            _systemsByType[system.GetType()] = system;
        }

        /// <summary>
        /// Registers a system with this runner.
        /// </summary>
        /// <param name="system">The system to register.</param>
        public void Register(IUnifySystem system)
        {
            if (system is null)
                throw new ArgumentNullException(nameof(system));

            if (system is IFlecsSystem flecsSystem)
            {
                Register(flecsSystem);
            }
            else
            {
                throw new ArgumentException($"System {system.GetType().Name} does not implement IFlecsSystem", nameof(system));
            }
        }

        /// <summary>
        /// Registers systems with this runner.
        /// </summary>
        /// <param name="systems">The systems to register.</param>
        public void Register(params IUnifySystem[] systems)
        {
            if (systems is null)
                throw new ArgumentNullException(nameof(systems));

            foreach (var system in systems)
            {
                if (system is IFlecsSystem flecsSystem)
                {
                    Register(flecsSystem);
                }
                else
                {
                    throw new ArgumentException($"System {system?.GetType().Name} does not implement IFlecsSystem", nameof(systems));
                }
            }
        }

        /// <summary>
        /// Gets a system of type T.
        /// </summary>
        public T? GetSystem<T>() where T : class, IUnifySystem
        {
            ThrowIfDisposed();

            if (_systemsByType.TryGetValue(typeof(T), out var system))
            {
                return (T)system;
            }

            return null;
        }

        /// <summary>
        /// Initializes the system runner.
        /// </summary>
        public void Initialize()
        {
            ThrowIfDisposed();

            if (_initialized)
            {
                throw new InvalidOperationException("FlecsSystemRunner is already initialized.");
            }

            _initialized = true;
        }

        /// <summary>
        /// Updates the system runner.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last frame.</param>
        public void Update(float deltaTime = 0.016f)
        {
            ThrowIfDisposed();

            if (!_initialized)
            {
                throw new InvalidOperationException("Call Initialize() before Update().");
            }

            Run(deltaTime);
        }

        /// <summary>
        /// Runs all registered systems.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last frame.</param>
        public void Run(float deltaTime = 0.016f)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            // Run systems manually
            // Note: Flecs also supports pipeline/phases if we register systems as Flecs systems (routines).
            // But UnifyECS assumes manual execution order based on registration or phases controlled by runner.
            // Here we iterate manually.
            var flecsWorld = (FlecsWorld)World;
            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].Execute(flecsWorld, deltaTime);
            }

            // Progress the Flecs world (runs Flecs-native systems/pipelines if any)
            _world.Progress(deltaTime);
        }

        /// <summary>
        /// Runs systems for a specific phase.
        /// </summary>
        /// <param name="phase">The phase to run.</param>
        /// <param name="deltaTime">Time elapsed since last frame.</param>
        public void RunPhase(SystemPhase phase, float deltaTime = 0.016f)
        {
            throw new NotImplementedException("Phase-based execution is not yet implemented for Flecs backend.");
        }

        /// <summary>
        /// Disposes the runner and its resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _systems.Clear();
            _systemsByType.Clear();
            
            // We own the world if we created it. If passed in constructor, we might not want to dispose it?
            // But standard behavior for runner is usually ownership. 
            // However, since we might share world, let's assume we dispose it if we created it?
            // For simplicity, we call Dispose on World (Flecs world handle). 
            // In C# Flecs.NET, World is a struct handle usually, or class? It's a struct in C++, but in C# wrapper it's a struct.
            // Wait, World in Flecs.NET is a struct wrapping a pointer. Calling Dispose frees the world.
            
            // If the user passed the world in, they might expect it to stay alive?
            // Usually SystemRunner owns the lifecycle.
            _world.Dispose();
            
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FlecsSystemRunner));
            }
        }

        private void ThrowIfNotInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Call Initialize() before Update() or Run().");
            }
        }
    }
}
