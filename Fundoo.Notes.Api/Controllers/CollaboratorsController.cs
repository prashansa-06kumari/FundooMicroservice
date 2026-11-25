using System.Security.Claims;
using Fundoo.Notes.Api.Contracts;
using Fundoo.Notes.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notes.Api.Controllers;

[ApiController]
[Authorize]
public class CollaboratorsController : ControllerBase
{
    private readonly ICollaboratorRepository _collaboratorRepository;

    public CollaboratorsController(ICollaboratorRepository collaboratorRepository)
    {
        _collaboratorRepository = collaboratorRepository;
    }

    private Guid? CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub")
                    ?? User.FindFirstValue("userId");

        return Guid.TryParse(value, out var id) ? id : null;
    }

    private string? CurrentUserEmail()
    {
        return User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email");
    }

    /// <summary>
    /// Adds a collaborator to a note by email.
    /// </summary>
    [HttpPost("api/notes/{noteId:guid}/collaborators")]
    public async Task<IActionResult> AddCollaborator(
        Guid noteId,
        [FromBody] AddCollaboratorRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var currentUserEmail = CurrentUserEmail();
        if (!string.IsNullOrWhiteSpace(currentUserEmail) &&
            string.Equals(currentUserEmail.Trim(), request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "You cannot add yourself as a collaborator on your own note." });
        }

        try
        {
            var collaborator = await _collaboratorRepository.AddCollaboratorAsync(
                noteId, request.Email, userId.Value, ct);

            return Created($"/api/notes/{noteId}/collaborators/{collaborator.Id}", collaborator);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Standalone route: Add a collaborator.
    /// </summary>
    [HttpPost("api/collaborator")]
    public async Task<IActionResult> AddCollaboratorStandalone(
        [FromBody] CreateCollaboratorStandaloneRequest request,
        CancellationToken ct)
    {
        return await AddCollaborator(request.NoteId, new AddCollaboratorRequest { Email = request.Email }, ct);
    }

    /// <summary>
    /// Gets all collaborators for a note.
    /// </summary>
    [HttpGet("api/notes/{noteId:guid}/collaborators")]
    [HttpGet("api/collaborator/{noteId:guid}")]
    public async Task<IActionResult> GetCollaborators(
        Guid noteId,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var collaborators = await _collaboratorRepository.GetCollaboratorsByNoteIdAsync(
            noteId, userId.Value, ct);

        return Ok(collaborators);
    }

    /// <summary>
    /// Removes a collaborator by ID.
    /// </summary>
    [HttpDelete("api/notes/{noteId:guid}/collaborators/{collaboratorId:guid}")]
    [HttpDelete("api/collaborator/{collaboratorId:guid}")]
    public async Task<IActionResult> RemoveCollaborator(
        Guid? noteId,
        Guid collaboratorId,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        // If noteId is not in route (e.g. /api/collaborator/{id}), pass Guid.Empty or resolve
        var resolvedNoteId = noteId ?? Guid.Empty;

        var removed = await _collaboratorRepository.RemoveCollaboratorAsync(
            resolvedNoteId, collaboratorId, userId.Value, ct);

        return removed ? NoContent() : NotFound(new { message = "Collaborator not found or you are not authorized to remove them." });
    }

    /// <summary>
    /// Removes a collaborator by email.
    /// </summary>
    [HttpDelete("api/notes/{noteId:guid}/collaborators/email/{email}")]
    public async Task<IActionResult> RemoveCollaboratorByEmail(
        Guid noteId,
        string email,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var removed = await _collaboratorRepository.RemoveCollaboratorByEmailAsync(
            noteId, email, userId.Value, ct);

        return removed ? NoContent() : NotFound(new { message = "Collaborator not found or you are not authorized to remove them." });
    }

    /// <summary>
    /// Gets all notes shared with the currently logged-in user.
    /// </summary>
    [HttpGet("api/notes/collaborations")]
    [HttpGet("api/collaborator/notes")]
    public async Task<IActionResult> GetCollaboratedNotes(CancellationToken ct)
    {
        var userId = CurrentUserId();
        var userEmail = CurrentUserEmail();
        if (userId == null || string.IsNullOrWhiteSpace(userEmail))
        {
            return BadRequest(new { message = "User identity or email not found in token." });
        }

        var notes = await _collaboratorRepository.GetCollaboratedNotesAsync(userId.Value, userEmail, ct);
        return Ok(notes);
    }
}
