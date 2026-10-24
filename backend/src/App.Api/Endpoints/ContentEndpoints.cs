using App.Application.Content;

namespace App.Api.Endpoints;

public static class ContentEndpoints
{
    public static void MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/topics").WithTags("Content");

        // Public: the onboarding screen lists topics before the user has an account.
        group.MapGet("/", (ContentService content, CancellationToken ct) => content.ListTopicsAsync(ct));

        group.MapGet("/{slug}/concepts", (string slug, ContentService content, CancellationToken ct) => content.ListConceptsAsync(slug, ct))
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
