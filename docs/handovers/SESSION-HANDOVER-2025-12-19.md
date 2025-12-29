# SESSION HANDOVER — UnifyECS (Generator + Arch backend)

Session date (local): 2025-12-19
Repo: `d:\lunar-snake\personal-work\plate-projects\unify-ecs`
Primary focus: UnifyECS generator enhancements + Arch backend correctness + test stability
Requested by: user (starting a new session)

---

## 0. Quick status (read this first)

- Status: generator work partially complete; Arch runtime tests still unstable (TESTRUNABORT in full run)
- Build: `dotnet build` for Arch runtime tests project succeeded in Release
- Tests:
- Isolated test `GeneratedArchSystemsTests.DeferredAndImmediateSystems_InteractCorrectly` passes
- Full `dotnet test` for `UnifyEcs.Runtime.Arch.Tests` in Release can hit `TESTRUNABORT` (process abort)
- Parallelization: attempted to re-enable; instability persists
- Entity mapping: ArchWorld mapping was refactored (see section 4)
- Pending deliverable from earlier objective: more generator tests for Any/None/Exclusive query emission + entity param variants (not finished in this session)

---

## 1. Why this handover exists

- You asked for a detailed handover doc so you can start a new session with full context.
- The session touched both:
- Generator-level correctness (Arch code emission, attribute parsing)
- Runtime-level stability (ArchWorld mapping, DefaultCommandBuffer interactions)

---

## 2. High-level session goals (what we tried to accomplish)

- Improve the Arch backend generator:
- Ensure QueryDescription emission supports filters correctly
- Ensure the generator handles attribute arrays robustly (Roslyn TypedConstant)
- Strengthen snapshot tests to cover new behaviors
- Stabilize Arch runtime tests:
- Remove cross-test shared state
- Fix any crash/abort caused by parallel execution
- Re-enable xUnit parallelization if possible

---

## 3. Repository state at end of session

### 3.1 Git summary (tracked modifications)

Captured from `git diff --stat`:

- `dotnet/src/UnifyEcs.Generators/Backends.Arch.cs`  
  94 insertions/deletions (net + changes)
- `dotnet/src/UnifyEcs.Generators/EcsGenerator.cs`  
  74 insertions/deletions (net + changes)
- `dotnet/tests/UnifyEcs.Generators.Tests/ArchBackendSnapshotTests.cs`  
  268 insertions/deletions (net + changes)
- `dotnet/tests/UnifyEcs.Generators.Tests/UnifyEcs.Generators.Tests.csproj`  
  1 line change

Captured from `git diff --name-only`:

- `dotnet/src/UnifyEcs.Generators/Backends.Arch.cs`
- `dotnet/src/UnifyEcs.Generators/EcsGenerator.cs`
- `dotnet/tests/UnifyEcs.Generators.Tests/ArchBackendSnapshotTests.cs`
- `dotnet/tests/UnifyEcs.Generators.Tests/UnifyEcs.Generators.Tests.csproj`

### 3.2 Git status (porcelain)

Captured from `git status --porcelain`:

