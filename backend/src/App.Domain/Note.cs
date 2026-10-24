namespace App.Domain;

// Example entity that demonstrates the API -> Application -> Infrastructure -> PostgreSQL flow.
// Replace or delete it once real domain entities exist.
public class Note
{
    public const int MaxTextLength = 500;

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Text { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
