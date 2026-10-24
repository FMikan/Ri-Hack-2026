using System.Text.Json;
using App.Application.Content;
using App.Domain;
using App.Domain.Content;
using App.Domain.Lessons;
using App.Domain.Matches;
using App.Domain.Progress;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App.Application.Lessons;

// The lesson after a match (idea §10): cards for the missed and chronic concepts, then three check questions.
public class LessonService(IAppDbContext db, ILessonGenerator generator, AiOptions ai, ILogger<LessonService> logger, TimeProvider time)
{
    private record StoredResult(Guid QuestionId, JsonElement? Payload, bool IsCorrect);

    public async Task<LessonDto> StartAsync(Guid userId, Guid matchId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var match = await db.Matches.AsNoTracking()
            .Include(m => m.Players).Include(m => m.Questions).Include(m => m.Answers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == matchId, ct)
            ?? throw new AppException(ErrorCodes.MatchNotFound, "This match does not exist.", 404);
        var me = match.Players.FirstOrDefault(p => p.UserId == userId && p.Kind == SeatKind.Human)
            ?? throw new AppException(ErrorCodes.MatchNotParticipant, "You did not play in this match.", 403);
        if (match.Phase != MatchPhase.Done)
            throw new AppException(ErrorCodes.MatchNotFinished, "The lesson opens when the match is over.");

        var misses = match.Answers.Where(a => a.Seat == me.Seat && a.Fraction < 1)
            .Join(match.Questions, a => a.Index, q => q.Index, (a, q) => (Question: q, Cost: q.BasePoints * (1 - a.Fraction)))
            .ToList();
        if (misses.Count == 0)
            throw new AppException(ErrorCodes.LessonNothingMissed, "Nothing was missed in this match, so there is nothing to patch.");

        // The variant re-tests the question the result screen showed as the Blunder card.
        var blunder = match.Questions.First(q => q.Index == ResultCardRules.Of(match, me.Seat, new Dictionary<Guid, double>()).BlunderIndex);
        var topicId = match.TopicId!.Value;
        var mastery = await db.ConceptMastery.AsNoTracking().Where(m => m.UserId == userId && m.TopicId == topicId).ToListAsync(ct);
        var conceptIds = LessonRules.Concepts(misses.Select(m => (m.Question.ConceptId, m.Cost)), mastery, blunder.ConceptId);
        var missed = new DeckQuestion(blunder.QuestionId, blunder.ConceptId, blunder.Type, blunder.Difficulty);
        var rating = await db.Users.Where(u => u.Id == userId).Select(u => u.Rating).FirstAsync(ct);
        var (cards, source) = await CardsAsync(conceptIds, DeckBuilder.DifficultyOf(rating), now, ct);

        var lesson = new Lesson
        {
            UserId = userId,
            MatchId = match.Id,
            ConceptIds = conceptIds,
            Cards = JsonSerializer.Serialize(cards, JsonSerializerOptions.Web),
            CardsSource = source,
            CheckQuestionIds = await ChecksAsync(userId, topicId, conceptIds, missed, (match.BandMin, match.BandMax), [missed.Id], now, ct),
            FirstForMatch = !await db.Lessons.AnyAsync(l => l.UserId == userId && l.MatchId == match.Id, ct),
            CreatedAt = now,
        };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync(ct);
        return await MapAsync(lesson, now, ct);
    }

    public async Task<LessonDto> GetAsync(Guid userId, Guid lessonId, CancellationToken ct) =>
        await MapAsync(await LoadAsync(userId, lessonId, ct), time.GetUtcNow(), ct);