- ` M dotnet/src/UnifyEcs.Generators/Backends.Arch.cs`
- ` M dotnet/src/UnifyEcs.Generators/EcsGenerator.cs`
- ` M dotnet/tests/UnifyEcs.Generators.Tests/ArchBackendSnapshotTests.cs`
- ` M dotnet/tests/UnifyEcs.Generators.Tests/UnifyEcs.Generators.Tests.csproj`
- `?? .vscode/`
- `?? README.md`
- `?? build/`
- `?? docs/rfcs/README.md`
- `?? docs/rfcs/RFC-0001-core-architecture.md`
- `?? docs/rfcs/RFC-0002-feature-capability-system.md`
- `?? docs/rfcs/RFC-0003-attribute-api-design.md`
- `?? docs/rfcs/RFC-0004-source-generator-pipeline.md`
- `?? docs/rfcs/RFC-0005-backend-adapters.md`
- `?? docs/rfcs/RFC-0006-missing-feature-policies.md`
- `?? docs/rfcs/RFC-0007-multi-backend-orchestration.md`
- `?? docs/rfcs/RFC-0009-component-registry-type-mapping.md`
- `?? docs/rfcs/RFC-0010-dots-backend-constraints.md`
- `?? docs/rfcs/RFC-0012-structural-changes-mutations.md`
- `?? docs/rfcs/RFC-0014-multi-world-and-query-model.md`
- `?? dotnet/Directory.Build.props`
- `?? dotnet/src/UnifyEcs.Analyzers/`
- `?? dotnet/src/UnifyEcs.Attributes/`
- `?? dotnet/src/UnifyEcs.Core/`
- `?? dotnet/src/UnifyEcs.Generators/Backends.Entitas.cs`
- `?? dotnet/src/UnifyEcs.Generators/EcsGenerator.Models.cs`
- `?? dotnet/src/UnifyEcs.Generators/IsExternalInit.cs`
- `?? dotnet/src/UnifyEcs.Generators/UnifyEcs.Generators.csproj`
- `?? dotnet/src/UnifyEcs.Generators/bin/`
- `?? dotnet/src/UnifyEcs.Generators/obj/`
- `?? dotnet/src/UnifyEcs.Runtime.Arch/`
- `?? dotnet/src/UnifyEcs.Sample.ArchGame/`
- `?? dotnet/tests/UnifyEcs.Analyzers.Tests/UnifyEcs.Analyzers.Tests.csproj`
- `?? dotnet/tests/UnifyEcs.Analyzers.Tests/UnifyEcsAnalyzer_Uecs019Tests.cs`
- `?? dotnet/tests/UnifyEcs.Analyzers.Tests/bin/`
- `?? dotnet/tests/UnifyEcs.Analyzers.Tests/obj/`
- `?? dotnet/tests/UnifyEcs.Attributes.Tests/`
- `?? dotnet/tests/UnifyEcs.Core.Tests/`
- `?? dotnet/tests/UnifyEcs.Generators.Tests/bin/`
- `?? dotnet/tests/UnifyEcs.Generators.Tests/obj/`
- `?? dotnet/tests/UnifyEcs.Runtime.Arch.Tests/`

Notes:
- There is an unusually large set of untracked content, including `bin/` and `obj/`.
- This strongly suggests `.gitignore` (or repo organization) is incomplete or not applied.
- Before committing anything, confirm the intended repo baseline.

### 3.3 Line endings warnings observed

Git printed warnings like:
- `LF will be replaced by CRLF the next time Git touches it`

Impact:
- These warnings indicate your working tree likely has mixed line endings.
- Not blocking for tests/build, but noisy.

---

## 4. Core runtime changes (Arch integration)

### 4.1 Arch NuGet package version

- Arch package: `Arch` version `2.1.0`
- Key discovery: `Arch.Core.Entity` does NOT have a public `(id, version)` constructor.
- Therefore: we cannot safely reconstruct `Arch.Core.Entity` from `UnifyECS.Entity` just from numbers.

### 4.2 ArchWorld entity mapping refactor

Purpose:
- Remove prior cross-test shared state and reduce parallel test interference.

Approach:
- Replace the old global mapping approach with a world-scoped, thread-safe mapping:
- `ConditionalWeakTable<World, ConcurrentDictionary<UnifyECS.Entity, Arch.Core.Entity>>`

Where:
- `dotnet/src/UnifyEcs.Runtime.Arch/ArchWorld.cs` (currently present in your working directory; appears untracked)

Behavior:
- On `CreateEntity`, we register the mapping: `RegisterEntity(_world, entity, archEntity)`
- For operations needing the Arch entity (Add/Get/Has/etc), we resolve via:
- `TryGetArchEntity(_world, entity, out archEntity)`
- On `DestroyEntity`, we remove mapping for that entity
- On `Dispose`, we remove the per-world mapping

Expected benefit:
- Avoid a single global map with locks and potential stale state between tests.

Known limitation:
- This still does not eliminate all possible parallel interference:
- It isolates per-world mapping
- But parallel tests can still crash/abort for other reasons (Arch internal state, command buffer, VSTest)

### 4.3 Generator “WorldAdapter” fallback mapping

Context:
- The Arch generator emits a nested `WorldAdapter` type for systems that need deferred structural changes.

What changed:
- The generator was updated so the emitted adapter:
- Registers entities with `global::UnifyECS.ArchWorld.RegisterEntity(...)`
- Resolves unknown entities with `global::UnifyECS.ArchWorld.TryGetArchEntity(...)`

Why:
- Avoid invalid entity reconstruction
- Keep a single source of truth for entity mapping

File:
- `dotnet/src/UnifyEcs.Generators/Backends.Arch.cs`

---

## 5. Generator fixes (Roslyn + emission)

### 5.1 Fix: TypedConstant array parsing

Symptom:
- Generator crash:
- `InvalidOperationException: TypedConstant is an array. Use Values property.`

