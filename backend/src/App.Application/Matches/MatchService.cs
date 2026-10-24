using System.Text.Json.Nodes;
using App.Application.Content;
using App.Application.Economy;
using App.Application.Me;
using App.Application.Queue;
using App.Domain;
using App.Domain.Content;
using App.Domain.Economy;
using App.Domain.Lessons;
using App.Domain.Matches;
using App.Domain.Progress;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Matches;

// Every command that changes a match runs in a transaction that first locks the match row (SELECT ... FOR UPDATE),
// so two answers, an answer and the tick, or a resign and the tick never interleave (docs §2.3).
public class MatchService(IAppDbContext db, Rewarder rewarder, TimeProvider time)
{
    public async Task<MatchStateDto> StartBotAsync(Guid userId, StartBotMatchRequest request, CancellationToken ct)
    {
        var user = await LoadUserAsync(userId, ct);
        var bot = BotPlayer.Personas[request.Persona];
        var band = DeckBuilder.Band(user.Rating, bot.DisplayRating);
        MatchPlayer[] seats =
        [
            Human(user),
            new() { Seat = 1, Kind = SeatKind.Bot, BotPersona = request.Persona, DisplayRating = bot.DisplayRating },
        ];
        // The bot has no profile, so all targeted slots come from the player's weak concepts (idea §8).
        return await StartAsync(user, MatchKind.Bot, request.TopicId, timed: true, band, seats,
            (pool, player, rng, now) => DeckBuilder.Build(pool, [player], targetingAllowed: true, band, now, rng, degrade: true), ct);
    }

    public async Task<MatchStateDto> StartPracticeAsync(Guid userId, StartPracticeRequest request, CancellationToken ct)
    {
        var user = await LoadUserAsync(userId, ct);
        var band = DeckBuilder.Band(user.Rating, user.Rating);
        return await StartAsync(user, MatchKind.Practice, request.TopicId, request.Timed, band, [Human(user)],
            (pool, player, rng, now) => DeckBuilder.BuildPractice(pool, player, request.ConceptIds ?? [], band, now, rng), ct);
    }

    // Calibration (idea §11): five timed D1–D2 questions on concepts the player has never answered, no rewards.
    public async Task<MatchStateDto> StartCalibrationAsync(Guid userId, StartCalibrationRequest request, CancellationToken ct)
    {
        var user = await LoadUserAsync(userId, ct);
        return await StartAsync(user, MatchKind.Calibration, request.TopicId, timed: true, (1, 2), [Human(user)], (pool, player, rng, _) =>
        {
            var deck = DeckBuilder.BuildCalibration(pool, player, rng);
            return deck.Count > 0 ? deck
                : throw new AppException(ErrorCodes.MatchNothingToCalibrate, "Every concept of this topic already has answers; there is nothing to calibrate.");
        }, ct);
    }

    public async Task<MatchStateDto> GetStateAsync(Guid userId, Guid matchId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var match = await LoadAsync(matchId, ct);
        var me = Seat(match, userId);
        // Presence (disconnect detection) and the moment this seat first received the open question (answer timing).
        me.LastSeenAt = now;
        if (match.Phase == MatchPhase.Question && me.DeliveredIndex != match.CurrentIndex)
        {
            me.DeliveredIndex = match.CurrentIndex;
            me.DeliveredAt = now;
        }
        await db.SaveChangesAsync(ct);
        return await MapAsync(match, me.Seat, now, ct);
    }

    public Task<MatchStateDto> SubmitAnswerAsync(Guid userId, Guid matchId, SubmitAnswerRequest request, CancellationToken ct) =>
        CommandAsync(userId, matchId, (match, seat, now) =>
            [MatchEngine.SubmitAnswer(match, seat, request.QuestionIndex, request.Payload, request.ClientElapsedMs, now)], ct);

    public async Task<MatchStateDto> BanAsync(Guid userId, Guid matchId, Guid topicId, CancellationToken ct) =>
        await CommandAsync(userId, matchId, (match, seat, now) =>
        {
            MatchEngine.Ban(match, seat, topicId, now);
            return [];
        }, ct);

