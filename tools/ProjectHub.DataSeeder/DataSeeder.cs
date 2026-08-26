using Microsoft.EntityFrameworkCore;
using ProjectHub.Domain.Entities;
using ProjectHub.Domain.Enums;
using ProjectHub.Domain.ValueObjects;
using ProjectHub.Persistence;

namespace ProjectHub.DataSeeder;

/// <summary>Running tally of rows inserted, printed as the final summary.</summary>
internal sealed class SeedSummary
{
    public int Roles;
    public int Users;
    public int Projects;
    public int Members;
    public int Sprints;
    public int Tasks;
    public int Comments;
    public int Attachments;
    public int Notifications;
}

/// <summary>
/// Generates a realistic dataset and writes it through the real domain factory methods and EF
/// mappings. Every aggregate is built exactly the way the running app builds it (value objects,
/// invariants, audit stamps), then inserted directly — the domain-event publishing interceptor is
/// deliberately NOT registered on this context, so seeding fires no emails/notifications.
///
/// Timestamps are backdated across the past ~12 months from <see cref="_now"/>, and child rows are
/// always stamped at or after their parent, so ordering and pagination look natural. Inserts are
/// batched (per project, and per ~500 notifications) with a change-tracker reset between batches so
/// memory stays flat regardless of volume.
/// </summary>
internal sealed class DataSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly SeedOptions _opt;
    private readonly Random _rng;
    private readonly string _passwordHash;
    private readonly DateTime _now;
    private readonly string _runTag;

    public DataSeeder(ApplicationDbContext db, SeedOptions opt)
    {
        _db = db;
        _opt = opt;
        _rng = new Random(opt.Seed);
        _now = DateTime.UtcNow;

        // Hash the shared password ONCE. BCrypt cost 12 is ~250ms; hashing per user would dominate
        // the whole run. Identical hashes for test users are perfectly fine.
        _passwordHash = BCrypt.Net.BCrypt.HashPassword(opt.Password, 12);

        // The RNG is deterministic, so a second run would regenerate identical emails and collide
        // with the unique index. This per-run tag keeps repeated (additive) runs conflict-free.
        _runTag = Guid.NewGuid().ToString("N")[..6];
    }

    public async Task<SeedSummary> RunAsync(CancellationToken ct = default)
    {
        var summary = new SeedSummary();

        await EnsureRolesAsync(summary, ct);
        var users = await SeedUsersAsync(summary, ct);
        await SeedProjectsAsync(users, summary, ct);
        await SeedNotificationsAsync(users, summary, ct);

        return summary;
    }

    // ---------------------------------------------------------------------------------------------
    // Roles — the three well-known roles the domain exposes as fixed-GUID statics. They are not
    // seeded by any migration, so we insert any that are missing (idempotent across runs).
    // ---------------------------------------------------------------------------------------------
    private async Task EnsureRolesAsync(SeedSummary summary, CancellationToken ct)
    {
        var known = new[] { Role.Admin, Role.Manager, Role.Member };
        var existingIds = await _db.Roles.Select(role => role.Id).ToListAsync(ct);

        foreach (var role in known.Where(role => !existingIds.Contains(role.Id)))
        {
            await _db.Roles.AddAsync(role, ct);

            // The static Role instances carry CreatedAtUtc = default(DateTime); stamp a real value
            // through EF metadata (the C# setter is private) — the same technique the
            // SoftDeleteInterceptor uses to write audit columns.
            _db.Entry(role).Property(entity => entity.CreatedAtUtc).CurrentValue = _now.AddDays(-400);
            summary.Roles++;
        }

        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
    }

    // ---------------------------------------------------------------------------------------------
    // Users — one stable admin login that survives re-runs, plus N randomized users. Each user gets
    // Member, and probabilistically Manager/Admin. Most confirm their email.
    // ---------------------------------------------------------------------------------------------
    private async Task<List<User>> SeedUsersAsync(SeedSummary summary, CancellationToken ct)
    {
        var users = new List<User>(_opt.Users + 1);

        // A well-known account for convenient login, created only if it does not already exist.
        var adminEmail = Email.Create("admin@projecthub.local");
        if (!await _db.Users.AnyAsync(user => user.Email == adminEmail, ct))
        {
            var adminCreated = _now.AddDays(-400);
            var admin = User.Register(adminEmail, "Ada", "Admin", _passwordHash, adminCreated);
            admin.AssignRole(Role.Admin, adminCreated);
            admin.AssignRole(Role.Manager, adminCreated);
            admin.AssignRole(Role.Member, adminCreated);
            admin.ConfirmEmail(adminCreated.AddMinutes(1));
            users.Add(admin);
        }

        for (var i = 0; i < _opt.Users; i++)
        {
            var first = Pick(FirstNames);
            var last = Pick(LastNames);

            // The ".{i}." guarantees intra-run uniqueness; the run tag guarantees cross-run uniqueness.
            var email = Email.Create($"{first}.{last}.{i}.{_runTag}@example.com");
            var createdAt = Between(_now.AddDays(-365), _now.AddDays(-1));

            var user = User.Register(email, first, last, _passwordHash, createdAt);
            user.AssignRole(Role.Member, createdAt);
            if (Chance(0.30)) user.AssignRole(Role.Manager, createdAt);
            if (Chance(0.08)) user.AssignRole(Role.Admin, createdAt);
            if (Chance(0.85)) user.ConfirmEmail(Between(createdAt, _now));

            users.Add(user);
        }

        await _db.Users.AddRangeAsync(users, ct);
        await _db.SaveChangesAsync(ct);
        summary.Users += users.Count;

        // Detach: we only need the in-memory Ids/CreatedAtUtc from here on, not tracked state.
        _db.ChangeTracker.Clear();
        return users;
    }

    // ---------------------------------------------------------------------------------------------
    // Projects — the bulk of the data. Each project: distinct members (one Owner), sprints in mixed
    // states, tasks with assignees/status/priority/due-dates, comments, and some attachments.
    // Persisted in three FK-safe steps so referenced rows always exist before their dependents.
    // ---------------------------------------------------------------------------------------------
    private async Task SeedProjectsAsync(List<User> users, SeedSummary summary, CancellationToken ct)
    {
        for (var p = 0; p < _opt.Projects; p++)
        {
            var projectCreatedAt = Between(_now.AddDays(-365), _now.AddDays(-2));
            var name = ProjectName.Create(BuildProjectName());
            var description = Chance(0.8) ? Pick(ProjectDescriptions) : null;

            var memberCount = Math.Min(NextInclusive(_opt.MinMembersPerProject, _opt.MaxMembersPerProject), users.Count);
            var members = PickDistinct(users, memberCount);
            var owner = members[0];
            var memberIds = members.Select(member => member.Id).ToList();

            var project = Project.Create(name, description, projectCreatedAt, owner.Id);
            project.AddMember(owner.Id, ProjectRole.Owner, projectCreatedAt, owner.Id);
            for (var m = 1; m < members.Count; m++)
            {
                project.AddMember(members[m].Id, PickMemberRole(), Between(projectCreatedAt, _now), owner.Id);
            }

            // Step 1: project (+ owned members) — establishes the ProjectId FK target.
            await _db.Projects.AddAsync(project, ct);
            await _db.SaveChangesAsync(ct);
            summary.Projects++;
            summary.Members += members.Count;

            // Step 2: sprints + tasks (both FK to the now-persisted project; tasks FK to existing users).
            var sprints = BuildSprints(project.Id, projectCreatedAt);
            var tasks = BuildTasks(project.Id, memberIds, projectCreatedAt);
            await _db.Sprints.AddRangeAsync(sprints, ct);
            await _db.ProjectTasks.AddRangeAsync(tasks.Select(entry => entry.Task), ct);
            await _db.SaveChangesAsync(ct);
            summary.Sprints += sprints.Count;
            summary.Tasks += tasks.Count;

            // Step 3: comments + attachments (FK to the now-persisted tasks).
            var (comments, attachments) = BuildTaskChildren(tasks, memberIds);
            await _db.Comments.AddRangeAsync(comments, ct);
            await _db.Attachments.AddRangeAsync(attachments, ct);
            await _db.SaveChangesAsync(ct);
            summary.Comments += comments.Count;
            summary.Attachments += attachments.Count;

            // Archive a fraction — done last, since an archived project rejects further mutation.
            if (Chance(_opt.ArchivedProjectRatio))
            {
                project.Archive(Between(projectCreatedAt, _now), owner.Id);
                await _db.SaveChangesAsync(ct);
            }

            _db.ChangeTracker.Clear();

            if ((p + 1) % 10 == 0 || p + 1 == _opt.Projects)
            {
                Console.WriteLine($"  ... {p + 1}/{_opt.Projects} projects");
            }
        }
    }

    private List<Sprint> BuildSprints(Guid projectId, DateTime projectCreatedAt)
    {
        var sprints = new List<Sprint>();
        var count = NextInclusive(_opt.MinSprintsPerProject, _opt.MaxSprintsPerProject);

        for (var s = 0; s < count; s++)
        {
            // Some sprints may be scheduled to start slightly in the future (still Planned).
            var start = Between(projectCreatedAt, _now.AddDays(30));
            var schedule = DateRange.Create(start, start.AddDays(NextInclusive(7, 21)));
            var createdAt = Between(projectCreatedAt, start < _now ? start : _now);

            var sprint = Sprint.Create(projectId, $"Sprint {s + 1}", schedule, createdAt);

            // Only advance status for sprints whose start is already in the past.
            if (start < _now)
            {
                var roll = _rng.NextDouble();
                if (roll < 0.4)
                {
                    var startedAt = Between(start, _now);
                    sprint.Start(startedAt);
                    sprint.Complete(Between(startedAt, _now));
                }
                else if (roll < 0.7)
                {
                    sprint.Start(Between(start, _now));
                }
            }

            sprints.Add(sprint);
        }

        return sprints;
    }

    private List<(ProjectTask Task, DateTime CreatedAt)> BuildTasks(Guid projectId, List<Guid> memberIds, DateTime projectCreatedAt)
    {
        var tasks = new List<(ProjectTask, DateTime)>();
        var count = NextInclusive(_opt.MinTasksPerProject, _opt.MaxTasksPerProject);

        for (var t = 0; t < count; t++)
        {
            var createdAt = Between(projectCreatedAt, _now);
            var title = TaskTitle.Create(BuildTaskTitle());
            var description = Chance(0.7) ? Pick(TaskDescriptions) : null;

            var task = ProjectTask.Create(projectId, title, description, PickPriority(), createdAt);

            if (Chance(0.8))
            {
                task.Assign(Pick(memberIds), Between(createdAt, _now));
            }

            // Walk the status ladder (Todo -> ... -> target) with monotonically increasing stamps.
            var target = PickTaskStatus();
            var stamp = createdAt;
            for (var status = 2; status <= (int)target; status++)
            {
                stamp = Between(stamp, _now);
                task.ChangeStatus((ProjectTaskStatus)status, stamp);
            }

            // Due date must be strictly after the utcNow we pass — anchor it to the creation instant.
            if (Chance(0.6))
            {
                task.SetDueDate(createdAt.AddDays(NextInclusive(1, 60)), createdAt);
            }

            tasks.Add((task, createdAt));
        }

        return tasks;
    }

    private (List<Comment> Comments, List<Attachment> Attachments) BuildTaskChildren(
        List<(ProjectTask Task, DateTime CreatedAt)> tasks,
        List<Guid> memberIds)
    {
        var comments = new List<Comment>();
        var attachments = new List<Attachment>();

        foreach (var (task, taskCreatedAt) in tasks)
        {
            var commentCount = NextInclusive(0, _opt.MaxCommentsPerTask);
            for (var c = 0; c < commentCount; c++)
            {
                var authorId = Pick(memberIds);
                var commentedAt = Between(taskCreatedAt, _now);
                var comment = Comment.Create(task.Id, authorId, CommentBody.Create(Pick(CommentSnippets)), commentedAt);

                if (Chance(0.15))
                {
                    comment.Edit(CommentBody.Create(Pick(CommentSnippets)), authorId, Between(commentedAt, _now));
                }

                comments.Add(comment);
            }

            if (Chance(_opt.AttachmentTaskRatio))
            {
                var attachmentCount = NextInclusive(1, 2);
                for (var a = 0; a < attachmentCount; a++)
                {
                    var (fileName, contentType) = Pick(Files);
                    var file = FileMetadata.Create(fileName, contentType, NextInclusive(4 * 1024, 5 * 1024 * 1024));
                    var storagePath = $"attachments/{task.Id:N}/{fileName}";
                    attachments.Add(Attachment.Upload(task.Id, Pick(memberIds), file, storagePath, Between(taskCreatedAt, _now)));
                }
            }
        }

        return (comments, attachments);
    }

    // ---------------------------------------------------------------------------------------------
    // Notifications — a handful per user, some already read. Flushed in ~500-row batches.
    // ---------------------------------------------------------------------------------------------
    private async Task SeedNotificationsAsync(List<User> users, SeedSummary summary, CancellationToken ct)
    {
        var batch = new List<Notification>(600);

        foreach (var user in users)
        {
            var count = NextInclusive(_opt.MinNotificationsPerUser, _opt.MaxNotificationsPerUser);
            for (var n = 0; n < count; n++)
            {
                var type = PickNotificationType();
                var createdAt = Between(user.CreatedAtUtc, _now);
                var notification = Notification.Create(user.Id, type, NotificationMessage(type), createdAt);

                if (Chance(0.4))
                {
                    notification.MarkAsRead(Between(createdAt, _now));
                }

                batch.Add(notification);
            }

            if (batch.Count >= 500)
            {
                summary.Notifications += await FlushNotificationsAsync(batch, ct);
            }
        }

        summary.Notifications += await FlushNotificationsAsync(batch, ct);
    }

    private async Task<int> FlushNotificationsAsync(List<Notification> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        var count = batch.Count;
        await _db.Notifications.AddRangeAsync(batch, ct);
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
        batch.Clear();
        return count;
    }

    // ---------------------------------------------------------------------------------------------
    // Randomization helpers
    // ---------------------------------------------------------------------------------------------
    private T Pick<T>(IReadOnlyList<T> items) => items[_rng.Next(items.Count)];

    private bool Chance(double probability) => _rng.NextDouble() < probability;

    private int NextInclusive(int min, int max) => max <= min ? min : _rng.Next(min, max + 1);

    /// <summary>A uniformly random UTC instant in [start, end]; returns start if the span is empty.</summary>
    private DateTime Between(DateTime start, DateTime end)
    {
        if (end <= start)
        {
            return start;
        }

        return start.AddSeconds(_rng.NextDouble() * (end - start).TotalSeconds);
    }

    /// <summary>Partial Fisher–Yates: distinct sample of <paramref name="count"/> items.</summary>
    private List<T> PickDistinct<T>(IReadOnlyList<T> source, int count)
    {
        count = Math.Min(count, source.Count);
        var pool = source.ToList();
        for (var i = 0; i < count; i++)
        {
            var j = _rng.Next(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool.GetRange(0, count);
    }

    private ProjectRole PickMemberRole()
    {
        var roll = _rng.NextDouble();
        return roll switch
        {
            < 0.20 => ProjectRole.Viewer,
            < 0.65 => ProjectRole.Contributor,
            _ => ProjectRole.Maintainer,
        };
    }

    private TaskPriority PickPriority()
    {
        var roll = _rng.NextDouble();
        return roll switch
        {
            < 0.25 => TaskPriority.Low,
            < 0.65 => TaskPriority.Medium,
            < 0.90 => TaskPriority.High,
            _ => TaskPriority.Critical,
        };
    }

    private ProjectTaskStatus PickTaskStatus()
    {
        var roll = _rng.NextDouble();
        return roll switch
        {
            < 0.30 => ProjectTaskStatus.Todo,
            < 0.60 => ProjectTaskStatus.InProgress,
            < 0.75 => ProjectTaskStatus.InReview,
            _ => ProjectTaskStatus.Done,
        };
    }

    private NotificationType PickNotificationType()
    {
        var values = Enum.GetValues<NotificationType>();
        return values[_rng.Next(values.Length)];
    }

    private static string NotificationMessage(NotificationType type) => type switch
    {
        NotificationType.TaskAssigned => "You were assigned to a task.",
        NotificationType.TaskStatusChanged => "A task you're watching changed status.",
        NotificationType.CommentAdded => "There's a new comment on a task you follow.",
        NotificationType.MentionedInComment => "You were mentioned in a comment.",
        NotificationType.ProjectInvitation => "You've been invited to join a project.",
        NotificationType.SprintStarted => "A new sprint has started.",
        _ => "You have a new notification.",
    };

    private string BuildProjectName() => $"{Pick(ProjectCodenames)} {Pick(ProjectDomains)} {Pick(ProjectSuffixes)}";

    private string BuildTaskTitle() => $"{Pick(TaskVerbs)} {Pick(TaskAreas)}";

    // ---------------------------------------------------------------------------------------------
    // Curated word banks — enough variety to look real without an external fake-data dependency.
    // ---------------------------------------------------------------------------------------------
    private static readonly string[] FirstNames =
    {
        "James", "Olivia", "Liam", "Emma", "Noah", "Ava", "Ethan", "Sophia", "Mason", "Isabella",
        "Lucas", "Mia", "Amir", "Layla", "Chen", "Yuki", "Priya", "Diego", "Fatima", "Omar",
        "Hannah", "Leo", "Nina", "Kofi", "Sara", "Tom", "Grace", "Ivan", "Zara", "Marcus",
    };

    private static readonly string[] LastNames =
    {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
        "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Nguyen", "Patel", "Kim", "Chen", "Khan",
        "Okafor", "Rossi", "Novak", "Haddad", "Silva", "Costa", "Dubois", "Ivanov", "Tanaka", "Adebayo",
    };

    private static readonly string[] ProjectCodenames =
    {
        "Phoenix", "Titan", "Nebula", "Atlas", "Orion", "Falcon", "Quantum", "Vertex", "Aurora", "Zephyr",
        "Helios", "Nimbus", "Onyx", "Pioneer", "Catalyst", "Horizon", "Summit", "Beacon", "Apex", "Cobalt",
    };

    private static readonly string[] ProjectDomains =
    {
        "Billing", "Analytics", "Onboarding", "Payments", "Inventory", "Messaging", "Identity", "Reporting",
        "Logistics", "Marketplace", "Compliance", "Scheduling", "Notifications", "Search", "Fulfilment",
    };

    private static readonly string[] ProjectSuffixes =
    {
        "Platform", "Service", "Revamp", "Migration", "Initiative", "Overhaul", "Modernization", "Rollout",
    };

    private static readonly string[] ProjectDescriptions =
    {
        "Consolidate legacy workflows into a single, observable service.",
        "Deliver a customer-facing dashboard with real-time metrics.",
        "Migrate the monolith module to an independently deployable service.",
        "Improve reliability and cut p99 latency across the critical path.",
        "Replace the third-party vendor integration with an in-house solution.",
        "Ship a self-serve onboarding flow to reduce time-to-value.",
        "Harden the system for SOC 2 and add end-to-end audit logging.",
        "Re-architect the data pipeline for near-real-time ingestion.",
        "Unify the design system and rebuild the core screens.",
        "Introduce role-based access control across all internal tools.",
    };

    private static readonly string[] TaskVerbs =
    {
        "Implement", "Fix", "Refactor", "Design", "Investigate", "Optimize", "Document", "Add",
        "Remove", "Migrate", "Test", "Review", "Configure", "Automate", "Harden",
    };

    private static readonly string[] TaskAreas =
    {
        "the login rate limiter", "the OAuth token refresh", "the pagination bug on the board",
        "the search indexing job", "the CSV export", "the retry policy for webhooks",
        "the caching layer", "the database migration script", "the email templates",
        "the file upload validation", "the dashboard charts", "the audit-log viewer",
        "the notification preferences", "the role permission checks", "the API error responses",
        "the background cleanup worker", "the connection pool settings", "the health checks",
        "the feature-flag rollout", "the timezone handling",
    };

    private static readonly string[] TaskDescriptions =
    {
        "Reproduce the issue, add a regression test, then fix the root cause.",
        "Break the work into small PRs and land behind a feature flag.",
        "Coordinate with the platform team before touching shared infra.",
        "Measure before and after; attach the benchmark to this task.",
        "Follow the existing pattern in the neighboring module.",
        "Needs product sign-off on the copy before shipping.",
        "Watch out for the N+1 query flagged in the last review.",
        "Ensure backward compatibility for existing API clients.",
        "Add telemetry so we can confirm the fix in production.",
        "Update the docs and changelog as part of this change.",
    };

    private static readonly string[] CommentSnippets =
    {
        "Taking a look at this now.", "Can you add a test for the edge case?",
        "LGTM once CI is green.", "I think this is a dupe of an earlier ticket.",
        "Blocked on the API change landing first.", "Nice work — much cleaner than before.",
        "Let's discuss this in standup tomorrow.", "Pushed a fix, please re-review.",
        "This is higher priority than we thought.", "Reproduced on staging, logs attached.",
        "Do we have metrics on how often this happens?", "Deferring to next sprint.",
        "Left a couple of inline comments.", "Merged and deployed to dev.",
        "Confirmed the fix works end to end.", "Rolling back — this caused a regression.",
        "Good catch, updating the PR.", "Assigning to myself for now.",
    };

    private static readonly (string FileName, string ContentType)[] Files =
    {
        ("design-spec.pdf", "application/pdf"),
        ("screenshot.png", "image/png"),
        ("error-log.txt", "text/plain"),
        ("architecture.png", "image/png"),
        ("export.csv", "text/csv"),
        ("proposal.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
        ("metrics.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        ("mockup.jpg", "image/jpeg"),
    };
}
