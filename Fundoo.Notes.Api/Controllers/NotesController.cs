using System.Security.Claims;
using Fundoo.Notes.Api.Contracts;
using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Common;
using Fundoo.Notes.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notes.Api.Controllers;

[ApiController]
[Route("api/notes")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly INoteRepository _notes;

    public NotesController(INoteRepository notes)
    {
        _notes = notes;
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

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateNoteRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var note = new Note
        {
            UserId = userId.Value,
            Title = request.Title.Trim(),
            Content = request.Content,
            Color = request.Color,
            IsPinned = false
        };

        await _notes.CreateAsync(note, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = note.Id },
            note);
    }

    /// <summary>
    /// Gets notes for the current user with pagination, keyword search, and label filtering.
    /// Supports:
    /// - Expectation 1: Manageable pagination batches (pageNumber, pageSize up to 100).
    /// - Expectation 2 & 3: Finding notes by title or content.
    /// - Expectation 4 & 5: Filtering by label and combined search.
    /// - Expectation 6 & 7: Handling empty results, invalid page/size values, and large batches.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] NoteQueryRequest? queryParams,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        // Expectation 7: Validate navigation and batch size values
        if (queryParams?.PageNumber.HasValue && queryParams.PageNumber.Value < 1)
        {
            return BadRequest(new
            {
                message = "Invalid page number. Page number must be 1 or greater."
            });
        }

        if (queryParams?.PageSize.HasValue && queryParams.PageSize.Value < 1)
        {
            return BadRequest(new
            {
                message = "Invalid page size. Page size must be 1 or greater."
            });
        }

        if (queryParams?.PageSize.HasValue && queryParams.PageSize.Value > 100)
        {
            return BadRequest(new
            {
                message = "Requested page size is unusually large. Maximum allowed page size is 100 notes per page."
            });
        }

        var filter = new NoteFilterParams
        {
            PageNumber = queryParams?.PageNumber ?? 1,
            PageSize = queryParams?.PageSize ?? 10,
            Query = queryParams?.Query,
            Title = queryParams?.Title,
            Content = queryParams?.Content,
            Label = queryParams?.Label,
            LabelId = queryParams?.LabelId,
            Color = queryParams?.Color,
            IsArchived = queryParams?.IsArchived ?? false,
            IsTrashed = queryParams?.IsTrashed ?? false
        };

        var result = await _notes.GetPagedAsync(userId.Value, filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Dedicated search endpoint across title, content, and labels with pagination.
    /// Supports Expectations 2, 3, 5, 6, 7.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? query = null,
        [FromQuery] string? title = null,
        [FromQuery] string? content = null,
        [FromQuery] string? label = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        if (pageNumber < 1)
        {
            return BadRequest(new { message = "Invalid page number. Page number must be 1 or greater." });
        }

        if (pageSize < 1)
        {
            return BadRequest(new { message = "Invalid page size. Page size must be 1 or greater." });
        }

        if (pageSize > 100)
        {
            return BadRequest(new { message = "Requested page size is unusually large. Maximum allowed page size is 100 notes per page." });
        }

        var filter = new NoteFilterParams
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            Query = query,
            Title = title,
            Content = content,
            Label = label,
            IsArchived = false,
            IsTrashed = false
        };

        var result = await _notes.GetPagedAsync(userId.Value, filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Dedicated endpoint to browse notes associated with a specific label name.
    /// Supports Expectations 4 and 5 (filtering by label and optional keyword search within that label).
    /// </summary>
    [HttpGet("by-label/{labelName}")]
    public async Task<IActionResult> GetByLabelName(
        string labelName,
        [FromQuery] string? query = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        if (pageNumber < 1)
        {
            return BadRequest(new { message = "Invalid page number. Page number must be 1 or greater." });
        }

        if (pageSize < 1)
        {
            return BadRequest(new { message = "Invalid page size. Page size must be 1 or greater." });
        }

        if (pageSize > 100)
        {
            return BadRequest(new { message = "Requested page size is unusually large. Maximum allowed page size is 100 notes per page." });
        }

        var filter = new NoteFilterParams
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            Label = labelName,
            Query = query,
            IsArchived = false,
            IsTrashed = false
        };

        var result = await _notes.GetPagedAsync(userId.Value, filter, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var note = await _notes.GetByIdAsync(id, userId.Value, CurrentUserEmail(), ct);

        return note == null ? NotFound() : Ok(note);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateNoteRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var note = new Note
        {
            Id = id,
            UserId = userId.Value,
            Title = request.Title.Trim(),
            Content = request.Content,
            Color = request.Color,
            IsPinned = request.IsPinned
        };

        return await _notes.UpdateAsync(note, CurrentUserEmail(), ct)
            ? NoContent()
            : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        // Repository permanently deletes only if this note is in trash.
        return await _notes.DeleteAsync(id, userId.Value, ct)
            ? NoContent()
            : NotFound(new
            {
                message = "Note not found, or move it to trash first."
            });
    }

    [HttpGet("archived")]
    public async Task<IActionResult> Archived(CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        return Ok(await _notes.GetArchivedAsync(userId.Value, ct));
    }

    [HttpGet("trash")]
    public async Task<IActionResult> Trash(CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        return Ok(await _notes.GetTrashAsync(userId.Value, ct));
    }

    [HttpPut("{id:guid}/archive")]
    public Task<IActionResult> Archive(
        Guid id,
        CancellationToken ct)
        => ChangeArchive(id, true, ct);

    [HttpPut("{id:guid}/unarchive")]
    public Task<IActionResult> Unarchive(
        Guid id,
        CancellationToken ct)
        => ChangeArchive(id, false, ct);

    private async Task<IActionResult> ChangeArchive(
        Guid id,
        bool archived,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        return await _notes.SetArchivedAsync(
            id, userId.Value, archived, ct)
            ? NoContent()
            : NotFound();
    }

    [HttpPut("{id:guid}/trash")]
    public Task<IActionResult> MoveToTrash(
        Guid id,
        CancellationToken ct)
        => ChangeTrash(id, true, ct);

    [HttpPut("{id:guid}/restore")]
    public Task<IActionResult> Restore(
        Guid id,
        CancellationToken ct)
        => ChangeTrash(id, false, ct);

    private async Task<IActionResult> ChangeTrash(
        Guid id,
        bool trashed,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        return await _notes.SetTrashedAsync(
            id, userId.Value, trashed, ct)
            ? NoContent()
            : NotFound();
    }

    // Word file specifies PUT for attaching a label.
    // POST is retained for backward compatibility with the old route.
    [HttpPut("{noteId:guid}/labels/{labelId:guid}")]
    [HttpPost("{noteId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> AddLabel(
        Guid noteId,
        Guid labelId,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        return await _notes.AddLabelAsync(
            noteId, labelId, userId.Value, ct)
            ? NoContent()
            : NotFound(new
            {
                message = "Note or label not found for this user."
            });
    }

    [HttpDelete("{noteId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> RemoveLabel(
        Guid noteId,
        Guid labelId,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        return await _notes.RemoveLabelAsync(
            noteId, labelId, userId.Value, ct)
            ? NoContent()
            : NotFound();
    }
}