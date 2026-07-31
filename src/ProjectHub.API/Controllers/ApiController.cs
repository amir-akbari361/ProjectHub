using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Common;

namespace ProjectHub.API.Controllers;

/// <summary>
/// The shared base for every API controller. Its single responsibility is translating the
/// Application layer's <see cref="Result"/> / <see cref="Result{TValue}"/> railway type into the
/// correct HTTP response. Controllers stay thin: they dispatch a command/query via MediatR and hand
/// the returned <c>Result</c> to <see cref="HandleResult{TValue}"/> — no status-code logic leaks into
/// each action, which keeps the mapping in ONE place (DRY) and every endpoint consistent.
/// </summary>
/// <remarks>
/// WHY A BASE CONTROLLER AND NOT A FILTER/MIDDLEWARE?
/// Our handlers return <c>Result</c> as a VALUE (railway-oriented), they don't throw on expected
/// business failures — so there's no exception for middleware to catch. The translation therefore
/// happens where the value is: right after dispatch. A base method is the simplest, most explicit
/// place for that, and it composes naturally with the typed <c>ActionResult&lt;T&gt;</c> return values.
///
/// Note the DIVISION OF LABOR with <c>GlobalExceptionHandler</c>: unexpected exceptions (a NullRef, a
/// DB outage) still bubble up and are turned into RFC 7807 problem details by that handler. This base
/// only maps EXPECTED, modeled failures (validation, not-found, conflict, auth) carried in a Result.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiController : ControllerBase
{
    /// <summary>
    /// Maps a value-bearing <see cref="Result{TValue}"/> to an HTTP response: on success returns
    /// <c>200 OK</c> with the payload; on failure delegates to <see cref="Problem(Error)"/> which
    /// chooses the status code from the error type. The optional <paramref name="onSuccess"/> lets a
    /// caller override the success shape (e.g., <c>201 Created</c> instead of <c>200 OK</c>).
    /// </summary>
    protected IActionResult HandleResult<TValue>(
        Result<TValue> result,
        Func<TValue, IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess is not null
                ? onSuccess(result.Value)
                : Ok(result.Value);
        }

        return Problem(result.Error);
    }

    /// <summary>
    /// Maps a payload-less <see cref="Result"/> to an HTTP response: on success returns
    /// <c>204 No Content</c> (there's nothing to serialize); on failure delegates to
    /// <see cref="Problem(Error)"/>. Used by commands like logout/confirm-email that succeed without
    /// returning data.
    /// </summary>
    protected IActionResult HandleResult(Result result)
    {
        return result.IsSuccess
            ? NoContent()
            : Problem(result.Error);
    }

    /// <summary>
    /// The single translation from a domain <see cref="Error"/> to an RFC 7807 problem response. The
    /// <see cref="ErrorType"/> discriminant picks the HTTP status; the error code becomes a stable,
    /// machine-readable "type" segment clients can branch on without parsing human text. Keeping this
    /// switch here means adding a new error category is a one-line change in ONE place.
    /// </summary>
    private ObjectResult Problem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        // ControllerBase.Problem builds a ProblemDetails with the standard fields. We surface the
        // domain error code as the "type" URI segment so clients get a stable identifier, and the
        // human message as "detail". This mirrors the shape GlobalExceptionHandler produces for
        // thrown exceptions, so clients see ONE consistent error contract regardless of source.
        return Problem(
            detail: error.Message,
            statusCode: statusCode,
            title: error.Code);
    }
}
