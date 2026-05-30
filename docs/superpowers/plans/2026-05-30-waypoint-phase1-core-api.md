# Waypoint Phase 1 — Core API + Data Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the foundational .NET 9 solution with EF Core data layer over Postgres, exposing minimal CRUD HTTP endpoints for projects/issues/comments with workflow-validated transitions. No auth (placeholder `[AllowAnonymous]` — auth comes in Phases 2 and 4). Ships as a `docker compose up` local stack with all integration tests green.

**Architecture:** Single .NET 9 minimal API process on port 8080. EF Core code-first migrations against Postgres 16. Domain logic lives in `Waypoint.Domain` (entities + workflow validation); transport in `Waypoint.Api` (endpoints + middleware + repositories); wire types in `Waypoint.Contracts`. Tests use xUnit + Testcontainers.PostgreSql for real-DB integration. No SPA, no second port, no auth pipeline yet.

**Tech Stack:** .NET 9, ASP.NET Core minimal API, EF Core 9, Npgsql, Postgres 16, xUnit, FluentAssertions, Testcontainers.PostgreSql, NetArchTest.

**Phase 1 scope reminder (from roadmap):** Entities = Project, State, IssueType, Workflow, WorkflowTransition, Issue, Comment, Activity, User. Endpoints = project CRUD, issue CRUD + transition + activity, comment CRUD. Deferred to later phases: Epic, Cycle, Label, Component, IssueIntent, WebhookSubscription, WebhookDelivery, ApiToken, TokenAuditLog, UserSession, Attachment.

---

## File Structure

This plan creates the following file tree. Each entity gets its own file in `Entities/` and `Configurations/`; each endpoint group gets its own file in `Endpoints/`; each repository gets its own file in `Repositories/`. One responsibility per file keeps each file under ~150 lines.

```
plane-replacement/
├── .gitignore                                  # .NET-aware ignore patterns
├── .dockerignore
├── Directory.Build.props                       # shared msbuild props (target framework, nullable, warnings-as-errors)
├── Directory.Packages.props                    # centralized package versions
├── Waypoint.sln
├── docker-compose.yml                          # api + postgres for local dev
├── docs/superpowers/{specs,plans}/             # already exist
├── src/
│   ├── Waypoint.Api/
│   │   ├── Waypoint.Api.csproj
│   │   ├── Program.cs                          # WebApplication wiring
│   │   ├── Dockerfile
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Endpoints/
│   │   │   ├── ProjectEndpoints.cs
│   │   │   ├── IssueEndpoints.cs
│   │   │   └── CommentEndpoints.cs
│   │   ├── Middleware/
│   │   │   ├── RequestIdMiddleware.cs
│   │   │   ├── ErrorEnvelopeMiddleware.cs
│   │   │   └── IdempotencyMiddleware.cs
│   │   ├── Repositories/
│   │   │   ├── IProjectRepository.cs
│   │   │   ├── ProjectRepository.cs
│   │   │   ├── IIssueRepository.cs
│   │   │   ├── IssueRepository.cs
│   │   │   ├── ICommentRepository.cs
│   │   │   └── CommentRepository.cs
│   │   └── Pagination/
│   │       └── Cursor.cs
│   ├── Waypoint.Domain/
│   │   ├── Waypoint.Domain.csproj
│   │   ├── WaypointDbContext.cs
│   │   ├── DesignTimeDbContextFactory.cs       # for `dotnet ef` CLI
│   │   ├── Entities/
│   │   │   ├── Project.cs
│   │   │   ├── State.cs
│   │   │   ├── IssueType.cs
│   │   │   ├── Workflow.cs
│   │   │   ├── WorkflowTransition.cs
│   │   │   ├── Issue.cs
│   │   │   ├── Comment.cs
│   │   │   ├── Activity.cs
│   │   │   └── User.cs
│   │   ├── Enums/
│   │   │   ├── Priority.cs
│   │   │   ├── StateGroup.cs
│   │   │   └── ActorType.cs
│   │   ├── Validation/
│   │   │   └── WorkflowTransitionValidator.cs
│   │   └── Migrations/                         # generated, committed
│   └── Waypoint.Contracts/
│       ├── Waypoint.Contracts.csproj
│       ├── ProjectDto.cs
│       ├── IssueDto.cs
│       ├── StateDto.cs
│       ├── CommentDto.cs
│       ├── ActivityDto.cs
│       ├── CreateProjectRequest.cs
│       ├── CreateIssueRequest.cs
│       ├── UpdateIssueRequest.cs
│       ├── TransitionIssueRequest.cs
│       ├── CreateCommentRequest.cs
│       ├── PagedResponse.cs
│       └── ErrorResponse.cs
└── tests/
    ├── Waypoint.Api.Tests/
    │   ├── Waypoint.Api.Tests.csproj
    │   ├── Fixtures/
    │   │   ├── PostgresFixture.cs              # Testcontainers boot/teardown
    │   │   └── WaypointApiFactory.cs           # WebApplicationFactory<Program>
    │   ├── Endpoints/
    │   │   ├── ProjectEndpointsTests.cs
    │   │   ├── IssueEndpointsTests.cs
    │   │   └── CommentEndpointsTests.cs
    │   └── Middleware/
    │       └── IdempotencyMiddlewareTests.cs
    ├── Waypoint.Domain.Tests/
    │   ├── Waypoint.Domain.Tests.csproj
    │   └── Validation/
    │       └── WorkflowTransitionValidatorTests.cs
    └── Waypoint.Architecture.Tests/
        ├── Waypoint.Architecture.Tests.csproj
        └── BoundaryTests.cs
```

---

## Task 1: Solution scaffold + shared build props

**Files:**
- Create: `Waypoint.sln`
- Create: `.gitignore`
- Create: `.dockerignore`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`

- [ ] **Step 1: Create the solution file**

Run:
```bash
cd C:/Users/mempg/projects/plane-replacement
dotnet new sln -n Waypoint
```
Expected: `The template "Solution File" was created successfully.`

- [ ] **Step 2: Write `.gitignore`**

Create `.gitignore`:
```
# Build outputs
bin/
obj/
*.user
*.suo
.vs/
.vscode/

# .NET tools
.config/dotnet-tools.json.lock

# Node
node_modules/
.next/
out/

# OS / editor
.DS_Store
Thumbs.db

# Secrets — should never appear in repo
appsettings.*.local.json
*.pfx
*.key

# Test outputs
TestResults/
coverage*.xml
StrykerOutput/
```

- [ ] **Step 3: Write `.dockerignore`**

Create `.dockerignore`:
```
**/bin
**/obj
**/.vs
**/.vscode
**/node_modules
**/.next
**/TestResults
**/StrykerOutput
.git
.gitignore
docs/
```

- [ ] **Step 4: Write `Directory.Build.props`**

Create `Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

- [ ] **Step 5: Write `Directory.Packages.props`** (central package management)

Create `Directory.Packages.props`:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="9.0.0" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="9.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="FluentAssertions" Version="6.12.2" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.1.0" />
    <PackageVersion Include="NetArchTest.Rules" Version="1.3.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Verify solution structure**

Run:
```bash
dotnet build
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` (no projects yet, but solution file is valid).

- [ ] **Step 7: Commit**

```bash
git add .gitignore .dockerignore Directory.Build.props Directory.Packages.props Waypoint.sln
git commit -m "chore(scaffold): add solution file and shared build props"
```

---

## Task 2: Create the three source projects

**Files:**
- Create: `src/Waypoint.Domain/Waypoint.Domain.csproj`
- Create: `src/Waypoint.Contracts/Waypoint.Contracts.csproj`
- Create: `src/Waypoint.Api/Waypoint.Api.csproj`

- [ ] **Step 1: Create Domain class library**

Run:
```bash
dotnet new classlib -n Waypoint.Domain -o src/Waypoint.Domain
rm src/Waypoint.Domain/Class1.cs
dotnet sln add src/Waypoint.Domain/Waypoint.Domain.csproj
```

- [ ] **Step 2: Edit `src/Waypoint.Domain/Waypoint.Domain.csproj`** to add EF Core packages

Replace the file contents with:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create Contracts class library (NO EF dependencies — enforced later)**

Run:
```bash
dotnet new classlib -n Waypoint.Contracts -o src/Waypoint.Contracts
rm src/Waypoint.Contracts/Class1.cs
dotnet sln add src/Waypoint.Contracts/Waypoint.Contracts.csproj
```

- [ ] **Step 4: Create Api project**

Run:
```bash
dotnet new webapi -n Waypoint.Api -o src/Waypoint.Api --use-minimal-apis --use-program-main false
rm src/Waypoint.Api/WeatherForecast.cs
dotnet sln add src/Waypoint.Api/Waypoint.Api.csproj
```

- [ ] **Step 5: Edit `src/Waypoint.Api/Waypoint.Api.csproj`** to add project references

Replace the file contents with:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="..\Waypoint.Domain\Waypoint.Domain.csproj" />
    <ProjectReference Include="..\Waypoint.Contracts\Waypoint.Contracts.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Replace `src/Waypoint.Api/Program.cs` with a minimal stub**

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGet("/healthz/live", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program; // for WebApplicationFactory<Program> in tests
```

- [ ] **Step 7: Verify it all builds**

Run:
```bash
dotnet build
```
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 8: Smoke-run the API**

Run:
```bash
dotnet run --project src/Waypoint.Api --urls http://localhost:5050 &
sleep 3
curl -s http://localhost:5050/healthz/live
kill %1
```
Expected output: `{"status":"ok"}`.

- [ ] **Step 9: Commit**

```bash
git add src/ Waypoint.sln
git commit -m "chore(scaffold): add Domain, Contracts, and Api projects"
```

---

## Task 3: Create the three test projects

**Files:**
- Create: `tests/Waypoint.Domain.Tests/Waypoint.Domain.Tests.csproj`
- Create: `tests/Waypoint.Api.Tests/Waypoint.Api.Tests.csproj`
- Create: `tests/Waypoint.Architecture.Tests/Waypoint.Architecture.Tests.csproj`

- [ ] **Step 1: Create Domain.Tests**

Run:
```bash
dotnet new xunit -n Waypoint.Domain.Tests -o tests/Waypoint.Domain.Tests
rm tests/Waypoint.Domain.Tests/UnitTest1.cs
dotnet sln add tests/Waypoint.Domain.Tests/Waypoint.Domain.Tests.csproj
```

Replace `tests/Waypoint.Domain.Tests/Waypoint.Domain.Tests.csproj` contents with:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Waypoint.Domain\Waypoint.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create Api.Tests**

Run:
```bash
dotnet new xunit -n Waypoint.Api.Tests -o tests/Waypoint.Api.Tests
rm tests/Waypoint.Api.Tests/UnitTest1.cs
dotnet sln add tests/Waypoint.Api.Tests/Waypoint.Api.Tests.csproj
```

Replace `tests/Waypoint.Api.Tests/Waypoint.Api.Tests.csproj` contents with:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Waypoint.Api\Waypoint.Api.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Testcontainers.PostgreSql" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create Architecture.Tests**

Run:
```bash
dotnet new xunit -n Waypoint.Architecture.Tests -o tests/Waypoint.Architecture.Tests
rm tests/Waypoint.Architecture.Tests/UnitTest1.cs
dotnet sln add tests/Waypoint.Architecture.Tests/Waypoint.Architecture.Tests.csproj
```

Replace `tests/Waypoint.Architecture.Tests/Waypoint.Architecture.Tests.csproj` contents with:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Waypoint.Api\Waypoint.Api.csproj" />
    <ProjectReference Include="..\..\src\Waypoint.Domain\Waypoint.Domain.csproj" />
    <ProjectReference Include="..\..\src\Waypoint.Contracts\Waypoint.Contracts.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="NetArchTest.Rules" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Verify everything builds**

Run:
```bash
dotnet build
```
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 5: Run the (empty) test suites**

Run:
```bash
dotnet test
```
Expected: `Passed!  - Failed: 0, Passed: 0, Skipped: 0`.

- [ ] **Step 6: Commit**

```bash
git add tests/ Waypoint.sln
git commit -m "chore(scaffold): add Domain, Api, and Architecture test projects"
```

---

## Task 4: Postgres fixture with Testcontainers

**Files:**
- Create: `tests/Waypoint.Api.Tests/Fixtures/PostgresFixture.cs`

This fixture spins up a real Postgres container per test class. Used by every integration test in this plan.

- [ ] **Step 1: Write the failing test** — a smoke test that just checks the fixture starts a container and the connection string is reachable

Create `tests/Waypoint.Api.Tests/Fixtures/PostgresFixtureSmokeTests.cs`:
```csharp
using FluentAssertions;
using Npgsql;
using Xunit;

namespace Waypoint.Api.Tests.Fixtures;

public class PostgresFixtureSmokeTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public PostgresFixtureSmokeTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Fixture_provides_a_reachable_postgres()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT 1", conn);
        var result = await cmd.ExecuteScalarAsync();
        result.Should().Be(1);
    }
}
```

- [ ] **Step 2: Add Npgsql package to Api.Tests for the raw connection in the test**

Edit `tests/Waypoint.Api.Tests/Waypoint.Api.Tests.csproj` and add inside the `PackageReference` `ItemGroup`:
```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
```

- [ ] **Step 3: Run the test to verify it fails**

Run:
```bash
dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~PostgresFixtureSmokeTests
```
Expected: FAIL — `PostgresFixture` does not exist.

- [ ] **Step 4: Implement `PostgresFixture`**

Create `tests/Waypoint.Api.Tests/Fixtures/PostgresFixture.cs`:
```csharp
using Testcontainers.PostgreSql;
using Xunit;

