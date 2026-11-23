using System.Security.Claims;
using Fundoo.Notes.Api.Contracts;
using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notes.Api.Controllers;

[ApiController]
[Route("api/labels")]
[Authorize]
public class LabelsController : ControllerBase
{
    private readonly ILabelRepository _labels;

    public LabelsController(ILabelRepository labels)
    {
        _labels = labels;
    }

    private Guid? CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub")
                    ?? User.FindFirstValue("userId");

        return Guid.TryParse(value, out var id) ? id : null;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateLabelRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var label = await _labels.CreateAsync(new Label
        {
            UserId = userId.Value,
            Name = request.Name
        }, ct);

        if (label == null)
        {
            return Conflict(new
            {
                message = "This label already exists."
            });
        }

        return Ok(label);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        return Ok(await _labels.GetAllAsync(userId.Value, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var label = await _labels.GetByIdAsync(id, userId.Value, ct);

        return label == null ? NotFound() : Ok(label);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateLabelRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var success = await _labels.UpdateAsync(new Label
        {
            Id = id,
            UserId = userId.Value,
            Name = request.Name
        }, ct);

        return success
            ? NoContent()
            : Conflict(new
            {
                message = "Label not found or name already exists."
            });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        return await _labels.DeleteAsync(id, userId.Value, ct)
            ? NoContent()
            : NotFound();
    }
}