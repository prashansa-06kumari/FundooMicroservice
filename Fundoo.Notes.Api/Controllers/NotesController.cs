using Fundoo.Notes.Api.Contracts;
using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Notes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notes.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notes")]
public class NotesController(INoteService notes) : ControllerBase
{
    // userId ONLY from the validated token, never from request body/query/route
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpPost]
    public async Task<IActionResult> Create(CreateNoteRequest r, CancellationToken ct)
    {
        var note = await notes.CreateAsync(UserId, r.Title, r.Content, r.Color, ct);
        return CreatedAtAction(nameof(Get), new { id = note.Id }, note);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await notes.ListAsync(UserId, NoteView.Active, ct));

    [HttpGet("archived")]
    public async Task<IActionResult> Archived(CancellationToken ct) =>
        Ok(await notes.ListAsync(UserId, NoteView.Archived, ct));

    [HttpGet("trash")]
    public async Task<IActionResult> Trash(CancellationToken ct) =>
        Ok(await notes.ListAsync(UserId, NoteView.Trashed, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var note = await notes.GetAsync(UserId, id, ct);
        return note is null ? NotFound() : Ok(note);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateNoteRequest r, CancellationToken ct)
    {
        var note = await notes.UpdateAsync(UserId, id, r.Title, r.Content, r.Color, r.IsPinned, ct);
        return note is null ? NotFound() : Ok(note);
    }

    [HttpPut("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        ToResult(await notes.SetArchivedAsync(UserId, id, true, ct));

    [HttpPut("{id:guid}/unarchive")]
    public async Task<IActionResult> Unarchive(Guid id, CancellationToken ct) =>
        ToResult(await notes.SetArchivedAsync(UserId, id, false, ct));

    [HttpPut("{id:guid}/trash")]
    public async Task<IActionResult> MoveToTrash(Guid id, CancellationToken ct) =>
        ToResult(await notes.SetTrashedAsync(UserId, id, true, ct));

    [HttpPut("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) =>
        ToResult(await notes.SetTrashedAsync(UserId, id, false, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        return await notes.DeleteAsync(UserId, id, ct) switch
        {
            DeleteResult.Deleted => NoContent(),
            DeleteResult.NotTrashed => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Only trashed notes can be permanently deleted."),
            _ => NotFound()
        };
    }

    private IActionResult ToResult(NoteDto? note) => note is null ? NotFound() : Ok(note);
}