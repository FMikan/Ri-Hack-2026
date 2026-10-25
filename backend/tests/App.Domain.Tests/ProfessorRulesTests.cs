using System.Text.Json;
using App.Domain.Content;
using App.Domain.Professor;

namespace App.Domain.Tests;

public class ProfessorRulesTests
{
    [Theory]
    [InlineData("TOPIC: specific | JWT expiry", "JWT expiry", ProfessorScope.Specific)]
    [InlineData("TOPIC: general | Docker networking", "Docker networking", ProfessorScope.General)]
    [InlineData("**TOPIC:** General | `SQL joins`.", "SQL joins", ProfessorScope.General)]
    [InlineData("topic: HTTP caching", "HTTP caching", ProfessorScope.Specific)]
    public void Header_gives_the_topic_and_scope(string line, string topic, ProfessorScope scope)
    {
        Assert.True(ProfessorRules.TryParseHeader(line, out var parsedTopic, out var parsedScope));
        Assert.Equal(topic, parsedTopic);
        Assert.Equal(scope, parsedScope);
    }

    [Theory]
    [InlineData("TOPIC: none")]
    [InlineData("TOPIC: specific | none")]
    [InlineData("TOPIC:")]
    public void Header_without_a_topic_offers_nothing(string line)
    {
        Assert.True(ProfessorRules.TryParseHeader(line, out var topic, out var scope));
        Assert.Null(topic);
        Assert.Null(scope);
    }

    [Fact]
    public void A_line_without_the_prefix_is_not_a_header() =>
        Assert.False(ProfessorRules.TryParseHeader("JWTs expire because...", out _, out _));

    [Fact]
    public void Header_round_trips()
    {
        Assert.True(ProfessorRules.TryParseHeader(ProfessorRules.Header("CORS", ProfessorScope.General), out var topic, out var scope));
        Assert.Equal(("CORS", ProfessorScope.General), (topic, scope));
        Assert.Equal("TOPIC: none", ProfessorRules.Header(null, null));
    }

    [Fact]
    public void Reader_hides_the_header_split_across_chunks_and_the_blank_line_after_it()
    {
        var reader = new ProfessorHeaderReader();

        var shown = string.Concat(new[] { "TOP", "IC: specific | DNS", "\n", "\nDNS turns ", "names into IPs.\n\nDone." }.Select(reader.Push)) + reader.Finish();

        Assert.Equal("DNS turns names into IPs.\n\nDone.", shown);
        Assert.Equal("DNS", reader.Topic);
        Assert.Equal(ProfessorScope.Specific, reader.Scope);
    }

    [Fact]
    public void Reader_keeps_an_answer_that_has_no_header()
    {
        var reader = new ProfessorHeaderReader();

        var shown = reader.Push("Sure! Here is how") + reader.Push(" it works.\nSecond line.") + reader.Finish();

        Assert.Equal("Sure! Here is how it works.\nSecond line.", shown);
        Assert.Null(reader.Topic);
    }

    [Fact]
    public void Reader_flushes_a_one_line_answer_at_the_end()
    {
        var reader = new ProfessorHeaderReader();

        Assert.Equal("", reader.Push("Hi there"));
        Assert.Equal("Hi there", reader.Finish());
    }

    [Fact]
    public void Title_prefers_the_topic_and_is_capped()
    {
        Assert.Equal("Docker networking", ProfessorRules.Title("Docker networking", "how do containers talk?"));
        var title = ProfessorRules.Title(null, new string('x', 100));
        Assert.Equal(ProfessorRules.TitleMaxLength, title.Length);
        Assert.EndsWith("…", title);
    }

    [Fact]
    public void Title_never_splits_an_emoji()
    {
        var title = ProfessorRules.Title(null, new string('x', ProfessorRules.TitleMaxLength - 2) + "😀 tail");

        Assert.Equal(new string('x', ProfessorRules.TitleMaxLength - 2) + "…", title);
        Assert.DoesNotContain(title, char.IsSurrogate);
    }

    [Fact]
    public void Multiple_choice_is_shuffled_and_the_key_follows_the_correct_text()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var question = ProfessorRules.QuizQuestion("multiple_choice", " What does DNS map? ", "```\ndig example.com\n```", "Bash",
                ["Names to IPs", "IPs to MACs", "Ports to apps", "Users to groups"], "A", "Because.", new Random(seed))!;

            Assert.Equal(QuestionType.MultipleChoice, question.Type);
            Assert.Equal("bash", question.Language);
            var content = question.Content;
            Assert.Equal("What does DNS map?", content.GetProperty("prompt").GetString());
            Assert.Equal("dig example.com", content.GetProperty("code").GetString());
            var options = content.GetProperty("options").EnumerateArray().ToList();
            Assert.Equal(["a", "b", "c", "d"], options.Select(o => o.GetProperty("id").GetString()));
            var key = content.GetProperty("answer").GetProperty("optionId").GetString();
            Assert.Equal("Names to IPs", options.Single(o => o.GetProperty("id").GetString() == key).GetProperty("text").GetString());
            Assert.Equal(1, Grader.Grade(question.Type, content.GetProperty("answer"), JsonSerializer.SerializeToElement(new { optionId = key })));
        }
    }

    [Fact]
    public void True_false_keeps_the_statement_and_key()
    {
        var question = ProfessorRules.QuizQuestion("true_false", "UDP guarantees delivery.", "", "", [], "False", "It does not.", new Random(1))!;

        Assert.Equal(QuestionType.TrueFalse, question.Type);
        Assert.Equal("UDP guarantees delivery.", question.Content.GetProperty("statement").GetString());
        Assert.False(question.Content.GetProperty("answer").GetProperty("value").GetBoolean());
        Assert.Null(question.Language);
    }

    [Theory]
    [InlineData("multiple_choice", "Q", "e", "a,b,c,d")] // key outside a-d
    [InlineData("multiple_choice", "Q", "a", "a,b,c")] // three options
    [InlineData("multiple_choice", "Q", "a", "a,b,b,d")] // duplicate option
    [InlineData("multiple_choice", "Q", "a", "a,b,,d")] // empty option
    [InlineData("multiple_choice", " ", "a", "a,b,c,d")] // no prompt
    [InlineData("true_false", "Q", "maybe", "")]
    [InlineData("order", "Q", "a", "a,b,c,d")] // a type the professor does not write
    public void Malformed_questions_are_dropped(string type, string prompt, string correct, string options) =>
        Assert.Null(ProfessorRules.QuizQuestion(type, prompt, null, null,
            options.Length == 0 ? [] : options.Split(','), correct, "Why.", new Random(1)));

    [Fact]
    public void True_false_about_code_is_dropped() =>
        Assert.Null(ProfessorRules.QuizQuestion("true_false", "This prints 1.", "print(1)", "py", [], "true", "Why.", new Random(1)));

    [Fact]
    public void Question_without_explanation_is_dropped() =>
        Assert.Null(ProfessorRules.QuizQuestion("true_false", "Q", null, null, [], "true", " ", new Random(1)));
}
