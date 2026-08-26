namespace ProjectHub.DataSeeder;

/// <summary>
/// Tunable knobs for a seeding run. Bound from the "Seed" section of appsettings.json, then
/// optionally overridden by CLI flags (see <c>Program</c>). All counts are per the entity named;
/// ranges are inclusive on both ends.
/// </summary>
internal sealed class SeedOptions
{
    /// <summary>RNG seed — fixes the *shape* of the data (names, counts, assignments) run-to-run.</summary>
    public int Seed { get; set; } = 20260826;

    /// <summary>Plaintext password shared by every seeded user, hashed once with BCrypt.</summary>
    public string Password { get; set; } = "Password123!";

    public int Users { get; set; } = 200;

    public int Projects { get; set; } = 50;

    public int MinMembersPerProject { get; set; } = 3;

    public int MaxMembersPerProject { get; set; } = 8;

    public int MinSprintsPerProject { get; set; } = 2;

    public int MaxSprintsPerProject { get; set; } = 4;

    public int MinTasksPerProject { get; set; } = 8;

    public int MaxTasksPerProject { get; set; } = 40;

    public int MaxCommentsPerTask { get; set; } = 8;

    public int MinNotificationsPerUser { get; set; } = 3;

    public int MaxNotificationsPerUser { get; set; } = 10;

    /// <summary>Fraction of projects that end up Archived (0..1).</summary>
    public double ArchivedProjectRatio { get; set; } = 0.1;

    /// <summary>Fraction of tasks that receive one or more attachments (0..1).</summary>
    public double AttachmentTaskRatio { get; set; } = 0.2;
}
