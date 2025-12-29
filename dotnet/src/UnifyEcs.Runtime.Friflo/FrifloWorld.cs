using System;
using System.Collections.Generic;
using System.Reflection;
using Friflo.Engine.ECS;

namespace UnifyECS
{
    /// <summary>
    /// Wrapper around Friflo.Engine.ECS.EntityStore implementing unified IWorld interface.
    /// Provides compatibility between UnifyECS.Entity and Friflo.Engine.ECS.Entity types.
    /// Note: Friflo.Engine.ECS requires components to be structs implementing IComponent.
    /// </summary>
    public sealed class FrifloWorld : IWorld
    {
        // Delegates for ref return support
        private delegate ref T GetComponentDelegate<T>(Friflo.Engine.ECS.Entity entity) where T : struct;
        private delegate bool TryGetComponentDelegate<T>(Friflo.Engine.ECS.Entity entity, out T component) where T : struct;
        private delegate void AddComponentDelegate<T>(Friflo.Engine.ECS.Entity entity, in T component) where T : struct;
        private delegate void AddDefaultComponentDelegate<T>(Friflo.Engine.ECS.Entity entity) where T : struct;
        private delegate bool HasComponentDelegate<T>(Friflo.Engine.ECS.Entity entity) where T : struct;
        private delegate void RemoveComponentDelegate<T>(Friflo.Engine.ECS.Entity entity) where T : struct;

        private static class FrifloOperations<T> where T : struct
        {
            public static readonly GetComponentDelegate<T> Get;
            public static readonly TryGetComponentDelegate<T> TryGet;
            public static readonly AddComponentDelegate<T> Add;
            public static readonly AddDefaultComponentDelegate<T> AddDefault;
            public static readonly HasComponentDelegate<T> Has;
            public static readonly RemoveComponentDelegate<T> Remove;

