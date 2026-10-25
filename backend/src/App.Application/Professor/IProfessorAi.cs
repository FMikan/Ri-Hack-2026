using App.Domain.Professor;

namespace App.Application.Professor;

// One earlier chat message as the model sees it; professor answers carry their header line again.
public record ProfessorTurnInput(bool FromUser, string Text);

// What a lesson or quiz is written from: the answer it was asked under, and the player's band (1 beginner .. 4 senior).
public record StudyInput(string Topic, ProfessorScope? Scope, string Question, string Answer, int Band);

public record GeneratedStudyCard(string Title, string Body, string Code, string Language, string Misconception);

public record GeneratedLesson(string Title, string Summary, GeneratedStudyCard[] Cards);

public record GeneratedQuizQuestion(string Type, string Prompt, string Code, string Language, string[] Options, string Correct, string Explanation);

public record GeneratedQuiz(string Title, GeneratedQuizQuestion[] Questions);

// The AI Professor (docs §5): a streamed chat answer that starts with the header line (ProfessorRules.TryParseHeader),
// and the lesson and quiz written from one answer. Without ANTHROPIC_API_KEY, Available is false and the endpoints
// that need the AI answer 503; there is no fallback content for an open question.
public interface IProfessorAi
{
    bool Available { get; }

    IAsyncEnumerable<string> ChatAsync(IReadOnlyList<ProfessorTurnInput> history, CancellationToken cancellationToken);

    Task<GeneratedLesson> LessonAsync(StudyInput input, CancellationToken cancellationToken);

    Task<GeneratedQuiz> QuizAsync(StudyInput input, CancellationToken cancellationToken);
}