namespace Waypoint.Api.Tests.Fixtures;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("waypoint_test")
        .WithUsername("waypoint")
        .WithPassword("waypoint")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run:
```bash
dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~PostgresFixtureSmokeTests
```
Expected: PASS. (First run will pull `postgres:16-alpine`, takes ~30s; subsequent runs are fast.)

- [ ] **Step 6: Commit**

```bash
git add tests/Waypoint.Api.Tests/
git commit -m "test(api): add PostgresFixture via Testcontainers"
```

---

## Task 5: WaypointDbContext skeleton + design-time factory

**Files:**
- Create: `src/Waypoint.Domain/WaypointDbContext.cs`
- Create: `src/Waypoint.Domain/DesignTimeDbContextFactory.cs`

The `DesignTimeDbContextFactory` lets `dotnet ef` CLI work without spinning up the whole API. Required for `dotnet ef migrations add`.

- [ ] **Step 1: Write the failing test** for an empty DbContext that can apply (no) migrations

Create `tests/Waypoint.Api.Tests/Fixtures/DbContextSmokeTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Fixtures;

public class DbContextSmokeTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;
    public DbContextSmokeTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DbContext_can_connect_to_postgres()
    {
        var options = new DbContextOptionsBuilder<WaypointDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        await using var ctx = new WaypointDbContext(options);
        var canConnect = await ctx.Database.CanConnectAsync();
        canConnect.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~DbContextSmokeTests
```
Expected: FAIL — `WaypointDbContext` does not exist.

- [ ] **Step 3: Create `WaypointDbContext`**

Create `src/Waypoint.Domain/WaypointDbContext.cs`:
```csharp
using Microsoft.EntityFrameworkCore;

namespace Waypoint.Domain;

public class WaypointDbContext(DbContextOptions<WaypointDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WaypointDbContext).Assembly);
    }
}
```

- [ ] **Step 4: Create `DesignTimeDbContextFactory`**

Create `src/Waypoint.Domain/DesignTimeDbContextFactory.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Waypoint.Domain;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<WaypointDbContext>
{
    public WaypointDbContext CreateDbContext(string[] args)
    {
        var connStr = Environment.GetEnvironmentVariable("WAYPOINT_DESIGN_TIME_CONNSTR")
                      ?? "Host=localhost;Database=waypoint_design;Username=waypoint;Password=waypoint";
        var options = new DbContextOptionsBuilder<WaypointDbContext>()
            .UseNpgsql(connStr)
            .Options;
        return new WaypointDbContext(options);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run:
```bash
dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~DbContextSmokeTests
```
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Waypoint.Domain/
git commit -m "feat(domain): add WaypointDbContext and design-time factory"
```

---

## Task 6: Shared enums (Priority, StateGroup, ActorType)

**Files:**
- Create: `src/Waypoint.Domain/Enums/Priority.cs`
- Create: `src/Waypoint.Domain/Enums/StateGroup.cs`
- Create: `src/Waypoint.Domain/Enums/ActorType.cs`

- [ ] **Step 1: Create `Priority`**

Create `src/Waypoint.Domain/Enums/Priority.cs`:
```csharp
namespace Waypoint.Domain.Enums;

public enum Priority
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4,
}
```

- [ ] **Step 2: Create `StateGroup`**

Create `src/Waypoint.Domain/Enums/StateGroup.cs`:
```csharp
namespace Waypoint.Domain.Enums;

public enum StateGroup
{
    Backlog = 0,
    Unstarted = 1,
    Started = 2,
    Completed = 3,
    Cancelled = 4,
}
```

- [ ] **Step 3: Create `ActorType`**

Create `src/Waypoint.Domain/Enums/ActorType.cs`:
```csharp
namespace Waypoint.Domain.Enums;

public enum ActorType
{
    User = 0,
    Service = 1,
    Passthrough = 2,
    System = 3,
}
```

- [ ] **Step 4: Build check**

Run:
```bash
dotnet build src/Waypoint.Domain
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/Waypoint.Domain/Enums/
git commit -m "feat(domain): add Priority, StateGroup, ActorType enums"
```

---

## Task 7: Project entity + migration

**Files:**
- Create: `src/Waypoint.Domain/Entities/Project.cs`
- Create: `src/Waypoint.Domain/Configurations/ProjectConfiguration.cs`
- Modify: `src/Waypoint.Domain/WaypointDbContext.cs` to add `DbSet<Project>`
- Generated: `src/Waypoint.Domain/Migrations/<timestamp>_AddProject.cs`

- [ ] **Step 1: Write the failing test** for projects table being created after migration

Create `tests/Waypoint.Api.Tests/Fixtures/MigrationsTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Fixtures;

public class MigrationsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;
    public MigrationsTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrations_apply_cleanly_and_create_projects_table()
    {
        var options = new DbContextOptionsBuilder<WaypointDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        await using (var ctx = new WaypointDbContext(options))
        {
            await ctx.Database.EnsureDeletedAsync();
            await ctx.Database.MigrateAsync();
        }
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.tables WHERE table_name = 'projects'", conn);
        var result = (long?)await cmd.ExecuteScalarAsync();
        result.Should().Be(1);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~MigrationsTests
```
Expected: FAIL — no migrations exist, `Migrate()` is a no-op, table not created.

- [ ] **Step 3: Create the `Project` entity**

Create `src/Waypoint.Domain/Entities/Project.cs`:
```csharp
namespace Waypoint.Domain.Entities;

public class Project
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string Identifier { get; set; }  // 3-letter prefix, e.g. "WAY"
    public Guid? DefaultStateId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

- [ ] **Step 4: Create the EF configuration**

Create `src/Waypoint.Domain/Configurations/ProjectConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.Slug).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Identifier).HasMaxLength(8).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.Identifier).IsUnique();
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasQueryFilter(p => p.DeletedAt == null);
    }
}
```

- [ ] **Step 5: Add `DbSet<Project>` to `WaypointDbContext`**

Replace `src/Waypoint.Domain/WaypointDbContext.cs` contents with:
```csharp
using Microsoft.EntityFrameworkCore;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain;

public class WaypointDbContext(DbContextOptions<WaypointDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("pgcrypto"); // for gen_random_uuid()
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WaypointDbContext).Assembly);
    }
}
```

- [ ] **Step 6: Install the dotnet-ef tool locally**

Run:
```bash
dotnet new tool-manifest --force
dotnet tool install dotnet-ef
```
Expected: `You can invoke the tool from this directory using ... 'dotnet ef'`.

- [ ] **Step 7: Generate the migration**

Run:
```bash
dotnet ef migrations add AddProject --project src/Waypoint.Domain --startup-project src/Waypoint.Domain
```
Expected: `Done.` — creates files under `src/Waypoint.Domain/Migrations/`.

- [ ] **Step 8: Run the test to verify it passes**

Run:
```bash
dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~MigrationsTests
```
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/Waypoint.Domain/ .config/
git commit -m "feat(domain): add Project entity, configuration, and initial migration"
```

---

## Task 8: State entity + workflow groups

**Files:**
- Create: `src/Waypoint.Domain/Entities/State.cs`
- Create: `src/Waypoint.Domain/Configurations/StateConfiguration.cs`
- Modify: `src/Waypoint.Domain/WaypointDbContext.cs`
- Generated migration: `AddState`

- [ ] **Step 1: Write the failing test** — states table exists with FK to projects

Add to `tests/Waypoint.Api.Tests/Fixtures/MigrationsTests.cs` (inside the same class):
```csharp
[Fact]
public async Task Migrations_create_states_table_with_project_fk()
{
    var options = new DbContextOptionsBuilder<WaypointDbContext>()
        .UseNpgsql(_fixture.ConnectionString).Options;
    await using (var ctx = new WaypointDbContext(options))
    {
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();
    }
    await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(@"
        SELECT count(*) FROM information_schema.table_constraints
        WHERE table_name = 'states' AND constraint_type = 'FOREIGN KEY'", conn);
    var fkCount = (long?)await cmd.ExecuteScalarAsync();
    fkCount.Should().BeGreaterThanOrEqualTo(1);
}
```

- [ ] **Step 2: Run to verify FAIL**

Run:
```bash
dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~Migrations_create_states_table
```
Expected: FAIL — states table doesn't exist.

- [ ] **Step 3: Create `State` entity**

Create `src/Waypoint.Domain/Entities/State.cs`:
```csharp
using Waypoint.Domain.Enums;

namespace Waypoint.Domain.Entities;

public class State
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public required string Name { get; set; }
    public StateGroup Group { get; set; }
    public required string Color { get; set; }  // hex like #RRGGBB
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

- [ ] **Step 4: Create configuration**

Create `src/Waypoint.Domain/Configurations/StateConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class StateConfiguration : IEntityTypeConfiguration<State>
{
    public void Configure(EntityTypeBuilder<State> builder)
    {
        builder.ToTable("states");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Color).HasMaxLength(7).IsRequired();
        builder.Property(s => s.Group).HasConversion<int>();
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(s => s.Project).WithMany().HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => new { s.ProjectId, s.Name }).IsUnique();
        builder.HasQueryFilter(s => s.DeletedAt == null);
    }
}
```

- [ ] **Step 5: Add `DbSet<State>` to `WaypointDbContext`**

Add to `WaypointDbContext` (after the `Projects` DbSet):
```csharp
public DbSet<State> States => Set<State>();
```

- [ ] **Step 6: Generate migration**

Run:
```bash
dotnet ef migrations add AddState --project src/Waypoint.Domain --startup-project src/Waypoint.Domain
```

- [ ] **Step 7: Verify tests pass**

Run:
```bash
dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~MigrationsTests
```
Expected: PASS (both `Migrations_apply_cleanly...` and the new test).

- [ ] **Step 8: Commit**

```bash
git add src/Waypoint.Domain/
git commit -m "feat(domain): add State entity with project FK"
```

---

## Task 9: IssueType entity

**Files:**
- Create: `src/Waypoint.Domain/Entities/IssueType.cs`
- Create: `src/Waypoint.Domain/Configurations/IssueTypeConfiguration.cs`
- Modify: `src/Waypoint.Domain/WaypointDbContext.cs`

- [ ] **Step 1: Write the failing test** for issue_types table

Add to `MigrationsTests.cs`:
```csharp
[Fact]
public async Task Migrations_create_issue_types_table()
{
    var options = new DbContextOptionsBuilder<WaypointDbContext>()
        .UseNpgsql(_fixture.ConnectionString).Options;
    await using (var ctx = new WaypointDbContext(options))
    {
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();
    }
    await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT count(*) FROM information_schema.tables WHERE table_name = 'issue_types'", conn);
    var result = (long?)await cmd.ExecuteScalarAsync();
    result.Should().Be(1);
}
```

- [ ] **Step 2: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~Migrations_create_issue_types`
Expected: FAIL.

- [ ] **Step 3: Create `IssueType`**

Create `src/Waypoint.Domain/Entities/IssueType.cs`:
```csharp
namespace Waypoint.Domain.Entities;

public class IssueType
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public required string Name { get; set; }      // Bug / Task / Story / Spike / custom
    public string? Description { get; set; }
    public Guid? DefaultWorkflowId { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

- [ ] **Step 4: Create configuration**

Create `src/Waypoint.Domain/Configurations/IssueTypeConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class IssueTypeConfiguration : IEntityTypeConfiguration<IssueType>
{
    public void Configure(EntityTypeBuilder<IssueType> builder)
    {
        builder.ToTable("issue_types");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(t => t.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(t => t.Project).WithMany().HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => new { t.ProjectId, t.Name }).IsUnique();
        builder.HasQueryFilter(t => t.DeletedAt == null);
    }
}
```

- [ ] **Step 5: Add DbSet**

In `WaypointDbContext`:
```csharp
public DbSet<IssueType> IssueTypes => Set<IssueType>();
```

- [ ] **Step 6: Migration**

Run: `dotnet ef migrations add AddIssueType --project src/Waypoint.Domain --startup-project src/Waypoint.Domain`

- [ ] **Step 7: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~MigrationsTests`

- [ ] **Step 8: Commit**

```bash
git add src/Waypoint.Domain/
git commit -m "feat(domain): add IssueType entity"
```

---

## Task 10: Workflow + WorkflowTransition entities

**Files:**
- Create: `src/Waypoint.Domain/Entities/Workflow.cs`
- Create: `src/Waypoint.Domain/Entities/WorkflowTransition.cs`
- Create: `src/Waypoint.Domain/Configurations/WorkflowConfiguration.cs`
- Create: `src/Waypoint.Domain/Configurations/WorkflowTransitionConfiguration.cs`
- Modify: `src/Waypoint.Domain/WaypointDbContext.cs`

- [ ] **Step 1: Failing test**

