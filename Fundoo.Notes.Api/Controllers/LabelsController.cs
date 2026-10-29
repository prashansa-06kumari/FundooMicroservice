using Fundoo.Notes.Api.Contracts;
using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Labels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notes.Api.Controllers;

[ApiController]
[Authorize]
public class LabelsController(ILabelService labels) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet("api/labels")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await labels.ListAsync(UserId, ct));

    [HttpPost("api/labels")]
    public async Task<IActionResult> Create(LabelRequest r, CancellationToken ct)
    {
        var outcome = await labels.CreateAsync(UserId, r.Name, ct);
        return outcome.Result switch
        {
            LabelResult.Duplicate => Problem(statusCode: StatusCodes.Status409Conflict, title: "A label with this name already exists."),
            _ => StatusCode(StatusCodes.Status201Created, outcome.Label)
        };
    }

    [HttpPut("api/labels/{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, LabelRequest r, CancellationToken ct)
    {
        var outcome = await labels.RenameAsync(UserId, id, r.Name, ct);
        return outcome.Result switch
        {
            LabelResult.NotFound => NotFound(),
            LabelResult.Duplicate => Problem(statusCode: StatusCodes.Status409Conflict, title: "A label with this name already exists."),
            _ => Ok(outcome.Label)
        };
    }

    [HttpDelete("api/labels/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await labels.DeleteAsync(UserId, id, ct) == LabelResult.NotFound ? NotFound() : NoContent();

    // Leading "/" makes these routes absolute, so they live under /api/notes/...
    [HttpPut("/api/notes/{noteId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> Attach(Guid noteId, Guid labelId, CancellationToken ct) =>
        await labels.AttachAsync(UserId, noteId, labelId, ct) == LabelResult.NotFound ? NotFound() : NoContent();

    [HttpDelete("/api/notes/{noteId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> Detach(Guid noteId, Guid labelId, CancellationToken ct) =>
        await labels.DetachAsync(UserId, noteId, labelId, ct) == LabelResult.NotFound ? NotFound() : NoContent();
}