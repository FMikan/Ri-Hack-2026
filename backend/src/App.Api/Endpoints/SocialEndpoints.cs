using System.Security.Claims;
using App.Application.Me;
using App.Application.Social;

namespace App.Api.Endpoints;

public static class SocialEndpoints
{
    public static void MapSocialEndpoints(this IEndpointRouteBuilder app)
    {
        var friends = app.MapGroup("/api/friends").WithTags("Social").RequireAuthorization();

        // Requests to me first, then friends (online first), then my own open requests.
        friends.MapGet("/", (ClaimsPrincipal user, SocialService social, CancellationToken ct) => social.ListAsync(user.UserId(), ct));

        // Asking someone who already asked me accepts their request (one friendship either way).
        friends.MapPost("/requests", async (FriendRequest request, ClaimsPrincipal user, SocialService social, CancellationToken ct) =>
            TypedResults.Created((string?)null, await social.RequestAsync(user.UserId(), request.Username, ct)))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        friends.MapPost("/requests/{userId:guid}/accept", (Guid userId, ClaimsPrincipal user, SocialService social, CancellationToken ct) =>
            social.AcceptAsync(user.UserId(), userId, ct))
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Unfriend, withdraw my request or decline theirs.
        friends.MapDelete("/{userId:guid}", async (Guid userId, ClaimsPrincipal user, SocialService social, CancellationToken ct) =>
        {
            await social.RemoveAsync(user.UserId(), userId, ct);
            return TypedResults.NoContent();
        });

        var challenges = app.MapGroup("/api/friend-challenges").WithTags("Social").RequireAuthorization();

        // { friendUserId? }: with it, the challenge shows in that friend's dashboard inbox; the code works for anyone for 10 minutes.
        challenges.MapPost("/", async (CreateFriendChallengeRequest request, ClaimsPrincipal user, SocialService social, CancellationToken ct) =>
            TypedResults.Created((string?)null, await social.CreateChallengeAsync(user.UserId(), request.FriendUserId, ct)))
            .ProducesProblem(StatusCodes.Status409Conflict);

        // The host polls this; matchId appears once a guest joined.
        challenges.MapGet("/{code}", (string code, SocialService social, CancellationToken ct) => social.GetChallengeAsync(code, ct))
            .ProducesProblem(StatusCodes.Status404NotFound);

        challenges.MapPost("/{code}/join", async (string code, ClaimsPrincipal user, SocialService social, CancellationToken ct) =>
            TypedResults.Created((string?)null, await social.JoinChallengeAsync(user.UserId(), code, ct)))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // 403 users/profile-private when the profile is for friends only.
        app.MapGet("/api/users/{username}", (string username, ClaimsPrincipal user, ProfileService profiles, CancellationToken ct) =>
            profiles.GetPublicAsync(user.UserId(), username, ct))
            .WithTags("Social")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
