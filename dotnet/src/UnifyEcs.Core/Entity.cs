using System;

namespace UnifyECS
{
    /// <summary>
    /// Opaque handle to an entity. Backend-agnostic.
    /// </summary>
    public readonly struct Entity : IEquatable<Entity>
    {
        /// <summary>
        /// Unique identifier within a world. -1 indicates an invalid/null entity.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Optional generation/version counter to detect stale references.
        /// </summary>
        public int Generation { get; }

        /// <summary>
        /// Returns true if this is a valid entity reference.
        /// </summary>
        public bool IsValid => Id >= 0;

        /// <summary>
        /// Null/invalid entity constant.
        /// </summary>
        public static readonly Entity Null = new Entity(-1, 0);

        public Entity(int id, int generation = 0)
        {
            Id = id;
            Generation = generation;
        }

        public bool Equals(Entity other) => Id == other.Id && Generation == other.Generation;

        public override bool Equals(object? obj) => obj is Entity other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Id, Generation);

        public static bool operator ==(Entity left, Entity right) => left.Equals(right);

        public static bool operator !=(Entity left, Entity right) => !left.Equals(right);

        public override string ToString() => IsValid ? $"Entity({Id}:{Generation})" : "Entity.Null";
    }
}
