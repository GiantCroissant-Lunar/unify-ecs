using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using UnifyECS;
using UnifyECS.Generators.Backends;

namespace UnifyECS.Generators
{
    /// <summary>
    /// Entry point for the UnifyECS incremental source generator.
    /// Phase 1A/1B: discover [EcsComponent] and [EcsSystem] (+ [Query]) and
    /// build high-level models that later backend emitters can consume.
    /// Currently emits a small debug file listing discovered types and a
    /// first-pass Arch backend implementation.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class EcsGenerator : IIncrementalGenerator
    {
        private sealed record GeneratorConfig(
            EcsBackend[] Backends,
            MissingFeatureBehavior GlobalMissingFeaturePolicy,
            IReadOnlyDictionary<EcsFeature, MissingFeatureBehavior> FeaturePolicies)
        {
            public bool EmitArch => Array.IndexOf(Backends, EcsBackend.Arch) >= 0;
            public bool EmitEntitas => Array.IndexOf(Backends, EcsBackend.Entitas) >= 0;
        }

        private const string ComponentAttributeMetadataName          = "UnifyECS.EcsComponentAttribute";
        private const string SystemAttributeMetadataName             = "UnifyECS.EcsSystemAttribute";
        private const string QueryAttributeMetadataName              = "UnifyECS.QueryAttribute";
        private const string EcsRequiresAttributeMetadataName        = "UnifyECS.EcsRequiresAttribute";
        private const string OnAddedAttributeMetadataName            = "UnifyECS.OnAddedAttribute";
        private const string OnRemovedAttributeMetadataName          = "UnifyECS.OnRemovedAttribute";
        private const string OnChangedAttributeMetadataName          = "UnifyECS.OnChangedAttribute";
        private const string StructuralChangesAttributeMetadataName  = "UnifyECS.StructuralChangesAttribute";
        private const string RequiresRandomAccessAttributeMetadataName = "UnifyECS.RequiresRandomAccessAttribute";
        private const string CommandBufferSystemAttributeMetadataName = "UnifyECS.CommandBufferSystemAttribute";
        private const string EcsOptimizeAttributeMetadataName        = "UnifyECS.EcsOptimizeAttribute";
        private const string InjectAttributeMetadataName             = "UnifyECS.InjectAttribute";
        private const string ICommandBufferTypeMetadataName          = "UnifyECS.ICommandBuffer";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Discover components
            var componentModels = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => IsStructWithAttributes(node),
                    static (ctx, ct) => TryCreateComponentModel(ctx, ct))
                .Where(static model => model is not null)!
                .Select(static (model, _) => model!);

