using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Notes;

public class NoteService(IAppDbContext db)
{
    public Task<List<NoteDto>> ListAsync(CancellationToken cancellationToken) =>
        db.Notes
            .OrderByDescending(n => n.CreatedAt)
            .Take(50) // ponytail: fixed page size, add paging when a real list needs it
            .Select(n => new NoteDto(n.Id, n.Text, n.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<NoteDto> CreateAsync(CreateNoteRequest request, CancellationToken cancellationToken)
    {
        var note = new Note { Text = request.Text.Trim() };
        db.Notes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        return new NoteDto(note.Id, note.Text, note.CreatedAt);
    }
}
