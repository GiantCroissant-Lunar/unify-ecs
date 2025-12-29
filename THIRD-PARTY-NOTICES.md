# Third-Party Notices

UnifyECS builds upon and integrates with several excellent open-source ECS implementations
and libraries. This project provides a unified abstraction layer over these backends.

## ECS Backend Dependencies

The following ECS implementations are used as runtime backends:

- **Arch**
  - License: Apache License 2.0
  - NuGet: `Arch` (v2.1.0)
  - Repository: https://github.com/genaray/Arch
  - Description: High-performance archetype-based ECS

- **Flecs.NET**
  - License: MIT
  - NuGet: `Flecs.NET.Debug`, `Flecs.NET.Bindings.Debug` (v4.0.4-build.546)
  - Repository: https://github.com/BeanCheeseBurrito/Flecs.NET
  - Description: .NET bindings for the Flecs ECS library

- **Friflo.Engine.ECS**
  - License: LGPL-3.0
  - NuGet: `Friflo.Engine.ECS` (v1.3.2)
  - Repository: https://github.com/friflo/Friflo.Json.Fliox
  - Description: High-performance ECS with JSON serialization support

## License Information

Each backend library maintains its own license. When using UnifyECS with a specific
backend, you must comply with that backend's license terms:

- **Apache License 2.0** (Arch): Permissive license requiring attribution
- **MIT License** (Flecs.NET): Permissive license requiring attribution
- **LGPL-3.0** (Friflo): Requires disclosure when distributing modified versions

UnifyECS itself is licensed under the **MIT License** (see `LICENSE`).

## Attribution

We are grateful to the maintainers and contributors of these ECS implementations
for their excellent work in the .NET and game development communities:

- Arch by genaray and contributors
- Flecs.NET by BeanCheeseBurrito and contributors
- Friflo.Engine.ECS by friflo and contributors

The notices above are informational. Please consult each project's original
license for complete terms and conditions.
