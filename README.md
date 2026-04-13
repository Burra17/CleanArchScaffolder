# Clean Arch CLI

A .NET global tool that scaffolds a complete **Clean Architecture** solution in seconds. One command gives you a fully wired solution with proper project references, dependency injection, Entity Framework Core, and a test project — ready to build and run.

## Features

- Interactive CLI powered by [Spectre.Console](https://spectreconsole.net/)
- Four-layer architecture: **Domain**, **Application**, **Infrastructure**, and **Api**
- xUnit test project with FluentAssertions
- Pre-configured project references following the dependency rule
- NuGet packages installed automatically (EF Core, MediatR, FluentValidation, Scalar)
- `AppDbContext` and `IAppDbContext` scaffolded and registered
- `DependencyInjection` extension methods in Application and Infrastructure
- `Program.cs` configured with controllers, OpenAPI, and Scalar
- Connection string pre-configured in `appsettings.json`
- `.gitignore` (standard Visual Studio template)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

## Installation

```bash
dotnet tool install -g CleanArchScaffolder
```

## Usage

```bash
clean-arch
```

You will be prompted to enter a solution name. The tool then generates the full project structure in the current directory.

## Generated Structure

```
MySolution.slnx
.gitignore
src/
  MySolution.Domain/
    Entities/
    ValueObjects/
    Enums/
  MySolution.Application/
    Interfaces/
      IAppDbContext.cs
    Features/
    DTOs/
    DependencyInjection.cs
  MySolution.Infrastructure/
    Persistence/
      AppDbContext.cs
    Repositories/
    DependencyInjection.cs
  MySolution.Api/
    Controllers/
    Program.cs
    appsettings.json
tests/
  MySolution.Application.Tests/
```

## Project References

```
Api --> Infrastructure --> Application --> Domain
Application.Tests --> Application
```

The Api layer depends on Infrastructure, which depends on Application, which depends on Domain. This enforces the Clean Architecture dependency rule — inner layers never reference outer layers.

## Installed Packages

| Project | Packages |
|---------|----------|
| **Infrastructure** | `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools` |
| **Api** | `Microsoft.EntityFrameworkCore.Design`, `Scalar.AspNetCore` |
| **Application** | `MediatR`, `FluentValidation`, `FluentValidation.DependencyInjectionExtensions` |
| **Application.Tests** | `xunit`, `FluentAssertions` |
| **Domain** | — |

## Building from Source

```bash
git clone <repo-url>
cd CleanArchScaffolder
dotnet pack
dotnet tool install -g CleanArchScaffolder --add-source ./nupkg
```

## License

MIT