Add to `MigrationsTests.cs`:
```csharp
[Fact]
public async Task Migrations_create_workflows_and_transitions_with_state_fks()
{
    var options = new DbContextOptionsBuilder<WaypointDbContext>()
        .UseNpgsql(_fixture.ConnectionString).Options;
    await using (var ctx = new WaypointDbContext(options))
    {
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();
    }
    await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(@"
        SELECT count(*) FROM information_schema.tables
        WHERE table_name IN ('workflows','workflow_transitions')", conn);
    var result = (long?)await cmd.ExecuteScalarAsync();
    result.Should().Be(2);
}
```

- [ ] **Step 2: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~Migrations_create_workflows`

- [ ] **Step 3: Create `Workflow`**

Create `src/Waypoint.Domain/Entities/Workflow.cs`:
```csharp
namespace Waypoint.Domain.Entities;

public class Workflow
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

- [ ] **Step 4: Create `WorkflowTransition`**

Create `src/Waypoint.Domain/Entities/WorkflowTransition.cs`:
```csharp
namespace Waypoint.Domain.Entities;

public class WorkflowTransition
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = null!;
    public Guid FromStateId { get; set; }
    public State FromState { get; set; } = null!;
    public Guid ToStateId { get; set; }
    public State ToState { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
```

- [ ] **Step 5: Configurations**

Create `src/Waypoint.Domain/Configurations/WorkflowConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("workflows");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(w => w.Name).HasMaxLength(100).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(500);
        builder.Property(w => w.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(w => w.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(w => w.Project).WithMany().HasForeignKey(w => w.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(w => new { w.ProjectId, w.Name }).IsUnique();
        builder.HasQueryFilter(w => w.DeletedAt == null);
    }
}
```

Create `src/Waypoint.Domain/Configurations/WorkflowTransitionConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("workflow_transitions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
        builder.HasOne(t => t.Workflow).WithMany().HasForeignKey(t => t.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(t => t.FromState).WithMany().HasForeignKey(t => t.FromStateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.ToState).WithMany().HasForeignKey(t => t.ToStateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.WorkflowId, t.FromStateId, t.ToStateId }).IsUnique();
    }
}
```

- [ ] **Step 6: Add DbSets**

In `WaypointDbContext`:
```csharp
public DbSet<Workflow> Workflows => Set<Workflow>();
public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();
```

- [ ] **Step 7: Migration**

Run: `dotnet ef migrations add AddWorkflows --project src/Waypoint.Domain --startup-project src/Waypoint.Domain`

- [ ] **Step 8: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~MigrationsTests`

- [ ] **Step 9: Commit**

```bash
git add src/Waypoint.Domain/
git commit -m "feat(domain): add Workflow and WorkflowTransition entities"
```

---

## Task 11: User entity (placeholder, no auth fields yet)

**Files:**
- Create: `src/Waypoint.Domain/Entities/User.cs`
- Create: `src/Waypoint.Domain/Configurations/UserConfiguration.cs`
- Modify: `src/Waypoint.Domain/WaypointDbContext.cs`

This is a minimal user. OIDC fields (`oidc_sub`, `oidc_issuer`) and sessions come in Phase 4.

- [ ] **Step 1: Failing test**

Add to `MigrationsTests.cs`:
```csharp
[Fact]
public async Task Migrations_create_users_table()
{
    var options = new DbContextOptionsBuilder<WaypointDbContext>()
        .UseNpgsql(_fixture.ConnectionString).Options;
    await using (var ctx = new WaypointDbContext(options))
    {
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();
    }
    await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT count(*) FROM information_schema.tables WHERE table_name = 'users'", conn);
    var result = (long?)await cmd.ExecuteScalarAsync();
    result.Should().Be(1);
}
```

- [ ] **Step 2: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~Migrations_create_users`

- [ ] **Step 3: Create `User`**

Create `src/Waypoint.Domain/Entities/User.cs`:
```csharp
namespace Waypoint.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

- [ ] **Step 4: Configuration**

Create `src/Waypoint.Domain/Configurations/UserConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(u => u.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasQueryFilter(u => u.DeletedAt == null);
    }
}
```

- [ ] **Step 5: Add DbSet**

In `WaypointDbContext`:
```csharp
public DbSet<User> Users => Set<User>();
```

- [ ] **Step 6: Migration**

Run: `dotnet ef migrations add AddUser --project src/Waypoint.Domain --startup-project src/Waypoint.Domain`

- [ ] **Step 7: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~MigrationsTests`

- [ ] **Step 8: Commit**

```bash
git add src/Waypoint.Domain/
git commit -m "feat(domain): add User entity (placeholder, no OIDC fields yet)"
```

---

## Task 12: Issue entity (with per-project sequence)

**Files:**
- Create: `src/Waypoint.Domain/Entities/Issue.cs`
- Create: `src/Waypoint.Domain/Configurations/IssueConfiguration.cs`
- Modify: `src/Waypoint.Domain/WaypointDbContext.cs`

Issues need a per-project `sequence_id` (e.g. `WAY-123`). We use a Postgres sequence per project named `seq_issues_<project_id>` for simplicity; the migration creates the column but sequence creation happens in the IssueRepository when a project is created in Task 17.

- [ ] **Step 1: Failing test**

Add to `MigrationsTests.cs`:
```csharp
[Fact]
public async Task Migrations_create_issues_table_with_sequence_id_unique_per_project()
{
    var options = new DbContextOptionsBuilder<WaypointDbContext>()
        .UseNpgsql(_fixture.ConnectionString).Options;
    await using (var ctx = new WaypointDbContext(options))
    {
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();
    }
    await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(@"
        SELECT count(*) FROM pg_indexes
        WHERE tablename = 'issues' AND indexdef LIKE '%project_id%sequence_id%'", conn);
    var result = (long?)await cmd.ExecuteScalarAsync();
    result.Should().BeGreaterThanOrEqualTo(1);
}
```

- [ ] **Step 2: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~Migrations_create_issues_table`

- [ ] **Step 3: Create `Issue`**

Create `src/Waypoint.Domain/Entities/Issue.cs`:
```csharp
using Waypoint.Domain.Enums;

namespace Waypoint.Domain.Entities;

public class Issue
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int SequenceId { get; set; }                // per-project counter, allocated by repo
    public required string Title { get; set; }
    public string DescriptionMd { get; set; } = string.Empty;
    public Guid StateId { get; set; }
    public State State { get; set; } = null!;
    public Priority Priority { get; set; } = Priority.None;
    public Guid IssueTypeId { get; set; }
    public IssueType IssueType { get; set; } = null!;
    public Guid? ParentIssueId { get; set; }
    public Issue? ParentIssue { get; set; }
    public Guid[] AssigneeIds { get; set; } = Array.Empty<Guid>();
    public DateTimeOffset? DueDate { get; set; }
    public string? ExternalId { get; set; }            // for the Plane import (Phase 3)
    public string? ExternalSource { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

- [ ] **Step 4: Configuration**

Create `src/Waypoint.Domain/Configurations/IssueConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("issues");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(i => i.Title).HasMaxLength(500).IsRequired();
        builder.Property(i => i.DescriptionMd).IsRequired();
        builder.Property(i => i.Priority).HasConversion<int>();
        builder.Property(i => i.AssigneeIds).HasColumnType("uuid[]").HasDefaultValueSql("'{}'::uuid[]");
        builder.Property(i => i.ExternalId).HasMaxLength(200);
        builder.Property(i => i.ExternalSource).HasMaxLength(50);
        builder.Property(i => i.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(i => i.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(i => i.Project).WithMany().HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.State).WithMany().HasForeignKey(i => i.StateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.IssueType).WithMany().HasForeignKey(i => i.IssueTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.ParentIssue).WithMany().HasForeignKey(i => i.ParentIssueId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.ProjectId, i.SequenceId }).IsUnique();
        builder.HasIndex(i => new { i.ProjectId, i.StateId });
        builder.HasIndex(i => i.UpdatedAt);
        builder.HasQueryFilter(i => i.DeletedAt == null);
    }
}
```

- [ ] **Step 5: Add DbSet**

In `WaypointDbContext`:
```csharp
public DbSet<Issue> Issues => Set<Issue>();
```

- [ ] **Step 6: Migration**

Run: `dotnet ef migrations add AddIssue --project src/Waypoint.Domain --startup-project src/Waypoint.Domain`

- [ ] **Step 7: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~MigrationsTests`

- [ ] **Step 8: Commit**

```bash
git add src/Waypoint.Domain/
git commit -m "feat(domain): add Issue entity with per-project sequence and FKs"
```

---

## Task 13: Comment entity

**Files:**
- Create: `src/Waypoint.Domain/Entities/Comment.cs`
- Create: `src/Waypoint.Domain/Configurations/CommentConfiguration.cs`
- Modify: `src/Waypoint.Domain/WaypointDbContext.cs`

- [ ] **Step 1: Failing test**

Add to `MigrationsTests.cs`:
```csharp
[Fact]
public async Task Migrations_create_comments_table_with_issue_and_author_fks()
{
    var options = new DbContextOptionsBuilder<WaypointDbContext>()
        .UseNpgsql(_fixture.ConnectionString).Options;
    await using (var ctx = new WaypointDbContext(options))
    {
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();
    }
    await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(@"
        SELECT count(*) FROM information_schema.tables WHERE table_name = 'comments'", conn);
    var result = (long?)await cmd.ExecuteScalarAsync();
    result.Should().Be(1);
}
```

- [ ] **Step 2: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~Migrations_create_comments`

- [ ] **Step 3: Create `Comment`**

Create `src/Waypoint.Domain/Entities/Comment.cs`:
```csharp
namespace Waypoint.Domain.Entities;

public class Comment
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public required string BodyMd { get; set; }
    public Guid? AuthorUserId { get; set; }
    public User? AuthorUser { get; set; }
    public Guid? ParentCommentId { get; set; }
    public Comment? ParentComment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

- [ ] **Step 4: Configuration**

Create `src/Waypoint.Domain/Configurations/CommentConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.BodyMd).IsRequired();
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne(c => c.Issue).WithMany().HasForeignKey(c => c.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.AuthorUser).WithMany().HasForeignKey(c => c.AuthorUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(c => c.ParentComment).WithMany().HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.IssueId, c.CreatedAt });
        builder.HasQueryFilter(c => c.DeletedAt == null);
    }
}
```

- [ ] **Step 5: Add DbSet**

In `WaypointDbContext`:
```csharp
public DbSet<Comment> Comments => Set<Comment>();
```

- [ ] **Step 6: Migration + PASS + commit**

Run:
```bash
dotnet ef migrations add AddComment --project src/Waypoint.Domain --startup-project src/Waypoint.Domain
dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~MigrationsTests
git add src/Waypoint.Domain/
git commit -m "feat(domain): add Comment entity"
```

---

## Task 14: Activity entity (issue timeline)

**Files:**
- Create: `src/Waypoint.Domain/Entities/Activity.cs`
- Create: `src/Waypoint.Domain/Configurations/ActivityConfiguration.cs`
- Modify: `src/Waypoint.Domain/WaypointDbContext.cs`

- [ ] **Step 1: Failing test**

Add to `MigrationsTests.cs`:
```csharp
[Fact]
public async Task Migrations_create_activity_table()
{
    var options = new DbContextOptionsBuilder<WaypointDbContext>()
        .UseNpgsql(_fixture.ConnectionString).Options;
    await using (var ctx = new WaypointDbContext(options))
    {
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();
    }
    await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT count(*) FROM information_schema.tables WHERE table_name = 'activity'", conn);
    var result = (long?)await cmd.ExecuteScalarAsync();
    result.Should().Be(1);
}
```

- [ ] **Step 2: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~Migrations_create_activity`

- [ ] **Step 3: Create `Activity`**

Create `src/Waypoint.Domain/Entities/Activity.cs`:
```csharp
using Waypoint.Domain.Enums;

namespace Waypoint.Domain.Entities;

public class Activity
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public ActorType ActorType { get; set; }
    public Guid? ActorId { get; set; }              // user_id for ActorType=User; null for System
    public string? ActorLabel { get; set; }         // e.g. "agent:meridian-trainer" for Passthrough
    public required string Verb { get; set; }       // "created", "transitioned", "commented", ...
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTimeOffset At { get; set; }
}
```

- [ ] **Step 4: Configuration**

Create `src/Waypoint.Domain/Configurations/ActivityConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waypoint.Domain.Entities;

namespace Waypoint.Domain.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("activity");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.ActorType).HasConversion<int>();
        builder.Property(a => a.ActorLabel).HasMaxLength(200);
        builder.Property(a => a.Verb).HasMaxLength(100).IsRequired();
        builder.Property(a => a.BeforeJson).HasColumnType("jsonb");
        builder.Property(a => a.AfterJson).HasColumnType("jsonb");
        builder.Property(a => a.At).HasDefaultValueSql("now()");
        builder.HasOne(a => a.Issue).WithMany().HasForeignKey(a => a.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.IssueId, a.At });
    }
}
```

- [ ] **Step 5: Add DbSet**

In `WaypointDbContext`:
```csharp
public DbSet<Activity> Activities => Set<Activity>();
```

- [ ] **Step 6: Migration + PASS + commit**

