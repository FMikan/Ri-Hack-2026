using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using App.Application.Lessons;
using App.Application.Matches;
using App.Domain;
using App.Domain.Lessons;
using App.Domain.Progress;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Tests;

// F2: the lesson after a match (idea §10): cards, three check questions, patching and the review queue.
public class LessonTests(TestApp app) : MatchTestBase(app)
{
    private async Task<(Session Session, MatchStateDto Match)> MatchWithMissesAsync(Func<int, bool>? right = null)
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        return (ana, await FinishedBotMatchAsync(ana, topic.Id, right));
    }

    private async Task<HttpResponseMessage> StartLessonAsync(Session session, Guid matchId) =>
        await PostAsync(session.Client, "/api/lessons", new StartLessonRequest { MatchId = matchId });

    private async Task<LessonDto> LessonAsync(Session session, Guid matchId)
    {
        var response = await StartLessonAsync(session, matchId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync<LessonDto>(response))!;
    }

    private Task<HttpResponseMessage> CheckAsync(Session session, LessonDto lesson, params bool[] right) =>
        PostAsync(session.Client, $"/api/lessons/{lesson.Id}/check", new
        {
            answers = lesson.Check.Select((c, i) => new
            {
                questionId = c.Question.Id,
                payload = right[i] ? QuestionFixtures.RightAnswer(c.Question.Type) : QuestionFixtures.WrongAnswer,
            }),
        });

    private async Task<LessonResultDto> CheckedAsync(Session session, LessonDto lesson, params bool[] right)
    {
        var response = await CheckAsync(session, lesson, right);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync<LessonResultDto>(response))!;
    }

    private Task<LessonDto> GetLessonAsync(Session session, Guid lessonId) =>
        session.Client.GetFromJsonAsync<LessonDto>($"/api/lessons/{lessonId}", App.Json)!;

    [Fact]
    public async Task Start_lesson_builds_cards_from_missed_and_chronic_concepts()
    {
        var (ana, match) = await MatchWithMissesAsync();
        var chronic = await ChronicGapAsync(ana, match);

        var lesson = await LessonAsync(ana, match.Id);

        Assert.Equal(LessonStatus.Generated, lesson.Status);
        Assert.Equal(4, lesson.Concepts.Length); // the three costliest misses and one chronic gap
        Assert.Equal(chronic, lesson.Concepts[^1].Id);
        Assert.Equal(lesson.Concepts.Select(c => c.Id), lesson.Cards.Select(c => c.ConceptId));
        Assert.All(lesson.Cards, c => Assert.StartsWith("AI card: ", c.Title));
        Assert.Equal(3, lesson.Check.Length);
        Assert.Equal([true, false, false], lesson.Check.Select(c => c.IsVariantOfMissed));
        Assert.All(lesson.Check, c => Assert.False(c.Question.Content.ContainsKey("answer")));
        Assert.True(lesson.RewardEligible);
        Assert.Equal(1, lesson.Attempt);
        Assert.Equal(lesson.Id, (await StateAsync(ana, match.Id)).Result!.LessonId);
    }

    [Fact]
    public async Task Lesson_with_no_missed_question_is_409()
    {
        var (ana, match) = await MatchWithMissesAsync(right: _ => true);

        var response = await StartLessonAsync(ana, match.Id);

        Assert.False(match.Result!.LessonAvailable);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.LessonNothingMissed, (await ReadAsync<ProblemDetails>(response))!.Type);
    }

    [Fact]
    public async Task Lesson_check_has_variant_of_missed_question_first()
    {
        var (ana, match) = await MatchWithMissesAsync();
        Assert.True(match.Result!.LessonAvailable);
        Assert.True(match.Result.LessonRewardEligible);
        var blunder = match.Result.Cards.Blunder!;

        var variant = (await LessonAsync(ana, match.Id)).Check[0].Question;

        Assert.NotEqual(blunder.Question.Id, variant.Id);
        Assert.Equal(blunder.Concept.Id, await App.InDbAsync(db => db.Questions.Where(q => q.Id == variant.Id).Select(q => q.ConceptId).FirstAsync()));
        Assert.Equal(blunder.Question.Difficulty, variant.Difficulty);
        Assert.NotEqual(blunder.Question.Type, variant.Type);
    }

    [Fact]
    public async Task Lesson_pass_patches_concepts_and_sets_review_due_tomorrow()
    {
        var (ana, match) = await MatchWithMissesAsync();
        var lesson = await LessonAsync(ana, match.Id);

        var result = await CheckedAsync(ana, lesson, true, false, true);

        Assert.True(result.Passed);
        Assert.Equal(lesson.Concepts.Select(c => c.Id).Order(), result.PatchedConcepts.Select(c => c.Id).Order());
        Assert.Equal([true, false, true], result.Results.Select(r => r.IsCorrect));
        Assert.All(result.Results, r => Assert.NotNull(r.Key.Answer));
        Assert.Null(result.RetryAfter);
        var tomorrow = App.Clock.GetUtcNow().AddDays(1);
        var (mastery, checks, stored) = await App.InDbAsync(async db => (
            await db.ConceptMastery.Where(m => m.UserId == ana.Auth.User.Id && lesson.Concepts.Select(c => c.Id).Contains(m.ConceptId)).ToListAsync(),
            await db.Answers.Where(a => a.LessonId == lesson.Id).ToListAsync(),
            await db.Lessons.FirstAsync(l => l.Id == lesson.Id)));
        Assert.Equal(lesson.Concepts.Length, mastery.Count);
        Assert.All(mastery, m => Assert.Equal((1, tomorrow), (m.ReviewStep!.Value, m.ReviewDueAt!.Value)));
        Assert.Equal(3, checks.Count);
        Assert.All(checks, a => Assert.Equal(AnswerSource.LessonCheck, a.Source));
        Assert.Equal(LessonStatus.Passed, stored.Status);
        Assert.True(stored.Rewarded);

        var again = await CheckAsync(ana, lesson, true, true, true);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(ErrorCodes.LessonAlreadyPassed, (await ReadAsync<ProblemDetails>(again))!.Type);
    }

    [Fact]
    public async Task Lesson_fail_allows_retry_after_10_minutes_without_reward_same_day()
    {
        var (ana, match) = await MatchWithMissesAsync();
        var lesson = await LessonAsync(ana, match.Id);

        var failed = await CheckedAsync(ana, lesson, false, true, true); // the variant is the gap: wrong means failed
        var tooSoon = await CheckAsync(ana, lesson, true, true, true);
        App.Clock.Advance(TimeSpan.FromMinutes(10));
        var retry = await GetLessonAsync(ana, lesson.Id);

        Assert.False(failed.Passed);
        Assert.Empty(failed.PatchedConcepts);
        Assert.Equal(App.Clock.GetUtcNow(), failed.RetryAfter);
        Assert.Equal(HttpStatusCode.Conflict, tooSoon.StatusCode);
        Assert.Equal(ErrorCodes.LessonRetryTooSoon, (await ReadAsync<ProblemDetails>(tooSoon))!.Type);
        Assert.Equal(LessonStatus.Failed, retry.Status);
        Assert.Equal(2, retry.Attempt);
        Assert.False(retry.RewardEligible);
        Assert.Equal(3, retry.LastResults!.Length);
        Assert.Empty(retry.Check.Select(c => c.Question.Id).Intersect(lesson.Check.Select(c => c.Question.Id))); // fresh questions

        Assert.True((await CheckedAsync(ana, retry, true, true, false)).Passed);
        Assert.False(await App.InDbAsync(db => db.Lessons.Where(l => l.Id == lesson.Id).Select(l => l.Rewarded).FirstAsync()));
    }

    [Fact]
    public async Task Retry_on_a_later_day_is_reward_eligible()
    {
        var (ana, match) = await MatchWithMissesAsync();
        var lesson = await LessonAsync(ana, match.Id);
        await CheckedAsync(ana, lesson, false, false, false);

        App.Clock.Advance(TimeSpan.FromDays(1));
        ana = await LoginAgainAsync(ana);
        var retry = await GetLessonAsync(ana, lesson.Id);

        Assert.True(retry.RewardEligible);
        Assert.True((await CheckedAsync(ana, retry, true, true, true)).Passed);
        Assert.True(await App.InDbAsync(db => db.Lessons.Where(l => l.Id == lesson.Id).Select(l => l.Rewarded).FirstAsync()));
    }

    [Fact]
    public async Task Second_lesson_for_same_match_is_not_reward_eligible_and_reuses_cached_cards()
    {
        var (ana, match) = await MatchWithMissesAsync();

        var first = await LessonAsync(ana, match.Id);
        var calls = App.Ai.CardCalls;
        var second = await LessonAsync(ana, match.Id);

        Assert.True(first.RewardEligible);
        Assert.False(second.RewardEligible);
        Assert.False((await StateAsync(ana, match.Id)).Result!.LessonRewardEligible);
        Assert.Equal(first.Concepts.Length, calls);
        Assert.Equal(calls, App.Ai.CardCalls); // cards are cached per concept and band
        Assert.Equal(first.Cards, second.Cards);
        Assert.Equal(LessonCardsSource.Cache, await App.InDbAsync(db => db.Lessons.Where(l => l.Id == second.Id).Select(l => l.CardsSource).FirstAsync()));
    }

    [Fact]
    public async Task Lesson_cards_fall_back_to_the_concept_description_when_the_generator_times_out()
    {
        var (ana, match) = await MatchWithMissesAsync();
        App.Ai.Delay = TimeSpan.FromSeconds(5);

        var lesson = await LessonAsync(ana, match.Id);

        Assert.All(lesson.Cards, c => Assert.Equal("D", c.Why));
        Assert.Equal(lesson.Concepts.Select(c => c.Name), lesson.Cards.Select(c => c.Title));
        Assert.Equal(0, await App.InDbAsync(db => db.ConceptLessonCards.CountAsync()));
        Assert.Equal(LessonCardsSource.Fallback, await App.InDbAsync(db => db.Lessons.Where(l => l.Id == lesson.Id).Select(l => l.CardsSource).FirstAsync()));
    }

    [Fact]
    public async Task Someone_elses_lesson_is_404()
    {
        var (ana, match) = await MatchWithMissesAsync();
        var lesson = await LessonAsync(ana, match.Id);
        var bob = await RegisterAsync("bob");

        Assert.Equal(HttpStatusCode.NotFound, (await bob.Client.GetAsync($"/api/lessons/{lesson.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await StartLessonAsync(bob, match.Id)).StatusCode);
    }

    // A weak concept of the match topic that was not in the deck: the lesson adds it as a chronic gap.
    private Task<Guid> ChronicGapAsync(Session session, MatchStateDto match) => App.InDbAsync(async db =>
    {
        var inDeck = await db.MatchQuestions.Where(q => q.MatchId == match.Id).Select(q => q.ConceptId).ToListAsync();
        var concept = await db.Concepts.Where(c => c.TopicId == match.Topic!.Id && !inDeck.Contains(c.Id)).OrderBy(c => c.SortOrder).FirstAsync();
        db.ConceptMastery.Add(new ConceptMastery { UserId = session.Auth.User.Id, ConceptId = concept.Id, TopicId = concept.TopicId, Mastery = 20, AnswerCount = 4 });
        await db.SaveChangesAsync();
        return concept.Id;
    });
}
