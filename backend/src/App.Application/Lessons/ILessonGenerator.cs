namespace App.Application.Lessons;

public record LessonCardInput(string Topic, string Concept, string ConceptDescription, int Band);

public record GeneratedCard(string Title, string Why, string? Code, string? Language, string Misconception);

// One lesson card for a concept at a difficulty band (idea §10, §16), cached per (concept, band).
public interface ILessonGenerator
{
    Task<GeneratedCard> CardAsync(LessonCardInput input, CancellationToken cancellationToken);
}