            // Discover systems (and their queries, requirements, reactive handlers)
            var systemModels = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => IsClassWithAttributes(node),
                    static (ctx, ct) => TryCreateSystemModel(ctx, ct))
                .Where(static model => model is not null)!
                .Select(static (model, _) => model!);

            // Generator configuration from MSBuild properties
            var config = context.AnalyzerConfigOptionsProvider
                .Select(static (options, _) => CreateGeneratorConfig(options.GlobalOptions));

            // Combine into a single value the generator can use to emit code
            var combined = componentModels.Collect()
                .Combine(systemModels.Collect())
                .Combine(config);

            context.RegisterSourceOutput(combined, static (spc, triple) =>
            {
                var ((components, systems), config) = triple;

                EmitComponentTypeRegistry(spc, components);

                // Debug summary for development
                EmitDebugSummary(spc, components, systems);

                // Arch backend emission (Phase 1 skeleton)
                if (config.EmitArch)
                {
                    EmitArchBackend(spc, components, systems, config);
                }

                // Entitas / other backends will be added in later phases when emitters are ready.
            });
        }

        private static bool IsStructWithAttributes(SyntaxNode node) =>
            node is StructDeclarationSyntax { AttributeLists.Count: > 0 };

        private static bool IsClassWithAttributes(SyntaxNode node) =>
            node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

        private static GeneratorConfig CreateGeneratorConfig(AnalyzerConfigOptions options)
        {
            // Backends: "Arch;Entitas;Dots" etc. Default to Arch only if not specified.
            if (!options.TryGetValue("build_property.UnifyEcsBackends", out var backendsText) ||
                string.IsNullOrWhiteSpace(backendsText))
            {
                backendsText = "Arch";
            }

            var backendList = new List<EcsBackend>();
            foreach (var raw in backendsText.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.Trim();
                if (token.Length == 0)
                    continue;

                if (Enum.TryParse<EcsBackend>(token, ignoreCase: true, out var backend))
                {
                    backendList.Add(backend);
                }
            }

            if (backendList.Count == 0)
            {
                backendList.Add(EcsBackend.Arch);
            }

            // Global missing feature policy: Error/Warn/NoOp/Emulate. Default to Error.
            var globalPolicy = MissingFeatureBehavior.Error;
            if (options.TryGetValue("build_property.UnifyEcsMissingFeaturePolicy", out var policyText) &&
                !string.IsNullOrWhiteSpace(policyText) &&
                Enum.TryParse<MissingFeatureBehavior>(policyText.Trim(), ignoreCase: true, out var parsedPolicy))
            {
                globalPolicy = parsedPolicy;
            }

            // Per-feature policy overrides: UnifyEcsPolicy_Reactive, UnifyEcsPolicy_Jobs, etc.
            var featurePolicies = new Dictionary<EcsFeature, MissingFeatureBehavior>();
            foreach (EcsFeature feature in Enum.GetValues(typeof(EcsFeature)))
            {
                if (feature == EcsFeature.None)
                    continue;

                var featureName = feature.ToString();
                var key = "build_property.UnifyEcsPolicy_" + featureName;
                if (!options.TryGetValue(key, out var featurePolicyText) ||
                    string.IsNullOrWhiteSpace(featurePolicyText))
                {
                    continue;
                }

                if (Enum.TryParse<MissingFeatureBehavior>(featurePolicyText.Trim(), ignoreCase: true, out var featurePolicy))
                {
                    featurePolicies[feature] = featurePolicy;
                }
            }

            return new GeneratorConfig(backendList.ToArray(), globalPolicy, featurePolicies);
        }

        private static ComponentModel? TryCreateComponentModel(
            GeneratorSyntaxContext context,
            System.Threading.CancellationToken ct)
        {
            if (context.Node is not StructDeclarationSyntax structDecl)
                return null;

            var symbol = context.SemanticModel.GetDeclaredSymbol(structDecl, ct) as INamedTypeSymbol;
            if (symbol is null)
                return null;

            var attr = GetAttribute(symbol, ComponentAttributeMetadataName);
            if (attr is null)
                return null;

            var ns = GetNamespace(symbol);
            var fullName = string.IsNullOrEmpty(ns) ? symbol.Name : $"{ns}.{symbol.Name}";

            // Determine IsTag from attribute named arguments
            var isTag = false;
            foreach (var (name, value) in attr.NamedArguments)
            {
                if (name == "IsTag" && value.Value is bool b)
                {
                    isTag = b;
                    break;
                }
            }

            var isPartial = structDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

            var structureHash = ComputeStructureHash(symbol);

            return new ComponentModel(
                symbol.Name,
                ns,
                fullName,
                symbol.DeclaredAccessibility,
                isPartial,
                isTag,
                structureHash,
                structDecl.GetLocation());
        }

        private static SystemModel? TryCreateSystemModel(
            GeneratorSyntaxContext context,
            System.Threading.CancellationToken ct)
        {
            if (context.Node is not ClassDeclarationSyntax classDecl)
                return null;

            var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;
            if (symbol is null)
                return null;

            var systemAttr = GetAttribute(symbol, SystemAttributeMetadataName);
            if (systemAttr is null)
                return null;

            var ns = GetNamespace(symbol);
            var fullName = string.IsNullOrEmpty(ns) ? symbol.Name : $"{ns}.{symbol.Name}";

            // Defaults from RFC-0011
            var phase = SystemPhase.Update;
            var order = 0;
            string? groupFullName = null;

            foreach (var (name, arg) in systemAttr.NamedArguments)
            {
                switch (name)
                {
                    case "Phase" when arg.Value is int phaseValue:
                        phase = (SystemPhase)phaseValue;
                        break;
                    case "Order" when arg.Value is int orderValue:
                        order = orderValue;
                        break;
                    case "Group" when arg.Value is ITypeSymbol groupSymbol:
                        groupFullName = groupSymbol.ToDisplayString();
                        break;
                }
            }

            var isPartial = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

            // EcsRequires
            var requirements = GetSystemRequirements(symbol);

            // RequiresRandomAccess
            var requiresRandomAccess = GetAttribute(symbol, RequiresRandomAccessAttributeMetadataName) is not null;

            // CommandBufferSystem
            string? commandBufferSystemType = null;
            var commandBufferAttr = GetAttribute(symbol, CommandBufferSystemAttributeMetadataName);
            if (commandBufferAttr is not null &&
                commandBufferAttr.ConstructorArguments.Length == 1 &&
                commandBufferAttr.ConstructorArguments[0].Value is ITypeSymbol ecbType)
            {
                commandBufferSystemType = ecbType.ToDisplayString();
            }

            // Arch InlineQuery optimization hint
            var archInlineQuery = false;
            foreach (var attr in symbol.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass is null)
                    continue;

                if (attrClass.ToDisplayString() != EcsOptimizeAttributeMetadataName)
                    continue;

                if (attr.ConstructorArguments.Length != 1 || attr.ConstructorArguments[0].Value is not int backendValue)
                    continue;

                if ((EcsBackend)backendValue != EcsBackend.Arch)
                    continue;

                foreach (var (name, arg) in attr.NamedArguments)
                {
                    if (name == "InlineQuery" && arg.Value is bool b && b)
                    {
                        archInlineQuery = true;
                        break;
                    }
                }

                if (archInlineQuery)
                    break;
            }

            string? commandBufferMemberName = null;
            foreach (var member in symbol.GetMembers())
            {
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
                if (!hasInject)
                    continue;

                commandBufferMemberName = member.Name;
                break;
            }

            // Discover query methods on this system
            var queries = new List<QueryModel>();
            var reactiveHandlers = new List<ReactiveHandlerModel>();

            foreach (var member in symbol.GetMembers().OfType<IMethodSymbol>())
            {
                if (member.MethodKind != MethodKind.Ordinary)
                    continue;

                // [Query]
                var queryAttr = GetAttribute(member, QueryAttributeMetadataName);
                var structuralAttr = GetAttribute(member, StructuralChangesAttributeMetadataName);
                if (queryAttr is not null)
                {
                    var queryModel = CreateQueryModel(member, queryAttr, structuralAttr);
                    if (queryModel is not null)
                    {
                        queries.Add(queryModel);
                    }
                }

                // Reactive handlers
                CollectReactiveHandlers(member, reactiveHandlers);
            }

            return new SystemModel(
                symbol.Name,
                ns,
                fullName,
                symbol.DeclaredAccessibility,
                isPartial,
                phase,
                order,
                groupFullName,
                requirements,
                requiresRandomAccess,
                archInlineQuery,
                commandBufferSystemType,
                commandBufferMemberName,
                reactiveHandlers.ToArray(),
                queries.ToArray(),
                classDecl.GetLocation());
        }

        private static SystemRequirementModel[] GetSystemRequirements(INamedTypeSymbol systemSymbol)
        {
            var list = new List<SystemRequirementModel>();

            foreach (var attr in systemSymbol.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass is null)
                    continue;

                if (attrClass.ToDisplayString() != EcsRequiresAttributeMetadataName)
                    continue;

                var features = EcsFeature.None;
                if (attr.ConstructorArguments.Length == 1 &&
                    attr.ConstructorArguments[0].Value is int featuresValue)
                {
                    features = (EcsFeature)featuresValue;
                }

                var behavior = MissingFeatureBehavior.Error;
                foreach (var (name, arg) in attr.NamedArguments)
                {
                    if (name == "IfMissing" && arg.Value is int behaviorValue)
                    {
                        behavior = (MissingFeatureBehavior)behaviorValue;
                        break;
                    }
                }

                list.Add(new SystemRequirementModel(features, behavior));
            }

            return list.Count == 0 ? Array.Empty<SystemRequirementModel>() : list.ToArray();
        }

        private static void CollectReactiveHandlers(
            IMethodSymbol method,
            List<ReactiveHandlerModel> handlers)
        {
            foreach (var attr in method.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass is null)
                    continue;

                var attrName = attrClass.ToDisplayString();
                ReactiveKind kind;
                if (attrName == OnAddedAttributeMetadataName)
                {
                    kind = ReactiveKind.OnAdded;
                }
                else if (attrName == OnRemovedAttributeMetadataName)
                {
                    kind = ReactiveKind.OnRemoved;
                }
                else if (attrName == OnChangedAttributeMetadataName)
                {
                    kind = ReactiveKind.OnChanged;
                }
                else
                {
                    continue;
                }

                var componentTypeName = "System.Object";
                if (attr.ConstructorArguments.Length == 1 &&
                    attr.ConstructorArguments[0].Value is ITypeSymbol typeSymbol)
                {
                    componentTypeName = typeSymbol.ToDisplayString();
                }

                var location = method.Locations.FirstOrDefault() ?? Location.None;
                handlers.Add(new ReactiveHandlerModel(kind, method.Name, componentTypeName, location));
            }
        }

        private static QueryModel? CreateQueryModel(
            IMethodSymbol method,
            AttributeData queryAttr,
            AttributeData? structuralAttr)
        {
            var methodName = method.Name;
            var returnTypeName = method.ReturnType.ToDisplayString();

            var parameterTypeNames = method.Parameters
                .Select(p => p.Type.ToDisplayString())
                .ToArray();

            var parameterNames = method.Parameters
                .Select(p => p.Name)
                .ToArray();

            var parameterRefKinds = method.Parameters
                .Select(p => p.RefKind)
                .ToArray();

            string[] all = Array.Empty<string>();
            string[] any = Array.Empty<string>();
            string[] none = Array.Empty<string>();
            string[] exclusive = Array.Empty<string>();
            var cached = true;

            foreach (var (name, arg) in queryAttr.NamedArguments)
            {
                switch (name)
                {
                    case "All":
                        all = ExtractComponentTypeNames(arg);
                        break;
                    case "Any":
                        any = ExtractComponentTypeNames(arg);
                        break;
                    case "None":
                        none = ExtractComponentTypeNames(arg);
                        break;
                    case "Exclusive":
                        exclusive = ExtractComponentTypeNames(arg);
                        break;
                    case "Cached" when arg.Value is bool b:
                        cached = b;
                        break;
                }
            }

            // If All was not explicitly specified, infer it from ref/in/out parameters
            // (excluding Entity parameters, which are handles rather than components).
            if (all.Length == 0)
            {
                var inferredAll = new List<string>();

                foreach (var parameter in method.Parameters)
                {
                    var refKind = parameter.RefKind;
                    if (refKind != RefKind.Ref && refKind != RefKind.In && refKind != RefKind.Out)
                    {
                        continue;
                    }

                    var type = parameter.Type;
                    var typeName = type.ToDisplayString();

                    // Skip Entity handles
                    if (string.Equals(typeName, "UnifyECS.Entity", StringComparison.Ordinal) ||
                        string.Equals(typeName, "Entity", StringComparison.Ordinal) ||
                        typeName.EndsWith(".Entity", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    inferredAll.Add(typeName);
                }

                if (inferredAll.Count > 0)
                {
                    all = inferredAll.ToArray();
                }
            }

            var hasStructuralChanges = false;
            var structuralMode = StructuralChangeMode.Deferred;
            var structuralChanges = Array.Empty<StructuralChangeType>();

            if (structuralAttr is not null)
            {
                hasStructuralChanges = true;

                foreach (var (name, arg) in structuralAttr.NamedArguments)
                {
                    switch (name)
                    {
                        case "Mode" when arg.Value is int modeValue:
                            structuralMode = (StructuralChangeMode)modeValue;
                            break;
                        case "Changes":
                            structuralChanges = ExtractStructuralChangeTypes(arg);
                            break;
                    }
                }
            }

            return new QueryModel(
                methodName,
                returnTypeName,
                parameterTypeNames,
                parameterNames,
                parameterRefKinds,
                all,
                any,
                none,
                exclusive,
                cached,
                hasStructuralChanges,
                structuralMode,
                structuralChanges,
                method.Locations.FirstOrDefault() ?? Location.None);
        }

        private static string[] ExtractComponentTypeNames(TypedConstant arg)
        {
            if (arg.Kind != TypedConstantKind.Array || arg.Values.IsDefaultOrEmpty)
                return Array.Empty<string>();

            var builder = new List<string>(arg.Values.Length);
            foreach (var value in arg.Values)
            {
                if (value.Value is ITypeSymbol typeSymbol)
                {
                    builder.Add(typeSymbol.ToDisplayString());
                }
            }

            return builder.Count == 0 ? Array.Empty<string>() : builder.ToArray();
        }

        private static StructuralChangeType[] ExtractStructuralChangeTypes(TypedConstant arg)
        {
            if (arg.Kind != TypedConstantKind.Array || arg.Values.IsDefaultOrEmpty)
                return Array.Empty<StructuralChangeType>();

            var builder = new List<StructuralChangeType>(arg.Values.Length);
            foreach (var value in arg.Values)
            {
                if (value.Value is int enumValue)
                {
                    builder.Add((StructuralChangeType)enumValue);
                }
            }

            return builder.Count == 0 ? Array.Empty<StructuralChangeType>() : builder.ToArray();
        }

        private static AttributeData? GetAttribute(ISymbol symbol, string metadataName)
        {
            foreach (var attr in symbol.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass is null)
                    continue;

                if (attrClass.ToDisplayString() == metadataName)
                    return attr;
            }

            return null;
        }

        private static ulong ComputeStructureHash(INamedTypeSymbol type)
        {
            const ulong fnvOffset = 14695981039346656037UL;
            const ulong fnvPrime  = 1099511628211UL;

            ulong hash = fnvOffset;

            foreach (var field in type.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic).OrderBy(f => f.Name))
            {
                foreach (var ch in field.Name)
                {
                    hash ^= ch;
                    hash *= fnvPrime;
                }

                var typeName = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                foreach (var ch in typeName)
                {
                    hash ^= ch;
                    hash *= fnvPrime;
                }
            }

            return hash;
        }

        private static void EmitComponentTypeRegistry(
            SourceProductionContext context,
            IReadOnlyList<ComponentModel> components)
        {
            if (components.Count == 0)
                return;

            var ordered = components
                .OrderBy(c => c.FullName, StringComparer.Ordinal)
                .ToArray();

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// UnifyECS Component Type Registry (RFC-0009)");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace UnifyECS");
            sb.AppendLine("{");
            sb.AppendLine("    public static partial class ComponentTypeRegistry");
            sb.AppendLine("    {");

            for (var i = 0; i < ordered.Length; i++)
            {
                var c = ordered[i];
                var fieldName = GetSafeIdentifier(c.FullName);
                sb.Append("        public static readonly ComponentTypeId ")
                  .Append(fieldName)
                  .Append(" = new ComponentTypeId(")
                  .Append(i)
                  .Append(", \"")
                  .Append(Escape(c.FullName))
                  .Append("\", 0x")
                  .Append(c.StructureHash.ToString("X16"))
                  .AppendLine("UL);");
            }

            sb.AppendLine();
            sb.Append("        public static readonly int TotalComponentTypes = ")
              .Append(ordered.Length)
              .AppendLine(";");
            sb.AppendLine();

            sb.AppendLine("        private static readonly Dictionary<Type, ComponentTypeId> _byType = new()");
            sb.AppendLine("        {");
            for (var i = 0; i < ordered.Length; i++)
            {
                var c = ordered[i];
                var fieldName = GetSafeIdentifier(c.FullName);
                sb.Append("            [typeof(global::")
                  .Append(c.FullName)
                  .Append(")] = ")
                  .Append(fieldName)
                  .AppendLine(",");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        private static readonly Dictionary<int, Type> _byId = new()");
            sb.AppendLine("        {");
            for (var i = 0; i < ordered.Length; i++)
            {
                var c = ordered[i];
                sb.Append("            [")
                  .Append(i)
                  .Append("] = typeof(global::")
                  .Append(c.FullName)
                  .AppendLine("),");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        public static ComponentTypeId GetId<T>() where T : struct => _byType[typeof(T)];");
            sb.AppendLine("        public static ComponentTypeId GetId(Type type) => _byType[type];");
            sb.AppendLine("        public static bool TryGetId(Type type, out ComponentTypeId id) => _byType.TryGetValue(type, out id);");
            sb.AppendLine("        public static Type GetType(int id) => _byId[id];");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource("UnifyEcs.ComponentTypeRegistry.g.cs", sb.ToString());
        }

        private static string GetSafeIdentifier(string fullName)
        {
            var sb = new StringBuilder(fullName.Length + 8);
            foreach (var ch in fullName)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_')
                {
                    sb.Append(ch);
                }
                else
                {
                    sb.Append('_');
                }
            }

            if (sb.Length == 0)
            {
                return "_Component";
            }

            if (!char.IsLetter(sb[0]) && sb[0] != '_')
            {
                sb.Insert(0, '_');
            }

            return sb.ToString();
        }

        private static string GetNamespace(INamedTypeSymbol symbol)
        {
            var ns = symbol.ContainingNamespace;
            return ns is null || ns.IsGlobalNamespace ? string.Empty : ns.ToDisplayString();
        }

        private static void EmitArchBackend(
            SourceProductionContext context,
            IReadOnlyList<ComponentModel> components,
            IReadOnlyList<SystemModel> systems,
            GeneratorConfig config)
        {
            var emitter = new ArchBackendEmitter();

            foreach (var system in systems)
            {
                var missing = GetMissingFeaturesForBackend(system, EcsBackend.Arch);

                // If there are unsupported features and the effective policy for any of them
                // is NoOp, emit a simple no-op IArchSystem stub instead of a full implementation.
                if (missing != EcsFeature.None && ShouldEmitNoOpStub(missing, config))
                {
                    var stubSource = emitter.EmitNoOpStub(system);
                    if (string.IsNullOrWhiteSpace(stubSource))
                        continue;

                    var stubHintName = system.FullName.Replace('.', '_') + ".Arch.g.cs";
                    context.AddSource(stubHintName, stubSource);
                    continue;
                }

                var emulated = GetEmulatedFeaturesForBackend(system, EcsBackend.Arch, config);
                var emulateReactive = (emulated & EcsFeature.Reactive) == EcsFeature.Reactive;
                var supportLevel = emulateReactive
                    ? FeatureSupportLevel.Emulated
                    : FeatureSupportLevel.Native;

                var source = emitter.EmitSystem(system, supportLevel);
                if (string.IsNullOrWhiteSpace(source))
                    continue;

                var hintName = system.FullName.Replace('.', '_') + ".Arch.g.cs";
                context.AddSource(hintName, source);

                // If this system requests reactive features and the policy is set to Emulate
                // for Arch, emit a separate partial that provides reactive emulation helpers.
                if (emulateReactive && system.ReactiveHandlers is { Length: > 0 })
                {
                    var reactiveSource = emitter.EmitReactiveEmulation(system);
                    if (!string.IsNullOrWhiteSpace(reactiveSource))
                    {
                        var reactiveHintName = system.FullName.Replace('.', '_') + ".Arch.Reactive.g.cs";
                        context.AddSource(reactiveHintName, reactiveSource);
                    }
                }
            }
        }

        private static EcsFeature GetMissingFeaturesForBackend(SystemModel system, EcsBackend backend)
        {
            if (system.Requirements is null || system.Requirements.Length == 0)
                return EcsFeature.None;

            var supported = BackendCapabilities.GetNativeFeatures(backend) |
                            BackendCapabilities.GetEmulatableFeatures(backend);

            var missing = EcsFeature.None;

            foreach (var requirement in system.Requirements)
            {
                var required = requirement.Features;
                var missingForRequirement = required & ~supported;
                if (missingForRequirement != EcsFeature.None)
                {
                    missing |= missingForRequirement;
                }
            }

            return missing;
        }

        private static bool ShouldEmitNoOpStub(EcsFeature missing, GeneratorConfig config)
        {
            if (missing == EcsFeature.None)
                return false;

            // 1. Per-feature overrides: if any missing feature has a NoOp policy, no-op.
            foreach (EcsFeature feature in Enum.GetValues(typeof(EcsFeature)))
            {
                if (feature == EcsFeature.None)
                    continue;

                if ((missing & feature) != feature)
                    continue;

                if (config.FeaturePolicies.TryGetValue(feature, out var policy) &&
                    policy == MissingFeatureBehavior.NoOp)
                {
                    return true;
                }
            }

            // 2. Fall back to global policy.
            return config.GlobalMissingFeaturePolicy == MissingFeatureBehavior.NoOp;
        }

        private static EcsFeature GetEmulatedFeaturesForBackend(SystemModel system, EcsBackend backend, GeneratorConfig config)
        {
            if (system.Requirements is null || system.Requirements.Length == 0)
                return EcsFeature.None;

            var native      = BackendCapabilities.GetNativeFeatures(backend);
            var emulatable  = BackendCapabilities.GetEmulatableFeatures(backend);
            var emulated    = EcsFeature.None;

            foreach (var requirement in system.Requirements)
            {
                foreach (EcsFeature feature in Enum.GetValues(typeof(EcsFeature)))
                {
                    if (feature == EcsFeature.None)
                        continue;

                    if ((requirement.Features & feature) != feature)
                        continue;

                    // Only consider features that are emulatable but not natively supported.
                    if ((emulatable & feature) != feature)
                        continue;

                    if ((native & feature) == feature)
                        continue;

                    var policy = ResolveFeaturePolicy(feature, config);
                    if (policy == MissingFeatureBehavior.Emulate)
                    {
                        emulated |= feature;
                    }
                }
            }

            return emulated;
        }

        private static MissingFeatureBehavior ResolveFeaturePolicy(EcsFeature feature, GeneratorConfig config)
        {
            if (config.FeaturePolicies.TryGetValue(feature, out var policy))
                return policy;

            return config.GlobalMissingFeaturePolicy;
        }

        private static void EmitDebugSummary(
            SourceProductionContext context,
            IReadOnlyList<ComponentModel> components,
            IReadOnlyList<SystemModel> systems)
        {
            if (context.CancellationToken.IsCancellationRequested)
                return;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// UnifyECS Phase 1B debug summary (models only)");
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine("namespace UnifyECS");
            sb.AppendLine("{");
            sb.AppendLine("    internal static partial class UnifyEcsDebugGenerated");
            sb.AppendLine("    {");
            sb.AppendLine("        public static void PrintSummary()");
            sb.AppendLine("        {");
            sb.AppendLine($"            Console.WriteLine(\"[UnifyECS] Components: {components.Count}, Systems: {systems.Count}\\n\");");

            if (components.Count > 0)
            {
                sb.AppendLine("            Console.WriteLine(\"[UnifyECS] Components:\");");
                foreach (var c in components.OrderBy(c => c.FullName))
                {
                    sb.AppendLine($"            Console.WriteLine(\"  - {Escape(c.FullName)} (Tag={c.IsTag})\");");
                }
            }

            if (systems.Count > 0)
            {
                sb.AppendLine("            Console.WriteLine(\"[UnifyECS] Systems:\");");
                foreach (var s in systems.OrderBy(s => s.FullName))
                {
                    sb.AppendLine($"            Console.WriteLine(\"  - {Escape(s.FullName)} Phase={s.Phase} Order={s.Order} Queries={s.Queries.Length}\");");
                }
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource("UnifyEcs.DebugSummary.g.cs", sb.ToString());
        }

        private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