            static FrifloOperations()
            {
                var type = typeof(T);
                if (!typeof(IComponent).IsAssignableFrom(type))
                {
                    // If T does not implement IComponent, we cannot generate valid Friflo calls.
                    // We'll throw at runtime if these are accessed.
                    Get = _ => throw new NotSupportedException($"Type {type.Name} must implement IComponent to be used with Friflo backend.");
                    TryGet = (Friflo.Engine.ECS.Entity _, out T _) => throw new NotSupportedException($"Type {type.Name} must implement IComponent to be used with Friflo backend.");
                    Add = (Friflo.Engine.ECS.Entity _, in T _) => throw new NotSupportedException($"Type {type.Name} must implement IComponent to be used with Friflo backend.");
                    AddDefault = _ => throw new NotSupportedException($"Type {type.Name} must implement IComponent to be used with Friflo backend.");
                    Has = _ => throw new NotSupportedException($"Type {type.Name} must implement IComponent to be used with Friflo backend.");
                    Remove = _ => throw new NotSupportedException($"Type {type.Name} must implement IComponent to be used with Friflo backend.");
                    return;
                }

                // Expression parameters
                var entityParam = System.Linq.Expressions.Expression.Parameter(typeof(Friflo.Engine.ECS.Entity), "entity");
                var componentParam = System.Linq.Expressions.Expression.Parameter(typeof(T).MakeByRefType(), "component"); // in T
                var outComponentParam = System.Linq.Expressions.Expression.Parameter(typeof(T).MakeByRefType(), "outComponent"); // out T

                // --- Get ---
                var getMethod = typeof(Friflo.Engine.ECS.Entity).GetMethod(nameof(Friflo.Engine.ECS.Entity.GetComponent), Type.EmptyTypes)!.MakeGenericMethod(type);
                var getCall = System.Linq.Expressions.Expression.Call(entityParam, getMethod);
                Get = System.Linq.Expressions.Expression.Lambda<GetComponentDelegate<T>>(getCall, entityParam).Compile();

                // --- Add (in T) ---
                var addMethod = typeof(Friflo.Engine.ECS.Entity).GetMethod(nameof(Friflo.Engine.ECS.Entity.AddComponent), new[] { type })!.MakeGenericMethod(type);
                var addCall = System.Linq.Expressions.Expression.Call(entityParam, addMethod, componentParam);
                Add = System.Linq.Expressions.Expression.Lambda<AddComponentDelegate<T>>(addCall, entityParam, componentParam).Compile();

                // --- AddDefault ---
                var addDefaultMethod = typeof(Friflo.Engine.ECS.Entity).GetMethod(nameof(Friflo.Engine.ECS.Entity.AddComponent), Type.EmptyTypes)!.MakeGenericMethod(type);
                var addDefaultCall = System.Linq.Expressions.Expression.Call(entityParam, addDefaultMethod);
                AddDefault = System.Linq.Expressions.Expression.Lambda<AddDefaultComponentDelegate<T>>(addDefaultCall, entityParam).Compile();

                // --- Has ---
                var hasMethod = typeof(Friflo.Engine.ECS.Entity).GetMethod(nameof(Friflo.Engine.ECS.Entity.HasComponent), Type.EmptyTypes)!.MakeGenericMethod(type);
                var hasCall = System.Linq.Expressions.Expression.Call(entityParam, hasMethod);
                Has = System.Linq.Expressions.Expression.Lambda<HasComponentDelegate<T>>(hasCall, entityParam).Compile();

                // --- Remove ---
                var removeMethod = typeof(Friflo.Engine.ECS.Entity).GetMethod(nameof(Friflo.Engine.ECS.Entity.RemoveComponent), Type.EmptyTypes)!.MakeGenericMethod(type);
                var removeCall = System.Linq.Expressions.Expression.Call(entityParam, removeMethod);
                Remove = System.Linq.Expressions.Expression.Lambda<RemoveComponentDelegate<T>>(removeCall, entityParam).Compile();

                // --- TryGet ---
                // For TryGet, we manually implement a small wrapper since we can't easily emit "out" with simple Expressions without block/assignment.
                // Or we can rely on Has+Get. Let's rely on Has+Get to keep it simple, or implement via reflection if needed.
                // Actually, Friflo doesn't seem to have TryGetComponent(out T). It works by checking Has or nullable? 
                // Wait, Entity.TryGetComponent<T>(out T component) exists?
                // Checking Friflo docs/API via what we saw... Friflo docs usually say "TryGetComponent".
                // Let's assume it exists. If not, we fallback to Has+Get.
                var tryGetMethod = typeof(Friflo.Engine.ECS.Entity).GetMethod("TryGetComponent", new[] { typeof(T).MakeByRefType() });
                if (tryGetMethod != null)
                {
                     tryGetMethod = tryGetMethod.MakeGenericMethod(type);
                     var tryGetCall = System.Linq.Expressions.Expression.Call(entityParam, tryGetMethod, outComponentParam);
                     TryGet = System.Linq.Expressions.Expression.Lambda<TryGetComponentDelegate<T>>(tryGetCall, entityParam, outComponentParam).Compile();
                }
                else
                {
                    // Fallback
                    TryGet = (Friflo.Engine.ECS.Entity e, out T c) =>
                    {
                        if (FrifloOperations<T>.Has(e))
                        {
                            c = FrifloOperations<T>.Get(e);
                            return true;
                        }
                        c = default;
                        return false;
                    };
                }
            }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Action<Friflo.Engine.ECS.Entity, object>> s_addDelegates = new();

        private readonly EntityStore _entityStore;
        private readonly Dictionary<int, Friflo.Engine.ECS.Entity> _idToFrifloEntity = new();
        private readonly Dictionary<int, UnifyECS.Entity> _idToUnifyMap = new();
        private int _nextEntityId = 1;

        internal FrifloWorld(EntityStore entityStore)
        {
            _entityStore = entityStore ?? throw new ArgumentNullException(nameof(entityStore));
        }

        public EntityStore EntityStore => _entityStore;

        public int EntityCount => _idToFrifloEntity.Count;

        public UnifyECS.Entity CreateEntity()
        {
            var frifloEntity = _entityStore.CreateEntity();
            var entityId = _nextEntityId++;
            _idToFrifloEntity[entityId] = frifloEntity;
            var entity = new UnifyECS.Entity(entityId, 1);
            _idToUnifyMap[entityId] = entity;
            return entity;
        }

        public UnifyECS.Entity CreateEntity(params object[] components)
        {
            if (components is null || components.Length == 0)
            {
                return CreateEntity();
            }

            var frifloEntity = _entityStore.CreateEntity();
            var entityId = _nextEntityId++;
            _idToFrifloEntity[entityId] = frifloEntity;
            var entity = new UnifyECS.Entity(entityId, 1);
            _idToUnifyMap[entityId] = entity;

            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    throw new ArgumentNullException(nameof(components), $"Component at index {i} is null");
                }
                
