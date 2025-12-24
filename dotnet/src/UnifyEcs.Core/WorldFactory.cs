using System;
using System.Collections.Generic;
using System.Linq;

namespace UnifyECS
{
    public interface IWorldFactory
    {
        EcsBackend Backend { get; }
        IWorld Create(WorldConfig config);
    }

    public static class WorldFactory
    {
        private static readonly Dictionary<EcsBackend, IWorldFactory> _factories = new Dictionary<EcsBackend, IWorldFactory>();

        public static void Register(EcsBackend backend, IWorldFactory factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _factories[backend] = factory;
        }

        public static IWorld Create(EcsBackend backend, WorldConfig? config = null)
        {
            if (!_factories.TryGetValue(backend, out var factory))
            {
                throw new InvalidOperationException($"Backend '{backend}' not registered. Ensure the backend assembly is referenced.");
            }

            return factory.Create(config ?? WorldConfig.Default);
        }

        public static IWorld Create(WorldConfig? config = null)
        {
            if (_factories.Count == 0)
            {
                throw new InvalidOperationException("No backends registered.");
            }

            var defaultBackend = _factories.Keys.First();
            return Create(defaultBackend, config);
        }
    }
}
