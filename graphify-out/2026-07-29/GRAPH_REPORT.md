# Graph Report - .  (2026-07-25)

## Corpus Check
- Corpus is ~22,944 words - fits in a single context window. You may not need a graph.

## Summary
- 537 nodes · 1008 edges · 30 communities (22 shown, 8 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 32 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Domain Model Core
- Persistence & Guard Utilities
- Project Aggregate & Tasks
- Application Pipeline & Services
- Project Configuration & Dependencies
- Project Member Tests
- Sprint Aggregate & Tests
- CQRS Messaging Abstractions
- Task Domain & Value Objects
- Comment Aggregate & Tests
- User Domain Tests
- Notification Aggregate & Tests
- Persistence Interfaces
- API Launch Configuration
- Web Launch Configuration
- Audit Log & Tests
- Application Exceptions
- Blazor Web Setup
- Application Test Stub
- Blazor Routing
- Blazor App Shell
- Infrastructure Stub
- Persistence Stub
- Counter Component
- Weather Component
- NavLink Component
- Navigation Layout
- API Entry Point
- Home Page

## God Nodes (most connected - your core abstractions)
1. `ProjectHub.Domain.Events` - 32 edges
2. `ProjectHub.Domain.Abstractions` - 22 edges
3. `IDomainEvent` - 21 edges
4. `ProjectHub.Domain.Entities` - 21 edges
5. `ProjectHub.Domain.Enums` - 20 edges
6. `ProjectHub.Domain.Exceptions` - 20 edges
7. `ProjectHub.Domain.Primitives` - 20 edges
8. `ProjectHub.Domain.ValueObjects` - 19 edges
9. `Project` - 16 edges
10. `ProjectHub.Domain.Common` - 15 edges

## Surprising Connections (you probably didn't know these)
- `IApplicationDbContext` --references--> `AuditLog`  [EXTRACTED]
  src/ProjectHub.Application/Abstractions/Persistence/IApplicationDbContext.cs → src/ProjectHub.Domain/Entities/AuditLog.cs
- `IApplicationDbContext` --references--> `Comment`  [EXTRACTED]
  src/ProjectHub.Application/Abstractions/Persistence/IApplicationDbContext.cs → src/ProjectHub.Domain/Entities/Comment.cs
- `IApplicationDbContext` --references--> `Notification`  [EXTRACTED]
  src/ProjectHub.Application/Abstractions/Persistence/IApplicationDbContext.cs → src/ProjectHub.Domain/Entities/Notification.cs
- `IApplicationDbContext` --references--> `Project`  [EXTRACTED]
  src/ProjectHub.Application/Abstractions/Persistence/IApplicationDbContext.cs → src/ProjectHub.Domain/Entities/Project.cs
- `IApplicationDbContext` --references--> `ProjectTask`  [EXTRACTED]
  src/ProjectHub.Application/Abstractions/Persistence/IApplicationDbContext.cs → src/ProjectHub.Domain/Entities/ProjectTask.cs

## Import Cycles
- None detected.

## Communities (30 total, 8 thin omitted)

### Community 0 - "Domain Model Core"
Cohesion: 0.07
Nodes (30): ProjectHub.Domain.Enums, ProjectHub.Domain.ValueObjects, ProjectHub.Domain.Abstractions, ProjectHub.Domain.Exceptions, ProjectHub.Domain.Primitives, ProjectHub.Domain.Entities, ProjectHub.Domain.Events, ProjectHub.Domain.Common (+22 more)

### Community 1 - "Persistence & Guard Utilities"
Cohesion: 0.07
Nodes (28): DbSet, long, Regex, CancellationToken, Task, IApplicationDbContext, Guard, DateTime (+20 more)

### Community 2 - "Project Aggregate & Tasks"
Cohesion: 0.09
Nodes (23): Guid, DateTime, Guid, IReadOnlyCollection, List, Project, DateTime, Guid (+15 more)

### Community 3 - "Application Pipeline & Services"
Cohesion: 0.05
Nodes (30): ProjectHub.Application.Behaviors, ProjectHub.Application, ProjectHub.Application.Abstractions.Services, IPipelineBehavior, IServiceCollection, Guid, ICurrentUser, DateTime (+22 more)

### Community 4 - "Project Configuration & Dependencies"
Cohesion: 0.06
Nodes (33): FluentValidation (11.11.0), FluentValidation.DependencyInjectionExtensions (11.11.0), Mapster (7.4.0), Mapster.DependencyInjection (1.0.1), MediatR (12.4.1), Microsoft.AspNetCore.OpenApi (9.0.5), Microsoft.EntityFrameworkCore (9.0.0), Microsoft.Extensions.DependencyInjection.Abstractions (9.0.0) (+25 more)

### Community 5 - "Project Member Tests"
Cohesion: 0.16
Nodes (9): IEnumerable, int, ProjectName, DateTime, Fact, ProjectMemberTests, DateTime, Fact (+1 more)

### Community 6 - "Sprint Aggregate & Tests"
Cohesion: 0.15
Nodes (11): DateTime, Guid, Sprint, SprintStatus, DateTime, IEnumerable, DateRange, DateTime (+3 more)

### Community 7 - "CQRS Messaging Abstractions"
Cohesion: 0.12
Nodes (12): ProjectHub.Application.Abstractions.Messaging, ProjectHub.Application.Common, IRequest, IRequestHandler, IBaseCommand, ICommand, ICommandHandler, IQuery (+4 more)

### Community 8 - "Task Domain & Value Objects"
Cohesion: 0.15
Nodes (9): IEnumerable, ValueObject, IEnumerable, int, TaskTitle, DateTime, Fact, Guid (+1 more)

### Community 9 - "Comment Aggregate & Tests"
Cohesion: 0.19
Nodes (10): DateTime, Guid, Comment, IEnumerable, int, CommentBody, DateTime, Fact (+2 more)

### Community 10 - "User Domain Tests"
Cohesion: 0.24
Nodes (5): InlineData, DateTime, Fact, UserTests, Theory

### Community 11 - "Notification Aggregate & Tests"
Cohesion: 0.20
Nodes (9): DateTime, Guid, int, Notification, NotificationType, DateTime, Fact, Guid (+1 more)

### Community 12 - "Persistence Interfaces"
Cohesion: 0.14
Nodes (8): ProjectHub.Application.Abstractions.Persistence, CancellationToken, Guid, Task, IRepository, CancellationToken, Task, IUnitOfWork

### Community 13 - "API Launch Configuration"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 14 - "Web Launch Configuration"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 15 - "Audit Log & Tests"
Cohesion: 0.30
Nodes (6): DateTime, Guid, AuditLog, DateTime, Fact, AuditLogTests

### Community 16 - "Application Exceptions"
Cohesion: 0.22
Nodes (7): ProjectHub.Application.Common.Exceptions, Exception, IReadOnlyDictionary, ConflictException, NotFoundException, ValidationException, DomainException

### Community 17 - "Blazor Web Setup"
Cohesion: 0.18
Nodes (9): Microsoft.AspNetCore.Components.Forms, Microsoft.AspNetCore.Components.Routing, Microsoft.AspNetCore.Components.Web, Microsoft.AspNetCore.Components.Web.Virtualization, Microsoft.JSInterop, ProjectHub.Web, ProjectHub.Web.Components, static (+1 more)

### Community 18 - "Application Test Stub"
Cohesion: 0.40
Nodes (3): ProjectHub.Application.Tests, Fact, UnitTest1

### Community 19 - "Blazor Routing"
Cohesion: 0.40
Nodes (4): FocusOnNavigate, Found, Router, RouteView

### Community 20 - "Blazor App Shell"
Cohesion: 0.50
Nodes (3): HeadOutlet, ImportMap, Routes

## Knowledge Gaps
- **83 isolated node(s):** `WeatherForecast`, `net9.0`, `Microsoft.AspNetCore.OpenApi (9.0.5)`, `Microsoft.NET.Sdk.Web`, `$schema` (+78 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **8 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ProjectHub.Domain.Exceptions` connect `Domain Model Core` to `Application Exceptions`?**
  _High betweenness centrality (0.202) - this node is a cross-community bridge._
- **Why does `ValidationException` connect `Application Exceptions` to `Application Pipeline & Services`?**
  _High betweenness centrality (0.174) - this node is a cross-community bridge._
- **What connects `WeatherForecast`, `net9.0`, `Microsoft.AspNetCore.OpenApi (9.0.5)` to the rest of the system?**
  _83 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Domain Model Core` be split into smaller, more focused modules?**
  _Cohesion score 0.07157894736842105 - nodes in this community are weakly interconnected._
- **Should `Persistence & Guard Utilities` be split into smaller, more focused modules?**
  _Cohesion score 0.06561085972850679 - nodes in this community are weakly interconnected._
- **Should `Project Aggregate & Tasks` be split into smaller, more focused modules?**
  _Cohesion score 0.09468599033816426 - nodes in this community are weakly interconnected._
- **Should `Application Pipeline & Services` be split into smaller, more focused modules?**
  _Cohesion score 0.049682875264270614 - nodes in this community are weakly interconnected._