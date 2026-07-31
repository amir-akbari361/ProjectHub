using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectHub.Application.Features.Authentication.ConfirmEmail;
using ProjectHub.Application.Features.Authentication.ForgotPassword;
using ProjectHub.Application.Features.Authentication.Login;
using ProjectHub.Application.Features.Authentication.RefreshToken;
using ProjectHub.Application.Features.Authentication.Register;
using ProjectHub.Application.Features.Authentication.ResetPassword;
using ProjectHub.Application.Features.Authentication.RevokeToken;

namespace ProjectHub.API.Controllers;

/// <summary>
/// The HTTP entry point for every authentication flow: registration, login, silent token refresh,
/// logout, email confirmation, and the forgot/reset password pair. Each action is deliberately THIN —
/// it binds the request body to a command and dispatches it through MediatR, then hands the returned
/// <c>Result</c> to the base <see cref="ApiController.HandleResult"/> for HTTP translation. All the
/// real work (validation, hashing, token minting, persistence) lives in the Application handlers, so
/// this controller carries zero business logic. That separation is the whole point of CQRS + MediatR:
/// the transport layer (HTTP) stays a dumb adapter over the use-case layer.
/// </summary>
/// <remarks>
/// WHY <see cref="ISender"/> AND NOT <c>IMediator</c>?
/// <see cref="ISender"/> is the narrow half of MediatR's API — it can only <c>Send</c> a request to its
/// single handler. <c>IMediator</c> also exposes <c>Publish</c> (fan-out to many notification handlers).
/// A controller only ever dispatches ONE command to ONE handler, so depending on the narrower interface
/// follows the Interface Segregation Principle: we ask for the smallest capability we actually need.
///
/// EVERYTHING HERE IS <c>[AllowAnonymous]</c>: by definition, a user calling these endpoints does NOT
/// yet have a valid access token (they're trying to GET one). Requiring auth would be a chicken-and-egg
/// deadlock. Authorization starts to matter on the FEATURE endpoints (projects, tasks) that come next.
/// </remarks>
[AllowAnonymous]
public sealed class AuthController : ApiController
{
    private readonly ISender _sender;

    /// <summary>
    /// The single injected dependency: MediatR's request dispatcher. Constructor injection (not a
    /// service-locator call inside actions) keeps the dependency explicit and the controller testable —
    /// a unit test can hand in a mock <see cref="ISender"/> and assert the right command was sent.
    /// </summary>
    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Registers a new account. On success returns <c>201 Created</c> with the new user's id/email —
    /// <c>201</c> (not <c>200</c>) because a registration CREATES a resource, and REST convention is to
    /// signal that with Created plus a body describing what was made.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        // Override the default 200 → 201 Created. We don't emit a Location header because there is no
        // GET /users/{id} endpoint yet; when one exists, CreatedAtAction would populate it.
        return HandleResult(result, value => StatusCode(StatusCodes.Status201Created, value));
    }

    /// <summary>
    /// Authenticates credentials and issues the access/refresh token pair. Returns <c>200 OK</c> with
    /// both tokens on success, <c>401 Unauthorized</c> when credentials are wrong (mapped from the
    /// handler's Unauthorized error, kept intentionally vague to avoid revealing WHICH field was wrong).
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Exchanges a still-valid refresh token for a NEW token pair (silent re-auth). Returns <c>200 OK</c>
    /// with the new pair; <c>401</c> if the presented refresh token is unknown, expired, or already
    /// revoked (rotation means a replayed old token is dead).
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Logout: revokes the presented refresh token so it can never be exchanged again. Returns
    /// <c>204 No Content</c> — success has no body to return; the session is simply gone.
    /// </summary>
    [HttpPost("revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Revoke(
        RevokeTokenCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Confirms an email address by redeeming the one-time token from the activation link. Returns
    /// <c>204 No Content</c> on success. The token is bound from the query string because this endpoint
    /// is hit by clicking a link in an email — a GET-style navigation carries data in the URL.
    /// </summary>
    [HttpPost("confirm-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ConfirmEmailCommand(token), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Starts the password-reset flow: emails a reset link IF the address maps to an account. Always
    /// returns <c>204 No Content</c> regardless of whether the email exists — telling the caller "no
    /// such account" would turn this into an email-enumeration oracle, so both outcomes look identical.
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Completes the password-reset flow: redeems the one-time token and sets the new password (which
    /// also revokes all existing sessions). Returns <c>204 No Content</c> on success, <c>400</c> if the
    /// token is invalid/expired or the new password fails policy.
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result);
    }
}
