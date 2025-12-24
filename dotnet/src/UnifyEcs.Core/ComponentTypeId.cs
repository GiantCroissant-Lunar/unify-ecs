namespace UnifyECS
{
    /// <summary>
    /// Stable identifier for a component type (RFC-0009).
    /// </summary>
    public readonly record struct ComponentTypeId(int Id, string TypeName, ulong StructureHash);
}