```bash
dotnet ef migrations add AddActivity --project src/Waypoint.Domain --startup-project src/Waypoint.Domain
dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~MigrationsTests
git add src/Waypoint.Domain/
git commit -m "feat(domain): add Activity entity (issue timeline)"
```

---

## Task 15: WorkflowTransitionValidator domain service

**Files:**
- Create: `src/Waypoint.Domain/Validation/WorkflowTransitionValidator.cs`
- Create: `tests/Waypoint.Domain.Tests/Validation/WorkflowTransitionValidatorTests.cs`

Validates that a proposed state transition is allowed by the issue's workflow. Pure logic, unit-tested.

- [ ] **Step 1: Write the failing test**

Create `tests/Waypoint.Domain.Tests/Validation/WorkflowTransitionValidatorTests.cs`:
```csharp
using FluentAssertions;
using Waypoint.Domain.Validation;
using Xunit;

namespace Waypoint.Domain.Tests.Validation;

public class WorkflowTransitionValidatorTests
{
    private static readonly Guid Backlog = Guid.NewGuid();
    private static readonly Guid InProgress = Guid.NewGuid();
    private static readonly Guid Done = Guid.NewGuid();

    private static readonly (Guid From, Guid To)[] Allowed =
    [
        (Backlog, InProgress),
        (InProgress, Done),
        (Done, InProgress),
    ];

    [Fact]
    public void Allowed_transition_returns_valid()
    {
        var sut = new WorkflowTransitionValidator(Allowed);
        sut.CanTransition(Backlog, InProgress).Should().BeTrue();
    }

    [Fact]
    public void Disallowed_transition_returns_invalid()
    {
        var sut = new WorkflowTransitionValidator(Allowed);
        sut.CanTransition(Done, Backlog).Should().BeFalse();
    }

    [Fact]
    public void Same_state_is_always_allowed_noop()
    {
        var sut = new WorkflowTransitionValidator(Allowed);
        sut.CanTransition(Backlog, Backlog).Should().BeTrue();
    }

    [Fact]
    public void Empty_workflow_rejects_everything_except_noops()
    {
        var sut = new WorkflowTransitionValidator([]);
        sut.CanTransition(Backlog, InProgress).Should().BeFalse();
        sut.CanTransition(Backlog, Backlog).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run to verify FAIL**

Run:
```bash
dotnet test tests/Waypoint.Domain.Tests
```
Expected: FAIL — `WorkflowTransitionValidator` does not exist.

- [ ] **Step 3: Implement**

Create `src/Waypoint.Domain/Validation/WorkflowTransitionValidator.cs`:
```csharp
namespace Waypoint.Domain.Validation;

public sealed class WorkflowTransitionValidator
{
    private readonly HashSet<(Guid From, Guid To)> _allowed;

    public WorkflowTransitionValidator(IEnumerable<(Guid From, Guid To)> allowedTransitions)
    {
        _allowed = new HashSet<(Guid, Guid)>(allowedTransitions);
    }

    public bool CanTransition(Guid fromStateId, Guid toStateId)
    {
        if (fromStateId == toStateId) return true;
        return _allowed.Contains((fromStateId, toStateId));
    }
}
```

- [ ] **Step 4: Run to verify PASS**

Run: `dotnet test tests/Waypoint.Domain.Tests`
Expected: PASS (all 4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Waypoint.Domain/Validation/ tests/Waypoint.Domain.Tests/
git commit -m "feat(domain): add WorkflowTransitionValidator with unit tests"
```

---

## Task 16: Cross-cutting middleware — RequestIdMiddleware

**Files:**
- Create: `src/Waypoint.Api/Middleware/RequestIdMiddleware.cs`
- Test goes inline via integration tests in Task 24.

Generates an `X-Request-Id` if missing and echoes it on the response. Sets it in `HttpContext.Items["RequestId"]` for downstream use.

- [ ] **Step 1: Write the failing test (integration via WebApplicationFactory)**

Since this needs the API host, we'll build the `WaypointApiFactory` here. Create `tests/Waypoint.Api.Tests/Fixtures/WaypointApiFactory.cs`:
```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Domain;

namespace Waypoint.Api.Tests.Fixtures;

public class WaypointApiFactory : WebApplicationFactory<Program>
{
    public required string PostgresConnectionString { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Remove any pre-registered DbContext
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<WaypointDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<WaypointDbContext>(opts => opts.UseNpgsql(PostgresConnectionString));
        });
    }

    public async Task EnsureMigratedAsync()
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        await ctx.Database.MigrateAsync();
    }
}
```

Create the test `tests/Waypoint.Api.Tests/Middleware/RequestIdMiddlewareTests.cs`:
```csharp
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Xunit;

namespace Waypoint.Api.Tests.Middleware;

public class RequestIdMiddlewareTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public RequestIdMiddlewareTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Response_includes_X_Request_Id_when_request_omits_it()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/healthz/live");
        response.Headers.Should().ContainKey("X-Request-Id");
        Guid.TryParse(response.Headers.GetValues("X-Request-Id").First(), out _).Should().BeTrue();
    }

    [Fact]
    public async Task Response_echoes_X_Request_Id_when_request_provides_one()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var requestId = "test-req-abc-123";
        client.DefaultRequestHeaders.Add("X-Request-Id", requestId);
        var response = await client.GetAsync("/healthz/live");
        response.Headers.GetValues("X-Request-Id").First().Should().Be(requestId);
    }
}
```

- [ ] **Step 2: Run to verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~RequestIdMiddlewareTests`
Expected: FAIL — no X-Request-Id header in response.

- [ ] **Step 3: Implement middleware**

Create `src/Waypoint.Api/Middleware/RequestIdMiddleware.cs`:
```csharp
namespace Waypoint.Api.Middleware;

public sealed class RequestIdMiddleware
{
    public const string HeaderName = "X-Request-Id";
    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var requestId = ctx.Request.Headers.TryGetValue(HeaderName, out var incoming) && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString();

        ctx.Items["RequestId"] = requestId;
        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers[HeaderName] = requestId;
            return Task.CompletedTask;
        });

        await _next(ctx);
    }
}
```

- [ ] **Step 4: Register middleware in Program.cs**

Edit `src/Waypoint.Api/Program.cs`. Replace contents with:
```csharp
using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Middleware;
using Waypoint.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddDbContext<WaypointDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
                   ?? "Host=localhost;Database=waypoint;Username=waypoint;Password=waypoint"));

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseMiddleware<RequestIdMiddleware>();

app.MapGet("/healthz/live", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
```

- [ ] **Step 5: Run to verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~RequestIdMiddlewareTests`
Expected: PASS (both tests).

- [ ] **Step 6: Commit**

```bash
git add src/Waypoint.Api/ tests/Waypoint.Api.Tests/
git commit -m "feat(api): add RequestIdMiddleware with integration tests"
```

---

## Task 17: ErrorEnvelopeMiddleware

**Files:**
- Create: `src/Waypoint.Api/Middleware/ErrorEnvelopeMiddleware.cs`
- Create: `src/Waypoint.Contracts/ErrorResponse.cs`

Catches unhandled exceptions and returns the standard error envelope. Also handles thrown `WaypointException` types with HTTP status mapping.

- [ ] **Step 1: Add `ErrorResponse` DTO to Contracts**

Create `src/Waypoint.Contracts/ErrorResponse.cs`:
```csharp
namespace Waypoint.Contracts;

public sealed record ErrorResponse(ErrorBody Error, string RequestId);

public sealed record ErrorBody(string Code, string Message, IReadOnlyDictionary<string, object>? Details = null);
```

- [ ] **Step 2: Add a domain exception type**

Create `src/Waypoint.Domain/WaypointException.cs`:
```csharp
namespace Waypoint.Domain;

public class WaypointException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }
    public IReadOnlyDictionary<string, object>? Details { get; }

    public WaypointException(string code, string message, int statusCode, IReadOnlyDictionary<string, object>? details = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details;
    }
}

public sealed class NotFoundException(string code, string message) : WaypointException(code, message, 404);
public sealed class ConflictException(string code, string message, IReadOnlyDictionary<string, object>? details = null) : WaypointException(code, message, 409, details);
public sealed class ValidationException(string code, string message, IReadOnlyDictionary<string, object>? details = null) : WaypointException(code, message, 422, details);
```

- [ ] **Step 3: Write the failing test**

Create `tests/Waypoint.Api.Tests/Middleware/ErrorEnvelopeMiddlewareTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Waypoint.Api.Middleware;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Waypoint.Domain;
using Xunit;

namespace Waypoint.Api.Tests.Middleware;

public class ErrorEnvelopeMiddlewareTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ErrorEnvelopeMiddlewareTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task NotFoundException_maps_to_404_with_envelope()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();

        // We'll add a test-only endpoint via WebApplicationFactory's IStartupFilter pattern, or
        // hit a real endpoint that we know throws. For now we exercise it via project GET (Task 19).
        // This test will be fleshed out once project endpoints exist; for the middleware itself we
        // use a probe endpoint added under #if DEBUG in Program.cs.
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/__test_throws/not_found");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        body!.Error.Code.Should().Be("test_not_found");
    }
}
```

- [ ] **Step 4: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~ErrorEnvelopeMiddlewareTests`
Expected: FAIL — endpoint and middleware don't exist.

- [ ] **Step 5: Implement middleware**

Create `src/Waypoint.Api/Middleware/ErrorEnvelopeMiddleware.cs`:
```csharp
using System.Text.Json;
using Waypoint.Contracts;
using Waypoint.Domain;

namespace Waypoint.Api.Middleware;

public sealed class ErrorEnvelopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorEnvelopeMiddleware> _logger;

    public ErrorEnvelopeMiddleware(RequestDelegate next, ILogger<ErrorEnvelopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (WaypointException ex)
        {
            await WriteEnvelope(ctx, ex.StatusCode, ex.Code, ex.Message, ex.Details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteEnvelope(ctx, 500, "internal_error", "An unexpected error occurred.", null);
        }
    }

    private static async Task WriteEnvelope(HttpContext ctx, int status, string code, string message, IReadOnlyDictionary<string, object>? details)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        var requestId = ctx.Items["RequestId"]?.ToString() ?? Guid.NewGuid().ToString();
        var envelope = new ErrorResponse(new ErrorBody(code, message, details), requestId);
        await JsonSerializer.SerializeAsync(ctx.Response.Body, envelope);
    }
}
```

- [ ] **Step 6: Register middleware and add the probe endpoint to Program.cs**

Edit `src/Waypoint.Api/Program.cs`. Replace contents with:
```csharp
using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Middleware;
using Waypoint.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddDbContext<WaypointDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
                   ?? "Host=localhost;Database=waypoint;Username=waypoint;Password=waypoint"));

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ErrorEnvelopeMiddleware>();

app.MapGet("/healthz/live", () => Results.Ok(new { status = "ok" }));

if (app.Environment.EnvironmentName == "Testing")
{
    app.MapGet("/__test_throws/not_found",
        () => throw new NotFoundException("test_not_found", "probe"));
}

app.Run();

public partial class Program;
```

- [ ] **Step 7: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~ErrorEnvelopeMiddlewareTests`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/ tests/Waypoint.Api.Tests/
git commit -m "feat(api): add ErrorEnvelopeMiddleware and WaypointException hierarchy"
```

---

## Task 18: Cursor pagination helpers

**Files:**
- Create: `src/Waypoint.Api/Pagination/Cursor.cs`
- Create: `src/Waypoint.Contracts/PagedResponse.cs`
- Create: `tests/Waypoint.Api.Tests/Pagination/CursorTests.cs`

- [ ] **Step 1: Add `PagedResponse` to Contracts**

Create `src/Waypoint.Contracts/PagedResponse.cs`:
```csharp
namespace Waypoint.Contracts;

public sealed record PagedResponse<T>(IReadOnlyList<T> Data, string? NextCursor, long TotalCount);
```

- [ ] **Step 2: Write the failing test**

Create `tests/Waypoint.Api.Tests/Pagination/CursorTests.cs`:
```csharp
using FluentAssertions;
using Waypoint.Api.Pagination;
using Xunit;

namespace Waypoint.Api.Tests.Pagination;

public class CursorTests
{
    [Fact]
    public void Encode_then_Decode_round_trips_a_DateTimeOffset_and_Guid()
    {
        var ts = new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero);
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var encoded = Cursor.Encode(ts, id);
        var (decTs, decId) = Cursor.Decode(encoded);
        decTs.Should().Be(ts);
        decId.Should().Be(id);
    }

    [Fact]
    public void Decode_throws_on_malformed_input()
    {
        Action act = () => Cursor.Decode("not-a-cursor");
        act.Should().Throw<FormatException>();
    }
}
```

- [ ] **Step 3: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~CursorTests`

- [ ] **Step 4: Implement**

Create `src/Waypoint.Api/Pagination/Cursor.cs`:
```csharp
using System.Text;

namespace Waypoint.Api.Pagination;

public static class Cursor
{
    public static string Encode(DateTimeOffset sortValue, Guid id)
    {
        var raw = $"{sortValue.ToUnixTimeMilliseconds()}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static (DateTimeOffset SortValue, Guid Id) Decode(string cursor)
    {
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            if (parts.Length != 2) throw new FormatException("Cursor must have two segments.");
            var ts = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(parts[0]));
            var id = Guid.Parse(parts[1]);
            return (ts, id);
        }
        catch (Exception ex) when (ex is not FormatException)
        {
            throw new FormatException("Malformed cursor", ex);
        }
    }
}
```

