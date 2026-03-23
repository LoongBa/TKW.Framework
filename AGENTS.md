# AGENTS.md - TKW Framework Developer Guide

This document provides guidance for AI agents working in this codebase.

## 1. Build, Test & Development Commands

### Build Commands
```bash
dotnet build TKW.Framework.sln
dotnet build Domain/TKWF.Domain.csproj
dotnet build TKW.Framework.sln -c Release
```

### Running Tests (MSTest)
```bash
dotnet test
dotnet test Test/DomainTest_Test/DomainTest_Test.csproj
dotnet test Test/DomainTest_Test/DomainTest_Test.csproj --filter "Name=GetUsers"
dotnet test --filter "FullyQualifiedName~DomainTest_Test.DomainTest.GetUsers"
dotnet test -v n
```

### Code Generation (xCodeGen)
```bash
dotnet run --project xCodeGen/xCodeGen.Cli/xCodeGen.Cli.csproj -- --help
```

---

## 2. Code Style Guidelines

### Language & Configuration
- Target: **.NET 10+**, **C# 12+** with `LangVersion:latest`
- **Nullable reference types enabled**: `<Nullable>enable</Nullable>`

### Namespace Conventions
```
TKW.Framework.[ModuleName]
Examples: TKW.Framework.Domain, TKW.Framework.Common.Tools, TKW.Framework.Domain.Session
```

### Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Interfaces | `I` + PascalCase | `IDomainService`, `IUserInfo` |
| Abstract Classes | `*Base` or Domain prefix | `DomainServiceBase`, `DomainHostInitializerBase` |
| Concrete Services | `*Service` or `*Manager` | `UserService`, `DepartmentManager` |
| Domain Controllers | `*Controller` | `UserController` |
| Contracts (AOP) | `I*Contract` | `IUserControllerContract` |
| Attributes | `*Attribute` | `DomainFilterAttribute` |
| Generic Type | `TUserInfo` | `DomainServiceBase<TUserInfo>` |

### Import Ordering
1. System namespaces (`System.*`)
2. Third-party packages (`Microsoft.*`, `Autofac.*`)
3. Internal TKW framework (`TKW.Framework.*`)
4. Project-specific imports

### Type Guidelines

#### Nullable Types
- Use `string?`, `T?` when null is valid
- Use non-nullable by default
- Use `null!` for DI-initialized properties

#### Generic Constraints
```csharp
public class DomainServiceBase<TUserInfo> : IDomainService
    where TUserInfo : class, IUserInfo, new()
```

### Error Handling

#### Null Checks - Use EnsureNotNull Extension
```csharp
// CORRECT
User = user.EnsureNotNull(nameof(user));

// WRONG
if (user == null) throw new ArgumentNullException(nameof(user));
```

#### Exception Philosophy
- **Domain Layer**: Only log, do not handle/transform exceptions
- **DomainException**: Framework-defined for business rule violations
- **Host Layer (Web)**: Maps exceptions to HTTP responses via middleware
- **Never swallow exceptions silently**

### Property & Field Conventions
- Use properties (auto-implemented when possible)
- Use `readonly` for immutable fields
- Use `private set` when setter is internal-only
- Use `[JsonIgnore]` for non-serialized properties

```csharp
public string SessionKey { get; set; } = string.Empty;
public bool IsAuthenticated { get; internal set; }

[JsonIgnore]
internal DomainHost<TUserInfo>? AttachedHost { get; set; }

public TUserInfo UserInfo { get; set; } = null!;
```

### Dependency Injection Pattern

#### Registering Domain Services
```csharp
containerBuilder.AddController<DepartmentService>();
containerBuilder.AddDomainController<IUserControllerContract, UserController>();
containerBuilder.UseSessionManager(new SessionManager<ProjectUser>());
```

#### Consuming Services (Required Pattern)
```csharp
// NEVER inject directly from DI container
// CORRECT: Use DomainUser context
var user = DomainHost.Root.UserHelper<ProjectUser, SessionHelper>().NewGuestSession();
var service = user.Use<DepartmentService>();
```

### Documentation
- Use XML documentation for public APIs (`<summary>`, `<param>`, `<returns>`)
- Chinese language in existing docs (consistent with codebase)
- Avoid unnecessary comments - code should be self-explanatory

---

## 3. Key Frameworks & Libraries
- **DI**: Autofac (`Autofac.Extensions.DependencyInjection`)
- **ORM**: Entity Framework Core, FreeSql
- **Logging**: Microsoft.Extensions.Logging
- **Testing**: MSTest (`Microsoft.NET.Test.Sdk`)

---

## 4. Important Patterns

### Domain Service vs Controller
- **DomainServiceBase**: Internal business logic, inherits `IDomainService`
- **DomainController**: Adds AOP via `IAopContract`, can be intercepted

### User Context Access
```csharp
// From domain service
protected TService Use<TService>() where TService : IDomainService
{
    return User.Use<TService>();
}
```

### Session Management
- Always create session via `DomainUserHelper`
- Session types: Guest, Authenticated, Retrieved

---

Last updated: 2026-03-17
