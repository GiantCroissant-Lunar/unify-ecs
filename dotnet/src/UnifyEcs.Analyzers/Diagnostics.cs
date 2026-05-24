using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace UnifyECS.Analyzers
{
    internal static class Diagnostics
    {
        // Tags applied to descriptors reported via RegisterCompilationEndAction.
        // Required by Roslyn's RS1037 so the IDE knows to suppress these in the
        // single-file incremental analyzer pass and re-run them at compilation
        // end (when the full registeredComponents set is known).
        private static readonly string[] CompilationEndTags = { WellKnownDiagnosticTags.CompilationEnd };

        public static readonly DiagnosticDescriptor UECS003_InvalidQueryParameter = new(
            id: "UECS003",
            title: "Invalid query parameter type",
            messageFormat: "Query method '{0}' parameter '{1}' type '{2}' is not a registered component",
            category: "UnifyECS.Query",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: CompilationEndTags);

        public static readonly DiagnosticDescriptor UECS010_QueryParametersDoNotMatchAll = new(
            id: "UECS010",
            title: "Query parameters do not match All components",
            messageFormat: "Query method '{0}' parameters do not match All components",
            category: "UnifyECS.Query",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: CompilationEndTags);

        public static readonly DiagnosticDescriptor UECS004_ComponentMustBeStruct = new(
            id: "UECS004",
            title: "Component must be a struct",
            messageFormat: "Component '{0}' must be a struct",
            category: "UnifyECS.ComponentShape",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UECS005_ComponentCannotBeGeneric = new(
            id: "UECS005",
            title: "Component cannot be generic",
            messageFormat: "Component '{0}' cannot be generic",
            category: "UnifyECS.ComponentShape",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UECS006_ComponentCannotBeNested = new(
            id: "UECS006",
            title: "Component cannot be a nested type",
            messageFormat: "Component '{0}' cannot be a nested type",
            category: "UnifyECS.ComponentShape",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UECS007_SystemMustBePartial = new(
            id: "UECS007",
            title: "System must be a partial class",
            messageFormat: "System '{0}' must be a partial class",
            category: "UnifyECS.SystemShape",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UECS008_SystemCannotBeAbstract = new(
            id: "UECS008",
            title: "System cannot be abstract",
            messageFormat: "System '{0}' cannot be abstract",
            category: "UnifyECS.SystemShape",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UECS009_SystemCannotBeGeneric = new(
            id: "UECS009",
            title: "System cannot be generic",
            messageFormat: "System '{0}' cannot be generic",
            category: "UnifyECS.SystemShape",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UECS011_ComponentContainsReferenceField = new(
            id: "UECS011",
            title: "Component contains reference type field",
            messageFormat: "Component '{0}' contains reference type field '{1}' (not DOTS-compatible)",
            category: "UnifyECS.ComponentFields",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UECS012_ImmediateStructuralChangesInParallelSystem = new(
            id: "UECS012",
            title: "Immediate structural changes in parallel system (DOTS)",
            messageFormat: "Immediate structural changes in parallel system (DOTS)",
            category: "UnifyECS.StructuralChanges",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: CompilationEndTags);

        public static readonly DiagnosticDescriptor UECS013_StructuralChangesMissingAttribute = new(
            id: "UECS013",
            title: "Method performs structural changes but missing [StructuralChanges]",
            messageFormat: "Method performs structural changes but missing [StructuralChanges]",
            category: "UnifyECS.StructuralChanges",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            customTags: CompilationEndTags);

        public static readonly DiagnosticDescriptor UECS014_CrossEntityWriteWithoutDeferredMode = new(
            id: "UECS014",
            title: "Cross-entity write without deferred mode",
            messageFormat: "Cross-entity write without deferred mode",
            category: "UnifyECS.StructuralChanges",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: CompilationEndTags);

        public static readonly DiagnosticDescriptor UECS015_ICommandBufferNotInjected = new(
            id: "UECS015",
            title: "ICommandBuffer used but not injected",
            messageFormat: "ICommandBuffer used but not injected",
            category: "UnifyECS.StructuralChanges",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UECS016_QueryMustReturnVoid = new(
            id: "UECS016",
            title: "Query method must return void",
            messageFormat: "Query method '{0}' must return void",
            category: "UnifyECS.Query",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: CompilationEndTags);

        public static readonly DiagnosticDescriptor UECS017_QueryCannotBeAsync = new(
            id: "UECS017",
            title: "Query method cannot be async",
            messageFormat: "Query method '{0}' cannot be async",
            category: "UnifyECS.Query",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: CompilationEndTags);

        public static readonly DiagnosticDescriptor UECS018_SystemCannotBeNested = new(
            id: "UECS018",
            title: "System cannot be a nested type",
            messageFormat: "System '{0}' cannot be a nested type",
            category: "UnifyECS.SystemShape",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UECS019_MixedWorldAndCommandBufferInDeferred = new(
            id: "UECS019",
            title: "Deferred structural query mixes IWorld and ICommandBuffer structural calls",
            messageFormat: "Deferred structural query method '{0}' mixes IWorld and ICommandBuffer structural calls",
            category: "UnifyECS.StructuralChanges",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            customTags: CompilationEndTags);

        public static readonly DiagnosticDescriptor UECS101_ComponentHasReferenceFieldRequiresManaged = new(
            id: "UECS101",
            title: "Component has reference field and requires [ManagedComponent]",
            messageFormat: "Component '{0}' has reference field - requires [ManagedComponent] for DOTS",
            category: "UnifyECS.ComponentFields",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
            UECS003_InvalidQueryParameter,
            UECS010_QueryParametersDoNotMatchAll,
            UECS004_ComponentMustBeStruct,
            UECS005_ComponentCannotBeGeneric,
            UECS006_ComponentCannotBeNested,
            UECS007_SystemMustBePartial,
            UECS008_SystemCannotBeAbstract,
            UECS009_SystemCannotBeGeneric,
            UECS011_ComponentContainsReferenceField,
            UECS012_ImmediateStructuralChangesInParallelSystem,
            UECS013_StructuralChangesMissingAttribute,
            UECS014_CrossEntityWriteWithoutDeferredMode,
            UECS015_ICommandBufferNotInjected,
            UECS016_QueryMustReturnVoid,
            UECS017_QueryCannotBeAsync,
            UECS018_SystemCannotBeNested,
            UECS019_MixedWorldAndCommandBufferInDeferred,
            UECS101_ComponentHasReferenceFieldRequiresManaged);
    }
}