    // Both players accepted: the match begins with the coin flip; the veto pool is the union of their active topics.
    // The ghost duel (idea §4): the recording's topic, deck and band, no veto; seat 1 replays the recorded player.
    public async Task<MatchStateDto> StartGhostAsync(Guid userId, Guid sourceMatchId, int recordedSeat, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var user = await LoadUserAsync(userId, ct);
        // Tracked on purpose: the source's questions become navigations of the new match.
        var source = await db.Matches
            .Include(m => m.Players).ThenInclude(p => p.User)
            .Include(m => m.Questions).ThenInclude(q => q.Question)
            .AsSplitQuery()
            .FirstAsync(m => m.Id == sourceMatchId, ct);
        var recorded = source.Players.First(p => p.Seat == recordedSeat);
        MatchPlayer[] seats =
        [
            Human(user),
            new() { Seat = 1, Kind = SeatKind.Ghost, UserId = recorded.UserId, User = recorded.User, DisplayRating = recorded.DisplayRating, GhostSourceMatchId = source.Id },
        ];
        var deck = source.Questions.OrderBy(q => q.Index).Select(q => new DeckSlot(new DeckQuestion(q.QuestionId, q.ConceptId, q.Type, q.Difficulty), q.Slot)).ToList();
        var match = MatchEngine.Create(MatchKind.Ghost, source.TopicId!.Value, timed: true, (source.BandMin, source.BandMax), Random.Shared.Next(),
            seats, deck, source.Questions.ToDictionary(q => q.QuestionId, q => q.Question!), now);
        return await SaveNewAsync(userId, match, now, ct);
    }

    public async Task<RematchDto> GetRematchAsync(Guid userId, Guid matchId, CancellationToken ct)
    {
        var match = await LoadAsync(matchId, ct);
        Seat(match, userId);
        return RematchOf(match, time.GetUtcNow());
    }

    // Asking twice is harmless; asking while the other player's request waits accepts it.
    public async Task<RematchDto> RequestRematchAsync(Guid userId, Guid matchId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var match = await LockedAsync(matchId, ct);
        var seat = Seat(match, userId).Seat;
        if (Rematch.StatusOf(match, now) == RematchStatus.Pending && match.RematchRequestedBySeat != seat)
            await StartRematchAsync(match, seat, now, ct);
        else if (!(Rematch.StatusOf(match, now) == RematchStatus.Pending && match.RematchRequestedBySeat == seat))
            Rematch.Request(match, seat, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return RematchOf(match, now);
    }

    public async Task<MatchStateDto> AcceptRematchAsync(Guid userId, Guid matchId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        Guid rematchId;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var match = await LockedAsync(matchId, ct);
            rematchId = await StartRematchAsync(match, Seat(match, userId).Seat, now, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        var rematch = await LoadAsync(rematchId, ct);
        return await MapAsync(rematch, Seat(rematch, userId).Seat, now, ct);
    }

    // Skips the queue (and the same-opponent rule) and goes straight to a new veto of the same kind (ranked or friend).
    private async Task<Guid> StartRematchAsync(Match match, int seat, DateTimeOffset now, CancellationToken ct)
    {
        Rematch.Accept(match, seat, now);
        foreach (var player in match.Players) await db.LeaveQueueAsync(player.UserId!.Value, ct);
        match.RematchMatchId = await CreateDuelAsync(match.Players[0].UserId!.Value, match.Players[1].UserId!.Value, match.Kind, now, ct, rematchOf: match.Id);
        return match.RematchMatchId.Value;
    }

    private static RematchDto RematchOf(Match match, DateTimeOffset now) => new(
        Rematch.StatusOf(match, now), match.RematchRequestedBySeat, match.RematchRequestedAt?.AddSeconds(GameRules.RematchSeconds), match.RematchMatchId);

    private async Task<Match> LockedAsync(Guid matchId, CancellationToken ct)
    {
        var exists = await db.Database.SqlQuery<int>($"select 1 as \"Value\" from matches where id = {matchId} for update").ToListAsync(ct);
        if (exists.Count == 0) throw MatchNotFound();
        return await LoadAsync(matchId, ct);
    }

    // A ranked (queue, rematch) or friend (challenge code) duel: both players claim their active-match slot; the veto starts.
    public async Task<Guid> CreateDuelAsync(Guid userA, Guid userB, MatchKind kind, DateTimeOffset now, CancellationToken ct, Guid? rematchOf = null)
    {
        var users = await db.Users.Include(u => u.Topics).Where(u => u.Id == userA || u.Id == userB).ToListAsync(ct);
        var playable = (await db.Topics.Where(t => t.IsActive).Select(t => t.Id).ToListAsync(ct)).ToHashSet();
        var (a, b) = (users.First(u => u.Id == userA), users.First(u => u.Id == userB));
        List<Guid> Topics(User user) => [.. user.Topics.Select(t => t.TopicId).Where(playable.Contains)];

        var seed = Random.Shared.Next();
        var match = MatchEngine.CreateRanked([Human(a, 0), Human(b, 1)], Veto.Start(Topics(a), Topics(b), new Random(seed)), seed, now, kind);
        match.RematchOfMatchId = rematchOf;
        db.Matches.Add(match);
        await db.SaveChangesAsync(ct);
        var claimed = await db.Users
            .Where(u => (u.Id == userA || u.Id == userB) && (u.CurrentMatchId == null || db.Matches.Any(m => m.Id == u.CurrentMatchId && m.Phase == MatchPhase.Done)))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.CurrentMatchId, match.Id), ct);
        if (claimed != 2)
            throw new AppException(ErrorCodes.MatchAlreadyInMatch, "One of the players is already in a match.");
        return match.Id;
    }

