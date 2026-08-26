using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProjectHub.DataSeeder;
using ProjectHub.Persistence;

// ---------------------------------------------------------------------------------------------
// Entry point. Resolves configuration + CLI overrides, opens a plain ApplicationDbContext (no
// save interceptors, so bulk loading fires no domain events/emails/notifications), applies any
// pending migrations, then runs the generator and prints a summary. Additive only — never deletes.
// ---------------------------------------------------------------------------------------------

var argMap = ParseArgs(args);

if (argMap.ContainsKey("help") || argMap.ContainsKey("h"))
{
    PrintHelp();
    return 0;
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Connection string precedence: --connection flag > env (ConnectionStrings__Database) > appsettings.
var connectionString = argMap.GetValueOrDefault("connection")
    ?? configuration.GetConnectionString("Database");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "No connection string. Pass --connection \"<value>\", set ConnectionStrings__Database, " +
        "or add ConnectionStrings:Database to appsettings.json.");
    return 1;
}

// Start from appsettings' Seed section, then let CLI flags win.
var options = new SeedOptions();
configuration.GetSection("Seed").Bind(options);
ApplyOverrides(options, argMap);

Console.WriteLine("ProjectHub data seeder");
Console.WriteLine($"  Target      : {DescribeServer(connectionString)}");
Console.WriteLine($"  Seed (RNG)  : {options.Seed}");
Console.WriteLine($"  Users       : {options.Users}");
Console.WriteLine($"  Projects    : {options.Projects}");
Console.WriteLine("  Mode        : additive (existing rows are never modified or deleted)");
Console.WriteLine();

var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connectionString, sqlServer =>
    {
        // Match the API's provider options so Migrate() reads the same history table and rides out
        // transient faults. NO interceptors are added here — that is the whole point of the seeder.
        sqlServer.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
        sqlServer.EnableRetryOnFailure();
    })
    .Options;

try
{
    await using var db = new ApplicationDbContext(dbOptions);

    Console.WriteLine("Applying migrations (if any are pending)...");
    await db.Database.MigrateAsync();

    var seeder = new DataSeeder(db, options);

    var startedAt = Environment.TickCount64;
    var summary = await seeder.RunAsync();
    var elapsed = TimeSpan.FromMilliseconds(Environment.TickCount64 - startedAt);

    Console.WriteLine();
    Console.WriteLine("Done. Rows inserted this run:");
    Console.WriteLine($"  Roles         : {summary.Roles}");
    Console.WriteLine($"  Users         : {summary.Users}");
    Console.WriteLine($"  Projects      : {summary.Projects}");
    Console.WriteLine($"  Members       : {summary.Members}");
    Console.WriteLine($"  Sprints       : {summary.Sprints}");
    Console.WriteLine($"  Tasks         : {summary.Tasks}");
    Console.WriteLine($"  Comments      : {summary.Comments}");
    Console.WriteLine($"  Attachments   : {summary.Attachments}");
    Console.WriteLine($"  Notifications : {summary.Notifications}");
    Console.WriteLine($"  Elapsed       : {elapsed:mm\\:ss}");
    Console.WriteLine();
    Console.WriteLine("Log in as any seeded user with this password:");
    Console.WriteLine($"  password: {options.Password}");
    Console.WriteLine("  known admin login: admin@projecthub.local (created once; reused on re-runs)");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Seeding failed: {ex.Message}");
    Console.Error.WriteLine(ex);
    return 1;
}

// ---------------------------------------------------------------------------------------------
// Local helpers
// ---------------------------------------------------------------------------------------------
static Dictionary<string, string?> ParseArgs(string[] args)
{
    // Accepts "--key value", "--key=value", and bare flags ("--help"). Keys are lower-cased.
    var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i < args.Length; i++)
    {
        var token = args[i];
        if (!token.StartsWith("--", StringComparison.Ordinal) && !token.StartsWith("-", StringComparison.Ordinal))
        {
            continue;
        }

        var key = token.TrimStart('-');
        var eq = key.IndexOf('=');
        if (eq >= 0)
        {
            map[key[..eq]] = key[(eq + 1)..];
            continue;
        }

        // A following non-flag token is this flag's value; otherwise it is a bare boolean flag.
        if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
        {
            map[key] = args[++i];
        }
        else
        {
            map[key] = null;
        }
    }

    return map;
}

static void ApplyOverrides(SeedOptions options, Dictionary<string, string?> argMap)
{
    if (TryGetInt(argMap, "seed", out var seed)) options.Seed = seed;
    if (TryGetInt(argMap, "users", out var users)) options.Users = users;
    if (TryGetInt(argMap, "projects", out var projects)) options.Projects = projects;
    if (argMap.TryGetValue("password", out var password) && !string.IsNullOrWhiteSpace(password))
    {
        options.Password = password;
    }
}

static bool TryGetInt(Dictionary<string, string?> argMap, string key, out int value)
{
    value = 0;
    return argMap.TryGetValue(key, out var raw) && int.TryParse(raw, out value);
}

static string DescribeServer(string connectionString)
{
    // Surface just Server/Database for the console banner; never echo the full string (may carry secrets).
    string? server = null, database = null;
    foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var eq = part.IndexOf('=');
        if (eq < 0) continue;
        var key = part[..eq].Trim();
        var val = part[(eq + 1)..].Trim();
        if (key.Equals("Server", StringComparison.OrdinalIgnoreCase) || key.Equals("Data Source", StringComparison.OrdinalIgnoreCase)) server = val;
        else if (key.Equals("Database", StringComparison.OrdinalIgnoreCase) || key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase)) database = val;
    }

    return $"{server ?? "?"} / {database ?? "?"}";
}

static void PrintHelp()
{
    Console.WriteLine("ProjectHub data seeder — fills the dev database with realistic test data (additive only).");
    Console.WriteLine();
    Console.WriteLine("Usage: dotnet run --project tools/ProjectHub.DataSeeder [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --connection <str>   Override the connection string.");
    Console.WriteLine("  --users <n>          Number of users to create (default from appsettings).");
    Console.WriteLine("  --projects <n>       Number of projects to create.");
    Console.WriteLine("  --seed <n>           RNG seed; fixes the shape of the generated data.");
    Console.WriteLine("  --password <str>     Shared password for all seeded users.");
    Console.WriteLine("  --help               Show this help and exit.");
    Console.WriteLine();
    Console.WriteLine("Connection precedence: --connection > env ConnectionStrings__Database > appsettings.json.");
}
