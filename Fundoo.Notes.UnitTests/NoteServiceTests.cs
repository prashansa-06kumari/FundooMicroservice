using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Application.Notes;

namespace Fundoo.Notes.UnitTests;

public class NoteServiceTests
{
    private readonly FakeNoteRepository _repo = new();
    private readonly NoteService _service;
    private readonly Guid _asha = Guid.NewGuid();
    private readonly Guid _ravi = Guid.NewGuid();

    public NoteServiceTests()
    {
        _service = new NoteService(_repo);
    }

    [Fact]
    public async Task Create_SetsOwnerFromCaller()
    {
        var note = await _service.CreateAsync(_asha, "Title", "Body", null);

        Assert.Equal(_asha, _repo.Notes.Single().UserId);
        Assert.Equal("Title", note.Title);
    }

    [Fact]
    public async Task Get_OtherUsersNote_ReturnsNull()
    {
        var note = await _service.CreateAsync(_asha, "Private", "Body", null);

        Assert.Null(await _service.GetAsync(_ravi, note.Id));
    }

    [Fact]
    public async Task Update_OtherUsersNote_ReturnsNull_AndDoesNotChangeIt()
    {
        var note = await _service.CreateAsync(_asha, "Original", "Body", null);

        var result = await _service.UpdateAsync(_ravi, note.Id, "Hacked", "Body", null, false);

        Assert.Null(result);
        Assert.Equal("Original", _repo.Notes.Single().Title);
    }

    [Fact]
    public async Task List_ReturnsOnlyCallersNotes()
    {
        await _service.CreateAsync(_asha, "A1", "", null);
        await _service.CreateAsync(_ravi, "R1", "", null);

        var list = await _service.ListAsync(_asha, NoteView.Active);

        Assert.Equal("A1", Assert.Single(list).Title);
    }

    [Fact]
    public async Task Archive_MovesNoteOutOfActive_AndIntoArchived()
    {
        var note = await _service.CreateAsync(_asha, "N", "", null);

        await _service.SetArchivedAsync(_asha, note.Id, true);

        Assert.Empty(await _service.ListAsync(_asha, NoteView.Active));
        Assert.Single(await _service.ListAsync(_asha, NoteView.Archived));
    }

    [Fact]
    public async Task Trash_ThenRestore_MovesNoteBackToActive()
    {
        var note = await _service.CreateAsync(_asha, "N", "", null);

        await _service.SetTrashedAsync(_asha, note.Id, true);
        Assert.Single(await _service.ListAsync(_asha, NoteView.Trashed));

        await _service.SetTrashedAsync(_asha, note.Id, false);
        Assert.Single(await _service.ListAsync(_asha, NoteView.Active));
    }

    [Fact]
    public async Task Delete_NoteThatIsNotTrashed_IsRejected()
    {
        var note = await _service.CreateAsync(_asha, "N", "", null);

        var result = await _service.DeleteAsync(_asha, note.Id);

        Assert.Equal(DeleteResult.NotTrashed, result);
        Assert.Single(_repo.Notes);
    }

    [Fact]
    public async Task Delete_TrashedNote_RemovesIt()
    {
        var note = await _service.CreateAsync(_asha, "N", "", null);
        await _service.SetTrashedAsync(_asha, note.Id, true);

        var result = await _service.DeleteAsync(_asha, note.Id);

        Assert.Equal(DeleteResult.Deleted, result);
        Assert.Empty(_repo.Notes);
    }

    [Fact]
    public async Task Delete_OtherUsersNote_ReturnsNotFound_AndKeepsNote()
    {
        var note = await _service.CreateAsync(_asha, "N", "", null);
        await _service.SetTrashedAsync(_asha, note.Id, true);

        var result = await _service.DeleteAsync(_ravi, note.Id);

        Assert.Equal(DeleteResult.NotFound, result);
        Assert.Single(_repo.Notes);
    }
}