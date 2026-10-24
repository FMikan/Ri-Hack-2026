using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Content;

public class ContentService(IAppDbContext db)
{
    // Only topics with enough questions to build a full deck are offered (the seeder sets is_active).
    public Task<List<TopicDto>> ListTopicsAsync(CancellationToken cancellationToken) =>
        db.Topics
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => new TopicDto(t.Id, t.Slug, t.Name, t.Description, t.Concepts.Count))
            .ToListAsync(cancellationToken);

    public async Task<List<ConceptDto>> ListConceptsAsync(string topicSlug, CancellationToken cancellationToken)
    {
        var topic = await db.Topics.Include(t => t.Concepts).FirstOrDefaultAsync(t => t.Slug == topicSlug, cancellationToken)
            ?? throw new AppException(ErrorCodes.TopicNotFound, $"Topic '{topicSlug}' does not exist.", 404);
        return topic.Concepts.OrderBy(c => c.SortOrder).Select(c => new ConceptDto(c.Id, c.Slug, c.Name, c.TopicId)).ToList();
    }
}
