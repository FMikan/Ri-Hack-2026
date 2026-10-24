using App.Application.Notes;

namespace App.Api.Endpoints;

public static class NoteEndpoints
{
    public static void MapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notes").WithTags("Notes");

        group.MapGet("/", (NoteService notes, CancellationToken ct) => notes.ListAsync(ct));

        // Request validation (DataAnnotations on CreateNoteRequest) runs before this handler via AddValidation().
        group.MapPost("/", async (CreateNoteRequest request, NoteService notes, CancellationToken ct) =>
            TypedResults.Created((string?)null, await notes.CreateAsync(request, ct)))
            .ProducesValidationProblem();
    }
}