    public Task<MatchStateDto> NextAsync(Guid userId, Guid matchId, CancellationToken ct) =>
        CommandAsync(userId, matchId, (match, _, now) =>
        {
            MatchEngine.Next(match, now);
            return [];
        }, ct);

    public Task<MatchStateDto> ResignAsync(Guid userId, Guid matchId, CancellationToken ct) =>
        CommandAsync(userId, matchId, (match, seat, now) =>
        {
            MatchEngine.Resign(match, seat, now);
            return [];
        }, ct);

    // The tick: matches whose phase deadline has passed.
    public Task<List<Guid>> DueMatchIdsAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return db.Matches.Where(m => m.Phase != MatchPhase.Done && m.PhaseDeadline <= now).Select(m => m.Id).ToListAsync(ct);
    }

    public async Task AdvanceAsync(Guid matchId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // SKIP LOCKED: a player's command holds the row right now; the next tick picks the match up again.
        var locked = await db.Database
            .SqlQuery<int>($"select 1 as \"Value\" from matches where id = {matchId} and phase <> 'done' and phase_deadline <= {now} for update skip locked")
            .ToListAsync(ct);
        if (locked.Count == 0) return;

        var match = await LoadAsync(matchId, ct);
        var wasDone = match.Phase == MatchPhase.Done;
        await LoadRecordingAsync(match, ct);
        var recorded = MatchEngine.Advance(match, now);
        // The veto just drew the topic: build the deck, then let the pick animation and the lobby run their course.
        if (await AttachDeckIfPickedAsync(match, now, ct)) recorded.AddRange(MatchEngine.Advance(match, now));
        await LearnAsync(match, recorded, ct);
        await SaveAsync(match, wasDone, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<List<MatchSummaryDto>> ListMineAsync(Guid userId, int limit, CancellationToken ct)
    {
        var matches = await db.Matches
            .Where(m => m.Phase == MatchPhase.Done && m.Players.Any(p => p.UserId == userId && p.Kind == SeatKind.Human))
            .OrderByDescending(m => m.FinishedAt)
            .Take(Math.Clamp(limit, 1, 50))
            .Include(m => m.Players).ThenInclude(p => p.User)
            .AsSplitQuery()
            .ToListAsync(ct);
        return await SummariesAsync(matches, userId, ct);
    }

    private async Task<List<MatchSummaryDto>> SummariesAsync(List<Match> matches, Guid userId, CancellationToken ct)
    {
        var topics = await TopicsAsync(matches.Select(m => m.TopicId), ct);
        var ids = matches.Select(m => m.Id).ToList();
        // Newest first (UUIDv7 ids break ties in creation order), so each match keeps its latest lesson.
        var lessons = (await db.Lessons.Where(l => l.UserId == userId && l.MatchId != null && ids.Contains(l.MatchId.Value))
                .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
                .Select(l => new { MatchId = l.MatchId!.Value, l.Id }).ToListAsync(ct))
            .GroupBy(l => l.MatchId)
            .ToDictionary(g => g.Key, g => g.First().Id);

        return matches.Select(m =>
        {
            var me = m.Players.First(p => p.UserId == userId && p.Kind == SeatKind.Human);
            var other = m.Players.FirstOrDefault(p => p.Seat != me.Seat);
            var outcome = m.ResultReason == MatchResultReason.Aborted ? MatchOutcome.Aborted
                : other is null ? MatchOutcome.Completed
                : m.WinnerSeat is null ? MatchOutcome.Draw
                : m.WinnerSeat == me.Seat ? MatchOutcome.Win : MatchOutcome.Loss;
            return new MatchSummaryDto(
                m.Id, m.Kind, m.TopicId is { } t ? topics.GetValueOrDefault(t) : null, other is null ? null : Opponent(other),
                me.Score, other?.Score ?? 0, outcome, me.RatingAfter - me.RatingBefore, m.FinishedAt, lessons.TryGetValue(m.Id, out var lessonId) ? lessonId : null);
        }).ToList();
    }

    public async Task<MatchReviewDto> ReviewAsync(Guid userId, Guid matchId, CancellationToken ct)
    {
        var match = await LoadAsync(matchId, ct);
        var me = Seat(match, userId);
        if (match.Phase != MatchPhase.Done)
            throw new AppException(ErrorCodes.MatchNotFinished, "The analysis opens when the match is over.");

        var opponentSeat = match.Players.FirstOrDefault(p => p.Seat != me.Seat)?.Seat;
        var concepts = await ConceptsAsync(match, ct);
        var questionIds = match.Questions.Select(q => q.QuestionId).ToList();
        var reported = (await db.QuestionReports.Where(r => r.UserId == userId && questionIds.Contains(r.QuestionId)).Select(r => r.QuestionId).ToListAsync(ct)).ToHashSet();
        var answers = await db.Answers.Where(a => a.MatchId == match.Id && a.UserId == userId).OrderBy(a => a.CreatedAt).ToListAsync(ct);
        var deltas = answers.GroupBy(a => a.ConceptId)
            .Select(g => new MasteryDeltaDto(concepts[g.Key], g.First().MasteryBefore, g.Last().MasteryAfter))
            .ToArray();

        return new MatchReviewDto(
            (await SummariesAsync([match], userId, ct))[0],
            match.Questions.Select(q => Review(match, q.Index, me.Seat, opponentSeat, concepts, reported.Contains(q.QuestionId))).ToArray(),
            deltas);
    }

    public async Task<List<BotDto>> ListBotsAsync(Guid userId, CancellationToken ct)
    {
        var rating = await db.Users.Where(u => u.Id == userId).Select(u => u.Rating).FirstAsync(ct);
        var recommended = BotPlayer.Recommended(rating);
        return BotPlayer.Personas.Select(p => new BotDto(p.Key, p.Value.DisplayRating, p.Key == recommended)).ToList();
    }

    private async Task<MatchStateDto> StartAsync(
        User user, MatchKind kind, Guid topicId, bool timed, (int Min, int Max) band, MatchPlayer[] seats,
        Func<IReadOnlyList<DeckQuestion>, DeckPlayer, Random, DateTimeOffset, List<DeckSlot>> buildDeck, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (!await db.Topics.AnyAsync(t => t.Id == topicId && t.IsActive, ct))
            throw new AppException(ErrorCodes.TopicNotFound, "This topic does not exist or is not playable yet.", 404);

        // Tracked on purpose: the deck's questions become navigations of the new match.
        var questions = await db.Questions.Where(q => q.TopicId == topicId && q.Status == QuestionStatus.Active).OrderBy(q => q.Id).ToListAsync(ct);
        var seed = Random.Shared.Next();
        var deck = buildDeck(
            questions.Select(q => new DeckQuestion(q.Id, q.ConceptId, q.Type, q.Difficulty)).ToList(),
            await DeckPlayerAsync(user.Id, topicId, now, ct), new Random(seed), now);
        if (deck.Count == 0)
            throw new AppException(ErrorCodes.TopicNotPlayable, "This topic has no questions yet.");

        var match = MatchEngine.Create(kind, topicId, timed, band, seed, seats, deck, questions.ToDictionary(q => q.Id), now);
        return await SaveNewAsync(user.Id, match, now, ct);
    }

    // Any match a player starts takes them out of the ranked queue and claims their single active-match slot
    // (the claim fails while another match still runs: a double click, two tabs).
    private async Task<MatchStateDto> SaveNewAsync(Guid userId, Match match, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LeaveQueueAsync(userId, ct);
        db.Matches.Add(match);
        await db.SaveChangesAsync(ct);
        var claimed = await db.Users
            .Where(u => u.Id == userId && (u.CurrentMatchId == null || db.Matches.Any(m => m.Id == u.CurrentMatchId && m.Phase == MatchPhase.Done)))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.CurrentMatchId, match.Id), ct);
        if (claimed == 0)
            throw new AppException(ErrorCodes.MatchAlreadyInMatch, "Finish or resign your current match first.");
        await transaction.CommitAsync(ct);
        return await MapAsync(match, 0, now, ct);
    }

    private async Task<MatchStateDto> CommandAsync(Guid userId, Guid matchId, Func<Match, int, DateTimeOffset, List<RecordedAnswer>> command, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var match = await LockedAsync(matchId, ct);
        var seat = Seat(match, userId).Seat;
        var wasDone = match.Phase == MatchPhase.Done;
        var recorded = command(match, seat, now);
        await AttachDeckIfPickedAsync(match, now, ct);
        await LearnAsync(match, recorded, ct);
        await SaveAsync(match, wasDone, ct);
        await transaction.CommitAsync(ct);
        return await MapAsync(match, seat, now, ct);
    }

    // Mastery and the answers log, for human answers that count (a timeout while disconnected does not).
    private async Task LearnAsync(Match match, List<RecordedAnswer> recorded, CancellationToken ct)
    {
        foreach (var (player, question, answer, counts) in recorded)
        {
            if (!counts || player.UserId is not { } userId) continue;
            await db.RecordAsync(new Answer
            {
                UserId = userId,
                QuestionId = question.QuestionId,
                ConceptId = question.ConceptId,
                TopicId = match.TopicId!.Value,
                QuestionType = question.Type,
                Difficulty = question.Difficulty,
                Source = match.Source,
                MatchId = match.Id,
                Payload = answer.Payload,
                IsCorrect = answer.IsCorrect,
                Fraction = answer.Fraction,
                Points = answer.Points,
                ElapsedMs = answer.ElapsedMs,
                TimeLimitMs = match.Timed ? question.TimeLimitMs : null,
                Label = answer.Label,
                CreatedAt = answer.AnsweredAt,
            }, match.Timed, ct);
        }
    }

    // The recorded player's answers in the source match, which the engine replays as each question starts.
    private async Task LoadRecordingAsync(Match match, CancellationToken ct)
    {
        if (match.Players.FirstOrDefault(p => p.Kind == SeatKind.Ghost) is not { GhostSourceMatchId: { } source } ghost) return;
        match.Recording = await db.MatchAnswers.AsNoTracking()
            .Where(a => a.MatchId == source && db.MatchPlayers.Any(p => p.MatchId == source && p.Seat == a.Seat && p.UserId == ghost.UserId))
            .ToListAsync(ct);
    }

    // The reward matrix for one player (docs §4.3) through the ledger; the seat keeps what this match paid for the result
    // screen. Every finished match except calibration counts as the day's activity (streak).
    private async Task PayAsync(Match match, MatchPlayer seat, CancellationToken ct)
    {
        var userId = seat.UserId!.Value;
        var spFromWins = await rewarder.SpTodayAsync(userId, [RewardType.MatchWin, RewardType.RatingUpset], ct);
        foreach (var grant in Rewards.ForMatch(match, seat, spFromWins))
        {
            if (!await rewarder.GrantAsync(userId, grant, ct)) continue;
            seat.CoinsEarned += grant.Coins;
            seat.XpEarned += grant.Xp;
            seat.SpEarned += grant.Sp;
        }
        if (match.Kind != MatchKind.Calibration && match.ResultReason != MatchResultReason.Aborted)
            await rewarder.RecordActivityAsync(seat.User!, ct);
    }

    // Ranked: the deck of both players on the topic the veto drew, in the band of their average rating.
    private async Task<bool> AttachDeckIfPickedAsync(Match match, DateTimeOffset now, CancellationToken ct)
    {
        if (match.Phase != MatchPhase.VetoPick || match.Questions.Count > 0 || match.TopicId is not { } topicId) return false;
        var questions = await db.Questions.Where(q => q.TopicId == topicId && q.Status == QuestionStatus.Active).OrderBy(q => q.Id).ToListAsync(ct);
        var humans = match.Players.Where(p => p.Kind == SeatKind.Human).ToList();
        var players = new List<DeckPlayer>();
        foreach (var human in humans) players.Add(await DeckPlayerAsync(human.UserId!.Value, topicId, now, ct));
        var band = DeckBuilder.Band(humans[0].DisplayRating, humans[^1].DisplayRating);
        var targeting = humans.All(h => h.User!.RankedMatches >= GameRules.ProfileAfterRankedMatches);
        var deck = DeckBuilder.Build(
            questions.Select(q => new DeckQuestion(q.Id, q.ConceptId, q.Type, q.Difficulty)).ToList(), players, targeting, band, now,
            new Random(match.RngSeed), degrade: true);
        MatchEngine.AttachDeck(match, band, deck, questions.ToDictionary(q => q.Id));
        return true;
    }

    private async Task SaveAsync(Match match, bool wasDone, CancellationToken ct)
    {
        if (!wasDone && match.Phase == MatchPhase.Done)
        {
            RankedResult.Apply(match);
            foreach (var seat in match.Players.Where(p => p.Kind == SeatKind.Human)) await PayAsync(match, seat, ct);
        }
        await db.SaveChangesAsync(ct);
        if (!wasDone && match.Phase == MatchPhase.Done)
            await db.Users.Where(u => u.CurrentMatchId == match.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.CurrentMatchId, (Guid?)null), ct);
    }

    // What the deck builder needs about the player: mastery in this topic, questions seen in 30 days, recently targeted concepts.
    private async Task<DeckPlayer> DeckPlayerAsync(Guid userId, Guid topicId, DateTimeOffset now, CancellationToken ct)
    {
        var mastery = await db.ConceptMastery.AsNoTracking().Where(m => m.UserId == userId && m.TopicId == topicId).ToDictionaryAsync(m => m.ConceptId, ct);
        var since = now.AddDays(-GameRules.SeenQuestionDays);
        var seen = await db.Answers.Where(a => a.UserId == userId && a.CreatedAt >= since).Select(a => a.QuestionId).Distinct().ToListAsync(ct);
        // A concept targeted in the last two matches rests until a passed lesson patches it (idea §8).
        var lastMatches = db.MatchPlayers.Where(p => p.UserId == userId).OrderByDescending(p => p.MatchId).Take(GameRules.TargetedRotationMatches).Select(p => p.MatchId);
        var targeted = await db.MatchQuestions.Where(q => lastMatches.Contains(q.MatchId) && q.Slot == MatchSlot.Targeted).Select(q => q.ConceptId).ToListAsync(ct);
        var targetedSince = await db.Matches.Where(m => lastMatches.Contains(m.Id)).MinAsync(m => (DateTimeOffset?)m.CreatedAt, ct);
        var patched = await db.Lessons.Where(l => l.UserId == userId && l.Status == LessonStatus.Passed && l.FinishedAt >= targetedSince).Select(l => l.ConceptIds).ToListAsync(ct);
        return new DeckPlayer(mastery, seen.ToHashSet(), targeted.Except(patched.SelectMany(c => c)).ToHashSet());
    }

    private async Task<Match> LoadAsync(Guid matchId, CancellationToken ct)
    {
        var match = await db.Matches
            .Include(m => m.Players).ThenInclude(p => p.User)
            .Include(m => m.Questions).ThenInclude(q => q.Question)
            .Include(m => m.Answers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == matchId, ct) ?? throw MatchNotFound();
        match.Players.Sort((a, b) => a.Seat.CompareTo(b.Seat));
        match.Questions.Sort((a, b) => a.Index.CompareTo(b.Index));
        return match;
    }

    private async Task<User> LoadUserAsync(Guid userId, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
        ?? throw new AppException(ErrorCodes.AuthRefreshInvalid, "The account no longer exists.", 401);

    private static MatchPlayer Human(User user, int seat = 0) =>
        new() { Seat = seat, Kind = SeatKind.Human, UserId = user.Id, User = user, DisplayRating = user.Rating };

    private static MatchPlayer Seat(Match match, Guid userId) =>
        match.Players.FirstOrDefault(p => p.UserId == userId && p.Kind == SeatKind.Human)
        ?? throw new AppException(ErrorCodes.MatchNotParticipant, "You are not playing in this match.", 403);

    private static AppException MatchNotFound() => new(ErrorCodes.MatchNotFound, "This match does not exist.", 404);

    private async Task<Dictionary<Guid, TopicDto>> TopicsAsync(IEnumerable<Guid?> topicIds, CancellationToken ct)
    {
        var ids = topicIds.OfType<Guid>().Distinct().ToList();
        return await db.Topics.Where(t => ids.Contains(t.Id))
            .Select(t => new TopicDto(t.Id, t.Slug, t.Name, t.Description, t.Concepts.Count))
            .ToDictionaryAsync(t => t.Id, ct);
    }

    private async Task<MatchStateDto> MapAsync(Match match, int seat, DateTimeOffset now, CancellationToken ct)
    {
        var topic = (await TopicsAsync([match.TopicId], ct)).Values.FirstOrDefault();
        var current = match.Phase is MatchPhase.Question or MatchPhase.Interstitial ? match.Current : null;
        var me = match.Players.First(p => p.Seat == seat);
        var opponentSeat = match.Players.FirstOrDefault(p => p.Seat != seat)?.Seat;

        var question = current is null ? null : new QuestionTurnDto(
            current.Index, match.Questions.Count, Public(current), match.QuestionStartedAt!.Value,
            me.DeliveredIndex == current.Index ? me.DeliveredAt : null,
            match.AnswerOf(seat, current.Index) is { } mine ? new MyAnswerDto(Json(mine.Payload), mine.ElapsedMs) : null);

        var interstitial = match.Phase != MatchPhase.Interstitial || current is null ? null : new InterstitialDto(
            current.Index, Key(current), Result(match.AnswerOf(seat, current.Index)!),
            opponentSeat is { } os && match.AnswerOf(os, current.Index) is { } theirs ? Result(theirs) : null);

        var veto = match.Veto is { } v && match.Phase is MatchPhase.VetoCoin or MatchPhase.VetoBan or MatchPhase.VetoPick
            ? await VetoAsync(v, me, ct)
            : null;

        var ghostSources = match.Players.Where(p => p.GhostSourceMatchId is not null).Select(p => p.GhostSourceMatchId!.Value).ToList();
        var recordedAt = await db.Matches.Where(m => ghostSources.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.FinishedAt, ct);

        return new MatchStateDto(
            match.Id, match.Kind, Status(match), match.Phase, now, match.PhaseDeadline, topic, match.Timed, seat,
            match.Players.Select(p => Player(match, p, now, p.GhostSourceMatchId is { } source ? recordedAt.GetValueOrDefault(source) : null)).ToArray(),
            veto, question, interstitial,
            match.Phase == MatchPhase.Done ? await ResultAsync(match, me, opponentSeat, ct) : null);
    }

    private async Task<VetoDto> VetoAsync(VetoState veto, MatchPlayer me, CancellationToken ct)
    {
        var topics = await TopicsAsync(veto.Pool.Select(t => (Guid?)t), ct);
        // My own mastery per topic (answered concepts only); the opponent's stays hidden (idea §5).
        var mastery = await db.ConceptMastery.Where(m => m.UserId == me.UserId && veto.Pool.Contains(m.TopicId) && m.AnswerCount > 0)
            .GroupBy(m => m.TopicId)
            .Select(g => new { g.Key, Mastery = g.Average(m => m.Mastery) })
            .ToDictionaryAsync(x => x.Key, x => x.Mastery, ct);
        var opponentPicks = veto.Picks.ElementAtOrDefault(1 - me.Seat) ?? [];
        return new VetoDto(
            veto.Pool.Select(t =>
            {
                var ban = veto.Bans.FirstOrDefault(b => b.TopicId == t);
                return new VetoTopicDto(topics[t], mastery.TryGetValue(t, out var m) ? Math.Round(m, 1) : null, opponentPicks.Contains(t), ban?.Seat, ban?.Auto);
            }).ToArray(),
            veto.CoinWinnerSeat, veto.TurnSeat, veto.BansRemaining, veto.FinalTwo?.ToArray(), veto.ChosenTopicId);
    }

    private async Task<Dictionary<Guid, ConceptDto>> ConceptsAsync(Match match, CancellationToken ct)
    {
        var conceptIds = match.Questions.Select(q => q.ConceptId).Distinct().ToList();
        return await db.Concepts.Where(c => conceptIds.Contains(c.Id))
            .Select(c => new ConceptDto(c.Id, c.Slug, c.Name, c.TopicId))
            .ToDictionaryAsync(c => c.Id, ct);
    }

    private async Task<MatchResultDto> ResultAsync(Match match, MatchPlayer me, int? opponentSeat, CancellationToken ct)
    {
        var deltas = await db.Answers
            .Where(a => a.MatchId == match.Id && a.UserId == me.UserId)
            .GroupBy(a => a.ConceptId)
            .Select(g => new { g.Key, Delta = g.Sum(a => a.MasteryAfter - a.MasteryBefore) })
            .ToDictionaryAsync(x => x.Key, x => x.Delta, ct);
        var concepts = await ConceptsAsync(match, ct);
        var cards = ResultCardRules.Of(match, me.Seat, deltas);
        var lesson = await db.Lessons.Where(l => l.MatchId == match.Id && l.UserId == me.UserId)
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).FirstOrDefaultAsync(ct);
        var lessonAvailable = match.Answers.Any(a => a.Seat == me.Seat && a.Fraction < 1);

        var opponent = opponentSeat is { } seat ? match.Players.First(p => p.Seat == seat) : null;
        return new MatchResultDto(
            match.WinnerSeat, match.ResultReason!.Value,
            new MyResultDto(me.Score, me.CorrectCount, me.RatingBefore, me.RatingAfter, me.SpEarned, me.CoinsEarned, me.XpEarned,
                me.User?.StreakLastActiveOn == Clock.Today(time.GetUtcNow())),
            opponent is null ? null : new OpponentResultDto(opponent.Score, opponent.CorrectCount),
            new ResultCardsDto(
                cards.BlunderIndex is { } b ? Review(match, b, me.Seat, opponentSeat, concepts, false) : null,
                cards.BrilliantIndex is { } r ? Review(match, r, me.Seat, opponentSeat, concepts, false) : null,
                cards.PracticeConceptId is { } c ? new PracticeConceptCardDto(concepts[c], Math.Round(cards.PracticeConceptDelta, 1)) : null),
            lessonAvailable,
            lessonAvailable && (lesson is null || LessonRules.RewardEligible(lesson, time.GetUtcNow())),
            lesson?.Id,
            Rematch.Available(match, me.Seat, time.GetUtcNow()),
            true);
    }

    private static ReviewQuestionDto Review(Match match, int index, int seat, int? opponentSeat, Dictionary<Guid, ConceptDto> concepts, bool reported)
    {
        var q = match.Questions.First(x => x.Index == index);
        var theirs = opponentSeat is { } os ? match.AnswerOf(os, index) : null;
        return new ReviewQuestionDto(
            index, Public(q), Key(q), concepts[q.ConceptId], q.Slot, Result(match.AnswerOf(seat, index)!), theirs is null ? null : Result(theirs),
            QuestionView.Explanation(q.Question!.Explanation), reported);
    }

    private static PlayerDto Player(Match match, MatchPlayer player, DateTimeOffset now, DateTimeOffset? ghostRecordedAt) => new(
        player.Seat, player.Kind, player.User is null ? null : UserRefDto.From(player.User), player.BotPersona, ghostRecordedAt, player.DisplayRating,
        match.Phase == MatchPhase.Done ? player.Score : match.Answers.Where(a => a.Seat == player.Seat && match.IsRevealed(a.Index)).Sum(a => a.Points),
        player.IsConnected(now),
        match.Phase == MatchPhase.Question && match.AnswerOf(player.Seat, match.CurrentIndex) is { } a && a.AnsweredAt <= now);

    private static MatchStatus Status(Match match) => match.Phase switch
    {
        MatchPhase.VetoCoin or MatchPhase.VetoBan or MatchPhase.VetoPick => MatchStatus.Veto,
        MatchPhase.Lobby => MatchStatus.Lobby,
        MatchPhase.Done => match.ResultReason == MatchResultReason.Aborted ? MatchStatus.Aborted : MatchStatus.Finished,
        _ => MatchStatus.InProgress,
    };

    private static OpponentDto Opponent(MatchPlayer p) => new(p.Kind, p.BotPersona, p.User is null ? null : UserRefDto.From(p.User), p.DisplayRating);

    private static QuestionPublicDto Public(MatchQuestion q) =>
        new(q.QuestionId, q.Type, q.Difficulty, q.Question!.Language, q.TimeLimitMs, q.BasePoints, QuestionJson.Public(q.Type, q.Question.Content));

    private static AnswerKeyDto Key(MatchQuestion q) => new(JsonNode.Parse(q.Question!.Content)!["answer"]!.DeepClone(), q.Question.Remember);

    private static AnswerResultDto Result(MatchAnswer a) => new(Json(a.Payload), a.IsCorrect, a.Fraction, a.Points, a.ElapsedMs, a.TimedOut, a.Label);

    private static JsonNode? Json(string? json) => json is null ? null : JsonNode.Parse(json);
}
