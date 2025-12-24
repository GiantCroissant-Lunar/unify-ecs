using Microsoft.CodeAnalysis;
using UnifyECS;

namespace UnifyECS.Generators
{
    /// <summary>
    /// High-level description of an [EcsComponent]-annotated struct.
    /// This is a lightweight, serializable view used by emitters.
    /// </summary>
    internal sealed record ComponentModel(
        string Name,
        string Namespace,
        string FullName,
        Accessibility Accessibility,
        bool IsPartial,
        bool IsTag,
        ulong StructureHash,
        Location Location);

    /// <summary>
    /// Description of a [Query]-annotated method within a system, including
    /// structural change metadata.
    /// </summary>
    internal sealed record QueryModel(
        string MethodName,
        string ReturnTypeName,
        string[] ParameterTypeNames,
        string[] ParameterNames,
        RefKind[] ParameterRefKinds,
        string[] AllComponents,
        string[] AnyComponents,
        string[] NoneComponents,
        string[] ExclusiveComponents,
        bool Cached,
        bool HasStructuralChanges,
        StructuralChangeMode StructuralChangeMode,
        StructuralChangeType[] StructuralChanges,
        Location Location);

    /// <summary>
    /// Feature requirements declared via [EcsRequires].
    /// </summary>
    internal sealed record SystemRequirementModel(
        EcsFeature Features,
        MissingFeatureBehavior IfMissing);

    /// <summary>
    /// Reactive handler kind (OnAdded/OnRemoved/OnChanged).
    /// </summary>
    internal enum ReactiveKind
    {
        OnAdded,
        OnRemoved,
        OnChanged
    }

    /// <summary>
    /// Description of a reactive handler method.
    /// </summary>
    internal sealed record ReactiveHandlerModel(
        ReactiveKind Kind,
        string MethodName,
        string ComponentTypeName,
        Location Location);

    /// <summary>
    /// High-level description of an [EcsSystem]-annotated class, its queries,
    /// feature requirements, and reactive/structural metadata.
    /// </summary>
    internal sealed record SystemModel(
        string Name,
        string Namespace,
        string FullName,
        Accessibility Accessibility,
        bool IsPartial,
        SystemPhase Phase,
        int Order,
        string? GroupFullName,
        SystemRequirementModel[] Requirements,
        bool RequiresRandomAccess,
        bool ArchInlineQuery,
        string? CommandBufferSystemType,
        string? CommandBufferMemberName,
        ReactiveHandlerModel[] ReactiveHandlers,
        QueryModel[] Queries,
        Location Location);
}
