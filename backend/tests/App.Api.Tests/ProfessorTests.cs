using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.Application.Professor;
using App.Domain;
using App.Domain.Professor;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Tests;

// The AI Professor (docs §6.12): a streamed chat about IT, and a lesson and a practice quiz written from one answer.
public class ProfessorTests(TestApp app) : ApiTestBase(app)
{
    private record SseEvent(string Type, JsonElement Data);

    // With the browser client's Accept header: it must still let Problem Details through for errors before the stream.
    private async Task<HttpResponseMessage> PostMessageAsync(Session session, Guid? chatId, string text) =>
        await session.Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/professor/messages")
        {
            Content = JsonContent.Create(new { chatId, text }, options: App.Json),
            Headers = { { "Accept", "text/event-stream, application/problem+json" } },
        });

    private async Task<List<SseEvent>> StreamAsync(Session session, Guid? chatId, string text)
    {
        var response = await PostMessageAsync(session, chatId, text);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        return [.. body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(block =>
        {
            var lines = block.Split('\n');
            var type = lines.Single(l => l.StartsWith("event:"))["event:".Length..].Trim();
            var data = string.Join('\n', lines.Where(l => l.StartsWith("data:")).Select(l => l["data:".Length..].TrimStart()));
            return new SseEvent(type, JsonDocument.Parse(data).RootElement.Clone());
        })];
    }

    private async Task<ProfessorTurnDto> AskAsync(Session session, Guid? chatId = null, string text = "How does DNS work?")
    {
        var events = await StreamAsync(session, chatId, text);
        Assert.Equal("done", events[^1].Type);
        return events[^1].Data.Deserialize<ProfessorTurnDto>(App.Json)!;
    }

    private async Task<T> ReadOkAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync<T>(response))!;
    }

    private Task<HttpResponseMessage> CreateAsync(Session session, string kind, Guid messageId) =>
        PostAsync(session.Client, $"/api/professor/{kind}", new { messageId });

    // A Senior developer (docs/premium-pretplata.md): no daily allowance, so a test can ask as often as it needs.
    private async Task<Session> SeniorAsync(string name)
    {
        var session = await RegisterAsync(name);
        var until = App.Clock.GetUtcNow().AddDays(30);
        await App.InDbAsync(db => db.Users.Where(u => u.Id == session.Auth.User.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.PremiumUntil, until)));
        return session;
    }

    private async Task<string> ProblemTypeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString()!;

    [Fact]
    public async Task Answer_streams_without_its_header_and_the_turn_is_saved()
    {
        var ana = await RegisterAsync("ana");

        var events = await StreamAsync(ana, null, "  How does DNS work?  ");

        Assert.All(events[..^1], e => Assert.Equal("delta", e.Type));
        Assert.Equal("DNS maps names to IPs.", string.Concat(events[..^1].Select(e => e.Data.GetProperty("text").GetString())));
        var done = events[^1].Data;
        Assert.Equal("done", events[^1].Type);
        Assert.Equal("professor", done.GetProperty("answer").GetProperty("role").GetString());
        Assert.Equal("specific", done.GetProperty("answer").GetProperty("scope").GetString());
        var turn = done.Deserialize<ProfessorTurnDto>(App.Json)!;
        Assert.Equal("DNS", turn.Chat.Title);
        Assert.Equal("How does DNS work?", turn.Question.Text);
        Assert.Equal(("DNS maps names to IPs.", "DNS", ProfessorScope.Specific), (turn.Answer.Text, turn.Answer.Topic, turn.Answer.Scope));

        var chat = await ana.Client.GetFromJsonAsync<ProfessorChatDto>($"/api/professor/chats/{turn.Chat.Id}", App.Json);
        Assert.Equal([ProfessorRole.User, ProfessorRole.Professor], chat!.Messages.Select(m => m.Role));
        Assert.Equal([turn.Question.Id, turn.Answer.Id], chat.Messages.Select(m => m.Id));
        var list = await ana.Client.GetFromJsonAsync<ProfessorChatSummaryDto[]>("/api/professor/chats", App.Json);
        Assert.Equal([turn.Chat.Id], list!.Select(c => c.Id));
        Assert.Equal([new ProfessorTurnInput(true, "How does DNS work?")], App.Ai.ChatCalls.Single());
    }

    [Fact]
    public async Task Next_message_sends_the_history_with_the_headers_and_moves_the_chat_up()
    {
        var ana = await SeniorAsync("ana");
        var first = await AskAsync(ana);
        App.Clock.Advance(TimeSpan.FromMinutes(1));
        var other = await AskAsync(ana, text: "What is TCP?");
        App.Clock.Advance(TimeSpan.FromMinutes(1));
        App.Ai.ChatChunks = ["TOPIC: general | DNS records\n\nThere are A, AAAA and CNAME records."];

        var second = await AskAsync(ana, first.Chat.Id, "And records?");

        Assert.Equal(first.Chat.Id, second.Chat.Id);
        Assert.Equal("DNS", second.Chat.Title); // the title comes from the first answer
        Assert.Equal(
            [new ProfessorTurnInput(true, "How does DNS work?"), new ProfessorTurnInput(false, "TOPIC: specific | DNS\n\nDNS maps names to IPs."), new ProfessorTurnInput(true, "And records?")],
            App.Ai.ChatCalls[^1]);
        var chat = await ana.Client.GetFromJsonAsync<ProfessorChatDto>($"/api/professor/chats/{first.Chat.Id}", App.Json);
        Assert.Equal(["How does DNS work?", "DNS maps names to IPs.", "And records?", "There are A, AAAA and CNAME records."], chat!.Messages.Select(m => m.Text));
        Assert.Equal(ProfessorScope.General, chat.Messages[^1].Scope);
        var list = await ana.Client.GetFromJsonAsync<ProfessorChatSummaryDto[]>("/api/professor/chats", App.Json);
        Assert.Equal([first.Chat.Id, other.Chat.Id], list!.Select(c => c.Id));
    }

    [Fact]
    public async Task Answer_without_a_header_is_kept_whole_and_offers_nothing()
    {
        var ana = await RegisterAsync("ana");
        App.Ai.ChatChunks = ["Sure, here is how it works.\n", "Line two."];

        var turn = await AskAsync(ana, text: "Tell me about recursion and why it matters");

        Assert.Equal("Sure, here is how it works.\nLine two.", turn.Answer.Text);
        Assert.Null(turn.Answer.Topic);
        Assert.Equal("Tell me about recursion and why it matters", turn.Chat.Title);
    }

    [Fact]
    public async Task Long_title_is_cut_without_splitting_an_emoji()
    {
        var ana = await RegisterAsync("ana");
        App.Ai.ChatChunks = ["No header, just an answer."];

        var turn = await AskAsync(ana, text: new string('x', ProfessorRules.TitleMaxLength - 2) + "😀 and more");

        Assert.Equal(new string('x', ProfessorRules.TitleMaxLength - 2) + "…", turn.Chat.Title);
    }

    [Fact]
    public async Task Off_topic_answer_has_no_topic_so_no_lesson_or_quiz()
    {
        var ana = await RegisterAsync("ana");
        App.Ai.ChatChunks = ["TOPIC: none\n\nI teach IT and computer science."];
        var turn = await AskAsync(ana, text: "Best pizza in Rijeka?");

        Assert.Null(turn.Answer.Topic);
        foreach (var kind in new[] { "lessons", "quizzes" })
        {
            var response = await CreateAsync(ana, kind, turn.Answer.Id);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(ErrorCodes.ProfessorNoTopic, await ProblemTypeAsync(response));
        }
        Assert.Empty(App.Ai.StudyCalls);
    }

    [Fact]
    public async Task Failed_answer_ends_with_an_error_and_saves_nothing()
    {
        var ana = await RegisterAsync("ana");
        App.Ai.ChatFailure = new HttpRequestException("overloaded");

        var events = await StreamAsync(ana, null, "How does DNS work?");

        Assert.Equal("error", events[^1].Type);
        Assert.Equal(ErrorCodes.ProfessorFailed, events[^1].Data.GetProperty("type").GetString());
        Assert.Equal(503, events[^1].Data.GetProperty("status").GetInt32());
        Assert.Empty((await ana.Client.GetFromJsonAsync<ProfessorChatSummaryDto[]>("/api/professor/chats", App.Json))!);
    }

    [Fact]
    public async Task Declined_answer_says_so()
    {
        var ana = await RegisterAsync("ana");
        App.Ai.ChatChunks = [];
        App.Ai.ChatFailure = new AppException(ErrorCodes.ProfessorDeclined, "The AI Professor cannot help with that one.");

        var events = await StreamAsync(ana, null, "Something odd");

        Assert.Equal(["error"], events.Select(e => e.Type));
        Assert.Equal(ErrorCodes.ProfessorDeclined, events[0].Data.GetProperty("type").GetString());
        Assert.Equal("The AI Professor cannot help with that one.", events[0].Data.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Empty_answer_is_an_error()
    {
        var ana = await RegisterAsync("ana");
        App.Ai.ChatChunks = ["TOPIC: specific | DNS\n\n  "];

        var events = await StreamAsync(ana, null, "How does DNS work?");

        Assert.Equal("error", events[^1].Type);
        Assert.Equal(ErrorCodes.ProfessorFailed, events[^1].Data.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Without_the_ai_the_professor_is_503_before_any_stream()
    {
        var ana = await RegisterAsync("ana");
        var turn = await AskAsync(ana);
        App.Ai.Available = false;

        var message = await PostMessageAsync(ana, null, "How does DNS work?");
        var lesson = await CreateAsync(ana, "lessons", turn.Answer.Id);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, message.StatusCode);
        Assert.Equal(ErrorCodes.ProfessorUnavailable, await ProblemTypeAsync(message));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, lesson.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ana.Client.GetAsync($"/api/professor/chats/{turn.Chat.Id}")).StatusCode);
    }

    [Fact]
    public async Task Blank_or_too_long_message_is_400()
    {
        var ana = await RegisterAsync("ana");

        Assert.Equal(HttpStatusCode.BadRequest, (await PostMessageAsync(ana, null, "   ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostMessageAsync(ana, null, new string('x', ProfessorRules.MessageMaxLength + 1))).StatusCode);
        Assert.Empty(App.Ai.ChatCalls);
    }

    [Fact]
    public async Task Another_players_chat_lesson_and_quiz_are_404()
    {
        var ana = await RegisterAsync("ana");
        var ben = await RegisterAsync("ben");
        var turn = await AskAsync(ana);
        var lesson = await ReadOkAsync<ProfessorLessonDto>(await CreateAsync(ana, "lessons", turn.Answer.Id));
        var quiz = await ReadOkAsync<ProfessorQuizDto>(await CreateAsync(ana, "quizzes", turn.Answer.Id));

        Assert.Equal(HttpStatusCode.NotFound, (await ben.Client.GetAsync($"/api/professor/chats/{turn.Chat.Id}")).StatusCode);
        var foreign = await PostMessageAsync(ben, turn.Chat.Id, "Hi");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(ErrorCodes.ProfessorChatNotFound, await ProblemTypeAsync(foreign));
        Assert.Equal(HttpStatusCode.NotFound, (await CreateAsync(ben, "lessons", turn.Answer.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CreateAsync(ben, "quizzes", turn.Answer.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ben.Client.GetAsync($"/api/professor/lessons/{lesson.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ben.Client.GetAsync($"/api/professor/quizzes/{quiz.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(ben.Client, $"/api/professor/quizzes/{quiz.Id}/answers", new { index = 0, payload = new { optionId = "a" } })).StatusCode);
        Assert.Empty((await ben.Client.GetFromJsonAsync<ProfessorChatSummaryDto[]>("/api/professor/chats", App.Json))!);
        Assert.Equal(HttpStatusCode.NotFound, (await CreateAsync(ana, "lessons", turn.Question.Id)).StatusCode); // a question, not an answer
    }

    [Fact]
    public async Task Lesson_is_written_once_per_answer_from_the_question_and_answer()
    {
        var ana = await RegisterAsync("ana");
        var turn = await AskAsync(ana);

        var lesson = await ReadOkAsync<ProfessorLessonDto>(await CreateAsync(ana, "lessons", turn.Answer.Id));
        var again = await ReadOkAsync<ProfessorLessonDto>(await CreateAsync(ana, "lessons", turn.Answer.Id));

        Assert.Equal(lesson.Id, again.Id);
        Assert.Equal(new StudyInput("DNS", ProfessorScope.Specific, "How does DNS work?", "DNS maps names to IPs.", 1), App.Ai.StudyCalls.Single()); // a new player: band 1
        Assert.Equal(("DNS basics", "How names become addresses.", "DNS", turn.Chat.Id), (lesson.Title, lesson.Summary, lesson.Topic, lesson.ChatId));
        Assert.Equal(
            [new ProfessorCard("Resolvers", "A resolver asks servers in turn.", "dig example.com", "bash", "DNS is not one server."), new ProfessorCard("Caching", "Answers are cached for their TTL.", null, null, null)],
            lesson.Cards);
        Assert.Null(lesson.QuizId);
        Assert.Equal(lesson.Id, (await ana.Client.GetFromJsonAsync<ProfessorLessonDto>($"/api/professor/lessons/{lesson.Id}", App.Json))!.Id);
        var chat = await ana.Client.GetFromJsonAsync<ProfessorChatDto>($"/api/professor/chats/{turn.Chat.Id}", App.Json);
        Assert.Equal([null, lesson.Id], chat!.Messages.Select(m => m.LessonId));
    }

    [Fact]
    public async Task Lesson_with_too_few_cards_or_a_failed_call_is_503_and_can_be_retried()
    {
        var ana = await RegisterAsync("ana");
        var turn = await AskAsync(ana);
        App.Ai.StudyFailure = new TimeoutException();

        var failed = await CreateAsync(ana, "lessons", turn.Answer.Id);
        App.Ai.StudyFailure = null;
        App.Ai.Lesson = App.Ai.Lesson with { Cards = App.Ai.Lesson.Cards[..1] };
        var thin = await CreateAsync(ana, "lessons", turn.Answer.Id);
        App.Ai.Reset();
        var retried = await CreateAsync(ana, "lessons", turn.Answer.Id);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal(ErrorCodes.ProfessorFailed, await ProblemTypeAsync(failed));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, thin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    [Fact]
    public async Task Quiz_hides_the_keys_and_grades_each_question_once()
    {
        var ana = await RegisterAsync("ana");
        var turn = await AskAsync(ana);
        var quiz = await ReadOkAsync<ProfessorQuizDto>(await CreateAsync(ana, "quizzes", turn.Answer.Id));
        var keys = await App.InDbAsync(async db => JsonSerializer.Deserialize<ProfessorQuizQuestion[]>(
            (await db.ProfessorQuizzes.SingleAsync()).Questions, ProfessorRules.Json)!.Select(q => q.Content.GetProperty("answer").Clone()).ToArray());

        Assert.Equal(("DNS check", 5), (quiz.Title, quiz.Questions.Length));
        Assert.All(quiz.Questions, q => Assert.False(q.Content.ContainsKey("answer")));
        Assert.All(quiz.Questions, q => Assert.Null(q.Result));
        Assert.Equal("multiple_choice", quiz.Questions[0].Content["type"]!.GetValue<string>());
        Assert.Equal("true_false", quiz.Questions[3].Content["type"]!.GetValue<string>());
        Assert.Equal("Q2 right", quiz.Questions[1].Content["options"]!.AsArray()
            .Single(o => o!["id"]!.GetValue<string>() == keys[1].GetProperty("optionId").GetString())!["text"]!.GetValue<string>());

        var right = await ReadOkAsync<ProfessorQuizResultDto>(await PostAsync(ana.Client, $"/api/professor/quizzes/{quiz.Id}/answers", new { index = 1, payload = keys[1] }));
        var repeat = await ReadOkAsync<ProfessorQuizResultDto>(await PostAsync(ana.Client, $"/api/professor/quizzes/{quiz.Id}/answers", new { index = 1, payload = new { optionId = "zz" } }));
        var wrong = await ReadOkAsync<ProfessorQuizResultDto>(await PostAsync(ana.Client, $"/api/professor/quizzes/{quiz.Id}/answers", new { index = 4, payload = new { value = true } }));
        var outside = await PostAsync(ana.Client, $"/api/professor/quizzes/{quiz.Id}/answers", new { index = 5, payload = new { value = true } });

        Assert.True(right.IsCorrect);
        Assert.Equal("Why Q2", right.Explanation);
        Assert.True(JsonElement.DeepEquals(keys[1], right.Answer));
        Assert.True(repeat.IsCorrect); // the first answer stays
        Assert.False(wrong.IsCorrect);
        Assert.False(wrong.Answer.GetProperty("value").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, outside.StatusCode);
        Assert.Equal(ErrorCodes.ProfessorWrongIndex, await ProblemTypeAsync(outside));
        var reloaded = await ana.Client.GetFromJsonAsync<ProfessorQuizDto>($"/api/professor/quizzes/{quiz.Id}", App.Json);
        Assert.Equal([null, true, null, null, false], reloaded!.Questions.Select(q => q.Result?.IsCorrect));
    }

    [Fact]
    public async Task Quiz_and_lesson_from_one_answer_link_to_each_other()
    {
        var ana = await RegisterAsync("ana");
        var turn = await AskAsync(ana);

        var quiz = await ReadOkAsync<ProfessorQuizDto>(await CreateAsync(ana, "quizzes", turn.Answer.Id));
        var lesson = await ReadOkAsync<ProfessorLessonDto>(await CreateAsync(ana, "lessons", turn.Answer.Id));
        var again = await ReadOkAsync<ProfessorQuizDto>(await CreateAsync(ana, "quizzes", turn.Answer.Id));

        Assert.Equal(quiz.Id, again.Id);
        Assert.Equal(lesson.Id, again.LessonId);
        Assert.Equal(quiz.Id, lesson.QuizId);
        Assert.Equal(2, App.Ai.StudyCalls.Count);
        var chat = await ana.Client.GetFromJsonAsync<ProfessorChatDto>($"/api/professor/chats/{turn.Chat.Id}", App.Json);
        Assert.Equal((lesson.Id, quiz.Id), (chat!.Messages[1].LessonId, chat.Messages[1].QuizId));
    }

    [Fact]
    public async Task Malformed_quiz_questions_are_dropped_and_too_few_is_503()
    {
        var ana = await SeniorAsync("ana");
        var turn = await AskAsync(ana);
        App.Clock.Advance(TimeSpan.FromMinutes(1));
        var other = await AskAsync(ana, turn.Chat.Id, "More?");
        var broken = FakeAi.Choice("Bad", "e");
        App.Ai.Quiz = new GeneratedQuiz("DNS check", [FakeAi.Choice("Q1"), broken, FakeAi.Choice("Q3"), FakeAi.Statement("S", true), broken with { Options = ["x", "x", "y", "z"], Correct = "a" }, FakeAi.Statement("T", false)]);

        var four = await ReadOkAsync<ProfessorQuizDto>(await CreateAsync(ana, "quizzes", turn.Answer.Id));
        App.Ai.Quiz = new GeneratedQuiz("DNS check", [FakeAi.Choice("Q1"), broken, FakeAi.Statement("S", true)]);
        var tooFew = await CreateAsync(ana, "quizzes", other.Answer.Id);

        Assert.Equal(4, four.Questions.Length);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, tooFew.StatusCode);
        Assert.Equal(1, await App.InDbAsync(db => db.ProfessorQuizzes.CountAsync()));
    }
}
