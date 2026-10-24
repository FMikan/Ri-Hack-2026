using App.Domain.Content;
using App.Domain.Matches;
using App.Domain.Matchmaking;
using App.Domain.Progress;
using App.Domain.Users;
using static App.Domain.Tests.Fixtures;

namespace App.Domain.Tests;

public class GhostMatchTests
{
    private static readonly TopicPool Topic = Pool(12, 1, 2);

    // The recorded player sat in seat 0 of the source match; the ghost always plays seat 1.
    private static List<MatchAnswer> Recording(Func<int, bool>? timedOut = null) =>
        Enumerable.Range(0, 10).Select(i => timedOut?.Invoke(i) == true
            ? new MatchAnswer { Seat = 0, Index = i, IsCorrect = false, Fraction = 0, Points = 0, ElapsedMs = 99_999, TimedOut = true, AnsweredAt = Now.AddDays(-3), Label = AnswerLabel.Miss }
            : new MatchAnswer { Seat = 0, Index = i, Payload = """{"value":true}""", IsCorrect = true, Fraction = 1, Points = 70 + i, ElapsedMs = 2_000 + i, AnsweredAt = Now.AddDays(-3), Label = AnswerLabel.Good })
        .ToList();

    private static Match Ghost(List<MatchAnswer> recording)
    {
        var deck = DeckBuilder.Build(Topic.Deck, [Player()], true, (1, 2), Now, new Random(2), degrade: false);
        MatchPlayer[] players =
        [
            new() { Seat = 0, Kind = SeatKind.Human, UserId = Guid.NewGuid(), DisplayRating = 1000 },
            new() { Seat = 1, Kind = SeatKind.Ghost, UserId = Guid.NewGuid(), DisplayRating = 1080, GhostSourceMatchId = Guid.NewGuid() },
        ];
        var match = MatchEngine.Create(MatchKind.Ghost, Guid.NewGuid(), true, (1, 2), 2, players, deck, Topic.ById, Now);
        match.Recording = recording;
        return match;
    }

    [Fact]
    public void Ghost_answers_arrive_at_their_recorded_time_with_their_recorded_points()
    {
        var match = Ghost(Recording());
        var start = Now.AddSeconds(GameRules.LobbySeconds);

        MatchEngine.Advance(match, start);
        MatchEngine.SubmitAnswer(match, 0, 0, Json("""{"value":true}"""), 500, start.AddMilliseconds(500));

        var ghost = match.AnswerOf(1, 0)!;
        Assert.Equal(start.AddMilliseconds(2_000), ghost.AnsweredAt);
        Assert.Equal((70, 2_000, AnswerLabel.Good), (ghost.Points, ghost.ElapsedMs, ghost.Label));
        Assert.Equal(MatchPhase.Question, match.Phase); // the ghost has not "answered" yet
        Assert.Equal(ghost.AnsweredAt, match.PhaseDeadline);
    }

    [Fact]
    public void A_recorded_timeout_arrives_at_the_question_deadline()
    {
        var match = Ghost(Recording(timedOut: i => i == 0));
        var start = Now.AddSeconds(GameRules.LobbySeconds);

        MatchEngine.Advance(match, start);

        var ghost = match.AnswerOf(1, 0)!;
        Assert.True(ghost.TimedOut);
        Assert.Equal(match.PhaseDeadline, ghost.AnsweredAt);
        Assert.Equal(match.Current.TimeLimitMs, ghost.ElapsedMs);
    }

    [Fact]
    public void Ghost_match_changes_only_the_live_player_against_the_recorded_rating()
    {
        var live = new User { Email = "a", Username = "a", UsernameNormalized = "a", PasswordHash = "x", CreatedAt = Now, Rating = 1000, RankedMatches = 3 };
        var recorded = new User { Email = "b", Username = "b", UsernameNormalized = "b", PasswordHash = "x", CreatedAt = Now, Rating = 1500, RankedMatches = 40 };
        var match = new Match { Kind = MatchKind.Ghost, RngSeed = 1, CreatedAt = Now, Phase = MatchPhase.Done, WinnerSeat = 0, ResultReason = MatchResultReason.Score };
        match.Players.Add(new MatchPlayer { Seat = 0, Kind = SeatKind.Human, UserId = Guid.NewGuid(), User = live, DisplayRating = 1000 });
        match.Players.Add(new MatchPlayer { Seat = 1, Kind = SeatKind.Ghost, UserId = Guid.NewGuid(), User = recorded, DisplayRating = 1200 });

        RankedResult.Apply(match);

        Assert.Equal((1024, 4), (live.Rating, live.RankedMatches)); // an upset against the recorded 1200, not today's 1500
        Assert.Equal((1500, 40), (recorded.Rating, recorded.RankedMatches));
        Assert.Equal((1000, 1024), (match.Players[0].RatingBefore, match.Players[0].RatingAfter));
        Assert.Null(match.Players[1].RatingAfter);
    }
}