Fix:
- Update `ExtractComponentTypeNames(TypedConstant arg)` to:
- If `arg.Values` is non-empty, read from it
- Else fallback to single type symbol

File:
- `dotnet/src/UnifyEcs.Generators/EcsGenerator.cs`

Note:
- Do not add comments to `ExtractComponentTypeNames` (per your request).

### 5.2 Fix: No-op stub implements correct interface

Symptom:
- Snapshot test expected generated no-op stub to implement `global::UnifyECS.IArchSystem`.

Fix:
- Ensure `EmitNoOpStub` writes:
- `: global::UnifyECS.IArchSystem`

File:
- `dotnet/src/UnifyEcs.Generators/Backends.Arch.cs`

---

## 6. Test changes (Generators tests)

### 6.1 Snapshot test diagnostics improvements

Goal:
- Make generator snapshot failures debuggable.

Change:
- `GetGeneratedSource` now includes:
- Generator diagnostics
- Driver diagnostics
- This reduces “silent” failures.

File:
- `dotnet/tests/UnifyEcs.Generators.Tests/ArchBackendSnapshotTests.cs`

---

## 7. Current failing issue: `TESTRUNABORT` in Arch runtime tests

### 7.1 What is happening

- Running the full test project:
- `dotnet test .\tests\UnifyEcs.Runtime.Arch.Tests\UnifyEcs.Runtime.Arch.Tests.csproj -c Release`
- can fail with:
- `TESTRUNABORT: 測試回合已中止。`

Meaning:
- The test host process aborts.
- This is NOT a typical xUnit assertion failure.

### 7.2 What we know from isolation

This test PASSES when run alone:

- `UnifyEcs.Runtime.Arch.Tests.GeneratedArchSystemsTests.DeferredAndImmediateSystems_InteractCorrectly`

Repro command (passes):

- `dotnet test .\tests\UnifyEcs.Runtime.Arch.Tests\UnifyEcs.Runtime.Arch.Tests.csproj -c Release --filter FullyQualifiedName~UnifyEcs.Runtime.Arch.Tests.GeneratedArchSystemsTests.DeferredAndImmediateSystems_InteractCorrectly --logger "console;verbosity=detailed"`

Conclusion:
- The abort is likely triggered by:
- Inter-test interaction
- Parallel test execution
- A process-level crash due to Arch internal state
- Or `DefaultCommandBuffer` + entity remapping edge cases under different test orderings

### 7.3 Why we did NOT fully fix it

- The abort appears only under a full run.
- The session was spent reducing known shared state (entity mapping) and validating isolated repro.
- Root cause is still unknown.

---

## 8. Repro and verification commands (copy/paste section)

All commands assume working directory:
- `d:\lunar-snake\personal-work\plate-projects\unify-ecs\dotnet`

### 8.1 Build the Arch test project (Release)

- `dotnet build .\tests\UnifyEcs.Runtime.Arch.Tests\UnifyEcs.Runtime.Arch.Tests.csproj -c Release`

Observed result (this session):
- Build succeeded

### 8.2 Run full Arch runtime test project (Release)

- `dotnet test .\tests\UnifyEcs.Runtime.Arch.Tests\UnifyEcs.Runtime.Arch.Tests.csproj -c Release`

Observed result (this session):
- Can fail with `TESTRUNABORT`

### 8.3 Run the isolated system interaction test

- `dotnet test .\tests\UnifyEcs.Runtime.Arch.Tests\UnifyEcs.Runtime.Arch.Tests.csproj -c Release --filter FullyQualifiedName~UnifyEcs.Runtime.Arch.Tests.GeneratedArchSystemsTests.DeferredAndImmediateSystems_InteractCorrectly --logger "console;verbosity=detailed"`

Observed result (this session):
- Pass

### 8.4 Run with VSTest blame to capture crash evidence

Try (next session):
- `dotnet test .\tests\UnifyEcs.Runtime.Arch.Tests\UnifyEcs.Runtime.Arch.Tests.csproj -c Release --blame --logger "console;verbosity=normal"`

If it crashes, VSTest should emit additional artifacts.

---

## 9. Next-session plan (recommended)

### 9.1 Establish baseline and reduce noise

- Confirm what is expected to be tracked vs untracked.
- Add/update `.gitignore` so `bin/` and `obj/` do not show as untracked.
- Ensure you know what branch/commit you are on.

### 9.2 Confirm whether parallelism is the actual trigger

Run the test project:
- With default parallelization
- Then with explicit xUnit parallelization disabled

