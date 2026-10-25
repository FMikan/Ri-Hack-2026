using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using App.Application.Content;
using App.Application.Subscription;
using App.Domain;
using App.Domain.Content;
using App.Domain.Matches;
using App.Domain.Professor;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App.Application.Professor;

// A question checked before the stream opens, so a missing chat or a missing AI is a normal Problem Details response.
public record ProfessorTurn(Guid UserId, ProfessorChat? Chat, string Question, IReadOnlyList<ProfessorTurnInput> History);

// The AI Professor (docs §3.13, §6.12): chats about IT, and lessons and quizzes written from one answer. Nothing here
// touches rewards, mastery or the question pool (idea §16: generated content never enters the pool).
public class ProfessorService(IAppDbContext db, IProfessorAi ai, ILogger<ProfessorService> logger, TimeProvider time)
{
    private const int ChatListLimit = 50;

    public async Task<ProfessorChatSummaryDto[]> ListChatsAsync(Guid userId, CancellationToken ct) =>
        await db.ProfessorChats.AsNoTracking().Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt).Take(ChatListLimit)
            .Select(c => new ProfessorChatSummaryDto(c.Id, c.Title, c.UpdatedAt))
            .ToArrayAsync(ct);

    public async Task<ProfessorChatDto> GetChatAsync(Guid userId, Guid chatId, CancellationToken ct)
    {
        var chat = await db.ProfessorChats.AsNoTracking().FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == userId, ct) ?? throw ChatNotFound();
        var messages = await db.ProfessorMessages.AsNoTracking().Where(m => m.ChatId == chatId).OrderBy(m => m.CreatedAt).ToListAsync(ct);
        var lessons = await db.ProfessorLessons.AsNoTracking().Where(l => l.ChatId == chatId).ToDictionaryAsync(l => l.MessageId, l => (Guid?)l.Id, ct);
        var quizzes = await db.ProfessorQuizzes.AsNoTracking().Where(q => q.ChatId == chatId).ToDictionaryAsync(q => q.MessageId, q => (Guid?)q.Id, ct);
        return new ProfessorChatDto(chat.Id, chat.Title,
            [.. messages.Select(m => Map(m, lessons.GetValueOrDefault(m.Id), quizzes.GetValueOrDefault(m.Id)))]);
    }

    public async Task<ProfessorTurn> PrepareTurnAsync(Guid userId, SendProfessorMessageRequest request, CancellationToken ct)
    {
        EnsureAvailable();
        await EnsureQuotaAsync(userId, u => (u.QuestionsUsed, u.QuestionsLimit), ct);
        ProfessorChat? chat = null;
        List<ProfessorTurnInput> history = [];
        if (request.ChatId is { } chatId)
        {
            chat = await db.ProfessorChats.FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == userId, ct) ?? throw ChatNotFound();
            var recent = await db.ProfessorMessages.AsNoTracking().Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.CreatedAt).Take(ProfessorRules.HistoryMessages).ToListAsync(ct);
            history = [.. Enumerable.Reverse(recent)
                .SkipWhile(m => m.Role != ProfessorRole.User) // the model's first message must be the player's
                .Select(m => new ProfessorTurnInput(m.Role == ProfessorRole.User,
                    m.Role == ProfessorRole.User ? m.Text : $"{ProfessorRules.Header(m.Topic, m.Scope)}\n\n{m.Text}"))];
        }
        return new ProfessorTurn(userId, chat, request.Text.Trim(), history);
    }

    // The answer as it is written. The header line is taken off before the player sees it; the turn is saved only once
    // the whole answer is there, so a failed or abandoned answer leaves the chat as it was.
    public async IAsyncEnumerable<ProfessorEvent> StreamTurnAsync(ProfessorTurn turn, [EnumeratorCancellation] CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProfessorRules.ChatTimeout);
        var reader = new ProfessorHeaderReader();
        var answer = new StringBuilder();
        AppException? failure = null;

        await using (var chunks = ai.ChatAsync([.. turn.History, new ProfessorTurnInput(true, turn.Question)], timeout.Token).GetAsyncEnumerator(timeout.Token))
        {
            while (failure is null)
            {
                string text;
                try
                {
                    if (!await chunks.MoveNextAsync()) break;
                    text = reader.Push(chunks.Current);
                }
                catch (AppException e)
                {
                    failure = e;
                    break;
                }
                catch (Exception e) when (!ct.IsCancellationRequested)
                {
                    logger.LogWarning(e, "The professor's answer failed or timed out");
                    failure = timeout.IsCancellationRequested
                        ? Failed("The AI Professor took too long to answer. Try again.")
                        : Failed("The AI Professor could not answer right now. Try again.");
                    break;
                }
                if (text.Length == 0) continue;
                answer.Append(text);
                yield return new ProfessorEvent("delta", new ProfessorDeltaDto(text));
            }
        }

        var rest = failure is null ? reader.Finish() : "";
        if (rest.Length > 0)
        {
            answer.Append(rest);
            yield return new ProfessorEvent("delta", new ProfessorDeltaDto(rest));
        }
        if (failure is null && string.IsNullOrWhiteSpace(answer.ToString())) failure = Failed("The AI Professor gave no answer. Try again.");

        ProfessorTurnDto? saved = null;
        if (failure is null)
        {
            try
            {
                saved = await SaveTurnAsync(turn, answer.ToString().Trim(), reader.Topic, reader.Scope, ct);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                logger.LogError(e, "The professor's answer could not be saved");
                failure = Failed("The answer could not be saved. Try again.");
            }
        }
        yield return saved is not null
            ? new ProfessorEvent("done", saved)
            : new ProfessorEvent("error", new ProfessorStreamErrorDto(failure!.Code, failure.Message, failure.Message, failure.Status));
    }

    public async Task<ProfessorLessonDto> CreateLessonAsync(Guid userId, Guid messageId, CancellationToken ct)
    {
        if (await db.ProfessorLessons.AsNoTracking().FirstOrDefaultAsync(l => l.MessageId == messageId && l.UserId == userId, ct) is { } existing)
            return await MapAsync(existing, ct);
        var (source, input) = await SourceAsync(userId, messageId, ct);
        await EnsureQuotaAsync(userId, u => (u.LessonsUsed, u.LessonsLimit), ct);

        var generated = await GenerateAsync(token => ai.LessonAsync(input, token), "lesson", ct);
        var cards = generated.Cards
            .Where(c => !string.IsNullOrWhiteSpace(c.Title) && !string.IsNullOrWhiteSpace(c.Body))
            .Take(ProfessorRules.LessonMaxCards)
            .Select(c =>
            {
                var code = ProfessorRules.Code(c.Code);
                var language = code is null || string.IsNullOrWhiteSpace(c.Language) ? null : c.Language.Trim().ToLowerInvariant();
                return new ProfessorCard(c.Title.Trim(), c.Body.Trim(), code, language, string.IsNullOrWhiteSpace(c.Misconception) ? null : c.Misconception.Trim());
            })
            .ToArray();
        if (cards.Length < ProfessorRules.LessonMinCards || string.IsNullOrWhiteSpace(generated.Title)) throw Failed("The AI Professor could not write the lesson. Try again.");

        var lesson = new ProfessorLesson
        {
            UserId = userId,
            ChatId = source.ChatId,
            MessageId = source.Id,
            Topic = input.Topic,
            Title = generated.Title.Trim(),
            Summary = generated.Summary.Trim(),
            Cards = JsonSerializer.Serialize(cards, ProfessorRules.Json),
            CreatedAt = time.GetUtcNow(),
        };
        db.ProfessorLessons.Add(lesson);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A double click wrote this answer's lesson first (unique message_id): show that one.
            return await MapAsync(await db.ProfessorLessons.AsNoTracking().FirstAsync(l => l.MessageId == messageId, ct), ct);
        }
        return await MapAsync(lesson, ct);
    }

    public async Task<ProfessorLessonDto> GetLessonAsync(Guid userId, Guid lessonId, CancellationToken ct) =>
        await MapAsync(await db.ProfessorLessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lessonId && l.UserId == userId, ct)
            ?? throw new AppException(ErrorCodes.ProfessorLessonNotFound, "This lesson does not exist.", 404), ct);

    public async Task<ProfessorQuizDto> CreateQuizAsync(Guid userId, Guid messageId, CancellationToken ct)
    {
        if (await db.ProfessorQuizzes.AsNoTracking().FirstOrDefaultAsync(q => q.MessageId == messageId && q.UserId == userId, ct) is { } existing)
            return await MapAsync(existing, ct);
        var (source, input) = await SourceAsync(userId, messageId, ct);
        await EnsureQuotaAsync(userId, u => (u.QuizzesUsed, u.QuizzesLimit), ct);

        var generated = await GenerateAsync(token => ai.QuizAsync(input, token), "quiz", ct);
        var rng = new Random();
        var questions = generated.Questions
            .Select(q => ProfessorRules.QuizQuestion(q.Type, q.Prompt, q.Code, q.Language, q.Options, q.Correct, q.Explanation, rng))
            .OfType<ProfessorQuizQuestion>()
            .Take(ProfessorRules.QuizQuestions)
            .ToArray();
        if (questions.Length < ProfessorRules.QuizMinQuestions || string.IsNullOrWhiteSpace(generated.Title)) throw Failed("The AI Professor could not write the quiz. Try again.");

        var quiz = new ProfessorQuiz
        {
            UserId = userId,
            ChatId = source.ChatId,
            MessageId = source.Id,
            Topic = input.Topic,
            Title = generated.Title.Trim(),
            Questions = JsonSerializer.Serialize(questions, ProfessorRules.Json),
            CreatedAt = time.GetUtcNow(),
        };
        db.ProfessorQuizzes.Add(quiz);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return await MapAsync(await db.ProfessorQuizzes.AsNoTracking().FirstAsync(q => q.MessageId == messageId, ct), ct);
        }
        return await MapAsync(quiz, ct);
    }

    public async Task<ProfessorQuizDto> GetQuizAsync(Guid userId, Guid quizId, CancellationToken ct) =>
        await MapAsync(await db.ProfessorQuizzes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quizId && q.UserId == userId, ct) ?? throw QuizNotFound(), ct);

    // Graded from the stored key; the first answer to a question stays (a repeat returns it).
    public async Task<ProfessorQuizResultDto> AnswerAsync(Guid userId, Guid quizId, ProfessorQuizAnswerRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Row lock: two answers at once must not overwrite each other's entry in the answers column.
        await db.Database.SqlQuery<int>($"select 1 as \"Value\" from professor_quizzes where id = {quizId} for update").ToListAsync(ct);
        var quiz = await db.ProfessorQuizzes.FirstOrDefaultAsync(q => q.Id == quizId && q.UserId == userId, ct) ?? throw QuizNotFound();
        var questions = Questions(quiz);
        if (request.Index < 0 || request.Index >= questions.Length)
            throw new AppException(ErrorCodes.ProfessorWrongIndex, "This quiz has no such question.");

        var answers = Answers(quiz);
        var answer = answers.FirstOrDefault(a => a.Index == request.Index);
        if (answer is null)
        {
            var question = questions[request.Index];
            var payload = request.Payload.ValueKind == JsonValueKind.Undefined ? JsonSerializer.SerializeToElement(new { }) : request.Payload;
            answer = new ProfessorQuizAnswer(request.Index, payload, Grader.Grade(question.Type, Key(question), payload) >= 1);
            quiz.Answers = JsonSerializer.Serialize(answers.Append(answer).OrderBy(a => a.Index), ProfessorRules.Json);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Result(questions[request.Index], answer);
    }

    // Today's use against the Junior developer allowance (2 questions, 1 lesson, 1 quiz a day, Europe/Zagreb); a Senior
    // developer has no limit (docs/premium-pretplata.md). A failed answer saves nothing, so it costs nothing.
    public async Task<ProfessorUsageDto> UsageAsync(Guid userId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var since = Clock.StartOf(Clock.Today(now));
        var senior = await db.Users.Where(u => u.Id == userId).Select(u => u.PremiumUntil > now).FirstAsync(ct);
        var questions = await db.ProfessorMessages.CountAsync(
            m => m.Role == ProfessorRole.User && m.CreatedAt >= since && db.ProfessorChats.Any(c => c.Id == m.ChatId && c.UserId == userId), ct);
        var lessons = await db.ProfessorLessons.CountAsync(l => l.UserId == userId && l.CreatedAt >= since, ct);
        var quizzes = await db.ProfessorQuizzes.CountAsync(q => q.UserId == userId && q.CreatedAt >= since, ct);
        int? Limit(int junior) => senior ? null : junior;
        return new ProfessorUsageDto(
            questions, Limit(Premium.JuniorProfessorQuestionsPerDay), lessons, Limit(Premium.JuniorProfessorLessonsPerDay), quizzes, Limit(Premium.JuniorProfessorQuizzesPerDay));
    }

    private async Task EnsureQuotaAsync(Guid userId, Func<ProfessorUsageDto, (int Used, int? Limit)> of, CancellationToken ct)
    {
        var (used, limit) = of(await UsageAsync(userId, ct));
        if (used >= limit) throw new AppException(ErrorCodes.ProfessorQuota, "You have used today's AI Professor allowance. Senior developers have no limit.");
    }

    private async Task<ProfessorTurnDto> SaveTurnAsync(ProfessorTurn turn, string answer, string? topic, ProfessorScope? scope, CancellationToken ct)
    {
        // Both messages are stamped when the turn is saved, the answer a microsecond later (timestamps are
        // microseconds): a turn stays together even when two tabs ask in the same chat at once.
        var askedAt = time.GetUtcNow();
        var answeredAt = askedAt.AddMicroseconds(1);
        var chat = turn.Chat;
        if (chat is null)
        {
            chat = new ProfessorChat { UserId = turn.UserId, Title = ProfessorRules.Title(topic, turn.Question), CreatedAt = askedAt, UpdatedAt = answeredAt };
            db.ProfessorChats.Add(chat);
        }
        else chat.UpdatedAt = answeredAt;

        var question = new ProfessorMessage { ChatId = chat.Id, Role = ProfessorRole.User, Text = turn.Question, CreatedAt = askedAt };
        var reply = new ProfessorMessage { ChatId = chat.Id, Role = ProfessorRole.Professor, Text = answer, Topic = topic, Scope = scope, CreatedAt = answeredAt };
        db.ProfessorMessages.AddRange(question, reply);
        await db.SaveChangesAsync(ct);
        return new ProfessorTurnDto(new ProfessorChatSummaryDto(chat.Id, chat.Title, chat.UpdatedAt), Map(question, null, null), Map(reply, null, null));
    }

    // The professor answer a lesson or quiz is written from, with the question it answered.
    private async Task<(ProfessorMessage Source, StudyInput Input)> SourceAsync(Guid userId, Guid messageId, CancellationToken ct)
    {
        var source = await db.ProfessorMessages.AsNoTracking()
            .Where(m => m.Id == messageId && m.Role == ProfessorRole.Professor && db.ProfessorChats.Any(c => c.Id == m.ChatId && c.UserId == userId))
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException(ErrorCodes.ProfessorMessageNotFound, "This answer does not exist.", 404);
        if (source.Topic is not { } topic)
            throw new AppException(ErrorCodes.ProfessorNoTopic, "This answer is not about IT, so there is nothing to make a lesson or quiz from.");
        EnsureAvailable();

        var question = await db.ProfessorMessages.AsNoTracking()
            .Where(m => m.ChatId == source.ChatId && m.Role == ProfessorRole.User && m.CreatedAt < source.CreatedAt)
            .OrderByDescending(m => m.CreatedAt).Select(m => m.Text).FirstAsync(ct);
        var rating = await db.Users.Where(u => u.Id == userId).Select(u => u.Rating).FirstAsync(ct);
        return (source, new StudyInput(topic, source.Scope, question, source.Text, DeckBuilder.DifficultyOf(rating)));
    }

    private async Task<T> GenerateAsync<T>(Func<CancellationToken, Task<T>> call, string what, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProfessorRules.GenerationTimeout);
        try
        {
            return await call(timeout.Token);
        }
        catch (Exception e) when (e is not AppException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "The professor could not write the {What}", what);
            throw Failed($"The AI Professor could not write the {what}. Try again.");
        }
    }

    private async Task<ProfessorLessonDto> MapAsync(ProfessorLesson lesson, CancellationToken ct)
    {
        var quizId = await db.ProfessorQuizzes.Where(q => q.MessageId == lesson.MessageId).Select(q => (Guid?)q.Id).FirstOrDefaultAsync(ct);
        return new ProfessorLessonDto(lesson.Id, lesson.ChatId, lesson.MessageId, lesson.Topic, lesson.Title, lesson.Summary,
            JsonSerializer.Deserialize<ProfessorCard[]>(lesson.Cards, ProfessorRules.Json)!, quizId, lesson.CreatedAt);
    }

    private async Task<ProfessorQuizDto> MapAsync(ProfessorQuiz quiz, CancellationToken ct)
    {
        var lessonId = await db.ProfessorLessons.Where(l => l.MessageId == quiz.MessageId).Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct);
        var answers = Answers(quiz).ToDictionary(a => a.Index);
        var questions = Questions(quiz).Select((q, i) => new ProfessorQuizQuestionDto(i, q.Type, q.Language,
            QuestionJson.Public(q.Type, q.Content.GetRawText()), answers.TryGetValue(i, out var a) ? Result(q, a) : null));
        return new ProfessorQuizDto(quiz.Id, quiz.ChatId, quiz.MessageId, quiz.Topic, quiz.Title, [.. questions], lessonId, quiz.CreatedAt);
    }

    private static ProfessorMessageDto Map(ProfessorMessage m, Guid? lessonId, Guid? quizId) =>
        new(m.Id, m.Role, m.Text, m.Topic, m.Scope, lessonId, quizId, m.CreatedAt);

    private static ProfessorQuizResultDto Result(ProfessorQuizQuestion question, ProfessorQuizAnswer answer) =>
        new(answer.Index, answer.Payload, answer.IsCorrect, Key(question), question.Explanation);

    private static JsonElement Key(ProfessorQuizQuestion question) => question.Content.GetProperty("answer");

    private static ProfessorQuizQuestion[] Questions(ProfessorQuiz quiz) => JsonSerializer.Deserialize<ProfessorQuizQuestion[]>(quiz.Questions, ProfessorRules.Json)!;

    private static List<ProfessorQuizAnswer> Answers(ProfessorQuiz quiz) => JsonSerializer.Deserialize<List<ProfessorQuizAnswer>>(quiz.Answers, ProfessorRules.Json)!;

    private void EnsureAvailable()
    {
        if (!ai.Available) throw new AppException(ErrorCodes.ProfessorUnavailable, "The AI Professor is not available right now.", 503);
    }

    private static AppException Failed(string detail) => new(ErrorCodes.ProfessorFailed, detail, 503);

    private static AppException ChatNotFound() => new(ErrorCodes.ProfessorChatNotFound, "This chat does not exist.", 404);

    private static AppException QuizNotFound() => new(ErrorCodes.ProfessorQuizNotFound, "This quiz does not exist.", 404);
}
