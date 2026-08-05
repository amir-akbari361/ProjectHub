using System.ComponentModel.DataAnnotations;

namespace ProjectHub.Infrastructure.Storage;

/// <summary>
/// Strongly-typed settings for the local <see cref="LocalFileStorage"/> adapter, bound from the
/// "FileStorage" configuration section. Mirrors <c>JwtOptions</c>/<c>EmailOptions</c>: a POCO that is
/// bound + validated at startup so a missing/blank root path fails fast at boot instead of on the first
/// upload.
/// </summary>
/// <remarks>
/// WHY <c>internal</c>?
/// Only the Infrastructure composition root and the adapter itself need to see these settings. Keeping the
/// type internal stops the Application/API layers taking an accidental dependency on a storage detail —
/// exactly the encapsulation the other options types enforce.
/// </remarks>
internal sealed class FileStorageOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Filesystem directory under which every uploaded blob is written. Relative paths are resolved against
    /// the app's base directory so the same value works under <c>dotnet run</c> and in a container layout.
    /// Required — a blank root has no safe default (writing to the CWD would scatter user data).
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string RootPath { get; set; } = string.Empty;
}
