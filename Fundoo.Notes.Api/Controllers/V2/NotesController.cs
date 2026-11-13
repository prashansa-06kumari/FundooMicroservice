using Asp.Versioning;
using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Notes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fundoo.Notes.Api.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Authorize]
[EnableRateLimiting("notes")]
[Route("api/v{version:apiVersion}/notes")]
public class NotesController(INoteService notes) : ControllerBase
{
    // userId ONLY from the validated token, never from request body/query/route
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var all = (await notes.ListAsync(UserId, NoteView.Active, ct)).ToList();

        return Ok(new
        {
            items = all.Skip((page - 1) * pageSize).Take(pageSize),
            page,
            pageSize,
            total = all.Count
        });
    }
}