- [ ] **Step 5: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~CursorTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/ tests/Waypoint.Api.Tests/Pagination/
git commit -m "feat(api): add cursor pagination helpers and PagedResponse DTO"
```

---

## Task 19: Project DTOs + ProjectRepository + create/get/list endpoints

**Files:**
- Create: `src/Waypoint.Contracts/ProjectDto.cs`
- Create: `src/Waypoint.Contracts/CreateProjectRequest.cs`
- Create: `src/Waypoint.Api/Repositories/IProjectRepository.cs`
- Create: `src/Waypoint.Api/Repositories/ProjectRepository.cs`
- Create: `src/Waypoint.Api/Endpoints/ProjectEndpoints.cs`
- Modify: `src/Waypoint.Api/Program.cs` to register repos and map endpoints
- Create: `tests/Waypoint.Api.Tests/Endpoints/ProjectEndpointsTests.cs`

Project creation also seeds a default workflow (one default state, all transitions allowed within it) and a default `Task` issue type. Without those, issues can't be created.

- [ ] **Step 1: DTOs in Contracts**

Create `src/Waypoint.Contracts/ProjectDto.cs`:
```csharp
namespace Waypoint.Contracts;

public sealed record ProjectDto(
    Guid Id,
    string Slug,
    string Name,
    string Identifier,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```

Create `src/Waypoint.Contracts/CreateProjectRequest.cs`:
```csharp
namespace Waypoint.Contracts;

public sealed record CreateProjectRequest(string Slug, string Name, string Identifier);
```

- [ ] **Step 2: Write the failing test**

Create `tests/Waypoint.Api.Tests/Endpoints/ProjectEndpointsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class ProjectEndpointsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public ProjectEndpointsTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task POST_then_GET_round_trips_a_project()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest(Slug: "test-proj-1", Name: "Test Project", Identifier: "TP1"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<ProjectDto>();
        created!.Slug.Should().Be("test-proj-1");
        created.Identifier.Should().Be("TP1");

        var get = await client.GetAsync($"/api/v1/projects/{created.Slug}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await get.Content.ReadFromJsonAsync<ProjectDto>();
        fetched!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GET_returns_404_envelope_for_missing_slug()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        var get = await client.GetAsync("/api/v1/projects/does-not-exist");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await get.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("project_not_found");
    }

    [Fact]
    public async Task POST_returns_409_on_duplicate_slug()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        var req = new CreateProjectRequest("dup-slug", "First", "DUP");
        var first = await client.PostAsJsonAsync("/api/v1/projects", req);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/v1/projects", req);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var err = await second.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("project_slug_exists");
    }
}
```

- [ ] **Step 3: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~ProjectEndpointsTests`

- [ ] **Step 4: Define the repository interface**

Create `src/Waypoint.Api/Repositories/IProjectRepository.cs`:
```csharp
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Repositories;

public interface IProjectRepository
{
    Task<Project> CreateAsync(string slug, string name, string identifier, CancellationToken ct);
    Task<Project?> GetBySlugAsync(string slug, CancellationToken ct);
}
```

- [ ] **Step 5: Implement the repository**

Create `src/Waypoint.Api/Repositories/ProjectRepository.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly WaypointDbContext _db;
    public ProjectRepository(WaypointDbContext db) => _db = db;

    public async Task<Project> CreateAsync(string slug, string name, string identifier, CancellationToken ct)
    {
        try
        {
            var project = new Project { Slug = slug, Name = name, Identifier = identifier };
            _db.Projects.Add(project);
            await _db.SaveChangesAsync(ct);

            // Seed default state
            var defaultState = new State
            {
                ProjectId = project.Id, Name = "Backlog", Group = StateGroup.Backlog,
                Color = "#94a3b8", SortOrder = 0, IsDefault = true
            };
            _db.States.Add(defaultState);
            await _db.SaveChangesAsync(ct);

            project.DefaultStateId = defaultState.Id;
            await _db.SaveChangesAsync(ct);

            // Seed default workflow (permissive self-loop)
            var workflow = new Workflow { ProjectId = project.Id, Name = "Default" };
            _db.Workflows.Add(workflow);
            await _db.SaveChangesAsync(ct);

            // Seed default issue type
            var issueType = new IssueType { ProjectId = project.Id, Name = "Task", IsDefault = true, DefaultWorkflowId = workflow.Id };
            _db.IssueTypes.Add(issueType);
            await _db.SaveChangesAsync(ct);

            return project;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new ConflictException("project_slug_exists", $"A project with slug '{slug}' or identifier '{identifier}' already exists.");
        }
    }

    public Task<Project?> GetBySlugAsync(string slug, CancellationToken ct) =>
        _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug, ct);
}
```

(`ConflictException` is from `Waypoint.Domain` — add `using Waypoint.Domain;` at top of file.)

- [ ] **Step 6: Add endpoints**

Create `src/Waypoint.Api/Endpoints/ProjectEndpoints.cs`:
```csharp
using Waypoint.Api.Repositories;
using Waypoint.Contracts;
using Waypoint.Domain;

namespace Waypoint.Api.Endpoints;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/projects");

        group.MapPost("/", async (CreateProjectRequest req, IProjectRepository repo, CancellationToken ct) =>
        {
            var p = await repo.CreateAsync(req.Slug, req.Name, req.Identifier, ct);
            var dto = new ProjectDto(p.Id, p.Slug, p.Name, p.Identifier, p.CreatedAt, p.UpdatedAt);
            return Results.Created($"/api/v1/projects/{p.Slug}", dto);
        });

        group.MapGet("/{slug}", async (string slug, IProjectRepository repo, CancellationToken ct) =>
        {
            var p = await repo.GetBySlugAsync(slug, ct);
            if (p is null) throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            return Results.Ok(new ProjectDto(p.Id, p.Slug, p.Name, p.Identifier, p.CreatedAt, p.UpdatedAt));
        });
    }
}
```

- [ ] **Step 7: Wire into Program.cs**

Edit `src/Waypoint.Api/Program.cs`. Replace contents with:
```csharp
using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Endpoints;
using Waypoint.Api.Middleware;
using Waypoint.Api.Repositories;
using Waypoint.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddDbContext<WaypointDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
                   ?? "Host=localhost;Database=waypoint;Username=waypoint;Password=waypoint"));
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ErrorEnvelopeMiddleware>();

app.MapGet("/healthz/live", () => Results.Ok(new { status = "ok" }));
app.MapProjectEndpoints();

if (app.Environment.EnvironmentName == "Testing")
{
    app.MapGet("/__test_throws/not_found",
        () => throw new NotFoundException("test_not_found", "probe"));
}

app.Run();

public partial class Program;
```

- [ ] **Step 8: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~ProjectEndpointsTests`
Expected: PASS (all 3 tests).

- [ ] **Step 9: Commit**

```bash
git add src/ tests/Waypoint.Api.Tests/Endpoints/
git commit -m "feat(api): add project create/get endpoints with default state and workflow seeding"
```

---

## Task 20: IssueRepository + Postgres sequence allocation

**Files:**
- Create: `src/Waypoint.Api/Repositories/IIssueRepository.cs`
- Create: `src/Waypoint.Api/Repositories/IssueRepository.cs`

Each project gets its own Postgres sequence `seq_issues_<project_id_hex>` for issue numbering. Sequence is created lazily on first issue creation per project (advisory-lock guarded so two concurrent creates don't race).

- [ ] **Step 1: Define the interface**

Create `src/Waypoint.Api/Repositories/IIssueRepository.cs`:
```csharp
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Repositories;

public interface IIssueRepository
{
    Task<Issue> CreateAsync(Guid projectId, string title, string descriptionMd, Guid? issueTypeId, CancellationToken ct);
    Task<Issue?> GetBySequenceAsync(Guid projectId, int seq, CancellationToken ct);
    Task<int> NextSequenceAsync(Guid projectId, CancellationToken ct);
}
```

- [ ] **Step 2: Implement**

Create `src/Waypoint.Api/Repositories/IssueRepository.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Repositories;

public sealed class IssueRepository : IIssueRepository
{
    private readonly WaypointDbContext _db;
    public IssueRepository(WaypointDbContext db) => _db = db;

    public async Task<int> NextSequenceAsync(Guid projectId, CancellationToken ct)
    {
        var seqName = $"seq_issues_{projectId:N}";
        var ensure = $@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_class WHERE relname = '{seqName}' AND relkind = 'S') THEN
                    CREATE SEQUENCE {seqName} START 1;
                END IF;
            END $$;";
        await _db.Database.ExecuteSqlRawAsync(ensure, ct);

        var sql = $"SELECT nextval('{seqName}')";
        await using var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32((long)result!);
    }

    public async Task<Issue> CreateAsync(Guid projectId, string title, string descriptionMd, Guid? issueTypeId, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync([projectId], ct)
            ?? throw new NotFoundException("project_not_found", "Project not found.");
        if (project.DefaultStateId is null)
            throw new ConflictException("project_has_no_default_state", "Project has no default state.");

        var typeId = issueTypeId ?? await _db.IssueTypes.Where(t => t.ProjectId == projectId && t.IsDefault).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct)
            ?? throw new ConflictException("project_has_no_default_issue_type", "Project has no default issue type.");

        var seq = await NextSequenceAsync(projectId, ct);
        var issue = new Issue
        {
            ProjectId = projectId,
            SequenceId = seq,
            Title = title,
            DescriptionMd = descriptionMd,
            StateId = project.DefaultStateId.Value,
            IssueTypeId = typeId,
        };
        _db.Issues.Add(issue);
        await _db.SaveChangesAsync(ct);

        _db.Activities.Add(new Activity
        {
            IssueId = issue.Id,
            ActorType = Waypoint.Domain.Enums.ActorType.System,
            Verb = "created",
        });
        await _db.SaveChangesAsync(ct);

        return issue;
    }

    public Task<Issue?> GetBySequenceAsync(Guid projectId, int seq, CancellationToken ct) =>
        _db.Issues.AsNoTracking()
            .Include(i => i.State)
            .Include(i => i.IssueType)
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.SequenceId == seq, ct);
}
```

- [ ] **Step 3: Register in Program.cs**

Add after the `ProjectRepository` registration in `Program.cs`:
```csharp
builder.Services.AddScoped<IIssueRepository, IssueRepository>();
```

- [ ] **Step 4: Verify build**

Run: `dotnet build`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Waypoint.Api/
git commit -m "feat(api): add IssueRepository with per-project sequence allocation"
```

---

## Task 21: Issue DTOs + create/get endpoints

**Files:**
- Create: `src/Waypoint.Contracts/IssueDto.cs`
- Create: `src/Waypoint.Contracts/CreateIssueRequest.cs`
- Create: `src/Waypoint.Api/Endpoints/IssueEndpoints.cs`
- Create: `tests/Waypoint.Api.Tests/Endpoints/IssueEndpointsTests.cs`
- Modify: `src/Waypoint.Api/Program.cs` to map issue endpoints

- [ ] **Step 1: DTOs**

Create `src/Waypoint.Contracts/IssueDto.cs`:
```csharp
namespace Waypoint.Contracts;

public sealed record IssueDto(
    Guid Id,
    int Sequence,
    string Title,
    string DescriptionMd,
    Guid StateId,
    string StateName,
    Guid IssueTypeId,
    string IssueTypeName,
    int Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```

Create `src/Waypoint.Contracts/CreateIssueRequest.cs`:
```csharp
namespace Waypoint.Contracts;

public sealed record CreateIssueRequest(string Title, string DescriptionMd, Guid? IssueTypeId = null);
```

- [ ] **Step 2: Failing test**

Create `tests/Waypoint.Api.Tests/Endpoints/IssueEndpointsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class IssueEndpointsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public IssueEndpointsTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task POST_creates_issue_with_seq_1_then_2()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("issues-proj", "Issues Project", "ISP"));

        var first = await client.PostAsJsonAsync("/api/v1/projects/issues-proj/issues",
            new CreateIssueRequest("First", "Body **md**"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstDto = await first.Content.ReadFromJsonAsync<IssueDto>();
        firstDto!.Sequence.Should().Be(1);

        var second = await client.PostAsJsonAsync("/api/v1/projects/issues-proj/issues",
            new CreateIssueRequest("Second", "Body 2"));
        var secondDto = await second.Content.ReadFromJsonAsync<IssueDto>();
        secondDto!.Sequence.Should().Be(2);
    }

    [Fact]
    public async Task GET_by_sequence_returns_issue()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("get-proj", "Get Project", "GP1"));
        await client.PostAsJsonAsync("/api/v1/projects/get-proj/issues",
            new CreateIssueRequest("Hello", "World"));

        var get = await client.GetAsync("/api/v1/projects/get-proj/issues/1");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await get.Content.ReadFromJsonAsync<IssueDto>();
        dto!.Title.Should().Be("Hello");
        dto.DescriptionMd.Should().Be("World");
        dto.StateName.Should().Be("Backlog");
    }

    [Fact]
    public async Task GET_returns_404_for_missing_sequence()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("nf-proj", "NF Project", "NF1"));
        var get = await client.GetAsync("/api/v1/projects/nf-proj/issues/9999");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var err = await get.Content.ReadFromJsonAsync<ErrorResponse>();
        err!.Error.Code.Should().Be("issue_not_found");
    }
}
```