public class GhostOfferTests
{
    private static readonly Guid Q1 = Guid.NewGuid(), Q2 = Guid.NewGuid();

    private static GhostCandidate Recording(int rating, int daysAgo, params Guid[] questions) =>
        new(Guid.NewGuid(), Guid.NewGuid(), rating, Guid.NewGuid(), Now.AddDays(-daysAgo), questions);

    [Fact]
    public void Freshest_recording_in_range_without_seen_questions_is_offered()
    {
        var old = Recording(1050, daysAgo: 9, Q1);
        var fresh = Recording(1080, daysAgo: 2, Q1);
        var tooFar = Recording(1300, daysAgo: 1, Q1);
        var seen = Recording(1000, daysAgo: 0, Q2);

        var offer = GhostOffers.Find(1000, TimeSpan.FromSeconds(30), [old, fresh, tooFar, seen], new HashSet<Guid> { Q2 });

        Assert.Equal(fresh, offer);
    }

    [Fact]
    public void The_range_widens_with_the_wait_like_the_queue()
    {
        var far = Recording(1300, daysAgo: 1, Q1);

        Assert.Null(GhostOffers.Find(1000, TimeSpan.FromSeconds(30), [far], new HashSet<Guid>()));
        Assert.Equal(far, GhostOffers.Find(1000, TimeSpan.FromSeconds(80), [far], new HashSet<Guid>()));
    }
}

public class RematchTests
{
    private static Match Finished(MatchKind kind = MatchKind.Ranked, DateTimeOffset? opponentSeen = null)
    {
        var match = new Match { Kind = kind, RngSeed = 1, CreatedAt = Now, Phase = MatchPhase.Done, WinnerSeat = 1, ResultReason = MatchResultReason.Score };
        match.Players.Add(new MatchPlayer { Seat = 0, Kind = SeatKind.Human, UserId = Guid.NewGuid(), DisplayRating = 1000, LastSeenAt = Now });
        match.Players.Add(new MatchPlayer { Seat = 1, Kind = kind == MatchKind.Ghost ? SeatKind.Ghost : SeatKind.Human, UserId = Guid.NewGuid(), DisplayRating = 1000, LastSeenAt = opponentSeen ?? Now });
        return match;
    }

    [Fact]
    public void Rematch_needs_a_finished_ranked_match_and_an_opponent_seen_in_the_last_2_minutes()
    {
        Assert.True(Rematch.Available(Finished(), 0, Now));
        Assert.False(Rematch.Available(Finished(opponentSeen: Now.AddMinutes(-3)), 0, Now));
        Assert.False(Rematch.Available(Finished(MatchKind.Ghost), 0, Now));
        Assert.False(Rematch.Available(Finished(MatchKind.Bot), 0, Now));
    }

    [Fact]
    public void Rematch_request_expires_in_20s()
    {
        var match = Finished();

        Rematch.Request(match, 0, Now);

        Assert.Equal(RematchStatus.Pending, Rematch.StatusOf(match, Now.AddSeconds(19)));
        Assert.Equal(RematchStatus.Expired, Rematch.StatusOf(match, Now.AddSeconds(20)));
        Assert.False(Rematch.Available(match, 1, Now.AddSeconds(25)));
    }

    [Fact]
    public void Only_the_other_player_accepts_and_only_while_pending()
    {
        var match = Finished();
        Rematch.Request(match, 0, Now);

        Assert.Equal(ErrorCodes.MatchRematchUnavailable, Assert.Throws<AppException>(() => Rematch.Accept(match, 0, Now)).Code);
        Assert.Equal(ErrorCodes.MatchRematchUnavailable, Assert.Throws<AppException>(() => Rematch.Accept(match, 1, Now.AddSeconds(21))).Code);
        Rematch.Accept(match, 1, Now.AddSeconds(5));
        Assert.Equal(RematchStatus.Accepted, Rematch.StatusOf(match, Now.AddSeconds(30)));
    }
}
