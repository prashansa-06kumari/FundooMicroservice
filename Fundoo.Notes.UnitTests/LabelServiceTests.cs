using Fundoo.Notes.Application.Labels;
using Fundoo.Notes.Application.Notes;
namespace Fundoo.Notes.UnitTests;

public class LabelServiceTests
{
    private readonly FakeNoteRepository _notes = new();
    private readonly FakeLabelRepository _labels = new();
    private readonly NoteService _noteService;
    private readonly LabelService _service;
    private readonly Guid _asha = Guid.NewGuid();
    private readonly Guid _ravi = Guid.NewGuid();

    public LabelServiceTests()
    {
        _noteService = new NoteService(_notes);
        _service = new LabelService(_labels, _notes);
    }

    [Fact]
    public async Task Create_DuplicateNameIgnoringCase_ForSameUser_IsRejected()
    {
        await _service.CreateAsync(_asha, "Work");

        var result = await _service.CreateAsync(_asha, "work");

        Assert.Equal(LabelResult.Duplicate, result.Result);
    }

    [Fact]
    public async Task Create_SameNameForDifferentUsers_IsAllowed()
    {
        await _service.CreateAsync(_asha, "Work");

        var result = await _service.CreateAsync(_ravi, "Work");

        Assert.Equal(LabelResult.Ok, result.Result);
    }

    [Fact]
    public async Task List_ReturnsOnlyCallersLabels()
    {
        await _service.CreateAsync(_asha, "Work");
        await _service.CreateAsync(_ravi, "Home");

        var list = await _service.ListAsync(_asha);

        Assert.Equal("Work", Assert.Single(list).Name);
    }

    [Fact]
    public async Task Attach_OwnNoteAndOwnLabel_Succeeds()
    {
        var note = await _noteService.CreateAsync(_asha, "N", "", null);
        var label = (await _service.CreateAsync(_asha, "Work")).Label!;

        var result = await _service.AttachAsync(_asha, note.Id, label.Id);

        Assert.Equal(LabelResult.Ok, result);
        Assert.Single(_labels.Links);
    }

    [Fact]
    public async Task Attach_OtherUsersLabel_ReturnsNotFound()
    {
        var note = await _noteService.CreateAsync(_asha, "N", "", null);
        var raviLabel = (await _service.CreateAsync(_ravi, "Home")).Label!;

        var result = await _service.AttachAsync(_asha, note.Id, raviLabel.Id);

        Assert.Equal(LabelResult.NotFound, result);
        Assert.Empty(_labels.Links);
    }

    [Fact]
    public async Task Attach_OtherUsersNote_ReturnsNotFound()
    {
        var ashaNote = await _noteService.CreateAsync(_asha, "N", "", null);
        var raviLabel = (await _service.CreateAsync(_ravi, "Home")).Label!;

        var result = await _service.AttachAsync(_ravi, ashaNote.Id, raviLabel.Id);

        Assert.Equal(LabelResult.NotFound, result);
        Assert.Empty(_labels.Links);
    }

    [Fact]
    public async Task Rename_OtherUsersLabel_ReturnsNotFound()
    {
        var ashaLabel = (await _service.CreateAsync(_asha, "Work")).Label!;

        var result = await _service.RenameAsync(_ravi, ashaLabel.Id, "Hacked");

        Assert.Equal(LabelResult.NotFound, result.Result);
    }
}