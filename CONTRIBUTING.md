# Contributing to UnifyECS

Thanks for your interest in contributing to **UnifyECS**!

This project is an MIT-licensed source-generation-based abstraction layer for writing backend-agnostic ECS game code. Contributions are welcome in the form of bug fixes, improvements to code generation, documentation, new backend adapters, and tests.

## Project layout

The C# code for this project lives under:

- `dotnet/src/UnifyEcs.Core` – core types, interfaces, and attributes
- `dotnet/src/UnifyEcs.Generators` – source generator pipeline
- `dotnet/src/UnifyEcs.Runtime.*` – backend-specific runtime implementations
- `dotnet/src/UnifyEcs.Sample.*` – sample games demonstrating usage
- `dotnet/UnifyGrid.sln` – solution for the C# projects

Documentation and design documents live under `docs/`:

- `docs/rfcs/` – Request for Comments design documents
- `docs/specs/` – Feature specifications
- `docs/handovers/` – Session handover documentation
- `docs/reviews/` – Code review reports

## Development workflow

1. **Clone** the repository and open the solution:
   - Open `dotnet/UnifyGrid.sln` in your preferred IDE (Rider, Visual Studio, VS Code + C#).

2. **Build** the solution:
   - Use your IDE's build command, or from the `dotnet` directory run:
     - `dotnet build UnifyGrid.sln`

3. **Run tests** before submitting changes:
   - From the `dotnet` directory:
     - `dotnet test UnifyGrid.sln`

4. **Add tests**:
   - For any non-trivial change, please add or update tests.
   - Where possible, keep tests close to the APIs they validate.

## Pull requests

- Keep PRs **small and focused** when possible.
- Describe the **motivation**, **approach**, and **any breaking changes** in the PR description.
- If you are changing core architecture or adding new features, please reference relevant RFCs (under `docs/rfcs/`) or create a new one.
- Follow the RFC process for significant architectural decisions.

## Code style

- Follow existing C# style in the repository (PascalCase types, camelCase locals, etc.).
- Prefer explicit types over `var` in public APIs; usage inside methods may use `var` where it improves readability.
- Avoid introducing new dependencies unless they are clearly justified.
- Keep source generators efficient and avoid reflection where possible.

## Adding new backend adapters

If you'd like to add support for a new ECS backend:

1. Review RFC-0005 (Backend Adapters) for architecture guidelines.
2. Create a new runtime project under `dotnet/src/UnifyEcs.Runtime.<Backend>/`.
3. Add corresponding generator support in `dotnet/src/UnifyEcs.Generators/`.
4. Create a sample game demonstrating the backend.
5. Document any backend-specific constraints or limitations.

## Licensing

By submitting a contribution, you agree that your work will be licensed under the
same license as this project, the **MIT License** (see `LICENSE`).