Option paths:
- Option A: assembly attribute to disable parallelization
- Option B: xUnit collection to serialize only the Arch runtime tests

### 9.3 Instrument to find crash point

- Use `dotnet test --blame` to produce crash logs
- Narrow scope with `--filter` by class name
- Binary search which test combination triggers abort

### 9.4 Confirm mapping correctness

- Ensure all entity creation paths register mapping:
- ArchWorld.CreateEntity
- Any external inner-world entity creation in tests must call ArchWorld.RegisterEntity
- Generator-emitted WorldAdapter must register entities

### 9.5 Decide on policy: “serialize Arch tests” vs “fix root crash”

- If crash is inside Arch 2.1.0, you may choose to serialize tests for stability.
- If crash is in our code (command buffer, mapping, world lifetime), fix root.

---

## 10. Notes on DisableParallelization.cs (important)

File:
- `dotnet/tests/UnifyEcs.Runtime.Arch.Tests/DisableParallelization.cs`

At end of session:
- The file content is empty.

Important:
- If you want to temporarily disable xUnit parallelization again, the file should contain:
- `using Xunit;`
- `[assembly: CollectionBehavior(DisableTestParallelization = true)]`

Caveat:
- Restoring it was attempted but failed due to the file being empty and patch tooling constraints.

---

## 11. Unresolved work from the original request (generator tests)

Original objective included:
- New generator tests for:
- Any/None/Exclusive query emission
- Entity parameter variants

Status:
- Some generator tests were updated/enhanced.
- The full desired coverage set is not finished.

Next session:
- Expand snapshot tests in `ArchBackendSnapshotTests` to assert:
- `.WithAny<T>()`
- `.WithNone<T>()`
- `.WithExclusive<T>()`
- Proper entity parameter mapping emission

---

## 12. Files touched (quick list)

Tracked modifications (per git):
- `dotnet/src/UnifyEcs.Generators/Backends.Arch.cs`
- `dotnet/src/UnifyEcs.Generators/EcsGenerator.cs`
- `dotnet/tests/UnifyEcs.Generators.Tests/ArchBackendSnapshotTests.cs`
- `dotnet/tests/UnifyEcs.Generators.Tests/UnifyEcs.Generators.Tests.csproj`

Notable runtime/test files involved in investigation (may be untracked depending on repo state):
- `dotnet/src/UnifyEcs.Runtime.Arch/ArchWorld.cs`
- `dotnet/tests/UnifyEcs.Runtime.Arch.Tests/DefaultCommandBufferWorldAdapterTests.cs`
- `dotnet/tests/UnifyEcs.Runtime.Arch.Tests/ArchWorldTests.cs`
- `dotnet/tests/UnifyEcs.Runtime.Arch.Tests/GeneratedArchSystemsTests.cs`
- `dotnet/tests/UnifyEcs.Runtime.Arch.Tests/DisableParallelization.cs`

---

## 13. Concrete next actions checklist (copy into next session)

- [ ] Run `git status --porcelain` and decide what’s intended to be committed
- [ ] Add `.gitignore` entries for `**/bin/` and `**/obj/` if not already ignored
- [ ] Run full Arch tests with `--blame`:
- [ ] `dotnet test ... -c Release --blame`
- [ ] If abort reproduces, capture and inspect blame output
- [ ] Re-enable temporary serialization if needed:
- [ ] Restore `DisableParallelization.cs` content
- [ ] Expand generator snapshot tests for Any/None/Exclusive emission

---

## 14. Appendix A — Important commands used this session

- `git status --porcelain`
- `git diff --stat`
- `git diff --name-only`
- `dotnet build .\tests\UnifyEcs.Runtime.Arch.Tests\UnifyEcs.Runtime.Arch.Tests.csproj -c Release`
- `dotnet test .\tests\UnifyEcs.Runtime.Arch.Tests\UnifyEcs.Runtime.Arch.Tests.csproj -c Release`
- `dotnet test ... --filter ... --logger "console;verbosity=detailed"`

---

## 15. Appendix B — Space for notes in next session

- Crash signature:
- Suspected tests involved:
- Artifacts produced by `--blame`:
- Decision: serialize tests? yes/no
- Decision: upgrade/downgrade Arch package? yes/no

---

## 16. End of handover

If you want, in the next session I can:
- Finish the original generator test coverage request (Any/None/Exclusive + entity param variants)
- Or focus entirely on eliminating the Arch runtime `TESTRUNABORT`
- Or both, via a 2-phase plan

