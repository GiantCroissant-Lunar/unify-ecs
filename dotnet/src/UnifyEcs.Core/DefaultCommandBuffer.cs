using System;
using System.Collections.Generic;

namespace UnifyECS
{
    /// <summary>
    /// Minimal ICommandBuffer implementation that records structural commands
    /// and replays them against an IWorld in insertion order.
    /// </summary>
    public sealed class DefaultCommandBuffer : ICommandBuffer
    {
        private interface ICommandImpl
        {
            void Execute(IWorld world, Dictionary<Entity, Entity> remap);
        }

        private sealed class CreateEntityCommand : ICommandImpl
        {
            private readonly Entity _tempEntity;
            private readonly object[] _components;

            public CreateEntityCommand(Entity tempEntity, object[] components)
            {
                _tempEntity = tempEntity;
                _components = components;
            }

            public void Execute(IWorld world, Dictionary<Entity, Entity> remap)
            {
                Entity real;
                if (_components.Length == 0)
                {
                    real = world.CreateEntity();
                }
                else
                {
                    real = world.CreateEntity(_components);
                }

                remap[_tempEntity] = real;
            }
        }

        private sealed class DestroyEntityCommand : ICommandImpl
        {
            private readonly Entity _entity;

            public DestroyEntityCommand(Entity entity)
            {
                _entity = entity;
            }

            public void Execute(IWorld world, Dictionary<Entity, Entity> remap)
            {
                var e = Remap(_entity, world, remap);
                world.DestroyEntity(e);
            }
        }

        private sealed class AddComponentCommand<T> : ICommandImpl where T : struct
        {
            private readonly Entity _entity;
            private readonly T _component;

            public AddComponentCommand(Entity entity, T component)
            {
                _entity = entity;
                _component = component;
            }

            public void Execute(IWorld world, Dictionary<Entity, Entity> remap)
            {
                var e = Remap(_entity, world, remap);
                world.Add(e, _component);
            }
        }

        private sealed class AddTagCommand<T> : ICommandImpl where T : struct
        {
            private readonly Entity _entity;

            public AddTagCommand(Entity entity)
            {
                _entity = entity;
            }

            public void Execute(IWorld world, Dictionary<Entity, Entity> remap)
            {
                var e = Remap(_entity, world, remap);
                world.Add<T>(e);
            }
        }

        private sealed class RemoveComponentCommand<T> : ICommandImpl where T : struct
        {
            private readonly Entity _entity;

            public RemoveComponentCommand(Entity entity)
            {
                _entity = entity;
            }

            public void Execute(IWorld world, Dictionary<Entity, Entity> remap)
            {
                var e = Remap(_entity, world, remap);
                world.Remove<T>(e);
            }
        }

        private sealed class SetComponentCommand<T> : ICommandImpl where T : struct
        {
            private readonly Entity _entity;
            private readonly T _component;

            public SetComponentCommand(Entity entity, T component)
            {
                _entity = entity;
                _component = component;
            }

            public void Execute(IWorld world, Dictionary<Entity, Entity> remap)
            {
                var e = Remap(_entity, world, remap);
                // IWorld does not currently expose a Set<T> API; use Add<T>(entity, component)
                // which in most backends behaves as "add or replace".
                world.Add(e, _component);
            }
        }

        private readonly object _gate = new object();
        private readonly List<ICommandImpl> _commands = new List<ICommandImpl>();
        private bool _disposed;
        private int _nextTempId = 1;

        public int CommandCount
        {
            get
            {
                lock (_gate)
                {
                    return _commands.Count;
                }
            }
        }

        public Entity CreateEntity()
        {
            lock (_gate)
            {
                EnsureNotDisposed();

                var temp = CreateTemporaryEntity();
                _commands.Add(new CreateEntityCommand(temp, Array.Empty<object>()));
                return temp;
            }
        }

        public Entity CreateEntity(params object[] components)
        {
            lock (_gate)
            {
                EnsureNotDisposed();

                components ??= Array.Empty<object>();
                var temp = CreateTemporaryEntity();
                _commands.Add(new CreateEntityCommand(temp, components));
                return temp;
            }
        }

        public void DestroyEntity(Entity entity)
        {
            lock (_gate)
            {
                EnsureNotDisposed();
                _commands.Add(new DestroyEntityCommand(entity));
            }
        }

        public void Add<T>(Entity entity, T component) where T : struct
        {
            lock (_gate)
            {
                EnsureNotDisposed();
                _commands.Add(new AddComponentCommand<T>(entity, component));
            }
        }

        public void Add<T>(Entity entity) where T : struct
        {
            lock (_gate)
            {
                EnsureNotDisposed();
                _commands.Add(new AddTagCommand<T>(entity));
            }
        }

        public void Remove<T>(Entity entity) where T : struct
        {
            lock (_gate)
            {
                EnsureNotDisposed();
                _commands.Add(new RemoveComponentCommand<T>(entity));
            }
        }

        public void Set<T>(Entity entity, T component) where T : struct
        {
            lock (_gate)
            {
                EnsureNotDisposed();
                _commands.Add(new SetComponentCommand<T>(entity, component));
            }
        }

        public void Playback(IWorld world)
        {
            lock (_gate)
            {
                EnsureNotDisposed();
                if (world is null) throw new ArgumentNullException(nameof(world));

                var remap = new Dictionary<Entity, Entity>();

                foreach (var command in _commands)
                {
                    command.Execute(world, remap);
                }
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                EnsureNotDisposed();
                _commands.Clear();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _commands.Clear();
                _disposed = true;
            }
        }

        private static Entity Remap(Entity entity, IWorld world, Dictionary<Entity, Entity> remap)
        {
            if (remap.TryGetValue(entity, out var mapped))
            {
                return mapped;
            }

            if (entity.Id < 0)
            {
                mapped = world.CreateEntity();
                remap[entity] = mapped;
                return mapped;
            }

            return entity;
        }

        private Entity CreateTemporaryEntity()
        {
            // Use a negative Id to avoid colliding with real world entities.
            var id = -_nextTempId;
            _nextTempId++;
            return new Entity(id, 0);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DefaultCommandBuffer));
            }
        }
    }
}
