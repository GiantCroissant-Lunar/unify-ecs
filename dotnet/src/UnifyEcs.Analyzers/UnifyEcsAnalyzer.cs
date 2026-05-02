using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UnifyECS.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnifyEcsAnalyzer : DiagnosticAnalyzer
    {
        private const string ComponentAttributeMetadataName          = "UnifyECS.EcsComponentAttribute";
        private const string ManagedComponentAttributeMetadataName   = "UnifyECS.ManagedComponentAttribute";
        private const string SystemAttributeMetadataName             = "UnifyECS.EcsSystemAttribute";
        private const string QueryAttributeMetadataName              = "UnifyECS.QueryAttribute";
        private const string SuppressAttributeMetadataName           = "UnifyECS.SuppressUnifyDiagnosticAttribute";
        private const string StructuralChangesAttributeMetadataName  = "UnifyECS.StructuralChangesAttribute";
        private const string EcsOptimizeAttributeMetadataName        = "UnifyECS.EcsOptimizeAttribute";
        private const string InjectAttributeMetadataName             = "UnifyECS.InjectAttribute";
        private const string ICommandBufferTypeMetadataName          = "UnifyECS.ICommandBuffer";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Diagnostics.All;

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Shape / component rules (UECS004-UECS009, UECS011/UECS101)
            context.RegisterSymbolAction(AnalyzeNamedTypeShapeAndFields, SymbolKind.NamedType);

            // Semantic rules that require compilation context (UECS003, UECS010, UECS012-UECS015)
            context.RegisterCompilationStartAction(RegisterSemanticAnalyzers);
        }

        private static void AnalyzeNamedTypeShapeAndFields(SymbolAnalysisContext context)
        {
            if (context.Symbol is not INamedTypeSymbol typeSymbol)
                return;

            AnalyzeComponentTypeShapeAndFields(context, typeSymbol);
            AnalyzeSystemTypeShapeAndInjection(context, typeSymbol);
        }

        private static void AnalyzeComponentTypeShapeAndFields(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
        {
            if (!HasAttribute(typeSymbol, ComponentAttributeMetadataName))
                return;

            var name = typeSymbol.Name;

            // UECS004: Component must be a struct
            if (typeSymbol.TypeKind != TypeKind.Struct)
            {
                ReportIfNotSuppressed(context, typeSymbol, Diagnostics.UECS004_ComponentMustBeStruct, name);
            }

            // UECS005: Component cannot be generic
            if (typeSymbol.IsGenericType)
            {
                ReportIfNotSuppressed(context, typeSymbol, Diagnostics.UECS005_ComponentCannotBeGeneric, name);
            }

            // UECS006: Component cannot be nested
            if (typeSymbol.ContainingType is not null)
            {
                ReportIfNotSuppressed(context, typeSymbol, Diagnostics.UECS006_ComponentCannotBeNested, name);
            }

            // UECS011 / UECS101: reference-type fields vs [ManagedComponent]
            var hasManagedComponent = HasAttribute(typeSymbol, ManagedComponentAttributeMetadataName);

            foreach (var member in typeSymbol.GetMembers().OfType<IFieldSymbol>())
            {
                if (member.IsStatic)
                    continue;

                var fieldType = member.Type;
                if (fieldType is null)
                    continue;

                // Treat any reference type (class, interface, array, delegate, string) as non-DOTS-safe
                if (!fieldType.IsReferenceType)
                    continue;

                var fieldName = member.Name;
                var location = member.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault() ?? Location.None;

                if (hasManagedComponent)
                {
                    // UECS101: warning, managed-only component is allowed but non-portable
                    if (!IsSuppressed(typeSymbol, Diagnostics.UECS101_ComponentHasReferenceFieldRequiresManaged.Id))
                    {
                        var diag = Diagnostic.Create(
                            Diagnostics.UECS101_ComponentHasReferenceFieldRequiresManaged,
                            location,
                            name);
                        context.ReportDiagnostic(diag);
                    }
                }
                else
                {
                    // UECS011: error, reference field without [ManagedComponent]
                    if (!IsSuppressed(typeSymbol, Diagnostics.UECS011_ComponentContainsReferenceField.Id))
                    {
                        var diag = Diagnostic.Create(
                            Diagnostics.UECS011_ComponentContainsReferenceField,
                            location,
                            name,
                            fieldName);
                        context.ReportDiagnostic(diag);
                    }
                }
            }
        }

        private static void AnalyzeSystemTypeShapeAndInjection(SymbolAnalysisContext context, INamedTypeSymbol typeSymbol)
        {
            if (!HasAttribute(typeSymbol, SystemAttributeMetadataName))
                return;

            var name = typeSymbol.Name;

            // UECS007: System must be a partial class
            if (typeSymbol.TypeKind != TypeKind.Class || !IsPartial(typeSymbol))
            {
                ReportIfNotSuppressed(context, typeSymbol, Diagnostics.UECS007_SystemMustBePartial, name);
            }

            // UECS008: System cannot be abstract
            if (typeSymbol.IsAbstract)
            {
                ReportIfNotSuppressed(context, typeSymbol, Diagnostics.UECS008_SystemCannotBeAbstract, name);
            }

            // UECS009: System cannot be generic
            if (typeSymbol.IsGenericType)
            {
                ReportIfNotSuppressed(context, typeSymbol, Diagnostics.UECS009_SystemCannotBeGeneric, name);
            }

            if (typeSymbol.ContainingType is not null)
            {
                ReportIfNotSuppressed(context, typeSymbol, Diagnostics.UECS018_SystemCannotBeNested, name);
            }

            // UECS015: ICommandBuffer used but not injected
            foreach (var member in typeSymbol.GetMembers())
            {
                // Ignore compiler-generated backing fields for auto-properties, etc.
                if (member is IFieldSymbol field && field.IsImplicitlyDeclared)
                    continue;

                ITypeSymbol? memberType = member switch
                {
                    IFieldSymbol f    => f.Type,
                    IPropertySymbol p => p.Type,
                    _                 => null
                };

                if (memberType is not INamedTypeSymbol namedType)
                    continue;

                if (namedType.ToDisplayString() != ICommandBufferTypeMetadataName)
                    continue;

                var hasInject = member.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == InjectAttributeMetadataName);
                if (!hasInject && !IsSuppressed(typeSymbol, Diagnostics.UECS015_ICommandBufferNotInjected.Id))
                {
                    var location = member.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault() ?? Location.None;
                    var diag = Diagnostic.Create(Diagnostics.UECS015_ICommandBufferNotInjected, location);
                    context.ReportDiagnostic(diag);
                }
            }
        }

        private static void RegisterSemanticAnalyzers(CompilationStartAnalysisContext context)
        {
            var entityType = context.Compilation.GetTypeByMetadataName("UnifyECS.Entity");
            var ecsBackendType = context.Compilation.GetTypeByMetadataName("UnifyECS.EcsBackend");
            var structuralModeType = context.Compilation.GetTypeByMetadataName("UnifyECS.StructuralChangeMode");
            var iWorldType = context.Compilation.GetTypeByMetadataName("UnifyECS.IWorld");
            var iCommandBufferType = context.Compilation.GetTypeByMetadataName("UnifyECS.ICommandBuffer");

            var dotsBackendValue = GetEnumConstant(ecsBackendType, "Dots");
            var immediateStructuralModeValue = GetEnumConstant(structuralModeType, "Immediate");
            var deferredStructuralModeValue = GetEnumConstant(structuralModeType, "Deferred");

            var registeredComponents = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            // Collect all [EcsComponent] types
            context.RegisterSymbolAction(ctx =>
            {
                if (ctx.Symbol is not INamedTypeSymbol type)
                    return;

                if (HasAttribute(type, ComponentAttributeMetadataName))
                {
                    registeredComponents.Add(type);
                }
            }, SymbolKind.NamedType);

            // Validate query parameters and structural rules on [EcsSystem] types
            context.RegisterSymbolAction(ctx =>
            {
                if (ctx.Symbol is not INamedTypeSymbol systemType)
                    return;

                AnalyzeSystemQueriesAndStructural(
                    ctx,
                    systemType,
                    entityType,
                    registeredComponents,
                    dotsBackendValue,
                    immediateStructuralModeValue,
                    deferredStructuralModeValue,
                    iWorldType,
                    iCommandBufferType);
            }, SymbolKind.NamedType);
        }

        private static int? GetEnumConstant(INamedTypeSymbol? enumType, string memberName)
        {
            if (enumType is null)
                return null;

            var field = enumType.GetMembers(memberName).OfType<IFieldSymbol>().FirstOrDefault();
            if (field is null || !field.HasConstantValue)
                return null;

            return field.ConstantValue is int value ? value : (int?)null;
        }

        private static void AnalyzeSystemQueriesAndStructural(
            SymbolAnalysisContext context,
            INamedTypeSymbol systemType,
            INamedTypeSymbol? entityType,
            HashSet<INamedTypeSymbol> registeredComponents,
            int? dotsBackendValue,
            int? immediateStructuralModeValue,
            int? deferredStructuralModeValue,
            INamedTypeSymbol? iWorldType,
            INamedTypeSymbol? iCommandBufferType)
        {
            if (!HasAttribute(systemType, SystemAttributeMetadataName))
                return;

            var isDotsParallelSystem = IsDotsParallelSystem(systemType, dotsBackendValue);

            foreach (var method in systemType.GetMembers().OfType<IMethodSymbol>())
            {
                var queryAttr = GetAttribute(method, QueryAttributeMetadataName);
                var structuralAttr = GetAttribute(method, StructuralChangesAttributeMetadataName);

                // Only analyze methods that participate in queries or declare structural changes.
                if (queryAttr is null && structuralAttr is null)
                    continue;

                if (method.ReturnType.SpecialType != SpecialType.System_Void &&
                    !IsSuppressed(systemType, Diagnostics.UECS016_QueryMustReturnVoid.Id))
                {
                    var location = method.Locations.FirstOrDefault() ?? systemType.Locations.FirstOrDefault() ?? Location.None;
                    var diag = Diagnostic.Create(Diagnostics.UECS016_QueryMustReturnVoid, location, method.Name);
                    context.ReportDiagnostic(diag);
                }

                if (method.IsAsync &&
                    !IsSuppressed(systemType, Diagnostics.UECS017_QueryCannotBeAsync.Id))
                {
                    var location = method.Locations.FirstOrDefault() ?? systemType.Locations.FirstOrDefault() ?? Location.None;
                    var diag = Diagnostic.Create(Diagnostics.UECS017_QueryCannotBeAsync, location, method.Name);
                    context.ReportDiagnostic(diag);
                }

                // Track component parameter types on this method
                var paramComponentTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

                foreach (var parameter in method.Parameters)
                {
                    // Only by-ref parameters (ref/in) are treated as components
                    if (parameter.RefKind != RefKind.Ref && parameter.RefKind != RefKind.In && parameter.RefKind != RefKind.RefReadOnly)
                        continue;

                    if (parameter.Type is not INamedTypeSymbol paramType)
                        continue;

                    // Allow Entity by-ref
                    if (entityType is not null && SymbolEqualityComparer.Default.Equals(paramType, entityType))
                        continue;

                    paramComponentTypes.Add(paramType);

                    // UECS003: parameter type must be a registered component
                    if (!registeredComponents.Contains(paramType))
                    {
                        if (IsSuppressed(systemType, Diagnostics.UECS003_InvalidQueryParameter.Id))
                            continue;

                        var location = parameter.Locations.FirstOrDefault() ?? method.Locations.FirstOrDefault() ?? systemType.Locations.FirstOrDefault() ?? Location.None;
                        var diagnostic = Diagnostic.Create(
                            Diagnostics.UECS003_InvalidQueryParameter,
                            location,
                            method.Name,
                            parameter.Name,
                            paramType.ToDisplayString());

                        context.ReportDiagnostic(diagnostic);
                    }
                }

                // UECS010: All components must appear as parameters (only when [Query] is present)
                if (queryAttr is not null)
                {
                    var allComponents = ExtractComponentTypesFromNamedArgument(queryAttr, "All");
                    if (!allComponents.IsDefaultOrEmpty)
                    {
                        foreach (var allType in allComponents)
                        {
                            if (allType is null)
                                continue;

                            if (entityType is not null && SymbolEqualityComparer.Default.Equals(allType, entityType))
                                continue;

                            if (!paramComponentTypes.Contains(allType))
                            {
                                if (IsSuppressed(systemType, Diagnostics.UECS010_QueryParametersDoNotMatchAll.Id))
                                    break;

                                var location = method.Locations.FirstOrDefault() ?? systemType.Locations.FirstOrDefault() ?? Location.None;
                                var diagnostic = Diagnostic.Create(
                                    Diagnostics.UECS010_QueryParametersDoNotMatchAll,
                                    location,
                                    method.Name);

                                context.ReportDiagnostic(diagnostic);
                                break;
                            }
                        }
                    }
                }

                // First-pass body scan for structural calls
                var (hasStructuralCalls, hasCrossEntityWrites, hasWorldStructuralCalls, hasCommandBufferStructuralCalls) =
                    ScanStructuralCalls(context.Compilation, method, entityType, iWorldType, iCommandBufferType);

                // UECS013: Method performs structural changes but missing [StructuralChanges]
                if (hasStructuralCalls && structuralAttr is null &&
                    !IsSuppressed(systemType, Diagnostics.UECS013_StructuralChangesMissingAttribute.Id))
                {
                    var location = method.Locations.FirstOrDefault() ?? systemType.Locations.FirstOrDefault() ?? Location.None;
                    var diag = Diagnostic.Create(Diagnostics.UECS013_StructuralChangesMissingAttribute, location);
                    context.ReportDiagnostic(diag);
                }

                // UECS014 / UECS019: determine whether this method is in Deferred mode.
                var isDeferredMode = false;
                if (structuralAttr is not null)
                {
                    var hasExplicitMode = false;

                    if (deferredStructuralModeValue is int deferredValue)
                    {
                        foreach (var pair in structuralAttr.NamedArguments)
                        {
                            var name = pair.Key;
                            var arg = pair.Value;
                            if (name != "Mode")
                                continue;

                            hasExplicitMode = true;

                            var constant = arg.Value;
                            int modeInt;

                            if (constant is int intValue)
                            {
                                modeInt = intValue;
                            }
                            else if (constant is { } && constant.GetType().IsEnum)
                            {
                                modeInt = (int)constant;
                            }
                            else
                            {
                                continue;
                            }

                            if (modeInt == deferredValue)
                            {
                                isDeferredMode = true;
                            }

                            break;
                        }
                    }

                    // If Mode is not specified, default is Deferred.
                    if (!hasExplicitMode)
                    {
                        isDeferredMode = true;
                    }
                }

                if (hasCrossEntityWrites)
                {
                    if (!isDeferredMode && !IsSuppressed(systemType, Diagnostics.UECS014_CrossEntityWriteWithoutDeferredMode.Id))
                    {
                        var location = method.Locations.FirstOrDefault() ?? systemType.Locations.FirstOrDefault() ?? Location.None;
                        var diag = Diagnostic.Create(Diagnostics.UECS014_CrossEntityWriteWithoutDeferredMode, location);
                        context.ReportDiagnostic(diag);
                    }
                }

                // UECS019: Deferred structural query mixes IWorld and ICommandBuffer structural calls
                if (isDeferredMode && hasWorldStructuralCalls && hasCommandBufferStructuralCalls &&
                    !IsSuppressed(systemType, Diagnostics.UECS019_MixedWorldAndCommandBufferInDeferred.Id))
                {
                    var location = method.Locations.FirstOrDefault() ?? systemType.Locations.FirstOrDefault() ?? Location.None;
                    var diag = Diagnostic.Create(Diagnostics.UECS019_MixedWorldAndCommandBufferInDeferred, location, method.Name);
                    context.ReportDiagnostic(diag);
                }

                // UECS012: Immediate structural changes in parallel system (DOTS)
                if (isDotsParallelSystem && immediateStructuralModeValue is int immediateValue)
                {
                    if (structuralAttr is not null)
                    {
                        foreach (var pair in structuralAttr.NamedArguments)
                        {
                            var name = pair.Key;
                            var arg = pair.Value;
                            if (name == "Mode" && arg.Value is int modeValue && modeValue == immediateValue)
                            {
                                if (!IsSuppressed(systemType, Diagnostics.UECS012_ImmediateStructuralChangesInParallelSystem.Id))
                                {
                                    var location = method.Locations.FirstOrDefault() ?? systemType.Locations.FirstOrDefault() ?? Location.None;
                                    var diag = Diagnostic.Create(
                                        Diagnostics.UECS012_ImmediateStructuralChangesInParallelSystem,
                                        location);
                                    context.ReportDiagnostic(diag);
                                }

                                break;
                            }
                        }
                    }
                }
            }
        }

        private static (bool hasStructuralCalls, bool hasCrossEntityWrites, bool hasWorldStructuralCalls, bool hasCommandBufferStructuralCalls) ScanStructuralCalls(
            Compilation compilation,
            IMethodSymbol method,
            INamedTypeSymbol? entityType,
            INamedTypeSymbol? iWorldType,
            INamedTypeSymbol? iCommandBufferType)
        {
            var hasStructural = false;
            var hasCrossEntity = false;
            var hasWorldStructural = false;
            var hasCommandBufferStructural = false;

            if (method.DeclaringSyntaxReferences.Length == 0)
                return (false, false, false, false);

            var syntaxRef = method.DeclaringSyntaxReferences[0];
            if (syntaxRef.GetSyntax() is not MethodDeclarationSyntax methodSyntax)
                return (false, false, false, false);

            var model = compilation.GetSemanticModel(methodSyntax.SyntaxTree);

            IParameterSymbol? entityParam = null;
            if (entityType is not null)
            {
                foreach (var p in method.Parameters)
                {
                    if (SymbolEqualityComparer.Default.Equals(p.Type, entityType))
                    {
                        entityParam = p;
                        break;
                    }
                }
            }

            foreach (var invocation in methodSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var info = model.GetSymbolInfo(invocation);
                var invoked = info.Symbol as IMethodSymbol
                              ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
                if (invoked is null)
                    continue;

                var containingType = invoked.ContainingType;
                if (containingType is null)
                    continue;

                var isWorldTarget = false;
                var isCommandBufferTarget = false;

                if (iWorldType is not null)
                {
                    if (SymbolEqualityComparer.Default.Equals(containingType, iWorldType) ||
                        containingType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iWorldType)))
                    {
                        isWorldTarget = true;
                    }
                }
                else
                {
                    if (containingType.ToDisplayString() == "UnifyECS.IWorld")
                    {
                        isWorldTarget = true;
                    }
                }

                if (iCommandBufferType is not null)
                {
                    if (SymbolEqualityComparer.Default.Equals(containingType, iCommandBufferType) ||
                        containingType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iCommandBufferType)))
                    {
                        isCommandBufferTarget = true;
                    }
                }
                else
                {
                    if (containingType.ToDisplayString() == "UnifyECS.ICommandBuffer")
                    {
                        isCommandBufferTarget = true;
                    }
                }

                if (!isWorldTarget && !isCommandBufferTarget)
                    continue;

                var methodName = invoked.Name;
                if (methodName is not ("CreateEntity" or "DestroyEntity" or "Add" or "Remove" or "Set"))
                    continue;

                hasStructural = true;

                if (isWorldTarget)
                {
                    hasWorldStructural = true;
                }

                if (isCommandBufferTarget)
                {
                    hasCommandBufferStructural = true;
                }

                // Cross-entity writes: world-based Add/Remove/Set/Destroy where entity argument is not the query's Entity parameter
                if (!isWorldTarget || entityType is null || entityParam is null)
                    continue;

                if (methodName is "Add" or "Remove" or "Set" or "DestroyEntity")
                {
                    var parameters = invoked.Parameters;
                    var entityParamIndex = -1;
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (SymbolEqualityComparer.Default.Equals(parameters[i].Type, entityType))
                        {
                            entityParamIndex = i;
                            break;
                        }
                    }

                    if (entityParamIndex < 0)
                        continue;

                    var args = invocation.ArgumentList.Arguments;
                    if (entityParamIndex >= args.Count)
                        continue;

                    var entityArgSyntax = args[entityParamIndex].Expression;
                    var argSymbol = model.GetSymbolInfo(entityArgSyntax).Symbol;

                    if (argSymbol is IParameterSymbol paramSymbol)
                    {
                        if (!SymbolEqualityComparer.Default.Equals(paramSymbol, entityParam))
                        {
                            hasCrossEntity = true;
                        }
                    }
                    else
                    {
                        // Anything other than the entity parameter is treated as cross-entity
                        hasCrossEntity = true;
                    }
                }
            }

            return (hasStructural, hasCrossEntity, hasWorldStructural, hasCommandBufferStructural);
        }

        private static bool IsDotsParallelSystem(INamedTypeSymbol systemType, int? dotsBackendValue)
        {
            if (dotsBackendValue is null)
                return false;

            foreach (var attr in systemType.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass is null)
                    continue;

                if (attrClass.ToDisplayString() != EcsOptimizeAttributeMetadataName)
                    continue;

                if (attr.ConstructorArguments.Length != 1 || attr.ConstructorArguments[0].Value is not int backendValue)
                    continue;

                if (backendValue != dotsBackendValue.Value)
                    continue;

                var parallel = false;
                foreach (var pair in attr.NamedArguments)
                {
                    var name = pair.Key;
                    var arg = pair.Value;
                    if (name == "Parallel" && arg.Value is bool b)
                    {
                        parallel = b;
                        break;
                    }
                }

                if (parallel)
                    return true;
            }

            return false;
        }

        private static ImmutableArray<INamedTypeSymbol> ExtractComponentTypesFromNamedArgument(
            AttributeData attribute,
            string argumentName)
        {
            foreach (var pair in attribute.NamedArguments)
            {
                var name = pair.Key;
                var value = pair.Value;
                if (name != argumentName)
                    continue;

                if (value.Kind != TypedConstantKind.Array || value.Values.IsDefaultOrEmpty)
                    return ImmutableArray<INamedTypeSymbol>.Empty;

                var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(value.Values.Length);
                foreach (var item in value.Values)
                {
                    if (item.Value is INamedTypeSymbol typeSymbol)
                    {
                        builder.Add(typeSymbol);
                    }
                }

                return builder.ToImmutable();
            }

            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

        private static bool HasAttribute(INamedTypeSymbol typeSymbol, string metadataName)
        {
            foreach (var attr in typeSymbol.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass is null)
                    continue;

                if (attrClass.ToDisplayString() == metadataName)
                    return true;
            }

            return false;
        }

        private static AttributeData? GetAttribute(IMethodSymbol methodSymbol, string metadataName)
        {
            foreach (var attr in methodSymbol.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass is null)
                    continue;

                if (attrClass.ToDisplayString() == metadataName)
                    return attr;
            }

            return null;
        }

        private static bool IsPartial(INamedTypeSymbol typeSymbol)
        {
            foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
            {
                if (syntaxRef.GetSyntax() is ClassDeclarationSyntax classDecl)
                {
                    if (classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ReportIfNotSuppressed(
            SymbolAnalysisContext context,
            INamedTypeSymbol symbol,
            DiagnosticDescriptor descriptor,
            params object[] messageArgs)
        {
            if (IsSuppressed(symbol, descriptor.Id))
                return;

            var location = symbol.Locations.FirstOrDefault() ?? Location.None;
            var diagnostic = Diagnostic.Create(descriptor, location, messageArgs);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsSuppressed(INamedTypeSymbol symbol, string diagnosticId)
        {
            foreach (var attr in symbol.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass is null)
                    continue;

                if (attrClass.ToDisplayString() != SuppressAttributeMetadataName)
                    continue;

                if (attr.ConstructorArguments.Length == 0)
                    continue;

                var arg = attr.ConstructorArguments[0];
                if (arg.Kind == TypedConstantKind.Array)
                {
                    foreach (var value in arg.Values)
                    {
                        if (value.Value is string id && id == diagnosticId)
                            return true;
                    }
                }
                else if (arg.Value is string singleId && singleId == diagnosticId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
