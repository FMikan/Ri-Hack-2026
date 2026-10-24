namespace App.Domain.Content;

public class Topic
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Slug { get; set; } // 'sql', 'javascript', ...
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true; // false = not offered in matchmaking (e.g. pool too small)
    public List<Concept> Concepts { get; } = [];
}

public class Concept
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid TopicId { get; set; }
    public required string Slug { get; set; } // 'sql.n-plus-one'
    public required string Name { get; set; }
    public required string Description { get; set; } // 1–2 sentences; input for fallback lesson cards
    public int SortOrder { get; set; }
}
