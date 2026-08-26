# ProjectHub.DataSeeder

A dev-time console tool that fills the ProjectHub database with a large, realistic dataset for
testing lists, search, pagination, notifications, and query performance. It reuses the real domain
factory methods and EF Core mappings (via project references to `ProjectHub.Persistence` and
`ProjectHub.Domain`), so seeded rows are indistinguishable from rows the running app produces.

It is **additive only** — it never modifies or deletes existing rows. Re-running it adds more data.
Every seeded user shares one password so you can sign in as anyone, plus a stable
`admin@projecthub.local` account created on the first run and reused afterwards.

Domain-event interceptors are intentionally **not** registered, so bulk seeding fires no emails or
notification side effects; notification rows are written directly instead.

## Usage

```bash
# Seed with the defaults from appsettings.json (~200 users, ~50 projects, thousands of tasks/comments)
dotnet run --project tools/ProjectHub.DataSeeder

# Override volume / RNG seed / target
dotnet run --project tools/ProjectHub.DataSeeder -- --users 500 --projects 120 --seed 42
dotnet run --project tools/ProjectHub.DataSeeder -- --connection "Server=AMIR;Database=ProjectHub;Trusted_Connection=True;TrustServerCertificate=True;"

dotnet run --project tools/ProjectHub.DataSeeder -- --help
```

Connection string precedence: `--connection` > env `ConnectionStrings__Database` > `appsettings.json`.

The tool applies any pending EF Core migrations before seeding, so it works against a fresh database.
