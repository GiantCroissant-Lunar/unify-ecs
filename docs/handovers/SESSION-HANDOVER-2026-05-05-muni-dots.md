# SESSION HANDOVER - UnifyECS and Muni DOTS adoption

Session date (local): 2026-05-05
Repo: `C:\lunar-horse\plate-projects\unify-ecs`
Related consumer: `C:\lunar-horse\yokan-projects\muni-dungeon`

---

## 0. Quick status

- Muni Dungeon should use Unity DOTS directly through Unity Package Manager packages in the Unity hosts.
- Do not block Muni DOTS adoption on UnifyECS Friflo/Flecs sample packaging.
- UnifyECS source generation is not yet ready to generate Unity.Entities code for Muni.
- The current generator has a `Dots` backend enum/capability model, but no DOTS backend emitter was found.

---

## 1. Friflo sample pack blocker

The `UnifyEcs.Sample.FrifloGame` sample originally had no `UnifyEcsBackends` property. Because `UnifyEcs.Generators.EcsGenerator.CreateGeneratorConfig` defaults missing backends to `Arch`, the Friflo sample generated `.Arch.g.cs` files and failed to compile without Arch runtime references.

Attempted project-level fix:

```xml
<PropertyGroup>
  <UnifyEcsBackends>Friflo</UnifyEcsBackends>
  <UnifyEcsPolicy_Reactive>Emulate</UnifyEcsPolicy_Reactive>
</PropertyGroup>
```

That moved generation to `.Friflo.g.cs`, but exposed Friflo emitter defects:

- It emits `entities.Query().WithAll<T>()`, but Friflo 1.4 exposes generic query methods such as `EntityStore.Query<T>()`.
- It names the chunk entity span `entities`, shadowing the `EntityStore entities` method parameter.
- It assumes tuple element arrays can be indexed directly by generated variable names without matching Friflo's `Chunk<T>` / `ChunkEntities` API shape.

Representative failed command:

```powershell
dotnet build .\dotnet\src\UnifyEcs.Sample.FrifloGame\UnifyEcs.Sample.FrifloGame.csproj -c Release
```

Representative errors:

- `ArchetypeQuery` has no `WithAll` method.
- Local variable/parameter named `entities` shadows an enclosing scope.
- Tuple deconstruction component variables cannot be inferred.

---

## 2. Muni DOTS decision

Muni Dungeon is on Unity `6000.4.4f1`. In this line, Unity Entities and Collections resolve as builtin packages at `6.4.0` in the host package lock files.

Muni already has the intended package boundary:

- `com.giantcroissant.muni-dungeon.runtime.world-sim`
- `com.giantcroissant.muni-dungeon.runtime.ecs-bridge`

Those packages already declare:

```json
"com.unity.collections": "6.4.0",
"com.unity.entities": "6.4.0"
```

The practical Muni path is:

1. Make DOTS explicit in the Unity host manifests.
2. Keep DOTS code in `runtime.world-sim` and the DI/ECS boundary in `runtime.ecs-bridge`.
3. Use plain Unity.Entities code first.
4. Revisit UnifyECS source generation only after a real DOTS backend emitter exists.

---

## 3. Can Muni reuse UnifyECS source generation?

Not yet for DOTS output.

Verified state:

- `UnifyEcs.Generators` has `EcsBackend.Dots`.
- `BackendCapabilities` describes DOTS native and emulatable features.
- No `EmitDotsBackend`, `DotsBackendEmitter`, or Unity.Entities emitter implementation was found.

Recommended future work:

- Add a `DotsBackendEmitter` that emits Unity.Entities-compatible components/systems.
- Add generator tests using `UnifyEcsBackends=Dots`.
- Validate the generated code inside a Unity host, not only with plain `dotnet test`, because Unity.Entities source-generation and assembly rules matter.
- Only then use UnifyECS source generation in Muni runtime packages.

---

## 4. Revisit checklist

- Decide whether Friflo/Flecs samples should be excluded from package build until their emitters are fixed.
- Fix Friflo query emission against Friflo 1.4 APIs.
- Avoid generated variable names that shadow runtime parameters.
- Implement DOTS backend emission before using UnifyECS generation in Muni.
- Keep the local NuGet feed at `C:\lunar-horse\packages\nuget` for UnifyECS packages consumed by Muni.