- [ ] **Step 3: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~IssueEndpointsTests`

- [ ] **Step 4: Add `IssueEndpoints`**

Create `src/Waypoint.Api/Endpoints/IssueEndpoints.cs`:
```csharp
using Waypoint.Api.Repositories;
using Waypoint.Contracts;
using Waypoint.Domain;

namespace Waypoint.Api.Endpoints;

public static class IssueEndpoints
{
    public static void MapIssueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/projects/{slug}/issues");

        group.MapPost("/", async (string slug, CreateIssueRequest req,
            IProjectRepository projects, IIssueRepository issues, CancellationToken ct) =>
        {
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var issue = await issues.CreateAsync(project.Id, req.Title, req.DescriptionMd, req.IssueTypeId, ct);
            var dto = await issues.GetBySequenceAsync(project.Id, issue.SequenceId, ct);
            return Results.Created($"/api/v1/projects/{slug}/issues/{dto!.SequenceId}", ToDto(dto));
        });

        group.MapGet("/{seq:int}", async (string slug, int seq,
            IProjectRepository projects, IIssueRepository issues, CancellationToken ct) =>
        {
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var issue = await issues.GetBySequenceAsync(project.Id, seq, ct)
                ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
            return Results.Ok(ToDto(issue));
        });
    }

    private static IssueDto ToDto(Waypoint.Domain.Entities.Issue i) => new(
        i.Id, i.SequenceId, i.Title, i.DescriptionMd,
        i.StateId, i.State.Name,
        i.IssueTypeId, i.IssueType.Name,
        (int)i.Priority,
        i.CreatedAt, i.UpdatedAt);
}
```

- [ ] **Step 5: Wire in Program.cs**

Add to `Program.cs` after `app.MapProjectEndpoints();`:
```csharp
app.MapIssueEndpoints();
```

- [ ] **Step 6: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~IssueEndpointsTests`
Expected: PASS (all 3 tests).

- [ ] **Step 7: Commit**

```bash
git add src/ tests/Waypoint.Api.Tests/Endpoints/IssueEndpointsTests.cs
git commit -m "feat(api): add issue create and get endpoints"
```

---

## Task 22: Issue transition endpoint with workflow validation

**Files:**
- Create: `src/Waypoint.Contracts/TransitionIssueRequest.cs`
- Modify: `src/Waypoint.Api/Repositories/IIssueRepository.cs`
- Modify: `src/Waypoint.Api/Repositories/IssueRepository.cs`
- Modify: `src/Waypoint.Api/Endpoints/IssueEndpoints.cs`
- Modify: `tests/Waypoint.Api.Tests/Endpoints/IssueEndpointsTests.cs`

- [ ] **Step 1: DTO**

Create `src/Waypoint.Contracts/TransitionIssueRequest.cs`:
```csharp
namespace Waypoint.Contracts;

public sealed record TransitionIssueRequest(Guid ToStateId, string? CommentMd = null);
```

- [ ] **Step 2: Failing test**

Add to `IssueEndpointsTests.cs`:
```csharp
[Fact]
public async Task POST_transition_to_existing_state_succeeds_when_workflow_allows()
{
    await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
    await factory.EnsureMigratedAsync();
    using var client = factory.CreateClient();

    await client.PostAsJsonAsync("/api/v1/projects",
        new CreateProjectRequest("trans-proj", "T", "TRP"));
    // Add a second state via raw DbContext for now; full state endpoints come later.
    using (var scope = factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<Waypoint.Domain.WaypointDbContext>();
        var project = db.Projects.Single(p => p.Slug == "trans-proj");
        var inProgress = new Waypoint.Domain.Entities.State
        {
            ProjectId = project.Id, Name = "In Progress",
            Group = Waypoint.Domain.Enums.StateGroup.Started,
            Color = "#22c55e", SortOrder = 1
        };
        db.States.Add(inProgress);
        var workflow = db.Workflows.Single(w => w.ProjectId == project.Id);
        db.WorkflowTransitions.Add(new Waypoint.Domain.Entities.WorkflowTransition
        {
            WorkflowId = workflow.Id,
            FromStateId = project.DefaultStateId!.Value,
            ToStateId = inProgress.Id,
        });
        db.SaveChanges();
    }

    var issue = await (await client.PostAsJsonAsync("/api/v1/projects/trans-proj/issues",
        new CreateIssueRequest("T1", "body"))).Content.ReadFromJsonAsync<IssueDto>();

    // Find the in-progress state id
    Guid inProgressId;
    using (var scope = factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<Waypoint.Domain.WaypointDbContext>();
        inProgressId = db.States.Single(s => s.Name == "In Progress").Id;
    }

    var resp = await client.PostAsJsonAsync($"/api/v1/projects/trans-proj/issues/{issue!.Sequence}/transitions",
        new TransitionIssueRequest(inProgressId));
    resp.StatusCode.Should().Be(HttpStatusCode.OK);
    var updated = await resp.Content.ReadFromJsonAsync<IssueDto>();
    updated!.StateName.Should().Be("In Progress");
}

[Fact]
public async Task POST_transition_returns_409_when_workflow_disallows()
{
    await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
    await factory.EnsureMigratedAsync();
    using var client = factory.CreateClient();

    await client.PostAsJsonAsync("/api/v1/projects",
        new CreateProjectRequest("bad-trans", "BT", "BTP"));
    Guid otherId;
    using (var scope = factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<Waypoint.Domain.WaypointDbContext>();
        var project = db.Projects.Single(p => p.Slug == "bad-trans");
        var other = new Waypoint.Domain.Entities.State
        {
            ProjectId = project.Id, Name = "Done",
            Group = Waypoint.Domain.Enums.StateGroup.Completed,
            Color = "#22c55e", SortOrder = 1
        };
        db.States.Add(other); db.SaveChanges();
        otherId = other.Id;
        // NO transition row added → disallowed
    }

    var issue = await (await client.PostAsJsonAsync("/api/v1/projects/bad-trans/issues",
        new CreateIssueRequest("Will fail", ""))).Content.ReadFromJsonAsync<IssueDto>();

    var resp = await client.PostAsJsonAsync($"/api/v1/projects/bad-trans/issues/{issue!.Sequence}/transitions",
        new TransitionIssueRequest(otherId));
    resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
    err!.Error.Code.Should().Be("transition_not_allowed");
}
```

Add this using to the top of the test file:
```csharp
using Microsoft.Extensions.DependencyInjection;
```

- [ ] **Step 3: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~IssueEndpointsTests`
Expected: FAIL (two new tests).

- [ ] **Step 4: Add transition method to `IIssueRepository`**

Add to `IIssueRepository.cs`:
```csharp
Task<Issue> TransitionAsync(Guid projectId, int seq, Guid toStateId, CancellationToken ct);
```

- [ ] **Step 5: Implement `TransitionAsync`**

Add to `IssueRepository.cs`:
```csharp
public async Task<Issue> TransitionAsync(Guid projectId, int seq, Guid toStateId, CancellationToken ct)
{
    var issue = await _db.Issues
        .Include(i => i.State).Include(i => i.IssueType)
        .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.SequenceId == seq, ct)
        ?? throw new NotFoundException("issue_not_found", "Issue not found.");

    var newState = await _db.States.FindAsync([toStateId], ct)
        ?? throw new NotFoundException("state_not_found", "Target state not found.");
    if (newState.ProjectId != projectId)
        throw new Waypoint.Domain.ValidationException("state_wrong_project", "State does not belong to this project.");

    if (issue.StateId == toStateId)
        return issue; // no-op

    var workflowId = issue.IssueType.DefaultWorkflowId
        ?? throw new ConflictException("issue_type_has_no_workflow", "Issue type has no default workflow.");

    var transitionRows = await _db.WorkflowTransitions
        .Where(t => t.WorkflowId == workflowId)
        .Select(t => new { t.FromStateId, t.ToStateId })
        .ToListAsync(ct);
    var transitions = transitionRows.Select(t => (t.FromStateId, t.ToStateId));

    var validator = new Waypoint.Domain.Validation.WorkflowTransitionValidator(transitions);
    if (!validator.CanTransition(issue.StateId, toStateId))
        throw new ConflictException("transition_not_allowed",
            $"Transition from state '{issue.State.Name}' to '{newState.Name}' is not allowed by the workflow.");

    var beforeStateId = issue.StateId;
    issue.StateId = toStateId;
    issue.UpdatedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);

    _db.Activities.Add(new Activity
    {
        IssueId = issue.Id,
        ActorType = Waypoint.Domain.Enums.ActorType.System,
        Verb = "transitioned",
        BeforeJson = $$"""{"state_id":"{{beforeStateId}}"}""",
        AfterJson = $$"""{"state_id":"{{toStateId}}"}""",
    });
    await _db.SaveChangesAsync(ct);

    return await GetBySequenceAsync(projectId, seq, ct)
        ?? throw new InvalidOperationException("Issue disappeared after transition.");
}
```

- [ ] **Step 6: Add endpoint to `IssueEndpoints`**

Inside `MapIssueEndpoints` after the GET, add:
```csharp
group.MapPost("/{seq:int}/transitions", async (string slug, int seq, TransitionIssueRequest req,
    IProjectRepository projects, IIssueRepository issues, CancellationToken ct) =>
{
    var project = await projects.GetBySlugAsync(slug, ct)
        ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
    var updated = await issues.TransitionAsync(project.Id, seq, req.ToStateId, ct);
    return Results.Ok(ToDto(updated));
});
```

- [ ] **Step 7: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~IssueEndpointsTests`
Expected: PASS (all 5 issue tests now).

- [ ] **Step 8: Commit**

```bash
git add src/ tests/Waypoint.Api.Tests/Endpoints/IssueEndpointsTests.cs
git commit -m "feat(api): add issue transition endpoint with workflow validation"
```

---

## Task 23: Issue list endpoint with cursor pagination

**Files:**
- Modify: `src/Waypoint.Api/Repositories/IIssueRepository.cs`
- Modify: `src/Waypoint.Api/Repositories/IssueRepository.cs`
- Modify: `src/Waypoint.Api/Endpoints/IssueEndpoints.cs`
- Modify: `tests/Waypoint.Api.Tests/Endpoints/IssueEndpointsTests.cs`

- [ ] **Step 1: Add interface method**

Add to `IIssueRepository.cs`:
```csharp
Task<(IReadOnlyList<Issue> Items, long Total)> ListAsync(Guid projectId, int limit, string? cursor, CancellationToken ct);
```

- [ ] **Step 2: Failing test**

Add to `IssueEndpointsTests.cs`:
```csharp
[Fact]
public async Task GET_list_returns_page_of_issues_with_next_cursor()
{
    await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
    await factory.EnsureMigratedAsync();
    using var client = factory.CreateClient();

    await client.PostAsJsonAsync("/api/v1/projects",
        new CreateProjectRequest("list-proj", "L", "LP1"));
    for (var i = 0; i < 5; i++)
    {
        await client.PostAsJsonAsync("/api/v1/projects/list-proj/issues",
            new CreateIssueRequest($"Issue {i}", "body"));
    }

    var resp = await client.GetAsync("/api/v1/projects/list-proj/issues?limit=3");
    resp.StatusCode.Should().Be(HttpStatusCode.OK);
    var page = await resp.Content.ReadFromJsonAsync<PagedResponse<IssueDto>>();
    page!.Data.Should().HaveCount(3);
    page.NextCursor.Should().NotBeNullOrEmpty();
    page.TotalCount.Should().Be(5);

    var resp2 = await client.GetAsync($"/api/v1/projects/list-proj/issues?limit=3&cursor={Uri.EscapeDataString(page.NextCursor!)}");
    var page2 = await resp2.Content.ReadFromJsonAsync<PagedResponse<IssueDto>>();
    page2!.Data.Should().HaveCount(2);
    page2.NextCursor.Should().BeNull();
}
```

- [ ] **Step 3: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~GET_list`

- [ ] **Step 4: Implement `ListAsync` (newest first)**

Add to `IssueRepository.cs`:
```csharp
public async Task<(IReadOnlyList<Issue> Items, long Total)> ListAsync(Guid projectId, int limit, string? cursor, CancellationToken ct)
{
    var query = _db.Issues.AsNoTracking()
        .Include(i => i.State).Include(i => i.IssueType)
        .Where(i => i.ProjectId == projectId);

    if (!string.IsNullOrEmpty(cursor))
    {
        var (ts, id) = Waypoint.Api.Pagination.Cursor.Decode(cursor);
        query = query.Where(i => i.CreatedAt < ts || (i.CreatedAt == ts && i.Id.CompareTo(id) < 0));
    }

    var total = await _db.Issues.AsNoTracking().Where(i => i.ProjectId == projectId).LongCountAsync(ct);
    var items = await query
        .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
        .Take(Math.Clamp(limit, 1, 200))
        .ToListAsync(ct);
    return (items, total);
}
```

- [ ] **Step 5: Add endpoint**

Inside `MapIssueEndpoints` before the closing brace, add:
```csharp
group.MapGet("/", async (string slug, int? limit, string? cursor,
    IProjectRepository projects, IIssueRepository issues, CancellationToken ct) =>
{
    var project = await projects.GetBySlugAsync(slug, ct)
        ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
    var pageSize = limit ?? 50;
    var (items, total) = await issues.ListAsync(project.Id, pageSize, cursor, ct);
    string? nextCursor = null;
    if (items.Count == pageSize)
    {
        var last = items[^1];
        nextCursor = Waypoint.Api.Pagination.Cursor.Encode(last.CreatedAt, last.Id);
    }
    var data = items.Select(ToDto).ToList();
    return Results.Ok(new PagedResponse<IssueDto>(data, nextCursor, total));
});
```

- [ ] **Step 6: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~IssueEndpointsTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/ tests/Waypoint.Api.Tests/Endpoints/IssueEndpointsTests.cs
git commit -m "feat(api): add issue list endpoint with cursor pagination"
```