                AddComponentFast(frifloEntity, component);
            }

            return entity;
        }

        private static void AddComponentFast(Friflo.Engine.ECS.Entity entity, object component)
        {
            var type = component.GetType();
            var action = s_addDelegates.GetOrAdd(type, CreateAddDelegate);
            action(entity, component);
        }

        private static Action<Friflo.Engine.ECS.Entity, object> CreateAddDelegate(Type type)
        {
            if (!type.IsValueType || !typeof(IComponent).IsAssignableFrom(type))
            {
                throw new NotSupportedException($"Friflo.Engine.ECS requires components to be structs implementing IComponent. Component type {type.Name} is not valid.");
            }

            // (Entity e, object o) => e.AddComponent((T)o)
            var paramEntity = System.Linq.Expressions.Expression.Parameter(typeof(Friflo.Engine.ECS.Entity), "e");
            var paramObj = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
            
            var method = typeof(Friflo.Engine.ECS.Entity)
                .GetMethod(nameof(Friflo.Engine.ECS.Entity.AddComponent), BindingFlags.Public | BindingFlags.Instance);
                
            if (method == null)
                throw new InvalidOperationException("Could not find AddComponent method on Friflo Entity.");

            var genericMethod = method.MakeGenericMethod(type);
            var castObj = System.Linq.Expressions.Expression.Convert(paramObj, type);
            var call = System.Linq.Expressions.Expression.Call(paramEntity, genericMethod, castObj);
            
            return System.Linq.Expressions.Expression.Lambda<Action<Friflo.Engine.ECS.Entity, object>>(call, paramEntity, paramObj).Compile();
        }

        public bool Exists(UnifyECS.Entity entity)
        {
            return _idToFrifloEntity.ContainsKey(entity.Id);
        }

        public void DestroyEntity(UnifyECS.Entity entity)
        {
            if (!_idToFrifloEntity.TryGetValue(entity.Id, out var frifloEntity))
            {
                throw new InvalidOperationException($"Entity {entity.Id} does not belong to this FrifloWorld or has been destroyed.");
            }
            
            _idToFrifloEntity.Remove(entity.Id);
            _idToUnifyMap.Remove(entity.Id);
        }

        ref T IWorld.Add<T>(UnifyECS.Entity entity, T component)
        {
            var frifloEntity = ToFriflo(entity);
            FrifloOperations<T>.Add(frifloEntity, in component);
            return ref FrifloOperations<T>.Get(frifloEntity);
        }

        ref T IWorld.Add<T>(UnifyECS.Entity entity)
        {
            var frifloEntity = ToFriflo(entity);
            FrifloOperations<T>.AddDefault(frifloEntity);
            return ref FrifloOperations<T>.Get(frifloEntity);
        }

        bool IWorld.Has<T>(UnifyECS.Entity entity)
        {
            var frifloEntity = ToFriflo(entity);
            return FrifloOperations<T>.Has(frifloEntity);
        }

        ref T IWorld.Get<T>(UnifyECS.Entity entity)
        {
            var frifloEntity = ToFriflo(entity);
            return ref FrifloOperations<T>.Get(frifloEntity);
        }

        bool IWorld.TryGet<T>(UnifyECS.Entity entity, out T component)
        {
            var frifloEntity = ToFriflo(entity);
            return FrifloOperations<T>.TryGet(frifloEntity, out component);
        }

        void IWorld.Remove<T>(UnifyECS.Entity entity)
        {
            var frifloEntity = ToFriflo(entity);
            FrifloOperations<T>.Remove(frifloEntity);
        }

        public void Dispose()
        {
            _idToFrifloEntity.Clear();
            _idToUnifyMap.Clear();
        }

        private Friflo.Engine.ECS.Entity ToFriflo(UnifyECS.Entity entity)
        {
            if (_idToFrifloEntity.TryGetValue(entity.Id, out var frifloEntity))
            {
                return frifloEntity;
            }

            throw new InvalidOperationException($"Entity {entity.Id} does not belong to this FrifloWorld or has been destroyed.");
        }

        public static UnifyECS.Entity MapEntity(Friflo.Engine.ECS.Entity frifloEntity)
        {
            return new UnifyECS.Entity(frifloEntity.Id, 1);
        }
    }

}
