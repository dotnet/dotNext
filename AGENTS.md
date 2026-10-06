# AGENTS.md

Instructions for AI coding agents working on the .NEXT repository.

## Pull Requests
Read [CONTRIBUTING.md](CONTRIBUTING.md) before making changes or opening a PR. It defines the rules for branches, labels, unit tests and backward compatibility. In particular, **all PRs must target the `develop` branch**.

## Repository Layout
- [Solution File](src/DotNext.slnx) - the solution. Libraries: `DotNext`, `DotNext.IO`, `DotNext.Threading`, `DotNext.Unsafe`, `DotNext.Metaprogramming`, `DotNext.MaintenanceServices`, `cluster/DotNext.Net.Cluster`, `cluster/DotNext.AspNetCore.Cluster`.
- Tests:
  - [Unit Tests](src/DotNext.Tests) - unit tests for all libraries
  - [AOT Tests](src/DotNext.Aot.Tests) - tests running under Native AOT.
- [Benchmarks](src/DotNext.Benchmarks) - benchmarks.
- [Examples](src/examples) - examples.
- [AI Skills](ai/plugins/dotnext-skills) - Agent Skills for users of the libraries. Update them if you change the public API they describe.
- [Changelog](CHANGELOG.md) - release notes, grouped by NuGet package.
- Documentation lives in the separate `gh-pages` branch (`docs/` folder, docfx). It is not part of `develop`.

## Build
- .NET SDK version is pinned in [global.json](global.json). All projects target `net10.0` with the latest C# version and nullable reference types enabled.
- Build: `dotnet build src/DotNext.slnx`
- Libraries are marked `IsAotCompatible`. New code must not introduce trimming or AOT warnings (IL2xxx/IL3xxx). Don't suppress them without a justification.
- Public members require XML documentation comments (documentation file generation is enabled).
- Code style is defined by [editorconfig](src/.editorconfig) file. Follow the style of the surrounding code.

## Coding Rules
- Avoid third-party dependencies. Don't add package references without discussion in the PR. Package versions are managed centrally in [Directory.Packages.props](src/Directory.Packages.props), don't specify versions in `.csproj` files.
- Avoid Reflection in new code, especially on hot paths. Where it's unavoidable, it must be trimming/AOT-safe (see Build).
- Prefer `Span<T>` and `ReadOnlySpan<T>` over arrays in synchronous code.
- Prefer `Memory<T>` and `ReadOnlyMemory<T>` over arrays in asynchronous code.
- Prefer `ValueTask` and `ValueTask<T>` over `Task` and `Task<T>` in asynchronous method implementations.
- The preferences above apply to new APIs and internal code. Don't change existing public signatures: it's a breaking change.
- Keep comments short and clear. Large comments are needed only when complex or concurrent behavior requires explanation.

## Tests
Tests use xunit.v3 on Microsoft.Testing.Platform. The project must be passed via `--project`, and VSTest `--filter` expressions don't work.
- All tests: `dotnet test --project src/DotNext.Tests/DotNext.Tests.csproj`
- Single test or class:
  ```
  dotnet test --project src/DotNext.Tests/DotNext.Tests.csproj --filter-method "*.TestName"
  dotnet test --project src/DotNext.Tests/DotNext.Tests.csproj --filter-class "*.ClassName"
  ```
- AOT tests (linux-x64):
  ```
  dotnet publish src/DotNext.Aot.Tests/DotNext.Aot.Tests.csproj -c Release -o src/DotNext.Aot.Tests/bin/aot -p:PublishAot=true
  src/DotNext.Aot.Tests/bin/aot/DotNext.Aot.Tests
  ```
- Concurrency tests can be flaky under load. If a test hangs rarely, don't just rerun it until it passes: report it.

## Pitfalls
- `[UnsafeAccessor]` methods declared as local functions inside a generic method fail to bind at runtime (`MissingMethodException`) even if the target member exists. Declare them as class-level `static extern` methods.
- Parameters widened with `[UnsafeAccessorType]` are type-checked at runtime: a wrong argument type throws `InvalidCastException`.