    public async Task<LessonResultDto> CheckAsync(Guid userId, Guid lessonId, SubmitLessonCheckRequest request, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Row lock: a double submit grades and rewards once.
        await db.Database.SqlQuery<int>($"select 1 as \"Value\" from lessons where id = {lessonId} for update").ToListAsync(ct);
        var lesson = await LoadAsync(userId, lessonId, ct);
        if (lesson.Status == LessonStatus.Passed)
            throw new AppException(ErrorCodes.LessonAlreadyPassed, "This lesson is already passed.");
        if (lesson.RetryAfter > now)
            throw new AppException(ErrorCodes.LessonRetryTooSoon, "Read the cards again; the check reopens 10 minutes after a miss.");

        var rewardEligible = LessonRules.RewardEligible(lesson, now);
        var questions = await db.Questions.AsNoTracking().Where(q => lesson.CheckQuestionIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var graded = lesson.CheckQuestionIds.Select(id =>
        {
            var question = questions[id];
            var payload = request.Answers.FirstOrDefault(a => a.QuestionId == id)?.Payload is { ValueKind: JsonValueKind.Object } p ? p : (JsonElement?)null;
            var fraction = payload is { } value ? Grader.Grade(question.Type, QuestionJson.Answer(question.Content), value) : 0;
            return (Question: question, Payload: payload, Fraction: fraction);
        }).ToList();

        foreach (var (question, payload, fraction) in graded)
        {
            var limit = GameRules.Types[question.Type].TimeMs;
            await db.RecordAsync(new Answer
            {
                UserId = userId,
                QuestionId = question.Id,
                ConceptId = question.ConceptId,
                TopicId = question.TopicId,
                QuestionType = question.Type,
                Difficulty = question.Difficulty,
                Source = AnswerSource.LessonCheck,
                LessonId = lesson.Id,
                Payload = payload?.GetRawText(),
                IsCorrect = fraction >= 1,
                Fraction = fraction,
                Points = Scoring.Points(GameRules.Types[question.Type].BasePoints, fraction, 0, limit, timed: false),
                ElapsedMs = 0,
                Label = Labels.Of(question.Difficulty, fraction, 0, limit, timedOut: false, timed: false),
                CreatedAt = now,
            }, timed: false, ct);
        }

        var passed = LessonRules.Passed(graded[0].Fraction >= 1, graded.Skip(1).Count(g => g.Fraction >= 1));
        lesson.LastAttemptAt = now;
        lesson.CheckResults = JsonSerializer.Serialize(graded.Select(g => new StoredResult(g.Question.Id, g.Payload, g.Fraction >= 1)), JsonSerializerOptions.Web);
        var patched = new List<ConceptDto>();
        if (passed)
        {
            lesson.Status = LessonStatus.Passed;
            lesson.FinishedAt = now;
            lesson.RetryAfter = null;
            // ponytail: coins (50), XP and SP (2 per concept, 7-day rule) are credited with the economy in F4; F2 marks the reward as used.
            lesson.Rewarded = rewardEligible;
            foreach (var conceptId in lesson.ConceptIds)
            {
                var concept = await db.ConceptMastery.FindAsync([userId, conceptId], ct);
                if (concept is null)
                {
                    concept = new ConceptMastery { UserId = userId, ConceptId = conceptId, TopicId = graded[0].Question.TopicId };
                    db.ConceptMastery.Add(concept);
                }
                ReviewSchedule.Patch(concept, now);
            }
            patched = await ConceptsAsync(lesson.ConceptIds, ct);
        }
        else
        {
            // A retry asks fresh questions: the keys of these ones were just shown.
            var variant = graded[0].Question;
            lesson.Status = LessonStatus.Failed;
            lesson.Attempt++;
            lesson.RetryAfter = now + GameRules.LessonRetryAfter;
            lesson.CheckQuestionIds = await ChecksAsync(userId, variant.TopicId, lesson.ConceptIds,
                new DeckQuestion(variant.Id, variant.ConceptId, variant.Type, variant.Difficulty), (variant.Difficulty, variant.Difficulty), [.. lesson.CheckQuestionIds], now, ct);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new LessonResultDto(
            passed,
            graded.Select((g, i) => new LessonCheckResultDto(g.Question.Id, g.Fraction >= 1, QuestionView.Key(g.Question), i == 0)).ToArray(),
            [.. patched], new RewardsDto(0, 0, 0), lesson.RetryAfter);
    }

    // The variant first, then two more from the lesson concepts; never a question listed in `exclude`.
    private async Task<List<Guid>> ChecksAsync(
        Guid userId, Guid topicId, List<Guid> conceptIds, DeckQuestion missed, (int Min, int Max) band, HashSet<Guid> exclude, DateTimeOffset now, CancellationToken ct)
    {
        var pool = await db.Questions.Where(q => q.TopicId == topicId && q.Status == QuestionStatus.Active)
            .Select(q => new DeckQuestion(q.Id, q.ConceptId, q.Type, q.Difficulty)).ToListAsync(ct);
        var since = now.AddDays(-GameRules.SeenQuestionDays);
        var seen = (await db.Answers.Where(a => a.UserId == userId && a.CreatedAt >= since).Select(a => a.QuestionId).Distinct().ToListAsync(ct)).ToHashSet();
        var rng = new Random();
        var variant = LessonRules.Variant(pool, missed, seen, exclude, rng) ?? missed;
        var others = LessonRules.OtherChecks(pool, conceptIds, variant, seen, new HashSet<Guid>(exclude) { missed.Id }, band, rng);
        return [variant.Id, .. others.Select(q => q.Id)];
    }

    // One card per concept: cached for the band, else generated (in parallel, one shared time limit), else built from
    // the concept's description. Only generated cards are cached, so a fallback is retried next time.
    private async Task<(LessonCard[] Cards, LessonCardsSource Source)> CardsAsync(List<Guid> conceptIds, int band, DateTimeOffset now, CancellationToken ct)
    {
        var concepts = await db.Concepts.Where(c => conceptIds.Contains(c.Id))
            .Join(db.Topics, c => c.TopicId, t => t.Id, (c, t) => new { c.Id, c.Name, c.Description, Topic = t.Name })
            .ToDictionaryAsync(c => c.Id, ct);
        var cached = await db.ConceptLessonCards.Where(c => conceptIds.Contains(c.ConceptId) && c.Band == band).ToDictionaryAsync(c => c.ConceptId, ct);
        var generated = await Task.WhenAll(conceptIds.Select(id => cached.ContainsKey(id)
            ? Task.FromResult<GeneratedCard?>(null)
            : ai.TryAsync(token => generator.CardAsync(new LessonCardInput(concepts[id].Topic, concepts[id].Name, concepts[id].Description, band), token), logger, ct)));

        var sources = new HashSet<LessonCardsSource>();
        var cards = conceptIds.Select((id, i) =>
        {
            if (cached.TryGetValue(id, out var hit))
            {
                sources.Add(LessonCardsSource.Cache);
                return JsonSerializer.Deserialize<LessonCard[]>(hit.Cards, JsonSerializerOptions.Web)![0];
            }
            if (generated[i] is { } g)
            {
                sources.Add(LessonCardsSource.Ai);
                return new LessonCard(id, g.Title, g.Why, g.Code, g.Language, g.Misconception);
            }
            sources.Add(LessonCardsSource.Fallback);
            return new LessonCard(id, concepts[id].Name, concepts[id].Description, null, null, "");
        }).ToArray();

        // ON CONFLICT: two lessons generating the same concept at once both succeed; the first card stays.
        foreach (var card in cards.Where((_, i) => generated[i] is not null))
        {
            var json = JsonSerializer.Serialize(new[] { card }, JsonSerializerOptions.Web);
            await db.Database.ExecuteSqlAsync(
                $"insert into concept_lesson_cards (concept_id, band, cards, created_at) values ({card.ConceptId}, {band}, {json}::jsonb, {now}) on conflict do nothing", ct);
        }
        var source = sources.Contains(LessonCardsSource.Fallback) ? LessonCardsSource.Fallback
            : sources.Contains(LessonCardsSource.Ai) ? LessonCardsSource.Ai
            : LessonCardsSource.Cache;
        return (cards, source);
    }

    private async Task<Lesson> LoadAsync(Guid userId, Guid lessonId, CancellationToken ct) =>
        await db.Lessons.FirstOrDefaultAsync(l => l.Id == lessonId && l.UserId == userId, ct)
        ?? throw new AppException(ErrorCodes.LessonNotFound, "This lesson does not exist.", 404);

    private async Task<List<ConceptDto>> ConceptsAsync(List<Guid> ids, CancellationToken ct)
    {
        var concepts = await db.Concepts.Where(c => ids.Contains(c.Id)).Select(c => new ConceptDto(c.Id, c.Slug, c.Name, c.TopicId)).ToDictionaryAsync(c => c.Id, ct);
        return ids.Select(id => concepts[id]).ToList();
    }

    private async Task<LessonDto> MapAsync(Lesson lesson, DateTimeOffset now, CancellationToken ct)
    {
        var results = lesson.CheckResults is null ? null : JsonSerializer.Deserialize<StoredResult[]>(lesson.CheckResults, JsonSerializerOptions.Web);
        var questionIds = lesson.CheckQuestionIds.Concat(results?.Select(r => r.QuestionId) ?? []).ToList();
        var questions = await db.Questions.AsNoTracking().Where(q => questionIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        return new LessonDto(
            lesson.Id, lesson.MatchId, lesson.Status,
            [.. await ConceptsAsync(lesson.ConceptIds, ct)],
            JsonSerializer.Deserialize<LessonCard[]>(lesson.Cards, JsonSerializerOptions.Web)!,
            lesson.CheckQuestionIds.Select((id, i) => new LessonCheckDto(QuestionView.Public(questions[id]), i == 0)).ToArray(),
            lesson.Attempt, LessonRules.RewardEligible(lesson, now), lesson.RetryAfter,
            results?.Select((r, i) => new LessonCheckResultDto(r.QuestionId, r.IsCorrect, QuestionView.Key(questions[r.QuestionId]), i == 0)).ToArray());
    }
}