---

## Task 24: Issue PATCH endpoint (title / description / priority)

**Files:**
- Create: `src/Waypoint.Contracts/UpdateIssueRequest.cs`
- Modify: `src/Waypoint.Api/Repositories/IIssueRepository.cs`
- Modify: `src/Waypoint.Api/Repositories/IssueRepository.cs`
- Modify: `src/Waypoint.Api/Endpoints/IssueEndpoints.cs`
- Modify: `tests/Waypoint.Api.Tests/Endpoints/IssueEndpointsTests.cs`

- [ ] **Step 1: DTO**

Create `src/Waypoint.Contracts/UpdateIssueRequest.cs`:
```csharp
namespace Waypoint.Contracts;

public sealed record UpdateIssueRequest(string? Title = null, string? DescriptionMd = null, int? Priority = null);
```

- [ ] **Step 2: Failing test**

Add to `IssueEndpointsTests.cs`:
```csharp
[Fact]
public async Task PATCH_updates_title_only_when_only_title_provided()
{
    await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
    await factory.EnsureMigratedAsync();
    using var client = factory.CreateClient();
    await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("patch-proj", "P", "PP1"));
    var issue = await (await client.PostAsJsonAsync("/api/v1/projects/patch-proj/issues",
        new CreateIssueRequest("Old", "Body unchanged"))).Content.ReadFromJsonAsync<IssueDto>();

    var resp = await client.PatchAsJsonAsync($"/api/v1/projects/patch-proj/issues/{issue!.Sequence}",
        new UpdateIssueRequest(Title: "New"));
    resp.StatusCode.Should().Be(HttpStatusCode.OK);
    var updated = await resp.Content.ReadFromJsonAsync<IssueDto>();
    updated!.Title.Should().Be("New");
    updated.DescriptionMd.Should().Be("Body unchanged");
}
```

- [ ] **Step 3: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~PATCH_updates_title`

- [ ] **Step 4: Add interface method**

Add to `IIssueRepository.cs`:
```csharp
Task<Issue> UpdateAsync(Guid projectId, int seq, string? title, string? descriptionMd, int? priority, CancellationToken ct);
```

- [ ] **Step 5: Implement**

Add to `IssueRepository.cs`:
```csharp
public async Task<Issue> UpdateAsync(Guid projectId, int seq, string? title, string? descriptionMd, int? priority, CancellationToken ct)
{
    var issue = await _db.Issues.FirstOrDefaultAsync(i => i.ProjectId == projectId && i.SequenceId == seq, ct)
        ?? throw new NotFoundException("issue_not_found", "Issue not found.");
    if (title is not null) issue.Title = title;
    if (descriptionMd is not null) issue.DescriptionMd = descriptionMd;
    if (priority is not null) issue.Priority = (Waypoint.Domain.Enums.Priority)priority.Value;
    issue.UpdatedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);

    _db.Activities.Add(new Activity
    {
        IssueId = issue.Id,
        ActorType = Waypoint.Domain.Enums.ActorType.System,
        Verb = "updated",
    });
    await _db.SaveChangesAsync(ct);

    return await GetBySequenceAsync(projectId, seq, ct)
        ?? throw new InvalidOperationException("Issue vanished after update.");
}
```

- [ ] **Step 6: Add endpoint**

Inside `MapIssueEndpoints` add:
```csharp
group.MapPatch("/{seq:int}", async (string slug, int seq, UpdateIssueRequest req,
    IProjectRepository projects, IIssueRepository issues, CancellationToken ct) =>
{
    var project = await projects.GetBySlugAsync(slug, ct)
        ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
    var updated = await issues.UpdateAsync(project.Id, seq, req.Title, req.DescriptionMd, req.Priority, ct);
    return Results.Ok(ToDto(updated));
});
```

- [ ] **Step 7: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~IssueEndpointsTests`

- [ ] **Step 8: Commit**

```bash
git add src/ tests/Waypoint.Api.Tests/Endpoints/IssueEndpointsTests.cs
git commit -m "feat(api): add issue PATCH endpoint"
```

---

## Task 25: Issue activity endpoint

**Files:**
- Create: `src/Waypoint.Contracts/ActivityDto.cs`
- Modify: `src/Waypoint.Api/Endpoints/IssueEndpoints.cs`
- Modify: `tests/Waypoint.Api.Tests/Endpoints/IssueEndpointsTests.cs`

- [ ] **Step 1: DTO**

Create `src/Waypoint.Contracts/ActivityDto.cs`:
```csharp
namespace Waypoint.Contracts;

public sealed record ActivityDto(
    Guid Id,
    string ActorType,       // serialized as the enum name
    Guid? ActorId,
    string? ActorLabel,
    string Verb,
    string? BeforeJson,
    string? AfterJson,
    DateTimeOffset At);
```

- [ ] **Step 2: Failing test**

Add to `IssueEndpointsTests.cs`:
```csharp
[Fact]
public async Task GET_activity_returns_creation_event()
{
    await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
    await factory.EnsureMigratedAsync();
    using var client = factory.CreateClient();
    await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("act-proj", "A", "AP1"));
    var issue = await (await client.PostAsJsonAsync("/api/v1/projects/act-proj/issues",
        new CreateIssueRequest("X", ""))).Content.ReadFromJsonAsync<IssueDto>();

    var resp = await client.GetAsync($"/api/v1/projects/act-proj/issues/{issue!.Sequence}/activity");
    resp.StatusCode.Should().Be(HttpStatusCode.OK);
    var events = await resp.Content.ReadFromJsonAsync<List<ActivityDto>>();
    events!.Should().NotBeEmpty();
    events.Should().Contain(e => e.Verb == "created");
}
```

- [ ] **Step 3: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~GET_activity`

- [ ] **Step 4: Add endpoint**

Inside `MapIssueEndpoints` add:
```csharp
group.MapGet("/{seq:int}/activity", async (string slug, int seq,
    IProjectRepository projects, Waypoint.Domain.WaypointDbContext db, CancellationToken ct) =>
{
    var project = await projects.GetBySlugAsync(slug, ct)
        ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
    var issue = await db.Issues.FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.SequenceId == seq, ct)
        ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
    var events = await db.Activities.AsNoTracking()
        .Where(a => a.IssueId == issue.Id)
        .OrderBy(a => a.At)
        .Select(a => new ActivityDto(a.Id, a.ActorType.ToString(), a.ActorId, a.ActorLabel,
            a.Verb, a.BeforeJson, a.AfterJson, a.At))
        .ToListAsync(ct);
    return Results.Ok(events);
});
```

(Add `using Microsoft.EntityFrameworkCore;` to the file if not already present.)

- [ ] **Step 5: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~IssueEndpointsTests`

- [ ] **Step 6: Commit**

```bash
git add src/ tests/Waypoint.Api.Tests/Endpoints/IssueEndpointsTests.cs
git commit -m "feat(api): add issue activity timeline endpoint"
```

---

## Task 26: Comment endpoints (create + list)

**Files:**
- Create: `src/Waypoint.Contracts/CommentDto.cs`
- Create: `src/Waypoint.Contracts/CreateCommentRequest.cs`
- Create: `src/Waypoint.Api/Repositories/ICommentRepository.cs`
- Create: `src/Waypoint.Api/Repositories/CommentRepository.cs`
- Create: `src/Waypoint.Api/Endpoints/CommentEndpoints.cs`
- Create: `tests/Waypoint.Api.Tests/Endpoints/CommentEndpointsTests.cs`
- Modify: `src/Waypoint.Api/Program.cs`

- [ ] **Step 1: DTOs**

Create `src/Waypoint.Contracts/CommentDto.cs`:
```csharp
namespace Waypoint.Contracts;

public sealed record CommentDto(
    Guid Id,
    Guid IssueId,
    string BodyMd,
    Guid? AuthorUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```

Create `src/Waypoint.Contracts/CreateCommentRequest.cs`:
```csharp
namespace Waypoint.Contracts;

public sealed record CreateCommentRequest(string BodyMd);
```

- [ ] **Step 2: Failing test**

Create `tests/Waypoint.Api.Tests/Endpoints/CommentEndpointsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Endpoints;

public class CommentEndpointsTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public CommentEndpointsTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task POST_then_GET_round_trips_a_comment()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("cmt-proj", "C", "CP1"));
        var issue = await (await client.PostAsJsonAsync("/api/v1/projects/cmt-proj/issues",
            new CreateIssueRequest("I", ""))).Content.ReadFromJsonAsync<IssueDto>();

        var resp = await client.PostAsJsonAsync($"/api/v1/projects/cmt-proj/issues/{issue!.Sequence}/comments",
            new CreateCommentRequest("Hello **comment**"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<CommentDto>();
        dto!.BodyMd.Should().Be("Hello **comment**");

        var listResp = await client.GetAsync($"/api/v1/projects/cmt-proj/issues/{issue.Sequence}/comments");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResp.Content.ReadFromJsonAsync<List<CommentDto>>();
        list!.Should().HaveCount(1);
        list[0].BodyMd.Should().Be("Hello **comment**");
    }
}
```

- [ ] **Step 3: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~CommentEndpointsTests`

- [ ] **Step 4: Repo interface**

Create `src/Waypoint.Api/Repositories/ICommentRepository.cs`:
```csharp
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Repositories;

public interface ICommentRepository
{
    Task<Comment> CreateAsync(Guid issueId, string bodyMd, Guid? authorUserId, CancellationToken ct);
    Task<IReadOnlyList<Comment>> ListByIssueAsync(Guid issueId, CancellationToken ct);
}
```

- [ ] **Step 5: Implementation**

Create `src/Waypoint.Api/Repositories/CommentRepository.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Waypoint.Domain;
using Waypoint.Domain.Entities;

namespace Waypoint.Api.Repositories;

public sealed class CommentRepository : ICommentRepository
{
    private readonly WaypointDbContext _db;
    public CommentRepository(WaypointDbContext db) => _db = db;

    public async Task<Comment> CreateAsync(Guid issueId, string bodyMd, Guid? authorUserId, CancellationToken ct)
    {
        var comment = new Comment { IssueId = issueId, BodyMd = bodyMd, AuthorUserId = authorUserId };
        _db.Comments.Add(comment);
        _db.Activities.Add(new Activity
        {
            IssueId = issueId,
            ActorType = authorUserId is null ? Waypoint.Domain.Enums.ActorType.System : Waypoint.Domain.Enums.ActorType.User,
            ActorId = authorUserId,
            Verb = "commented",
        });
        await _db.SaveChangesAsync(ct);
        return comment;
    }

    public async Task<IReadOnlyList<Comment>> ListByIssueAsync(Guid issueId, CancellationToken ct) =>
        await _db.Comments.AsNoTracking()
            .Where(c => c.IssueId == issueId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);
}
```

- [ ] **Step 6: Endpoints**

Create `src/Waypoint.Api/Endpoints/CommentEndpoints.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Waypoint.Api.Repositories;
using Waypoint.Contracts;
using Waypoint.Domain;

namespace Waypoint.Api.Endpoints;

public static class CommentEndpoints
{
    public static void MapCommentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/projects/{slug}/issues/{seq:int}/comments");

        group.MapPost("/", async (string slug, int seq, CreateCommentRequest req,
            IProjectRepository projects, ICommentRepository comments, WaypointDbContext db, CancellationToken ct) =>
        {
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var issue = await db.Issues.FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.SequenceId == seq, ct)
                ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
            var c = await comments.CreateAsync(issue.Id, req.BodyMd, authorUserId: null, ct);
            var dto = new CommentDto(c.Id, c.IssueId, c.BodyMd, c.AuthorUserId, c.CreatedAt, c.UpdatedAt);
            return Results.Created($"/api/v1/comments/{c.Id}", dto);
        });

        group.MapGet("/", async (string slug, int seq,
            IProjectRepository projects, ICommentRepository comments, WaypointDbContext db, CancellationToken ct) =>
        {
            var project = await projects.GetBySlugAsync(slug, ct)
                ?? throw new NotFoundException("project_not_found", $"Project '{slug}' not found.");
            var issue = await db.Issues.FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.SequenceId == seq, ct)
                ?? throw new NotFoundException("issue_not_found", $"Issue {project.Identifier}-{seq} not found.");
            var list = await comments.ListByIssueAsync(issue.Id, ct);
            return Results.Ok(list.Select(c => new CommentDto(c.Id, c.IssueId, c.BodyMd, c.AuthorUserId, c.CreatedAt, c.UpdatedAt)));
        });
    }
}
```

- [ ] **Step 7: Register in Program.cs**

Add after `IssueRepository` registration:
```csharp
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
```

And after `app.MapIssueEndpoints();`:
```csharp
app.MapCommentEndpoints();
```

- [ ] **Step 8: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~CommentEndpointsTests`

