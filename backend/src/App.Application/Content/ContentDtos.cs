namespace App.Application.Content;

public record TopicDto(Guid Id, string Slug, string Name, string Description, int ConceptCount);

public record ConceptDto(Guid Id, string Slug, string Name, Guid TopicId);
