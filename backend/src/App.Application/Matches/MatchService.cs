using System.Text.Json.Nodes;
using App.Application.Content;
using App.Application.Me;
using App.Domain;
using App.Domain.Content;
using App.Domain.Matches;
using App.Domain.Progress;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Matches;

// Every command that changes a match runs in a transaction that first locks the match row (SELECT ... FOR UPDATE),
// so two answers, an answer and the tick, or a resign and the tick never interleave (docs §2.3).
public class MatchService(IAppDbContext db, TimeProvider time)
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
        await LearnAsync(match, MatchEngine.Advance(match, now), ct);
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
        var topics = await TopicsAsync(matches.Select(m => m.TopicId), ct);

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
                me.Score, other?.Score ?? 0, outcome, null, m.FinishedAt, null);
        }).ToList();
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
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Matches.Add(match);
        await db.SaveChangesAsync(ct);
        // One active match per user: the claim fails while another match is still running (a double click, two tabs).
        var claimed = await db.Users
            .Where(u => u.Id == user.Id && (u.CurrentMatchId == null || db.Matches.Any(m => m.Id == u.CurrentMatchId && m.Phase == MatchPhase.Done)))
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
        var exists = await db.Database.SqlQuery<int>($"select 1 as \"Value\" from matches where id = {matchId} for update").ToListAsync(ct);
        if (exists.Count == 0) throw MatchNotFound();

        var match = await LoadAsync(matchId, ct);
        var seat = Seat(match, userId).Seat;
        var wasDone = match.Phase == MatchPhase.Done;
        await LearnAsync(match, command(match, seat, now), ct);
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
            var concept = await db.ConceptMastery.FindAsync([userId, question.ConceptId], ct);
            if (concept is null)
            {
                concept = new ConceptMastery { UserId = userId, ConceptId = question.ConceptId, TopicId = match.TopicId!.Value };
                db.ConceptMastery.Add(concept);
            }
            var before = concept.Mastery;
            Mastery.Apply(concept, answer.Fraction, Mastery.Weight(question.Difficulty, question.Type, answer.Fraction, answer.Label, match.Timed), answer.AnsweredAt);
            db.Answers.Add(new Answer
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
                MasteryBefore = before,
                MasteryAfter = concept.Mastery,
                CreatedAt = answer.AnsweredAt,
            });
        }
    }

    private async Task SaveAsync(Match match, bool wasDone, CancellationToken ct)
    {
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
        // ponytail: rotation ignores lessons until F2 adds patching ("not targeted again until patched").
        var lastMatches = db.MatchPlayers.Where(p => p.UserId == userId).OrderByDescending(p => p.MatchId).Take(GameRules.TargetedRotationMatches).Select(p => p.MatchId);
        var targeted = await db.MatchQuestions.Where(q => lastMatches.Contains(q.MatchId) && q.Slot == MatchSlot.Targeted).Select(q => q.ConceptId).ToListAsync(ct);
        return new DeckPlayer(mastery, seen.ToHashSet(), targeted.ToHashSet());
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

    private static MatchPlayer Human(User user) =>
        new() { Seat = 0, Kind = SeatKind.Human, UserId = user.Id, User = user, DisplayRating = user.Rating };

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

        return new MatchStateDto(
            match.Id, match.Kind, Status(match), match.Phase, now, match.PhaseDeadline, topic, match.Timed, seat,
            match.Players.Select(p => Player(match, p, now)).ToArray(),
            question, interstitial,
            match.Phase == MatchPhase.Done ? await ResultAsync(match, me, opponentSeat, ct) : null);
    }

    private async Task<MatchResultDto> ResultAsync(Match match, MatchPlayer me, int? opponentSeat, CancellationToken ct)
    {
        var deltas = await db.Answers
            .Where(a => a.MatchId == match.Id && a.UserId == me.UserId)
            .GroupBy(a => a.ConceptId)
            .Select(g => new { g.Key, Delta = g.Sum(a => a.MasteryAfter - a.MasteryBefore) })
            .ToDictionaryAsync(x => x.Key, x => x.Delta, ct);
        var conceptIds = match.Questions.Select(q => q.ConceptId).Distinct().ToList();
        var concepts = await db.Concepts.Where(c => conceptIds.Contains(c.Id))
            .Select(c => new ConceptDto(c.Id, c.Slug, c.Name, c.TopicId))
            .ToDictionaryAsync(c => c.Id, ct);
        var cards = ResultCardRules.Of(match, me.Seat, deltas);

        ReviewQuestionDto? Review(int? index)
        {
            if (index is not { } i) return null;
            var q = match.Questions.First(x => x.Index == i);
            var theirs = opponentSeat is { } os ? match.AnswerOf(os, i) : null;
            return new ReviewQuestionDto(i, Public(q), Key(q), concepts[q.ConceptId], q.Slot, Result(match.AnswerOf(me.Seat, i)!), theirs is null ? null : Result(theirs), null, false);
        }

        var opponent = opponentSeat is { } seat ? match.Players.First(p => p.Seat == seat) : null;
        return new MatchResultDto(
            match.WinnerSeat, match.ResultReason!.Value,
            // ponytail: rating, SP, coins, XP and streak stay zero until F3 (Elo) and F4 (economy).
            new MyResultDto(me.Score, me.CorrectCount, null, null, 0, 0, 0, false),
            opponent is null ? null : new OpponentResultDto(opponent.Score, opponent.CorrectCount),
            new ResultCardsDto(
                Review(cards.BlunderIndex), Review(cards.BrilliantIndex),
                cards.PracticeConceptId is { } c ? new PracticeConceptCardDto(concepts[c], Math.Round(cards.PracticeConceptDelta, 1)) : null),
            false, false, null, false, false); // lessons and review arrive in F2, rematch in F3
    }

    private static PlayerDto Player(Match match, MatchPlayer player, DateTimeOffset now) => new(
        player.Seat, player.Kind, player.User is null ? null : UserRefDto.From(player.User), player.BotPersona, null, player.DisplayRating,
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
