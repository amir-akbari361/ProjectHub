namespace ProjectHub.Persistence.Constants;

/// <summary>
/// Central definition of database schema names. Grouping tables into schemas keeps a large model
/// navigable and lets us grant permissions per bounded context later. Constants (not magic strings)
/// mean a rename is one edit, and the compiler catches typos in configuration classes.
/// </summary>
internal static class Schemas
{
    internal const string Identity = "identity";
    internal const string Projects = "projects";
    internal const string Collaboration = "collaboration";
    internal const string Auditing = "auditing";
}
