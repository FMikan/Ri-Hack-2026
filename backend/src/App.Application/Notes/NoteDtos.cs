using System.ComponentModel.DataAnnotations;
using App.Domain;

namespace App.Application.Notes;

public record NoteDto(Guid Id, string Text, DateTimeOffset CreatedAt);

public record CreateNoteRequest
{
    [Required]
    [StringLength(Note.MaxTextLength)]
    public required string Text { get; init; }
}