- [ ] **Step 9: Commit**

```bash
git add src/ tests/Waypoint.Api.Tests/Endpoints/CommentEndpointsTests.cs
git commit -m "feat(api): add comment create and list endpoints"
```

---

## Task 27: Idempotency middleware

**Files:**
- Create: `src/Waypoint.Api/Middleware/IdempotencyMiddleware.cs`
- Create: `tests/Waypoint.Api.Tests/Middleware/IdempotencyMiddlewareTests.cs`
- Modify: `src/Waypoint.Api/Program.cs`

For Phase 1 we use a per-process in-memory cache (24h TTL) rather than a DB table — DB-backed idempotency arrives in Phase 2 alongside per-token scoping. This is a Phase 1 placeholder that proves the pipeline.

- [ ] **Step 1: Failing test**

Create `tests/Waypoint.Api.Tests/Middleware/IdempotencyMiddlewareTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Waypoint.Api.Tests.Fixtures;
using Waypoint.Contracts;
using Xunit;

namespace Waypoint.Api.Tests.Middleware;

public class IdempotencyMiddlewareTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public IdempotencyMiddlewareTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Repeating_POST_with_same_Idempotency_Key_returns_first_response()
    {
        await using var factory = new WaypointApiFactory { PostgresConnectionString = _pg.ConnectionString };
        await factory.EnsureMigratedAsync();
        using var client = factory.CreateClient();
        var key = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", key);

        var first = await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("idem-proj", "I", "ID1"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await first.Content.ReadFromJsonAsync<ProjectDto>();

        var second = await client.PostAsJsonAsync("/api/v1/projects",
            new CreateProjectRequest("idem-proj-different", "I2", "ID2"));   // different body — should NOT matter
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondBody = await second.Content.ReadFromJsonAsync<ProjectDto>();
        secondBody!.Id.Should().Be(firstBody!.Id);   // same response replayed
    }
}
```

- [ ] **Step 2: Verify FAIL**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~IdempotencyMiddlewareTests`

- [ ] **Step 3: Implement middleware**

Create `src/Waypoint.Api/Middleware/IdempotencyMiddleware.cs`:
```csharp
using System.Collections.Concurrent;
using System.Text;

namespace Waypoint.Api.Middleware;

public sealed class IdempotencyMiddleware
{
    public const string HeaderName = "Idempotency-Key";
    private static readonly ConcurrentDictionary<string, CachedResponse> _cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly RequestDelegate _next;
    public IdempotencyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!HttpMethods.IsPost(ctx.Request.Method) && !HttpMethods.IsPatch(ctx.Request.Method))
        {
            await _next(ctx);
            return;
        }
        if (!ctx.Request.Headers.TryGetValue(HeaderName, out var key) || string.IsNullOrWhiteSpace(key))
        {
            await _next(ctx);
            return;
        }

        var k = key.ToString();
        if (_cache.TryGetValue(k, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            ctx.Response.StatusCode = cached.StatusCode;
            ctx.Response.ContentType = cached.ContentType;
            await ctx.Response.Body.WriteAsync(cached.Body);
            return;
        }

        var originalBody = ctx.Response.Body;
        using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;
        await _next(ctx);
        buffer.Position = 0;
        var bytes = buffer.ToArray();
        _cache[k] = new CachedResponse(ctx.Response.StatusCode, ctx.Response.ContentType ?? "application/json", bytes, DateTimeOffset.UtcNow + Ttl);
        await originalBody.WriteAsync(bytes);
        ctx.Response.Body = originalBody;
    }

    private sealed record CachedResponse(int StatusCode, string ContentType, byte[] Body, DateTimeOffset ExpiresAt);
}
```

- [ ] **Step 4: Register in Program.cs**

Add after `app.UseMiddleware<ErrorEnvelopeMiddleware>();`:
```csharp
app.UseMiddleware<IdempotencyMiddleware>();
```

- [ ] **Step 5: Verify PASS**

Run: `dotnet test tests/Waypoint.Api.Tests --filter FullyQualifiedName~IdempotencyMiddlewareTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/ tests/Waypoint.Api.Tests/Middleware/IdempotencyMiddlewareTests.cs
git commit -m "feat(api): add in-memory IdempotencyMiddleware (DB-backed version in Phase 2)"
```

---

## Task 28: Architecture tests — Phase 1 module boundary rules

**Files:**
- Create: `tests/Waypoint.Architecture.Tests/BoundaryTests.cs`

Only Phase-1-applicable rules. The two-port resolver rules appear in Phase 2 when those types exist.

- [ ] **Step 1: Write the failing test (or in this case, the test that's intended to PASS as a guard)**

Create `tests/Waypoint.Architecture.Tests/BoundaryTests.cs`:
```csharp
using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Waypoint.Architecture.Tests;

public class BoundaryTests
{
    private static readonly Assembly DomainAsm = typeof(Waypoint.Domain.WaypointDbContext).Assembly;
    private static readonly Assembly ApiAsm = typeof(Program).Assembly;
    private static readonly Assembly ContractsAsm = typeof(Waypoint.Contracts.ProjectDto).Assembly;

    [Fact]
    public void Domain_must_not_reference_Api()
    {
        var result = Types.InAssembly(DomainAsm)
            .Should().NotHaveDependencyOn("Waypoint.Api")
            .GetResult();
        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Contracts_must_not_reference_EFCore()
    {
        var result = Types.InAssembly(ContractsAsm)
            .Should().NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();
        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Contracts_must_not_reference_Npgsql()
    {
        var result = Types.InAssembly(ContractsAsm)
            .Should().NotHaveDependencyOn("Npgsql")
            .GetResult();
        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
```

- [ ] **Step 2: Run — expect PASS** (the rules are designed to be satisfied by the existing code)

Run:
```bash
dotnet test tests/Waypoint.Architecture.Tests
```
Expected: PASS (3 tests). If any fail, the failure message names the offending type — fix the dependency leak before continuing.

- [ ] **Step 3: Commit**

```bash
git add tests/Waypoint.Architecture.Tests/BoundaryTests.cs
git commit -m "test(arch): add Phase 1 module boundary rules"
```

---

## Task 29: Dockerfile for Waypoint.Api

**Files:**
- Create: `src/Waypoint.Api/Dockerfile`

- [ ] **Step 1: Write the Dockerfile**

Create `src/Waypoint.Api/Dockerfile`:
```dockerfile
# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Waypoint.Api/Waypoint.Api.csproj      src/Waypoint.Api/
COPY src/Waypoint.Domain/Waypoint.Domain.csproj    src/Waypoint.Domain/
COPY src/Waypoint.Contracts/Waypoint.Contracts.csproj src/Waypoint.Contracts/
RUN dotnet restore src/Waypoint.Api/Waypoint.Api.csproj
COPY src/ src/
RUN dotnet publish src/Waypoint.Api/Waypoint.Api.csproj -c Release -o /publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app
COPY --from=build /publish ./
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Waypoint.Api.dll"]
```

- [ ] **Step 2: Build the image to verify**

Run (from repo root):
```bash
docker build -f src/Waypoint.Api/Dockerfile -t waypoint-api:phase1 .
```
Expected: build succeeds, final image tagged `waypoint-api:phase1`.

- [ ] **Step 3: Commit**

```bash
git add src/Waypoint.Api/Dockerfile
git commit -m "build(api): add multi-stage Dockerfile"
```

---

## Task 30: docker-compose.yml for local development

**Files:**
- Create: `docker-compose.yml`

- [ ] **Step 1: Write compose file**

Create `docker-compose.yml`:
```yaml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: waypoint
      POSTGRES_USER: waypoint
      POSTGRES_PASSWORD: waypoint
    ports:
      - "5432:5432"
    volumes:
      - waypoint-pg:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U waypoint"]
      interval: 2s
      timeout: 5s
      retries: 30

  api:
    build:
      context: .
      dockerfile: src/Waypoint.Api/Dockerfile
    environment:
      ConnectionStrings__Postgres: "Host=postgres;Database=waypoint;Username=waypoint;Password=waypoint"
      ASPNETCORE_ENVIRONMENT: Development
    depends_on:
      postgres:
        condition: service_healthy
    ports:
      - "8080:8080"

volumes:
  waypoint-pg:
```

- [ ] **Step 2: Start the stack**

Run:
```bash
docker compose up -d --build
sleep 10
```

- [ ] **Step 3: Apply migrations against the live DB**

Run:
```bash
WAYPOINT_DESIGN_TIME_CONNSTR="Host=localhost;Database=waypoint;Username=waypoint;Password=waypoint" \
  dotnet ef database update --project src/Waypoint.Domain --startup-project src/Waypoint.Domain
```
Expected: `Done.`

- [ ] **Step 4: Smoke test the running stack**

Run:
```bash
curl -s http://localhost:8080/healthz/live
curl -s -X POST http://localhost:8080/api/v1/projects \
  -H "Content-Type: application/json" \
  -d '{"slug":"smoke","name":"Smoke","identifier":"SMK"}'
curl -s -X POST http://localhost:8080/api/v1/projects/smoke/issues \
  -H "Content-Type: application/json" \
  -d '{"title":"first issue","descriptionMd":"hello"}'
curl -s http://localhost:8080/api/v1/projects/smoke/issues/1
```
Expected: each call returns 200/201 with sensible JSON. Final GET returns the issue with `"sequence": 1` and `"stateName": "Backlog"`.

- [ ] **Step 5: Tear down**

Run:
```bash
docker compose down
```

- [ ] **Step 6: Commit**

```bash
git add docker-compose.yml
git commit -m "build: add docker-compose for local development"
```

---

## Task 31: Full test sweep + push

**No new files** — just verification.

- [ ] **Step 1: Run the entire test suite**

Run:
```bash
dotnet test
```
Expected: All tests pass. Output should show > 25 tests passing across Domain.Tests, Api.Tests, and Architecture.Tests.

- [ ] **Step 2: Verify migration count is sane**

Run:
```bash
ls src/Waypoint.Domain/Migrations/*.cs | grep -v Designer | grep -v ModelSnapshot | wc -l
```
Expected: 8 migration files (Project, State, IssueType, Workflows, User, Issue, Comment, Activity).

- [ ] **Step 3: Push to Gitea**

Run:
```bash
git push origin main
```
Expected: `main -> main` push succeeds.

- [ ] **Step 4: Verify GitHub mirror picked up**

Wait ~30 seconds then run:
```bash
gh api repos/casachristo/plane-replacement/commits/main --jq '.commit.message' | head -3
```
Expected: shows the latest commit message ("test sweep + push" or similar).

- [ ] **Step 5: Commit and push completed** — Phase 1 is shipped.

---

## Phase 1 done — what now

Phase 1 ships:
- Solution scaffold with 6 projects (3 src, 3 tests)
- 9 entities + 8 migrations
- 13 endpoints across projects, issues, comments
- 3 middleware (request-id, error-envelope, idempotency)
- Workflow validation that rejects illegal state transitions with 409
- Cursor pagination
- Real-DB integration tests via Testcontainers
- `docker compose up` local stack
- Architecture tests enforcing the Phase 1 boundary rules

### What's deliberately NOT in Phase 1 (and where it shows up)

These are real spec endpoints that you might expect to see — they're held back on purpose:

| Not in Phase 1 | Where it ships | Why deferred |
|---|---|---|
| `GET /api/v1/projects` (list) | Phase 4 (SPA needs it) or earlier if Cairn needs to enumerate projects | No consumer in Phase 1 — adding it would be code-without-tests-that-matter |
| `DELETE /api/v1/projects/{slug}/issues/{seq}` (soft-delete) | Phase 5 (admin operations) | Soft-delete is already wired (`deleted_at` column + query filter); the endpoint just isn't exposed yet |
| `PATCH /api/v1/comments/{id}` and `DELETE /api/v1/comments/{id}` | Phase 4 (SPA comment editor) | Same reason |
| `POST /api/v1/projects/{slug}/issues/{seq}/links` | Phase 5 | Issue links table not added yet |
| `GET /api/v1/search` | Phase 5 | Search needs more thought (Postgres full-text vs. external engine) |
| Webhook delivery on issue changes | Phase 5 | Webhook tables not in Phase 1 |
| Any auth — endpoints are wide open | Phase 2 (internal) + Phase 4 (public) | Explicit scope decision in the roadmap |

If during Phase 2/3 work the absence of `GET /api/v1/projects` becomes annoying, add it as a one-task addendum to this plan rather than waiting for Phase 4.

### Next plan

Come back and write `docs/superpowers/plans/phase2-internal-surface.md` — adds the second Kestrel port, `IPrincipalResolver`, `ServiceBearerResolver`, seed-token CLI, `Waypoint.Contracts` NuGet packaging, and intent endpoints.
